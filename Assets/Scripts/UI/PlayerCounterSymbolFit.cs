using UnityEngine;
using UnityEngine.UI;

// Atlas sprites, font glyphs and vector outlines have different intrinsic bounds.
// Give player counter symbols the same visible span without stretching their art.
public sealed class PlayerCounterSymbolFit : BaseMeshEffect
{
    public override void ModifyMesh(VertexHelper mesh)
    {
        if(!IsActive() || mesh.currentVertCount==0)return;
        var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
        var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
        UIVertex vertex=default;
        for(int i=0;i<mesh.currentVertCount;i++){
            mesh.PopulateUIVertex(ref vertex,i);
            min=Vector2.Min(min,vertex.position);max=Vector2.Max(max,vertex.position);
        }
        float span=Mathf.Max(max.x-min.x,max.y-min.y);
        if(span<.001f)return;
        var rect=graphic.rectTransform.rect;
        float scale=(Mathf.Min(rect.width,rect.height)-4)/span;
        var center=(min+max)*.5f;
        for(int i=0;i<mesh.currentVertCount;i++){
            mesh.PopulateUIVertex(ref vertex,i);
            var xy=((Vector2)vertex.position-center)*scale+rect.center;
            vertex.position=new Vector3(xy.x,xy.y,vertex.position.z);
            mesh.SetUIVertex(vertex,i);
        }
    }
}
