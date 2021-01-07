using ImGuiNET;
using System;
using System.Numerics;
using System.Text;
using System.Collections.Generic;
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

        public bool iconBrowserOpen = false;
        public int pasteIcon = -1;
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
            else
                pasteIcon = -1;

            foreach (BarUI bar in bars)
                bar.Draw();
        }

        private void DrawPluginConfigWindow()
        {
            ImGui.SetNextWindowSizeConstraints(new Vector2(588, 500), ImGui.GetIO().DisplaySize);
            ImGui.Begin("QoL Bar Configuration", ref configOpen);

            if (ImGui.Checkbox("Export on Delete", ref config.ExportOnDelete))
                config.Save();

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();
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
                    ImGui.SetClipboardText(plugin.ExportBar(config.BarConfigs[i], false));
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Export to clipboard without default settings (May change with updates).\n" +
                        "Right click to export with every setting (Longer string, doesn't change).");

                    if (ImGui.IsMouseReleased(1))
                        ImGui.SetClipboardText(plugin.ExportBar(config.BarConfigs[i], true));
                }

                if (i > 0)
                {
                    ImGui.SameLine();
                    if (ImGui.Button(config.ExportOnDelete ? "Cut" : "Delete"))
                        plugin.ExecuteCommand("/echo <se> Right click to delete!");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip($"Right click this button to delete bar #{i + 1}!" +
                            (config.ExportOnDelete ? "\nThe bar will be exported to clipboard first." : ""));

                        if (ImGui.IsMouseReleased(1))
                        {
                            if (config.ExportOnDelete)
                                ImGui.SetClipboardText(plugin.ExportBar(config.BarConfigs[i], false));

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
                AddBar(new BarConfig());
            ImGui.NextColumn();
            ImGui.NextColumn();
            if (ImGui.Button("Import", textsize))
            {
                try
                {
                    AddBar(plugin.ImportBar(ImGui.GetClipboardText()));
                }
                catch (Exception e) // Try as a shortcut instead
                {
                    try
                    {
                        var sh = plugin.ImportShortcut(ImGui.GetClipboardText());
                        var bar = new BarConfig();
                        bar.ShortcutList.Add(sh);
                        AddBar(bar);
                    }
                    catch (Exception e2)
                    {
                        PluginLog.LogError("Invalid import string!");
                        PluginLog.LogError($"{e.GetType()}\n{e.Message}");
                        PluginLog.LogError($"{e2.GetType()}\n{e2.Message}");
                    }
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Import a bar from the clipboard,\n" +
                    "or import a single shortcut as a new bar.");

            ImGui.Columns(1); // I just wanna know who did this and where they live

            ImGui.End();
        }

        private void AddBar(BarConfig bar)
        {
            config.BarConfigs.Add(bar);
            bars.Add(new BarUI(plugin, config, bars.Count));
            config.Save();
        }

        private void RefreshBarIndexes()
        {
            for (int i = 0; i < bars.Count; i++)
                bars[i].SetBarNumber(i);
        }

        public bool ToggleBarVisible(int i)
        {
            if (i < 0 || i >= bars.Count)
            {
                plugin.pluginInterface.Framework.Gui.Chat.PrintError($"Bar #{i + 1} does not exist.");
                return false;
            }
            else
            {
                bars[i].ToggleVisible();
                return true;
            }
        }

        public bool ToggleBarVisible(string name)
        {
            var found = false;
            for (int i = 0; i < bars.Count; ++i)
            {
                if (config.BarConfigs[i].Title == name)
                    found = ToggleBarVisible(i) || found;
            }
            if (!found)
                plugin.pluginInterface.Framework.Gui.Chat.PrintError($"Bar \"{name}\" does not exist.");

            return found;
        }

        private void DrawIconBrowser()
        {
            var iconSize = 48 * ImGui.GetIO().FontGlobalScale;
            ImGui.SetNextWindowSizeConstraints(new Vector2((iconSize + ImGui.GetStyle().FramePadding.X * 2) * 11 + ImGui.GetStyle().FramePadding.X * 2 + 16), ImGui.GetIO().DisplaySize); // whyyyyyyyyyyyyyyyyyyyy
            ImGui.Begin("Icon Browser", ref iconBrowserOpen);
            if (ImGui.BeginTabBar("Icon Tabs", ImGuiTabBarFlags.NoTooltip))
            {
                BeginIconList(" ★ ", iconSize);
                AddIcons(0, 100, "System");
                AddIcons(62000, 62600, "Class/Job Icons");
                AddIcons(62800, 62900, "Gearsets");
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
                AddIcons(68000, 68400, "Mounts Log");
                AddIcons(68400, 69000, "Minions Log");
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
                AddIcons(61200, 61250, "Markers");
                AddIcons(61290, 61390, "Markers 2");
                AddIcons(61390, 62000, "UI 2");
                AddIcons(62600, 62620, "HQ FC Banners");
                AddIcons(63900, 64000, "Map Icons");
                AddIcons(64500, 64600, "Stamps");
                AddIcons(65000, 65900, "Currencies");
                AddIcons(76300, 78000, "Group Pose");
                AddIcons(180000, 180060, "Stamps/Chocobo Racing");
                EndIconList();

                BeginIconList("Misc 2", iconSize);
                AddIcons(62900, 63200, "Achievements/Hunting Log");
                AddIcons(65900, 66000, "Fishing");
                AddIcons(66400, 66500, "Tags");
                AddIcons(67000, 68000, "Fashion Log");
                AddIcons(71000, 71500, "Quests");
                AddIcons(72000, 72500, "BLU UI");
                AddIcons(72500, 76000, "Bozja UI");
                AddIcons(76000, 76300, "Mahjong");
                AddIcons(80000, 80200, "Quest Log");
                AddIcons(80730, 81000, "Relic Log");
                AddIcons(83000, 84000, "FC Ranks");
                EndIconList();

                BeginIconList("Garbage", iconSize);
                AddIcons(61000, 61100, "Splash Logos");
                AddIcons(62620, 62800, "World Map");
                AddIcons(63200, 63900, "Zone Maps");
                AddIcons(66500, 67000, "Gardening Log");
                AddIcons(69000, 70000, "Mount/Minion Footprints");
                AddIcons(70200, 71000, "DoH/DoL Logs");
                AddIcons(78000, 80000, "Fishing Log");
                AddIcons(80200, 80730, "Notebooks");
                AddIcons(81000, 82060, "Notebooks 2");
                AddIcons(84000, 85000, "Hunts");
                AddIcons(85000, 90000, "UI 3");
                AddIcons(150000, 180000, "Tutorials");
                EndIconList();

                BeginIconList("Spoilers", iconSize);
                AddIcons(82100, 83000, "Triple Triad"); // Out of order because people might want to use these
                AddIcons(71500, 72000, "Credits");
                AddIcons(82060, 82100, "Trusts");
                AddIcons(100000, 114000, "Quest Images");
                AddIcons(114100, 120000, "New Game+");
                AddIcons(120000, 130000, "Popup Texts");
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
        private string _name;
        private float _iconSize;
        private string _tooltip;
        private List<(int, int)> _iconList;
        private bool BeginIconList(string name, float iconSize)
        {
            _tooltip = "Contains:";
            if (ImGui.BeginTabItem(name))
            {
                _name = name;
                _tabExists = true;
                _i = 0;
                _columns = (int)((ImGui.GetContentRegionAvail().X + ImGui.GetStyle().FramePadding.X * 2) / (iconSize + ImGui.GetStyle().FramePadding.X * 2)); // WHYYYYYYYYYYYYYYYYYYYYY
                _iconSize = iconSize;
                _iconList = new List<(int, int)>();
            }
            else
                _tabExists = false;

            return _tabExists;
        }

        private void EndIconList()
        {
            if (_tabExists)
            {
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(_tooltip);
                DrawIconList();
                ImGui.EndTabItem();
            }
            else if (ImGui.IsItemHovered())
                ImGui.SetTooltip(_tooltip);
        }

        private void AddIcons(int start, int end, string desc = "")
        {
            _tooltip += $"\n\t{start} -> {end - 1}{(!string.IsNullOrEmpty(desc) ? ("   " + desc) : "")}";
            if (_tabExists)
                _iconList.Add((start, end));
        }

        private void DrawIconList()
        {
            ImGui.BeginChild($"{_name}##IconList");
            foreach ((int start, int end) in _iconList)
            {
                for (int icon = start; icon < end; icon++)
                {
                    if (bars[0].DrawIconButton(icon, new Vector2(_iconSize), 1.0f, new Vector4(1), true))
                    {
                        if (ImGui.IsItemClicked())
                        {
                            pasteIcon = icon;
                            ImGui.SetClipboardText($"::{icon}");
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip($"{icon}");
                        if (_i % _columns != _columns - 1)
                            ImGui.SameLine();
                        _i++;
                    }
                }
            }
            ImGui.EndChild();
        }

        public void Dispose()
        {
            foreach (BarUI bar in bars)
                bar.Dispose();
        }
    }
}
