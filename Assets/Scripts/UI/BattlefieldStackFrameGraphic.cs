using UnityEngine;
using UnityEngine.UI;

// A hollow rounded frame, so translucent rows keep their original fill.
public sealed class BattlefieldStackFrameGraphic : MaskableGraphic
{
    public float Radius = 3;
    public float Thickness = 1;

    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        var rect = rectTransform.rect;
        float radius = Mathf.Min(Mathf.Max(Thickness, Radius), Mathf.Min(rect.width, rect.height) * .5f);
        float inner = Mathf.Max(0, radius - Thickness);
        float fringe = .6f / Mathf.Max(.1f, canvas == null ? 1 : canvas.scaleFactor);
        const int segments = 6, points = 4 * (segments + 1);
        for (int corner = 0; corner < 4; corner++)
        {
            var center = new Vector2(corner is 0 or 3 ? rect.xMax - radius : rect.xMin + radius,
                corner < 2 ? rect.yMax - radius : rect.yMin + radius);
            for (int step = 0; step <= segments; step++)
            {
                float angle = (corner * 90 + step * 90f / segments) * Mathf.Deg2Rad;
                var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var vertex = UIVertex.simpleVert;
                for (int ring = 0; ring < 4; ring++) {
                    vertex.color = ring is 0 or 3 ? new Color(color.r, color.g, color.b, 0) : color;
                    float distance = ring == 0 ? radius + fringe : ring == 1 ? radius : ring == 2 ? inner : inner - fringe;
                    vertex.position = center + normal * distance; vertices.AddVert(vertex);
                }
            }
        }
        for (int i = 0; i < points; i++)
        {
            int next = (i + 1) % points;
            for (int ring = 0; ring < 3; ring++) {
                vertices.AddTriangle(i * 4 + ring, next * 4 + ring, i * 4 + ring + 1);
                vertices.AddTriangle(i * 4 + ring + 1, next * 4 + ring, next * 4 + ring + 1);
            }
        }
    }
}
