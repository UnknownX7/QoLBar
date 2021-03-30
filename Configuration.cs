using System;
using System.Numerics;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.ComponentModel;
using Newtonsoft.Json;
using ImGuiNET;
using Dalamud.Configuration;
using Dalamud.Plugin;

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

        public BarCfg Upgrade()
        {
            var oldPos = Position / ImGui.GetIO().DisplaySize;
            var oldOffset = Offset / ImGui.GetIO().DisplaySize;

            var bar = new BarCfg
            {
                Name = Title,
                Hidden = Hidden,
                Visibility = (BarCfg.BarVisibility)Visibility,
                Alignment = (BarCfg.BarAlign)Alignment,
                DockSide = (BarCfg.BarDock)DockSide,
                Hint = Hint,
                ButtonWidth = ButtonWidth,
                Editing = !HideAdd,
                Position = new[] { oldPos.X, oldPos.Y },
                LockedPosition = LockedPosition,
                Offset = new[] { oldOffset.X, oldOffset.Y },
                Scale = Scale,
                RevealAreaScale = RevealAreaScale,
                FontScale = FontScale,
                Spacing = new[] { Spacing, Spacing },
                NoBackground = NoBackground,
                ConditionSet = ConditionSet
            };

            foreach (var sh in ShortcutList)
                bar.ShortcutList.Add(sh.Upgrade(this, false));

            return bar;
        }
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

        public ShCfg Upgrade(BarConfig bar, bool sub)
        {
            var sh = new ShCfg
            {
                Name = Name,
                Type = Type switch
                {
                    ShortcutType.Category => ShCfg.ShortcutType.Category,
                    ShortcutType.Spacer => ShCfg.ShortcutType.Spacer,
                    _ => ShCfg.ShortcutType.Command
                },
                Command = Command,
                Hotkey = Hotkey,
                KeyPassthrough = KeyPassthrough,
                Mode = (ShCfg.ShortcutMode)Mode,
                Color = ImGui.ColorConvertFloat4ToU32(IconTint),
                IconZoom = IconZoom,
                IconOffset = new[] { IconOffset.X, IconOffset.Y },
                CategoryWidth = CategoryWidth,
                CategoryStaysOpen = CategoryStaysOpen,
                CategoryColumns = CategoryColumns,
                CategorySpacing = new[] { (int)bar.CategorySpacing.X, (int)bar.CategorySpacing.Y },
                CategoryScale = bar.CategoryScale,
                CategoryFontScale = bar.CategoryFontScale,
                CategoryNoBackground = bar.NoCategoryBackgrounds,
                CategoryOnHover = !sub ? bar.OpenCategoriesOnHover : bar.OpenSubcategoriesOnHover
            };

            if (SubList != null)
            {
                sh.SubList ??= new List<ShCfg>();
                foreach (var s in SubList)
                    sh.SubList.Add(s.Upgrade(bar, true));
            }

            return sh;
        }
    }

    // TODO: go through and rename stuff
    public class BarCfg
    {
        public enum BarVisibility
        {
            Slide,
            Immediate,
            Always
        }
        public enum BarAlign
        {
            LeftOrTop,
            Center,
            RightOrBottom
        }
        public enum BarDock
        {
            Top,
            Left,
            Bottom,
            Right,
            UndockedH,
            UndockedV
        }

        [JsonProperty("n")]  [DefaultValue("")]                   public string Name = string.Empty;
        [JsonProperty("sL")] [DefaultValue(null)]                 public List<ShCfg> ShortcutList = new List<ShCfg>();
        [JsonProperty("h")]  [DefaultValue(false)]                public bool Hidden = false;
        [JsonProperty("v")]  [DefaultValue(BarVisibility.Always)] public BarVisibility Visibility = BarVisibility.Always;
        [JsonProperty("a")]  [DefaultValue(BarAlign.Center)]      public BarAlign Alignment = BarAlign.Center;
        [JsonProperty("d")]  [DefaultValue(BarDock.Bottom)]       public BarDock DockSide = BarDock.Bottom;
        [JsonProperty("ht")] [DefaultValue(false)]                public bool Hint = false;
        [JsonProperty("bW")] [DefaultValue(100)]                  public int ButtonWidth = 100;
        [JsonProperty("e")]  [DefaultValue(false)]                public bool Editing = true;
        [JsonProperty("p")]  [DefaultValue(new[] { 0f, 0f })]     public float[] Position = new float[2]; // TODO
        [JsonProperty("l")]  [DefaultValue(false)]                public bool LockedPosition = false;
        [JsonProperty("o")]  [DefaultValue(new[] { 0f, 0f })]     public float[] Offset = new float[2]; // TODO
        [JsonProperty("s")]  [DefaultValue(1.0f)]                 public float Scale = 1.0f;
        [JsonProperty("rA")] [DefaultValue(1.0f)]                 public float RevealAreaScale = 1.0f;
        [JsonProperty("fS")] [DefaultValue(1.0f)]                 public float FontScale = 1.0f;
        [JsonProperty("sp")] [DefaultValue(new[] { 8, 4 })]       public int[] Spacing = new[] { 8, 4 }; // TODO
        [JsonProperty("nB")] [DefaultValue(false)]                public bool NoBackground = false;
        [JsonProperty("c")]  [DefaultValue(-1)]                   public int ConditionSet = -1;
    }

    public class ShCfg
    {
        public enum ShortcutType
        {
            Command,
            Category,
            Spacer
        }
        public enum ShortcutMode
        {
            Default,
            Incremental,
            Random
        }

        [JsonProperty("n")]   [DefaultValue("")]                   public string Name = string.Empty;
        [JsonProperty("t")]   [DefaultValue(ShortcutType.Command)] public ShortcutType Type = ShortcutType.Command;
        [JsonProperty("c")]   [DefaultValue("")]                   public string Command = string.Empty;
        [JsonProperty("k")]   [DefaultValue(0)]                    public int Hotkey = 0;
        [JsonProperty("kP")]  [DefaultValue(false)]                public bool KeyPassthrough = false;
        [JsonProperty("sL")]  [DefaultValue(null)]                 public List<ShCfg> SubList;
        [JsonProperty("m")]   [DefaultValue(ShortcutMode.Default)] public ShortcutMode Mode = ShortcutMode.Default;
        [JsonProperty("cl")]  [DefaultValue(0xFFFFFFFF)]           public uint Color = 0xFFFFFFFF; // TODO
        [JsonProperty("iZ")]  [DefaultValue(1.0f)]                 public float IconZoom = 1.0f;
        [JsonProperty("iO")]  [DefaultValue(new[] { 0f, 0f })]     public float[] IconOffset = new float[2]; // TODO
        [JsonProperty("cW")]  [DefaultValue(140)]                  public int CategoryWidth = 140;
        [JsonProperty("cSO")] [DefaultValue(false)]                public bool CategoryStaysOpen = false;
        [JsonProperty("cC")]  [DefaultValue(1)]                    public int CategoryColumns = 1;
        [JsonProperty("cSp")] [DefaultValue(new[] { 8, 4 })]       public int[] CategorySpacing = new[] { 8, 4 }; // TODO
        [JsonProperty("cS")]  [DefaultValue(1.0f)]                 public float CategoryScale = 1.0f;
        [JsonProperty("cF")]  [DefaultValue(1.0f)]                 public float CategoryFontScale = 1.0f;
        [JsonProperty("cNB")] [DefaultValue(false)]                public bool CategoryNoBackground = false;
        [JsonProperty("cH")]  [DefaultValue(false)]                public bool CategoryOnHover = false;

        // TODO: move to shortcut ui variables
        [JsonIgnore] public int _i = 0;
        [JsonIgnore] public ShCfg _parent = null;
        [JsonIgnore] public bool _activated = false;
    }

    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public List<BarConfig> BarConfigs = new List<BarConfig>();
        public List<DisplayConditionSet> ConditionSets = new List<DisplayConditionSet>();
        public bool ExportOnDelete = true;
        public bool ResizeRepositionsBars = false;
        public bool UseIconFrame = false;
        public bool AlwaysDisplayBars = false;
        public bool OptOutGameUIOffHide = false;
        public bool OptOutCutsceneHide = false;
        public bool OptOutGPoseHide = false;
        public bool NoConditionCache = false;

        public string PluginVersion = ".INITIAL";
        [JsonIgnore] public string PrevPluginVersion = string.Empty;

        [JsonIgnore] public static DirectoryInfo ConfigFolder => QoLBar.Interface.ConfigDirectory;
        [JsonIgnore] private static DirectoryInfo iconFolder;
        [JsonIgnore] private static DirectoryInfo backupFolder;
        [JsonIgnore] private static FileInfo tempConfig;
        [JsonIgnore] public static FileInfo ConfigFile => QoLBar.Interface.ConfigFile;

        [JsonIgnore] private static bool displayUpdateWindow = false;
        [JsonIgnore] private static bool updateWindowAgree = false;

        public string GetVersion() => PluginVersion;
        public void UpdateVersion()
        {
            if (PluginVersion != ".INITIAL")
                PrevPluginVersion = PluginVersion;
            PluginVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        public bool CheckVersion() => PluginVersion == Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public bool IsVersionGE(string v1, string v2) => new Version(v1) >= new Version(v2);

        public void CheckDisplayUpdateWindow()
        {
            if (!string.IsNullOrEmpty(PrevPluginVersion))
            {
                if (IsVersionGE("1.3.2.1", PrevPluginVersion))
                    displayUpdateWindow = true;
            }
        }

        public void Initialize()
        {
            if (ConfigFolder.Exists)
            {
                iconFolder = new DirectoryInfo(Path.Combine(ConfigFolder.FullName, "icons"));
                backupFolder = new DirectoryInfo(Path.Combine(ConfigFolder.FullName, "backups"));
                tempConfig = new FileInfo(backupFolder.FullName + "\\temp.json");
            }
            
            if (BarConfigs.Count < 1)
                BarConfigs.Add(new BarConfig());
        }

        public void Save(bool failed = false)
        {
            try
            {
                QoLBar.Interface.SavePluginConfig(this);
            }
            catch
            {
                if (!failed)
                {
                    PluginLog.LogError("Failed to save! Retrying...");
                    Save(true);
                }
                else
                {
                    PluginLog.LogError("Failed to save again :(");
                    QoLBar.PrintError("[QoLBar] Error saving config, is something else writing to it?");
                }
            }
        }

        public string GetPluginIconPath()
        {
            try
            {
                if (!iconFolder.Exists)
                    iconFolder.Create();
                return iconFolder.FullName;
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, "Failed to create icon folder");
                return "";
            }
        }

        public string GetPluginBackupPath()
        {
            try
            {
                if (!backupFolder.Exists)
                    backupFolder.Create();
                return backupFolder.FullName;
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, "Failed to create backup folder");
                return "";
            }
        }

        public void TryBackup()
        {
            if (!CheckVersion())
            {
                if (!tempConfig.Exists)
                    SaveTempConfig();

                try
                {
                    tempConfig.CopyTo(backupFolder.FullName + $"\\v{PluginVersion} {DateTime.Now:yyyy-MM-dd HH.mm.ss}.json");
                }
                catch (Exception e)
                {
                    PluginLog.LogError(e, "Failed to back up config!");
                }

                UpdateVersion();
                Save();
                CheckDisplayUpdateWindow();
            }

            SaveTempConfig();
        }

        public void SaveTempConfig()
        {
            try
            {
                if (!backupFolder.Exists)
                    backupFolder.Create();
                ConfigFile.CopyTo(tempConfig.FullName, true);
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, "Failed to save temp config!");
            }
        }

        public void LoadConfig(FileInfo file)
        {
            if (file.Exists)
            {
                try
                {
                    file.CopyTo(ConfigFile.FullName, true);
                    QoLBar.Plugin.Reload();
                }
                catch (Exception e)
                {
                    PluginLog.LogError(e, "Failed to load config!");
                }
            }
        }

        public void DrawUpdateWindow()
        {
            if (displayUpdateWindow)
            {
                var window = ImGui.GetIO().DisplaySize;
                ImGui.SetNextWindowPos(new System.Numerics.Vector2(window.X / 2, window.Y / 2), ImGuiCond.Appearing, new System.Numerics.Vector2(0.5f));
                ImGui.SetNextWindowSize(new System.Numerics.Vector2(550, 280) * ImGui.GetIO().FontGlobalScale);
                ImGui.Begin("QoL Bar Updated!", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings);
                ImGui.TextWrapped("QoL Bar has a new feature where categories may now run commands like a normal shortcut, " +
                    "this may cause problems for people who were using the plugin BEFORE JANUARY 4TH, due to " +
                    "the command setting being used for tooltips. Please verify that you understand the risks " +
                    "and that YOU MAY ACCIDENTALLY SEND CHAT MESSAGES WHEN CLICKING CATEGORIES. Additionally, " +
                    "YOU MAY DELETE ALL COMMANDS FROM ALL CATEGORIES AFTERWARDS if you are worried. Selecting " +
                    "YES will remove EVERY command from EVERY category in your config, note that this has no " +
                    "real downside if you have not started to utilize this feature. Selecting NO will close this " +
                    "popup permanently, you may also change your mind after selecting YES if you restore the " +
                    "version backup from the config, please be aware that old configs will possibly contain " +
                    "commands again if you do reload one of them.");
                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Checkbox("I UNDERSTAND", ref updateWindowAgree);
                if (updateWindowAgree)
                {
                    ImGui.Spacing();
                    ImGui.Spacing();
                    if (ImGui.Button("YES, DELETE THEM"))
                    {
                        static void DeleteRecursive(Shortcut sh)
                        {
                            if (sh.Type == Shortcut.ShortcutType.Category)
                            {
                                sh.Command = string.Empty;
                                if (sh.SubList != null)
                                {
                                    foreach (var sh2 in sh.SubList)
                                        DeleteRecursive(sh2);
                                }
                            }
                        }
                        foreach (var bar in BarConfigs)
                        {
                            foreach (var sh in bar.ShortcutList)
                                DeleteRecursive(sh);
                        }
                        Save();
                        displayUpdateWindow = false;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("NO, I AM FINE"))
                        displayUpdateWindow = false;
                }
                ImGui.End();
            }
        }
    }
}
