using UnityEngine;
using UnityEngine.UI;

// Vector gear: no font glyph dependency and no bitmap scaling artifacts.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ForgeGearIcon : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.48f;
        const int segments=64;
        for(int i=0;i<=segments;i++)
        {
            float angle=i*2*Mathf.PI/segments;
            var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            float outer=(i%8>=2 && i%8<=5)?radius:radius*.76f;
            mesh.AddVert(direction*radius*.38f,color,Vector2.zero);
            mesh.AddVert(direction*outer,color,Vector2.zero);
            if(i==0)continue;
            int n=i*2;mesh.AddTriangle(n-2,n-1,n);mesh.AddTriangle(n,n-1,n+1);
        }
    }
}
