using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Editor designation operations; physical copies move between sections.</summary>
public static class DeckCommander
{
    public static bool IsFormat(string format)=>format=="commander"||format=="duelcommander"||format=="brawl";
    public static bool Has(DeckDefinition deck)=>deck?.commanders?.Any(e=>e!=null&&e.count>0)==true;
    public static bool TryAdapt(DeckDefinition deck,IEnumerable<string> legal)
    {
        if(deck==null||!IsFormat(deck.format)||Has(deck)||deck.sideboard==null||deck.sideboard.Sum(e=>Math.Max(0,e?.count??0))!=1)return false;
        var entry=deck.sideboard.Single(e=>e!=null&&e.count==1);
        if(legal?.Contains(entry.PrintingKey,StringComparer.OrdinalIgnoreCase)!=true)return false;
        deck.commanders??=new List<DeckEntry>();deck.commanders.Add(DeckCompanion.Copy(entry));
        deck.sideboard.Remove(entry);DeckCompanion.ClearMissing(deck);return true;
    }
    public static bool Select(DeckDefinition deck,CardDefinition card,IEnumerable<string> legal,bool partner,IEnumerable<string> partners,bool preferMain=false)
    {
        if(deck==null||card==null||!IsFormat(deck.format)||legal?.Contains(card.PrintingKey,StringComparer.OrdinalIgnoreCase)!=true)return false;
        deck.commanders??=new List<DeckEntry>();
        if(deck.commanders.Any(e=>e.PrintingKey==card.PrintingKey))return false;
        if(partner && (deck.commanders.Count!=1||partners?.Contains(card.PrintingKey,StringComparer.OrdinalIgnoreCase)!=true))return false;
        var list=preferMain?deck.cards:deck.sideboard;
        var from=list.FirstOrDefault(e=>e.count>0&&e.PrintingKey==card.PrintingKey);
        if(from==null){list=preferMain?deck.sideboard:deck.cards;from=list.FirstOrDefault(e=>e.count>0&&e.PrintingKey==card.PrintingKey);}
        if(!partner)foreach(var old in deck.commanders.ToArray())Remove(deck,old);
        if(from!=null){from.count--;if(from.count<=0)list.Remove(from);}
        deck.commanders.Add(new DeckEntry{setCode=card.setCode,collectorNumber=card.CollectorNumber,cardId=card.cardID,count=1});
        DeckCompanion.ClearMissing(deck);return true;
    }
    public static void Remove(DeckDefinition deck,DeckEntry entry)
    {
        if(!deck.commanders.Remove(entry))return;
        var side=deck.sideboard.FirstOrDefault(e=>e.PrintingKey==entry.PrintingKey);
        if(side==null){var copy=DeckCompanion.Copy(entry);copy.count=entry.count;deck.sideboard.Add(copy);}else side.count+=entry.count;
    }
    public static void MoveToMain(DeckDefinition deck,DeckEntry entry)
    {
        if(!deck.commanders.Remove(entry))return;
        MergeIntoMain(deck,entry);
    }
    public static void MergeIntoMain(DeckDefinition deck,DeckEntry entry)
    {
        var main=deck.cards.FirstOrDefault(e=>e.PrintingKey==entry.PrintingKey);
        if(main!=null)main.count+=entry.count;
        else {var copy=DeckCompanion.Copy(entry);copy.count=entry.count;deck.cards.Add(copy);}
    }
}
