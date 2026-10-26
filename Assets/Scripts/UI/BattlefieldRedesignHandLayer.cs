using UnityEngine;
using UnityEngine.UI;

// Lift only the hovered face above battlefield overlays. Its resting hit area,
// sibling order and native card identity remain under the existing hand root.
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class BattlefieldRedesignHandLayer : MonoBehaviour
{
    ForgeBattlefieldCardPointer pointer;
    Canvas overlay;
    int layer, order;

    public void Configure(ForgeBattlefieldCardPointer owner, int sortingLayer, int sortingOrder)
    {
        if(pointer!=owner){
            if(pointer!=null)pointer.VisualStateChanged-=Apply;
            pointer=owner;
            if(pointer!=null)pointer.VisualStateChanged+=Apply;
        }
        layer = sortingLayer; order = sortingOrder;
        overlay = GetComponent<Canvas>();
        if (overlay == null) overlay = gameObject.AddComponent<Canvas>();
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        Apply();
    }
    void LateUpdate() => Apply();
    void Apply()
    {
        if (overlay == null || pointer == null) return;
        // Enter raises synchronously. Exit keeps the face above headers while
        // it animates back, instead of dropping beneath them halfway down.
        bool returning=transform.localPosition.sqrMagnitude>.25f||(transform.localScale-Vector3.one).sqrMagnitude>.0001f;
        bool raised = pointer.isActiveAndEnabled && (pointer.IsCastingDrag || pointer.AllowHandHover && pointer.IsHovered||returning);
        var group = pointer.GetComponent<CanvasGroup>();
        raised &= group == null || group.alpha > .02f;
        if (overlay.overrideSorting != raised) overlay.overrideSorting = raised;
        if (!raised) return;
        if (overlay.sortingLayerID != layer) overlay.sortingLayerID = layer;
        int effectiveOrder = order;
        foreach (var parent in transform.parent.GetComponentsInParent<Canvas>())
            if (parent.overrideSorting) effectiveOrder = Mathf.Max(effectiveOrder, parent.sortingOrder + 1);
        if (overlay.sortingOrder != effectiveOrder) overlay.sortingOrder = effectiveOrder;
    }
    void OnDisable() { if (overlay != null) overlay.overrideSorting = false; }
    void OnDestroy(){if(pointer!=null)pointer.VisualStateChanged-=Apply;}
}
