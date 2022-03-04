namespace QoLBar.Conditions
{
    public class RoleCondition : ICondition, IConditionCategory
    {
        public string ID => "r";
        public string ConditionName => "Role";
        public string CategoryName => "Role";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => true;
    }
}