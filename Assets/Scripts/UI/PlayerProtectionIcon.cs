using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class PlayerProtectionIcon : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var points=new[]{new Vector2(-.42f,.44f),new Vector2(0,.5f),new Vector2(.42f,.44f),new Vector2(.36f,-.12f),new Vector2(0,-.5f),new Vector2(-.36f,-.12f)};
        var size=rectTransform.rect.size;
        for(int i=0;i<points.Length;i++) {
            var a=Vector2.Scale(points[i],size);var b=Vector2.Scale(points[(i+1)%points.Length],size);int start=mesh.currentVertCount;
            mesh.AddVert(a,color,Vector2.zero);mesh.AddVert(b,color,Vector2.zero);
            mesh.AddVert(b*.78f,color,Vector2.zero);mesh.AddVert(a*.78f,color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
