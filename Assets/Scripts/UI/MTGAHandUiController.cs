using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MTGAHandUiController : MonoBehaviour
{
    [SerializeField] private GameRules gameRules;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private RectTransform playerHandRoot;
    [SerializeField] private RectTransform opponentHandRoot;
    [SerializeField] private Transform playerBattlefieldArea;
    [SerializeField] private Text hintText;

    private readonly List<MTGAHandUiCard> playerCards = new List<MTGAHandUiCard>(12);
    private readonly List<MTGAHandUiCard> opponentCards = new List<MTGAHandUiCard>(12);

    private Canvas canvas;
    private Button phaseButton;
    private Text phaseButtonText;
    private Text phaseStatusText;
    private Text playerLifeText;
    private Text opponentLifeText;
    private ArenaHudController arenaHud;

    private void Awake()
    {
        ResolveReferences();
        ConfigureCanvasAndRoots();
        EnsurePhaseButton();
        CacheCards();
        RefreshAll();
    }

    private void Start()
    {
        ResolveReferences();
        ConfigureCanvasAndRoots();
        EnsurePhaseButton();
        CacheCards();
        RefreshAll();
    }

    private void Update()
    {
        if (gameRules == null || playerCards.Count == 0 || opponentCards.Count == 0)
        {
            ResolveReferences();
            ConfigureCanvasAndRoots();
            EnsurePhaseButton();
            CacheCards();
        }

        RefreshAll();
    }

    private void ResolveReferences()
    {
        if (gameRules == null) gameRules = Object.FindFirstObjectByType<GameRules>();
        if (mainCamera == null) mainCamera = Camera.main;
        if (canvas == null) canvas = GameObject.Find("Canvas")?.GetComponent<Canvas>();
        if (playerHandRoot == null) playerHandRoot = GameObject.Find("MTGAPlayerHandUI")?.GetComponent<RectTransform>();
        if (opponentHandRoot == null) opponentHandRoot = GameObject.Find("MTGAOpponentHandUI")?.GetComponent<RectTransform>();
        if (playerBattlefieldArea == null) playerBattlefieldArea = GameObject.Find("ArenaWhitebox")?.transform.Find("PlayerBattlefieldArea");
        if (hintText == null) hintText = GameObject.Find("ArenaInteractionHintText")?.GetComponent<Text>();
        if (phaseStatusText == null) phaseStatusText = GameObject.Find("ArenaPhaseStatusText")?.GetComponent<Text>();
        if (playerLifeText == null) playerLifeText = GameObject.Find("ArenaPlayerLife")?.GetComponent<Text>();
        if (opponentLifeText == null) opponentLifeText = GameObject.Find("ArenaOpponentLife")?.GetComponent<Text>();
    }

    private void ConfigureCanvasAndRoots()
    {
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        ConfigurePlayerRoot(playerHandRoot);
        ConfigureOpponentRoot(opponentHandRoot);
        EnsureEventSystem();
    }

    private static void ConfigurePlayerRoot(RectTransform root)
    {
        if (root == null) return;

        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = new Vector2(0f, 292f);
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;
    }

    private static void ConfigureOpponentRoot(RectTransform root)
    {
        if (root == null) return;

        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = new Vector2(0f, 86f);
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;
    }

    private void EnsurePhaseButton()
    {
        if (canvas == null) return;
        if (arenaHud == null) arenaHud = canvas.GetComponent<ArenaHudController>();
        if (arenaHud == null) arenaHud = canvas.gameObject.AddComponent<ArenaHudController>();
        arenaHud.Bind(gameRules, hintText);
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    private void CacheCards()
    {
        playerCards.Clear();
        opponentCards.Clear();
        CacheCards(playerHandRoot, playerCards);
        CacheCards(opponentHandRoot, opponentCards);
    }

    private static void CacheCards(RectTransform root, List<MTGAHandUiCard> cards)
    {
        if (root == null) return;

        foreach (Transform child in root)
        {
            MTGAHandUiCard card = child.GetComponent<MTGAHandUiCard>();
            if (card != null) cards.Add(card);
        }

        cards.Sort((a, b) => a.Index.CompareTo(b.Index));
    }

    private void RefreshAll()
    {
        if (gameRules == null) return;

        RefreshHand(playerCards, gameRules.GetHandCards(1), false);
        RefreshHand(opponentCards, gameRules.GetHandCards(2), true);
        RefreshHud();
    }

    private void RefreshHand(List<MTGAHandUiCard> visuals, IReadOnlyList<RuntimeCard> cards, bool hideFace)
    {
        for (int i = 0; i < visuals.Count; i++)
        {
            bool active = i < cards.Count;
            visuals[i].gameObject.SetActive(active);
            if (!active) continue;

            RuntimeCard card = cards[i];
            visuals[i].SetRuntimeCard(card, hideFace);
            bool playable = !hideFace && gameRules != null && gameRules.CanPlayCard(visuals[i].Owner, i);
            visuals[i].SetPlayable(playable);
            LayoutCard(visuals[i], i, cards.Count, hideFace);
        }
    }

    private static void LayoutCard(MTGAHandUiCard visual, int index, int count, bool opponent)
    {
        if (visual == null || visual.IsDragging) return;

        float center = (count - 1) * 0.5f;
        float offset = index - center;

        if (opponent)
        {
            float normalized = center > 0f ? Mathf.Abs(offset) / center : 0f;
            Vector2 position = new Vector2(offset * 70f, 76f - normalized * 5f);
            float rotation = offset * 1.65f;
            visual.ApplyLayout(position, new Vector2(74f, 104f), rotation);
            return;
        }

        float playerNormalized = center > 0f ? Mathf.Abs(offset) / center : 0f;
        Vector2 playerPosition = new Vector2(offset * 132f, 26f - playerNormalized * 38f);
        float playerRotation = -offset * 3.65f;
        visual.ApplyLayout(playerPosition, new Vector2(194f, 274f), playerRotation);
    }

    private void RefreshHud()
    {
        if (phaseStatusText != null)
        {
            phaseStatusText.text = $"P{gameRules.GetCurrentPlayer()} - {gameRules.GetCurrentPhase()}";
        }

        if (playerLifeText != null) playerLifeText.text = gameRules.GetLife(1).ToString();
        if (opponentLifeText != null) opponentLifeText.text = gameRules.GetLife(2).ToString();

    }

    public bool CanDrag(MTGAHandUiCard card)
    {
        return card != null && card.Owner == 1 && gameRules != null && gameRules.CanPlayCard(card.Owner, card.Index);
    }

    public void OnCardHover(MTGAHandUiCard card)
    {
        if (card == null || card.Owner != 1 || gameRules == null) return;

        RuntimeCard runtimeCard = gameRules.GetHandCard(card.Owner, card.Index);
        if (runtimeCard == null) return;

        SetHint($"Preview: {runtimeCard.Data.cardName}. Drag it to your battlefield.");
    }

    public void OnCardDragStarted(MTGAHandUiCard card)
    {
        if (card == null || gameRules == null) return;

        RuntimeCard runtimeCard = gameRules.GetHandCard(card.Owner, card.Index);
        string cardName = runtimeCard != null ? runtimeCard.Data.cardName : "Card";
        SetHint($"Dragging: {cardName}. Release over your battlefield.");
    }

    public void OnCardDropped(MTGAHandUiCard card, PointerEventData eventData)
    {
        if (card == null || gameRules == null) return;

        if (!IsPointerOverPlayerBattlefield(eventData))
        {
            SetHint("Drop canceled: not over your battlefield.");
            return;
        }

        RuntimeCard runtimeCard = gameRules.GetHandCard(card.Owner, card.Index);
        string cardName = runtimeCard != null ? runtimeCard.Data.cardName : "Card";
        bool played = gameRules.PlayCard(card.Owner, card.Index);
        if (played)
        {
            SetHint(gameRules.HasPendingStack()
                ? $"Played: {cardName}. Resolve the stack."
                : $"Played: {cardName}.");
            RefreshAll();
            return;
        }

        SetHint("Cannot play this card now. Check turn, phase, mana, or stack.");
    }

    private void OnPhaseButtonClicked()
    {
        if (gameRules == null) return;

        if (gameRules.HasPendingStack())
        {
            gameRules.ResolveTopOfStack();
            SetHint("Resolved top of stack.");
        }
        else
        {
            gameRules.NextPhase();
            SetHint($"Phase: P{gameRules.GetCurrentPlayer()} - {gameRules.GetCurrentPhase()}.");
        }

        RefreshAll();
    }

    private bool IsPointerOverPlayerBattlefield(PointerEventData eventData)
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null || playerBattlefieldArea == null) return false;

        BoxCollider collider = playerBattlefieldArea.GetComponent<BoxCollider>();
        if (collider == null) return false;

        Ray ray = mainCamera.ScreenPointToRay(eventData.position);
        Plane plane = new Plane(Vector3.up, playerBattlefieldArea.position);
        if (!plane.Raycast(ray, out float distance)) return false;

        Vector3 point = ray.GetPoint(distance);
        Bounds bounds = collider.bounds;
        bounds.Expand(new Vector3(0.45f, 0.45f, 0.45f));
        point.y = bounds.center.y;
        return bounds.Contains(point);
    }

    private void SetHint(string value)
    {
        if (hintText != null) hintText.text = value;
    }
}

public sealed class MTGAHandUiCard : MonoBehaviour, IPointerEnterHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const float DragAnchorHeight = 0.62f;

    [SerializeField] private int owner = 1;
    [SerializeField] private int index;

    private MTGAHandUiController controller;
    private RectTransform rect;
    private Canvas canvas;
    private RectTransform root;
    private Image selectedGlow;
    private Image psdFrame;
    private Image importedCardFace;
    private readonly System.Collections.Generic.List<Image> manaCostIcons = new System.Collections.Generic.List<Image>();
    private Outline psdFrameOutline;
    private Outline outline;
    private Vector2 startAnchoredPosition;
    private Quaternion startRotation;
    private int startSiblingIndex;
    private bool dragging;
    private static Sprite roundedCardGlowSprite;

    public int Owner => owner;
    public int Index => index;
    public bool IsDragging => dragging;

    public void Configure(int newOwner, int newIndex)
    {
        owner = newOwner;
        index = newIndex;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (rect == null) rect = GetComponent<RectTransform>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (root == null) root = transform.parent as RectTransform;
        if (controller == null) controller = Object.FindFirstObjectByType<MTGAHandUiController>();
        if (selectedGlow == null)
        {
            Transform glow = transform.Find("SelectedGlow");
            selectedGlow = glow != null ? glow.GetComponent<Image>() : null;
        }
        if (selectedGlow != null)
        {
            selectedGlow.sprite = GetRoundedCardGlowSprite();
            selectedGlow.preserveAspect = false;
        }
        if (outline == null) outline = GetComponent<Outline>();
        if (psdFrame == null)
        {
            Transform frame = transform.Find("PsdFrame");
            psdFrame = frame != null ? frame.GetComponent<Image>() : null;
        }
        if (importedCardFace == null)
        {
            Transform imported = transform.Find("ImportedCardFace");
            importedCardFace = imported != null ? imported.GetComponent<Image>() : null;
        }
        if (psdFrameOutline == null && psdFrame != null) psdFrameOutline = psdFrame.GetComponent<Outline>();
        if (manaCostIcons.Count == 0)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (!child.name.StartsWith("ManaCostIcon_")) continue;
                Image icon = child.GetComponent<Image>();
                if (icon != null) manaCostIcons.Add(icon);
            }
        }
    }

    public void SetRuntimeCard(RuntimeCard runtimeCard, bool hideFace)
    {
        if (runtimeCard == null) return;

        CardData data = runtimeCard.Data;
        Sprite fullCardSprite = null;
        bool hasImportedFace = !hideFace && CardImageLibrary.TryLoadFullCard(data, out fullCardSprite);
        ApplyPsdFrame(data, hideFace || hasImportedFace);
        SetText("TitleBand/Text", hideFace || hasImportedFace ? "" : data.cardName);
        SetText("RulesBox/Text", hideFace || hasImportedFace ? "" : data.cardTypes.ToDisplayString());
        ApplyManaCost(data, hideFace || hasImportedFace);
        SetColor("Face", hideFace ? new Color(0.12f, 0.07f, 0.04f, 1f) : GetFaceColor(data.cardTypes));
        SetColor("Art", hideFace ? new Color(0.28f, 0.13f, 0.06f, 1f) : GetArtColor(data.cardTypes));
        SetFallbackFaceActive(!hasImportedFace);
        SetArtwork(data, hideFace || hasImportedFace);
        ApplyImportedCardFace(fullCardSprite, hasImportedFace);
    }

    private void ApplyImportedCardFace(Sprite sprite, bool active)
    {
        if (importedCardFace == null && active)
        {
            GameObject faceObject = new GameObject("ImportedCardFace", typeof(RectTransform), typeof(Image));
            faceObject.transform.SetParent(transform, false);
            RectTransform faceRect = faceObject.GetComponent<RectTransform>();
            faceRect.anchorMin = Vector2.zero;
            faceRect.anchorMax = Vector2.one;
            faceRect.offsetMin = Vector2.zero;
            faceRect.offsetMax = Vector2.zero;
            importedCardFace = faceObject.GetComponent<Image>();
            importedCardFace.raycastTarget = false;
            importedCardFace.preserveAspect = false;
        }

        if (importedCardFace == null) return;
        importedCardFace.gameObject.SetActive(active);
        importedCardFace.sprite = active ? sprite : null;
        importedCardFace.color = Color.white;
        if (active) importedCardFace.transform.SetAsLastSibling();
    }

    private void SetFallbackFaceActive(bool active)
    {
        SetChildActive("Art", active);
        SetChildActive("TitleBand", active);
        SetChildActive("RulesBox", active);
    }

    private void SetChildActive(string path, bool active)
    {
        Transform child = transform.Find(path);
        if (child != null) child.gameObject.SetActive(active);
    }

    private void ApplyManaCost(CardData data, bool hideFace)
    {
        Transform legacyText = transform.Find("CostPip");
        Transform legacyBackground = transform.Find("CostPipBg");
        if (legacyText != null) legacyText.gameObject.SetActive(false);
        if (legacyBackground != null) legacyBackground.gameObject.SetActive(false);

        bool showCost = !hideFace && data != null && !data.HasType(CardType.Land) && !data.GetManaCost().IsEmpty;
        if (!showCost)
        {
            SetManaCostIconsActive(false);
            return;
        }

        ManaCost cost = data.GetManaCost();
        var symbols = new System.Collections.Generic.List<string>();
        if (cost.Generic > 0)
        {
            string genericSymbol = GetManaSymbolCode(CardVisualStyle.GetPrimaryColor(data));
            for (int i = 0; i < cost.Generic; i++) symbols.Add(genericSymbol);
        }
        AddManaSymbols(symbols, "W", cost.White);
        AddManaSymbols(symbols, "U", cost.Blue);
        AddManaSymbols(symbols, "B", cost.Black);
        AddManaSymbols(symbols, "R", cost.Red);
        AddManaSymbols(symbols, "G", cost.Green);
        AddManaSymbols(symbols, "C", cost.Colorless);

        const float iconSize = 28f;
        const float spacing = 2f;

        for (int i = 0; i < symbols.Count; i++)
        {
            Image icon = GetOrCreateManaCostIcon(i);

            icon.gameObject.SetActive(true);
            icon.name = $"ManaCostIcon_{i + 1}_{symbols[i]}";
            icon.sprite = Resources.Load<Sprite>($"UI/ManaSymbols/Mana_{symbols[i]}");
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(1f, 1f);
            iconRect.anchorMax = new Vector2(1f, 1f);
            iconRect.pivot = new Vector2(1f, 1f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = new Vector2(-8f - i * (iconSize + spacing), -8f);
            icon.transform.SetAsLastSibling();
        }

        for (int i = symbols.Count; i < manaCostIcons.Count; i++)
        {
            manaCostIcons[i].gameObject.SetActive(false);
        }
    }

    private Image GetOrCreateManaCostIcon(int index)
    {
        if (index < manaCostIcons.Count) return manaCostIcons[index];

        GameObject iconObject = new GameObject($"ManaCostIcon_{index + 1}", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(transform, false);
        Image icon = iconObject.GetComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        manaCostIcons.Add(icon);
        return icon;
    }

    private void SetManaCostIconsActive(bool active)
    {
        for (int i = 0; i < manaCostIcons.Count; i++)
        {
            manaCostIcons[i].gameObject.SetActive(active);
        }
    }

    private static void AddManaSymbols(System.Collections.Generic.List<string> symbols, string code, int count)
    {
        for (int i = 0; i < count; i++) symbols.Add(code);
    }

    private static string GetManaSymbolCode(ManaColor color)
    {
        switch (color)
        {
            case ManaColor.White: return "W";
            case ManaColor.Blue: return "U";
            case ManaColor.Black: return "B";
            case ManaColor.Red: return "R";
            case ManaColor.Green: return "G";
            default: return "C";
        }
    }

    private void ApplyPsdFrame(CardData data, bool hideFace)
    {
        if (psdFrame == null)
        {
            GameObject frameObject = new GameObject("PsdFrame", typeof(RectTransform), typeof(Image));
            frameObject.transform.SetParent(transform, false);
            RectTransform frameRect = frameObject.GetComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            psdFrame = frameObject.GetComponent<Image>();
            psdFrame.raycastTarget = false;
            psdFrame.preserveAspect = false;
            psdFrameOutline = frameObject.AddComponent<Outline>();
            psdFrameOutline.effectColor = Color.clear;
            psdFrameOutline.effectDistance = new Vector2(2.2f, 2.2f);
            psdFrameOutline.useGraphicAlpha = true;

            Transform art = transform.Find("Art");
            if (art != null) frameObject.transform.SetSiblingIndex(art.GetSiblingIndex() + 1);

            Transform titleBand = transform.Find("TitleBand");
            Transform rulesBox = transform.Find("RulesBox");
            Transform costPipBg = transform.Find("CostPipBg");
            Transform costPip = transform.Find("CostPip");
            if (titleBand != null) titleBand.SetAsLastSibling();
            if (rulesBox != null) rulesBox.SetAsLastSibling();
            if (costPipBg != null) costPipBg.SetAsLastSibling();
            if (costPip != null) costPip.SetAsLastSibling();
        }

        psdFrame.gameObject.SetActive(!hideFace);
        if (hideFace) return;

        psdFrame.sprite = CardVisualStyle.LoadFrame(data);
        psdFrame.color = Color.white;
        SetPanelAlpha("TitleBand", 0f);
        SetPanelAlpha("RulesBox", 0f);
    }

    private void SetArtwork(CardData data, bool hideFace)
    {
        Transform artTransform = transform.Find("Art");
        Image artImage = artTransform != null ? artTransform.GetComponent<Image>() : null;
        if (artImage == null) return;

        artImage.sprite = hideFace ? null : data.artwork;
        artImage.preserveAspect = false;
    }

    private void SetPanelAlpha(string path, float alpha)
    {
        Transform child = transform.Find(path);
        Image image = child != null ? child.GetComponent<Image>() : null;
        if (image == null) return;

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    public void ApplyLayout(Vector2 anchoredPosition, Vector2 size, float zRotation)
    {
        ResolveReferences();
        if (rect == null || dragging) return;

        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        rect.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        rect.localScale = Vector3.one;

        Vector3 localPosition = rect.localPosition;
        localPosition.z = 0f;
        rect.localPosition = localPosition;
    }

    public void SetPlayable(bool playable)
    {
        ResolveReferences();

        if (selectedGlow != null)
        {
            if (rect != null)
            {
                selectedGlow.rectTransform.sizeDelta = rect.sizeDelta + new Vector2(12f, 12f);
            }

            Color color = selectedGlow.color;
            color.r = 0.02f;
            color.g = 0.92f;
            color.b = 1f;
            color.a = playable ? 0.72f : 0f;
            selectedGlow.color = color;
        }

        if (outline != null)
        {
            outline.effectColor = Owner == 2
                ? new Color(0f, 0f, 0f, 0.38f)
                : Color.clear;
        }

        if (psdFrameOutline != null)
        {
            psdFrameOutline.effectColor = Color.clear;
            psdFrameOutline.effectDistance = Vector2.zero;
        }
    }

    private static Sprite GetRoundedCardGlowSprite()
    {
        if (roundedCardGlowSprite != null) return roundedCardGlowSprite;

        const int width = 96;
        const int height = 136;
        const float radius = 10f;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Card Glow Rounded Rect",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[width * height];
        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        Vector2 inner = new Vector2(center.x - radius - 1f, center.y - radius - 1f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 distance = new Vector2(Mathf.Abs(x - center.x), Mathf.Abs(y - center.y)) - inner;
                float outside = new Vector2(Mathf.Max(distance.x, 0f), Mathf.Max(distance.y, 0f)).magnitude - radius;
                float alpha = Mathf.Clamp01(1f - outside);
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        roundedCardGlowSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        roundedCardGlowSprite.name = "Card Glow Rounded Rect";
        roundedCardGlowSprite.hideFlags = HideFlags.HideAndDontSave;
        return roundedCardGlowSprite;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        controller?.OnCardHover(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        ResolveReferences();
        if (controller == null || rect == null || root == null || !controller.CanDrag(this)) return;

        dragging = true;
        startAnchoredPosition = rect.anchoredPosition;
        startRotation = rect.localRotation;
        startSiblingIndex = rect.GetSiblingIndex();
        rect.SetAsLastSibling();
        rect.localRotation = Quaternion.identity;
        SetRaycastTarget(false);
        controller.OnCardDragStarted(this);
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || root == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, eventData.position, null, out Vector2 localPoint);
        float verticalOffset = rect.rect.height * DragAnchorHeight;
        rect.anchoredPosition = localPoint - new Vector2(0f, verticalOffset);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging) return;

        dragging = false;
        SetRaycastTarget(true);
        controller?.OnCardDropped(this, eventData);
        rect.anchoredPosition = startAnchoredPosition;
        rect.localRotation = startRotation;
        rect.SetSiblingIndex(startSiblingIndex);
    }

    private void SetRaycastTarget(bool value)
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            graphic.raycastTarget = value;
        }
    }

    private void SetText(string path, string value)
    {
        Transform child = transform.Find(path);
        Text text = child != null ? child.GetComponent<Text>() : null;
        if (text != null) text.text = value;
    }

    private void SetColor(string path, Color color)
    {
        Transform child = transform.Find(path);
        Image image = child != null ? child.GetComponent<Image>() : null;
        if (image != null) image.color = color;
    }

    private static Color GetFaceColor(CardType cardTypes)
    {
        if (cardTypes.Has(CardType.Land)) return new Color(0.78f, 0.84f, 0.78f, 1f);
        if (cardTypes.Has(CardType.Creature)) return new Color(0.72f, 0.84f, 0.90f, 1f);
        if (cardTypes.IsInstantOrSorcery()) return new Color(0.88f, 0.74f, 0.62f, 1f);
        if (cardTypes.Has(CardType.Artifact)) return new Color(0.78f, 0.78f, 0.80f, 1f);
        if (cardTypes.Has(CardType.Enchantment) || cardTypes.Has(CardType.Planeswalker)) return new Color(0.82f, 0.72f, 0.88f, 1f);
        return new Color(0.74f, 0.78f, 0.72f, 1f);
    }

    private static Color GetArtColor(CardType cardTypes)
    {
        if (cardTypes.Has(CardType.Land)) return new Color(0.12f, 0.42f, 0.56f, 1f);
        if (cardTypes.Has(CardType.Creature)) return new Color(0.06f, 0.40f, 0.60f, 1f);
        if (cardTypes.IsInstantOrSorcery()) return new Color(0.75f, 0.25f, 0.10f, 1f);
        if (cardTypes.Has(CardType.Artifact)) return new Color(0.36f, 0.36f, 0.40f, 1f);
        if (cardTypes.Has(CardType.Enchantment) || cardTypes.Has(CardType.Planeswalker)) return new Color(0.45f, 0.26f, 0.62f, 1f);
        return new Color(0.10f, 0.36f, 0.62f, 1f);
    }
}
