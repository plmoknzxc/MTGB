using System;
using UnityEngine;

// Unscaled presentation tween. Retarget from the current pose, never from an old snapshot.
[DisallowMultipleComponent]
public sealed class ForgeUiMotion : MonoBehaviour
{
    private RectTransform rect, follow;
    private Vector2 start, end, startSize, endSize;
    private float startAngle, endAngle, elapsed, duration, arc;
    private Action completed;
    public bool IsMoving { get; private set; }

    public static ForgeUiMotion On(RectTransform rect)
    {
        var motion=rect.GetComponent<ForgeUiMotion>();
        return motion!=null?motion:rect.gameObject.AddComponent<ForgeUiMotion>();
    }

    public void MoveTo(Vector2 position, Vector2 size, float angle = 0, float seconds = .32f, float lift = 0)
    {
        rect = (RectTransform)transform;
        if (follow == null && end == position && endSize == size && Mathf.Abs(Mathf.DeltaAngle(endAngle, angle)) < .01f
            && IsMoving) return;
        Begin(position, size, angle, seconds, lift, null);
    }

    public void Follow(RectTransform destination, float seconds, Action onComplete)
    {
        if (follow == destination && IsMoving) return;
        Pose(destination, (RectTransform)transform.parent, out var position, out var size, out var angle);
        Begin(position, size, angle, seconds, Mathf.Min(38,Vector2.Distance(((RectTransform)transform).anchoredPosition,position)*.07f), onComplete);
        follow = destination;
    }

    public void Cancel() { IsMoving = false; follow = null; completed = null; }

    private void Begin(Vector2 position, Vector2 size, float angle, float seconds, float lift, Action callback)
    {
        rect = (RectTransform)transform;
        start = rect.anchoredPosition; startSize = rect.sizeDelta; startAngle = rect.localEulerAngles.z;
        end = position; endSize = size; endAngle = angle;
        duration = Mathf.Max(.01f, seconds); elapsed = 0; arc = lift;
        follow = null; completed = callback; IsMoving = true;
    }

    private void LateUpdate()
    {
        if (!IsMoving) return;
        if (follow != null) Pose(follow, (RectTransform)rect.parent, out end, out endSize, out endAngle);
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float eased = 1 - Mathf.Pow(1 - t, 3);
        rect.anchoredPosition = Vector2.LerpUnclamped(start, end, eased) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * arc);
        rect.sizeDelta = Vector2.LerpUnclamped(startSize, endSize, eased);
        rect.localRotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(startAngle, endAngle, eased));
        if (t < 1) return;
        IsMoving = false; follow = null;
        var callback = completed; completed = null; callback?.Invoke();
    }

    public static void Pose(RectTransform source, RectTransform space, out Vector2 position, out Vector2 size, out float angle)
    {
        position = space.InverseTransformPoint(source.TransformPoint(source.rect.center));
        var right = space.InverseTransformVector(source.TransformVector(Vector3.right * source.rect.width));
        var up = space.InverseTransformVector(source.TransformVector(Vector3.up * source.rect.height));
        size = new Vector2(right.magnitude, up.magnitude);
        angle = Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg;
    }
}
