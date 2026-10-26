using UnityEngine;
using UnityEngine.UI;

// Analytic geometry with a one-screen-pixel alpha fringe. No tiny bitmap is
// enlarged, and diagonal strokes remain smooth at fractional Canvas scales.
public static class BattlefieldUiStroke
{
    public static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float halfWidth, Color color, float fringe)
    {
        var normal = new Vector2(-(b - a).y, (b - a).x).normalized;
        var clear = new Color(color.r, color.g, color.b, 0);
        int start = mesh.currentVertCount;
        float[] offsets = { halfWidth + fringe, halfWidth, -halfWidth, -halfWidth - fringe };
        for (int i = 0; i < 4; i++)
        {
            var tint = i is 0 or 3 ? clear : color;
            mesh.AddVert(a + normal * offsets[i], tint, Vector2.zero);
            mesh.AddVert(b + normal * offsets[i], tint, Vector2.zero);
        }
        for (int i = 0; i < 3; i++)
        {
            int n = start + i * 2;
            mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n + 2, n + 1, n + 3);
        }
    }
}
