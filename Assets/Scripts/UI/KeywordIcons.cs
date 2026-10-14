using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Exact Forge ability atlas regions; native keyword identities stay unchanged.</summary>
public static class KeywordIcons
{
    [Serializable] private sealed class Entry { public string kind; public int x,y,width,height; }
    [Serializable] private sealed class Index { public Entry[] entries; }
    private static readonly Dictionary<string,Sprite> icons=new(StringComparer.OrdinalIgnoreCase);
    private static bool loaded;

    public static bool TryLoad(string kind,out Sprite sprite)
    {
        if(!loaded) {
            loaded=true;
            var texture=Resources.Load<Texture2D>("UI/KeywordIcons/ForgeAbilities");
            var index=Resources.Load<TextAsset>("UI/KeywordIcons/Index");
            if(texture!=null&&index!=null)foreach(var entry in JsonUtility.FromJson<Index>(index.text).entries) {
                var icon=Sprite.Create(texture,new Rect(entry.x,texture.height-entry.y-entry.height,entry.width,entry.height),new Vector2(.5f,.5f));
                icon.name=entry.kind;icons[entry.kind]=icon;
            }
        }
        sprite=null;
        return kind!=null&&icons.TryGetValue(kind,out sprite);
    }

    // Details, rather than keyword presence, carry amounts/costs. Never infer a
    // payment from the name alone; compound/nonmana ward stays in the inspector.
    public static string Parameter(ForgeCardView card,string kind,out bool mana)
    {
        mana=false;
        var values=(card.currentKeywordDetails??Array.Empty<string>())
            .Where(d=>d.StartsWith(kind+":",StringComparison.OrdinalIgnoreCase))
            .Select(d=>d.Substring(kind.Length+1).Trim()).Distinct().ToArray();
        if(values.Length==0)return "";
        if(kind=="WARD") {
            if(values.Length!=1)return "…";
            string cost=values[0];
            if(Regex.IsMatch(cost,@"^(?:\{(?:[0-9]+|[WUBRGCXYZSP/]+)\})+$")) {mana=true;return cost;}
            if(Regex.IsMatch(cost,@"^(?:[0-9]+|[WUBRGCXYZSP/]+)(?: (?:[0-9]+|[WUBRGCXYZSP/]+))*$")) {
                mana=true;return string.Concat(cost.Split(' ').Select(t=>"{"+t+"}"));
            }
            return "…";
        }
        if(kind is "ANNIHILATOR" or "TOXIC") {
            var amounts=values.Select(v=>v.Split(':')[0]).ToArray();
            if(amounts.All(a=>int.TryParse(a,out _)))return string.Join("/",amounts);
        }
        return "";
    }
}
