using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class CardDatabaseRepository
{
    public const int SupportedSchemaVersion = 8;
    public const int MinimumSchemaVersion = SupportedSchemaVersion;
    public const string FolderName = "CardDatabases";
    public const string ScriptFolderName = "CardScripts";
    public const string CoreDatabaseName = "core.mtgbdb";

    private const string CardQueryTemplate = @"
SELECT
    c.card_id,
    c.oracle_id,
    c.set_code,
    c.collector_number,
    c.name,
    c.type_flags,
    c.mana_cost,
    c.rules_text,
    c.power,
    c.toughness,
    COALESCE(c.supertype_flags, 0),
    COALESCE(c.subtypes, ''),
    COALESCE((SELECT cc.loyalty FROM card_characteristics cc WHERE cc.card_key = c.card_key LIMIT 1), 0),
    COALESCE((SELECT cc.defense FROM card_characteristics cc WHERE cc.card_key = c.card_key LIMIT 1), 0),
    COALESCE(c.script_path, ''),
    c.content_kind
FROM cards c
WHERE c.enabled <> 0
ORDER BY c.card_id;";

    private static readonly List<CardDefinition> LoadedCards = new List<CardDefinition>();
    private static readonly List<string> loadWarnings = new List<string>();
    public static IReadOnlyList<string> LoadWarnings => loadWarnings;
    private static bool loaded;

    public static string ExternalRoot => Path.Combine(PrintingKeyUtility.ApplicationRoot, FolderName);
    public static string ScriptRoot => Path.Combine(PrintingKeyUtility.ApplicationRoot, ScriptFolderName);

    public static IReadOnlyList<CardDefinition> LoadAll(bool forceReload = false)
    {
        if (loaded && !forceReload) return LoadedCards;

        Clear();
        EnsureFolders();

        LoadedCards.AddRange(LoadDatabases(EnumerateDatabasePaths(), loadWarnings));
        loaded = true;
        Debug.Log($"Loaded {LoadedCards.Count} cards from {FolderName}.");
        return LoadedCards;
    }

    private static List<CardDefinition> LoadDatabases(IEnumerable<string> paths, List<string> warnings)
    {
        var cards = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (string databasePath in paths)
        {
            try
            {
                // Read and validate the entire file before replacing any previous definitions.
                foreach (CardDefinition card in LoadDatabase(databasePath))
                {
                    if (cards.TryGetValue(card.PrintingKey, out CardDefinition previous))
                    {
                        string warning = $"卡牌 {card.PrintingKey} 使用后加载的数据库 '{databasePath}'，覆盖 '{previous.sourceDatabasePath}'。";
                        warnings.Add(warning);
                        Debug.LogWarning(warning);
                    }
                    cards[card.PrintingKey] = card;
                }
            }
            catch (Exception exception)
            {
                string warning = $"Could not load card database '{databasePath}': {exception.Message}";
                warnings.Add(warning);
                Debug.LogError(warning);
            }
        }
        var result = new List<CardDefinition>(cards.Values);
        result.Sort((a, b) => StringComparer.Ordinal.Compare(a.PrintingKey, b.PrintingKey));
        return result;
    }

#if UNITY_EDITOR
    public static IReadOnlyList<CardDefinition> LoadPathsForAutomation(params string[] paths)
        => LoadDatabases(paths, new List<string>());
#endif

    public static CardDefinition FindById(int cardId)
    {
        IReadOnlyList<CardDefinition> cards = LoadAll();
        CardDefinition match = null;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].cardID != cardId) continue;
            if (match != null) return null; // A library-local ID cannot identify an ambiguous printing.
            match = cards[i];
        }
        return match;
    }

    public static CardDefinition FindByPrintingKey(string setCode, string collectorNumber)
    {
        if (string.IsNullOrWhiteSpace(setCode) || string.IsNullOrWhiteSpace(collectorNumber))
        {
            return null;
        }

        string expectedSet = setCode.Trim();
        string expectedCollector = collectorNumber.Trim();
        IReadOnlyList<CardDefinition> cards = LoadAll();
        for (int i = 0; i < cards.Count; i++)
        {
            CardDefinition card = cards[i];
            if (card != null
                && string.Equals(card.setCode, expectedSet, StringComparison.OrdinalIgnoreCase)
                && string.Equals(card.CollectorNumber, expectedCollector, StringComparison.OrdinalIgnoreCase))
            {
                return card;
            }
        }

        return null;
    }

    public static void Clear()
    {
        for (int i = 0; i < LoadedCards.Count; i++)
        {
            if (LoadedCards[i] == null) continue;
            if (LoadedCards[i]?.artworkRuntime == true && LoadedCards[i].artwork != null)
            {
                Texture2D texture = LoadedCards[i].artwork.texture;
                UnityEngine.Object.Destroy(LoadedCards[i].artwork);
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
        }
        LoadedCards.Clear();
        loadWarnings.Clear();
        loaded = false;
    }

    private static List<CardDefinition> LoadDatabase(string databasePath)
    {
        using (NativeSqliteDatabase database = NativeSqliteDatabase.OpenReadOnlyDatabase(databasePath))
        {
            string versionText = database.ExecuteScalarString(
                "SELECT value FROM metadata WHERE key = 'schema_version' LIMIT 1;");
            if (!int.TryParse(versionText, out int version) || !IsSupportedSchemaVersion(version))
            {
                throw new InvalidDataException(
                    $"Unsupported schema version '{versionText}'. Expected {SupportedSchemaVersion}.");
            }

            var cardsByKey = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase);
            var databaseCards = new List<CardDefinition>();
            var databasePrintingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var databaseCardIds = new HashSet<int>();
            database.Query(CardQueryTemplate, row =>
            {
                int cardId = row.GetInt32(0);
                string setCode = row.GetString(2).Trim();
                string collectorText = row.GetString(3).Trim();
                string printingKey = $"{setCode}/{collectorText}";

                if (cardId <= 0 || !databaseCardIds.Add(cardId))
                {
                    throw new InvalidDataException($"Invalid or duplicate library-local card ID {cardId}.");
                }
                if (!databasePrintingKeys.Add(printingKey))
                {
                    throw new InvalidDataException($"Duplicate printing {printingKey} within database.");
                }
                int contentKind = row.GetInt32(15);
                if (contentKind != 0 && contentKind != 1)
                    throw new InvalidDataException($"Invalid content category {contentKind} for {printingKey}.");

                var card = new CardDefinition();
                card.cardName = row.GetString(4);
                card.cardID = cardId;
                card.oracleID = row.GetString(1);
                card.setCode = setCode;
                card.collectorNumberText = collectorText;
                int.TryParse(collectorText, out card.collectorNumber);
                card.cardTypes = (CardType)row.GetInt32(5);
                card.manaCost = row.GetString(6);
                card.description = row.GetString(7);
                card.attack = row.GetInt32(8);
                card.defence = row.GetInt32(9);
                card.supertypes = (CardSupertype)row.GetInt32(10);
                card.subtypes = row.GetString(11);
                card.loyalty = row.GetInt32(12);
                card.battleDefense = row.GetInt32(13);
                card.luaScript = LoadScript(card, databasePath, row.GetString(14));
                card.customContent = contentKind == 1;
                card.sourceDatabasePath = Path.GetFullPath(databasePath);
                databaseCards.Add(card);
                cardsByKey[printingKey] = card;
            });

            LoadFeatures(database, cardsByKey);
            return databaseCards;
        }
    }

    private static void LoadFeatures(
        NativeSqliteDatabase database,
        Dictionary<string, CardDefinition> cardsByKey)
    {
        var featuresByKey = new Dictionary<string, CardFeatureDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (CardDefinition card in cardsByKey.Values)
        {
            card.features.Clear();
        }

        const string featureQuery = @"
SELECT card_key, face_index, feature_index, feature_role, role_value,
       name, type_flags, mana_cost, rules_text, power, toughness, loyalty, defense,
       supertype_flags, subtypes, power_expression, toughness_expression
FROM card_features
ORDER BY card_key, face_index, feature_index;";
        database.Query(featureQuery, row =>
        {
            string cardKey = row.GetString(0);
            if (!cardsByKey.TryGetValue(cardKey, out CardDefinition card)) return;

            var feature = new CardFeatureDefinition
            {
                faceIndex = row.GetInt32(1),
                featureIndex = row.GetInt32(2),
                role = (CardFeatureRole)row.GetInt32(3),
                roleValue = row.GetString(4),
                cardName = row.GetString(5),
                cardTypes = (CardType)row.GetInt32(6),
                manaCost = row.GetString(7),
                description = row.GetString(8),
                attack = row.GetInt32(9),
                defence = row.GetInt32(10),
                loyalty = row.GetInt32(11),
                battleDefense = row.GetInt32(12),
                supertypes = (CardSupertype)row.GetInt32(13),
                subtypes = row.GetString(14),
                powerExpression = row.GetString(15),
                toughnessExpression = row.GetString(16)
            };
            card.features.Add(feature);
            featuresByKey[BuildFeatureKey(cardKey, feature.faceIndex, feature.featureIndex)] = feature;
        });

        database.Query(@"
SELECT card_key, face_index, feature_index, entry_order, entry_kind, chapter_numbers, text
FROM feature_rule_entries
ORDER BY card_key, face_index, feature_index, entry_order;", row =>
        {
            string key = BuildFeatureKey(row.GetString(0), row.GetInt32(1), row.GetInt32(2));
            if (!featuresByKey.TryGetValue(key, out CardFeatureDefinition feature)) return;
            feature.ruleEntries.Add(new CardRuleEntryDefinition
            {
                order = row.GetInt32(3),
                kind = (CardRuleEntryKind)row.GetInt32(4),
                chapterNumbers = row.GetString(5),
                text = row.GetString(6)
            });
        });

        database.Query(@"
SELECT card_key, face_index, feature_index, effect_order, trigger, effect_key, parameters_json
FROM feature_effects
ORDER BY card_key, face_index, feature_index, effect_order;", row =>
        {
            string key = BuildFeatureKey(row.GetString(0), row.GetInt32(1), row.GetInt32(2));
            if (!featuresByKey.TryGetValue(key, out CardFeatureDefinition feature)) return;
            feature.effects.Add(new CardEffectDefinition
            {
                order = row.GetInt32(3),
                trigger = row.GetString(4),
                effectKey = row.GetString(5),
                parametersJson = row.GetString(6)
            });
        });

        database.Query(@"
SELECT card_key, face_index, feature_index, string_index, text
FROM feature_strings
ORDER BY card_key, face_index, feature_index, string_index;", row =>
        {
            string key = BuildFeatureKey(row.GetString(0), row.GetInt32(1), row.GetInt32(2));
            if (!featuresByKey.TryGetValue(key, out CardFeatureDefinition feature)) return;
            feature.strings.Add(new CardStringDefinition
            {
                index = row.GetInt32(3),
                text = row.GetString(4)
            });
        });


        foreach (CardDefinition card in cardsByKey.Values)
        {
            ValidateV7Payload(card);
            CardFeatureDefinition primary = FindFrontPrimaryFeature(card);
            card.targetMode = CardTargetModeInference.Infer(primary.description, primary.effects);
            card.effectKey = FindOnPlayEffectKey(primary);
            card.Normalize();
        }
    }

    private static string BuildFeatureKey(string cardKey, int faceIndex, int featureIndex)
    {
        return $"{cardKey}\n{faceIndex}\n{featureIndex}";
    }

    private static bool IsSupportedSchemaVersion(int version)
    {
        return version >= MinimumSchemaVersion && version <= SupportedSchemaVersion;
    }

    private static CardFeatureDefinition FindFrontPrimaryFeature(CardDefinition card)
    {
        for (int i = 0; i < card.features.Count; i++)
        {
            CardFeatureDefinition feature = card.features[i];
            if (feature != null
                && feature.faceIndex == 0
                && feature.featureIndex == 0
                && feature.role == CardFeatureRole.Primary)
            {
                return feature;
            }
        }
        return null;
    }

    private static string FindOnPlayEffectKey(CardFeatureDefinition feature)
    {
        for (int i = 0; i < feature.effects.Count; i++)
        {
            CardEffectDefinition effect = feature.effects[i];
            if (effect != null && string.Equals(effect.trigger, "on_play", StringComparison.OrdinalIgnoreCase))
            {
                return effect.effectKey ?? string.Empty;
            }
        }
        return string.Empty;
    }

    private static void ValidateV7Payload(CardDefinition card)
    {
        var primaryCountsByFace = new Dictionary<int, int>();
        for (int i = 0; i < card.features.Count; i++)
        {
            CardFeatureDefinition feature = card.features[i];
            if (feature == null
                || !Enum.IsDefined(typeof(CardFeatureRole), feature.role)
                || string.IsNullOrWhiteSpace(feature.cardName)
                || feature.cardTypes == CardType.None
                || feature.cardTypes == CardType.Kindred
                || string.IsNullOrWhiteSpace(feature.powerExpression)
                || string.IsNullOrWhiteSpace(feature.toughnessExpression))
            {
                throw new InvalidDataException(
                    $"Card '{card.PrintingKey}' has an incomplete schema v7 feature at index {i}.");
            }
            if (!string.IsNullOrEmpty(feature.description) && feature.ruleEntries.Count == 0)
            {
                throw new InvalidDataException(
                    $"Card '{card.PrintingKey}' stores rules only in the legacy rules_text mirror.");
            }

            for (int ruleIndex = 0; ruleIndex < feature.ruleEntries.Count; ruleIndex++)
            {
                CardRuleEntryDefinition entry = feature.ruleEntries[ruleIndex];
                if (entry == null
                    || entry.order != ruleIndex
                    || !Enum.IsDefined(typeof(CardRuleEntryKind), entry.kind))
                {
                    throw new InvalidDataException(
                        $"Card '{card.PrintingKey}' has invalid schema v7 rule entries.");
                }
            }

            if (!primaryCountsByFace.ContainsKey(feature.faceIndex))
                primaryCountsByFace[feature.faceIndex] = 0;
            if (feature.role == CardFeatureRole.Primary)
            {
                primaryCountsByFace[feature.faceIndex]++;
                if (feature.featureIndex != 0)
                    throw new InvalidDataException(
                        $"Card '{card.PrintingKey}' has a primary feature outside feature index 0.");
            }
            else if (feature.featureIndex == 0)
            {
                throw new InvalidDataException(
                    $"Card '{card.PrintingKey}' has a non-primary feature at feature index 0.");
            }
        }

        foreach (KeyValuePair<int, int> pair in primaryCountsByFace)
        {
            if (pair.Value != 1)
                throw new InvalidDataException(
                    $"Card '{card.PrintingKey}' face {pair.Key} must have exactly one primary feature.");
        }

        CardFeatureDefinition primary = FindFrontPrimaryFeature(card);
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
            throw new InvalidDataException(
                $"Card '{card.PrintingKey}' root mirror does not match its schema v7 front primary feature.");
        }
    }

    private static string LoadScript(CardDefinition card, string databasePath, string legacyRelativePath)
    {
        if (PrintingKeyUtility.TryGetPrinting(card, out string setCode, out string collectorNumber))
        {
            foreach (string root in EnumerateScriptRoots())
            {
                string conventionPath = PrintingKeyUtility.BuildScriptPath(root, setCode, collectorNumber);
                if (File.Exists(conventionPath))
                {
                    return File.ReadAllText(conventionPath);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(legacyRelativePath)) return string.Empty;

        if (TryReadRelativeScript(PrintingKeyUtility.ApplicationRoot, legacyRelativePath, out string contents))
        {
            return contents;
        }

        string databaseRoot = Path.GetFullPath(Path.GetDirectoryName(databasePath) ?? ExternalRoot);
        if (TryReadRelativeScript(databaseRoot, legacyRelativePath, out contents))
        {
            return contents;
        }

        Debug.LogWarning($"Card script was not found: {legacyRelativePath}");
        return string.Empty;
    }

    private static bool TryReadRelativeScript(string root, string relativePath, out string contents)
    {
        contents = string.Empty;
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(relativePath)) return false;

        try
        {
            string fullRoot = Path.GetFullPath(root);
            string path = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!PrintingKeyUtility.IsPathInsideRoot(fullRoot, path))
            {
                Debug.LogWarning($"Rejected card script outside root '{fullRoot}': {relativePath}");
                return false;
            }
            if (!File.Exists(path)) return false;
            contents = File.ReadAllText(path);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not read card script '{relativePath}': {exception.Message}");
            return false;
        }
    }

    private static IEnumerable<string> EnumerateDatabasePaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string root in EnumerateDatabaseRoots())
        {
            string core = Path.Combine(root, CoreDatabaseName);
            string coreFullPath = Path.GetFullPath(core);
            if (File.Exists(core) && seen.Add(coreFullPath)) yield return core;

            if (!Directory.Exists(root)) continue;
            string[] databases = Directory.GetFiles(root, "*.mtgbdb", SearchOption.TopDirectoryOnly);
            Array.Sort(databases, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < databases.Length; i++)
            {
                string fullPath = Path.GetFullPath(databases[i]);
                if (seen.Add(fullPath)) yield return databases[i];
            }
        }
    }

    private static IEnumerable<string> EnumerateDatabaseRoots()
    {
        yield return ExternalRoot;
        yield return Path.Combine(PrintingKeyUtility.ApplicationRoot, "数据库");
        yield return Path.Combine(PrintingKeyUtility.ApplicationRoot, "Databases");
        yield return Path.Combine(PrintingKeyUtility.ApplicationRoot, "Database");
    }

    private static IEnumerable<string> EnumerateScriptRoots()
    {
        yield return ScriptRoot;
        yield return Path.Combine(PrintingKeyUtility.ApplicationRoot, "脚本");
    }

    private static void EnsureFolders()
    {
        Directory.CreateDirectory(ExternalRoot);
        Directory.CreateDirectory(ScriptRoot);
    }

}
