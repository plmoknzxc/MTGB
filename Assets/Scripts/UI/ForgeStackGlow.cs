using UnityEngine;
using UnityEngine.UI;

// A soft travelling highlight on the stack's current top item. No material,
// texture allocation, or input interception is needed for the animated border.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ForgeStackGlow : MaskableGraphic
{
    const int Segments=144;
    bool curved;
    float nextFrame;
    public void Configure(bool ability,bool active)
    {
        raycastTarget=false;
        if(curved!=ability){curved=ability;SetVerticesDirty();}
        enabled=active;
    }
    void Update()
    {
        if(Time.unscaledTime<nextFrame)return;
        nextFrame=Time.unscaledTime+1f/30;
        SetVerticesDirty();
    }
    Vector2 Point(float t)
    {
        var r=rectTransform.rect;
        float w=r.width,h=r.height,b=curved?54:0;
        float bottom=curved?Mathf.PI*Mathf.Sqrt((w*w/4+b*b)/2):w;
        float d=Mathf.Repeat(t,1)*(w+2*(h-b)+bottom);
        Vector2 p;
        if(d<w)p=new Vector2(-w/2+d,h/2);
        else if((d-=w)<h-b)p=new Vector2(w/2,h/2-d);
        else if((d-=h-b)<bottom) {
            float a=d/bottom*Mathf.PI;
            p=curved?new Vector2(w/2*Mathf.Cos(a),-h/2+b-b*Mathf.Sin(a)):new Vector2(w/2-d,-h/2);
        }
        else {d-=bottom;p=new Vector2(-w/2,-h/2+b+d);}
        return p+r.center;
    }
    Vector2 Normal(float t)
    {
        var direction=(Point(t+.0001f)-Point(t-.0001f)).normalized;
        return new Vector2(-direction.y,direction.x);
    }
    Color Tint(float t,float opacity)
    {
        float phase=Time.unscaledTime/3.2f;
        float distance=Mathf.Repeat(phase-t,1);
        float light=Mathf.Pow(Mathf.Clamp01(1-distance/.24f),2);
        var c=Color.Lerp(new Color(.22f,.62f,.85f),new Color(.86f,.98f,1),light);
        c.a=opacity*(.2f+.8f*light);return c;
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        for(int band=0;band<3;band++) {
            float width=band==0?5.5f:band==1?2.8f:1.1f;
            float alpha=band==0?.12f:band==1?.3f:.95f;
            for(int i=0;i<Segments;i++) {
                float a=i/(float)Segments,b=(i+1)/(float)Segments;
                Vector2 p=Point(a),q=Point(b),n=Normal(a)*width,m=Normal(b)*width;
                int start=vh.currentVertCount;
                vh.AddVert(p-n,Tint(a,alpha),Vector2.zero);vh.AddVert(p+n,Tint(a,alpha),Vector2.zero);
                vh.AddVert(q+m,Tint(b,alpha),Vector2.zero);vh.AddVert(q-m,Tint(b,alpha),Vector2.zero);
                vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
            }
        }
    }
}
