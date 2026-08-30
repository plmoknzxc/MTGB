using System;

[Flags]
public enum ActivatedAbilityRuleTrait
{
    None = 0,
    CouldAddMana = 1 << 0,
    RequiresTarget = 1 << 1,
    LoyaltyAbility = 1 << 2,
    MovesCardIntoOrOutOfLibrary = 1 << 3
}

/// <summary>
/// Runtime-only ability definition. Delegates are not Unity-serializable;
/// abilities must be constructed in code (C# or Lua), not in the Inspector.
/// </summary>
public class ActivatedAbility
{
    private const ActivatedAbilityRuleTrait ManaAbilityDisqualifiers =
        ActivatedAbilityRuleTrait.RequiresTarget
        | ActivatedAbilityRuleTrait.LoyaltyAbility
        | ActivatedAbilityRuleTrait.MovesCardIntoOrOutOfLibrary;

    public string Name;
    public string Description;
    public ActivatedAbilityRuleTrait RuleTraits;
    public ManaPool ProducedMana;
    public ScriptAbilityCost Cost;
    public ScriptAbilityTiming Timing;
    public string CanActivateHook;
    public string ResolveHook;

    public Func<GameRules, RuntimeCard, bool> CanActivate;
    public Action<GameRules, RuntimeCard> Execute;

    public bool IsManaAbility =>
        (RuleTraits & ActivatedAbilityRuleTrait.CouldAddMana) != 0
        && (RuleTraits & ManaAbilityDisqualifiers) == 0;

    public bool HasProducedMana => !ProducedMana.IsEmpty;

    public bool IsActivatable(GameRules rules, RuntimeCard card)
    {
        return CanActivate != null && CanActivate(rules, card);
    }

    public void Activate(GameRules rules, RuntimeCard card)
    {
        Execute?.Invoke(rules, card);
    }
}
