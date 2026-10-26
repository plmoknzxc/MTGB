#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public static class OnlineRoomFormatValidation
{
    [MenuItem("MTGB/验证/联机/房间赛制选择")]
    public static string Run()
    {
        var owner = new GameObject("RoomFormatValidation"); owner.SetActive(false);
        var service = owner.AddComponent<OnlineMatchService>();
        try
        {
            var catalog = CardDatabaseRepository.LoadAll();
            var deck = DeckRepository.CreateDefaultDeck(catalog);
            var method = typeof(OnlineMatchService).GetMethod("ValidateDeck", BindingFlags.Instance | BindingFlags.NonPublic);
            bool Accept(DeckDefinition value, string format)
                => (bool)method.Invoke(service, new object[] { value, new OnlineRoomConfig { formatId = format }, null });
            Require(Accept(deck, "none") && Accept(deck, "legacy"), "Legal deck rejected");
            Require(!Accept(deck, "standard"), "Off-format deck accepted");
            Require(!Accept(deck, "commander") && !Accept(deck, "invalid"), "Unsupported format accepted");
            DeckEntry Entry(string name, int count = 1)
            {
                var card = catalog.First(c => c.cardName == name && !c.forgeAmbiguous && !c.customContent);
                return new DeckEntry { cardId = card.cardID, setCode = card.setCode, collectorNumber = card.CollectorNumber, count = count };
            }
            foreach (var format in new[] { "duelcommander", "brawl" })
            {
                var commanderDeck = new DeckDefinition { format = format, cards = new() { Entry("Plains", 99) }, commanders = new() { Entry("Isamaru, Hound of Konda") }, sideboard = new() };
                Require(Accept(commanderDeck, format), format + ": legal 99+1 rejected");
                commanderDeck.cards[0].count = 59;
                Require(!Accept(commanderDeck, format), format + ": 60-card deck accepted");
                commanderDeck.cards[0].count = 99;
                commanderDeck.commanders.Clear();
                Require(!Accept(commanderDeck, format), format + ": missing commander accepted");
                var commandConfig = (OnlineRoomConfig)typeof(OnlineMatchService).GetMethod("SanitizeRoomConfig", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { new OnlineRoomConfig { formatId = format, startingLife = 40, bestOf = 3, allowCustomContent = true, useAnte = true } });
                Require(commandConfig.startingLife == (format == "brawl" ? 25 : 20) && commandConfig.bestOf == (format == "brawl" ? 1 : 3) && !commandConfig.allowCustomContent && !commandConfig.useAnte, format + ": authoritative room rules lost");
            }
            var lutri = Entry("Lutri, the Spellchaser");
            var companionDeck = new DeckDefinition { format = "duelcommander", cards = new() { Entry("Plains", 99) }, commanders = new() { Entry("Kenrith, the Returned King") }, sideboard = new() { lutri }, companion = DeckCompanion.Copy(lutri) };
            Require(!Accept(companionDeck, "duelcommander"), "Duel banned companion accepted");
            companionDeck.companion = null; companionDeck.sideboard.Clear(); companionDeck.cards[0].count = 98; companionDeck.cards.Add(lutri);
            Require(Accept(companionDeck, "duelcommander"), "Companion-only ban incorrectly bans main-deck Lutri");
            var legality = new DeckFormatLegality(); legality.PrepareForValidation("duelcommander", catalog);
            var lutriCard = DeckRepository.ResolveCard(lutri, catalog);
            Require(legality.Get(lutriCard, "duelcommander", DeckFormatLegality.Current, true) == "legal" && legality.Get(lutriCard, "duelcommander", DeckFormatLegality.Current, false, true) == "banned", "Commander/companion ban roles conflated");
            var restricted = JsonUtility.FromJson<DeckDefinition>(JsonUtility.ToJson(deck));
            var recall = catalog.First(card => card.cardName == "Ancestral Recall" && !card.forgeAmbiguous);
            restricted.cards.Add(new DeckEntry { cardId = recall.cardID, setCode = recall.setCode, collectorNumber = recall.CollectorNumber, count = 2 });
            Require(!Accept(restricted, "vintage"), "Two restricted copies accepted");
            restricted.cards[restricted.cards.Count - 1].count = 1;
            Require(Accept(restricted, "vintage"), "Single restricted copy rejected");
            var config = new OnlineRoomConfig { formatId = "modern", bestOf = 3 };
            var sanitized = (OnlineRoomConfig)typeof(OnlineMatchService).GetMethod("SanitizeRoomConfig", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { config });
            var copy = JsonUtility.FromJson<OnlineRoomConfig>(JsonUtility.ToJson(sanitized));
            Require(copy.formatId == "modern" && copy.bestOf == 3, "Format lost in room configuration");
            const string result = "PASS: room format roundtrip, legal deck acceptance, illegal deck rejection, Vintage restricted count, Duel/Brawl 100-card structure and fixed room rules, unsupported variants rejected. Native commander eligibility is validated separately.";
            Debug.Log(result); return result;
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
