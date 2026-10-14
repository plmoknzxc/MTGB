using UnityEngine;
using UnityEngine.UI;

// One small UI mesh per playable hand card. Pulse opacity without rebuilding geometry.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ForgePlayableGlow : MaskableGraphic
{
    // Selected cards have a steady bright inner edge and a warm halo, distinct
    // from the cyan pulse that only means a card is available to play.
    public bool selected;
    private void Update()
    {
        canvasRenderer.SetAlpha(selected?.96f+.04f*Mathf.Sin(Time.unscaledTime*2.1f):.87f+.13f*Mathf.Sin(Time.unscaledTime*2.1f));
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if(selected) {
            Ring(mesh,22,12,0,.18f);
            Ring(mesh,12,6,.18f,.48f);
            Ring(mesh,6,2,.48f,1);
            Ring(mesh,2,-1,1,1);
            return;
        }
        Ring(mesh,25,16,0,.09f);
        Ring(mesh,16,9,.09f,.24f);
        Ring(mesh,9,4,.24f,.62f);
        Ring(mesh,4,1,.62f,.95f);
        Ring(mesh,1,-1,.95f,.95f);
    }

    private Vector2 Corner(int index,float expansion)
    {
        const int arcSteps=8;
        int corner=index/arcSteps;
        float angle=(corner*90+(index%arcSteps)*90f/(arcSteps-1))*Mathf.Deg2Rad;
        float radius=6+Mathf.Max(0,expansion);
        var half=rectTransform.rect.size*.5f+Vector2.one*expansion;
        var center=new Vector2((corner==0 || corner==3?1:-1)*(half.x-radius),
            (corner<2?1:-1)*(half.y-radius));
        return center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
    }

    private void Ring(VertexHelper mesh,float outer,float inner,float outerAlpha,float innerAlpha)
    {
        int first=mesh.currentVertCount;
        for(int i=0;i<32;i++)
        {
            Color tint=selected?new Color(1,.83f,.32f):new Color(.2f,.8f,1);
            mesh.AddVert(Corner(i,outer),new Color(tint.r,tint.g,tint.b,outerAlpha),Vector2.zero);
            mesh.AddVert(Corner(i,inner),new Color(tint.r,tint.g,tint.b,innerAlpha),Vector2.zero);
        }
        for(int i=0;i<32;i++)
        {
            int a=first+i*2,b=first+(i+1)%32*2;
            mesh.AddTriangle(a,b,a+1);mesh.AddTriangle(a+1,b,b+1);
        }
    }
}
