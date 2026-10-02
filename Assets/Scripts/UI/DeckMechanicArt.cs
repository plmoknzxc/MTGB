using System;
using System.Collections.Generic;

// Display references only. Previewing Undercity does not make it a legal
// choice for the ordinary venture action; the engine owns dungeon selection.
public static class DeckMechanicArt
{
    public static bool IsDungeon(DeckKeywordGlossary.Entry entry) => entry != null &&
        (entry.English == "Venture into the Dungeon" || entry.English == "Venture into Undercity" || entry.English == "The Initiative");
    public static bool IsRing(DeckKeywordGlossary.Entry entry) => entry?.English == "The Ring Tempts You";
    public static bool HasArt(DeckKeywordGlossary.Entry entry) => IsDungeon(entry) || IsRing(entry);

    public static List<DeckRelatedTokenArt.Art> GetReferences(CardDefinition card, bool loadImages,
        DeckKeywordGlossary.Entry mechanic = null, string format = null)
    {
        var result = new List<DeckRelatedTokenArt.Art>();
        if (card == null) return result;
        var found = DeckKeywordGlossary.Find(card.description, card:card);
        found.AddRange(DeckKeywordGlossary.Find(GamePreferences.CardRules(card), card:card));
        if (card.features != null) foreach (var face in card.features)
            if (face != null) found.AddRange(DeckKeywordGlossary.Find(face.description, card:card));
        bool venture = mechanic == null ? found.Exists(e => e.English == "Venture into the Dungeon") : mechanic.English == "Venture into the Dungeon";
        bool undercity = mechanic == null ? found.Exists(e => e.English == "The Initiative" || e.English == "Venture into Undercity") :
            mechanic.English == "The Initiative" || mechanic.English == "Venture into Undercity";
        bool ring = mechanic == null ? found.Exists(IsRing) : IsRing(mechanic);
        if (venture)
        {
            Add("Dungeon of the Mad Mage", "dungeon");
            Add("Lost Mine of Phandelver", "dungeon");
            Add("Tomb of Annihilation", "dungeon");
        }
        // A keyword link describes that action's choices. Only a whole-card
        // overview in a Commander format also includes the other dungeon.
        if (undercity || mechanic == null && venture &&
            (format == "commander" || format == "duelcommander" || format == "brawl")) Add("Undercity", "dungeon");
        if (ring) Add("The Ring", "ring");
        return result;

        void Add(string name, string group)
        {
            UnityEngine.Sprite sprite = null;
            if (loadImages) SpecialEffectArt.TryLoad(name, out sprite);
            result.Add(new DeckRelatedTokenArt.Art { name=name, key="helper:"+name, sprite=sprite, group=group });
        }
    }
}
