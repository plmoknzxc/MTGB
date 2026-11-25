using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class BattlefieldScrollChevron : MaskableGraphic
{
    public int direction = 1;
    public bool SmoothEdges;
    MenuThemeStyle laneTheme;
    bool canPage;
    // Local coordinates for a right-facing tab. Mirror the complete silhouette for Previous.
    static readonly Vector2[] LacquerTab = {
        new(-14,7),new(-10,12),new(2,12),new(15,1),new(15,-1),new(2,-12),new(-10,-12),new(-14,-7)
    };
    static readonly Vector2[] MetalTab = {
        new(-14,6),new(-8,13),new(1,13),new(15,2),new(15,-2),new(1,-13),new(-8,-13),new(-14,-6)
    };
    static readonly Vector2[] Arrowhead = { new(-2,7),new(9,0),new(-2,-7),new(.25f,0) };

    internal void SetLaneState(MenuThemeStyle theme,bool available)
    {
        if(laneTheme==theme && canPage==available)return;
        laneTheme=theme;canPage=available;SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var center = rectTransform.rect.center;
        if(laneTheme!=null){DrawLaneArrow(mesh,center);return;}
        var tip = center + new Vector2(4 * direction, 0);
        Segment(mesh, center + new Vector2(-4 * direction, 8), tip);
        Segment(mesh, tip, center + new Vector2(-4 * direction, -8));
    }

    void DrawLaneArrow(VertexHelper mesh,Vector2 center)
    {
        bool tide=laneTheme.Id=="wandering-emperor-tidelights";
        Color accent=tide?laneTheme.NavigationEdge:laneTheme.MainAccent;
        Color paper=laneTheme.ArchivePaper;
        float fringe=.75f/Mathf.Max(.1f,canvas.scaleFactor);
        Vector2 Point(float x,float y)=>center+new Vector2(direction*x,y);
        void Stroke(Vector2 a,Vector2 b,float radius,Color tint)=>
            BattlefieldUiStroke.Line(mesh,Point(a.x,a.y),Point(b.x,b.y),radius,tint,fringe);
        if(!canPage){
            Color end=Color.Lerp(laneTheme.ControlEdge,laneTheme.SectionFill,.36f);
            Stroke(new(-4,6),new(2,0),1,end);
            Stroke(new(2,0),new(-4,-6),1,end);
            return;
        }

        var shape=tide?LacquerTab:MetalTab;
        Color baseColor=Color.Lerp(laneTheme.Recess,tide?laneTheme.ActionFill:laneTheme.Slate,.65f);
        Color lip=Color.Lerp(baseColor,accent,.26f);
        // A single folded rim catches light on the leading edge. The tail sits in shadow.
        int first=mesh.currentVertCount;
        mesh.AddVert(center,baseColor,Vector2.zero);
        for(int i=0;i<shape.Length;i++){
            var p=shape[i];
            Color face=Color.Lerp(baseColor,lip,Mathf.InverseLerp(-12,12,p.y));
            mesh.AddVert(Point(p.x,p.y),face,Vector2.zero);
            var outside=p+p.normalized*fringe;
            face.a=0;mesh.AddVert(Point(outside.x,outside.y),face,Vector2.zero);
        }
        for(int i=0;i<shape.Length;i++){
            int a=first+1+i*2,b=first+1+(i+1)%shape.Length*2;
            mesh.AddTriangle(first,a,b);
            mesh.AddTriangle(a,a+1,b);mesh.AddTriangle(a+1,b+1,b);
        }
        Stroke(shape[1],shape[2],.65f,accent);
        Stroke(shape[2],shape[3],.8f,accent);
        Stroke(shape[4],shape[5],.65f,Color.Lerp(lip,accent,.60f));
        // The shaft and two faces read as one arrow even over a bright playmat.
        Stroke(new(-9,0),new(2,0),1.15f,paper);
        int head=mesh.currentVertCount;
        for(int i=0;i<Arrowhead.Length;i++){
            var p=Arrowhead[i];var tint=p.y<0?accent:paper;
            mesh.AddVert(Point(p.x,p.y),tint,Vector2.zero);
        }
        mesh.AddTriangle(head,head+1,head+3);
        mesh.AddTriangle(head+3,head+1,head+2);
        Stroke(Arrowhead[0],Arrowhead[1],.36f,paper);
        Stroke(Arrowhead[1],Arrowhead[2],.36f,accent);
    }
    private void Segment(VertexHelper mesh, Vector2 start, Vector2 end)
    {
        if (SmoothEdges) {
            BattlefieldUiStroke.Line(mesh, start, end, 1.4f, color, .65f / Mathf.Max(.1f, canvas.scaleFactor));
            return;
        }
        var offset = new Vector2(-(end-start).y, (end-start).x).normalized * 1.4f;
        int index = mesh.currentVertCount;
        mesh.AddVert(start+offset, color, Vector2.zero);
        mesh.AddVert(end+offset, color, Vector2.zero);
        mesh.AddVert(end-offset, color, Vector2.zero);
        mesh.AddVert(start-offset, color, Vector2.zero);
        mesh.AddTriangle(index, index+1, index+2);
        mesh.AddTriangle(index, index+2, index+3);
    }
}
