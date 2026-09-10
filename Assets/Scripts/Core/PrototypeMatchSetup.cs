using System.Collections.Generic;

public static class PrototypeMatchSetup
{
    private const string TrainingIslandLua =
        "local Card = {}\n\n" +
        "function Card.OnInit(self)\n" +
        "    Game:RegisterManaAbility(self.Card, \"{U}\")\n" +
        "end\n\n" +
        "return Card";

    public static void PrepareDecks(List<CardDefinition> player1Deck, List<CardDefinition> player2Deck)
    {
        if (player1Deck == null || player2Deck == null) return;

        EnsurePlayableSampleDecks(player1Deck, player2Deck);
        EnsureCardInDeck(player1Deck, "SOS", "65");
    }

    public static void MoveKnownCardsToTop(List<CardDefinition> player1Deck)
    {
        MoveCardToTopOfDeck(player1Deck, "SOS/65");
    }

    public static void SeedOpponentStartingBattlefield(List<RuntimeCard> opponentBattlefield)
    {
        if (opponentBattlefield == null) return;

        CardDefinition land = CardDatabaseRepository.FindByPrintingKey("MTGB", "1")
            ?? CreateRuntimeCardData("Training Island", 2001, CardType.Land, "Land\n{T}: Add {U} to your mana pool.", 0, 0, 0, TrainingIslandLua, string.Empty);
        CardDefinition bear = CardDatabaseRepository.FindByPrintingKey("MTGB", "8")
            ?? CreateRuntimeCardData("Arena Bear", 2002, CardType.Creature, "Creature\nA simple 2/2 creature for combat testing.", 2, 2, 2, string.Empty, string.Empty);

        RuntimeCard startingLand = new RuntimeCard(land, 2);
        RuntimeCard startingCreature = new RuntimeCard(bear, 2);
        opponentBattlefield.Add(startingLand);
        opponentBattlefield.Add(startingCreature);
    }

    private static void EnsurePlayableSampleDecks(List<CardDefinition> player1Deck, List<CardDefinition> player2Deck)
    {
        if (player1Deck.Count > 0 && player2Deck.Count > 0) return;

        CardDefinition land = CardDatabaseRepository.FindByPrintingKey("MTGB", "1")
            ?? CreateRuntimeCardData("Training Island", 1001, CardType.Land, "Land\n{T}: Add {U} to your mana pool.", 0, 0, 0, TrainingIslandLua, string.Empty);
        CardDefinition bear = CardDatabaseRepository.FindByPrintingKey("MTGB", "8")
            ?? CreateRuntimeCardData("Arena Bear", 1002, CardType.Creature, "Creature\nA simple 2/2 creature for combat testing.", 2, 2, 2, string.Empty, string.Empty);
        CardDefinition lightning = CardDatabaseRepository.FindByPrintingKey("MTGB", "10")
            ?? CreateRuntimeCardData("Whitebox Lightning", 1003, CardType.Sorcery, "Sorcery\nDeal 4 damage to the opponent.", 1, 0, 0, string.Empty, "Deal4ToOpponent");
        CardDefinition insight = CardDatabaseRepository.FindByPrintingKey("MTGB", "9")
            ?? CreateRuntimeCardData("Arena Insight", 1004, CardType.Sorcery, "Sorcery\nDraw 1 card.", 1, 0, 0, string.Empty, "Draw1");
        CardDefinition adept = CardDatabaseRepository.FindByPrintingKey("MTGB", "11")
            ?? CreateRuntimeCardData("Rune Adept", 1005, CardType.Creature, "Creature\nWhen enters battlefield, gets +1/+1.", 2, 1, 1, string.Empty, "SelfPlus1Plus1");
        ConfigurePrototypeTargeting(lightning);

        if (player1Deck.Count == 0)
        {
            AddSampleDeck(player1Deck, land, bear, lightning, insight, adept);
        }

        if (player2Deck.Count == 0)
        {
            AddSampleDeck(player2Deck, land, bear, lightning, insight, adept);
        }
    }

    private static void EnsureCardInDeck(List<CardDefinition> deck, string setCode, string collector)
    {
        if (deck == null) return;
        for (int i = 0; i < deck.Count; i++)
        {
            if (deck[i] != null && deck[i].PrintingKey == $"{setCode}/{collector}") return;
        }

        CardDefinition card = CardDatabaseRepository.FindByPrintingKey(setCode, collector);
        if (card != null) deck.Add(card);
    }

    private static void MoveCardToTopOfDeck(List<CardDefinition> deck, string printingKey)
    {
        if (deck == null) return;
        for (int i = 0; i < deck.Count; i++)
        {
            if (deck[i] == null || deck[i].PrintingKey != printingKey) continue;

            CardDefinition card = deck[i];
            deck.RemoveAt(i);
            deck.Insert(0, card);
            return;
        }
    }

    private static CardDefinition CreateRuntimeCardData(string cardName, int cardID, CardType cardTypes, string description, int cost, int attack, int defence, string luaScript, string effectKey)
    {
        CardDefinition card = new CardDefinition();
        card.name = cardName;
        card.cardName = cardName;
        card.cardID = cardID;
        card.setCode = "MTGB";
        card.collectorNumber = cardID >= 2000 ? cardID - 1000 : cardID;
        card.collectorNumberText = card.collectorNumber.ToString();
        card.cardTypes = cardTypes;
        card.description = description;
        card.manaCost = cost > 0 ? $"{{{cost}}}" : string.Empty;
        card.attack = attack;
        card.defence = defence;
        card.luaScript = luaScript;
        card.effectKey = effectKey;
        return card;
    }

    private static void AddSampleDeck(List<CardDefinition> deck, CardDefinition land, CardDefinition bear, CardDefinition lightning, CardDefinition insight, CardDefinition adept)
    {
        deck.Add(land);
        deck.Add(land);
        deck.Add(land);
        deck.Add(bear);
        deck.Add(lightning);
        deck.Add(land);
        deck.Add(insight);
        deck.Add(adept);
        deck.Add(land);
        deck.Add(bear);
    }

    private static void ConfigurePrototypeTargeting(CardDefinition card)
    {
        if (card == null) return;
        if (card.PrintingKey == "MTGB/10" || string.Equals(card.effectKey, "Deal4ToOpponent", System.StringComparison.Ordinal))
        {
            card.targetMode = CardTargetMode.AnyCreatureOrPlayer;
        }
    }
}
