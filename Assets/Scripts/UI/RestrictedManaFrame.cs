using UnityEngine;
using UnityEngine.UI;

// Spending restrictions keep their mana hue, inside a distinct shield frame.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RestrictedManaFrame : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();var r=rectTransform.rect;
        var points=new[]{new Vector2(-.47f,.35f),new Vector2(0,.49f),new Vector2(.47f,.35f),new Vector2(.39f,-.16f),new Vector2(0,-.49f),new Vector2(-.39f,-.16f)};
        for(int i=0;i<points.Length;i++) {
            var a=Vector2.Scale(points[i],r.size)+r.center;var b=Vector2.Scale(points[(i+1)%points.Length],r.size)+r.center;
            var normal=new Vector2(-(b-a).y,(b-a).x).normalized*1.15f;int start=mesh.currentVertCount;
            mesh.AddVert(a+normal,color,Vector2.zero);mesh.AddVert(b+normal,color,Vector2.zero);
            mesh.AddVert(b-normal,color,Vector2.zero);mesh.AddVert(a-normal,color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
