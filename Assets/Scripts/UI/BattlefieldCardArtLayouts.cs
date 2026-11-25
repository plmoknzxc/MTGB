using System;
using System.IO;
using UnityEngine;

// Overrides describe the selected printing, not the rules identity/card name.
// Rects use normalized source-image coordinates, with the origin at bottom left.
[CreateAssetMenu(menuName = "MTGB/Battlefield card art layouts")]
public sealed class BattlefieldCardArtLayouts : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string printingKey;
        public bool backFace;
        public bool keepFullScan;
        public Rect illustration = new Rect(.07f, .455f, .86f, .425f);
        public bool rotateClockwise;
        public Vector2 focus = new Vector2(.5f, 1);
    }
    public Entry[] entries = Array.Empty<Entry>();

    public Entry Find(Sprite art)
    {
        if(!CardImageLibrary.TryGetImageSource(art,out string path) || string.Equals(Path.GetExtension(path),".image",StringComparison.OrdinalIgnoreCase)
            ||path.Replace('\\','/').Contains("/CustomCardImages/",StringComparison.OrdinalIgnoreCase)) return null;
        string set = new DirectoryInfo(Path.GetDirectoryName(path)).Name;
        string number = Path.GetFileNameWithoutExtension(path);
        foreach(var entry in entries ?? Array.Empty<Entry>())
            if(entry != null && string.Equals(entry.printingKey+(entry.backFace?"_2":""), set+"/"+number, StringComparison.OrdinalIgnoreCase)) return entry;
        return null;
    }

    public static Rect IllustrationPixels(ForgeCardView value, Sprite art, string visibleType, Entry layout,
        float aspect, out bool rotated, out bool sideIllustration)
    {
        var r=art.textureRect;
        var region=new Rect(.07f,.455f,.86f,.425f);
        var focus=new Vector2(.5f,1);
        // Original type describes the front face, even when the visible back is a Saga.
        string printedType=value.backFace||string.IsNullOrEmpty(value.originalType)?visibleType??"":value.originalType;
        bool saga=printedType.Contains("Saga",StringComparison.OrdinalIgnoreCase)||printedType.Contains("传纪")||printedType.Contains("傳紀")||printedType.Contains("英雄譚");
        bool sideArt=printedType.Contains("Case",StringComparison.OrdinalIgnoreCase)||printedType.Contains("Class",StringComparison.OrdinalIgnoreCase)
            ||printedType.Contains("案件")||printedType.Contains("职业")||printedType.Contains("職業")||printedType.Contains("事件")||printedType.Contains("クラス");
        sideIllustration=saga||sideArt;
        if(sideIllustration){region=new Rect(saga?.50f:.08f,.165f,.415f,.72f);focus=new Vector2(.5f,saga?.65f:.75f);}
        if(value.planeswalker)region=new Rect(.09f,.45f,.82f,.45f);
        rotated=value.battle && r.height>r.width;
        if(value.battle)region=rotated?new Rect(.145f,.09f,.405f,.85f):new Rect(.09f,.45f,.85f,.405f);
        if(layout!=null){region=layout.illustration;rotated=layout.rotateClockwise;focus=layout.focus;}
        return Fit(r,region,rotated?1/aspect:aspect,focus);
    }

    public static Rect Fit(Rect textureRect, Rect normalized, float aspect, Vector2 focus)
    {
        // Invalid authoring values cannot sample a neighbouring atlas sprite.
        float x = Mathf.Clamp(normalized.x,0,.999f), y = Mathf.Clamp(normalized.y,0,.999f);
        float w = Mathf.Clamp(normalized.width, .001f, 1-x);
        float h = Mathf.Clamp(normalized.height, .001f, 1-y);
        var crop = new Rect(textureRect.x+x*textureRect.width, textureRect.y+y*textureRect.height,
            w*textureRect.width, h*textureRect.height);
        if(crop.width/crop.height > aspect){float fitted=crop.height*aspect;crop.x+=(crop.width-fitted)*Mathf.Clamp01(focus.x);crop.width=fitted;}
        else{float fitted=crop.width/aspect;crop.y+=(crop.height-fitted)*Mathf.Clamp01(focus.y);crop.height=fitted;}
        return crop;
    }
}
