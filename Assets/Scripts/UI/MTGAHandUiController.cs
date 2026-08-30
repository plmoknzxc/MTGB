using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MTGAHandUiController : MonoBehaviour
{
    private const float PlayerHandCenterOffsetX = -30f;
    private const float PlayerHandSafeWidth = 1280f;

    [SerializeField] private GameRules gameRules;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private RectTransform playerHandRoot;
    [SerializeField] private RectTransform opponentHandRoot;
    [SerializeField] private Transform playerBattlefieldArea;
    [SerializeField] private Text hintText;
    [SerializeField] private MTGAHandLayoutSettings layout = new MTGAHandLayoutSettings();

    private readonly List<MTGAHandUiCard> playerCards = new List<MTGAHandUiCard>(12);
    private readonly List<MTGAHandUiCard> opponentCards = new List<MTGAHandUiCard>(12);

    private Canvas canvas;
    private Button phaseButton;
    private Text phaseButtonText;
    private Text phaseStatusText;
    private Text playerLifeText;
    private Text opponentLifeText;
    private ArenaHudController arenaHud;
    private ArenaWhiteboxInteraction whiteboxInteraction;
    private int pendingCastOwner;
    private int pendingCastHandIndex = -1;

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
        if (whiteboxInteraction == null) whiteboxInteraction = Object.FindFirstObjectByType<ArenaWhiteboxInteraction>();
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

    private void ConfigurePlayerRoot(RectTransform root)
    {
        if (root == null) return;
        MTGAHandLayoutSettings settings = EnsureLayout();

        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = new Vector2(0f, settings.playerRootY);
        root.sizeDelta = new Vector2(0f, settings.playerRootHeight);
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;
    }

    private void ConfigureOpponentRoot(RectTransform root)
    {
        if (root == null) return;
        MTGAHandLayoutSettings settings = EnsureLayout();

        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, settings.opponentRootY);
        root.sizeDelta = new Vector2(0f, settings.opponentRootHeight);
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

        int localPlayer = OnlinePlayerContext.LocalPlayerId;
        int opponentPlayer = OnlinePlayerContext.OpponentPlayerId;
        RefreshHand(playerCards, playerHandRoot, gameRules.GetHandCards(localPlayer), false, localPlayer);
        RefreshHand(opponentCards, opponentHandRoot, gameRules.GetHandCards(opponentPlayer), true, opponentPlayer);
        RefreshHud();
    }

    private void RefreshHand(List<MTGAHandUiCard> visuals, RectTransform root, IReadOnlyList<RuntimeCard> cards, bool hideFace, int owner)
    {
        EnsureHandCapacity(root, visuals, cards.Count, owner);

        int visibleCount = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (!ShouldHideHandCard(i, hideFace)) visibleCount++;
        }

        int visibleIndex = 0;
        for (int i = 0; i < visuals.Count; i++)
        {
            bool active = i < cards.Count && !ShouldHideHandCard(i, hideFace);
            visuals[i].gameObject.SetActive(active);
            if (!active) continue;

            RuntimeCard card = cards[i];
            visuals[i].SetRuntimeCard(card, hideFace);
            bool discardable = gameRules != null && gameRules.CanDiscardHandCardToMaxHandSize(owner, i);
            bool selectedForDiscard = gameRules != null && gameRules.IsHandCardSelectedForDiscard(owner, i);
            bool mulliganBottomSelectable = TryGetMulliganBottomOption(owner, i, out GameChoiceRequest mulliganChoice, out GameChoiceOption mulliganOption);
            bool selectedForMulliganBottom = mulliganBottomSelectable
                && mulliganChoice.selectedOptionIds.Contains(mulliganOption.optionId);
            bool playable = !hideFace
                && !IsCastInteractionLocked()
                && gameRules != null
                && (discardable || mulliganBottomSelectable || gameRules.CanPlayCard(visuals[i].Owner, i));
            visuals[i].SetPlayable(playable, selectedForDiscard || selectedForMulliganBottom);
            LayoutCard(visuals[i], visibleIndex, visibleCount, hideFace);
            visibleIndex++;
        }
    }

    private static void EnsureHandCapacity(RectTransform root, List<MTGAHandUiCard> visuals, int requiredCount, int owner)
    {
        if (root == null || visuals == null) return;

        bool opponentPerspective = owner != OnlinePlayerContext.LocalPlayerId;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        while (visuals.Count < requiredCount)
        {
            int index = visuals.Count;
            string name = opponentPerspective ? $"OpponentHandCard_{index + 1}" : $"PlayerHandCard_{index + 1}";
            Vector2 size = opponentPerspective ? new Vector2(74f, 104f) : new Vector2(194f, 274f);
            MTGAHandUiCard card = MTGAUiCardFactory.CreateCard(root, name, font, owner, index, size);
            visuals.Add(card);
        }

        for (int i = 0; i < visuals.Count; i++)
        {
            if (visuals[i] == null) continue;
            visuals[i].Configure(owner, i);
            visuals[i].name = opponentPerspective ? $"OpponentHandCard_{i + 1}" : $"PlayerHandCard_{i + 1}";
        }
    }

    private bool ShouldHideHandCard(int handIndex, bool opponentHand)
    {
        int owner = opponentHand ? OnlinePlayerContext.OpponentPlayerId : OnlinePlayerContext.LocalPlayerId;
        return pendingCastOwner == owner && pendingCastHandIndex == handIndex;
    }

    private void LayoutCard(MTGAHandUiCard visual, int index, int count, bool opponent)
    {
        if (visual == null || visual.IsDragging) return;

        MTGAHandLayoutSettings settings = EnsureLayout();
        float center = (count - 1) * 0.5f;
        float offset = index - center;

        if (opponent)
        {
            float normalized = center > 0f ? Mathf.Abs(offset) / center : 0f;
            float spacing = count <= 1 ? 0f : Mathf.Min(settings.opponentMaxSpacing, settings.opponentTotalWidth / Mathf.Max(1, count - 1));
            float scale = count <= settings.fullScaleCardCount ? 1f : Mathf.Clamp(spacing / Mathf.Max(0.01f, settings.opponentMaxSpacing), settings.opponentMinScale, 1f);
            Vector2 position = new Vector2(offset * spacing, settings.opponentBaseY - normalized * settings.opponentArcDepth);
            float rotation = offset * settings.opponentRotationPerOffset;
            visual.ApplyLayout(position, new Vector2(settings.opponentCardWidth, settings.opponentCardHeight) * scale, rotation);
            return;
        }

        float playerNormalized = center > 0f ? Mathf.Abs(offset) / center : 0f;
        float usableWidth = Mathf.Min(settings.playerTotalWidth, PlayerHandSafeWidth);
        float naturalWidth = settings.playerCardWidth + Mathf.Max(0, count - 1) * settings.playerMaxSpacing;
        float playerScale = count <= settings.fullScaleCardCount
            ? 1f
            : Mathf.Clamp(usableWidth / Mathf.Max(1f, naturalWidth), settings.playerMinScale, 1f);
        float playerSpacing = count <= 1 ? 0f : settings.playerMaxSpacing * playerScale;
        Vector2 playerPosition = new Vector2(
            PlayerHandCenterOffsetX + offset * playerSpacing,
            settings.playerBaseY - playerNormalized * settings.playerArcDepth * playerScale);
        float playerRotation = offset * settings.playerRotationPerOffset;
        visual.ApplyLayout(playerPosition, new Vector2(settings.playerCardWidth, settings.playerCardHeight) * playerScale, playerRotation);
    }

    private void RefreshHud()
    {
        if (phaseStatusText != null)
        {
            string priority = gameRules.IsAwaitingPriority()
                ? $" / P{gameRules.GetPriorityPlayer()} \u4f18\u5148\u6743"
                : string.Empty;
            phaseStatusText.text = $"P{gameRules.GetCurrentPlayer()} - {gameRules.GetCurrentPhaseLabel()}{priority}";
        }

        if (playerLifeText != null) playerLifeText.text = gameRules.GetLife(OnlinePlayerContext.LocalPlayerId).ToString();
        if (opponentLifeText != null) opponentLifeText.text = gameRules.GetLife(OnlinePlayerContext.OpponentPlayerId).ToString();

    }

    public bool CanDrag(MTGAHandUiCard card)
    {
        return card != null
            && card.Owner == OnlinePlayerContext.LocalPlayerId
            && !IsCastInteractionLocked()
            && (gameRules == null || !gameRules.IsDiscardingToHandSize())
            && gameRules != null
            && gameRules.CanPlayCard(card.Owner, card.Index);
    }

    private bool IsCastInteractionLocked()
    {
        if (whiteboxInteraction == null) whiteboxInteraction = Object.FindFirstObjectByType<ArenaWhiteboxInteraction>();
        return whiteboxInteraction != null && whiteboxInteraction.IsChoosingSpellTarget;
    }

    public void RestoreHandInputAfterExternalCancel()
    {
        ResolveReferences();
        if (playerCards.Count == 0 || opponentCards.Count == 0) CacheCards();

        RestoreHandInput(playerCards);
        RestoreHandInput(opponentCards);
        RefreshAll();
    }

    public void SetPendingCastCard(int owner, int handIndex)
    {
        pendingCastOwner = owner;
        pendingCastHandIndex = handIndex;
        RefreshAll();
    }

    public void ClearPendingCastCard()
    {
        pendingCastOwner = 0;
        pendingCastHandIndex = -1;
        RefreshAll();
    }

    private static void RestoreHandInput(List<MTGAHandUiCard> cards)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i]?.RestoreInputAfterExternalCancel();
        }
    }

    public void OnCardHover(MTGAHandUiCard card)
    {
        if (card == null || card.Owner != OnlinePlayerContext.LocalPlayerId || gameRules == null) return;
        if (IsCastInteractionLocked()) return;

        RuntimeCard runtimeCard = gameRules.GetHandCard(card.Owner, card.Index);
        if (runtimeCard == null) return;

        if (gameRules.CanDiscardHandCardToMaxHandSize(card.Owner, card.Index))
        {
            SetHint($"清除步骤：点击弃掉 {runtimeCard.Data.cardName}。还需弃 {gameRules.GetDiscardToHandSizeRemaining()} 张。");
            return;
        }

        if (TryGetMulliganBottomOption(card.Owner, card.Index, out GameChoiceRequest choice, out _))
        {
            int remaining = Mathf.Max(0, choice.minimumSelections - choice.selectedOptionIds.Count);
            SetHint($"调度：点击选择 {runtimeCard.Data.cardName} 置于牌库底。还需选择 {remaining} 张。");
            return;
        }

        string issue = gameRules.GetHandCardPlayIssue(card.Owner, card.Index);
        SetHint(string.IsNullOrEmpty(issue)
            ? $"Preview: {runtimeCard.Data.cardName}. Drag it to your battlefield."
            : $"{runtimeCard.Data.cardName}: {issue}");
    }

    public void OnCardDragStarted(MTGAHandUiCard card)
    {
        if (OnlineMatchService.Instance?.CanSubmitRuleAction == false) return;
        if (card == null || gameRules == null) return;
        if (IsCastInteractionLocked()) return;

        RuntimeCard runtimeCard = gameRules.GetHandCard(card.Owner, card.Index);
        string cardName = runtimeCard != null ? runtimeCard.Data.cardName : "Card";
        SetHint($"Dragging: {cardName}. Release over your battlefield.");
    }

    public void OnCardDropped(MTGAHandUiCard card, PointerEventData eventData)
    {
        if (OnlineMatchService.Instance?.CanSubmitRuleAction == false) return;
        if (card == null || gameRules == null) return;
        if (gameRules.IsDiscardingToHandSize())
        {
            SetHint($"清除步骤：请选择要弃的手牌。还需弃 {gameRules.GetDiscardToHandSizeRemaining()} 张。");
            RefreshAll();
            return;
        }

        if (IsCastInteractionLocked())
        {
            SetHint("\u8bf7\u5148\u786e\u8ba4\u65bd\u653e\u3001\u53d6\u6d88\uff0c\u6216\u5b8c\u6210\u76ee\u6807\u9009\u62e9\u3002");
            RefreshAll();
            return;
        }

        if (!IsPointerOverPlayerBattlefield(eventData))
        {
            SetHint("Drop canceled: not over your battlefield.");
            return;
        }

        RuntimeCard runtimeCard = gameRules.GetHandCard(card.Owner, card.Index);
        string cardName = runtimeCard != null ? runtimeCard.Data.cardName : "Card";
        IReadOnlyList<CardFeatureRuntime> castFeatures =
            gameRules.GetPlayableHandFeatures(card.Owner, card.Index);
        if (castFeatures.Count > 1)
        {
            if (whiteboxInteraction == null) whiteboxInteraction = Object.FindFirstObjectByType<ArenaWhiteboxInteraction>();
            if (whiteboxInteraction != null
                && whiteboxInteraction.BeginHandCastFeatureSelection(card.Owner, card.Index))
            {
                SetHint($"Choose how to cast {cardName}.");
                RefreshAll();
                return;
            }
        }

        if (whiteboxInteraction == null)
        {
            whiteboxInteraction = Object.FindFirstObjectByType<ArenaWhiteboxInteraction>();
        }

        bool castStarted = whiteboxInteraction != null
            && whiteboxInteraction.BeginHandCastFeatureSelection(card.Owner, card.Index);
        if (castStarted)
        {
            if (gameRules.IsChoosingSpellTarget())
                SetHint($"Choose targets for {cardName}.");
            else if (gameRules.IsConfirmingCast())
                SetHint($"Confirm casting {cardName}.");
            else if (gameRules.HasActiveChoice())
                SetHint(gameRules.GetActiveChoice()?.prompt ?? $"Complete casting {cardName}.");
            else if (gameRules.HasPendingStack())
                SetHint($"Cast: {cardName}. Resolve the stack.");
            else
                SetHint($"Played: {cardName}.");
            RefreshAll();
            return;
        }

        SetHint(gameRules.GetHandCardPlayIssue(card.Owner, card.Index));
    }

    public void OnCardClicked(MTGAHandUiCard card, PointerEventData eventData)
    {
        if (OnlineMatchService.Instance?.CanSubmitRuleAction == false) return;
        if (card == null || gameRules == null || eventData.button != PointerEventData.InputButton.Left) return;

        RuntimeCard runtimeCard = gameRules.GetHandCard(card.Owner, card.Index);
        string cardName = runtimeCard != null ? runtimeCard.Data.cardName : "Card";
        if (gameRules.CanDiscardHandCardToMaxHandSize(card.Owner, card.Index))
        {
            if (gameRules.ApplyAction(new GameAction
                {
                    type = GameActionType.ToggleDiscard,
                    playerId = card.Owner,
                    zoneIndex = card.Index
                }).accepted)
            {
                bool selected = gameRules.IsHandCardSelectedForDiscard(card.Owner, card.Index);
                SetHint(selected
                    ? $"已选择 {cardName}。选够 {gameRules.GetDiscardToHandSizeRequiredCount()} 张后点击确认。"
                    : $"已取消 {cardName}，可以重新选择。");
                RefreshAll();
            }
            return;
        }

        if (!TryGetMulliganBottomOption(card.Owner, card.Index, out GameChoiceRequest choice, out GameChoiceOption option)) return;
        bool wasSelected = choice.selectedOptionIds.Contains(option.optionId);
        if (gameRules.ApplyAction(new GameAction
            {
                type = GameActionType.ToggleChoiceOption,
                playerId = card.Owner,
                choiceRequestId = choice.requestId,
                optionId = option.optionId
            }).accepted)
        {
            bool selected = !wasSelected;
            int selectedCount = choice.selectedOptionIds.Count + (selected ? 1 : -1);
            SetHint(selected
                ? $"已选择 {cardName} 置于牌库底（{selectedCount}/{choice.minimumSelections}）。"
                : $"已取消 {cardName}，可以重新选择置底牌。");
            RefreshAll();
        }
    }

    private bool TryGetMulliganBottomOption(
        int owner,
        int handIndex,
        out GameChoiceRequest choice,
        out GameChoiceOption option)
    {
        choice = gameRules != null ? gameRules.GetActiveChoice() : null;
        option = null;
        if (choice == null
            || choice.kind != GameChoiceKind.MulliganBottom
            || choice.choosingPlayerId != owner
            || owner != OnlinePlayerContext.LocalPlayerId)
        {
            return false;
        }

        for (int i = 0; i < choice.options.Count; i++)
        {
            GameChoiceOption candidate = choice.options[i];
            if (candidate != null
                && candidate.legal
                && candidate.zone == GameChoiceZone.Hand
                && candidate.ownerPlayerId == owner
                && candidate.zoneIndex == handIndex)
            {
                option = candidate;
                return true;
            }
        }
        return false;
    }

    private void OnPhaseButtonClicked()
    {
        if (gameRules == null || OnlineMatchService.Instance?.CanSubmitRuleAction == false) return;

        GameActionType actionType = gameRules.GetPrimaryAdvanceActionType(
            OnlinePlayerContext.LocalPlayerId);
        gameRules.ApplyAction(new GameAction
        {
            type = actionType,
            playerId = OnlinePlayerContext.LocalPlayerId
        });
        if (actionType == GameActionType.CommitAttackers)
        {
            SetHint("\u5df2\u5ba3\u544a\u653b\u51fb\u8005\u3002");
        }
        else
        {
            SetHint($"Phase: P{gameRules.GetCurrentPlayer()} - {gameRules.GetCurrentPhaseLabel()}.");
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

    public void ApplyLayoutSettings(MTGAHandLayoutSettings settings)
    {
        if (settings == null) return;

        EnsureLayout().CopyFrom(settings);
        ConfigureCanvasAndRoots();
        RefreshAll();
    }

    public string GetLayoutSettingsJson()
    {
        return JsonUtility.ToJson(EnsureLayout().Clone());
    }

    public void ApplyLayoutSettingsJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        MTGAHandLayoutSettings incoming = JsonUtility.FromJson<MTGAHandLayoutSettings>(json);
        if (incoming == null) return;

        EnsureLayout().CopyFrom(incoming);
        ConfigureCanvasAndRoots();
        RefreshAll();
    }

    public void ResetLayoutSettings()
    {
        layout = new MTGAHandLayoutSettings();
        ConfigureCanvasAndRoots();
        RefreshAll();
    }

    private MTGAHandLayoutSettings EnsureLayout()
    {
        if (layout == null) layout = new MTGAHandLayoutSettings();
        layout.Sanitize();
        return layout;
    }

    private void SetHint(string value)
    {
        if (hintText != null) hintText.text = value;
    }
}

public sealed partial class MTGAHandUiCard : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
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

        CardDefinition data = runtimeCard.Data;
        Sprite fullCardSprite = null;
        Sprite cardBackSprite = null;
        bool hasCardBack = hideFace && CardImageLibrary.TryLoadCardBack(out cardBackSprite);
        bool hasImportedFace = !hideFace && CardImageLibrary.TryLoadFullCard(data, out fullCardSprite);
        bool hasFullFace = hasCardBack || hasImportedFace;
        ApplyPsdFrame(data, hideFace || hasImportedFace);
        SetText("TitleBand/Text", hideFace || hasImportedFace ? "" : data.cardName);
        SetText("RulesBox/Text", hideFace || hasImportedFace ? "" : data.GetTypeLine());
        ApplyManaCost(data, hideFace || hasImportedFace);
        SetColor("Face", hideFace ? new Color(0.12f, 0.07f, 0.04f, 1f) : GetFaceColor(data.cardTypes));
        SetColor("Art", hideFace ? new Color(0.28f, 0.13f, 0.06f, 1f) : GetArtColor(data.cardTypes));
        SetFallbackFaceActive(!hasFullFace);
        SetArtwork(data, hideFace || hasImportedFace);
        ApplyImportedCardFace(hasCardBack ? cardBackSprite : fullCardSprite, hasFullFace);
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

    private void ApplyManaCost(CardDefinition data, bool hideFace)
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
        if (cost.Generic > 0) symbols.Add(cost.Generic.ToString());
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
            bool generic = int.TryParse(symbols[i], out _);
            string spriteCode = generic ? "C" : symbols[i];
            Sprite sprite = Resources.Load<Sprite>($"UI/ManaSymbols/Mana_{spriteCode}");
            icon.sprite = sprite;
            icon.color = Color.white;
            SetGenericManaLabel(icon, generic ? symbols[i] : string.Empty);
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

    private static void SetGenericManaLabel(Image icon, string value)
    {
        if (icon == null) return;
        Transform existing = icon.transform.Find("GenericLabel");
        Text label = existing != null ? existing.GetComponent<Text>() : null;
        if (label == null)
        {
            GameObject labelObject = new GameObject("GenericLabel", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(icon.transform, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            label = labelObject.GetComponent<Text>();
            label.alignment = TextAnchor.MiddleCenter;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        label.text = value;
        label.gameObject.SetActive(!string.IsNullOrEmpty(value));
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

    private void ApplyPsdFrame(CardDefinition data, bool hideFace)
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

    private void SetArtwork(CardDefinition data, bool hideFace)
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

    public void SetPlayable(bool playable, bool selectedForDiscard = false)
    {
        ResolveReferences();

        if (selectedGlow != null)
        {
            if (rect != null)
            {
                selectedGlow.rectTransform.sizeDelta = rect.sizeDelta + new Vector2(12f, 12f);
            }

            Color color = selectedGlow.color;
            color.r = selectedForDiscard ? 1f : 0.02f;
            color.g = selectedForDiscard ? 0.72f : 0.92f;
            color.b = selectedForDiscard ? 0.08f : 1f;
            color.a = playable ? 0.72f : 0f;
            selectedGlow.color = color;
        }

        if (outline != null)
        {
            outline.effectColor = Owner != OnlinePlayerContext.LocalPlayerId
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

    public void OnPointerClick(PointerEventData eventData)
    {
        controller?.OnCardClicked(this, eventData);
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

    public void RestoreInputAfterExternalCancel()
    {
        ResolveReferences();
        dragging = false;
        SetRaycastTarget(true);
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
