using System;
using System.Numerics;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Dalamud.Plugin;
using Dalamud.Game.Chat;
using Dalamud.Data.LuminaExtensions;
using ImGuiScene;
using QoLBar.Attributes;

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
        [DefaultValue(100)] public int ButtonWidth = 100;
        [DefaultValue(false)] public bool AutoButtonWidth = false;
        [DefaultValue(false)] public bool HideAdd = false;
        public Vector2 Position = Vector2.Zero;
        [DefaultValue(false)] public bool LockedPosition = false;
        public Vector2 Offset = Vector2.Zero;
        [DefaultValue(1.0f)] public float Scale = 1.0f;
        [DefaultValue(1.0f)] public float CategoryScale = 1.0f;
        [DefaultValue(1.0f)] public float RevealAreaScale = 1.0f;
        [DefaultValue(1.0f)] public float FontScale = 1.0f;
        [DefaultValue(1.0f)] public float CategoryFontScale = 1.0f;
        [DefaultValue(false)] public bool NoBackground = false;
        [DefaultValue(false)] public bool NoCategoryBackgrounds = false;
        [DefaultValue(false)] public bool OpenCategoriesOnHover = false;
    }

    public class Shortcut
    {
        [DefaultValue("")] public string Name = string.Empty;
        public enum ShortcutType
        {
            Single,
            Multiline,
            Category,
            Spacer
        }
        [DefaultValue(ShortcutType.Single)] public ShortcutType Type = ShortcutType.Single;
        [DefaultValue("")] public string Command = string.Empty;
        [DefaultValue(null)] public List<Shortcut> SubList;
        [DefaultValue(false)] public bool HideAdd = false;
        [DefaultValue(140)] public int CategoryWidth = 140;
        [DefaultValue(false)] public bool CategoryStaysOpen = false;
        [DefaultValue(1)] public int CategoryColumns = 1;
        [DefaultValue(1.0f)] public float IconZoom = 1.0f;
        [DefaultValue(null)] public Vector4 IconTint = Vector4.One;
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
        public DalamudPluginInterface pluginInterface;
        private PluginCommandManager<QoLBar> commandManager;
        private Configuration config;
        public PluginUI ui;
        private bool commandReady = true;
        private readonly Queue<string> commandQueue = new Queue<string>();
        private readonly QoLSerializer qolSerializer = new QoLSerializer();

        public readonly Dictionary<int, TextureWrap> textureDictionary = new Dictionary<int, TextureWrap>();

        public const int FrameIconID = 114000;

        public string Name => "QoL Bar";

        public void Initialize(DalamudPluginInterface pInterface)
        {
            pluginInterface = pInterface;

            config = (Configuration)pluginInterface.GetPluginConfig() ?? new Configuration();
            config.Initialize(pluginInterface);

            ui = new PluginUI(this, config);
            pluginInterface.UiBuilder.OnOpenConfigUi += ToggleConfig;
            pluginInterface.UiBuilder.OnBuildUi += ui.Draw;
            pluginInterface.ClientState.OnLogin += InitCommands;

            commandManager = new PluginCommandManager<QoLBar>(this, pluginInterface);
        }

        public void ToggleConfig(object sender, EventArgs e) => ui.ToggleConfig();

        [Command("/qolbar")]
        [HelpMessage("Open the configuration menu.")]
        public void ToggleConfig(string command = null, string argument = null) => ui.ToggleConfig();

        [Command("/qolicons")]
        [HelpMessage("Open the icon browser.")]
        public void ToggleIconBrowser(string command = null, string argument = null) => ui.ToggleIconBrowser();

        [Command("/qoltoggle")]
        [HelpMessage("Hide or reveal a bar using its name or index.")]
        private void OnQoLToggle(string command, string argument)
        {
            if (int.TryParse(argument, out int id))
                ui.ToggleBarVisible(id - 1);
            else
                ui.ToggleBarVisible(argument);
        }

        public void LoadIcon(int icon, bool overwrite = false)
        {
            if (!textureDictionary.ContainsKey(icon) || overwrite)
            {
                textureDictionary[icon] = null;
                Task.Run(() => {
                    try
                    {
                        var iconTex = pluginInterface.Data.GetIcon(icon);
                        var tex = pluginInterface.UiBuilder.LoadImageRaw(iconTex.GetRgbaImageData(), iconTex.Header.Width, iconTex.Header.Height, 4);
                        if (tex != null && tex.ImGuiHandle != IntPtr.Zero)
                            textureDictionary[icon] = tex;
                    }
                    catch { }
                });
            }
        }

        public void LoadIcon(int iconSlot, string path, bool overwrite = false)
        {
            if (!textureDictionary.ContainsKey(iconSlot) || overwrite)
            {
                textureDictionary[iconSlot] = null;
                Task.Run(() =>
                {
                    try
                    {
                        var iconTex = pluginInterface.Data.GetFile<Lumina.Data.Files.TexFile>(path);
                        var tex = pluginInterface.UiBuilder.LoadImageRaw(iconTex.GetRgbaImageData(), iconTex.Header.Width, iconTex.Header.Height, 4);
                        if (tex != null && tex.ImGuiHandle != IntPtr.Zero)
                            textureDictionary[iconSlot] = tex;
                    }
                    catch { }
                });
            }
        }

        private void CleanBarConfig(BarConfig bar)
        {
            CleanShortcuts(bar.ShortcutList);
        }

        private void CleanShortcuts(List<Shortcut> shortcuts)
        {
            foreach (var sh in shortcuts)
            {
                CleanShortcut(sh);
            }
        }

        private void CleanShortcut(Shortcut sh)
        {
            if (sh.Type != Shortcut.ShortcutType.Category)
                sh.SubList = null;
            else
                CleanShortcuts(sh.SubList);
        }

        public string ExportObject(object o, bool saveAllValues)
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

        public T ImportObject<T>(string import)
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

        public string ExportBar(BarConfig bar, bool saveAllValues)
        {
            CleanBarConfig(bar);
            return ExportObject(bar, saveAllValues);
        }

        public BarConfig ImportBar(string import)
        {
            return ImportObject<BarConfig>(import);
        }

        public string ExportShortcut(Shortcut sh, bool saveAllValues)
        {
            CleanShortcut(sh);
            return ExportObject(sh, saveAllValues);
        }

        public Shortcut ImportShortcut(string import)
        {
            return ImportObject<Shortcut>(import);
        }

        // I'm too dumb to do any of this so its (almost) all taken from here https://git.sr.ht/~jkcclemens/CCMM/tree/master/Custom%20Commands%20and%20Macro%20Macros/GameFunctions.cs
        #region Chat Injection
        private delegate IntPtr GetUIModuleDelegate(IntPtr basePtr);
        private delegate void EasierProcessChatBoxDelegate(IntPtr uiModule, IntPtr message, IntPtr unused, byte a4);

        private GetUIModuleDelegate GetUIModule;
        private EasierProcessChatBoxDelegate _EasierProcessChatBox;

        private IntPtr uiModulePtr;

        private void InitCommands(object sender = null, EventArgs e = null)
        {
            try
            {
                var getUIModulePtr = pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 83 7F ?? 00 48 8B F0");
                var easierProcessChatBoxPtr = pluginInterface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9");
                uiModulePtr = pluginInterface.TargetModuleScanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 8D 54 24 ?? 48 83 C1 10 E8 ?? ?? ?? ??");

                GetUIModule = Marshal.GetDelegateForFunctionPointer<GetUIModuleDelegate>(getUIModulePtr);
                _EasierProcessChatBox = Marshal.GetDelegateForFunctionPointer<EasierProcessChatBoxDelegate>(easierProcessChatBoxPtr);
            }
            catch
            {
                var chat = pluginInterface.Framework.Gui.Chat;
                chat.PrintChat(new XivChatEntry { MessageBytes = Encoding.UTF8.GetBytes("[QoLBar] Error with loading signatures") });
            }
        }

        public void ReadyCommand()
        {
            commandReady = true;
            ExecuteCommand();
        }

        public void ExecuteCommand(string command)
        {
            commandQueue.Enqueue(command);
            ExecuteCommand(); // Attempt to run immediately
        }

        public void ExecuteCommand()
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
                var chat = pluginInterface.Framework.Gui.Chat;
                chat.PrintChat(new XivChatEntry { MessageBytes = Encoding.UTF8.GetBytes("[QoLBar] Error with injecting command") });
            }
        }
        #endregion

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            commandManager.Dispose();
            pluginInterface.SavePluginConfig(config);

            pluginInterface.UiBuilder.OnOpenConfigUi -= ToggleConfig;
            pluginInterface.UiBuilder.OnBuildUi -= ui.Draw;
            pluginInterface.ClientState.OnLogin -= InitCommands;

            pluginInterface.Dispose();

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
}
