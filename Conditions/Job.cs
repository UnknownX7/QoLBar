namespace QoLBar.Conditions
{
    public class JobCondition : ICondition, IConditionCategory
    {
        public string ID => "j";
        public string ConditionName => "Job";
        public string CategoryName => "Job";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => DalamudApi.ClientState.LocalPlayer is { } player && player.ClassJob.Id == (uint)arg;
    }
}