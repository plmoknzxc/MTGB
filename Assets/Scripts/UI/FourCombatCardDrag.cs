using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The drag image never participates in picking. Only the topmost raycast at release counts.
public sealed class FourCombatCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    internal RectTransform board;
    internal ScrollRect destinations;
    internal Func<bool> live;
    internal Action begin, finish;
    internal Action<Transform> hover, drop;
    private RectTransform ghost;
    private PointerEventData pointer;
    private List<RaycastResult> hits;

    public void OnBeginDrag(PointerEventData e)
    {
        if (e.pointerPress!=null && e.pointerPress!=gameObject && e.pointerPress.GetComponentInParent<Button>()!=GetComponent<Button>())return;
        if (ghost != null || e.button != PointerEventData.InputButton.Left || live?.Invoke() != true) return;
        e.eligibleForClick = false;
        pointer = e;
        ghost = Instantiate((RectTransform)transform, board, false);
        ghost.name = "DraggedCombatCard";
        ghost.anchorMin = ghost.anchorMax = ghost.pivot = new Vector2(.5f,.5f);
        ghost.localScale = Vector3.one * .9f;
        var group = ghost.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        group.alpha = .9f;
        ghost.GetComponent<FourCombatCardDrag>().enabled = false;
        begin?.Invoke();
        OnDrag(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (ghost == null || pointer.pointerId != e.pointerId) return;
        if (live?.Invoke() != true) { Cancel(); return; }
        pointer = e;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(board,e.position,e.pressEventCamera,out var point))
            ghost.anchoredPosition = point + new Vector2(16,18);
        hover?.Invoke(Hit());
    }

    private Transform Hit()
    {
        hits ??= new List<RaycastResult>();
        hits.Clear();
        EventSystem.current.RaycastAll(pointer,hits);
        return hits.Count > 0 ? hits[0].gameObject.transform : null;
    }

    private void Update()
    {
        if (ghost == null) return;
        if (live?.Invoke() != true) { Cancel(); return; }
        var viewport = destinations.viewport;
        float travel = destinations.content.rect.width - viewport.rect.width;
        if (travel <= 1 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,pointer.position,pointer.pressEventCamera,out var point)
            || !viewport.rect.Contains(point)) return;
        const float edge = 42;
        float left = point.x - viewport.rect.xMin, right = viewport.rect.xMax - point.x;
        float speed = left < edge ? -1 + left/edge : right < edge ? 1 - right/edge : 0;
        if (speed == 0) return;
        destinations.horizontalNormalizedPosition = Mathf.Clamp01(destinations.horizontalNormalizedPosition + speed*540*Time.unscaledDeltaTime/travel);
        hover?.Invoke(Hit());
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (ghost == null || pointer.pointerId != e.pointerId) return;
        e.eligibleForClick = false;
        pointer = e;
        var target = live?.Invoke() == true ? Hit() : null;
        Cancel();
        if (target != null) drop?.Invoke(target);
    }

    private void OnDisable() => Cancel();
    private void Cancel()
    {
        if (ghost == null) return;
        Destroy(ghost.gameObject);
        ghost = null;
        pointer = null;
        finish?.Invoke();
    }
}
