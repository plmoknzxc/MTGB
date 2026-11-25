using TMPro;
using UnityEngine;

public sealed partial class BattlefieldWorldCard
{
    bool compactRoom;
    MeshRenderer[] roomFaces,roomShades,roomLocks;
    MeshRenderer roomSeam;
    TextMeshPro[] roomNames;

    void BuildRoomFace()
    {
        roomFaces=new MeshRenderer[2];roomShades=new MeshRenderer[2];
        roomNames=new TextMeshPro[2];roomLocks=new MeshRenderer[2];
        for(int i=0;i<2;i++){
            float x=i==0?-.245f:.245f;
            roomFaces[i]=Surface("Room art "+i,new Vector3(x,0,-.007f),new Vector2(.47f,.48f),Color.white);
            roomShades[i]=Surface("Room locked shade "+i,new Vector3(x,0,-.010f),new Vector2(.48f,.47f),new Color(.015f,.025f,.035f,.68f));
            roomNames[i]=Label("Room name "+i,new Vector3(x,.26f,-.027f),new Vector2(.41f,.11f),.82f,TextAlignmentOptions.Left);
            roomLocks[i]=Surface("Room lock icon "+i,new Vector3(x,-.025f,-.028f),Vector2.one*.24f,new Color(.88f,.91f,.90f,.95f));
            roomLocks[i].GetComponent<MeshFilter>().sharedMesh=table.RoomLockMesh;
        }
        roomSeam=Surface("Room divider",new Vector3(0,.0275f,-.024f),new Vector2(.012f,.605f),new Color(.10f,.16f,.19f));
    }

    void BindRoomFace(ForgeCardView value,Sprite art)
    {
        compactRoom=value.room&&compactFace&&art!=null;
        if(!compactRoom)return;
        if(roomFaces==null)BuildRoomFace();
        title.text="";
        compactKind.text=value.creature?"":CompactType(value,value.currentType??value.originalType??"");
        compactKind.transform.localPosition=new Vector3(0,-.263f,-.027f);
        compactKind.rectTransform.sizeDelta=new Vector2(.88f,.12f);
        compactKind.fontSize=.70f;compactKind.enableAutoSizing=true;
        compactKind.fontSizeMin=.50f;compactKind.fontSizeMax=.70f;
        compactKind.textWrappingMode=TextWrappingModes.NoWrap;
        compactKind.overflowMode=TextOverflowModes.Ellipsis;
        compactKind.alignment=TextAlignmentOptions.Center;
        if(!string.IsNullOrEmpty(stats.text)){
            stats.enableAutoSizing=false;stats.fontSize=1.15f;
            float width=Mathf.Clamp(stats.GetPreferredValues(stats.text).x+.04f,.23f,.48f);
            stats.transform.localPosition=new Vector3(.43f-width*.5f,-.263f,-.029f);
            stats.rectTransform.sizeDelta=new Vector2(width-.03f,.13f);
            stats.enableAutoSizing=true;stats.fontSizeMin=.60f;stats.fontSizeMax=1.15f;
            statsPlate.transform.localPosition=new Vector3(.43f-width*.5f,-.263f,-.024f);
            statsPlate.transform.localScale=new Vector3(width,.13f,1);
            float kindWidth=.86f-width-.025f;
            compactKind.transform.localPosition=new Vector3(-.43f+kindWidth*.5f,-.263f,-.027f);
            compactKind.rectTransform.sizeDelta=new Vector2(kindWidth,.13f);
            compactKind.alignment=TextAlignmentOptions.Left;
        }
        float nameY=aspect*.5f+compactHeaderHeight*.5f;
        float dividerTop=aspect*.5f+compactHeaderHeight-.022f;
        roomSeam.transform.localPosition=new Vector3(0,(dividerTop-.275f)*.5f,-.024f);
        roomSeam.transform.localScale=new Vector3(.012f,dividerTop+.275f,1);
        compactFoot.transform.localScale=new Vector3(.984f,.20f,1);
        compactFoot.transform.localPosition=new Vector3(0,-.23f,-.012f);
        // The original upright scan contains two sideways doors, lower = left.
        // Sample each illustration independently; printed rules/names stay out
        // of the crop and the same physical card keeps its native input anchor.
        for(int i=0;i<2;i++){
            var renderer=roomFaces[i];
            renderer.transform.localRotation=Quaternion.Euler(0,0,-90);
            renderer.transform.localScale=new Vector3(.47f,.48f,1);
            var crop=BattlefieldCardArtLayouts.Fit(art.textureRect,new Rect(.14f,i==0?.09f:.53f,.395f,.42f),.47f/.48f,new Vector2(.5f,.5f));
            renderer.GetPropertyBlock(properties);
            properties.SetTexture("_MainTex",art.texture);
            properties.SetVector("_UVRect",new Vector4(crop.x/art.texture.width,crop.y/art.texture.height,crop.width/art.texture.width,crop.height/art.texture.height));
            properties.SetFloat("_MipBias",-art.texture.mipMapBias);
            properties.SetFloat("_Radius",0);renderer.SetPropertyBlock(properties);
            bool unlocked=i==0?value.leftRoomUnlocked:value.rightRoomUnlocked;
            roomNames[i].font=table.CardNameFont;
            roomNames[i].text=GamePreferences.CardFaceName((i==0?value.leftRoom:value.rightRoom)??"");
            roomNames[i].transform.localPosition=new Vector3(i==0?-.245f:.245f,nameY,-.027f);
            roomNames[i].rectTransform.sizeDelta=new Vector2(.41f,table.darkMetalCardFrames?.125f:.11f);
            roomNames[i].enableAutoSizing=true;roomNames[i].fontSizeMin=.55f;roomNames[i].fontSizeMax=table.darkMetalCardFrames?.95f:.82f;
            roomNames[i].fontStyle=table.darkMetalCardFrames||GamePreferences.Language.StartsWith("zh")?FontStyles.Normal:FontStyles.Bold;
            roomNames[i].overflowMode=TextOverflowModes.Ellipsis;
            roomNames[i].color=table.darkMetalCardFrames
                ?(unlocked?new Color(.95f,.93f,.86f):new Color(.62f,.64f,.63f))
                :(unlocked?new Color(.024f,.030f,.035f):new Color(.23f,.25f,.26f));
        }
    }

    static string LocalRoomText(string zh,string en,string ja)
        =>GamePreferences.Language=="ja-JP"?ja:GamePreferences.Language is "en" or "en-US"?en:zh;

    void ResetRoomFace()
    {
        compactRoom=false;
        if(roomNames==null)return;
        foreach(var label in roomNames)label.text="";
    }

    void SyncRoomVisibility(bool visible)
    {
        if(roomFaces==null)return;
        for(int i=0;i<2;i++){
            roomFaces[i].enabled=visible&&compactRoom;
            roomLocks[i].enabled=roomShades[i].enabled=visible&&compactRoom&&!(i==0?card.leftRoomUnlocked:card.rightRoomUnlocked);
        }
        roomSeam.enabled=visible&&compactRoom;
        if(compactRoom){face.enabled=false;compactNameRule.enabled=false;}
    }

    // Vector geometry remains sharp at scene scale without font/emoji fallback.
    // The table owns one mesh shared by both doors and every Room instance.
    internal static Mesh CreateRoomLockMesh()
    {
        var vertices=new System.Collections.Generic.List<Vector3>();
        var triangles=new System.Collections.Generic.List<int>();
        void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d){
            int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        Vector2 Corner(int corner,float angle,Vector2 half,float radius){
            var center=new Vector2((corner==0||corner==3?1:-1)*(half.x-radius),(corner<2?1:-1)*(half.y-radius)-.13f);
            return center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
        }
        var outer=new System.Collections.Generic.List<Vector2>();var inner=new System.Collections.Generic.List<Vector2>();
        for(int corner=0;corner<4;corner++)for(int step=0;step<=8;step++){
            float angle=(corner+step/8f)*Mathf.PI*.5f;
            outer.Add(Corner(corner,angle,new Vector2(.30f,.22f),.065f));
            inner.Add(Corner(corner,angle,new Vector2(.252f,.172f),.017f));
        }
        for(int i=0;i<outer.Count;i++){int next=(i+1)%outer.Count;Quad(outer[i],outer[next],inner[next],inner[i]);}
        var archCenter=new Vector2(0,.13f);
        for(int i=0;i<24;i++){
            float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;
            var p=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var q=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
            Quad(archCenter+p*.215f,archCenter+q*.215f,archCenter+q*.155f,archCenter+p*.155f);
        }
        foreach(float side in new[]{-1f,1f})Quad(new Vector2(side*.215f,.05f),new Vector2(side*.215f,.13f),new Vector2(side*.155f,.13f),new Vector2(side*.155f,.05f));
        var keyCenter=new Vector2(0,-.095f);
        for(int i=0;i<20;i++){
            float a=i*Mathf.PI*2/20,b=(i+1)*Mathf.PI*2/20;int n=vertices.Count;
            vertices.Add(keyCenter);vertices.Add(keyCenter+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.043f);vertices.Add(keyCenter+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.043f);
            triangles.AddRange(new[]{n,n+1,n+2});
        }
        Quad(new Vector2(-.023f,-.12f),new Vector2(.023f,-.12f),new Vector2(.031f,-.22f),new Vector2(-.031f,-.22f));
        var uv=new Vector2[vertices.Count];for(int i=0;i<uv.Length;i++)uv[i]=Vector2.one*.5f;
        var mesh=new Mesh{name="World Room padlock",hideFlags=HideFlags.DontSave};
        mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.uv=uv;mesh.RecalculateBounds();return mesh;
    }
}
