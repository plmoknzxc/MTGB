using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    private CardDefinition previewFaceCard;
    private readonly List<int> previewFaceIndexes = new List<int>();
    private RectTransform previewPanelRect, previewFrontRect, previewBackRect, previewFrontPlate, previewBackPlate, previewDetailsRect;
    private Image previewBackImage;
    private Text previewMissingLabel;
    private float previewLayoutWidth = -1f;
    private bool previewLayoutDual;

    private void CreateCardPreviewChrome()
    {
        if (cardPreviewImage == null || cardPreviewGroup == null) return;
        Transform panel = cardPreviewGroup.transform;
        previewPanelRect = panel as RectTransform;
        previewFrontRect = cardPreviewImage.rectTransform;
        previewDetailsRect = panel.Find("CardInfoScroll") as RectTransform;
        previewFrontPlate = CreatePreviewPlate(panel, "CardPreviewPlate");
        previewFrontPlate.SetSiblingIndex(previewFrontRect.GetSiblingIndex());
        previewBackPlate = CreatePreviewPlate(panel, "CardPreviewBackPlate");
        previewBackPlate.SetSiblingIndex(previewFrontRect.GetSiblingIndex());

        var back = new GameObject("CardPreviewBackImage", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(panel, false);
        previewBackRect = (RectTransform)back.transform;
        previewBackRect.SetSiblingIndex(previewFrontRect.GetSiblingIndex() + 1);
        previewBackImage = back.GetComponent<Image>();
        previewBackImage.preserveAspect = true;
        previewBackImage.raycastTarget = false;

        var missing = new GameObject("MissingFaceImage", typeof(RectTransform), typeof(Text));
        missing.transform.SetParent(panel, false);
        previewMissingLabel = missing.GetComponent<Text>();
        previewMissingLabel.font = cardInfoText.font;
        previewMissingLabel.fontSize = 15;
        previewMissingLabel.color = new Color(.73f, .79f, .84f);
        previewMissingLabel.alignment = TextAnchor.MiddleCenter;
        previewMissingLabel.raycastTarget = false;
        missing.SetActive(false);
        previewBackRect.gameObject.SetActive(false);
        previewBackPlate.gameObject.SetActive(false);
        RefreshPreviewLayout();
    }

    private static RectTransform CreatePreviewPlate(Transform panel, string name)
    {
        var plate = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
        plate.transform.SetParent(panel, false);
        var rect = (RectTransform)plate.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
        rect.pivot = new Vector2(.5f, 1);
        var image = plate.GetComponent<Image>();
        image.sprite = IntegratedUiUtility.RoundedRect(303, 414, 9);
        image.color = new Color(.025f, .04f, .06f);
        image.raycastTarget = false;
        var outline = plate.GetComponent<Outline>();
        outline.effectColor = new Color(.48f, .54f, .59f, .85f);
        outline.effectDistance = new Vector2(1, -1);
        return rect;
    }

    private void RefreshAvailablePreviewFaces(CardDefinition card)
    {
        previewFaceIndexes.Clear();
        if (card == null) return;
        previewFaceIndexes.Add(0);
        if (card.features != null)
            foreach (CardFeatureDefinition feature in card.features)
                if (feature != null && feature.faceIndex > 0 && !previewFaceIndexes.Contains(feature.faceIndex))
                    previewFaceIndexes.Add(feature.faceIndex);
        if (CardImageLibrary.HasBackFace(card) && !previewFaceIndexes.Contains(1)) previewFaceIndexes.Add(1);
        previewFaceIndexes.Sort();
    }

    private void RefreshPreviewImages(CardDefinition card)
    {
        if (cardPreviewImage == null) return;
        Sprite front = card == null ? null : LoadCardSprite(card);
        cardPreviewImage.sprite = front;
        cardPreviewImage.color = front == null ? GetFallbackColor(card) : Color.white;
        cardPreviewImage.preserveAspect = true;
        bool dual = card != null && previewFaceIndexes.Count > 1;
        previewBackRect.gameObject.SetActive(dual);
        previewBackPlate.gameObject.SetActive(dual);
        Sprite back = dual && CardImageLibrary.TryLoadFullCard(card, previewFaceIndexes[1], out Sprite faceSprite)
            ? faceSprite : null;
        previewBackImage.sprite = back;
        previewBackImage.color = back == null ? new Color(.10f, .14f, .18f) : Color.white;
        previewMissingLabel.gameObject.SetActive(dual && back == null);
        if (dual && back == null)
            previewMissingLabel.text = GamePreferences.Language.StartsWith("zh") ? "这一面暂无卡图"
                : GamePreferences.Language.StartsWith("ja") ? "この面の画像は未取得" : "No image for this face";
        previewLayoutWidth = -1f;
        RefreshPreviewLayout();
    }

    private void LateUpdate()
    {
        UpdateDeckCollapseLayout();
        if (previewPanelRect != null && Mathf.Abs(previewPanelRect.rect.width - previewLayoutWidth) > .5f)
            RefreshPreviewLayout();
    }

    private void RefreshPreviewLayout()
    {
        if (previewPanelRect == null || previewFrontRect == null) return;
        float panelWidth = previewPanelRect.rect.width;
        if (panelWidth <= 0) return;
        bool dual = previewFaceCard != null && previewFaceIndexes.Count > 1;
        if (Mathf.Abs(panelWidth - previewLayoutWidth) < .5f && dual == previewLayoutDual) return;
        previewLayoutWidth = panelWidth;
        previewLayoutDual = dual;
        float width = dual ? Mathf.Min(260f, Mathf.Max(1f, (panelWidth - 48f) * .5f)) : Mathf.Min(287f, panelWidth - 36f);
        float height = width * 402f / 287f;
        float centerOffset = dual ? (width + 12f) * .5f : 0f;
        PlacePreviewRect(previewFrontRect, -centerOffset, -18f, width, height);
        PlacePreviewRect(previewFrontPlate, -centerOffset, -12f, width + 12f, height + 12f);
        PlacePreviewRect(previewBackRect, centerOffset, -18f, width, height);
        PlacePreviewRect(previewBackPlate, centerOffset, -12f, width + 12f, height + 12f);
        var missing = previewMissingLabel.rectTransform;
        PlacePreviewRect(missing, centerOffset, -18f - height * .5f + 35f, width - 12f, 70f);
        if (previewDetailsRect != null)
            previewDetailsRect.offsetMax = new Vector2(-12f, -height - 30f);
    }

    private static void PlacePreviewRect(RectTransform rect, float x, float y, float width, float height)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
