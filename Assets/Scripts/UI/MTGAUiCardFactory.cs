using UnityEngine;
using UnityEngine.UI;

public static class MTGAUiCardFactory
{
    public static MTGAHandUiCard CreateCard(Transform parent, string name, Font font, int owner, int index, Vector2 size)
    {
        RectTransform card = CreateRect(parent, name);
        card.sizeDelta = size;

        MTGAHandUiCard handCard = card.gameObject.AddComponent<MTGAHandUiCard>();
        handCard.Configure(owner, index);

        Image glow = AddImage(card, "SelectedGlow", new Color(0f, 0.88f, 1f, 0f));
        RectTransform glowRect = glow.rectTransform;
        glowRect.anchorMin = new Vector2(0.5f, 0.5f);
        glowRect.anchorMax = new Vector2(0.5f, 0.5f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);
        glowRect.anchoredPosition = Vector2.zero;
        glowRect.sizeDelta = size + new Vector2(10f, 10f);

        Image frame = card.gameObject.AddComponent<Image>();
        frame.color = new Color(0.025f, 0.026f, 0.030f, 1f);
        // The root frame is the card's single pointer hit surface. Child graphics stay
        // non-interactive so dynamically-created hand cards receive clicks reliably.
        frame.raycastTarget = true;

        Outline outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.clear;
        outline.effectDistance = new Vector2(1f, 1f);

        Image face = AddImage(card, "Face", new Color(0.74f, 0.78f, 0.72f, 1f));
        Stretch(face.rectTransform, 9f, 9f, 9f, 9f);

        Image title = AddImage(card, "TitleBand", new Color(0.82f, 0.80f, 0.70f, 1f));
        Place(title.rectTransform, new Vector2(0f, size.y * 0.40f), new Vector2(size.x - 28f, size.y * 0.10f));

        Image art = AddImage(card, "Art", new Color(0.08f, 0.30f, 0.48f, 1f));
        Place(art.rectTransform, new Vector2(0f, size.y * 0.16f), new Vector2(size.x - 34f, size.y * 0.40f));

        Image rules = AddImage(card, "RulesBox", new Color(0.72f, 0.73f, 0.66f, 1f));
        Place(rules.rectTransform, new Vector2(0f, -size.y * 0.23f), new Vector2(size.x - 34f, size.y * 0.27f));

        Text titleText = AddText(title.transform, "Text", string.Empty, font, 16, Color.black, TextAnchor.MiddleLeft);
        Stretch(titleText.rectTransform, 8f, 8f, 0f, 0f);

        Text typeText = AddText(rules.transform, "Text", string.Empty, font, 14, new Color(0.08f, 0.08f, 0.08f), TextAnchor.UpperLeft);
        Stretch(typeText.rectTransform, 8f, 8f, 6f, 6f);

        Text pip = AddText(card, "CostPip", string.Empty, font, 18, Color.white, TextAnchor.MiddleCenter);
        Place(pip.rectTransform, new Vector2(size.x * 0.38f, size.y * 0.41f), new Vector2(size.x * 0.13f, size.x * 0.13f));

        Image pipBg = AddImage(card, "CostPipBg", new Color(0.05f, 0.07f, 0.10f, 0f));
        Place(pipBg.rectTransform, new Vector2(size.x * 0.38f, size.y * 0.41f), new Vector2(size.x * 0.14f, size.x * 0.14f));
        pip.transform.SetAsLastSibling();

        return handCard;
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    private static Image AddImage(Transform parent, string name, Color color)
    {
        RectTransform rect = CreateRect(parent, name);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text AddText(Transform parent, string name, string value, Font font, int size, Color color, TextAnchor alignment)
    {
        RectTransform rect = CreateRect(parent, name);
        Text text = rect.gameObject.AddComponent<Text>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
