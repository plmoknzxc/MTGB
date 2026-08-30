using System;
using System.Collections.Generic;
using UnityEngine;

public enum DamageEventKind
{
    Noncombat = 0,
    Combat = 1
}

public enum DamageReplacementScope
{
    Any = 0,
    SourceDealsDamage = 1,
    CardIsDealtDamage = 2,
    PlayerIsDealtDamage = 3
}

public enum DamageReplacementOperation
{
    Prevent = 0,
    Add = 1,
    Multiply = 2,
    Set = 3
}

public sealed class DamageEventRuntime
{
    private readonly HashSet<long> appliedReplacementIds = new HashSet<long>();

    public RuntimeCard Source { get; }
    public RuntimeCard CardTarget { get; }
    public int PlayerTarget { get; }
    public DamageEventKind Kind { get; }
    public int OriginalAmount { get; }
    public int Amount { get; private set; }

    public DamageEventRuntime(
        RuntimeCard source,
        RuntimeCard cardTarget,
        int playerTarget,
        int amount,
        DamageEventKind kind)
    {
        Source = source;
        CardTarget = cardTarget;
        PlayerTarget = playerTarget;
        OriginalAmount = Mathf.Max(0, amount);
        Amount = OriginalAmount;
        Kind = kind;
    }

    public bool HasApplied(long replacementId)
    {
        return appliedReplacementIds.Contains(replacementId);
    }

    public void Apply(DamageReplacementRuntime replacement)
    {
        if (replacement == null || HasApplied(replacement.Id)) return;

        appliedReplacementIds.Add(replacement.Id);
        switch (replacement.Operation)
        {
            case DamageReplacementOperation.Prevent:
                Amount = replacement.Value < 0
                    ? 0
                    : Mathf.Max(0, Amount - replacement.Value);
                break;
            case DamageReplacementOperation.Add:
                Amount = Mathf.Max(0, Amount + replacement.Value);
                break;
            case DamageReplacementOperation.Multiply:
                Amount = Mathf.Max(0, Amount * Mathf.Max(0, replacement.Value));
                break;
            case DamageReplacementOperation.Set:
                Amount = Mathf.Max(0, replacement.Value);
                break;
        }
    }
}

public sealed class DamageReplacementRuntime
{
    public long Id { get; }
    public long Timestamp { get; }
    public RuntimeCard EffectSource { get; }
    public int EffectSourceZoneChangeVersion { get; }
    public DamageReplacementScope Scope { get; }
    public DamageReplacementOperation Operation { get; }
    public RuntimeCard ScopeCard { get; }
    public int ScopeCardZoneChangeVersion { get; }
    public int ScopePlayer { get; }
    public int Value { get; }
    public bool CombatOnly { get; }
    public bool UntilEndOfTurn { get; }

    public DamageReplacementRuntime(
        long id,
        long timestamp,
        RuntimeCard effectSource,
        DamageReplacementScope scope,
        DamageReplacementOperation operation,
        RuntimeCard scopeCard,
        int scopePlayer,
        int value,
        bool combatOnly,
        bool untilEndOfTurn)
    {
        Id = id;
        Timestamp = timestamp;
        EffectSource = effectSource;
        EffectSourceZoneChangeVersion = effectSource?.ZoneChangeVersion ?? -1;
        Scope = scope;
        Operation = operation;
        ScopeCard = scopeCard;
        ScopeCardZoneChangeVersion = scopeCard?.ZoneChangeVersion ?? -1;
        ScopePlayer = scopePlayer;
        Value = value;
        CombatOnly = combatOnly;
        UntilEndOfTurn = untilEndOfTurn;
    }

    public bool AppliesTo(DamageEventRuntime damageEvent, Func<RuntimeCard, bool> isOnBattlefield)
    {
        if (damageEvent == null || damageEvent.Amount <= 0) return false;
        if (CombatOnly && damageEvent.Kind != DamageEventKind.Combat) return false;
        if (EffectSource != null
            && (EffectSource.ZoneChangeVersion != EffectSourceZoneChangeVersion
                || !isOnBattlefield(EffectSource)))
        {
            return false;
        }

        switch (Scope)
        {
            case DamageReplacementScope.Any:
                return true;
            case DamageReplacementScope.SourceDealsDamage:
                return ReferenceEquals(damageEvent.Source, ScopeCard)
                    && ScopeCard != null
                    && ScopeCard.ZoneChangeVersion == ScopeCardZoneChangeVersion;
            case DamageReplacementScope.CardIsDealtDamage:
                return ReferenceEquals(damageEvent.CardTarget, ScopeCard)
                    && ScopeCard != null
                    && ScopeCard.ZoneChangeVersion == ScopeCardZoneChangeVersion;
            case DamageReplacementScope.PlayerIsDealtDamage:
                return damageEvent.PlayerTarget == ScopePlayer;
            default:
                return false;
        }
    }
}
