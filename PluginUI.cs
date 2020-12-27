using ImGuiNET;
using System;
using System.Numerics;
using System.Collections.Generic;
using static QoLBar.BarConfig;
using Dalamud.Plugin;

namespace QoLBar
{
    public class PluginUI : IDisposable
    {
        public bool IsVisible => true;

        private readonly List<BarUI> bars;

        private readonly QoLBar plugin;
        private readonly Configuration config;

        public PluginUI(QoLBar p, Configuration config)
        {
            plugin = p;
            this.config = config;

            bars = new List<BarUI>();
            for (int i = 0; i < config.BarConfigs.Count; i++)
            {
                bars.Add(new BarUI(p, config, i));
            }
        }

        public void Draw()
        {
            if (!IsVisible) return;

            foreach (BarUI bar in bars)
            {
                bar.Draw();
            }
        }

        public void Dispose()
        {

        }
    }
}
