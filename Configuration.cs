using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

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

        [JsonIgnore] private static DalamudPluginInterface pluginInterface;
        [JsonIgnore] private static string ConfigFolder => pluginInterface.GetPluginConfigDirectory();
        [JsonIgnore] private static DirectoryInfo iconFolder;

        public void Initialize(DalamudPluginInterface pInterface)
        {
            pluginInterface = pInterface;
            if (ConfigFolder != "")
                iconFolder = new DirectoryInfo(Path.Combine(ConfigFolder, "icons"));

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
                PluginLog.Error(e, "Failed to create icon folder");
                return "";
            }
        }
    }
}
