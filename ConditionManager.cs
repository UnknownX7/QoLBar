using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace QoLBar
{
    public interface IDisplayPriority
    {
        public int DisplayPriority { get; }
    }

    public interface ICondition : IDisplayPriority
    {
        public string ID { get; }
        public string ConditionName { get; }
        public bool Check(dynamic arg);
    }

    public interface IDrawableCondition
    {
        public string GetTooltip(CndCfg cndCfg);
        public string GetSelectableTooltip(CndCfg cndCfg);
        public void Draw(CndCfg cndCfg);
    }

    public interface IArgCondition
    {
        public dynamic GetDefaultArg(CndCfg cndCfg);
    }

    public interface IOnImportCondition
    {
        public void OnImport(CndCfg cndCfg);
    }

    public interface IConditionCategory : IDisplayPriority
    {
        public string CategoryName { get; }
    }

    public interface IConditionSetPreset
    {
        public string Name { get; }
        public CndSetCfg Generate();
    }

    public static class ConditionManager
    {
        public enum BinaryOperator
        {
            AND,
            OR,
            EQUALS,
            XOR
        }

        private static readonly Dictionary<string, ICondition> conditions = new();
        private static readonly Dictionary<ICondition, IConditionCategory> categoryMap = new();
        private static readonly Dictionary<(ICondition, dynamic), bool> conditionCache = new();
        private static readonly Dictionary<CndSetCfg, (bool prev, float time)> conditionSetCache = new();
        private static readonly Dictionary<CndSetCfg, List<bool>> debugSteps = new();
        private static readonly HashSet<CndSetCfg> lockedSets = new();
        private static float lastConditionCache = 0;

        public static List<(IConditionCategory category, List<ICondition> conditions)> ConditionCategories { get; private set; } = new();
        public static List<IConditionSetPreset> Presets { get; } = new();

        public static void Initialize()
        {
            foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsAssignableTo(typeof(IConditionCategory)) && !t.IsInterface))
            {
                var category = (IConditionCategory)Activator.CreateInstance(t);
                if (category == null) continue;

                var list = new List<ICondition>();
                ConditionCategories.Add((category, list));
                if (!t.IsAssignableTo(typeof(ICondition))) continue;

                list.Add((ICondition)category);
            }

            foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsAssignableTo(typeof(ICondition)) && !t.IsInterface))
            {
                var condition = (ICondition)Activator.CreateInstance(t);
                if (condition == null) continue;

                conditions.Add(condition.ID, condition);

                var categoryType = t.GetCustomAttributes().FirstOrDefault(attr => attr.GetType().IsAssignableTo(typeof(IConditionCategory)))?.GetType();
                if (categoryType == null)
                {
                    if (t.IsAssignableTo(typeof(IConditionCategory)))
                        categoryMap.Add(condition, (IConditionCategory)condition);
                    continue;
                }

                var (category, list) = ConditionCategories.FirstOrDefault(tuple => tuple.category.GetType() == categoryType);
                if (category == null) continue;

                list.Add(condition);
                categoryMap.Add(condition, category);
            }

            ConditionCategories = ConditionCategories.OrderBy(t => t.category.DisplayPriority).ToList();
            for (int i = 0; i < ConditionCategories.Count; i++)
            {
                var (category, list) = ConditionCategories[i];
                ConditionCategories[i] = (category, list.OrderBy(c => c.DisplayPriority).ToList());
            }

            foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsAssignableTo(typeof(IConditionSetPreset)) && !t.IsInterface))
            {
                var preset = (IConditionSetPreset)Activator.CreateInstance(t);
                if (preset == null) continue;
                Presets.Add(preset);
            }
        }

        public static ICondition GetCondition(string id) => conditions.TryGetValue(id, out var condition) ? condition : null;

        public static IConditionCategory GetConditionCategory(ICondition condition) => categoryMap[condition];

        public static IConditionCategory GetConditionCategory(string id) => GetConditionCategory(GetCondition(id));

        public static bool CheckCondition(string id, dynamic arg = null, bool negate = false)
        {
            var condition = GetCondition(id);
            return condition != null && (!negate ? CheckCondition(condition, arg) : !CheckCondition(condition, arg));
        }

        private static bool CheckCondition(ICondition condition, dynamic arg)
        {
            if (conditionCache.TryGetValue((condition, arg), out bool cache)) // ReSharper / Rider hates this being a var for some reason
                return cache;

            try
            {
                cache = condition.Check(arg);
            }
            catch
            {
                cache = false;
            }

            conditionCache[(condition, arg)] = cache;
            return cache;
        }

        private static bool CheckUnaryCondition(bool negate, ICondition condition, dynamic arg)
        {
            try
            {
                return !negate ? condition.Check(arg) : !condition.Check(arg);
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckBinaryCondition(bool prev, BinaryOperator op, bool negate, ICondition condition, dynamic arg)
        {
            return op switch
            {
                BinaryOperator.AND => prev && CheckUnaryCondition(negate, condition, arg),
                BinaryOperator.OR => prev || CheckUnaryCondition(negate, condition, arg),
                BinaryOperator.EQUALS => prev == CheckUnaryCondition(negate, condition, arg),
                BinaryOperator.XOR => prev ^ CheckUnaryCondition(negate, condition, arg),
                _ => prev
            };
        }

        public static bool CheckConditionSet(int i) => i >= 0 && i < QoLBar.Config.CndSetCfgs.Count && CheckConditionSet(QoLBar.Config.CndSetCfgs[i]);

        public static bool CheckConditionSet(CndSetCfg set)
        {
            if (lockedSets.Contains(set))
                return conditionSetCache.TryGetValue(set, out var c) && c.prev;

            if (conditionSetCache.TryGetValue(set, out var cache) && QoLBar.RunTime <= cache.time + (QoLBar.Config.NoConditionCache ? 0 : 0.1f))
                return cache.prev;

            lockedSets.Add(set);

            var first = true;
            var prev = true;
            var steps = new List<bool>();
            foreach (var cnd in set.Conditions)
            {
                var condition = GetCondition(cnd.ID);
                if (condition == null) continue;

                if (first)
                {
                    prev = CheckUnaryCondition(cnd.Negate, condition, cnd.Arg);
                    first = false;
                }
                else
                {
                    prev = CheckBinaryCondition(prev, cnd.Operator, cnd.Negate, condition, cnd.Arg);
                }

                steps.Add(prev);
            }

            lockedSets.Remove(set);

            conditionSetCache[set] = (prev, QoLBar.RunTime);
            debugSteps[set] = steps;
            return prev;
        }

        public static List<bool> GetDebugSteps(CndSetCfg set) => debugSteps.TryGetValue(set, out var steps) ? steps : null;

        public static void UpdateCache()
        {
            if (QoLBar.Config.NoConditionCache)
            {
                conditionCache.Clear();
                return;
            }

            if (QoLBar.RunTime < lastConditionCache + 0.1f) return;

            conditionCache.Clear();
            lastConditionCache = QoLBar.RunTime;
        }

        public static void SwapConditionSet(int from, int to)
        {
            var set = QoLBar.Config.CndSetCfgs[from];

            foreach (var bar in QoLBar.Config.BarCfgs)
            {
                if (bar.ConditionSet == from)
                    bar.ConditionSet = to;
                else if (bar.ConditionSet == to)
                    bar.ConditionSet = from;
            }

            foreach (var condition in from s in QoLBar.Config.CndSetCfgs from condition in s.Conditions where condition.ID == Conditions.ConditionSetCondition.constID select condition)
            {
                if (condition.Arg == from)
                    condition.Arg = to;
                else if (condition.Arg == to)
                    condition.Arg = from;
            }

            QoLBar.Config.CndSetCfgs.RemoveAt(from);
            QoLBar.Config.CndSetCfgs.Insert(to, set);
            QoLBar.Config.Save();

            IPC.MovedConditionSetProvider.SendMessage(from, to);
        }

        public static void RemoveConditionSet(int i)
        {
            foreach (var bar in QoLBar.Config.BarCfgs)
            {
                if (bar.ConditionSet > i)
                    bar.ConditionSet -= 1;
                else if (bar.ConditionSet == i)
                    bar.ConditionSet = -1;
            }

            foreach (var s in QoLBar.Config.CndSetCfgs)
            {
                for (int j = s.Conditions.Count - 1; j >= 0; j--)
                {
                    var cond = s.Conditions[j];
                    if (cond.ID != Conditions.ConditionSetCondition.constID) continue;

                    if (cond.Arg > i)
                        cond.Arg -= 1;
                    else if (cond.Arg == i)
                        s.Conditions.RemoveAt(j);
                }
            }

            QoLBar.Config.CndSetCfgs.RemoveAt(i);
            QoLBar.Config.Save();

            IPC.RemovedConditionSetProvider.SendMessage(i);
        }

        public static void ShiftCondition(CndSetCfg set, CndCfg cndCfg, bool increment)
        {
            var i = set.Conditions.IndexOf(cndCfg);
            if (!increment ? i <= 0 : i >= (set.Conditions.Count - 1)) return;

            var j = (increment ? i + 1 : i - 1);
            var condition = set.Conditions[i];
            set.Conditions.RemoveAt(i);
            set.Conditions.Insert(j, condition);
            QoLBar.Config.Save();
        }
    }
}