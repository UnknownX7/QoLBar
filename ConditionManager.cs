using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace QoLBar
{
    public interface ICondition
    {
        public string ID { get; }
        public bool Check(dynamic arg);
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class MiscConditionAttribute : Attribute { }

    [MiscCondition]
    public class SomeCondition : ICondition
    {
        public string ID => "s";
        public bool Check(dynamic arg) => true;
    }

    [MiscCondition]
    public class IsLoggedInCondition : ICondition
    {
        public string ID => "l";
        public bool Check(dynamic arg) => DalamudApi.Condition.Any();
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
        private static readonly List<ICondition> miscConditions = new();
        private static readonly Dictionary<(ICondition, dynamic), bool> conditionCache = new();
        private static readonly Dictionary<CndSet, (bool prev, float time)> conditionSetCache = new();
        private static readonly HashSet<CndSet> lockedSets = new();
        private static float lastConditionCache = 0;

        public static void Initialize()
        {
            foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsAssignableTo(typeof(ICondition)) && !t.IsInterface))
            {
                var condition = (ICondition)Activator.CreateInstance(t);
                if (condition == null) continue;

                conditions.Add(condition.ID, condition);

                if (t.GetCustomAttribute<MiscConditionAttribute>() != null)
                    miscConditions.Add(condition);
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

        public static bool CheckConditionSet(int i)
        {
            return true;
        }

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