using System;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace QoLBar;

public static class IconBrowserUI
{
    public static bool iconBrowserOpen = false;
    public static bool doPasteIcon = false;
    public static int pasteIcon = 0;

    private static bool _tabExists = false;
    private static int _i, _columns;
    private static string _name;
    private static float _iconSize;
    private static string _tooltip;
    private static bool _useLowQuality = false;
    private static List<(int, int)> _iconList;
    private static bool _displayOutsideMain = true;

    private const int iconMax = 350_000;
    private static HashSet<int> _iconExistsCache;
    private static readonly Dictionary<string, List<int>> _iconCache = new();

    public static void ToggleIconBrowser() => iconBrowserOpen = !iconBrowserOpen;

    public static void Draw()
    {
        if (!ImGuiEx.SetBoolOnGameFocus(ref _displayOutsideMain)) return;

        if (!iconBrowserOpen) { doPasteIcon = false; return; }

        var iconSize = 48 * ImGuiHelpers.GlobalScale;
        ImGui.SetNextWindowSizeConstraints(new Vector2((iconSize + ImGui.GetStyle().ItemSpacing.X) * 11 + ImGui.GetStyle().WindowPadding.X * 2 + 8), ImGuiHelpers.MainViewport.Size); // whyyyyyyyyyyyyyyyyyyyy
        ImGui.Begin("Icon Browser", ref iconBrowserOpen);

        ImGuiEx.ShouldDrawInViewport(out _displayOutsideMain);

        if (ImGuiEx.AddHeaderIconButton("RebuildIconCache", TextureDictionary.FrameIconID + 105, 1.0f, Vector2.Zero, 0, 0xFFFFFFFF, "nhg"))
            BuildCache(true);

        if (ImGui.BeginTabBar("Icon Tabs", ImGuiTabBarFlags.NoTooltip))
        {
            BeginIconList(" ★ ", iconSize);
            AddIcons(0, 100, "System");
            AddIcons(62_000, 62_600, "Classes/Jobs");
            AddIcons(62_800, 62_900, "Gearsets");
            AddIcons(66_000, 66_400, "Macros");
            AddIcons(90_000, 100_000, "FC Crests/Symbols");
            AddIcons(114_000, 114_100, "New Game+");
            AddIcons(230_850, 231_000, "Classes/Jobs (GPose)");
            AddIcons(TextureDictionary.FrameIconID, TextureDictionary.FrameIconID + 3000, "Extra");
            EndIconList();

            BeginIconList("Custom", iconSize);
            ImGuiEx.SetItemTooltip("Place images inside \"%AppData%\\XIVLauncher\\pluginConfigs\\QoLBar\\icons\"\n" +
                                   "to load them as usable icons, the file names must be in the format \"#.img\" (# > 0).\n" +
                                   "I.e. \"1.jpg\" \"2.png\" \"3.png\" \"732487.jpg\" and so on.");
            if (_tabExists)
            {
                if (ImGui.Button("Refresh Custom Icons"))
                    QoLBar.Plugin.AddUserIcons();
                ImGui.SameLine();
                if (ImGui.Button("Open Icon Folder"))
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = QoLBar.Config.GetPluginIconPath(),
                        UseShellExecute = true
                    });
            }
            foreach (var kv in QoLBar.GetUserIcons())
                AddIcons(kv.Key, kv.Key + 1);
            _tooltip = "";
            EndIconList();

            BeginIconList("Misc", iconSize);
            AddIcons(60_000, 61_000, "UI");
            AddIcons(61_200, 61_250, "Markers");
            AddIcons(61_290, 61_390, "Markers 2");
            AddIcons(61_390, 62_000, "UI 2");
            AddIcons(62_600, 62_620, "HQ FC Banners");
            AddIcons(63_900, 64_000, "Map Markers");
            AddIcons(64_500, 64_550, "Stamps");
            AddIcons(65_000, 65_900, "Currencies");
            AddIcons(180_000, 180_060, "Chocobo Racing");
            AddIcons(230_000, 230_850, "GPose");
            AddIcons(231_000, 240_000, "GPose 2");
            EndIconList();

            BeginIconList("Misc 2", iconSize);
            AddIcons(62_900, 63_200, "Achievements/Hunting Log");
            AddIcons(63_875, 63_900, "Cosmic Exploration");
            AddIcons(65_900, 66_000, "Fishing");
            AddIcons(66_400, 66_500, "Tags");
            AddIcons(67_000, 68_000, "Fashion Log");
            AddIcons(70_120, 70_200, "Animals");
            AddIcons(70_500, 70_960, "Cosmic Exploration 2");
            AddIcons(70_960, 71_450, "Quests");
            AddIcons(72_000, 72_500, "BLU UI");
            AddIcons(72_500, 72_620, "Bozja UI");
            AddIcons(76_000, 76_200, "Mahjong");
            AddIcons(80_000, 80_200, "Quest Log");
            AddIcons(80_730, 81_000, "Relic Log");
            AddIcons(82_000, 82_100, "Misc UI");
            AddIcons(82_270, 82_325, "Occult Crescent UI");
            AddIcons(83_000, 84_000, "FC Ranks");
            AddIcons(180_060, 180_100, "UI Text");
            AddIcons(240_000, 241_000, "Strategy Board");
            EndIconList();

            BeginIconList("Actions", iconSize);
            AddIcons(100, 4_000, "Classes/Jobs");
            AddIcons(5_100, 8_000, "Traits");
            AddIcons(8_000, 9_000, "Fashion");
            AddIcons(9_000, 10_000, "PvP");
            AddIcons(19_600, 19_800, "Event");
            AddIcons(19_800, 20_000, "Mount");
            AddIcons(61_250, 61_290, "Duties/Trials");
            AddIcons(64_200, 64_325, "FC");
            AddIcons(64_550, 64_600, "Occult Crescent");
            AddIcons(64_600, 64_800, "Eureka");
            AddIcons(64_800, 65_000, "NPC");
            AddIcons(70_000, 70_120, "Chocobo Racing");
            AddIcons(82_200, 82_270, "Occult Crescent 2");
            AddIcons(246_000, 250_000, "Emotes");
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
            AddIcons(50_000, 54_000, "Housing");
            AddIcons(58_000, 59_000, "Fashion");
            EndIconList();

            BeginIconList("Equipment", iconSize);
            AddIcons(30_000, 50_000, "Equipment");
            AddIcons(54_000, 54_225, "Belts");
            AddIcons(54_225, 54_400, "Flowers");
            AddIcons(54_400, 58_000, "Special Equipment");
            AddIcons(200_000, 210_000, "Glasses");
            EndIconList();

            BeginIconList("Aesthetics", iconSize);
            AddIcons(130_000, 142_000);
            AddIcons(250_000, 251_001);
            EndIconList();

            BeginIconList("Statuses", iconSize);
            AddIcons(210_000, 230_000);
            EndIconList();

            BeginIconList("Garbage", iconSize, true);
            AddIcons(61_000, 61_100, "Splash Logos");
            AddIcons(62_620, 62_800, "World Map");
            AddIcons(63_200, 63_875, "Zone Maps");
            AddIcons(66_500, 67_000, "Gardening Log");
            AddIcons(69_000, 70_000, "Mount/Minion Footprints");
            AddIcons(70_200, 70_500, "DoH/DoL Logs");
            AddIcons(71_450, 71_500, "Credits");
            AddIcons(78_000, 80_000, "Fishing Log");
            AddIcons(80_200, 80_730, "Notebooks");
            AddIcons(81_000, 82_000, "Notebooks 2");
            AddIcons(82_100, 82_200, "Housing");
            AddIcons(84_000, 85_000, "Hunts");
            AddIcons(85_000, 87_000, "Large UI");
            AddIcons(150_000, 170_000, "Tutorials");
            AddIcons(190_000, 200_000, "Adventurer Plates");
            AddIcons(241_000, 241_200, "Cosmic Exploration Zones");
            EndIconList();

            BeginIconList("Spoilers", iconSize, true);
            AddIcons(87_000, 90_000, "Triple Triad"); // Out of order because people might want to use these
            AddIcons(72_620, 76_000, "Duty Support");
            AddIcons(120_000, 130_000, "Popup Texts");
            AddIcons(142_000, 150_000, "Japanese Popup Texts");
            AddIcons(181_000, 181_500, "Boss Titles");
            EndIconList();

            BeginIconList("Spoilers 2", iconSize, true);
            AddIcons(71_500, 72_000, "Credits");
            AddIcons(100_000, 114_000, "Quest Images");
            AddIcons(114_100, 120_000, "New Game+");
            EndIconList();

            BeginIconList("Unsorted Future Icons", iconSize);
            AddIcons(10_000, 19_600);
            AddIcons(61_100, 61_200);
            AddIcons(64_000, 64_200);
            AddIcons(64_325, 64_500);
            AddIcons(76_200, 78_000);
            AddIcons(82_325, 83_000);
            //AddIcons(170_000, 180_000); // Still empty icons
            AddIcons(181_500, 190_000);
            AddIcons(241_200, 246_000);
            AddIcons(251_001, iconMax);
            EndIconList();

            ImGui.EndTabBar();
        }
        ImGui.End();

        if (iconBrowserOpen) return;
        QoLBar.CleanTextures(false);
    }

    private static bool BeginIconList(string name, float iconSize, bool useLowQuality = false)
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

            if (useLowQuality)
                _useLowQuality = true;
        }
        else
        {
            _tabExists = false;
        }

        return _tabExists;
    }

    private static void EndIconList()
    {
        if (_tabExists)
        {
            if (!string.IsNullOrEmpty(_tooltip))
                ImGuiEx.SetItemTooltip(_tooltip);
            BuildTabCache();
            DrawIconList();
            ImGui.EndTabItem();
        }
        else if (!string.IsNullOrEmpty(_tooltip))
        {
            ImGuiEx.SetItemTooltip(_tooltip);
        }
    }

    private static void AddIcons(int start, int end, string desc = "")
    {
        _tooltip += $"\n\t{start} -> {end - 1}{(!string.IsNullOrEmpty(desc) ? ("   " + desc) : "")}";
        if (_tabExists)
            _iconList.Add((start, end));
    }

    private static void DrawIconList()
    {
        if (_columns <= 0) return;

        ImGui.BeginChild($"{_name}##IconList");

        var cache = _iconCache[_name];

        ImGuiListClipperPtr clipper;
        unsafe { clipper = new(ImGuiNative.ImGuiListClipper()); }
        clipper.Begin((cache.Count - 1) / _columns + 1, _iconSize + ImGui.GetStyle().ItemSpacing.Y);

        var iconSize = new Vector2(_iconSize);
        var settings = new ImGuiEx.IconSettings { size = iconSize };
        while (clipper.Step())
        {
            for (int row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
            {
                var start = row * _columns;
                var end = Math.Min(start + _columns, cache.Count);
                for (int i = start; i < end; i++)
                {
                    var icon = cache[i];
                    ShortcutUI.DrawIcon(icon, settings, _useLowQuality ? "ln" : "n");
                    if (ImGui.IsItemClicked())
                    {
                        doPasteIcon = true;
                        pasteIcon = icon;
                        ImGui.SetClipboardText($"::{icon}");
                    }

                    if (ImGui.IsItemHovered())
                    {
                        var tex = QoLBar.TextureDictionary[icon];
                        if (!ImGui.IsMouseDown(ImGuiMouseButton.Right))
                            ImGui.SetTooltip($"{icon}");
                        else if (tex != null && tex.Handle != nint.Zero)
                        {
                            ImGui.BeginTooltip();
                            ImGui.Image(tex.Handle, new Vector2(700 * ImGuiHelpers.GlobalScale));
                            ImGui.EndTooltip();
                        }
                    }
                    if (_i % _columns != _columns - 1)
                        ImGui.SameLine();
                    _i++;
                }
            }
        }

        clipper.Destroy();

        ImGui.EndChild();
    }

    private static void BuildTabCache()
    {
        if (_iconCache.ContainsKey(_name)) return;
        DalamudApi.LogInfo($"Building Icon Browser cache for tab \"{_name}\"");

        var cache = _iconCache[_name] = new();
        foreach (var (start, end) in _iconList)
        {
            for (int icon = start; icon < end; icon++)
            {
                if (_iconExistsCache.Contains(icon))
                    cache.Add(icon);
            }
        }

        DalamudApi.LogInfo($"Done building tab cache! {cache.Count} icons found.");
    }

    public static void BuildCache(bool rebuild)
    {
        DalamudApi.LogInfo("Building Icon Browser cache");

        _iconCache.Clear();
        _iconExistsCache = !rebuild ? QoLBar.Config.LoadIconCache() ?? new() : new();

        if (_iconExistsCache.Count == 0)
        {
            for (int i = 0; i < iconMax; i++)
            {
                if (TextureDictionary.IconExists((uint)i))
                    _iconExistsCache.Add(i);
            }

            _iconExistsCache.Remove(125052); // Remove broken image (TextureFormat R8G8B8X8 is not supported for image conversion)

            QoLBar.Config.SaveIconCache(_iconExistsCache);
        }

        foreach (var kv in QoLBar.textureDictionaryLR.GetUserIcons())
            _iconExistsCache.Add(kv.Key);

        foreach (var kv in QoLBar.textureDictionaryLR.GetTextureOverrides())
            _iconExistsCache.Add(kv.Key);

        DalamudApi.LogInfo($"Done building cache! {_iconExistsCache.Count} icons found.");
    }
}