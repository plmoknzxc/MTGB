using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Counter chips on the table and the inspector draw from the same atlas.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PermanentCounterIcon : MaskableGraphic
{
    static readonly Dictionary<string,Sprite> artwork=new(StringComparer.OrdinalIgnoreCase);
    static bool loaded;
    Sprite sprite;
    public override Texture mainTexture => sprite!=null?sprite.texture:base.mainTexture;

    static Sprite Artwork(string key)
    {
        if(!loaded){
            KeywordIcons.LoadAtlas("UI/PermanentCounters/Artwork","UI/PermanentCounters/Index",artwork);
            loaded=true;
        }
        return key!=null&&artwork.TryGetValue(key,out var image)?image:null;
    }
    public static Sprite Symbol(string kind)
    {
        var image=Artwork(kind);
        if(image!=null)return image;
        // Forge can name keyword counters in title case, including "First strike".
        KeywordIcons.TryLoad(kind?.Replace(' ','_'),out var keyword);
        if(keyword!=null)return keyword;
        uint hash=2166136261;
        foreach(char c in kind??"")hash=unchecked((hash^c)*16777619);
        return Artwork("SEAL_"+(hash%64));
    }
    public static Sprite Plate(string kind)=>Artwork(kind switch {
        "P1P1"=>"PLATE_P1P1","M1M1"=>"PLATE_M1M1","MORE"=>"PLATE_MORE","LORE"=>"PLATE_LORE",_=>"PLATE_NEUTRAL"
    });

    public string Kind { get; private set; }
    public void Bind(string kind)
    {
        Kind=kind;
        sprite=Symbol(kind);
        SetMaterialDirty();
        SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if(sprite==null)return;
        var r=GetPixelAdjustedRect();
        var uv=UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
        var tint=new Color(1,1,1,color.a);
        mesh.AddVert(new Vector2(r.xMin,r.yMin),tint,new Vector2(uv.x,uv.y));
        mesh.AddVert(new Vector2(r.xMin,r.yMax),tint,new Vector2(uv.x,uv.w));
        mesh.AddVert(new Vector2(r.xMax,r.yMax),tint,new Vector2(uv.z,uv.w));
        mesh.AddVert(new Vector2(r.xMax,r.yMin),tint,new Vector2(uv.z,uv.y));
        mesh.AddTriangle(0,1,2);mesh.AddTriangle(2,3,0);
    }
}
