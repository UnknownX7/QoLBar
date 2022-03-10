using Dalamud.Game.ClientState.Conditions;

namespace QoLBar.Conditions;

public class CurrentJobPreset : IConditionSetPreset
{
    public string Name => "Current Job";
    public CndSetCfg Generate()
    {
        var jobRow = DalamudApi.ClientState.LocalPlayer?.ClassJob;
        if (jobRow == null) return null;

        var set = new CndSetCfg { Name = jobRow.GameData?.Abbreviation };
        set.Conditions.Add(new() { ID = new JobCondition().ID, Arg = jobRow.Id });

        return set;
    }
}

public class CurrentRolePreset : IConditionSetPreset
{
    public string Name => "Current Role";
    public CndSetCfg Generate()
    {
        var jobRow = DalamudApi.ClientState.LocalPlayer?.ClassJob;
        if (jobRow == null) return null;

        var role = jobRow.GameData?.Role;
        var set = new CndSetCfg { Name = role is > 0 and < 5 ? RoleCondition.roleDictionary[(int)role] : string.Empty };
        set.Conditions.Add(new() { ID = new RoleCondition().ID, Arg = role });

        return set;
    }
}

public class AllCurrentConditionFlagsPreset : IConditionSetPreset
{
    public string Name => "All currently active condition flags";
    public CndSetCfg Generate()
    {
        var set = new CndSetCfg { Name = "Condition Flags Active" };

        for (int i = 0; i < Condition.MaxConditionEntries; i++)
        {
            if (!DalamudApi.Condition[i]) continue;
            set.Conditions.Add(new() { ID = ConditionFlagCondition.constID, Arg = i });
        }

        return set;
    }
}

public class OutOfCombatPreset : IConditionSetPreset
{
    public string Name => "Out of Combat";
    public CndSetCfg Generate()
    {
        var set = new CndSetCfg { Name = "Out of Combat" };
        set.Conditions.Add(new() { ID = ConditionFlagCondition.constID, Arg = (int)ConditionFlag.InCombat, Negate = true });
        return set;
    }
}

public class OutofTheWayPreset : IConditionSetPreset
{
    public string Name => "Not in most content, cutscenes, or loading";
    public CndSetCfg Generate()
    {
        var set = new CndSetCfg { Name = "Out of the Way" };
        set.Conditions.Add(new() { ID = ConditionFlagCondition.constID, Arg = (int)ConditionFlag.BoundByDuty, Negate = true });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 732, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 763, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 795, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 827, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 920, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = new ZoneCondition().ID, Arg = 975, Operator = ConditionManager.BinaryOperator.OR });
        set.Conditions.Add(new() { ID = ConditionFlagCondition.constID, Arg = (int)ConditionFlag.BetweenAreas, Negate = true });
        set.Conditions.Add(new() { ID = ConditionFlagCondition.constID, Arg = (int)ConditionFlag.OccupiedInCutSceneEvent, Negate = true });

        return set;
    }
}