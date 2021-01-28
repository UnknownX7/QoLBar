using System.Collections.Generic;
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

        public bool CheckCondition()
        {
            return Type switch
            {
                ConditionType.ConditionFlag => ConditionCache.GetCondition(Condition),
                ConditionType.Job => ConditionCache.GetCondition(200 + Condition),
                ConditionType.Role => ConditionCache.GetCondition(400 + Condition),
                ConditionType.Misc => ConditionCache.GetCondition(1000 + Condition),
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

        public bool CheckConditions(QoLBar plugin)
        {
            if (ConditionCache.GetLastCache() == _lastCache)
                return _cached;

            _currentPos = 0;
            _cached = Parse();
            _lastCache = ConditionCache.GetLastCache();
            return _cached;
        }
    }

    public static class ConditionCache
    {
        private static QoLBar plugin;
        private static readonly Dictionary<int, bool> _conditionCache = new Dictionary<int, bool>();
        private static float _lastCache = 0;
        public static float GetLastCache() => _lastCache;

        public static void Initialize(QoLBar p) => plugin = p;

        public static bool GetCondition(int cond)
        {
            CheckCache();

            if (_conditionCache.TryGetValue(cond, out var b))
                return b;
            else
            {
                if (cond < 200)
                {
                    b = plugin.pluginInterface.ClientState.Condition[(ConditionFlag)cond];
                }
                else if (cond < 400)
                {
                    var player = plugin.pluginInterface.ClientState.LocalPlayer;
                    b = (player != null) && (player.ClassJob.Id == (cond - 200));
                }
                else if (cond < 600)
                {
                    var player = plugin.pluginInterface.ClientState.LocalPlayer;
                    b = (player != null) && plugin.pluginInterface.Data.IsDataReady && (((cond < 30) ? player.ClassJob.GameData.Role : player.ClassJob.GameData.ClassJobCategory.Row) == (cond - 400));
                }
                else
                {
                    b = cond switch
                    {
                        1000 => plugin.pluginInterface.ClientState.Condition.Any(),
                        _ => false,
                    };
                }

                _conditionCache[cond] = b;
                return b;
            }
        }

        public static bool CheckCache()
        {
            if (plugin.GetDrawTime() >= (_lastCache + 0.1f)) // Somewhat expensive, only run 10x/sec
            {
                ClearCache();
                _lastCache = plugin.GetDrawTime();
                return true;
            }
            else
                return false;
        }

        public static void ClearCache() => _conditionCache.Clear();
    }
}