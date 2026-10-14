using System;
using System.Linq;
using UnityEngine;

/// <summary>Full combined scan, linked to an exact physical printing and Meld face.</summary>
public static class MeldCardArt
{
    [Serializable] private sealed class Entry { public string key,state,name,artKey; }
    [Serializable] private sealed class Index { public Entry[] entries; }
    private static Entry[] entries;

    public static bool TryLoad(ForgeCardView card,out Sprite sprite)
    {
        sprite=null;
        if(card==null||card.faceDown||card.stateName!="Meld")return false;
        if(entries==null) {
            var asset=Resources.Load<TextAsset>("MeldCardArt");
            entries=asset==null?Array.Empty<Entry>():JsonUtility.FromJson<Index>(asset.text).entries??Array.Empty<Entry>();
        }
        var matches=entries.Where(e=>e.key==card.printingKey&&e.state==card.stateName&&e.name==card.name).ToArray();
        return matches.Length==1&&CardImageLibrary.TryLoadPrintingKey(matches[0].artKey,out sprite);
    }
    public static bool TryLoad(CardDefinition card,out Sprite sprite)
    {
        sprite=null;
        var back=card?.features?.FirstOrDefault(f=>f.faceIndex==1&&f.role==CardFeatureRole.Primary);
        return back!=null&&TryLoad(new ForgeCardView{printingKey=card.PrintingKey,stateName="Meld",name=back.cardName},out sprite);
    }
}
