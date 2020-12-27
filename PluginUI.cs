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

        private bool configOpen = false;
        public void ToggleConfig() => configOpen = !configOpen;

        public PluginUI(QoLBar p, Configuration config)
        {
            plugin = p;
            this.config = config;

            bars = new List<BarUI>();
            for (int i = 0; i < config.BarConfigs.Count; i++)
                bars.Add(new BarUI(p, config, i));
        }

        public void Draw()
        {
            if (!IsVisible) return;

            if (configOpen)
                DrawPluginConfigWindow();

            foreach (BarUI bar in bars)
                bar.Draw();
        }

        private void DrawPluginConfigWindow()
        {
            ImGui.SetNextWindowSize(new Vector2(600, 500), ImGuiCond.FirstUseEver);

            ImGui.Begin("QoL Bar Configuration", ref configOpen);

            ImGui.End();
        }

        public void Dispose()
        {
            foreach (BarUI bar in bars)
                bar.Dispose();
        }
    }
}
