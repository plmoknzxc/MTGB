using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class RingBearerIcon : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();float scale=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/20;
        for(int i=0;i<40;i++) {
            float a=i*Mathf.PI/20,b=(i+1)*Mathf.PI/20;int start=mesh.currentVertCount;
            mesh.AddVert(new Vector2(Mathf.Cos(a)*8,Mathf.Sin(a)*8)*scale,color,Vector2.zero);
            mesh.AddVert(new Vector2(Mathf.Cos(b)*8,Mathf.Sin(b)*8)*scale,color,Vector2.zero);
            mesh.AddVert(new Vector2(Mathf.Cos(b)*5,Mathf.Sin(b)*5)*scale,color,Vector2.zero);
            mesh.AddVert(new Vector2(Mathf.Cos(a)*5,Mathf.Sin(a)*5)*scale,color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
