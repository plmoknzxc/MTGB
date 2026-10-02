using System;
using UnityEngine;
using UnityEngine.UI;

public static class ReplayWidgets
{
    public static Canvas Canvas(string name, int order = 450)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        return canvas;
    }
    public static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(width, height); return r;
    }
    public static RectTransform Panel(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var rect = Rect(parent, name, x, y, w, h); rect.gameObject.AddComponent<Image>().color = color; return rect;
    }
    public static Text Label(Transform parent, string value, float x, float y, float w, float h, int size = 22,
        TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var r = Rect(parent, "Label", x, y, w, h);
        var text = r.gameObject.AddComponent<Text>(); text.font = GamePreferences.Font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value; text.fontSize = size; text.color = MenuVisualTheme.Paper; text.alignment = alignment;
        text.raycastTarget = false; text.supportRichText = false;
        r.gameObject.AddComponent<PreferenceText>().SetBaseSize(size);
        return text;
    }
    public static Button Button(Transform parent, string value, float x, float y, float w, Action action, bool primary = false)
    {
        var rect = Panel(parent, value, x, y, w, 52, primary ? MenuVisualTheme.Primary : MenuVisualTheme.Raised);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(() => action?.Invoke());
        var label = Label(rect, value, 0, 0, w - 16, 48, 20, TextAnchor.MiddleCenter);
        label.resizeTextForBestFit = true; label.resizeTextMinSize = 14; label.resizeTextMaxSize = 26;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        return button;
    }
    public static InputField Input(Transform parent, string value, float x, float y, float w)
    {
        var r = Panel(parent, "NameInput", x, y, w, 52, MenuVisualTheme.Ink);
        var input = r.gameObject.AddComponent<InputField>();
        var labelRect = Rect(r, "InputText", 0, 0, w - 24, 46);
        var label = labelRect.gameObject.AddComponent<Text>();
        label.font = GamePreferences.Font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 22; label.color = MenuVisualTheme.Paper; label.supportRichText = false;
        label.alignment = TextAnchor.MiddleLeft;
        input.textComponent = label; input.text = value; input.characterLimit = 120;
        input.customCaretColor = true; input.caretColor = Color.white; input.caretWidth = 3;
        input.caretBlinkRate = .85f;
        input.selectionColor = new Color(.3f, .55f, .9f, .65f);
        label.gameObject.AddComponent<PreferenceText>().SetBaseSize(22);
        return input;
    }
    public static Toggle AutoSaveToggle(Transform parent)
    {
        var row = Rect(parent, "AutoSaveReplays", 0, 0, 680, 44);
        row.gameObject.AddComponent<Image>().color = Color.clear;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;
        var toggle = row.gameObject.AddComponent<Toggle>();
        var box = Panel(row, "Box", 0, 0, 28, 28, MenuVisualTheme.Raised);
        box.anchorMin = box.anchorMax = new Vector2(0, .5f); box.anchoredPosition = new Vector2(18, 0);
        var check = Panel(box, "Check", 0, 0, 16, 16, MenuVisualTheme.Accent);
        var label = Label(row, "自动保存所有录像", 0, 0, 550, 44, 19);
        label.rectTransform.anchorMin = new Vector2(0, 0); label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(46, 0); label.rectTransform.offsetMax = Vector2.zero;
        toggle.targetGraphic = box.GetComponent<Image>(); toggle.graphic = check.GetComponent<Image>();
        toggle.SetIsOnWithoutNotify(GamePreferences.AutoSaveReplays);
        toggle.onValueChanged.AddListener(GamePreferences.SetAutoSaveReplays);
        row.gameObject.AddComponent<ReplayPreferenceToggle>();
        return toggle;
    }
    public static string TimeText(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"hh\:mm\:ss" : @"mm\:ss");
}

public sealed class ReplayPreferenceToggle : MonoBehaviour
{
    private void OnEnable() { GamePreferences.Changed += Refresh; Refresh(); }
    private void OnDisable() { GamePreferences.Changed -= Refresh; }
    private void Refresh() { GetComponent<Toggle>().SetIsOnWithoutNotify(GamePreferences.AutoSaveReplays); }
}
