using UnityEngine;
using UnityEngine.UI;

// Small, outlined loyalty shield; the current value is a separate readable label.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ForgeLoyaltyShield : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var r=rectTransform.rect;
        Vector2[] shape={new Vector2(-.46f,.45f),new Vector2(.46f,.45f),new Vector2(.42f,-.15f),new Vector2(0,-.48f),new Vector2(-.42f,-.15f)};
        void Layer(float scale,Color tint) {
            int start=mesh.currentVertCount;
            mesh.AddVert(r.center,tint,Vector2.zero);
            foreach(var p in shape)mesh.AddVert(r.center+Vector2.Scale(p,new Vector2(r.width,r.height))*scale,tint,Vector2.zero);
            for(int i=0;i<shape.Length;i++)mesh.AddTriangle(start,start+1+i,start+1+(i+1)%shape.Length);
        }
        Layer(1,new Color(.78f,.83f,.85f));
        Layer(.85f,new Color(.035f,.045f,.065f));
    }
}
