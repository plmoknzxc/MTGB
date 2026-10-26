using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class BattlefieldScrollChevron : MaskableGraphic
{
    public int direction = 1;
    public bool SmoothEdges;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var center = rectTransform.rect.center;
        var tip = center + new Vector2(4 * direction, 0);
        Segment(mesh, center + new Vector2(-4 * direction, 8), tip);
        Segment(mesh, tip, center + new Vector2(-4 * direction, -8));
    }
    private void Segment(VertexHelper mesh, Vector2 start, Vector2 end)
    {
        if (SmoothEdges) {
            BattlefieldUiStroke.Line(mesh, start, end, 1.4f, color, .65f / Mathf.Max(.1f, canvas.scaleFactor));
            return;
        }
        var offset = new Vector2(-(end-start).y, (end-start).x).normalized * 1.4f;
        int index = mesh.currentVertCount;
        mesh.AddVert(start+offset, color, Vector2.zero);
        mesh.AddVert(end+offset, color, Vector2.zero);
        mesh.AddVert(end-offset, color, Vector2.zero);
        mesh.AddVert(start-offset, color, Vector2.zero);
        mesh.AddTriangle(index, index+1, index+2);
        mesh.AddTriangle(index, index+2, index+3);
    }
}
