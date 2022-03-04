using System;

namespace QoLBar.Conditions
{
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
        public bool Check(dynamic arg) => true;
    }

    [MiscCondition]
    public class HaveTargetCondition : ICondition
    {
        public string ID => "ht";
        public string ConditionName => "Have Target";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => true;
    }

    [MiscCondition]
    public class HaveFocusTargetCondition : ICondition
    {
        public string ID => "hf";
        public string ConditionName => "Have Focus Target";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => true;
    }

    [MiscCondition]
    public class WeaponDrawnCondition : ICondition
    {
        public string ID => "wd";
        public string ConditionName => "Weapon Drawn";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => true;
    }

    [MiscCondition]
    public class EorzeaTimespanCondition : ICondition
    {
        public string ID => "et";
        public string ConditionName => "Eorzea Timespan";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => true;
    }

    [MiscCondition]
    public class LocalTimespanCondition : ICondition
    {
        public string ID => "lt";
        public string ConditionName => "Local Timespan";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => true;
    }

    [MiscCondition]
    public class HUDLayoutCondition : ICondition
    {
        public string ID => "hl";
        public string ConditionName => "Current HUD Layout";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => true;
    }

    [MiscCondition]
    public class AddonExistsCondition : ICondition
    {
        public string ID => "ae";
        public string ConditionName => "Addon Exists";
        public int DisplayPriority => 100;
        public bool Check(dynamic arg) => true;
    }

    [MiscCondition]
    public class AddonVisibleCondition : ICondition
    {
        public string ID => "av";
        public string ConditionName => "Addon Visible";
        public int DisplayPriority => 101;
        public bool Check(dynamic arg) => true;
    }

    [MiscCondition]
    public class PluginCondition : ICondition
    {
        public string ID => "p";
        public string ConditionName => "Plugin Enabled";
        public int DisplayPriority => 102;
        public bool Check(dynamic arg) => true;
    }

}