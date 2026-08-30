using System.Collections.Generic;

public static class MatchStartConfig
{
    private static DeckDefinition playerDeck;
    private static DeckDefinition opponentDeck;
    private static int? deterministicSeed;
    private static int? configuredStartingLife;
    private static int? configuredFirstPlayer;

    public static bool HasConfiguredDecks => playerDeck != null;

    public static void SetDecks(DeckDefinition player, DeckDefinition opponent)
    {
        playerDeck = Clone(player);
        opponentDeck = Clone(opponent ?? player);
    }

    public static void SetDeterministicSeed(int seed)
    {
        deterministicSeed = seed;
    }

    public static bool TryGetDeterministicSeed(out int seed)
    {
        if (deterministicSeed.HasValue)
        {
            seed = deterministicSeed.Value;
            return true;
        }

        seed = 0;
        return false;
    }

    public static void SetOnlineRules(int startingLife, int firstPlayer)
    {
        configuredStartingLife = System.Math.Max(1, System.Math.Min(200, startingLife));
        configuredFirstPlayer = firstPlayer == 2 ? 2 : 1;
    }

    public static bool TryGetOnlineRules(out int startingLife, out int firstPlayer)
    {
        startingLife = configuredStartingLife ?? 0;
        firstPlayer = configuredFirstPlayer ?? 0;
        return configuredStartingLife.HasValue && configuredFirstPlayer.HasValue;
    }

    public static void ClearOnlineRules()
    {
        configuredStartingLife = null;
        configuredFirstPlayer = null;
    }

    public static void ClearOnlineSession()
    {
        playerDeck = null;
        opponentDeck = null;
        deterministicSeed = null;
        ClearOnlineRules();
    }

    public static bool ApplyTo(List<CardDefinition> player1Deck, List<CardDefinition> player2Deck, out string warning)
    {
        warning = string.Empty;
        if (playerDeck == null || player1Deck == null || player2Deck == null) return false;

        IReadOnlyList<CardDefinition> cards = CardDatabaseRepository.LoadAll();
        List<CardDefinition> playerRuntimeDeck = DeckRepository.BuildRuntimeDeck(playerDeck, cards, out string playerWarning);
        List<CardDefinition> opponentRuntimeDeck = DeckRepository.BuildRuntimeDeck(opponentDeck ?? playerDeck, cards, out string opponentWarning);
        if (playerRuntimeDeck.Count == 0 || opponentRuntimeDeck.Count == 0)
        {
            warning = "Selected deck could not be built.";
            return false;
        }

        player1Deck.Clear();
        player1Deck.AddRange(playerRuntimeDeck);
        player2Deck.Clear();
        player2Deck.AddRange(opponentRuntimeDeck);

        if (!string.IsNullOrEmpty(playerWarning)) warning = playerWarning;
        if (!string.IsNullOrEmpty(opponentWarning))
        {
            warning = string.IsNullOrEmpty(warning) ? opponentWarning : warning + " " + opponentWarning;
        }

        return true;
    }

    private static DeckDefinition Clone(DeckDefinition source)
    {
        if (source == null) return null;

        var clone = new DeckDefinition
        {
            name = source.name,
            commanders = CloneEntries(source.commanders),
            cards = CloneEntries(source.cards),
            sideboard = CloneEntries(source.sideboard)
        };

        return clone;
    }

    private static List<DeckEntry> CloneEntries(List<DeckEntry> source)
    {
        var clone = new List<DeckEntry>();
        if (source == null) return clone;

        for (int i = 0; i < source.Count; i++)
        {
            DeckEntry entry = source[i];
            if (entry == null) continue;
            clone.Add(new DeckEntry
            {
                setCode = entry.setCode,
                collectorNumber = entry.collectorNumber,
                cardId = entry.cardId,
                count = entry.count
            });
        }

        return clone;
    }
}
