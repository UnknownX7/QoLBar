using System;
using System.Numerics;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.ComponentModel;
using System.Reflection;
using System.Dynamic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ImGuiNET;
using Dalamud.Plugin;
using QoLBar.Attributes;

// I'm too lazy to make a file just for this
[assembly: AssemblyTitle("QoLBar")]
[assembly: AssemblyVersion("1.3.2.0")]

// Disclaimer: I have no idea what I'm doing.
namespace QoLBar
{
    public class BarConfig
    {
        [DefaultValue("")] public string Title = string.Empty;
        [DefaultValue(null)] public List<Shortcut> ShortcutList = new List<Shortcut>();
        [DefaultValue(false)] public bool Hidden = false;
        public enum VisibilityMode
        {
            Slide,
            Immediate,
            Always
        }
        [DefaultValue(VisibilityMode.Always)] public VisibilityMode Visibility = VisibilityMode.Always;
        public enum BarAlign
        {
            LeftOrTop,
            Center,
            RightOrBottom
        }
        [DefaultValue(BarAlign.Center)] public BarAlign Alignment = BarAlign.Center;
        public enum BarDock
        {
            Top,
            Left,
            Bottom,
            Right,
            UndockedH,
            UndockedV
        }
        [DefaultValue(BarDock.Bottom)] public BarDock DockSide = BarDock.Bottom;
        [DefaultValue(false)] public bool Hint = false;
        [DefaultValue(100)] public int ButtonWidth = 100;
        [DefaultValue(false)] public bool HideAdd = false;
        public Vector2 Position = Vector2.Zero;
        [DefaultValue(false)] public bool LockedPosition = false;
        public Vector2 Offset = Vector2.Zero;
        [DefaultValue(1.0f)] public float Scale = 1.0f;
        [DefaultValue(1.0f)] public float CategoryScale = 1.0f;
        [DefaultValue(1.0f)] public float RevealAreaScale = 1.0f;
        [DefaultValue(1.0f)] public float FontScale = 1.0f;
        [DefaultValue(1.0f)] public float CategoryFontScale = 1.0f;
        [DefaultValue(8)] public int Spacing = 8;
        public Vector2 CategorySpacing = new Vector2(8, 4);
        [DefaultValue(false)] public bool NoBackground = false;
        [DefaultValue(false)] public bool NoCategoryBackgrounds = false;
        [DefaultValue(false)] public bool OpenCategoriesOnHover = false;
        [DefaultValue(false)] public bool OpenSubcategoriesOnHover = false;
        [DefaultValue(-1)] public int ConditionSet = -1;
    }

    public class Shortcut
    {
        [DefaultValue("")] public string Name = string.Empty;
        public enum ShortcutType
        {
            Command,
            Multiline_DEPRECATED,
            Category,
            Spacer
        }
        [DefaultValue(ShortcutType.Command)] public ShortcutType Type = ShortcutType.Command;
        [DefaultValue("")] public string Command = string.Empty;
        [DefaultValue(0)] public int Hotkey = 0;
        [DefaultValue(false)] public bool KeyPassthrough = false;
        [DefaultValue(null)] public List<Shortcut> SubList;
        [DefaultValue(false)] public bool HideAdd = false;
        public enum ShortcutMode
        {
            Default,
            Incremental,
            Random
        }
        [DefaultValue(ShortcutMode.Default)] public ShortcutMode Mode = ShortcutMode.Default;
        [DefaultValue(140)] public int CategoryWidth = 140;
        [DefaultValue(false)] public bool CategoryStaysOpen = false;
        [DefaultValue(1)] public int CategoryColumns = 1;
        [DefaultValue(1.0f)] public float IconZoom = 1.0f;
        public Vector2 IconOffset = Vector2.Zero;
        public Vector4 IconTint = Vector4.One;

        [JsonIgnore] public int _i = 0;
        [JsonIgnore] public Shortcut _parent = null;
        [JsonIgnore] public bool _activated = false;
    }

    public class QoLSerializer : DefaultSerializationBinder
    {
        private readonly static Type barType = typeof(BarConfig);
        private readonly static Type shortcutType = typeof(Shortcut);
        private readonly static Type vector2Type = typeof(Vector2);
        private readonly static Type vector4Type = typeof(Vector4);
        private readonly static string barShortName = "b";
        private readonly static string shortcutShortName = "s";
        private readonly static string vector2ShortName = "2";
        private readonly static string vector4ShortName = "4";
        private readonly static Dictionary<string, Type> types = new Dictionary<string, Type>
        {
            [barType.FullName] = barType,
            [barShortName] = barType,
            [shortcutType.FullName] = shortcutType,
            [shortcutShortName] = shortcutType,
            [vector2Type.FullName] = vector2Type,
            [vector2ShortName] = vector2Type,
            [vector4Type.FullName] = vector4Type,
            [vector4ShortName] = vector4Type
        };
        private readonly static Dictionary<Type, string> typeNames = new Dictionary<Type, string>
        {
            [barType] = barShortName,
            [shortcutType] = shortcutShortName,
            [vector2Type] = vector2ShortName,
            [vector4Type] = vector4ShortName
        };

        public override Type BindToType(string assemblyName, string typeName)
        {
            if (types.ContainsKey(typeName))
                return types[typeName];
            else
                return base.BindToType(assemblyName, typeName);
        }

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            if (typeNames.ContainsKey(serializedType))
            {
                assemblyName = null;
                typeName = typeNames[serializedType];
            }
            else
                base.BindToName(serializedType, out assemblyName, out typeName);
        }
    }

    public class QoLBar : IDalamudPlugin
    {
        public static DalamudPluginInterface Interface { get; private set; }
        private PluginCommandManager commandManager;
        public static Configuration Config { get; private set; }
        public static QoLBar Plugin { get; private set; }
        public PluginUI ui;
        private bool commandReady = true;
        private bool pluginReady = false;
        public readonly int maxCommandLength = 180; // 180 is the max per line for macros, 500 is the max you can actually type into the chat, however it is still possible to inject more
        private readonly Queue<string> commandQueue = new Queue<string>();
        private static readonly QoLSerializer qolSerializer = new QoLSerializer();

        public static readonly TextureDictionary textureDictionary = new TextureDictionary();

        public IntPtr textActiveBoolPtr = IntPtr.Zero;
        public unsafe bool GameTextInputActive => (textActiveBoolPtr != IntPtr.Zero) && *(bool*)textActiveBoolPtr;

        public const int FrameIconID = 114_000;
        private const int SafeIconID = 1_000_000;
        public int GetSafeIconID(byte i) => SafeIconID + i;

        public string Name => "QoL Bar";

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
                // I don't know what I'm doing, but it works
                var dataptr = Interface.TargetModuleScanner.GetStaticAddressFromSig("48 8B 05 ?? ?? ?? ?? 48 8B 48 28 80 B9 8E 18 00 00 00");
                if (dataptr != IntPtr.Zero)
                    textActiveBoolPtr = *(IntPtr*)(*(IntPtr*)dataptr + 0x28) + 0x188E;
            }
            catch { }
        }

        public void ReadyPlugin()
        {
            textureDictionary.LoadTexture(46); // Magnifying glass / Search
            textureDictionary.AddTex(FrameIconID, "ui/uld/icona_frame.tex");
            textureDictionary.LoadTexture(FrameIconID);
            AddUserIcons();
            InitCommands();
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

        public void ToggleConfig(object sender, EventArgs e) => ui.ToggleConfig();

        [Command("/qolbar")]
        [HelpMessage("Open the configuration menu.")]
        public void ToggleConfig(string command = null, string argument = null) => ui.ToggleConfig();

        [Command("/qolicons")]
        [HelpMessage("Open the icon browser.")]
        public void ToggleIconBrowser(string command = null, string argument = null) => IconBrowserUI.ToggleIconBrowser();

        [Command("/qoltoggle")]
        [HelpMessage("Hide or reveal a bar using its name or index.")]
        private void OnQoLToggle(string command, string argument)
        {
            if (int.TryParse(argument, out var id))
                ui.ToggleBarVisible(id - 1);
            else
                ui.ToggleBarVisible(argument);
        }

        public static bool IsLoggedIn() => ConditionCache.GetCondition(DisplayCondition.ConditionType.Misc, 0);

        private void Update(Dalamud.Game.Internal.Framework framework)
        {
            ReadyCommand();
            Keybind.Run(GameTextInputActive);
        }

        private static long _frameCount = 0;
        public static long GetFrameCount() => _frameCount;
        private static float _drawTime = 0;
        public static float GetDrawTime() => _drawTime;
        private void Draw()
        {
            if (_addUserIcons)
                AddUserIcons(ref _addUserIcons);

            _frameCount++;
            _drawTime += ImGui.GetIO().DeltaTime;

            if (pluginReady)
                ui.Draw();
        }

        public void CheckHideOptOuts()
        {
            //pluginInterface.UiBuilder.DisableAutomaticUiHide = false;
            Interface.UiBuilder.DisableUserUiHide = Config.OptOutGameUIOffHide;
            Interface.UiBuilder.DisableCutsceneUiHide = Config.OptOutCutsceneHide;
            Interface.UiBuilder.DisableGposeUiHide = Config.OptOutGPoseHide;
        }

        public static Dictionary<int, string> GetUserIcons() => textureDictionary.GetUserIcons();

        bool _addUserIcons = false;
        private bool AddUserIcons(ref bool b) => b = !textureDictionary.AddUserIcons(Config.GetPluginIconPath());
        public void AddUserIcons() => _addUserIcons = true;

        private static void CleanBarConfig(BarConfig bar)
        {
            if (bar.DockSide == BarConfig.BarDock.UndockedH || bar.DockSide == BarConfig.BarDock.UndockedV)
            {
                bar.Alignment = bar.GetDefaultValue(x => x.Alignment);
                bar.RevealAreaScale = bar.GetDefaultValue(x => x.RevealAreaScale);
                bar.Offset = bar.GetDefaultValue(x => x.Offset);
                bar.Hint = bar.GetDefaultValue(x => x.Hint);
            }
            else
            {
                bar.LockedPosition = bar.GetDefaultValue(x => x.LockedPosition);
                bar.Position = bar.GetDefaultValue(x => x.Position);
                bar.RevealAreaScale = bar.GetDefaultValue(x => x.RevealAreaScale);

                if (bar.Visibility == BarConfig.VisibilityMode.Always)
                {
                    bar.RevealAreaScale = bar.GetDefaultValue(x => x.RevealAreaScale);
                    bar.Hint = bar.GetDefaultValue(x => x.Hint);
                }
            }
            // Cursed optimization...
            if (bar.CategorySpacing.X == 8 && bar.CategorySpacing.Y == 4)
                bar.CategorySpacing = bar.GetDefaultValue(x => x.CategorySpacing);
            else if (bar.CategorySpacing == Vector2.Zero)
                bar.CategorySpacing = new Vector2(0.1f);

            CleanShortcut(bar.ShortcutList);
        }

        private static void CleanShortcut(List<Shortcut> shortcuts)
        {
            foreach (var sh in shortcuts)
                CleanShortcut(sh);
        }

        private static void CleanShortcut(Shortcut sh)
        {
            if (sh.Type != Shortcut.ShortcutType.Category)
            {
                sh.SubList = sh.GetDefaultValue(x => x.SubList);
                sh.HideAdd = sh.GetDefaultValue(x => x.HideAdd);
                sh.CategoryColumns = sh.GetDefaultValue(x => x.CategoryColumns);
                sh.CategoryStaysOpen = sh.GetDefaultValue(x => x.CategoryStaysOpen);
                sh.CategoryWidth = sh.GetDefaultValue(x => x.CategoryWidth);
            }
            else
            {
                sh.Command = sh.GetDefaultValue(x => x.Command);
                sh.CategoryColumns = Math.Max(sh.CategoryColumns, 1);
                CleanShortcut(sh.SubList);
            }

            if (sh.Type == Shortcut.ShortcutType.Spacer)
            {
                sh.Command = sh.GetDefaultValue(x => x.Command);
                sh.Mode = sh.GetDefaultValue(x => x.Mode);
            }

            if (!sh.Name.StartsWith("::"))
            {
                sh.IconZoom = sh.GetDefaultValue(x => x.IconZoom);
                sh.IconOffset = sh.GetDefaultValue(x => x.IconOffset);
            }

            sh.IconTint.X = Math.Min(Math.Max(sh.IconTint.X, 0), 1);
            sh.IconTint.Y = Math.Min(Math.Max(sh.IconTint.Y, 0), 1);
            sh.IconTint.Z = Math.Min(Math.Max(sh.IconTint.Z, 0), 1);
            sh.IconTint.W = Math.Min(Math.Max(sh.IconTint.W, 0), 2);
            if (sh.IconTint == Vector4.One)
                sh.IconTint = sh.GetDefaultValue(x => x.IconTint);
            else if (sh.IconTint.W == 0)
                sh.IconTint = new Vector4(1, 1, 1, 0);
        }

        public static T CopyObject<T>(T o)
        {
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Objects, SerializationBinder = qolSerializer };
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(o, settings), settings);
        }

        public static string ExportObject(object o, bool saveAllValues)
        {
            string jstring = !saveAllValues ? JsonConvert.SerializeObject(o, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                SerializationBinder = qolSerializer
            }) :
            JsonConvert.SerializeObject(o, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects
            });

            var bytes = Encoding.UTF8.GetBytes(jstring);
            using var mso = new MemoryStream();
            using (var gs = new GZipStream(mso, CompressionMode.Compress))
            {
                gs.Write(bytes, 0, bytes.Length);
            }
            return Convert.ToBase64String(mso.ToArray());
        }

        public static T ImportObject<T>(string import)
        {
            var data = Convert.FromBase64String(import);
            byte[] lengthBuffer = new byte[4];
            Array.Copy(data, data.Length - 4, lengthBuffer, 0, 4);
            int uncompressedSize = BitConverter.ToInt32(lengthBuffer, 0);

            var buffer = new byte[uncompressedSize];
            using (var ms = new MemoryStream(data))
            {
                using var gzip = new GZipStream(ms, CompressionMode.Decompress);
                gzip.Read(buffer, 0, uncompressedSize);
            }
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(buffer), new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                SerializationBinder = qolSerializer
            });
        }

        public static string ExportBar(BarConfig bar, bool saveAllValues)
        {
            if (!saveAllValues)
            {
                bar = CopyObject(bar);
                CleanBarConfig(bar);
            }
            return ExportObject(bar, saveAllValues);
        }

        public static bool allowImportConditions = false;
        public static bool allowImportHotkeys = false;
        public static BarConfig ImportBar(string import)
        {
            var bar = ImportObject<BarConfig>(import);

            if (!allowImportConditions)
                bar.ConditionSet = bar.GetDefaultValue(x => x.ConditionSet);

            if (!allowImportHotkeys)
            {
                static void removeHotkeys(List<Shortcut> shortcuts)
                {
                    foreach (var sh in shortcuts)
                    {
                        sh.Hotkey = sh.GetDefaultValue(x => x.Hotkey);
                        sh.KeyPassthrough = sh.GetDefaultValue(x => x.KeyPassthrough);
                        if (sh.SubList != null && sh.SubList.Count > 0)
                            removeHotkeys(sh.SubList);
                    }
                }
                removeHotkeys(bar.ShortcutList);
            }

            return bar;
        }

        public static string ExportShortcut(Shortcut sh, bool saveAllValues)
        {
            if (!saveAllValues)
            {
                sh = CopyObject(sh);
                CleanShortcut(sh);
            }
            return ExportObject(sh, saveAllValues);
        }

        public static Shortcut ImportShortcut(string import)
        {
            var sh = ImportObject<Shortcut>(import);

            if (!allowImportHotkeys)
            {
                sh.Hotkey = sh.GetDefaultValue(x => x.Hotkey);
                sh.KeyPassthrough = sh.GetDefaultValue(x => x.KeyPassthrough);
            }

            return sh;
        }

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
                    PluginLog.LogInformation($"Received message from {pluginName} for: {msg.Action}");
                    if (msg.Action == "Import")
                        ui.ImportBar(msg.Import);
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

        private void DisposeIPC()
        {
            dynamic msg = new ExpandoObject();
            msg.Sender = "QoLBar";
            msg.Action = "Unloaded";
            Interface.SendMessage(msg);
            Interface.UnsubscribeAny();
        }

        // I'm too dumb to do any of this so its (almost) all taken from here https://git.sr.ht/~jkcclemens/CCMM/tree/master/Custom%20Commands%20and%20Macro%20Macros/GameFunctions.cs
        #region Chat Injection
        private delegate IntPtr GetUIModuleDelegate(IntPtr basePtr);
        private delegate void EasierProcessChatBoxDelegate(IntPtr uiModule, IntPtr message, IntPtr unused, byte a4);

        private GetUIModuleDelegate GetUIModule;
        private EasierProcessChatBoxDelegate _EasierProcessChatBox;

        public IntPtr uiModulePtr;

        private void InitCommands()
        {
            try
            {
                var getUIModulePtr = Interface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 83 7F ?? 00 48 8B F0");
                var easierProcessChatBoxPtr = Interface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9");
                uiModulePtr = Interface.TargetModuleScanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 8D 54 24 ?? 48 83 C1 10 E8 ?? ?? ?? ??");

                GetUIModule = Marshal.GetDelegateForFunctionPointer<GetUIModuleDelegate>(getUIModulePtr);
                _EasierProcessChatBox = Marshal.GetDelegateForFunctionPointer<EasierProcessChatBoxDelegate>(easierProcessChatBoxPtr);
            }
            catch
            {
                PrintError("Error with loading signatures");
            }
        }

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
            if (!commandReady || commandQueue.Count == 0)
                return;

            try
            {
                if (uiModulePtr == null || uiModulePtr == IntPtr.Zero)
                    InitCommands();

                var uiModule = GetUIModule(Marshal.ReadIntPtr(uiModulePtr));

                if (uiModule == IntPtr.Zero)
                {
                    throw new ApplicationException("uiModule was null");
                }

                commandReady = false;
                var command = commandQueue.Dequeue();

                var bytes = Encoding.UTF8.GetBytes(command);

                var mem1 = Marshal.AllocHGlobal(400);
                var mem2 = Marshal.AllocHGlobal(bytes.Length + 30);

                Marshal.Copy(bytes, 0, mem2, bytes.Length);
                Marshal.WriteByte(mem2 + bytes.Length, 0);
                Marshal.WriteInt64(mem1, mem2.ToInt64());
                Marshal.WriteInt64(mem1 + 8, 64);
                Marshal.WriteInt64(mem1 + 8 + 8, bytes.Length + 1);
                Marshal.WriteInt64(mem1 + 8 + 8 + 8, 0);

                _EasierProcessChatBox(uiModule, mem1, IntPtr.Zero, 0);

                Marshal.FreeHGlobal(mem1);
                Marshal.FreeHGlobal(mem2);
            }
            catch
            {
                PrintError("Error with injecting command");
            }
        }
        #endregion

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

            foreach (var t in textureDictionary)
                t.Value?.Dispose();
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
