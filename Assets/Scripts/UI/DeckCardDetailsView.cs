using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Reuses the existing scroll viewport; only the detail content is replaced.
public sealed class DeckCardDetailsView
{
    private static Color Paper => MenuVisualTheme.ArchivePaper;
    private static Color Muted => MenuVisualTheme.Stone;
    private readonly TMPro.TextMeshProUGUI title;
    private readonly UnityEngine.UI.Text printing;
    private readonly UnityEngine.UI.Text type;
    private readonly TMPro.TextMeshProUGUI rules;
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
    private bool shownHasBack;
    private int languageRevision = -1;

    public DeckCardDetailsView(UnityEngine.UI.Text oldText, System.Action<CardDefinition, Transform> openArt,
        System.Action formatChanged, UnityEngine.UI.Dropdown dropdownTemplate,
        System.Func<CardDefinition,RectTransform,DeckKeywordGlossary.Entry,bool,bool> onRelatedHover,
        Text headerTarget, RectTransform gallery)
    {
        this.openArt = openArt;
        relatedHover=onRelatedHover;
        font = oldText.font;
        legacyText = oldText.gameObject;
        content = oldText.transform.parent;
        scroll = content.GetComponentInParent<UnityEngine.UI.ScrollRect>(true);
        oldText.gameObject.SetActive(false);
        var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 9;
        layout.padding = new RectOffset(6, 5, 10, 20);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        headerTarget.enabled=false;
        var actions=new GameObject("DetailActions",typeof(RectTransform),typeof(HorizontalLayoutGroup));
        var row=(RectTransform)actions.transform;
        row.SetParent(headerTarget.transform,false);
        row.anchorMin=Vector2.zero;row.anchorMax=Vector2.one;row.offsetMin=row.offsetMax=Vector2.zero;
        var strip=actions.GetComponent<HorizontalLayoutGroup>();
        strip.spacing=MainMenuController.InspectorToolGap;
        strip.childControlWidth=strip.childControlHeight=true;
        strip.childForceExpandWidth=false;strip.childForceExpandHeight=true;

        Image Tab(string name,string caption,int index,bool iconOnly=false)
        {
            var tab=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement));
            tab.transform.SetParent(row,false);
            var size=tab.GetComponent<LayoutElement>();
            size.minWidth=size.preferredWidth=iconOnly?MainMenuController.InspectorToolSize:0;
            size.flexibleWidth=iconOnly?0:1;
            var label=Label(tab.transform,"Label",16,Paper);
            label.text=caption;label.alignment=TextAnchor.MiddleCenter;
            label.resizeTextForBestFit=true;label.resizeTextMinSize=12;label.resizeTextMaxSize=label.fontSize;
            label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
            label.rectTransform.offsetMin=new Vector2(4,2);label.rectTransform.offsetMax=new Vector2(-4,-2);
            label.enabled=!iconOnly;
            tab.GetComponent<Button>().onClick.AddListener(()=>SelectTab(index));
            return tab.GetComponent<Image>();
        }
        infoTabBackground=Tab("CardInfoTab","卡牌详情",0);
        artTabBackground=Tab("CardArtButton","异画·下载",1);
        settingsTabBackground=Tab("SettingsButton","设置",2,true);
        var gear=new GameObject("Gear",typeof(RectTransform),typeof(ForgeGearIcon)).GetComponent<ForgeGearIcon>();
        gear.transform.SetParent(settingsTabBackground.transform,false);
        gear.rectTransform.sizeDelta=new Vector2(20,20);
        gear.color=MenuVisualTheme.ArchivePaper;gear.raycastTarget=false;
        artContent=gallery.gameObject;
        settingsContent = new GameObject("DeckEditorSettings", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup));
        settingsContent.transform.SetParent(content, false);
        var settingsLayout = settingsContent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        settingsLayout.spacing = 12;
        settingsLayout.childControlWidth = settingsLayout.childControlHeight = true;
        settingsLayout.childForceExpandWidth = true;
        settingsLayout.childForceExpandHeight = false;
        if (GamePreferences.LanguageSelectionEnabled)
            CreateSettingsRow("语言", new[] { "简体中文", "日本語", "English" },
                new[] { "zh-CN", "ja-JP", "en-US" }, value => GamePreferences.SetLanguage(value), languageButtons);
        CreateSettingsRow("文字大小", new[] { "小", "标准", "大" },
            new[] { "0.85", "1", "1.3" }, value => GamePreferences.SetScale(float.Parse(value, System.Globalization.CultureInfo.InvariantCulture)), sizeButtons);
        var themePicker = MenuThemePicker.Create(settingsContent.transform);
        themePicker.gameObject.AddComponent<LayoutElement>().preferredHeight = MenuThemePicker.Height;
        PlayerAvatarPicker.Create(settingsContent.transform).gameObject.AddComponent<LayoutElement>().preferredHeight=PlayerAvatarPicker.Height;
        ReplayWidgets.AutoSaveToggle(settingsContent.transform);
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
        hint.text = "N/A 只取消禁限，仍检查该赛制的卡池。";
        hint.horizontalOverflow = HorizontalWrapMode.Wrap;
        hint.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 48;
        Transform header = CreateNameCostRow(content, "CardHeader");
        title = DetailTitle(header, "CardName", 25);
        title.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
        costs = CardSymbolText.Create(header, "ManaCost", 22, Paper);
        costs.alignment = TMPro.TextAlignmentOptions.TopRight;
        costs.enableWordWrapping = false;
        costWidth = costs.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        Transform typeRow=CreateNameCostRow(content,"TypeAndStats");
        type = Label(typeRow, "CardType", 15, MenuVisualTheme.Stone);
        type.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
        stats = Label(typeRow, "Stats", 17, MenuVisualTheme.ArchivePaper);
        stats.alignment=TextAnchor.UpperRight;
        stats.horizontalOverflow=HorizontalWrapMode.Overflow;
        stats.gameObject.AddComponent<LayoutElement>();
        printing = Label(content, "Printing", 13, Muted);
        var divider = new GameObject("Divider", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.LayoutElement));
        divider.transform.SetParent(content, false);
        divider.GetComponent<UnityEngine.UI.Image>().color = MenuVisualTheme.Etching;
        divider.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        divider.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 1;
        rules = CardSymbolText.Create(content, "Rules", 18, Paper);
        rules.lineSpacing = 5;
        rules.paragraphSpacing = 24;
        flavorHeading = Label(content, "FlavorHeading", 14, Muted);
        flavorHeading.text = "风味文字";
        flavor = Label(content, "Flavor", 18, MenuVisualTheme.Stone);
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
        selectedTab = tab;
        foreach (Transform child in content)
            if (child.gameObject.name != "DetailActions" && child.gameObject != settingsContent && child.gameObject != artContent && child.gameObject != legacyText)
                child.gameObject.SetActive(tab == 0);
        artContent.SetActive(tab == 1);
        settingsContent.SetActive(tab == 2);
        void StyleTab(Image background, bool selected)
        {
            MenuVisualTheme.StyleDeckTool(background.GetComponent<Button>(),selected);
            var marker=MenuVisualTheme.Detail(background.transform,"ActiveRule",MenuVisualTheme.MainAccent);
            marker.rectTransform.anchorMin=Vector2.zero;marker.rectTransform.anchorMax=Vector2.right;
            marker.rectTransform.offsetMin=new Vector2(5,1);marker.rectTransform.offsetMax=new Vector2(-5,3);
            marker.gameObject.SetActive(selected);
        }
        StyleTab(infoTabBackground, tab == 0);
        StyleTab(artTabBackground, tab == 1);
        StyleTab(settingsTabBackground, tab == 2);
        if (tab == 1 && shownCard != null) openArt?.Invoke(shownCard, artContent.transform);
        RefreshSettings();
        if (tab == 0)
        {
            int face = Mathf.Max(0, shownFaceIndex);
            shownFaceIndex = -1;
            Show(shownCard, face, shownHasBack);
        }
        if (scroll != null) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; }
    }

    private void RefreshSettings()
    {
        if (displayedLanguageRevision != GamePreferences.Revision)
        {
            displayedLanguageRevision = GamePreferences.Revision;
            RefreshBanlistOptions();
        }
        else if (banlistDropdown != null && displayedFormat != GamePreferences.DeckFormat) RefreshBanlistOptions();
        string[] languages = { "zh-CN", "ja-JP", "en-US" };
        for (int i = 0; i < languageButtons.Count; i++)
            MenuVisualTheme.StyleDeckTool(languageButtons[i], GamePreferences.Language == languages[i]);
        int selectedSize = GamePreferences.TextScale < .9f ? 0 : GamePreferences.TextScale > 1.2f ? 2 : 1;
        for (int i = 0; i < sizeButtons.Count; i++)
            MenuVisualTheme.StyleDeckTool(sizeButtons[i], i == selectedSize);
    }

    private UnityEngine.UI.Dropdown CreateDropdown(UnityEngine.UI.Dropdown template, string name)
    {
        var dropdown = Object.Instantiate(template, settingsContent.transform, false);
        dropdown.name = name;
        var size=dropdown.GetComponent<LayoutElement>()??dropdown.gameObject.AddComponent<LayoutElement>();
        size.minWidth=size.preferredWidth=-1;
        size.preferredHeight=44;
        MenuVisualTheme.StyleArchiveDropdown(dropdown);
        PreferenceText.SetSize(dropdown.captionText,16);
        dropdown.onValueChanged.RemoveAllListeners();
        return dropdown;
    }

    public void RefreshLegalitySettings()
    {
        RefreshBanlistOptions();
        RefreshSettings();
    }

    public void Dispose() { GamePreferences.Changed -= RefreshSettings; }

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
        go.GetComponent<UnityEngine.UI.Image>().color = MenuVisualTheme.Slate;
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

    public void Show(CardDefinition card, int faceIndex = 0, bool hasBack = false)
    {
        if (ReferenceEquals(card, shownCard) && card != null && shownFaceIndex == faceIndex
            && shownHasBack == hasBack && languageRevision == GamePreferences.Revision) return;
        bool switchingFace = ReferenceEquals(card, shownCard) && faceIndex != shownFaceIndex;
        relatedHover?.Invoke(shownCard, rules.rectTransform, null, false);
        DeckKeywordLinkHover.Hide();
        languageRevision = GamePreferences.Revision;
        shownCard = card;
        shownFaceIndex = faceIndex;
        shownHasBack = hasBack;
        foreach (GameObject section in secondarySections)
        {
            section.SetActive(false);
            Object.Destroy(section);
        }
        secondarySections.Clear();
        CardFeatureDefinition displayedFace = null;
        if (faceIndex > 0 && card?.features != null)
            displayedFace = card.features.Find(f => f != null && f.faceIndex == faceIndex && f.featureIndex == 0);
        bool unknownFace = faceIndex > 0 && displayedFace == null;
        title.text = card == null ? "请选择卡牌" : unknownFace ? DeckCompanion.Text("背面","Back face","第２面") : displayedFace == null ? GamePreferences.CardName(card) : GamePreferences.FeatureName(displayedFace);
        printing.text = card == null ? string.Empty : card.PrintingKey;
        type.text = card == null || unknownFace ? string.Empty : displayedFace == null ? GamePreferences.CardType(card)
            : GamePreferences.FeatureType(displayedFace, FeatureTypeLine(displayedFace));
        string primaryRules = unknownFace ? DeckCompanion.Text("此面暂无规则资料","No rules data for this face","この面のルール情報は未収録") : card == null ? string.Empty : displayedFace == null ? GamePreferences.CardRules(card)
            : GamePreferences.FeatureRules(displayedFace);
        primaryRules = WithAvailabilityNotice(card, primaryRules);
        SetRules(rules, primaryRules);
        bool hasRules = !string.IsNullOrWhiteSpace(primaryRules);
        rules.gameObject.SetActive(hasRules);
        flavor.text = faceIndex == 0 ? GamePreferences.CardFlavor(card) : string.Empty;
        bool hasFlavor = !string.IsNullOrWhiteSpace(flavor.text);
        flavor.gameObject.SetActive(hasFlavor);
        flavorHeading.gameObject.SetActive(hasFlavor);
        stats.text = string.Empty;
        if (displayedFace != null) stats.text = Stats(displayedFace);
        else if (card != null && !unknownFace)
        {
            if (card.cardTypes.Has(CardType.Creature)) stats.text = $"{card.attack} / {card.defence}";
            else if (card.cardTypes.Has(CardType.Planeswalker)) stats.text = $"忠诚值    {card.loyalty}";
            else if (card.cardTypes.Has(CardType.Battle)) stats.text = $"布防值    {card.battleDefense}";
        }
        stats.gameObject.SetActive(stats.text.Length > 0);
        stats.GetComponent<LayoutElement>().minWidth=Mathf.Ceil(stats.preferredWidth)+2;
        string manaCost = unknownFace ? string.Empty : displayedFace == null ? card?.manaCost : displayedFace.manaCost;
        bool hasCost = !string.IsNullOrWhiteSpace(manaCost);
        costs.text = CardSymbolText.Format(manaCost);
        SetCostWidth(costs, costWidth, hasCost);
        if (card != null)
        {
            // The heading already shows the primary feature of the face in view.
            // Older definitions can store the front only on the card itself.
            var features = card.features?.Where(f => f != null).ToList() ?? new List<CardFeatureDefinition>();
            if (faceIndex != 0 && !features.Any(f => f.faceIndex == 0 && f.featureIndex == 0))
                features.Add(card.GetPrimaryFeature());
            foreach (var feature in features.OrderBy(f => f.faceIndex == faceIndex ? -1 : f.faceIndex).ThenBy(f => f.featureIndex))
            {
                if (feature.faceIndex == faceIndex && feature.featureIndex == 0) continue;
                secondarySections.Add(CreateFeatureSection(card, feature));
            }
            if (hasBack && faceIndex != 1 && !features.Any(f => f.faceIndex == 1))
            {
                var missing = Label(content, "MissingBackRules", 17, Muted);
                missing.text = DeckCompanion.Text("背面暂无规则资料","No rules data for the back face","第２面のルール情報は未収録");
                secondarySections.Add(missing.gameObject);
            }
        }
        if (scroll != null)
        {
            if (switchingFace && selectedTab == 0 && scroll.isActiveAndEnabled)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
        }
        if (selectedTab != 0) SelectTab(selectedTab);
    }

    private GameObject CreateFeatureSection(CardDefinition card, CardFeatureDefinition feature)
    {
        var section = new GameObject("Feature_" + feature.faceIndex + "_" + feature.featureIndex,
            typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup));
        section.transform.SetParent(content, false);
        var image = section.GetComponent<UnityEngine.UI.Image>();
        image.sprite = IntegratedUiUtility.RoundedRect(300, 160, 8);
        image.color = MenuVisualTheme.ArchivePanel;
        image.raycastTarget = false;
        var layout = section.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding = new RectOffset(13, 13, 12, 14);
        layout.spacing = 6;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var eyebrow = Label(section.transform, "FeatureKind", 14, MenuVisualTheme.MistBlue);
        eyebrow.text = FeatureKind(card, feature);
        Transform featureHeader = CreateNameCostRow(section.transform, "FeatureHeader");
        var name = DetailTitle(featureHeader, "FeatureName", 21);
        name.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
        name.text = GamePreferences.FeatureName(feature);
        var cost = CardSymbolText.Create(featureHeader, "FeatureCost", 19, Paper);
        cost.alignment = TMPro.TextAlignmentOptions.TopRight;
        cost.enableWordWrapping = false;
        var featureCostWidth = cost.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        cost.text = CardSymbolText.Format(feature.manaCost);
        SetCostWidth(cost, featureCostWidth, !string.IsNullOrWhiteSpace(feature.manaCost));
        var typeLine = Label(section.transform, "FeatureType", 16, MenuVisualTheme.Stone);
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
        if (!string.IsNullOrEmpty(statsText)) Label(section.transform, "FeatureStats", 17, MenuVisualTheme.Silver).text = statsText;
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
            case "Other face": return feature.faceIndex == 1 ? DeckCompanion.Text("背面","Back face","第２面")
                : DeckCompanion.Text($"第 {feature.faceIndex + 1} 面",$"Face {feature.faceIndex + 1}",$"第{feature.faceIndex + 1}面");
            case "Front face": return zh ? "正面" : ja ? "第１面" : en;
            default: return zh ? "另一部分" : ja ? "別の部分" : en;
        }
    }

    private static string WithAvailabilityNotice(CardDefinition card, string source)
    {
        // Match the canonical name across printings and display languages.
        // Tug of War is not in the pinned catalog yet; keep the same notice if added.
        if (card == null || !(card.cardName is "Shahrazad" or "Enter the Dungeon"
            or "The Countdown Is at One" or "Tug of War")) return source;
        string notice = GamePreferences.Language == "en-US" ? "(Temporarily unavailable)"
            : GamePreferences.Language == "ja-JP" ? "（現在正常に使用できません）"
            : "（暂时无法正常使用）";
        return string.IsNullOrWhiteSpace(source) ? notice : notice + "\n" + source;
    }

    private void SetRules(TMPro.TextMeshProUGUI text, string source)
    {
        text.GetComponent<PreferenceText>().SetMenuFace(MenuTypography.Face.Control);
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

    private static TMPro.TextMeshProUGUI DetailTitle(Transform parent,string name,int size)
    {
        var label=CardSymbolText.Create(parent,name,size,Paper);
        label.GetComponent<PreferenceText>().SetMenuFace(MenuTypography.Face.Heading);
        label.alignment=TMPro.TextAlignmentOptions.TopLeft;
        return label;
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
