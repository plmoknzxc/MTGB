using UnityEngine;
using UnityEngine.UI;

// The menu canvas uploads this mesh on layout changes. The vertex shader applies
// five approved Cubism keyforms, leaving the UI and mesh idle between poses.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TidelightsPortraitMotion : Graphic
{
    static readonly int Body = Shader.PropertyToID("_Body");
    static readonly int Hair = Shader.PropertyToID("_Hair");
    static readonly int PixelScale = Shader.PropertyToID("_PixelScale");
    TidelightsClip clip;
    Material poseMaterial;

    public override Texture mainTexture => clip?.Atlas;

    internal void Bind(TidelightsClip source)
    {
        clip = source;
        raycastTarget = false;
        poseMaterial = new Material(clip.Shader) { hideFlags = HideFlags.DontSave };
        material = poseMaterial;
        SetAllDirty();
    }

    internal void SetFrame(int index)
    {
        poseMaterial.SetVector(Body, clip.Body[index]);
        poseMaterial.SetVector(Hair, clip.Hair[index]);
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (clip == null) return;
        Rect rect = rectTransform.rect;
        var scale = new Vector2(rect.width / clip.Size.x, -rect.height / clip.Size.y);
        poseMaterial.SetVector(PixelScale, scale);
        foreach (var original in clip.Vertices)
        {
            var vertex = original;
            vertex.position = new Vector3(rect.xMin + vertex.position.x * scale.x,
                rect.yMax + vertex.position.y * scale.y);
            vertex.color = color;
            mesh.AddVert(vertex);
        }
        for (int i = 0; i < clip.Indices.Length; i += 3)
            mesh.AddTriangle(clip.Indices[i], clip.Indices[i+1], clip.Indices[i+2]);
    }

    protected override void OnDestroy()
    {
        if (poseMaterial != null) Destroy(poseMaterial);
        base.OnDestroy();
    }
}
