using System;
using System.Collections.Generic;
using UnityEngine;

public static class CardEffectExecutor
{
    public static bool HasOnPlayEffect(RuntimeCard card)
    {
        if (card == null) return false;
        return CardEffectRegistry.TryGet(card.Data.effectKey, out _) || HasCardScriptHook(card, "OnPlay");
    }

    public static void ExecuteOnPlayEffect(GameRules rules, RuntimeCard card)
    {
        if (card == null) return;

        using (CardScriptRuntimeManager.UseRules(rules))
        {
            if (HasCardScriptHook(card, "OnPlay"))
            {
                InvokeCardScript(card, "OnPlay");
                return;
            }
        }

        if (CardEffectRegistry.TryGet(card.Data.effectKey, out ICardEffect effect))
        {
            effect.OnPlay(rules, card);
        }
    }

    public static void ExecuteTriggeredHook(GameRules rules, RuntimeCard source, string functionName)
    {
        if (source == null || string.IsNullOrEmpty(functionName)) return;

        using (CardScriptRuntimeManager.UseRules(rules))
        {
            InvokeCardScript(source, functionName);
        }
    }

    public static void CollectHookSources(
        IReadOnlyList<RuntimeCard> cards,
        string functionName,
        RuntimeCard excludedCard,
        List<RuntimeCard> destination)
    {
        if (cards == null || destination == null || string.IsNullOrEmpty(functionName)) return;
        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard card = cards[i];
            if (card == null || ReferenceEquals(card, excludedCard)) continue;
            if (HasCardScriptHook(card, functionName)) destination.Add(card);
        }
    }

    public static bool HasCardScriptHook(RuntimeCard card, string functionName)
    {
        if (card == null || string.IsNullOrEmpty(functionName)) return false;
        card.EnsureScriptInstance();
        return card.ScriptInstance != null && card.ScriptInstance.HasFunction(functionName);
    }

    public static bool EvaluateCardScriptPredicate(
        GameRules rules,
        RuntimeCard card,
        string functionName,
        bool defaultValue = true)
    {
        if (card == null || string.IsNullOrEmpty(functionName)) return defaultValue;
        card.EnsureScriptInstance();
        ICardScriptInstance script = card.ScriptInstance;
        if (script == null || !script.HasFunction(functionName)) return defaultValue;

        try
        {
            using (CardScriptRuntimeManager.UseRules(rules))
            {
                return CardScriptRuntimeManager.Instance != null
                    ? CardScriptRuntimeManager.Instance.InvokeBoolean(script, functionName, defaultValue)
                    : defaultValue;
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Lua card predicate '{functionName}' failed on " +
                $"{card.Data?.cardName ?? "unknown card"}: {exception.Message}");
            return defaultValue;
        }
    }

    private static void InvokeCardScript(RuntimeCard card, string functionName)
    {
        if (card == null || string.IsNullOrEmpty(functionName)) return;

        card.EnsureScriptInstance();
        ICardScriptInstance script = card.ScriptInstance;
        if (script == null) return;

        try
        {
            CardScriptRuntimeManager.Instance?.Invoke(script, functionName);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Lua card hook '{functionName}' failed on {card.Data?.cardName ?? "unknown card"}: {exception.Message}");
        }
    }
}
