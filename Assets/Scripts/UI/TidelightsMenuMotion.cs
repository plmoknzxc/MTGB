using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TidelightsMenuMotion : MonoBehaviour
{
    MenuThemeStyle theme;
    RawImage backdrop;
    Texture still;
    TidelightsClip clip;
    TidelightsPortraitMotion portrait;
    TidelightsRainGraphic rain;
    RectTransform effects;
    double started;
    long lastTick;

    internal static bool SupportsArtwork(MenuThemeStyle style) => style.Motion == "tidelights";

    internal static void Apply(RawImage artwork, MenuThemeStyle style)
    {
        if (!Application.IsPlaying(artwork.gameObject)) return;
        var motion = artwork.GetComponent<TidelightsMenuMotion>();
        if (motion != null && motion.theme != style) motion.enabled = false;
        if (!GamePreferences.MenuMotionEnabled || !SupportsArtwork(style) || artwork.texture == null)
        {
            if (motion != null) motion.enabled = false;
            return;
        }
        if (motion == null) motion = artwork.gameObject.AddComponent<TidelightsMenuMotion>();
        motion.theme = style;
        motion.backdrop = artwork;
        if (motion.clip == null || artwork.texture != motion.clip.Scenery) motion.still = artwork.texture;
        motion.enabled = true;
        if (motion.isActiveAndEnabled)
        {
            motion.StartMotion();
            if (motion.clip != null) artwork.texture = motion.clip.Scenery;
        }
    }

    static RectTransform Fill(Transform parent, string name)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    void OnEnable() => StartMotion();

    void StartMotion()
    {
        if (backdrop == null || clip != null) return;
        clip = TidelightsClip.Acquire(theme);
        if (clip == null) return;
        backdrop.texture = clip.Scenery;
        effects = Fill(transform, "Parasol weather");
        effects.gameObject.SetActive(false);
        // Keep the extra vertex channels and weather updates inside this backdrop.
        var canvas = effects.gameObject.AddComponent<Canvas>();
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3;
        rain = Fill(effects, "Rain and ripples").gameObject.AddComponent<TidelightsRainGraphic>();
        rain.raycastTarget = false;
        rain.Bind(clip);
        portrait = Fill(effects, "Emperor and parasol").gameObject.AddComponent<TidelightsPortraitMotion>();
        portrait.Bind(clip);
        started = Time.unscaledTimeAsDouble;
        lastTick = -1;
        Update();
        effects.gameObject.SetActive(true);
    }

    void Update()
    {
        if (clip == null) return;
        long tick = (long)((Time.unscaledTimeAsDouble - started) * clip.Fps);
        if (tick == lastTick) return;
        lastTick = tick;
        portrait.SetFrame((int)(tick % clip.Body.Length));
        rain.SetTime((float)((double)tick / clip.Fps));
    }

    void OnDisable()
    {
        if (clip == null) return;
        if (backdrop != null && backdrop.texture == clip.Scenery) backdrop.texture = still;
        effects.gameObject.SetActive(false);
        Destroy(effects.gameObject);
        effects = null;
        portrait = null;
        rain = null;
        clip.Release();
        clip = null;
    }
}
