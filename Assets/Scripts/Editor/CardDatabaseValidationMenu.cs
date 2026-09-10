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
            var primaryCountsByFace = new Dictionary<int, int>();
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

                if (string.IsNullOrWhiteSpace(feature.cardName) || feature.cardTypes == CardType.None)
                {
                    throw new InvalidOperationException(
                        $"Card '{card.PrintingKey}' has an incomplete V7 feature at " +
                        $"face={feature.faceIndex}, feature={feature.featureIndex}.");
                }
                if (feature.cardTypes == CardType.Kindred)
                {
                    throw new InvalidOperationException(
                        $"Card '{card.PrintingKey}' has Kindred as its only card type.");
                }
                if (string.IsNullOrWhiteSpace(feature.powerExpression)
                    || string.IsNullOrWhiteSpace(feature.toughnessExpression))
                {
                    throw new InvalidOperationException(
                        $"Card '{card.PrintingKey}' has unnormalized characteristic expressions at " +
                        $"face={feature.faceIndex}, feature={feature.featureIndex}.");
                }
                if (!string.IsNullOrEmpty(feature.description) && feature.ruleEntries.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Card '{card.PrintingKey}' stores rules only in the legacy rules_text mirror at " +
                        $"face={feature.faceIndex}, feature={feature.featureIndex}.");
                }

                ValidateRuleEntries(card.PrintingKey, feature);

                if (!primaryCountsByFace.ContainsKey(feature.faceIndex))
                    primaryCountsByFace[feature.faceIndex] = 0;
                if (feature.role == CardFeatureRole.Primary)
                {
                    primaryCountsByFace[feature.faceIndex]++;
                    if (feature.featureIndex != 0)
                    {
                        throw new InvalidOperationException(
                            $"Card '{card.PrintingKey}' has a primary V7 feature outside feature index 0.");
                    }
                }
                else if (feature.featureIndex == 0)
                {
                    throw new InvalidOperationException(
                        $"Card '{card.PrintingKey}' has a non-primary V7 feature at feature index 0.");
                }

                if (feature.role != CardFeatureRole.Primary) structuredFeatureCount++;
                ruleEntryCount += feature.ruleEntries.Count;
            }

            foreach (KeyValuePair<int, int> pair in primaryCountsByFace)
            {
                if (pair.Value != 1)
                {
                    throw new InvalidOperationException(
                        $"Card '{card.PrintingKey}' face {pair.Key} must have exactly one primary feature.");
                }
            }

            ValidatePrimaryMirror(card);
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

    private static void ValidateRuleEntries(string printingKey, CardFeatureDefinition feature)
    {
        for (int i = 0; i < feature.ruleEntries.Count; i++)
        {
            CardRuleEntryDefinition entry = feature.ruleEntries[i];
            if (entry == null || entry.order != i)
            {
                throw new InvalidOperationException(
                    $"Card '{printingKey}' has non-contiguous V7 rule entry order at " +
                    $"face={feature.faceIndex}, feature={feature.featureIndex}.");
            }
            if (!Enum.IsDefined(typeof(CardRuleEntryKind), entry.kind))
            {
                throw new InvalidOperationException(
                    $"Card '{printingKey}' has an unsupported V7 rule entry kind.");
            }
            if (entry.kind == CardRuleEntryKind.Text && !string.IsNullOrEmpty(entry.chapterNumbers))
            {
                throw new InvalidOperationException(
                    $"Card '{printingKey}' has chapter numbers on a normal rule entry.");
            }
            if (entry.kind == CardRuleEntryKind.Chapter
                && (string.IsNullOrWhiteSpace(entry.chapterNumbers)
                    || string.IsNullOrWhiteSpace(entry.text)))
            {
                throw new InvalidOperationException(
                    $"Card '{printingKey}' has an incomplete chapter rule entry.");
            }
        }
    }

    private static void ValidatePrimaryMirror(CardDefinition card)
    {
        CardFeatureDefinition primary = null;
        for (int i = 0; i < card.features.Count; i++)
        {
            CardFeatureDefinition feature = card.features[i];
            if (feature != null && feature.faceIndex == 0 && feature.featureIndex == 0)
            {
                primary = feature;
                break;
            }
        }

        if (primary == null
            || !string.Equals(card.cardName, primary.cardName, StringComparison.Ordinal)
            || card.cardTypes != primary.cardTypes
            || card.supertypes != primary.supertypes
            || !string.Equals(card.subtypes ?? string.Empty, primary.subtypes ?? string.Empty, StringComparison.Ordinal)
            || !string.Equals(card.description ?? string.Empty, primary.description ?? string.Empty, StringComparison.Ordinal)
            || !string.Equals(card.manaCost ?? string.Empty, primary.manaCost ?? string.Empty, StringComparison.Ordinal)
            || card.attack != primary.attack
            || card.defence != primary.defence
            || card.loyalty != primary.loyalty
            || card.battleDefense != primary.battleDefense)
        {
            throw new InvalidOperationException(
                $"Card '{card.PrintingKey}' legacy mirror does not match its V7 front primary feature.");
        }
    }
}
