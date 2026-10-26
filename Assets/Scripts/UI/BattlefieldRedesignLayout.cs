using System.Collections;
using UnityEngine;

// Production battlefield marker/bootstrap, with an editor-only offline layout preview.
[DisallowMultipleComponent]
public sealed class BattlefieldRedesignLayout : MonoBehaviour
{
    public const string SceneName = "IntegratedBattlefieldRedesign";
    [SerializeField] private TextAsset editorPreview;
    [SerializeField] private bool previewWhenOffline = true;

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
            var state = JsonUtility.FromJson<ForgeSeatView>(editorPreview.text);
            // Offline review is read-only. Native callbacks are bound when a real match attaches.
            controller.Present(state, state.seat, false, null, null, "副本场景 · 只读布局预览");
        }
#endif
    }
}
