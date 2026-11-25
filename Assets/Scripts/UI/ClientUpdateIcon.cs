using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class ClientUpdateIcon : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        float unit=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/32f;
        Vector2 center=rectTransform.rect.center;
        void Stroke(Vector2 from,Vector2 to)
        {
            Vector2 normal=Vector2.Perpendicular((to-from).normalized)*1.65f;
            int i=mesh.currentVertCount;
            mesh.AddVert(center+(from-normal)*unit,color,Vector2.zero);
            mesh.AddVert(center+(from+normal)*unit,color,Vector2.zero);
            mesh.AddVert(center+(to+normal)*unit,color,Vector2.zero);
            mesh.AddVert(center+(to-normal)*unit,color,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
        }
        Stroke(new Vector2(0,13),new Vector2(0,-3));
        Stroke(new Vector2(-7,3),new Vector2(.9f,-4.8f));
        Stroke(new Vector2(7,3),new Vector2(-.9f,-4.8f));
        Stroke(new Vector2(-11,-5),new Vector2(-11,-11));
        Stroke(new Vector2(11,-5),new Vector2(11,-11));
        Stroke(new Vector2(-12.6f,-11),new Vector2(12.6f,-11));
    }
}
