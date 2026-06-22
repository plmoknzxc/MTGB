using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class CardDatabaseRepository
{
    public const int SupportedSchemaVersion = 1;
    public const string FolderName = "CardDatabases";
    public const string CoreDatabaseName = "core.mtgbdb";
    public const string ExpansionFolderName = "Expansions";

    private const string CardQuery = @"
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

    private static readonly List<CardData> LoadedCards = new List<CardData>();
    private static bool loaded;

    public static string ExternalRoot => Path.Combine(GetApplicationRoot(), FolderName);
    public static string ExpansionRoot => Path.Combine(ExternalRoot, ExpansionFolderName);

    public static IReadOnlyList<CardData> LoadAll(bool forceReload = false)
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

    public static CardData FindById(int cardId)
    {
        IReadOnlyList<CardData> cards = LoadAll();
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].cardID == cardId) return cards[i];
        }
        return null;
    }

    public static void Clear()
    {
        for (int i = 0; i < LoadedCards.Count; i++)
        {
            if (LoadedCards[i] == null) continue;
            if (Application.isPlaying) UnityEngine.Object.Destroy(LoadedCards[i]);
            else UnityEngine.Object.DestroyImmediate(LoadedCards[i]);
        }
        LoadedCards.Clear();
        loaded = false;
    }

    private static void LoadDatabase(string databasePath, HashSet<string> printingKeys, HashSet<int> cardIds)
    {
        using (NativeSqliteDatabase database = NativeSqliteDatabase.OpenReadOnlyDatabase(databasePath))
        {
            string versionText = database.ExecuteScalarString(
                "SELECT value FROM metadata WHERE key = 'schema_version' LIMIT 1;");
            if (!int.TryParse(versionText, out int version) || version != SupportedSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Unsupported schema version '{versionText}'. Expected {SupportedSchemaVersion}.");
            }

            database.Query(CardQuery, row =>
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

                var card = ScriptableObject.CreateInstance<CardData>();
                card.name = row.GetString(4);
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
                card.luaScript = LoadScript(databasePath, row.GetString(10));
                card.effectKey = row.GetString(11);
                LoadedCards.Add(card);
            });
        }
    }

    private static string LoadScript(string databasePath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return string.Empty;

        string root = Path.GetFullPath(Path.GetDirectoryName(databasePath) ?? ExternalRoot);
        string path = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"Rejected card script outside database folder: {relativePath}");
            return string.Empty;
        }
        if (!File.Exists(path))
        {
            Debug.LogWarning($"Card script was not found: {path}");
            return string.Empty;
        }
        return File.ReadAllText(path);
    }

    private static IEnumerable<string> EnumerateDatabasePaths()
    {
        string core = Path.Combine(ExternalRoot, CoreDatabaseName);
        if (File.Exists(core)) yield return core;

        if (!Directory.Exists(ExpansionRoot)) yield break;
        string[] expansions = Directory.GetFiles(ExpansionRoot, "*.mtgbdb", SearchOption.TopDirectoryOnly);
        Array.Sort(expansions, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < expansions.Length; i++) yield return expansions[i];
    }

    private static void EnsureFolders()
    {
        Directory.CreateDirectory(ExternalRoot);
        Directory.CreateDirectory(ExpansionRoot);
    }

    private static string GetApplicationRoot()
    {
        DirectoryInfo parent = Directory.GetParent(Application.dataPath);
        return parent != null ? parent.FullName : Application.dataPath;
    }
}
