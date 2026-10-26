using System;
using UnityEngine;

// Presentation only. Layout supplies the latest destination; repeated snapshots
// never restart a transition and leaving elements stop accepting input at once.
[DisallowMultipleComponent, DefaultExecutionOrder(1000)]
public sealed class BattlefieldUiTransition : MonoBehaviour
{
    RectTransform rect;
    CanvasGroup group;
    Vector2 position, size, fromPosition, fromSize, targetPosition, targetSize;
    float alpha, fromAlpha, targetAlpha, elapsed;
    bool initialized, horizontal = true, interactive, leaving, dragged;
    Action hidden;
    const float Duration = .24f;

    public bool IsMoving => initialized && elapsed < Duration;
    public static BattlefieldUiTransition On(RectTransform rect)
    {
        var motion=rect.GetComponent<BattlefieldUiTransition>();
        return motion!=null?motion:rect.gameObject.AddComponent<BattlefieldUiTransition>();
    }

    public void Place(Vector2 destination, Vector2 dimensions, float opacity, Vector2 arrival,
        bool acceptsInput = true, bool animateHorizontal = true)
    {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        if(group==null)group=gameObject.AddComponent<CanvasGroup>();
        gameObject.SetActive(true);
        if (!initialized) {
            initialized = true; position = destination + arrival; size = dimensions; alpha = 0;
        }
        horizontal = animateHorizontal; interactive = acceptsInput;
        if (leaving || targetPosition != destination || targetSize != dimensions || !Mathf.Approximately(targetAlpha, opacity)) {
            fromPosition = position; fromSize = size; fromAlpha = alpha;
            targetPosition = destination; targetSize = dimensions; targetAlpha = opacity; elapsed = 0;
        }
        leaving = false; hidden = null;
        Apply();
    }

    public void Hide(Vector2 departure, Action onHidden = null)
    {
        if (!initialized) { gameObject.SetActive(false); onHidden?.Invoke(); return; }
        if (!gameObject.activeSelf) { onHidden?.Invoke(); return; }
        interactive = false;
        if (!leaving) {
            fromPosition = position; fromSize = size; fromAlpha = alpha;
            targetPosition += departure; targetAlpha = 0; elapsed = 0; leaving = true;
            hidden = onHidden ?? (() => gameObject.SetActive(false));
        }
        Apply();
    }

    public void Rebase()
    {
        if (!initialized) return;
        rect = (RectTransform)transform;
        position = fromPosition = targetPosition = rect.anchoredPosition;
        size = fromSize = targetSize = rect.sizeDelta;
        elapsed = Duration;
    }

    void LateUpdate()
    {
        if (!initialized) return;
        elapsed = Mathf.Min(Duration, elapsed + Time.unscaledDeltaTime);
        float eased = 1 - Mathf.Pow(1 - elapsed / Duration, 3);
        position = Vector2.LerpUnclamped(fromPosition, targetPosition, eased);
        size = Vector2.LerpUnclamped(fromSize, targetSize, eased);
        alpha = Mathf.LerpUnclamped(fromAlpha, targetAlpha, eased);
        Apply();
        if (leaving && elapsed >= Duration) {
            leaving = false; var callback = hidden; hidden = null; callback?.Invoke();
        }
    }

    void Apply()
    {
        // Drag owns the live card pose. Hover owns its child artwork as before.
        var pointer = GetComponent<ForgeBattlefieldCardPointer>();
        if (pointer != null && pointer.IsCastingDrag) dragged = true;
        else {
            if (dragged) {
                dragged = false; fromPosition = position = rect.anchoredPosition;
                fromSize = size; fromAlpha = alpha; elapsed = 0;
            }
            rect.anchoredPosition = horizontal ? position : new Vector2(rect.anchoredPosition.x, position.y);
            rect.sizeDelta = size;
        }
        group.alpha = alpha;
        group.interactable = group.blocksRaycasts = interactive && targetAlpha > .02f && alpha > .1f;
    }
}
