using ImGuiNET;

namespace QoLBar.Conditions
{
    public class ConditionSetCondition : ICondition, IDrawableCondition, IConditionCategory
    {
        public string ID => "cs";
        public string ConditionName => "Condition Set";
        public string CategoryName => "Condition Set";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => ConditionManager.CheckConditionSet((int)arg);
        public string GetTooltip(CndCfg cndCfg) => null;
        public string GetSelectableTooltip(CndCfg cndCfg) => null;
        public void Draw(CndCfg cndCfg)
        {
            var i = (int)cndCfg.Arg;
            if (!ImGui.BeginCombo("##Sets", (i >= 0 && i < QoLBar.Config.CndSets.Count) ? $"[{i + 1}] {QoLBar.Config.CndSets[i].Name}" : string.Empty)) return;

            for (int ind = 0; ind < QoLBar.Config.CndSets.Count; ind++)
            {
                var s = QoLBar.Config.CndSets[ind];
                if (!ImGui.Selectable($"[{ind + 1}] {s.Name}", ind == i)) continue;

                cndCfg.Arg = ind;
                QoLBar.Config.Save();
            }
            ImGui.EndCombo();
        }
    }
}