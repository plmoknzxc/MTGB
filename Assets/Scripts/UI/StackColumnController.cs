using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 堆叠列（spec 第 7 节，MTGA 式）：桌面右缘内侧（right:70、垂直中于桌面上部 top:290），宽 172。
/// 标题「堆叠 STACK · N」→ 堆叠项（最新在最上，44px 横条 = 迷你卡背 + 名称 + 类型/费用）→
/// 溢出尾项「+N 项 · 滚动查看」。当前结算项蓝框 + 右侧「结算中」小签。
/// 点击/悬停 → 左栏检视放大；多项时 ScrollRect 拖动滚动。
/// </summary>
public class StackColumnController : MonoBehaviour
{
    private const float ColumnWidth = 172f;
    private const float ItemHeight = 44f;
    private const float ItemGap = 8f;
    private const int MaxVisibleItems = 5;

    [SerializeField] private GameRules gameRules;
    [SerializeField] private InspectPanelController inspectPanel;

    private Text headerText;
    private RectTransform content;
    private GameObject tailItem;
    private Text tailText;
    private readonly List<StackItemView> items = new List<StackItemView>(8);

    private sealed class StackItemView
    {
        public GameObject Root;
        public Text Title;
        public Text Sub;
        public Image Frame;
        public RuntimeCard Card;
        public bool Resolving;
    }

    private void Awake() => EnsureBuilt();

    /// <summary>场景重载后重建（Initialize 创建的 UI 不随序列化保留引用）。</summary>
    public void EnsureBuilt()
    {
        if (headerText != null || gameRules == null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        items.Clear();
        tailItem = null;
        Initialize(gameRules, canvas.transform, inspectPanel);
    }

    public void Initialize(GameRules rules, Transform canvasTransform, InspectPanelController inspect)
    {
        Transform stale = canvasTransform.Find("StackColumn");
        if (stale != null) Destroy(stale.gameObject);
        items.Clear();
        tailItem = null;
        gameRules = rules;
        inspectPanel = inspect;

        RectTransform root = IntegratedUiUtility.CreateRect(canvasTransform, "StackColumn");
        root.anchorMin = new Vector2(1f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 0f);
        root.anchoredPosition = new Vector2(-70f, -290f);
        root.sizeDelta = new Vector2(ColumnWidth, 420f);

        headerText = IntegratedUiUtility.Text(root, "Header", "堆叠  STACK · 0", 9,
            IntegratedArenaPalette.FaintText, TextAnchor.UpperRight);
        headerText.rectTransform.anchorMin = new Vector2(0f, 1f);
        headerText.rectTransform.anchorMax = new Vector2(1f, 1f);
        headerText.rectTransform.pivot = new Vector2(1f, 1f);
        headerText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
        headerText.rectTransform.sizeDelta = new Vector2(0f, 14f);

        // 滚动容器（无滚动条，拖动/滚轮滚动）
        RectTransform viewport = IntegratedUiUtility.CreateRect(root, "Viewport");
        viewport.anchorMin = new Vector2(0f, 1f);
        viewport.anchorMax = new Vector2(1f, 1f);
        viewport.pivot = new Vector2(0.5f, 1f);
        viewport.anchoredPosition = new Vector2(0f, -18f);
        viewport.sizeDelta = new Vector2(0f, 402f);
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        ScrollRect scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        content = IntegratedUiUtility.CreateRect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);
        scrollRect.content = content;
        scrollRect.viewport = viewport;
    }

    private void Update()
    {
        if (gameRules == null || !gameRules.IsInitialized()) return;
        if (headerText == null) EnsureBuilt();
        if (headerText == null) return;
        Refresh();
    }

    private void Refresh()
    {
        IReadOnlyList<RuntimeCard> stackCards = gameRules.GetStackCards();
        int total = gameRules.GetStackCount();
        headerText.text = $"堆叠  STACK · {total}";

        int visible = Mathf.Min(stackCards.Count, MaxVisibleItems);
        EnsureItemCapacity(visible);
        for (int i = 0; i < items.Count; i++)
        {
            bool active = i < visible;
            items[i].Root.SetActive(active);
            if (!active) continue;

            RuntimeCard card = stackCards[stackCards.Count - 1 - i]; // 最新在最上
            items[i].Card = card;
            bool resolving = gameRules.HasPendingStack() && i == 0;
            ApplyItem(items[i], card, resolving);
        }

        int overflow = Mathf.Max(0, stackCards.Count - MaxVisibleItems);
        if (overflow > 0)
        {
            if (tailItem == null) tailItem = CreateTailItem();
            tailItem.SetActive(true);
            tailText.text = $"+{overflow} 项 · 滚动查看";
            var rect = tailItem.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -(visible * (ItemHeight + ItemGap)));
            rect.sizeDelta = new Vector2(0f, 32f);
            content.sizeDelta = new Vector2(0f, visible * (ItemHeight + ItemGap) + 32f);
        }
        else if (tailItem != null)
        {
            tailItem.SetActive(false);
            content.sizeDelta = new Vector2(0f, visible * (ItemHeight + ItemGap));
        }
    }

    private void ApplyItem(StackItemView view, RuntimeCard card, bool resolving)
    {
        view.Resolving = resolving;
        view.Frame.color = resolving
            ? IntegratedArenaPalette.Hex("#4B91FFFF")
            : IntegratedArenaPalette.Hex("#39414FFF");
        view.Title.text = card?.Data?.cardName ?? "触发式异能";
        ManaCost cost = card?.Data?.GetManaCost() ?? default;
        string kind = card == null ? "异能" : card.Data.GetTypeLine().Split('\n')[0];
        view.Sub.text = $"{kind}{(cost.IsEmpty ? string.Empty : $" · {card.Data.manaCost}")}";

        view.Root.GetComponent<Image>().color = resolving
            ? new Color(0.12f, 0.15f, 0.21f, 1f)
            : new Color(0.10f, 0.125f, 0.17f, 1f);
    }

    private void EnsureItemCapacity(int required)
    {
        while (items.Count < required)
        {
            items.Add(CreateItem(items.Count));
        }
    }

    private StackItemView CreateItem(int index)
    {
        RectTransform root = IntegratedUiUtility.CreateRect(content, $"StackItem_{index + 1}");
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, -(index * (ItemHeight + ItemGap)));
        root.sizeDelta = new Vector2(0f, ItemHeight);

        Image frame = root.gameObject.AddComponent<Image>();
        frame.color = IntegratedArenaPalette.StackItem;
        frame.raycastTarget = true;
        ObsidianArenaTheme.EnsureOutline(frame, IntegratedArenaPalette.Hex("#39414FFF"), new Vector2(1f, -1f));

        // 迷你卡背图标
        Image mini = IntegratedUiUtility.Panel(root, "Mini", new Color(0.11f, 0.13f, 0.17f, 1f));
        mini.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        mini.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        mini.rectTransform.pivot = new Vector2(0f, 0.5f);
        mini.rectTransform.anchoredPosition = new Vector2(5f, 0f);
        mini.rectTransform.sizeDelta = new Vector2(22f, 30f);
        ObsidianArenaTheme.EnsureOutline(mini, IntegratedArenaPalette.Hex("#4A5464FF"), new Vector2(1f, -1f));
        if (CardImageLibrary.TryLoadCardBack(out Sprite back))
        {
            mini.sprite = back;
            mini.color = new Color(0.72f, 0.72f, 0.78f, 1f);
        }

        Text title = IntegratedUiUtility.Text(root, "Title", string.Empty, 11, IntegratedArenaPalette.Text, TextAnchor.UpperLeft);
        title.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(32f, -6f);
        title.rectTransform.sizeDelta = new Vector2(-44f, 20f);

        Text sub = IntegratedUiUtility.Text(root, "Sub", string.Empty, 8, IntegratedArenaPalette.MutedText, TextAnchor.UpperLeft);
        sub.rectTransform.anchorMin = new Vector2(0f, 0f);
        sub.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        sub.rectTransform.pivot = new Vector2(0f, 0f);
        sub.rectTransform.anchoredPosition = new Vector2(32f, 5f);
        sub.rectTransform.sizeDelta = new Vector2(-44f, 16f);

        var view = new StackItemView { Root = root.gameObject, Title = title, Sub = sub, Frame = frame };
        StackItemHoverHandler handler = root.gameObject.AddComponent<StackItemHoverHandler>();
        handler.Controller = this;
        handler.View = view;
        return view;
    }

    private GameObject CreateTailItem()
    {
        RectTransform root = IntegratedUiUtility.CreateRect(content, "StackOverflowTail");
        Image frame = root.gameObject.AddComponent<Image>();
        frame.color = new Color(0.08f, 0.09f, 0.13f, 0.6f);
        frame.raycastTarget = false;
        ObsidianArenaTheme.EnsureOutline(frame, IntegratedArenaPalette.Hex("#39414FFF"), new Vector2(1f, -1f));
        tailText = IntegratedUiUtility.Text(root, "Text", string.Empty, 9, IntegratedArenaPalette.FaintText, TextAnchor.MiddleCenter);
        IntegratedUiUtility.Stretch(tailText.rectTransform, 4f, 0f, 4f, 0f);
        return root.gameObject;
    }

    private void NotifyInspect(StackItemView view)
    {
        if (view?.Card != null && inspectPanel != null) inspectPanel.Show(view.Card);
    }

    private sealed class StackItemHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public StackColumnController Controller;
        public StackColumnController.StackItemView View;

        public void OnPointerEnter(PointerEventData eventData) => Controller?.NotifyInspect(View);
        public void OnPointerClick(PointerEventData eventData) => Controller?.NotifyInspect(View);
    }
}
