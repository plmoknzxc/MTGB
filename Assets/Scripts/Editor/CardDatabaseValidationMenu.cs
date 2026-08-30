using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CardDatabaseValidationMenu
{
    [MenuItem("Tools/MTGB/Validate Runtime Card Databases")]
    public static void ValidateFromMenu()
    {
        ValidateForAutomation();
    }

    public static void ValidateFromCommandLine()
    {
        try
        {
            ValidateForAutomation();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void ValidateForAutomation()
    {
        IReadOnlyList<CardDefinition> cards = CardDatabaseRepository.LoadAll(true);
        if (cards.Count == 0)
        {
            throw new InvalidOperationException("No enabled cards were loaded from CardDatabases.");
        }

        int featureCount = 0;
        int structuredFeatureCount = 0;
        int ruleEntryCount = 0;
        int scriptCount = 0;
        var printingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < cards.Count; i++)
        {
            CardDefinition card = cards[i];
            if (card == null)
            {
                throw new InvalidOperationException($"Card at index {i} is null.");
            }

            card.Normalize();
            if (string.IsNullOrWhiteSpace(card.PrintingKey))
            {
                throw new InvalidOperationException(
                    $"Card '{card.cardName}' has no V7 printing key.");
            }
            if (!printingKeys.Add(card.PrintingKey))
            {
                throw new InvalidOperationException(
                    $"Duplicate V7 printing key '{card.PrintingKey}'.");
            }

            var runtimeCard = new RuntimeCard(card, 1, false);
            if (runtimeCard.PrimaryFeature == null)
            {
                throw new InvalidOperationException(
                    $"Card '{card.PrintingKey}' has no runtime primary feature.");
            }

            featureCount += card.features.Count;
            for (int featureIndex = 0; featureIndex < card.features.Count; featureIndex++)
            {
                CardFeatureDefinition feature = card.features[featureIndex];
                if (feature == null)
                {
                    throw new InvalidOperationException(
                        $"Card '{card.PrintingKey}' has a null feature at index {featureIndex}.");
                }

                if (!Enum.IsDefined(typeof(CardFeatureRole), feature.role))
                {
                    throw new InvalidOperationException(
                        $"Card '{card.PrintingKey}' uses unsupported feature role {(int)feature.role}.");
                }

                if (feature.role != CardFeatureRole.Primary) structuredFeatureCount++;
                ruleEntryCount += feature.ruleEntries.Count;
            }
        }

        GameObject rulesObject = new GameObject("V7 Database Validation Rules");
        try
        {
            GameRules rules = rulesObject.AddComponent<GameRules>();
            using var runtime = new LuaCSharpCardScriptRuntime();
            using (CardScriptApi.UseRules(rules))
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    CardDefinition card = cards[i];
                    if (string.IsNullOrWhiteSpace(card.luaScript)) continue;

                    var runtimeCard = new RuntimeCard(card, 1, false);
                    runtimeCard.ReassignObjectId(100000 + i);
                    using ICardScriptInstance instance =
                        runtime.CreateCardInstance(runtimeCard, card.luaScript);
                    if (instance == null)
                    {
                        throw new InvalidOperationException(
                            $"Lua script '{card.PrintingKey}' failed to load against the V7 runtime.");
                    }
                    scriptCount++;
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rulesObject);
        }

        Debug.Log(
            $"Runtime card database validation passed: schema={CardDatabaseRepository.SupportedSchemaVersion}, " +
            $"cards={cards.Count}, features={featureCount}, structured_features={structuredFeatureCount}, " +
            $"rule_entries={ruleEntryCount}, scripts={scriptCount}.");
    }
}
