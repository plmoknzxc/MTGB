using UnityEngine;
using UnityEngine.UI;

// A transparent-centred mesh. The ink key separates the light from both the art and the table.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ForgePlayableGlow : MaskableGraphic
{
    public bool selected;
    internal bool SheetFrame;
    static readonly Color EdgeInk=new Color32(18,22,29,255);
    internal static readonly Color ChosenLight=new Color32(255,210,191,255);
    internal static readonly Color ReadyLight=new Color32(140,255,199,255);
    static readonly Vector2[] Radial={Vector2.right,Vector2.up,Vector2.left,Vector2.down};

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        // A sheet fills its entire tile, so keep its marks just inside the viewport.
        float edge=SheetFrame?-3:0;
        Color light=selected?ChosenLight:ReadyLight;
        Ring(mesh,edge+3,edge-3,EdgeInk);
        Ring(mesh,edge+1,edge-1,selected?Color.Lerp(light,SheetFrame?color:EdgeInk,.32f):light);
        if(selected) {
            float reach=Mathf.Clamp(Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.11f,7,25);
            for(int corner=0;corner<4;corner++){
                CornerBand(mesh,corner,edge+4,edge-5,reach+2,EdgeInk);
                CornerBand(mesh,corner,edge+2,edge-3,reach,light);
            }
        }
    }

    private Vector2 Corner(int index,float expansion)
    {
        const int arcSteps=8;
        int corner=index/arcSteps;
        float angle=(corner*90+(index%arcSteps)*90f/(arcSteps-1))*Mathf.Deg2Rad;
        float radius=Mathf.Max(1,6+expansion);
        var half=rectTransform.rect.size*.5f+Vector2.one*expansion;
        var center=new Vector2((corner==0 || corner==3?1:-1)*(half.x-radius),
            (corner<2?1:-1)*(half.y-radius));
        return center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
    }

    // Extend each end of the arc along its tangent to form a single continuous bracket.
    private void CornerBand(VertexHelper mesh,int corner,float outer,float inner,float reach,Color tint)
    {
        int first=mesh.currentVertCount;
        for(int step=-1;step<=8;step++){
            int index=corner*8+Mathf.Clamp(step,0,7);
            Vector2 leg=step<0?-Radial[(corner+1)%4]*reach:step==8?-Radial[corner]*reach:Vector2.zero;
            mesh.AddVert(Corner(index,outer)+leg,tint,Vector2.zero);
            mesh.AddVert(Corner(index,inner)+leg,tint,Vector2.zero);
            if(step<0)continue;
            int a=first+step*2;
            mesh.AddTriangle(a,a+2,a+1);mesh.AddTriangle(a+1,a+2,a+3);
        }
    }

    private void Ring(VertexHelper mesh,float outer,float inner,Color tint)
    {
        int first=mesh.currentVertCount;
        for(int i=0;i<32;i++)
        {
            mesh.AddVert(Corner(i,outer),tint,Vector2.zero);
            mesh.AddVert(Corner(i,inner),tint,Vector2.zero);
        }
        for(int i=0;i<32;i++)
        {
            int a=first+i*2,b=first+(i+1)%32*2;
            mesh.AddTriangle(a,b,a+1);mesh.AddTriangle(a+1,b,b+1);
        }
    }
}
