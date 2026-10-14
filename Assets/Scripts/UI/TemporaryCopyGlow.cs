using UnityEngine;
using UnityEngine.UI;

// Silver flowing edge around a temporary token, preserving the normal arched face.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TemporaryCopyGlow : MaskableGraphic
{
    private float nextFrame;
    private void Update(){if(Time.unscaledTime<nextFrame)return;nextFrame=Time.unscaledTime+.05f;SetVerticesDirty();}
    private Vector2 Edge(int index,float expansion)
    {
        const int steps=20;
        int side=index/steps;float t=(index%steps)/(float)steps;
        var half=rectTransform.rect.size*.5f;
        Vector2 p=side switch {
            0=>new Vector2(Mathf.Lerp(-half.x,half.x,t),half.y-rectTransform.rect.height*(35f/240)*(1-Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow(t*2-1,2))))),
            1=>new Vector2(half.x,Mathf.Lerp(half.y-rectTransform.rect.height*35f/240,-half.y,t)),
            2=>new Vector2(Mathf.Lerp(half.x,-half.x,t),-half.y),
            _=>new Vector2(-half.x,Mathf.Lerp(-half.y,half.y-rectTransform.rect.height*35f/240,t))
        };
        return p+new Vector2(p.x/half.x,p.y/half.y).normalized*expansion;
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();Ring(mesh,8,3,0,.17f,false);Ring(mesh,3,.3f,.17f,.55f,false);Ring(mesh,1.8f,-.2f,.82f,.82f,true);
    }
    private void Ring(VertexHelper mesh,float outer,float inner,float a,float b,bool flowing)
    {
        int start=mesh.currentVertCount;
        for(int i=0;i<80;i++) {
            float phase=i*.57f-Time.unscaledTime*3;
            float opacity=flowing?.22f+.78f*Mathf.Pow(.5f+.5f*Mathf.Sin(phase),2):1;
            float ripple=flowing?Mathf.Sin(phase*1.7f)*.6f:0;
            mesh.AddVert(Edge(i,outer+ripple),new Color(.82f,.94f,1,a*opacity),Vector2.zero);
            mesh.AddVert(Edge(i,inner+ripple),new Color(.95f,.98f,1,b*opacity),Vector2.zero);
        }
        for(int i=0;i<80;i++){int p=start+i*2,q=start+(i+1)%80*2;mesh.AddTriangle(p,q,p+1);mesh.AddTriangle(p+1,q,q+1);}
    }
}
