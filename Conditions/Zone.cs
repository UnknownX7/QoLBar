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
        var territories = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType>();
        if (territories == null) return;

        var r = territories.GetRow((uint)cndCfg.Arg);
        if (ImGui.BeginCombo("##Zone", r?.PlaceName.Value?.Name.ToString()))
        {
            foreach (var t in territories)
            {
                if (ImGui.Selectable($"{t.PlaceName.Value?.Name}##{t.RowId}", t.RowId == cndCfg.Arg))
                {
                    cndCfg.Arg = t.RowId;
                    QoLBar.Config.Save();
                }
                ImGuiEx.SetItemTooltip($"ID: {t.RowId}");
            }
            ImGui.EndCombo();
        }
        ImGuiEx.SetItemTooltip($"ID: {cndCfg.Arg}");
    }
    // This list is completely and utterly awful so help people out a little bit
    public dynamic GetDefaultArg(CndCfg cndCfg) => DalamudApi.ClientState.TerritoryType;
}