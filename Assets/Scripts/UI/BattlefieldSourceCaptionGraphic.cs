using UnityEngine;
using UnityEngine.UI;

// Marks each group directly over its cards. The count and the space between
// groups remain legible when multiple sources share a narrow tray.
public sealed class BattlefieldSourceCaptionGraphic : MaskableGraphic
{
    public Color accent = new(.60f, .77f, .84f, .68f);
    public bool opponent;
    internal MenuThemeStyle Theme;
    string zone;
    float start, glyphScale,plateWidth,countStart;
    bool current;

    internal void SetCaption(string source,float left,float scale,float width,float countX,bool selected)
    {
        if(zone==source && start==left && glyphScale==scale && plateWidth==width && countStart==countX && current==selected)return;
        zone=source;start=left;glyphScale=scale;plateWidth=width;countStart=countX;current=selected;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = rectTransform.rect;
        if (r.width < 2 || r.height < 2) return;
        if(Theme!=null){
            if(Theme.IsTidelights){
                DrawLanternTab(mesh,r);
                return;
            }
            float center=r.center.y;
            var plate=Rect.MinMaxRect(r.xMin,r.yMin+2,r.xMin+Mathf.Min(r.width,plateWidth),r.yMax-2);
            if(plate.width<6)return;
            const float cut=8;
            var edge=Color.Lerp(Theme.ControlEdge,accent,current?.66f:.22f);
            var plateTop=Color.Lerp(Theme.ArchivePanel,accent,current?.30f:.07f);
            var plateBottom=Color.Lerp(Theme.SectionFill,accent,current?.12f:.03f);
            Plaque(mesh,plate,cut,edge,edge);
            var insetPlate=Rect.MinMaxRect(plate.xMin+1.4f,plate.yMin+1.4f,plate.xMax-1.4f,plate.yMax-1.4f);
            Plaque(mesh,insetPlate,cut-.7f,plateBottom,plateTop);
            float baseY=opponent?insetPlate.yMax-2:insetPlate.yMin;
            if(current)Strip(mesh,insetPlate.xMin+cut,insetPlate.xMax-cut,baseY,baseY+2,accent,edge);
            float divider=r.xMin+countStart-5;
            if(divider>insetPlate.xMin+6 && divider<insetPlate.xMax-5)
                Strip(mesh,divider,divider+1,center-7,center+7,edge,edge);
            if(glyphScale<=0)return;
            var origin=new Vector2(r.xMin+start,center);
            Vector2 P(float x,float y)=>origin+new Vector2(x,y)*glyphScale;
            void Stroke(float x1,float y1,float x2,float y2){
                var a=P(x1,y1);var b=P(x2,y2);
                Vector2 n=Vector2.Perpendicular((b-a).normalized)*(.8f*glyphScale);
                int i=mesh.currentVertCount;
                Add(mesh,a-n,accent);Add(mesh,a+n,accent);Add(mesh,b+n,accent);Add(mesh,b-n,accent);
                mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
            }
            if(zone=="Command"){
                Stroke(0,4,3,-4);Stroke(3,-4,13,-4);Stroke(13,-4,16,4);
                Stroke(16,4,11,1);Stroke(11,1,8,6);Stroke(8,6,5,1);Stroke(5,1,0,4);
                Stroke(3,-7,13,-7);
            }else if(zone=="Graveyard"){
                Stroke(2,-6,2,2);Stroke(2,2,6,6);Stroke(6,6,11,6);Stroke(11,6,15,2);Stroke(15,2,15,-6);
                Stroke(0,-7,17,-7);Stroke(6,0,11,0);
            }else if(zone=="Exile"){
                Stroke(0,0,8,7);Stroke(8,7,16,0);Stroke(16,0,8,-7);Stroke(8,-7,0,0);Stroke(4,-7,12,7);
            }else if(zone=="Companion"){
                Stroke(0,-1,5,4);Stroke(5,4,10,-1);Stroke(10,-1,5,-6);Stroke(5,-6,0,-1);
                Stroke(7,2,12,7);Stroke(12,7,17,2);Stroke(17,2,12,-3);
            }else if(zone is "Prepared" or "Battlefield"){
                Stroke(0,5,8,2);Stroke(8,2,16,5);Stroke(16,5,16,-5);Stroke(16,-5,8,-8);
                Stroke(8,-8,0,-5);Stroke(0,-5,0,5);Stroke(8,2,8,-8);
            }else{
                Stroke(5,6,15,6);Stroke(15,6,15,-7);Stroke(15,-7,5,-7);Stroke(5,-7,5,6);
                if(zone=="Hand"){Stroke(2,-3,0,7);Stroke(0,7,10,9);}
            }
            return;
        }
        float slope = Mathf.Min(30, r.height * .64f, r.width * .08f);
        float inset = Mathf.Min(1.5f, r.height * .08f);
        Vector2[] outer = Shape(r, slope, opponent);
        Vector2[] inner = Shape(Rect.MinMaxRect(r.xMin + inset, r.yMin + inset, r.xMax - inset, r.yMax - inset), slope, opponent);
        Color frame = Color.Lerp(new Color(.43f, .53f, .57f, .83f), accent, .22f);
        Color top = new(.16f, .22f, .25f, .97f);
        Color bottom = new(.035f, .052f, .065f, .97f);
        for (int i = 0; i < 8; i++) {
            Add(mesh, outer[i], frame);
            Add(mesh, inner[i], Color.Lerp(bottom, top, Mathf.InverseLerp(r.yMin, r.yMax, inner[i].y)));
        }
        for (int i = 0; i < 8; i++) {
            int a = i * 2, b = ((i + 1) % 8) * 2;
            mesh.AddTriangle(a, b, a + 1);
            mesh.AddTriangle(a + 1, b, b + 1);
        }
        for (int i = 1; i < 7; i++) mesh.AddTriangle(1, i * 2 + 1, (i + 1) * 2 + 1);

    }

    void DrawLanternTab(VertexHelper mesh,Rect r)
    {
        float right=Mathf.Min(r.xMax,r.xMin+plateWidth);
        if(right-r.xMin<6)return;
        float mid=r.center.y;
        Color light=Color.Lerp(accent,Theme.ArchivePaper,.22f);
        if(current){
            Color wash=accent;wash.a=.13f;
            var clear=wash;clear.a=0;
            Strip(mesh,r.xMin,right,r.yMin+2,r.yMax-2,wash,clear);
            float edge=opponent?r.yMax-3:r.yMin+1;
            Strip(mesh,r.xMin+start,right-5,edge,edge+2,light,accent);
        }
        float stampLeft=r.xMin+countStart-4;
        if(stampLeft>r.xMin && right-6>stampLeft){
            var stamp=Rect.MinMaxRect(stampLeft,mid-11,right-6,mid+11);
            Color paper=current?Color.Lerp(accent,Theme.ArchivePaper,.48f):(Color)Theme.Slate;
            Plaque(mesh,stamp,2,paper,paper);
        }
        if(glyphScale<=0)return;
        float x=r.xMin+start+7*glyphScale;
        Color shell=current?light:(Color)Theme.Stone;
        Color rib=current?Color.Lerp(accent,Theme.Recess,.62f):(Color)Theme.ArchivePanel;
        void Bar(float l,float b,float w,float h,Color tint) =>
            Strip(mesh,x+l*glyphScale,x+(l+w)*glyphScale,mid+b*glyphScale,mid+(b+h)*glyphScale,tint,tint);
        void Point(float px,float py,Color color)=>Add(mesh,new Vector2(x+px*glyphScale,mid+py*glyphScale),color);
        int first=mesh.currentVertCount;
        if(zone=="Command"){
            // A permanent crest distinguishes the command zone beside the browsed source.
            Point(-7,4,shell);Point(-3,1,shell);Point(0,6,shell);Point(3,1,shell);Point(7,4,shell);
            Point(5,-3,accent);Point(-5,-3,accent);
            mesh.AddTriangle(first,first+1,first+6);mesh.AddTriangle(first+1,first+2,first+3);
            mesh.AddTriangle(first+1,first+3,first+5);mesh.AddTriangle(first+1,first+5,first+6);
            mesh.AddTriangle(first+3,first+4,first+5);
            Bar(-5,-6,10,1.5f,shell);
            return;
        }
        Color paperLight=current?shell:Color.Lerp(Theme.Slate,shell,.2f);
        Point(-3,6,paperLight);Point(3,6,paperLight);Point(6,3,paperLight);Point(6,-3,paperLight);
        Point(3,-6,paperLight);Point(-3,-6,paperLight);Point(-6,-3,paperLight);Point(-6,3,paperLight);
        for(int i=1;i<7;i++)mesh.AddTriangle(first,first+i,first+i+1);
        Bar(-3,7,6,1.5f,shell);Bar(-3,-8,6,1.5f,shell);Bar(-.7f,-10.5f,1.4f,2.5f,shell);
        Bar(-5.5f,-2,11,1,rib);Bar(-5.5f,2,11,1,rib);
    }

    static void Plaque(VertexHelper mesh,Rect r,float cut,Color bottom,Color top)
    {
        cut=Mathf.Min(cut,r.width*.18f,r.height*.24f);
        int i=mesh.currentVertCount;
        Add(mesh,new Vector2(r.xMin,r.yMin),bottom);
        Add(mesh,new Vector2(r.xMin,r.yMax-cut),top);
        Add(mesh,new Vector2(r.xMin+cut,r.yMax),top);
        Add(mesh,r.max,top);
        Add(mesh,new Vector2(r.xMax,r.yMin+cut),bottom);
        Add(mesh,new Vector2(r.xMax-cut,r.yMin),bottom);
        for(int n=1;n<5;n++)mesh.AddTriangle(i,i+n,i+n+1);
    }

    static void Strip(VertexHelper mesh,float left,float right,float bottom,float top,Color near,Color far)
    {
        int i=mesh.currentVertCount;
        Add(mesh,new Vector2(left,bottom),near);
        Add(mesh,new Vector2(left,top),near);
        Add(mesh,new Vector2(right,top),far);
        Add(mesh,new Vector2(right,bottom),far);
        mesh.AddTriangle(i,i+1,i+2);
        mesh.AddTriangle(i,i+2,i+3);
    }

    private static Vector2[] Shape(Rect r, float slope, bool opposite)
    {
        float upper = opposite ? slope : 0;
        float lower = opposite ? 0 : slope;
        float bevel = Mathf.Min(2, r.height * .06f);
        return new[] {
            new Vector2(r.xMin + upper + bevel, r.yMax), new Vector2(r.xMax - upper - bevel, r.yMax),
            new Vector2(r.xMax - upper, r.yMax - bevel), new Vector2(r.xMax - lower, r.yMin + bevel),
            new Vector2(r.xMax - lower - bevel, r.yMin), new Vector2(r.xMin + lower + bevel, r.yMin),
            new Vector2(r.xMin + lower, r.yMin + bevel), new Vector2(r.xMin + upper, r.yMax - bevel)
        };
    }

    private static void Add(VertexHelper mesh, Vector2 position, Color tint)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = tint;
        mesh.AddVert(vertex);
    }
}
