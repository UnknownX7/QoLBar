using System;
using ImGuiNET;

namespace QoLBar.Conditions;

public class ZoneCondition : ICondition, IDrawableCondition, IArgCondition, IConditionCategory
{
    public string ID => "z";
    public string ConditionName => "Zone";
    public string CategoryName => "Zone";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => DalamudApi.ClientState.TerritoryType == (ushort)arg;
    public string GetTooltip(CndCfg cndCfg) => null;
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg)
    {
        static string formatName(Lumina.Excel.GeneratedSheets.TerritoryType t) => $"[{t.RowId}] {t.PlaceName.Value?.Name}";
        if (!ImGuiEx.ExcelSheetCombo<Lumina.Excel.GeneratedSheets.TerritoryType>("##Zone", out var territory, s => formatName(s.GetRow((uint)cndCfg.Arg)),
            ImGuiComboFlags.None, (t, s) => formatName(t).Contains(s, StringComparison.CurrentCultureIgnoreCase),
            t => ImGui.Selectable(formatName(t), cndCfg.Arg == t.RowId))) return;
        cndCfg.Arg = territory.RowId;
        QoLBar.Config.Save();
    }
    // This list is completely and utterly awful so help people out a little bit
    public dynamic GetDefaultArg(CndCfg cndCfg) => DalamudApi.ClientState.TerritoryType;
}