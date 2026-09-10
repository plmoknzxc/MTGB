using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 区域堆（spec 第 8 节）：牌库/坟场/放逐，双方各一组，横排 3 只 54×74。
/// 牌库叠真卡背，空堆用虚线斜纹框；右下角数量签，下方 8.5px 标签。
/// 位置（镜像）：对手组右上 right:36 top:96；己方组左下 left:36 bottom:124。
/// 点击 → 浏览详情视图（⚠ 交互稿未定稿，当前置灰提示，见 spec 第 12 节遗留 1）。
/// </summary>
public class ZonePilesController : MonoBehaviour
{
    private const float PileW = 54f, PileH = 74f, PileGap = 12f;

    [SerializeField] private GameRules gameRules;

    private readonly PileView[] piles = new PileView[3];
    private int owner;
    private static Texture2D emptyPileTexture;

    private sealed class PileView
    {
        public Image Stack;
        public Text Count;
        public int Kind; // 0 牌库 1 坟场 2 放逐
        public int Owner;
    }

    public enum Corner
    {
        OpponentTopRight,
        PlayerBottomLeft
    }

    private Corner corner;

    private void Awake() => EnsureBuilt();

    /// <summary>场景重载后重建。</summary>
    public void EnsureBuilt()
    {
        if ((piles[0] != null && piles[0].Stack != null) || gameRules == null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        Initialize(gameRules, canvas.transform, corner, owner);
    }

    public void Initialize(GameRules rules, Transform canvasTransform, Corner corner, int owner)
    {
        gameRules = rules;
        this.corner = corner;
        this.owner = owner;
        Transform stale = canvasTransform.Find($"ZonePiles_{corner}");
        if (stale != null) Destroy(stale.gameObject);
        RectTransform root = IntegratedUiUtility.CreateRect(canvasTransform, $"ZonePiles_{corner}");
        if (corner == Corner.OpponentTopRight)
        {
            root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 0f);
            root.anchoredPosition = new Vector2(-36f, -96f);
        }
        else
        {
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(36f, 124f);
        }
        root.sizeDelta = new Vector2(PileW * 3 + PileGap * 2, PileH + 14f);

        string[] labels = { "牌库", "坟场", "放逐" };
        for (int i = 0; i < 3; i++)
        {
            piles[i] = CreatePile(root, i, labels[i], owner);
            ApplyStackVisual(piles[i], 0);
        }
    }

    private PileView CreatePile(RectTransform root, int index, string label, int owner)
    {
        RectTransform pile = IntegratedUiUtility.CreateRect(root, $"Pile_{label}");
        pile.anchorMin = pile.anchorMax = new Vector2(0f, 1f);
        pile.pivot = new Vector2(0f, 1f);
        pile.anchoredPosition = new Vector2(index * (PileW + PileGap), 0f);
        pile.sizeDelta = new Vector2(PileW, PileH);

        Image stack = pile.gameObject.AddComponent<Image>();
        stack.color = Color.white;
        stack.raycastTarget = true;

        Image countBack = IntegratedUiUtility.Panel(pile, "CountBadge", IntegratedArenaPalette.Hex("#161B24FF"));
        countBack.sprite = IntegratedUiUtility.RoundedRect(28f, 20f, 2f, "pileCount");
        countBack.rectTransform.anchorMin = countBack.rectTransform.anchorMax = new Vector2(1f, 0f);
        countBack.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        countBack.rectTransform.anchoredPosition = new Vector2(6f, 4f);
        countBack.rectTransform.sizeDelta = new Vector2(28f, 20f);
        ObsidianArenaTheme.EnsureOutline(countBack, IntegratedArenaPalette.Hex("#39414FFF"), new Vector2(1f, -1f));
        Text count = IntegratedUiUtility.Text(countBack.transform, "Count", "0", Mathf.RoundToInt(10.5f), IntegratedArenaPalette.Text, TextAnchor.MiddleCenter);
        IntegratedUiUtility.Stretch(count.rectTransform, 0f, 0f, 0f, 0f);

        Text labelText = IntegratedUiUtility.Text(pile, "Label", label, 9, IntegratedArenaPalette.FaintText, TextAnchor.UpperCenter);
        labelText.rectTransform.anchorMin = labelText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        labelText.rectTransform.pivot = new Vector2(0.5f, 1f);
        labelText.rectTransform.anchoredPosition = new Vector2(0f, -4f);
        labelText.rectTransform.sizeDelta = new Vector2(PileW, 12f);

        ZonePileClickHandler handler = pile.gameObject.AddComponent<ZonePileClickHandler>();
        handler.View = new PileView { Stack = stack, Count = count, Kind = index, Owner = owner };
        return handler.View;
    }

    private void Update()
    {
        if (gameRules == null || !gameRules.IsInitialized()) return;
        if (piles[0] == null || piles[0].Stack == null) EnsureBuilt();
        if (piles[0] == null || piles[0].Stack == null) return;
        for (int i = 0; i < piles.Length; i++)
        {
            PileView pile = piles[i];
            int count = pile.Kind switch
            {
                0 => gameRules.GetDeckCount(pile.Owner),
                1 => gameRules.GetGraveyardCount(pile.Owner),
                _ => gameRules.GetExileCount(pile.Owner)
            };
            if (pile.Count.text == count.ToString()) continue;
            pile.Count.text = count.ToString();
            ApplyStackVisual(pile, count);
        }
    }

    private void ApplyStackVisual(PileView pile, int count)
    {
        if (count > 0 && pile.Kind == 0 && CardImageLibrary.TryLoadCardBack(out Sprite back))
        {
            pile.Stack.sprite = back;
            pile.Stack.color = new Color(0.68f, 0.68f, 0.68f, 1f); // brightness(.68)
            pile.Stack.type = Image.Type.Simple;
        }
        else
        {
            pile.Stack.sprite = GetEmptyPileSprite();
            pile.Stack.color = Color.white;
        }
    }

    private static Sprite GetEmptyPileSprite()
    {
        if (emptyPileTexture != null) return IntegratedUiUtility.CreateSprite(emptyPileTexture, "EmptyPile");
        const int width = 54;
        const int height = 74;
        emptyPileTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "EmptyPile",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color stripeA = IntegratedArenaPalette.PileStripeA;
        Color stripeB = IntegratedArenaPalette.PileStripeB;
        Color border = IntegratedArenaPalette.CardBorder;
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool borderPx = x < 1 || x >= width - 1 || y < 1 || y >= height - 1;
                if (borderPx)
                {
                    // 虚线边框（沿边每 6px 亮 3px）
                    bool onEdgeDash = borderPx && ((x + y) % 6 < 3);
                    pixels[y * width + x] = onEdgeDash ? border : stripeB;
                }
                else
                {
                    pixels[y * width + x] = (x + y) % 12 < 6 ? stripeA : stripeB;
                }
            }
        }

        emptyPileTexture.SetPixels(pixels);
        emptyPileTexture.Apply(false, true);
        return IntegratedUiUtility.CreateSprite(emptyPileTexture, "EmptyPile");
    }

    private sealed class ZonePileClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public PileView View;

        public void OnPointerClick(PointerEventData eventData)
        {
            // 浏览详情视图交互稿未定稿（spec 第 12 节遗留 1），暂不响应
        }
    }
}
