using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Reuses the existing scroll viewport; only the detail content is replaced.
public sealed class DeckCardDetailsView
{
    private static readonly Color Paper = MenuVisualTheme.Paper;
    private static readonly Color Muted = MenuVisualTheme.Muted;
    private readonly UnityEngine.UI.Text title;
    private readonly UnityEngine.UI.Text printing;
    private readonly UnityEngine.UI.Text type;
    private readonly TMPro.TextMeshProUGUI rules;
    private readonly UnityEngine.UI.Text rulesHeading;
    private readonly UnityEngine.UI.Text flavor;
    private readonly UnityEngine.UI.Text flavorHeading;
    private readonly UnityEngine.UI.Text stats;
    private readonly TMPro.TextMeshProUGUI costs;
    private readonly UnityEngine.UI.LayoutElement costWidth;
    private readonly UnityEngine.UI.ScrollRect scroll;
    private readonly Font font;
    private readonly Transform content;
    private readonly GameObject legacyText;
    private readonly GameObject settingsContent;
    private readonly UnityEngine.UI.Image infoTabBackground, artTabBackground, settingsTabBackground;
    private readonly GameObject artContent;
    private readonly System.Action<CardDefinition, Transform> openArt;
    private readonly System.Func<CardDefinition,RectTransform,DeckKeywordGlossary.Entry,bool,bool> relatedHover;
    // Commander formats are temporarily omitted from the deck format picker. Keep
    // DeckFormatLegality's supported formats intact so existing decks still load.
    private static readonly string[] FormatOptions = { "standard", "pioneer", "modern-group", "legacy", "vintage", "pauper", "historic", "timeless" };
    private readonly List<UnityEngine.UI.Button> formatButtons = new List<UnityEngine.UI.Button>();
    private readonly List<string> formatButtonKeys = new List<string>();
    private readonly UnityEngine.UI.Button modernButton;
    private readonly UnityEngine.UI.Button commanderButton;
    private GameObject formatMenuOverlay;
    private readonly List<UnityEngine.UI.Button> languageButtons = new List<UnityEngine.UI.Button>();
    private readonly List<UnityEngine.UI.Button> sizeButtons = new List<UnityEngine.UI.Button>();
    private readonly UnityEngine.UI.Dropdown banlistDropdown;
    private readonly List<string> banlistValues = new List<string>();
    private string displayedFormat;
    private int displayedLanguageRevision = -1;
    private int selectedTab;
    private readonly List<GameObject> secondarySections = new List<GameObject>();
    private CardDefinition shownCard;
    private int shownFaceIndex = -1;
    private int languageRevision = -1;

    public DeckCardDetailsView(UnityEngine.UI.Text oldText, System.Action<CardDefinition, Transform> openArt,
        System.Action formatChanged = null,
        UnityEngine.UI.Dropdown dropdownTemplate = null,
        System.Func<CardDefinition,RectTransform,DeckKeywordGlossary.Entry,bool,bool> onRelatedHover = null)
    {
        this.openArt = openArt;
        relatedHover=onRelatedHover;
        font = oldText.font;
        legacyText = oldText.gameObject;
        content = oldText.transform.parent;
        scroll = content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        oldText.gameObject.SetActive(false);
        var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 9;
        layout.padding = new RectOffset(10, 10, 10, 20);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        var actions=new GameObject("DetailActions",typeof(RectTransform),typeof(UnityEngine.UI.LayoutElement));
        actions.transform.SetParent(content,false);actions.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=48;
        var infoButton = new GameObject("CardInfoTab", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        infoButton.transform.SetParent(actions.transform, false);
        var ir = (RectTransform)infoButton.transform; ir.anchorMin = Vector2.zero; ir.anchorMax = new Vector2(.32f, 1); ir.offsetMin = ir.offsetMax = Vector2.zero;
        infoTabBackground = infoButton.GetComponent<UnityEngine.UI.Image>();
        infoTabBackground.sprite = IntegratedUiUtility.RoundedRect(100, 48, 6);
        infoButton.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => SelectTab(0));
        var infoLabel = Label(infoButton.transform, "Label", 16, Paper);
        infoLabel.text = "卡牌信息"; infoLabel.alignment = TextAnchor.MiddleCenter;
        infoLabel.rectTransform.anchorMin = Vector2.zero; infoLabel.rectTransform.anchorMax = Vector2.one;
        infoLabel.rectTransform.offsetMin = infoLabel.rectTransform.offsetMax = Vector2.zero;
        var artButton = new GameObject("CardArtButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        artButton.transform.SetParent(actions.transform, false);
        var ar=(RectTransform)artButton.transform;ar.anchorMin=new Vector2(.34f,0);ar.anchorMax=new Vector2(.67f,1);ar.offsetMin=ar.offsetMax=Vector2.zero;
        artButton.GetComponent<UnityEngine.UI.Image>().sprite = IntegratedUiUtility.RoundedRect(200,48,6);
        artTabBackground = artButton.GetComponent<UnityEngine.UI.Image>();
        artTabBackground.color = new Color(.19f,.28f,.39f);
        artButton.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => SelectTab(1));
        var artLabel = Label(artButton.transform, "Label", 16, Paper);
        artLabel.text = "异画·下载";
        artLabel.alignment = TextAnchor.MiddleCenter;
        artLabel.rectTransform.anchorMin = Vector2.zero; artLabel.rectTransform.anchorMax = Vector2.one;
        artLabel.rectTransform.offsetMin = artLabel.rectTransform.offsetMax = Vector2.zero;
        var settings=new GameObject("SettingsButton",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));
        settings.transform.SetParent(actions.transform,false);
        var sr=(RectTransform)settings.transform;sr.anchorMin=new Vector2(.69f,0);sr.anchorMax=Vector2.one;sr.offsetMin=sr.offsetMax=Vector2.zero;
        settings.GetComponent<UnityEngine.UI.Image>().sprite=IntegratedUiUtility.RoundedRect(100,48,6);
        settingsTabBackground = settings.GetComponent<UnityEngine.UI.Image>();
        settingsTabBackground.color=new Color(.17f,.22f,.29f);
        settings.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(()=>SelectTab(2));
        var sl=Label(settings.transform,"Label",18,Paper);sl.text="设置";sl.alignment=TextAnchor.MiddleCenter;sl.rectTransform.anchorMin=Vector2.zero;sl.rectTransform.anchorMax=Vector2.one;sl.rectTransform.offsetMin=sl.rectTransform.offsetMax=Vector2.zero;
        foreach (var label in new[] { infoLabel, artLabel, sl })
        {
            label.fontSize = 16;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.rectTransform.offsetMin = new Vector2(8, 4);
            label.rectTransform.offsetMax = new Vector2(-8, -4);
        }
        artContent = new GameObject("DeckEditorArtContent", typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement));
        artContent.transform.SetParent(content, false);
        artContent.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 680;
        settingsContent = new GameObject("DeckEditorSettings", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup));
        settingsContent.transform.SetParent(content, false);
        var settingsLayout = settingsContent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        settingsLayout.spacing = 12;
        settingsLayout.childControlWidth = settingsLayout.childControlHeight = true;
        settingsLayout.childForceExpandWidth = true;
        settingsLayout.childForceExpandHeight = false;
        CreateSettingsRow("语言", new[] { "简体中文", "日本語", "English" },
            new[] { "zh-CN", "ja-JP", "en-US" }, value => GamePreferences.SetLanguage(value), languageButtons);
        CreateSettingsRow("文字大小", new[] { "小", "标准", "大" },
            new[] { "0.85", "1", "1.3" }, value => GamePreferences.SetScale(float.Parse(value, System.Globalization.CultureInfo.InvariantCulture)), sizeButtons);
        ReplayWidgets.AutoSaveToggle(settingsContent.transform);
        var formatTitle = Label(settingsContent.transform, "FormatHeading", 18, Paper);
        formatTitle.text = "当前赛制";
        formatTitle.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 29;
        for (int row = 0; row < FormatOptions.Length; row += 2)
        {
            var holder = CreateButtonRow(settingsContent.transform, "FormatRow" + row);
            for (int column = 0; column < 2 && row + column < FormatOptions.Length; column++)
            {
                string key = FormatOptions[row + column];
                if (key == "modern-group")
                {
                    modernButton = CreateSettingsButton(holder.transform, FormatLabel(key),
                        () => OpenFormatChoices(modernButton, new[] { "modern", "premodern" }, formatChanged));
                }
                else if (key == "commander-group")
                {
                    commanderButton = CreateSettingsButton(holder.transform, FormatLabel(key),
                        () => OpenFormatChoices(commanderButton, new[] { "commander", "duelcommander", "brawl" }, formatChanged));
                }
                else
                {
                    var button = CreateSettingsButton(holder.transform, FormatLabel(key), () => SelectFormat(key, formatChanged));
                    formatButtons.Add(button);
                    formatButtonKeys.Add(key);
                }
            }
        }
        RefreshFormatOptions();
        var banlistTitle = Label(settingsContent.transform, "BanlistHeading", 18, Paper);
        banlistTitle.text = "赛制规则 · 日期";
        banlistTitle.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 29;
        if (dropdownTemplate != null)
        {
            banlistDropdown = CreateDropdown(dropdownTemplate, "BanlistDateDropdown");
            banlistDropdown.onValueChanged.AddListener(index =>
            {
                if (index < 0 || index >= banlistValues.Count) return;
                GamePreferences.SetDeckBanlist(banlistValues[index]);
                formatChanged?.Invoke();
                RefreshSettings();
            });
            RefreshBanlistOptions();
        }
        var hint = Label(settingsContent.transform, "ApplyHint", 14, Muted);
        hint.text = "更改立即生效。N/A 只取消禁限，仍检查该赛制的卡池。";
        hint.horizontalOverflow = HorizontalWrapMode.Wrap;
        hint.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 48;
        Transform header = CreateNameCostRow(content, "CardHeader");
        title = Label(header, "CardName", 25, Paper);
        title.fontStyle = FontStyle.Bold;
        title.verticalOverflow = VerticalWrapMode.Overflow;
        title.alignment = TextAnchor.UpperLeft;
        title.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
        title.GetComponent<PreferenceText>().SetBaseSize(25);
        costs = CardSymbolText.Create(header, "ManaCost", 22, Paper);
        costs.alignment = TMPro.TextAlignmentOptions.TopRight;
        costs.enableWordWrapping = false;
        costWidth = costs.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        costs.gameObject.AddComponent<PreferenceText>().SetBaseSize(22);
        printing = Label(content, "Printing", 14, Muted);
        var divider = new GameObject("Divider", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
        divider.transform.SetParent(content, false);
        divider.GetComponent<UnityEngine.UI.Image>().color = new Color(.50f,.55f,.62f,.7f);
        divider.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        divider.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 1;
        type = Label(content, "CardType", 18, new Color(.72f,.81f,.91f));
        stats = Label(content, "Stats", 18, new Color(.82f,.71f,.50f));
        rulesHeading = Label(content, "RulesHeading", 14, Muted);
        rulesHeading.text = "规则文本";
        rules = CardSymbolText.Create(content, "Rules", 20, Paper);
        rules.lineSpacing = 5;
        flavorHeading = Label(content, "FlavorHeading", 14, Muted);
        flavorHeading.text = "风味文字";
        flavor = Label(content, "Flavor", 18, MenuVisualTheme.Muted);
        flavor.fontStyle = FontStyle.Italic;
        flavor.lineSpacing = 1.15f;
        artContent.SetActive(false);
        settingsContent.SetActive(false);
        GamePreferences.Changed += RefreshSettings;
        RefreshSettings();
        SelectTab(0);
    }

    private void SelectTab(int tab)
    {
        CloseFormatChoices();
        selectedTab = tab;
        foreach (Transform child in content)
            if (child.gameObject.name != "DetailActions" && child.gameObject != settingsContent && child.gameObject != artContent && child.gameObject != legacyText)
                child.gameObject.SetActive(tab == 0);
        artContent.SetActive(tab == 1);
        settingsContent.SetActive(tab == 2);
        infoTabBackground.color = tab == 0 ? MenuVisualTheme.Primary : MenuVisualTheme.Raised;
        artTabBackground.color = tab == 1 ? MenuVisualTheme.Primary : MenuVisualTheme.Raised;
        settingsTabBackground.color = tab == 2 ? MenuVisualTheme.Primary : MenuVisualTheme.Raised;
        if (tab == 1 && shownCard != null) openArt?.Invoke(shownCard, artContent.transform);
        RefreshSettings();
        if (tab == 0)
        {
            int face = Mathf.Max(0, shownFaceIndex);
            shownFaceIndex = -1;
            Show(shownCard, face);
        }
        if (scroll != null) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; }
    }

    private void RefreshSettings()
    {
        if (displayedLanguageRevision != GamePreferences.Revision)
        {
            displayedLanguageRevision = GamePreferences.Revision;
            RefreshFormatOptions();
            RefreshBanlistOptions();
        }
        else if (banlistDropdown != null && displayedFormat != GamePreferences.DeckFormat) RefreshBanlistOptions();
        SyncFormatSelection();
        string[] languages = { "zh-CN", "ja-JP", "en-US" };
        for (int i = 0; i < languageButtons.Count; i++)
            languageButtons[i].GetComponent<UnityEngine.UI.Image>().color = GamePreferences.Language == languages[i]
                ? MenuVisualTheme.Primary : MenuVisualTheme.Raised;
        int selectedSize = GamePreferences.TextScale < .9f ? 0 : GamePreferences.TextScale > 1.2f ? 2 : 1;
        for (int i = 0; i < sizeButtons.Count; i++)
            sizeButtons[i].GetComponent<UnityEngine.UI.Image>().color = i == selectedSize ? MenuVisualTheme.Primary : MenuVisualTheme.Raised;
    }

    private static bool IsCommanderFormat(string key) => key == "commander" || key == "duelcommander" || key == "brawl";
    private static bool IsModernFormat(string key) => key == "modern" || key == "premodern";

    private UnityEngine.UI.Dropdown CreateDropdown(UnityEngine.UI.Dropdown template, string name)
    {
        var dropdown = Object.Instantiate(template, settingsContent.transform, false);
        dropdown.name = name;
        dropdown.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 44;
        dropdown.onValueChanged.RemoveAllListeners();
        return dropdown;
    }

    private static string FormatLabel(string key)
    {
        switch (key)
        {
            case "standard": return GamePreferences.T("标准赛");
            case "pioneer": return GamePreferences.T("先驱");
            case "modern-group": return GamePreferences.T("摩登") + " ▾";
            case "modern": return GamePreferences.T("摩登");
            case "premodern": return GamePreferences.T("前摩登");
            case "legacy": return GamePreferences.T("薪传");
            case "vintage": return GamePreferences.T("特选");
            case "pauper": return GamePreferences.T("纯铁");
            case "commander-group": return GamePreferences.T("指挥官") + " ▾";
            case "commander": return GamePreferences.T("官禁");
            case "duelcommander": return GamePreferences.T("法禁");
            case "brawl": return GamePreferences.T("争锋");
            case "historic": return GamePreferences.T("史迹");
            case "timeless": return GamePreferences.T("永恒");
            default: return key;
        }
    }

    private void RefreshFormatOptions()
    {
        CloseFormatChoices();
        for (int i = 0; i < formatButtons.Count; i++)
            formatButtons[i].GetComponentInChildren<UnityEngine.UI.Text>().text = FormatLabel(formatButtonKeys[i]);
        if (modernButton != null) modernButton.GetComponentInChildren<UnityEngine.UI.Text>().text = FormatLabel("modern-group");
        if (commanderButton != null) commanderButton.GetComponentInChildren<UnityEngine.UI.Text>().text = FormatLabel("commander-group");
        SyncFormatSelection();
    }

    private void SyncFormatSelection()
    {
        string selected = GamePreferences.DeckFormat;
        for (int i = 0; i < formatButtons.Count; i++)
            formatButtons[i].GetComponent<UnityEngine.UI.Image>().color = selected == formatButtonKeys[i]
                ? MenuVisualTheme.Primary : MenuVisualTheme.Raised;
        if (modernButton != null)
            modernButton.GetComponent<UnityEngine.UI.Image>().color = IsModernFormat(selected)
                ? MenuVisualTheme.Primary : MenuVisualTheme.Raised;
        if (commanderButton != null)
            commanderButton.GetComponent<UnityEngine.UI.Image>().color = IsCommanderFormat(selected)
                ? MenuVisualTheme.Primary : MenuVisualTheme.Raised;
    }

    private void OpenFormatChoices(UnityEngine.UI.Button trigger, string[] keys, System.Action formatChanged)
    {
        if (formatMenuOverlay != null) { CloseFormatChoices(); return; }
        var canvas = trigger.GetComponentInParent<Canvas>().rootCanvas;
        formatMenuOverlay = new GameObject("FormatChoicesOverlay", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(CanvasGroup));
        formatMenuOverlay.transform.SetParent(canvas.transform, false);
        var visibility = formatMenuOverlay.GetComponent<CanvasGroup>();
        visibility.alpha = 0;
        visibility.interactable = visibility.blocksRaycasts = false;
        var overlayRect = (RectTransform)formatMenuOverlay.transform;
        overlayRect.anchorMin = Vector2.zero; overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
        formatMenuOverlay.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        formatMenuOverlay.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(CloseFormatChoices);

        var panel = new GameObject("FormatChoices", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup));
        panel.transform.SetParent(formatMenuOverlay.transform, false);
        panel.GetComponent<UnityEngine.UI.Image>().color = MenuVisualTheme.Panel;
        var layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding = new RectOffset(5, 5, 5, 5);
        layout.spacing = 3;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        foreach (string key in keys)
        {
            string choice = key;
            var button = CreateSettingsButton(panel.transform, FormatLabel(key), () =>
            {
                CloseFormatChoices();
                SelectFormat(choice, formatChanged);
            });
            button.GetComponent<UnityEngine.UI.Image>().color = GamePreferences.DeckFormat == key
                ? MenuVisualTheme.Primary : MenuVisualTheme.Raised;
        }
        var triggerRect = (RectTransform)trigger.transform;
        var corners = new Vector3[4];
        triggerRect.GetWorldCorners(corners);
        Vector3 lower = overlayRect.InverseTransformPoint(corners[0]);
        Vector3 upper = overlayRect.InverseTransformPoint(corners[2]);
        float height = keys.Length * 42 + (keys.Length - 1) * 3 + 10;
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.sizeDelta = new Vector2(upper.x - lower.x, height);
        float top = lower.y - 4;
        if (top - height < overlayRect.rect.yMin + 8) top = upper.y + height + 4;
        panelRect.anchoredPosition = new Vector2(lower.x, top);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        Canvas.ForceUpdateCanvases();
        visibility.alpha = 1;
        visibility.interactable = visibility.blocksRaycasts = true;
    }

    private void CloseFormatChoices()
    {
        if (formatMenuOverlay == null) return;
        Object.Destroy(formatMenuOverlay);
        formatMenuOverlay = null;
    }

    private void SelectFormat(string key, System.Action formatChanged)
    {
        GamePreferences.SetDeckFormat(key);
        RefreshBanlistOptions();
        formatChanged?.Invoke();
        RefreshSettings();
    }

    public void Dispose() { GamePreferences.Changed -= RefreshSettings; CloseFormatChoices(); }

    private void RefreshBanlistOptions()
    {
        if (banlistDropdown == null) return;
        displayedFormat = GamePreferences.DeckFormat;
        banlistValues.Clear();
        banlistValues.AddRange(DeckFormatLegality.Dates(displayedFormat));
        if (banlistValues.Count == 0) banlistValues.Add(DeckFormatLegality.Current);
        banlistValues.Add(DeckFormatLegality.NoBanlist);
        banlistDropdown.ClearOptions();
        var options = new List<UnityEngine.UI.Dropdown.OptionData>(banlistValues.Count);
        foreach (string value in banlistValues) options.Add(new UnityEngine.UI.Dropdown.OptionData(GamePreferences.T(DeckFormatLegality.Label(value))));
        banlistDropdown.AddOptions(options);
        int selected = banlistValues.IndexOf(GamePreferences.DeckBanlist);
        if (selected < 0)
        {
            GamePreferences.SetDeckBanlist(banlistValues[0]);
            selected = 0;
        }
        banlistDropdown.SetValueWithoutNotify(selected);
        banlistDropdown.RefreshShownValue();
    }

    private void CreateSettingsRow(string titleText, string[] labels, string[] values, System.Action<string> select,
        List<UnityEngine.UI.Button> buttons)
    {
        var heading = Label(settingsContent.transform, titleText + "Heading", 18, Paper);
        heading.text = titleText;
        heading.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 29;
        var row = CreateButtonRow(settingsContent.transform, titleText + "Row");
        for (int i = 0; i < labels.Length; i++)
        {
            string value = values[i];
            var button = CreateSettingsButton(row.transform, labels[i], () => select(value));
            buttons.Add(button);
        }
    }

    private static GameObject CreateButtonRow(Transform parent, string name)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 42;
        var layout = row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return row;
    }

    private UnityEngine.UI.Button CreateSettingsButton(Transform parent, string value, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(value, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<UnityEngine.UI.Image>().color = MenuVisualTheme.Raised;
        go.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
        var button = go.GetComponent<UnityEngine.UI.Button>();
        button.onClick.AddListener(action);
        var label = Label(go.transform, "Label", 16, Paper);
        label.text = value;
        label.alignment = TextAnchor.MiddleCenter;
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        label.GetComponent<PreferenceText>().SetBaseSize(16);
        return button;
    }

    public void Show(CardDefinition card, int faceIndex = 0)
    {
        if (ReferenceEquals(card, shownCard) && card != null && shownFaceIndex == faceIndex && languageRevision == GamePreferences.Revision) return;
        relatedHover?.Invoke(shownCard, rules.rectTransform, null, false);
        DeckKeywordLinkHover.Hide();
        languageRevision = GamePreferences.Revision;
        shownCard = card;
        shownFaceIndex = faceIndex;
        foreach (GameObject section in secondarySections)
        {
            section.SetActive(false);
            Object.Destroy(section);
        }
        secondarySections.Clear();
        CardFeatureDefinition displayedFace = null;
        if (faceIndex > 0 && card?.features != null)
            displayedFace = card.features.Find(f => f != null && f.faceIndex == faceIndex && f.featureIndex == 0);
        title.text = card == null ? "请选择卡牌" : displayedFace == null ? GamePreferences.CardName(card) : GamePreferences.FeatureName(displayedFace);
        printing.text = card == null ? string.Empty : card.PrintingKey;
        type.text = card == null ? string.Empty : displayedFace == null ? GamePreferences.CardType(card)
            : GamePreferences.FeatureType(displayedFace, FeatureTypeLine(displayedFace));
        string primaryRules = card == null ? string.Empty : displayedFace == null ? GamePreferences.CardRules(card)
            : GamePreferences.FeatureRules(displayedFace);
        SetRules(rules, primaryRules);
        bool hasRules = !string.IsNullOrWhiteSpace(primaryRules);
        rules.gameObject.SetActive(hasRules);
        rulesHeading.gameObject.SetActive(hasRules);
        flavor.text = displayedFace == null ? card?.flavorText ?? string.Empty : string.Empty;
        bool hasFlavor = !string.IsNullOrWhiteSpace(flavor.text);
        flavor.gameObject.SetActive(hasFlavor);
        flavorHeading.gameObject.SetActive(hasFlavor);
        stats.text = string.Empty;
        if (displayedFace != null) stats.text = Stats(displayedFace);
        else if (card != null)
        {
            if (card.cardTypes.Has(CardType.Creature)) stats.text = $"力量 / 防御力    {card.attack} / {card.defence}";
            else if (card.cardTypes.Has(CardType.Planeswalker)) stats.text = $"忠诚值    {card.loyalty}";
            else if (card.cardTypes.Has(CardType.Battle)) stats.text = $"布防值    {card.battleDefense}";
        }
        stats.gameObject.SetActive(stats.text.Length > 0);
        string manaCost = displayedFace == null ? card?.manaCost : displayedFace.manaCost;
        bool hasCost = !string.IsNullOrWhiteSpace(manaCost);
        costs.text = CardSymbolText.Format(manaCost);
        SetCostWidth(costs, costWidth, hasCost);
        if (card?.features != null)
        {
            var features = new List<CardFeatureDefinition>(card.features);
            features.Sort((a,b) => a == null ? 1 : b == null ? -1 : a.faceIndex != b.faceIndex
                ? a.faceIndex.CompareTo(b.faceIndex) : a.featureIndex.CompareTo(b.featureIndex));
            foreach (CardFeatureDefinition feature in features)
            {
                if (feature == null || ReferenceEquals(feature, displayedFace)) continue;
                if (displayedFace == null && feature.faceIndex == 0 && feature.featureIndex == 0) continue;
                secondarySections.Add(CreateFeatureSection(card, feature));
            }
        }
        if (scroll != null) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; }
        if (selectedTab != 0) SelectTab(selectedTab);
    }

    private GameObject CreateFeatureSection(CardDefinition card, CardFeatureDefinition feature)
    {
        var section = new GameObject("Feature_" + feature.faceIndex + "_" + feature.featureIndex,
            typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup));
        section.transform.SetParent(content, false);
        var image = section.GetComponent<UnityEngine.UI.Image>();
        image.sprite = IntegratedUiUtility.RoundedRect(300, 160, 8);
        image.color = new Color(.12f,.17f,.23f,.96f);
        image.raycastTarget = false;
        var layout = section.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding = new RectOffset(13, 13, 12, 14);
        layout.spacing = 6;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var eyebrow = Label(section.transform, "FeatureKind", 14, new Color(.78f,.66f,.47f));
        eyebrow.text = FeatureKind(card, feature);
        Transform featureHeader = CreateNameCostRow(section.transform, "FeatureHeader");
        var name = Label(featureHeader, "FeatureName", 21, Paper);
        name.fontStyle = FontStyle.Bold;
        name.verticalOverflow = VerticalWrapMode.Overflow;
        name.alignment = TextAnchor.UpperLeft;
        name.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
        name.GetComponent<PreferenceText>().SetBaseSize(21);
        name.text = GamePreferences.FeatureName(feature);
        var cost = CardSymbolText.Create(featureHeader, "FeatureCost", 19, Paper);
        cost.alignment = TMPro.TextAlignmentOptions.TopRight;
        cost.enableWordWrapping = false;
        var featureCostWidth = cost.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        cost.gameObject.AddComponent<PreferenceText>().SetBaseSize(19);
        cost.text = CardSymbolText.Format(feature.manaCost);
        SetCostWidth(cost, featureCostWidth, !string.IsNullOrWhiteSpace(feature.manaCost));
        var typeLine = Label(section.transform, "FeatureType", 16, new Color(.72f,.81f,.91f));
        typeLine.text = GamePreferences.FeatureType(feature, FeatureTypeLine(feature));
        string featureRules = GamePreferences.FeatureRules(feature);
        if (!string.IsNullOrWhiteSpace(featureRules))
        {
            var text = CardSymbolText.Create(section.transform, "FeatureRules", 18, Paper);
            text.gameObject.AddComponent<PreferenceText>().SetBaseSize(18);
            SetRules(text, featureRules);
            text.lineSpacing = 4;
        }
        string statsText = Stats(feature);
        if (!string.IsNullOrEmpty(statsText)) Label(section.transform, "FeatureStats", 17, new Color(.82f,.71f,.50f)).text = statsText;
        return section;
    }

    private static string FeatureTypeLine(CardFeatureDefinition feature)
    {
        string line = (feature.supertypes.ToDisplayString() + " " + feature.cardTypes.ToDisplayString()).Trim();
        return string.IsNullOrWhiteSpace(feature.subtypes) ? line : line + " - " + feature.subtypes.Trim();
    }

    private static string Stats(CardFeatureDefinition feature)
    {
        if (feature.cardTypes.Has(CardType.Creature)) return feature.attack + " / " + feature.defence;
        if (feature.cardTypes.Has(CardType.Planeswalker)) return "忠诚值 " + feature.loyalty;
        if (feature.cardTypes.Has(CardType.Battle)) return "布防值 " + feature.battleDefense;
        return string.Empty;
    }

    private static string FeatureKind(CardDefinition card, CardFeatureDefinition feature)
    {
        string en;
        if (feature.faceIndex == 0 && feature.featureIndex == 0) en = "Front face";
        else switch (feature.role)
        {
            case CardFeatureRole.Adventure: en = "Adventure"; break;
            case CardFeatureRole.Prepare: en = "Prepare"; break;
            case CardFeatureRole.Omen: en = "Omen"; break;
            case CardFeatureRole.RoomDoor: en = "Room"; break;
            case CardFeatureRole.SplitPart: en = "Split"; break;
            default:
                if (feature.faceIndex > 0) en = "Other face";
                else if (card.forgeSplitType == "Adventure") en = "Adventure";
                else if (card.forgeSplitType == "Prepare") en = "Prepare";
                else if (card.forgeSplitType == "Omen") en = "Omen";
                else if (card.forgeSplitType == "Split") en = "Split";
                else en = "Additional spell";
                break;
        }
        bool zh = GamePreferences.Language.StartsWith("zh");
        bool ja = GamePreferences.Language.StartsWith("ja");
        switch (en)
        {
            case "Adventure": return zh ? "历险" : ja ? "出来事" : en;
            case "Prepare": return zh ? "备法" : ja ? "準備" : en;
            case "Omen": return zh ? "预兆" : ja ? "予兆" : en;
            case "Room": return zh ? "房间" : ja ? "部屋" : en;
            case "Split": return zh ? "连体牌" : ja ? "分割カード" : en;
            case "Other face": return zh ? "另一面" : ja ? "もう一方の面" : en;
            case "Front face": return zh ? "正面" : ja ? "第１面" : en;
            default: return zh ? "另一部分" : ja ? "別の部分" : en;
        }
    }

    private void SetRules(TMPro.TextMeshProUGUI text, string source)
    {
        List<DeckKeywordGlossary.Entry> keywords = DeckKeywordGlossary.Find(source,card:shownCard);
        keywords.AddRange(DeckTokenLinks.Find(shownCard,source));
        text.richText = true;
        text.raycastTarget = keywords.Count > 0;
        text.text = CardSymbolText.Format(DeckKeywordGlossary.Markup(source, keywords, shownCard));
        var hover = text.GetComponent<DeckKeywordLinkHover>();
        if (hover == null) hover = text.gameObject.AddComponent<DeckKeywordLinkHover>();
        hover.Configure(keywords, font, shownCard,
            relatedHover);
    }

    private UnityEngine.UI.Text Label(Transform parent, string name, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<UnityEngine.UI.Text>();
        text.font = GamePreferences.Font != null ? GamePreferences.Font : font;
        text.fontSize = size;
        text.color = color;
        text.supportRichText = false;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.gameObject.AddComponent<PreferenceText>().SetBaseSize(size);
        return text;
    }

    private static Transform CreateNameCostRow(Transform parent, string name)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        var layout = row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        return row.transform;
    }

    private static void SetCostWidth(TMPro.TextMeshProUGUI text, UnityEngine.UI.LayoutElement width, bool hasCost)
    {
        text.gameObject.SetActive(hasCost);
        if (!hasCost) return;
        float preferred = Mathf.Ceil(text.GetPreferredValues(text.text).x) + 4;
        width.minWidth = width.preferredWidth = preferred;
    }
}
