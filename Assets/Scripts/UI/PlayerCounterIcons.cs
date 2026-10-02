using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Local Forge icon atlas. This supplies assets without choosing HUD placement.</summary>
public static class PlayerCounterIcons
{
    [Serializable] private sealed class Entry { public string kind; public int x,y,width,height; }
    [Serializable] private sealed class Index { public Entry[] entries; }
    private static readonly Dictionary<string,Sprite> icons=new Dictionary<string,Sprite>(StringComparer.OrdinalIgnoreCase);
    private static bool loaded;
    public static bool TryLoad(string kind,out Sprite sprite)
    {
        if(!loaded) {
            loaded=true;
            var texture=Resources.Load<Texture2D>("UI/PlayerCounters/ForgeIcons");
            var data=Resources.Load<TextAsset>("UI/PlayerCounters/Index");
            if(texture!=null && data!=null)foreach(var entry in JsonUtility.FromJson<Index>(data.text).entries) {
                var icon=Sprite.Create(texture,new Rect(entry.x,texture.height-entry.y-entry.height,entry.width,entry.height),new Vector2(.5f,.5f));
                icon.name=entry.kind;icons[entry.kind]=icon;
            }
        }
        sprite=null;return kind!=null && icons.TryGetValue(kind,out sprite);
    }
}
