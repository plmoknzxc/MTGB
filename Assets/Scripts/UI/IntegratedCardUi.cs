using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 屏幕空间卡牌视觉（手牌/检视共用基础）：真实卡图 → 卡背 → 生成模板卡三态。
/// 模板卡结构对齐 HTML .gen：名牌(+法力费)/色框画窗(+菱形符号)/类型行/规则文本行/攻防角标。
/// 所有子元素按比例布局，随 RectTransform 尺寸缩放。
/// </summary>
public class IntegratedCardUi : MonoBehaviour
{
    private Image rootImage;
    private Image faceImage;         // 真实卡图/卡背
    private GameObject templateRoot;
    private Text nameText;
    private Text costText;
    private Text typeText;
    private Text ptText;
    private Image hoverOutline;
    private Outline outline;
    private Vector2 size;

    public RectTransform Rect { get; private set; }

    public RuntimeCard Card { get; private set; }

    public static IntegratedCardUi Create(Transform parent, string objectName, Vector2 cardSize)
    {
        IntegratedCardUi card = new GameObject(objectName, typeof(RectTransform)).AddComponent<IntegratedCardUi>();
        card.Rect = card.GetComponent<RectTransform>();
        card.Rect.SetParent(parent, false);
        card.size = cardSize;
        card.Build();
        return card;
    }

    private void Build()
    {
        Rect.sizeDelta = size;
        Rect.pivot = new Vector2(0.5f, 0.5f);

        // 根 Image = 唯一射线命中面（模板卡的深色卡底）
        rootImage = gameObject.AddComponent<Image>();
        rootImage.sprite = IntegratedUiUtility.RoundedRect(size.x, size.y, 7f * (size.x / 124f), $"cardRoot{Mathf.RoundToInt(size.x)}x{Mathf.RoundToInt(size.y)}");
        rootImage.color = IntegratedArenaPalette.GenBack;
        rootImage.raycastTarget = true;
        outline = gameObject.AddComponent<Outline>();
        outline.effectColor = Color.clear;
        outline.effectDistance = new Vector2(1.4f, -1.4f);

        faceImage = new GameObject("Face", typeof(RectTransform)).AddComponent<Image>();
        IntegratedUiUtility.Stretch(faceImage.rectTransform, 0f, 0f, 0f, 0f);
        faceImage.raycastTarget = false;
        faceImage.preserveAspect = false;
        faceImage.sprite = IntegratedUiUtility.RoundedRect(size.x, size.y, 6f * (size.x / 124f), $"cardFace{Mathf.RoundToInt(size.x)}x{Mathf.RoundToInt(size.y)}");

        templateRoot = new GameObject("Template", typeof(RectTransform)).transform.gameObject;
        RectTransform templateRect = templateRoot.GetComponent<RectTransform>();
        templateRect.SetParent(transform, false);
        IntegratedUiUtility.Stretch(templateRect, 0f, 0f, 0f, 0f);

        // 名牌带
        Image nameBand = AddTemplateImage(templateRect.transform, "NameBand", IntegratedArenaPalette.GenNameBand);
        nameBand.rectTransform.anchorMin = new Vector2(0f, 1f);
        nameBand.rectTransform.anchorMax = new Vector2(1f, 1f);
        nameBand.rectTransform.pivot = new Vector2(0.5f, 1f);
        nameBand.rectTransform.anchoredPosition = new Vector2(0f, -size.y * 0.023f);
        nameBand.rectTransform.sizeDelta = new Vector2(-size.x * 0.05f, size.y * 0.075f);
        ObsidianArenaTheme.EnsureOutline(nameBand, IntegratedArenaPalette.LineStrong, new Vector2(0f, -0.6f));

        nameText = IntegratedUiUtility.Text(nameBand.transform, "Name", string.Empty, ScaleFont(9.5f),
            IntegratedArenaPalette.GenNameText, TextAnchor.MiddleLeft);
        IntegratedUiUtility.Stretch(nameText.rectTransform, 6f, 1f, 30f, 1f);

        costText = IntegratedUiUtility.Text(nameBand.transform, "Cost", string.Empty, ScaleFont(8f),
            IntegratedArenaPalette.GenCostText, TextAnchor.MiddleRight);
        IntegratedUiUtility.Stretch(costText.rectTransform, 0f, 1f, 5f, 1f);

        // 画窗（色框 + 菱形符号）
        Image art = AddTemplateImage(templateRect.transform, "Art", IntegratedArenaPalette.GenUArtA);
        art.rectTransform.anchorMin = new Vector2(0f, 1f);
        art.rectTransform.anchorMax = new Vector2(1f, 1f);
        art.rectTransform.pivot = new Vector2(0.5f, 1f);
        art.rectTransform.anchoredPosition = new Vector2(0f, -size.y * 0.115f);
        art.rectTransform.sizeDelta = new Vector2(-size.x * 0.11f, -size.y * 0.115f - size.y * 0.40f);
        ObsidianArenaTheme.EnsureOutline(art, IntegratedArenaPalette.GenArtBorder, new Vector2(0.8f, 0.8f));

        Image artMark = AddTemplateImage(art.transform, "Mark", IntegratedArenaPalette.GenUArtMark);
        artMark.sprite = DiamondSprite();
        float mark = Mathf.Min(size.x, size.y) * 0.11f;
        artMark.rectTransform.sizeDelta = new Vector2(mark * 0.78f, mark);

        // 类型行
        typeText = IntegratedUiUtility.Text(templateRoot.transform, "TypeLine", string.Empty, ScaleFont(7f),
            IntegratedArenaPalette.GenTypeText, TextAnchor.LowerLeft);
        typeText.rectTransform.anchorMin = new Vector2(0f, 1f);
        typeText.rectTransform.anchorMax = new Vector2(1f, 1f);
        typeText.rectTransform.pivot = new Vector2(0f, 0f);
        typeText.rectTransform.anchoredPosition = new Vector2(size.x * 0.06f, -size.y * 0.585f);
        typeText.rectTransform.sizeDelta = new Vector2(-size.x * 0.12f, size.y * 0.065f);

        // 规则文本骨架线
        float[] lineWidths = { 0.60f, 0.47f, 0.34f };
        for (int i = 0; i < lineWidths.Length; i++)
        {
            Image line = AddTemplateImage(templateRoot.transform, $"RuleLine{i + 1}", IntegratedArenaPalette.GenRuleLine);
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(0f, 1f);
            line.rectTransform.pivot = new Vector2(0f, 0f);
            line.rectTransform.anchoredPosition = new Vector2(size.x * 0.06f, -size.y * (0.685f + i * 0.045f));
            line.rectTransform.sizeDelta = new Vector2(size.x * lineWidths[i], Mathf.Max(2f, size.y * 0.017f));
        }

        // 攻防角标
        Image pt = AddTemplateImage(templateRoot.transform, "PtBadge", IntegratedArenaPalette.GenPtBack);
        pt.rectTransform.anchorMin = new Vector2(1f, 0f);
        pt.rectTransform.anchorMax = new Vector2(1f, 0f);
        pt.rectTransform.pivot = new Vector2(1f, 0f);
        pt.rectTransform.anchoredPosition = new Vector2(-size.x * 0.04f, size.y * 0.03f);
        pt.rectTransform.sizeDelta = new Vector2(size.x * 0.24f, size.y * 0.062f);
        ObsidianArenaTheme.EnsureOutline(pt, IntegratedArenaPalette.GenPtBorder, new Vector2(0.8f, -0.8f));
        ptText = IntegratedUiUtility.Text(pt.transform, "PT", string.Empty, ScaleFont(8.5f),
            IntegratedArenaPalette.Text, TextAnchor.MiddleCenter);
        IntegratedUiUtility.Stretch(ptText.rectTransform, 1f, 0f, 1f, 0f);

        // 真实卡图模式的细边框
        hoverOutline = new GameObject("HoverOutline", typeof(RectTransform)).AddComponent<Image>();
        hoverOutline.rectTransform.SetParent(transform, false);
        hoverOutline.rectTransform.anchorMin = Vector2.zero;
        hoverOutline.rectTransform.anchorMax = Vector2.one;
        hoverOutline.rectTransform.offsetMin = new Vector2(-4f, -4f);
        hoverOutline.rectTransform.offsetMax = new Vector2(4f, 4f);
        hoverOutline.sprite = IntegratedUiUtility.RoundedRectOutline(size.x + 8f, size.y + 8f, 9f, 2.4f,
            $"hover{Mathf.RoundToInt(size.x)}x{Mathf.RoundToInt(size.y)}");
        hoverOutline.color = new Color(IntegratedArenaPalette.Blue.r, IntegratedArenaPalette.Blue.g,
            IntegratedArenaPalette.Blue.b, 0.9f);
        hoverOutline.raycastTarget = false;
        hoverOutline.gameObject.SetActive(false);
    }

    private Image AddTemplateImage(Transform parent, string name, Color color)
    {
        Image image = IntegratedUiUtility.Panel(parent, name, color);
        return image;
    }

    private int ScaleFont(float cssPx)
    {
        return Mathf.Max(6, Mathf.RoundToInt(cssPx * (size.x / 124f)));
    }

    private static Sprite diamondSprite;

    private static Sprite DiamondSprite()
    {
        if (diamondSprite != null) return diamondSprite;
        const int size = 32;
        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = (Mathf.Abs(x - center) + Mathf.Abs(y - center)) / center;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1.05f - distance));
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Diamond",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        diamondSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        diamondSprite.hideFlags = HideFlags.HideAndDontSave;
        return diamondSprite;
    }

    /// <summary>设置卡面内容。faceDown = 显示卡背（对手手牌）。</summary>
    public void SetCard(RuntimeCard runtimeCard, bool faceDown)
    {
        Card = runtimeCard;
        CardDefinition data = runtimeCard?.Data;
        Sprite loadedSprite = null;
        if (faceDown) CardImageLibrary.TryLoadCardBack(out loadedSprite);
        else if (data != null) CardImageLibrary.TryLoadFullCard(data, out loadedSprite);

        bool hasImage = loadedSprite != null;
        faceImage.gameObject.SetActive(hasImage);
        if (hasImage)
        {
            faceImage.sprite = loadedSprite;
            faceImage.color = faceDown
                ? new Color(0.47f, 0.44f, 0.50f, 1f) // 对手卡背压暗 + 降饱和近似
                : Color.white;
        }

        templateRoot.SetActive(!hasImage);
        if (!hasImage && data != null) ApplyTemplateContent(data);
    }

    private void ApplyTemplateContent(CardDefinition data)
    {
        ManaColor primary = CardVisualStyle.GetPrimaryColor(data);
        TemplateCardTexture.GetColorSchemePublic(primary,
            out Color bandColor, out Color artA, out _, out Color artMark);

        ManaCost cost = data.GetManaCost();
        nameText.text = data.cardName ?? string.Empty;
        costText.text = cost.IsEmpty ? string.Empty : data.manaCost;
        typeText.text = data.GetTypeLine();
        bool hasPt = data.cardTypes.Has(CardType.Creature);
        ptText.text = hasPt ? $"{data.attack}/{data.defence}" : string.Empty;
        ptText.transform.parent.gameObject.SetActive(hasPt);

        Transform band = nameText.transform.parent;
        if (band.TryGetComponent(out Image bandImage)) bandImage.color = bandColor;
        Transform art = templateRoot.transform.Find("Art");
        if (art != null && art.TryGetComponent(out Image artImage))
        {
            artImage.color = artA;
            Transform mark = art.Find("Mark");
            if (mark != null && mark.TryGetComponent(out Image markImage)) markImage.color = artMark;
        }
    }

    public void SetHoverVisual(bool hovered)
    {
        if (hoverOutline != null) hoverOutline.gameObject.SetActive(hovered);
        if (outline != null)
        {
            outline.effectColor = hovered
                ? IntegratedArenaPalette.Blue
                : new Color(0f, 0f, 0f, 0.4f);
        }
    }
}
