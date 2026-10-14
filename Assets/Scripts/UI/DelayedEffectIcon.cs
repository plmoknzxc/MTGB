using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class DelayedEffectIcon : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();float scale=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/16;
        for(int i=0;i<32;i++){float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;Line(mesh,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*6,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*6,scale);}
        Line(mesh,Vector2.zero,new Vector2(0,3.5f),scale);Line(mesh,Vector2.zero,new Vector2(3,-1.5f),scale);
    }
    private void Line(VertexHelper mesh,Vector2 a,Vector2 b,float scale)
    {
        Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized*.7f;int start=mesh.currentVertCount;
        mesh.AddVert((a+normal)*scale,color,Vector2.zero);mesh.AddVert((b+normal)*scale,color,Vector2.zero);mesh.AddVert((b-normal)*scale,color,Vector2.zero);mesh.AddVert((a-normal)*scale,color,Vector2.zero);
        mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
    }
}
