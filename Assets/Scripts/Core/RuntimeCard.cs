using System.Collections.Generic;
using UnityEngine;
using XLua;

[System.Serializable]
[LuaCallCSharp]
public class RuntimeCard
{
    // Static Data
    public CardData Data { get; private set; }
    
    // Dynamic Stats
    public int CurrentAttack;
    public int CurrentDefence;
    public int DamageTaken { get; private set; }
    public bool IsTapped;
    public int OwnerPlayerID; // 1 or 2
    public bool IsDead { get; private set; }
    
    // Dynamic Abilities (Script Context)
    public LuaTable ScriptInstance; 
    
    // Keywords cache for fast lookup (managed by Lua or C#)
    private HashSet<string> Keywords = new HashSet<string>();

    // Activated Abilities — lazy-initialized; most cards never add abilities
    private List<ActivatedAbility> _abilities;
    public List<ActivatedAbility> Abilities
    {
        get
        {
            if (_abilities == null) _abilities = new List<ActivatedAbility>(1);
            return _abilities;
        }
    }

    // Combat State
    public bool IsAttacking;
    public bool IsBlocking;
    public object AttackTarget; // Usually int PlayerID, but could be Planeswalker
    public RuntimeCard BlockingTarget; // The attacker this card is blocking
    public List<RuntimeCard> Blockers = new List<RuntimeCard>(); // Creatures blocking this card

    public RuntimeCard(CardData data, int ownerID)
    {
        Data = data;
        OwnerPlayerID = ownerID;
        
        // Initialize Stats
        CurrentAttack = data.attack;
        CurrentDefence = data.defence;
        DamageTaken = 0;
        IsTapped = false;
        
        // Initialize Script
        EnsureScriptInstance();
    }

    public bool HasKeyword(string keywordName)
    {
        return Keywords.Contains(keywordName);
    }

    // Helper for Lua to add keywords
    public void AddKeyword(string keyword) => Keywords.Add(keyword);
    public void RemoveKeyword(string keyword) => Keywords.Remove(keyword);

    public void EnsureScriptInstance()
    {
        if (ScriptInstance != null) return;
        if (Data == null || string.IsNullOrEmpty(Data.luaScript)) return;

        var manager = XLuaManager.Instance;
        if (manager == null || manager.LuaEnv == null) return;

        // Create a new instance of the card script
        ScriptInstance = manager.CreateCardInstance(this, Data.luaScript);
    }

    public void OnDestroy()
    {
        // Optional: Call OnDestroy in Lua
        if (ScriptInstance != null)
        {
            var onDestroy = ScriptInstance.Get<System.Action<LuaTable>>("OnDie"); // Or OnDestroy
            if (onDestroy != null)
            {
                onDestroy(ScriptInstance);
            }
            ScriptInstance.Dispose();
            ScriptInstance = null;
        }
    }

    public void Tap()
    {
        if (!IsTapped)
        {
            IsTapped = true;
            GameEvents.TriggerCardTapped(this);
        }
    }

    public void Untap()
    {
        if (IsTapped)
        {
            IsTapped = false;
            GameEvents.TriggerCardUntapped(this);
        }
    }
    
    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;

        DamageTaken += damage;
        GameEvents.TriggerDamageDealt(this, damage);

        if (DamageTaken >= CurrentDefence)
        {
            Die();
        }
    }

    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        GameEvents.TriggerCreatureDied(this);
        OnDestroy();
    }

    public void ClearDamage()
    {
        DamageTaken = 0;
    }

    // Combat State Management
    public void DeclareAttack(object target)
    {
        IsAttacking = true;
        AttackTarget = target;
        // Tapping logic is handled by GameRules based on Vigilance
    }

    public void DeclareBlock(RuntimeCard targetAttacker)
    {
        IsBlocking = true;
        BlockingTarget = targetAttacker;
        targetAttacker.Blockers.Add(this);
    }

    public void RemoveFromCombat()
    {
        if (IsBlocking && BlockingTarget != null)
        {
            BlockingTarget.Blockers.Remove(this);
        }

        if (IsAttacking && Blockers.Count > 0)
        {
            foreach (var blocker in Blockers)
            {
                blocker.IsBlocking = false;
                blocker.BlockingTarget = null;
            }
        }

        IsAttacking = false;
        IsBlocking = false;
        AttackTarget = null;
        BlockingTarget = null;
        Blockers.Clear();
    }

    public bool HasActivatableAbility(GameRules rules)
    {
        foreach (var a in Abilities)
        {
            if (a.IsActivatable(rules, this)) return true;
        }
        return false;
    }

    public ActivatedAbility GetFirstActivatableAbility(GameRules rules)
    {
        foreach (var a in Abilities)
        {
            if (a.IsActivatable(rules, this)) return a;
        }
        return null;
    }
}
