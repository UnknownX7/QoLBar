using System;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Objects.Enums;

namespace QoLBar.Conditions
{
    public static class MiscConditionHelpers
    {
        public const string TimespanRegex = @"^([0-9Xx]{1,2}:[0-9Xx]{2})\s*-\s*([0-9Xx]{1,2}:[0-9Xx]{2})$";

        private static (double, double, double, double) ParseTime(string str) => str.Length switch
        {
            4 => (0, char.GetNumericValue(str[0]), char.GetNumericValue(str[2]), char.GetNumericValue(str[3])),
            5 => (char.GetNumericValue(str[0]), char.GetNumericValue(str[1]), char.GetNumericValue(str[3]), char.GetNumericValue(str[4])),
            _ => (0, 0, 0, 0)
        };

        public static bool IsTimeBetween(string tStr, string minStr, string maxStr)
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
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class MiscConditionAttribute : Attribute, IConditionCategory
    {
        public string CategoryName => "Misc";
        public int DisplayPriority => 100;
    }

    [MiscCondition]
    public class LoggedInCondition : ICondition
    {
        public string ID => "l";
        public string ConditionName => "Is Logged In";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => DalamudApi.Condition.Any();
    }

    [MiscCondition]
    public class CharacterCondition : ICondition
    {
        public string ID => "c";
        public string ConditionName => "Character ID";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => (ulong)arg == DalamudApi.ClientState.LocalContentId;
    }

    [MiscCondition]
    public class HaveTargetCondition : ICondition
    {
        public string ID => "ht";
        public string ConditionName => "Have Target";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => DalamudApi.TargetManager.Target != null;
    }

    [MiscCondition]
    public class HaveFocusTargetCondition : ICondition
    {
        public string ID => "hf";
        public string ConditionName => "Have Focus Target";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => DalamudApi.TargetManager.FocusTarget != null;
    }

    [MiscCondition]
    public class WeaponDrawnCondition : ICondition
    {
        public string ID => "wd";
        public string ConditionName => "Weapon Drawn";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => DalamudApi.ClientState.LocalPlayer is { } player && (player.StatusFlags & StatusFlags.WeaponOut) != 0;
    }

    [MiscCondition]
    public class EorzeaTimespanCondition : ICondition
    {
        public string ID => "et";
        public string ConditionName => "Eorzea Timespan";
        public int DisplayPriority => 0;

        private static bool CheckEorzeaTimeCondition(string arg)
        {
            var reg = Regex.Match(arg, MiscConditionHelpers.TimespanRegex);
            return reg.Success && MiscConditionHelpers.IsTimeBetween(Game.EorzeaTime.ToString("HH:mm"), reg.Groups[1].Value, reg.Groups[2].Value);
        }

        public bool Check(dynamic arg) => arg is string range && CheckEorzeaTimeCondition(range);
    }

    [MiscCondition]
    public class LocalTimespanCondition : ICondition
    {
        public string ID => "lt";
        public string ConditionName => "Local Timespan";
        public int DisplayPriority => 0;

        private static bool CheckLocalTimeCondition(string arg)
        {
            var reg = Regex.Match(arg, MiscConditionHelpers.TimespanRegex);
            return reg.Success && MiscConditionHelpers.IsTimeBetween(DateTime.Now.ToString("HH:mm"), reg.Groups[1].Value, reg.Groups[2].Value);
        }

        public bool Check(dynamic arg) => arg is string range && CheckLocalTimeCondition(range);
    }

    [MiscCondition]
    public class HUDLayoutCondition : ICondition
    {
        public string ID => "hl";
        public string ConditionName => "Current HUD Layout";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => (byte)arg == Game.CurrentHUDLayout;
    }

    [MiscCondition]
    public class AddonExistsCondition : ICondition
    {
        public string ID => "ae";
        public string ConditionName => "Addon Exists";
        public int DisplayPriority => 100;
        public unsafe bool Check(dynamic arg) => arg is string addon && Game.GetAddonStructByName(addon, 1) != null;
    }

    [MiscCondition]
    public class AddonVisibleCondition : ICondition
    {
        public string ID => "av";
        public string ConditionName => "Addon Visible";
        public int DisplayPriority => 101;
        public unsafe bool Check(dynamic arg) => arg is string addon && Game.GetAddonStructByName(addon, 1) is var atkBase && atkBase != null && atkBase->IsVisible;
    }

    [MiscCondition]
    public class PluginCondition : ICondition
    {
        public string ID => "p";
        public string ConditionName => "Plugin Enabled";
        public int DisplayPriority => 102;
        public bool Check(dynamic arg) => arg is string plugin && QoLBar.HasPlugin(plugin);
    }
}