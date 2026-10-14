using UnityEngine;
using UnityEngine.UI;

// Filled silhouettes and a half-screen-pixel edge avoid hairline noise when
// the battlefield Canvas scales down. The same glyph is used in the inspector.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PermanentCounterIcon : MaskableGraphic
{
    public string Kind { get; private set; }
    public void Bind(string kind)
    {
        // Forge keyword counters use title case; enum counters use uppercase.
        // Keep custom rune identities unchanged while sharing known silhouettes.
        Kind=kind?.ToUpperInvariant() switch {
            "LIFELINK"=>"LIFELINK", "FLYING"=>"FLYING", "DEATHTOUCH"=>"DEATHTOUCH", _=>kind
        };
        SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Color hole=new Color(.025f,.045f,.065f,1f);
        switch(Kind) {
            case "STUN":
                Fill(mesh,color,0,7,2,2,7,0,2,-2,0,-7,-2,-2,-7,0,-2,2);break;
            case "SHIELD":
                Fill(mesh,color,-6,5,0,7,6,5,5,-2,0,-7,-5,-2);
                Line(mesh,-3,0,-.5f,-2,hole,1.7f);Line(mesh,-.5f,-2,3,2,hole,1.7f);break;
            case "CHARGE":
                Fill(mesh,color,-7,4,4,4,4,-4,-7,-4);Fill(mesh,color,4,2,6,2,6,-2,4,-2);
                Fill(mesh,hole,0,3,-3,-.5f,0,-.5f);Fill(mesh,hole,-2,-3,1,.5f,-2,.5f);break;
            case "OIL":
                Fill(mesh,color,0,7,-4,-1,4,-1);Disc(mesh,0,-2,4.5f,color);break;
            case "TIME":
                Line(mesh,-6,6,6,6);Line(mesh,-6,-6,6,-6);
                Fill(mesh,color,-4,4,4,4,0,0);Fill(mesh,color,0,0,4,-4,-4,-4);break;
            case "AGE":
                Disc(mesh,0,0,6.5f,color);Disc(mesh,0,0,4.5f,hole);Disc(mesh,0,0,2.4f,color);break;
            case "QUEST":
                Line(mesh,-5,-7,-5,7);Fill(mesh,color,-5,6,6,6,3,2,6,-1,-5,-1);break;
            case "VERSE":
                Line(mesh,-2,-3,-2,5);Line(mesh,5,-1,5,7);
                Fill(mesh,color,-2,5,5,7,5,4,-2,2);Disc(mesh,-4,-4,2.5f,color);Disc(mesh,3,-2,2.5f,color);break;
            case "DEATHTOUCH":case "POISON":
                Disc(mesh,0,1,6,color);Fill(mesh,color,-3,-2,3,-2,3,-6,-3,-6);
                Disc(mesh,-2,1,1.5f,hole);Disc(mesh,2,1,1.5f,hole);Line(mesh,0,-4,0,-6,hole,1.4f);break;
            case "FLYING":
                Fill(mesh,color,0,-5,-6,1,-7,6,-2,3,0,0);
                Fill(mesh,color,0,-5,0,0,2,3,7,6,6,1);break;
            case "LIFELINK":
                Disc(mesh,-3,3,3.5f,color);Disc(mesh,3,3,3.5f,color);Fill(mesh,color,-6,2,6,2,0,-7);break;
            case "MORE":
                Disc(mesh,-5,0,1.3f,color);Disc(mesh,0,0,1.3f,color);Disc(mesh,5,0,1.3f,color);break;
            default:
                // Stable rune fallback for kinds without a dedicated silhouette.
                Fill(mesh,color,0,7,6,3,6,-3,0,-7,-6,-3,-6,3);
                uint hash=2166136261;foreach(char c in Kind??"")hash=(hash^c)*16777619;
                for(int i=0;i<6;i++)if((hash&(1u<<i))!=0) {
                    float a=i*Mathf.PI/3;Line(mesh,0,0,Mathf.Cos(a)*4,Mathf.Sin(a)*4,hole);
                }
                break;
        }
    }
    void Disc(VertexHelper mesh,float x,float y,float radius,Color tint)
    {
        var points=new float[48];for(int i=0;i<24;i++){float a=i*Mathf.PI/12;points[i*2]=x+Mathf.Cos(a)*radius;points[i*2+1]=y+Mathf.Sin(a)*radius;}Fill(mesh,tint,points);
    }
    void Line(VertexHelper mesh,float ax,float ay,float bx,float by,Color? tint=null,float thickness=1.8f)
    {
        var a=new Vector2(ax,ay);var b=new Vector2(bx,by);
        var n=new Vector2(-(b-a).y,(b-a).x).normalized*thickness*.5f;
        Fill(mesh,tint??color,a.x+n.x,a.y+n.y,b.x+n.x,b.y+n.y,b.x-n.x,b.y-n.y,a.x-n.x,a.y-n.y);
    }
    void Fill(VertexHelper mesh,Color tint,params float[] coordinates)
    {
        float scale=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/16;
        float pixelScale=Mathf.Max(.01f,Mathf.Abs(rectTransform.lossyScale.x));
        float feather=.5f/(Mathf.Max(.01f,scale)*pixelScale);
        var center=Vector2.zero;int length=coordinates.Length/2;
        for(int i=0;i<length;i++)center+=new Vector2(coordinates[i*2],coordinates[i*2+1]);center/=length;
        var transparent=tint;transparent.a=0;
        for(int i=0;i<length;i++) {
            int next=(i+1)%length;
            var a=new Vector2(coordinates[i*2],coordinates[i*2+1]);var b=new Vector2(coordinates[next*2],coordinates[next*2+1]);
            int start=mesh.currentVertCount;
            mesh.AddVert(center*scale,tint,Vector2.zero);mesh.AddVert(a*scale,tint,Vector2.zero);mesh.AddVert(b*scale,tint,Vector2.zero);mesh.AddTriangle(start,start+1,start+2);
            var normal=new Vector2((b-a).y,-(b-a).x).normalized;
            if(Vector2.Dot(normal,(a+b)*.5f-center)<0)normal=-normal;
            start=mesh.currentVertCount;
            mesh.AddVert(a*scale,tint,Vector2.zero);mesh.AddVert(b*scale,tint,Vector2.zero);
            mesh.AddVert((b+normal*feather)*scale,transparent,Vector2.zero);mesh.AddVert((a+normal*feather)*scale,transparent,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
