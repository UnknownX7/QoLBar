using System;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Dynamic;
using System.Linq.Expressions;
using ImGuiNET;
using Dalamud.Plugin;
using Dalamud.Hooking;

#pragma warning disable IDE0060 // Remove unused parameter

// I'm too lazy to make a file just for this
[assembly: AssemblyTitle("QoLBar")]
[assembly: AssemblyVersion("2.1.1.0")]

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
        private bool _pluginReady = false;
        private bool PluginReady => _pluginReady;

        public static TextureDictionary TextureDictionary => Config.UseHRIcons ? textureDictionaryHR : textureDictionaryLR;
        public static readonly TextureDictionary textureDictionaryLR = new TextureDictionary(false, false);
        public static readonly TextureDictionary textureDictionaryHR = new TextureDictionary(true, false);
        public static readonly TextureDictionary textureDictionaryGSLR = new TextureDictionary(false, true);
        public static readonly TextureDictionary textureDictionaryGSHR = new TextureDictionary(true, true);

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

        private static unsafe void InitializePointers()
        {
            try { Game.textActiveBoolPtr = *(IntPtr*)(Interface.Framework.Gui.GetBaseUIObject() + 0x28) + 0x188E; }
            catch { PluginLog.Error("Failed loading textActiveBoolPtr"); }

            try
            {
                Game.ProcessChatBox = Marshal.GetDelegateForFunctionPointer<Game.ProcessChatBoxDelegate>(Interface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9"));
                Game.uiModule = Interface.Framework.Gui.GetUIModule();

                try
                {
                    Game.ExecuteMacroHook = new Hook<Game.ExecuteMacroDelegate>(Interface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 4D 28"), new Game.ExecuteMacroDelegate(Game.ExecuteMacroDetour));
                    Game.ExecuteMacroHook.Enable();

                    Game.numCopiedMacroLinesPtr = Interface.TargetModuleScanner.ScanText("49 8D 5E 70 BF ?? 00 00 00") + 0x5;
                    Game.numExecutedMacroLinesPtr = Interface.TargetModuleScanner.ScanText("41 83 F8 ?? 0F 8D ?? ?? ?? ?? 49 6B C8 68") + 0x3;

                    var vtbl = (IntPtr*)(*(IntPtr*)Game.uiModule);
                    var GetRaptureShellModule = Marshal.GetDelegateForFunctionPointer<Game.GetModuleDelegate>(*(vtbl + 9)); // Client__UI__UIModule_GetRaptureShellModule / vf9
                    var GetRaptureMacroModule = Marshal.GetDelegateForFunctionPointer<Game.GetModuleDelegate>(*(vtbl + 12)); // Client__UI__UIModule_GetRaptureMacroModule / vf12

                    Game.raptureShellModule = GetRaptureShellModule(Game.uiModule);
                    Game.raptureMacroModule = GetRaptureMacroModule(Game.uiModule);
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
            TextureDictionary.AddExtraTextures(textureDictionaryGSLR, textureDictionaryGSHR);

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
            Game.ReadyCommand();
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

        public static void CleanTextures(bool disposing)
        {
            if (disposing)
            {
                textureDictionaryLR.Dispose();
                textureDictionaryHR.Dispose();
                textureDictionaryGSLR.Dispose();
                textureDictionaryGSHR.Dispose();
            }
            else
            {
                textureDictionaryLR.TryEmpty();
                textureDictionaryHR.TryEmpty();
                textureDictionaryGSLR.TryEmpty();
                textureDictionaryGSHR.TryEmpty();
            }
        }

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

            Game.ExecuteMacroHook.Dispose();
            Game.NumCopiedMacroLines = 15;
            Game.NumExecutedMacroLines = 15;

            CleanTextures(true);
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

        public static byte[] GetGrayscaleImageData(this Lumina.Data.Files.TexFile tex)
        {
            var rgba = Dalamud.Data.LuminaExtensions.TexFileExtensions.GetRgbaImageData(tex);
            var pixels = rgba.Length / 4;
            var newData = new byte[rgba.Length];
            for (int i = 0; i < pixels; i++)
            {
                var pixel = i * 4;
                var alpha = rgba[pixel + 3];

                if (alpha > 0)
                {
                    var avg = (byte)(0.2125f * rgba[pixel] + 0.7154f * rgba[pixel + 1] + 0.0721f * rgba[pixel + 2]);
                    newData[pixel] = avg;
                    newData[pixel + 1] = avg;
                    newData[pixel + 2] = avg;
                }

                newData[pixel + 3] = alpha;
            }
            return newData;
        }
    }
}
