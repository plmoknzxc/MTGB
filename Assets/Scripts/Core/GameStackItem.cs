using System;
using System.Collections.Generic;

public enum GameStackItemKind
{
    Spell = 0,
    TriggeredAbility = 1,
    ActivatedAbility = 2
}

[Serializable]
public sealed class GameStackItem
{
    public long StackObjectId { get; private set; }
    public GameStackItemKind Kind { get; }
    public RuntimeCard SourceCard { get; }
    public long SourceObjectId { get; }
    public int ControllerPlayerId { get; }
    public string HookName { get; }
    public CardFeatureRuntime SpellFeature { get; }
    public CardRuleEntryDefinition RuleEntry { get; }
    public int AbilityValue { get; }
    public GameEventContext EventContext { get; }
    public SpellCastDeclaration CastDeclaration { get; }
    public IReadOnlyList<SpellTargetReference> Targets => targets;

    private readonly List<SpellTargetReference> targets;

    private GameStackItem(
        GameStackItemKind kind,
        RuntimeCard sourceCard,
        int controllerPlayerId,
        string hookName,
        CardFeatureRuntime spellFeature,
        CardRuleEntryDefinition ruleEntry,
        int abilityValue,
        GameEventContext eventContext,
        IEnumerable<SpellTargetReference> capturedTargets = null,
        long sourceObjectId = 0,
        SpellCastDeclaration capturedCastDeclaration = null)
    {
        Kind = kind;
        SourceCard = sourceCard;
        SourceObjectId = sourceObjectId > 0
            ? sourceObjectId
            : sourceCard?.ObjectId ?? 0;
        ControllerPlayerId = controllerPlayerId;
        HookName = hookName ?? string.Empty;
        SpellFeature = spellFeature;
        RuleEntry = ruleEntry;
        AbilityValue = abilityValue;
        EventContext = eventContext;
        CastDeclaration = kind == GameStackItemKind.Spell
            ? capturedCastDeclaration?.CopyForSource(
                sourceCard,
                controllerPlayerId)
                ?? sourceCard?.CurrentCastDeclaration?.CopyForSource(
                    sourceCard,
                    controllerPlayerId)
            : null;
        targets = capturedTargets != null
            ? new List<SpellTargetReference>(capturedTargets)
            : sourceCard != null
                ? new List<SpellTargetReference>(sourceCard.SpellTargets)
                : new List<SpellTargetReference>();
    }

    public static GameStackItem Spell(RuntimeCard card, CardFeatureRuntime feature)
    {
        return new GameStackItem(
            GameStackItemKind.Spell,
            card,
            card != null ? card.ControllerPlayerID : 0,
            string.Empty,
            feature,
            null,
            0,
            null);
    }

    public static GameStackItem TriggeredAbility(RuntimeCard sourceCard, string hookName)
    {
        return new GameStackItem(
            GameStackItemKind.TriggeredAbility,
            sourceCard,
            sourceCard != null ? sourceCard.ControllerPlayerID : 0,
            hookName,
            null,
            null,
            0,
            null);
    }

    public static GameStackItem TriggeredAbility(
        RuntimeCard sourceCard,
        string hookName,
        GameEventContext eventContext)
    {
        return new GameStackItem(
            GameStackItemKind.TriggeredAbility,
            sourceCard,
            sourceCard != null ? sourceCard.ControllerPlayerID : 0,
            hookName,
            null,
            null,
            0,
            eventContext);
    }

    public static GameStackItem ActivatedAbility(RuntimeCard sourceCard, string hookName)
    {
        return new GameStackItem(
            GameStackItemKind.ActivatedAbility,
            sourceCard,
            sourceCard != null ? sourceCard.ControllerPlayerID : 0,
            hookName,
            null,
            null,
            0,
            null);
    }

    public static GameStackItem ChapterAbility(
        RuntimeCard sourceCard,
        CardFeatureRuntime feature,
        CardRuleEntryDefinition ruleEntry,
        string hookName,
        int chapter)
    {
        return new GameStackItem(
            GameStackItemKind.TriggeredAbility,
            sourceCard,
            sourceCard != null ? sourceCard.ControllerPlayerID : 0,
            hookName,
            feature,
            ruleEntry,
            chapter,
            null);
    }

    public static GameStackItem FeatureAbility(
        RuntimeCard sourceCard,
        CardFeatureRuntime feature,
        string hookName)
    {
        return new GameStackItem(
            GameStackItemKind.TriggeredAbility,
            sourceCard,
            sourceCard != null ? sourceCard.ControllerPlayerID : 0,
            hookName,
            feature,
            null,
            0,
            null);
    }

    public GameStackItem CopyForController(int controllerPlayerId)
    {
        if (controllerPlayerId != 1 && controllerPlayerId != 2) return null;

        return new GameStackItem(
            Kind,
            SourceCard,
            controllerPlayerId,
            HookName,
            SpellFeature,
            RuleEntry,
            AbilityValue,
            EventContext?.Clone(),
            targets,
            SourceObjectId,
            CastDeclaration);
    }

    public bool AssignStackObjectId(long stackObjectId)
    {
        if (stackObjectId <= 0 || StackObjectId > 0) return false;
        StackObjectId = stackObjectId;
        return true;
    }
}
