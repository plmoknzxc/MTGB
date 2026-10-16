using UnityEngine;
using UnityEngine.UI;

// Presentation only: existing controls retain their listeners and serialized references.
public static class MenuVisualTheme
{
    public static readonly Color Ink = new Color(.055f, .067f, .09f, 1);
    public static readonly Color Panel = new Color(.09f, .11f, .145f, 1);
    public static readonly Color Raised = new Color(.14f, .175f, .23f, 1);
    public static readonly Color Accent = new Color(.40f, .65f, .96f, 1);
    public static readonly Color Primary = new Color(.18f, .34f, .56f, 1);
    public static readonly Color Line = new Color(.23f, .29f, .38f, 1);
    public static readonly Color Paper = new Color(.92f, .95f, .99f, 1);
    public static readonly Color Muted = new Color(.61f, .68f, .78f, 1);

    private static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static void Place(Transform root, string name, float x, float y, float w, float h)
    {
        RectTransform r = Find(root, name) as RectTransform;
        if (r == null) return;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform r, float inset = 0)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.one * inset; r.offsetMax = Vector2.one * -inset;
    }

    private static void Text(Transform root, string name, string value, int size)
    {
        var t = Find(root, name)?.GetComponent<Text>();
        if (t == null) return;
        t.text = value; t.fontSize = size; t.color = Paper;
    }

    private static void Background(Transform root, float opacity)
    {
        if (root.Find("SanctumArtwork") != null) return;
        var go = new GameObject("SanctumArtwork", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        go.transform.SetParent(root, false); go.transform.SetAsFirstSibling();
        Stretch((RectTransform)go.transform);
        var image = go.GetComponent<RawImage>();
        image.texture = Resources.Load<Texture2D>("Menu/ArchiveSanctum");
        image.color = new Color(.68f, .79f, 1f, opacity); image.raycastTarget = false;
        var fit = go.GetComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = image.texture != null ? (float)image.texture.width / image.texture.height : 16f / 9;
    }

    private static void Style(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Text>(true))
        {
            t.color = t.name.Contains("Help") || t.name == "Kicker" || t.name == "Subtitle" ? Muted : Paper;
            if (t.name == "Kicker") { t.color = Accent; t.fontSize = 14; }
        }
        foreach (var i in root.GetComponentsInChildren<Image>(true))
        {
            if (i.name == "StartScreen" || i.name == "DeckBuilderScreen") { i.color = Ink; continue; }
            if (i.name.EndsWith("Scroll") || i.name.EndsWith("Backplate") || i.name == "CardPreviewPanel" || i.name == "TopControlBand") { i.color = Panel; continue; }
            if (i.sprite != null) continue;
            if (i.name.Contains("Rule") || i.name == "Divider") i.color = Accent;
            else if (i.name.Contains("Background")) i.color = Ink;
            else if (i.name.Contains("Card") || i.name.Contains("Panel") || i.name.Contains("Band") || i.name.Contains("Backplate")) i.color = Panel;
        }
        foreach (var b in root.GetComponentsInChildren<Button>(true))
        {
            bool primary = b.name == "OnlineMatchButton" || b.name == "HostButton" || b.name == "ReadyButton" || b.name == "SideboardReadyButton" || b.name == "SaveDeckButton";
            var i = b.GetComponent<Image>(); if (i != null) i.color = primary ? Primary : Panel;
            var outline = b.GetComponent<Outline>() ?? b.gameObject.AddComponent<Outline>();
            outline.effectColor = primary ? Accent : Line; outline.effectDistance = Vector2.one;
            var colors = b.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.22f,1.22f,1.22f); colors.pressedColor = new Color(.7f,.7f,.7f); b.colors = colors;
            var label = b.transform.Find("Label") as RectTransform;
            if (label != null) Stretch(label, 8);
        }
        foreach (var field in root.GetComponentsInChildren<InputField>(true))
        {
            field.GetComponent<Image>().color = Ink;
            field.selectionColor = new Color(.3f,.5f,.8f,.6f);
        }
    }

    // Shared by the authored scene and runtime so the start screen has one layout.
    public static void ApplyStartScreen(Transform start)
    {
        Style(start);
        Background(start, .58f);
        var content = start.Find("StartMenuContent");
        if (content == null)
        {
            content = new GameObject("StartMenuContent", typeof(RectTransform), typeof(CanvasGroup)).transform;
            content.SetParent(start, false);
            Stretch((RectTransform)content);
            foreach (string name in new[] { "StartLogoSlot", "OnlineMatchButton", "SettingsButton", "DeckEditorButton" })
                Find(start, name).SetParent(content, false);
        }
        Place(start, "StartLogoSlot", -570, 235, 570, 220);
        Place(start, "StartTitle", 0, 28, 570, 180);
        Text(start, "StartTitle", "MTGB", 112);
        var title = Find(start, "StartTitle").GetComponent<Text>();
        title.resizeTextForBestFit = false;
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        Place(start, "OnlineMatchButton", -570, -10, 570, 86);
        Place(start, "SettingsButton", -570, -118, 570, 86);
        Place(start, "DeckEditorButton", -570, -226, 570, 86);
        Find(start, "SettingsButton").GetComponentInChildren<Text>(true).text = "观看录像";
        foreach (string name in new[] { "OnlineMatchButton", "DeckEditorButton", "SettingsButton" })
        {
            var button = Find(start, name);
            var label = button.Find("Text") as RectTransform;
            if (label != null) { Stretch(label, 12); label.offsetMin = new Vector2(24, 12); label.GetComponent<Text>().fontSize = 26; }
        }

    }

    public static void ApplyMain(Transform root)
    {
        Style(root);
        ApplyStartScreen(Find(root, "StartScreen"));
        var deck = Find(root, "DeckBuilderScreen");
        var toolbarBand = Find(deck, "TopControlBand") as RectTransform;
        toolbarBand.anchorMin = new Vector2(0, 1); toolbarBand.anchorMax = Vector2.one;
        toolbarBand.offsetMin = new Vector2(0, -96); toolbarBand.offsetMax = Vector2.zero;
        toolbarBand.GetComponent<Image>().color = new Color(.105f, .13f, .17f);
        toolbarBand.GetComponent<Image>().raycastTarget = false;
        Place(deck, "BackToTitleButton", -864, 482, 120, 44);
        Place(deck, "DeckDropdown", -550, 482, 440, 44);
        Place(deck, "DeckNameInput", -550, 482, 440, 44);
        Find(deck, "DeckNameInput").gameObject.SetActive(false);
        var dropdown = Find(deck, "DeckDropdown").GetComponent<Dropdown>();
        Stretch(dropdown.captionText.rectTransform, 10);
        dropdown.captionText.rectTransform.offsetMax = new Vector2(-38, -4);
        DropdownChevron.Apply(dropdown);
        var template = dropdown.template;
        template.anchorMin = new Vector2(0, 0); template.anchorMax = new Vector2(1, 0);
        template.pivot = new Vector2(.5f, 1); template.anchoredPosition = new Vector2(0, -4);
        template.sizeDelta = new Vector2(0, 260);
        var item = dropdown.itemText.transform.parent as RectTransform;
        item.anchorMin = new Vector2(0, 1); item.anchorMax = Vector2.one;
        item.pivot = new Vector2(.5f, 1); item.anchoredPosition = Vector2.zero;
        item.sizeDelta = new Vector2(0, 44);
        var content = item.parent as RectTransform;
        content.pivot = new Vector2(.5f, 1); content.sizeDelta = new Vector2(0, 44);
        Stretch(dropdown.itemText.rectTransform, 2);
        dropdown.itemText.rectTransform.offsetMin = new Vector2(12, 2);
        dropdown.itemText.rectTransform.offsetMax = new Vector2(-12, -2);
        dropdown.itemText.verticalOverflow = VerticalWrapMode.Overflow;
        dropdown.itemText.alignment = TextAnchor.MiddleLeft;
        dropdown.itemText.color = Paper;
        dropdown.itemText.fontSize = 20;
        // Stencil masks need opaque pixels even when the mask graphic is hidden.
        var viewport = template.GetComponent<ScrollRect>().viewport;
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        dropdown.template.GetComponent<ScrollRect>().scrollSensitivity = 40;
        Place(deck, "RenameDeckButton", -100, 482, 148, 44);
        Place(deck, "NewDeckButton", 40, 482, 92, 44);
        Place(deck, "SaveDeckButton", 144, 482, 92, 44);
        Place(deck, "ReloadButton", 248, 482, 92, 44);
        Place(deck, "CollectionSearchInput", 470, 482, 300, 44);
        Place(deck, "CollectionSearchButton", 676, 482, 88, 44);
        Place(deck, "AdvancedSearchButton", 824, 482, 184, 44);
        Place(deck, "CardPreviewPanel", -760, -20, 352, 878);
        Place(deck, "CardPreviewImage", 0, 204, 302, 423);
        Place(deck, "CardInfoScroll", 0, -215, 378, 372);
        Region(deck, "CardPreviewPanel", .005f, .01f, .245f, .90f);
        Region(deck, "DeckBackplate", .25f, .01f, .745f, .90f);
        Region(deck, "MainDeckCountText", .26f, .86f, .735f, .90f);
        Region(deck, "MainDeckScroll", .255f, .315f, .74f, .855f);
        Region(deck, "SideboardCountText", .26f, .275f, .735f, .31f);
        Region(deck, "SideboardScroll", .255f, .01f, .74f, .27f);
        Region(deck, "CollectionBackplate", .75f, .01f, .995f, .90f);
        Region(deck, "CollectionCountText", .76f, .86f, .985f, .90f);
        Region(deck, "CollectionScroll", .755f, .075f, .99f, .855f);
        Grid(deck, "CollectionContent", 2, 180, 252);
        Grid(deck, "MainDeckContent", 4, 180, 252);
        Grid(deck, "SideboardContent", 4, 170, 238);
        ConfigureScrolling(deck);
        // Four fixed-size cards need a fixed-width deck area. Give surplus width to details.
        var deckGrid = Find(deck, "MainDeckContent").GetComponent<GridLayoutGroup>();
        float deckWidth = deckGrid.cellSize.x * 4 + deckGrid.spacing.x * 3
            + deckGrid.padding.horizontal + 44 + 30;
        foreach (string name in new[] { "DeckBackplate", "MainDeckScroll", "SideboardScroll", "MainDeckCountText", "SideboardCountText" })
        {
            var area = (RectTransform)Find(deck, name);
            area.anchorMin = new Vector2(.745f, area.anchorMin.y);
            area.anchorMax = new Vector2(.745f, area.anchorMax.y);
            float inset = name == "DeckBackplate" ? 0 : 10;
            area.offsetMin = new Vector2(-deckWidth + inset, 0);
            area.offsetMax = new Vector2(-inset, 0);
        }
        var detailPanel = (RectTransform)Find(deck, "CardPreviewPanel");
        detailPanel.anchorMax = new Vector2(.745f, detailPanel.anchorMax.y);
        detailPanel.offsetMax = new Vector2(-deckWidth - 10, 0);
        // A quiet slate frame gives the artwork and rules their own visual hierarchy.
        Tint(deck, "CardPreviewPanel", new Color(.075f, .105f, .14f));
        Tint(deck, "CardInfoScroll", new Color(.105f, .14f, .18f));
        Tint(deck, "DeckBackplate", new Color(.105f, .13f, .175f));
        Tint(deck, "MainDeckScroll", new Color(.08f, .10f, .14f));
        Tint(deck, "SideboardScroll", new Color(.08f, .10f, .14f));
        Tint(deck, "CollectionBackplate", new Color(.095f, .155f, .16f));
        Tint(deck, "CollectionScroll", new Color(.065f, .115f, .125f));

        var preview = Find(deck, "CardPreviewImage") as RectTransform;
        preview.anchorMin = preview.anchorMax = new Vector2(.5f, 1);
        preview.pivot = new Vector2(.5f, 1);
        preview.anchoredPosition = new Vector2(0, -37);
        preview.sizeDelta = new Vector2(287, 402);
        preview.GetComponent<Image>().preserveAspect = true;
        var details = Find(deck, "CardInfoScroll") as RectTransform;
        details.anchorMin = Vector2.zero; details.anchorMax = Vector2.one;
        details.offsetMin = new Vector2(20, 12);
        details.offsetMax = new Vector2(-12, -451);
    }

    private static void Tint(Transform root, string name, Color color)
    {
        var image = Find(root, name)?.GetComponent<Image>();
        if (image != null) image.color = color;
    }

    private static void Region(Transform root, string name, float left, float bottom, float right, float top)
    {
        var rect = Find(root, name) as RectTransform;
        if (rect == null) return;
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void ConfigureScrolling(Transform root)
    {
        foreach (var scroll in root.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true))
        {
            if (scroll.name == "Template" || scroll.viewport == null) continue;
            scroll.scrollSensitivity = 60f;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            bool isCardDetail = scroll.name == "CardInfoScroll";
            // Keep the track outside the card hit areas so grabbing it never starts a card drag.
            scroll.viewport.offsetMax = new Vector2(isCardDetail ? -26f : -44f, scroll.viewport.offsetMax.y);
            if (scroll.verticalScrollbar != null)
            {
                var existingTrack = scroll.verticalScrollbar.GetComponent<UnityEngine.UI.Image>();
                if (existingTrack != null) existingTrack.color = Ink;
                if (scroll.verticalScrollbar.targetGraphic != null)
                    scroll.verticalScrollbar.targetGraphic.color = new Color(.36f, .47f, .63f);
                continue;
            }
            var track = new GameObject("VerticalScrollbar", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Scrollbar));
            track.transform.SetParent(scroll.transform, false);
            var rect = (RectTransform)track.transform;
            rect.anchorMin = new Vector2(1, 0); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1, .5f);
            rect.offsetMin = new Vector2(isCardDetail ? -20 : -36, 10); rect.offsetMax = new Vector2(-4, -10);
            track.GetComponent<UnityEngine.UI.Image>().color = Ink;
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            handle.transform.SetParent(track.transform, false);
            Stretch((RectTransform)handle.transform, 4);
            var image = handle.GetComponent<UnityEngine.UI.Image>(); image.color = new Color(.36f, .47f, .63f);
            var bar = track.GetComponent<UnityEngine.UI.Scrollbar>();
            bar.handleRect = (RectTransform)handle.transform; bar.targetGraphic = image;
            bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = isCardDetail
                ? UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide
                : UnityEngine.UI.ScrollRect.ScrollbarVisibility.Permanent;
        }
    }

    private static void Grid(Transform root, string name, int columns, float w, float h)
    {
        var grid = Find(root, name)?.GetComponent<GridLayoutGroup>();
        if (grid == null) return;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = columns;
        grid.cellSize = new Vector2(w, h); grid.spacing = new Vector2(10, 12);
    }

    public static void ApplyLobby(Transform root)
    {
        Style(root);
        // A quiet, opaque room surface keeps the focus on players and readiness.
        root.GetComponent<Image>().color = new Color(.065f, .065f, .08f);
        var content = Find(root, "Content");
        Find(content, "HeaderBlueRule").gameObject.SetActive(false);
        Find(content, "HeaderRedRule").gameObject.SetActive(false);
        Place(content, "HeaderBand", 0, 437, 1560, 100);
        Place(content, "BackButton", -714, 437, 132, 48);
        Place(content, "Title", -360, 452, 510, 48);
        Place(content, "Subtitle", -360, 410, 510, 28);
        Place(content, "ConnectionBadge", 660, 437, 240, 42);
        Text(content, "Title", "联机对战", 32);
        Text(content, "Subtitle", "连接房间，选择牌组，准备对局", 17);

        var connection = Find(root, "ConnectionCard");
        var setup = Find(root, "MatchSetupCard");
        var room = Find(root, "RoomSummaryCard");
        var prep = Find(root, "DeckPreparationCard");
        foreach (var card in new[] { connection, setup, room, prep })
        {
            Place(card.parent, card.name, card == connection || card == room ? -400 : 400, -28, 760, 760);
            Place(card, "Kicker", 0, 323, 648, 24);
            Place(card, "Heading", 0, 276, 648, 48);
        }
        Text(connection, "Kicker", "连接", 14);
        Text(connection, "Heading", "创建或加入房间", 30);
        string[] labels = { "NameLabel", "EndpointLabel", "RoomLabel", "ProfileLabel" };
        string[] fields = { "NameInput", "AddressInput", "RoomInput", "ProfileInput" };
        for (int i = 0; i < labels.Length; i++)
        {
            float y = 193 - i * 68;
            Place(connection, labels[i], -250, y, 148, 34);
            Place(connection, fields[i], 78, y, 492, 48);
        }
        Place(connection, "AddressInput", 1, 125, 338, 48);
        Place(connection, "PortInput", 255, 125, 138, 48);
        Place(connection, "HostButton", -170, -104, 308, 56);
        Place(connection, "ConnectButton", 170, -104, 308, 56);
        Place(connection, "ConnectionHelp", 0, -172, 648, 56);
        Place(connection, "DeckStepHint", 0, -276, 648, 112);
        Place(connection, "DeckLabel", 0, 29, 604, 28);
        Place(connection, "DeckValue", 0, -19, 604, 56);
        Text(connection, "DeckLabel", "连接之后", 15);
        Text(connection, "DeckValue", "选择出战牌组，等待双方准备后开始比赛。", 18);

        Text(setup, "Kicker", "房主设置", 14);
        Text(setup, "Heading", "对局规则", 30);
        Place(setup, "LifeLabel", -227, 205, 194, 28);
        Place(setup, "LifeInput", -227, 157, 194, 48);
        Place(setup, "FormatLabel", 113, 205, 422, 28);
        Place(setup, "FormatDropdown", 113, 157, 422, 48);
        Place(setup, "SetsLabel", 0, 86, 648, 28);
        Place(setup, "CardFormatDropdown", 0, 39, 648, 48);
        Place(setup, "CustomToggle", 0, -55, 648, 88);
        var custom = Find(setup, "CustomToggle");
        Place(custom, "Label", -32, 25, 440, 30);
        Place(custom, "Description", -5, -25, 494, 48);
        Place(custom, "State", 249, 17, 110, 30);
        Place(setup, "Divider", 0, -119, 648, 1);
        Place(setup, "RulesHelp", 0, -180, 648, 90);
        Text(setup, "RulesHelp", "主牌至少 60 张 · 备牌最多 15 张\n非基本牌合计最多 4 张 · 每局随机决定先后手", 17);
        Place(setup, "ConnectionStatusPanel", 0, -287, 648, 92);
        Place(Find(setup, "ConnectionStatusPanel"), "Status", 0, 0, 604, 68);
        var ante = Find(setup, "AnteToggle");
        if (ante != null)
        {
            Place(setup, "AnteToggle", 0, -153, 648, 88);
            Place(ante, "Label", -32, 25, 440, 30);
            Place(ante, "Description", -5, -25, 494, 48);
            Place(ante, "State", 249, 17, 110, 30);
            Place(setup, "Divider", 0, -209, 648, 1);
        }

        Text(room, "Kicker", "房间", 14);
        Place(room, "RoomTitle", 0, 270, 648, 52);
        Place(room, "RoomRules", 0, 202, 648, 72);
        Place(room, "SeriesStatus", 0, 140, 648, 36);
        Place(room, "PlayersLabel", 0, 76, 648, 32);
        Place(room, "PlayersPanel", 0, -53, 648, 206);
        Place(room, "StatusPanel", 0, -216, 648, 68);
        Place(Find(room, "StatusPanel"), "Status", 0, 0, 604, 54);
        Place(room, "LeaveRoomButton", -215, -308, 218, 50);
        Text(prep, "Kicker", "准备", 14);
        Text(prep, "Heading", "选择出战牌组", 30);
        Place(prep, "DeckLabel", 0, 200, 648, 30);
        Place(prep, "DeckDropdown", 0, 148, 648, 56);
        Place(prep, "SelectedDeckSummary", 0, 36, 648, 112);
        Place(prep, "DeckValue", 0, 0, 600, 82);
        Place(prep, "PreparationInfo", 0, -127, 648, 174);
        var info = Find(prep, "PreparationInfo");
        Place(info, "Title", 0, 55, 604, 32);
        Place(info, "Body", 0, -22, 604, 98);
        Text(info, "Title", "准备与开局", 18);
        Text(info, "Body", "双方提交牌组并准备后，由房主开始比赛。\nBO3 每局结束后可调整备牌。", 17);
        info.Find("Body").GetComponent<UnityEngine.UI.Text>().alignment=TextAnchor.UpperLeft;
        Place(prep, "ReadyButton", -170, -260, 308, 58);
        Place(prep, "StartMatchButton", 170, -260, 308, 58);
        Place(prep, "ReadyHelp", 0, -317, 648, 38);
        Text(prep, "ReadyHelp", "需要更换牌组时，请先取消准备。", 16);
        ApplyLobbyColors(root);

        var side = Find(root, "SideboardPage");
        Place(side, "SideboardHeader", 0, 322, 1720, 120);
        Place(side, "Kicker", -520, 32, 680, 24);
        Place(side, "Heading", -520, -14, 680, 48);
        Text(side, "Kicker", "局间调整   /   换备", 16);
        foreach (string name in new[] { "MainDeckCard", "SideboardCard" })
        {
            var card = Find(side, name);
            Place(side, name, name == "MainDeckCard" ? -430 : 430, -52, 820, 584);
            Place(card, "Heading", 0, 248, 740, 40);
            string scroll = name == "MainDeckCard" ? "MainDeckScroll" : "SideboardScroll";
            Place(card, scroll, 0, -24, 780, 484);
        }
        Grid(side, "MainDeckContent", 4, 170, 238);
        Grid(side, "SideboardContent", 4, 170, 238);
        Place(side, "DeckSummary", -500, -391, 720, 44);
        Place(side, "ResetSideboardButton", 205, -391, 220, 54);
        Place(side, "SideboardReadyButton", 462, -391, 260, 54);
        Place(side, "SideboardStartButton", 745, -391, 260, 54);
        Place(side, "SideboardStatus", 0, -450, 1640, 40);
        ConfigureScrolling(side);
    }

    private static void ApplyLobbyColors(Transform root)
    {
        Color surface = new Color(.13f, .14f, .17f);
        Color field = new Color(.085f, .095f, .12f);
        Color raised = new Color(.12f, .16f, .18f);
        Color accent = new Color(.42f, .8f, .7f);
        Color edge = new Color(.20f, .27f, .29f);
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.transform == root || image.sprite != null) continue;
            image.color = surface;
            string name = image.name;
            if (name.EndsWith("Input") || name.EndsWith("Dropdown") || name == "SelectedDeckSummary"
                || name == "DeckStepHint" || name == "Seat1" || name == "Seat2") image.color = field;
            if (name == "HeaderBand" || name == "PlayersPanel") image.color = Color.clear;
            if (name == "Divider") image.color = edge;
            var outline = image.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = edge;
                outline.effectDistance = new Vector2(1, -1);
                outline.enabled = name != "HeaderBand" && name != "PlayersPanel";
            }
        }
        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            // Keep selectable graphics clickable; other labels should not intercept input.
            var selectable = text.GetComponentInParent<Selectable>();
            text.raycastTarget = selectable != null && selectable.targetGraphic == text;
            text.color = text.name == "Kicker" || text.name == "SeatNumber" ? accent :
                text.name.Contains("Help") || text.name.EndsWith("Label") || text.name == "Description"
                || text.name == "Subtitle" || text.name == "RoomRules" || text.name == "Status"
                || text.name == "PlayerState" ? Muted : Paper;
            if (text.name == "Checkmark") text.color = accent;
        }
        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            bool primary = button.name == "HostButton" || button.name == "ReadyButton" || button.name == "SideboardReadyButton";
            button.GetComponent<Image>().color = primary ? new Color(.15f, .37f, .33f) : raised;
            button.GetComponent<Outline>().effectColor = primary ? accent * new Color(1, 1, 1, .65f) : edge;
            var colors = button.colors;
            colors.disabledColor = new Color(.55f, .55f, .55f, .65f);
            button.colors = colors;
            button.GetComponentInChildren<Text>(true).color = Paper;
        }
        // Resized controls must also resize their caption and editable text regions.
        foreach (var fieldControl in root.GetComponentsInChildren<InputField>(true))
        {
            Stretch(fieldControl.textComponent.rectTransform, 12);
            if (fieldControl.placeholder != null) Stretch(fieldControl.placeholder.rectTransform, 12);
        }
        foreach (var dropdown in root.GetComponentsInChildren<Dropdown>(true))
        {
            Stretch(dropdown.captionText.rectTransform, 12);
            dropdown.captionText.rectTransform.offsetMin = new Vector2(12, 4);
            dropdown.captionText.rectTransform.offsetMax = new Vector2(-38, -4);
            dropdown.captionText.verticalOverflow = VerticalWrapMode.Overflow;
            var arrow = dropdown.transform.Find("Arrow") as RectTransform;
            if (arrow != null)
            {
                arrow.anchorMin = arrow.anchorMax = new Vector2(1, .5f);
                arrow.anchoredPosition = new Vector2(-22, 0);
            }
        }
    }
}
