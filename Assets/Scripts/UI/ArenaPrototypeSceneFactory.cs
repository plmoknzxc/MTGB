using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ArenaPrototypeSceneFactory
{
    public static void Build(bool reuseExistingCamera)
    {
        Build(reuseExistingCamera, false);
    }

    public static void Build(bool reuseExistingCamera, bool replaceExisting)
    {
        if (replaceExisting) ClearGeneratedObjects();
        else if (GameObject.Find("ArenaWhitebox") != null) return;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.38f, 0.43f, 0.54f);

        Material boardBase = Material("Arena Obsidian", ObsidianArenaTheme.Void);
        Material playerZone = Material("Arena Player Zone", new Color(0.10f, 0.25f, 0.52f, 0.72f), true);
        Material opponentZone = Material("Arena Opponent Zone", new Color(0.34f, 0.07f, 0.12f, 0.64f), true);
        Material rowBand = Material("Arena Row Band", new Color(0.10f, 0.13f, 0.19f, 0.48f), true);
        Material boundary = Material("Arena Zone Boundary", new Color(0.26f, 0.36f, 0.53f, 0.60f), true);
        Material cardFace = Material("Card Face", new Color(0.74f, 0.78f, 0.72f, 1f));
        Material creatureFace = Material("Creature Face", new Color(0.08f, 0.43f, 0.62f, 1f));
        Material selectedGlow = HighlightMaterial("Selection Glow", new Color(0.12f, 0.94f, 1.00f, 0.82f));

        Camera camera = reuseExistingCamera ? Camera.main : null;
        if (camera == null) camera = CreateCamera();
        ConfigureCamera(camera);

        Transform arena = new GameObject("ArenaWhitebox").transform;
        CreateArenaTable(arena, boardBase, playerZone, opponentZone, rowBand, boundary);

        Transform playerArea = CreateDropArea(arena, "PlayerBattlefieldArea", new Vector3(0f, 0.08f, -1.62f), new Vector3(14.00f, 0.08f, 3.50f));
        Transform opponentArea = CreateDropArea(arena, "OpponentBattlefieldArea", new Vector3(0f, 0.08f, 1.62f), new Vector3(14.00f, 0.08f, 3.50f));

        CreateBattlefield(arena, cardFace, creatureFace, selectedGlow);

        GameObject luaSystem = new GameObject("CardScriptRuntimeManager");
        luaSystem.SetActive(false);
        luaSystem.AddComponent<CardScriptRuntimeManager>();

        GameObject systems = new GameObject("Systems");
        systems.SetActive(false);
        GameRules rules = systems.AddComponent<GameRules>();
        systems.AddComponent<CardManager>();
        ArenaWhiteboxInteraction interaction = systems.AddComponent<ArenaWhiteboxInteraction>();
        ArenaWhiteboxCardSync sync = systems.AddComponent<ArenaWhiteboxCardSync>();

        Canvas canvas = CreateCanvas();
        CreateEventSystem();
        BuildHud(canvas.transform, rules, interaction, camera, arena, playerArea, opponentArea);
        Assign(sync, "gameRules", rules);
        Assign(sync, "cardRoot", arena);

        luaSystem.SetActive(true);
        systems.SetActive(true);
    }

    private static void ClearGeneratedObjects()
    {
        DestroyIfExists("ArenaWhitebox");
        DestroyIfExists("Canvas");
        DestroyIfExists("EventSystem");
        DestroyIfExists("Systems");
        DestroyIfExists("CardScriptRuntimeManager");
        DestroyIfExists("Main Camera");
    }

    private static void DestroyIfExists(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing == null) return;

        if (Application.isPlaying) Object.Destroy(existing);
        else Object.DestroyImmediate(existing);
    }

    private static Camera CreateCamera()
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        Camera camera = go.AddComponent<Camera>();
        go.AddComponent<AudioListener>();
        return camera;
    }

    private static void ConfigureCamera(Camera camera)
    {
        camera.orthographic = false;
        camera.fieldOfView = 36f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = ObsidianArenaTheme.Void;
        camera.transform.SetPositionAndRotation(new Vector3(0f, 11.40f, -5.70f), Quaternion.Euler(68f, 0f, 0f));
    }

    private static void CreateArenaTable(
        Transform parent,
        Material boardBase,
        Material playerZone,
        Material opponentZone,
        Material rowBand,
        Material boundary)
    {
        Cube(parent, "ArenaBaseSlab", new Vector3(0f, -0.12f, 0f), new Vector3(18.20f, 0.10f, 11.20f), Quaternion.identity, boardBase);

        Surface(parent, "ArenaOpponentFieldZone", new Vector3(0f, -0.045f, 1.82f), new Vector3(14.60f, 0.018f, 3.40f), opponentZone);
        Surface(parent, "ArenaPlayerFieldZone", new Vector3(0f, -0.043f, -1.82f), new Vector3(14.60f, 0.018f, 3.40f), playerZone);
        Surface(parent, "ArenaOpponentBackRowBand", new Vector3(0f, -0.030f, 2.48f), new Vector3(13.80f, 0.012f, 0.86f), rowBand);
        Surface(parent, "ArenaOpponentFrontRowBand", new Vector3(0f, -0.029f, 1.18f), new Vector3(13.80f, 0.012f, 0.94f), rowBand);
        Surface(parent, "ArenaPlayerFrontRowBand", new Vector3(0f, -0.028f, -1.18f), new Vector3(13.80f, 0.012f, 0.94f), rowBand);
        Surface(parent, "ArenaPlayerBackRowBand", new Vector3(0f, -0.027f, -2.48f), new Vector3(13.80f, 0.012f, 0.86f), rowBand);
        Surface(parent, "ArenaBattlefieldCenterBoundary", new Vector3(0f, -0.020f, 0f), new Vector3(14.20f, 0.012f, 0.035f), boundary);
    }

    private static Transform CreateDropArea(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject area = Cube(parent, name, position, scale, Quaternion.identity, null);
        area.GetComponent<MeshRenderer>().enabled = false;
        return area.transform;
    }

    private static void CreateDeckPile(Transform parent, string name, Vector3 position, float yaw, Material material, float width)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 layerPosition = position + new Vector3(0.020f * i, 0.012f * i, -0.016f * i);
            Cube(parent, $"{name}_Layer_{i}", layerPosition, new Vector3(width, 0.034f, width * 1.42f), Quaternion.Euler(0f, yaw, 0f), material);
        }
    }

    private static void CreatePlayerHand(Transform parent, Material face, Material glow)
    {
        float[] xs = { -2.52f, -1.68f, -0.84f, 0f, 0.84f, 1.68f, 2.52f };
        float[] zOffsets = { 0.19f, 0.09f, 0.025f, 0f, 0.025f, 0.09f, 0.19f };
        float[] yaws = { -21f, -14f, -7f, 0f, 7f, 14f, 21f };

        for (int i = 0; i < xs.Length; i++)
        {
            Vector3 position = new Vector3(xs[i], 0.34f + i * 0.006f, -2.96f + zOffsets[i]);
            Vector3 scale = new Vector3(0.78f, 0.040f, 1.08f);
            GameObject card = CreateCard(parent, $"PlayerHandCard_{i + 1}", position, scale, yaws[i], face, glow, true);
            card.transform.localEulerAngles += new Vector3(0f, 0f, 0f);
        }
    }

    private static void CreateOpponentHand(Transform parent, Material back, Material glow)
    {
        float[] xs = { -2.28f, -1.52f, -0.76f, 0f, 0.76f, 1.52f, 2.28f };
        float[] yaws = { -14f, -9f, -4f, 0f, 4f, 9f, 14f };

        for (int i = 0; i < xs.Length; i++)
        {
            Vector3 position = new Vector3(xs[i], 0.24f + i * 0.004f, 2.88f - Mathf.Abs(i - 3) * 0.035f);
            CreateCard(parent, $"OpponentHandCard_{i + 1}", position, new Vector3(0.48f, 0.034f, 0.68f), yaws[i], back, glow, false);
        }
    }

    private static void CreateBattlefield(Transform parent, Material face, Material creatureFace, Material glow)
    {
        float[] playerLandX = { -2.84f, -2.12f, -1.40f, -0.68f };
        for (int i = 0; i < playerLandX.Length; i++)
        {
            Vector3 position = new Vector3(playerLandX[i], 0.20f + i * 0.004f, -1.32f + i * 0.04f);
            CreateCard(parent, $"PlayerBattlefieldCard_{i + 1}", position, new Vector3(0.62f, 0.036f, 0.86f), -8f + i * 4f, face, glow, true);
        }

        CreateCard(parent, "PlayerCreatureCard", new Vector3(0.05f, 0.26f, -0.42f), new Vector3(0.70f, 0.044f, 0.92f), 0f, creatureFace, glow, true);
        CreateCard(parent, "PlayerBattlefieldCard_5", new Vector3(1.14f, 0.20f, -1.12f), new Vector3(0.62f, 0.036f, 0.86f), 6f, face, glow, true);
        CreateCard(parent, "PlayerBattlefieldCard_6", new Vector3(1.86f, 0.20f, -1.19f), new Vector3(0.62f, 0.036f, 0.86f), 9f, face, glow, true);

        float[] opponentLandX = { -2.80f, -2.02f, -1.24f, -0.46f };
        for (int i = 0; i < opponentLandX.Length; i++)
        {
            Vector3 position = new Vector3(opponentLandX[i], 0.20f + i * 0.004f, 1.46f - i * 0.035f);
            CreateCard(parent, $"OpponentBattlefieldCard_{i + 1}", position, new Vector3(0.60f, 0.036f, 0.84f), -7f + i * 4f, face, glow, true);
        }

        CreateCard(parent, "OpponentCreatureCard", new Vector3(1.38f, 0.24f, 1.50f), new Vector3(0.64f, 0.040f, 0.86f), 4f, creatureFace, glow, true);
        CreateCard(parent, "OpponentSupportCard", new Vector3(2.18f, 0.22f, 1.42f), new Vector3(0.58f, 0.036f, 0.80f), 7f, face, glow, true);
        CreateCard(parent, "OpponentBattlefieldCard_5", new Vector3(0.36f, 0.20f, 1.28f), new Vector3(0.60f, 0.036f, 0.84f), 5f, face, glow, true);
        CreateCard(parent, "OpponentBattlefieldCard_6", new Vector3(2.92f, 0.20f, 1.30f), new Vector3(0.60f, 0.036f, 0.84f), 8f, face, glow, true);
    }

    private static GameObject CreateCard(Transform parent, string name, Vector3 position, Vector3 scale, float yaw, Material body, Material glow, bool faceUp)
    {
        GameObject card = Cube(parent, name, position, scale, Quaternion.Euler(0f, yaw, 0f), body);

        Material frame = Material($"{name} Frame", new Color(0.025f, 0.025f, 0.030f, 1f));
        Material title = Material($"{name} Title", new Color(0.80f, 0.78f, 0.68f, 1f));
        Material art = Material($"{name} Art", faceUp ? new Color(0.06f, 0.26f, 0.42f, 1f) : new Color(0.28f, 0.14f, 0.06f, 1f));
        Material text = Material($"{name} Text", new Color(0.70f, 0.72f, 0.66f, 1f));

        Cube(card.transform, "CardOuterFrame", new Vector3(0f, 0.530f, 0f), new Vector3(1.08f, 0.010f, 1.08f), Quaternion.identity, frame);
        Cube(card.transform, "HandArt", new Vector3(0f, 0.548f, -0.08f), new Vector3(0.78f, 0.012f, 0.44f), Quaternion.identity, art);
        Cube(card.transform, "HandTitle", new Vector3(0f, 0.552f, -0.42f), new Vector3(0.84f, 0.012f, 0.10f), Quaternion.identity, title);
        Cube(card.transform, "RulesBlock", new Vector3(0f, 0.552f, 0.28f), new Vector3(0.82f, 0.012f, 0.18f), Quaternion.identity, text);
        AddCardText(card.transform, "TitleText", new Vector3(0f, 0.568f, -0.42f), 0.075f, faceUp ? "Card" : "");
        AddCardText(card.transform, "PTText", new Vector3(0.29f, 0.568f, 0.40f), 0.080f, "");
        Cube(card.transform, "InteractionHoverGlow", Vector3.zero, new Vector3(1.06f, 0.036f, 1.06f), Quaternion.identity, glow).SetActive(false);
        Cube(card.transform, "InteractionSelectedGlow", Vector3.zero, new Vector3(1.12f, 0.038f, 1.12f), Quaternion.identity, glow).SetActive(false);

        if (!faceUp)
        {
            card.transform.Find("HandTitle")?.gameObject.SetActive(false);
            card.transform.Find("RulesBlock")?.gameObject.SetActive(false);
        }

        return card;
    }

    private static Canvas CreateCanvas()
    {
        GameObject go = new GameObject("Canvas");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void BuildHud(Transform canvas, GameRules rules, ArenaWhiteboxInteraction interaction, Camera camera, Transform arena, Transform playerArea, Transform opponentArea)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Text(canvas, "PlayerName", "Sapphire Schnee", new Vector2(210, 64), new Vector2(360, 54), 28, font, TextAnchor.MiddleLeft);
        Text(canvas, "OpponentName", "jack//", new Vector2(116, 1000), new Vector2(260, 48), 28, font, TextAnchor.MiddleLeft);
        Text playerLife = Text(canvas, "ArenaPlayerLife", "19", new Vector2(960, 172), new Vector2(116, 92), 46, font, TextAnchor.MiddleCenter);
        Text opponentLife = Text(canvas, "ArenaOpponentLife", "19", new Vector2(960, 855), new Vector2(110, 88), 42, font, TextAnchor.MiddleCenter);
        Text phase = Text(canvas, "ArenaPhaseStatusText", "P1 - Main1", new Vector2(960, 730), new Vector2(220, 34), 20, font, TextAnchor.MiddleCenter);
        Text stack = Text(canvas, "ArenaStackStatusText", "Stack: empty", new Vector2(960, 535), new Vector2(260, 34), 18, font, TextAnchor.MiddleCenter);
        Text hint = Text(canvas, "ArenaInteractionHintText", "\u62d6\u52a8\u624b\u724c\u5230\u5df1\u65b9\u6218\u573a\u6253\u51fa\u3002", new Vector2(960, 46), new Vector2(620, 36), 20, font, TextAnchor.MiddleCenter);
        Image stackGlow = Panel(canvas, "ArenaStackGlow", new Vector2(960, 540), new Vector2(560, 48), new Color(0.26f, 0.52f, 0.96f, 0.10f));

        Button resolve = Button(canvas, "ArenaResolveButton", "\u4e0b\u4e00\u6b65", new Vector2(1755, 106), new Vector2(260, 74), font, ObsidianArenaTheme.BlueDeep);
        Button endTurn = Button(canvas, "ArenaEndTurnButton", "\u7ed3\u675f\u56de\u5408", new Vector2(1765, 170), new Vector2(150, 46), font, ObsidianArenaTheme.RedDeep);
        Button(canvas, "AutoToggleButton", "Auto", new Vector2(1600, 126), new Vector2(70, 34), font, ObsidianArenaTheme.Surface);
        Button(canvas, "InfoButton", "i", new Vector2(1600, 82), new Vector2(42, 42), font, ObsidianArenaTheme.Surface);

        GameObject preview = LargePreview(canvas, font);

        Assign(interaction, "mainCamera", camera);
        Assign(interaction, "cardRoot", arena);
        Assign(interaction, "largeCardPreview", preview);
        Assign(interaction, "resolveButton", resolve);
        Assign(interaction, "endTurnButton", endTurn);
        Assign(interaction, "interactionHintText", hint);
        Assign(interaction, "stackGlowImage", stackGlow);
        Assign(interaction, "gameRules", rules);
        Assign(interaction, "playerLifeText", playerLife);
        Assign(interaction, "opponentLifeText", opponentLife);
        Assign(interaction, "phaseStatusText", phase);
        Assign(interaction, "stackStatusText", stack);
        Assign(interaction, "playerBattlefieldArea", playerArea);
        Assign(interaction, "opponentBattlefieldArea", opponentArea);
    }

    private static GameObject LargePreview(Transform parent, Font font)
    {
        GameObject panel = Panel(parent, "ArenaLargeCardPreview", new Vector2(1695, 565), new Vector2(360, 520), ObsidianArenaTheme.Surface).gameObject;
        Text(panel.transform, "TitleBand/Text", "\u5361\u724c\u9884\u89c8", new Vector2(0, 222), new Vector2(312, 38), 22, font, TextAnchor.MiddleLeft);
        Panel(panel.transform, "ArtBlock", new Vector2(0, 84), new Vector2(312, 244), new Color(0.08f, 0.26f, 0.42f, 1f));
        Text(panel.transform, "RulesBlock/Text", "Rules text", new Vector2(0, -132), new Vector2(300, 140), 18, font, TextAnchor.UpperLeft);
        Text(panel.transform, "PreviewPTBox/Text", "2/2", new Vector2(116, -226), new Vector2(76, 36), 20, font, TextAnchor.MiddleCenter);
        return panel;
    }

    private static void CreateEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    private static GameObject Cube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = localScale;
        if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static GameObject Surface(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = Cube(parent, name, localPosition, localScale, Quaternion.identity, material);
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        return go;
    }

    private static GameObject Disc(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static void AddCardText(Transform parent, string name, Vector3 localPosition, float size, string value)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        TextMesh mesh = go.AddComponent<TextMesh>();
        mesh.text = value;
        mesh.fontSize = 32;
        mesh.characterSize = size;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = Color.black;
    }

    private static Text Text(Transform parent, string path, string value, Vector2 anchored, Vector2 size, int fontSize, Font font, TextAnchor align)
    {
        string[] parts = path.Split('/');
        Transform current = parent;
        for (int i = 0; i < parts.Length; i++)
        {
            Transform child = current.Find(parts[i]);
            if (child == null)
            {
                child = new GameObject(parts[i], typeof(RectTransform)).transform;
                child.SetParent(current, false);
            }

            current = child;
        }

        Text text = current.gameObject.GetComponent<Text>() ?? current.gameObject.AddComponent<Text>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = Color.white;
        RectTransform rect = current.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchored;
        rect.sizeDelta = size;
        return text;
    }

    private static Image Panel(Transform parent, string name, Vector2 anchored, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchored;
        rect.sizeDelta = size;
        return image;
    }

    private static Button Button(Transform parent, string name, string label, Vector2 anchored, Vector2 size, Font font, Color color)
    {
        Image image = Panel(parent, name, anchored, size, color);
        Button button = image.gameObject.AddComponent<Button>();
        Text(image.transform, "Text", label, Vector2.zero, size, 30, font, TextAnchor.MiddleCenter);
        return button;
    }

    private static Material Material(string name, Color color, bool transparent = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = name };
        material.color = color;
        if (transparent)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        return material;
    }

    private static Material HighlightMaterial(string name, Color edgeColor)
    {
        Shader shader = Shader.Find("MTGB/Arena Card Highlight");
        if (shader == null) return Material(name, edgeColor, true);

        Material material = new Material(shader) { name = name };
        material.SetColor("_Color", new Color(0.04f, 0.65f, 1.00f, 0.26f));
        material.SetColor("_EdgeColor", edgeColor);
        material.SetFloat("_Width", 0.13f);
        material.SetFloat("_Intensity", 1.35f);
        material.SetFloat("_PulseSpeed", 2.15f);
        return material;
    }

    private static void Assign(Object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }
}
