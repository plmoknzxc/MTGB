using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 双手牌（spec 第 6 节）。
/// 己方：底部居中扇形，卡 124×173、重叠 −30、扇角 ±9°、下沉弧度 0–2px；
///       悬停上浮 38px、放大 1.14、蓝色描边 + 辉光；点击可打出地牌（PlayHandCard）。
/// 对手：顶部居中扇形卡背，卡 78×110（≈己方 63%）、扇角 ±12°、压暗降饱和、右下角数量圆标。
/// </summary>
public class IntegratedHandController : MonoBehaviour
{
    private const float PlayerCardW = 124f, PlayerCardH = 173f;
    private const float PlayerOverlap = -30f, PlayerFanDeg = 9f, PlayerLift = 38f, PlayerHoverScale = 1.14f;
    private const float PlayerBaseY = 8f, PlayerArcDepth = 2f, PlayerSafeWidth = 1280f;
    private const float OpponentCardW = 78f, OpponentCardH = 110f;
    private const float OpponentOverlap = -18f, OpponentFanDeg = 12f, OpponentTopY = 44f;
    private const float OpponentBrightness = 0.47f;

    [SerializeField] private GameRules gameRules;
    [SerializeField] private InspectPanelController inspectPanel;

    private RectTransform playerRoot;
    private RectTransform opponentRoot;
    private readonly List<HandSlot> playerCards = new List<HandSlot>(12);
    private readonly List<HandSlot> opponentCards = new List<HandSlot>(12);
    private int playerHoverIndex = -1;
    private Text opponentCountText;

    private sealed class HandSlot
    {
        public IntegratedCardUi Card;
        public int Index;
        public RuntimeCard LastCard;
        public bool LastFaceDown;
    }

    private void Awake() => EnsureBuilt();

    /// <summary>场景重载后重建（手牌根与卡槽引用不随序列化保留）。</summary>
    public void EnsureBuilt()
    {
        if ((playerRoot != null && opponentRoot != null) || gameRules == null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        Initialize(gameRules, canvas.transform, inspectPanel);
    }

    public void Initialize(GameRules rules, Transform canvasTransform, InspectPanelController inspect)
    {
        gameRules = rules;
        inspectPanel = inspect;
        foreach (string rootName in new[] { "PlayerHandRoot", "OpponentHandRoot" })
        {
            Transform stale = canvasTransform.Find(rootName);
            if (stale != null) Destroy(stale.gameObject);
        }
        playerCards.Clear();
        opponentCards.Clear();
        opponentCountText = null;

        playerRoot = IntegratedUiUtility.CreateRect(canvasTransform, "PlayerHandRoot");
        playerRoot.anchorMin = new Vector2(0.5f, 0f);
        playerRoot.anchorMax = new Vector2(0.5f, 0f);
        playerRoot.pivot = new Vector2(0.5f, 0f);
        playerRoot.anchoredPosition = new Vector2(0f, PlayerBaseY);
        playerRoot.SetAsLastSibling();

        opponentRoot = IntegratedUiUtility.CreateRect(canvasTransform, "OpponentHandRoot");
        opponentRoot.anchorMin = new Vector2(0.5f, 1f);
        opponentRoot.anchorMax = new Vector2(0.5f, 1f);
        opponentRoot.pivot = new Vector2(0.5f, 1f);
        opponentRoot.anchoredPosition = new Vector2(0f, -OpponentTopY);
    }

    private void Update()
    {
        if (gameRules == null || !gameRules.IsInitialized()) return;
        if (playerRoot == null || opponentRoot == null) EnsureBuilt();
        if (playerRoot == null || opponentRoot == null) return;
        RefreshHand(playerCards, playerRoot, OnlinePlayerContext.LocalPlayerId, false);
        RefreshHand(opponentCards, opponentRoot, OnlinePlayerContext.OpponentPlayerId, true);
    }

    private void RefreshHand(List<HandSlot> slots, RectTransform root, int owner, bool opponent)
    {
        IReadOnlyList<RuntimeCard> cards = gameRules.GetHandCards(owner);
        while (slots.Count < cards.Count)
        {
            var slot = new HandSlot
            {
                Card = IntegratedCardUi.Create(root,
                    opponent ? $"OpponentHandCard_{slots.Count + 1}" : $"PlayerHandCard_{slots.Count + 1}",
                    opponent ? new Vector2(OpponentCardW, OpponentCardH) : new Vector2(PlayerCardW, PlayerCardH)),
                Index = slots.Count
            };
            AddPointerHandlers(slot.Card, opponent);
            slots.Add(slot);
        }

        for (int i = 0; i < slots.Count; i++)
        {
            bool active = i < cards.Count;
            slots[i].Card.gameObject.SetActive(active);
            if (!active) continue;
            slots[i].Index = i;
            if (!ReferenceEquals(slots[i].LastCard, cards[i]) || slots[i].LastFaceDown != opponent)
            {
                slots[i].Card.SetCard(cards[i], opponent);
                slots[i].LastCard = cards[i];
                slots[i].LastFaceDown = opponent;
            }
        }

        if (slots.Count > cards.Count)
        {
            for (int i = cards.Count; i < slots.Count; i++)
            {
                if (slots[i].Card != null) Destroy(slots[i].Card.gameObject);
            }
            slots.RemoveRange(cards.Count, slots.Count - cards.Count);
        }

        if (opponent)
        {
            LayoutOpponent(cards.Count);
            RefreshOpponentCount(cards.Count);
        }
        else
        {
            LayoutPlayer();
        }
    }

    /// <summary>对手卡背扇：78×110、重叠 −18、扇角 ±12°（原点 50% 130% 以顶部枢轴近似）。</summary>
    private void LayoutOpponent(int count)
    {
        if (count == 0) return;
        float spacing = OpponentCardW + OpponentOverlap;
        float center = (count - 1) * 0.5f;
        float rotationPerOffset = count > 1 ? (OpponentFanDeg * 2f) / (count - 1) : 0f;
        for (int i = 0; i < count; i++)
        {
            RectTransform rect = opponentCards[i].Card.Rect;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            float offset = i - center;
            rect.anchoredPosition = new Vector2(offset * spacing, 0f);
            rect.localRotation = Quaternion.Euler(0f, 0f, offset * rotationPerOffset);
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(OpponentCardW, OpponentCardH);
        }
    }

    private void RefreshOpponentCount(int count)
    {
        Text badge = EnsureOpponentCountBadge();
        if (count <= 0 || opponentCards.Count == 0)
        {
            badge.gameObject.SetActive(false);
            return;
        }

        IntegratedCardUi last = opponentCards[count - 1].Card;
        badge.gameObject.SetActive(true);
        badge.transform.SetParent(last.transform, false);
        badge.rectTransform.anchorMin = new Vector2(1f, 0f);
        badge.rectTransform.anchorMax = new Vector2(1f, 0f);
        badge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        badge.rectTransform.anchoredPosition = new Vector2(-OpponentCardW * 0.09f, OpponentCardH * 0.09f);
        badge.rectTransform.sizeDelta = new Vector2(24f, 24f);
        badge.text = count.ToString();
    }

    private Text EnsureOpponentCountBadge()
    {
        if (opponentCountText != null) return opponentCountText;
        Image badge = IntegratedUiUtility.Panel(opponentRoot, "OpponentHandCount", IntegratedArenaPalette.Hex("#161B24FF"));
        badge.sprite = IntegratedUiUtility.Circle(48);
        badge.raycastTarget = false;
        ObsidianArenaTheme.EnsureOutline(badge, IntegratedArenaPalette.Hex("#39414FFF"), new Vector2(1f, -1f));
        opponentCountText = IntegratedUiUtility.Text(badge.transform, "Count", "0", 11,
            IntegratedArenaPalette.Text, TextAnchor.MiddleCenter);
        IntegratedUiUtility.Stretch(opponentCountText.rectTransform, 0f, 0f, 0f, 0f);
        return opponentCountText;
    }

    // ---------- 己方扇形布局 + 悬停 ----------

    private void AddPointerHandlers(IntegratedCardUi card, bool opponent)
    {
        HandHoverHandler handler = card.gameObject.AddComponent<HandHoverHandler>();
        handler.Controller = this;
        handler.Opponent = opponent;
    }

    private void LayoutPlayer()
    {
        int count = playerCards.Count;
        if (count == 0) return;
        int hoverIndex = playerHoverIndex >= count ? -1 : playerHoverIndex;

        float spacing = PlayerCardW + PlayerOverlap;
        float naturalWidth = PlayerCardW + (count - 1) * spacing;
        float scale = 1f;
        if (naturalWidth > PlayerSafeWidth)
        {
            scale = Mathf.Max(0.62f, PlayerSafeWidth / naturalWidth);
            spacing *= scale;
        }

        float center = (count - 1) * 0.5f;
        float rotationPerOffset = count > 1 ? (PlayerFanDeg * 2f) / (count - 1) : 0f;
        for (int i = 0; i < count; i++)
        {
            IntegratedCardUi card = playerCards[i].Card;
            float offset = i - center;
            bool hovered = i == hoverIndex;

            RectTransform rect = card.Rect;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);

            float arcDown = Mathf.Abs(offset) / Mathf.Max(1f, center) * PlayerArcDepth;
            Vector2 position = new Vector2(offset * spacing, -arcDown * scale);
            float rotation = offset * rotationPerOffset;
            float cardScale = scale;

            if (hovered)
            {
                position.y += PlayerLift;
                rotation = 0f;
                cardScale = scale * PlayerHoverScale;
            }

            // 悬停卡置于最上层
            if (hovered) rect.SetAsLastSibling();
            else if (rect.GetSiblingIndex() != i) rect.SetSiblingIndex(i);

            card.SetHoverVisual(hovered);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            rect.localScale = new Vector3(cardScale, cardScale, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(PlayerCardW, PlayerCardH);
        }

        // 数量徽标挂在最后一张卡上，重排 sibling 后需要保持最上层
        if (opponentCountText != null) opponentCountText.transform.SetAsLastSibling();
    }

    public void NotifyHover(IntegratedCardUi card, bool hovered, bool opponent)
    {
        if (opponent) return;
        for (int i = 0; i < playerCards.Count; i++)
        {
            if (playerCards[i].Card != card) continue;
            playerHoverIndex = hovered ? i : (playerHoverIndex == i ? -1 : playerHoverIndex);
            if (hovered)
            {
                if (inspectPanel != null && gameRules != null)
                {
                    RuntimeCard runtimeCard = gameRules.GetHandCard(OnlinePlayerContext.LocalPlayerId, i);
                    if (runtimeCard != null) inspectPanel.Show(runtimeCard);
                }
                LayoutPlayer();
            }
            else if (playerHoverIndex < 0)
            {
                LayoutPlayer();
            }
            return;
        }
    }

    public void NotifyClick(IntegratedCardUi card)
    {
        if (gameRules == null || OnlineMatchService.Instance?.CanSubmitRuleAction == false) return;
        for (int i = 0; i < playerCards.Count; i++)
        {
            if (playerCards[i].Card != card) continue;
            int owner = OnlinePlayerContext.LocalPlayerId;
            RuntimeCard runtimeCard = gameRules.GetHandCard(owner, i);
            if (runtimeCard == null) return;
            if (!gameRules.CanPlayCard(owner, i)) return;

            gameRules.ApplyAction(new GameAction
            {
                type = GameActionType.PlayHandCard,
                playerId = owner,
                zoneIndex = i
            });
            return;
        }
    }

    private sealed class HandHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public IntegratedHandController Controller;
        public bool Opponent;

        public void OnPointerEnter(PointerEventData eventData) => Controller?.NotifyHover(GetComponent<IntegratedCardUi>(), true, Opponent);
        public void OnPointerExit(PointerEventData eventData) => Controller?.NotifyHover(GetComponent<IntegratedCardUi>(), false, Opponent);
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Opponent && eventData.button == PointerEventData.InputButton.Left)
                Controller?.NotifyClick(GetComponent<IntegratedCardUi>());
        }
    }
}
