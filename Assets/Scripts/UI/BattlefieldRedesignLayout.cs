using System.Collections;
using UnityEngine;

// Production battlefield marker/bootstrap, with an editor-only offline layout preview.
[DisallowMultipleComponent]
public sealed class BattlefieldRedesignLayout : MonoBehaviour
{
    [SerializeField] private TextAsset editorPreview;
    [SerializeField] private bool previewWhenOffline = true;

#if UNITY_EDITOR
    public ForgeSeatView ReadEditorPreview() => editorPreview == null ? null
        : ServerSnapshotJson.Deserialize<ForgeSeatView>(editorPreview.text);
#endif

    // The scene contains editable HUD templates. Reveal them only after a seat
    // view has populated and themed the controls for the first rendered frame.
    public void SetHudVisible(bool visible)
    {
        if (!TryGetComponent<CanvasGroup>(out var group)) return;
        group.alpha = visible ? 1 : 0;
        group.interactable = group.blocksRaycasts = visible;
    }

    private IEnumerator Start()
    {
        yield return null;
        if(ReplayPlayback.Current!=null||ReplayExportWorker.IsWorker)yield break;
        var owner = FindFirstObjectByType<OnlineMatchService>();
        if (owner != null && (owner.IsInMatch || owner.IsPreparingMatch || owner.EndReview != null))
        {
            ForgeBattlefieldController.Open(owner);
            yield break;
        }
#if UNITY_EDITOR
        if (previewWhenOffline && editorPreview != null)
        {
            while (!GamePreferences.Ready) yield return null;
            var controller = FindFirstObjectByType<ForgeBattlefieldController>();
            if (controller == null) controller = new GameObject("ForgeBattlefield").AddComponent<ForgeBattlefieldController>();
            var state = ReadEditorPreview();
            // Offline review is read-only. Native callbacks are bound when a real match attaches.
            controller.Present(state, state.seat, false, null, null, "战场预览");
        }
#endif
    }
}
