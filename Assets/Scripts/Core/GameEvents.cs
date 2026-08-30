using System;
using System.Collections.Generic;

public static class GameEvents
{
    // A centralized listener manager for Lua scripts could be added here
    // For now, we rely on the GameRules or a dedicated EventManager to dispatch to Lua instances
    
    // Game Flow
    public static event Action<RuntimeCard> OnCardPlayed;
    public static event Action<RuntimeCard> OnCreatureDied;
    public static event Action<TurnPhase> OnPhaseChanged;
    public static event Action<int> OnStackChanged;
    public static event Action<RuntimeCard> OnCardTapped;
    public static event Action<RuntimeCard> OnCardUntapped;
    public static event Action<MatchOutcome> OnMatchEnded;
    
    // Combat
    public static event Action<RuntimeCard, object> OnAttackDeclared; // Attacker, Target (Player ID or Planeswalker)
    public static event Action<RuntimeCard, RuntimeCard> OnBlockDeclared; // Blocker, Attacker
    public static event Action<RuntimeCard, int> OnDamageDealt;
    public static event Action<RuntimeCard, RuntimeCard, int> OnDamagePrevented; // Source, card target, prevented damage
    public static event Action<int, int, int> OnPlayerDamaged; // Player, Damage, Remaining life
    public static event Action<RuntimeCard> OnCreatureRegenerated;
    public static event Func<Action, bool> OnCombatAnimationRequested;

    public static void TriggerCardPlayed(RuntimeCard card) => OnCardPlayed?.Invoke(card);
    public static void TriggerCreatureDied(RuntimeCard card) => OnCreatureDied?.Invoke(card);
    public static void TriggerPhaseChanged(TurnPhase phase) => OnPhaseChanged?.Invoke(phase);
    public static void TriggerStackChanged(int stackCount) => OnStackChanged?.Invoke(stackCount);
    public static void TriggerCardTapped(RuntimeCard card) => OnCardTapped?.Invoke(card);
    public static void TriggerCardUntapped(RuntimeCard card) => OnCardUntapped?.Invoke(card);
    public static void TriggerMatchEnded(MatchOutcome outcome) => OnMatchEnded?.Invoke(outcome);
    
    // Attack target is now typically an integer (Player ID) or a card (Planeswalker)
    public static void TriggerAttackDeclared(RuntimeCard attacker, object target) => OnAttackDeclared?.Invoke(attacker, target);
    
    // New event for blocking
    public static void TriggerBlockDeclared(RuntimeCard blocker, RuntimeCard attacker) => OnBlockDeclared?.Invoke(blocker, attacker);
    
    public static void TriggerDamageDealt(RuntimeCard target, int damage) => OnDamageDealt?.Invoke(target, damage);
    public static void TriggerDamagePrevented(RuntimeCard source, RuntimeCard target, int damage) => OnDamagePrevented?.Invoke(source, target, damage);
    public static void TriggerPlayerDamaged(int player, int damage, int remainingLife) => OnPlayerDamaged?.Invoke(player, damage, remainingLife);
    public static void TriggerCreatureRegenerated(RuntimeCard card) => OnCreatureRegenerated?.Invoke(card);

    public static bool TriggerCombatAnimationRequested(Action onComplete)
    {
        if (OnCombatAnimationRequested == null) return false;

        foreach (Func<Action, bool> handler in OnCombatAnimationRequested.GetInvocationList())
        {
            if (handler(onComplete)) return true;
        }

        return false;
    }
}
