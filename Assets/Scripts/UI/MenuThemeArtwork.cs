using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RawImage))]
public sealed class MenuThemeArtwork : MonoBehaviour
{
    internal enum Role { Start, Hall, Deck, Replay }

    [SerializeField] Role role;

    internal static void Bind(RawImage image, Role artworkRole)
    {
        var artwork = image.GetComponent<MenuThemeArtwork>() ?? image.gameObject.AddComponent<MenuThemeArtwork>();
        artwork.role = artworkRole;
        artwork.Apply();
    }

    internal static void RefreshAll()
    {
        foreach (var artwork in FindObjectsByType<MenuThemeArtwork>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            artwork.Apply();
    }

    internal static void RefreshStartMotion()
    {
        foreach (var artwork in FindObjectsByType<MenuThemeArtwork>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (artwork.role == Role.Start)
                TidelightsMenuMotion.Apply(artwork.GetComponent<RawImage>(), MenuThemeCatalog.Current);
    }

    void Apply()
    {
        var theme = MenuThemeCatalog.Current;
        var path = role switch
        {
            Role.Deck => theme.DeckBackdrop,
            Role.Replay => theme.ReplayBackdrop,
            _ => theme.Hall
        };
        var image = GetComponent<RawImage>();
        image.texture = theme.Load<Texture2D>(path);
        if (role == Role.Deck)
            image.material = theme.Load<Material>(theme.DeckMaterial);
        if (TryGetComponent<AspectRatioFitter>(out var fit))
            fit.aspectRatio = image.texture != null ? (float)image.texture.width / image.texture.height : 16f / 9;
        if (role == Role.Start)
            TidelightsMenuMotion.Apply(image, theme);
    }
}
