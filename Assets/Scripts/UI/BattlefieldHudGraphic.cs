using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

// ComfyUI supplies the complete frame artwork. Unity nine-slices it to fit the
// changing viewport. The small marks below identify rows and selectable targets.
public sealed class BattlefieldHudGraphic : Image
{
    internal const float EntryRadius=15;
    public enum Surface { Journal, Record, Stack, StackHeader, Entry, Phase, Seat, Portrait, Tray, Rail, JournalHeader, OpponentTray }

    MenuThemeStyle theme;
    Surface surface;
    Color trim;
    Vector2 inkUv;
    bool targetable;
    float recordOpen;
    bool UsesPlainTexture => surface is Surface.Seat or Surface.Portrait or Surface.Phase or Surface.Entry or Surface.Rail;
    public override Texture mainTexture => UsesPlainTexture ? Texture2D.whiteTexture : base.mainTexture;

    internal void SetRecordOpen(float progress)
    {
        if(Mathf.Approximately(recordOpen,progress))return;
        recordOpen=progress;
        SetVerticesDirty();
    }

    public static void Apply(RectTransform host, MenuThemeStyle style, Surface part, Color accent, bool targetable = false)
    {
        var child = host.Find("DuelFinish") as RectTransform;
        if (child == null)
        {
            var go = new GameObject("DuelFinish", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattlefieldHudGraphic));
            child = (RectTransform)go.transform;
            child.SetParent(host, false);
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.offsetMin = child.offsetMax = Vector2.zero;
            if (part == Surface.Seat)
            {
                child.offsetMin = new Vector2(-8, -3);
                child.offsetMax = new Vector2(8, 0);
            }
            go.GetComponent<BattlefieldHudGraphic>().raycastTarget = false;
        }
        // The portrait lies under its transparent metal bezel. All other
        // controls keep their native text, icons and click target above the frame.
        if (part == Surface.Portrait)
        {
            if (child.GetSiblingIndex() != host.childCount - 1) child.SetAsLastSibling();
        }
        else if (child.GetSiblingIndex() != 0) child.SetAsFirstSibling();

        var graphic = child.GetComponent<BattlefieldHudGraphic>();
        if (graphic.theme == style && graphic.surface == part && graphic.trim == accent && graphic.targetable == targetable) return;
        graphic.theme = style;
        graphic.surface = part;
        graphic.trim = accent;
        graphic.targetable = targetable;
        string name = part switch {
            Surface.Journal => "Journal", Surface.Stack => "Stack", Surface.Portrait => "Avatar",
            Surface.Phase => "Phase", Surface.Seat => "Seat", Surface.Tray or Surface.OpponentTray => "Hand",
            Surface.Rail => "HandRail", _ => "Ink"
        };
        bool landscape = part is Surface.JournalHeader or Surface.StackHeader;
        bool tray = part is Surface.Tray or Surface.OpponentTray;
        bool customTray = tray && style.HandFrame != null;
        graphic.sprite = customTray ? style.HandFrame : graphic.UsesPlainTexture ? null : landscape ? style.Load<Sprite>(style.HeaderArt) ?? Resources.Load<Sprite>(MenuThemeCatalog.Default.HeaderArt) : style.BattlefieldFrame(name);
        graphic.type = landscape || part == Surface.Portrait ? Type.Simple : Type.Sliced;
        graphic.pixelsPerUnitMultiplier = customTray ? 1 : 1.55f;
        if (tray) {
            bool upper = customTray && part == Surface.OpponentTray;
            child.offsetMin = customTray ? new Vector2(-9, upper ? -19 : 0) : Vector2.zero;
            child.offsetMax = customTray ? new Vector2(9, upper ? 0 : 19) : Vector2.zero;
            child.localScale = new Vector3(1, upper ? -1 : 1, 1);
        }
        var ink = style.BattlefieldFrame("Ink");
        if (ink != null)
        {
            var uv = DataUtility.GetOuterUV(ink);
            graphic.inkUv = new Vector2((uv.x+uv.z)*.5f,(uv.y+uv.w)*.5f);
        }
        if (part == Surface.Seat)
        {
            var print=child.Find("SeatPrint")?.GetComponent<SeatBackdropGraphic>();
            if (print==null) {
                var go=new GameObject("SeatPrint",typeof(RectTransform),typeof(SeatBackdropGraphic));
                print=go.GetComponent<SeatBackdropGraphic>();
                print.rectTransform.SetParent(child,false);
                print.rectTransform.anchorMin=Vector2.zero;
                print.rectTransform.anchorMax=Vector2.one;
                print.rectTransform.offsetMin=new Vector2(2,2);
                print.rectTransform.offsetMax=new Vector2(-2,-2);
                print.raycastTarget=false;
            }
            print.texture=style.Load<Texture2D>(style.DuelSeatBackdrop);
            print.enabled=print.texture!=null;
            print.color=new Color(1,1,1,.60f);
        }
        graphic.SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (theme == null || (!UsesPlainTexture && sprite == null)) return;
        Rect r = GetPixelAdjustedRect();
        if (r.width < 2 || r.height < 2) return;

        switch (surface)
        {
            case Surface.Rail:
                DrawSourceRail(mesh,r);
                return;
            case Surface.Seat:
                DrawPlaque(mesh,r);
                return;
            case Surface.Phase:
                DrawPhasePlate(mesh,r);
                return;
            case Surface.Portrait:
                DrawPortraitRim(mesh,r);
                return;
            case Surface.JournalHeader:
            case Surface.StackHeader:
                DrawHorizon(mesh, r);
                return;
            case Surface.Record:
                Color wash=Color.Lerp(theme.ControlEdge,trim,.32f),fade=wash;
                wash.a=.045f+recordOpen*.03f;fade.a=.008f;
                mesh.AddVert(new Vector2(r.xMin,r.yMin),fade,inkUv);
                mesh.AddVert(new Vector2(r.xMin,r.yMax),wash,inkUv);
                mesh.AddVert(new Vector2(r.xMax,r.yMax),wash,inkUv);
                mesh.AddVert(new Vector2(r.xMax,r.yMin),fade,inkUv);
                mesh.AddTriangle(0,1,2);mesh.AddTriangle(0,2,3);
                Color seam=theme.ControlEdge;seam.a=.24f+.13f*recordOpen;
                Rule(mesh,r.xMin+8,r.xMax-8,r.yMin+1,1,seam);
                return;
            case Surface.Entry:
                Color upper=Color.Lerp(theme.ArchivePanel,trim,targetable?.22f:.085f);
                Color lower=Color.Lerp(theme.SectionFill,Color.black,.14f);
                Color rim=Color.Lerp(theme.ControlEdge,trim,.30f);
                rim.a=targetable?.57f:.27f;
                StackSurfaceMesh.Draw(mesh,r,Rect.zero,EntryRadius,1,upper,lower,rim);
                return;
        }
        base.OnPopulateMesh(mesh);
        if((surface is Surface.Tray or Surface.OpponentTray) && theme.HandFrame == null)DrawTrayBinding(mesh,r);
        if(surface==Surface.Journal){
            Color seam=theme.ControlEdge;seam.a=.32f;
            float left=r.xMin+3,right=r.xMax-3,top=r.yMax-2,bottom=r.yMin+2;
            Rule(mesh,left+7,right-7,top,1,seam);
            Rule(mesh,left+7,right-7,bottom,1,seam);
            for(int side=0;side<2;side++){
                float x=side==0?left:right,inward=side==0?1:-1;
                Line(mesh,new Vector2(x,top-7),new Vector2(x+7*inward,top),1,seam);
                Line(mesh,new Vector2(x,bottom+7),new Vector2(x+7*inward,bottom),1,seam);
            }
        }
    }

    void DrawTrayBinding(VertexHelper mesh,Rect r)
    {
        Color rim=Color.Lerp(theme.ControlEdge,theme.ArchivePaper,.22f);
        rim.a=.78f;
        Color glint=Color.Lerp(trim,theme.ArchivePaper,.34f);
        bool tide=theme.Id=="wandering-emperor-tidelights";
        for(int side=0;side<2;side++){
            float dx=side==0?1:-1,x=side==0?r.xMin+1.5f:r.xMax-1.5f;
            Line(mesh,new Vector2(x,r.yMin+7),new Vector2(x,r.yMax-7),1.8f,rim);
            for(int end=0;end<2;end++){
                float dy=end==0?1:-1,y=end==0?r.yMin+1.5f:r.yMax-1.5f;
                Vector2 P(float a,float b)=>new(x+dx*a,y+dy*b);
                Line(mesh,P(0,6),P(6,0),2,glint);
                Line(mesh,P(6,0),P(34,0),2,glint);
                if(tide){
                    Line(mesh,P(4,17),P(4,6),1,rim);
                    Line(mesh,P(4,6),P(18,6),1,rim);
                }else Line(mesh,P(4,16),P(16,4),1.6f,rim);
            }
        }
        Rule(mesh,r.xMin+34,r.xMax-34,r.yMax-1.5f,1.4f,rim);
        Rule(mesh,r.xMin+34,r.xMax-34,r.yMin+1.5f,1.4f,rim);
    }

    void DrawSourceRail(VertexHelper mesh,Rect r)
    {
        bool tide=theme.IsTidelights;
        Color ink=Color.Lerp(theme.SectionFill,Color.black,.20f);
        Color light=tide?Color.Lerp(theme.Recess,theme.ArchivePanel,.60f):Color.Lerp(theme.ArchivePanel,trim,.13f);
        mesh.AddVert(r.min,ink,inkUv);
        mesh.AddVert(new Vector2(r.xMin,r.yMax),light,inkUv);
        mesh.AddVert(r.max,light,inkUv);
        mesh.AddVert(new Vector2(r.xMax,r.yMin),ink,inkUv);
        mesh.AddTriangle(0,1,2);mesh.AddTriangle(0,2,3);
        Color seam=Color.Lerp(theme.ControlEdge,trim,.25f);
        if(tide){
            seam=theme.Etching;seam.a=.38f;
            Rule(mesh,r.xMin+4,r.xMax-4,r.yMin+1,1,seam);
            return;
        }
        seam.a=.63f;
        Rule(mesh,r.xMin,r.xMax,r.yMin+1,1.5f,seam);
        Rule(mesh,r.xMin,r.xMax,r.yMax-1,1.5f,seam);
        Color engraving=seam;engraving.a=.23f;
        float y=r.center.y;
        // The Sanctum rail echoes the facets in its stonework.
        for(float x=r.xMin+7;x+40<r.xMax;x+=40){
            Line(mesh,new Vector2(x,y),new Vector2(x+15,y),1,engraving);
            Line(mesh,new Vector2(x+15,y),new Vector2(x+20,y+4),1,engraving);
            Line(mesh,new Vector2(x+20,y+4),new Vector2(x+25,y),1,engraving);
            Line(mesh,new Vector2(x+25,y),new Vector2(x+20,y-4),1,engraving);
            Line(mesh,new Vector2(x+20,y-4),new Vector2(x+15,y),1,engraving);
            Line(mesh,new Vector2(x+25,y),new Vector2(x+40,y),1,engraving);
        }
    }

    // One ink wash beneath the text, with a folded corner and a narrow metallic edge.
    void DrawPlaque(VertexHelper mesh, Rect r)
    {
        float left=r.xMin, right=r.xMax, top=r.yMax, bottom=r.yMin;
        const float cut=SeatBackdropGraphic.Corner;
        Color light=Color.Lerp(theme.ArchivePanel,trim,.14f), dark=Color.Lerp(theme.SectionFill,Color.black,.28f);
        light.a=.98f;dark.a=.97f;
        mesh.AddVert(new Vector2(left,top),light,inkUv);
        mesh.AddVert(new Vector2(right-cut,top),dark,inkUv);
        mesh.AddVert(new Vector2(right,top-cut),dark,inkUv);
        mesh.AddVert(new Vector2(right,bottom),dark,inkUv);
        mesh.AddVert(new Vector2(left,bottom),Color.Lerp(light,dark,.65f),inkUv);
        mesh.AddTriangle(0,1,2);mesh.AddTriangle(0,2,3);mesh.AddTriangle(0,3,4);
        Color edge=theme.ControlEdge;edge.a=.46f;
        Rule(mesh,left+1,right-cut,top-1,1,edge);
        Line(mesh,new Vector2(right-cut,top-1),new Vector2(right-1,top-cut),1,edge);
        Line(mesh,new Vector2(right-1,top-cut),new Vector2(right-1,bottom+1),1,edge);
        edge.a=.23f;Rule(mesh,left+1,right-1,bottom+1,1,edge);
        Line(mesh,new Vector2(left+1,bottom+1),new Vector2(left+1,top-1),1,edge);
        Color inlay=trim;inlay.a=.92f;
        Rule(mesh,left+1,left+49,top-1,2,inlay);
    }

    // One continuous ground for the readout and its optional action row.
    void DrawPhasePlate(VertexHelper mesh, Rect r)
    {
        Color light=Color.Lerp(theme.ArchivePanel,trim,.11f);
        Color dark=Color.Lerp(theme.SectionFill,Color.black,.32f);
        light.a=dark.a=.98f;
        mesh.AddVert(new Vector2(r.xMin,r.yMax),light,inkUv);
        mesh.AddVert(new Vector2(r.xMax,r.yMax),dark,inkUv);
        mesh.AddVert(new Vector2(r.xMax,r.yMin),dark,inkUv);
        mesh.AddVert(new Vector2(r.xMin,r.yMin),Color.Lerp(light,dark,.72f),inkUv);
        mesh.AddTriangle(0,1,2);mesh.AddTriangle(0,2,3);
        Color seam=theme.ControlEdge;seam.a=.46f;
        Rule(mesh,r.xMin,r.xMax,r.yMax-1,1,seam);
        Rule(mesh,r.xMin,r.xMax,r.yMin+1,1,seam);
        Rule(mesh,r.xMin,r.xMin+27,r.yMax-1,2,trim);
    }

    void DrawPortraitRim(VertexHelper mesh, Rect r)
    {
        float radius=Mathf.Min(r.width,r.height)*.45f;
        Color silver=Color.Lerp(theme.ControlEdge,theme.ArchivePaper,.46f);
        Arc(mesh,r.center,radius+.5f,0,360,1.4f,silver);
        Color accent=targetable?(Color)theme.ArchivePaper:trim;
        float sweep=targetable?164:68, thickness=targetable?3f:2f;
        Arc(mesh,r.center,radius+3.5f,12,12+sweep,thickness,accent);
        Arc(mesh,r.center,radius+3.5f,192,192+sweep,thickness,accent);
    }

    void Arc(VertexHelper mesh,Vector2 center,float radius,float start,float end,float width,Color tint)
    {
        int steps=Mathf.CeilToInt((end-start)/6), first=mesh.currentVertCount;
        for(int i=0;i<=steps;i++){
            float angle=Mathf.Lerp(start,end,(float)i/steps)*Mathf.Deg2Rad;
            var normal=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            mesh.AddVert(center+normal*(radius-width*.5f),tint,inkUv);
            mesh.AddVert(center+normal*(radius+width*.5f),tint,inkUv);
            if(i==0)continue;
            int k=first+i*2;
            mesh.AddTriangle(k-2,k-1,k+1);mesh.AddTriangle(k-2,k+1,k);
        }
    }

    // Crop at the source aspect ratio. The two docks show different sections of the
    // same landscape, fading behind the title and into the panel below.
    void DrawHorizon(VertexHelper mesh, Rect r)
    {
        Vector4 uv = DataUtility.GetOuterUV(sprite);
        float fraction = Mathf.Min(1, r.width * sprite.rect.height / (r.height * sprite.rect.width));
        float start = (1 - fraction) * (surface == Surface.JournalHeader ? .30f : .84f);
        for (int y = 0; y < 3; y++)
        {
            float v = y == 1 ? .32f : y / 2f;
            for (int x = 0; x < 4; x++)
            {
                float u = x / 3f;
                float alpha = x switch { 0 => .15f, 1 => .29f, 2 => .77f, _ => .53f };
                if(surface==Surface.JournalHeader&&x>=2)alpha*=.55f;
                alpha *= y == 0 ? 0 : y == 1 ? .72f : 1f;
                mesh.AddVert(new Vector2(r.xMin + u*r.width, r.yMin + v*r.height), new Color(1,1,1,alpha),
                    new Vector2(Mathf.Lerp(uv.x,uv.z,start + u*fraction),Mathf.Lerp(uv.y,uv.w,v)));
                if (y == 0 || x == 0) continue;
                int i = y*4+x;
                mesh.AddTriangle(i-5,i-1,i);
                mesh.AddTriangle(i-5,i,i-4);
            }
        }
    }

    void Rule(VertexHelper mesh,float x1,float x2,float y,float width,Color tint) =>
        Line(mesh,new Vector2(x1,y),new Vector2(x2,y),width,tint);

    void Line(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color tint)
    {
        Vector2 normal = Vector2.Perpendicular((b-a).normalized)*(.5f*width);
        int i = mesh.currentVertCount;
        mesh.AddVert(a-normal,tint,inkUv); mesh.AddVert(b-normal,tint,inkUv);
        mesh.AddVert(b+normal,tint,inkUv); mesh.AddVert(a+normal,tint,inkUv);
        mesh.AddTriangle(i,i+1,i+2); mesh.AddTriangle(i,i+2,i+3);
    }
}

// Sample the entire theme print inside the same clipped corner as its metal rim.
internal sealed class SeatBackdropGraphic : RawImage
{
    internal const float Corner=12;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r=GetPixelAdjustedRect();
        if(r.width<=0 || r.height<=0)return;
        float cut=Mathf.Min(Corner-1,Mathf.Min(r.width,r.height));
        void Point(float x,float y)=>mesh.AddVert(new Vector2(x,y),color,
            new Vector2((x-r.xMin)/r.width,(y-r.yMin)/r.height));
        Point(r.xMin,r.yMax);Point(r.xMax-cut,r.yMax);Point(r.xMax,r.yMax-cut);
        Point(r.xMax,r.yMin);Point(r.xMin,r.yMin);
        mesh.AddTriangle(0,1,2);mesh.AddTriangle(0,2,3);mesh.AddTriangle(0,3,4);
    }
}
