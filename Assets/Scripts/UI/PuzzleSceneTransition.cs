using UnityEngine;
using UnityEngine.UI;

// Keep a themed frame over scene activation until the destination has real content.
internal static class PuzzleSceneTransition
{
    internal enum Destination { Menu, Battlefield }
    private static Canvas cover;
    private static Text caption;
    private static Destination destination;

    internal static bool Pending(Destination page) => cover != null && destination == page;

    internal static void Show(Destination page, string message)
    {
        destination = page;
        if (cover == null)
        {
            cover = ReplayWidgets.Canvas("PuzzleSceneTransition", 2000);
            Object.DontDestroyOnLoad(cover.gameObject);
            var surface = ReplayWidgets.Panel(cover.transform, "Surface", 0, 0, 0, 0, MenuVisualTheme.Ink);
            surface.anchorMin = Vector2.zero;
            surface.anchorMax = Vector2.one;
            surface.offsetMin = surface.offsetMax = Vector2.zero;
            var art = ReplayWidgets.Rect(surface, "Table", 0, 0, 0, 0).gameObject.AddComponent<RawImage>();
            var theme = MenuThemeCatalog.Current;
            art.texture = theme.Load<Texture2D>(theme.BattlefieldBackdrop);
            art.enabled = art.texture != null;
            art.color = new Color(.72f,.74f,.78f);
            art.raycastTarget = false;
            var fit = art.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = art.texture == null ? 16f/9 : (float)art.texture.width/art.texture.height;
            caption = ReplayWidgets.Label(surface, "", 0, 0, 640, 64, 28, TextAnchor.MiddleCenter);
            MenuTypography.Use(caption, MenuTypography.Face.Control);
        }
        caption.text = message;
    }

    internal static void Ready(Destination page)
    {
        if (!Pending(page)) return;
        cover.gameObject.SetActive(false);
        Object.Destroy(cover.gameObject);
        cover = null;
        caption = null;
    }
}
