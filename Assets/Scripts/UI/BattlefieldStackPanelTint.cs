using UnityEngine;
using UnityEngine.UI;

// A light diagonal tint on the existing rounded panel sprite.
public sealed class BattlefieldStackPanelTint : BaseMeshEffect
{
    private static readonly Color Top = new(38 / 255f, 49 / 255f, 57 / 255f, 196 / 255f);
    private static readonly Color Bottom = new(9 / 255f, 21 / 255f, 30 / 255f, 219 / 255f);

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive()) return;
        var rect = graphic.rectTransform.rect;
        var vertex = new UIVertex();
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            float x = Mathf.InverseLerp(rect.xMin, rect.xMax, vertex.position.x);
            float y = 1 - Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y);
            vertex.color = (Color)vertex.color * Color.Lerp(Top, Bottom, x * .35f + y * .65f);
            vertices.SetUIVertex(vertex, i);
        }
    }
}
