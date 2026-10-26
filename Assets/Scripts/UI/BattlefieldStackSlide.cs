using UnityEngine;

// The copied scene keeps its two controls on a separate fixed rail. Only the
// complete panel slides, and incoming snapshots do not restart the transition.
[DisallowMultipleComponent]
public sealed class BattlefieldStackSlide : MonoBehaviour
{
    RectTransform rect;
    float progress, start, target, elapsed;
    bool initialized;
    public void SetCollapsed(bool collapsed)
    {
        rect = (RectTransform)transform;
        float next = collapsed ? 1 : 0;
        if (!initialized) { initialized = true; start = progress = target = next; }
        if (target != next) { start = progress; target = next; elapsed = 0; }
        Apply();
    }
    void LateUpdate()
    {
        if (!initialized) return;
        elapsed = Mathf.Min(.28f, elapsed + Time.unscaledDeltaTime);
        float t = elapsed / .28f;
        progress = Mathf.Lerp(start, target, 1 - Mathf.Pow(1 - t, 3));
        Apply();
    }
    // Layout owns the vertical inset; hiding animates only the horizontal axis.
    void Apply() => rect.anchoredPosition = new Vector2(-28 + progress * (rect.rect.width + 48), rect.anchoredPosition.y);
}
