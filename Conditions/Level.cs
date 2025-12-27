using System;
using Dalamud.Bindings.ImGui;

namespace QoLBar.Conditions;

[AttributeUsage(AttributeTargets.Class)]
public class LevelConditionAttribute : Attribute, IConditionCategory
{
    public string CategoryName => "Level";
    public int DisplayPriority => 0;
}

[LevelCondition]
public class LevelGreaterEqualCondition : ICondition, IDrawableCondition, IArgCondition
{
    public string ID => "lge";
    public string ConditionName => "is greater than or equal to";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => DalamudApi.ClientState.LocalPlayer is { } player && player.Level >= (uint)arg;
    public string GetTooltip(CndCfg cndCfg) => "Ctrl + Left Click to enter a value as text";
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg) 
    {
        var _ = (int)cndCfg.Arg;
        if (ImGui.SliderInt("##LevelSlider", ref _, 1, 100))
        {
            cndCfg.Arg = _;
        }
    }
    public dynamic GetDefaultArg(CndCfg cndCfg) => DalamudApi.ClientState.LocalPlayer?.Level ?? 0;
}

[LevelCondition]
public class LevelLessCondition : ICondition, IDrawableCondition, IArgCondition
{
    public string ID => "llt";
    public string ConditionName => "is less than";
    public int DisplayPriority => 0;
    public bool Check(dynamic arg) => DalamudApi.ClientState.LocalPlayer is { } player && player.Level < (uint)arg;
    public string GetTooltip(CndCfg cndCfg) => "Ctrl + Left Click to enter a value as text";
    public string GetSelectableTooltip(CndCfg cndCfg) => null;
    public void Draw(CndCfg cndCfg) 
    {
        var _ = (int)cndCfg.Arg;
        if (ImGui.SliderInt("##LevelSlider", ref _, 1, 100))
        {
            cndCfg.Arg = _;
        }
    }
    public dynamic GetDefaultArg(CndCfg cndCfg) => DalamudApi.ClientState.LocalPlayer?.Level ?? 0;
}