using System;
using System.Collections.Generic;
using System.Text;

public enum SpellCastCostKind
{
    Alternative = 0,
    Additional = 1
}

public enum SpellCastDeclarationStage
{
    Modes = 0,
    AlternativeCost = 1,
    AdditionalCosts = 2,
    XValue = 3,
    Targets = 4,
    DiscardCost = 5,
    SacrificeCost = 6,
    ExileCost = 7,
    RevealCost = 8,
    Confirmation = 9,
    Complete = 10
}

[Flags]
public enum SpellTargetKind
{
    None = 0,
    Permanent = 1 << 0,
    Player = 1 << 1,
    Card = 1 << 2,
    Spell = 1 << 3,
    Ability = 1 << 4
}

[Flags]
public enum SpellTargetZone
{
    None = 0,
    Battlefield = 1 << 0,
    Graveyard = 1 << 1,
    Exile = 1 << 2,
    Hand = 1 << 3,
    Library = 1 << 4,
    Stack = 1 << 5
}

public enum SpellTargetControllerRelation
{
    Any = 0,
    You = 1,
    Opponent = 2
}

[Flags]
public enum SpellTargetRelation
{
    None = 0,
    Another = 1 << 0,
    DistinctAcrossGroups = 1 << 1,
    SharesCardType = 1 << 2,
    SharesColor = 1 << 3,
    SameController = 1 << 4,
    DifferentController = 1 << 5
}

[Serializable]
public sealed class SpellCastCostOption
{
    public string Id { get; }
    public string Label { get; }
    public SpellCastCostKind Kind { get; }
    public ScriptAbilityCost Cost { get; }
    public bool Optional { get; }

    public SpellCastCostOption(
        string id,
        string label,
        SpellCastCostKind kind,
        ScriptAbilityCost cost,
        bool optional)
    {
        Id = NormalizeId(id);
        Label = string.IsNullOrWhiteSpace(label) ? Id : label.Trim();
        Kind = kind;
        Cost = cost?.Clone() ?? new ScriptAbilityCost();
        Optional = optional;
    }

    private static string NormalizeId(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
    }
}

[Serializable]
public sealed class SpellCastModeDefinition
{
    public string Id { get; }
    public string Label { get; }

    public SpellCastModeDefinition(string id, string label)
    {
        Id = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        Label = string.IsNullOrWhiteSpace(label) ? Id : label.Trim();
    }
}

[Serializable]
public sealed class SpellTargetGroupDefinition
{
    public string Id { get; }
    public string Prompt { get; }
    public CardTargetMode TargetMode { get; }
    public int MinimumTargets { get; }
    public int MaximumTargets { get; }
    public string FilterHookName { get; }
    public bool DistinctWithinGroup { get; }
    public SpellTargetKind TargetKinds { get; }
    public SpellTargetZone AllowedZones { get; }
    public SpellTargetControllerRelation ControllerRelation { get; }
    public CardType RequiredCardTypes { get; }
    public ManaColorFlags RequiredColors { get; }
    public SpellTargetRelation Relations { get; }
    public string RelationGroupId { get; }

    public SpellTargetGroupDefinition(
        string id,
        string prompt,
        CardTargetMode targetMode,
        int minimumTargets,
        int maximumTargets,
        string filterHookName,
        bool distinctWithinGroup)
    {
        Id = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        Prompt = string.IsNullOrWhiteSpace(prompt) ? "Choose targets." : prompt.Trim();
        TargetMode = targetMode;
        MinimumTargets = Math.Max(0, minimumTargets);
        MaximumTargets = Math.Max(MinimumTargets, maximumTargets);
        FilterHookName = filterHookName?.Trim() ?? string.Empty;
        DistinctWithinGroup = distinctWithinGroup;
        TargetKinds = GetLegacyTargetKinds(targetMode);
        AllowedZones = TargetKinds.HasFlag(SpellTargetKind.Permanent)
            ? SpellTargetZone.Battlefield
            : SpellTargetZone.None;
        ControllerRelation = GetLegacyControllerRelation(targetMode);
        RequiredCardTypes = IsLegacyCreatureMode(targetMode)
            ? CardType.Creature
            : CardType.None;
        RequiredColors = ManaColorFlags.None;
        Relations = SpellTargetRelation.None;
        RelationGroupId = string.Empty;
    }

    public SpellTargetGroupDefinition(
        string id,
        string prompt,
        SpellTargetKind targetKinds,
        SpellTargetZone allowedZones,
        SpellTargetControllerRelation controllerRelation,
        CardType requiredCardTypes,
        ManaColorFlags requiredColors,
        int minimumTargets,
        int maximumTargets,
        string filterHookName,
        bool distinctWithinGroup,
        SpellTargetRelation relations,
        string relationGroupId)
    {
        Id = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        Prompt = string.IsNullOrWhiteSpace(prompt) ? "Choose targets." : prompt.Trim();
        TargetMode = CardTargetMode.None;
        TargetKinds = targetKinds;
        AllowedZones = allowedZones;
        ControllerRelation = controllerRelation;
        RequiredCardTypes = requiredCardTypes;
        RequiredColors = requiredColors;
        MinimumTargets = Math.Max(0, minimumTargets);
        MaximumTargets = Math.Max(MinimumTargets, maximumTargets);
        FilterHookName = filterHookName?.Trim() ?? string.Empty;
        DistinctWithinGroup = distinctWithinGroup;
        Relations = relations;
        RelationGroupId = relationGroupId?.Trim() ?? string.Empty;
    }

    public bool Allows(SpellTargetKind kind)
    {
        return (TargetKinds & kind) != 0;
    }

    public bool Allows(GameZone zone)
    {
        SpellTargetZone targetZone;
        switch (zone)
        {
            case GameZone.Battlefield: targetZone = SpellTargetZone.Battlefield; break;
            case GameZone.Graveyard: targetZone = SpellTargetZone.Graveyard; break;
            case GameZone.Exile: targetZone = SpellTargetZone.Exile; break;
            case GameZone.Hand: targetZone = SpellTargetZone.Hand; break;
            case GameZone.Library: targetZone = SpellTargetZone.Library; break;
            case GameZone.Stack: targetZone = SpellTargetZone.Stack; break;
            default: targetZone = SpellTargetZone.None; break;
        }
        return (AllowedZones & targetZone) != 0;
    }

    private static SpellTargetKind GetLegacyTargetKinds(CardTargetMode mode)
    {
        switch (mode)
        {
            case CardTargetMode.AnyPlayer:
            case CardTargetMode.OpponentPlayer:
                return SpellTargetKind.Player;
            case CardTargetMode.AnyCreatureOrPlayer:
            case CardTargetMode.OpponentCreatureOrPlayer:
                return SpellTargetKind.Permanent | SpellTargetKind.Player;
            case CardTargetMode.None:
                return SpellTargetKind.None;
            default:
                return SpellTargetKind.Permanent;
        }
    }

    private static SpellTargetControllerRelation GetLegacyControllerRelation(CardTargetMode mode)
    {
        switch (mode)
        {
            case CardTargetMode.FriendlyCreature:
            case CardTargetMode.FriendlyPermanent:
                return SpellTargetControllerRelation.You;
            case CardTargetMode.OpponentCreature:
            case CardTargetMode.OpponentPermanent:
            case CardTargetMode.OpponentPlayer:
            case CardTargetMode.OpponentCreatureOrPlayer:
                return SpellTargetControllerRelation.Opponent;
            default:
                return SpellTargetControllerRelation.Any;
        }
    }

    private static bool IsLegacyCreatureMode(CardTargetMode mode)
    {
        return mode == CardTargetMode.AnyCreature
            || mode == CardTargetMode.OpponentCreature
            || mode == CardTargetMode.FriendlyCreature
            || mode == CardTargetMode.AnyCreatureOrPlayer
            || mode == CardTargetMode.OpponentCreatureOrPlayer;
    }
}

[Serializable]
public sealed class SpellCastProfile
{
    private readonly List<SpellCastModeDefinition> modes =
        new List<SpellCastModeDefinition>();
    private readonly List<SpellCastCostOption> alternativeCosts =
        new List<SpellCastCostOption>();
    private readonly List<SpellCastCostOption> additionalCosts =
        new List<SpellCastCostOption>();
    private readonly List<SpellTargetGroupDefinition> targetGroups =
        new List<SpellTargetGroupDefinition>();

    public int MinimumModes { get; private set; }
    public int MaximumModes { get; private set; }
    public int MinimumX { get; private set; }
    public int MaximumX { get; private set; }
    public IReadOnlyList<SpellCastModeDefinition> Modes => modes;
    public IReadOnlyList<SpellCastCostOption> AlternativeCosts => alternativeCosts;
    public IReadOnlyList<SpellCastCostOption> AdditionalCosts => additionalCosts;
    public IReadOnlyList<SpellTargetGroupDefinition> TargetGroups => targetGroups;
    public bool UsesX => MaximumX > 0;

    public bool ConfigureModes(int minimum, int maximum)
    {
        if (minimum < 0 || maximum < minimum) return false;
        MinimumModes = minimum;
        MaximumModes = maximum;
        return true;
    }

    public bool ConfigureX(int minimum, int maximum)
    {
        if (minimum < 0 || maximum < minimum) return false;
        MinimumX = minimum;
        MaximumX = maximum;
        return true;
    }

    public bool AddMode(string id, string label)
    {
        if (string.IsNullOrWhiteSpace(id) || ContainsMode(id)) return false;
        modes.Add(new SpellCastModeDefinition(id, label));
        if (MaximumModes <= 0)
        {
            MinimumModes = 1;
            MaximumModes = 1;
        }
        return true;
    }

    public bool AddCost(SpellCastCostOption option)
    {
        if (option == null || string.IsNullOrEmpty(option.Id) || FindCost(option.Id) != null)
        {
            return false;
        }

        if (option.Kind == SpellCastCostKind.Alternative)
        {
            alternativeCosts.Add(option);
        }
        else
        {
            additionalCosts.Add(option);
        }
        return true;
    }

    public bool AddTargetGroup(SpellTargetGroupDefinition group)
    {
        if (group == null
            || string.IsNullOrEmpty(group.Id)
            || group.MaximumTargets <= 0
            || FindTargetGroup(group.Id) != null)
        {
            return false;
        }

        targetGroups.Add(group);
        return true;
    }

    public SpellCastCostOption FindCost(string id)
    {
        for (int i = 0; i < alternativeCosts.Count; i++)
        {
            if (IdsEqual(alternativeCosts[i].Id, id)) return alternativeCosts[i];
        }
        for (int i = 0; i < additionalCosts.Count; i++)
        {
            if (IdsEqual(additionalCosts[i].Id, id)) return additionalCosts[i];
        }
        return null;
    }

    public SpellTargetGroupDefinition FindTargetGroup(string id)
    {
        for (int i = 0; i < targetGroups.Count; i++)
        {
            if (IdsEqual(targetGroups[i].Id, id)) return targetGroups[i];
        }
        return null;
    }

    private bool ContainsMode(string id)
    {
        for (int i = 0; i < modes.Count; i++)
        {
            if (IdsEqual(modes[i].Id, id)) return true;
        }
        return false;
    }

    private static bool IdsEqual(string left, string right)
    {
        return string.Equals(left, right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

[Serializable]
public sealed class SpellTargetSelection
{
    public string GroupId { get; }
    public SpellTargetReference Target { get; }

    public SpellTargetSelection(string groupId, SpellTargetReference target)
    {
        GroupId = groupId?.Trim() ?? string.Empty;
        Target = target;
    }
}

[Serializable]
public sealed class SpellCastDeclaration
{
    private readonly List<string> selectedModeIds = new List<string>();
    private readonly List<string> selectedAdditionalCostIds = new List<string>();
    private readonly List<SpellTargetSelection> targets =
        new List<SpellTargetSelection>();

    public RuntimeCard Source { get; }
    public long SourceObjectId { get; }
    public int SourceZoneChangeVersion { get; }
    public int PlayerId { get; }
    public int SourcePlayerId { get; }
    public GameChoiceZone SourceZone { get; }
    public int SourceZoneIndex { get; }
    public CardFeatureRuntime Feature { get; }
    public bool WithoutPayingManaCost { get; }
    public int XValue { get; set; }
    public SpellCastDeclarationStage Stage { get; set; }
    public int TargetGroupIndex { get; set; }
    public string AlternativeCostId { get; set; } = string.Empty;
    public ScriptAbilityCost LockedCost { get; private set; }
    public bool IsCostLocked => LockedCost != null;
    public IReadOnlyList<string> SelectedModeIds => selectedModeIds;
    public IReadOnlyList<string> SelectedAdditionalCostIds => selectedAdditionalCostIds;
    public IReadOnlyList<SpellTargetSelection> Targets => targets;
    public bool HasTargets => targets.Count > 0;

    public SpellCastDeclaration(
        RuntimeCard source,
        int playerId,
        int sourcePlayerId,
        GameChoiceZone sourceZone,
        int sourceZoneIndex,
        CardFeatureRuntime feature,
        bool withoutPayingManaCost = false)
    {
        Source = source;
        SourceObjectId = source?.ObjectId ?? 0;
        SourceZoneChangeVersion = source?.ZoneChangeVersion ?? -1;
        PlayerId = playerId;
        SourcePlayerId = sourcePlayerId;
        SourceZone = sourceZone;
        SourceZoneIndex = sourceZoneIndex;
        Feature = feature;
        WithoutPayingManaCost = withoutPayingManaCost;
    }

    public bool HasCurrentSourceIdentity =>
        Source != null
        && Source.ObjectId == SourceObjectId
        && Source.ZoneChangeVersion == SourceZoneChangeVersion;

    public void SetModes(IEnumerable<string> modeIds)
    {
        selectedModeIds.Clear();
        AddUniqueIds(selectedModeIds, modeIds);
    }

    public void SetAdditionalCosts(IEnumerable<string> costIds)
    {
        selectedAdditionalCostIds.Clear();
        AddUniqueIds(selectedAdditionalCostIds, costIds);
    }

    public void ReplaceTargets(string groupId, IEnumerable<SpellTargetReference> values)
    {
        string normalized = groupId?.Trim() ?? string.Empty;
        targets.RemoveAll(target => string.Equals(
            target?.GroupId,
            normalized,
            StringComparison.OrdinalIgnoreCase));
        if (values == null) return;

        foreach (SpellTargetReference value in values)
        {
            if (value != null) targets.Add(new SpellTargetSelection(normalized, value));
        }
    }

    public IReadOnlyList<SpellTargetReference> GetTargets(string groupId)
    {
        var result = new List<SpellTargetReference>();
        for (int i = 0; i < targets.Count; i++)
        {
            SpellTargetSelection selection = targets[i];
            if (selection != null
                && string.Equals(
                    selection.GroupId,
                    groupId,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Add(selection.Target);
            }
        }
        return result;
    }

    public void LockCost(ScriptAbilityCost cost)
    {
        LockedCost = cost?.Clone() ?? new ScriptAbilityCost();
    }

    public SpellCastDeclaration CopyForSource(
        RuntimeCard source,
        int playerId = 0)
    {
        var copy = new SpellCastDeclaration(
            source,
            playerId > 0 ? playerId : PlayerId,
            SourcePlayerId,
            SourceZone,
            SourceZoneIndex,
            Feature,
            WithoutPayingManaCost)
        {
            XValue = XValue,
            Stage = Stage,
            TargetGroupIndex = TargetGroupIndex,
            AlternativeCostId = AlternativeCostId
        };
        copy.SetModes(selectedModeIds);
        copy.SetAdditionalCosts(selectedAdditionalCostIds);
        for (int i = 0; i < targets.Count; i++)
        {
            SpellTargetSelection selection = targets[i];
            if (selection?.Target == null) continue;
            copy.targets.Add(new SpellTargetSelection(
                selection.GroupId,
                selection.Target));
        }
        if (LockedCost != null) copy.LockCost(LockedCost);
        return copy;
    }

    public void AppendDeterministicState(StringBuilder builder)
    {
        if (builder == null) return;
        builder.Append(SourceObjectId).Append('/')
            .Append(SourceZoneChangeVersion).Append('/')
            .Append(PlayerId).Append('/')
            .Append(SourcePlayerId).Append('/')
            .Append((int)SourceZone).Append('/')
            .Append(SourceZoneIndex).Append('/')
            .Append(Feature?.FeatureIndex ?? -1).Append('/')
            .Append(WithoutPayingManaCost ? 1 : 0).Append('/')
            .Append(XValue).Append('/')
            .Append((int)Stage).Append('/')
            .Append(TargetGroupIndex).Append('/')
            .Append(AlternativeCostId).Append('/');
        AppendIds(builder, selectedModeIds);
        builder.Append('/');
        AppendIds(builder, selectedAdditionalCostIds);
        builder.Append('/');
        for (int i = 0; i < targets.Count; i++)
        {
            SpellTargetSelection selection = targets[i];
            SpellTargetReference target = selection?.Target;
            builder.Append(selection?.GroupId).Append('=')
                .Append(target?.Card?.ObjectId ?? 0).Append(':')
                .Append(target?.CardZoneChangeVersion ?? -1).Append(':')
                .Append(target?.PlayerID ?? 0).Append(':')
                .Append(target?.StackObjectId ?? 0).Append(':')
                .Append((int)(target?.DeclaredZone ?? GameZone.None)).Append(',');
        }
        builder.Append('/');
        if (LockedCost != null)
        {
            builder.Append(LockedCost.Mana.ToSymbolString()).Append(':')
                .Append(LockedCost.Life).Append(':')
                .Append(LockedCost.DiscardCount).Append(':')
                .Append(LockedCost.SacrificePermanentCount).Append(':')
                .Append(LockedCost.ExileHandCount).Append(':')
                .Append(LockedCost.RevealHandCount);
        }
    }

    private static void AddUniqueIds(List<string> target, IEnumerable<string> values)
    {
        if (values == null) return;
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            string normalized = value.Trim();
            if (!target.Exists(item => string.Equals(
                    item,
                    normalized,
                    StringComparison.OrdinalIgnoreCase)))
            {
                target.Add(normalized);
            }
        }
    }

    private static void AppendIds(StringBuilder builder, List<string> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            builder.Append(values[i]).Append(',');
        }
    }
}
