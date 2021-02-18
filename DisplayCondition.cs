using System;
using System.Numerics;
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
            Misc
        }

        public ConditionType Type = ConditionType.ConditionFlag;
        public int Condition = 0;
        public dynamic Arg = 0;

        public bool CheckCondition()
        {
            return Type switch
            {
                ConditionType.ConditionFlag => ConditionCache.GetCondition(Condition),
                ConditionType.Job => ConditionCache.GetCondition(200 + Condition),
                ConditionType.Role => ConditionCache.GetCondition(400 + Condition),
                ConditionType.Misc => ConditionCache.GetCondition(1000 + Condition, Arg),
                _ => false,
            };
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
            [0] = "No role",
            [1] = "Tank",
            [2] = "Melee DPS",
            [3] = "Ranged DPS",
            [4] = "Healer",
            [30] = "DoW",
            [31] = "DoM",
            [32] = "DoL",
            [33] = "DoH"
        };

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
                        if (ImGui.BeginCombo("##Type", names[(int)cond.Type]))
                        {
                            for (int n = 0; n < names.Length; n++)
                            {
                                if (ImGui.Selectable(names[n], n == (int)cond.Type))
                                {
                                    cond.Type = (DisplayCondition.ConditionType)n;
                                    config.Save();
                                }
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
                                break;
                            case DisplayCondition.ConditionType.Job:
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
                                break;
                            case DisplayCondition.ConditionType.Role:
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
                                break;
                            case DisplayCondition.ConditionType.Misc:
                                var opts = new string[]
                                {
                                    "Logged in",
                                    "Character ID"
                                };

                                if (ImGui.BeginCombo("##Misc", (0 <= cond.Condition && cond.Condition < opts.Length) ? opts[cond.Condition] : string.Empty))
                                {
                                    if (ImGui.Selectable(opts[0], cond.Condition == 0))
                                    {
                                        cond.Condition = 0;
                                        cond.Arg = 0;
                                        config.Save();
                                    }

                                    if (ImGui.Selectable(opts[1], cond.Condition == 1))
                                    {
                                        cond.Condition = 1;
                                        cond.Arg = QoLBar.Interface.ClientState.LocalContentId;
                                        config.Save();
                                    }
                                    if (ImGui.IsItemHovered())
                                        ImGui.SetTooltip("Selecting this will assign the current character's ID to this condition.");

                                    ImGui.EndCombo();
                                }
                                if (cond.Condition == 1 && ImGui.IsItemHovered())
                                    ImGui.SetTooltip($"ID: {cond.Arg}");
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
        private static readonly Dictionary<(int, dynamic), bool> _conditionCache = new Dictionary<(int, dynamic), bool>();
        private static float _lastCache = 0;
        public static float GetLastCache() => _lastCache;

        public static bool GetCondition(int cond, dynamic arg = null)
        {
            var pluginInterface = QoLBar.Interface;
            CheckCache();

            if (_conditionCache.TryGetValue((cond, arg), out var b))
                return b;
            else
            {
                if (cond < 200)
                {
                    b = pluginInterface.ClientState.Condition[(ConditionFlag)cond];
                }
                else if (cond < 400)
                {
                    var player = pluginInterface.ClientState.LocalPlayer;
                    b = (player != null) && (player.ClassJob.Id == (cond - 200));
                }
                else if (cond < 600)
                {
                    var player = pluginInterface.ClientState.LocalPlayer;
                    b = (player != null) && pluginInterface.Data.IsDataReady && ((((cond - 400) < 30) ? player.ClassJob.GameData.Role : player.ClassJob.GameData.ClassJobCategory.Row) == (cond - 400));
                }
                else
                {
                    b = cond switch
                    {
                        1000 => pluginInterface.ClientState.Condition.Any(),
                        1001 => (ulong)arg == pluginInterface.ClientState.LocalContentId,
                        _ => false,
                    };
                }

                _conditionCache[(cond, arg)] = b;
                return b;
            }
        }

        public static bool CheckCache()
        {
            if (QoLBar.GetDrawTime() >= (_lastCache + 0.1f)) // Somewhat expensive, only run 10x/sec
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