using UnityEngine;
using UnityEngine.UI;

public static class ObsidianArenaTheme
{
    public static readonly Color Void = Hex("#070A0FFF");
    public static readonly Color NavyField = Hex("#0D1A30FF");
    public static readonly Color RedField = Hex("#241016FF");
    public static readonly Color Surface = Hex("#11141BFA");
    public static readonly Color SurfaceRaised = Hex("#171C27FF");
    public static readonly Color Field = Hex("#0B0F16FF");
    public static readonly Color Line = Hex("#394355FF");
    public static readonly Color LineSoft = Hex("#263041FF");
    public static readonly Color Text = Hex("#EDF2F8FF");
    public static readonly Color MutedText = Hex("#8995A9FF");
    public static readonly Color Blue = Hex("#4285F4FF");
    public static readonly Color BlueDeep = Hex("#244987FF");
    public static readonly Color Red = Hex("#DC3C5AFF");
    public static readonly Color RedDeep = Hex("#5B1D2BFF");
    public static readonly Color Success = Hex("#52C7A2FF");
    public static readonly Color Warning = Hex("#D8A84EFF");

    public static void StyleButton(Button button, Color baseColor, bool addFrame = true)
    {
        if (button == null) return;

        Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        if (image != null)
        {
            image.color = baseColor;
            if (addFrame) EnsureOutline(image, Line, new Vector2(1f, -1f));
        }

        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.14f, 1.14f, 1.14f, 1f);
        colors.selectedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.disabledColor = new Color(0.38f, 0.42f, 0.48f, 0.52f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    public static void StyleField(Selectable selectable)
    {
        if (selectable == null) return;
        Image image = selectable.targetGraphic as Image ?? selectable.GetComponent<Image>();
        if (image != null)
        {
            image.color = Field;
            EnsureOutline(image, LineSoft, new Vector2(1f, -1f));
        }

        ColorBlock colors = selectable.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
        colors.selectedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
        colors.disabledColor = new Color(0.42f, 0.44f, 0.48f, 0.58f);
        selectable.colors = colors;
    }

    public static void EnsureOutline(Graphic graphic, Color color, Vector2 distance)
    {
        if (graphic == null) return;
        Outline outline = graphic.GetComponent<Outline>();
        if (outline == null) outline = graphic.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    public static Color Hex(string value)
    {
        return ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.magenta;
    }
}
