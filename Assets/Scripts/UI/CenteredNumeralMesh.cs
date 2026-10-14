using UnityEngine;
using UnityEngine.UI;

// Numeric HUD values need their visible glyphs centered, rather than a font's
// ascender/descender line box (which includes space unused by digits).
[RequireComponent(typeof(Text))]
public sealed class CenteredNumeralMesh : BaseMeshEffect
{
    public override void ModifyMesh(VertexHelper mesh)
    {
        if(!IsActive() || mesh.currentVertCount==0)return;
        var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
        var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
        UIVertex vertex=default;
        for(int i=0;i+3<mesh.currentVertCount;i+=4){
            mesh.PopulateUIVertex(ref vertex,i);var a=vertex.position;
            mesh.PopulateUIVertex(ref vertex,i+2);var b=vertex.position;
            if(Mathf.Abs(a.x-b.x)<.001f || Mathf.Abs(a.y-b.y)<.001f)continue;
            for(int j=0;j<4;j++){mesh.PopulateUIVertex(ref vertex,i+j);min=Vector2.Min(min,vertex.position);max=Vector2.Max(max,vertex.position);}
        }
        if(float.IsInfinity(min.x))return;
        var delta=graphic.rectTransform.rect.center-(min+max)*.5f;
        for(int i=0;i<mesh.currentVertCount;i++){mesh.PopulateUIVertex(ref vertex,i);vertex.position+=(Vector3)delta;mesh.SetUIVertex(vertex,i);}
    }
}
