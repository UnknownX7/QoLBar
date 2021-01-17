using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace QoLBar
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public List<BarConfig> BarConfigs = new List<BarConfig>();
        public bool ExportOnDelete = true;
        public bool ResizeRepositionsBars = false;
        public bool UseIconFrame = false;
        public bool OptOutGameUIOffHide = false;
        public bool OptOutCutsceneHide = false;
        public bool OptOutGPoseHide = false;
        public string PluginVersion = ".INITIAL";

        [JsonIgnore] private static QoLBar plugin;
        [JsonIgnore] private static DalamudPluginInterface pluginInterface;
        [JsonIgnore] private static string ConfigFolder => pluginInterface.GetPluginConfigDirectory();
        [JsonIgnore] private static DirectoryInfo iconFolder;
        [JsonIgnore] private static DirectoryInfo backupFolder;
        [JsonIgnore] private static FileInfo tempConfig;
        [JsonIgnore] private static readonly string filePath = Path.Combine(new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncher",
            "pluginConfigs",
            "QoLBar.json",
        });

        public string GetVersion() => PluginVersion;
        public void UpdateVersion() => PluginVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public bool CheckVersion() => PluginVersion == Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public void Initialize(QoLBar p)
        {
            plugin = p;
            pluginInterface = p.pluginInterface;
            if (ConfigFolder != "")
            {
                iconFolder = new DirectoryInfo(Path.Combine(ConfigFolder, "icons"));
                backupFolder = new DirectoryInfo(Path.Combine(ConfigFolder, "backups"));
                tempConfig = new FileInfo(backupFolder.FullName + "\\temp.json");
            }

            if (BarConfigs.Count < 1)
                BarConfigs.Add(new BarConfig());
        }

        public void Save(bool failed = false)
        {
            try
            {
                pluginInterface.SavePluginConfig(this);
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
                    var chat = pluginInterface.Framework.Gui.Chat;
                    chat.PrintError("[QoLBar] Error saving config, is something else writing to it?");
                }
            }
        }

        public string GetPath() => filePath;

        public string GetPluginConfigPath() => ConfigFolder;

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
            }

            SaveTempConfig();
        }

        public void SaveTempConfig()
        {
            try
            {
                if (!backupFolder.Exists)
                    backupFolder.Create();
                var file = new FileInfo(filePath);
                file.CopyTo(tempConfig.FullName, true);
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, "Failed to save temp config!");
            }
        }

        public FileInfo GetTempConfig() => tempConfig;

        public void LoadConfig(FileInfo file)
        {
            if (file.Exists)
            {
                try
                {
                    file.CopyTo(filePath, true);
                    plugin.Reload();
                }
                catch (Exception e)
                {
                    PluginLog.LogError(e, "Failed to load config!");
                }
            }
        }
    }
}
