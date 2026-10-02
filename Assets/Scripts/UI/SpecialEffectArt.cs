using System;
using System.Linq;
using UnityEngine;

/// <summary>Exact local helper-card faces, separate from playable card definitions.</summary>
public static class SpecialEffectArt
{
    [Serializable] private sealed class Localized { public string language,key,source; public int face; }
    [Serializable] private sealed class Entry { public string name,key,source; public int face; public Localized[] localized; }
    [Serializable] private sealed class Index { public Entry[] entries; }
    private static Entry[] entries;
    private static string NameKey(string name)=>new string((name??"").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    public static bool TryLoad(string name,out Sprite sprite)
    {
        sprite=null;
        if(entries==null) {
            var data=Resources.Load<TextAsset>("SpecialEffectArt");
            entries=data==null?Array.Empty<Entry>():JsonUtility.FromJson<Index>(data.text).entries??Array.Empty<Entry>();
        }
        var entry=entries.FirstOrDefault(x=>NameKey(x.name)==NameKey(name));
        var local=entry?.localized?.FirstOrDefault(x=>x.language==GamePreferences.Language);
        if(local!=null&&CardImageLibrary.TryLoadPrintingKey(local.key,local.face,local.language,out sprite))return true;
        return entry!=null && CardImageLibrary.TryLoadPrintingKey(entry.key,entry.face,out sprite);
    }
}
