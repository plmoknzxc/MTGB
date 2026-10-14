using UnityEngine;
using UnityEngine.UI;

// The Forge atlas shield touches its cell's upper/side edges. Draw the complete
// outline inside the graphic rect so the thin upper rim survives HUD scaling.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ExperienceCounterIcon : MaskableGraphic
{
    static readonly Vector2[] Rim={new Vector2(-14,10),new Vector2(-5,10),new Vector2(-2,8),new Vector2(-3,5),new Vector2(0,2),new Vector2(3,5),new Vector2(2,8),new Vector2(5,10),new Vector2(14,10),new Vector2(12,5),new Vector2(11,3),new Vector2(11,-3),new Vector2(9,-7),new Vector2(0,-12),new Vector2(-9,-7),new Vector2(-11,-3),new Vector2(-11,3),new Vector2(-12,5)};
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();float scale=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/32;
        for(int i=0;i<Rim.Length;i++)Line(vh,Rim[i],Rim[(i+1)%Rim.Length],scale);
        Line(vh,new Vector2(0,2),new Vector2(0,-12),scale);
    }
    void Line(VertexHelper vh,Vector2 a,Vector2 b,float scale)
    {
        Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized*.95f;int start=vh.currentVertCount;
        vh.AddVert((a+normal)*scale,color,Vector2.zero);vh.AddVert((b+normal)*scale,color,Vector2.zero);
        vh.AddVert((b-normal)*scale,color,Vector2.zero);vh.AddVert((a-normal)*scale,color,Vector2.zero);
        vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
    }
}
