using UnityEngine;
using UnityEngine.UI;

// Artwork uses the Image's nine-slice mesh; keep the tint as a missing-asset fallback.
public sealed class BattlefieldHandPanelTint : BaseMeshEffect
{
    public bool reversed;
    private static Sprite traySprite;
    private static Sprite openTraySprite;
    private static Sprite modernSprite;

    public static void ApplyModernFrame(Image image,bool caption=false)
    {
        if(modernSprite==null){
            var source=IntegratedUiUtility.RoundedRect(64,64,8,"ModernHandSurface");
            modernSprite=Sprite.Create(source.texture,source.rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(10,10,10,10));
            modernSprite.name="ModernHandSurface";
        }
        image.sprite=modernSprite;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=1;
        image.color=caption?new Color(.095f,.19f,.25f,.98f):new Color(.035f,.065f,.09f,.96f);
        var rect=image.transform.Find("ModernOutline") as RectTransform;
        if(rect==null){
            var go=new GameObject("ModernOutline",typeof(RectTransform),typeof(CanvasRenderer),typeof(BattlefieldStackFrameGraphic));
            rect=(RectTransform)go.transform;rect.SetParent(image.transform,false);
        }
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var frame=rect.GetComponent<BattlefieldStackFrameGraphic>();
        frame.Radius=8;frame.Thickness=1.2f;frame.color=caption?new Color(.4f,.62f,.75f,.85f):new Color(.34f,.54f,.66f,.85f);
        frame.raycastTarget=false;frame.SetVerticesDirty();
        var oldOutline=image.GetComponent<Outline>();if(oldOutline!=null)oldOutline.enabled=false;
    }
    public static void ApplyArtwork(Image image,bool openSides=false)
    {
        if (traySprite == null) {
            var sprites = Resources.LoadAll<Sprite>("UI/BattlefieldRedesign/HandTrayMetal");
            if (sprites.Length > 0) traySprite = sprites[0];
        }
        if(openSides&&openTraySprite==null&&traySprite!=null){
            // Draw only the centre rail and fabric; the chamfered side caps
            // would otherwise compete with the first/last exposed card edges.
            var rect=traySprite.rect;var border=traySprite.border;
            rect.x+=border.x;rect.width-=border.x+border.z;
            openTraySprite=Sprite.Create(traySprite.texture,rect,new Vector2(.5f,.5f),traySprite.pixelsPerUnit,0,SpriteMeshType.FullRect,new Vector4(0,border.y,0,border.w));
            openTraySprite.name="HandTrayOpenSides";
        }
        image.sprite = openSides?openTraySprite:traySprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 2;
        image.color = new Color(1, 1, 1, .94f);
    }
    public static void ApplyAdaptiveFrame(Image image)
    {
        ApplyArtwork(image);
        if(image.sprite==null)return;
        float edge=Mathf.Clamp(Mathf.Min(image.rectTransform.rect.width*.012f,image.rectTransform.rect.height*.04f),4,12);
        image.pixelsPerUnitMultiplier=Mathf.Max(image.sprite.border.x,image.sprite.border.z)/edge;
    }
    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive()) return;
        if (graphic is Image image && image.sprite != null) return;
        var rect = graphic.rectTransform.rect;
        vertices.Clear();
        // Soft edges, no opaque rectangular frame behind the cards.
        const int columns=20, rows=8;
        for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++){
            float u=x/(float)columns,v=y/(float)rows;
            float inward=reversed?1-v:v;
            float fade=Mathf.SmoothStep(0,1,Mathf.Min(u,1-u)*10)*(1-Mathf.SmoothStep(.58f,1,inward));
            var vertex=UIVertex.simpleVert;
            vertex.position=new Vector3(Mathf.Lerp(rect.xMin,rect.xMax,u),Mathf.Lerp(rect.yMin,rect.yMax,v));
            vertex.color=new Color(.035f,.065f,.085f,.88f*fade);vertex.uv0=new Vector2(u,v);vertices.AddVert(vertex);
        }
        for(int y=0;y<rows;y++)for(int x=0;x<columns;x++){
            int i=y*(columns+1)+x;vertices.AddTriangle(i,i+columns+1,i+1);vertices.AddTriangle(i+1,i+columns+1,i+columns+2);
        }
    }
}
