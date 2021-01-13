using ImGuiNET;
using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Plugin;

namespace QoLBar
{
    public class PluginUI : IDisposable
    {
        public bool IsVisible => true;

        private List<BarUI> bars;

        private readonly QoLBar plugin;
        private Configuration config;

#if DEBUG
        private bool configOpen = true;
#else
        private bool configOpen = false;
#endif
        public void ToggleConfig() => configOpen = !configOpen;

        public bool iconBrowserOpen = false;
        public bool doPasteIcon = false;
        public int pasteIcon = 0;
        public void ToggleIconBrowser() => iconBrowserOpen = !iconBrowserOpen;

        public PluginUI(QoLBar p, Configuration config)
        {
            plugin = p;
            this.config = config;

            bars = new List<BarUI>();
            for (int i = 0; i < config.BarConfigs.Count; i++)
                bars.Add(new BarUI(p, config, i));
        }

        public void Reload(Configuration config)
        {
            Dispose();

            this.config = config;

            bars.Clear();
            for (int i = 0; i < config.BarConfigs.Count; i++)
                bars.Add(new BarUI(plugin, config, i));
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
                doPasteIcon = false;

            foreach (BarUI bar in bars)
                bar.Draw();
        }

        private void DrawPluginConfigWindow()
        {
            ImGui.SetNextWindowSizeConstraints(new Vector2(588, 500), ImGui.GetIO().DisplaySize);
            ImGui.Begin("QoL Bar Configuration", ref configOpen);

            if (ImGui.BeginTabBar("Config Tabs"))
            {
                if (ImGui.BeginTabItem("Bar Manager"))
                {
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

                        ImGui.Separator();
                        ImGui.NextColumn();

                        ImGui.PopID();
                    }

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

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Settings"))
                {
                    if (ImGui.Checkbox("Export on Delete", ref config.ExportOnDelete))
                        config.Save();
                    ImGui.SameLine(ImGui.GetWindowWidth() / 2);
                    if (ImGui.Checkbox("Resizing Repositions Bars", ref config.ResizeRepositionsBars))
                        config.Save();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Undocked bars will automatically readjust if you change resolutions.");
                    if (ImGui.Checkbox("Use Hotbar Frames on Icons", ref config.UseIconFrame))
                        config.Save();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("This option will invert the ' f ' argument for all icons.");

                    ImGui.Spacing();
                    ImGui.Spacing();
                    ImGui.TextUnformatted("Opt out of Dalamud settings for hiding UI");
                    if (ImGui.Checkbox("Game UI Toggled", ref config.OptOutGameUIOffHide))
                    {
                        config.Save();
                        plugin.CheckHideOptOuts();
                    }
                    ImGui.SameLine(ImGui.GetWindowWidth() / 3);
                    if (ImGui.Checkbox("In Cutscene", ref config.OptOutCutsceneHide))
                    {
                        config.Save();
                        plugin.CheckHideOptOuts();
                    }
                    ImGui.SameLine(ImGui.GetWindowWidth() / 3 * 2);
                    if (ImGui.Checkbox("In /gpose", ref config.OptOutGPoseHide))
                    {
                        config.Save();
                        plugin.CheckHideOptOuts();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Backups"))
                {
                    var path = config.GetPluginBackupPath();
                    var configFile = new FileInfo(config.GetPath());

                    if (ImGui.Button("Open Folder"))
                        Process.Start(path);
                    ImGui.SameLine();
                    if (ImGui.Button("Create Backup"))
                        BackupFile(configFile);

                    ImGui.Separator();
                    ImGui.Columns(3, "QoLBarBackups", false);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var i = 0;
                        var directory = new DirectoryInfo(path);
                        foreach (var file in directory.GetFiles())
                        {
                            if (file.Extension == ".json")
                            {
                                ImGui.PushID(i);

                                ImGui.TextUnformatted(file.Name);
                                ImGui.NextColumn();
                                ImGui.TextUnformatted(file.LastWriteTime.ToString());
                                ImGui.NextColumn();
                                ImGui.SmallButton("Load");
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip($"Double click this button to overwrite and\n" +
                                        $"reload the current config with {file.Name}");

                                    if (ImGui.IsMouseDoubleClicked(0))
                                        config.LoadConfig(file);
                                }

                                ImGui.SameLine();
                                if (ImGui.SmallButton("Delete"))
                                    plugin.ExecuteCommand("/echo <se> Double right click to delete!");
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip($"Double right click this button to delete {file.Name}");

                                    if (ImGui.IsMouseDoubleClicked(1))
                                        DeleteFile(file);
                                }

                                ImGui.Separator();
                                ImGui.NextColumn();

                                ImGui.PopID();
                                i++;
                            }
                        }
                    }
                    ImGui.Columns(1);

                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

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
                plugin.PrintError($"Bar #{i + 1} does not exist.");
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
                plugin.PrintError($"Bar \"{name}\" does not exist.");

            return found;
        }

        private void BackupFile(FileInfo file, string name = "", bool overwrite = false)
        {
            try
            {
                if (file.Extension != ".json")
                    throw new InvalidOperationException("File must be json!");

                if (string.IsNullOrEmpty(name))
                    name = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");

                var path = config.GetPluginBackupPath() + $"\\{name}.json";
                file.CopyTo(path, overwrite);
                PluginLog.LogInformation($"Saved file to {path}");
            }
            catch (Exception e)
            {
                plugin.PrintError($"Failed to save: {e.Message}");
            }
        }

        private void DeleteFile(FileInfo file)
        {
            try
            {
                if (file.Extension != ".json")
                    throw new InvalidOperationException("File must be json!");

                file.Delete();
                PluginLog.LogInformation($"Deleted file {file.FullName}");
            }
            catch (Exception e)
            {
                plugin.PrintError($"Failed to delete: {e.Message}");
            }
        }

        private void DrawIconBrowser()
        {
            var iconSize = 48 * ImGui.GetIO().FontGlobalScale;
            ImGui.SetNextWindowSizeConstraints(new Vector2((iconSize + ImGui.GetStyle().ItemSpacing.X) * 11 + ImGui.GetStyle().WindowPadding.X * 2 + 8), ImGui.GetIO().DisplaySize); // whyyyyyyyyyyyyyyyyyyyy
            ImGui.Begin("Icon Browser", ref iconBrowserOpen);
            if (ImGui.BeginTabBar("Icon Tabs", ImGuiTabBarFlags.NoTooltip))
            {
                BeginIconList(" ★ ", iconSize);
                AddIcons(0, 100, "System");
                AddIcons(62_000, 62_600, "Class/Job Icons");
                AddIcons(62_800, 62_900, "Gearsets");
                AddIcons(66_000, 66_400, "Macros");
                AddIcons(90_000, 100_000, "FC Crests/Symbols");
                AddIcons(114_000, 114_100, "New Game+ Icons");
                EndIconList();

                BeginIconList("Custom", iconSize);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Place images inside \"%%AppData%%\\XIVLauncher\\pluginConfigs\\QoLBar\\icons\"\n" +
                        "to load them as usable icons, the file names must be in the format \"#.img\" (# > 0).\n" +
                        "I.e. \"1.jpg\" \"2.png\" \"3.png\" \"732487.jpg\" and so on.");
                if (_tabExists)
                {
                    if (ImGui.Button("Refresh Custom Icons"))
                        plugin.LoadUserIcons();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Can possibly cause a very rare crash!");
                    ImGui.SameLine();
                    if (ImGui.Button("Open Icon Folder"))
                        Process.Start(config.GetPluginIconPath());
                }
                foreach (var kv in plugin.userIcons)
                    AddIcons(kv.Key, kv.Key + 1);
                _tooltip = "";
                EndIconList();

                BeginIconList("Misc", iconSize);
                AddIcons(60_000, 61_000, "UI");
                AddIcons(61_200, 61_250, "Markers");
                AddIcons(61_290, 61_390, "Markers 2");
                AddIcons(61_390, 62_000, "UI 2");
                AddIcons(62_600, 62_620, "HQ FC Banners");
                AddIcons(63_900, 64_000, "Map Icons");
                AddIcons(64_500, 64_600, "Stamps");
                AddIcons(65_000, 65_900, "Currencies");
                AddIcons(76_300, 78_000, "Group Pose");
                AddIcons(180_000, 180_060, "Stamps/Chocobo Racing");
                EndIconList();

                BeginIconList("Misc 2", iconSize);
                AddIcons(62_900, 63_200, "Achievements/Hunting Log");
                AddIcons(65_900, 66_000, "Fishing");
                AddIcons(66_400, 66_500, "Tags");
                AddIcons(67_000, 68_000, "Fashion Log");
                AddIcons(71_000, 71_500, "Quests");
                AddIcons(72_000, 72_500, "BLU UI");
                AddIcons(72_500, 76_000, "Bozja UI");
                AddIcons(76_000, 76_300, "Mahjong");
                AddIcons(80_000, 80_200, "Quest Log");
                AddIcons(80_730, 81_000, "Relic Log");
                AddIcons(83_000, 84_000, "FC Ranks");
                EndIconList();

                BeginIconList("Actions", iconSize);
                AddIcons(100, 4_000, "Classes/Jobs");
                AddIcons(5_100, 8_000, "Traits");
                AddIcons(8_000, 9_000, "Fashion");
                AddIcons(9_000, 10_000, "PvP");
                AddIcons(61_100, 61_200, "Event");
                AddIcons(61_250, 61_290, "Duties/Trials");
                AddIcons(64_000, 64_200, "Emotes");
                AddIcons(64_200, 64_325, "FC");
                AddIcons(64_325, 64_500, "Emotes 2");
                AddIcons(64_600, 64_800, "Eureka");
                AddIcons(64_800, 65_000, "NPC");
                AddIcons(70_000, 70_200, "Chocobo Racing");
                EndIconList();

                BeginIconList("Mounts & Minions", iconSize);
                AddIcons(4_000, 4_400, "Mounts");
                AddIcons(4_400, 5_100, "Minions");
                AddIcons(59_000, 59_400, "Mounts... again?");
                AddIcons(59_400, 60_000, "Minion Items");
                AddIcons(68_000, 68_400, "Mounts Log");
                AddIcons(68_400, 69_000, "Minions Log");
                EndIconList();

                BeginIconList("Items", iconSize);
                AddIcons(20_000, 30_000, "General");
                AddIcons(50_000, 54_400, "Housing");
                AddIcons(58_000, 59_000, "Fashion");
                EndIconList();

                BeginIconList("Equipment", iconSize);
                AddIcons(30_000, 50_000, "Equipment");
                AddIcons(54_400, 58_000, "Special Equipment");
                EndIconList();

                BeginIconList("Aesthetics", iconSize);
                AddIcons(130_000, 142_000);
                EndIconList();

                BeginIconList("Statuses", iconSize);
                AddIcons(10_000, 20_000);
                EndIconList();

                BeginIconList("Garbage", iconSize);
                AddIcons(61_000, 61_100, "Splash Logos");
                AddIcons(62_620, 62_800, "World Map");
                AddIcons(63_200, 63_900, "Zone Maps");
                AddIcons(66_500, 67_000, "Gardening Log");
                AddIcons(69_000, 70_000, "Mount/Minion Footprints");
                AddIcons(70_200, 71_000, "DoH/DoL Logs");
                AddIcons(78_000, 80_000, "Fishing Log");
                AddIcons(80_200, 80_730, "Notebooks");
                AddIcons(81_000, 82_060, "Notebooks 2");
                AddIcons(84_000, 85_000, "Hunts");
                AddIcons(85_000, 90_000, "UI 3");
                AddIcons(150_000, 180_000, "Tutorials");
                EndIconList();

                BeginIconList("Spoilers", iconSize);
                AddIcons(82_100, 83_000, "Triple Triad"); // Out of order because people might want to use these
                AddIcons(71_500, 72_000, "Credits");
                AddIcons(82_060, 82_100, "Trusts");
                AddIcons(100_000, 114_000, "Quest Images");
                AddIcons(114_100, 120_000, "New Game+");
                AddIcons(120_000, 130_000, "Popup Texts");
                AddIcons(142_000, 150_000, "Japanese Popup Texts");
                AddIcons(180_060, 180_100, "Trusts Names");
                AddIcons(181_000, 181_500, "Boss Titles");
                AddIcons(181_500, 200_000, "Placeholder");
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
                _columns = (int)((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().WindowPadding.X) / (iconSize + ImGui.GetStyle().ItemSpacing.X)); // WHYYYYYYYYYYYYYYYYYYYYY
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
                if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(_tooltip))
                    ImGui.SetTooltip(_tooltip);
                DrawIconList();
                ImGui.EndTabItem();
            }
            else if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(_tooltip))
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
                    if (bars[0].DrawIconButton(icon, new Vector2(_iconSize), 1.0f, Vector2.Zero, Vector4.One, "_", true))
                    {
                        if (ImGui.IsItemClicked())
                        {
                            doPasteIcon = true;
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
