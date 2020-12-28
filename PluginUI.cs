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

            ImGui.SameLine(30);
            ImGui.Text("Bar Manager");
            ImGui.Spacing();

            Vector2 textsize = new Vector2(-1, 0);
            float textx = 0.0f;

            ImGui.Columns(3, "QoLBarsList", false);
            for (int i = 0; i < bars.Count; i++)
            {
                ImGui.PushID(i);

                ImGui.Text($"#{i + 1}");
                ImGui.SameLine();

                textx = ImGui.GetCursorPosX();

                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##Title", ref config.BarConfigs[i].Title, 32))
                    config.Save();

                textsize = ImGui.GetItemRectSize();

                ImGui.NextColumn();

                if (ImGui.Button("O"))
                    ImGui.OpenPopup($"BarConfig##{i}");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Options");
                bars[i].BarConfigPopup();
                ImGui.SameLine();
                if (ImGui.Button(config.BarConfigs[i].Hidden ? "R" : "H"))
                    bars[i].ToggleVisible();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(config.BarConfigs[i].Hidden ? "Reveal" : "Hide");

                ImGui.NextColumn();

                if (ImGui.Button("↑") && i > 0)
                {
                    var b = bars[i];
                    bars.RemoveAt(i);
                    bars.Insert(i - 1, b);

                    var b2 = config.BarConfigs[i];
                    config.BarConfigs.RemoveAt(i);
                    config.BarConfigs.Insert(i - 1, b2);
                    config.Save();
                    RefreshBarIndexes();
                }
                ImGui.SameLine();
                if (ImGui.Button("↓") && i < (bars.Count - 1))
                {
                    var b = bars[i];
                    bars.RemoveAt(i);
                    bars.Insert(i + 1, b);

                    var b2 = config.BarConfigs[i];
                    config.BarConfigs.RemoveAt(i);
                    config.BarConfigs.Insert(i + 1, b2);
                    config.Save();
                    RefreshBarIndexes();
                }
                ImGui.SameLine();
                //if (ImGui.Button("Copy")) // Probably not needed
                if (i > 0)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("Delete"))
                        plugin.ExecuteCommand("/echo <se> Right click to delete!");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"Right click this button to delete bar #{i + 1}!");

                        if (ImGui.IsMouseReleased(1))
                        {
                            bars.RemoveAt(i);
                            config.BarConfigs.RemoveAt(i);
                            config.Save();
                            RefreshBarIndexes();
                        }
                    }
                }

                ImGui.NextColumn();

                ImGui.PopID();
            }

            //textsize.Y -= 6;
            ImGui.Spacing();
            ImGui.SameLine(textx);
            if (ImGui.Button("+", textsize))
            {
                config.BarConfigs.Add(new BarConfig());
                bars.Add(new BarUI(plugin, config, bars.Count));
                config.Save();
            }

            ImGui.Columns(1); // I just wanna know who did this and where they live

            ImGui.End();
        }

        private void RefreshBarIndexes()
        {
            for (int i = 0; i < bars.Count; i++)
                bars[i].SetBarNumber(i);
        }

        public void Dispose()
        {
            foreach (BarUI bar in bars)
                bar.Dispose();
        }
    }
}
