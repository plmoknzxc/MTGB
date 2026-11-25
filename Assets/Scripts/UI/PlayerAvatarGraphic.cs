using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

internal sealed class PlayerPortrait
{
    internal readonly string Id;
    internal readonly Rect UV;
    readonly string resource, chinese, english, japanese;

    internal PlayerPortrait(string id,string chinese,string english,string japanese,string resource,Rect uv)
    {
        Id=id; this.chinese=chinese; this.english=english; this.japanese=japanese;
        this.resource=resource; UV=uv;
    }

    internal string Name=>DeckCompanion.Text(chinese,english,japanese);
    internal Texture2D Load()=>Resources.Load<Texture2D>(resource);
    internal bool Matches(string query)=>Name.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0 ||
        Id.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;
}

// Portrait IDs are player preferences. The atlases are only texture storage.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PlayerAvatarGraphic : MaskableGraphic
{
    // Both fields belong to one binding. A script reload must not restore the
    // texture without the portrait that supplies its UV coordinates.
    [NonSerialized] Texture2D portraitAtlas;
    PlayerPortrait portrait;
    public override Texture mainTexture => portraitAtlas!=null?portraitAtlas:Texture2D.whiteTexture;

    internal const string DefaultId="emperor", RivalId="fox";
    const string Tide="Menu/EmperorTide/TideDuelPortraits", Sanctum="Menu/SanctumDuelPortraits";
    static readonly Rect Left=new(0,0,.5f,1), Right=new(.5f,0,.5f,1);
    internal static readonly PlayerPortrait[] Catalog={
        new(DefaultId,"飘萍皇","The Wandering Emperor","放浪皇",Tide,Left),
        new(RivalId,"狐面武士","Fox-mask samurai","狐面の侍",Tide,Right),
        new("dusk-dragon","暮影龙","Dusk dragon","黄昏の竜",Sanctum,Left),
        new("moon-dragon","银月龙","Moon dragon","銀月の竜",Sanctum,Right)
    };
    static readonly Dictionary<string,PlayerPortrait> ById=Catalog.ToDictionary(item=>item.Id,StringComparer.Ordinal);
    internal static bool Valid(string id)=>id!=null && ById.ContainsKey(id);
    internal static PlayerPortrait Resolve(string id)=>id!=null && ById.TryGetValue(id,out var item)?item:Catalog[0];

    internal void SetAvatar(string id)
    {
        var next=Resolve(id);
        if(portrait==next)return;
        portrait=next;
        portraitAtlas=next.Load();
        SetMaterialDirty();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();var r=rectTransform.rect;
        if(portraitAtlas!=null){
            Vector2 uv = portrait.UV.center;
            float radius=Mathf.Min(r.width,r.height)*.45f;
            mesh.AddVert(r.center,Color.white,uv);
            for(int i=0;i<=48;i++){
                float angle=i*Mathf.PI*2/48;
                var n=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                mesh.AddVert(r.center+radius*n,Color.white,uv+Vector2.Scale(n,portrait.UV.size*.496f));
                if(i>0)mesh.AddTriangle(0,i,i+1);
            }
            return;
        }
        Ellipse(mesh,new Vector2(r.center.x,r.center.y+r.height*.16f),r.width*.16f,r.height*.18f);
        Ellipse(mesh,new Vector2(r.center.x,r.center.y-r.height*.24f),r.width*.32f,r.height*.25f);
    }
    private void Ellipse(VertexHelper mesh,Vector2 center,float x,float y)
    {
        int start=mesh.currentVertCount;mesh.AddVert(center,color,Vector2.zero);
        for(int i=0;i<=40;i++) {
            float angle=i*Mathf.PI*2/40;
            mesh.AddVert(center+new Vector2(Mathf.Cos(angle)*x,Mathf.Sin(angle)*y),color,Vector2.zero);
            if(i>0)mesh.AddTriangle(start,start+i,start+i+1);
        }
    }
}
