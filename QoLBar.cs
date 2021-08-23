using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Plugin;
using Dalamud.Utility;
using ImGuiNET;

// Disclaimer: I have no idea what I'm doing.
namespace QoLBar
{
    public class QoLBar : IDalamudPlugin
    {
        public string Name => "QoL Bar";

        public static DalamudPluginInterface Interface { get; private set; }
        public static ChatGui ChatGui { get; private set; }
        public static ClientState ClientState { get; private set; }
        public static CommandManager CommandManager { get; private set; }
        public static Condition Condition { get; private set; }
        public static DataManager DataManager { get; private set; }
        public static Framework Framework { get; private set; }
        public static GameGui GameGui { get; private set; }
        public static KeyState KeyState { get; private set; }
        public static SigScanner SigScanner { get; private set; }
        public static TargetManager TargetManager { get; private set; }

        private readonly PluginCommandManager pluginCommandManager;
        public static Configuration Config { get; private set; }
        public static QoLBar Plugin { get; private set; }
        public PluginUI ui;
        private bool pluginReady = false;

        public static TextureDictionary TextureDictionary => Config.UseHRIcons ? textureDictionaryHR : textureDictionaryLR;
        public static readonly TextureDictionary textureDictionaryLR = new(false, false);
        public static readonly TextureDictionary textureDictionaryHR = new(true, false);
        public static readonly TextureDictionary textureDictionaryGSLR = new(false, true);
        public static readonly TextureDictionary textureDictionaryGSHR = new(true, true);

        public QoLBar(
            DalamudPluginInterface pluginInterface,
            ChatGui chatGui,
            ClientState clientState,
            CommandManager commandManager,
            Condition condition,
            DataManager dataManager,
            Framework framework,
            GameGui gameGui,
            KeyState keyState,
            SigScanner sigScanner,
            TargetManager targetManager)
        {
            Plugin = this;

            Interface = pluginInterface;
            ChatGui = chatGui;
            ClientState = clientState;
            CommandManager = commandManager;
            Condition = condition;
            DataManager = dataManager;
            Framework = framework;
            GameGui = gameGui;
            KeyState = keyState;
            SigScanner = sigScanner;
            TargetManager = targetManager;

            Config = (Configuration)Interface.GetPluginConfig() ?? new();
            Config.Initialize();
            Config.TryBackup(); // Backup on version change

            Framework.OnUpdateEvent += Update;

            ui = new PluginUI();
            Interface.UiBuilder.OpenConfigUi += ToggleConfig;
            Interface.UiBuilder.Draw += Draw;

            CheckHideOptOuts();

            pluginCommandManager = new();
            ReadyPlugin();
            //SetupIPC();
        }

        public void ReadyPlugin()
        {
            var iconPath = Config.GetPluginIconPath();
            textureDictionaryLR.AddUserIcons(iconPath);
            textureDictionaryHR.AddUserIcons(iconPath);

            textureDictionaryLR.LoadTexture(46); // Magnifying glass / Search
            TextureDictionary.AddExtraTextures(textureDictionaryLR, textureDictionaryHR);
            TextureDictionary.AddExtraTextures(textureDictionaryGSLR, textureDictionaryGSHR);
            IconBrowserUI.BuildCache(false);

            Game.Initialize();

            pluginReady = true;
        }

        public void Reload()
        {
            Config = (Configuration)Interface.GetPluginConfig() ?? new();
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

        public static bool HasPlugin(string name) => Interface.PluginInternalNames.Any(x => x == name);

        public static bool IsLoggedIn() => ConditionCache.GetCondition(DisplayCondition.ConditionType.Misc, 0);

        private static float _runTime = 0;
        public static float GetRunTime() => _runTime;
        private static long _frameCount = 0;
        public static long GetFrameCount() => _frameCount;
        private void Update(Framework framework)
        {
            _frameCount++;
            _runTime += ImGui.GetIO().DeltaTime;

            if (!pluginReady) return;

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

            if (!pluginReady) return;

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

        private bool _addUserIcons = false;
        private void AddUserIcons(ref bool b)
        {
            b = !TextureDictionary.AddUserIcons(Config.GetPluginIconPath());
            IconBrowserUI.BuildCache(false);
        }

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

        public static void PrintEcho(string message) => ChatGui.Print($"[QoLBar] {message}");
        public static void PrintError(string message) => ChatGui.PrintError($"[QoLBar] {message}");

        /*private void SetupIPC()
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
        }*/

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            //DisposeIPC();

            pluginCommandManager.Dispose();
            Config.Save();
            Config.SaveTempConfig();

            Framework.OnUpdateEvent -= Update;

            Interface.UiBuilder.OpenConfigUi -= ToggleConfig;
            Interface.UiBuilder.Draw -= Draw;

            Interface.Dispose();

            ui.Dispose();

            Game.Dispose();

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
            var rgba = tex.GetRgbaImageData();
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

        public static object Cast(this Type Type, object data)
        {
            var DataParam = Expression.Parameter(typeof(object), "data");
            var Body = Expression.Block(Expression.Convert(Expression.Convert(DataParam, data.GetType()), Type));

            var Run = Expression.Lambda(Body, DataParam).Compile();
            var ret = Run.DynamicInvoke(data);
            return ret;
        }
    }
}
