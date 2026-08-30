using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ArenaHudController : MonoBehaviour
{
    private static readonly ManaColor[] ManaColors =
    {
        ManaColor.White,
        ManaColor.Blue,
        ManaColor.Black,
        ManaColor.Red,
        ManaColor.Green,
        ManaColor.Colorless
    };

    private static readonly Color[] ManaTints =
    {
        new Color(0.95f, 0.87f, 0.62f, 1f),
        new Color(0.18f, 0.64f, 1f, 1f),
        new Color(0.48f, 0.27f, 0.68f, 1f),
        new Color(0.94f, 0.30f, 0.18f, 1f),
        new Color(0.28f, 0.72f, 0.38f, 1f),
        new Color(0.58f, 0.65f, 0.72f, 1f)
    };

    private static readonly string[] ManaSymbols = { "W", "U", "B", "R", "G", "C" };

    private GameRules gameRules;
    private Text hintText;
    private RectTransform hudRoot;
    private Text phaseText;
    private Text actionPromptText;
    private Image phaseRing;
    private Image phaseFill;
    private Text controlText;
    private Image controlFill;
    private Button endPhaseButton;
    private Image endPhaseFill;
    private Text endPhaseText;
    private Button skipOpponentButton;
    private Image skipOpponentFill;
    private RectTransform manaPanel;
    private Text manaEmptyText;
    private ArenaWhiteboxInteraction whiteboxInteraction;
    private readonly GameObject[] manaItems = new GameObject[6];
    private readonly Text[] manaCounts = new Text[6];
    private readonly RectTransform[] manaItemRects = new RectTransform[6];
    private readonly RectTransform[] manaTokenRects = new RectTransform[6];
    private ArenaMatchLayoutSettings.HudLayout layout = new ArenaMatchLayoutSettings.HudLayout();
    private RectTransform phaseRingRect;
    private RectTransform phaseButtonRect;
    private RectTransform actionPromptRect;
    private RectTransform controlRingRect;
    private RectTransform controlButtonRect;
    private RectTransform endRingRect;
    private RectTransform endButtonRect;
    private RectTransform skipOpponentRect;
    private bool fullControl;
    private bool built;
    private static Sprite circleSprite;

    public bool FullControl => fullControl;

    private void Awake()
    {
        BuildIfNeeded();
        ResolveReferences();
        Refresh(true);
    }

    private void Start()
    {
        BuildIfNeeded();
        ResolveReferences();
        Refresh(true);
    }

    private void Update()
    {
        if (gameRules == null) ResolveReferences();
        Refresh(false);
    }

    public void Bind(GameRules rules, Text interactionHint)
    {
        gameRules = rules;
        hintText = interactionHint;
        if (gameRules != null) fullControl = gameRules.IsFullControl();
        BuildIfNeeded();
        ApplyCurrentLayout();
        Refresh(true);
    }

    public void ApplyLayoutSettings(ArenaMatchLayoutSettings.HudLayout settings)
    {
        if (settings == null) return;

        layout.CopyFrom(settings);
        layout.Sanitize();
        BuildIfNeeded();
        ApplyCurrentLayout();
        Refresh(true);
    }

    private void ResolveReferences()
    {
        if (gameRules == null) gameRules = Object.FindFirstObjectByType<GameRules>();
        if (hintText == null) hintText = GameObject.Find("ArenaInteractionHintText")?.GetComponent<Text>();
        if (whiteboxInteraction == null) whiteboxInteraction = Object.FindFirstObjectByType<ArenaWhiteboxInteraction>();
    }

    private void BuildIfNeeded()
    {
        if (built) return;
        built = true;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        circleSprite = circleSprite != null ? circleSprite : CreateCircleSprite(128);

        GameObject rootObject = new GameObject("ArenaHud", typeof(RectTransform));
        rootObject.transform.SetParent(transform, false);
        hudRoot = rootObject.GetComponent<RectTransform>();
        hudRoot.anchorMin = Vector2.zero;
        hudRoot.anchorMax = Vector2.one;
        hudRoot.offsetMin = Vector2.zero;
        hudRoot.offsetMax = Vector2.zero;
        hudRoot.SetAsLastSibling();

        BuildPhaseControl(font);
        BuildActionPrompt(font);
        BuildBottomControls(font);
        BuildManaPool(font);
        BuildOpponentSkipControl(font);
        ApplyCurrentLayout();
    }

    private void ApplyCurrentLayout()
    {
        layout.Sanitize();
        SetRect(phaseRingRect, layout.phaseRingPosition, new Vector2(layout.phaseRingSize, layout.phaseRingSize));
        SetCenteredSize(phaseButtonRect, layout.phaseButtonSize);
        SetRect(actionPromptRect, layout.actionPromptPosition, layout.actionPromptSize);
        SetRect(controlRingRect, layout.controlRingPosition, new Vector2(layout.controlRingSize, layout.controlRingSize));
        SetCenteredSize(controlButtonRect, layout.controlButtonSize);
        SetRect(endRingRect, layout.endRingPosition, new Vector2(layout.endRingSize, layout.endRingSize));
        SetCenteredSize(endButtonRect, layout.endButtonSize);
        SetRect(skipOpponentRect, layout.skipOpponentPosition, layout.skipOpponentSize);
        if (manaPanel != null) manaPanel.anchoredPosition = layout.manaPanelPosition;

        for (int i = 0; i < manaItemRects.Length; i++)
        {
            RectTransform itemRect = manaItemRects[i];
            if (itemRect == null) continue;
            itemRect.anchoredPosition = new Vector2(76f + i * layout.manaItemSpacing, 0f);
            itemRect.sizeDelta = new Vector2(layout.manaItemWidth, 44f);
            SetCenteredSize(manaTokenRects[i], layout.manaTokenSize);
        }
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetCenteredSize(RectTransform rect, float size)
    {
        if (rect == null) return;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);
    }

    private void BuildPhaseControl(Font font)
    {
        GameObject outer = Circle(hudRoot, "PhaseRing", new Vector2(-34f, 216f), 174f, ObsidianArenaTheme.Blue);
        RectTransform outerRect = outer.GetComponent<RectTransform>();
        phaseRingRect = outerRect;
        outerRect.anchorMin = outerRect.anchorMax = new Vector2(1f, 0f);
        outerRect.pivot = new Vector2(1f, 0f);
        phaseRing = outer.GetComponent<Image>();
        phaseRing.raycastTarget = false;

        GameObject buttonObject = Circle(outerRect, "PhaseButton", Vector2.zero, 150f, ObsidianArenaTheme.Surface);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        phaseButtonRect = buttonRect;
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = Vector2.zero;
        phaseFill = buttonObject.GetComponent<Image>();
        phaseFill.raycastTarget = false;

        phaseText = CreateText(buttonRect, "PhaseText", "", 19, font, TextAnchor.MiddleCenter, Color.white);
        phaseText.rectTransform.anchorMin = new Vector2(0.08f, 0.12f);
        phaseText.rectTransform.anchorMax = new Vector2(0.92f, 0.88f);
        phaseText.rectTransform.offsetMin = Vector2.zero;
        phaseText.rectTransform.offsetMax = Vector2.zero;
        AddTextOutline(phaseText, new Color(0f, 0f, 0f, 0.86f), 1.2f);
    }

    private void BuildActionPrompt(Font font)
    {
        GameObject panelObject = new GameObject("ActionPrompt", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(hudRoot, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        actionPromptRect = rect;
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-208f, 310f);
        rect.sizeDelta = new Vector2(430f, 44f);

        Image background = panelObject.GetComponent<Image>();
        background.color = new Color(ObsidianArenaTheme.Surface.r, ObsidianArenaTheme.Surface.g, ObsidianArenaTheme.Surface.b, 0.88f);
        background.raycastTarget = false;

        actionPromptText = CreateText(rect, "ActionPromptText", "", 15, font, TextAnchor.MiddleRight, ObsidianArenaTheme.Text);
        Stretch(actionPromptText.rectTransform, 8f);
        AddTextOutline(actionPromptText, Color.black, 0.9f);
    }

    private void BuildBottomControls(Font font)
    {
        GameObject controlOuter = Circle(hudRoot, "ControlRing", new Vector2(-154f, 24f), 92f, ObsidianArenaTheme.Blue);
        RectTransform controlOuterRect = controlOuter.GetComponent<RectTransform>();
        controlOuter.GetComponent<Image>().raycastTarget = false;
        controlOuterRect.anchorMin = controlOuterRect.anchorMax = new Vector2(1f, 0f);
        controlOuterRect.pivot = new Vector2(1f, 0f);

        GameObject controlButtonObject = Circle(controlOuterRect, "FullControlButton", Vector2.zero, 78f, ObsidianArenaTheme.Surface);
        RectTransform controlRect = controlButtonObject.GetComponent<RectTransform>();
        controlRect.anchorMin = controlRect.anchorMax = new Vector2(0.5f, 0.5f);
        controlRect.pivot = new Vector2(0.5f, 0.5f);
        controlRect.anchoredPosition = Vector2.zero;
        controlFill = controlButtonObject.GetComponent<Image>();
        Button controlButton = controlButtonObject.AddComponent<Button>();
        controlButton.targetGraphic = controlFill;
        controlButton.transition = Selectable.Transition.None;
        controlButton.onClick.AddListener(ToggleFullControl);

        controlText = CreateText(controlRect, "ControlText", "", 17, font, TextAnchor.MiddleCenter, Color.white);
        Stretch(controlText.rectTransform, 4f);
        AddTextOutline(controlText, Color.black, 1f);

        GameObject endOuter = Circle(hudRoot, "EndPhaseRing", new Vector2(-42f, 18f), 106f, ObsidianArenaTheme.Red);
        RectTransform endOuterRect = endOuter.GetComponent<RectTransform>();
        endOuter.GetComponent<Image>().raycastTarget = false;
        endOuterRect.anchorMin = endOuterRect.anchorMax = new Vector2(1f, 0f);
        endOuterRect.pivot = new Vector2(1f, 0f);

        GameObject endButtonObject = Circle(endOuterRect, "EndPhaseButton", Vector2.zero, 90f, ObsidianArenaTheme.RedDeep);
        RectTransform endRect = endButtonObject.GetComponent<RectTransform>();
        endButtonRect = endRect;
        endRect.anchorMin = endRect.anchorMax = new Vector2(0.5f, 0.5f);
        endRect.pivot = new Vector2(0.5f, 0.5f);
        endRect.anchoredPosition = Vector2.zero;
        endPhaseButton = endButtonObject.AddComponent<Button>();
        endPhaseFill = endButtonObject.GetComponent<Image>();
        endPhaseButton.targetGraphic = endPhaseFill;
        endPhaseButton.transition = Selectable.Transition.None;
        endPhaseButton.onClick.AddListener(OnEndPhaseClicked);

        endPhaseText = CreateText(endRect, "EndPhaseText", "\u7ed3\u675f\n\u5f53\u524d\u9636\u6bb5", 15, font, TextAnchor.MiddleCenter, Color.white);
        Stretch(endPhaseText.rectTransform, 4f);
        AddTextOutline(endPhaseText, Color.black, 1f);
    }

    private void BuildOpponentSkipControl(Font font)
    {
        GameObject buttonObject = new GameObject("SkipOpponentTurnButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(hudRoot, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        skipOpponentRect = rect;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(26f, -26f);
        rect.sizeDelta = new Vector2(196f, 58f);

        skipOpponentFill = buttonObject.GetComponent<Image>();
        skipOpponentButton = buttonObject.GetComponent<Button>();
        skipOpponentButton.targetGraphic = skipOpponentFill;
        skipOpponentButton.transition = Selectable.Transition.None;
        skipOpponentButton.onClick.AddListener(OnSkipOpponentTurnClicked);

        Text label = CreateText(rect, "SkipOpponentTurnText", "\u8df3\u8fc7\u5bf9\u624b\u56de\u5408", 18, font, TextAnchor.MiddleCenter, Color.white);
        Stretch(label.rectTransform, 8f);
        AddTextOutline(label, Color.black, 1f);
    }

    private void BuildManaPool(Font font)
    {
        GameObject panelObject = new GameObject("ManaPool", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(hudRoot, false);
        manaPanel = panelObject.GetComponent<RectTransform>();
        manaPanel.anchorMin = manaPanel.anchorMax = new Vector2(1f, 0f);
        manaPanel.pivot = new Vector2(1f, 0f);
        manaPanel.anchoredPosition = new Vector2(-34f, 140f);
        manaPanel.sizeDelta = new Vector2(112f, 58f);
        Image background = panelObject.GetComponent<Image>();
        background.color = new Color(ObsidianArenaTheme.Surface.r, ObsidianArenaTheme.Surface.g, ObsidianArenaTheme.Surface.b, 0.88f);
        background.raycastTarget = false;

        Text title = CreateText(manaPanel, "ManaTitle", "\u6cd5\u672f\u529b", 15, font, TextAnchor.MiddleRight, ObsidianArenaTheme.MutedText);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 0.5f);
        titleRect.pivot = new Vector2(0f, 0.5f);
        titleRect.anchoredPosition = new Vector2(10f, 0f);
        titleRect.sizeDelta = new Vector2(64f, 38f);

        manaEmptyText = CreateText(manaPanel, "EmptyCount", "0", 20, font, TextAnchor.MiddleLeft, Color.white);
        RectTransform emptyRect = manaEmptyText.rectTransform;
        emptyRect.anchorMin = emptyRect.anchorMax = new Vector2(0f, 0.5f);
        emptyRect.pivot = new Vector2(0f, 0.5f);
        emptyRect.anchoredPosition = new Vector2(82f, 0f);
        emptyRect.sizeDelta = new Vector2(22f, 34f);
        AddTextOutline(manaEmptyText, Color.black, 1f);

        for (int i = 0; i < ManaColors.Length; i++)
        {
            GameObject item = new GameObject($"Mana_{ManaSymbols[i]}", typeof(RectTransform));
            item.transform.SetParent(manaPanel, false);
            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = itemRect.anchorMax = new Vector2(0f, 0.5f);
            itemRect.pivot = new Vector2(0f, 0.5f);
            itemRect.anchoredPosition = new Vector2(76f + i * 56f, 0f);
            itemRect.sizeDelta = new Vector2(52f, 44f);

            Sprite manaSprite = Resources.Load<Sprite>($"UI/ManaSymbols/Mana_{ManaSymbols[i]}");
            GameObject token = Circle(itemRect, "Token", new Vector2(2f, 4f), 36f, manaSprite != null ? Color.white : ManaTints[i]);
            RectTransform tokenRect = token.GetComponent<RectTransform>();
            tokenRect.anchorMin = tokenRect.anchorMax = new Vector2(0f, 0.5f);
            tokenRect.pivot = new Vector2(0f, 0.5f);
            if (manaSprite != null)
            {
                Image tokenImage = token.GetComponent<Image>();
                tokenImage.sprite = manaSprite;
                tokenImage.preserveAspect = true;
            }
            else
            {
                Text symbol = CreateText(tokenRect, "Symbol", ManaSymbols[i], 17, font, TextAnchor.MiddleCenter, Color.white);
                Stretch(symbol.rectTransform, 2f);
                AddTextOutline(symbol, new Color(0f, 0f, 0f, 0.82f), 1f);
            }

            Text count = CreateText(itemRect, "Count", "0", 20, font, TextAnchor.MiddleLeft, Color.white);
            RectTransform countRect = count.rectTransform;
            countRect.anchorMin = countRect.anchorMax = new Vector2(0f, 0.5f);
            countRect.pivot = new Vector2(0f, 0.5f);
            countRect.anchoredPosition = new Vector2(39f, 1f);
            countRect.sizeDelta = new Vector2(18f, 34f);
            AddTextOutline(count, Color.black, 1f);

            manaItems[i] = item;
            manaCounts[i] = count;
            manaItemRects[i] = itemRect;
            manaTokenRects[i] = tokenRect;
        }
    }

    private void Refresh(bool force)
    {
        if (!built || gameRules == null) return;

        int localPlayer = OnlinePlayerContext.LocalPlayerId;
        OnlineMatchService online = OnlineMatchService.Instance;
        bool ruleInputAvailable = online == null || online.CanSubmitRuleAction;
        bool playerTurn = gameRules.GetCurrentPlayer() == localPlayer;
        bool gameOver = gameRules.IsGameOver();
        TurnPhase phase = gameRules.GetCurrentPhase();
        bool resolving = gameRules.HasPendingStack();
        bool choosingSpellTarget = whiteboxInteraction != null && whiteboxInteraction.IsChoosingSpellTarget;
        bool combatAnimating = gameRules.IsCombatAnimationInProgress();
        bool discardingToHandSize = gameRules.IsDiscardingToHandSize();
        bool orderingTriggers = gameRules.IsOrderingTriggeredAbilities();
        bool resolvingGenericChoice = gameRules.IsResolvingGenericChoice();
        bool choosingLegend = gameRules.IsChoosingLegendRule();
        bool choosingLibraryCard = gameRules.IsChoosingLibraryCard();
        bool choosingCastFeature = gameRules.IsChoosingCastFeature();
        GameChoiceRequest activeChoice = gameRules.GetActiveChoice();
        bool canConfirmDiscard = discardingToHandSize
            && gameRules.CanConfirmDiscardToHandSize(gameRules.GetDiscardToHandSizePlayer());
        bool canConfirmModalChoice = activeChoice != null
            && activeChoice.choosingPlayerId == localPlayer
            && gameRules.CanConfirmActiveChoice();
        bool choosingMulliganBottom = activeChoice?.kind == GameChoiceKind.MulliganBottom;
        bool hasModalChoice = orderingTriggers
            || (resolvingGenericChoice && !choosingMulliganBottom)
            || choosingLegend
            || choosingLibraryCard
            || choosingCastFeature;
        bool awaitingPriority = gameRules.IsAwaitingPriority();
        int priorityPlayer = gameRules.GetPriorityPlayer();
        int turnNumber = gameRules.GetTurnNumber();

        string priorityLabel = awaitingPriority
            ? priorityPlayer == localPlayer ? "\u5df1\u65b9\u4f18\u5148\u6743" : "\u5bf9\u624b\u4f18\u5148\u6743"
            : string.Empty;
        bool openingMulligan = gameRules.IsOpeningMulligan();
        phaseText.text = gameOver
            ? "\u5bf9\u5c40\u7ed3\u675f"
            : openingMulligan
                ? $"开局调度\n{(gameRules.GetStartingPlayer() == localPlayer ? "你是先手" : "你是后手")}\n玩家 {gameRules.GetStartingPlayer()} 先手"
            : awaitingPriority
                ? $"\u7b2c {turnNumber} \u56de\u5408\n{GetPhaseLabel(phase)}\n{priorityLabel}"
                : $"\u7b2c {turnNumber} \u56de\u5408\n{GetPhaseLabel(phase)}";
        if (actionPromptText != null)
        {
            actionPromptText.text = !ruleInputAvailable
                ? online.InteractionState == OnlineClientInteractionState.Disconnected
                    ? online.IsReconnecting
                        ? $"连接中断，正在自动重连… 剩余 {Mathf.CeilToInt((float)online.ReconnectGraceRemainingSeconds)} 秒"
                        : "已与服务器断开连接。"
                    : online.InteractionState == OnlineClientInteractionState.Resyncing
                    ? "正在与服务器重新同步…"
                    : "正在等待服务器确认…"
                : gameOver
                ? gameRules.GetMatchOutcome().message
                : discardingToHandSize
                ? $"\u5df2\u9009 {gameRules.GetDiscardToHandSizeSelectedCount()}/{gameRules.GetDiscardToHandSizeRequiredCount()} \u5f20\u624b\u724c"
                : choosingMulliganBottom
                ? $"已选 {activeChoice.selectedOptionIds.Count}/{activeChoice.minimumSelections} 张手牌置于牌库底"
                : hasModalChoice
                ? activeChoice?.prompt ?? gameRules.GetCurrentActionPrompt(localPlayer)
                : choosingSpellTarget
                ? whiteboxInteraction.CanConfirmPendingTargetSpell
                    ? "\u786e\u8ba4\u65bd\u653e\u6216\u53d6\u6d88\uff1b\u4e0d\u80fd\u540c\u65f6\u505a\u5176\u4ed6\u6e38\u620f\u52a8\u4f5c\u3002"
                    : "\u8bf7\u9009\u62e9\u76ee\u6807\u6216\u53d6\u6d88\uff1b\u53ef\u91cd\u65b0\u70b9\u51fb\u76ee\u6807\u6539\u9009\u3002"
                : gameRules.GetCurrentActionPrompt(localPlayer);
        }
        Color ringColor = playerTurn
            ? ObsidianArenaTheme.Blue
            : ObsidianArenaTheme.Red;
        phaseRing.color = ringColor;
        phaseFill.color = playerTurn
            ? ObsidianArenaTheme.BlueDeep
            : ObsidianArenaTheme.RedDeep;
        endPhaseButton.interactable = ruleInputAvailable && !gameOver && (
            discardingToHandSize
                ? canConfirmDiscard
                : orderingTriggers || resolvingGenericChoice || choosingLegend
                    ? canConfirmModalChoice
                    : choosingLibraryCard || choosingCastFeature
                        ? false
                        : choosingSpellTarget || gameRules.CanHumanAdvance());
        bool choosingAttackers = phase == TurnPhase.DeclareAttackers
            && localPlayer == gameRules.GetCurrentPlayer()
            && !gameRules.HasDeclaredAttackers();
        bool attackersDeclared = phase == TurnPhase.DeclareAttackers
            && localPlayer == gameRules.GetCurrentPlayer()
            && gameRules.HasDeclaredAttackers();
        bool confirmingBlockers = phase == TurnPhase.DeclareBlockers
            && localPlayer != gameRules.GetCurrentPlayer();
        string priorityActionLabel = GetPriorityActionLabel(awaitingPriority, priorityPlayer, localPlayer);
        endPhaseText.text = gameOver
            ? "\u5bf9\u5c40\n\u7ed3\u675f"
            : activeChoice?.kind == GameChoiceKind.MulliganDecision
            ? "确认\n调度选择"
            : activeChoice?.kind == GameChoiceKind.MulliganBottom
            ? "确认\n置于牌库底"
            : discardingToHandSize
            ? "\u786e\u8ba4\n\u4e22\u5f03"
            : orderingTriggers
            ? "\u786e\u8ba4\n\u89e6\u53d1\u987a\u5e8f"
            : resolvingGenericChoice
            ? "\u786e\u8ba4\n\u9009\u62e9"
            : choosingLegend
            ? "\u786e\u8ba4\n\u4fdd\u7559"
            : choosingLibraryCard
            ? "\u9009\u62e9\n\u724c\u5e93"
            : choosingCastFeature
            ? "\u9009\u62e9\n\u65bd\u653e\u65b9\u5f0f"
            : combatAnimating
            ? "\u6218\u6597\n\u7ed3\u7b97\u4e2d"
             : choosingSpellTarget
             ? whiteboxInteraction.CanConfirmPendingTargetSpell ? "\u786e\u8ba4\n\u65bd\u653e" : "\u9009\u62e9\n\u76ee\u6807"
             : priorityActionLabel != null
             ? priorityActionLabel
             : resolving
             ? "\u8ba9\u8fc7\n\u4f18\u5148\u6743"
            : choosingAttackers
                ? "\u653b\u51fb\n\u5ba3\u8a00"
                : attackersDeclared
                    ? "\u8fdb\u5165\n\u963b\u6321"
                    : confirmingBlockers
                        ? "\u786e\u8ba4\n\u963b\u6321"
                        : "\u7ed3\u675f\n\u5f53\u524d\u6b65\u9aa4";
        endPhaseFill.color = discardingToHandSize
            ? canConfirmDiscard
                ? ObsidianArenaTheme.BlueDeep
                : new Color(ObsidianArenaTheme.Surface.r, ObsidianArenaTheme.Surface.g, ObsidianArenaTheme.Surface.b, 0.72f)
            : hasModalChoice
            ? endPhaseButton.interactable
                ? ObsidianArenaTheme.BlueDeep
                : new Color(ObsidianArenaTheme.Surface.r, ObsidianArenaTheme.Surface.g, ObsidianArenaTheme.Surface.b, 0.72f)
            : choosingSpellTarget
            ? ObsidianArenaTheme.BlueDeep
            : resolving || combatAnimating
            ? ObsidianArenaTheme.BlueDeep
            : ObsidianArenaTheme.RedDeep;
        bool canSkipOpponentTurn = (online == null || !online.IsInMatch)
            && !gameOver
            && !openingMulligan
            && activeChoice == null
            && !playerTurn
            && !resolving;
        skipOpponentButton.interactable = canSkipOpponentTurn;
        skipOpponentFill.color = canSkipOpponentTurn
            ? ObsidianArenaTheme.RedDeep
            : new Color(ObsidianArenaTheme.Surface.r, ObsidianArenaTheme.Surface.g, ObsidianArenaTheme.Surface.b, 0.72f);
        RefreshControlState();

        ManaPool pool = gameRules.GetManaPool(OnlinePlayerContext.LocalPlayerId);
        int activeManaColors = 0;
        for (int i = 0; i < ManaColors.Length; i++)
        {
            int amount = pool.GetAmount(ManaColors[i]);
            manaItems[i].SetActive(amount > 0);
            if (amount <= 0) continue;

            RectTransform itemRect = manaItemRects[i] != null
                ? manaItemRects[i]
                : manaItems[i].GetComponent<RectTransform>();
            itemRect.anchoredPosition = new Vector2(76f + activeManaColors * layout.manaItemSpacing, 0f);
            itemRect.sizeDelta = new Vector2(layout.manaItemWidth, itemRect.sizeDelta.y);
            manaCounts[i].text = amount.ToString();
            activeManaColors++;
        }

        manaEmptyText.gameObject.SetActive(activeManaColors == 0);
        manaPanel.sizeDelta = new Vector2(
            activeManaColors == 0 ? layout.manaEmptyWidth : layout.manaBaseWidth + activeManaColors * layout.manaItemSpacing,
            layout.manaPanelHeight);
    }

    private static string GetPriorityActionLabel(bool awaitingPriority, int priorityPlayer, int localPlayer)
    {
        if (!awaitingPriority) return null;
        return priorityPlayer == localPlayer ? "让过\n优先权" : "等待\n对手";
    }

    private void RefreshControlState()
    {
        controlText.text = fullControl ? "\u5168\u63a7" : "\u81ea\u52a8";
        controlFill.color = fullControl
            ? ObsidianArenaTheme.BlueDeep
            : ObsidianArenaTheme.Surface;
    }

    private void ToggleFullControl()
    {
        if (OnlineMatchService.Instance?.CanSubmitRuleAction == false) return;
        fullControl = !fullControl;
        if (gameRules != null)
        {
            GameActionResult result = gameRules.ApplyAction(new GameAction
            {
                type = GameActionType.SetFullControl,
                playerId = OnlinePlayerContext.LocalPlayerId,
                value = fullControl ? 1 : 0
            });
            if (!result.accepted)
            {
                fullControl = !fullControl;
            }
        }
        RefreshControlState();
        SetHint(fullControl ? "\u5df2\u5f00\u542f\u5168\u63a7\u6a21\u5f0f\u3002" : "\u5df2\u8fd4\u56de\u81ea\u52a8\u6a21\u5f0f\u3002");
    }

    private void OnEndPhaseClicked()
    {
        if (gameRules == null || OnlineMatchService.Instance?.CanSubmitRuleAction == false) return;

        if (whiteboxInteraction == null)
        {
            whiteboxInteraction = Object.FindFirstObjectByType<ArenaWhiteboxInteraction>();
        }

        if (whiteboxInteraction != null)
        {
            whiteboxInteraction.HandlePrimaryAction();
            Refresh(true);
            return;
        }

        if (gameRules.IsDiscardingToHandSize())
        {
            int playerID = gameRules.GetDiscardToHandSizePlayer();
            if (gameRules.ApplyAction(new GameAction
                {
                    type = GameActionType.ConfirmDiscard,
                    playerId = playerID
                }).accepted)
            {
                SetHint("\u5df2\u786e\u8ba4\u4e22\u5f03\u3002");
            }
            else
            {
                SetHint($"\u8bf7\u9009\u62e9 {gameRules.GetDiscardToHandSizeRequiredCount()} \u5f20\u624b\u724c\u540e\u518d\u786e\u8ba4\u3002");
            }

            Refresh(true);
            return;
        }

        TurnPhase previousPhase = gameRules.GetCurrentPhase();
        int localPlayer = OnlinePlayerContext.LocalPlayerId;
        GameActionType primaryAction = gameRules.GetPrimaryAdvanceActionType(localPlayer);
        if (primaryAction == GameActionType.CommitAttackers)
        {
            gameRules.ApplyAction(new GameAction
            {
                type = primaryAction,
                playerId = localPlayer
            });
            SetHint("\u5df2\u5ba3\u544a\u653b\u51fb\u8005\u3002");
            Refresh(true);
            return;
        }

        if (previousPhase == TurnPhase.DeclareBlockers)
        {
            string blockReason = gameRules.GetDeclareBlockersAdvanceBlockReason();
            if (!string.IsNullOrEmpty(blockReason))
            {
                SetHint(blockReason);
                Refresh(true);
                return;
            }
        }

        gameRules.ApplyAction(new GameAction
        {
            type = primaryAction,
            playerId = localPlayer
        });
        if (previousPhase == TurnPhase.DeclareBlockers
            && localPlayer != gameRules.GetCurrentPlayer())
        {
            SetHint("\u5df2\u786e\u8ba4\u963b\u6321\uff0c\u8fdb\u5165\u6218\u6597\u4f24\u5bb3\u3002");
        }
        else
        {
            SetHint("已让过优先权。");
        }
        Refresh(true);
    }

    private void OnSkipOpponentTurnClicked()
    {
        if (gameRules == null
            || OnlineMatchService.Instance?.IsInMatch == true
            || gameRules.IsOpeningMulligan()
            || gameRules.HasActiveChoice()
            || gameRules.GetCurrentPlayer() == OnlinePlayerContext.LocalPlayerId
            || gameRules.HasPendingStack()) return;
        gameRules.ApplyAction(new GameAction
        {
            type = GameActionType.EndTurn,
            playerId = OnlinePlayerContext.LocalPlayerId
        });
        Refresh(true);
    }

    private void SetHint(string value)
    {
        if (hintText != null) hintText.text = value;
    }

    private static string GetPhaseLabel(TurnPhase phase)
    {
        return GameRules.GetPhaseDisplayName(phase);
    }

    private static GameObject Circle(Transform parent, string name, Vector2 anchoredPosition, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(size, size);
        Image image = go.GetComponent<Image>();
        image.sprite = circleSprite;
        image.color = color;
        return go;
    }

    private static Text CreateText(Transform parent, string name, string value, int size, Font font, TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static void AddTextOutline(Text text, Color color, float distance)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = true;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ArenaHudCircle",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        float radius = center - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(radius - distance + 1f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "ArenaHudCircle";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
