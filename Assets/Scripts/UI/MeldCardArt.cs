using System;
using System.Linq;
using UnityEngine;

/// <summary>Full combined scan, linked to an exact physical printing and Meld face.</summary>
public static class MeldCardArt
{
    [Serializable] private sealed class Entry { public string key,state,name,artKey,partnerKey,partnerName; }
    [Serializable] private sealed class Index { public Entry[] entries; }
    private static Entry[] entries;

    private static Entry[] Entries() {
        if(entries==null) {
            var asset=Resources.Load<TextAsset>("MeldCardArt");
            entries=asset==null?Array.Empty<Entry>():JsonUtility.FromJson<Index>(asset.text).entries??Array.Empty<Entry>();
        }
        return entries;
    }

    public static bool TryPair(CardDefinition card,System.Collections.Generic.IDictionary<string,CardDefinition> catalog,
        out CardDefinition primary,out CardDefinition partner)
    {
        primary=partner=null;
        if(card?.forgeSplitType!="Meld")return false;
        // Preserve the selected physical part even if several alternate arts share a partner.
        foreach(var e in Entries().OrderByDescending(e=>e.key==card.PrintingKey||e.partnerKey==card.PrintingKey)
            .ThenByDescending(e=>e.key.StartsWith(card.setCode+"/",StringComparison.OrdinalIgnoreCase))){
            if(e.partnerKey==null || !catalog.TryGetValue(e.key,out var a) || !catalog.TryGetValue(e.partnerKey,out var b))continue;
            if(a.cardName!=card.cardName && b.cardName!=card.cardName)continue;
            if(b.cardName!=e.partnerName)continue;
            primary=a.cardName==card.cardName?card:a;
            partner=b.cardName==card.cardName?card:b;
            return true;
        }
        return false;
    }

    public static bool TryLoad(ForgeCardView card,out Sprite sprite)
    {
        sprite=null;
        if(card==null||card.faceDown||card.stateName!="Meld")return false;
        var matches=Entries().Where(e=>e.key==card.printingKey&&e.state==card.stateName&&e.name==card.name).ToArray();
        return matches.Length==1&&CardImageLibrary.TryLoadPrintingKey(matches[0].artKey,out sprite);
    }
    public static bool TryLoad(CardDefinition card,out Sprite sprite)
    {
        sprite=null;
        var back=card?.features?.FirstOrDefault(f=>f.faceIndex==1&&f.role==CardFeatureRole.Primary);
        return back!=null&&TryLoad(new ForgeCardView{printingKey=card.PrintingKey,stateName="Meld",name=back.cardName},out sprite);
    }
}
