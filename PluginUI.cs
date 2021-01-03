using ImGuiNET;
using System;
using System.Numerics;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using Dalamud.Plugin;

namespace QoLBar
{
    public class PluginUI : IDisposable
    {
        public bool IsVisible => true;

        private readonly List<BarUI> bars;

        private readonly QoLBar plugin;
        private readonly Configuration config;

#if DEBUG
        private bool configOpen = true;
#else
        private bool configOpen = false;
#endif
        public void ToggleConfig() => configOpen = !configOpen;

        bool iconBrowserOpen = false;
        public void ToggleIconBrowser() => iconBrowserOpen = !iconBrowserOpen;

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
            plugin.ReadyCommand();

            if (!IsVisible) return;

            if (configOpen)
                DrawPluginConfigWindow();

            if (iconBrowserOpen)
                DrawIconBrowser();

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
            ImGui.Separator();

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
                if (ImGui.Button("Export"))
                    ImGui.SetClipboardText(ExportBar(config.BarConfigs[i]));
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Export to clipboard");
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

            ImGui.Separator();
            ImGui.Spacing();
            ImGui.SameLine(textx);
            if (ImGui.Button("+", textsize))
            {
                config.BarConfigs.Add(new BarConfig());
                bars.Add(new BarUI(plugin, config, bars.Count));
                config.Save();
            }
            ImGui.NextColumn();
            ImGui.NextColumn();
            if (ImGui.Button("Import", textsize))
            {
                try
                {
                    config.BarConfigs.Add(ImportBar(ImGui.GetClipboardText()));
                    bars.Add(new BarUI(plugin, config, bars.Count));
                    config.Save();
                }
                catch (Exception e)
                {
                    PluginLog.LogError("Invalid import string!");
                    PluginLog.LogError($"{e.GetType()}\n{e.Message}");
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Import from clipboard");

            ImGui.Columns(1); // I just wanna know who did this and where they live

            ImGui.End();
        }

        private void RefreshBarIndexes()
        {
            for (int i = 0; i < bars.Count; i++)
                bars[i].SetBarNumber(i);
        }

        private void CleanBarConfig(BarConfig bar)
        {
            CleanShortcuts(bar.ShortcutList);
        }

        private void CleanShortcuts(List<Shortcut> shortcuts)
        {
            foreach (var sh in shortcuts)
            {
                if (sh.Type != Shortcut.ShortcutType.Category)
                    sh.SubList = null;
                else
                    CleanShortcuts(sh.SubList);
            }
        }

        private string ExportBar(BarConfig bar)
        {
            CleanBarConfig(bar);

            string jstring = JsonConvert.SerializeObject(bar, Formatting.None, new JsonSerializerSettings
            {
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                TypeNameHandling = TypeNameHandling.Objects
            });

            var bytes = Encoding.UTF8.GetBytes(jstring);
            using var mso = new MemoryStream();
            using (var gs = new GZipStream(mso, CompressionMode.Compress))
            {
                gs.Write(bytes, 0, bytes.Length);
            }
            return Convert.ToBase64String(mso.ToArray());
        }

        private BarConfig ImportBar(string import)
        {
            var data = Convert.FromBase64String(import);
            byte[] lengthBuffer = new byte[4];
            Array.Copy(data, data.Length - 4, lengthBuffer, 0, 4);
            int uncompressedSize = BitConverter.ToInt32(lengthBuffer, 0);

            var buffer = new byte[uncompressedSize];
            using (var ms = new MemoryStream(data))
            {
                using var gzip = new GZipStream(ms, CompressionMode.Decompress);
                gzip.Read(buffer, 0, uncompressedSize);
            }
            return JsonConvert.DeserializeObject<BarConfig>(Encoding.UTF8.GetString(buffer), new JsonSerializerSettings
            {
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                TypeNameHandling = TypeNameHandling.Objects
            });
        }

        private void DrawIconBrowser()
        {
            var iconSize = 48 * ImGui.GetIO().FontGlobalScale;
            ImGui.SetNextWindowSizeConstraints(new Vector2((iconSize + ImGui.GetStyle().FramePadding.X * 2) * 11 + ImGui.GetStyle().FramePadding.X * 2 + 16), ImGui.GetIO().DisplaySize); // whyyyyyyyyyyyyyyyyyyyy
            ImGui.Begin("Icon Browser", ref iconBrowserOpen);
            if (ImGui.BeginTabBar("Icon Tabs", ImGuiTabBarFlags.NoTooltip)) // TODO: Actually categorize icons
            {
                BeginIconList(" ★ ", iconSize);
                AddIcons(0, 100, "System");
                AddIcons(66000, 66400, "Macros");
                AddIcons(90000, 100000, "FC Crests/Symbols");
                AddIcons(114000, 114100, "New Game+ Icons");
                EndIconList();

                BeginIconList("Actions", iconSize);
                AddIcons(100, 4000, "Classes/Jobs");
                AddIcons(5100, 8000, "Traits");
                AddIcons(8000, 9000, "Fashion");
                AddIcons(9000, 10000, "PvP");
                AddIcons(61100, 61200, "Event");
                AddIcons(61250, 61290, "Duties/Trials");
                AddIcons(64000, 64200, "Emotes");
                AddIcons(64200, 64325, "FC");
                AddIcons(64325, 64500, "Emotes 2");
                AddIcons(64600, 64800, "Eureka");
                AddIcons(64800, 65000, "NPC");
                AddIcons(70000, 70200, "Chocobo Racing");
                EndIconList();

                BeginIconList("Mounts & Minions", iconSize);
                AddIcons(4000, 4400, "Mounts");
                AddIcons(4400, 5100, "Minions");
                AddIcons(59000, 59400, "Mounts... again?");
                AddIcons(59400, 60000, "Minion Items");
                AddIcons(68000, 68400, "Mounts Menu");
                AddIcons(68400, 69000, "Minions Menu");
                EndIconList();

                BeginIconList("Items", iconSize);
                AddIcons(20000, 30000, "General");
                AddIcons(50000, 54400, "Housing");
                AddIcons(58000, 59000, "Fashion");
                EndIconList();

                BeginIconList("Equipment", iconSize);
                AddIcons(30000, 50000, "Equipment");
                AddIcons(54400, 58000, "Special Equipment");
                EndIconList();

                BeginIconList("Aesthetics", iconSize);
                AddIcons(130000, 142000);
                EndIconList();

                BeginIconList("Statuses", iconSize);
                AddIcons(10000, 20000);
                EndIconList();

                BeginIconList("Misc", iconSize);
                AddIcons(60000, 61000, "UI");
                AddIcons(61000, 61100, "Splash Logos");
                AddIcons(61200, 61250, "Markers");
                AddIcons(61290, 61390, "Markers 2");
                AddIcons(61390, 64000, "UI 2");
                AddIcons(64500, 64600, "Stamps");
                AddIcons(65000, 65900, "Currencies");
                AddIcons(72000, 72500, "BLU UI");
                AddIcons(76300, 78000, "Group Pose");
                AddIcons(180000, 180060, "Stamps/Chocobo Racing");
                EndIconList();

                BeginIconList("Misc 2", iconSize);
                AddIcons(65900, 66000, "Fishing");
                AddIcons(66400, 68000, "UI 3");
                AddIcons(69000, 70000, "Mount/Minion Footprints");
                AddIcons(70200, 71000, "DoH/DoL Logs");
                AddIcons(71000, 71500, "Quests");
                AddIcons(72500, 76000, "Eureka UI");
                AddIcons(76000, 76300, "Mahjong");
                AddIcons(78000, 80000, "Fishing Log");
                EndIconList();

                BeginIconList("Misc 3", iconSize);
                AddIcons(80000, 82060, "Notebooks");
                AddIcons(83000, 85000, "FC/Hunts");
                AddIcons(85000, 90000, "UI 4");
                AddIcons(150000, 180000, "Tutorials");
                EndIconList();

                BeginIconList("Spoilers", iconSize);
                AddIcons(82100, 83000, "Triple Triad"); // Out of order because people might want to use these
                AddIcons(71500, 72000, "Credits");
                AddIcons(82060, 82100, "Trusts");
                AddIcons(100000, 114000, "Quest Images");
                AddIcons(114100, 120000, "New Game+");
                AddIcons(120000, 130000, "Popup Texts (Unreadable spoilers)");
                AddIcons(142000, 150000, "Japanese Popup Texts");
                AddIcons(180060, 180100, "Trusts Names");
                AddIcons(181000, 181500, "Boss Titles");
                AddIcons(181500, 200000, "Placeholder");
                EndIconList();

                ImGui.EndTabBar();
            }
            ImGui.End();
        }

        private bool _tabExists = false;
        private int _i, _columns;
        private float _iconSize;
        private string _tooltip;
        private bool BeginIconList(string name, float iconSize)
        {
            _tooltip = "Contains:";
            if (ImGui.BeginTabItem(name))
            {
                ImGui.BeginChild($"{name}##IconList");
                _tabExists = true;
                _i = 0;
                _columns = (int)((ImGui.GetContentRegionAvail().X + ImGui.GetStyle().FramePadding.X * 2) / (iconSize + ImGui.GetStyle().FramePadding.X * 2)); // WHYYYYYYYYYYYYYYYYYYYYY
                _iconSize = iconSize;
            }
            else
                _tabExists = false;

            return _tabExists;
        }

        private void EndIconList()
        {
            if (_tabExists)
            {
                ImGui.EndChild();
                ImGui.EndTabItem();
            }
            else if (ImGui.IsItemHovered())
                ImGui.SetTooltip(_tooltip);
        }

        private void AddIcons(int start, int end, string desc = "")
        {
            _tooltip += $"\n\t{start} -> {end - 1}{(!string.IsNullOrEmpty(desc) ? ("   " + desc) : "")}";
            if (_tabExists)
            {
                for (int icon = start; icon < end; icon++)
                {
                    if (bars[0].DrawIconButton(icon, new Vector2(_iconSize), 1.0f, true))
                    {
                        if (ImGui.IsItemClicked())
                            ImGui.SetClipboardText($"::{icon}");
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip($"{icon}");
                        if (_i % _columns != _columns - 1)
                            ImGui.SameLine();
                        _i++;
                    }
                }
            }
        }

        public void Dispose()
        {
            foreach (BarUI bar in bars)
                bar.Dispose();
        }
    }
}
