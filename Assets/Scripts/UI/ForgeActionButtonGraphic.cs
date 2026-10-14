using UnityEngine;
using UnityEngine.UI;

// The prototype's eight-pixel cut corners and diagonal gold gradient.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ForgeActionButtonGraphic : MaskableGraphic
{
    public bool Secondary;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();var r=rectTransform.rect;float cut=8;
        var corners=new[]{new Vector2(r.xMin+cut,r.yMax),new Vector2(r.xMax,r.yMax),new Vector2(r.xMax,r.yMin+cut),new Vector2(r.xMax-cut,r.yMin),new Vector2(r.xMin,r.yMin),new Vector2(r.xMin,r.yMax-cut)};
        Color Tint(Vector2 p){float t=Mathf.Clamp01((p.x-r.xMin)/r.width*.85f+(r.yMax-p.y)/r.height*.15f);return Color.Lerp(Secondary?new Color(.38f,.54f,.77f):new Color(.953f,.816f,.584f),Secondary?new Color(.18f,.30f,.48f):new Color(.729f,.533f,.282f),t)*color;}
        mesh.AddVert(r.center,Tint(r.center),Vector2.zero);
        foreach(var p in corners)mesh.AddVert(p,Tint(p),Vector2.zero);
        for(int i=0;i<corners.Length;i++)mesh.AddTriangle(0,1+i,1+(i+1)%corners.Length);
    }
}
