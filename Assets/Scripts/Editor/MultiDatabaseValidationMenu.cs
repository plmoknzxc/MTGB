#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class MultiDatabaseValidationMenu
{
    [MenuItem("MTGB/Validate Multiple Card Databases")]
    public static void ValidateForAutomation()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before database validation.");
        string root = Path.Combine(PrintingKeyUtility.ApplicationRoot, "Logs", "multidatabase-validation");
        Directory.CreateDirectory(root);
        string officialA = Fixture(root, "a.mtgbdb", "MTGB/1", false);
        string officialB = Fixture(root, "b.mtgbdb", "SOS/65", false);
        string diy = Fixture(root, "core.mtgbdb", "TST/10", true);
        string patch = Fixture(root, "z-patch.mtgbdb", "MTGB/1", true);
        var cards = CardDatabaseRepository.LoadPathsForAutomation(officialA, officialB, diy);
        Require(cards.Count == 3 && cards.All(c => c.cardID == 1), "Distinct printings with the same local ID were lost.");
        Require(!cards.Single(c => c.PrintingKey == "MTGB/1").customContent
            && !cards.Single(c => c.PrintingKey == "SOS/65").customContent
            && cards.Single(c => c.PrintingKey == "TST/10").customContent, "Classification depends on filename rather than data.");
        var overridden = CardDatabaseRepository.LoadPathsForAutomation(officialA, officialB, diy, patch);
        Require(overridden.Count == 3 && overridden.Single(c => c.PrintingKey == "MTGB/1").customContent,
            "Last-loaded printing did not replace its predecessor.");
        var reversed = CardDatabaseRepository.LoadPathsForAutomation(patch, officialA, officialB, diy);
        Require(!reversed.Single(c => c.PrintingKey == "MTGB/1").customContent, "Precedence is not determined by load order.");
        Require(OnlineContentManifest.ComputeContentHash(cards) == OnlineContentManifest.ComputeContentHash(reversed),
            "Equivalent resolved content changed hash with its physical database origin.");
        Require(OnlineContentManifest.ComputeContentHash(cards) != OnlineContentManifest.ComputeContentHash(overridden),
            "Content classification does not participate in the online manifest.");
        foreach (var card in cards)
        {
            Require(CardImageLibrary.GetExpectedRelativePath(card).Replace('\\', '/') == "CardImages/" + card.setCode + "/" + card.CollectorNumber + ".png",
                "Art path used local ID instead of printing identity.");
            if (!card.customContent)
                Require(CardImageLibrary.TryLoadFullCard(card, out var art) && art != null,
                    "Official multi-library art failed to load for " + card.PrintingKey);
            Require(!string.IsNullOrWhiteSpace(card.luaScript), "Multi-library Lua lookup failed for " + card.PrintingKey);
            Require(MainMenuController.MatchesCollectionFilter(card, card.PrintingKey, card.customContent ? 2 : 1), "Category search omitted matching card.");
            Require(!MainMenuController.MatchesCollectionFilter(card, "", card.customContent ? 1 : 2), "Category search leaked the other class.");
        }
        var deck = new DeckDefinition { name = "__MultiDatabaseValidation_" + Guid.NewGuid().ToString("N") };
        string deckPath = null;
        try
        {
            foreach (var card in cards) Require(DeckRepository.AddCard(deck, card, 1) == 1, "Cannot add different printing with colliding local ID.");
            Require(deck.cards.Count == 3, "Deck merged distinct cards by local ID.");
            deckPath = DeckRepository.Save(deck);
            var reloaded = DeckRepository.Load(deckPath);
            var runtimeDeck = DeckRepository.BuildRuntimeDeck(reloaded, cards, out string warning);
            Require(runtimeDeck.Count == 3 && runtimeDeck.Select(c => c.PrintingKey).Distinct().Count() == 3
                && string.IsNullOrEmpty(warning), "Deck round-trip resolved the wrong printing.");
            Require(DeckRepository.ResolveCard(new DeckEntry { cardId = 1 }, cards) == null, "Ambiguous legacy ID should not resolve arbitrarily.");
            Require(DeckRepository.ResolveCard(new DeckEntry { cardId = 1, setCode = "MISSING", collectorNumber = "1" }, cards) == null,
                "Missing printing fell back to an unrelated card ID.");
        }
        finally { if (deckPath != null) File.Delete(deckPath); }
        ValidateRoomClassification();
        Debug.Log("Multiple database validation passed: local IDs, overrides, categories, art/Lua lookup, deck round-trip and online restrictions. Fixtures: " + root);
    }

    private static string Fixture(string root, string name, string printing, bool diy)
    {
        string path = Path.Combine(root, name);
        File.Copy(Path.Combine(CardDatabaseRepository.ExternalRoot, "core.mtgbdb"), path, true);
        using var database = NativeSqliteDatabase.OpenReadWriteDatabase(path);
        // Isolated fixtures retain the editor-authored feature graph, changing only identity/classification.
        database.ExecuteNonQuery("PRAGMA foreign_keys=ON; BEGIN IMMEDIATE;");
        try
        {
            database.ExecuteNonQuery($"DELETE FROM cards WHERE card_key <> '{printing}'; UPDATE cards SET card_id=1, content_kind={(diy ? 1 : 0)}; COMMIT;");
        }
        catch { database.ExecuteNonQuery("ROLLBACK;"); throw; }
        return path;
    }

    private static void ValidateRoomClassification()
    {
        var root = new GameObject("Room Category Validation");
        root.SetActive(false);
        try
        {
            var service = root.AddComponent<OnlineMatchService>();
            MethodInfo validate = typeof(OnlineMatchService).GetMethod("ValidateDeck", BindingFlags.Instance | BindingFlags.NonPublic);
            var deck = new DeckDefinition();
            var official = CardDatabaseRepository.FindByPrintingKey("MTGB", "1");
            DeckRepository.AddCard(deck, official, 60);
            object[] args = { deck, new OnlineRoomConfig(), null };
            Require((bool)validate.Invoke(service, args), "Official content rejected: " + args[2]);
            DeckRepository.AddCard(deck, CardDatabaseRepository.FindByPrintingKey("TST", "10"), 1);
            Require(!(bool)validate.Invoke(service, args), "DIY content accepted without room permission.");
            ((OnlineRoomConfig)args[1]).allowCustomContent = true;
            Require((bool)validate.Invoke(service, args), "DIY content rejected with room permission: " + args[2]);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
