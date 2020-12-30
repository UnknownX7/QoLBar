using Dalamud.Configuration;
using Dalamud.Game.Chat;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;

namespace QoLBar
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public List<BarConfig> BarConfigs = new List<BarConfig>();

        [JsonIgnore] private DalamudPluginInterface pluginInterface;

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
                    PluginLog.LogError("[QoLBar] Failed to save! Retrying...");
                    Save(true);
                }
                else
                {
                    PluginLog.LogError("[QoLBar] Failed to save again :(");
                    var chat = pluginInterface.Framework.Gui.Chat;
                    chat.PrintChat(new XivChatEntry { MessageBytes = Encoding.UTF8.GetBytes("[QoLBar] Error saving config, is something else writing to it?") });
                }
            }
        }
    }
}
