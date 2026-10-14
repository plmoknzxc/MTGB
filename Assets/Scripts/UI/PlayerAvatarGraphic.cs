using UnityEngine;
using UnityEngine.UI;

// Neutral portrait until a player supplies an avatar. No font-dependent glyphs.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PlayerAvatarGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();var r=rectTransform.rect;
        Ellipse(mesh,new Vector2(r.center.x,r.center.y+r.height*.16f),r.width*.16f,r.height*.18f);
        Ellipse(mesh,new Vector2(r.center.x,r.center.y-r.height*.24f),r.width*.32f,r.height*.25f);
    }
    private void Ellipse(VertexHelper mesh,Vector2 center,float x,float y)
    {
        int start=mesh.currentVertCount;mesh.AddVert(center,color,Vector2.zero);
        for(int i=0;i<=40;i++) {
            float angle=i*Mathf.PI*2/40;
            mesh.AddVert(center+new Vector2(Mathf.Cos(angle)*x,Mathf.Sin(angle)*y),color,Vector2.zero);
            if(i>0)mesh.AddTriangle(start,start+i,start+i+1);
        }
    }
}
