using System;
using System.Collections.Generic;

[Serializable]
public sealed class DeckDefinition
{
    public string name = "New Deck";
    public List<DeckEntry> commanders = new List<DeckEntry>();
    public List<DeckEntry> cards = new List<DeckEntry>();
    public List<DeckEntry> sideboard = new List<DeckEntry>();
}

[Serializable]
public sealed class DeckEntry
{
    public string setCode;
    public string collectorNumber;
    public int cardId;
    public int count = 1;

    public string PrintingKey => string.IsNullOrWhiteSpace(setCode) || string.IsNullOrWhiteSpace(collectorNumber)
        ? string.Empty
        : $"{setCode.Trim()}/{collectorNumber.Trim()}";
}
