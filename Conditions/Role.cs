using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace QoLBar.Conditions;

public class RoleCondition : ICondition, IDrawableCondition, IArgCondition, IConditionCategory
{
    public static readonly Dictionary<int, string> roleDictionary = new()
    {
        [1] = "Tank",
        [2] = "Melee DPS",
        [3] = "Ranged DPS",
        [4] = "Healer",
        [30] = "DoW",
        [31] = "DoM",
        [32] = "DoL",
        [33] = "DoH"
    };

    public string ID => "r";
    public string ConditionName => "Role";
    public string CategoryName => "Role";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => DalamudApi.ObjectTable.LocalPlayer is { } player
        && ((uint)arg < 30 ? player.ClassJob.ValueNullable?.Role : player.ClassJob.ValueNullable?.ClassJobCategory.RowId) == (uint)arg;
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg)
    {
        roleDictionary.TryGetValue((int)cndCfg.Arg, out string s);
        if (!ImGui.BeginCombo("##Role", s)) return;

        foreach (var (id, name) in roleDictionary)
        {
            if (!ImGui.Selectable(name, id == cndCfg.Arg)) continue;

            cndCfg.Arg = id;
            QoLBar.Config.Save();
        }
        ImGui.EndCombo();
    }
    public dynamic GetDefaultArg(CndCfg cndCfg) => roleDictionary.FirstOrDefault(kv => Check(kv.Key)).Key;
}