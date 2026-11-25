using UnityEngine;
using UnityEngine.UI;

// A brief contact mark. It lives on the presentation canvas, so the card and
// its input area stay in place even if the next request arrives immediately.
public sealed class BattlefieldChoicePulse : MaskableGraphic
{
    const float Duration = .29f;
    float elapsed;
    bool releasing;

    public void Play(Vector2 position, Color tint, bool release)
    {
        rectTransform.anchoredPosition = position;
        color = tint;
        releasing = release;
        elapsed = 0;
        gameObject.SetActive(true);
        SetVerticesDirty();
    }

    void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= Duration) gameObject.SetActive(false);
        else SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        float t = Mathf.Clamp01(elapsed / Duration);
        float ease = 1 - (1-t)*(1-t)*(1-t);
        float radius = releasing ? Mathf.Lerp(12, 30, ease) : Mathf.Lerp(28, 12, ease);
        Color ink = color;
        ink.a *= 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.32f, 1, t));
        Color shadow = new Color(.05f, .06f, .09f, ink.a * .85f);
        for (int quadrant = 0; quadrant < 4; quadrant++)
        {
            float start = quadrant * 90 + 24;
            Arc(mesh, radius, start, 2.8f, shadow);
            Arc(mesh, radius, start, 1.4f, ink);
        }
    }

    static void Arc(VertexHelper mesh, float radius, float start, float thickness, Color tint)
    {
        for (int i = 0; i <= 6; i++)
        {
            float angle = (start + i * 7) * Mathf.Deg2Rad;
            Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            int n = mesh.currentVertCount;
            mesh.AddVert(normal * (radius - thickness), tint, Vector2.zero);
            mesh.AddVert(normal * (radius + thickness), tint, Vector2.zero);
            if (i == 0) continue;
            mesh.AddTriangle(n-2, n-1, n);
            mesh.AddTriangle(n, n-1, n+1);
        }
    }
}
