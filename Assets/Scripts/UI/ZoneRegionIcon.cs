using UnityEngine;
using UnityEngine.UI;

// Small vector symbols remain crisp without depending on font glyphs or card art.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ZoneRegionIcon : MaskableGraphic
{
    public int Kind;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();float scale=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/24f;
        void Line(Vector2 a,Vector2 b)
        {
            a*=scale;b*=scale;var normal=new Vector2(-(b-a).y,(b-a).x).normalized*.8f*scale;
            int start=mesh.currentVertCount;
            mesh.AddVert(a-normal,color,Vector2.zero);mesh.AddVert(a+normal,color,Vector2.zero);
            mesh.AddVert(b+normal,color,Vector2.zero);mesh.AddVert(b-normal,color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
        void Path(params Vector2[] points){for(int i=1;i<points.Length;i++)Line(points[i-1],points[i]);}
        if(Kind==0) {
            Path(new Vector2(-10,4),new Vector2(0,10),new Vector2(10,4),new Vector2(0,-2),new Vector2(-10,4));
            Path(new Vector2(-10,0),new Vector2(0,-6),new Vector2(10,0));
            Path(new Vector2(-10,-4),new Vector2(0,-10),new Vector2(10,-4));
        } else if(Kind==1) {
            Path(new Vector2(-8,-8),new Vector2(-8,3),new Vector2(-6,8),new Vector2(0,10),new Vector2(6,8),new Vector2(8,3),new Vector2(8,-8));
            Line(new Vector2(-11,-9),new Vector2(11,-9));
            Line(new Vector2(-3,2),new Vector2(3,2));Line(new Vector2(0,5),new Vector2(0,-2));
        } else {
            for(int i=0;i<32;i++) {
                float a=i*Mathf.PI*2/32,b=(i+1)*Mathf.PI*2/32;
                Line(new Vector2(Mathf.Cos(a)*7,Mathf.Sin(a)*7),new Vector2(Mathf.Cos(b)*7,Mathf.Sin(b)*7));
            }
            Line(new Vector2(-10,-10),new Vector2(10,10));
            Path(new Vector2(3,10),new Vector2(10,10),new Vector2(10,3));
        }
    }
}
