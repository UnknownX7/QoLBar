using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Dynamic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Diagnostics;
using ImGuiNET;
using Dalamud.Plugin;
using QoLBar.Attributes;

#pragma warning disable IDE0060 // Remove unused parameter

// I'm too lazy to make a file just for this
[assembly: AssemblyTitle("QoLBar")]
[assembly: AssemblyVersion("2.0.2.0")]

// Disclaimer: I have no idea what I'm doing.
namespace QoLBar
{
    public class QoLBar : IDalamudPlugin
    {
        public string Name => "QoL Bar";

        public static DalamudPluginInterface Interface { get; private set; }
        private PluginCommandManager commandManager;
        public static Configuration Config { get; private set; }
        public static QoLBar Plugin { get; private set; }
        public PluginUI ui;
        private bool commandReady = true;
        private bool pluginReady = false;
        public readonly int maxCommandLength = 180; // 180 is the max per line for macros, 500 is the max you can actually type into the chat, however it is still possible to inject more
        private readonly Queue<string> commandQueue = new Queue<string>();

        public static TextureDictionary TextureDictionary => Config.UseHRIcons ? textureDictionaryHR : textureDictionaryLR;
        public static readonly TextureDictionary textureDictionaryLR = new TextureDictionary(false);
        public static readonly TextureDictionary textureDictionaryHR = new TextureDictionary(true);
        public const int FrameIconID = 114_000;
        private const int SafeIconID = 1_000_000;
        public int GetSafeIconID(byte i) => SafeIconID + i;

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);
        public static bool IsGameFocused
        {
            get
            {
                var activatedHandle = GetForegroundWindow();
                if (activatedHandle == IntPtr.Zero)
                    return false;

                var procId = Process.GetCurrentProcess().Id;
                GetWindowThreadProcessId(activatedHandle, out int activeProcId);

                return activeProcId == procId;
            }
        }

        public static IntPtr textActiveBoolPtr = IntPtr.Zero;
        public static unsafe bool GameTextInputActive => (textActiveBoolPtr != IntPtr.Zero) && *(bool*)textActiveBoolPtr;

        // Command Execution
        private delegate void ProcessChatBoxDelegate(IntPtr uiModule, IntPtr message, IntPtr unused, byte a4);
        private delegate IntPtr GetUIModuleDelegate(IntPtr basePtr);
        private ProcessChatBoxDelegate ProcessChatBox;
        public IntPtr uiModule = IntPtr.Zero;

        // Macro Execution
        private delegate void ExecuteMacroDelegate(IntPtr raptureShellModule, IntPtr macro);
        private ExecuteMacroDelegate ExecuteMacro;
        public IntPtr raptureShellModule = IntPtr.Zero;
        public IntPtr raptureMacroModule = IntPtr.Zero;

        public void Initialize(DalamudPluginInterface pInterface)
        {
            Plugin = this;

            Interface = pInterface;

            Config = (Configuration)Interface.GetPluginConfig() ?? new Configuration();
            Config.Initialize();
            Config.TryBackup(); // Backup on version change

            Interface.Framework.OnUpdateEvent += Update;

            ui = new PluginUI();
            Interface.UiBuilder.OnOpenConfigUi += ToggleConfig;
            Interface.UiBuilder.OnBuildUi += Draw;

            CheckHideOptOuts();

            commandManager = new PluginCommandManager();

            SetupIPC();

            InitializePointers();

            Task.Run(async () =>
            {
                while (!Config.AlwaysDisplayBars && !ui.configOpen && !IsLoggedIn())
                    await Task.Delay(1000);
                ReadyPlugin();
            });
        }

        private unsafe void InitializePointers()
        {
            try
            {
                var dataptr = Interface.TargetModuleScanner.GetStaticAddressFromSig("48 8B 05 ?? ?? ?? ?? 48 8B 48 28 80 B9 8E 18 00 00 00");
                textActiveBoolPtr = *(IntPtr*)(*(IntPtr*)dataptr + 0x28) + 0x188E;
            }
            catch { PluginLog.Error("Failed loading textActiveBoolPtr"); }

            try
            {
                var getUIModulePtr = Interface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 83 7F ?? 00 48 8B F0");
                var easierProcessChatBoxPtr = Interface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9");
                var uiModulePtr = Interface.TargetModuleScanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 8D 54 24 ?? 48 83 C1 10 E8");

                var GetUIModule = Marshal.GetDelegateForFunctionPointer<GetUIModuleDelegate>(getUIModulePtr);

                uiModule = GetUIModule(*(IntPtr*)uiModulePtr);
                ProcessChatBox = Marshal.GetDelegateForFunctionPointer<ProcessChatBoxDelegate>(easierProcessChatBoxPtr);

                try
                {
                    var executeMacroPtr = Interface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 4D 28");
                    ExecuteMacro = Marshal.GetDelegateForFunctionPointer<ExecuteMacroDelegate>(executeMacroPtr);

                    // TODO: Fix these to not break easily
                    raptureShellModule = uiModule + 0xA9548;
                    raptureMacroModule = uiModule + 0x4428;
                }
                catch { PluginLog.Error("Failed loading ExecuteMacro"); }
            }
            catch { PluginLog.Error("Failed loading ExecuteCommand"); }
        }

        public void ReadyPlugin()
        {
            var iconPath = Config.GetPluginIconPath();
            textureDictionaryLR.AddUserIcons(iconPath);
            textureDictionaryHR.AddUserIcons(iconPath);

            textureDictionaryLR.LoadTexture(46); // Magnifying glass / Search
            textureDictionaryHR.LoadTexture(46);
            textureDictionaryLR.AddTex(FrameIconID, "ui/uld/icona_frame.tex");
            textureDictionaryHR.AddTex(FrameIconID, "ui/uld/icona_frame_hr1.tex");
            textureDictionaryLR.LoadTexture(FrameIconID);
            textureDictionaryHR.LoadTexture(FrameIconID);
            pluginReady = true;
        }

        public void Reload()
        {
            Config = (Configuration)Interface.GetPluginConfig() ?? new Configuration();
            Config.Initialize();
            Config.UpdateVersion();
            Config.Save();
            ui.Reload();
            CheckHideOptOuts();
        }

        public void ToggleConfig(object sender, EventArgs e) => ToggleConfig();

        [Command("/qolbar")]
        [HelpMessage("Open the configuration menu.")]
        public void ToggleConfig(string command = null, string argument = null) => ui.ToggleConfig();

        [Command("/qolicons")]
        [HelpMessage("Open the icon browser.")]
        public void ToggleIconBrowser(string command = null, string argument = null) => IconBrowserUI.ToggleIconBrowser();

        [Command("/qolvisible")]
        [HelpMessage("Hide or reveal a bar using its name or index. Usage: /qolvisible [on|off|toggle] <bar>")]
        private void OnQoLVisible(string command, string argument)
        {
            var reg = Regex.Match(argument, @"^(\w+) (.+)");
            if (reg.Success)
            {
                var subcommand = reg.Groups[1].Value.ToLower();
                var bar = reg.Groups[2].Value;
                var useID = int.TryParse(bar, out var id);
                switch (subcommand)
                {
                    case "on":
                    case "reveal":
                    case "r":
                        if (useID)
                            ui.SetBarHidden(id - 1, false, false);
                        else
                            ui.SetBarHidden(bar, false, false);
                        break;
                    case "off":
                    case "hide":
                    case "h":
                        if (useID)
                            ui.SetBarHidden(id - 1, false, true);
                        else
                            ui.SetBarHidden(bar, false, true);
                        break;
                    case "toggle":
                    case "t":
                        if (useID)
                            ui.SetBarHidden(id - 1, true);
                        else
                            ui.SetBarHidden(bar, true);
                        break;
                    default:
                        PrintError("Invalid subcommand.");
                        break;
                }
            }
            else
                PrintError("Usage: /qolvisible [on|off|toggle] <bar>");
        }

        public static bool IsLoggedIn() => ConditionCache.GetCondition(DisplayCondition.ConditionType.Misc, 0);

        private static float _runTime = 0;
        public static float GetRunTime() => _runTime;
        private static long _frameCount = 0;
        public static long GetFrameCount() => _frameCount;
        private void Update(Dalamud.Game.Internal.Framework framework)
        {
            _frameCount++;
            _runTime += ImGui.GetIO().DeltaTime;

            Config.DoTimedBackup();
            ReadyCommand();
            Keybind.Run();
            Keybind.SetupHotkeys(ui.bars);
        }

        private static float _drawTime = 0;
        public static float GetDrawTime() => _drawTime;
        private void Draw()
        {
            if (_addUserIcons)
                AddUserIcons(ref _addUserIcons);

            _drawTime += ImGui.GetIO().DeltaTime;

            if (pluginReady)
            {
                Config.DrawUpdateWindow();
                ui.Draw();
            }
        }

        public void CheckHideOptOuts()
        {
            //pluginInterface.UiBuilder.DisableAutomaticUiHide = false;
            Interface.UiBuilder.DisableUserUiHide = Config.OptOutGameUIOffHide;
            Interface.UiBuilder.DisableCutsceneUiHide = Config.OptOutCutsceneHide;
            Interface.UiBuilder.DisableGposeUiHide = Config.OptOutGPoseHide;
        }

        public static Dictionary<int, string> GetUserIcons() => TextureDictionary.GetUserIcons();

        bool _addUserIcons = false;
        private bool AddUserIcons(ref bool b) => b = !TextureDictionary.AddUserIcons(Config.GetPluginIconPath());
        public void AddUserIcons() => _addUserIcons = true;

        public static void PrintEcho(string message) => Interface.Framework.Gui.Chat.Print($"[QoLBar] {message}");
        public static void PrintError(string message) => Interface.Framework.Gui.Chat.PrintError($"[QoLBar] {message}");

        private void SetupIPC()
        {
            Interface.SubscribeAny(OnReceiveMessage);
            dynamic msg = new ExpandoObject();
            msg.Sender = "QoLBar";
            msg.Action = "Loaded";
            msg.Version = Config.PluginVersion;
            Interface.SendMessage(msg);
        }

        private void OnReceiveMessage(string pluginName, dynamic msg)
        {
            try
            {
                if (!string.IsNullOrEmpty(msg.Action))
                {
                    PluginLog.LogVerbose($"Received message from {pluginName} for: {msg.Action}");
                    if (msg.Action == "Import")
                        ui.ImportBar(msg.Import);
                    else if (msg.Action == "CheckCondition")
                    {
                        int i = msg.Index;
                        var b = i >= 0 && i < Config.ConditionSets.Count && Config.ConditionSets[i].CheckConditions();

                        dynamic response = new ExpandoObject();
                        response.Sender = "QoLBar";
                        response.Receiver = pluginName;
                        response.Action = "ReturnCondition";
                        response.Return = b;
                        if (!Interface.SendMessage(pluginName, response))
                            Interface.SendMessage(response);
                    }
                    else if (msg.Action == "GetConditionSets")
                    {
                        var names = new string[Config.ConditionSets.Count];
                        for (int i = 0; i < Config.ConditionSets.Count; i++)
                            names[i] = Config.ConditionSets[i].Name;

                        dynamic response = new ExpandoObject();
                        response.Sender = "QoLBar";
                        response.Receiver = pluginName;
                        response.Action = "ReturnConditionSets";
                        response.Return = names;
                        if (!Interface.SendMessage(pluginName, response))
                            Interface.SendMessage(response);
                    }
                    else if (msg.Action == "ping")
                    {
                        dynamic response = new ExpandoObject();
                        response.Sender = "QoLBar";
                        response.Receiver = pluginName;
                        response.Action = "pong";
                        response.Version = Config.PluginVersion;
                        if (!Interface.SendMessage(pluginName, response))
                            Interface.SendMessage(response);
                    }
                }
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, $"Received message from {pluginName}, but it was invalid!");
            }
        }

        public static void SendIPCMovedCondition(int from, int to)
        {
            dynamic msg = new ExpandoObject();
            msg.Sender = "QoLBar";
            msg.Action = "MovedCondition";
            msg.Index = from;
            msg.NewIndex = to;
            Interface.SendMessage(msg);
        }

        public static void SendIPCDeletedCondition(int i)
        {
            dynamic msg = new ExpandoObject();
            msg.Sender = "QoLBar";
            msg.Action = "DeletedCondition";
            msg.Index = i;
            Interface.SendMessage(msg);
        }

        private void DisposeIPC()
        {
            dynamic msg = new ExpandoObject();
            msg.Sender = "QoLBar";
            msg.Action = "Unloaded";
            Interface.SendMessage(msg);
            Interface.UnsubscribeAny();
        }

        // Command Execution, taken from https://git.sr.ht/~jkcclemens/CCMM/tree/master/Macrology/GameFunctions.cs
        private void ReadyCommand()
        {
            commandReady = true;
            ExecuteCommand();
        }

        public void ExecuteCommand(string command)
        {
            foreach (string c in command.Split('\n'))
            {
                if (!string.IsNullOrEmpty(c))
                    commandQueue.Enqueue(c.Substring(0, Math.Min(c.Length, maxCommandLength)));
            }
            ExecuteCommand(); // Attempt to run immediately
        }

        private void ExecuteCommand()
        {
            while (commandQueue.Count > 0 && commandReady)
            {
                commandReady = false;
                var command = commandQueue.Dequeue();

                if (command.StartsWith("//"))
                {
                    command = command.Substring(2).ToLower();
                    switch (command[0])
                    {
                        case 'm': // Execute Macro
                            if (int.TryParse(command.Substring(1), out var macro) && 0 <= macro && macro < 200)
                                ExecuteMacro(raptureShellModule, raptureMacroModule + 0x58 + (0x688 * macro));
                            else
                                PrintError("Invalid macro. Usage: \"//m0\" for individual macro #0, \"//m100\" for shared macro #0, valid up to 199");
                            break;
                        case ' ': // Comment
                            commandReady = true;
                            break;
                    }
                }
                else
                {
                    try
                    {
                        var bytes = Encoding.UTF8.GetBytes(command);

                        var mem1 = Marshal.AllocHGlobal(400);
                        var mem2 = Marshal.AllocHGlobal(bytes.Length + 30);

                        Marshal.Copy(bytes, 0, mem2, bytes.Length);
                        Marshal.WriteByte(mem2 + bytes.Length, 0);
                        Marshal.WriteInt64(mem1, mem2.ToInt64());
                        Marshal.WriteInt64(mem1 + 8, 64);
                        Marshal.WriteInt64(mem1 + 8 + 8, bytes.Length + 1);
                        Marshal.WriteInt64(mem1 + 8 + 8 + 8, 0);

                        ProcessChatBox(uiModule, mem1, IntPtr.Zero, 0);

                        Marshal.FreeHGlobal(mem1);
                        Marshal.FreeHGlobal(mem2);
                    }
                    catch { PrintError("Failed injecting command"); }
                }
            }
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            DisposeIPC();

            commandManager.Dispose();
            Config.Save();
            Config.SaveTempConfig();

            Interface.Framework.OnUpdateEvent -= Update;

            Interface.UiBuilder.OnOpenConfigUi -= ToggleConfig;
            Interface.UiBuilder.OnBuildUi -= Draw;

            Interface.Dispose();

            ui.Dispose();

            textureDictionaryLR.Dispose();
            textureDictionaryHR.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    public static class Extensions
    {
        public static T2 GetDefaultValue<T, T2>(this T _, Expression<Func<T, T2>> expression)
        {
            if (((MemberExpression)expression.Body).Member.GetCustomAttribute(typeof(DefaultValueAttribute)) is DefaultValueAttribute attribute)
                return (T2)attribute.Value;
            else
                return default;
        }
    }
}
