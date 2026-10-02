using UnityEngine;
using UnityEngine.UI;

// The selectable's tint only affects its background. Dim its caption as well.
[DisallowMultipleComponent]
public sealed class DisabledButtonVisual : MonoBehaviour
{
    private Button button;
    private Graphic[] captions;
    private bool wasEnabled = true;
    public static void Bind(Button button)
    {
        if (button.GetComponent<DisabledButtonVisual>() != null) return;
        // Card portraits and board pieces retain their own interaction highlights.
        var label = button.transform.Find("Label") ?? button.transform.Find("Text") ?? button.transform.Find("Text (TMP)");
        if (label != null && label.GetComponent<Graphic>() != null) button.gameObject.AddComponent<DisabledButtonVisual>();
    }
    private void Awake()
    {
        button = GetComponent<Button>();
        var labels = new System.Collections.Generic.List<Graphic>();
        foreach (Transform child in transform)
            if ((child.name == "Label" || child.name == "Text" || child.name == "Text (TMP)") && child.TryGetComponent<Graphic>(out var graphic)) labels.Add(graphic);
        captions = labels.ToArray();
    }
    private void LateUpdate()
    {
        bool enabledNow = button.enabled && button.IsInteractable();
        var colors = button.colors;
        var disabled = new Color(.25f,.25f,.25f,.8f);
        if (colors.disabledColor != disabled) { colors.disabledColor = disabled; button.colors = colors; }
        if (!enabledNow || !wasEnabled)
            foreach (var label in captions) if (label != null) label.canvasRenderer.SetAlpha(enabledNow ? 1 : .38f);
        wasEnabled = enabledNow;
    }
    private void OnDisable()
    {
        if (captions != null) foreach (var label in captions) if (label != null) label.canvasRenderer.SetAlpha(1);
        wasEnabled = true;
    }
}
