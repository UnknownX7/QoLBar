using ImGuiNET;

namespace QoLBar.Conditions
{
    public class JobCondition : ICondition, IDrawableCondition, IArgCondition, IConditionCategory
    {
        public string ID => "j";
        public string ConditionName => "Job";
        public string CategoryName => "Job";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => DalamudApi.ClientState.LocalPlayer is { } player && player.ClassJob.Id == (uint)arg;
        public string GetTooltip(CndCfg cndCfg) => null;
        public string GetSelectableTooltip(CndCfg cndCfg) => "Advanced condition.";
        public void Draw(CndCfg cndCfg)
        {
            var jobs = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.ClassJob>();
            if (jobs == null) return;

            var r = jobs.GetRow((uint)cndCfg.Arg);
            if (!ImGui.BeginCombo("##Job", r?.Abbreviation.ToString())) return;

            foreach (var j in jobs)
            {
                if (j.RowId == 0) continue;
                if (!ImGui.Selectable(j.Abbreviation.ToString(), j.RowId == cndCfg.Arg)) continue;

                cndCfg.Arg = j.RowId;
                QoLBar.Config.Save();
            }
            ImGui.EndCombo();
        }
        public dynamic GetDefaultArg(CndCfg cndCfg) => DalamudApi.ClientState.LocalPlayer is { } player ? player.ClassJob.Id : 0;
    }
}