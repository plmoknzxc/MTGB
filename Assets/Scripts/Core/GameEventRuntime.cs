using System;
using System.Collections.Generic;
using System.Text;

public enum GameZone
{
    None = 0,
    Library = 1,
    Hand = 2,
    Battlefield = 3,
    Graveyard = 4,
    Exile = 5,
    Stack = 6,
    Command = 7
}

public enum GameEventType
{
    None = 0,
    SpellCast = 1,
    SpellResolved = 2,
    PermanentEntered = 3,
    PermanentLeft = 4,
    ZoneChanged = 5,
    CardDrawn = 6,
    CardDiscarded = 7,
    CardDied = 8,
    CardTapped = 9,
    CardUntapped = 10,
    PhaseChanged = 11,
    AttackDeclared = 12,
    BlockDeclared = 13,
    Damage = 14,
    LifeChanged = 15,
    CounterChanged = 16,
    AbilityActivated = 17,
    AbilityResolved = 18,
    CardRevealed = 19,
    CardMilled = 20,
    PermanentSacrificed = 21,
    TokenCreated = 22
}

public enum GameEventReplacementOperation
{
    InvokeHook = 0,
    Prevent = 1,
    AddAmount = 2,
    MultiplyAmount = 3,
    SetAmount = 4,
    ChangeDestination = 5
}

public enum ReplacementEffectCategory
{
    General = 0,
    BackFaceUpAsEnters = 1,
    CopyAsEnters = 2,
    EntersUnderControl = 3,
    SelfReplacement = 4
}

public enum RuntimeEffectDuration
{
    Permanent = 0,
    UntilEndOfTurn = 1,
    WhileSourceRemains = 2,
    Once = 3
}

public enum RuleEffectOperation
{
    Add = 0,
    Set = 1,
    Minimum = 2,
    Maximum = 3,
    Allow = 4,
    Deny = 5
}

[Serializable]
public sealed class GameEventContext
{
    public long eventId;
    public GameEventType type;
    public RuntimeCard source;
    public RuntimeCard card;
    public long sourceObjectId;
    public long cardObjectId;
    public int sourceZoneChangeVersion = -1;
    public int cardZoneChangeVersion = -1;
    public int playerId;
    public int controllerId;
    public GameZone fromZone;
    public GameZone toZone;
    public int amount;
    public int originalAmount;
    public string detail;
    public DamageEventKind damageKind;
    public bool prevented;
    public bool replaced;
    public bool protectionPreventionApplied;
    public bool sourcePreventionApplied;
    public bool targetPreventionApplied;
    [NonSerialized] private HashSet<long> appliedReplacementIds;
    [NonSerialized] private HashSet<long> appliedDamageReplacementIds;

    public bool MatchesRegisteredType(GameEventType registeredType)
    {
        if (registeredType == type) return true;

        if (registeredType == GameEventType.ZoneChanged)
        {
            return type == GameEventType.PermanentEntered
                || type == GameEventType.PermanentLeft
                || type == GameEventType.CardDrawn
                || type == GameEventType.CardDiscarded
                || type == GameEventType.CardDied;
        }

        return registeredType == GameEventType.PermanentLeft
            && type == GameEventType.CardDied;
    }

    public void CaptureObjectIdentities()
    {
        if (sourceObjectId <= 0 && source != null)
        {
            sourceObjectId = source.ObjectId;
            sourceZoneChangeVersion = source.ZoneChangeVersion;
        }
        if (cardObjectId <= 0 && card != null)
        {
            cardObjectId = card.ObjectId;
            cardZoneChangeVersion = card.ZoneChangeVersion;
        }
    }

    public bool HasAppliedReplacement(long effectId)
    {
        return appliedReplacementIds != null && appliedReplacementIds.Contains(effectId);
    }

    public void MarkReplacementApplied(long effectId)
    {
        appliedReplacementIds ??= new HashSet<long>();
        appliedReplacementIds.Add(effectId);
    }

    public bool HasAppliedDamageReplacement(long effectId)
    {
        return appliedDamageReplacementIds != null
            && appliedDamageReplacementIds.Contains(effectId);
    }

    public void MarkDamageReplacementApplied(long effectId)
    {
        appliedDamageReplacementIds ??= new HashSet<long>();
        appliedDamageReplacementIds.Add(effectId);
    }

    public GameEventContext Clone()
    {
        var clone = new GameEventContext
        {
            eventId = eventId,
            type = type,
            source = source,
            card = card,
            sourceObjectId = sourceObjectId,
            cardObjectId = cardObjectId,
            sourceZoneChangeVersion = sourceZoneChangeVersion,
            cardZoneChangeVersion = cardZoneChangeVersion,
            playerId = playerId,
            controllerId = controllerId,
            fromZone = fromZone,
            toZone = toZone,
            amount = amount,
            originalAmount = originalAmount,
            detail = detail,
            damageKind = damageKind,
            prevented = prevented,
            replaced = replaced,
            protectionPreventionApplied = protectionPreventionApplied,
            sourcePreventionApplied = sourcePreventionApplied,
            targetPreventionApplied = targetPreventionApplied
        };
        if (appliedReplacementIds != null)
        {
            clone.appliedReplacementIds =
                new HashSet<long>(appliedReplacementIds);
        }
        if (appliedDamageReplacementIds != null)
        {
            clone.appliedDamageReplacementIds =
                new HashSet<long>(appliedDamageReplacementIds);
        }
        return clone;
    }
}

public abstract class ScriptRuntimeEffect
{
    public long Id { get; }
    public long Timestamp { get; }
    public RuntimeCard Source { get; }
    public int ControllerId { get; }
    public int SourceZoneChangeVersion { get; }
    public RuntimeEffectDuration Duration { get; }
    public string LinkId { get; }

    protected ScriptRuntimeEffect(
        long id,
        long timestamp,
        RuntimeCard source,
        RuntimeEffectDuration duration,
        string linkId = null)
    {
        Id = id;
        Timestamp = timestamp;
        Source = source;
        ControllerId = source?.ControllerPlayerID ?? 0;
        SourceZoneChangeVersion = source?.ZoneChangeVersion ?? -1;
        Duration = duration;
        LinkId = linkId?.Trim() ?? string.Empty;
    }

    public bool IsActive(Func<RuntimeCard, bool> isSourceActive)
    {
        if (Source == null) return false;
        if (Duration != RuntimeEffectDuration.WhileSourceRemains) return true;
        if (Source.ZoneChangeVersion != SourceZoneChangeVersion) return false;
        return isSourceActive != null && isSourceActive(Source);
    }
}

public sealed class ScriptContinuousRegistration : ScriptRuntimeEffect
{
    public ContinuousEffectOperation Operation { get; }
    public string FilterHookName { get; }
    public string DependencyKey { get; }
    public CardType CardTypes { get; }
    public ManaColorFlags Colors { get; }
    public string TextValue { get; }
    public int ValueA { get; }
    public int ValueB { get; }
    public bool CharacteristicDefining { get; }
    public ContinuousEffectCharacteristic Reads { get; }
    public ContinuousEffectCharacteristic Writes { get; }
    public RuntimeCard CopySource { get; }
    public CardSupertype Supertypes { get; }
    public Dictionary<RuntimeCard, long> AppliedEffectIds { get; } =
        new Dictionary<RuntimeCard, long>();

    public ScriptContinuousRegistration(
        long id,
        long timestamp,
        RuntimeCard source,
        RuntimeEffectDuration duration,
        ContinuousEffectOperation operation,
        string filterHookName,
        string dependencyKey,
        CardType cardTypes,
        ManaColorFlags colors,
        string textValue,
        int valueA,
        int valueB,
        string linkId,
        bool characteristicDefining = false,
        ContinuousEffectCharacteristic reads = ContinuousEffectCharacteristic.None,
        ContinuousEffectCharacteristic writes = ContinuousEffectCharacteristic.None,
        RuntimeCard copySource = null,
        CardSupertype supertypes = CardSupertype.None)
        : base(id, timestamp, source, duration, linkId)
    {
        Operation = operation;
        FilterHookName = filterHookName?.Trim() ?? string.Empty;
        DependencyKey = dependencyKey?.Trim() ?? string.Empty;
        CardTypes = cardTypes;
        Colors = colors;
        TextValue = textValue?.Trim() ?? string.Empty;
        ValueA = valueA;
        ValueB = valueB;
        CharacteristicDefining = characteristicDefining;
        Reads = reads;
        Writes = writes;
        CopySource = copySource;
        Supertypes = supertypes;
    }
}

public sealed class ScriptRuleRegistration : ScriptRuntimeEffect
{
    public string RuleKey { get; }
    public RuleEffectOperation Operation { get; }
    public int Value { get; }
    public int AffectedPlayerId { get; }
    public string FilterHookName { get; }

    public ScriptRuleRegistration(
        long id,
        long timestamp,
        RuntimeCard source,
        RuntimeEffectDuration duration,
        string ruleKey,
        RuleEffectOperation operation,
        int value,
        int affectedPlayerId,
        string filterHookName,
        string linkId)
        : base(id, timestamp, source, duration, linkId)
    {
        RuleKey = ruleKey?.Trim() ?? string.Empty;
        Operation = operation;
        Value = value;
        AffectedPlayerId = affectedPlayerId;
        FilterHookName = filterHookName?.Trim() ?? string.Empty;
    }
}

public sealed class ScriptTriggerRegistration : ScriptRuntimeEffect
{
    public GameEventType EventType { get; }
    public string HookName { get; }
    public string ConditionHookName { get; }
    public bool OtherCardsOnly { get; }

    public ScriptTriggerRegistration(
        long id,
        long timestamp,
        RuntimeCard source,
        RuntimeEffectDuration duration,
        GameEventType eventType,
        string hookName,
        string conditionHookName,
        bool otherCardsOnly,
        string linkId)
        : base(id, timestamp, source, duration, linkId)
    {
        EventType = eventType;
        HookName = hookName ?? string.Empty;
        ConditionHookName = conditionHookName ?? string.Empty;
        OtherCardsOnly = otherCardsOnly;
    }
}

public sealed class ScriptReplacementRegistration : ScriptRuntimeEffect
{
    public GameEventType EventType { get; }
    public GameEventReplacementOperation Operation { get; }
    public string HookName { get; }
    public string ConditionHookName { get; }
    public int Value { get; }
    public ReplacementEffectCategory Category { get; }

    public ScriptReplacementRegistration(
        long id,
        long timestamp,
        RuntimeCard source,
        RuntimeEffectDuration duration,
        GameEventType eventType,
        GameEventReplacementOperation operation,
        string hookName,
        int value,
        string conditionHookName,
        string linkId,
        ReplacementEffectCategory category)
        : base(id, timestamp, source, duration, linkId)
    {
        EventType = eventType;
        Operation = operation;
        HookName = hookName ?? string.Empty;
        ConditionHookName = conditionHookName?.Trim() ?? string.Empty;
        Value = value;
        Category = category;
    }
}

public sealed class GameEventPipeline
{
    private readonly List<ScriptTriggerRegistration> triggers =
        new List<ScriptTriggerRegistration>();
    private readonly List<ScriptReplacementRegistration> replacements =
        new List<ScriptReplacementRegistration>();
    private readonly List<ScriptContinuousRegistration> continuousEffects =
        new List<ScriptContinuousRegistration>();
    private readonly List<ScriptRuleRegistration> ruleEffects =
        new List<ScriptRuleRegistration>();
    private long nextEventId = 1;
    private long nextEffectId = 1;
    private long nextTimestamp = 1;

    public IReadOnlyList<ScriptTriggerRegistration> Triggers => triggers;
    public IReadOnlyList<ScriptReplacementRegistration> Replacements => replacements;
    public IReadOnlyList<ScriptContinuousRegistration> ContinuousEffects => continuousEffects;
    public IReadOnlyList<ScriptRuleRegistration> RuleEffects => ruleEffects;

    public ScriptTriggerRegistration RegisterTrigger(
        RuntimeCard source,
        GameEventType eventType,
        string hookName,
        string conditionHookName,
        RuntimeEffectDuration duration,
        bool otherCardsOnly,
        string linkId = null)
    {
        if (source == null || eventType == GameEventType.None || string.IsNullOrWhiteSpace(hookName))
        {
            return null;
        }

        var registration = new ScriptTriggerRegistration(
            nextEffectId++,
            nextTimestamp++,
            source,
            duration,
            eventType,
            hookName.Trim(),
            conditionHookName?.Trim(),
            otherCardsOnly,
            linkId);
        triggers.Add(registration);
        return registration;
    }

    public ScriptReplacementRegistration RegisterReplacement(
        RuntimeCard source,
        GameEventType eventType,
        GameEventReplacementOperation operation,
        string hookName,
        int value,
        RuntimeEffectDuration duration,
        string conditionHookName = null,
        string linkId = null,
        ReplacementEffectCategory category = ReplacementEffectCategory.General)
    {
        if (source == null || eventType == GameEventType.None) return null;

        var registration = new ScriptReplacementRegistration(
            nextEffectId++,
            nextTimestamp++,
            source,
            duration,
            eventType,
            operation,
            hookName?.Trim(),
            value,
            conditionHookName,
            linkId,
            category);
        replacements.Add(registration);
        return registration;
    }

    public ScriptContinuousRegistration RegisterContinuous(
        RuntimeCard source,
        ContinuousEffectOperation operation,
        string filterHookName,
        string dependencyKey,
        CardType cardTypes,
        ManaColorFlags colors,
        string textValue,
        int valueA,
        int valueB,
        RuntimeEffectDuration duration,
        string linkId,
        bool characteristicDefining = false,
        ContinuousEffectCharacteristic reads = ContinuousEffectCharacteristic.None,
        ContinuousEffectCharacteristic writes = ContinuousEffectCharacteristic.None,
        RuntimeCard copySource = null,
        CardSupertype supertypes = CardSupertype.None)
    {
        if (source == null) return null;
        var registration = new ScriptContinuousRegistration(
            nextEffectId++,
            nextTimestamp++,
            source,
            duration,
            operation,
            filterHookName,
            dependencyKey,
            cardTypes,
            colors,
            textValue,
            valueA,
            valueB,
            linkId,
            characteristicDefining,
            reads,
            writes,
            copySource,
            supertypes);
        continuousEffects.Add(registration);
        return registration;
    }

    public ScriptRuleRegistration RegisterRuleEffect(
        RuntimeCard source,
        string ruleKey,
        RuleEffectOperation operation,
        int value,
        int affectedPlayerId,
        string filterHookName,
        RuntimeEffectDuration duration,
        string linkId)
    {
        if (source == null || string.IsNullOrWhiteSpace(ruleKey)) return null;

        var registration = new ScriptRuleRegistration(
            nextEffectId++,
            nextTimestamp++,
            source,
            duration,
            ruleKey,
            operation,
            value,
            affectedPlayerId,
            filterHookName,
            linkId);
        ruleEffects.Add(registration);
        return registration;
    }

    public bool Remove(long effectId)
    {
        bool removed = triggers.RemoveAll(item => item.Id == effectId) > 0;
        removed = replacements.RemoveAll(item => item.Id == effectId) > 0 || removed;
        removed = continuousEffects.RemoveAll(item => item.Id == effectId) > 0 || removed;
        return ruleEffects.RemoveAll(item => item.Id == effectId) > 0 || removed;
    }

    public ScriptContinuousRegistration FindContinuous(long effectId)
    {
        return continuousEffects.Find(item => item.Id == effectId);
    }

    public int RemoveForSource(RuntimeCard source)
    {
        if (source == null) return 0;
        int removed = triggers.RemoveAll(item => ReferenceEquals(item.Source, source));
        removed += replacements.RemoveAll(item => ReferenceEquals(item.Source, source));
        removed += continuousEffects.RemoveAll(item => ReferenceEquals(item.Source, source));
        removed += ruleEffects.RemoveAll(item => ReferenceEquals(item.Source, source));
        return removed;
    }

    public int RemoveLinked(RuntimeCard source, string linkId)
    {
        if (source == null || string.IsNullOrWhiteSpace(linkId)) return 0;
        string normalizedLinkId = linkId.Trim();
        bool Matches(ScriptRuntimeEffect effect)
        {
            return ReferenceEquals(effect.Source, source)
                && string.Equals(
                    effect.LinkId,
                    normalizedLinkId,
                    StringComparison.Ordinal);
        }

        int removed = triggers.RemoveAll(item => Matches(item));
        removed += replacements.RemoveAll(item => Matches(item));
        removed += continuousEffects.RemoveAll(item => Matches(item));
        removed += ruleEffects.RemoveAll(item => Matches(item));
        return removed;
    }

    public GameEventContext Prepare(
        GameEventContext context,
        Func<RuntimeCard, bool> isOnBattlefield,
        Action<ScriptReplacementRegistration, GameEventContext> invokeReplacement)
    {
        return Prepare(
            context,
            isOnBattlefield,
            invokeReplacement,
            null,
            null);
    }

    public GameEventContext Prepare(
        GameEventContext context,
        Func<RuntimeCard, bool> isOnBattlefield,
        Action<ScriptReplacementRegistration, GameEventContext> invokeReplacement,
        Func<ScriptReplacementRegistration, GameEventContext, bool> shouldApply,
        Func<GameEventContext, IReadOnlyList<ScriptReplacementRegistration>,
            ScriptReplacementRegistration> chooseReplacement)
    {
        if (context == null) return null;
        InitializeEvent(context);

        while (true)
        {
            IReadOnlyList<ScriptReplacementRegistration> applicable =
                GetApplicableReplacements(context, isOnBattlefield, shouldApply);
            if (applicable.Count == 0) break;

            ScriptReplacementRegistration replacement =
                chooseReplacement?.Invoke(context, applicable);
            if (replacement == null || !ContainsReplacement(applicable, replacement))
            {
                replacement = applicable[0];
            }

            ApplyReplacementToEvent(replacement, context, invokeReplacement);
        }

        return context;
    }

    private static bool ContainsReplacement(
        IReadOnlyList<ScriptReplacementRegistration> registrations,
        ScriptReplacementRegistration replacement)
    {
        if (registrations == null || replacement == null) return false;
        for (int i = 0; i < registrations.Count; i++)
        {
            if (ReferenceEquals(registrations[i], replacement)) return true;
        }
        return false;
    }

    public void InitializeEvent(GameEventContext context)
    {
        if (context == null) return;
        context.CaptureObjectIdentities();
        if (context.eventId > 0) return;

        context.eventId = nextEventId++;
        context.originalAmount = context.amount;
    }

    public IReadOnlyList<ScriptReplacementRegistration> GetApplicableReplacements(
        GameEventContext context,
        Func<RuntimeCard, bool> isOnBattlefield,
        Func<ScriptReplacementRegistration, GameEventContext, bool> shouldApply)
    {
        var applicable = new List<ScriptReplacementRegistration>();
        if (context == null) return applicable;

        InitializeEvent(context);
        ReplacementEffectCategory highestCategory = ReplacementEffectCategory.General;
        bool hasApplicable = false;
        for (int i = 0; i < replacements.Count; i++)
        {
            ScriptReplacementRegistration candidate = replacements[i];
            if (!context.MatchesRegisteredType(candidate.EventType)
                || context.HasAppliedReplacement(candidate.Id)
                || !candidate.IsActive(isOnBattlefield)
                || (shouldApply != null && !shouldApply(candidate, context)))
            {
                continue;
            }

            if (!hasApplicable || candidate.Category > highestCategory)
            {
                highestCategory = candidate.Category;
                applicable.Clear();
                hasApplicable = true;
            }
            if (candidate.Category == highestCategory)
            {
                applicable.Add(candidate);
            }
        }

        applicable.Sort((left, right) =>
        {
            int timestampOrder = left.Timestamp.CompareTo(right.Timestamp);
            return timestampOrder != 0
                ? timestampOrder
                : left.Id.CompareTo(right.Id);
        });
        return applicable;
    }

    public bool ApplyReplacementToEvent(
        ScriptReplacementRegistration replacement,
        GameEventContext context,
        Action<ScriptReplacementRegistration, GameEventContext> invokeReplacement)
    {
        if (replacement == null
            || context == null
            || context.HasAppliedReplacement(replacement.Id)
            || !replacements.Contains(replacement))
        {
            return false;
        }

        context.MarkReplacementApplied(replacement.Id);
        ApplyReplacement(replacement, context, invokeReplacement);
        if (replacement.Duration == RuntimeEffectDuration.Once)
        {
            replacements.Remove(replacement);
        }
        return true;
    }

    public GameEventContext Complete(
        GameEventContext context,
        Func<RuntimeCard, bool> isOnBattlefield,
        Func<ScriptTriggerRegistration, GameEventContext, bool> shouldTrigger,
        Action<ScriptTriggerRegistration, GameEventContext> queueTrigger)
    {
        if (context == null) return null;
        context.CaptureObjectIdentities();
        if (context.eventId <= 0)
        {
            context.eventId = nextEventId++;
            context.originalAmount = context.amount;
        }

        triggers.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));
        for (int i = 0; i < triggers.Count; i++)
        {
            ScriptTriggerRegistration trigger = triggers[i];
            if (!context.MatchesRegisteredType(trigger.EventType)
                || !trigger.IsActive(isOnBattlefield)
                || (trigger.OtherCardsOnly && ReferenceEquals(trigger.Source, context.card))
                || (shouldTrigger != null && !shouldTrigger(trigger, context)))
            {
                continue;
            }

            queueTrigger?.Invoke(trigger, context.Clone());
            if (trigger.Duration == RuntimeEffectDuration.Once)
            {
                triggers.RemoveAt(i--);
            }
        }

        Cleanup(isOnBattlefield);
        return context;
    }

    public GameEventContext Publish(
        GameEventContext context,
        Func<RuntimeCard, bool> isOnBattlefield,
        Action<ScriptReplacementRegistration, GameEventContext> invokeReplacement,
        Func<ScriptTriggerRegistration, GameEventContext, bool> shouldTrigger,
        Action<ScriptTriggerRegistration, GameEventContext> queueTrigger)
    {
        return Publish(
            context,
            isOnBattlefield,
            invokeReplacement,
            null,
            null,
            shouldTrigger,
            queueTrigger);
    }

    public GameEventContext Publish(
        GameEventContext context,
        Func<RuntimeCard, bool> isOnBattlefield,
        Action<ScriptReplacementRegistration, GameEventContext> invokeReplacement,
        Func<ScriptReplacementRegistration, GameEventContext, bool> shouldApplyReplacement,
        Func<GameEventContext, IReadOnlyList<ScriptReplacementRegistration>,
            ScriptReplacementRegistration> chooseReplacement,
        Func<ScriptTriggerRegistration, GameEventContext, bool> shouldTrigger,
        Action<ScriptTriggerRegistration, GameEventContext> queueTrigger)
    {
        Prepare(
            context,
            isOnBattlefield,
            invokeReplacement,
            shouldApplyReplacement,
            chooseReplacement);
        return context.prevented
            ? context
            : Complete(context, isOnBattlefield, shouldTrigger, queueTrigger);
    }

    public void EndTurn()
    {
        triggers.RemoveAll(item => item.Duration == RuntimeEffectDuration.UntilEndOfTurn);
        replacements.RemoveAll(item => item.Duration == RuntimeEffectDuration.UntilEndOfTurn);
        continuousEffects.RemoveAll(
            item => item.Duration == RuntimeEffectDuration.UntilEndOfTurn);
        ruleEffects.RemoveAll(
            item => item.Duration == RuntimeEffectDuration.UntilEndOfTurn);
    }

    public void Clear()
    {
        triggers.Clear();
        replacements.Clear();
        continuousEffects.Clear();
        ruleEffects.Clear();
        nextEventId = 1;
        nextEffectId = 1;
        nextTimestamp = 1;
    }

    public void AppendDeterministicState(StringBuilder builder)
    {
        if (builder == null) return;
        triggers.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));
        replacements.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));
        builder.Append("EP[")
            .Append(nextEventId).Append('/')
            .Append(nextEffectId).Append('/')
            .Append(nextTimestamp).Append("]TR[");
        for (int i = 0; i < triggers.Count; i++)
        {
            ScriptTriggerRegistration trigger = triggers[i];
            builder.Append(trigger.Id).Append('/')
                .Append(trigger.Timestamp).Append('/')
                .Append(trigger.Source?.ObjectId ?? 0).Append('/')
                .Append(trigger.SourceZoneChangeVersion).Append('/')
                .Append(trigger.ControllerId).Append('/')
                .Append((int)trigger.Duration).Append('/')
                .Append((int)trigger.EventType).Append('/')
                .Append(trigger.HookName).Append('/')
                .Append(trigger.ConditionHookName).Append('/')
                .Append(trigger.OtherCardsOnly ? 1 : 0).Append('/')
                .Append(trigger.LinkId).Append(',');
        }
        builder.Append("]RP[");
        for (int i = 0; i < replacements.Count; i++)
        {
            ScriptReplacementRegistration replacement = replacements[i];
            builder.Append(replacement.Id).Append('/')
                .Append(replacement.Timestamp).Append('/')
                .Append(replacement.Source?.ObjectId ?? 0).Append('/')
                .Append(replacement.SourceZoneChangeVersion).Append('/')
                .Append(replacement.ControllerId).Append('/')
                .Append((int)replacement.Duration).Append('/')
                .Append((int)replacement.EventType).Append('/')
                .Append((int)replacement.Operation).Append('/')
                .Append(replacement.HookName).Append('/')
                .Append(replacement.ConditionHookName).Append('/')
                .Append(replacement.Value).Append('/')
                .Append((int)replacement.Category).Append('/')
                .Append(replacement.LinkId).Append(',');
        }
        builder.Append("]CE[");
        continuousEffects.Sort(CompareContinuousRegistrations);
        for (int i = 0; i < continuousEffects.Count; i++)
        {
            ScriptContinuousRegistration effect = continuousEffects[i];
            builder.Append(effect.Id).Append('/')
                .Append(effect.Timestamp).Append('/')
                .Append(effect.Source?.ObjectId ?? 0).Append('/')
                .Append(effect.SourceZoneChangeVersion).Append('/')
                .Append(effect.ControllerId).Append('/')
                .Append((int)effect.Duration).Append('/')
                .Append((int)effect.Operation).Append('/')
                .Append(effect.FilterHookName).Append('/')
                .Append(effect.DependencyKey).Append('/')
                .Append((int)effect.CardTypes).Append('/')
                .Append((int)effect.Colors).Append('/')
                .Append(effect.TextValue).Append('/')
                .Append(effect.ValueA).Append('/')
                .Append(effect.ValueB).Append('/')
                .Append(effect.CharacteristicDefining ? 1 : 0).Append('/')
                .Append((int)effect.Reads).Append('/')
                .Append((int)effect.Writes).Append('/')
                .Append(effect.CopySource?.ObjectId ?? 0).Append('/')
                .Append((int)effect.Supertypes).Append('/')
                .Append(effect.LinkId).Append('/');
            var applied = new List<KeyValuePair<RuntimeCard, long>>(
                effect.AppliedEffectIds);
            applied.Sort((left, right) =>
                (left.Key?.ObjectId ?? 0).CompareTo(right.Key?.ObjectId ?? 0));
            for (int appliedIndex = 0; appliedIndex < applied.Count; appliedIndex++)
            {
                KeyValuePair<RuntimeCard, long> pair = applied[appliedIndex];
                builder.Append(pair.Key?.ObjectId ?? 0).Append('=')
                    .Append(pair.Value).Append(';');
            }
            builder.Append(',');
        }
        builder.Append("]RE[");
        ruleEffects.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));
        for (int i = 0; i < ruleEffects.Count; i++)
        {
            ScriptRuleRegistration effect = ruleEffects[i];
            builder.Append(effect.Id).Append('/')
                .Append(effect.Timestamp).Append('/')
                .Append(effect.Source?.ObjectId ?? 0).Append('/')
                .Append(effect.SourceZoneChangeVersion).Append('/')
                .Append(effect.ControllerId).Append('/')
                .Append((int)effect.Duration).Append('/')
                .Append(effect.RuleKey).Append('/')
                .Append((int)effect.Operation).Append('/')
                .Append(effect.Value).Append('/')
                .Append(effect.AffectedPlayerId).Append('/')
                .Append(effect.FilterHookName).Append('/')
                .Append(effect.LinkId).Append(',');
        }
        builder.Append("]|");
    }

    private static int CompareContinuousRegistrations(
        ScriptContinuousRegistration left,
        ScriptContinuousRegistration right)
    {
        return left.Timestamp.CompareTo(right.Timestamp);
    }

    private static void ApplyReplacement(
        ScriptReplacementRegistration replacement,
        GameEventContext context,
        Action<ScriptReplacementRegistration, GameEventContext> invokeReplacement)
    {
        switch (replacement.Operation)
        {
            case GameEventReplacementOperation.Prevent:
                context.amount = 0;
                context.prevented = true;
                break;
            case GameEventReplacementOperation.AddAmount:
                context.amount = NormalizeReplacementAmount(
                    context,
                    context.amount + replacement.Value);
                break;
            case GameEventReplacementOperation.MultiplyAmount:
                context.amount = NormalizeReplacementAmount(
                    context,
                    context.amount * Math.Max(0, replacement.Value));
                break;
            case GameEventReplacementOperation.SetAmount:
                context.amount = NormalizeReplacementAmount(context, replacement.Value);
                break;
            case GameEventReplacementOperation.ChangeDestination:
            case GameEventReplacementOperation.InvokeHook:
                invokeReplacement?.Invoke(replacement, context);
                break;
        }

        context.replaced = true;
    }

    private static int NormalizeReplacementAmount(
        GameEventContext context,
        int amount)
    {
        return context != null
            && (context.type == GameEventType.LifeChanged
                || context.type == GameEventType.CounterChanged)
            ? amount
            : Math.Max(0, amount);
    }

    private void Cleanup(Func<RuntimeCard, bool> isSourceActive)
    {
        triggers.RemoveAll(item =>
            item.Source == null
            || (item.Duration == RuntimeEffectDuration.WhileSourceRemains
                && !item.IsActive(isSourceActive)));
        replacements.RemoveAll(item =>
            item.Source == null
            || (item.Duration == RuntimeEffectDuration.WhileSourceRemains
                && !item.IsActive(isSourceActive)));
        continuousEffects.RemoveAll(item =>
            item.Source == null
            || (item.Duration == RuntimeEffectDuration.WhileSourceRemains
                && !item.IsActive(isSourceActive)));
        ruleEffects.RemoveAll(item =>
            item.Source == null
            || (item.Duration == RuntimeEffectDuration.WhileSourceRemains
                && !item.IsActive(isSourceActive)));
    }
}
