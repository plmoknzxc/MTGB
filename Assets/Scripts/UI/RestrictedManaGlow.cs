using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Reuse the ordinary mana glyph, including its size and hue. The cool contour
// marks spending restrictions without replacing the symbol with another shape.
public sealed class RestrictedManaGlow : BaseMeshEffect
{
    public override void ModifyMesh(VertexHelper mesh)
    {
        if (!IsActive()) return;
        var original = new List<UIVertex>();
        mesh.GetUIVertexStream(original);
        var result = new List<UIVertex>(original.Count * 17);
        for (int ring = 0; ring < 2; ring++)
        {
            float radius = ring == 0 ? 2.2f : .9f;
            var tint = new Color(.35f, .9f, 1f, ring == 0 ? .14f : .48f);
            for (int direction = 0; direction < 8; direction++)
            {
                float angle = direction * Mathf.PI / 4;
                var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                foreach (var source in original)
                {
                    var vertex = source;
                    vertex.position += offset;
                    tint.a = (ring == 0 ? .14f : .48f) * source.color.a / 255f;
                    vertex.color = tint;
                    result.Add(vertex);
                }
            }
        }
        result.AddRange(original);
        mesh.Clear();
        mesh.AddUIVertexTriangleStream(result);
    }
}
