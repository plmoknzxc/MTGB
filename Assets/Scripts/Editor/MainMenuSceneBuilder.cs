using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Pipeline.Commands;

public static class MainMenuSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("Tools/MTGB/Build Main Menu Scene")]
    public static void BuildScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildSceneForBatch();
    }

    [CliCommand("mtgb_rebuild_obsidian_ui", "Rebuild the MTGB main scene with the Obsidian Arena UI")]
    public static void RebuildObsidianUiFromCli()
    {
        BuildSceneForBatch();
    }

    public static void BuildSceneForBatch()
    {
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateSceneBasics();

        Canvas canvas = CreateCanvas();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject startScreen = CreateScreenRoot(canvas.transform, "StartScreen", true);
        GameObject deckBuilderScreen = CreateScreenRoot(canvas.transform, "DeckBuilderScreen", false);

        StretchToParent(Panel(startScreen.transform, "StartBackground", Vector2.zero, Vector2.zero,
            ObsidianArenaTheme.Void).rectTransform);
        Panel(startScreen.transform, "StartLeftPanel", Vector2.zero, new Vector2(900f, 1080f),
            ObsidianArenaTheme.NavyField);
        Panel(startScreen.transform, "StartRightField", new Vector2(900f, 0f), new Vector2(1020f, 1080f),
            ObsidianArenaTheme.RedField);
        Panel(startScreen.transform, "StartCenterDividerBlue", new Vector2(896f, 0f), new Vector2(3f, 1080f),
            ObsidianArenaTheme.Blue);
        Panel(startScreen.transform, "StartCenterDividerRed", new Vector2(902f, 0f), new Vector2(2f, 1080f),
            ObsidianArenaTheme.Red);

        GameObject logoSlot = RectContainer(startScreen.transform, "StartLogoSlot", new Vector2(110f, -248f),
            new Vector2(690f, 340f));
        Text logo = Text(logoSlot.transform, "StartTitle", "MTG<color=#4285F4>B</color>", Vector2.zero,
            new Vector2(690f, 214f), 184, font, TextAnchor.MiddleLeft, ObsidianArenaTheme.Text);
        logo.fontStyle = FontStyle.Bold;
        logo.supportRichText = true;
        Text subtitle = Text(logoSlot.transform, "StartSubtitle", "B A T T L E G R O U N D", new Vector2(4f, -218f),
            new Vector2(680f, 62f), 39, font, TextAnchor.MiddleLeft, ObsidianArenaTheme.MutedText);
        subtitle.fontStyle = FontStyle.Bold;

        Panel(startScreen.transform, "StartButtonPanel", new Vector2(1160f, -238f), new Vector2(660f, 520f),
            new Color(ObsidianArenaTheme.Surface.r, ObsidianArenaTheme.Surface.g, ObsidianArenaTheme.Surface.b, 0.38f));
        Button onlineMatchButton = MenuButton(startScreen.transform, "OnlineMatchButton", "联机对战", "ENTER BATTLEGROUND",
            new Vector2(1210f, -282f), new Vector2(590f, 132f), font, ObsidianArenaTheme.BlueDeep, ObsidianArenaTheme.Blue);
        Button deckEditorButton = MenuButton(startScreen.transform, "DeckEditorButton", "卡组编辑", "CONFIGURE DECK",
            new Vector2(1210f, -430f), new Vector2(590f, 112f), font, ObsidianArenaTheme.Surface, ObsidianArenaTheme.Blue);
        Button settingsButton = MenuButton(startScreen.transform, "SettingsButton", "游戏设置", "SYSTEM",
            new Vector2(1210f, -558f), new Vector2(590f, 112f), font, ObsidianArenaTheme.Surface, ObsidianArenaTheme.Red);

        StretchToParent(Panel(
            deckBuilderScreen.transform,
            "Background",
            Vector2.zero,
            Vector2.zero,
            ObsidianArenaTheme.Void).rectTransform);
        Panel(deckBuilderScreen.transform, "TopControlBand", new Vector2(424f, -24f), new Vector2(1472f, 76f), ObsidianArenaTheme.SurfaceRaised);
        Panel(deckBuilderScreen.transform, "TopBlueRule", new Vector2(424f, -22f), new Vector2(1024f, 3f), ObsidianArenaTheme.Blue);
        Panel(deckBuilderScreen.transform, "TopRedRule", new Vector2(1448f, -22f), new Vector2(448f, 3f), ObsidianArenaTheme.Red);
        Panel(deckBuilderScreen.transform, "CollectionBackplate", new Vector2(424f, -112f), new Vector2(1472f, 404f), ObsidianArenaTheme.Surface);
        Panel(deckBuilderScreen.transform, "DeckBackplate", new Vector2(424f, -550f), new Vector2(1472f, 458f), ObsidianArenaTheme.Surface);

        Dropdown deckDropdown = Dropdown(deckBuilderScreen.transform, "DeckDropdown", new Vector2(448f, -38f), new Vector2(300f, 44f), font);
        InputField deckNameInput = Input(deckBuilderScreen.transform, "DeckNameInput", new Vector2(766f, -38f), new Vector2(360f, 44f), font, "Deck name");
        Button newDeckButton = Button(deckBuilderScreen.transform, "NewDeckButton", "新建", new Vector2(1144f, -38f), new Vector2(92f, 44f), font, ObsidianArenaTheme.SurfaceRaised);
        Button saveDeckButton = Button(deckBuilderScreen.transform, "SaveDeckButton", "保存", new Vector2(1248f, -38f), new Vector2(92f, 44f), font, ObsidianArenaTheme.BlueDeep);
        Button reloadButton = Button(deckBuilderScreen.transform, "ReloadButton", "重载", new Vector2(1352f, -38f), new Vector2(116f, 44f), font, ObsidianArenaTheme.SurfaceRaised);
        Button advancedSearchButton = Button(deckBuilderScreen.transform, "AdvancedSearchButton", "高级搜索", new Vector2(1648f, -34f), new Vector2(224f, 52f), font, ObsidianArenaTheme.BlueDeep);
        Button backToTitleButton = Button(deckBuilderScreen.transform, "BackToTitleButton", "返回", new Vector2(24f, -24f), new Vector2(92f, 44f), font, ObsidianArenaTheme.SurfaceRaised);

        Text collectionCountText = Text(deckBuilderScreen.transform, "CollectionCountText", "Collection", new Vector2(448f, -124f), new Vector2(420f, 34f), 25, font, TextAnchor.MiddleLeft, Color.white);
        RectTransform collectionContent = ScrollGrid(deckBuilderScreen.transform, "CollectionScroll", "CollectionContent", new Vector2(448f, -166f), new Vector2(1424f, 330f), new Vector2(126f, 176f), new Vector2(14f, 16f));

        Text mainDeckCountText = Text(deckBuilderScreen.transform, "MainDeckCountText", "Main Deck", new Vector2(448f, -566f), new Vector2(420f, 34f), 25, font, TextAnchor.MiddleLeft, Color.white);
        RectTransform mainDeckContent = ScrollGrid(deckBuilderScreen.transform, "MainDeckScroll", "MainDeckContent", new Vector2(448f, -608f), new Vector2(930f, 382f), new Vector2(112f, 157f), new Vector2(12f, 14f));

        Text sideboardCountText = Text(deckBuilderScreen.transform, "SideboardCountText", "Sideboard", new Vector2(1410f, -566f), new Vector2(420f, 34f), 25, font, TextAnchor.MiddleLeft, Color.white);
        RectTransform sideboardContent = ScrollGrid(deckBuilderScreen.transform, "SideboardScroll", "SideboardContent", new Vector2(1410f, -608f), new Vector2(462f, 382f), new Vector2(98f, 137f), new Vector2(10f, 12f));

        PreviewRefs preview = PreviewPanel(deckBuilderScreen.transform, font);
        Text cardInfoText = preview.Text;

        CreateEventSystem();

        GameObject systems = new GameObject("MainMenuSystems");
        MainMenuController controller = systems.AddComponent<MainMenuController>();
        Assign(controller, "startScreenGroup", startScreen.GetComponent<CanvasGroup>());
        Assign(controller, "deckBuilderGroup", deckBuilderScreen.GetComponent<CanvasGroup>());
        Assign(controller, "onlineMatchButton", onlineMatchButton);
        Assign(controller, "deckEditorButton", deckEditorButton);
        Assign(controller, "settingsButton", settingsButton);
        Assign(controller, "backToTitleButton", backToTitleButton);
        Assign(controller, "advancedSearchButton", advancedSearchButton);
        Assign(controller, "deckDropdown", deckDropdown);
        Assign(controller, "deckNameInput", deckNameInput);
        Assign(controller, "collectionCountText", collectionCountText);
        Assign(controller, "mainDeckCountText", mainDeckCountText);
        Assign(controller, "sideboardCountText", sideboardCountText);
        Assign(controller, "cardInfoText", cardInfoText);
        Assign(controller, "newDeckButton", newDeckButton);
        Assign(controller, "saveDeckButton", saveDeckButton);
        Assign(controller, "reloadButton", reloadButton);
        Assign(controller, "collectionContent", collectionContent);
        Assign(controller, "mainDeckContent", mainDeckContent);
        Assign(controller, "sideboardContent", sideboardContent);
        Assign(controller, "cardPreviewGroup", preview.Group);
        Assign(controller, "cardPreviewImage", preview.Image);
        Assign(controller, "cardPreviewText", preview.Text);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();
        Debug.Log($"MTGB main menu scene generated: {ScenePath}");
    }

    private static void AddSceneToBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(scene => scene.path == ScenePath)) return;

        EditorBuildSettings.scenes = scenes
            .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) })
            .ToArray();
    }

    private static GameObject CreateScreenRoot(Transform parent, string name, bool visible)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        CanvasGroup group = go.GetComponent<CanvasGroup>();
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
        go.SetActive(visible);
        return go;
    }

    private static void Assign(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null) property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateSceneBasics()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = ObsidianArenaTheme.Void;
        cameraObject.AddComponent<AudioListener>();

        GameObject lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
    }

    private static Canvas CreateCanvas()
    {
        GameObject go = new GameObject("MainMenuCanvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    private static RectTransform ScrollGrid(Transform parent, string name, string contentName, Vector2 anchoredPosition, Vector2 size, Vector2 cellSize, Vector2 spacing)
    {
        Image shell = Panel(parent, name, anchoredPosition, size, ObsidianArenaTheme.SurfaceRaised);
        ObsidianArenaTheme.EnsureOutline(shell, ObsidianArenaTheme.LineSoft, new Vector2(1f, -1f));
        RectTransform shellRect = shell.rectTransform;
        ScrollRect scroll = shell.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(shell.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(10f, 10f);
        viewportRect.offsetMax = new Vector2(-10f, -10f);
        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        viewportImage.raycastTarget = true;

        GameObject content = new GameObject(contentName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = cellSize;
        grid.spacing = spacing;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, Mathf.FloorToInt((size.x - 28f) / (cellSize.x + spacing.x)));

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        _ = shellRect;
        return contentRect;
    }

    private static PreviewRefs PreviewPanel(Transform parent, Font font)
    {
        Image panel = Panel(parent, "CardPreviewPanel", new Vector2(24f, -80f), new Vector2(382f, 928f), ObsidianArenaTheme.Surface);
        ObsidianArenaTheme.EnsureOutline(panel, ObsidianArenaTheme.LineSoft, new Vector2(1f, -1f));
        CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;

        Image cardImage = AddImage(panel.transform, "CardPreviewImage", Color.white);
        RectTransform imageRect = cardImage.rectTransform;
        imageRect.anchorMin = new Vector2(0.5f, 1f);
        imageRect.anchorMax = new Vector2(0.5f, 1f);
        imageRect.pivot = new Vector2(0.5f, 1f);
        imageRect.anchoredPosition = new Vector2(0f, -24f);
        imageRect.sizeDelta = new Vector2(334f, 468f);

        Text previewText = ScrollText(panel.transform, "CardInfoScroll", "CardInfo", new Vector2(24f, -520f), new Vector2(334f, 430f), font, 16);

        return new PreviewRefs(group, cardImage, previewText);
    }

    private static Text ScrollText(Transform parent, string name, string contentTextName, Vector2 anchoredPosition, Vector2 size, Font font, int fontSize)
    {
        Image shell = Panel(parent, name, anchoredPosition, size, ObsidianArenaTheme.Field);
        ScrollRect scroll = shell.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(shell.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(12f, 12f);
        viewportRect.offsetMax = new Vector2(-12f, -12f);
        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        viewportImage.raycastTarget = true;

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 760f);

        Text text = Text(content.transform, contentTextName, string.Empty, Vector2.zero, new Vector2(size.x - 24f, 760f), fontSize, font, TextAnchor.UpperLeft, ObsidianArenaTheme.Text);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0f, 1f);
        text.rectTransform.pivot = new Vector2(0f, 1f);
        text.rectTransform.anchoredPosition = Vector2.zero;
        text.rectTransform.sizeDelta = new Vector2(size.x - 24f, 760f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        return text;
    }

    private static Image Panel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static GameObject RectContainer(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return go;
    }

    private static void StretchToParent(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static Image AddImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text Text(Transform parent, string name, string value, Vector2 anchoredPosition, Vector2 size, int fontSize, Font font, TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static InputField Input(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Font font, string placeholder)
    {
        Image background = Panel(parent, name, anchoredPosition, size, ObsidianArenaTheme.Field);
        InputField input = background.gameObject.AddComponent<InputField>();

        Text text = Text(background.transform, "Text", string.Empty, new Vector2(12f, -4f), new Vector2(size.x - 24f, size.y - 8f), 20, font, TextAnchor.MiddleLeft, Color.white);
        Text placeholderText = Text(background.transform, "Placeholder", placeholder, new Vector2(12f, -4f), new Vector2(size.x - 24f, size.y - 8f), 20, font, TextAnchor.MiddleLeft, ObsidianArenaTheme.MutedText);

        input.textComponent = text;
        input.placeholder = placeholderText;
        input.targetGraphic = background;
        ObsidianArenaTheme.StyleField(input);
        return input;
    }

    private static Dropdown Dropdown(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Font font)
    {
        Image background = Panel(parent, name, anchoredPosition, size, ObsidianArenaTheme.Field);
        Dropdown dropdown = background.gameObject.AddComponent<Dropdown>();
        Text label = Text(background.transform, "Label", string.Empty, new Vector2(12f, -4f), new Vector2(size.x - 46f, size.y - 8f), 20, font, TextAnchor.MiddleLeft, Color.white);
        Text(background.transform, "Arrow", "v", new Vector2(size.x - 32f, -4f), new Vector2(24f, size.y - 8f), 20, font, TextAnchor.MiddleCenter, ObsidianArenaTheme.Blue);

        Image templateImage = Panel(background.transform, "Template", new Vector2(0f, -size.y - 2f), new Vector2(size.x, 280f), ObsidianArenaTheme.SurfaceRaised);
        ScrollRect scroll = templateImage.gameObject.AddComponent<ScrollRect>();
        templateImage.gameObject.SetActive(false);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(templateImage.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = Color.clear;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 30f);

        Toggle item = CreateDropdownItem(content.transform, font);
        dropdown.targetGraphic = background;
        dropdown.captionText = label;
        dropdown.itemText = item.GetComponentInChildren<Text>();
        dropdown.template = templateImage.rectTransform;
        ObsidianArenaTheme.StyleField(dropdown);
        scroll.content = contentRect;
        scroll.viewport = viewportRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        return dropdown;
    }

    private static Toggle CreateDropdownItem(Transform parent, Font font)
    {
        GameObject itemObject = new GameObject("Item", typeof(RectTransform), typeof(Toggle), typeof(Image));
        itemObject.transform.SetParent(parent, false);
        RectTransform itemRect = itemObject.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 1f);
        itemRect.anchorMax = new Vector2(1f, 1f);
        itemRect.pivot = new Vector2(0.5f, 1f);
        itemRect.sizeDelta = new Vector2(0f, 30f);
        itemObject.GetComponent<Image>().color = ObsidianArenaTheme.SurfaceRaised;

        Text(itemObject.transform, "Item Label", "Option", new Vector2(10f, 0f), new Vector2(260f, 30f), 18, font, TextAnchor.MiddleLeft, Color.white);
        Toggle toggle = itemObject.GetComponent<Toggle>();
        toggle.targetGraphic = itemObject.GetComponent<Image>();
        return toggle;
    }

    private static Button Button(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, Font font, Color color)
    {
        Image background = Panel(parent, name, anchoredPosition, size, color);
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ObsidianArenaTheme.StyleButton(button, color);

        Text text = Text(background.transform, "Text", label, Vector2.zero, size, Mathf.RoundToInt(Mathf.Min(24f, size.y * 0.50f)), font, TextAnchor.MiddleCenter, Color.white);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static Button MenuButton(Transform parent, string name, string label, string meta, Vector2 position,
        Vector2 size, Font font, Color color, Color accent)
    {
        Button button = Button(parent, name, label, position, size, font, color);
        Text labelText = button.GetComponentInChildren<Text>();
        labelText.alignment = TextAnchor.MiddleLeft;
        labelText.fontSize = 31;
        labelText.fontStyle = FontStyle.Bold;
        labelText.rectTransform.offsetMin = new Vector2(42f, 0f);
        labelText.rectTransform.offsetMax = new Vector2(-250f, 0f);

        Text metaText = Text(button.transform, "Meta", meta, Vector2.zero, new Vector2(232f, size.y),
            15, font, TextAnchor.MiddleRight, new Color(ObsidianArenaTheme.Text.r, ObsidianArenaTheme.Text.g, ObsidianArenaTheme.Text.b, 0.72f));
        metaText.fontStyle = FontStyle.Bold;
        metaText.rectTransform.anchorMin = new Vector2(1f, 0f);
        metaText.rectTransform.anchorMax = Vector2.one;
        metaText.rectTransform.pivot = new Vector2(1f, 0.5f);
        metaText.rectTransform.anchoredPosition = new Vector2(-30f, 0f);
        metaText.rectTransform.sizeDelta = new Vector2(232f, 0f);

        Image rule = Panel(button.transform, "AccentRule", Vector2.zero, new Vector2(8f, size.y), accent);
        rule.rectTransform.anchorMin = new Vector2(0f, 0f);
        rule.rectTransform.anchorMax = new Vector2(0f, 1f);
        rule.rectTransform.pivot = new Vector2(0f, 1f);
        rule.rectTransform.anchoredPosition = Vector2.zero;
        rule.rectTransform.sizeDelta = new Vector2(8f, 0f);
        rule.raycastTarget = false;
        return button;
    }

    private readonly struct PreviewRefs
    {
        public readonly CanvasGroup Group;
        public readonly Image Image;
        public readonly Text Text;

        public PreviewRefs(CanvasGroup group, Image image, Text text)
        {
            Group = group;
            Image = image;
            Text = text;
        }
    }
}
