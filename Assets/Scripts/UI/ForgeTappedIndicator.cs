using UnityEngine;
using UnityEngine.UI;

// A translucent clockwise arrow, without the opaque circular background of the {T} glyph.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ForgeTappedIndicator : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.33f;
        float half=radius*.19f;
        const int segments=30;
        for(int i=0;i<=segments;i++)
        {
            float angle=Mathf.Lerp(210,-60,i/(float)segments)*Mathf.Deg2Rad;
            var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            mesh.AddVert(direction*(radius-half),color,Vector2.zero);
            mesh.AddVert(direction*(radius+half),color,Vector2.zero);
            if(i==0)continue;
            int n=i*2;
            mesh.AddTriangle(n-2,n-1,n);mesh.AddTriangle(n,n-1,n+1);
        }
        float end=-60*Mathf.Deg2Rad;
        var radial=new Vector2(Mathf.Cos(end),Mathf.Sin(end));
        var tangent=new Vector2(radial.y,-radial.x);
        var center=radial*radius;
        int tip=mesh.currentVertCount;
        mesh.AddVert(center+radial*half*2.3f,color,Vector2.zero);
        mesh.AddVert(center-radial*half*2.3f,color,Vector2.zero);
        mesh.AddVert(center+tangent*half*3.4f,color,Vector2.zero);
        mesh.AddTriangle(tip,tip+1,tip+2);
    }
}
