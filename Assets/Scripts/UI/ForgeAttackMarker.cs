using UnityEngine;
using UnityEngine.UI;

// The attack state sits above the card, leaving its face and stats unobstructed.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ForgeAttackMarker : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r=rectTransform.rect;
        // Continuous peaked ribbon: soft transparent edges, tapered tips and a warm core.
        // Vertex gradients avoid the hard parallel outlines of stacked thick lines.
        AddRibbon(vh,r,-16,-2,new Color(1,.13f,.015f,0),new Color(1,.22f,.025f,.24f));
        AddRibbon(vh,r,-9,0,new Color(1,.15f,.015f,0),new Color(1,.3f,.035f,.55f));
        AddRibbon(vh,r,0,9,new Color(1,.3f,.035f,.55f),new Color(1,.15f,.015f,0));
        AddRibbon(vh,r,-3,0,new Color(1,.32f,.035f,.55f),new Color(1,.83f,.48f,.95f));
        AddRibbon(vh,r,0,2,new Color(1,.83f,.48f,.95f),new Color(1,.4f,.08f,.15f));
    }

    private static void AddRibbon(VertexHelper vh,Rect r,float lower,float upper,Color lowerColor,Color upperColor)
    {
        const int segments=48;
        int first=vh.currentVertCount;
        float scale=r.height/40f;
        for(int i=0;i<=segments;i++)
        {
            float x=i*2f/segments-1;
            float taper=Mathf.Pow(1-Mathf.Abs(x),.65f);
            float y=r.center.y+(-.12f+.43f*(1-Mathf.Abs(x)))*r.height;
            var lo=lowerColor;var hi=upperColor;
            lo.a*=taper;hi.a*=taper;
            vh.AddVert(new Vector2(r.center.x+x*r.width*.5f,y+lower*scale*taper),lo,Vector2.zero);
            vh.AddVert(new Vector2(r.center.x+x*r.width*.5f,y+upper*scale*taper),hi,Vector2.zero);
            if(i==segments)continue;
            int n=first+i*2;
            vh.AddTriangle(n,n+1,n+3);vh.AddTriangle(n,n+3,n+2);
        }
    }
}
