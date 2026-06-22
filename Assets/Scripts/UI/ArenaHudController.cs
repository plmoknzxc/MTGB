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
    private readonly GameObject[] manaItems = new GameObject[6];
    private readonly Text[] manaCounts = new Text[6];
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
        BuildIfNeeded();
        Refresh(true);
    }

    private void ResolveReferences()
    {
        if (gameRules == null) gameRules = Object.FindFirstObjectByType<GameRules>();
        if (hintText == null) hintText = GameObject.Find("ArenaInteractionHintText")?.GetComponent<Text>();
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
        BuildBottomControls(font);
        BuildManaPool(font);
        BuildOpponentSkipControl(font);
    }

    private void BuildPhaseControl(Font font)
    {
        GameObject outer = Circle(hudRoot, "PhaseRing", new Vector2(-34f, 350f), 174f, new Color(0.04f, 0.72f, 0.96f, 0.96f));
        RectTransform outerRect = outer.GetComponent<RectTransform>();
        outerRect.anchorMin = outerRect.anchorMax = new Vector2(1f, 0f);
        outerRect.pivot = new Vector2(1f, 0f);
        phaseRing = outer.GetComponent<Image>();
        phaseRing.raycastTarget = false;

        GameObject buttonObject = Circle(outerRect, "PhaseButton", Vector2.zero, 150f, new Color(0.025f, 0.08f, 0.14f, 0.98f));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
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

    private void BuildBottomControls(Font font)
    {
        GameObject controlOuter = Circle(hudRoot, "ControlRing", new Vector2(-154f, 24f), 92f, new Color(0.04f, 0.72f, 0.96f, 0.92f));
        RectTransform controlOuterRect = controlOuter.GetComponent<RectTransform>();
        controlOuterRect.anchorMin = controlOuterRect.anchorMax = new Vector2(1f, 0f);
        controlOuterRect.pivot = new Vector2(1f, 0f);

        GameObject controlButtonObject = Circle(controlOuterRect, "FullControlButton", Vector2.zero, 78f, new Color(0.025f, 0.08f, 0.14f, 0.98f));
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

        GameObject endOuter = Circle(hudRoot, "EndPhaseRing", new Vector2(-42f, 18f), 106f, new Color(0.98f, 0.49f, 0.12f, 0.96f));
        RectTransform endOuterRect = endOuter.GetComponent<RectTransform>();
        endOuterRect.anchorMin = endOuterRect.anchorMax = new Vector2(1f, 0f);
        endOuterRect.pivot = new Vector2(1f, 0f);

        GameObject endButtonObject = Circle(endOuterRect, "EndPhaseButton", Vector2.zero, 90f, new Color(0.30f, 0.08f, 0.025f, 0.98f));
        RectTransform endRect = endButtonObject.GetComponent<RectTransform>();
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
        background.color = new Color(0.015f, 0.035f, 0.06f, 0.78f);
        background.raycastTarget = false;

        Text title = CreateText(manaPanel, "ManaTitle", "\u6cd5\u672f\u529b", 15, font, TextAnchor.MiddleRight, new Color(0.62f, 0.82f, 0.92f, 1f));
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
        }
    }

    private void Refresh(bool force)
    {
        if (!built || gameRules == null) return;

        bool playerTurn = gameRules.GetCurrentPlayer() == 1;
        TurnPhase phase = gameRules.GetCurrentPhase();
        bool resolving = gameRules.HasPendingStack();
        int turnNumber = gameRules.GetTurnNumber();

        phaseText.text = resolving
            ? $"\u7b2c {turnNumber} \u56de\u5408\n\u7b49\u5f85\u7ed3\u7b97"
            : $"\u7b2c {turnNumber} \u56de\u5408\n{GetPhaseLabel(phase)}";
        Color ringColor = playerTurn
            ? new Color(0.04f, 0.72f, 0.96f, 0.98f)
            : new Color(0.96f, 0.16f, 0.12f, 0.98f);
        phaseRing.color = ringColor;
        phaseFill.color = playerTurn
            ? new Color(0.02f, 0.25f, 0.38f, 0.98f)
            : new Color(0.34f, 0.035f, 0.025f, 0.98f);
        endPhaseButton.interactable = playerTurn;
        endPhaseText.text = resolving ? "\u7ed3\u7b97\n\u5806\u53e0" : "\u7ed3\u675f\n\u5f53\u524d\u9636\u6bb5";
        endPhaseFill.color = resolving
            ? new Color(0.04f, 0.38f, 0.22f, 0.98f)
            : new Color(0.30f, 0.08f, 0.025f, 0.98f);
        skipOpponentButton.interactable = !playerTurn && !resolving;
        skipOpponentFill.color = !playerTurn && !resolving
            ? new Color(0.82f, 0.10f, 0.075f, 0.96f)
            : new Color(0.05f, 0.065f, 0.08f, 0.72f);
        RefreshControlState();

        ManaPool pool = gameRules.GetManaPool(1);
        int activeManaColors = 0;
        for (int i = 0; i < ManaColors.Length; i++)
        {
            int amount = pool.GetAmount(ManaColors[i]);
            manaItems[i].SetActive(amount > 0);
            if (amount <= 0) continue;

            RectTransform itemRect = manaItems[i].GetComponent<RectTransform>();
            itemRect.anchoredPosition = new Vector2(76f + activeManaColors * 56f, 0f);
            manaCounts[i].text = amount.ToString();
            activeManaColors++;
        }

        manaEmptyText.gameObject.SetActive(activeManaColors == 0);
        manaPanel.sizeDelta = new Vector2(activeManaColors == 0 ? 112f : 82f + activeManaColors * 56f, 58f);
    }

    private void RefreshControlState()
    {
        controlText.text = fullControl ? "\u5168\u63a7" : "\u81ea\u52a8";
        controlFill.color = fullControl
            ? new Color(0.02f, 0.45f, 0.64f, 1f)
            : new Color(0.025f, 0.08f, 0.14f, 0.98f);
    }

    private void ToggleFullControl()
    {
        fullControl = !fullControl;
        RefreshControlState();
        SetHint(fullControl ? "\u5df2\u5f00\u542f\u5168\u63a7\u6a21\u5f0f\u3002" : "\u5df2\u8fd4\u56de\u81ea\u52a8\u6a21\u5f0f\u3002");
    }

    private void OnEndPhaseClicked()
    {
        if (gameRules == null) return;
        if (gameRules.HasPendingStack())
        {
            gameRules.ResolveTopOfStack();
            Refresh(true);
            return;
        }

        gameRules.NextPhase();
        Refresh(true);
    }

    private void OnSkipOpponentTurnClicked()
    {
        if (gameRules == null || gameRules.GetCurrentPlayer() != 2 || gameRules.HasPendingStack()) return;
        gameRules.EndTurn();
        Refresh(true);
    }

    private void SetHint(string value)
    {
        if (hintText != null) hintText.text = value;
    }

    private static string GetPhaseLabel(TurnPhase phase)
    {
        switch (phase)
        {
            case TurnPhase.Untap: return "\u91cd\u7f6e\u9636\u6bb5";
            case TurnPhase.Draw: return "\u62bd\u724c\u9636\u6bb5";
            case TurnPhase.Main1: return "\u7b2c\u4e00\u4e3b\u8981\u9636\u6bb5";
            case TurnPhase.Combat: return "\u6218\u6597\u9636\u6bb5";
            case TurnPhase.Main2: return "\u7b2c\u4e8c\u4e3b\u8981\u9636\u6bb5";
            case TurnPhase.End: return "\u7ed3\u675f\u9636\u6bb5";
            default: return phase.ToString();
        }
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
