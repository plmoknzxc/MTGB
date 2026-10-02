#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ForgeMigrationValidation
{
    private static OnlineMatchSession session;
    private static double deadline;
    private static int responses;
    public static bool IsRunning => session != null;
    public static Exception LastError { get; private set; }

    [MenuItem("MTGB/Forge/Validate Persistent Session")]
    public static void ValidateSession()
    {
        if (session != null) throw new InvalidOperationException("Session validation is already running.");
        LastError = null;
        CardDatabaseRepository.LoadAll();
        var deck = DeckRepository.Load(Path.Combine(DeckRepository.DeckRoot, "Starter.mdk"));
        Require(deck != null, "Missing Starter test deck.");
        string path = DeckRepository.Save(deck);
        var reloaded = DeckRepository.Load(path);
        Require(DeckRepository.GetTotalCount(reloaded) == 60, "Deck save/reload lost cards.");
        session = new OnlineMatchSession(Guid.NewGuid().ToString("N"), reloaded, reloaded, 42);
        responses = 0; deadline = EditorApplication.timeSinceStartup + 60;
        EditorApplication.update += TickSession;
    }

    private static void TickSession()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup < deadline, "Forge session validation timed out.");
            session.Poll();
            Require(session.Failure == null, session.Failure);
            if (session.GetView(1).outcome.isFinished)
            {
                Require(session.GetView(1).outcome.winnerPlayerId == 2, "Concession produced wrong winner.");
                session.Dispose(); session = null; EditorApplication.update -= TickSession;
                Debug.Log("MTGB Forge persistent session validation passed: saved deck, seat isolation, duplicate rejection, concession, process cleanup.");
                return;
            }
            for (int seat = 1; seat <= 2; seat++)
            {
                var view = session.GetView(seat);
                var request = view.forge.request;
                if (!view.forge.awaitingInput) continue;
                Require(!session.GetView(3 - seat).forge.awaitingInput && session.GetView(3 - seat).forge.request.requestId == 0, "Opponent received private request.");
                foreach (var player in view.forge.players)
                    if (player.seat != seat) Require(player.hand.Length == 0, "Opponent hand leaked.");
                if (responses >= 5)
                {
                    Require(session.ApplyAction(new GameAction { type = GameActionType.Concede, playerId = 1 }).accepted, "Concession rejected.");
                    return;
                }
                int index = Array.FindIndex(request.options, option => option.id == "ok");
                if (index < 0) index = 0;
                var action = new GameAction { type = GameActionType.ConfirmChoice, playerId = seat, sourceObjectId = request.requestId, optionId = index };
                Require(!session.ApplyAction(new GameAction { type = action.type, playerId = 3 - seat,
                    sourceObjectId = request.requestId, optionId = index }).accepted, "Other seat answered private input.");
                Require(!session.ApplyAction(new GameAction { type = action.type, playerId = seat,
                    sourceObjectId = request.requestId, optionId = request.options.Length }).accepted, "Out-of-range option accepted.");
                Require(!session.ApplyAction(new GameAction { type = action.type, playerId = seat,
                    sourceObjectId = request.requestId - 1, optionId = index }).accepted, "Stale choice accepted.");
                Require(session.ApplyAction(action).accepted, "Valid option rejected.");
                Require(!session.ApplyAction(action).accepted, "Duplicate option executed twice.");
                responses++;
                return;
            }
        }
        catch (Exception exception)
        {
            LastError = exception;
            session?.Dispose(); session = null; EditorApplication.update -= TickSession;
            Debug.LogException(exception);
        }
    }

    [MenuItem("MTGB/Forge/Validate Catalog")]
    public static void ValidateCatalog()
    {
        var cards = CardDatabaseRepository.LoadAll(true);
        Require(cards.Count > 90000, "Official printing catalog is unexpectedly incomplete.");
        var sample = cards.First(card => card.PrintingKey == "MH1/254");
        Require(sample.cardName == "Snow-Covered Forest", "MH1 printing mapped to the wrong card.");
        Require(CardDatabaseRepository.FindByPrintingKey("MTGB", "1") == null, "Legacy test printing must not be substituted.");
        var legacyId = new DeckEntry { cardId = cards[0].cardID, count = 1 };
        Require(DeckRepository.ResolveCard(legacyId, cards) == null && !DeckRepository.EntryMatchesCard(legacyId, cards[0]),
            "Legacy local ID silently mapped to a Forge printing.");
        Require(cards.Any(card => card.features.Count > 1), "Multi-part cards are missing.");
        Require(DeckRepository.GetTotalCount(DeckRepository.CreateDefaultDeck(cards)) == 60, "Starter deck is invalid.");
        var exceptions = new DeckDefinition();
        Require(DeckRepository.AddCard(exceptions, cards.First(card => card.cardName == "Seven Dwarves" && !card.forgeAmbiguous), 8) == 7,
            "Forge's seven-copy exception was lost.");
        Require(DeckRepository.AddCard(exceptions, cards.First(card => card.cardName == "Relentless Rats" && !card.forgeAmbiguous), 20) == 20,
            "Forge's unlimited-copy exception was lost.");
        foreach (var card in cards.Where(card => card.forgeAmbiguous))
            Require(DeckRepository.ResolveCard(new DeckEntry { setCode = card.setCode, collectorNumber = card.CollectorNumber }, cards) == null,
                "Ambiguous printing resolved silently: " + card.PrintingKey);
        foreach (string name in new[] { "Starter", "Forge-Validation" })
        {
            string path = Path.Combine(DeckRepository.DeckRoot, name + ".mdk");
            byte[] before = File.ReadAllBytes(path);
            var deck = DeckRepository.Load(path);
            Require(deck != null && DeckRepository.GetTotalCount(deck) == 60, "Test deck must contain 60 main cards: " + name);
            Require(deck.sideboard.Sum(entry => entry.count) <= 15, "Test sideboard is too large: " + name);
            foreach (var entry in deck.cards.Concat(deck.sideboard))
                Require(DeckRepository.ResolveCard(entry, cards) != null, "Test printing did not resolve: " + entry.PrintingKey);
            var copy = DeckRepository.Load(path);
            Require(!DeckRepository.EnforceCopyLimits(copy, cards), "Test deck exceeds Forge copy limits: " + name);
            Require(before.SequenceEqual(File.ReadAllBytes(path)), "Loading rewrote the saved deck.");
        }
        Debug.Log($"MTGB Forge catalog validation passed: {cards.Count} printings.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
