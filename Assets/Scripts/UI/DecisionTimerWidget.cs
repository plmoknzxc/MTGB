using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 决策计时环（spec 第 9 节右下角簇）：40px 圆环（蓝弧 + 剩余时间）+ 副标「决策剩余时间 DECISION TIMER」。
/// 时长来源与超时行为尚未联机对齐（spec 第 12 节遗留 5），当前为纯展示倒计时：
/// 回合/阶段/优先权归属变化时重置，仅在等待本地玩家决策时走表。
/// </summary>
public class DecisionTimerWidget : MonoBehaviour
{
    public const float DefaultDuration = 60f;

    [SerializeField] private GameRules gameRules;

    private Image ringBackground;
    private Image ringArc;
    private Text timeText;
    private float remaining;
    private Transform uiRoot;
    private int lastTurn = -1;
    private int lastPhase = -1;
    private int lastPriority = -1;

    private void Awake() => EnsureBuilt();

    /// <summary>场景重载后重建。</summary>
    public void EnsureBuilt()
    {
        if (timeText != null || gameRules == null) return;
        Initialize(gameRules, transform);
    }

    public void Initialize(GameRules rules, Transform parent)
    {
        gameRules = rules;
        Transform stale = parent.Find("DecisionTimer");
        if (stale != null) Destroy(stale.gameObject);

        RectTransform root = IntegratedUiUtility.CreateRect(parent, "DecisionTimer");
        root.sizeDelta = new Vector2(96f, 44f);

        ringBackground = IntegratedUiUtility.Panel(root, "RingBack", IntegratedArenaPalette.Hex("#232A37FF"));
        ringBackground.sprite = RingSprite(IntegratedArenaPalette.Hex("#232A37FF"));
        ringBackground.rectTransform.sizeDelta = new Vector2(40f, 40f);
        ringBackground.raycastTarget = false;

        ringArc = IntegratedUiUtility.Panel(root, "RingArc", IntegratedArenaPalette.Blue);
        ringArc.sprite = RingSprite(IntegratedArenaPalette.Blue);
        ringArc.type = Image.Type.Filled;
        ringArc.fillMethod = Image.FillMethod.Radial360;
        ringArc.fillOrigin = 2; // Top
        ringArc.fillClockwise = true;
        ringArc.rectTransform.sizeDelta = new Vector2(40f, 40f);
        ringArc.raycastTarget = false;

        timeText = IntegratedUiUtility.Text(root, "Time", "1:00", 10, IntegratedArenaPalette.Text, TextAnchor.MiddleCenter);
        timeText.rectTransform.sizeDelta = new Vector2(40f, 40f);
        timeText.rectTransform.anchoredPosition = Vector2.zero;

        Text title = IntegratedUiUtility.Text(root, "Title", "决策剩余时间", 11, IntegratedArenaPalette.Text, TextAnchor.MiddleLeft);
        title.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        title.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        title.rectTransform.pivot = new Vector2(0f, 0.5f);
        title.rectTransform.anchoredPosition = new Vector2(46f, 4f);
        title.rectTransform.sizeDelta = new Vector2(90f, 14f);

        Text sub = IntegratedUiUtility.Text(root, "Sub", "DECISION TIMER", 8, IntegratedArenaPalette.MutedText, TextAnchor.MiddleLeft);
        sub.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        sub.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        sub.rectTransform.pivot = new Vector2(0f, 0.5f);
        sub.rectTransform.anchoredPosition = new Vector2(46f, -10f);
        sub.rectTransform.sizeDelta = new Vector2(90f, 12f);
    }

    private static Sprite ringSprite;

    private static Sprite RingSprite(Color _)
    {
        if (ringSprite != null) return ringSprite;
        ringSprite = IntegratedUiUtility.RoundedRectOutline(40f, 40f, 19f, 3.5f, "timerRing");
        return ringSprite;
    }

    private void Update()
    {
        if (gameRules == null || !gameRules.IsInitialized()) return;
        if (timeText == null) { EnsureBuilt(); if (timeText == null) return; }
        int turn = gameRules.GetTurnNumber();
        int phase = gameRules != null ? (int)gameRules.GetCurrentPhase() : -1;
        int priority = gameRules != null && gameRules.IsAwaitingPriority() ? gameRules.GetPriorityPlayer() : 0;

        if (turn != lastTurn || phase != lastPhase || priority != lastPriority)
        {
            lastTurn = turn;
            lastPhase = phase;
            lastPriority = priority;
            remaining = DefaultDuration;
        }

        bool counting = gameRules != null
            && !gameRules.IsGameOver()
            && gameRules.IsAwaitingPriority()
            && priority == OnlinePlayerContext.LocalPlayerId;
        if (counting) remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);

        int totalSeconds = Mathf.CeilToInt(remaining);
        timeText.text = $"{totalSeconds / 60}:{totalSeconds % 60:00}";
        ringArc.fillAmount = Mathf.Clamp01(remaining / DefaultDuration);
        ringArc.color = remaining <= 10f ? IntegratedArenaPalette.Red : IntegratedArenaPalette.Blue;
    }
}
