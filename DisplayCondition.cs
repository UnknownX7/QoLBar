using System;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using ImGuiNET;
using Dalamud.Game.ClientState;

namespace QoLBar
{
    public class DisplayCondition
    {
        public enum ConditionType
        {
            Logic,
            ConditionFlag,
            Job,
            Role,
            Misc,
            Zone
        }

        public ConditionType Type = ConditionType.Logic;
        public int Condition = 0;
        public dynamic Arg = 0;

        public bool CheckCondition()
        {
            if (Type == ConditionType.Logic)
                return false;
            else
                return ConditionCache.GetCondition(Type, Condition, (Type == ConditionType.Misc) ? Arg : null);
        }

        public bool IsLogic() => Type == ConditionType.Logic;
        public bool IsOr() => IsLogic() && Condition == 0;
        public bool IsXor() => IsLogic() && Condition == 1;
        public bool IsNot() => IsLogic() && Condition == 2;
        public bool IsEquals() => IsLogic() && Condition == 3;
    }

    public class DisplayConditionSet
    {
        public string Name = string.Empty;
        public readonly List<DisplayCondition> Conditions = new List<DisplayCondition>();

        private bool _cached = false;
        private float _lastCache = 0;
        private int _currentPos = 0;
        private DisplayCondition curCond => Conditions[_currentPos];

        private static readonly Array conditionFlags = Enum.GetValues(typeof(ConditionFlag));
        public static Dictionary<uint, Lumina.Excel.GeneratedSheets.ClassJob> classDictionary;
        private static readonly Dictionary<int, string> roleDictionary = new Dictionary<int, string>
        {
            [0] = "No Role",
            [1] = "Tank",
            [2] = "Melee DPS",
            [3] = "Ranged DPS",
            [4] = "Healer",
            [30] = "DoW",
            [31] = "DoM",
            [32] = "DoL",
            [33] = "DoH"
        };
        public static Dictionary<uint, Lumina.Excel.GeneratedSheets.TerritoryType> territoryDictionary;

        public void Add() => Conditions.Add(new DisplayCondition());
        public void Add(DisplayCondition cond) => Conditions.Add(cond);
        public void Insert(int i, DisplayCondition cond) => Conditions.Insert(i, cond);
        public void Remove(int i) => Conditions.RemoveAt(i);

        private bool Parse()
        {
            while (_currentPos < Conditions.Count)
            {
                bool b = true;

                try
                {
                    if (curCond.IsNot())
                    {
                        _currentPos++;
                        b = !ParseBool();
                    }
                    else
                        b = ParseBool();

                    // AND is implicit

                    if (curCond.IsOr())
                    {
                        _currentPos++;
                        return b || Parse();
                    }
                    else if (curCond.IsXor())
                    {
                        _currentPos++;
                        var next = Parse();
                        return b && !next || !b && next;
                    }
                    else if (curCond.IsEquals())
                    {
                        _currentPos++;
                        return b == Parse();
                    }
                }
                catch {}

                if (!b)
                    return false;
            }
            return true;
        }

        private bool ParseBool()
        {
            var b = curCond.CheckCondition();
            _currentPos++;
            return b;
        }

        public bool CheckConditions()
        {
            ConditionCache.CheckCache();
            if (ConditionCache.GetLastCache() == _lastCache)
                return _cached;

            _currentPos = 0;
            _cached = Parse();
            _lastCache = ConditionCache.GetLastCache();
            return _cached;
        }

        public static void DrawEditor()
        {
            var config = QoLBar.Config;
            for (int i = 0; i < config.ConditionSets.Count; i++)
            {
                ImGui.PushID(i);

                var set = config.ConditionSets[i];

                var open = ImGui.TreeNodeEx($"#{i + 1}##Node", ImGuiTreeNodeFlags.FramePadding | ImGuiTreeNodeFlags.AllowItemOverlap | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                ImGui.SameLine();
                if (ImGui.InputText("##Name", ref set.Name, 32))
                    config.Save();

                ImGui.SameLine();
                if (open)
                {
                    if (ImGui.Button("   +   "))
                    {
                        set.Add();
                        config.Save();
                    }
                }
                else
                {
                    if (ImGui.Button("↑") && i > 0)
                        SwapConditionSet(i, i - 1);
                    ImGui.SameLine();
                    if (ImGui.Button("↓") && i < (config.ConditionSets.Count - 1))
                        SwapConditionSet(i, i + 1);
                }
                ImGui.SameLine();
                if (ImGui.Button("Delete"))
                    QoLBar.Plugin.ExecuteCommand("/echo <se> Right click to delete!");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"Right click this button to delete this set!");
                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                        RemoveConditionSet(i);
                }

                if (open)
                {
                    ImGui.SameLine();
                    ImGui.TextUnformatted($"{set.CheckConditions()}");

                    ImGui.Indent();

                    ImGui.Columns(3, $"QoLConditionSet{i}", false);

                    for (int j = 0; j < set.Conditions.Count; j++)
                    {
                        ImGui.PushID(j);

                        var cond = set.Conditions[j];

                        var names = Enum.GetNames(typeof(DisplayCondition.ConditionType));
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.BeginCombo("##Type", cond.Type.ToString()))
                        {
                            for (int n = 0; n < names.Length; n++)
                            {
                                if (n == 4) continue;

                                if (ImGui.Selectable(names[n], n == (int)cond.Type))
                                {
                                    cond.Type = (DisplayCondition.ConditionType)n;
                                    // This list is completely and utterly awful so help people out a little bit
                                    if (cond.Type == DisplayCondition.ConditionType.Zone)
                                        cond.Condition = QoLBar.Interface.ClientState.TerritoryType;
                                    config.Save();
                                }
                            }

                            // Always display Misc last
                            if (ImGui.Selectable("Misc", cond.Type == DisplayCondition.ConditionType.Misc))
                            {
                                cond.Type = DisplayCondition.ConditionType.Misc;
                                cond.Arg = 0;
                                config.Save();
                            }

                            ImGui.EndCombo();
                        }

                        ImGui.NextColumn();

                        ImGui.SetNextItemWidth(-1);
                        switch (cond.Type)
                        {
                            case DisplayCondition.ConditionType.Logic:
                                ImGui.Combo("##LogicOperator", ref cond.Condition, "OR\0XOR\0NOT\0EQUALS");
                                break;
                            case DisplayCondition.ConditionType.ConditionFlag:
                                {
                                    if (ImGui.BeginCombo("##Flag", ((ConditionFlag)cond.Condition).ToString()))
                                    {
                                        foreach (ConditionFlag flag in conditionFlags)
                                        {
                                            if (ImGui.Selectable(flag.ToString(), (int)flag == cond.Condition))
                                            {
                                                cond.Condition = (int)flag;
                                                config.Save();
                                            }
                                        }
                                        ImGui.EndCombo();
                                    }
                                }
                                break;
                            case DisplayCondition.ConditionType.Job:
                                {
                                    classDictionary.TryGetValue((uint)cond.Condition, out var r);
                                    if (ImGui.BeginCombo("##Job", r?.Abbreviation.ToString()))
                                    {
                                        foreach (var kv in classDictionary)
                                        {
                                            if (kv.Key == 0) continue;

                                            if (ImGui.Selectable(kv.Value.Abbreviation.ToString(), (int)kv.Key == cond.Condition))
                                            {
                                                cond.Condition = (int)kv.Key;
                                                config.Save();
                                            }
                                        }
                                        ImGui.EndCombo();
                                    }
                                }
                                break;
                            case DisplayCondition.ConditionType.Role:
                                {
                                    roleDictionary.TryGetValue(cond.Condition, out var s);
                                    if (ImGui.BeginCombo("##Role", s))
                                    {
                                        foreach (var kv in roleDictionary)
                                        {
                                            if (ImGui.Selectable(kv.Value, kv.Key == cond.Condition))
                                            {
                                                cond.Condition = kv.Key;
                                                config.Save();
                                            }
                                        }
                                        ImGui.EndCombo();
                                    }
                                }
                                break;
                            case DisplayCondition.ConditionType.Misc:
                                {
                                    var opts = new string[]
                                    {
                                        "Logged in",
                                        "Character ID",
                                        "Have Target",
                                        "Have Focus Target",
                                        "Weapon Drawn",
                                        "Eorzea Timespan"
                                    };

                                    if (cond.Condition == 5)
                                        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2);
                                    if (ImGui.BeginCombo("##Misc", (0 <= cond.Condition && cond.Condition < opts.Length) ? opts[cond.Condition] : string.Empty))
                                    {
                                        void AddMiscConditionSelectable(int id, dynamic arg)
                                        {
                                            if (ImGui.Selectable(opts[id], cond.Condition == id))
                                            {
                                                cond.Condition = id;
                                                cond.Arg = arg;
                                                config.Save();
                                            }
                                        }

                                        AddMiscConditionSelectable(0, 0);

                                        AddMiscConditionSelectable(1, QoLBar.Interface.ClientState.LocalContentId);
                                        if (ImGui.IsItemHovered())
                                            ImGui.SetTooltip("Selecting this will assign the current character's ID to this condition.");

                                        AddMiscConditionSelectable(2, 0);

                                        AddMiscConditionSelectable(3, 0);

                                        AddMiscConditionSelectable(4, 0);

                                        AddMiscConditionSelectable(5, "");

                                        ImGui.EndCombo();
                                    }
                                    if (cond.Condition == 1 && ImGui.IsItemHovered())
                                        ImGui.SetTooltip($"ID: {cond.Arg}");
                                    if (cond.Condition == 5)
                                    {
                                        ImGui.SameLine();
                                        string timespan = cond.Arg is string ? cond.Arg : string.Empty;
                                        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                                        if (ImGui.InputText("##Timespan", ref timespan, 16))
                                        {
                                            cond.Arg = timespan;
                                            config.Save();
                                        }
                                        if (ImGui.IsItemHovered())
                                            ImGui.SetTooltip("Timespan must be formatted as \"01:30-22:10\" and may contain \"X\" wildcards.\n" +
                                                "I.e \"XX:30-XX:10\" will return true for times such as 01:30, 13:54, and 21:09.\n" +
                                                "The minimum time is inclusive, but the maximum is not.");
                                    }
                                }
                                break;
                            case DisplayCondition.ConditionType.Zone:
                                {
                                    territoryDictionary.TryGetValue((uint)cond.Condition, out var r);
                                    if (ImGui.BeginCombo("##Zone", r?.PlaceName.Value.Name.ToString()))
                                    {
                                        foreach (var kv in territoryDictionary)
                                        {
                                            if (ImGui.Selectable($"{kv.Value.PlaceName.Value.Name}##{kv.Key}", kv.Key == cond.Condition))
                                            {
                                                cond.Condition = (int)kv.Key;
                                                config.Save();
                                            }
                                            if (ImGui.IsItemHovered())
                                                ImGui.SetTooltip($"ID: {kv.Key}");
                                        }
                                        ImGui.EndCombo();
                                    }
                                    if (ImGui.IsItemHovered())
                                        ImGui.SetTooltip($"ID: {cond.Condition}");
                                }
                                break;
                        }

                        ImGui.NextColumn();

                        if (ImGui.Button("↑") && j > 0)
                        {
                            set.Remove(j);
                            set.Insert(j - 1, cond);
                            config.Save();
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("↓") && j < (set.Conditions.Count - 1))
                        {
                            set.Remove(j);
                            set.Insert(j + 1, cond);
                            config.Save();
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Delete"))
                            QoLBar.Plugin.ExecuteCommand("/echo <se> Right click to delete!");
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip($"Right click this button to delete this condition!");
                            if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                            {
                                set.Remove(j);
                                config.Save();
                            }
                        }
                        if (cond.Type != DisplayCondition.ConditionType.Logic)
                        {
                            ImGui.SameLine();
                            ImGui.TextUnformatted($"{cond.CheckCondition()}");
                        }

                        ImGui.NextColumn();

                        ImGui.PopID();
                    }

                    ImGui.Columns(1);

                    ImGui.Unindent();
                }

                ImGui.Separator();

                ImGui.PopID();
            }

            if (ImGui.Button("+", new Vector2(-1, 0)))
            {
                config.ConditionSets.Add(new DisplayConditionSet());
                config.Save();
            }
        }

        private static void SwapConditionSet(int from, int to)
        {
            var config = QoLBar.Config;
            var set = config.ConditionSets[from];
            foreach (var bar in config.BarConfigs)
            {
                if (bar.ConditionSet == from)
                    bar.ConditionSet = to;
                else if (bar.ConditionSet == to)
                    bar.ConditionSet = from;
            }
            config.ConditionSets.RemoveAt(from);
            config.ConditionSets.Insert(to, set);
            config.Save();
        }

        private static void RemoveConditionSet(int i)
        {
            var config = QoLBar.Config;
            foreach (var bar in config.BarConfigs)
            {
                if (bar.ConditionSet > i)
                    bar.ConditionSet -= 1;
                else if (bar.ConditionSet == i)
                    bar.ConditionSet = -1;
            }
            config.ConditionSets.RemoveAt(i);
            config.Save();
        }
    }

    public static class ConditionCache
    {
        private static readonly Dictionary<(DisplayCondition.ConditionType, int, dynamic), bool> _conditionCache = new Dictionary<(DisplayCondition.ConditionType, int, dynamic), bool>();
        private static float _lastCache = 0;
        public static float GetLastCache() => _lastCache;

        public static bool GetCondition(DisplayCondition.ConditionType type, int cond, dynamic arg = null)
        {
            var pluginInterface = QoLBar.Interface;
            CheckCache();

            if (_conditionCache.TryGetValue((type, cond, arg), out var b))
                return b;
            else
            {
                var player = pluginInterface.ClientState.LocalPlayer;
                switch (type)
                {
                    case DisplayCondition.ConditionType.ConditionFlag:
                        b = pluginInterface.ClientState.Condition[(ConditionFlag)cond];
                        break;
                    case DisplayCondition.ConditionType.Job:
                        b = (player != null) && (player.ClassJob.Id == cond);
                        break;
                    case DisplayCondition.ConditionType.Role:
                        b = (player != null) && pluginInterface.Data.IsDataReady && (((cond < 30) ? player.ClassJob.GameData.Role : player.ClassJob.GameData.ClassJobCategory.Row) == cond);
                        break;
                    case DisplayCondition.ConditionType.Misc:
                        b = cond switch
                        {
                            0 => pluginInterface.ClientState.Condition.Any(),
                            1 => !(arg is string) && (ulong)arg == pluginInterface.ClientState.LocalContentId,
                            2 => pluginInterface.ClientState.Targets.CurrentTarget != null,
                            3 => pluginInterface.ClientState.Targets.FocusTarget != null,
                            4 => (player != null) && IsWeaponDrawn(player),
                            5 => arg is string && CheckTimeCondition(arg),
                            _ => false,
                        };
                        break;
                    case DisplayCondition.ConditionType.Zone:
                        b = pluginInterface.ClientState.TerritoryType == cond;
                        break;
                }

                _conditionCache[(type, cond, arg)] = b;
                return b;
            }
        }

        private static unsafe bool IsWeaponDrawn(Dalamud.Game.ClientState.Actors.Types.PlayerCharacter player) => (*(byte*)(player.Address + 0x1980) & 0b100) > 0;

        private static unsafe DateTimeOffset GetEorzeaTime() => DateTimeOffset.FromUnixTimeSeconds(*(long*)(QoLBar.Interface.Framework.Address.BaseAddress + 0x1608));

        private static (double, double, double, double) ParseTime(string str) => (char.GetNumericValue(str[0]), char.GetNumericValue(str[1]), char.GetNumericValue(str[3]), char.GetNumericValue(str[4]));

        private static bool IsTimeBetween(string tStr, string minStr, string maxStr)
        {
            var t = ParseTime(tStr);
            var min = ParseTime(minStr);
            var max = ParseTime(maxStr);

            var minTime = 0.0;
            var maxTime = 0.0;
            var curTime = 0.0;
            if (min.Item1 >= 0 && max.Item1 >= 0)
            {
                minTime += min.Item1 * 1000;
                maxTime += max.Item1 * 1000;
                curTime += t.Item1 * 1000;
            }
            if (min.Item2 >= 0 && max.Item2 >= 0)
            {
                minTime += min.Item2 * 100;
                maxTime += max.Item2 * 100;
                curTime += t.Item2 * 100;
            }
            if (min.Item3 >= 0 && max.Item3 >= 0)
            {
                minTime += min.Item3 * 10;
                maxTime += max.Item3 * 10;
                curTime += t.Item3 * 10;
            }
            if (min.Item4 >= 0 && max.Item4 >= 0)
            {
                minTime += min.Item4;
                maxTime += max.Item4;
                curTime += t.Item4;
            }
            return (minTime < maxTime) ? (minTime <= curTime && curTime < maxTime) : (minTime <= curTime || curTime < maxTime);
        }

        private static bool CheckTimeCondition(string arg)
        {
            var reg = Regex.Match(arg, @"^([0-9Xx]{2}:[0-9Xx]{2})\s*-\s*([0-9Xx]{2}:[0-9Xx]{2})$");
            if (reg.Groups.Count == 3)
                return IsTimeBetween(GetEorzeaTime().ToString("HH:mm"), reg.Groups[1].Value, reg.Groups[2].Value);
            else
                return false;
        }

        public static bool CheckCache()
        {
            if (QoLBar.GetDrawTime() > (_lastCache + (QoLBar.Config.NoConditionCache ? 0 : 0.1f)))
            {
                ClearCache();
                _lastCache = QoLBar.GetDrawTime();
                return true;
            }
            else
                return false;
        }

        public static void ClearCache() => _conditionCache.Clear();
    }
}