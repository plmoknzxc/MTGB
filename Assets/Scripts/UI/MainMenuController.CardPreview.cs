using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    private CardDefinition previewFaceCard;
    private readonly List<int> previewFaceIndexes = new List<int>();
    private int previewFacePosition;
    private int CurrentPreviewFace => previewFaceIndexes.Count == 0 ? 0 : previewFaceIndexes[previewFacePosition];
    private RectTransform previewPanelRect, previewDetailsRect;
    private RectTransform previewMat, previewGallery;
    internal const float InspectorToolSize=36, InspectorToolGap=6, InspectorToolInset=10;
    private Button previewFlipButton;
    private Image previewFlipIcon;
    private Text previewMissingLabel, previewHeading;
    private Coroutine previewFlipAnimation;
    private Vector2 previewLayoutSize;
    private bool previewHadSwitch, previewWasGallery;
    private int previewRevision = -1;

    private void CreateCardPreviewChrome()
    {
        if (cardPreviewImage == null || cardPreviewGroup == null) return;
        previewPanelRect = (RectTransform)cardPreviewGroup.transform;
        previewDetailsRect = previewPanelRect.Find("CardInfoScroll") as RectTransform;
        previewHeading=AddText(previewPanelRect,"CardDetailsHeading","卡牌详情",21,MenuVisualTheme.ArchivePaper,TextAnchor.MiddleLeft);
        MenuTypography.Use(previewHeading,MenuTypography.Face.Heading);
        previewMat=MenuVisualTheme.Detail(previewPanelRect,"PortraitMat",MenuVisualTheme.Recess).rectTransform;
        previewMat.SetAsFirstSibling();
        previewMat.gameObject.AddComponent<RectMask2D>();
        MenuVisualTheme.Background(previewMat,MenuThemeArtwork.Role.Hall,.11f);
        previewGallery=(RectTransform)new GameObject("DeckEditorArtContent",typeof(RectTransform)).transform;
        previewGallery.SetParent(previewPanelRect,false);
        previewGallery.anchorMin=Vector2.zero;previewGallery.anchorMax=Vector2.one;
        previewGallery.gameObject.SetActive(false);
        cardPreviewImage.preserveAspect = true;
        cardPreviewImage.raycastTarget = false;

        previewMissingLabel = AddText(previewPanelRect,"MissingFaceImage","",17,MenuVisualTheme.Muted,TextAnchor.MiddleCenter);
        previewMissingLabel.gameObject.SetActive(false);
        var flip = ReplayWidgets.Panel(previewPanelRect, "FlipPreviewFace", 0, 0, 46, 40, MenuVisualTheme.Slate);
        previewFlipButton = flip.gameObject.AddComponent<Button>();
        MenuVisualTheme.StyleArchiveControl(previewFlipButton);
        previewFlipButton.onClick.AddListener(CyclePreviewFace);
        previewFlipIcon = MenuVisualTheme.Detail(flip, "FlipIcon", MenuVisualTheme.Silver);
        previewFlipIcon.sprite = Resources.Load<Sprite>("Menu/FlipCard");
        previewFlipIcon.preserveAspect = true;
        previewFlipIcon.rectTransform.sizeDelta = new Vector2(34, 34);
        previewFlipButton.gameObject.SetActive(false);
        RefreshPreviewLayout();
    }

    private void RefreshAvailablePreviewFaces(CardDefinition card)
    {
        int previous = CurrentPreviewFace;
        previewFaceIndexes.Clear();
        previewFacePosition = 0;
        if (card == null) return;
        previewFaceIndexes.Add(0);
        if (card.features != null)
            foreach (var feature in card.features)
                if (feature != null && feature.faceIndex > 0 && !previewFaceIndexes.Contains(feature.faceIndex))
                    previewFaceIndexes.Add(feature.faceIndex);
        if (!previewFaceIndexes.Contains(1) && (CardImageLibrary.HasBackFace(card) || card.forgeSplitType is "Modal" or "Transform"))
            previewFaceIndexes.Add(1);
        previewFaceIndexes.Sort();
        previewFacePosition = Mathf.Max(0,previewFaceIndexes.IndexOf(previous));
    }

    private void CyclePreviewFace()
    {
        if (previewFaceCard == null || previewFaceIndexes.Count < 2 || previewFlipAnimation != null) return;
        previewFlipAnimation = StartCoroutine(FlipPreview());
    }

    private IEnumerator FlipPreview()
    {
        yield return TurnPreview(0, 88);
        if (previewFaceIndexes.Count > 1)
        {
            previewFacePosition = (previewFacePosition + 1) % previewFaceIndexes.Count;
            RenderCardDetail(previewFaceCard);
        }
        yield return TurnPreview(-88, 0);
        previewFlipAnimation = null;
    }

    private IEnumerator TurnPreview(float start, float end)
    {
        const float halfDuration = .12f;
        float began = Time.unscaledTime;
        for (float t = 0; t < 1; t = (Time.unscaledTime - began) / halfDuration)
        {
            float angle = Mathf.Lerp(start, end, Mathf.SmoothStep(0, 1, t));
            SetPreviewRotation(angle);
            yield return null;
        }
        SetPreviewRotation(end);
    }

    private void SetPreviewRotation(float angle)
    {
        var rotation = Quaternion.Euler(0, angle, 0);
        cardPreviewImage.rectTransform.localRotation = rotation;
        previewMissingLabel.rectTransform.localRotation = rotation;
    }

    private void CancelPreviewFlip()
    {
        if (previewFlipAnimation == null) return;
        StopCoroutine(previewFlipAnimation);
        previewFlipAnimation = null;
        SetPreviewRotation(0);
    }

    private void OnDisable() => CancelPreviewFlip();

    private void RefreshPreviewImages(CardDefinition card)
    {
        if (cardPreviewImage == null || previewFlipButton == null) return;
        int index = CurrentPreviewFace;
        Sprite art = null;
        if (card != null)
        {
            if (index == 0) art = LoadCardSprite(card);
            else CardImageLibrary.TryLoadFullCard(card,index,out art);
        }
        cardPreviewImage.sprite = art;
        cardPreviewImage.enabled = art != null;
        cardPreviewImage.color = Color.white;
        bool missing = card != null && art == null;
        previewMissingLabel.gameObject.SetActive(missing && !previewGallery.gameObject.activeSelf);
        if (missing) previewMissingLabel.text = DeckCompanion.Text("此面暂无卡图","No artwork for this face","この面の画像は未取得");
        previewFlipIcon.rectTransform.localScale = index == 0 ? Vector3.one : new Vector3(-1, 1, 1);
        RefreshPreviewLayout();
    }

    private void LateUpdate()
    {
        if (previewFlipAnimation != null && !IsDeckBuilderVisible()) CancelPreviewFlip();
        UpdateDeckCollapseLayout();
        RefreshPreviewLayout();
    }

    private void RefreshPreviewLayout()
    {
        if (previewPanelRect == null || previewFlipButton == null) return;
        Vector2 panel = previewPanelRect.rect.size;
        bool gallery=previewGallery.gameObject.activeSelf;
        bool canFlip=previewFaceIndexes.Count>1;
        if (panel == previewLayoutSize && canFlip == previewHadSwitch && gallery==previewWasGallery && previewRevision==GamePreferences.Revision) return;
        previewLayoutSize = panel;
        previewHadSwitch = canFlip;
        previewWasGallery=gallery;
        previewMat.gameObject.SetActive(!gallery);
        cardPreviewImage.gameObject.SetActive(!gallery);
        previewFlipButton.gameObject.SetActive(!gallery && canFlip);
        previewMissingLabel.gameObject.SetActive(!gallery && previewFaceCard!=null && cardPreviewImage.sprite==null);
        previewDetailsRect.gameObject.SetActive(!gallery);
        previewRevision=GamePreferences.Revision;
        previewHeading.text = DeckCompanion.Text("卡牌详情","Card details","カード詳細");
        float width = Mathf.Max(1,Mathf.Min(242,panel.x-32,panel.y*.355f/1.4f));
        float height = width*1.4f;
        void AtTop(RectTransform rect,float y,float w,float h)
        {
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);
            rect.pivot=new Vector2(.5f,1);
            rect.anchoredPosition=new Vector2(0,-y);
            rect.sizeDelta=new Vector2(w,h);
        }
        float headerHeight=2*InspectorToolInset+InspectorToolSize;
        float collapseSlot=InspectorToolSize+InspectorToolGap;
        AtTop(previewHeading.rectTransform,InspectorToolInset,panel.x-2*InspectorToolInset-collapseSlot,InspectorToolSize);
        previewHeading.rectTransform.anchoredPosition+=Vector2.left*(collapseSlot*.5f);
        previewGallery.offsetMin=new Vector2(3,4);
        previewGallery.offsetMax=new Vector2(-3,-headerHeight);
        float artTop=headerHeight+12;
        AtTop(cardPreviewImage.rectTransform,artTop,width,height);
        AtTop(previewMissingLabel.rectTransform,artTop,width,height);
        float bottom = artTop+height;
        AtTop(previewMat,headerHeight,panel.x-2,height+24);
        if (canFlip)
        {
            var flip = (RectTransform)previewFlipButton.transform;
            AtTop(flip, bottom + 15, flip.sizeDelta.x, flip.sizeDelta.y);
            bottom += 15 + flip.sizeDelta.y;
        }
        previewDetailsRect.offsetMin = new Vector2(7,8);
        previewDetailsRect.offsetMax = new Vector2(-5,-bottom-18);
    }
}
