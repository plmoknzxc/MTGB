using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class CardDatabaseRepository
{
    public const int SupportedSchemaVersion = 7;
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
    {0},
    {1},
    {2},
    {3},
    {4},
    COALESCE(c.script_path, ''),
    COALESCE((
        SELECT ce.effect_key
        FROM card_effects ce
        WHERE ce.card_key = c.card_key AND ce.trigger = 'on_play'
        ORDER BY ce.effect_order
        LIMIT 1
    ), '')
FROM cards c
WHERE c.enabled <> 0
ORDER BY c.card_id;";

    private static readonly List<CardDefinition> LoadedCards = new List<CardDefinition>();
    private static bool loaded;

    public static string ExternalRoot => Path.Combine(PrintingKeyUtility.ApplicationRoot, FolderName);
    public static string ScriptRoot => Path.Combine(PrintingKeyUtility.ApplicationRoot, ScriptFolderName);

    public static IReadOnlyList<CardDefinition> LoadAll(bool forceReload = false)
    {
        if (loaded && !forceReload) return LoadedCards;

        Clear();
        EnsureFolders();

        var printingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cardIds = new HashSet<int>();
        foreach (string databasePath in EnumerateDatabasePaths())
        {
            try
            {
                LoadDatabase(databasePath, printingKeys, cardIds);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not load card database '{databasePath}': {exception.Message}");
            }
        }

        loaded = true;
        Debug.Log($"Loaded {LoadedCards.Count} cards from {FolderName}.");
        return LoadedCards;
    }

    public static CardDefinition FindById(int cardId)
    {
        IReadOnlyList<CardDefinition> cards = LoadAll();
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].cardID == cardId) return cards[i];
        }
        return null;
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
        loaded = false;
    }

    private static void LoadDatabase(string databasePath, HashSet<string> printingKeys, HashSet<int> cardIds)
    {
        bool customContent = !string.Equals(
            Path.GetFileName(databasePath),
            CoreDatabaseName,
            StringComparison.OrdinalIgnoreCase);
        using (NativeSqliteDatabase database = NativeSqliteDatabase.OpenReadOnlyDatabase(databasePath))
        {
            string versionText = database.ExecuteScalarString(
                "SELECT value FROM metadata WHERE key = 'schema_version' LIMIT 1;");
            if (!int.TryParse(versionText, out int version) || !IsSupportedSchemaVersion(version))
            {
                throw new InvalidDataException(
                    $"Unsupported schema version '{versionText}'. Expected {SupportedSchemaVersion}.");
            }

            string targetModeExpression = HasColumn(database, "cards", "target_mode")
                ? "COALESCE(c.target_mode, 0)"
                : "0";
            string supertypeExpression = HasColumn(database, "cards", "supertype_flags")
                ? "COALESCE(c.supertype_flags, 0)"
                : "0";
            string subtypeExpression = HasColumn(database, "cards", "subtypes")
                ? "COALESCE(c.subtypes, '')"
                : "''";
            bool hasCharacteristics = HasTable(database, "card_characteristics");
            string loyaltyExpression = hasCharacteristics
                ? "COALESCE((SELECT cc.loyalty FROM card_characteristics cc WHERE cc.card_key = c.card_key LIMIT 1), 0)"
                : "0";
            string battleDefenseExpression = hasCharacteristics
                ? "COALESCE((SELECT cc.defense FROM card_characteristics cc WHERE cc.card_key = c.card_key LIMIT 1), 0)"
                : "0";
            string cardQuery = string.Format(
                CardQueryTemplate,
                targetModeExpression,
                supertypeExpression,
                subtypeExpression,
                loyaltyExpression,
                battleDefenseExpression);

            var cardsByKey = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase);
            database.Query(cardQuery, row =>
            {
                int cardId = row.GetInt32(0);
                string setCode = row.GetString(2).Trim();
                string collectorText = row.GetString(3).Trim();
                string printingKey = $"{setCode}/{collectorText}";

                if (!cardIds.Add(cardId))
                {
                    Debug.LogWarning($"Skipped duplicate card ID {cardId} from '{databasePath}'.");
                    return;
                }
                if (!printingKeys.Add(printingKey))
                {
                    cardIds.Remove(cardId);
                    Debug.LogWarning($"Skipped duplicate printing {printingKey} from '{databasePath}'.");
                    return;
                }

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
                card.targetMode = (CardTargetMode)row.GetInt32(10);
                card.supertypes = (CardSupertype)row.GetInt32(11);
                card.subtypes = row.GetString(12);
                card.loyalty = row.GetInt32(13);
                card.battleDefense = row.GetInt32(14);
                if (card.battleDefense == 0 && card.HasType(CardType.Battle) && !card.HasType(CardType.Creature))
                {
                    card.battleDefense = card.defence;
                }
                card.luaScript = LoadScript(card, databasePath, row.GetString(15));
                card.effectKey = row.GetString(16);
                card.customContent = customContent;
                if (card.targetMode == CardTargetMode.None)
                {
                    card.targetMode = InferTargetMode(card);
                }
                card.Normalize();
                LoadedCards.Add(card);
                cardsByKey[printingKey] = card;
            });

            if (version >= 3 && HasTable(database, "card_features"))
            {
                LoadFeatures(database, cardsByKey);
            }
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

        if (HasTable(database, "feature_rule_entries"))
        {
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
        }

        if (HasTable(database, "feature_effects"))
        {
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
        }

        if (HasTable(database, "feature_strings"))
        {
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
        }


        foreach (CardDefinition card in cardsByKey.Values)
        {
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

    private static bool HasColumn(NativeSqliteDatabase database, string tableName, string columnName)
    {
        bool found = false;
        database.Query($"PRAGMA table_info({tableName});", row =>
        {
            if (string.Equals(row.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
            }
        });
        return found;
    }

    private static bool HasTable(NativeSqliteDatabase database, string tableName)
    {
        bool found = false;
        database.Query("SELECT name FROM sqlite_master WHERE type = 'table';", row =>
        {
            if (string.Equals(row.GetString(0), tableName, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
            }
        });
        return found;
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

    private static CardTargetMode InferTargetMode(CardDefinition card)
    {
        if (card == null) return CardTargetMode.None;

        string text = $"{card.description} {card.effectKey}".ToUpperInvariant();
        if (!text.Contains("TARGET") && !text.Contains("目标")) return CardTargetMode.None;

        if (text.Contains("OPPONENT") && (text.Contains("CREATURE OR PLAYER") || text.Contains("ANY TARGET")))
            return CardTargetMode.OpponentCreatureOrPlayer;
        if (text.Contains("CREATURE OR PLAYER") || text.Contains("ANY TARGET"))
            return CardTargetMode.AnyCreatureOrPlayer;
        if (text.Contains("OPPONENT") && text.Contains("CREATURE"))
            return CardTargetMode.OpponentCreature;
        if (text.Contains("CREATURE"))
            return CardTargetMode.AnyCreature;
        if (text.Contains("OPPONENT") && text.Contains("PERMANENT"))
            return CardTargetMode.OpponentPermanent;
        if (text.Contains("PERMANENT"))
            return CardTargetMode.AnyPermanent;
        if (text.Contains("OPPONENT") && text.Contains("PLAYER"))
            return CardTargetMode.OpponentPlayer;
        if (text.Contains("PLAYER"))
            return CardTargetMode.AnyPlayer;

        return CardTargetMode.None;
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
