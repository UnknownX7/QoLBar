using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Logging;

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

    public interface IConditionCategory : IDisplayPriority
    {
        public string CategoryName { get; }
    }

    public static class ConditionManager
    {
        public enum BinaryOperator
        {
            AND,
            OR,
            XOR,
            EQUALS
        }

        private static readonly Dictionary<string, ICondition> conditions = new();
        private static List<(IConditionCategory category, List<ICondition> conditions)> categories = new();
        private static readonly Dictionary<(ICondition, dynamic), bool> conditionCache = new();
        private static readonly Dictionary<CndSet, (bool prev, float time)> conditionSetCache = new();
        private static readonly HashSet<CndSet> lockedSets = new();
        private static float lastConditionCache = 0;

        public static void Initialize()
        {
            foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsAssignableTo(typeof(IConditionCategory)) && !t.IsInterface))
            {
                var category = (IConditionCategory)Activator.CreateInstance(t);
                if (category == null) continue;

                var list = new List<ICondition>();
                categories.Add((category, list));
                if (!t.IsAssignableTo(typeof(ICondition))) continue;

                list.Add((ICondition)category);
            }

            foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsAssignableTo(typeof(ICondition)) && !t.IsInterface))
            {
                var condition = (ICondition)Activator.CreateInstance(t);
                if (condition == null) continue;

                conditions.Add(condition.ID, condition);

                var categoryType = t.GetCustomAttributes().FirstOrDefault(attr => attr.GetType().IsAssignableTo(typeof(IConditionCategory)))?.GetType();
                if (categoryType == null) continue;

                var (category, list) = categories.FirstOrDefault(tuple => tuple.category.GetType() == categoryType);
                if (category == null) continue;

                list.Add(condition);
            }

            categories = categories.OrderBy(t => t.category.DisplayPriority).ToList();
            for (int i = 0; i < categories.Count; i++)
            {
                var (category, list) = categories[i];
                categories[i] = (category, list.OrderBy(c => c.DisplayPriority).ToList());
            }

            foreach (var (attribute, list) in categories)
            {
                foreach (var condition in list)
                {
                    PluginLog.Error($"{attribute} -> {condition}");
                }
            }
        }

        public static ICondition GetCondition(string id) => conditions.TryGetValue(id, out var condition) ? condition : null;

        public static bool CheckCondition(string id, dynamic arg = null)
        {
            var condition = GetCondition(id);
            return condition != null && CheckCondition(condition, arg);
        }

        private static bool CheckCondition(ICondition condition, dynamic arg)
        {
            if (conditionCache.TryGetValue((condition, arg), out bool cache)) // ReSharper / Rider hates this being a var for some reason
                return cache;

            cache = condition.Check(arg);
            conditionCache[(condition, arg)] = cache;
            return cache;
        }

        private static bool CheckUnaryCondition(bool negate, ICondition condition, dynamic arg) => !negate ? condition.Check(arg) : !condition.Check(arg);

        private static bool CheckBinaryCondition(bool prev, BinaryOperator op, bool negate, ICondition condition, dynamic arg)
        {
            return op switch
            {
                BinaryOperator.AND => prev && CheckUnaryCondition(negate, condition, arg),
                BinaryOperator.OR => prev || CheckUnaryCondition(negate, condition, arg),
                BinaryOperator.XOR => prev ^ CheckUnaryCondition(negate, condition, arg),
                BinaryOperator.EQUALS => prev == CheckUnaryCondition(negate, condition, arg),
                _ => prev
            };
        }

        public static bool CheckConditionSet(int i) => i >= 0 && i < QoLBar.Config.CndSets.Count && CheckConditionSet(QoLBar.Config.CndSets[i]);

        public static bool CheckConditionSet(CndSet set)
        {
            if (lockedSets.Contains(set))
                return conditionSetCache.TryGetValue(set, out var c) && c.prev;

            if (conditionSetCache.TryGetValue(set, out var cache) && QoLBar.RunTime <= cache.time + (QoLBar.Config.NoConditionCache ? 0 : 0.1f))
                return cache.prev;

            lockedSets.Add(set);

            var first = true;
            var prev = true;
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
            }

            lockedSets.Remove(set);

            conditionSetCache[set] = (prev, QoLBar.RunTime);
            return prev;
        }

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
    }
}