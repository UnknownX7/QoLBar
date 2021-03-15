using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ImGuiNET;

namespace QoLBar
{
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

        [JsonIgnore] public static DirectoryInfo ConfigFolder => QoLBar.Interface.ConfigDirectory;
        [JsonIgnore] private static DirectoryInfo iconFolder;
        [JsonIgnore] private static DirectoryInfo backupFolder;
        [JsonIgnore] private static FileInfo tempConfig;
        [JsonIgnore] public static FileInfo ConfigFile => QoLBar.Interface.ConfigFile;

        [JsonIgnore] private static bool displayUpdateWindow = false;
        [JsonIgnore] private static bool updateWindowAgree = false;

        public string GetVersion() => PluginVersion;
        public void UpdateVersion() => PluginVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public bool CheckVersion() => PluginVersion == Assembly.GetExecutingAssembly().GetName().Version.ToString();

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
                    var chat = QoLBar.Interface.Framework.Gui.Chat;
                    chat.PrintError("[QoLBar] Error saving config, is something else writing to it?");
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
                displayUpdateWindow = true;
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
