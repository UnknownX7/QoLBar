using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.ComponentModel;
using Newtonsoft.Json;
using ImGuiNET;
using Dalamud.Configuration;
using Dalamud.Interface.Utility;

namespace QoLBar;

public class BarCfg
{
    public enum BarDock
    {
        Top,
        Right,
        Bottom,
        Left,
        Undocked
    }
    public enum BarAlign
    {
        LeftOrTop,
        Center,
        RightOrBottom
    }
    public enum BarVisibility
    {
        Slide,
        Immediate,
        Always
    }

    [JsonProperty("n")]  [DefaultValue("")]                   public string Name = string.Empty;
    [JsonProperty("k")]  [DefaultValue(0)]                    public int Hotkey = 0;
    [JsonProperty("sL")] [DefaultValue(null)]                 public List<ShCfg> ShortcutList = new();
    [JsonProperty("h")]  [DefaultValue(false)]                public bool Hidden = false;
    [JsonProperty("d")]  [DefaultValue(BarDock.Bottom)]       public BarDock DockSide = BarDock.Bottom;
    [JsonProperty("a")]  [DefaultValue(BarAlign.Center)]      public BarAlign Alignment = BarAlign.Center;
    [JsonProperty("v")]  [DefaultValue(BarVisibility.Always)] public BarVisibility Visibility = BarVisibility.Always;
    [JsonProperty("ht")] [DefaultValue(false)]                public bool Hint = false;
    [JsonProperty("bW")] [DefaultValue(100)]                  public int ButtonWidth = 100;
    [JsonProperty("e")]  [DefaultValue(false)]                public bool Editing = false;
    [JsonProperty("cT")] [DefaultValue(false)]                public bool ClickThrough = false;
    [JsonProperty("p")]  [DefaultValue(new[] { 0f, 0f })]     public float[] Position = new float[2];
    [JsonProperty("l")]  [DefaultValue(false)]                public bool LockedPosition = false;
    [JsonProperty("co")] [DefaultValue(0)]                    public int Columns = 0;
    [JsonProperty("s")]  [DefaultValue(1.0f)]                 public float Scale = 1.0f;
    [JsonProperty("rA")] [DefaultValue(1.0f)]                 public float RevealAreaScale = 1.0f;
    [JsonProperty("fS")] [DefaultValue(1.0f)]                 public float FontScale = 1.0f;
    [JsonProperty("sp")] [DefaultValue(new[] { 8, 4 })]       public int[] Spacing = { 8, 4 };
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
    [JsonProperty("cl")]  [DefaultValue(0xFFFFFFFF)]           public uint Color = 0xFFFFFFFF;
    //[JsonProperty("cl2")] [DefaultValue(0xE6494949)]           public uint ColorBg = 0xE6494949; // TODO: Decide how to use this
    [JsonProperty("clA")] [DefaultValue(0)]                    public int ColorAnimation = 0;
    [JsonProperty("iZ")]  [DefaultValue(1.0f)]                 public float IconZoom = 1.0f;
    [JsonProperty("iO")]  [DefaultValue(new[] { 0f, 0f })]     public float[] IconOffset = new float[2];
    [JsonProperty("iR")]  [DefaultValue(0)]                    public float IconRotation = 0.0f;
    [JsonProperty("cdA")] [DefaultValue(0u)]                   public uint CooldownAction = 0;
    [JsonProperty("cdS")] [DefaultValue(0)]                    public int CooldownStyle = 0;
    [JsonProperty("cW")]  [DefaultValue(140)]                  public int CategoryWidth = 140;
    [JsonProperty("cSO")] [DefaultValue(false)]                public bool CategoryStaysOpen = false;
    [JsonProperty("cC")]  [DefaultValue(1)]                    public int CategoryColumns = 1;
    [JsonProperty("cSp")] [DefaultValue(new[] { 8, 4 })]       public int[] CategorySpacing = { 8, 4 };
    [JsonProperty("cS")]  [DefaultValue(1.0f)]                 public float CategoryScale = 1.0f;
    [JsonProperty("cF")]  [DefaultValue(1.0f)]                 public float CategoryFontScale = 1.0f;
    [JsonProperty("cNB")] [DefaultValue(false)]                public bool CategoryNoBackground = false;
    [JsonProperty("cH")]  [DefaultValue(false)]                public bool CategoryOnHover = false;
    [JsonProperty("cHC")] [DefaultValue(false)]                public bool CategoryHoverClose = false;

    [JsonProperty("_i")]  [DefaultValue(0)]                    public int _i = 0;
}

public class CndSetCfg
{
    [JsonProperty("n")] [DefaultValue("")]   public string Name = "";
    [JsonProperty("c")] [DefaultValue(null)] public List<CndCfg> Conditions = new();
}

public class CndCfg
{
    [JsonProperty("i")] [DefaultValue(Conditions.ConditionFlagCondition.constID)] public string ID = Conditions.ConditionFlagCondition.constID;
    [JsonProperty("a")] [DefaultValue(0)]                                         public dynamic Arg = 0;
    [JsonProperty("n")] [DefaultValue(false)]                                     public bool Negate = false;
    [JsonProperty("o")] [DefaultValue(ConditionManager.BinaryOperator.AND)]       public ConditionManager.BinaryOperator Operator = ConditionManager.BinaryOperator.AND;
}

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    [Obsolete] public List<BarConfig> BarConfigs { internal get; set; }
    public List<BarCfg> BarCfgs = new();
    [Obsolete] public List<DisplayConditionSet> ConditionSets { internal get; set; }
    public List<CndSetCfg> CndSetCfgs = new();
    public bool ExportOnDelete = true;
    public bool UseIconFrame = false;
    public bool AlwaysDisplayBars = false;
    public bool OptOutGameUIOffHide = false;
    public bool OptOutCutsceneHide = false;
    public bool OptOutGPoseHide = false;
    public bool NoConditionCache = false;
    public bool UseHRIcons = false;
    public int BackupTimer = 30;
    public int PieOpacity = 112;
    //public float PieAngleOffset = 0;
    public bool PieAlternateAngle = false;
    public bool PiesAlwaysCenter = false;
    public bool PiesMoveMouse = false;
    public bool PiesReturnMouse = false;
    public bool PiesReadjustMouse = false;
    public bool UsePenumbra = false;
    public float FontSize = QoLBar.DefaultFontSize;

    public string PluginVersion = ".INITIAL";
    [JsonIgnore] public string PrevPluginVersion = string.Empty;
    [JsonIgnore] public bool FirstStart = false;

    [JsonIgnore] public static DirectoryInfo ConfigFolder => DalamudApi.PluginInterface.ConfigDirectory;
    [JsonIgnore] private static DirectoryInfo iconFolder;
    [JsonIgnore] private static DirectoryInfo backupFolder;
    [JsonIgnore] private static FileInfo iconCache;
    [JsonIgnore] private static FileInfo tempConfig;
    [JsonIgnore] private static FileInfo timedConfig;
    [JsonIgnore] public static FileInfo ConfigFile => DalamudApi.PluginInterface.ConfigFile;

    [JsonIgnore] private static bool displayUpdateWindow = false;
    [JsonIgnore] private static bool updateWindowAgree = false;

    [JsonIgnore] private static float lastSave = -1;

    public string GetVersion() => PluginVersion;
    public void UpdateVersion()
    {
        if (PluginVersion != ".INITIAL")
            PrevPluginVersion = PluginVersion;
        PluginVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
    }

    public bool CheckVersion() => PluginVersion == Assembly.GetExecutingAssembly().GetName().Version?.ToString();

    public void CheckDisplayUpdateWindow()
    {
        if (!string.IsNullOrEmpty(PrevPluginVersion))
        {
            //var v = new Version(PrevPluginVersion);
            //if (new Version("1.3.2.0") >= v)
            //    displayUpdateWindow = true;
        }
    }

    public void Initialize()
    {
        if (ConfigFolder.Exists)
        {
            iconFolder = new DirectoryInfo(Path.Combine(ConfigFolder.FullName, "icons"));
            backupFolder = new DirectoryInfo(Path.Combine(ConfigFolder.FullName, "backups"));
            iconCache = new FileInfo(ConfigFolder.FullName + "\\iconCache.json");
            tempConfig = new FileInfo(backupFolder.FullName + "\\temp.json");
            timedConfig = new FileInfo(backupFolder.FullName + "\\timedBackup.json");
        }

#pragma warning disable CS0612 // Type or member is obsolete
        if (PluginVersion != ".INITIAL")
            Legacy.UpdateConfig(this);

        if (Version == 0)
        {
            for (int i = 0; i < BarConfigs.Count; i++)
                BarCfgs.Add(BarConfigs[i].Upgrade());
            BarConfigs = null;
            Version++;
        }

        if (ConditionSets is { Count: > 0 })
        {
            CndSetCfgs.Clear();
            foreach (var set in ConditionSets)
                CndSetCfgs.Add(set.Upgrade());
            ConditionSets = null;
        }
#pragma warning restore CS0612 // Type or member is obsolete

        if (BarCfgs.Count < 1)
            FirstStart = true;
    }

    public void Save(bool failed = false)
    {
        try
        {
            DalamudApi.PluginInterface.SavePluginConfig(this);
            lastSave = QoLBar.RunTime;
        }
        catch
        {
            if (!failed)
            {
                DalamudApi.LogError("Failed to save! Retrying...");
                Save(true);
            }
            else
            {
                DalamudApi.LogError("Failed to save again :(");
                QoLBar.PrintError("Error saving config, is something else writing to it?");
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
            DalamudApi.LogError("Failed to create icon folder", e);
            return string.Empty;
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
            DalamudApi.LogError("Failed to create backup folder", e);
            return string.Empty;
        }
    }

    public void TryBackup()
    {
        if (!CheckVersion())
        {
            if (!tempConfig.Exists)
                SaveTempConfig();

            try { tempConfig.CopyTo(backupFolder.FullName + $"\\v{PluginVersion} {DateTime.Now:yyyy-MM-dd HH.mm.ss}.json"); }
            catch (Exception e) { DalamudApi.LogError("Failed to back up config!", e); }

            UpdateVersion();
            Save();
            CheckDisplayUpdateWindow();
            DeleteIconCache();
            lastSave = -1;
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
        catch (Exception e) { DalamudApi.LogError("Failed to save temp config!", e); }
    }

    public void DoTimedBackup()
    {
        if (BackupTimer <= 0 || lastSave < 0 || QoLBar.RunTime < lastSave + BackupTimer * 60) return;

        SaveTimedConfig();
        lastSave = -1; // Prevent from pointlessly saving if the config is never changed
        DalamudApi.LogInfo("Performed timed backup!");
    }

    private void SaveTimedConfig()
    {
        try
        {
            if (!backupFolder.Exists)
                backupFolder.Create();
            ConfigFile.CopyTo(timedConfig.FullName, true);
        }
        catch (Exception e) { DalamudApi.LogError("Failed to save timed backup!", e); }
    }

    public void LoadConfig(FileInfo file)
    {
        if (!file.Exists) return;

        try
        {
            file.CopyTo(ConfigFile.FullName, true);
            QoLBar.Plugin.Reload();
        }
        catch (Exception e) { DalamudApi.LogError("Failed to load config!", e); }
    }

    public void SaveIconCache(HashSet<int> cache)
    {
        if (!iconFolder.Exists)
            iconFolder.Create();
        try { File.WriteAllText(iconCache.FullName, JsonConvert.SerializeObject(cache, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Objects })); } catch { }
    }

    public HashSet<int> LoadIconCache()
    {
        if (!iconFolder.Exists)
            iconFolder.Create();
        try { return JsonConvert.DeserializeObject<HashSet<int>>(File.ReadAllText(iconCache.FullName)); }
        catch { return null; }
    }

    public void DeleteIconCache() { try { iconCache.Delete(); } catch { } }

    public void DrawUpdateWindow()
    {
        if (!displayUpdateWindow) return;
        var window = ImGuiHelpers.MainViewport.Size;
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(window.X / 2, window.Y / 2), ImGuiCond.Appearing, new System.Numerics.Vector2(0.5f));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(550, 280) * ImGuiHelpers.GlobalScale);
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
                static void DeleteRecursive(ShCfg sh)
                {
                    if (sh.Type == ShCfg.ShortcutType.Category)
                    {
                        sh.Command = string.Empty;
                        if (sh.SubList != null)
                        {
                            foreach (var sh2 in sh.SubList)
                                DeleteRecursive(sh2);
                        }
                    }
                }
                foreach (var bar in BarCfgs)
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