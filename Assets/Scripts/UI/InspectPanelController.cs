using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 左栏检视面板（spec 第 9 节，262 宽）：顶部蓝条 + 「检视 INSPECT」+ 来源编号 →
/// 卡图（338 高，真实卡图优先）→ 名称 + 费用圆片 → 类型行 → 规则文字 → 底部提示。
/// 悬停任意卡即更新；未悬停时保持上次内容（MTGA 同款常驻左栏）。
/// </summary>
public class InspectPanelController : MonoBehaviour
{
    private static readonly Vector2 PanelSize = new Vector2(262f, 530f);

    private Text setNumberText;
    private Image artImage;
    private Text nameText;
    private RectTransform costRow;
    private Text typeText;
    private Text rulesText;
    private RuntimeCard currentCard;

    private void Awake() => EnsureBuilt();

    /// <summary>场景保存/重载后序列化引用会丢失，按需重建面板（场景中残留的旧面板先清除）。</summary>
    public void EnsureBuilt()
    {
        if (nameText != null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        Initialize(canvas.transform);
    }

    public void Initialize(Transform canvasTransform)
    {
        Transform stale = canvasTransform.Find("InspectPanel");
        if (stale != null) Destroy(stale.gameObject);
        RectTransform root = IntegratedUiUtility.CreateRect(canvasTransform, "InspectPanel");
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 0f);
        root.anchoredPosition = new Vector2(36f, -180f);
        root.sizeDelta = PanelSize;

        Image back = root.gameObject.AddComponent<Image>();
        back.sprite = IntegratedUiUtility.RoundedRect(PanelSize.x, PanelSize.y, 2f, "inspectBack");
        back.color = IntegratedArenaPalette.PanelStrong;
        back.raycastTarget = false;
        ObsidianArenaTheme.EnsureOutline(back, IntegratedArenaPalette.LineStrong, new Vector2(1f, -1f));

        // 顶部 3px 蓝条
        Image topBar = IntegratedUiUtility.Panel(root, "TopBar", IntegratedArenaPalette.Blue);
        topBar.rectTransform.anchorMin = new Vector2(0f, 1f);
        topBar.rectTransform.anchorMax = new Vector2(1f, 1f);
        topBar.rectTransform.pivot = new Vector2(0.5f, 1f);
        topBar.rectTransform.anchoredPosition = new Vector2(0f, 0f);
        topBar.rectTransform.sizeDelta = new Vector2(0f, 3f);

        Text header = IntegratedUiUtility.Text(root, "Header", "检视  INSPECT", 10,
            IntegratedArenaPalette.MutedText, TextAnchor.MiddleLeft);
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = new Vector2(1f, 1f);
        header.rectTransform.pivot = new Vector2(0f, 1f);
        header.rectTransform.anchoredPosition = new Vector2(13f, -9f);
        header.rectTransform.sizeDelta = new Vector2(-26f, 22f);

        setNumberText = IntegratedUiUtility.Text(root, "SetNumber", string.Empty, 10,
            IntegratedArenaPalette.MutedText, TextAnchor.MiddleRight);
        setNumberText.rectTransform.anchorMin = new Vector2(0f, 1f);
        setNumberText.rectTransform.anchorMax = new Vector2(1f, 1f);
        setNumberText.rectTransform.pivot = new Vector2(1f, 1f);
        setNumberText.rectTransform.anchoredPosition = new Vector2(-13f, -9f);
        setNumberText.rectTransform.sizeDelta = new Vector2(120f, 22f);

        // 卡图 338 高
        artImage = IntegratedUiUtility.Panel(root, "Art", new Color(0.05f, 0.07f, 0.10f, 1f));
        artImage.rectTransform.anchorMin = new Vector2(0f, 1f);
        artImage.rectTransform.anchorMax = new Vector2(1f, 1f);
        artImage.rectTransform.pivot = new Vector2(0.5f, 1f);
        artImage.rectTransform.anchoredPosition = new Vector2(0f, -36f);
        artImage.rectTransform.sizeDelta = new Vector2(-2f, 338f);
        artImage.preserveAspect = false;

        nameText = IntegratedUiUtility.Text(root, "Name", string.Empty, 16, IntegratedArenaPalette.Text, TextAnchor.MiddleLeft);
        nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
        nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
        nameText.rectTransform.pivot = new Vector2(0f, 1f);
        nameText.rectTransform.anchoredPosition = new Vector2(13f, -382f);
        nameText.rectTransform.sizeDelta = new Vector2(-26f, 24f);

        costRow = IntegratedUiUtility.CreateRect(root, "CostRow");
        costRow.anchorMin = new Vector2(0f, 1f);
        costRow.anchorMax = new Vector2(0f, 1f);
        costRow.pivot = new Vector2(0f, 1f);
        costRow.anchoredPosition = new Vector2(13f, -410f);
        costRow.sizeDelta = new Vector2(200f, 21f);

        typeText = IntegratedUiUtility.Text(root, "TypeLine", string.Empty, 10, IntegratedArenaPalette.MutedText, TextAnchor.MiddleLeft);
        typeText.rectTransform.anchorMin = new Vector2(0f, 1f);
        typeText.rectTransform.anchorMax = new Vector2(1f, 1f);
        typeText.rectTransform.pivot = new Vector2(0f, 1f);
        typeText.rectTransform.anchoredPosition = new Vector2(13f, -438f);
        typeText.rectTransform.sizeDelta = new Vector2(-26f, 16f);

        rulesText = IntegratedUiUtility.Text(root, "Rules", string.Empty, 12, IntegratedArenaPalette.SoftText, TextAnchor.UpperLeft);
        rulesText.rectTransform.anchorMin = new Vector2(0f, 1f);
        rulesText.rectTransform.anchorMax = new Vector2(1f, 1f);
        rulesText.rectTransform.pivot = new Vector2(0f, 1f);
        rulesText.rectTransform.anchoredPosition = new Vector2(13f, -462f);
        rulesText.rectTransform.sizeDelta = new Vector2(-26f, 60f);

        Text footer = IntegratedUiUtility.Text(root, "Footer", "HOVER · 悬停任意牌查看", 8, IntegratedArenaPalette.GhostText, TextAnchor.LowerLeft);
        footer.rectTransform.anchorMin = new Vector2(0f, 0f);
        footer.rectTransform.anchorMax = new Vector2(1f, 0f);
        footer.rectTransform.pivot = new Vector2(0f, 0f);
        footer.rectTransform.anchoredPosition = new Vector2(13f, 6f);
        footer.rectTransform.sizeDelta = new Vector2(-26f, 14f);
    }

    public void Show(RuntimeCard card)
    {
        if (nameText == null) EnsureBuilt();
        if (nameText == null || card == null || ReferenceEquals(card, currentCard)) return;
        currentCard = card;
        CardDefinition data = card.Data;

        setNumberText.text = string.IsNullOrEmpty(data.setCode) && data.collectorNumber <= 0
            ? string.Empty
            : $"{data.setCode}·{data.collectorNumberText}";
        nameText.text = data.cardName ?? string.Empty;
        typeText.text = data.GetTypeLine();
        rulesText.text = ExtractRulesText(data);

        if (CardImageLibrary.TryLoadFullCard(data, out Sprite sprite))
        {
            artImage.sprite = sprite;
            artImage.color = Color.white;
        }
        else
        {
            var texture = TemplateCardTexture.Build(data, 220, 306);
            artImage.sprite = IntegratedUiUtility.CreateSprite(texture, texture.name);
            artImage.color = Color.white;
        }

        RebuildCostPips(data.GetManaCost());
    }

    private static string ExtractRulesText(CardDefinition data)
    {
        string description = data.description ?? string.Empty;
        int newline = description.IndexOf('\n');
        return newline >= 0 ? description[(newline + 1)..].Trim() : description.Trim();
    }

    private void RebuildCostPips(ManaCost cost)
    {
        for (int i = costRow.childCount - 1; i >= 0; i--)
        {
            Destroy(costRow.GetChild(i).gameObject);
        }

        if (cost.IsEmpty) return;

        const float pipSize = 21f;
        const float gap = 5f;
        float x = 0f;
        void AddPip(string code, string label)
        {
            Sprite symbol = Resources.Load<Sprite>($"UI/ManaSymbols/Mana_{code}");
            Image pip = IntegratedUiUtility.Panel(costRow, $"Pip_{code}_{x}", Color.white);
            pip.sprite = symbol != null ? symbol : IntegratedUiUtility.Circle(42);
            pip.rectTransform.anchorMin = new Vector2(0f, 0f);
            pip.rectTransform.anchorMax = new Vector2(0f, 0f);
            pip.rectTransform.pivot = new Vector2(0f, 0.5f);
            pip.rectTransform.anchoredPosition = new Vector2(x, pipSize * 0.5f);
            pip.rectTransform.sizeDelta = new Vector2(pipSize, pipSize);
            x += pipSize + gap;
            if (symbol != null || string.IsNullOrEmpty(label)) return;
            Text text = IntegratedUiUtility.Text(pip.transform, "V", label, 12, Color.white, TextAnchor.MiddleCenter);
            IntegratedUiUtility.Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        }

        if (cost.Generic > 0) AddPip("C", cost.Generic.ToString());
        AddRepeated("W", cost.White);
        AddRepeated("U", cost.Blue);
        AddRepeated("B", cost.Black);
        AddRepeated("R", cost.Red);
        AddRepeated("G", cost.Green);
        AddRepeated("C", cost.Colorless);

        void AddRepeated(string code, int count)
        {
            for (int i = 0; i < count; i++) AddPip(code, null);
        }
    }
}
