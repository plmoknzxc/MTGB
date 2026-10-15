using System;
using System.Collections.Generic;

[Serializable]
public sealed class DeckDefinition
{
    public string name = "New Deck";
    // Local file identity, never included in the network deck payload.
    [NonSerialized] public string sourcePath;
    public string format = "standard";
    public string banlist = "current";
    public List<DeckEntry> commanders = new List<DeckEntry>();
    public List<DeckEntry> cards = new List<DeckEntry>();
    public List<DeckEntry> sideboard = new List<DeckEntry>();
    // A designation referencing one existing sideboard card, not another copy.
    public DeckEntry companion;
    public List<DeckEntry> attractions = new List<DeckEntry>();
    public List<DeckEntry> contraptions = new List<DeckEntry>();
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
