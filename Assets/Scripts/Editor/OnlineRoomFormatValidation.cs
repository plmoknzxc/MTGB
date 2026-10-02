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
            Require(!Accept(deck, "commander") && !Accept(deck, "duelcommander") && !Accept(deck, "brawl") && !Accept(deck, "invalid"), "Unsupported format accepted");
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
            const string result = "PASS: room format roundtrip, legal deck acceptance, illegal deck rejection, Vintage restricted count, unsupported variants rejected.";
            Debug.Log(result); return result;
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
