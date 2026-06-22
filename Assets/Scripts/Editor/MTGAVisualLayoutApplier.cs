using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MTGAVisualLayoutApplier
{
    private const string AutoApplySessionKey = "MTGB_MTGAVisualLayoutApplied_20260608";
    private const string LayoutRootName = "MTGAVisualReferenceLayout";

    [InitializeOnLoadMethod]
    private static void ScheduleAutoApply()
    {
        EditorApplication.delayCall += TryAutoApply;
    }

    [MenuItem("Tools/MTGB/Apply MTGA Visual Layout")]
    public static void ApplyFromMenu()
    {
        ApplyLayout(true);
    }

    private static void TryAutoApply()
    {
        if (SessionState.GetBool(AutoApplySessionKey, false)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoApply;
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;

        bool looksLikeArenaScene = GameObject.Find("ArenaWhitebox") != null
            || GameObject.Find("PlayerHandCard_1") != null
            || scene.path.EndsWith("Main.unity")
            || scene.path.EndsWith("MTGAPrototype.unity");

        if (!looksLikeArenaScene) return;

        SessionState.SetBool(AutoApplySessionKey, true);
        ApplyLayout(false);
    }

    private static void ApplyLayout(bool logResult)
    {
        ConfigureCamera();
        Transform root = RebuildReferenceRoot();
        BuildBoardGuides(root);
        LayoutBattlefield(root);
        LayoutPreviewUi();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (logResult)
        {
            Debug.Log("Applied MTGA visual layout: fanned player hand, compact opponent hand, battlefield rows, and preview framing.");
        }
    }

    private static Transform RebuildReferenceRoot()
    {
        GameObject existing = GameObject.Find(LayoutRootName);
        if (existing != null) Object.DestroyImmediate(existing);

        GameObject root = new GameObject(LayoutRootName);
        return root.transform;
    }

    private static void ConfigureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = GameObject.Find("Main Camera") ?? new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.GetComponent<Camera>() ?? cameraObject.AddComponent<Camera>();
            if (cameraObject.GetComponent<AudioListener>() == null) cameraObject.AddComponent<AudioListener>();
        }

        camera.orthographic = false;
        camera.fieldOfView = 36f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.018f, 0.026f, 0.045f);
        camera.transform.SetPositionAndRotation(new Vector3(0f, 11.40f, -5.70f), Quaternion.Euler(68f, 0f, 0f));
    }

    private static void BuildBoardGuides(Transform root)
    {
        Material baseStone = MakeMaterial("MTGA Stone Base", new Color(0.055f, 0.095f, 0.155f));
        Material playerZone = MakeMaterial("MTGA Player Zone", new Color(0.075f, 0.145f, 0.210f, 0.72f));
        Material opponentZone = MakeMaterial("MTGA Opponent Zone", new Color(0.070f, 0.120f, 0.190f, 0.64f));
        Material rowBand = MakeMaterial("MTGA Row Band", new Color(0.105f, 0.190f, 0.270f, 0.42f));
        Material boundary = MakeMaterial("MTGA Zone Boundary", new Color(0.18f, 0.40f, 0.54f, 0.55f));
        Cube(root, "MTGA_Table", new Vector3(0f, -0.12f, 0f), new Vector3(18.20f, 0.08f, 11.20f), Quaternion.identity, baseStone);

        Surface(root, "MTGA_OpponentFieldZone", new Vector3(0f, -0.045f, 1.82f), new Vector3(14.60f, 0.018f, 3.40f), opponentZone);
        Surface(root, "MTGA_PlayerFieldZone", new Vector3(0f, -0.043f, -1.82f), new Vector3(14.60f, 0.018f, 3.40f), playerZone);
        Surface(root, "MTGA_OpponentBackRowBand", new Vector3(0f, -0.030f, 2.48f), new Vector3(13.80f, 0.012f, 0.86f), rowBand);
        Surface(root, "MTGA_OpponentFrontRowBand", new Vector3(0f, -0.029f, 1.18f), new Vector3(13.80f, 0.012f, 0.94f), rowBand);
        Surface(root, "MTGA_PlayerFrontRowBand", new Vector3(0f, -0.028f, -1.18f), new Vector3(13.80f, 0.012f, 0.94f), rowBand);
        Surface(root, "MTGA_PlayerBackRowBand", new Vector3(0f, -0.027f, -2.48f), new Vector3(13.80f, 0.012f, 0.86f), rowBand);
        Surface(root, "MTGA_BattlefieldCenterBoundary", new Vector3(0f, -0.020f, 0f), new Vector3(14.20f, 0.012f, 0.035f), boundary);
    }

    private static void LayoutHands(Transform root)
    {
        Material face = LoadMaterial("Assets/Cards/Materials/Card3D_ArtPlaceholder.mat", new Color(0.70f, 0.74f, 0.68f));
        Material back = LoadMaterial("Assets/Cards/Materials/Card3D_DarkFrame.mat", new Color(0.06f, 0.05f, 0.10f));
        Material selected = MakeMaterial("MTGA Selected Cyan", new Color(0.00f, 0.90f, 1.00f, 0.75f));

        float[] playerX = { -2.52f, -1.68f, -0.84f, 0f, 0.84f, 1.68f, 2.52f };
        float[] playerZOffset = { 0.19f, 0.09f, 0.025f, 0f, 0.025f, 0.09f, 0.19f };
        float[] playerYaw = { -21f, -14f, -7f, 0f, 7f, 14f, 21f };
        for (int i = 0; i < playerX.Length; i++)
        {
            Vector3 pos = new Vector3(playerX[i], 0.34f + i * 0.006f, -2.96f + playerZOffset[i]);
            GameObject card = FindOrCreateCard(root, $"PlayerHandCard_{i + 1}", true, face, selected);
            PlaceCard(card, pos, new Vector3(0.78f, 0.040f, 1.08f), playerYaw[i]);
            SetCardTint(card, i == 4 ? selected : face, true);
        }

        float[] opponentX = { -2.28f, -1.52f, -0.76f, 0f, 0.76f, 1.52f, 2.28f };
        float[] opponentYaw = { -14f, -9f, -4f, 0f, 4f, 9f, 14f };
        for (int i = 0; i < opponentX.Length; i++)
        {
            GameObject card = FindOrCreateCard(root, $"OpponentHandCard_{i + 1}", false, back, selected);
            PlaceCard(card, new Vector3(opponentX[i], 0.24f + i * 0.004f, 2.88f - Mathf.Abs(i - 3) * 0.035f), new Vector3(0.48f, 0.034f, 0.68f), opponentYaw[i]);
            SetCardTint(card, back, false);
        }
    }

    private static void LayoutBattlefield(Transform root)
    {
        Material face = LoadMaterial("Assets/Cards/Materials/Card3D_ArtPlaceholder.mat", new Color(0.72f, 0.76f, 0.68f));
        Material glow = MakeHighlightMaterial("MTGA Battlefield Glow");

        string[] playerCards = { "PlayerBattlefieldCard_1", "PlayerBattlefieldCard_2", "PlayerBattlefieldCard_3", "PlayerBattlefieldCard_4", "PlayerCreatureCard", "PlayerBattlefieldCard_5", "PlayerBattlefieldCard_6" };
        Vector3[] playerPos = {
            new Vector3(-2.84f, 0.20f, -1.32f),
            new Vector3(-2.12f, 0.20f, -1.28f),
            new Vector3(-1.40f, 0.20f, -1.24f),
            new Vector3(-0.68f, 0.20f, -1.20f),
            new Vector3(0.05f, 0.26f, -0.42f),
            new Vector3(1.14f, 0.20f, -1.12f),
            new Vector3(1.86f, 0.20f, -1.19f)
        };

        for (int i = 0; i < playerCards.Length; i++)
        {
            GameObject card = FindOrCreateCard(root, playerCards[i], true, face, glow);
            Vector3 scale = playerCards[i] == "PlayerCreatureCard" ? new Vector3(0.70f, 0.044f, 0.92f) : new Vector3(0.62f, 0.036f, 0.86f);
            PlaceCard(card, playerPos[i], scale, Mathf.Lerp(-8f, 9f, i / (float)(playerCards.Length - 1)));
        }

        string[] opponentCards = { "OpponentBattlefieldCard_1", "OpponentBattlefieldCard_2", "OpponentBattlefieldCard_3", "OpponentBattlefieldCard_4", "OpponentBattlefieldCard_5", "OpponentCreatureCard", "OpponentSupportCard", "OpponentBattlefieldCard_6" };
        Vector3[] opponentPos = {
            new Vector3(-2.80f, 0.20f, 1.46f),
            new Vector3(-2.02f, 0.20f, 1.43f),
            new Vector3(-1.24f, 0.20f, 1.39f),
            new Vector3(-0.46f, 0.20f, 1.36f),
            new Vector3(0.36f, 0.20f, 1.28f),
            new Vector3(1.38f, 0.24f, 1.50f),
            new Vector3(2.18f, 0.22f, 1.42f),
            new Vector3(2.92f, 0.20f, 1.30f)
        };

        for (int i = 0; i < opponentCards.Length; i++)
        {
            GameObject card = FindOrCreateCard(root, opponentCards[i], true, face, glow);
            PlaceCard(card, opponentPos[i], new Vector3(0.60f, 0.036f, 0.84f), Mathf.Lerp(-7f, 8f, i / (float)(opponentCards.Length - 1)));
        }
    }

    private static void LayoutPiles(Transform root)
    {
        Material back = LoadMaterial("Assets/Cards/Materials/Card3D_DarkFrame.mat", new Color(0.06f, 0.05f, 0.10f));
        Material face = LoadMaterial("Assets/Cards/Materials/Card3D_ArtPlaceholder.mat", new Color(0.62f, 0.65f, 0.58f));

        PlacePile(root, "PlayerDeckPile", new Vector3(-4.12f, 0.20f, -2.30f), -8f, back, true);
        PlacePile(root, "PlayerGravePile", new Vector3(-3.42f, 0.18f, -2.23f), -9f, face, false);
        PlacePile(root, "OpponentDeckPile", new Vector3(3.78f, 0.20f, 2.18f), 7f, back, true);
        PlacePile(root, "OpponentGravePile", new Vector3(3.08f, 0.18f, 2.12f), 9f, face, false);
    }

    private static void LayoutPreviewUi()
    {
        RectTransform preview = FindRect("ArenaLargeCardPreview");
        if (preview != null)
        {
            preview.anchorMin = preview.anchorMax = new Vector2(1f, 0.5f);
            preview.pivot = new Vector2(1f, 0.5f);
            preview.anchoredPosition = new Vector2(-34f, 30f);
            preview.sizeDelta = new Vector2(360f, 520f);
            preview.localRotation = Quaternion.Euler(0f, 0f, -5f);
        }

        RectTransform endTurn = FindRect("ArenaEndTurnButton");
        if (endTurn != null)
        {
            endTurn.anchorMin = endTurn.anchorMax = new Vector2(1f, 0f);
            endTurn.pivot = new Vector2(1f, 0f);
            endTurn.anchoredPosition = new Vector2(-42f, 54f);
            endTurn.sizeDelta = new Vector2(240f, 68f);
        }

        RectTransform playerLife = FindRect("ArenaPlayerLife");
        if (playerLife != null) playerLife.anchoredPosition = new Vector2(0f, -390f);

        RectTransform opponentLife = FindRect("ArenaOpponentLife");
        if (opponentLife != null) opponentLife.anchoredPosition = new Vector2(0f, 374f);
    }

    private static void PlacePile(Transform root, string baseName, Vector3 position, float yaw, Material material, bool back)
    {
        GameObject existing = GameObject.Find(baseName);
        if (existing != null)
        {
            PlaceCard(existing, position, new Vector3(0.52f, 0.070f, 0.74f), yaw);
            SetCardTint(existing, material, !back);
            return;
        }

        for (int i = 0; i < 7; i++)
        {
            GameObject layer = CreatePlainCard(root, $"{baseName}_Layer_{i}", back ? material : material);
            PlaceCard(layer, position + new Vector3(i * 0.022f, i * 0.012f, -i * 0.018f), new Vector3(0.52f, 0.035f, 0.74f), yaw);
        }
    }

    private static GameObject FindOrCreateCard(Transform fallbackParent, string name, bool faceUp, Material material, Material glow)
    {
        GameObject card = GameObject.Find(name);
        if (card == null)
        {
            card = CreatePlainCard(fallbackParent, name, material);
        }

        EnsureCardDetails(card.transform, faceUp, material, glow);
        return card;
    }

    private static GameObject CreatePlainCard(Transform parent, string name, Material material)
    {
        GameObject card = GameObject.CreatePrimitive(PrimitiveType.Cube);
        card.name = name;
        card.transform.SetParent(parent, false);
        MeshRenderer renderer = card.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return card;
    }

    private static void EnsureCardDetails(Transform card, bool faceUp, Material material, Material glow)
    {
        MeshRenderer renderer = card.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sharedMaterial = material;

        if (faceUp)
        {
            EnsureChildCube(card, "MTGA_ArtWindow", new Vector3(0f, 0.535f, -0.06f), new Vector3(0.72f, 0.012f, 0.46f), MakeMaterial("MTGA Card Art Blue", new Color(0.05f, 0.22f, 0.38f)));
            EnsureChildCube(card, "MTGA_TitleBand", new Vector3(0f, 0.545f, -0.42f), new Vector3(0.88f, 0.012f, 0.10f), MakeMaterial("MTGA Card Title", new Color(0.86f, 0.82f, 0.70f)));
            EnsureChildCube(card, "MTGA_TypeBand", new Vector3(0f, 0.545f, 0.21f), new Vector3(0.88f, 0.012f, 0.08f), MakeMaterial("MTGA Card Type", new Color(0.78f, 0.76f, 0.68f)));
        }
        else
        {
            EnsureChildCube(card, "MTGA_BackCore", new Vector3(0f, 0.535f, 0f), new Vector3(0.70f, 0.012f, 0.78f), MakeMaterial("MTGA Card Back Core", new Color(0.18f, 0.10f, 0.06f)));
        }

        GameObject selection = EnsureChildCube(card, "MTGA_SelectionRim", new Vector3(0f, 0.565f, 0f), new Vector3(1.08f, 0.012f, 1.08f), glow);
        selection.SetActive(card.name == "PlayerHandCard_5");
    }

    private static GameObject EnsureChildCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        Transform child = parent.Find(name);
        GameObject go;
        if (child == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
        }
        else
        {
            go = child.gameObject;
        }

        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        return go;
    }

    private static void PlaceCard(GameObject card, Vector3 position, Vector3 scale, float yaw)
    {
        card.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        card.transform.localScale = scale;
    }

    private static void SetCardTint(GameObject card, Material material, bool faceUp)
    {
        MeshRenderer renderer = card.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sharedMaterial = material;

        Transform rim = card.transform.Find("MTGA_SelectionRim");
        if (rim != null) rim.gameObject.SetActive(card.name == "PlayerHandCard_5" && faceUp);
    }

    private static RectTransform FindRect(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<RectTransform>() : null;
    }

    private static GameObject Cube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = localScale;
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        return go;
    }

    private static GameObject Surface(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = Cube(parent, name, localPosition, localScale, Quaternion.identity, material);
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        return go;
    }

    private static Material LoadMaterial(string path, Color fallbackColor)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        return material != null ? material : MakeMaterial("MTGA Fallback", fallbackColor);
    }

    private static Material MakeMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = new Material(shader) { name = name };
        material.color = color;
        if (color.a < 0.99f)
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

    private static Material MakeHighlightMaterial(string name)
    {
        Shader shader = Shader.Find("MTGB/Arena Card Highlight");
        if (shader == null) return MakeMaterial(name, new Color(0.00f, 0.72f, 1.00f, 0.55f));

        Material material = new Material(shader) { name = name };
        material.SetColor("_Color", new Color(0.04f, 0.65f, 1.00f, 0.26f));
        material.SetColor("_EdgeColor", new Color(0.12f, 0.94f, 1.00f, 0.82f));
        material.SetFloat("_Width", 0.13f);
        material.SetFloat("_Intensity", 1.35f);
        material.SetFloat("_PulseSpeed", 2.15f);
        return material;
    }
}
