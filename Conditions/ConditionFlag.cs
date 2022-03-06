using System;
using Dalamud.Game.ClientState.Conditions;
using ImGuiNET;

namespace QoLBar.Conditions
{
    public class ConditionFlagCondition : ICondition, IDrawableCondition, IConditionCategory
    {
        public const string constID = "cf";
        private static readonly Array conditionFlags = Enum.GetValues(typeof(ConditionFlag));

        public string ID => constID;
        public string ConditionName => "Condition Flag";
        public string CategoryName => "Condition Flag";
        public int DisplayPriority => 0;
        public bool Check(dynamic arg) => DalamudApi.Condition[(ConditionFlag)arg];
        public string GetTooltip(CndCfg cndCfg) => null;
        public string GetSelectableTooltip(CndCfg cndCfg) => null;
        public void Draw(CndCfg cndCfg)
        {
            if (!ImGui.BeginCombo("##Flag", ((ConditionFlag)cndCfg.Arg).ToString())) return;

            foreach (ConditionFlag flag in conditionFlags)
            {
                if (!ImGui.Selectable(flag.ToString(), (int)flag == cndCfg.Arg)) continue;

                cndCfg.Arg = (int)flag;
                QoLBar.Config.Save();
            }
            ImGui.EndCombo();
        }
    }
}