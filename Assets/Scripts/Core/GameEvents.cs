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
    
    // Combat
    public static event Action<RuntimeCard, object> OnAttackDeclared; // Attacker, Target (Player ID or Planeswalker)
    public static event Action<RuntimeCard, RuntimeCard> OnBlockDeclared; // Blocker, Attacker
    public static event Action<RuntimeCard, int> OnDamageDealt;

    public static void TriggerCardPlayed(RuntimeCard card) => OnCardPlayed?.Invoke(card);
    public static void TriggerCreatureDied(RuntimeCard card) => OnCreatureDied?.Invoke(card);
    public static void TriggerPhaseChanged(TurnPhase phase) => OnPhaseChanged?.Invoke(phase);
    public static void TriggerStackChanged(int stackCount) => OnStackChanged?.Invoke(stackCount);
    public static void TriggerCardTapped(RuntimeCard card) => OnCardTapped?.Invoke(card);
    public static void TriggerCardUntapped(RuntimeCard card) => OnCardUntapped?.Invoke(card);
    
    // Attack target is now typically an integer (Player ID) or a card (Planeswalker)
    public static void TriggerAttackDeclared(RuntimeCard attacker, object target) => OnAttackDeclared?.Invoke(attacker, target);
    
    // New event for blocking
    public static void TriggerBlockDeclared(RuntimeCard blocker, RuntimeCard attacker) => OnBlockDeclared?.Invoke(blocker, attacker);
    
    public static void TriggerDamageDealt(RuntimeCard target, int damage) => OnDamageDealt?.Invoke(target, damage);
}
