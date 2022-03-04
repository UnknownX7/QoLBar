namespace QoLBar.Conditions
{
    public class RoleCondition : ICondition, IConditionCategory
    {
        public string ID => "r";
        public string ConditionName => "Role";
        public string CategoryName => "Role";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => arg is not string
            && DalamudApi.DataManager.IsDataReady
            && DalamudApi.ClientState.LocalPlayer is { } player
            && ((uint)arg < 30 ? player.ClassJob.GameData?.Role : player.ClassJob.GameData?.ClassJobCategory.Row) == (uint)arg;
    }
}