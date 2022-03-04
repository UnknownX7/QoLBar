namespace QoLBar.Conditions
{
    public class ConditionFlagCondition : ICondition, IConditionCategory
    {
        public string ID => "cf";
        public string ConditionName => "Condition Flag";
        public string CategoryName => "Condition Flag";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => true;
    }
}