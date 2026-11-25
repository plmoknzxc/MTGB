using UnityEngine;
using UnityEngine.UI;

public sealed class StackIllustration : RawImage
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        Rect rect = GetPixelAdjustedRect();
        float shortSide = Mathf.Min(rect.width, rect.height);
        StackSurfaceMesh.Draw(mesh, rect, uvRect, shortSide * .072f, shortSide * .008f,
            color, color, Color.clear);
    }
}

// The UVs follow the original rectangular crop all the way to the curved edge.
// Both the painted surface and its fine rim end at the same antialiased contour.
internal static class StackSurfaceMesh
{
    internal static void Draw(VertexHelper mesh, Rect rect, Rect uv, float radius, float feather,
        Color upper, Color lower, Color rim)
    {
        mesh.Clear();
        if (rect.width < 2 || rect.height < 2) return;
        radius = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * .5f);
        feather = Mathf.Min(feather, radius * .45f);
        const int steps = 7, points = 4 * (steps + 1);
        mesh.AddVert(rect.center, Color.Lerp(lower, upper, .5f), uv.center);

        for (int corner = 0; corner < 4; corner++) {
            var center = new Vector2(corner is 0 or 3 ? rect.xMax - radius : rect.xMin + radius,
                corner < 2 ? rect.yMax - radius : rect.yMin + radius);
            for (int step = 0; step <= steps; step++) {
                float angle = (corner + (float)step / steps) * Mathf.PI * .5f;
                var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                for (int ring = 0; ring < 3; ring++) {
                    var point = center + normal * (radius - (2 - ring) * feather);
                    Vector2 unit = Rect.PointToNormalized(rect, point);
                    float light = .64f * unit.y + .36f * (1 - unit.x);
                    Color tint = Color.Lerp(lower, upper, light);
                    if (ring > 0) {
                        float alpha = tint.a;
                        tint = Color.Lerp(tint, rim, rim.a * Mathf.Lerp(.16f, 1, light));
                        tint.a = ring == 1 ? alpha : 0;
                    }
                    mesh.AddVert(point, tint, Rect.NormalizedToPoint(uv, unit));
                }
            }
        }
        for (int i = 0; i < points; i++) {
            int a = 1 + 3 * i, b = 1 + 3 * ((i + 1) % points);
            mesh.AddTriangle(0, b, a);
            for (int ring = 0; ring < 2; ring++) {
                mesh.AddTriangle(a + ring, b + ring, a + ring + 1);
                mesh.AddTriangle(a + ring + 1, b + ring, b + ring + 1);
            }
        }
    }
}
