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

        [JsonIgnore] private DalamudPluginInterface pluginInterface;
        // Temporary
        [JsonIgnore] private static readonly DirectoryInfo configFolder = new DirectoryInfo(Path.Combine(new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XIVLauncher",
            "pluginConfigs",
            "QoLBar",
        }));
        [JsonIgnore] private static readonly DirectoryInfo iconFolder = new DirectoryInfo(Path.Combine(configFolder.FullName, "icons"));

        public void Initialize(DalamudPluginInterface pInterface)
        {
            pluginInterface = pInterface;

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

        public string GetPluginConfigPath()
        {
            try
            {
                if (!configFolder.Exists)
                    configFolder.Create();
                return configFolder.FullName;
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "Failed to create config folder");
                return "";
            }
        }

        public string GetPluginIconPath()
        {
            try
            {
                if (!configFolder.Exists)
                    configFolder.Create();
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
