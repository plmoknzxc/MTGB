using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 定稿战场 UI 的程序化资源与 uGUI 构建助手：圆角矩形/圆形/渐变贴图运行时生成，
/// Text/Panel/Button 等沿用 ArenaPrototypeSceneFactory 的构建方式（无 prefab，全部代码创建）。
/// </summary>
public static class IntegratedUiUtility
{
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;

    private static Font cachedFont;
    private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

    public static Font Font
    {
        get
        {
            if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return cachedFont;
        }
    }

    /// <summary>圆角矩形 sprite（白色，着色靠 Image.color），同尺寸复用缓存。</summary>
    public static Sprite RoundedRect(float width, float height, float radius, string cacheKey = null)
    {
        int w = Mathf.Max(4, Mathf.CeilToInt(width));
        int h = Mathf.Max(4, Mathf.CeilToInt(height));
        int r = Mathf.Clamp(Mathf.CeilToInt(radius), 1, Mathf.Min(w, h) / 2);
        string key = cacheKey ?? $"rr{w}x{h}r{r}";
        if (SpriteCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        Color[] pixels = new Color[w * h];
        Vector2 inner = new Vector2(w * 0.5f - r - 1f, h * 0.5f - r - 1f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2 distance = new Vector2(Mathf.Abs(x - (w - 1) * 0.5f), Mathf.Abs(y - (h - 1) * 0.5f)) - inner;
                float outside = new Vector2(Mathf.Max(distance.x, 0f), Mathf.Max(distance.y, 0f)).magnitude - r;
                float alpha = Mathf.Clamp01(0.5f - outside);
                pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        return CacheSprite(key, pixels, w, h);
    }

    /// <summary>圆角矩形描边（边框线宽 border，内部透明），用于卡牌描边/辉光。</summary>
    public static Sprite RoundedRectOutline(float width, float height, float radius, float border, string cacheKey = null)
    {
        int w = Mathf.Max(4, Mathf.CeilToInt(width));
        int h = Mathf.Max(4, Mathf.CeilToInt(height));
        int r = Mathf.Clamp(Mathf.CeilToInt(radius), 1, Mathf.Min(w, h) / 2 - 1);
        int b = Mathf.Clamp(Mathf.CeilToInt(border), 1, r);
        string key = cacheKey ?? $"ro{w}x{h}r{r}b{b}";
        if (SpriteCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        Color[] pixels = new Color[w * h];
        Vector2 inner = new Vector2(w * 0.5f - r - 1f, h * 0.5f - r - 1f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2 distance = new Vector2(Mathf.Abs(x - (w - 1) * 0.5f), Mathf.Abs(y - (h - 1) * 0.5f)) - inner;
                float outside = new Vector2(Mathf.Max(distance.x, 0f), Mathf.Max(distance.y, 0f)).magnitude - r;
                float signed = -outside; // >0 在圆角矩形内部
                float alpha = Mathf.Clamp01(Mathf.Min(signed + 0.5f, b - signed + 0.5f));
                pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        return CacheSprite(key, pixels, w, h);
    }

    /// <summary>实心圆 sprite。</summary>
    public static Sprite Circle(int diameter, float softness = 0.5f)
    {
        string key = $"circle{diameter}";
        if (SpriteCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        Color[] pixels = new Color[diameter * diameter];
        float center = (diameter - 1) * 0.5f;
        float radius = center;
        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                pixels[y * diameter + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((radius - distance) / softness + 0.5f));
            }
        }

        return CacheSprite(key, pixels, diameter, diameter);
    }

    /// <summary>
    /// 垂直渐变贴图（自上而下）。stops 的 t 为 0(顶)→1(底)。
    /// 用于 oppzone/handzone 压暗带、地板纵向渐变等。
    /// </summary>
    public static Texture2D VerticalGradientTexture(int width, int height, IList<(float t, Color color)> stops)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "VerticalGradient",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float t = (float)y / (height - 1);
            Color color = SampleStops(stops, t);
            for (int x = 0; x < width; x++) pixels[y * width + x] = color;
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    /// <summary>水平渐变贴图（自左而右），用于左右暗角。</summary>
    public static Texture2D HorizontalGradientTexture(int width, int height, IList<(float t, Color color)> stops)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "HorizontalGradient",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color[] pixels = new Color[width * height];
        for (int x = 0; x < width; x++)
        {
            Color color = SampleStops(stops, (float)x / (width - 1));
            for (int y = 0; y < height; y++) pixels[y * width + x] = color;
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    public static Color SampleStops(IList<(float t, Color color)> stops, float t)
    {
        if (stops == null || stops.Count == 0) return Color.magenta;
        if (t <= stops[0].t) return stops[0].color;
        for (int i = 1; i < stops.Count; i++)
        {
            if (t > stops[i].t) continue;
            float span = stops[i].t - stops[i - 1].t;
            float k = span <= 0f ? 0f : (t - stops[i - 1].t) / span;
            return Color.Lerp(stops[i - 1].color, stops[i].color, k);
        }

        return stops[stops.Count - 1].color;
    }

    public static Sprite CreateSprite(Texture2D texture, string name)
    {
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        return sprite;
    }

    public static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    /// <summary>实底面板（Image 默认不参与射线，需要交互时显式打开）。</summary>
    public static Image Panel(Transform parent, string name, Color color, bool raycastTarget = false)
    {
        RectTransform rect = CreateRect(parent, name);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    public static Text Text(Transform parent, string name, string value, int fontSize, Color color,
        TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
    {
        RectTransform rect = CreateRect(parent, name);
        Text text = rect.gameObject.AddComponent<Text>();
        text.text = value;
        text.font = Font;
        text.fontSize = Mathf.Max(1, fontSize);
        text.fontStyle = style;
        text.color = color;
        text.alignment = anchor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    /// <summary>大写英文微标签（模拟 CSS letter-spacing：逐字符插空格）。</summary>
    public static string Spaced(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return string.Join(" ", value.ToUpperInvariant().Replace(" ", "  ").ToCharArray()).Replace("   ", "  ");
    }

    public static void Place(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    public static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>圆钮（圆形底 + 可选描边），返回 Button。</summary>
    public static Button CircleButton(Transform parent, string name, float diameter, Color backColor, Color borderColor)
    {
        Image back = Panel(parent, name, backColor);
        back.sprite = Circle(Mathf.CeilToInt(diameter));
        back.raycastTarget = true;
        Button button = back.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.16f, 1.16f, 1.16f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        button.colors = colors;
        if (borderColor.a > 0f) ObsidianArenaTheme.EnsureOutline(back, borderColor, new Vector2(1f, -1f));
        return button;
    }

    private static Sprite CacheSprite(string key, Color[] pixels, int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = key,
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = key;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        SpriteCache[key] = sprite;
        return sprite;
    }
}
