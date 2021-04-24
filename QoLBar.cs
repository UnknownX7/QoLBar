using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Dynamic;
using System.Linq.Expressions;
using System.Diagnostics;
using ImGuiNET;
using Dalamud.Plugin;
using QoLBar.Attributes;

#pragma warning disable IDE0060 // Remove unused parameter

// I'm too lazy to make a file just for this
[assembly: AssemblyTitle("QoLBar")]
[assembly: AssemblyVersion("2.0.4.1")]

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
        private bool _pluginReady = false;
        private bool PluginReady => _pluginReady && Interface.Framework.Gui.GetBaseUIObject() != IntPtr.Zero;

        public readonly int maxCommandLength = 180; // 180 is the max per line for macros, 500 is the max you can actually type into the chat, however it is still possible to inject more
        private bool macroMode = false;
        private readonly Queue<string> commandQueue = new Queue<string>();
        private readonly Queue<string> macroQueue = new Queue<string>();
        private readonly Queue<IntPtr> freeMemQueue = new Queue<IntPtr>();

        public static TextureDictionary TextureDictionary => Config.UseHRIcons ? textureDictionaryHR : textureDictionaryLR;
        public static readonly TextureDictionary textureDictionaryLR = new TextureDictionary(false);
        public static readonly TextureDictionary textureDictionaryHR = new TextureDictionary(true);

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
        public static unsafe bool IsGameTextInputActive => (textActiveBoolPtr != IntPtr.Zero) && *(bool*)textActiveBoolPtr;
        public static unsafe bool IsMacroRunning => *(int*)(raptureShellModule + 0x2C0) >= 0;

        // Command Execution
        private delegate void ProcessChatBoxDelegate(IntPtr uiModule, IntPtr message, IntPtr unused, byte a4);
        private delegate IntPtr GetModuleDelegate(IntPtr basePtr);
        private ProcessChatBoxDelegate ProcessChatBox;
        public static IntPtr uiModule = IntPtr.Zero;

        // Macro Execution
        private delegate void ExecuteMacroDelegate(IntPtr raptureShellModule, IntPtr macro);
        private ExecuteMacroDelegate ExecuteMacro;
        public static IntPtr raptureShellModule = IntPtr.Zero;
        public static IntPtr raptureMacroModule = IntPtr.Zero;

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

            ReadyPlugin();
            SetupIPC();
        }

        private unsafe void InitializePointers()
        {
            try { textActiveBoolPtr = *(IntPtr*)(Interface.Framework.Gui.GetBaseUIObject() + 0x28) + 0x188E; }
            catch { PluginLog.Error("Failed loading textActiveBoolPtr"); }

            try
            {
                ProcessChatBox = Marshal.GetDelegateForFunctionPointer<ProcessChatBoxDelegate>(Interface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9"));
                uiModule = Interface.Framework.Gui.GetUIModule();

                try
                {
                    ExecuteMacro = Marshal.GetDelegateForFunctionPointer<ExecuteMacroDelegate>(Interface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 4D 28"));

                    var vtbl = (IntPtr*)(*(IntPtr*)uiModule);
                    var GetRaptureShellModule = Marshal.GetDelegateForFunctionPointer<GetModuleDelegate>(*(vtbl + 9)); // Client__UI__UIModule_GetRaptureShellModule / vf9
                    var GetRaptureMacroModule = Marshal.GetDelegateForFunctionPointer<GetModuleDelegate>(*(vtbl + 12)); // Client__UI__UIModule_GetRaptureMacroModule / vf12

                    raptureShellModule = GetRaptureShellModule(uiModule);
                    raptureMacroModule = GetRaptureMacroModule(uiModule);
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
            TextureDictionary.AddExtraTextures(textureDictionaryLR, textureDictionaryHR);

            InitializePointers();

            _pluginReady = true;
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

            if (!PluginReady) return;

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

            if (!PluginReady) return;

            Config.DrawUpdateWindow();
            ui.Draw();
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

#pragma warning disable CS0618 // Type or member is obsolete
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
#pragma warning restore CS0618 // Type or member is obsolete

        // Command Execution, taken from https://git.sr.ht/~jkcclemens/CCMM/tree/master/Macrology/GameFunctions.cs
        private void ReadyCommand()
        {
            commandReady = true;
            ExecuteCommand();

            if (commandReady)
            {
                macroMode = false;

                // If the user forgot to close off the macro with "//m" then try to execute it now
                if (macroQueue.Count > 0)
                    CreateAndExecuteMacro();

                // If we arent executing commands, slowly free the fake macro memory
                if (freeMemQueue.Count > 0 && !IsMacroRunning)
                    Marshal.FreeHGlobal(freeMemQueue.Dequeue());
            }
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
                            try
                            {
                                if (int.TryParse(command.Substring(1), out var macro))
                                {
                                    if (0 <= macro && macro < 200)
                                        ExecuteMacro(raptureShellModule, raptureMacroModule + 0x58 + (0x688 * macro));
                                    else
                                        PrintError("Invalid macro. Usage: \"//m0\" for individual macro #0, \"//m100\" for shared macro #0, valid up to 199.");
                                }
                                else
                                {
                                    if (macroMode)
                                    {
                                        macroMode = false;
                                        CreateAndExecuteMacro();
                                    }
                                    else
                                    {
                                        macroMode = true;
                                        commandReady = true;
                                    }
                                }
                            }
                            catch { PrintError("Failed running macro"); }
                            break;
                        case ' ': // Comment
                            commandReady = true;
                            break;
                    }
                }
                else
                {
                    if (macroMode)
                    {
                        if (macroQueue.Count < 15)
                        {
                            macroQueue.Enqueue(command + "\0");
                            commandReady = true;
                        }
                        else
                            PrintError("Failed to add command to macro, capacity reached. Please close off the macro with another \"//m\" if you didn't intend to do this.");
                    }
                    else
                    {
                        try
                        {
                            var bytes = Encoding.UTF8.GetBytes(command + "\0");
                            var memStr = Marshal.AllocHGlobal(0x18 + bytes.Length);

                            Marshal.WriteIntPtr(memStr, memStr + 0x18); // String pointer
                            Marshal.WriteInt64(memStr + 0x8, bytes.Length); // Byte capacity (unused)
                            Marshal.WriteInt64(memStr + 0x10, bytes.Length); // Byte length
                            Marshal.Copy(bytes, 0, memStr + 0x18, bytes.Length); // String

                            ProcessChatBox(uiModule, memStr, IntPtr.Zero, 0);

                            Marshal.FreeHGlobal(memStr);
                        }
                        catch { PrintError("Failed injecting command"); }
                    }
                }
            }
        }

        private void CreateAndExecuteMacro()
        {
            var macro = Marshal.AllocHGlobal(0x688); // 1672
            Marshal.WriteInt64(macro, 0x00000001000101D1); // 0xD1 0x01 0x01 0x00 0x01 0x00 0x00 0x00 (first 4 bytes are icon id, second 4 are a key in a separate file to prevent using other icons)
            Marshal.WriteIntPtr(macro + 0x8, macro + 0x2A); // Title string pointer
            Marshal.WriteInt64(macro + 0x10, 0x1); // Title byte capacity (unused)
            Marshal.WriteInt64(macro + 0x18, 0x1); // Title byte length
            Marshal.WriteInt64(macro + 0x20, 0); // ???
            Marshal.WriteInt64(macro + 0x28, 0x0100); // Title (first 2 bytes are 0x00 0x01) (actual start is +0x2A)
            //Marshal.WriteInt64(macro + 0x30, 0);
            //Marshal.WriteInt64(macro + 0x38, 0); // Title end (actual end is +0x3E)
            Marshal.WriteInt64(macro + 0x40, 0); // padding???
            Marshal.WriteInt64(macro + 0x48, 0);
            Marshal.WriteInt64(macro + 0x50, 0);
            Marshal.WriteInt64(macro + 0x58, 0);
            Marshal.WriteInt64(macro + 0x60, 0);
            Marshal.WriteInt64(macro + 0x68, 0);
            // Begin macro line
            for (int i = 0; i < 15; i++)
            {
                var memStr = IntPtr.Zero;
                var length = 1;
                if (macroQueue.Count > 0)
                {
                    var bytes = Encoding.UTF8.GetBytes(macroQueue.Dequeue());
                    length = bytes.Length;
                    memStr = Marshal.AllocHGlobal(length);
                    Marshal.Copy(bytes, 0, memStr, length);

                    freeMemQueue.Enqueue(memStr);
                }

                var line = macro + 0x70 + (0x68 * i);
                if (memStr == IntPtr.Zero)
                    Marshal.WriteIntPtr(line, line + 0x22); // String pointer
                else
                    Marshal.WriteIntPtr(line, memStr);
                Marshal.WriteInt64(line + 0x8, length); // Byte capacity (unused)
                Marshal.WriteInt64(line + 0x10, length); // Byte length
                Marshal.WriteInt64(line + 0x18, 0); // ???
                if (memStr == IntPtr.Zero)
                    Marshal.WriteInt64(line + 0x20, 0x0101); // Unused string (seems to start with 0x01 0x01 if its unused, else 0x00 0x01) (actual start is +0x22)
                else
                    Marshal.WriteInt64(line + 0x20, 0x0100);
                //Marshal.WriteInt64(line + 0x28, 0);
                //Marshal.WriteInt64(line + 0x30, 0);
                //Marshal.WriteInt64(line + 0x38, 0);
                //Marshal.WriteInt64(line + 0x40, 0);
                //Marshal.WriteInt64(line + 0x48, 0);
                //Marshal.WriteInt64(line + 0x50, 0);
                //Marshal.WriteInt64(line + 0x58, 0);
                //Marshal.WriteInt64(line + 0x60, 0); // String end (actual end is +0x61)
            }

            freeMemQueue.Enqueue(macro);
            ExecuteMacro(raptureShellModule, macro);
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
