using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;
using System.Collections.Generic;

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

        public void Save()
        {
            pluginInterface.SavePluginConfig(this);
        }
    }
}
