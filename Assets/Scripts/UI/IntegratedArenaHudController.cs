using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 总装（spec 第 2/9 节）：身份牌（双方镜像 330×76）、左上设置/认输圆钮、
/// 优先权徽标、右下角操作簇（法力行 → 回合徽章 + 阶段文本 + 决策计时 + 跳过战斗 + 结束回合）。
/// 数据每帧轮询 GameRules 只读 getter；操作只发 GameAction（规则引擎零改动，spec 第 11 节）。
/// </summary>
public class IntegratedArenaHudController : MonoBehaviour
{
    [SerializeField] private GameRules gameRules;
    [SerializeField] private DecisionTimerWidget timer;

    private Text playerLife;
    private Text opponentLife;
    private Text phaseText;
    private Text roundText;
    private RectTransform manaRow;
    private Text manaLabel;
    private Text turnNumberText;
    private Button skipButton;
    private Button endTurnButton;
    private Text endTurnLabel;
    private Image priorityPill;
    private Text priorityText;
    private Image priorityDot;
    private string manaSignature = string.Empty;
    private float pulseTime;

    private void Awake() => EnsureBuilt();

    /// <summary>场景重载后重建（面板/操作簇等引用不随序列化保留）。</summary>
    public void EnsureBuilt()
    {
        if ((playerLife != null && opponentLife != null) || gameRules == null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        Initialize(gameRules, canvas.transform);
    }

    public void Initialize(GameRules rules, Transform canvasTransform)
    {
        gameRules = rules;
        foreach (string rootName in new[] { "OpponentPlate", "PlayerPlate", "GearBox", "PriorityPill", "CornerCluster" })
        {
            Transform stale = canvasTransform.Find(rootName);
            if (stale != null) Destroy(stale.gameObject);
        }
        BuildPlate(canvasTransform, opponent: true);
        BuildPlate(canvasTransform, opponent: false);
        BuildGearButtons(canvasTransform);
        BuildPriorityPill(canvasTransform);
        BuildCornerCluster(canvasTransform);
    }

    // ---------- 身份牌 ----------

    private void BuildPlate(Transform canvas, bool opponent)
    {
        RectTransform root = IntegratedUiUtility.CreateRect(canvas, opponent ? "OpponentPlate" : "PlayerPlate");
        root.sizeDelta = new Vector2(330f, 76f);
        if (opponent)
        {
            root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-52f, -16f);
        }
        else
        {
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(36f, 20f);
        }

        Color faction = opponent ? IntegratedArenaPalette.Red : IntegratedArenaPalette.Blue;
        Color lifeColor = opponent ? IntegratedArenaPalette.RedSoft : IntegratedArenaPalette.BluePlateLife;
        Color hexBack = opponent ? IntegratedArenaPalette.Hex("#4A1F2AFF") : IntegratedArenaPalette.Hex("#1A3E75FF");
        Color hexMark = opponent ? IntegratedArenaPalette.RedBright : IntegratedArenaPalette.BlueSoft;

        Image back = root.gameObject.AddComponent<Image>();
        back.sprite = IntegratedUiUtility.RoundedRect(330f, 76f, 2f, "plate330");
        back.color = IntegratedArenaPalette.Panel;
        back.raycastTarget = false;
        ObsidianArenaTheme.EnsureOutline(back, IntegratedArenaPalette.LineStrong, new Vector2(1f, -1f));

        // 左缘 4px 阵营色条
        Image bar = IntegratedUiUtility.Panel(root, "FactionBar", faction);
        bar.rectTransform.anchorMin = new Vector2(0f, 0f);
        bar.rectTransform.anchorMax = new Vector2(0f, 1f);
        bar.rectTransform.pivot = new Vector2(0f, 0.5f);
        bar.rectTransform.anchoredPosition = Vector2.zero;
        bar.rectTransform.sizeDelta = new Vector2(4f, 0f);

        // 六边形头像框
        Image hex = IntegratedUiUtility.Panel(root, "Avatar", hexBack);
        hex.sprite = HexagonSprite();
        hex.rectTransform.anchorMin = hex.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        hex.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        hex.rectTransform.anchoredPosition = new Vector2(10f + 23f, 0f);
        hex.rectTransform.sizeDelta = new Vector2(46f, 46f);
        Image mark = IntegratedUiUtility.Panel(hex.transform, "Mark", hexMark);
        mark.sprite = DiamondSpriteShared();
        mark.rectTransform.sizeDelta = new Vector2(11f, 15f);

        Text name = IntegratedUiUtility.Text(root, "Name", opponent ? "Opponent" : "Sapphire Schnee", 15,
            IntegratedArenaPalette.Text, TextAnchor.LowerLeft);
        name.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        name.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        name.rectTransform.pivot = new Vector2(0f, 0.5f);
        name.rectTransform.anchoredPosition = new Vector2(68f, 3f);
        name.rectTransform.sizeDelta = new Vector2(-170f, 22f);

        Text sub = IntegratedUiUtility.Text(root, "Sub", opponent ? "对手 · OPPONENT" : "你 · YOU", 9,
            IntegratedArenaPalette.MutedText, TextAnchor.UpperLeft);
        sub.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        sub.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        sub.rectTransform.pivot = new Vector2(0f, 0.5f);
        sub.rectTransform.anchoredPosition = new Vector2(68f, -14f);
        sub.rectTransform.sizeDelta = new Vector2(-170f, 14f);

        Text life = IntegratedUiUtility.Text(root, "Life", "20", 30, lifeColor, TextAnchor.LowerRight, FontStyle.Bold);
        life.rectTransform.anchorMin = new Vector2(1f, 0.5f);
        life.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        life.rectTransform.pivot = new Vector2(1f, 0.5f);
        life.rectTransform.anchoredPosition = new Vector2(-18f, 2f);
        life.rectTransform.sizeDelta = new Vector2(80f, 34f);

        Text lifeLabel = IntegratedUiUtility.Text(root, "LifeLabel", "LIFE", 8,
            IntegratedArenaPalette.MutedText, TextAnchor.UpperRight);
        lifeLabel.rectTransform.anchorMin = new Vector2(1f, 0.5f);
        lifeLabel.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        lifeLabel.rectTransform.pivot = new Vector2(1f, 0.5f);
        lifeLabel.rectTransform.anchoredPosition = new Vector2(-18f, -14f);
        lifeLabel.rectTransform.sizeDelta = new Vector2(80f, 12f);

        if (opponent) opponentLife = life;
        else playerLife = life;
    }

    // ---------- 左上圆钮 ----------

    private void BuildGearButtons(Transform canvas)
    {
        RectTransform root = IntegratedUiUtility.CreateRect(canvas, "GearBox");
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(36f, -32f);
        root.sizeDelta = new Vector2(98f, 44f);

        Button settings = IntegratedUiUtility.CircleButton(root, "SettingsButton", 44f, IntegratedArenaPalette.Panel, IntegratedArenaPalette.LineStrong);
        settings.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0.5f);
        settings.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0.5f);
        settings.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);
        settings.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        settings.onClick.AddListener(OnSettingsClicked);
        Image settingsIcon = IntegratedUiUtility.Panel(settings.transform, "Icon", IntegratedArenaPalette.MutedText);
        settingsIcon.sprite = GearIconSprite();
        settingsIcon.rectTransform.sizeDelta = new Vector2(20f, 20f);

        Button concede = IntegratedUiUtility.CircleButton(root, "ConcedeButton", 44f, IntegratedArenaPalette.Panel, IntegratedArenaPalette.LineStrong);
        concede.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 0.5f);
        concede.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
        concede.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
        concede.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        concede.onClick.AddListener(OnConcedeClicked);
        Image heartIcon = IntegratedUiUtility.Panel(concede.transform, "Icon", IntegratedArenaPalette.MutedText);
        heartIcon.sprite = HeartIconSprite();
        heartIcon.rectTransform.sizeDelta = new Vector2(19f, 19f);
    }

    private void OnSettingsClicked()
    {
        // 设置面板未在设计稿范围内，占位
    }

    private void OnConcedeClicked()
    {
        if (gameRules == null || OnlineMatchService.Instance?.CanSubmitRuleAction == false) return;
        gameRules.ApplyAction(new GameAction
        {
            type = GameActionType.Concede,
            playerId = OnlinePlayerContext.LocalPlayerId
        });
    }

    // ---------- 优先权徽标 ----------

    private void BuildPriorityPill(Transform canvas)
    {
        RectTransform root = IntegratedUiUtility.CreateRect(canvas, "PriorityPill");
        root.anchorMin = root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(1f, 0f);
        root.anchoredPosition = new Vector2(-52f, 158f);
        root.sizeDelta = new Vector2(190f, 26f);

        priorityPill = root.gameObject.AddComponent<Image>();
        priorityPill.color = new Color(IntegratedArenaPalette.Blue.r, IntegratedArenaPalette.Blue.g,
            IntegratedArenaPalette.Blue.b, 0.10f);
        priorityPill.raycastTarget = false;
        ObsidianArenaTheme.EnsureOutline(priorityPill, new Color(IntegratedArenaPalette.Blue.r,
            IntegratedArenaPalette.Blue.g, IntegratedArenaPalette.Blue.b, 0.4f), new Vector2(1f, -1f));

        priorityDot = IntegratedUiUtility.Panel(root, "Dot", IntegratedArenaPalette.Blue);
        priorityDot.sprite = IntegratedUiUtility.Circle(16);
        priorityDot.rectTransform.anchorMin = priorityDot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        priorityDot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        priorityDot.rectTransform.anchoredPosition = new Vector2(13f, 0f);
        priorityDot.rectTransform.sizeDelta = new Vector2(6f, 6f);

        priorityText = IntegratedUiUtility.Text(root, "Text", "你拥有优先权", 10,
            IntegratedArenaPalette.BlueText, TextAnchor.MiddleLeft);
        priorityText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        priorityText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        priorityText.rectTransform.pivot = new Vector2(0f, 0.5f);
        priorityText.rectTransform.anchoredPosition = new Vector2(22f, 0f);
        priorityText.rectTransform.sizeDelta = new Vector2(-30f, 18f);
    }

    // ---------- 右下角操作簇 ----------

    private void BuildCornerCluster(Transform canvas)
    {
        RectTransform cluster = IntegratedUiUtility.CreateRect(canvas, "CornerCluster");
        cluster.anchorMin = cluster.anchorMax = new Vector2(1f, 0f);
        cluster.pivot = new Vector2(1f, 0f);
        cluster.anchoredPosition = new Vector2(-52f, 30f);
        cluster.sizeDelta = new Vector2(560f, 120f);

        // 第一行：法力池
        manaRow = IntegratedUiUtility.CreateRect(cluster, "ManaRow");
        manaRow.anchorMin = manaRow.anchorMax = new Vector2(1f, 1f);
        manaRow.pivot = new Vector2(1f, 1f);
        manaRow.anchoredPosition = new Vector2(0f, 0f);
        manaRow.sizeDelta = new Vector2(560f, 22f);
        manaLabel = IntegratedUiUtility.Text(manaRow, "Label", "可用法力", 9,
            IntegratedArenaPalette.FaintText, TextAnchor.MiddleRight);
        manaLabel.rectTransform.anchorMin = manaLabel.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        manaLabel.rectTransform.pivot = new Vector2(1f, 0.5f);
        manaLabel.rectTransform.anchoredPosition = new Vector2(-8f, 0f);
        manaLabel.rectTransform.sizeDelta = new Vector2(120f, 16f);

        // 第二行：回合徽章 + 阶段 + 计时 + 分隔线 + 按钮
        RectTransform ctlRow = IntegratedUiUtility.CreateRect(cluster, "ControlRow");
        ctlRow.anchorMin = ctlRow.anchorMax = new Vector2(1f, 0f);
        ctlRow.pivot = new Vector2(1f, 0f);
        ctlRow.anchoredPosition = Vector2.zero;
        ctlRow.sizeDelta = new Vector2(560f, 78f);

        // 回合六角徽章
        RectTransform turnChip = IntegratedUiUtility.CreateRect(ctlRow, "TurnChip");
        turnChip.anchorMin = turnChip.anchorMax = new Vector2(1f, 0f);
        turnChip.pivot = new Vector2(1f, 0.5f);
        turnChip.anchoredPosition = new Vector2(-430f, 39f);
        turnChip.sizeDelta = new Vector2(56f, 50f);
        Image hexBack = turnChip.gameObject.AddComponent<Image>();
        hexBack.sprite = TurnBadgeSprite();
        hexBack.color = Color.white;
        hexBack.raycastTarget = false;
        turnNumberText = IntegratedUiUtility.Text(turnChip, "TurnNumber", "1", 20, IntegratedArenaPalette.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
        turnNumberText.rectTransform.anchoredPosition = new Vector2(0f, 4f);
        turnNumberText.rectTransform.sizeDelta = new Vector2(56f, 24f);
        Text turnLabel = IntegratedUiUtility.Text(turnChip, "TurnLabel", "TURN", 8, IntegratedArenaPalette.BlueBright, TextAnchor.MiddleCenter);
        turnLabel.rectTransform.anchoredPosition = new Vector2(0f, -13f);
        turnLabel.rectTransform.sizeDelta = new Vector2(56f, 12f);

        Text phase = IntegratedUiUtility.Text(ctlRow, "PhaseText", "第一主要阶段", Mathf.RoundToInt(12.5f), IntegratedArenaPalette.Text, TextAnchor.MiddleLeft);
        phase.rectTransform.anchorMin = phase.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        phase.rectTransform.pivot = new Vector2(1f, 0.5f);
        phase.rectTransform.anchoredPosition = new Vector2(-221f, 8f);
        phase.rectTransform.sizeDelta = new Vector2(200f, 18f);
        phaseText = phase;

        Text round = IntegratedUiUtility.Text(ctlRow, "RoundText", "回合 1 · ROUND 1", 8, IntegratedArenaPalette.MutedText, TextAnchor.MiddleLeft);
        round.rectTransform.anchorMin = round.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        round.rectTransform.pivot = new Vector2(1f, 0.5f);
        round.rectTransform.anchoredPosition = new Vector2(-221f, -11f);
        round.rectTransform.sizeDelta = new Vector2(200f, 14f);
        roundText = round;

        // 计时环
        GameObject timerObject = new GameObject("DecisionTimerWidget", typeof(RectTransform));
        timerObject.transform.SetParent(ctlRow, false);
        var timerWidget = timerObject.AddComponent<DecisionTimerWidget>();
        timerWidget.Initialize(gameRules, timerObject.transform);
        RectTransform timerRect = timerObject.GetComponent<RectTransform>();
        timerRect.anchorMin = timerRect.anchorMax = new Vector2(1f, 0.5f);
        timerRect.pivot = new Vector2(1f, 0.5f);
        timerRect.anchoredPosition = new Vector2(-209f, 0f);
        timerRect.sizeDelta = new Vector2(96f, 44f);
        timer = timerWidget;

        // 竖分隔线
        Image divider = IntegratedUiUtility.Panel(ctlRow, "Divider", IntegratedArenaPalette.Line);
        divider.rectTransform.anchorMin = divider.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        divider.rectTransform.pivot = new Vector2(1f, 0.5f);
        divider.rectTransform.anchoredPosition = new Vector2(-106f, 0f);
        divider.rectTransform.sizeDelta = new Vector2(1f, 56f);

        // 跳过战斗
        skipButton = IntegratedUiUtility.CircleButton(ctlRow, "SkipCombatButton", 62f,
            new Color(IntegratedArenaPalette.Panel.r, IntegratedArenaPalette.Panel.g, IntegratedArenaPalette.Panel.b, 0.92f),
            IntegratedArenaPalette.LineStrong);
        RectTransform skipRect = skipButton.GetComponent<RectTransform>();
        skipRect.anchorMin = skipRect.anchorMax = new Vector2(1f, 0.5f);
        skipRect.pivot = new Vector2(1f, 0.5f);
        skipRect.anchoredPosition = new Vector2(-92f, 0f);
        skipRect.sizeDelta = new Vector2(62f, 62f);
        Text skipLabel = IntegratedUiUtility.Text(skipButton.transform, "Label", "跳过战斗", 11,
            IntegratedArenaPalette.SoftText, TextAnchor.MiddleCenter);
        IntegratedUiUtility.Stretch(skipLabel.rectTransform, 4f, 0f, 4f, 0f);
        skipButton.onClick.AddListener(OnSkipClicked);

        // 结束回合（主圆钮：蓝环 + 辉光）
        endTurnButton = IntegratedUiUtility.CircleButton(ctlRow, "EndTurnButton", 78f, IntegratedArenaPalette.Hex("#1A3B6FFF"), Color.clear);
        RectTransform endRect = endTurnButton.GetComponent<RectTransform>();
        endRect.anchorMin = endRect.anchorMax = new Vector2(1f, 0.5f);
        endRect.pivot = new Vector2(1f, 0.5f);
        endRect.anchoredPosition = Vector2.zero;
        endRect.sizeDelta = new Vector2(78f, 78f);
        Image endBack = endTurnButton.image;
        endBack.sprite = IntegratedUiUtility.Circle(96);
        ObsidianArenaTheme.EnsureOutline(endBack, IntegratedArenaPalette.Blue, new Vector2(2f, -2f));
        var glow = endBack.gameObject.AddComponent<Shadow>();
        glow.effectColor = new Color(IntegratedArenaPalette.Blue.r, IntegratedArenaPalette.Blue.g,
            IntegratedArenaPalette.Blue.b, 0.35f);
        glow.effectDistance = Vector2.zero;
        endTurnLabel = IntegratedUiUtility.Text(endTurnButton.transform, "Label", "结束回合", 13, Color.white, TextAnchor.LowerCenter);
        endTurnLabel.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        endTurnLabel.rectTransform.sizeDelta = new Vector2(78f, 22f);
        Text finish = IntegratedUiUtility.Text(endTurnButton.transform, "Finish", "F I N I S H", 7,
            IntegratedArenaPalette.BlueGlow, TextAnchor.UpperCenter);
        finish.rectTransform.anchoredPosition = new Vector2(0f, -8f);
        finish.rectTransform.sizeDelta = new Vector2(78f, 12f);
        endTurnButton.onClick.AddListener(OnEndTurnClicked);
    }

    // ---------- 每帧刷新 ----------

    private void Update()
    {
        if (gameRules == null || !gameRules.IsInitialized()) return;
        if (playerLife == null || opponentLife == null) EnsureBuilt();
        if (playerLife == null || opponentLife == null) return;
        int local = OnlinePlayerContext.LocalPlayerId;
        int opponent = OnlinePlayerContext.OpponentPlayerId;

        if (playerLife != null) playerLife.text = gameRules.GetLife(local).ToString();
        if (opponentLife != null) opponentLife.text = gameRules.GetLife(opponent).ToString();

        int turn = gameRules.GetTurnNumber();
        string phaseLabel = gameRules.GetCurrentPhaseLabel();
        if (phaseText != null)
        {
            phaseText.text = gameRules.IsGameOver() ? "对局结束" : phaseLabel;
        }
        if (roundText != null) roundText.text = $"回合 {turn} · ROUND {turn}";
        if (turnNumberText != null) turnNumberText.text = turn.ToString();

        RefreshManaPool();
        RefreshPriority();
        RefreshButtons();
    }

    private void RefreshManaPool()
    {
        if (manaRow == null) return;
        ManaPool pool = gameRules.GetManaPool(OnlinePlayerContext.LocalPlayerId);
        string signature = $"{pool.White}|{pool.Blue}|{pool.Black}|{pool.Red}|{pool.Green}|{pool.Colorless}";
        if (signature == manaSignature) return;
        manaSignature = signature;

        for (int i = manaRow.childCount - 1; i >= 0; i--)
        {
            if (manaRow.GetChild(i).name != "Label") Destroy(manaRow.GetChild(i).gameObject);
        }

        const float pipSize = 17f;
        const float gap = 6f;
        float x = 0f; // 法力圆片自右缘起，标签移到最左侧
        AddPips("W", pool.White, ref x, pipSize, gap);
        AddPips("U", pool.Blue, ref x, pipSize, gap);
        AddPips("B", pool.Black, ref x, pipSize, gap);
        AddPips("R", pool.Red, ref x, pipSize, gap);
        AddPips("G", pool.Green, ref x, pipSize, gap);
        AddPips("C", pool.Colorless, ref x, pipSize, gap);
        if (manaLabel != null) manaLabel.rectTransform.anchoredPosition = new Vector2(-(x + 10f), 0f);
    }

    private void AddPips(string code, int count, ref float x, float size, float gap)
    {
        Sprite symbol = Resources.Load<Sprite>($"UI/ManaSymbols/Mana_{code}");
        for (int i = 0; i < count; i++)
        {
            Image pip = IntegratedUiUtility.Panel(manaRow, $"Mana_{code}_{i}", Color.white);
            pip.sprite = symbol != null ? symbol : IntegratedUiUtility.Circle(34);
            pip.rectTransform.anchorMin = pip.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            pip.rectTransform.pivot = new Vector2(1f, 0.5f);
            x += size + gap;
            pip.rectTransform.anchoredPosition = new Vector2(-x, 0f);
            pip.rectTransform.sizeDelta = new Vector2(size, size);
        }
    }

    private void RefreshPriority()
    {
        if (priorityPill == null || priorityText == null || priorityDot == null) return;
        bool mine = gameRules.IsAwaitingPriority() && gameRules.GetPriorityPlayer() == OnlinePlayerContext.LocalPlayerId;
        Color back = mine
            ? new Color(IntegratedArenaPalette.Blue.r, IntegratedArenaPalette.Blue.g, IntegratedArenaPalette.Blue.b, 0.10f)
            : new Color(IntegratedArenaPalette.Red.r, IntegratedArenaPalette.Red.g, IntegratedArenaPalette.Red.b, 0.10f);
        Color border = mine
            ? new Color(IntegratedArenaPalette.Blue.r, IntegratedArenaPalette.Blue.g, IntegratedArenaPalette.Blue.b, 0.4f)
            : new Color(IntegratedArenaPalette.Red.r, IntegratedArenaPalette.Red.g, IntegratedArenaPalette.Red.b, 0.4f);
        Color text = mine ? IntegratedArenaPalette.BlueText : IntegratedArenaPalette.RedSoft;
        Color dot = mine ? IntegratedArenaPalette.Blue : IntegratedArenaPalette.Red;
        string label = gameRules.IsGameOver()
            ? "对局已结束"
            : mine ? "你拥有优先权" : "等待对手 · 对手拥有优先权";

        priorityPill.color = back;
        if (priorityPill.GetComponent<Outline>() is Outline outline) outline.effectColor = border;
        priorityText.color = text;
        priorityText.text = label;
        pulseTime += Time.unscaledDeltaTime;
        dot.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(pulseTime * 2.2f));
        priorityDot.color = dot;
    }

    private void RefreshButtons()
    {
        if (endTurnButton == null || skipButton == null) return;
        bool canAct = OnlineMatchService.Instance?.CanSubmitRuleAction != false
            && !gameRules.IsGameOver()
            && gameRules.GetCurrentPlayer() == OnlinePlayerContext.LocalPlayerId;
        bool openingMulligan = gameRules.IsOpeningMulligan();

        if (endTurnLabel != null) endTurnLabel.text = openingMulligan ? "保留手牌" : "结束回合";
        endTurnButton.interactable = canAct || openingMulligan;
        skipButton.interactable = canAct && !openingMulligan;
    }

    private void OnEndTurnClicked()
    {
        if (gameRules == null || OnlineMatchService.Instance?.CanSubmitRuleAction == false) return;
        int local = OnlinePlayerContext.LocalPlayerId;

        if (gameRules.IsOpeningMulligan())
        {
            GameChoiceRequest choice = gameRules.GetActiveChoice();
            if (choice != null && choice.choosingPlayerId == local && gameRules.CanConfirmActiveChoice())
            {
                gameRules.ApplyAction(new GameAction
                {
                    type = GameActionType.ConfirmChoice,
                    playerId = local,
                    choiceRequestId = choice.requestId
                });
            }
            return;
        }

        GameActionResult result = gameRules.ApplyAction(new GameAction { type = GameActionType.EndTurn, playerId = local });
        if (!result.accepted)
        {
            // 不在可结束时机（如堆叠待结算）时退化为推进阶段
            GameActionType primary = gameRules.GetPrimaryAdvanceActionType(local);
            gameRules.ApplyAction(new GameAction { type = primary, playerId = local });
        }
    }

    private void OnSkipClicked()
    {
        if (gameRules == null || OnlineMatchService.Instance?.CanSubmitRuleAction == false) return;
        int local = OnlinePlayerContext.LocalPlayerId;
        if (gameRules.GetCurrentPlayer() != local || gameRules.IsOpeningMulligan()) return;
        GameActionType primary = gameRules.GetPrimaryAdvanceActionType(local);
        gameRules.ApplyAction(new GameAction { type = primary, playerId = local });
    }

    // ---------- 共享 sprite ----------

    private static Sprite hexagonSprite;
    private static Sprite diamondShared;
    private static Sprite turnBadgeSprite;
    private static Sprite gearIcon;
    private static Sprite heartIcon;

    public static Sprite HexagonSprite()
    {
        if (hexagonSprite != null) return hexagonSprite;
        const int size = 64;
        Color[] pixels = new Color[size * size];
        // clip-path: polygon(25% 0,75% 0,100% 50%,75% 100%,25% 100%,0 50%)
        Vector2[] hex = new Vector2[6]
        {
            new Vector2(0.25f, 0f), new Vector2(0.75f, 0f), new Vector2(1f, 0.5f),
            new Vector2(0.75f, 1f), new Vector2(0.25f, 1f), new Vector2(0f, 0.5f)
        };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                pixels[y * size + x] = new Color(1f, 1f, 1f, PointInPolygon(p, hex) ? 1f : 0f);
            }
        }

        hexagonSprite = CreateCached("Hexagon", pixels, size);
        return hexagonSprite;
    }

    public static Sprite DiamondSpriteShared()
    {
        if (diamondShared != null) return diamondShared;
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

        diamondShared = CreateCached("DiamondShared", pixels, size);
        return diamondShared;
    }

    /// <summary>回合六角徽章：外六角深蓝底 + 内六角（模拟 CSS 双层 clip-path 渐变）。</summary>
    private static Sprite TurnBadgeSprite()
    {
        if (turnBadgeSprite != null) return turnBadgeSprite;
        const int size = 72;
        Color outer = IntegratedArenaPalette.Hex("#1B2A44FF");
        Color outerTint = new Color(IntegratedArenaPalette.Blue.r, IntegratedArenaPalette.Blue.g,
            IntegratedArenaPalette.Blue.b, 0.28f);
        Color inner = IntegratedArenaPalette.Hex("#10151FFF");
        Color[] pixels = new Color[size * size];
        Vector2[] hexOuter = HexPolygon(0.98f);
        Vector2[] hexInner = HexPolygon(0.88f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                Color pixel = Color.clear;
                if (PointInPolygon(p, hexOuter))
                {
                    pixel = Color.Lerp(outer, outerTint, 1f - p.y); // 165° 渐变近似
                    if (PointInPolygon(p, hexInner)) pixel = inner;
                }
                pixels[y * size + x] = pixel;
            }
        }

        turnBadgeSprite = CreateCached("TurnBadge", pixels, size);
        return turnBadgeSprite;
    }

    /// <summary>齿轮线条图标（圆环 + 齿 + 中孔）。</summary>
    private static Sprite GearIconSprite()
    {
        if (gearIcon != null) return gearIcon;
        const int size = 48;
        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center, dy = y - center;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                bool tooth = radius > 15.5f && radius < 21f && Mathf.Repeat(angle / (2f * Mathf.PI) * 8f + 0.5f, 1f) < 0.30f;
                bool ring = Mathf.Abs(radius - 13.5f) < 1.4f;
                bool hub = radius < 5.2f;
                bool hole = radius < 2.6f;
                pixels[y * size + x] = new Color(1f, 1f, 1f, tooth || ring || (hub && !hole) ? 1f : 0f);
            }
        }

        gearIcon = CreateCached("GearIcon", pixels, size);
        return gearIcon;
    }

    /// <summary>心形线条图标（认输）。</summary>
    private static Sprite HeartIconSprite()
    {
        if (heartIcon != null) return heartIcon;
        const int size = 48;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - (size - 1) * 0.5f) / (size * 0.5f);
                float ny = -(y - (size - 1) * 0.5f) / (size * 0.5f) + 0.08f;
                float a = nx * nx + ny * ny - 0.42f;
                float value = a * a * a - nx * nx * ny * ny * ny;
                pixels[y * size + x] = new Color(1f, 1f, 1f, value < 0f ? 1f : 0f);
            }
        }

        heartIcon = CreateCached("HeartIcon", pixels, size);
        return heartIcon;
    }

    private static Vector2[] HexPolygon(float scale)
    {
        return new Vector2[6]
        {
            new Vector2(0.5f - 0.25f * scale, 0.5f - 0.5f * scale),
            new Vector2(0.5f + 0.25f * scale, 0.5f - 0.5f * scale),
            new Vector2(0.5f + 0.5f * scale, 0.5f),
            new Vector2(0.5f + 0.25f * scale, 0.5f + 0.5f * scale),
            new Vector2(0.5f - 0.25f * scale, 0.5f + 0.5f * scale),
            new Vector2(0.5f - 0.5f * scale, 0.5f)
        };
    }

    private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if (((polygon[i].y > point.y) != (polygon[j].y > point.y))
                && point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static Sprite CreateCached(string name, Color[] pixels, int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
