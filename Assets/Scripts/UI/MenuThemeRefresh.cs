using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

// Repaint existing menu controls in place. Their selection, scroll position, layouts
// and input listeners belong to their screens and must survive a theme change.
internal sealed class MenuThemeRefresh
{
    readonly Dictionary<Color32, Color32> colors = new();
    readonly HashSet<Color32> ambiguous = new();
    readonly Sprite previousHeader, nextHeader, previousButton, nextButton;
    readonly Color previousMuted, nextMuted;

    MenuThemeRefresh(MenuThemeStyle previous, MenuThemeStyle next)
    {
        // Palette roles are the serialized Color32 fields on every menu profile.
        foreach (var field in typeof(MenuThemeStyle).GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (field.FieldType != typeof(Color32)) continue;
            var oldColor = (Color32)field.GetValue(previous);
            var newColor = (Color32)field.GetValue(next);
            if (colors.TryGetValue(oldColor, out var other) && !other.Equals(newColor))
                ambiguous.Add(oldColor);
            else colors[oldColor] = newColor;
        }
        previousMuted = previous.Stone;
        nextMuted = next.Stone;
        previousHeader = Header(previous);
        nextHeader = Header(next);
        previousButton = previous.Load<Sprite>(previous.ButtonSprite);
        nextButton = next.Load<Sprite>(next.ButtonSprite);
    }

    static Sprite Header(MenuThemeStyle theme) => theme.Load<Sprite>(theme.HeaderArt)
        ?? Resources.Load<Sprite>(MenuThemeCatalog.Default.HeaderArt);

    internal static void Apply(MenuThemeStyle previous, MenuThemeStyle next)
    {
        if (previous == null || next == null || previous == next) return;
        var refresh = new MenuThemeRefresh(previous, next);
        var roots = new HashSet<Transform>();
        foreach (var scope in Object.FindObjectsByType<MenuTypography>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // The deck and title share a canvas with their overlay panels and dropdown lists.
            var root = scope.transform;
            roots.Add(root.name == "StartScreen" && root.parent != null ? root.parent : root);
        }
        foreach (var lobby in Object.FindObjectsByType<OnlineLobbyPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            roots.Add(lobby.transform);
        foreach (var replay in Object.FindObjectsByType<ReplayBrowser>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            roots.Add(replay.transform);
        foreach (var settings in Object.FindObjectsByType<GameSettingsPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            roots.Add(settings.transform);
        foreach (var field in Object.FindObjectsByType<ForgeBattlefieldController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (field.JournalThemeRoot != null) roots.Add(field.JournalThemeRoot);
            if (field.JournalToggleThemeRoot != null) roots.Add(field.JournalToggleThemeRoot);
        }
        foreach (var root in roots)
        {
            var ancestor = root.parent;
            while (ancestor != null && !roots.Contains(ancestor)) ancestor = ancestor.parent;
            if (ancestor == null) refresh.Repaint(root);
        }
    }

    Color Recolor(Color color)
    {
        Color32 key = color;
        // Only match an actual palette value, not an arbitrary color that rounds to it.
        if (color == (Color)key && !ambiguous.Contains(key) && colors.TryGetValue(key, out var mapped))
            return mapped;
        // Room form placeholders intentionally use the muted foreground at reduced opacity.
        if (color.a < previousMuted.a && color.r == previousMuted.r && color.g == previousMuted.g && color.b == previousMuted.b)
            return new Color(nextMuted.r, nextMuted.g, nextMuted.b, color.a);
        return color;
    }

    void Repaint(Transform root)
    {
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            graphic.color = Recolor(graphic.color);
            if (graphic is not Image image) continue;
            if ((image.name == "Panorama" || image.name == "TopControlBand") && image.sprite == previousHeader)
            {
                image.sprite = nextHeader;
                if (nextHeader != null && image.TryGetComponent<AspectRatioFitter>(out var fit))
                    fit.aspectRatio = nextHeader.rect.width / nextHeader.rect.height;
            }
            else if (previousButton != null && image.sprite == previousButton)
                image.sprite = nextButton;
        }
        foreach (var shadow in root.GetComponentsInChildren<Shadow>(true))
            shadow.effectColor = Recolor(shadow.effectColor);
        foreach (var control in root.GetComponentsInChildren<Selectable>(true))
        {
            if (control is InputField field)
            {
                field.selectionColor = Recolor(field.selectionColor);
                if (field.customCaretColor) field.caretColor = Recolor(field.caretColor);
            }
            if (control.transition != Selectable.Transition.ColorTint) continue;
            var state = control.colors;
            state.normalColor = Recolor(state.normalColor);
            state.highlightedColor = Recolor(state.highlightedColor);
            state.pressedColor = Recolor(state.pressedColor);
            state.selectedColor = Recolor(state.selectedColor);
            state.disabledColor = Recolor(state.disabledColor);
            if (state.Equals(control.colors)) continue;
            // Let Selectable resolve its own current pressed/hovered/disabled state immediately.
            var instant = state;
            instant.fadeDuration = 0;
            control.colors = instant;
            control.colors = state;
        }
    }
}
