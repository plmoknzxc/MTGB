using UnityEngine;
using UnityEngine.UI;

// Presentation only: existing controls retain their listeners and serialized references.
public static class MenuVisualTheme
{
    private static Texture2D deckToolbarShade;
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
        t.text = value; PreferenceText.SetSize(t,size); t.color = Paper;
    }

    internal static void Background(Transform root, MenuThemeArtwork.Role role, float opacity)
    {
        var existing = root.Find("SanctumArtwork");
        var go = existing != null ? existing.gameObject : new GameObject("SanctumArtwork", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        if (existing == null) go.transform.SetParent(root, false);
        go.transform.SetAsFirstSibling();
        Stretch((RectTransform)go.transform);
        var image = go.GetComponent<RawImage>();
        image.color = new Color(1f, 1f, 1f, opacity); image.raycastTarget = false;
        var fit = go.GetComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        MenuThemeArtwork.Bind(image, role);
    }

    private static void Style(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Text>(true))
        {
            t.color = t.name.Contains("Help") || t.name == "Kicker" || t.name == "Subtitle" ? Muted : Paper;
            if (t.name == "Kicker") { t.color = Accent; PreferenceText.SetSize(t,14); }
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
        MenuTypography.Scope(start);
        Style(start);
        Background(start, MenuThemeArtwork.Role.Start, .96f);
        var content = start.Find("StartMenuContent");
        if (content == null)
        {
            content = new GameObject("StartMenuContent", typeof(RectTransform), typeof(CanvasGroup)).transform;
            content.SetParent(start, false);
            Stretch((RectTransform)content);
            foreach (string name in new[] { "StartLogoSlot", "OnlineMatchButton", "SettingsButton", "DeckEditorButton" })
                Find(start, name).SetParent(content, false);
        }
        Place(start, "StartLogoSlot", -555, 186, 570, 220);
        Place(start, "StartTitle", 0, 28, 570, 180);
        Text(start, "StartTitle", "MTGB", 104);
        var title = Find(start, "StartTitle").GetComponent<Text>();
        title.GetComponent<PreferenceText>().KeepFont = false;
        MenuTypography.Use(title, MenuTypography.Face.Wordmark);
        title.color = ArchivePaper;
        title.resizeTextForBestFit = false;
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        Find(start, "SettingsButton").GetComponentInChildren<Text>(true).text = "观看录像";
        ApplyStartNavigation(start);
        StyleStartIcon<ForgeGearIcon>(content,"StartSettingsButton","GearIcon",28);
        StyleStartIcon<ClientUpdateIcon>(content,"StartUpdateButton","UpdateIcon",104);
    }

    static void StyleStartIcon<T>(Transform content,string name,string iconName,float inset) where T : MaskableGraphic
    {
        var rect = content.Find(name) as RectTransform;
        if (rect == null)
        {
            rect = (RectTransform)new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).transform;
            rect.SetParent(content, false);
            var icon = new GameObject(iconName, typeof(RectTransform), typeof(T)).GetComponent<T>();
            icon.rectTransform.SetParent(rect, false);
            icon.rectTransform.sizeDelta = new Vector2(34, 34);
            icon.raycastTarget = false;
        }
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.sizeDelta = new Vector2(64, 64);
        rect.anchoredPosition = new Vector2(-inset, -24);
        var button = rect.GetComponent<Button>();
        StyleArchiveControl(button);
        Stretch(button.targetGraphic.rectTransform, 2.5f);
        rect.GetComponentInChildren<T>(true).color = ArchivePaper;
    }

    public static void ApplyStartNavigation(Transform start)
    {
        EnsureLocalPlayButton(start);
        string[] names={"OnlineMatchButton","LocalAiButton","SettingsButton","DeckEditorButton"};
        float[] rows = {74, -11, -113, -198};
        for (int i = 0; i < names.Length; i++)
        {
            var target = Find(start, names[i]);
            if (target == null) continue;
            Place(start, names[i], -638, rows[i], 404, 69);
            var button = target.GetComponent<Button>();
            var label = button.GetComponentInChildren<Text>();
            var arrow = target.Find("MenuArrow");
            if (arrow != null) arrow.gameObject.SetActive(false);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(28, 2);
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.resizeTextForBestFit = false;
            label.raycastTarget = false;
            PreferenceText.SetSize(label, 29);
            StyleArchiveButton(button, label, i == 0);
            button.GetComponent<Image>().color = i == 0 ? MenuThemeCatalog.Current.NavigationEdge : Silver;
            Stretch(button.targetGraphic.rectTransform, 3.5f);
        }
    }

    static Sprite archiveButtonFace;
    static MenuThemeStyle buttonStyle;
    static Sprite ArchiveButtonFace
    {
        get
        {
            if (archiveButtonFace == null || buttonStyle != MenuThemeCatalog.Current)
            {
                buttonStyle = MenuThemeCatalog.Current;
                archiveButtonFace = buttonStyle.Load<Sprite>(buttonStyle.ButtonSprite) ?? MenuThemeCatalog.Default.Load<Sprite>(MenuThemeCatalog.Default.ButtonSprite);
            }
            return archiveButtonFace;
        }
    }
    static Sprite MoonlitFrieze => MenuThemeCatalog.Current.Load<Sprite>(MenuThemeCatalog.Current.HeaderArt)
        ?? Resources.Load<Sprite>(MenuThemeCatalog.Default.HeaderArt);
    internal static Color DialogShade => MenuThemeCatalog.Current.DialogShade;
    internal static Color ArchivePaper => MenuThemeCatalog.Current.ArchivePaper;
    internal static Color ArchivePanel => MenuThemeCatalog.Current.ArchivePanel;
    internal static Color Recess => MenuThemeCatalog.Current.Recess;
    internal static Color Slate => MenuThemeCatalog.Current.Slate;
    internal static Color LitSlate => MenuThemeCatalog.Current.LitSlate;
    internal static Color MistBlue => MenuThemeCatalog.Current.MistBlue;
    internal static Color Silver => MenuThemeCatalog.Current.Silver;
    internal static Color Stone => MenuThemeCatalog.Current.Stone;
    internal static Color Etching => MenuThemeCatalog.Current.Etching;
    static Color ActionFill => MenuThemeCatalog.Current.ActionFill;
    static Color ActionHover => MenuThemeCatalog.Current.ActionHover;
    static Color ActionPressed => MenuThemeCatalog.Current.ActionPressed;
    static Color ActionEdge => MenuThemeCatalog.Current.ActionEdge;
    static Color ActionText => MenuThemeCatalog.Current.ActionText;
    internal static Color Warning => MenuThemeCatalog.Current.Warning;
    internal static Color MainAccent => MenuThemeCatalog.Current.MainAccent;
    internal static Color SideAccent => MenuThemeCatalog.Current.SideAccent;
    internal static Color LibraryAccent => MenuThemeCatalog.Current.LibraryAccent;
    internal static Color SideSurface => MenuThemeCatalog.Current.SideSurface;
    static Color ControlEdge => MenuThemeCatalog.Current.ControlEdge;
    static Sprite deckStoneWash;

    internal static Sprite StoneWash()
    {
        if (deckStoneWash != null) return deckStoneWash;
        var texture = new Texture2D(2,2,TextureFormat.RGBA32,false)
        {
            name = "Observatory slate", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels32(new Color32[] { new(19,30,39,255), new(24,39,48,255),
            new(38,56,66,255), new(51,66,72,255) });
        texture.Apply(false,true);
        deckStoneWash = Sprite.Create(texture,new Rect(0,0,2,2),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
        deckStoneWash.hideFlags = HideFlags.HideAndDontSave;
        return deckStoneWash;
    }

    private static void TintMenuControl(Selectable control, bool primary = false, bool recessed = false)
    {
        var colors = control.colors;
        colors.colorMultiplier = 1;
        colors.fadeDuration = .10f;
        colors.normalColor = primary ? ActionFill : recessed ? Recess : MenuThemeCatalog.Current.ControlFill;
        colors.highlightedColor = colors.selectedColor = primary ? ActionHover : recessed ? MenuThemeCatalog.Current.InputFocus : MenuThemeCatalog.Current.ControlHover;
        colors.pressedColor = primary ? ActionPressed : MenuThemeCatalog.Current.ControlPressed;
        colors.disabledColor = MenuThemeCatalog.Current.ControlDisabled;
        control.colors = colors;
        control.targetGraphic.CrossFadeColor(control.IsInteractable() ? colors.normalColor : colors.disabledColor, 0, true, true);
    }

    internal static Image Detail(Transform parent, string name, Color color)
    {
        var child = parent.Find(name);
        var image = child == null ? new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>() : child.GetComponent<Image>();
        if (child == null) image.transform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    internal static void SectionTop(Transform parent,Color accent)
    {
        var line=Detail(parent,"SectionTop",accent).rectTransform;
        line.anchorMin=Vector2.up;line.anchorMax=Vector2.one;
        line.offsetMin=new Vector2(0,-3);line.offsetMax=Vector2.zero;
    }

    internal static RectTransform ArchivePage(Canvas canvas, string name, string title, string back, System.Action close)
    {
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var surface = ReplayWidgets.Panel(canvas.transform, "Background", 0, 0, 0, 0, Ink);
        Stretch(surface);
        Background(surface, MenuThemeArtwork.Role.Hall, .63f);
        var root = ReplayWidgets.Rect(canvas.transform, name, 0, 0, 1640, 900);
        MenuTypography.Scope(root);
        var heading = ReplayWidgets.Label(root, title, -320, 401, 1000, 90, 50);
        MenuTypography.Use(heading, MenuTypography.Face.Heading);
        var rule = ReplayWidgets.Panel(root, "HeaderRule", 0, 342, 1640, 2, Etching);
        rule.GetComponent<Image>().raycastTarget = false;
        var button = ReplayWidgets.Button(root, back, 710, 401, 220, close);
        StyleDeckTool(button);
        return root;
    }

    internal static RectTransform ArchiveSection(Transform root, string name, string title, float x, float y,
        float width, float height, Color accent)
    {
        var panel = ReplayWidgets.Panel(root, name, x, y, width, height, ArchivePanel);
        StyleArchiveSection(panel.GetComponent<Image>(), accent);
        var header = ReplayWidgets.Label(panel, title, 0, height / 2 - 46, width - 64, 66, 31);
        header.color = accent;
        MenuTypography.Use(header, MenuTypography.Face.Heading);
        var line = ReplayWidgets.Panel(panel, "HeaderRule", 0, height / 2 - 86, width - 64, 1, Etching);
        line.GetComponent<Image>().raycastTarget = false;
        return panel;
    }

    internal static void StyleArchiveSection(Image panel, Color accent)
    {
        panel.color = MenuThemeCatalog.Current.SectionFill;
        ObsidianArenaTheme.EnsureOutline(panel, Etching, Vector2.one);
        SectionTop(panel.transform, accent);
    }

    internal static void StyleDeckDialogSurface(RectTransform panel, Text heading, float headerHeight = 72)
    {
        MenuTypography.Scope(panel);
        var surface = panel.GetComponent<Image>();
        surface.sprite = StoneWash();
        surface.color = MenuThemeCatalog.Current.DialogTint;
        StyleArchiveLintel(panel, headerHeight);

        var rim = DialogRim(panel, "DialogFrame", 0, 3.5f, MistBlue);
        DialogRim(rim, "InnerRim", 6, 1.5f, Etching);
        var title = heading.rectTransform;
        title.anchorMin = Vector2.up;
        title.anchorMax = Vector2.one;
        title.offsetMin = new Vector2(36, -headerHeight);
        title.offsetMax = new Vector2(-60, 0);
        heading.alignment = TextAnchor.MiddleLeft;
        heading.color = ArchivePaper;
        MenuTypography.Use(heading, MenuTypography.Face.Heading);
        ObsidianArenaTheme.EnsureOutline(heading, new Color(.035f, .04f, .065f, .94f), new Vector2(1.5f, -1.5f));
    }

    internal static void StyleArchiveLintel(RectTransform panel, float headerHeight, float artworkOpacity = 1f)
    {
        // Crop the same panorama as the deck toolbar while keeping the moon round
        // when a picker changes width between its empty and populated layouts.
        var band = Detail(panel, "DialogLintel", Recess);
        band.transform.SetAsFirstSibling();
        band.rectTransform.anchorMin = Vector2.up;
        band.rectTransform.anchorMax = Vector2.one;
        band.rectTransform.offsetMin = new Vector2(0, -headerHeight);
        band.rectTransform.offsetMax = Vector2.zero;
        if (!band.TryGetComponent<RectMask2D>(out _)) band.gameObject.AddComponent<RectMask2D>();
        var art = Detail(band.transform, "Panorama", new Color(1, 1, 1, artworkOpacity));
        art.sprite = MoonlitFrieze;
        var fitter = art.GetComponent<AspectRatioFitter>() ?? art.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = art.sprite.rect.width / art.sprite.rect.height;
        var rule = Detail(band.transform, "Rule", MistBlue).rectTransform;
        rule.anchorMin = Vector2.zero;
        rule.anchorMax = Vector2.right;
        rule.offsetMin = Vector2.zero;
        rule.offsetMax = Vector2.up * 2;

    }

    static RectTransform DialogRim(Transform parent, string name, float inset, float thickness, Color color)
    {
        var existing = parent.Find(name);
        var rim = existing != null ? existing.GetComponent<BattlefieldStackFrameGraphic>()
            : new GameObject(name, typeof(RectTransform), typeof(BattlefieldStackFrameGraphic)).GetComponent<BattlefieldStackFrameGraphic>();
        if (existing == null) rim.transform.SetParent(parent, false);
        Stretch(rim.rectTransform, inset);
        rim.Radius = thickness + 1;
        rim.Thickness = thickness;
        rim.color = color;
        rim.raycastTarget = false;
        return rim.rectTransform;
    }

    internal static void StylePreferenceToggle(Toggle toggle)
    {
        var box = (Image)toggle.targetGraphic;
        bool available = toggle.interactable;
        var muted = Color.Lerp(Color.black, Color.white, Mathf.Lerp(Recess.grayscale, ArchivePaper.grayscale, .55f));
        box.color = available ? Recess : Color.Lerp(Color.black, Color.white, Recess.grayscale);
        ObsidianArenaTheme.EnsureOutline(box, available ? MistBlue : muted, Vector2.one);
        toggle.graphic.color = available ? SideAccent : muted;
        toggle.GetComponentInChildren<Text>(true).color = available ? ArchivePaper : muted;
    }

    internal static void StyleDeckTool(Button button, bool primary = false)
    {
        var label = button.GetComponentInChildren<Text>(true);
        StyleArchiveControl(button,primary);
        MenuTypography.Use(label,MenuTypography.Face.Control);
        label.color = primary ? ActionText : ArchivePaper;
        label.raycastTarget = false;
        label.fontStyle = FontStyle.Normal;
    }

    internal static void StyleSearchButton(Button button, bool active)
    {
        StyleDeckTool(button, active);
        if (!active) return;
        button.GetComponent<Image>().color = MenuThemeCatalog.Current.SearchEdge;
        Stretch(button.targetGraphic.rectTransform, 2.5f);
        var colors = button.colors;
        colors.normalColor = MenuThemeCatalog.Current.SearchFill;
        colors.highlightedColor = colors.selectedColor = MenuThemeCatalog.Current.SearchHover;
        button.colors = colors;
        button.targetGraphic.CrossFadeColor(colors.normalColor, 0, true, true);
    }

    // Long search-result lists use one tinted graphic per option.
    internal static void StyleDeckOption(Button button, bool selected = false)
    {
        var face = button.GetComponent<Image>();
        face.sprite = ArchiveButtonFace;
        face.type = Image.Type.Sliced;
        face.color = Color.white;
        button.targetGraphic = face;
        if (button.TryGetComponent<Outline>(out var rim)) rim.enabled = false;
        TintMenuControl(button, selected);
        var label = button.GetComponentInChildren<Text>();
        MenuTypography.Use(label, MenuTypography.Face.Control);
        label.color = selected ? ActionText : ArchivePaper;
        label.raycastTarget = false;
    }

    internal static void StyleArchiveButton(Button button, Text label, bool primary = false)
    {
        StyleArchiveControl(button, primary);
        MenuTypography.Use(label,MenuTypography.Face.Control);
        label.color = primary ? ActionText : ArchivePaper;
        label.fontStyle = FontStyle.Normal;
        label.raycastTarget = false;
        label.canvasRenderer.SetColor(Color.white);
    }

    internal static void StyleArchiveControl(Selectable control, bool primary = false, bool recessed = false)
    {
        if (control is Button button) ButtonPressSound.Bind(button);
        var face = control.GetComponent<Image>();
        face.sprite = ArchiveButtonFace;
        face.type = Image.Type.Sliced;
        face.raycastTarget = true;
        control.transition = Selectable.Transition.ColorTint;
        if (recessed)
        {
            // InputField inserts the caret as the first sibling of its text. Paint the
            // background on the parent so both the caret and the selection stay above it.
            face.color = Color.white;
            var edge = control.GetComponent<Outline>() ?? control.gameObject.AddComponent<Outline>();
            edge.enabled = true;
            edge.effectColor = ControlEdge;
            edge.effectDistance = new Vector2(1,-1);
            control.targetGraphic = face;
            TintMenuControl(control,recessed:true);
            return;
        }
        face.color = primary ? ActionEdge : ControlEdge;
        if (control.TryGetComponent<Outline>(out var outline)) outline.enabled = false;
        var fill = Detail(control.transform,"ControlFill",Color.white);
        fill.transform.SetAsFirstSibling();
        fill.sprite = face.sprite;
        fill.type = Image.Type.Sliced;
        Stretch(fill.rectTransform,1.5f);
        control.targetGraphic = fill;
        TintMenuControl(control,primary);
    }

    internal static void StyleArchiveDropdown(Dropdown dropdown)
    {
        StyleArchiveControl(dropdown);
        dropdown.captionText.color = ArchivePaper;
        MenuTypography.Use(dropdown.captionText,MenuTypography.Face.Control);
        MenuTypography.Use(dropdown.itemText,MenuTypography.Face.Control);
        dropdown.captionText.fontStyle = FontStyle.Normal;
        var clip = (RectTransform)dropdown.captionText.transform.parent;
        clip.anchorMin = Vector2.zero; clip.anchorMax = Vector2.one;
        clip.offsetMin = new Vector2(12,0); clip.offsetMax = new Vector2(-34,0);
        dropdown.captionText.alignment = TextAnchor.MiddleLeft;
        var arrow = dropdown.transform.Find("DropdownChevron")?.GetComponent<Graphic>();
        if (arrow != null) arrow.color = Stone;
        var popup = dropdown.template;
        popup.GetComponent<Image>().color = ArchivePanel;
        var border = popup.GetComponent<Outline>();
        if (border != null) { border.effectColor = Etching; border.effectDistance = new Vector2(1,-1); }
        var item = popup.GetComponentInChildren<Toggle>(true);
        var colors = item.colors;
        colors.normalColor = ArchivePanel;
        colors.highlightedColor = colors.selectedColor = LitSlate;
        colors.pressedColor = Slate;
        item.colors = colors;
        if (item.graphic != null) item.graphic.color = MistBlue;
        dropdown.itemText.color = ArchivePaper;
    }

    internal static void StyleDeckInput(InputField field)
    {
        StyleArchiveControl(field,recessed:true);
        MenuTypography.Use(field.textComponent,MenuTypography.Face.Control);
        field.textComponent.color = ArchivePaper;
        if (field.placeholder != null) field.placeholder.color = Stone;
        if (field.placeholder is Text hint) MenuTypography.Use(hint,MenuTypography.Face.Control);
        field.customCaretColor = true;
        field.caretColor = Silver;
        field.caretWidth = 3;
        field.caretBlinkRate = .85f;
        field.selectionColor = MenuThemeCatalog.Current.Selection;
    }

    internal static void StyleSearchInput(InputField field)
    {
        StyleDeckInput(field);
        // A separate rim stays legible while the recessed face tints on focus.
        // Its empty centre leaves the native caret and selection visible.
        DialogRim(field.transform, "InputRim", 0, 1.5f, MenuThemeCatalog.Current.InputRim).SetAsFirstSibling();
    }

    public static Button EnsureLocalPlayButton(Transform start)
    {
        var existing=Find(start,"LocalAiButton");
        if(existing!=null)return existing.GetComponent<Button>();
        var source=Find(start,"OnlineMatchButton").GetComponent<Button>();
        var button=Object.Instantiate(source,start.Find("StartMenuContent")??start);
        button.name="LocalAiButton";
        // A navigation clone must never inherit the online entry's events.
        button.onClick=new Button.ButtonClickedEvent();
        var label=button.GetComponentInChildren<Text>(true);
        label.text="本地模式";
        var binding=label.GetComponent<PreferenceText>()??label.gameObject.AddComponent<PreferenceText>();
        binding.Literal=true;
        return button;
    }

    public static void ApplyMain(Transform root)
    {
        Style(root);
        ApplyStartScreen(Find(root, "StartScreen"));
        var deck = Find(root, "DeckBuilderScreen");
        MenuTypography.Scope(deck);
        var toolbarBand = Find(deck, "TopControlBand") as RectTransform;
        toolbarBand.anchorMin = new Vector2(0, 1); toolbarBand.anchorMax = Vector2.one;
        toolbarBand.offsetMin = new Vector2(0,-82); toolbarBand.offsetMax = Vector2.zero;
        DeckSurface(deck, "TopControlBand", Color.white, MoonlitFrieze);
        toolbarBand.GetComponent<Image>().raycastTarget = false;
        var shade=new GameObject("ToolbarShade",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
        shade.transform.SetParent(toolbarBand,false);
        Stretch(shade.rectTransform);
        if(deckToolbarShade==null)
            deckToolbarShade=IntegratedUiUtility.HorizontalGradientTexture(128,1,new (float,Color)[]{
                (0,new Color(1,1,1,.93f)),(.29f,new Color(1,1,1,.81f)),
                (.53f,new Color(1,1,1,.2f)),(.70f,new Color(1,1,1,.16f)),(.88f,new Color(1,1,1,.90f))});
        shade.texture=deckToolbarShade;
        shade.color=Recess;
        shade.raycastTarget=false;
        Place(deck, "BackToTitleButton", -864, 482, 120, 44);
        Place(deck, "DeckDropdown", -550, 482, 440, 44);
        Place(deck, "DeckNameInput", -550, 482, 440, 44);
        Find(deck, "DeckNameInput").gameObject.SetActive(false);
        var dropdown = Find(deck, "DeckDropdown").GetComponent<Dropdown>();
        Stretch(dropdown.captionText.rectTransform, 10);
        dropdown.captionText.rectTransform.offsetMax = new Vector2(-38, -4);
        DropdownChevron.Apply(dropdown);
        StyleArchiveDropdown(dropdown);
        StyleDeckInput(Find(deck,"DeckNameInput").GetComponent<InputField>());
        StyleDeckInput(Find(deck,"CollectionSearchInput").GetComponent<InputField>());
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
        dropdown.itemText.color = ArchivePaper;
        PreferenceText.SetSize(dropdown.itemText,20);
        // Stencil masks need opaque pixels even when the mask graphic is hidden.
        var viewport = template.GetComponent<ScrollRect>().viewport;
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        dropdown.template.GetComponent<ScrollRect>().scrollSensitivity = 40;
        Place(deck, "NewDeckButton", 40, 482, 92, 44);
        Place(deck, "SaveDeckButton", 144, 482, 92, 44);
        Place(deck, "ReloadButton", 248, 482, 92, 44);
        Place(deck, "CollectionSearchInput", 470, 482, 300, 44);
        Place(deck, "CollectionSearchButton", 676, 482, 88, 44);
        Place(deck, "AdvancedSearchButton", 824, 482, 184, 44);
        var catalogue = Find(deck, "CollectionBackplate");
        Find(deck, "CollectionScroll").SetParent(catalogue, false);
        Region(deck, "CollectionBackplate", .717f, .012f, .992f, .924f);
        Region(catalogue, "CollectionScroll", .016f, .067f, .986f, .828f);
        var libraryScroll = (RectTransform)Find(catalogue,"CollectionScroll");
        libraryScroll.anchorMax = new Vector2(.986f,1);
        libraryScroll.offsetMax = new Vector2(-5,-104);
        Region(deck, "CardPreviewPanel", .008f, .012f, .203f, .924f);
        Grid(deck, "CollectionContent", 2, 232, 325);
        foreach (string name in new[] { "MainDeckContent", "SideboardContent" })
        {
            Grid(deck, name, 5, 202, 283);
        }
        var workspace = deck.Find("Background");
        workspace.GetComponent<Image>().color = Recess;
        Background(workspace, MenuThemeArtwork.Role.Deck, .08f);
        DeckSurface(deck,"CardPreviewPanel",MenuThemeCatalog.Current.PreviewFill);
        DeckSurface(deck,"CardInfoScroll",MenuThemeCatalog.Current.DetailFill);
        DeckSurface(deck,"DeckBackplate",MenuThemeCatalog.Current.MainSurface);
        DeckSurface(deck,"MainDeckScroll",MenuThemeCatalog.Current.MainWell);
        DeckSurface(deck,"SideboardScroll",MenuThemeCatalog.Current.SideWell);
        DeckSurface(deck,"CollectionBackplate",MenuThemeCatalog.Current.LibrarySurface);
        DeckSurface(deck,"CollectionScroll",MenuThemeCatalog.Current.LibraryWell);
        SectionTop(Find(deck,"CardPreviewPanel"),Stone);
        SectionTop(Find(deck,"DeckBackplate"),MainAccent);
        SectionTop(catalogue,LibraryAccent);
        ConfigureScrolling(deck, Stone, true);
        var mainTitle=Find(deck,"MainDeckCountText").GetComponent<Text>();
        var sideTitle=Find(deck,"SideboardCountText").GetComponent<Text>();
        MenuTypography.Use(mainTitle,MenuTypography.Face.Heading);
        MenuTypography.Use(sideTitle,MenuTypography.Face.Heading);
        mainTitle.color=MainAccent;
        sideTitle.color=SideAccent;
        var catalogueTitle = Find(deck, "CollectionCountText").GetComponent<Text>();
        MenuTypography.Use(catalogueTitle,MenuTypography.Face.Heading);
        catalogueTitle.color = LibraryAccent;
        catalogueTitle.alignment = TextAnchor.MiddleLeft;
        PreferenceText.SetSize(catalogueTitle, 26);

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

    private static void DeckSurface(Transform root, string name, Color color, Sprite artwork = null)
    {
        var image = Find(root, name).GetComponent<Image>();
        image.color = color;
        image.sprite = artwork;
        image.type = Image.Type.Simple;
        if (image.TryGetComponent<Outline>(out var border)) border.enabled = false;
    }

    internal static void StyleDeckScrollbar(ScrollRect scroll)
    {
        var bar = scroll.verticalScrollbar;
        var track = (RectTransform)bar.transform;
        track.anchorMin = new Vector2(1,0); track.anchorMax = Vector2.one;
        track.offsetMin = new Vector2(-20,5); track.offsetMax = new Vector2(0,-5);
        scroll.viewport.offsetMax = new Vector2(-20,scroll.viewport.offsetMax.y);
        StyleDeckScrollThumb(bar);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    internal static void StyleDeckScrollThumb(Scrollbar bar)
    {
        var track = (RectTransform)bar.transform;
        bar.GetComponent<Image>().color = Color.clear;
        var rail = Detail(track,"Rail",Etching).rectTransform;
        rail.SetAsFirstSibling();
        rail.anchorMin = new Vector2(.5f,0); rail.anchorMax = new Vector2(.5f,1);
        rail.sizeDelta = new Vector2(1,0); rail.anchoredPosition = Vector2.zero;
        var handle = bar.handleRect;
        handle.offsetMin = new Vector2(0,handle.offsetMin.y);
        handle.offsetMax = new Vector2(0,handle.offsetMax.y);
        handle.GetComponent<Image>().color = Color.clear;
        var grip = Detail(handle,"Thumb",Color.white);
        grip.rectTransform.anchorMin = new Vector2(.5f,0); grip.rectTransform.anchorMax = new Vector2(.5f,1);
        grip.rectTransform.sizeDelta = new Vector2(4,0); grip.rectTransform.anchoredPosition = Vector2.zero;
        bar.targetGraphic = grip;
        var colors = bar.colors; colors.normalColor = MenuThemeCatalog.Current.ScrollThumb;
        colors.highlightedColor = colors.selectedColor = Stone;
        colors.pressedColor = Silver; bar.colors = colors;
        grip.CrossFadeColor(colors.normalColor,0,true,true);
    }

    private static void Region(Transform root, string name, float left, float bottom, float right, float top)
    {
        var rect = Find(root, name) as RectTransform;
        if (rect == null) return;
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void ConfigureScrolling(Transform root, Color handleColor, bool deckWorkspace = false)
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
            var trackInset = new Vector2(isCardDetail ? -20 : -36, 10);
            if (scroll.verticalScrollbar != null)
            {
                var existingTrack = scroll.verticalScrollbar.GetComponent<UnityEngine.UI.Image>();
                if (existingTrack != null) existingTrack.color = Ink;
                if (scroll.verticalScrollbar.targetGraphic != null)
                    scroll.verticalScrollbar.targetGraphic.color = handleColor;
                if (deckWorkspace) StyleDeckScrollbar(scroll);
                continue;
            }
            var track = new GameObject("VerticalScrollbar", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Scrollbar));
            track.transform.SetParent(scroll.transform, false);
            var rect = (RectTransform)track.transform;
            rect.anchorMin = new Vector2(1, 0); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1, .5f);
            rect.offsetMin = trackInset; rect.offsetMax = new Vector2(-4, -10);
            track.GetComponent<UnityEngine.UI.Image>().color = Ink;
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            handle.transform.SetParent(track.transform, false);
            Stretch((RectTransform)handle.transform, 4);
            var image = handle.GetComponent<UnityEngine.UI.Image>(); image.color = handleColor;
            var bar = track.GetComponent<UnityEngine.UI.Scrollbar>();
            bar.handleRect = (RectTransform)handle.transform; bar.targetGraphic = image;
            bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = isCardDetail
                ? UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide
                : UnityEngine.UI.ScrollRect.ScrollbarVisibility.Permanent;
            if (deckWorkspace) StyleDeckScrollbar(scroll);
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
        Background(root, MenuThemeArtwork.Role.Hall, .62f);
        var content = Find(root, "Content");
        Find(content, "HeaderBlueRule").gameObject.SetActive(false);
        Find(content, "HeaderRedRule").gameObject.SetActive(false);
        Place(content, "HeaderBand", 0, 377, 1560, 2);
        Place(content, "BackButton", -714, 433, 132, 50);
        Place(content, "Title", -328, 433, 554, 76);
        Place(content, "ConnectionBadge", 655, 433, 248, 52);
        Text(content, "Title", "联机对战", 46);

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
        ApplyLobbyColors(root);

        ConfigureScrolling(Find(root,"SideboardPage"),Stone);
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
            if (name.EndsWith("Input") || name.EndsWith("Dropdown") || name == "DeckNotice"
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
