using UnityEngine;
using UnityEngine.UI;

// Preserve the existing rounded sprite and draw the mana colors at native UI resolution.
public sealed class BattlefieldLandFrameTint : BaseMeshEffect
{
    private Color left=Color.gray,right=Color.gray;
    public void Configure(Color first,Color second){left=first;right=second;graphic.SetVerticesDirty();}
    public override void ModifyMesh(VertexHelper mesh)
    {
        if(!IsActive())return;
        var bounds=((RectTransform)transform).rect;var vertex=new UIVertex();
        for(int i=0;i<mesh.currentVertCount;i++){
            mesh.PopulateUIVertex(ref vertex,i);
            vertex.color=Color.Lerp(left,right,Mathf.InverseLerp(bounds.xMin,bounds.xMax,vertex.position.x));
            mesh.SetUIVertex(vertex,i);
        }
    }
}
