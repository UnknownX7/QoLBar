namespace QoLBar.Conditions
{
    public class ConditionSetCondition : ICondition, IConditionCategory
    {
        public string ID => "cs";
        public string ConditionName => "Condition Set";
        public string CategoryName => "Condition Set";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => arg is not string && ConditionManager.CheckConditionSet((int)arg);
    }
}