using System;

/// <summary>
/// Runtime-only ability definition. Delegates are not Unity-serializable;
/// abilities must be constructed in code (C# or Lua), not in the Inspector.
/// </summary>
public class ActivatedAbility
{
    public string Name;
    public string Description;

    public Func<GameRules, RuntimeCard, bool> CanActivate;
    public Action<GameRules, RuntimeCard> Execute;

    public bool IsActivatable(GameRules rules, RuntimeCard card)
    {
        return CanActivate != null && CanActivate(rules, card);
    }

    public void Activate(GameRules rules, RuntimeCard card)
    {
        Execute?.Invoke(rules, card);
    }
}
