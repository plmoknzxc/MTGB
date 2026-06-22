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
    [SerializeField] private Text phaseStatusText;
    [SerializeField] private Text stackStatusText;
    [SerializeField] private Text manaPoolText;
    [SerializeField] private Transform playerBattlefieldArea;
    [SerializeField] private Transform opponentBattlefieldArea;

    private const string HintIdle = "\u60ac\u505c\u5361\u724c\u67e5\u770b\u9884\u89c8\uff0c\u62d6\u52a8\u624b\u724c\u5230\u5df1\u65b9\u6218\u573a\u6253\u51fa\u3002";
    private const string HintDrag = "\u62d6\u5230\u5df1\u65b9\u6218\u573a\u533a\u57df\u540e\u677e\u5f00\u9f20\u6807\u6253\u51fa\u3002";
    private const string HintResolveStack = "\u8bf7\u5148\u89e3\u51b3\u5806\u53e0\u3002";

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
    private float pulseTime;

    public bool IsDraggingCard => isDragging;

    private void Awake()
    {
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
        if (phaseStatusText == null) phaseStatusText = GameObject.Find("ArenaPhaseStatusText")?.GetComponent<Text>();
        if (stackStatusText == null) stackStatusText = GameObject.Find("ArenaStackStatusText")?.GetComponent<Text>();
        if (manaPoolText == null) manaPoolText = GameObject.Find("ArenaManaPoolText")?.GetComponent<Text>();
        if (playerBattlefieldArea == null) playerBattlefieldArea = cardRoot?.Find("PlayerBattlefieldArea");
        if (opponentBattlefieldArea == null) opponentBattlefieldArea = cardRoot?.Find("OpponentBattlefieldArea");

        if (resolveButton != null) resolveButton.onClick.AddListener(OnResolveClicked);
        if (endTurnButton != null) endTurnButton.onClick.AddListener(OnEndTurnClicked);
        GameEvents.OnPhaseChanged += OnPhaseChanged;
        GameEvents.OnStackChanged += OnStackChanged;
        GameEvents.OnCardTapped += OnCardStateChanged;
        GameEvents.OnCardUntapped += OnCardStateChanged;

        RefreshRulesHud();
        SetHint(HintIdle);
    }

    private void OnDestroy()
    {
        if (resolveButton != null) resolveButton.onClick.RemoveListener(OnResolveClicked);
        if (endTurnButton != null) endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        GameEvents.OnPhaseChanged -= OnPhaseChanged;
        GameEvents.OnStackChanged -= OnStackChanged;
        GameEvents.OnCardTapped -= OnCardStateChanged;
        GameEvents.OnCardUntapped -= OnCardStateChanged;
    }

    private void Update()
    {
        if (isDragging)
        {
            UpdateDrag();
            UpdatePulse();
            RefreshRulesHud();
            return;
        }

        UpdateHover();
        UpdatePulse();
        RefreshRulesHud();

        if (Input.GetMouseButtonDown(0))
        {
            if (!TryBeginDrag(hoveredCard))
            {
                if (TryActivateManaAbilityOnClick(hoveredCard)) return;
                SelectCard(hoveredCard);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SelectCard(null);
        }
    }

    private bool TryBeginDrag(Transform card)
    {
        if (card == null || gameRules == null) return false;
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
        Transform targetArea = owner == 1 ? playerBattlefieldArea : opponentBattlefieldArea;
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
            bool played = gameRules.PlayCard(owner, handIndex);
            if (played)
            {
                SetHint(gameRules.HasPendingStack()
                    ? $"\u5df2\u6253\u51fa\uff1a{cardName}\uff0c\u8bf7\u70b9\u51fb\u89e3\u51b3\u5806\u53e0\u3002"
                    : $"\u5df2\u6253\u51fa\uff1a{cardName}");
            }
            else
            {
                ManaCost needCost = runtimeCard != null && !runtimeCard.Data.HasType(CardType.Land)
                    ? runtimeCard.Data.GetManaCost() : default;
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
        SetCardGlow(hoveredCard, IsCardActionable(hoveredCard), "InteractionHoverGlow");
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
        SetResolveActive(IsCardActionable(selectedCard) || (gameRules != null && gameRules.HasPendingStack()));
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
        if (selectedCard != null)
        {
            SetHint($"\u5df2\u9009\u62e9\uff1a{GetReadableCardName(selectedCard)}\u3002\u70b9\u51fb\u53f3\u4e0b\u6309\u94ae\u6267\u884c\u3002");
        }
        else if (hoveredCard != null)
        {
            WhiteboxAction action = GetSelectedAction(hoveredCard);
            string actionText = action == WhiteboxAction.PlayCard
                ? "\u62d6\u5230\u5df1\u65b9\u6218\u573a\u6253\u51fa"
                : action == WhiteboxAction.Attack
                    ? "\u70b9\u51fb\u9009\u62e9\u5e76\u5ba3\u544a\u653b\u51fb"
                    : action == WhiteboxAction.ActivateAbility
                        ? "\u70b9\u51fb\u9009\u62e9\u5e76\u542f\u52a8\u80fd\u529b"
                        : "\u5f53\u524d\u4e0d\u53ef\u884c\u52a8";
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
        if (gameRules == null) return;

        if (playerLifeText != null) playerLifeText.text = gameRules.GetLife(1).ToString();
        if (opponentLifeText != null) opponentLifeText.text = gameRules.GetLife(2).ToString();
        if (phaseStatusText != null) phaseStatusText.text = $"P{gameRules.GetCurrentPlayer()} - {gameRules.GetCurrentPhase()}";

        if (stackStatusText != null)
        {
            RuntimeCard top = gameRules.GetTopStackCard();
            int stackCount = gameRules.GetStackCount();
            stackStatusText.text = stackCount == 0 ? "Stack: empty" : $"Stack x{stackCount}: {top.Data.cardName}";
        }

        if (manaPoolText != null)
        {
            var pool = gameRules.GetManaPool(gameRules.GetCurrentPlayer());
            manaPoolText.text = pool.IsEmpty ? "Mana: 0" : $"Mana: {pool.ToDisplayString()}";
        }

        if (gameRules.HasPendingStack())
        {
            SetResolveActive(true);
            SetEndTurnText("\u7b49\u5f85\u54cd\u5e94");
        }
        else if (selectedCard == null)
        {
            SetResolveActive(false);
            SetEndTurnText("\u7ed3\u675f\u56de\u5408");
        }
        else
        {
            SetResolveActive(IsCardActionable(selectedCard));
        }
    }

    private void OnPhaseChanged(TurnPhase phase)
    {
        RefreshRulesHud();
        SetHint($"\u9636\u6bb5\u53d8\u66f4\uff1aP{gameRules.GetCurrentPlayer()} - {phase}");
    }

    private void OnStackChanged(int stackCount)
    {
        RefreshRulesHud();
        SetHint(stackCount == 0
            ? "\u5806\u53e0\u5df2\u6e05\u7a7a\u3002"
            : $"\u5806\u53e0\u4e2d\u6709 {stackCount} \u4e2a\u5bf9\u8c61\uff0c\u70b9\u51fb\u89e3\u51b3\u3002");
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
            if (gameRules.CanDeclareAttacker(battlefieldOwner, battlefieldIndex))
                return WhiteboxAction.Attack;

            RuntimeCard runtimeCard = gameRules.GetBattlefieldCard(battlefieldOwner, battlefieldIndex);
            if (runtimeCard != null && gameRules.CanActivateAnyAbility(runtimeCard))
                return WhiteboxAction.ActivateAbility;

            return WhiteboxAction.None;
        }

        return WhiteboxAction.None;
    }

    private bool IsCardActionable(Transform card)
    {
        return GetSelectedAction(card) != WhiteboxAction.None;
    }

    private static bool TryGetHandIndex(Transform card, out int owner, out int index)
    {
        owner = card.name.Contains("Opponent") ? 2 : 1;
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

        owner = card.name.Contains("Opponent") ? 2 : 1;

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

    private bool TryActivateManaAbilityOnClick(Transform card)
    {
        if (gameRules == null || card == null) return false;
        if (!TryGetBattlefieldIndex(card, out int owner, out int battlefieldIndex)) return false;

        RuntimeCard runtimeCard = gameRules.GetBattlefieldCard(owner, battlefieldIndex);
        if (runtimeCard == null || !runtimeCard.Data.HasType(CardType.Land)) return false;

        if (owner != gameRules.GetCurrentPlayer())
        {
            SetHint("\u4e0d\u80fd\u542f\u52a8\u5bf9\u624b\u7684\u5730\u3002");
            return true;
        }

        bool wasTapped = runtimeCard.IsTapped;
        if (gameRules.ActivateAbility(runtimeCard))
        {
            SelectCard(null);
            RefreshRulesHud();
            SetHint(wasTapped && !runtimeCard.IsTapped
                ? $"\u5df2\u91cd\u7f6e {runtimeCard.Data.cardName}\uff0c\u5e76\u79fb\u9664\u672a\u4f7f\u7528\u7684\u6cd5\u529b\u3002\u6cd5\u529b\u6c60\uff1a{gameRules.GetManaPool(owner).ToDisplayString()}\u3002"
                : $"\u5df2\u6a2a\u7f6e {runtimeCard.Data.cardName}\uff0c\u6cd5\u529b\u6c60\uff1a{gameRules.GetManaPool(owner).ToDisplayString()}\u3002");
            return true;
        }

        SetHint($"{runtimeCard.Data.cardName} \u5df2\u6a2a\u7f6e\uff0c\u4ea7\u751f\u7684\u6cd5\u529b\u5df2\u88ab\u4f7f\u7528\uff0c\u4e0d\u80fd\u518d\u64a4\u56de\u3002");
        RefreshRulesHud();
        return true;
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

        string label = "\u89e3\u51b3";
        if (gameRules != null && gameRules.HasPendingStack())
        {
            label = "\u89e3\u51b3\u5806\u53e0";
        }
        else if (selectedCard != null)
        {
            WhiteboxAction selectedAction = GetSelectedAction(selectedCard);
            label = selectedAction == WhiteboxAction.Attack
                ? "\u5ba3\u544a\u653b\u51fb"
                : selectedAction == WhiteboxAction.PlayCard
                    ? "\u6253\u51fa\u5361\u724c"
                    : selectedAction == WhiteboxAction.ActivateAbility
                        ? "\u542f\u52a8\u80fd\u529b"
                        : "\u89e3\u51b3";
        }

        Text text = resolveButton.GetComponentInChildren<Text>();
        if (text != null) text.text = active ? label : "\u89e3\u51b3";

        Image image = resolveButton.GetComponent<Image>();
        if (image != null) image.color = active ? new Color(0.20f, 0.78f, 0.28f, 0.98f) : new Color(1f, 0.42f, 0.08f, 0.98f);
    }

    private void SetEndTurnText(string value)
    {
        if (endTurnButton == null) return;

        Text text = endTurnButton.GetComponentInChildren<Text>();
        if (text != null) text.text = value;
    }

    private void OnResolveClicked()
    {
        if (gameRules != null && gameRules.HasPendingStack())
        {
            gameRules.ResolveTopOfStack();
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
            bool played = gameRules.PlayCard(handOwner, handIndex);
            SetHint(played
                ? $"\u5df2\u5c06 {cardName} \u653e\u5165\u5806\u53e0\u3002"
                : "\u5f53\u524d\u4e0d\u80fd\u6253\u51fa\uff1a\u68c0\u67e5\u56de\u5408\u3001\u9636\u6bb5\u3001\u8d39\u7528\u6216\u5806\u53e0\u3002");
            SelectCard(null);
            return;
        }

        if (action == WhiteboxAction.Attack && TryGetBattlefieldIndex(selectedCard, out int owner, out int battlefieldIndex))
        {
            string cardName = GetReadableCardName(selectedCard);
            gameRules.DeclareAttacker(owner, battlefieldIndex);
            SetHint($"\u5df2\u5ba3\u544a {cardName} \u653b\u51fb\u3002");
            SelectCard(null);
            return;
        }

        if (action == WhiteboxAction.ActivateAbility && TryGetBattlefieldIndex(selectedCard, out int abilityOwner, out int abilityIndex))
        {
            RuntimeCard runtimeCard = gameRules.GetBattlefieldCard(abilityOwner, abilityIndex);
            bool wasTapped = runtimeCard != null && runtimeCard.IsTapped;
            if (runtimeCard != null && gameRules.ActivateAbility(runtimeCard))
            {
                var ability = runtimeCard.GetFirstActivatableAbility(gameRules);
                string abilityName = ability != null ? ability.Name : "\u80fd\u529b";
                SetHint(wasTapped && !runtimeCard.IsTapped
                    ? $"\u5df2\u91cd\u7f6e {runtimeCard.Data.cardName}\uff0c\u5e76\u79fb\u9664\u672a\u4f7f\u7528\u7684\u6cd5\u529b\u3002"
                    : $"\u5df2\u542f\u52a8\uff1a{abilityName}\u3002");
                SelectCard(null);
                return;
            }

            SetHint("\u65e0\u6cd5\u542f\u52a8\u8be5\u80fd\u529b\u3002");
            return;
        }

        SetHint("\u5f53\u524d\u9636\u6bb5\u4e0d\u80fd\u6267\u884c\u8be5\u52a8\u4f5c\u3002");
    }

    private void OnEndTurnClicked()
    {
        if (gameRules == null) return;

        if (gameRules.HasPendingStack())
        {
            SetHint(HintResolveStack);
            return;
        }

        gameRules.EndTurn();
        RefreshRulesHud();
    }

    private enum WhiteboxAction
    {
        None,
        PlayCard,
        Attack,
        ActivateAbility
    }
}
