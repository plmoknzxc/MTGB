using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ArenaWhiteboxInteraction : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform cardRoot;
    [SerializeField] private GameObject largeCardPreview;
    [SerializeField] private Button resolveButton;
    [SerializeField] private Button endTurnButton;
    [SerializeField] private Text interactionHintText;
    [SerializeField] private Image stackGlowImage;
    [SerializeField] private GameRules gameRules;
    [SerializeField] private Text playerLifeText;
    [SerializeField] private Text opponentLifeText;
    [SerializeField] private Text playerNameText;
    [SerializeField] private Text opponentNameText;
    [SerializeField] private Text phaseStatusText;
    [SerializeField] private Text stackStatusText;
    [SerializeField] private Text manaPoolText;
    [SerializeField] private Transform playerBattlefieldArea;
    [SerializeField] private Transform opponentBattlefieldArea;
    [SerializeField] private Button playerTargetButton;
    [SerializeField] private Button opponentTargetButton;

    private const string HintIdle = "\u60ac\u505c\u5361\u724c\u67e5\u770b\u9884\u89c8\uff0c\u62d6\u52a8\u624b\u724c\u5230\u5df1\u65b9\u6218\u573a\u6253\u51fa\u3002";
    private const string HintDrag = "\u62d6\u5230\u5df1\u65b9\u6218\u573a\u533a\u57df\u540e\u677e\u5f00\u9f20\u6807\u6253\u51fa\u3002";
    private const string HintResolveStack = "\u8bf7\u5148\u89e3\u51b3\u5806\u53e0\u3002";

    private ArenaMatchLayoutSettings.StackLayout stackLayout = new ArenaMatchLayoutSettings.StackLayout();
    private ArenaMatchLayoutSettings.HudLayout hudLayout = new ArenaMatchLayoutSettings.HudLayout();

    private readonly RaycastHit[] raycastHits = new RaycastHit[12];
    private Transform hoveredCard;
    private Transform selectedCard;
    private Transform draggedCard;
    private Vector3 dragStartLocalPosition;
    private Vector3 dragStartLocalEulerAngles;
    private Plane dragPlane;
    private int dragOwner;
    private int dragHandIndex;
    private bool isDragging;
    private bool choosingSpellTarget;
    private bool confirmingSpellCast;
    private int targetHandOwner;
    private int targetHandIndex = -1;
    private GameChoiceZone targetCastSourceZone = GameChoiceZone.Hand;
    private float pulseTime;
    private Canvas canvas;
    private RectTransform canvasRect;
    private RectTransform stackPanel;
    private RectTransform targetModePanel;
    private Text targetModeText;
    private RectTransform castCancelButton;
    private Button castCancelButtonComponent;
    private Text castCancelButtonText;
    private RectTransform targetArrowBody;
    private RectTransform targetArrowHead;
    private Image targetArrowBodyImage;
    private Image targetArrowHeadImage;
    private RuntimeCard pendingTargetCard;
    private int pendingTargetPlayerID;
    private MTGAHandUiCard pendingCastVisual;
    private RectTransform stackInspectButton;
    private Button stackInspectButtonComponent;
    private Text stackInspectButtonText;
    private RectTransform stackBrowserPanel;
    private RectTransform stackBrowserViewport;
    private RectTransform stackBrowserContent;
    private ScrollRect stackBrowserScrollRect;
    private Scrollbar stackBrowserScrollbar;
    private Button stackBrowserCloseButton;
    private Text stackBrowserCloseButtonText;
    private Text stackBrowserTitleText;
    private bool stackBrowserOpen;
    private BrowserZone stackBrowserZone = BrowserZone.Stack;
    private int stackBrowserOwner;
    private RectTransform stackPreviewPanel;
    private MTGAHandUiCard stackPreviewVisual;
    private readonly List<Transform> legalTargetHighlights = new List<Transform>(16);
    private readonly List<Transform> selectedTargetHighlights = new List<Transform>(8);
    private readonly List<RectTransform> extraTargetArrowBodies = new List<RectTransform>(4);
    private readonly List<RectTransform> extraTargetArrowHeads = new List<RectTransform>(4);
    private readonly List<Vector2> selectedTargetScreenCenters = new List<Vector2>(8);
    private readonly List<MTGAHandUiCard> stackCardVisuals = new List<MTGAHandUiCard>(4);
    private readonly List<RuntimeCard> stackCardRuntimeCards = new List<RuntimeCard>(4);
    private readonly List<MTGAHandUiCard> stackBrowserCardVisuals = new List<MTGAHandUiCard>(8);
    private readonly List<RuntimeCard> stackBrowserRuntimeCards = new List<RuntimeCard>(8);
    private readonly List<RuntimeCard> stackBrowserSourceCards = new List<RuntimeCard>(16);
    private readonly List<int> stackBrowserDeckIndices = new List<int>(16);
    private readonly List<ZoneButtonBinding> zoneButtons = new List<ZoneButtonBinding>(6);
    private RectTransform triggerOrderPanel;
    private RectTransform triggerOrderViewport;
    private RectTransform triggerOrderContent;
    private ScrollRect triggerOrderScrollRect;
    private Text triggerOrderTitleText;
    private readonly List<Button> triggerOrderButtons = new List<Button>(8);
    private readonly List<Text> triggerOrderButtonTexts = new List<Text>(8);
    private bool resetBrowserScrollOnNextRefresh;
    private static Sprite arrowHeadSprite;
    private static Sprite playerPortraitSprite;

    public bool IsDraggingCard => isDragging;
    public bool IsChoosingSpellTarget =>
        choosingSpellTarget
        || confirmingSpellCast
        || (gameRules != null
            && (gameRules.IsChoosingSpellTarget()
                || gameRules.IsConfirmingCast()
                || gameRules.IsChoosingCastFeature()));
    public bool CanConfirmPendingTargetSpell =>
        confirmingSpellCast
        || (choosingSpellTarget
            && gameRules != null
            && gameRules.CanConfirmActiveChoice());

    public void ApplyLayoutSettings(ArenaMatchLayoutSettings.StackLayout stack, ArenaMatchLayoutSettings.HudLayout hud)
    {
        if (stack != null) stackLayout.CopyFrom(stack);
        if (hud != null) hudLayout.CopyFrom(hud);
        stackLayout.Sanitize();
        hudLayout.Sanitize();

        EnsureStackPanel();
        EnsureTargetModePanel();
        EnsureCastCancelButton();
        EnsureTargetArrow();
        EnsurePlayerTargetButtons();
        EnsureZoneButtons();
        ApplyRuntimeLayout();
        RefreshRulesHud();
    }

    private enum BrowserZone
    {
        Stack,
        Deck,
        Graveyard,
        Exile,
        LibrarySearch
    }

    private sealed class ZoneButtonBinding
    {
        public BrowserZone Zone;
        public int Owner;
        public Button Button;
        public MTGAHandUiCard CardVisual;
        public Text CountText;
        public Text LabelText;
        public Image Image;
    }

    private void Awake()
    {
        canvas = Object.FindFirstObjectByType<Canvas>();
        canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        if (mainCamera == null) mainCamera = Camera.main;
        if (cardRoot == null) cardRoot = GameObject.Find("ArenaWhitebox")?.transform;
        if (largeCardPreview == null) largeCardPreview = GameObject.Find("ArenaLargeCardPreview");
        if (resolveButton == null) resolveButton = GameObject.Find("ArenaResolveButton")?.GetComponent<Button>();
        if (endTurnButton == null) endTurnButton = GameObject.Find("ArenaEndTurnButton")?.GetComponent<Button>();
        if (interactionHintText == null) interactionHintText = GameObject.Find("ArenaInteractionHintText")?.GetComponent<Text>();
        if (stackGlowImage == null) stackGlowImage = GameObject.Find("ArenaStackGlow")?.GetComponent<Image>();
        if (gameRules == null) gameRules = Object.FindFirstObjectByType<GameRules>();
        if (playerLifeText == null) playerLifeText = GameObject.Find("ArenaPlayerLife")?.GetComponent<Text>();
        if (opponentLifeText == null) opponentLifeText = GameObject.Find("ArenaOpponentLife")?.GetComponent<Text>();
        if (playerNameText == null) playerNameText = GameObject.Find("PlayerName")?.GetComponent<Text>();
        if (opponentNameText == null) opponentNameText = GameObject.Find("OpponentName")?.GetComponent<Text>();
        if (phaseStatusText == null) phaseStatusText = GameObject.Find("ArenaPhaseStatusText")?.GetComponent<Text>();
        if (stackStatusText == null) stackStatusText = GameObject.Find("ArenaStackStatusText")?.GetComponent<Text>();
        if (manaPoolText == null) manaPoolText = GameObject.Find("ArenaManaPoolText")?.GetComponent<Text>();
        if (playerBattlefieldArea == null) playerBattlefieldArea = cardRoot?.Find("PlayerBattlefieldArea");
        if (opponentBattlefieldArea == null) opponentBattlefieldArea = cardRoot?.Find("OpponentBattlefieldArea");
        if (playerTargetButton == null) playerTargetButton = GameObject.Find("ArenaPlayerTargetButton")?.GetComponent<Button>();
        if (opponentTargetButton == null) opponentTargetButton = GameObject.Find("ArenaOpponentTargetButton")?.GetComponent<Button>();
        EnsureStackPanel();
        EnsureTargetModePanel();
        EnsureCastCancelButton();
        EnsureTargetArrow();
        EnsurePlayerTargetButtons();
        EnsureZoneButtons();

        if (resolveButton != null) resolveButton.onClick.AddListener(OnResolveClicked);
        if (endTurnButton != null) endTurnButton.onClick.AddListener(OnEndTurnClicked);
        if (castCancelButtonComponent != null) castCancelButtonComponent.onClick.AddListener(OnCastCancelClicked);
        if (playerTargetButton != null) playerTargetButton.onClick.AddListener(OnPlayerTargetClicked);
        if (opponentTargetButton != null) opponentTargetButton.onClick.AddListener(OnOpponentTargetClicked);
        GameEvents.OnPhaseChanged += OnPhaseChanged;
        GameEvents.OnStackChanged += OnStackChanged;
        GameEvents.OnCardTapped += OnCardStateChanged;
        GameEvents.OnCardUntapped += OnCardStateChanged;

        RefreshRulesHud();
        RefreshTargetButtons();
        SetHint(HintIdle);
    }

    private void OnDestroy()
    {
        if (resolveButton != null) resolveButton.onClick.RemoveListener(OnResolveClicked);
        if (endTurnButton != null) endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        if (castCancelButtonComponent != null) castCancelButtonComponent.onClick.RemoveListener(OnCastCancelClicked);
        if (playerTargetButton != null) playerTargetButton.onClick.RemoveListener(OnPlayerTargetClicked);
        if (opponentTargetButton != null) opponentTargetButton.onClick.RemoveListener(OnOpponentTargetClicked);
        for (int i = 0; i < zoneButtons.Count; i++)
        {
            ZoneButtonBinding binding = zoneButtons[i];
            if (binding?.Button != null) binding.Button.onClick.RemoveAllListeners();
        }
        GameEvents.OnPhaseChanged -= OnPhaseChanged;
        GameEvents.OnStackChanged -= OnStackChanged;
        GameEvents.OnCardTapped -= OnCardStateChanged;
        GameEvents.OnCardUntapped -= OnCardStateChanged;
    }

    private void Update()
    {
        if (OnlineMatchService.Instance?.CanSubmitRuleAction == false)
        {
            if (isDragging) EndDrag(false, false);
            UpdatePulse();
            RefreshRulesHud();
            SetHint(OnlineMatchService.Instance.InteractionState == OnlineClientInteractionState.Resyncing
                ? "正在与服务器重新同步…"
                : "正在等待服务器确认…");
            return;
        }

        if (isDragging)
        {
            UpdateDrag();
            UpdatePulse();
            RefreshRulesHud();
            return;
        }

        UpdatePulse();
        RefreshRulesHud();

        if (gameRules != null && gameRules.IsChoosingLibraryCard())
        {
            SetCardGlow(hoveredCard, false, "InteractionHoverGlow");
            hoveredCard = null;
            selectedCard = null;

            if (Input.GetMouseButtonDown(1))
            {
                if (TryShowStackCardPreviewAtPointer()) return;
                if (HideStackPreviewIfVisible()) return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnStackBrowserCloseClicked();
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                TryHandleLibrarySearchClickAtPointer();
            }

            return;
        }

        if (gameRules != null && gameRules.IsChoosingLegendRule())
        {
            UpdateHover();

            if (Input.GetMouseButtonDown(1))
            {
                if (TryShowStackCardPreviewAtPointer()) return;
                HideStackPreviewIfVisible();
            }

            if (Input.GetMouseButtonDown(0))
            {
                TryHandleLegendRuleClick(hoveredCard);
            }

            return;
        }

        if (gameRules != null && gameRules.IsOrderingTriggeredAbilities())
        {
            SetCardGlow(hoveredCard, false, "InteractionHoverGlow");
            SetCardGlow(selectedCard, false, "InteractionSelectedGlow");
            hoveredCard = null;
            selectedCard = null;
            return;
        }

        UpdateHover();

        if (Input.GetMouseButtonDown(1))
        {
            if (TryShowStackCardPreviewAtPointer()) return;
            if (HideStackPreviewIfVisible()) return;
        }

        if (stackBrowserOpen && Input.GetMouseButtonDown(0))
        {
            TryHandleExileCastClickAtPointer();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (stackBrowserOpen)
            {
                OnStackBrowserCloseClicked();
                return;
            }

            if (HideStackPreviewIfVisible()) return;

            if (choosingSpellTarget
                || confirmingSpellCast
                || (gameRules != null && gameRules.IsChoosingCastFeature()))
            {
                if (gameRules != null && gameRules.IsChoosingCastFeature())
                {
                    gameRules.ApplyAction(new GameAction
                    {
                        type = GameActionType.CancelCast,
                        playerId = OnlinePlayerContext.LocalPlayerId
                    });
                    SetHint("\u5df2\u53d6\u6d88\u65bd\u653e\u65b9\u5f0f\u9009\u62e9\u3002");
                    RestoreHandInputAfterTargetCancel();
                    RefreshRulesHud();
                }
                else
                {
                    ClearSpellTargetSelection(confirmingSpellCast
                        ? "\u5df2\u64a4\u56de\u65bd\u653e\u3002"
                        : "\u5df2\u53d6\u6d88\u76ee\u6807\u9009\u62e9\u3002");
                }
                return;
            }

            if (gameRules != null && gameRules.IsResolvingGenericChoice())
            {
                GameChoiceRequest choice = gameRules.GetActiveChoice();
                if (choice != null
                    && choice.mayCancel
                    && gameRules.ApplyAction(new GameAction
                    {
                        type = GameActionType.CancelChoice,
                        playerId = OnlinePlayerContext.LocalPlayerId,
                        choiceRequestId = choice.requestId
                    }).accepted)
                {
                    SetHint("\u5df2\u53d6\u6d88\u9009\u62e9\u3002");
                    RefreshRulesHud();
                }
                return;
            }

            SelectCard(null);
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (gameRules != null && gameRules.IsResolvingGenericChoice())
            {
                return;
            }

            if (confirmingSpellCast)
            {
                return;
            }

            if (choosingSpellTarget)
            {
                TryHandleSpellTargetClick(hoveredCard);
                return;
            }

            if (!TryBeginDrag(hoveredCard))
            {
                if (TrySetPendingAttackTargetOnClick(hoveredCard)) return;
                if (TryTogglePendingAttackerOnClick(hoveredCard)) return;
                if (TryHandleBlockClick(hoveredCard)) return;
                if (TryActivateManaAbilityOnClick(hoveredCard)) return;
                SelectCard(hoveredCard);
            }
        }
    }

    private bool TryBeginDrag(Transform card)
    {
        if (card == null || gameRules == null) return false;
        if (choosingSpellTarget || confirmingSpellCast) return false;
        if (!TryGetHandIndex(card, out int owner, out int handIndex)) return false;
        if (!gameRules.CanPlayCard(owner, handIndex)) return false;

        SelectCard(null);
        draggedCard = card;
        dragOwner = owner;
        dragHandIndex = handIndex;
        dragStartLocalPosition = card.localPosition;
        dragStartLocalEulerAngles = card.localEulerAngles;
        dragPlane = new Plane(Vector3.up, card.position);
        isDragging = true;
        SetCardGlow(card, true, "InteractionHoverGlow");
        SetHint(HintDrag);
        MoveDraggedCardToMouse();
        return true;
    }

    private void UpdateDrag()
    {
        MoveDraggedCardToMouse();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            EndDrag(false, false);
            return;
        }

        if (Input.GetMouseButtonUp(0))
        {
            bool droppedInBattlefield = IsPointerOverBattlefield(dragOwner);
            bool canPlayNow = gameRules != null && gameRules.CanPlayCard(dragOwner, dragHandIndex);
            EndDrag(droppedInBattlefield, canPlayNow);
        }
    }

    private void MoveDraggedCardToMouse()
    {
        if (mainCamera == null || draggedCard == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (dragPlane.Raycast(ray, out float distance))
        {
            draggedCard.position = ray.GetPoint(distance);
        }
    }

    private bool IsPointerOverBattlefield(int owner)
    {
        Transform targetArea = owner == OnlinePlayerContext.LocalPlayerId ? playerBattlefieldArea : opponentBattlefieldArea;
        if (targetArea == null || draggedCard == null) return false;

        BoxCollider areaCollider = targetArea.GetComponent<BoxCollider>();
        if (areaCollider == null) return false;

        Bounds bounds = areaCollider.bounds;
        bounds.Expand(new Vector3(0.45f, 0.25f, 0.45f));
        Vector3 point = draggedCard.position;
        point.y = bounds.center.y;
        return bounds.Contains(point);
    }

    private void EndDrag(bool droppedInBattlefield, bool canPlayNow)
    {
        Transform card = draggedCard;
        int owner = dragOwner;
        int handIndex = dragHandIndex;

        isDragging = false;
        draggedCard = null;
        dragOwner = 0;
        dragHandIndex = -1;

        if (card != null)
        {
            card.localPosition = dragStartLocalPosition;
            card.localEulerAngles = dragStartLocalEulerAngles;
            SetCardGlow(card, false, "InteractionHoverGlow");
        }

        hoveredCard = null;
        selectedCard = null;

        if (droppedInBattlefield && canPlayNow)
        {
            RuntimeCard runtimeCard = gameRules.GetHandCard(owner, handIndex);
            string cardName = runtimeCard != null ? runtimeCard.Data.cardName : "\u5361\u724c";
            bool played = BeginHandCastThroughAction(owner, handIndex);
            if (played)
            {
                if (!gameRules.HasActiveChoice())
                {
                    SetHint(gameRules.HasPendingStack()
                        ? $"\u5df2\u6253\u51fa\uff1a{cardName}\uff0c\u8bf7\u70b9\u51fb\u89e3\u51b3\u5806\u53e0\u3002"
                        : $"\u5df2\u6253\u51fa\uff1a{cardName}");
                }
            }
            else
            {
                ManaCost needCost = runtimeCard != null && !runtimeCard.HasType(CardType.Land)
                    ? runtimeCard.GetManaCost() : default;
                int pool = gameRules.GetMana(owner);
                SetHint(!needCost.IsEmpty && pool < needCost.Total
                    ? $"\u6cd5\u529b\u4e0d\u8db3\uff1a\u9700\u8981{needCost.ToSymbolString()}\uff0c\u6c60\u4e2d{gameRules.GetManaPool(owner).ToDisplayString()}\u3002\u8bf7\u5148\u6a2a\u7f6e\u5730\u3002"
                    : "\u5f53\u524d\u4e0d\u80fd\u6253\u51fa\uff1a\u68c0\u67e5\u56de\u5408\u3001\u9636\u6bb5\u3001\u8d39\u7528\u6216\u5806\u53e0\u3002");
            }
        }
        else if (!droppedInBattlefield)
        {
            SetHint("\u672a\u62d6\u5230\u6218\u573a\u533a\u57df\uff0c\u5df2\u53d6\u6d88\u3002");
        }
        else
        {
            SetHint("\u5f53\u524d\u4e0d\u80fd\u6253\u51fa\uff1a\u68c0\u67e5\u56de\u5408\u3001\u9636\u6bb5\u3001\u8d39\u7528\u6216\u5806\u53e0\u3002");
        }

        UpdatePreview(null);
        RefreshRulesHud();
    }

    private void UpdateHover()
    {
        Transform newHover = RaycastCard();
        if (newHover == hoveredCard) return;

        SetCardGlow(hoveredCard, false, "InteractionHoverGlow");
        hoveredCard = newHover;
        bool hoverActionable = gameRules != null && gameRules.IsChoosingLegendRule()
            ? IsLegendRuleCardVisual(hoveredCard)
            : choosingSpellTarget
                ? IsLegalSpellTargetVisual(hoveredCard)
                : IsCardActionable(hoveredCard);
        SetCardGlow(hoveredCard, hoverActionable, "InteractionHoverGlow");
        UpdatePreview(hoveredCard != null ? hoveredCard : selectedCard);
        UpdateHint();
    }

    private Transform RaycastCard()
    {
        if (mainCamera == null || cardRoot == null) return null;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        int hitCount = Physics.RaycastNonAlloc(ray, raycastHits, 100f);
        for (int i = 0; i < hitCount; i++)
        {
            Transform card = FindCardRoot(raycastHits[i].collider.transform);
            if (card != null) return card;
        }

        return null;
    }

    private Transform FindCardRoot(Transform hit)
    {
        Transform current = hit;
        while (current != null && current != cardRoot)
        {
            if (IsInteractiveCard(current.name)) return current;
            current = current.parent;
        }

        return null;
    }

    private static bool IsInteractiveCard(string objectName)
    {
        return objectName.Contains("Card") &&
               !objectName.Contains("Glow") &&
               !objectName.Contains("Frame") &&
               !objectName.Contains("Block") &&
               !objectName.Contains("Band") &&
               !objectName.Contains("Text") &&
               !objectName.Contains("Box");
    }

    private void SelectCard(Transform card)
    {
        if (selectedCard == card) card = null;

        selectedCard = card;
        UpdatePreview(selectedCard != null ? selectedCard : hoveredCard);
        SetResolveActive(!choosingSpellTarget && (IsCardActionable(selectedCard) || (gameRules != null && gameRules.HasPendingStack())));
        UpdateHint();
    }

    private void SetCardGlow(Transform card, bool active, string glowName)
    {
        if (card == null) return;

        Transform glow = card.Find(glowName);
        if (glow != null)
        {
            glow.gameObject.SetActive(active);
        }
    }

    private void UpdatePreview(Transform card)
    {
        if (largeCardPreview == null) return;

        string title = "\u5361\u724c\u9884\u89c8";
        string rules = "\u80fd\u529b\n\u6682\u65e0\u9009\u4e2d\u5361\u724c\u3002";
        string pt = "-";
        Color artColor = new Color(0.22f, 0.25f, 0.58f, 1f);

        RuntimeCard runtimeCard = GetRuntimeCardForVisual(card);
        if (runtimeCard != null)
        {
            title = runtimeCard.Data.cardName;
            rules = runtimeCard.Data.description;
            pt = runtimeCard.Data.HasType(CardType.Creature) ? $"{runtimeCard.CurrentAttack}/{runtimeCard.CurrentDefence}" : "-";
            artColor = GetPreviewColor(runtimeCard.Data.cardTypes);
        }
        else if (card != null)
        {
            title = GetReadableCardName(card);
            rules = "\u767d\u76d2\u5361\u4f4d\uff0c\u7528\u4e8e\u5bf9\u9f50\u6218\u573a\u548c\u4ea4\u4e92\u3002";
        }

        SetText("TitleBand/Text", title);
        SetText("RulesBlock/Text", rules);
        SetText("PreviewPTBox/Text", pt);

        Transform art = largeCardPreview.transform.Find("ArtBlock");
        Image image = art != null ? art.GetComponent<Image>() : null;
        if (image != null) image.color = artColor;
    }

    private static Color GetPreviewColor(CardType cardTypes)
    {
        if (cardTypes.Has(CardType.Land)) return new Color(0.18f, 0.44f, 0.58f, 1f);
        if (cardTypes.Has(CardType.Creature)) return new Color(0.15f, 0.40f, 0.62f, 1f);
        if (cardTypes.IsInstantOrSorcery()) return new Color(0.62f, 0.18f, 0.12f, 1f);
        if (cardTypes.Has(CardType.Artifact)) return new Color(0.52f, 0.52f, 0.56f, 1f);
        if (cardTypes.Has(CardType.Enchantment) || cardTypes.Has(CardType.Planeswalker)) return new Color(0.60f, 0.42f, 0.70f, 1f);
        return new Color(0.22f, 0.25f, 0.58f, 1f);
    }

    private RuntimeCard GetRuntimeCardForVisual(Transform card)
    {
        if (gameRules == null || card == null) return null;

        if (TryGetHandIndex(card, out int handOwner, out int handIndex))
        {
            return gameRules.GetHandCard(handOwner, handIndex);
        }

        if (TryGetBattlefieldIndex(card, out int battlefieldOwner, out int battlefieldIndex))
        {
            return gameRules.GetBattlefieldCard(battlefieldOwner, battlefieldIndex);
        }

        return null;
    }

    private void UpdatePulse()
    {
        pulseTime += Time.deltaTime * 5f;
        float pulse = 0.5f + Mathf.Sin(pulseTime) * 0.5f;
        ScaleGlow(hoveredCard, "InteractionHoverGlow", Mathf.Lerp(0.98f, 1.06f, pulse));
        ScaleGlow(selectedCard, "InteractionSelectedGlow", Mathf.Lerp(1.00f, 1.08f, pulse));

        if (stackGlowImage != null)
        {
            bool stackActive = gameRules != null && gameRules.HasPendingStack();
            Color color = stackGlowImage.color;
            color.a = stackActive ? Mathf.Lerp(0.20f, 0.48f, pulse) : 0.08f;
            stackGlowImage.color = color;
        }
    }

    private static void ScaleGlow(Transform card, string glowName, float multiplier)
    {
        if (card == null) return;

        Transform glow = card.Find(glowName);
        if (glow == null || !glow.gameObject.activeSelf) return;

        float baseSize = glowName.Contains("Selected") ? 1.12f : 1.06f;
        float baseHeight = glowName.Contains("Selected") ? 0.038f : 0.036f;
        glow.localScale = new Vector3(baseSize * multiplier, baseHeight, baseSize * multiplier);
    }

    private void UpdateHint()
    {
        if (choosingSpellTarget)
        {
            RuntimeCard source = GetPendingCastSource();
            string sourceName = source != null ? source.Data.cardName : "\u8fd9\u5f20\u724c";
            string target = GetPendingTargetLabel();
            SetHint(string.IsNullOrEmpty(target)
                ? $"\u6b63\u5728\u4e3a {sourceName} \u9009\u62e9\u76ee\u6807\u3002\u70b9\u51fb\u5408\u6cd5\u5361\u724c\u76ee\u6807\u6216\u53d1\u5149\u73a9\u5bb6\u9762\u677f\u3002Esc \u64a4\u56de\u3002"
                : $"\u5df2\u4e3a {sourceName} \u9009\u62e9\u76ee\u6807\uff1a{target}\u3002\u70b9\u51fb\u786e\u8ba4\u65bd\u653e\uff0c\u6216\u6309 Esc \u64a4\u56de\u3002");
            return;
        }

        if (selectedCard != null)
        {
            WhiteboxAction selectedAction = GetSelectedAction(selectedCard);
            SetHint(selectedAction == WhiteboxAction.Block
                ? $"\u5df2\u9009\u62e9\u963b\u6321\u8005\uff1a{GetReadableCardName(selectedCard)}\u3002\u70b9\u51fb\u5bf9\u9762\u7684\u653b\u51fb\u8005\u6307\u5b9a\u963b\u6321\u3002"
                : $"\u5df2\u9009\u62e9\uff1a{GetReadableCardName(selectedCard)}\u3002\u70b9\u51fb\u53f3\u4e0b\u6309\u94ae\u6267\u884c\u3002");
        }
        else if (hoveredCard != null)
        {
            WhiteboxAction action = GetSelectedAction(hoveredCard);
            RuntimeCard hoveredRuntimeCard = GetRuntimeCardForVisual(hoveredCard);
            string actionText = "\u5f53\u524d\u4e0d\u53ef\u884c\u52a8";
            if (action == WhiteboxAction.PlayCard)
            {
                actionText = "\u62d6\u5230\u5df1\u65b9\u6218\u573a\u6253\u51fa";
            }
            else if (action == WhiteboxAction.Attack)
            {
                actionText = "\u70b9\u51fb\u9009\u62e9\u4e3a\u653b\u51fb\u8005";
            }
            else if (action == WhiteboxAction.Block)
            {
                actionText = hoveredRuntimeCard != null && hoveredRuntimeCard.IsBlocking
                    ? "\u70b9\u51fb\u53d6\u6d88\u963b\u6321"
                    : "\u70b9\u51fb\u6307\u5b9a\u963b\u6321";
            }
            else if (action == WhiteboxAction.ActivateAbility)
            {
                actionText = "\u70b9\u51fb\u9009\u62e9\u5e76\u542f\u52a8\u80fd\u529b";
            }
            SetHint($"\u9884\u89c8\uff1a{GetReadableCardName(hoveredCard)}\u3002{actionText}\u3002");
        }
        else
        {
            SetHint(HintIdle);
        }
    }

    private void SetHint(string value)
    {
        if (interactionHintText != null) interactionHintText.text = value;
    }

    private void RefreshRulesHud()
    {
        SynchronizePendingCastInteraction();
        RefreshTargetButtons();
        SetCastCancelButtonVisible(
            choosingSpellTarget
            || confirmingSpellCast
            || (gameRules != null && gameRules.IsChoosingCastFeature()));
        if (gameRules == null) return;

        SetEndTurnInteractable(gameRules.CanHumanAdvance());

        if (playerLifeText != null) playerLifeText.text = gameRules.GetLife(OnlinePlayerContext.LocalPlayerId).ToString();
        if (opponentLifeText != null) opponentLifeText.text = gameRules.GetLife(OnlinePlayerContext.OpponentPlayerId).ToString();
        if (phaseStatusText != null)
        {
            string priority = gameRules.IsAwaitingPriority()
                ? $" / P{gameRules.GetPriorityPlayer()} \u4f18\u5148\u6743"
                : string.Empty;
            phaseStatusText.text = $"P{gameRules.GetCurrentPlayer()} - {gameRules.GetCurrentPhaseLabel()}{priority}";
        }

        RefreshStackVisuals();
        RefreshZoneButtons();
        RefreshTriggerOrderPanel();

        if (manaPoolText != null)
        {
            var pool = gameRules.GetManaPool(gameRules.GetCurrentPlayer());
            manaPoolText.text = pool.IsEmpty ? "Mana: 0" : $"Mana: {pool.ToDisplayString()}";
        }

        if (gameRules.IsGameOver())
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(false);
            SetEndTurnText("\u5bf9\u5c40\u7ed3\u675f");
            SetEndTurnInteractable(false);
            SetHint(gameRules.GetMatchOutcome().message);
        }
        else if (gameRules.IsOrderingTriggeredAbilities())
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(false);
            GameChoiceRequest choice = gameRules.GetActiveChoice();
            bool canConfirm = choice != null && gameRules.CanConfirmActiveChoice();
            SetEndTurnText("\u786e\u8ba4\u89e6\u53d1\u987a\u5e8f");
            SetEndTurnInteractable(canConfirm);
        }
        else if (gameRules.IsResolvingGenericChoice())
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(false);
            GameChoiceRequest choice = gameRules.GetActiveChoice();
            SetEndTurnText("\u786e\u8ba4");
            SetEndTurnInteractable(choice != null && gameRules.CanConfirmActiveChoice());
        }
        else if (gameRules.IsChoosingLegendRule())
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(false);
            GameChoiceRequest choice = gameRules.GetActiveChoice();
            bool canConfirm = choice != null && gameRules.CanConfirmActiveChoice();
            SetEndTurnText("\u786e\u8ba4\u4fdd\u7559");
            SetEndTurnInteractable(canConfirm);
        }
        else if (gameRules.IsDiscardingToHandSize())
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(false);
            bool canConfirmDiscard = gameRules.CanConfirmDiscardToHandSize(gameRules.GetDiscardToHandSizePlayer());
            SetEndTurnText("\u786e\u8ba4\u4e22\u5f03");
            SetEndTurnInteractable(canConfirmDiscard);
        }
        else if (gameRules.IsChoosingLibraryCard())
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(false);
            SetEndTurnText("\u9009\u62e9\u724c\u5e93");
        }
        else if (gameRules.IsChoosingCastFeature())
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(false);
            SetEndTurnText("\u9009\u62e9\u65bd\u653e\u65b9\u5f0f");
            SetEndTurnInteractable(false);
        }
        else if (choosingSpellTarget || confirmingSpellCast)
        {
            SetResolveActive(CanConfirmPendingTargetSpell);
            SetEndTurnText(CanConfirmPendingTargetSpell ? "\u786e\u8ba4\u65bd\u653e" : "\u9009\u62e9\u76ee\u6807");
            if (choosingSpellTarget) RefreshTargetSelectionVisuals();
            else RefreshCastConfirmationPanel();
        }
        else if (gameRules.IsCombatAnimationInProgress())
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(false);
            SetEndTurnText("\u6218\u6597\u7ed3\u7b97\u4e2d");
        }
        else if (gameRules.IsAwaitingPriority()
            && gameRules.GetPriorityPlayer() != OnlinePlayerContext.LocalPlayerId)
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(false);
            SetEndTurnText("等待对手");
        }
        else if (gameRules.HasPendingStack())
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(true);
            SetEndTurnText("\u8ba9\u8fc7\u4f18\u5148\u6743");
        }
        else if (gameRules.GetCurrentPhase() == TurnPhase.DeclareAttackers
            && OnlinePlayerContext.LocalPlayerId == gameRules.GetCurrentPlayer()
            && !gameRules.HasDeclaredAttackers())
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(false);
            SetEndTurnText("\u653b\u51fb\u5ba3\u8a00");
        }
        else if (gameRules.GetCurrentPhase() == TurnPhase.DeclareAttackers
            && OnlinePlayerContext.LocalPlayerId == gameRules.GetCurrentPlayer())
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(false);
            SetEndTurnText("\u8fdb\u5165\u963b\u6321");
        }
        else if (gameRules.GetCurrentPhase() == TurnPhase.DeclareBlockers
            && OnlinePlayerContext.LocalPlayerId != gameRules.GetCurrentPlayer())
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(false);
            SetEndTurnText("\u786e\u8ba4\u963b\u6321");
        }
        else if (gameRules.IsAwaitingPriority())
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(false);
            SetEndTurnText("让过优先权");
        }
        else if (selectedCard == null)
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(false);
            SetEndTurnText("\u7ed3\u675f\u5f53\u524d\u6b65\u9aa4");
        }
        else
        {
            ClearTargetSelectionVisuals();
            SetResolveActive(IsCardActionable(selectedCard));
        }
    }

    private void SynchronizePendingCastInteraction()
    {
        if (gameRules == null) return;

        bool rulesChoosingTarget = gameRules.IsChoosingSpellTarget();
        bool rulesConfirmingCast = gameRules.IsConfirmingCast();
        if (!rulesChoosingTarget && !rulesConfirmingCast)
        {
            if (choosingSpellTarget || confirmingSpellCast)
            {
                ClearSpellTargetSelectionState(false);
            }
            return;
        }

        GameChoiceRequest choice = gameRules.GetActiveChoice();
        if (choice == null) return;
        if (choosingSpellTarget == rulesChoosingTarget
            && confirmingSpellCast == rulesConfirmingCast
            && targetHandOwner == choice.sourcePlayerId
            && targetHandIndex == choice.sourceZoneIndex)
        {
            return;
        }

        choosingSpellTarget = rulesChoosingTarget;
        confirmingSpellCast = rulesConfirmingCast;
        targetHandOwner = choice.sourcePlayerId;
        targetHandIndex = choice.sourceZoneIndex;
        targetCastSourceZone = choice.sourceZone;
        pendingTargetCard = gameRules.GetSelectedSpellTargetCard();
        pendingTargetPlayerID = gameRules.GetSelectedSpellTargetPlayer();
        selectedCard = null;
        SetPendingCastHandHidden(true);
        SetCastCancelButtonVisible(true);
    }

    private void RefreshStackVisuals()
    {
        EnsureStackPanel();
        if (stackPanel == null) return;
        SyncLibrarySearchBrowserState();

        IReadOnlyList<RuntimeCard> stackCards = gameRules != null ? gameRules.GetStackCards() : null;
        int stackCount = stackCards != null ? stackCards.Count : 0;
        RuntimeCard pendingCastSource = GetPendingCastSource();
        int visualStackCount = stackCount + (pendingCastSource != null ? 1 : 0);
        RefreshStackPileCards(stackCards, stackCount, pendingCastSource);
        RefreshStackInspectButton(visualStackCount);
        RefreshStackBrowser(stackCards, stackCount, pendingCastSource);

        if (stackStatusText != null)
        {
            stackStatusText.text = string.Empty;
            stackStatusText.gameObject.SetActive(false);
        }

        if (stackPreviewPanel != null && stackPreviewPanel.gameObject.activeSelf)
        {
            stackPreviewPanel.SetAsLastSibling();
        }
    }

    private RuntimeCard GetPendingCastSource()
    {
        return (choosingSpellTarget || confirmingSpellCast) && gameRules != null
            ? gameRules.GetPendingCastSourceCard()
            : null;
    }

    private void RefreshStackPileCards(IReadOnlyList<RuntimeCard> stackCards, int stackCount, RuntimeCard pendingCastSource)
    {
        bool hasPendingCast = pendingCastSource != null;
        int totalCount = stackCount + (hasPendingCast ? 1 : 0);
        int visibleCount = Mathf.Min(totalCount, stackLayout.maxVisibleCards);
        int startIndex = totalCount - visibleCount;
        int formalVisualIndex = 0;
        bool pendingVisualShown = false;
        ResizeRuntimeCardList(stackCardRuntimeCards, stackCardVisuals.Count);

        for (int i = 0; i < visibleCount; i++)
        {
            int combinedIndex = startIndex + i;
            bool isPendingCast = hasPendingCast && combinedIndex == stackCount;
            RuntimeCard card = isPendingCast ? pendingCastSource : stackCards?[combinedIndex];
            if (card == null) continue;

            MTGAHandUiCard visual;
            if (isPendingCast)
            {
                visual = GetOrCreatePendingCastVisual();
                pendingVisualShown = true;
            }
            else
            {
                visual = GetOrCreateStackCardVisual(formalVisualIndex);
                ResizeRuntimeCardList(stackCardRuntimeCards, stackCardVisuals.Count);
                stackCardRuntimeCards[formalVisualIndex] = card;
                formalVisualIndex++;
            }

            visual.gameObject.SetActive(true);
            visual.SetRuntimeCard(card, false);
            visual.SetPlayable(false);

            int depthFromTop = visibleCount - i - 1;
            ApplyStackCardLayout(visual, stackLayout.cardSize, GetStackPilePosition(depthFromTop), GetStackPileRotation(depthFromTop));
            SetUiCardGlow(visual, isPendingCast ? new Color(0f, 0.90f, 1f, 0.52f) : new Color(0.08f, 0.78f, 1f, 0.36f), depthFromTop == 0);
            visual.transform.SetAsLastSibling();
        }

        for (int i = formalVisualIndex; i < stackCardVisuals.Count; i++)
        {
            stackCardVisuals[i].gameObject.SetActive(false);
            stackCardRuntimeCards[i] = null;
        }

        if (!pendingVisualShown && pendingCastVisual != null) pendingCastVisual.gameObject.SetActive(false);
    }

    private Vector2 GetStackPilePosition(int depthFromTop)
    {
        if (depthFromTop <= 0) return stackLayout.topCardPosition;
        return stackLayout.topCardPosition + stackLayout.depthOffset * depthFromTop;
    }

    private float GetStackPileRotation(int depthFromTop)
    {
        return depthFromTop <= 0
            ? 0f
            : stackLayout.firstBackRotation + (depthFromTop - 1) * stackLayout.rotationStep;
    }

    private MTGAHandUiCard GetOrCreateStackCardVisual(int index)
    {
        while (stackCardVisuals.Count <= index)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            MTGAHandUiCard visual = MTGAUiCardFactory.CreateCard(
                stackPanel,
                $"ArenaStackCard_{stackCardVisuals.Count + 1}",
                font,
                1,
                stackCardVisuals.Count,
                stackLayout.cardSize);
            stackCardVisuals.Add(visual);
            stackCardRuntimeCards.Add(null);
        }

        return stackCardVisuals[index];
    }

    private MTGAHandUiCard GetOrCreatePendingCastVisual()
    {
        if (pendingCastVisual != null) return pendingCastVisual;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        pendingCastVisual = MTGAUiCardFactory.CreateCard(
            stackPanel,
            "ArenaPendingCastCard",
            font,
            1,
            0,
            stackLayout.cardSize);
        return pendingCastVisual;
    }

    private void RefreshStackInspectButton(int stackCount)
    {
        EnsureStackInspectButton();
        if (stackInspectButton == null || stackInspectButtonText == null) return;

        bool visible = stackCount > stackLayout.inspectThreshold;
        stackInspectButton.gameObject.SetActive(visible);
        if (!visible)
        {
            if (stackBrowserOpen && stackBrowserZone == BrowserZone.Stack) CloseStackBrowser();
            return;
        }

        stackInspectButtonText.text = $"{stackCount}  \u203a";
        stackInspectButton.SetAsLastSibling();
    }

    private void RefreshStackBrowser(IReadOnlyList<RuntimeCard> stackCards, int stackCount, RuntimeCard pendingCastSource)
    {
        if (!stackBrowserOpen)
        {
            if (stackBrowserPanel != null) stackBrowserPanel.gameObject.SetActive(false);
            for (int i = 0; i < stackBrowserCardVisuals.Count; i++)
            {
                stackBrowserCardVisuals[i].gameObject.SetActive(false);
            }
            return;
        }

        EnsureStackBrowserPanel();
        if (stackBrowserPanel == null) return;
        ApplyRuntimeLayout();

        stackBrowserPanel.gameObject.SetActive(true);
        stackBrowserPanel.SetAsLastSibling();

        BuildBrowserSourceCards(stackCards, stackCount, pendingCastSource);
        int visibleCount = stackBrowserSourceCards.Count;
        if (visibleCount == 0)
        {
            if (IsLibrarySearchBrowserActive()) OnStackBrowserCloseClicked();
            else CloseStackBrowser();
            return;
        }

        if (stackBrowserTitleText != null) stackBrowserTitleText.text = GetBrowserTitle(visibleCount);
        if (stackBrowserCloseButtonText != null)
        {
            stackBrowserCloseButtonText.text = IsLibrarySearchBrowserActive() ? "\u786e\u8ba4" : "\u5b8c\u6210";
        }
        bool canConfirmLibrarySearch = !IsLibrarySearchBrowserActive()
            || gameRules.CanConfirmLibrarySearch(stackBrowserOwner);
        if (stackBrowserCloseButton != null)
        {
            stackBrowserCloseButton.interactable = canConfirmLibrarySearch;
            Image closeImage = stackBrowserCloseButton.targetGraphic as Image;
            if (closeImage != null)
            {
                closeImage.color = canConfirmLibrarySearch
                    ? new Color(1f, 0.42f, 0.08f, 0.98f)
                    : new Color(0.30f, 0.18f, 0.12f, 0.72f);
            }
        }
        ResizeRuntimeCardList(stackBrowserRuntimeCards, stackBrowserCardVisuals.Count);

        float cardWidth = stackLayout.browserCardSize.x;
        float cardHeight = stackLayout.browserCardSize.y;
        float spacing = stackLayout.browserCardSpacing;
        float margin = stackLayout.browserMargin;
        float viewportWidth = stackBrowserViewport != null && stackBrowserViewport.rect.width > 1f
            ? stackBrowserViewport.rect.width
            : stackLayout.browserViewportSize.x;
        float contentWidth = Mathf.Max(viewportWidth, margin * 2f + cardWidth + Mathf.Max(0, visibleCount - 1) * spacing);
        if (stackBrowserContent != null) stackBrowserContent.sizeDelta = new Vector2(contentWidth, stackLayout.browserContentHeight);
        bool needsScroll = contentWidth > viewportWidth + 1f;
        if (stackBrowserScrollbar != null) stackBrowserScrollbar.gameObject.SetActive(needsScroll);

        float startX = needsScroll
            ? margin + cardWidth * 0.5f
            : contentWidth * 0.5f - (visibleCount - 1) * spacing * 0.5f;
        for (int i = 0; i < visibleCount; i++)
        {
            RuntimeCard card = stackBrowserSourceCards[i];
            if (card == null) continue;

            MTGAHandUiCard visual = GetOrCreateStackBrowserCardVisual(i);
            ResizeRuntimeCardList(stackBrowserRuntimeCards, stackBrowserCardVisuals.Count);
            stackBrowserRuntimeCards[i] = card;
            visual.gameObject.SetActive(true);
            visual.SetRuntimeCard(card, false);
            visual.SetPlayable(false);
            CanvasGroup canvasGroup = visual.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = visual.gameObject.AddComponent<CanvasGroup>();
            int deckIndex = GetBrowserDeckIndex(i);
            bool isLegalLibraryChoice = IsLibrarySearchBrowserActive() && gameRules != null
                && gameRules.CanSelectLibrarySearchCard(stackBrowserOwner, deckIndex);
            bool isSelectedLibraryChoice = isLegalLibraryChoice
                && gameRules.GetLibrarySearchSelectedDeckIndex() == deckIndex;
            bool isCastableExileCard = stackBrowserZone == BrowserZone.Exile
                && stackBrowserOwner == OnlinePlayerContext.LocalPlayerId
                && gameRules != null
                && gameRules.CanCastExileCard(stackBrowserOwner, deckIndex);
            canvasGroup.alpha = IsLibrarySearchBrowserActive() && !isLegalLibraryChoice ? 0.22f : 1f;
            canvasGroup.blocksRaycasts = true;

            RectTransform rect = visual.transform as RectTransform;
            if (rect == null) continue;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(cardWidth, cardHeight);
            rect.anchoredPosition = new Vector2(startX + i * spacing, 0f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            bool glowActive = (stackBrowserZone == BrowserZone.Stack && i == visibleCount - 1)
                || isLegalLibraryChoice
                || isCastableExileCard;
            Color glowColor = isSelectedLibraryChoice
                ? new Color(1f, 0.74f, 0.08f, 0.78f)
                : GetBrowserCardGlowColor(card, pendingCastSource, isLegalLibraryChoice);
            SetUiCardGlow(visual, glowColor, glowActive);
            visual.transform.SetAsLastSibling();
        }

        for (int i = visibleCount; i < stackBrowserCardVisuals.Count; i++)
        {
            stackBrowserCardVisuals[i].gameObject.SetActive(false);
            stackBrowserRuntimeCards[i] = null;
            CanvasGroup canvasGroup = stackBrowserCardVisuals[i].GetComponent<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        if (resetBrowserScrollOnNextRefresh && stackBrowserScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            stackBrowserScrollRect.horizontalNormalizedPosition = stackBrowserZone == BrowserZone.Stack ? 1f : 0f;
            resetBrowserScrollOnNextRefresh = false;
        }

        if (stackBrowserCloseButton != null) stackBrowserCloseButton.transform.SetAsLastSibling();
    }

    private MTGAHandUiCard GetOrCreateStackBrowserCardVisual(int index)
    {
        while (stackBrowserCardVisuals.Count <= index)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            MTGAHandUiCard visual = MTGAUiCardFactory.CreateCard(
                stackBrowserContent != null ? stackBrowserContent : stackBrowserPanel,
                $"ArenaStackBrowserCard_{stackBrowserCardVisuals.Count + 1}",
                font,
                1,
                stackBrowserCardVisuals.Count,
                stackLayout.browserCardSize);
            stackBrowserCardVisuals.Add(visual);
            stackBrowserRuntimeCards.Add(null);
        }

        MTGAHandUiCard existing = stackBrowserCardVisuals[index];
        if (stackBrowserContent != null && existing != null && existing.transform.parent != stackBrowserContent)
        {
            existing.transform.SetParent(stackBrowserContent, false);
        }

        return existing;
    }

    private IReadOnlyList<RuntimeCard> GetZoneCards(int owner, BrowserZone zone)
    {
        if (gameRules == null) return new List<RuntimeCard>(0);

        switch (zone)
        {
            case BrowserZone.Deck:
                return gameRules.GetVisibleZoneCards(
                    OnlinePlayerContext.LocalPlayerId,
                    owner,
                    GameZone.Library);
            case BrowserZone.LibrarySearch:
                return gameRules.GetDeckCards(owner);
            case BrowserZone.Graveyard:
                return gameRules.GetGraveyardCards(owner);
            case BrowserZone.Exile:
                return gameRules.GetExileCards(owner);
            default:
                return gameRules.GetStackCards();
        }
    }

    private int GetZoneCount(int owner, BrowserZone zone)
    {
        if (gameRules == null) return 0;

        switch (zone)
        {
            case BrowserZone.Deck:
                return gameRules.GetDeckCount(owner);
            case BrowserZone.Graveyard:
                return gameRules.GetGraveyardCount(owner);
            case BrowserZone.Exile:
                return gameRules.GetExileCount(owner);
            case BrowserZone.LibrarySearch:
                return gameRules.GetDeckCount(owner);
            default:
                return gameRules.GetStackCount();
        }
    }

    private static string GetZoneDisplayName(BrowserZone zone)
    {
        switch (zone)
        {
            case BrowserZone.Deck:
                return "\u724c\u5e93";
            case BrowserZone.Graveyard:
                return "\u575f\u5893\u573a";
            case BrowserZone.Exile:
                return "\u653e\u9010\u533a";
            case BrowserZone.LibrarySearch:
                return "\u724c\u5e93";
            default:
                return "\u5806\u53e0";
        }
    }

    private void BuildBrowserSourceCards(IReadOnlyList<RuntimeCard> stackCards, int stackCount, RuntimeCard pendingCastSource)
    {
        stackBrowserSourceCards.Clear();
        stackBrowserDeckIndices.Clear();

        if (stackBrowserZone == BrowserZone.Stack)
        {
            if (stackCards != null)
            {
                for (int i = 0; i < stackCount; i++)
                {
                    if (stackCards[i] != null)
                    {
                        stackBrowserSourceCards.Add(stackCards[i]);
                        stackBrowserDeckIndices.Add(-1);
                    }
                }
            }

            if (pendingCastSource != null)
            {
                stackBrowserSourceCards.Add(pendingCastSource);
                stackBrowserDeckIndices.Add(-1);
            }
            return;
        }

        if (gameRules == null) return;
        IReadOnlyList<RuntimeCard> source = GetZoneCards(stackBrowserOwner, stackBrowserZone);
        if (stackBrowserZone == BrowserZone.LibrarySearch)
        {
            AddLibrarySearchCards(source, true);
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
            {
                stackBrowserSourceCards.Add(source[i]);
                stackBrowserDeckIndices.Add(i);
            }
        }
    }

    private void AddLibrarySearchCards(IReadOnlyList<RuntimeCard> source, bool legalCards)
    {
        if (source == null || gameRules == null) return;

        for (int deckIndex = 0; deckIndex < source.Count; deckIndex++)
        {
            RuntimeCard card = source[deckIndex];
            if (card == null) continue;

            bool isLegal = gameRules.CanSelectLibrarySearchCard(stackBrowserOwner, deckIndex);
            if (isLegal != legalCards) continue;

            stackBrowserSourceCards.Add(card);
            stackBrowserDeckIndices.Add(deckIndex);
        }
    }

    private int GetBrowserDeckIndex(int visualIndex)
    {
        return visualIndex >= 0 && visualIndex < stackBrowserDeckIndices.Count
            ? stackBrowserDeckIndices[visualIndex]
            : -1;
    }

    private string GetBrowserTitle(int visibleCount)
    {
        if (stackBrowserZone == BrowserZone.Stack) return $"\u5806\u53e0 {visibleCount}";
        if (stackBrowserZone == BrowserZone.LibrarySearch) return "\u9009\u62e9\u4e00\u5f20\u57fa\u672c\u5730";

        string owner = stackBrowserOwner == OnlinePlayerContext.LocalPlayerId ? "\u5df1\u65b9" : "\u5bf9\u624b";
        return $"{owner}{GetZoneDisplayName(stackBrowserZone)} {visibleCount}";
    }

    private Color GetBrowserCardGlowColor(RuntimeCard card, RuntimeCard pendingCastSource, bool legalLibraryChoice = false)
    {
        if (legalLibraryChoice)
        {
            return new Color(0.06f, 0.94f, 1f, 0.62f);
        }

        if (stackBrowserZone == BrowserZone.Stack && ReferenceEquals(card, pendingCastSource))
        {
            return new Color(0f, 0.90f, 1f, 0.46f);
        }

        return new Color(0.08f, 0.78f, 1f, 0.34f);
    }

    private void SetStackBrowserOpen(bool open)
    {
        if (IsLibrarySearchBrowserActive()) return;
        stackBrowserZone = BrowserZone.Stack;
        stackBrowserOwner = 0;
        stackBrowserOpen = open;
        if (open) resetBrowserScrollOnNextRefresh = true;
        RefreshStackVisuals();
    }

    private void OpenZoneBrowser(int owner, BrowserZone zone)
    {
        if (IsLibrarySearchBrowserActive()) return;
        if (gameRules == null || GetZoneCount(owner, zone) <= 0) return;
        if (zone == BrowserZone.Deck
            && !gameRules.CanPlayerInspectZone(
                OnlinePlayerContext.LocalPlayerId,
                owner,
                GameZone.Library))
        {
            SetHint("牌库是隐藏区域，只能在搜寻牌库时查看。");
            return;
        }

        stackBrowserZone = zone;
        stackBrowserOwner = owner;
        stackBrowserOpen = true;
        resetBrowserScrollOnNextRefresh = true;
        RefreshStackVisuals();
    }

    private void CloseStackBrowser()
    {
        stackBrowserOpen = false;
        if (stackBrowserPanel != null) stackBrowserPanel.gameObject.SetActive(false);
        for (int i = 0; i < stackBrowserCardVisuals.Count; i++)
        {
            if (stackBrowserCardVisuals[i] != null) stackBrowserCardVisuals[i].gameObject.SetActive(false);
            if (i < stackBrowserRuntimeCards.Count) stackBrowserRuntimeCards[i] = null;
        }
    }

    private bool IsLibrarySearchBrowserActive()
    {
        return gameRules != null
            && gameRules.IsChoosingLibraryCard()
            && gameRules.GetLibrarySearchPlayer() == OnlinePlayerContext.LocalPlayerId
            && stackBrowserZone == BrowserZone.LibrarySearch;
    }

    private void SyncLibrarySearchBrowserState()
    {
        if (gameRules == null
            || !gameRules.IsChoosingLibraryCard()
            || gameRules.GetLibrarySearchPlayer() != OnlinePlayerContext.LocalPlayerId)
        {
            if (stackBrowserZone == BrowserZone.LibrarySearch)
            {
                CloseStackBrowser();
            }

            return;
        }

        stackBrowserZone = BrowserZone.LibrarySearch;
        stackBrowserOwner = gameRules.GetLibrarySearchPlayer();
        if (!stackBrowserOpen) resetBrowserScrollOnNextRefresh = true;
        stackBrowserOpen = true;
    }

    private void OnStackBrowserCloseClicked()
    {
        if (IsLibrarySearchBrowserActive())
        {
            if (!IsRuleInputAvailable()) return;
            int player = gameRules.GetLibrarySearchPlayer();
            bool selectedCard = gameRules.HasLibrarySearchSelection();
            if (gameRules.ApplyAction(new GameAction
                {
                    type = GameActionType.ConfirmLibrarySearch,
                    playerId = player
                }).accepted)
            {
                SetHint(selectedCard ? "\u5df2\u786e\u8ba4\u627e\u724c\uff0c\u5df2\u6d17\u724c\u3002" : "\u672a\u9009\u62e9\u724c\uff0c\u5df2\u6d17\u724c\u3002");
                CloseStackBrowser();
            }
            else
            {
                SetHint("\u8be5\u641c\u7d22\u5fc5\u987b\u9009\u62e9\u4e00\u5f20\u5408\u6cd5\u5361\u724c\u540e\u624d\u80fd\u786e\u8ba4\u3002");
            }
        }

        if (!IsLibrarySearchBrowserActive()) CloseStackBrowser();
        RefreshRulesHud();
    }

    private bool TryHandleLibrarySearchClickAtPointer()
    {
        if (!IsLibrarySearchBrowserActive()) return false;

        int index = GetRuntimeCardIndexUnderPointer(stackBrowserCardVisuals, stackBrowserRuntimeCards);
        if (index < 0)
        {
            SetHint(gameRules.GetLibrarySearchPrompt());
            return false;
        }

        RuntimeCard preview = index < stackBrowserRuntimeCards.Count ? stackBrowserRuntimeCards[index] : null;
        string cardName = preview != null && preview.Data != null ? preview.Data.cardName : "\u8be5\u724c";
        int player = gameRules.GetLibrarySearchPlayer();
        int deckIndex = GetBrowserDeckIndex(index);
        if (!gameRules.CanSelectLibrarySearchCard(player, deckIndex))
        {
            SetHint($"{cardName} \u4e0d\u662f\u6b64\u6548\u679c\u53ef\u4ee5\u627e\u7684\u57fa\u672c\u5730\u3002");
            return true;
        }

        if (gameRules.ApplyAction(new GameAction
            {
                type = GameActionType.ToggleLibraryCard,
                playerId = player,
                zoneIndex = deckIndex
            }).accepted)
        {
            bool selected = gameRules.GetLibrarySearchSelectedDeckIndex() == deckIndex;
            SetHint(selected
                ? $"\u5df2\u9009\u62e9 {cardName}\uff0c\u70b9\u51fb\u786e\u8ba4\u6267\u884c\u3002"
                : $"\u5df2\u53d6\u6d88 {cardName}\uff0c\u53ef\u4ee5\u91cd\u65b0\u9009\u62e9\u3002");
            return true;
        }

        SetHint($"\u65e0\u6cd5\u9009\u62e9 {cardName}\u3002");
        return true;
    }

    private bool TryHandleExileCastClickAtPointer()
    {
        if (stackBrowserZone != BrowserZone.Exile
            || stackBrowserOwner != OnlinePlayerContext.LocalPlayerId
            || gameRules == null)
        {
            return false;
        }

        int visualIndex = GetRuntimeCardIndexUnderPointer(stackBrowserCardVisuals, stackBrowserRuntimeCards);
        if (visualIndex < 0) return false;

        int exileIndex = GetBrowserDeckIndex(visualIndex);
        RuntimeCard source = gameRules.GetExileCard(stackBrowserOwner, exileIndex);
        if (source == null) return false;

        string issue = gameRules.GetExileCardCastIssue(stackBrowserOwner, exileIndex);
        if (!string.IsNullOrEmpty(issue))
        {
            SetHint(issue);
            return true;
        }

        bool requiresTarget = gameRules.RequiresTarget(source);
        if (!gameRules.ApplyAction(new GameAction
            {
                type = GameActionType.BeginExileCast,
                playerId = stackBrowserOwner,
                zoneIndex = exileIndex
            }).accepted)
        {
            SetHint(gameRules.GetExileCardCastIssue(stackBrowserOwner, exileIndex));
            return true;
        }

        int owner = stackBrowserOwner;
        string cardName = source.GetCardName();
        CloseStackBrowser();
        if (requiresTarget)
        {
            BeginSpellTargetSelection(owner, exileIndex, cardName, GameChoiceZone.Exile);
        }
        else
        {
            BeginCastConfirmation(owner, exileIndex, cardName, GameChoiceZone.Exile);
        }
        return true;
    }

    private int GetRuntimeCardIndexUnderPointer(List<MTGAHandUiCard> visuals, List<RuntimeCard> cards)
    {
        int count = Mathf.Min(visuals.Count, cards.Count);
        for (int i = count - 1; i >= 0; i--)
        {
            MTGAHandUiCard visual = visuals[i];
            if (visual == null || !visual.gameObject.activeInHierarchy) continue;
            if (!IsPointerOverRect(visual.transform as RectTransform)) continue;
            return i;
        }

        return -1;
    }

    private void EnsureZoneButtons()
    {
        EnsureCanvasReferences();
        if (canvas == null || zoneButtons.Count > 0) return;

        CreateZoneButton(1, BrowserZone.Deck, "\u724c\u5e93", new Vector2(0f, 0f), hudLayout.playerDeckPosition);
        CreateZoneButton(1, BrowserZone.Graveyard, "\u575f\u573a", new Vector2(0f, 0f), hudLayout.playerGraveyardPosition);
        CreateZoneButton(1, BrowserZone.Exile, "\u653e\u9010", new Vector2(0f, 0f), hudLayout.playerExilePosition);
        CreateZoneButton(2, BrowserZone.Deck, "\u724c\u5e93", new Vector2(1f, 1f), hudLayout.opponentDeckPosition);
        CreateZoneButton(2, BrowserZone.Graveyard, "\u575f\u573a", new Vector2(1f, 1f), hudLayout.opponentGraveyardPosition);
        CreateZoneButton(2, BrowserZone.Exile, "\u653e\u9010", new Vector2(1f, 1f), hudLayout.opponentExilePosition);
    }

    private void CreateZoneButton(int owner, BrowserZone zone, string label, Vector2 anchor, Vector2 position)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject buttonObject = new GameObject($"ArenaP{owner}{zone}ZoneButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        buttonObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = hudLayout.zoneButtonSize;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.006f, 0.012f, 0.018f, 0.78f);
        image.raycastTarget = true;

        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = GetPlayerUiColor(owner, 0.52f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        int capturedOwner = owner;
        BrowserZone capturedZone = zone;
        button.onClick.AddListener(() => OpenZoneBrowser(capturedOwner, capturedZone));

        MTGAHandUiCard zoneCard = MTGAUiCardFactory.CreateCard(buttonObject.transform, "ZoneCard", font, owner, 0, new Vector2(72f, 102f));
        RectTransform cardRect = zoneCard.transform as RectTransform;
        if (cardRect != null)
        {
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(72f, 102f);
            cardRect.localRotation = Quaternion.identity;
        }
        zoneCard.SetPlayable(false);

        Text labelText = CreateZoneOverlayText(buttonObject.transform, "Label", label, font, 14, TextAnchor.MiddleCenter);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 7f);
        labelRect.sizeDelta = new Vector2(0f, 22f);

        Text countText = CreateZoneOverlayText(buttonObject.transform, "Count", "0", font, 16, TextAnchor.MiddleCenter);
        RectTransform countRect = countText.rectTransform;
        countRect.anchorMin = countRect.anchorMax = new Vector2(1f, 0f);
        countRect.pivot = new Vector2(1f, 0f);
        countRect.anchoredPosition = new Vector2(-2f, 2f);
        countRect.sizeDelta = new Vector2(30f, 24f);

        zoneButtons.Add(new ZoneButtonBinding
        {
            Zone = zone,
            Owner = owner,
            Button = button,
            CardVisual = zoneCard,
            CountText = countText,
            LabelText = labelText,
            Image = image
        });
    }

    private static Text CreateZoneOverlayText(Transform parent, string name, string value, Font font, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.text = value;
        text.raycastTarget = false;
        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, 1f);
        return text;
    }

    private void RefreshZoneButtons()
    {
        EnsureZoneButtons();
        for (int i = 0; i < zoneButtons.Count; i++)
        {
            ZoneButtonBinding binding = zoneButtons[i];
            if (binding == null || binding.Button == null) continue;

            int count = GetZoneCount(binding.Owner, binding.Zone);
            binding.Button.interactable = count > 0;
            if (binding.CountText != null) binding.CountText.text = count.ToString();
            if (binding.LabelText != null) binding.LabelText.text = GetZoneShortName(binding.Zone);
            RuntimeCard topCard = GetZoneTopCard(binding.Owner, binding.Zone);
            if (binding.CardVisual != null)
            {
                binding.CardVisual.gameObject.SetActive(topCard != null);
                if (topCard != null)
                {
                    binding.CardVisual.SetRuntimeCard(topCard, binding.Zone == BrowserZone.Deck);
                    binding.CardVisual.SetPlayable(false);
                }
            }

            if (binding.Image != null)
            {
                Color baseColor = Color.Lerp(new Color(0.006f, 0.012f, 0.018f, 0.76f), GetPlayerUiColor(binding.Owner, 0.92f), count > 0 ? 0.24f : 0.08f);
                baseColor.a = count > 0 ? 0.88f : 0.42f;
                binding.Image.color = baseColor;
            }
        }
    }

    private RuntimeCard GetZoneTopCard(int owner, BrowserZone zone)
    {
        IReadOnlyList<RuntimeCard> cards = GetZoneCards(owner, zone);
        if (cards == null || cards.Count == 0) return null;
        return zone == BrowserZone.Deck ? cards[0] : cards[cards.Count - 1];
    }

    private static string GetZoneShortName(BrowserZone zone)
    {
        switch (zone)
        {
            case BrowserZone.Deck:
                return "\u724c\u5e93";
            case BrowserZone.Graveyard:
                return "\u575f\u573a";
            case BrowserZone.Exile:
                return "\u653e\u9010";
            default:
                return "\u5806\u53e0";
        }
    }

    private void EnsureStackInspectButton()
    {
        EnsureStackPanel();
        if (stackPanel == null || stackInspectButton != null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject buttonObject = new GameObject("ArenaStackInspectButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(stackPanel, false);
        stackInspectButton = buttonObject.GetComponent<RectTransform>();
        stackInspectButton.anchorMin = stackInspectButton.anchorMax = new Vector2(1f, 0.5f);
        stackInspectButton.pivot = new Vector2(1f, 0.5f);
        stackInspectButton.anchoredPosition = stackLayout.inspectButtonPosition;
        stackInspectButton.sizeDelta = stackLayout.inspectButtonSize;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.02f, 0.12f, 0.18f, 0.72f);
        image.raycastTarget = true;

        stackInspectButtonComponent = buttonObject.GetComponent<Button>();
        stackInspectButtonComponent.targetGraphic = image;
        stackInspectButtonComponent.transition = Selectable.Transition.None;
        stackInspectButtonComponent.onClick.AddListener(() => SetStackBrowserOpen(!stackBrowserOpen));

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
        textObject.transform.SetParent(buttonObject.transform, false);
        stackInspectButtonText = textObject.GetComponent<Text>();
        stackInspectButtonText.font = font;
        stackInspectButtonText.fontSize = 20;
        stackInspectButtonText.alignment = TextAnchor.MiddleCenter;
        stackInspectButtonText.color = Color.white;
        stackInspectButtonText.raycastTarget = false;
        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, 1f);

        RectTransform textRect = stackInspectButtonText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        buttonObject.SetActive(false);
    }

    private void EnsureStackBrowserPanel()
    {
        EnsureCanvasReferences();
        if (canvas == null || stackBrowserPanel != null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject panelObject = new GameObject("ArenaStackBrowserPanel", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        panelObject.transform.SetParent(canvas.transform, false);
        stackBrowserPanel = panelObject.GetComponent<RectTransform>();
        stackBrowserPanel.anchorMin = Vector2.zero;
        stackBrowserPanel.anchorMax = Vector2.one;
        stackBrowserPanel.offsetMin = Vector2.zero;
        stackBrowserPanel.offsetMax = Vector2.zero;
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.62f);
        panelImage.raycastTarget = true;

        stackBrowserScrollRect = panelObject.GetComponent<ScrollRect>();
        stackBrowserScrollRect.horizontal = true;
        stackBrowserScrollRect.vertical = false;
        stackBrowserScrollRect.movementType = ScrollRect.MovementType.Clamped;
        stackBrowserScrollRect.inertia = true;
        stackBrowserScrollRect.scrollSensitivity = 26f;

        GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(Text), typeof(Outline));
        titleObject.transform.SetParent(panelObject.transform, false);
        stackBrowserTitleText = titleObject.GetComponent<Text>();
        stackBrowserTitleText.font = font;
        stackBrowserTitleText.fontSize = 58;
        stackBrowserTitleText.alignment = TextAnchor.MiddleCenter;
        stackBrowserTitleText.color = new Color(0.88f, 0.96f, 1f, 0.96f);
        stackBrowserTitleText.raycastTarget = false;
        Outline titleOutline = titleObject.GetComponent<Outline>();
        titleOutline.effectColor = Color.black;
        titleOutline.effectDistance = new Vector2(2f, 2f);
        RectTransform titleRect = stackBrowserTitleText.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -64f);
        titleRect.sizeDelta = new Vector2(480f, 76f);

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(panelObject.transform, false);
        stackBrowserViewport = viewportObject.GetComponent<RectTransform>();
        stackBrowserViewport.anchorMin = stackBrowserViewport.anchorMax = new Vector2(0.5f, 0.5f);
        stackBrowserViewport.pivot = new Vector2(0.5f, 0.5f);
        stackBrowserViewport.anchoredPosition = stackLayout.browserViewportPosition;
        stackBrowserViewport.sizeDelta = stackLayout.browserViewportSize;
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;
        Mask viewportMask = viewportObject.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewportObject.transform, false);
        stackBrowserContent = contentObject.GetComponent<RectTransform>();
        stackBrowserContent.anchorMin = new Vector2(0f, 0.5f);
        stackBrowserContent.anchorMax = new Vector2(0f, 0.5f);
        stackBrowserContent.pivot = new Vector2(0f, 0.5f);
        stackBrowserContent.anchoredPosition = Vector2.zero;
        stackBrowserContent.sizeDelta = new Vector2(1320f, 360f);

        GameObject scrollbarObject = new GameObject("HorizontalScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarObject.transform.SetParent(panelObject.transform, false);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = scrollbarRect.anchorMax = new Vector2(0.5f, 0f);
        scrollbarRect.pivot = new Vector2(0.5f, 0f);
        scrollbarRect.anchoredPosition = new Vector2(0f, 136f);
        scrollbarRect.sizeDelta = new Vector2(980f, 18f);
        Image scrollbarImage = scrollbarObject.GetComponent<Image>();
        scrollbarImage.color = new Color(0.02f, 0.10f, 0.14f, 0.78f);
        stackBrowserScrollbar = scrollbarObject.GetComponent<Scrollbar>();
        stackBrowserScrollbar.direction = Scrollbar.Direction.LeftToRight;

        GameObject handleAreaObject = new GameObject("Sliding Area", typeof(RectTransform));
        handleAreaObject.transform.SetParent(scrollbarObject.transform, false);
        RectTransform handleAreaRect = handleAreaObject.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(8f, 2f);
        handleAreaRect.offsetMax = new Vector2(-8f, -2f);

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(handleAreaObject.transform, false);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;
        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = new Color(0.08f, 0.78f, 1f, 0.88f);
        stackBrowserScrollbar.handleRect = handleRect;
        stackBrowserScrollbar.targetGraphic = handleImage;
        stackBrowserScrollbar.gameObject.SetActive(false);
        stackBrowserScrollRect.viewport = stackBrowserViewport;
        stackBrowserScrollRect.content = stackBrowserContent;
        stackBrowserScrollRect.horizontalScrollbar = stackBrowserScrollbar;
        stackBrowserScrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        GameObject closeObject = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObject.transform.SetParent(panelObject.transform, false);
        RectTransform closeRect = closeObject.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 52f);
        closeRect.sizeDelta = new Vector2(280f, 70f);
        Image closeImage = closeObject.GetComponent<Image>();
        closeImage.color = new Color(1f, 0.42f, 0.08f, 0.98f);
        stackBrowserCloseButton = closeObject.GetComponent<Button>();
        stackBrowserCloseButton.targetGraphic = closeImage;
        stackBrowserCloseButton.transition = Selectable.Transition.None;
        stackBrowserCloseButton.onClick.AddListener(OnStackBrowserCloseClicked);

        GameObject closeTextObject = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
        closeTextObject.transform.SetParent(closeObject.transform, false);
        stackBrowserCloseButtonText = closeTextObject.GetComponent<Text>();
        stackBrowserCloseButtonText.font = font;
        stackBrowserCloseButtonText.text = "\u5b8c\u6210";
        stackBrowserCloseButtonText.fontSize = 30;
        stackBrowserCloseButtonText.alignment = TextAnchor.MiddleCenter;
        stackBrowserCloseButtonText.color = Color.white;
        stackBrowserCloseButtonText.raycastTarget = false;
        Outline closeOutline = closeTextObject.GetComponent<Outline>();
        closeOutline.effectColor = Color.black;
        closeOutline.effectDistance = new Vector2(1.4f, 1.4f);
        RectTransform closeTextRect = stackBrowserCloseButtonText.rectTransform;
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;

        panelObject.SetActive(false);
    }

    private static void ResizeRuntimeCardList(List<RuntimeCard> cards, int count)
    {
        while (cards.Count < count) cards.Add(null);
        while (cards.Count > count) cards.RemoveAt(cards.Count - 1);
    }

    private bool TryShowStackCardPreviewAtPointer()
    {
        RuntimeCard card = null;
        if (pendingCastVisual != null && pendingCastVisual.gameObject.activeInHierarchy && IsPointerOverRect(pendingCastVisual.transform as RectTransform))
        {
            card = GetPendingCastSource();
        }

        if (card == null) card = GetRuntimeCardUnderPointer(stackBrowserCardVisuals, stackBrowserRuntimeCards);
        if (card == null) card = GetRuntimeCardUnderPointer(stackCardVisuals, stackCardRuntimeCards);
        if (card == null) return false;

        ShowStackPreview(card);
        return true;
    }

    private RuntimeCard GetRuntimeCardUnderPointer(List<MTGAHandUiCard> visuals, List<RuntimeCard> cards)
    {
        int count = Mathf.Min(visuals.Count, cards.Count);
        for (int i = count - 1; i >= 0; i--)
        {
            MTGAHandUiCard visual = visuals[i];
            if (visual == null || !visual.gameObject.activeInHierarchy) continue;
            if (!IsPointerOverRect(visual.transform as RectTransform)) continue;
            return cards[i];
        }

        return null;
    }

    private bool IsPointerOverRect(RectTransform rect)
    {
        return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, GetCanvasCamera());
    }

    private void ShowStackPreview(RuntimeCard card)
    {
        if (card == null) return;

        EnsureStackPreviewPanel();
        if (stackPreviewPanel == null || stackPreviewVisual == null) return;

        stackPreviewPanel.gameObject.SetActive(true);
        stackPreviewPanel.SetAsLastSibling();
        stackPreviewVisual.SetRuntimeCard(card, false);
        stackPreviewVisual.SetPlayable(false);
        SetUiCardGlow(stackPreviewVisual, new Color(0.08f, 0.78f, 1f, 0.26f), true);
    }

    private bool HideStackPreviewIfVisible()
    {
        if (stackPreviewPanel == null || !stackPreviewPanel.gameObject.activeSelf) return false;
        stackPreviewPanel.gameObject.SetActive(false);
        return true;
    }

    private void EnsureStackPreviewPanel()
    {
        EnsureCanvasReferences();
        if (canvas == null || stackPreviewPanel != null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject panelObject = new GameObject("ArenaStackPreviewPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        stackPreviewPanel = panelObject.GetComponent<RectTransform>();
        stackPreviewPanel.anchorMin = stackPreviewPanel.anchorMax = new Vector2(0f, 0.5f);
        stackPreviewPanel.pivot = new Vector2(0f, 0.5f);
        stackPreviewPanel.anchoredPosition = stackLayout.previewPanelPosition;
        stackPreviewPanel.sizeDelta = stackLayout.previewPanelSize;
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.008f, 0.014f, 0.020f, 0.78f);
        panelImage.raycastTarget = false;

        stackPreviewVisual = MTGAUiCardFactory.CreateCard(panelObject.transform, "ArenaStackPreviewCard", font, 1, 0, stackLayout.previewCardSize);
        RectTransform cardRect = stackPreviewVisual.transform as RectTransform;
        if (cardRect != null)
        {
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = stackLayout.previewCardSize;
            cardRect.localRotation = Quaternion.identity;
        }

        panelObject.SetActive(false);
    }

    private void ApplyStackCardLayout(MTGAHandUiCard visual, Vector2 size, Vector2 position, float zRotation)
    {
        if (visual == null) return;

        RectTransform rect = visual.transform as RectTransform;
        if (rect == null) return;

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        rect.localScale = Vector3.one;
    }

    private void SetUiCardGlow(MTGAHandUiCard visual, Color color, bool active)
    {
        if (visual == null) return;

        Transform glow = visual.transform.Find("SelectedGlow");
        Image image = glow != null ? glow.GetComponent<Image>() : null;
        if (image == null) return;

        RectTransform cardRect = visual.transform as RectTransform;
        if (cardRect != null) image.rectTransform.sizeDelta = cardRect.sizeDelta + new Vector2(18f, 18f);
        image.color = active ? color : new Color(color.r, color.g, color.b, 0f);
    }

    private string BuildStackHeader(IReadOnlyList<RuntimeCard> stackCards, int stackCount)
    {
        RuntimeCard pendingSource = GetPendingCastSource();
        bool hasPendingCast = pendingSource != null;
        if (stackCount == 0 && !hasPendingCast) return string.Empty;

        if (hasPendingCast)
        {
            RuntimeCard source = pendingSource;
            string target = GetPendingTargetLabel();
            return string.IsNullOrEmpty(target)
                ? $"\u9884\u65bd\u653e\n{source.Data.cardName}"
                : $"\u9884\u65bd\u653e\n{source.Data.cardName} -> {target}";
        }

        RuntimeCard top = stackCount > 0 ? stackCards[stackCount - 1] : null;
        string topName = top != null ? top.Data.cardName : string.Empty;
        return stackCount > 1 ? $"\u5806\u53e0 x{stackCount}\n\u9876\u90e8\uff1a{topName}" : $"\u5806\u53e0\n{topName}";
    }

    private static string GetStackTargetLabel(RuntimeCard card)
    {
        if (card == null) return string.Empty;
        if (card.TargetCard != null) return card.TargetCard.Data != null ? card.TargetCard.Data.cardName : "\u5361\u724c";
        if (card.TargetPlayerID > 0) return card.TargetPlayerID == OnlinePlayerContext.LocalPlayerId ? "\u5df1\u65b9\u73a9\u5bb6" : "\u5bf9\u624b\u73a9\u5bb6";
        return string.Empty;
    }

    private bool HasSelectedSpellTarget()
    {
        return gameRules != null
            && gameRules.IsChoosingSpellTarget()
            && gameRules.GetSelectedSpellTargetCount() > 0;
    }

    private string GetPendingTargetLabel()
    {
        int selectedCount = gameRules != null
            ? gameRules.GetSelectedSpellTargetCount()
            : 0;
        if (selectedCount > 1) return $"\u5df2\u9009\u62e9 {selectedCount} \u4e2a\u76ee\u6807";

        if (pendingTargetCard != null)
        {
            return pendingTargetCard.Data != null ? pendingTargetCard.Data.cardName : "\u5361\u724c";
        }

        if (pendingTargetPlayerID > 0) return GetPlayerTargetLabel(pendingTargetPlayerID);
        return string.Empty;
    }

    private bool TryGetPendingTargetScreenCenter(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

        if (pendingTargetCard != null)
        {
            ArenaBattlefieldCardVisual[] visuals = Object.FindObjectsByType<ArenaBattlefieldCardVisual>(FindObjectsSortMode.None);
            for (int i = 0; i < visuals.Length; i++)
            {
                ArenaBattlefieldCardVisual visual = visuals[i];
                if (visual == null) continue;
                RuntimeCard card = gameRules != null ? gameRules.GetBattlefieldCard(visual.Owner, visual.BattlefieldIndex) : null;
                if (!ReferenceEquals(card, pendingTargetCard)) continue;

                screenPosition = mainCamera != null
                    ? (Vector2)mainCamera.WorldToScreenPoint(visual.transform.position)
                    : Vector2.zero;
                return mainCamera != null;
            }
        }

        if (pendingTargetPlayerID == OnlinePlayerContext.LocalPlayerId && playerTargetButton != null)
        {
            RectTransform rect = playerTargetButton.transform as RectTransform;
            if (rect != null)
            {
                screenPosition = RectTransformUtility.WorldToScreenPoint(GetCanvasCamera(), rect.position);
                return true;
            }
        }

        if (pendingTargetPlayerID == OnlinePlayerContext.OpponentPlayerId && opponentTargetButton != null)
        {
            RectTransform rect = opponentTargetButton.transform as RectTransform;
            if (rect != null)
            {
                screenPosition = RectTransformUtility.WorldToScreenPoint(GetCanvasCamera(), rect.position);
                return true;
            }
        }

        return false;
    }

    private void ConfirmPendingTargetSpell()
    {
        if (gameRules == null
            || !choosingSpellTarget
            || !gameRules.CanConfirmActiveChoice())
        {
            return;
        }

        RuntimeCard source = GetPendingCastSource();
        string issue = gameRules.GetPendingCastIssue(targetHandOwner);
        if (source == null || !string.IsNullOrEmpty(issue))
        {
            ClearSpellTargetSelection("\u8be5\u5361\u73b0\u5728\u5df2\u4e0d\u80fd\u65bd\u653e\uff0c\u9884\u65bd\u653e\u5df2\u64a4\u56de\u3002");
            return;
        }

        string sourceName = source.Data.cardName;
        string targetName = GetPendingTargetLabel();
        bool played = gameRules.ApplyAction(new GameAction
        {
            type = GameActionType.ConfirmCast,
            playerId = targetHandOwner
        }).accepted;

        if (played && gameRules.IsChoosingSpellTarget())
        {
            pendingTargetCard = null;
            pendingTargetPlayerID = 0;
            choosingSpellTarget = true;
            confirmingSpellCast = false;
            SetHint($"\u7ee7\u7eed\u4e3a {sourceName} \u9009\u62e9\u4e0b\u4e00\u7ec4\u76ee\u6807\u3002");
        }
        else if (played && gameRules.IsConfirmingCast())
        {
            BeginCastConfirmation(
                targetHandOwner,
                targetHandIndex,
                sourceName,
                targetCastSourceZone);
        }
        else
        {
            if (played) ClearSpellTargetSelectionState(false);
            SetHint(played
                ? $"\u5df2\u5c06 {sourceName} \u4ee5 {targetName} \u4e3a\u76ee\u6807\u653e\u5165\u5806\u53e0\u3002"
                : $"\u65e0\u6cd5\u4ee5 {targetName} \u4e3a\u76ee\u6807\u65bd\u653e {sourceName}\u3002");
        }
        RefreshRulesHud();
    }

    private void ConfirmPendingCast()
    {
        if (gameRules == null || !confirmingSpellCast) return;

        RuntimeCard source = GetPendingCastSource();
        string issue = gameRules.GetPendingCastIssue(targetHandOwner);
        if (source == null || !string.IsNullOrEmpty(issue))
        {
            ClearSpellTargetSelection("\u8be5\u5361\u73b0\u5728\u5df2\u4e0d\u80fd\u65bd\u653e\uff0c\u9884\u65bd\u653e\u5df2\u64a4\u56de\u3002");
            return;
        }

        string sourceName = source.Data.cardName;
        bool played = gameRules.ApplyAction(new GameAction
        {
            type = GameActionType.ConfirmCast,
            playerId = targetHandOwner
        }).accepted;

        if (played) ClearSpellTargetSelectionState(false);
        SetHint(played
            ? $"\u5df2\u5c06 {sourceName} \u653e\u5165\u5806\u53e0\u3002"
            : $"\u65e0\u6cd5\u65bd\u653e {sourceName}\u3002");
        RefreshRulesHud();
    }

    public bool TryConfirmPendingTargetSpell()
    {
        if (confirmingSpellCast)
        {
            ConfirmPendingCast();
            return true;
        }

        if (!choosingSpellTarget) return false;
        if (gameRules == null || !gameRules.CanConfirmActiveChoice())
        {
            SetHint("\u5f53\u524d\u76ee\u6807\u6570\u91cf\u4e0d\u6ee1\u8db3\u8981\u6c42\uff0c\u8bf7\u7ee7\u7eed\u9009\u62e9\u6216\u53d6\u6d88\u3002");
            return true;
        }

        ConfirmPendingTargetSpell();
        return true;
    }

    private void SetPendingCastHandHidden(bool hidden)
    {
        if (targetCastSourceZone != GameChoiceZone.Hand) return;
        MTGAHandUiController controller = Object.FindFirstObjectByType<MTGAHandUiController>();
        if (controller == null) return;

        if (hidden)
        {
            controller.SetPendingCastCard(targetHandOwner, targetHandIndex);
        }
        else
        {
            controller.ClearPendingCastCard();
        }
    }

    private void OnPhaseChanged(TurnPhase phase)
    {
        RefreshRulesHud();
        SetHint($"\u9636\u6bb5\u53d8\u66f4\uff1aP{gameRules.GetCurrentPlayer()} - {GameRules.GetPhaseDisplayName(phase)}");
    }

    private void OnStackChanged(int stackCount)
    {
        RefreshRulesHud();
        SetHint(stackCount == 0
            ? "\u5806\u53e0\u5df2\u6e05\u7a7a\u3002"
            : $"\u5806\u53e0\u4e2d\u6709 {stackCount} \u4e2a\u5bf9\u8c61\uff0c\u70b9\u51fb\u8ba9\u8fc7\u4f18\u5148\u6743\u3002");
    }

    private void OnCardStateChanged(RuntimeCard card)
    {
        RefreshRulesHud();
    }

    private WhiteboxAction GetSelectedAction(Transform card)
    {
        if (gameRules == null || card == null) return WhiteboxAction.None;

        if (TryGetHandIndex(card, out int handOwner, out int handIndex))
        {
            return gameRules.CanPlayCard(handOwner, handIndex) ? WhiteboxAction.PlayCard : WhiteboxAction.None;
        }

        if (TryGetBattlefieldIndex(card, out int battlefieldOwner, out int battlefieldIndex))
        {
            if (gameRules.IsChoosingBlockers())
            {
                if (TryGetSelectedBlocker(out int blockerOwner, out int blockerIndex, out _) &&
                    battlefieldOwner == gameRules.GetCurrentPlayer())
                {
                    RuntimeCard targetAttacker = gameRules.GetBattlefieldCard(battlefieldOwner, battlefieldIndex);
                    if (gameRules.CanDeclareBlocker(blockerOwner, blockerIndex, targetAttacker))
                        return WhiteboxAction.Block;
                }

                if (gameRules.CanDeclareBlockerAgainstAnyAttacker(battlefieldOwner, battlefieldIndex))
                    return WhiteboxAction.Block;

                if (gameRules.CanCancelBlocker(battlefieldOwner, battlefieldIndex))
                    return WhiteboxAction.Block;
            }

            if (gameRules.CanDeclareAttacker(battlefieldOwner, battlefieldIndex))
                return WhiteboxAction.Attack;

            RuntimeCard runtimeCard = gameRules.GetBattlefieldCard(battlefieldOwner, battlefieldIndex);
            if (runtimeCard != null
                && (runtimeCard.HasType(CardType.Land)
                    ? gameRules.CanActivateManaAbility(runtimeCard) || gameRules.CanActivateAnyAbility(runtimeCard)
                    : gameRules.CanActivateAnyAbility(runtimeCard)))
                return WhiteboxAction.ActivateAbility;

            return WhiteboxAction.None;
        }

        return WhiteboxAction.None;
    }

    private bool IsCardActionable(Transform card)
    {
        return GetSelectedAction(card) != WhiteboxAction.None;
    }

    private bool TryGetSelectedBlocker(out int owner, out int index, out RuntimeCard blocker)
    {
        owner = 0;
        index = -1;
        blocker = null;
        if (selectedCard == null || gameRules == null || !gameRules.IsChoosingBlockers()) return false;
        if (!TryGetBattlefieldIndex(selectedCard, out owner, out index)) return false;

        blocker = gameRules.GetBattlefieldCard(owner, index);
        return blocker != null && gameRules.CanDeclareBlockerAgainstAnyAttacker(owner, index);
    }

    private static bool TryGetHandIndex(Transform card, out int owner, out int index)
    {
        owner = card.name.Contains("Opponent")
            ? OnlinePlayerContext.OpponentPlayerId
            : OnlinePlayerContext.LocalPlayerId;
        index = ParseTrailingIndex(card.name) - 1;
        return card.name.Contains("HandCard") && index >= 0;
    }

    private static bool TryGetBattlefieldIndex(Transform card, out int owner, out int index)
    {
        ArenaBattlefieldCardVisual binding = card.GetComponent<ArenaBattlefieldCardVisual>();
        if (binding != null)
        {
            owner = binding.Owner;
            index = binding.BattlefieldIndex;
            return index >= 0;
        }

        owner = card.name.Contains("Opponent")
            ? OnlinePlayerContext.OpponentPlayerId
            : OnlinePlayerContext.LocalPlayerId;

        if (card.name.Contains("BattlefieldCard_"))
        {
            index = ParseTrailingIndex(card.name) - 1;
            return index >= 0;
        }

        if (card.name.Contains("CreatureCard") || card.name.Contains("SupportCard"))
        {
            index = card.name.Contains("Support") ? 1 : 0;
            return true;
        }

        index = -1;
        return false;
    }

    private bool TryTogglePendingAttackerOnClick(Transform card)
    {
        if (gameRules == null || card == null || !gameRules.IsChoosingAttackers()) return false;
        if (!TryGetBattlefieldIndex(card, out int owner, out int battlefieldIndex)) return false;
        if (owner != OnlinePlayerContext.LocalPlayerId
            || owner != gameRules.GetCurrentPlayer()
            || !gameRules.CanDeclareAttacker(owner, battlefieldIndex))
        {
            return false;
        }

        RuntimeCard runtimeCard = gameRules.GetBattlefieldCard(owner, battlefieldIndex);
        if (runtimeCard == null) return false;

        bool changed = gameRules.ApplyAction(new GameAction
        {
            type = GameActionType.ToggleAttacker,
            playerId = owner,
            zoneIndex = battlefieldIndex
        }).accepted;
        if (changed)
        {
            bool pending = gameRules.IsPendingAttacker(runtimeCard);
            SelectCard(pending ? card : null);
            string action = pending ? "\u5df2\u9009\u4e3a\u653b\u51fb\u8005" : "\u5df2\u53d6\u6d88\u653b\u51fb";
            SetHint(pending
                ? $"{runtimeCard.Data.cardName} {action}\u3002\u53ef\u70b9\u51fb\u5bf9\u624b\u7684\u9e4f\u6d1b\u5ba2\u6216\u6218\u5f79\u6539\u53d8\u653b\u51fb\u76ee\u6807\u3002"
                : $"{runtimeCard.Data.cardName} {action}\u3002");
        }
        else
        {
            SelectCard(null);
            SetHint($"{runtimeCard.Data.cardName} \u73b0\u5728\u4e0d\u80fd\u653b\u51fb\u3002");
        }

        return true;
    }

    private bool TrySetPendingAttackTargetOnClick(Transform clickedVisual)
    {
        if (gameRules == null
            || clickedVisual == null
            || selectedCard == null
            || !gameRules.IsChoosingAttackers())
        {
            return false;
        }

        RuntimeCard attacker = GetRuntimeCardForVisual(selectedCard);
        RuntimeCard target = GetRuntimeCardForVisual(clickedVisual);
        if (attacker == null
            || target == null
            || !gameRules.IsPendingAttacker(attacker)
            || !gameRules.CanAttackTarget(attacker, target))
        {
            return false;
        }

        bool accepted = gameRules.ApplyAction(new GameAction
        {
            type = GameActionType.SetAttackerTarget,
            playerId = attacker.ControllerPlayerID,
            sourceObjectId = attacker.ObjectId,
            targetObjectId = target.ObjectId
        }).accepted;
        if (!accepted) return false;

        SetHint($"{attacker.GetCardName()} \u5c06\u653b\u51fb {target.GetCardName()}\u3002");
        SelectCard(null);
        RefreshRulesHud();
        return true;
    }

    private bool TryHandleBlockClick(Transform card)
    {
        if (gameRules == null || !gameRules.IsChoosingBlockers()) return false;
        if (card == null || !TryGetBattlefieldIndex(card, out int owner, out int battlefieldIndex))
        {
            SelectCard(null);
            return false;
        }

        RuntimeCard clickedCard = gameRules.GetBattlefieldCard(owner, battlefieldIndex);
        if (clickedCard == null) return false;

        if (gameRules.CanCancelBlocker(owner, battlefieldIndex))
        {
            string blockerName = clickedCard.Data.cardName;
            RuntimeCard targetAttacker = clickedCard.BlockingTarget;
            string attackerName = targetAttacker != null ? targetAttacker.Data.cardName : "\u653b\u51fb\u8005";
            if (gameRules.ApplyAction(new GameAction
                {
                    type = GameActionType.CancelBlocker,
                    playerId = owner,
                    zoneIndex = battlefieldIndex
                }).accepted)
            {
                SelectCard(null);
                SetHint($"{blockerName} \u5df2\u53d6\u6d88\u963b\u6321 {attackerName}\u3002");
                RefreshRulesHud();
                return true;
            }
        }

        if (TryGetSelectedBlocker(out int blockerOwner, out int blockerIndex, out RuntimeCard selectedBlocker))
        {
            if (owner == gameRules.GetCurrentPlayer())
            {
                if (gameRules.ApplyAction(new GameAction
                    {
                        type = GameActionType.DeclareBlocker,
                        playerId = blockerOwner,
                        zoneIndex = blockerIndex,
                        targetObjectId = clickedCard.ObjectId
                    }).accepted)
                {
                    SetHint($"{selectedBlocker.Data.cardName} \u963b\u6321 {clickedCard.Data.cardName}\u3002");
                    SelectCard(null);
                    RefreshRulesHud();
                    return true;
                }

                SetHint($"{selectedBlocker.Data.cardName} \u4e0d\u80fd\u963b\u6321 {clickedCard.Data.cardName}\u3002");
                return true;
            }

            if (gameRules.CanDeclareBlockerAgainstAnyAttacker(owner, battlefieldIndex))
            {
                SelectCard(card);
                SetHint($"\u5df2\u9009\u62e9\u963b\u6321\u8005\uff1a{clickedCard.Data.cardName}\u3002\u70b9\u51fb\u5bf9\u9762\u7684\u653b\u51fb\u8005\u3002");
                return true;
            }

            SetHint("\u8bf7\u70b9\u51fb\u5bf9\u9762\u7684\u653b\u51fb\u8005\u6765\u6307\u5b9a\u963b\u6321\u3002");
            return true;
        }

        if (gameRules.CanDeclareBlockerAgainstAnyAttacker(owner, battlefieldIndex))
        {
            SelectCard(card);
            SetHint($"\u5df2\u9009\u62e9\u963b\u6321\u8005\uff1a{clickedCard.Data.cardName}\u3002\u70b9\u51fb\u5bf9\u9762\u7684\u653b\u51fb\u8005\u3002");
            return true;
        }

        if (owner == gameRules.GetCurrentPlayer() && clickedCard.IsAttacking)
        {
            SetHint("\u5148\u9009\u62e9\u5df1\u65b9\u53ef\u963b\u6321\u751f\u7269\uff0c\u518d\u70b9\u51fb\u8fd9\u4e2a\u653b\u51fb\u8005\u3002");
            return true;
        }

        SetHint($"{clickedCard.Data.cardName} \u73b0\u5728\u4e0d\u80fd\u963b\u6321\u3002");
        return true;
    }

    private bool TryActivateManaAbilityOnClick(Transform card)
    {
        if (gameRules == null || card == null) return false;
        if (!TryGetBattlefieldIndex(card, out int owner, out int battlefieldIndex)) return false;

        RuntimeCard runtimeCard = gameRules.GetBattlefieldCard(owner, battlefieldIndex);
        if (runtimeCard == null || !runtimeCard.HasType(CardType.Land)) return false;

        if (owner != OnlinePlayerContext.LocalPlayerId)
        {
            SetHint("\u4e0d\u80fd\u542f\u52a8\u5bf9\u624b\u7684\u5730\u3002");
            return true;
        }

        bool wasTapped = runtimeCard.IsTapped;
        if ((gameRules.CanActivateManaAbility(runtimeCard)
                || gameRules.CanActivateAnyAbility(runtimeCard))
            && gameRules.ApplyAction(new GameAction
            {
                type = GameActionType.ActivateAbility,
                playerId = owner,
                sourceObjectId = runtimeCard.ObjectId,
                value = 1
            }).accepted)
        {
            SelectCard(null);
            RefreshRulesHud();
            SetHint(gameRules.IsResolvingGenericChoice()
                ? $"\u8bf7\u9009\u62e9 {runtimeCard.Data.cardName} \u8981\u542f\u52a8\u7684\u80fd\u529b\u3002"
                : wasTapped && !runtimeCard.IsTapped
                    ? $"\u5df2\u91cd\u7f6e {runtimeCard.Data.cardName}\uff0c\u5e76\u79fb\u9664\u672a\u4f7f\u7528\u7684\u6cd5\u529b\u3002\u6cd5\u529b\u6c60\uff1a{gameRules.GetManaPool(owner).ToDisplayString()}\u3002"
                    : $"\u5df2\u542f\u52a8 {runtimeCard.Data.cardName}\u3002\u6cd5\u529b\u6c60\uff1a{gameRules.GetManaPool(owner).ToDisplayString()}\u3002");
            return true;
        }

        SetHint(runtimeCard.IsTapped
            ? $"{runtimeCard.Data.cardName} \u5df2\u6a2a\u7f6e\uff0c\u4ea7\u751f\u7684\u6cd5\u529b\u5df2\u88ab\u4f7f\u7528\uff0c\u4e0d\u80fd\u518d\u64a4\u56de\u3002"
            : $"{runtimeCard.Data.cardName} \u73b0\u5728\u6ca1\u6709\u53ef\u542f\u52a8\u7684\u80fd\u529b\u3002");
        RefreshRulesHud();
        return true;
    }

    public bool BeginHandCastFeatureSelection(int owner, int handIndex)
    {
        if (gameRules == null) gameRules = Object.FindFirstObjectByType<GameRules>();
        return gameRules != null && BeginHandCastThroughAction(owner, handIndex);
    }

    public bool BeginHandSpellTargetSelection(int owner, int handIndex)
    {
        if (gameRules == null) gameRules = Object.FindFirstObjectByType<GameRules>();
        if (gameRules == null) return false;

        return BeginHandCastThroughAction(owner, handIndex);
    }

    public bool BeginHandCastConfirmation(int owner, int handIndex)
    {
        if (gameRules == null) gameRules = Object.FindFirstObjectByType<GameRules>();
        if (gameRules == null) return false;

        return BeginHandCastThroughAction(owner, handIndex);
    }

    private bool BeginHandCastThroughAction(int owner, int handIndex)
    {
        RuntimeCard source = gameRules?.GetHandCard(owner, handIndex);
        string sourceName = source?.Data?.cardName ?? "\u5361\u724c";
        if (source == null) return false;

        GameActionResult result = gameRules.ApplyAction(new GameAction
        {
            type = GameActionType.BeginHandCast,
            playerId = owner,
            zoneIndex = handIndex
        });
        if (!result.accepted)
        {
            SetHint(gameRules.GetHandCardPlayIssue(owner, handIndex));
            return false;
        }

        GameChoiceRequest choice = gameRules.GetActiveChoice();
        if (choice == null)
        {
            RefreshRulesHud();
            return true;
        }

        ClearSpellTargetSelectionState(false);
        if (choice.kind == GameChoiceKind.CastFeature)
        {
            SetCastCancelButtonVisible(true);
            RefreshTriggerOrderPanel();
            SetHint("\u9009\u62e9\u8fd9\u5f20\u724c\u8981\u65bd\u653e\u7684\u7279\u5f81\u3002");
            RefreshRulesHud();
            return true;
        }

        if (choice.kind == GameChoiceKind.SpellTarget)
        {
            BeginSpellTargetSelection(owner, handIndex, sourceName);
            return true;
        }

        if (choice.kind == GameChoiceKind.CastConfirmation)
        {
            BeginCastConfirmation(owner, handIndex, sourceName);
            return true;
        }

        return true;
    }

    private void BeginCastConfirmation(
        int owner,
        int handIndex,
        string cardName,
        GameChoiceZone sourceZone = GameChoiceZone.Hand)
    {
        confirmingSpellCast = true;
        choosingSpellTarget = false;
        targetHandOwner = owner;
        targetHandIndex = handIndex;
        targetCastSourceZone = sourceZone;
        pendingTargetCard = null;
        pendingTargetPlayerID = 0;
        selectedCard = null;
        SetPendingCastHandHidden(true);
        SetResolveActive(true);
        ClearTargetSelectionVisuals();
        RefreshTargetButtons();
        RefreshStackVisuals();
        SetHint($"\u786e\u8ba4\u65bd\u653e {cardName}\uff1a\u70b9\u51fb\u786e\u8ba4\u8fdb\u5165\u5806\u53e0\uff0c\u6216\u70b9\u51fb\u53d6\u6d88\u64a4\u56de\u3002");
        RefreshRulesHud();
    }

    private void BeginSpellTargetSelection(
        int owner,
        int handIndex,
        string cardName,
        GameChoiceZone sourceZone = GameChoiceZone.Hand)
    {
        choosingSpellTarget = true;
        confirmingSpellCast = false;
        targetHandOwner = owner;
        targetHandIndex = handIndex;
        targetCastSourceZone = sourceZone;
        pendingTargetCard = null;
        pendingTargetPlayerID = 0;
        selectedCard = null;
        SetPendingCastHandHidden(true);
        SetResolveActive(false);
        RefreshTargetButtons();
        RefreshStackVisuals();
        SetHint($"\u4e3a {cardName} \u9009\u62e9\u76ee\u6807\u3002\u70b9\u51fb\u5408\u6cd5\u5361\u724c\u6216\u73a9\u5bb6\u9762\u677f\uff0c\u518d\u786e\u8ba4\u65bd\u653e\u3002");
        RefreshRulesHud();
    }

    private void ClearSpellTargetSelection(string hint)
    {
        ClearSpellTargetSelectionState();
        SetHint(hint);
        RefreshRulesHud();
    }

    private void ClearSpellTargetSelectionState(bool cancelRulesState = true)
    {
        if (cancelRulesState && gameRules != null && targetHandOwner > 0)
        {
            gameRules.ApplyAction(new GameAction
            {
                type = GameActionType.CancelCast,
                playerId = targetHandOwner
            });
        }

        choosingSpellTarget = false;
        confirmingSpellCast = false;
        SetPendingCastHandHidden(false);
        targetHandOwner = 0;
        targetHandIndex = -1;
        targetCastSourceZone = GameChoiceZone.Hand;
        pendingTargetCard = null;
        pendingTargetPlayerID = 0;
        SetCardGlow(hoveredCard, false, "InteractionHoverGlow");
        SetCardGlow(selectedCard, false, "InteractionSelectedGlow");
        hoveredCard = null;
        selectedCard = null;
        ClearTargetSelectionVisuals();
        RefreshTargetButtons();
        SetCastCancelButtonVisible(false);
        RefreshStackVisuals();
        SetResolveActive(false);
        UpdatePreview(null);
        RestoreHandInputAfterTargetCancel();
    }

    private void ClearChosenSpellTarget(string sourceName)
    {
        pendingTargetCard = gameRules != null ? gameRules.GetSelectedSpellTargetCard() : null;
        pendingTargetPlayerID = gameRules != null ? gameRules.GetSelectedSpellTargetPlayer() : 0;
        SetResolveActive(false);
        RefreshTargetSelectionVisuals();
        RefreshTargetButtons();
        RefreshStackVisuals();
        SetHint($"\u5df2\u53d6\u6d88\u76ee\u6807\uff0c\u7ee7\u7eed\u4e3a {sourceName} \u9009\u62e9\u76ee\u6807\u3002");
    }

    private static void RestoreHandInputAfterTargetCancel()
    {
        MTGAHandUiController controller = Object.FindFirstObjectByType<MTGAHandUiController>();
        controller?.RestoreHandInputAfterExternalCancel();
    }

    private bool TryHandleSpellTargetClick(Transform card)
    {
        if (gameRules == null || !choosingSpellTarget) return false;

        RuntimeCard source = GetPendingCastSource();
        string issue = gameRules.GetPendingCastIssue(targetHandOwner);
        if (source == null || !string.IsNullOrEmpty(issue))
        {
            ClearSpellTargetSelection(string.IsNullOrEmpty(issue) ? "\u8be5\u5361\u73b0\u5728\u5df2\u4e0d\u80fd\u6253\u51fa\uff0c\u76ee\u6807\u9009\u62e9\u5df2\u53d6\u6d88\u3002" : issue);
            return true;
        }

        if (card != null && TryGetBattlefieldIndex(card, out int targetOwner, out int targetIndex))
        {
            RuntimeCard target = gameRules.GetBattlefieldCard(targetOwner, targetIndex);
            if (target != null && gameRules.CanTargetPendingCastCard(targetHandOwner, target))
            {
                string sourceName = source.Data.cardName;
                string targetName = target.Data.cardName;
                if (!gameRules.ApplyAction(new GameAction
                    {
                        type = GameActionType.SelectCardTarget,
                        playerId = targetHandOwner,
                        targetObjectId = target.ObjectId
                    }).accepted)
                {
                    SetHint($"{targetName} \u4e0d\u662f\u5408\u6cd5\u76ee\u6807\u3002");
                    return true;
                }

                pendingTargetCard = gameRules.GetSelectedSpellTargetCard();
                pendingTargetPlayerID = gameRules.GetSelectedSpellTargetPlayer();
                if (!HasSelectedSpellTarget())
                {
                    ClearChosenSpellTarget(sourceName);
                    return true;
                }

                SetResolveActive(gameRules.CanConfirmActiveChoice());
                RefreshTargetSelectionVisuals();
                RefreshStackVisuals();
                SetHint($"\u5df2\u9009\u62e9 {targetName} \u4f5c\u4e3a {sourceName} \u7684\u76ee\u6807\u3002\u70b9\u51fb\u786e\u8ba4\u65bd\u653e\uff0c\u6216\u6309 Esc \u64a4\u56de\u3002");
                return true;
            }

            SetHint($"{target?.Data.cardName ?? "\u8be5\u5361"} \u4e0d\u662f\u5408\u6cd5\u76ee\u6807\u3002");
            return true;
        }

        SetHint("\u8bf7\u70b9\u51fb\u5408\u6cd5\u5361\u724c\u76ee\u6807\uff0c\u6216\u70b9\u51fb\u53d1\u5149\u7684\u73a9\u5bb6\u9762\u677f\u3002");
        return true;
    }

    private void OnPlayerTargetClicked()
    {
        if (!IsRuleInputAvailable()) return;
        TryChoosePlayerTarget(OnlinePlayerContext.LocalPlayerId);
    }

    private void OnOpponentTargetClicked()
    {
        if (!IsRuleInputAvailable()) return;
        TryChoosePlayerTarget(OnlinePlayerContext.OpponentPlayerId);
    }

    private bool TryChoosePlayerTarget(int targetPlayerID)
    {
        if (gameRules == null || !choosingSpellTarget) return false;

        RuntimeCard source = GetPendingCastSource();
        string issue = gameRules.GetPendingCastIssue(targetHandOwner);
        if (source == null || !string.IsNullOrEmpty(issue))
        {
            ClearSpellTargetSelection(string.IsNullOrEmpty(issue) ? "\u8be5\u5361\u73b0\u5728\u5df2\u4e0d\u80fd\u6253\u51fa\uff0c\u76ee\u6807\u9009\u62e9\u5df2\u53d6\u6d88\u3002" : issue);
            return true;
        }

        if (!gameRules.CanTargetPendingCastPlayer(targetHandOwner, targetPlayerID))
        {
            SetHint($"{GetPlayerTargetLabel(targetPlayerID)} \u4e0d\u662f\u5408\u6cd5\u73a9\u5bb6\u76ee\u6807\u3002");
            RefreshTargetButtons();
            return true;
        }

        string sourceName = source.Data.cardName;
        if (!gameRules.ApplyAction(new GameAction
            {
                type = GameActionType.SelectPlayerTarget,
                playerId = targetHandOwner,
                targetPlayerId = targetPlayerID
            }).accepted)
        {
            SetHint($"{GetPlayerTargetLabel(targetPlayerID)} \u4e0d\u662f\u5408\u6cd5\u73a9\u5bb6\u76ee\u6807\u3002");
            return true;
        }

        pendingTargetCard = gameRules.GetSelectedSpellTargetCard();
        pendingTargetPlayerID = gameRules.GetSelectedSpellTargetPlayer();
        if (!HasSelectedSpellTarget())
        {
            ClearChosenSpellTarget(sourceName);
            return true;
        }

        SetResolveActive(gameRules.CanConfirmActiveChoice());
        RefreshTargetSelectionVisuals();
        RefreshStackVisuals();
        SetHint($"\u5df2\u9009\u62e9 {GetPlayerTargetLabel(targetPlayerID)} \u4f5c\u4e3a {sourceName} \u7684\u76ee\u6807\u3002\u70b9\u51fb\u786e\u8ba4\u65bd\u653e\uff0c\u6216\u6309 Esc \u64a4\u56de\u3002");
        return true;
    }

    private bool IsLegalSpellTargetVisual(Transform card)
    {
        if (gameRules == null || !choosingSpellTarget || card == null) return false;
        if (!TryGetBattlefieldIndex(card, out int owner, out int index)) return false;

        RuntimeCard target = gameRules.GetBattlefieldCard(owner, index);
        return target != null && gameRules.CanTargetPendingCastCard(targetHandOwner, target);
    }

    private bool IsLegendRuleCardVisual(Transform card)
    {
        if (gameRules == null || !gameRules.IsChoosingLegendRule() || card == null) return false;
        if (!TryGetBattlefieldIndex(card, out int owner, out int index)) return false;

        RuntimeCard runtimeCard = gameRules.GetBattlefieldCard(owner, index);
        return runtimeCard != null
            && gameRules.CanSelectLegendRuleCard(gameRules.GetLegendRulePlayer(), runtimeCard);
    }

    private bool TryHandleLegendRuleClick(Transform card)
    {
        if (gameRules == null || !gameRules.IsChoosingLegendRule() || card == null) return false;
        if (!TryGetBattlefieldIndex(card, out int owner, out int index)) return false;

        RuntimeCard runtimeCard = gameRules.GetBattlefieldCard(owner, index);
        int choosingPlayer = gameRules.GetLegendRulePlayer();
        if (runtimeCard == null || !gameRules.CanSelectLegendRuleCard(choosingPlayer, runtimeCard))
        {
            SetHint("\u8bf7\u9009\u62e9\u9700\u8981\u4fdd\u7559\u7684\u540c\u540d\u4f20\u5947\u6c38\u4e45\u7269\u3002");
            return true;
        }

        Transform previousSelection = selectedCard;
        GameChoiceRequest choice = gameRules.GetActiveChoice();
        GameChoiceOption selectedOption = null;
        if (choice != null)
        {
            for (int optionIndex = 0; optionIndex < choice.options.Count; optionIndex++)
            {
                GameChoiceOption option = choice.options[optionIndex];
                if (option != null
                    && option.ownerPlayerId == owner
                    && option.zoneIndex == index)
                {
                    selectedOption = option;
                    break;
                }
            }
        }

        if (selectedOption == null
            || !gameRules.ApplyAction(new GameAction
            {
                type = GameActionType.ToggleChoiceOption,
                playerId = choosingPlayer,
                choiceRequestId = choice.requestId,
                optionId = selectedOption.optionId
            }).accepted)
        {
            return true;
        }

        if (previousSelection != null && previousSelection != card)
        {
            SetCardGlow(previousSelection, false, "InteractionSelectedGlow");
        }

        if (gameRules.IsLegendRuleCardSelected(runtimeCard))
        {
            selectedCard = card;
            SetCardGlow(selectedCard, true, "InteractionSelectedGlow");
            SetHint($"\u5df2\u9009\u62e9\u4fdd\u7559 {runtimeCard.Data.cardName}\uff0c\u70b9\u51fb\u786e\u8ba4\u4fdd\u7559\u3002");
        }
        else
        {
            SetCardGlow(card, false, "InteractionSelectedGlow");
            selectedCard = null;
            SetHint("\u5df2\u53d6\u6d88\u9009\u62e9\uff0c\u8bf7\u91cd\u65b0\u9009\u62e9\u8981\u4fdd\u7559\u7684\u4f20\u5947\u6c38\u4e45\u7269\u3002");
        }

        RefreshRulesHud();
        return true;
    }

    private void RefreshTargetSelectionVisuals()
    {
        RefreshTargetModePanel();
        RefreshLegalTargetHighlights();
        RefreshTargetArrow();
    }

    private void ClearTargetSelectionVisuals()
    {
        if (targetModePanel != null) targetModePanel.gameObject.SetActive(false);
        if (targetArrowBody != null) targetArrowBody.gameObject.SetActive(false);
        if (targetArrowHead != null) targetArrowHead.gameObject.SetActive(false);
        SetExtraTargetArrowsActive(0);
        ClearLegalTargetHighlights();
    }

    private void RefreshTargetModePanel()
    {
        EnsureTargetModePanel();
        if (targetModePanel == null || targetModeText == null || gameRules == null) return;

        RuntimeCard source = GetPendingCastSource();
        string sourceName = source != null ? source.Data.cardName : "\u8fd9\u5f20\u724c";
        string targetLabel = GetPendingTargetLabel();
        targetModeText.text = string.IsNullOrEmpty(targetLabel)
            ? $"\u9009\u62e9\u76ee\u6807\n{sourceName}\n\u70b9\u51fb\u6218\u573a\u76ee\u6807\u6216\u53d1\u5149\u73a9\u5bb6\u9762\u677f"
            : $"\u786e\u8ba4\u65bd\u653e\n{sourceName}\n\u76ee\u6807\uff1a{targetLabel}";
        targetModePanel.gameObject.SetActive(true);
    }

    private void RefreshCastConfirmationPanel()
    {
        EnsureTargetModePanel();
        if (targetModePanel == null || targetModeText == null || gameRules == null) return;

        RuntimeCard source = GetPendingCastSource();
        string sourceName = source != null ? source.Data.cardName : "\u8fd9\u5f20\u724c";
        targetModeText.text = $"\u786e\u8ba4\u65bd\u653e\n{sourceName}\n\u786e\u8ba4\u540e\u8fdb\u5165\u5806\u53e0";
        targetModePanel.gameObject.SetActive(true);
        if (targetArrowBody != null) targetArrowBody.gameObject.SetActive(false);
        if (targetArrowHead != null) targetArrowHead.gameObject.SetActive(false);
        ClearLegalTargetHighlights();
    }

    private void RefreshLegalTargetHighlights()
    {
        ClearLegalTargetHighlights();
        if (cardRoot == null) return;

        ArenaBattlefieldCardVisual[] visuals = cardRoot.GetComponentsInChildren<ArenaBattlefieldCardVisual>(false);
        for (int i = 0; i < visuals.Length; i++)
        {
            Transform visual = visuals[i].transform;
            if (!IsLegalSpellTargetVisual(visual)) continue;

            SetCardGlow(visual, true, "InteractionSelectedGlow");
            legalTargetHighlights.Add(visual);
            RuntimeCard target = GetRuntimeCardForVisual(visual);
            if (target != null && gameRules.IsPendingSpellTargetSelected(target))
            {
                SetCardGlow(visual, true, "InteractionHoverGlow");
                selectedTargetHighlights.Add(visual);
            }
        }
    }

    private void ClearLegalTargetHighlights()
    {
        for (int i = 0; i < selectedTargetHighlights.Count; i++)
        {
            SetCardGlow(selectedTargetHighlights[i], false, "InteractionHoverGlow");
        }

        for (int i = 0; i < legalTargetHighlights.Count; i++)
        {
            SetCardGlow(legalTargetHighlights[i], false, "InteractionSelectedGlow");
        }

        selectedTargetHighlights.Clear();
        legalTargetHighlights.Clear();
    }

    private void RefreshTargetArrow()
    {
        EnsureTargetArrow();
        if (targetArrowBody == null || targetArrowHead == null || canvasRect == null) return;

        if (!TryGetTargetSourceScreenCenter(out Vector2 startScreen))
        {
            targetArrowBody.gameObject.SetActive(false);
            targetArrowHead.gameObject.SetActive(false);
            SetExtraTargetArrowsActive(0);
            return;
        }

        CollectSelectedSpatialTargetScreenCenters(selectedTargetScreenCenters);
        if (selectedTargetScreenCenters.Count > 0)
        {
            EnsureExtraTargetArrowCount(selectedTargetScreenCenters.Count - 1);
            PositionTargetArrow(
                targetArrowBody,
                targetArrowHead,
                startScreen,
                selectedTargetScreenCenters[0]);
            for (int i = 1; i < selectedTargetScreenCenters.Count; i++)
            {
                PositionTargetArrow(
                    extraTargetArrowBodies[i - 1],
                    extraTargetArrowHeads[i - 1],
                    startScreen,
                    selectedTargetScreenCenters[i]);
            }

            SetExtraTargetArrowsActive(selectedTargetScreenCenters.Count - 1);
            return;
        }

        SetExtraTargetArrowsActive(0);
        Vector2 endScreen = Input.mousePosition;
        if (hoveredCard != null && IsLegalSpellTargetVisual(hoveredCard))
        {
            endScreen = mainCamera != null
                ? (Vector2)mainCamera.WorldToScreenPoint(hoveredCard.position)
                : endScreen;
        }
        else if (TryGetHoveredPlayerTargetScreenCenter(out Vector2 playerTargetScreen))
        {
            endScreen = playerTargetScreen;
        }

        PositionTargetArrow(targetArrowBody, targetArrowHead, startScreen, endScreen);
    }

    private void PositionTargetArrow(
        RectTransform body,
        RectTransform head,
        Vector2 startScreen,
        Vector2 endScreen)
    {
        if (body == null || head == null || canvasRect == null) return;

        Camera uiCamera = GetCanvasCamera();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, startScreen, uiCamera, out Vector2 startLocal) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, endScreen, uiCamera, out Vector2 endLocal))
        {
            body.gameObject.SetActive(false);
            head.gameObject.SetActive(false);
            return;
        }

        Vector2 delta = endLocal - startLocal;
        float length = delta.magnitude;
        if (length < 24f)
        {
            body.gameObject.SetActive(false);
            head.gameObject.SetActive(false);
            return;
        }

        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        body.gameObject.SetActive(true);
        head.gameObject.SetActive(true);

        body.anchoredPosition = startLocal + delta * 0.5f;
        body.sizeDelta = new Vector2(Mathf.Max(0f, length - stackLayout.targetArrowHeadSize * 0.8f), stackLayout.targetArrowWidth);
        body.localRotation = Quaternion.Euler(0f, 0f, angle);

        head.anchoredPosition = endLocal;
        head.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void CollectSelectedSpatialTargetScreenCenters(List<Vector2> results)
    {
        results.Clear();
        GameChoiceRequest choice = gameRules != null ? gameRules.GetActiveChoice() : null;
        if (choice == null || choice.kind != GameChoiceKind.SpellTarget) return;

        ArenaBattlefieldCardVisual[] visuals =
            Object.FindObjectsByType<ArenaBattlefieldCardVisual>(FindObjectsSortMode.None);
        for (int i = 0; i < choice.selectedOptionIds.Count; i++)
        {
            GameChoiceOption option = choice.FindOption(choice.selectedOptionIds[i]);
            if (option == null) continue;

            if (option.zone == GameChoiceZone.Player)
            {
                Button button = option.ownerPlayerId == OnlinePlayerContext.LocalPlayerId
                    ? playerTargetButton
                    : option.ownerPlayerId == OnlinePlayerContext.OpponentPlayerId
                        ? opponentTargetButton
                        : null;
                RectTransform rect = button != null ? button.transform as RectTransform : null;
                if (rect != null)
                {
                    results.Add(RectTransformUtility.WorldToScreenPoint(GetCanvasCamera(), rect.position));
                }
                continue;
            }

            if (option.zone != GameChoiceZone.Battlefield || mainCamera == null) continue;
            for (int visualIndex = 0; visualIndex < visuals.Length; visualIndex++)
            {
                ArenaBattlefieldCardVisual visual = visuals[visualIndex];
                if (visual == null) continue;
                RuntimeCard card = gameRules.GetBattlefieldCard(
                    visual.Owner,
                    visual.BattlefieldIndex);
                if (card == null
                    || card.ObjectId != option.objectId
                    || card.ZoneChangeVersion != option.objectZoneChangeVersion)
                {
                    continue;
                }

                results.Add(mainCamera.WorldToScreenPoint(visual.transform.position));
                break;
            }
        }
    }

    private bool TryGetTargetSourceScreenCenter(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

        if (pendingCastVisual != null && pendingCastVisual.gameObject.activeInHierarchy)
        {
            RectTransform pendingRect = pendingCastVisual.transform as RectTransform;
            if (pendingRect != null)
            {
                screenPosition = RectTransformUtility.WorldToScreenPoint(GetCanvasCamera(), pendingRect.position);
                return true;
            }
        }

        MTGAHandUiCard[] handCards = Object.FindObjectsByType<MTGAHandUiCard>(FindObjectsSortMode.None);
        for (int i = 0; i < handCards.Length; i++)
        {
            MTGAHandUiCard handCard = handCards[i];
            if (handCard == null || !handCard.gameObject.activeInHierarchy) continue;
            if (handCard.Owner != targetHandOwner || handCard.Index != targetHandIndex) continue;

            RectTransform rect = handCard.transform as RectTransform;
            if (rect == null) continue;
            screenPosition = RectTransformUtility.WorldToScreenPoint(GetCanvasCamera(), rect.position);
            return true;
        }

        string prefix = targetHandOwner == OnlinePlayerContext.LocalPlayerId
            ? "PlayerHandCard_"
            : "OpponentHandCard_";
        GameObject legacyCard = GameObject.Find(prefix + (targetHandIndex + 1));
        if (legacyCard != null && mainCamera != null)
        {
            screenPosition = mainCamera.WorldToScreenPoint(legacyCard.transform.position);
            return true;
        }

        return false;
    }

    private bool TryGetHoveredPlayerTargetScreenCenter(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;
        if (TryGetHoveredPlayerTargetScreenCenter(
                playerTargetButton,
                OnlinePlayerContext.LocalPlayerId,
                out screenPosition)) return true;
        if (TryGetHoveredPlayerTargetScreenCenter(
                opponentTargetButton,
                OnlinePlayerContext.OpponentPlayerId,
                out screenPosition)) return true;
        return false;
    }

    private bool TryGetHoveredPlayerTargetScreenCenter(Button button, int playerID, out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;
        if (!IsPlayerTargetLegal(playerID)) return false;
        if (button == null || !button.gameObject.activeInHierarchy) return false;

        Camera uiCamera = GetCanvasCamera();
        RectTransform rect = button.transform as RectTransform;
        if (rect == null || !RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, uiCamera)) return false;

        screenPosition = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.position);
        return true;
    }

    private void ApplyRuntimeLayout()
    {
        if (stackPanel != null)
        {
            stackPanel.anchoredPosition = stackLayout.panelPosition;
            stackPanel.sizeDelta = stackLayout.panelSize;
        }

        if (stackInspectButton != null)
        {
            stackInspectButton.anchoredPosition = stackLayout.inspectButtonPosition;
            stackInspectButton.sizeDelta = stackLayout.inspectButtonSize;
        }

        if (targetModePanel != null)
        {
            targetModePanel.anchoredPosition = stackLayout.targetPanelPosition;
            targetModePanel.sizeDelta = stackLayout.targetPanelSize;
        }

        if (castCancelButton != null)
        {
            castCancelButton.anchoredPosition = stackLayout.cancelButtonPosition;
            castCancelButton.sizeDelta = stackLayout.cancelButtonSize;
        }

        if (targetArrowHead != null)
        {
            targetArrowHead.sizeDelta = new Vector2(stackLayout.targetArrowHeadSize, stackLayout.targetArrowHeadSize);
        }

        if (stackBrowserViewport != null)
        {
            stackBrowserViewport.anchoredPosition = stackLayout.browserViewportPosition;
            stackBrowserViewport.sizeDelta = stackLayout.browserViewportSize;
        }

        if (stackBrowserContent != null)
        {
            stackBrowserContent.sizeDelta = new Vector2(stackBrowserContent.sizeDelta.x, stackLayout.browserContentHeight);
        }

        if (stackBrowserScrollbar != null)
        {
            RectTransform rect = stackBrowserScrollbar.transform as RectTransform;
            if (rect != null)
            {
                rect.anchoredPosition = stackLayout.browserScrollbarPosition;
                rect.sizeDelta = stackLayout.browserScrollbarSize;
            }
        }

        if (stackBrowserCloseButton != null)
        {
            RectTransform rect = stackBrowserCloseButton.transform as RectTransform;
            if (rect != null)
            {
                rect.anchoredPosition = stackLayout.browserClosePosition;
                rect.sizeDelta = stackLayout.browserCloseSize;
            }
        }

        if (stackPreviewPanel != null)
        {
            stackPreviewPanel.anchoredPosition = stackLayout.previewPanelPosition;
            stackPreviewPanel.sizeDelta = stackLayout.previewPanelSize;
        }

        if (stackPreviewVisual != null)
        {
            RectTransform rect = stackPreviewVisual.transform as RectTransform;
            if (rect != null) rect.sizeDelta = stackLayout.previewCardSize;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        int localPlayer = OnlinePlayerContext.LocalPlayerId;
        int opponentPlayer = OnlinePlayerContext.OpponentPlayerId;
        ConfigurePlayerTargetButton(playerTargetButton, localPlayer, GetExistingPlayerName(localPlayer, "Sapphire Schnee"), false, font);
        ConfigurePlayerTargetButton(opponentTargetButton, opponentPlayer, GetExistingPlayerName(opponentPlayer, "Opponent"), true, font);
        ApplyZoneButtonLayout();
    }

    private void ApplyZoneButtonLayout()
    {
        for (int i = 0; i < zoneButtons.Count; i++)
        {
            ZoneButtonBinding binding = zoneButtons[i];
            RectTransform rect = binding?.Button != null ? binding.Button.transform as RectTransform : null;
            if (rect == null) continue;

            rect.sizeDelta = hudLayout.zoneButtonSize;
            if (binding.Owner == OnlinePlayerContext.LocalPlayerId)
            {
                rect.anchoredPosition = binding.Zone == BrowserZone.Deck
                    ? hudLayout.playerDeckPosition
                    : binding.Zone == BrowserZone.Graveyard
                        ? hudLayout.playerGraveyardPosition
                        : hudLayout.playerExilePosition;
            }
            else
            {
                rect.anchoredPosition = binding.Zone == BrowserZone.Deck
                    ? hudLayout.opponentDeckPosition
                    : binding.Zone == BrowserZone.Graveyard
                        ? hudLayout.opponentGraveyardPosition
                        : hudLayout.opponentExilePosition;
            }
        }
    }

    private Camera GetCanvasCamera()
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return canvas.worldCamera != null ? canvas.worldCamera : mainCamera;
    }

    private void RefreshTriggerOrderPanel()
    {
        EnsureTriggerOrderPanel();
        if (triggerOrderPanel == null) return;

        bool orderingTriggers = gameRules != null && gameRules.IsOrderingTriggeredAbilities();
        bool choosingFeature = gameRules != null && gameRules.IsChoosingCastFeature();
        GameChoiceRequest choice = gameRules != null ? gameRules.GetActiveChoice() : null;
        bool genericChoice = gameRules != null
            && gameRules.IsResolvingGenericChoice()
            && choice?.kind != GameChoiceKind.MulliganBottom;
        bool nonSpatialTargetChoice = choice != null
            && choice.kind == GameChoiceKind.SpellTarget
            && HasNonSpatialTargetOption(choice);
        bool visible = orderingTriggers
            || choosingFeature
            || (genericChoice && choice != null)
            || nonSpatialTargetChoice;
        triggerOrderPanel.gameObject.SetActive(visible);
        if (!visible) return;

        int optionCount = choice == null
            ? 0
            : choice.selectionMode == GameChoiceSelectionMode.Number
                ? Mathf.Max(0, choice.maximumValue - choice.minimumValue + 1)
                : choice.options.Count;
        while (triggerOrderButtons.Count < optionCount)
        {
            CreateTriggerOrderButton(triggerOrderButtons.Count);
        }

        for (int i = 0; i < triggerOrderButtons.Count; i++)
        {
            bool active = i < optionCount;
            Button button = triggerOrderButtons[i];
            button.gameObject.SetActive(active);
            if (!active) continue;

            GameChoiceOption option = choice.selectionMode == GameChoiceSelectionMode.Number
                ? null
                : choice.options[i];
            int selectedIndex = option != null
                ? choice.selectedOptionIds.IndexOf(option.optionId)
                : -1;
            int rank = orderingTriggers
                ? gameRules.GetTriggeredAbilityOrderRank(i)
                : choice.selectionMode == GameChoiceSelectionMode.Ordered && selectedIndex >= 0
                    ? selectedIndex + 1
                    : 0;
            int numberValue = choice.minimumValue + i;
            int allocatedAmount = option != null
                ? choice.GetAllocatedAmount(option.optionId)
                : 0;
            int repeatedCount = option != null
                ? choice.GetSelectionCount(option.optionId)
                : 0;
            bool selected = choice.selectionMode == GameChoiceSelectionMode.Number
                ? choice.hasSelectedValue && choice.selectedValue == numberValue
                : choice.selectionMode == GameChoiceSelectionMode.Distribution
                    ? allocatedAmount > 0
                    : selectedIndex >= 0;
            triggerOrderButtonTexts[i].text = choice.selectionMode == GameChoiceSelectionMode.Number
                ? numberValue.ToString()
                : choice.selectionMode == GameChoiceSelectionMode.Distribution
                    ? $"{option.label}    {allocatedAmount}/{option.maximumAmount}"
                    : choice.selectionMode == GameChoiceSelectionMode.Repeated && repeatedCount > 0
                        ? $"{option.label}    x{repeatedCount}/{option.maximumAmount}"
                    : rank > 0
                        ? $"{rank}. {option.label}"
                        : option.label;
            button.image.color = selected
                ? new Color(0.03f, 0.62f, 0.78f, 0.96f)
                : new Color(0.07f, 0.12f, 0.18f, 0.96f);
            button.interactable = IsRuleInputAvailable() && (option == null || option.legal);

            RectTransform buttonRect = button.transform as RectTransform;
            buttonRect.anchoredPosition = new Vector2(0f, -i * 56f);
        }

        triggerOrderContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, optionCount * 56f));
        triggerOrderTitleText.text = choosingFeature
            ? "\u9009\u62e9\u65bd\u653e\u65b9\u5f0f"
            : genericChoice || nonSpatialTargetChoice
                ? choice.prompt
                : "\u89e6\u53d1\u5f0f\u5f02\u80fd\u987a\u5e8f\n\u6309\u4ece\u5806\u53e0\u5e95\u5230\u5806\u53e0\u9876\u7684\u987a\u5e8f\u70b9\u51fb";
        triggerOrderPanel.SetAsLastSibling();
    }

    private static bool HasNonSpatialTargetOption(GameChoiceRequest choice)
    {
        if (choice == null) return false;
        for (int i = 0; i < choice.options.Count; i++)
        {
            GameChoiceOption option = choice.options[i];
            if (option != null
                && option.zone != GameChoiceZone.Battlefield
                && option.zone != GameChoiceZone.Player)
            {
                return true;
            }
        }
        return false;
    }

    private void EnsureTriggerOrderPanel()
    {
        EnsureCanvasReferences();
        if (canvas == null || triggerOrderPanel != null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject panelObject = new GameObject(
            "ArenaTriggerOrderPanel",
            typeof(RectTransform),
            typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);
        triggerOrderPanel = panelObject.GetComponent<RectTransform>();
        triggerOrderPanel.anchorMin = triggerOrderPanel.anchorMax = new Vector2(0.5f, 0.5f);
        triggerOrderPanel.pivot = new Vector2(0.5f, 0.5f);
        triggerOrderPanel.anchoredPosition = new Vector2(0f, 20f);
        triggerOrderPanel.sizeDelta = new Vector2(540f, 560f);
        panelObject.GetComponent<Image>().color = new Color(0.015f, 0.055f, 0.09f, 0.96f);

        GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleObject.transform.SetParent(triggerOrderPanel, false);
        triggerOrderTitleText = titleObject.GetComponent<Text>();
        triggerOrderTitleText.font = font;
        triggerOrderTitleText.fontSize = 22;
        triggerOrderTitleText.alignment = TextAnchor.MiddleCenter;
        triggerOrderTitleText.color = Color.white;
        triggerOrderTitleText.raycastTarget = false;
        RectTransform titleRect = triggerOrderTitleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -12f);
        titleRect.sizeDelta = new Vector2(-28f, 72f);

        GameObject viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D));
        viewportObject.transform.SetParent(triggerOrderPanel, false);
        triggerOrderViewport = viewportObject.GetComponent<RectTransform>();
        triggerOrderViewport.anchorMin = Vector2.zero;
        triggerOrderViewport.anchorMax = Vector2.one;
        triggerOrderViewport.offsetMin = new Vector2(18f, 18f);
        triggerOrderViewport.offsetMax = new Vector2(-18f, -92f);
        viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(triggerOrderViewport, false);
        triggerOrderContent = contentObject.GetComponent<RectTransform>();
        triggerOrderContent.anchorMin = new Vector2(0f, 1f);
        triggerOrderContent.anchorMax = new Vector2(1f, 1f);
        triggerOrderContent.pivot = new Vector2(0.5f, 1f);
        triggerOrderContent.anchoredPosition = Vector2.zero;
        triggerOrderContent.sizeDelta = Vector2.zero;

        triggerOrderScrollRect = panelObject.AddComponent<ScrollRect>();
        triggerOrderScrollRect.viewport = triggerOrderViewport;
        triggerOrderScrollRect.content = triggerOrderContent;
        triggerOrderScrollRect.horizontal = false;
        triggerOrderScrollRect.vertical = true;
        triggerOrderScrollRect.movementType = ScrollRect.MovementType.Clamped;
        triggerOrderScrollRect.scrollSensitivity = 28f;
        triggerOrderPanel.gameObject.SetActive(false);
    }

    private void CreateTriggerOrderButton(int itemIndex)
    {
        GameObject buttonObject = new GameObject(
            $"TriggerOrder_{itemIndex + 1}",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(triggerOrderContent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(0.5f, 1f);
        buttonRect.sizeDelta = new Vector2(-12f, 48f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.08f, 0.72f, 0.86f, 1f);
        colors.pressedColor = new Color(0.02f, 0.48f, 0.62f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = 17;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 4f);
        textRect.offsetMax = new Vector2(-12f, -4f);

        int capturedIndex = itemIndex;
        button.onClick.AddListener(() =>
        {
            if (gameRules == null || !IsRuleInputAvailable()) return;
            if (gameRules.IsChoosingCastFeature())
            {
                int handIndex = gameRules.GetActiveChoice()?.sourceZoneIndex ?? -1;
                if (!gameRules.ApplyAction(new GameAction
                    {
                        type = GameActionType.SelectCastFeature,
                        playerId = OnlinePlayerContext.LocalPlayerId,
                        zoneIndex = handIndex,
                        featureIndex = capturedIndex
                    }).accepted)
                {
                    return;
                }

                int localPlayer = OnlinePlayerContext.LocalPlayerId;
                RuntimeCard source = gameRules.GetHandCard(localPlayer, handIndex);
                if (source == null) return;
                string featureName = source.SpellFeature?.CardName ?? source.Data.cardName;
                if (gameRules.IsChoosingSpellTarget())
                {
                    BeginSpellTargetSelection(localPlayer, handIndex, featureName);
                }
                else
                {
                    BeginCastConfirmation(localPlayer, handIndex, featureName);
                }
            }
            else if (gameRules.IsResolvingGenericChoice())
            {
                GameChoiceRequest choice = gameRules.GetActiveChoice();
                if (choice == null) return;

                if (choice.selectionMode == GameChoiceSelectionMode.Number)
                {
                    int value = choice.minimumValue + capturedIndex;
                    if (!gameRules.ApplyAction(new GameAction
                        {
                            type = GameActionType.SetChoiceNumber,
                            playerId = OnlinePlayerContext.LocalPlayerId,
                            choiceRequestId = choice.requestId,
                            value = value
                        }).accepted)
                    {
                        return;
                    }
                }
                else
                {
                    if (capturedIndex >= choice.options.Count) return;
                    GameChoiceOption option = choice.options[capturedIndex];
                    if (choice.selectionMode == GameChoiceSelectionMode.Distribution)
                    {
                        int currentAmount = choice.GetAllocatedAmount(option.optionId);
                        int nextAmount = currentAmount >= option.maximumAmount
                            ? 0
                            : currentAmount + 1;
                        if (!gameRules.ApplyAction(new GameAction
                            {
                                type = GameActionType.SetChoiceAllocation,
                                playerId = OnlinePlayerContext.LocalPlayerId,
                                choiceRequestId = choice.requestId,
                                optionId = option.optionId,
                                value = nextAmount
                            }).accepted)
                        {
                            return;
                        }
                    }
                    else if (!gameRules.ApplyAction(new GameAction
                        {
                            type = GameActionType.ToggleChoiceOption,
                            playerId = OnlinePlayerContext.LocalPlayerId,
                            choiceRequestId = choice.requestId,
                            optionId = option.optionId
                        }).accepted)
                    {
                        return;
                    }
                }
            }
            else
            {
                GameChoiceRequest choice = gameRules.GetActiveChoice();
                if (choice == null
                    || capturedIndex < 0
                    || capturedIndex >= choice.options.Count
                    || !gameRules.ApplyAction(new GameAction
                    {
                        type = GameActionType.ToggleChoiceOption,
                        playerId = OnlinePlayerContext.LocalPlayerId,
                        choiceRequestId = choice.requestId,
                        optionId = choice.options[capturedIndex].optionId
                    }).accepted)
                {
                    return;
                }
            }
            RefreshTriggerOrderPanel();
            RefreshRulesHud();
        });

        triggerOrderButtons.Add(button);
        triggerOrderButtonTexts.Add(text);
    }

    private void EnsureCanvasReferences()
    {
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvasRect == null && canvas != null) canvasRect = canvas.transform as RectTransform;
    }

    private void EnsureStackPanel()
    {
        EnsureCanvasReferences();
        if (canvas == null) return;

        if (stackPanel == null)
        {
            Transform existing = canvas.transform.Find("ArenaStackPanel");
            if (existing != null)
            {
                stackPanel = existing as RectTransform;
            }
            else
            {
                GameObject panelObject = new GameObject("ArenaStackPanel", typeof(RectTransform), typeof(Image));
                panelObject.transform.SetParent(canvas.transform, false);
                stackPanel = panelObject.GetComponent<RectTransform>();
                Image image = panelObject.GetComponent<Image>();
                image.color = Color.clear;
                image.raycastTarget = false;
            }
        }

        Image panelImage = stackPanel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = Color.clear;
            panelImage.raycastTarget = false;
        }

        stackPanel.anchorMin = stackPanel.anchorMax = new Vector2(1f, 0.5f);
        stackPanel.pivot = new Vector2(1f, 0.5f);
        stackPanel.anchoredPosition = stackLayout.panelPosition;
        stackPanel.sizeDelta = stackLayout.panelSize;
        stackPanel.SetAsLastSibling();

        if (stackStatusText == null) stackStatusText = GameObject.Find("ArenaStackStatusText")?.GetComponent<Text>();
        if (stackStatusText == null)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject textObject = new GameObject("ArenaStackStatusText", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(stackPanel, false);
            stackStatusText = textObject.GetComponent<Text>();
            stackStatusText.font = font;
        }
        else if (stackStatusText.transform.parent != stackPanel)
        {
            stackStatusText.transform.SetParent(stackPanel, false);
        }

        RectTransform textRect = stackStatusText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0f, -10f);
        textRect.sizeDelta = new Vector2(-18f, 52f);
        stackStatusText.fontSize = 16;
        stackStatusText.alignment = TextAnchor.UpperCenter;
        stackStatusText.color = Color.white;
        stackStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        stackStatusText.verticalOverflow = VerticalWrapMode.Truncate;
        stackStatusText.raycastTarget = false;
    }

    private void EnsureTargetModePanel()
    {
        EnsureCanvasReferences();
        if (canvas == null) return;

        if (targetModePanel == null)
        {
            Transform existing = canvas.transform.Find("ArenaTargetModePanel");
            if (existing != null)
            {
                targetModePanel = existing as RectTransform;
            }
            else
            {
                GameObject panelObject = new GameObject("ArenaTargetModePanel", typeof(RectTransform), typeof(Image));
                panelObject.transform.SetParent(canvas.transform, false);
                targetModePanel = panelObject.GetComponent<RectTransform>();
                Image image = panelObject.GetComponent<Image>();
                image.color = new Color(0.02f, 0.15f, 0.23f, 0.90f);
                image.raycastTarget = false;
            }
        }

        targetModePanel.anchorMin = targetModePanel.anchorMax = new Vector2(0.5f, 0f);
        targetModePanel.pivot = new Vector2(0.5f, 0f);
        targetModePanel.anchoredPosition = stackLayout.targetPanelPosition;
        targetModePanel.sizeDelta = stackLayout.targetPanelSize;
        targetModePanel.SetAsLastSibling();

        if (targetModeText == null)
        {
            Transform existingText = targetModePanel.Find("Text");
            targetModeText = existingText != null ? existingText.GetComponent<Text>() : null;
        }

        if (targetModeText == null)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(targetModePanel, false);
            targetModeText = textObject.GetComponent<Text>();
            targetModeText.font = font;
        }

        RectTransform textRect = targetModeText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 8f);
        textRect.offsetMax = new Vector2(-14f, -8f);
        targetModeText.fontSize = 21;
        targetModeText.alignment = TextAnchor.MiddleCenter;
        targetModeText.color = Color.white;
        targetModeText.horizontalOverflow = HorizontalWrapMode.Wrap;
        targetModeText.verticalOverflow = VerticalWrapMode.Truncate;
        targetModeText.raycastTarget = false;
        targetModePanel.gameObject.SetActive(false);
    }

    private void EnsureCastCancelButton()
    {
        EnsureCanvasReferences();
        if (canvas == null) return;

        if (castCancelButton == null)
        {
            Transform existing = canvas.transform.Find("ArenaCastCancelButton");
            if (existing != null)
            {
                castCancelButton = existing as RectTransform;
                castCancelButtonComponent = existing.GetComponent<Button>();
                castCancelButtonText = existing.GetComponentInChildren<Text>();
            }
            else
            {
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                GameObject buttonObject = new GameObject("ArenaCastCancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(canvas.transform, false);
                castCancelButton = buttonObject.GetComponent<RectTransform>();
                castCancelButtonComponent = buttonObject.GetComponent<Button>();

                Image image = buttonObject.GetComponent<Image>();
                image.color = new Color(0.40f, 0.06f, 0.05f, 0.96f);
                image.raycastTarget = true;
                castCancelButtonComponent.targetGraphic = image;
                castCancelButtonComponent.transition = Selectable.Transition.None;

                GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
                textObject.transform.SetParent(buttonObject.transform, false);
                castCancelButtonText = textObject.GetComponent<Text>();
                castCancelButtonText.font = font;
                castCancelButtonText.fontSize = 22;
                castCancelButtonText.alignment = TextAnchor.MiddleCenter;
                castCancelButtonText.color = Color.white;
                castCancelButtonText.raycastTarget = false;
                Outline outline = textObject.GetComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(1.2f, 1.2f);

                RectTransform textRect = castCancelButtonText.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
            }
        }

        castCancelButton.anchorMin = castCancelButton.anchorMax = new Vector2(1f, 0f);
        castCancelButton.pivot = new Vector2(1f, 0f);
        castCancelButton.anchoredPosition = stackLayout.cancelButtonPosition;
        castCancelButton.sizeDelta = stackLayout.cancelButtonSize;
        if (castCancelButtonText != null) castCancelButtonText.text = "\u53d6\u6d88";
        castCancelButton.gameObject.SetActive(false);
    }

    private void SetCastCancelButtonVisible(bool visible)
    {
        EnsureCastCancelButton();
        if (castCancelButton == null) return;

        castCancelButton.gameObject.SetActive(visible);
        if (castCancelButtonComponent != null)
            castCancelButtonComponent.interactable = visible && IsRuleInputAvailable();
        if (visible) castCancelButton.SetAsLastSibling();
    }

    private void EnsureTargetArrow()
    {
        EnsureCanvasReferences();
        if (canvas == null) return;

        if (targetArrowBody == null)
        {
            GameObject bodyObject = new GameObject("ArenaTargetArrowBody", typeof(RectTransform), typeof(Image));
            bodyObject.transform.SetParent(canvas.transform, false);
            targetArrowBody = bodyObject.GetComponent<RectTransform>();
            targetArrowBodyImage = bodyObject.GetComponent<Image>();
            targetArrowBodyImage.color = new Color(0.05f, 0.90f, 1f, 0.92f);
            targetArrowBodyImage.raycastTarget = false;
            targetArrowBody.anchorMin = targetArrowBody.anchorMax = new Vector2(0.5f, 0.5f);
            targetArrowBody.pivot = new Vector2(0.5f, 0.5f);
        }

        if (targetArrowHead == null)
        {
            GameObject headObject = new GameObject("ArenaTargetArrowHead", typeof(RectTransform), typeof(Image));
            headObject.transform.SetParent(canvas.transform, false);
            targetArrowHead = headObject.GetComponent<RectTransform>();
            targetArrowHeadImage = headObject.GetComponent<Image>();
            targetArrowHeadImage.sprite = GetArrowHeadSprite();
            targetArrowHeadImage.color = new Color(0.05f, 0.90f, 1f, 0.92f);
            targetArrowHeadImage.raycastTarget = false;
            targetArrowHead.anchorMin = targetArrowHead.anchorMax = new Vector2(0.5f, 0.5f);
            targetArrowHead.pivot = new Vector2(0.5f, 0.5f);
            targetArrowHead.sizeDelta = new Vector2(stackLayout.targetArrowHeadSize, stackLayout.targetArrowHeadSize);
        }

        targetArrowBody.SetAsLastSibling();
        targetArrowHead.SetAsLastSibling();
        targetArrowBody.gameObject.SetActive(false);
        targetArrowHead.gameObject.SetActive(false);
    }

    private void EnsureExtraTargetArrowCount(int count)
    {
        EnsureCanvasReferences();
        if (canvas == null) return;

        while (extraTargetArrowBodies.Count < count)
        {
            GameObject bodyObject = new GameObject(
                $"ArenaTargetArrowBody_{extraTargetArrowBodies.Count + 2}",
                typeof(RectTransform),
                typeof(Image));
            bodyObject.transform.SetParent(canvas.transform, false);
            RectTransform body = bodyObject.GetComponent<RectTransform>();
            Image bodyImage = bodyObject.GetComponent<Image>();
            bodyImage.color = new Color(0.05f, 0.90f, 1f, 0.92f);
            bodyImage.raycastTarget = false;
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 0.5f);
            body.pivot = new Vector2(0.5f, 0.5f);

            GameObject headObject = new GameObject(
                $"ArenaTargetArrowHead_{extraTargetArrowHeads.Count + 2}",
                typeof(RectTransform),
                typeof(Image));
            headObject.transform.SetParent(canvas.transform, false);
            RectTransform head = headObject.GetComponent<RectTransform>();
            Image headImage = headObject.GetComponent<Image>();
            headImage.sprite = GetArrowHeadSprite();
            headImage.color = new Color(0.05f, 0.90f, 1f, 0.92f);
            headImage.raycastTarget = false;
            head.anchorMin = head.anchorMax = new Vector2(0.5f, 0.5f);
            head.pivot = new Vector2(0.5f, 0.5f);
            head.sizeDelta = new Vector2(
                stackLayout.targetArrowHeadSize,
                stackLayout.targetArrowHeadSize);

            extraTargetArrowBodies.Add(body);
            extraTargetArrowHeads.Add(head);
        }
    }

    private void SetExtraTargetArrowsActive(int activeCount)
    {
        for (int i = 0; i < extraTargetArrowBodies.Count; i++)
        {
            bool active = i < activeCount;
            extraTargetArrowBodies[i].gameObject.SetActive(active);
            extraTargetArrowHeads[i].gameObject.SetActive(active);
            if (active)
            {
                extraTargetArrowBodies[i].SetAsLastSibling();
                extraTargetArrowHeads[i].SetAsLastSibling();
            }
        }
    }

    private static Sprite GetArrowHeadSprite()
    {
        if (arrowHeadSprite != null) return arrowHeadSprite;

        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ArenaTargetArrowHead",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float widthAtX = Mathf.Lerp(center, 2f, x / (float)(size - 1));
                float alpha = Mathf.Abs(y - center) <= widthAtX ? 1f : 0f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        arrowHeadSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        arrowHeadSprite.name = "ArenaTargetArrowHead";
        arrowHeadSprite.hideFlags = HideFlags.HideAndDontSave;
        return arrowHeadSprite;
    }

    private void EnsurePlayerTargetButtons()
    {
        EnsureCanvasReferences();
        if (canvas == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (playerTargetButton == null)
        {
            playerTargetButton = FindOrCreatePlayerTargetButton(canvas.transform, "ArenaPlayerTargetButton", font);
        }

        if (opponentTargetButton == null)
        {
            opponentTargetButton = FindOrCreatePlayerTargetButton(canvas.transform, "ArenaOpponentTargetButton", font);
        }

        int localPlayer = OnlinePlayerContext.LocalPlayerId;
        int opponentPlayer = OnlinePlayerContext.OpponentPlayerId;
        ConfigurePlayerTargetButton(playerTargetButton, localPlayer, GetExistingPlayerName(localPlayer, "Sapphire Schnee"), false, font);
        ConfigurePlayerTargetButton(opponentTargetButton, opponentPlayer, GetExistingPlayerName(opponentPlayer, "Opponent"), true, font);
    }

    private static Button FindOrCreatePlayerTargetButton(Transform parent, string name, Font font)
    {
        Button existing = GameObject.Find(name)?.GetComponent<Button>();
        return existing != null ? existing : CreatePlayerTargetButton(parent, name, font);
    }

    private static Button CreatePlayerTargetButton(Transform parent, string name, Font font)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(420f, 116f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.006f, 0.012f, 0.018f, 0.88f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.interactable = false;
        return button;
    }

    private void ConfigurePlayerTargetButton(Button button, int playerID, string playerName, bool opponent, Font font)
    {
        if (button == null) return;

        RectTransform rect = button.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = rect.anchorMax = opponent ? Vector2.one : Vector2.zero;
            rect.pivot = opponent ? Vector2.one : Vector2.zero;
            rect.anchoredPosition = opponent ? hudLayout.opponentPanelPosition : hudLayout.playerPanelPosition;
            rect.sizeDelta = opponent ? hudLayout.opponentPanelSize : hudLayout.playerPanelSize;
        }

        button.gameObject.SetActive(true);
        button.transition = Selectable.Transition.None;
        button.targetGraphic = button.GetComponent<Image>();

        Transform legacyText = button.transform.Find("Text");
        if (legacyText != null) legacyText.gameObject.SetActive(false);

        Image accent = GetOrCreatePanelImage(button.transform, "TargetAccent");
        SetChildRect(accent.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, 108f), new Vector2(420f, 8f));

        Image avatar = GetOrCreatePanelImage(button.transform, "Avatar");
        avatar.sprite = GetPlayerPortraitSprite();
        avatar.color = GetPlayerUiColor(playerID, 0.92f);
        SetChildRect(avatar.rectTransform, Vector2.zero, Vector2.zero, new Vector2(opponent ? 322f : 18f, 18f), new Vector2(80f, 80f));

        Text avatarText = GetOrCreatePanelText(avatar.transform, "AvatarText", font);
        SetChildRect(avatarText.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(80f, 80f));
        avatarText.text = GetAvatarInitial(playerName, playerID);
        avatarText.fontSize = 30;
        avatarText.alignment = TextAnchor.MiddleCenter;
        avatarText.color = Color.white;
        EnsureTextOutline(avatarText, new Color(0f, 0f, 0f, 0.92f), 1.5f);

        bool isLocalPlayer = playerID == OnlinePlayerContext.LocalPlayerId;
        Text nameText = GetOrCreatePanelText(button.transform, isLocalPlayer ? "PlayerName" : "OpponentName", font);
        if (isLocalPlayer) playerNameText = nameText;
        else opponentNameText = nameText;
        SetChildRect(nameText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(opponent ? 24f : 108f, 74f), new Vector2(280f, 28f));
        nameText.text = playerName;
        nameText.fontSize = 22;
        nameText.alignment = opponent ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        nameText.color = Color.white;
        EnsureTextOutline(nameText, new Color(0f, 0f, 0f, 0.92f), 1.2f);

        Text lpText = GetOrCreatePanelText(button.transform, "LPLabel", font);
        SetChildRect(lpText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(opponent ? 104f : 108f, 30f), new Vector2(44f, 36f));
        lpText.text = "LP";
        lpText.fontSize = 26;
        lpText.alignment = TextAnchor.MiddleLeft;
        lpText.color = new Color(0.86f, 0.92f, 0.95f, 1f);
        EnsureTextOutline(lpText, Color.black, 1f);

        Text lifeText = GetOrCreatePanelText(button.transform, isLocalPlayer ? "ArenaPlayerLife" : "ArenaOpponentLife", font);
        if (isLocalPlayer) playerLifeText = lifeText;
        else opponentLifeText = lifeText;
        SetChildRect(lifeText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(opponent ? 150f : 158f, 20f), new Vector2(148f, 58f));
        lifeText.fontSize = 46;
        lifeText.alignment = TextAnchor.MiddleRight;
        lifeText.color = Color.white;
        EnsureTextOutline(lifeText, Color.black, 1.5f);

        Outline outline = button.GetComponent<Outline>();
        if (outline == null) outline = button.gameObject.AddComponent<Outline>();
        outline.useGraphicAlpha = true;
        RefreshPlayerTargetButtonState(
            button,
            playerID,
            IsPlayerTargetLegal(playerID),
            gameRules != null && gameRules.IsPendingSpellTargetPlayerSelected(playerID));
    }

    private string GetExistingPlayerName(int playerID, string fallback)
    {
        bool isLocalPlayer = playerID == OnlinePlayerContext.LocalPlayerId;
        Text text = isLocalPlayer ? playerNameText : opponentNameText;
        if (text != null && !string.IsNullOrEmpty(text.text)) return text.text;

        string objectName = isLocalPlayer ? "PlayerName" : "OpponentName";
        Text existing = GameObject.Find(objectName)?.GetComponent<Text>();
        return existing != null && !string.IsNullOrEmpty(existing.text) ? existing.text : fallback;
    }

    private static Image GetOrCreatePanelImage(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            GameObject childObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            childObject.transform.SetParent(parent, false);
            return childObject.GetComponent<Image>();
        }

        Image image = child.GetComponent<Image>();
        return image != null ? image : child.gameObject.AddComponent<Image>();
    }

    private static Text GetOrCreatePanelText(Transform parent, string name, Font font)
    {
        Transform child = parent.Find(name);
        Text text = child != null ? child.GetComponent<Text>() : null;
        if (text == null)
        {
            GameObject existing = GameObject.Find(name);
            text = existing != null ? existing.GetComponent<Text>() : null;
        }

        if (text == null)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            text = textObject.GetComponent<Text>();
        }
        else if (text.transform.parent != parent)
        {
            text.transform.SetParent(parent, false);
        }

        text.font = font;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.gameObject.SetActive(true);
        return text;
    }

    private static void SetChildRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void EnsureTextOutline(Text text, Color color, float distance)
    {
        Outline outline = text.GetComponent<Outline>();
        if (outline == null) outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = true;
    }

    private static Sprite GetPlayerPortraitSprite()
    {
        if (playerPortraitSprite != null) return playerPortraitSprite;

        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ArenaPlayerPortraitDiamond",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float diamond = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                float alpha = Mathf.Clamp01(center - diamond + 2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        playerPortraitSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        playerPortraitSprite.name = "ArenaPlayerPortraitDiamond";
        playerPortraitSprite.hideFlags = HideFlags.HideAndDontSave;
        return playerPortraitSprite;
    }

    private static string GetAvatarInitial(string value, int playerID)
    {
        bool isLocalPlayer = playerID == OnlinePlayerContext.LocalPlayerId;
        if (string.IsNullOrEmpty(value)) return isLocalPlayer ? "P" : "O";
        for (int i = 0; i < value.Length; i++)
        {
            if (!char.IsWhiteSpace(value[i])) return char.ToUpperInvariant(value[i]).ToString();
        }

        return isLocalPlayer ? "P" : "O";
    }

    private static Color GetPlayerUiColor(int playerID, float alpha)
    {
        return playerID == OnlinePlayerContext.LocalPlayerId
            ? new Color(0.02f, 0.62f, 0.95f, alpha)
            : new Color(0.94f, 0.12f, 0.10f, alpha);
    }

    private static string GetPlayerTargetLabel(int playerID)
    {
        return playerID == OnlinePlayerContext.LocalPlayerId ? "\u5df1\u65b9\u73a9\u5bb6" : "\u5bf9\u624b\u73a9\u5bb6";
    }

    private bool IsPlayerTargetLegal(int playerID)
    {
        return choosingSpellTarget
            && gameRules != null
            && gameRules.CanTargetPendingCastPlayer(targetHandOwner, playerID);
    }

    private void RefreshPlayerTargetButtonState(
        Button button,
        int playerID,
        bool legal,
        bool selected)
    {
        if (button == null) return;

        button.gameObject.SetActive(true);
        bool actionable = legal && IsRuleInputAvailable();
        button.interactable = actionable;
        Color teamColor = GetPlayerUiColor(playerID, 1f);
        Color panelColor = selected
            ? Color.Lerp(new Color(0.006f, 0.012f, 0.018f, 0.92f), teamColor, 0.52f)
            : legal
            ? Color.Lerp(new Color(0.006f, 0.012f, 0.018f, 0.92f), teamColor, 0.22f)
            : new Color(0.006f, 0.012f, 0.018f, 0.84f);

        Image image = button.GetComponent<Image>();
        if (image != null) image.color = panelColor;

        Transform accent = button.transform.Find("TargetAccent");
        Image accentImage = accent != null ? accent.GetComponent<Image>() : null;
        if (accentImage != null)
        {
            accentImage.color = legal ? teamColor : GetPlayerUiColor(playerID, 0.34f);
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = legal ? GetPlayerUiColor(playerID, 0.96f) : new Color(0f, 0f, 0f, 0.58f);
            outline.effectDistance = selected
                ? new Vector2(6f, -6f)
                : legal
                    ? new Vector2(4f, -4f)
                    : new Vector2(1.5f, -1.5f);
        }
    }

    private void RefreshTargetButtons()
    {
        RefreshPlayerTargetButtonState(
            playerTargetButton,
            OnlinePlayerContext.LocalPlayerId,
            IsPlayerTargetLegal(OnlinePlayerContext.LocalPlayerId),
            gameRules != null
                && gameRules.IsPendingSpellTargetPlayerSelected(OnlinePlayerContext.LocalPlayerId));
        RefreshPlayerTargetButtonState(
            opponentTargetButton,
            OnlinePlayerContext.OpponentPlayerId,
            IsPlayerTargetLegal(OnlinePlayerContext.OpponentPlayerId),
            gameRules != null
                && gameRules.IsPendingSpellTargetPlayerSelected(OnlinePlayerContext.OpponentPlayerId));
    }

    private static int ParseTrailingIndex(string value)
    {
        int underscore = value.LastIndexOf('_');
        if (underscore < 0 || underscore >= value.Length - 1) return -1;
        return int.TryParse(value.Substring(underscore + 1), out int result) ? result : -1;
    }

    private string GetReadableCardName(Transform card)
    {
        RuntimeCard runtimeCard = GetRuntimeCardForVisual(card);
        if (runtimeCard != null) return runtimeCard.Data.cardName;

        if (card.name.Contains("PlayerBattlefield")) return "\u5df1\u65b9\u6218\u573a\u5361";
        if (card.name.Contains("OpponentBattlefield")) return "\u5bf9\u624b\u6218\u573a\u5361";
        if (card.name.Contains("PlayerHand")) return "\u5df1\u65b9\u624b\u724c";
        if (card.name.Contains("OpponentHand")) return "\u5bf9\u624b\u624b\u724c";
        if (card.name.Contains("PlayerCreature")) return "\u5df1\u65b9\u573a\u4e0a\u751f\u7269";
        if (card.name.Contains("OpponentCreature")) return "\u5bf9\u624b\u573a\u4e0a\u751f\u7269";
        if (card.name.Contains("Support")) return "\u652f\u63f4\u5361\u4f4d";
        return card.name;
    }

    private void SetText(string path, string value)
    {
        if (largeCardPreview == null) return;

        Transform textTransform = largeCardPreview.transform.Find(path);
        Text text = textTransform != null ? textTransform.GetComponent<Text>() : null;
        if (text != null) text.text = value;
    }

    private void SetResolveActive(bool active)
    {
        if (resolveButton == null) return;

        bool actionable = active && IsRuleInputAvailable();
        resolveButton.interactable = actionable;

        string label = "\u89e3\u51b3";
        if (confirmingSpellCast)
        {
            label = "\u786e\u8ba4\u65bd\u653e";
        }
        else if (choosingSpellTarget)
        {
            label = HasSelectedSpellTarget() ? "\u786e\u8ba4\u65bd\u653e" : "\u9009\u62e9\u76ee\u6807";
        }
        else if (gameRules != null && gameRules.HasPendingStack())
        {
            label = "\u8ba9\u8fc7\u4f18\u5148\u6743";
        }
        else if (gameRules != null && gameRules.IsDiscardingToHandSize())
        {
            label = "\u5f03\u724c";
        }
        else if (selectedCard != null)
        {
            WhiteboxAction selectedAction = GetSelectedAction(selectedCard);
            label = selectedAction == WhiteboxAction.Attack
                ? "\u5ba3\u544a\u653b\u51fb"
                : selectedAction == WhiteboxAction.PlayCard
                    ? "\u6253\u51fa\u5361\u724c"
                    : selectedAction == WhiteboxAction.Block
                        ? "\u9009\u62e9\u76ee\u6807"
                        : selectedAction == WhiteboxAction.ActivateAbility
                            ? "\u542f\u52a8\u80fd\u529b"
                            : "\u89e3\u51b3";
        }

        Text text = resolveButton.GetComponentInChildren<Text>();
        if (text != null) text.text = active || choosingSpellTarget ? label : "\u89e3\u51b3";

        Image image = resolveButton.GetComponent<Image>();
        if (image != null) image.color = actionable
            ? new Color(0.20f, 0.78f, 0.28f, 0.98f)
            : new Color(0.30f, 0.18f, 0.12f, 0.72f);
    }

    private void SetEndTurnText(string value)
    {
        if (endTurnButton == null) return;

        Text text = endTurnButton.GetComponentInChildren<Text>();
        if (text != null) text.text = value;
    }

    private void SetEndTurnInteractable(bool interactable)
    {
        if (endTurnButton == null) return;

        interactable = interactable && IsRuleInputAvailable();
        endTurnButton.interactable = interactable;
        Image image = endTurnButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = interactable
                ? new Color(1f, 0.42f, 0.08f, 0.98f)
                : new Color(0.30f, 0.18f, 0.12f, 0.72f);
        }
    }

    private void OnCastCancelClicked()
    {
        if (!IsRuleInputAvailable()) return;
        if (gameRules != null && gameRules.IsChoosingCastFeature())
        {
            if (gameRules.ApplyAction(new GameAction
                {
                    type = GameActionType.CancelCast,
                    playerId = OnlinePlayerContext.LocalPlayerId
                }).accepted)
            {
                SetCastCancelButtonVisible(false);
                RestoreHandInputAfterTargetCancel();
                RefreshRulesHud();
            }
            return;
        }

        if (!choosingSpellTarget && !confirmingSpellCast) return;
        ClearSpellTargetSelection("\u5df2\u64a4\u56de\u65bd\u653e\u3002");
    }

    private void OnResolveClicked()
    {
        if (!IsRuleInputAvailable()) return;
        if (choosingSpellTarget || confirmingSpellCast)
        {
            TryConfirmPendingTargetSpell();
            return;
        }

        if (gameRules != null && gameRules.HasPendingStack())
        {
            gameRules.ApplyAction(new GameAction
            {
                type = GameActionType.AdvancePhase,
                playerId = OnlinePlayerContext.LocalPlayerId
            });
            SelectCard(null);
            return;
        }

        if (selectedCard == null)
        {
            SetHint("\u6ca1\u6709\u9009\u4e2d\u7684\u5361\u724c\u3002");
            return;
        }

        WhiteboxAction action = GetSelectedAction(selectedCard);
        if (action == WhiteboxAction.PlayCard && TryGetHandIndex(selectedCard, out int handOwner, out int handIndex))
        {
            string cardName = GetReadableCardName(selectedCard);
            bool played = BeginHandCastThroughAction(handOwner, handIndex);
            if (!gameRules.HasActiveChoice())
            {
                SetHint(played
                    ? $"\u5df2\u6253\u51fa {cardName}\u3002"
                    : "\u5f53\u524d\u4e0d\u80fd\u6253\u51fa\uff1a\u68c0\u67e5\u56de\u5408\u3001\u9636\u6bb5\u3001\u8d39\u7528\u6216\u5806\u53e0\u3002");
                SelectCard(null);
            }
            return;
        }

        if (action == WhiteboxAction.Attack && TryGetBattlefieldIndex(selectedCard, out int owner, out int battlefieldIndex))
        {
            string cardName = GetReadableCardName(selectedCard);
            gameRules.ApplyAction(new GameAction
            {
                type = GameActionType.ToggleAttacker,
                playerId = owner,
                zoneIndex = battlefieldIndex
            });
            SetHint($"\u5df2\u9009\u62e9 {cardName} \u4f5c\u4e3a\u653b\u51fb\u8005\u3002");
            SelectCard(null);
            return;
        }

        if (action == WhiteboxAction.Block)
        {
            SetHint("\u8bf7\u70b9\u51fb\u5bf9\u9762\u7684\u653b\u51fb\u8005\u6765\u6307\u5b9a\u963b\u6321\u3002");
            return;
        }

        if (action == WhiteboxAction.ActivateAbility && TryGetBattlefieldIndex(selectedCard, out int abilityOwner, out int abilityIndex))
        {
            RuntimeCard runtimeCard = gameRules.GetBattlefieldCard(abilityOwner, abilityIndex);
            bool wasTapped = runtimeCard != null && runtimeCard.IsTapped;
            if (runtimeCard != null
                && gameRules.ApplyAction(new GameAction
                {
                    type = GameActionType.ActivateAbility,
                    playerId = abilityOwner,
                    sourceObjectId = runtimeCard.ObjectId
                }).accepted)
            {
                SetHint(gameRules.IsResolvingGenericChoice()
                    ? $"\u8bf7\u9009\u62e9 {runtimeCard.Data.cardName} \u8981\u542f\u52a8\u7684\u80fd\u529b\u3002"
                    : wasTapped && !runtimeCard.IsTapped
                        ? $"\u5df2\u91cd\u7f6e {runtimeCard.Data.cardName}\uff0c\u5e76\u79fb\u9664\u672a\u4f7f\u7528\u7684\u6cd5\u529b\u3002"
                        : $"\u5df2\u542f\u52a8 {runtimeCard.Data.cardName} \u7684\u80fd\u529b\u3002");
                SelectCard(null);
                RefreshRulesHud();
                return;
            }

            SetHint("\u65e0\u6cd5\u542f\u52a8\u8be5\u80fd\u529b\u3002");
            return;
        }

        SetHint("\u5f53\u524d\u9636\u6bb5\u4e0d\u80fd\u6267\u884c\u8be5\u52a8\u4f5c\u3002");
    }

    public void HandlePrimaryAction()
    {
        OnEndTurnClicked();
    }

    private void OnEndTurnClicked()
    {
        if (gameRules == null || !IsRuleInputAvailable()) return;
        if (gameRules.IsOrderingTriggeredAbilities())
        {
            GameChoiceRequest choice = gameRules.GetActiveChoice();
            if (gameRules.ApplyAction(new GameAction
                {
                    type = GameActionType.ConfirmChoice,
                    playerId = OnlinePlayerContext.LocalPlayerId,
                    choiceRequestId = choice?.requestId ?? 0
                }).accepted)
            {
                SetHint("\u5df2\u6309\u9009\u62e9\u987a\u5e8f\u5c06\u89e6\u53d1\u5f0f\u5f02\u80fd\u653e\u5165\u5806\u53e0\u3002");
            }
            else
            {
                SetHint("\u8bf7\u5148\u6309\u4ece\u5806\u53e0\u5e95\u5230\u5806\u53e0\u9876\u7684\u987a\u5e8f\u9009\u5b8c\u6240\u6709\u89e6\u53d1\u5f0f\u5f02\u80fd\u3002");
            }

            RefreshRulesHud();
            return;
        }

        if (gameRules.IsResolvingGenericChoice())
        {
            GameChoiceRequest choice = gameRules.GetActiveChoice();
            if (gameRules.ApplyAction(new GameAction
                {
                    type = GameActionType.ConfirmChoice,
                    playerId = OnlinePlayerContext.LocalPlayerId,
                    choiceRequestId = choice?.requestId ?? 0
                }).accepted)
            {
                SetHint("\u5df2\u786e\u8ba4\u9009\u62e9\u3002");
            }
            else
            {
                SetHint("\u8bf7\u5148\u5b8c\u6210\u5f53\u524d\u9009\u62e9\u3002");
            }

            RefreshRulesHud();
            return;
        }

        if (gameRules.IsChoosingLegendRule())
        {
            int player = gameRules.GetLegendRulePlayer();
            GameChoiceRequest choice = gameRules.GetActiveChoice();
            if (gameRules.ApplyAction(new GameAction
                {
                    type = GameActionType.ConfirmChoice,
                    playerId = player,
                    choiceRequestId = choice?.requestId ?? 0
                }).accepted)
            {
                SetCardGlow(selectedCard, false, "InteractionSelectedGlow");
                selectedCard = null;
                SetHint("\u5df2\u6267\u884c\u4f20\u5947\u89c4\u5219\u3002");
            }
            else
            {
                SetHint("\u8bf7\u5148\u9009\u62e9\u4e00\u4e2a\u540c\u540d\u4f20\u5947\u6c38\u4e45\u7269\u4fdd\u7559\u3002");
            }

            RefreshRulesHud();
            return;
        }

        if (gameRules.IsDiscardingToHandSize())
        {
            int player = gameRules.GetDiscardToHandSizePlayer();
            if (gameRules.ApplyAction(new GameAction
                {
                    type = GameActionType.ConfirmDiscard,
                    playerId = player
                }).accepted)
            {
                SetHint("\u5df2\u786e\u8ba4\u5f03\u724c\u3002");
            }
            else
            {
                SetHint($"\u8bf7\u9009\u62e9 {gameRules.GetDiscardToHandSizeRequiredCount()} \u5f20\u624b\u724c\u540e\u518d\u786e\u8ba4\u3002");
            }
            RefreshRulesHud();
            return;
        }

        if (choosingSpellTarget || confirmingSpellCast)
        {
            TryConfirmPendingTargetSpell();
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
            RefreshRulesHud();
            return;
        }

        if (previousPhase == TurnPhase.DeclareBlockers)
        {
            string blockReason = gameRules.GetDeclareBlockersAdvanceBlockReason();
            if (!string.IsNullOrEmpty(blockReason))
            {
                SetHint(blockReason);
                RefreshRulesHud();
                return;
            }
        }

        GameActionResult result = gameRules.ApplyAction(new GameAction
        {
            type = primaryAction,
            playerId = localPlayer
        });
        if (!result.accepted)
        {
            GameChoiceRequest choice = gameRules.GetActiveChoice();
            SetHint(choice != null
                ? $"请先完成或取消当前操作：{choice.prompt}"
                : "当前不能结束步骤：请检查优先权或尚未完成的游戏动作。");
            RefreshRulesHud();
            return;
        }
        if (previousPhase == TurnPhase.DeclareBlockers
            && localPlayer != gameRules.GetCurrentPlayer())
        {
            SetHint("\u5df2\u786e\u8ba4\u963b\u6321\uff0c\u8fdb\u5165\u6218\u6597\u4f24\u5bb3\u3002");
        }
        else
        {
            SetHint("已让过优先权。");
        }
        RefreshRulesHud();
    }

    private static bool IsRuleInputAvailable()
    {
        return OnlineMatchService.Instance?.CanSubmitRuleAction != false;
    }

    private enum WhiteboxAction
    {
        None,
        PlayCard,
        Attack,
        Block,
        ActivateAbility
    }
}
