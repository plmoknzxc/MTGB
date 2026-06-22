using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MTGAHandUiConverter
{
    private const string PlayerHandRootName = "MTGAPlayerHandUI";
    private const string OpponentHandRootName = "MTGAOpponentHandUI";

    [MenuItem("Tools/MTGB/Convert Player Hand To UI")]
    public static void ConvertPlayerHandToUi()
    {
        ConvertBothHandsToUi();
    }

    [MenuItem("Tools/MTGB/Convert Both Hands To UI")]
    public static void ConvertBothHandsToUi()
    {
        CleanupDuplicateSystems();
        DeleteSceneHandCards();

        Canvas canvas = EnsureCanvas();
        DeleteCanvasRoot(canvas, PlayerHandRootName);
        DeleteCanvasRoot(canvas, OpponentHandRootName);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildPlayerHand(canvas.transform, font);
        BuildOpponentHand(canvas.transform, font);
        EnsureGameplaySystems();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Converted both player and opponent hands to screen-space UI cards, removed the original 3D hand cards, and ensured gameplay scripts are present.");
    }

    [MenuItem("Tools/MTGB/Cleanup Duplicate Systems")]
    public static void CleanupDuplicateSystems()
    {
        GameObject[] systemsObjects = FindAllSystemsObjects();
        if (systemsObjects.Length <= 1) return;

        GameObject keep = null;
        foreach (GameObject systems in systemsObjects)
        {
            if (systems.GetComponent<MTGAHandUiController>() != null)
            {
                keep = systems;
                break;
            }
        }

        if (keep == null)
        {
            foreach (GameObject systems in systemsObjects)
            {
                if (systems.activeSelf)
                {
                    keep = systems;
                    break;
                }
            }
        }

        if (keep == null) keep = systemsObjects[0];

        foreach (GameObject systems in systemsObjects)
        {
            if (systems != keep)
            {
                Object.DestroyImmediate(systems);
            }
        }

        keep.name = "Systems";
        keep.SetActive(true);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static void DeleteSceneHandCards()
    {
        GameObject arena = GameObject.Find("ArenaWhitebox");
        if (arena == null) return;

        List<GameObject> toDelete = new List<GameObject>();
        foreach (Transform child in arena.transform)
        {
            if (child.name.StartsWith("PlayerHandCard_") || child.name.StartsWith("OpponentHandCard_"))
            {
                toDelete.Add(child.gameObject);
            }
        }

        foreach (GameObject go in toDelete)
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void DeleteCanvasRoot(Canvas canvas, string rootName)
    {
        Transform existing = canvas.transform.Find(rootName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
    }

    private static void BuildPlayerHand(Transform canvas, Font font)
    {
        RectTransform root = CreateRect(canvas, PlayerHandRootName);
        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = new Vector2(0f, 292f);

        float[] xs = { -396f, -264f, -132f, 0f, 132f, 264f, 396f };
        float[] ys = { -12f, 0f, 14f, 26f, 14f, 0f, -12f };
        float[] rotations = { 11f, 7f, 3f, 0f, -3f, -7f, -11f };

        for (int i = 0; i < xs.Length; i++)
        {
            RectTransform card = CreateUiCard(root, $"PlayerHandCard_{i + 1}", font, 1, i, true, new Vector2(194f, 274f), i == 4);
            card.anchoredPosition = new Vector2(xs[i], ys[i]);
            card.localRotation = Quaternion.Euler(0f, 0f, rotations[i]);
            card.SetAsLastSibling();
        }
    }

    private static void BuildOpponentHand(Transform canvas, Font font)
    {
        RectTransform root = CreateRect(canvas, OpponentHandRootName);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = new Vector2(0f, 86f);

        float[] xs = { -210f, -140f, -70f, 0f, 70f, 140f, 210f };
        float[] ys = { 71f, 73f, 75f, 76f, 75f, 73f, 71f };
        float[] rotations = { -4.95f, -3.3f, -1.65f, 0f, 1.65f, 3.3f, 4.95f };

        for (int i = 0; i < xs.Length; i++)
        {
            RectTransform card = CreateUiCard(root, $"OpponentHandCard_{i + 1}", font, 2, i, false, new Vector2(74f, 104f), false);
            card.anchoredPosition = new Vector2(xs[i], ys[i]);
            card.localRotation = Quaternion.Euler(0f, 0f, rotations[i]);
            card.SetAsLastSibling();
        }
    }

    private static void EnsureGameplaySystems()
    {
        CleanupDuplicateSystems();

        GameObject systems = FindSystemsObject();
        if (systems == null) systems = new GameObject("Systems");

        if (systems.GetComponent<GameRules>() == null) systems.AddComponent<GameRules>();
        if (systems.GetComponent<CardManager>() == null) systems.AddComponent<CardManager>();
        if (systems.GetComponent<ArenaWhiteboxInteraction>() == null) systems.AddComponent<ArenaWhiteboxInteraction>();
        if (systems.GetComponent<ArenaWhiteboxCardSync>() == null) systems.AddComponent<ArenaWhiteboxCardSync>();
        if (systems.GetComponent<MTGAHandUiController>() == null) systems.AddComponent<MTGAHandUiController>();

        systems.SetActive(true);

        if (GameObject.Find("EventSystem") == null && Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }

    private static GameObject FindSystemsObject()
    {
        GameObject[] systemsObjects = FindAllSystemsObjects();
        if (systemsObjects.Length == 0) return null;

        foreach (GameObject systems in systemsObjects)
        {
            if (systems.GetComponent<MTGAHandUiController>() != null) return systems;
        }

        foreach (GameObject systems in systemsObjects)
        {
            if (systems.activeSelf) return systems;
        }

        return systemsObjects[0];
    }

    private static GameObject[] FindAllSystemsObjects()
    {
        List<GameObject> results = new List<GameObject>();
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in allObjects)
        {
            if (go.name != "Systems") continue;
            if (!go.scene.IsValid()) continue;
            results.Add(go);
        }

        return results.ToArray();
    }

    private static Canvas EnsureCanvas()
    {
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject == null)
        {
            canvasObject = new GameObject("Canvas");
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        return canvas;
    }

    private static RectTransform CreateUiCard(Transform parent, string name, Font font, int owner, int index, bool faceUp, Vector2 size, bool selected)
    {
        RectTransform card = CreateRect(parent, name);
        card.sizeDelta = size;

        MTGAHandUiCard handCard = card.gameObject.AddComponent<MTGAHandUiCard>();
        handCard.Configure(owner, index);

        Image glow = AddImage(card, "SelectedGlow", new Color(0.0f, 0.88f, 1.0f, selected ? 0.035f : 0f));
        RectTransform glowRect = glow.rectTransform;
        glowRect.anchorMin = new Vector2(0.5f, 0.5f);
        glowRect.anchorMax = new Vector2(0.5f, 0.5f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);
        glowRect.anchoredPosition = Vector2.zero;
        glowRect.sizeDelta = size + new Vector2(8f, 8f);

        Image frame = card.gameObject.AddComponent<Image>();
        frame.color = new Color(0.025f, 0.026f, 0.030f, 1f);
        Outline outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = faceUp ? Color.clear : new Color(0f, 0f, 0f, 0.38f);
        outline.effectDistance = new Vector2(1f, 1f);

        Image face = AddImage(card, "Face", faceUp ? GetCardFaceColor(index) : new Color(0.12f, 0.07f, 0.04f, 1f));
        Stretch(face.rectTransform, 9f, 9f, 9f, 9f);

        Image title = AddImage(card, "TitleBand", faceUp ? new Color(0.82f, 0.80f, 0.70f, 1f) : new Color(0.24f, 0.14f, 0.08f, 1f));
        Place(title.rectTransform, new Vector2(0f, size.y * 0.40f), new Vector2(size.x - 28f, size.y * 0.10f));

        Image art = AddImage(card, "Art", faceUp ? GetCardArtColor(index) : new Color(0.28f, 0.13f, 0.06f, 1f));
        Place(art.rectTransform, new Vector2(0f, size.y * 0.16f), new Vector2(size.x - 34f, size.y * 0.40f));

        Image rules = AddImage(card, "RulesBox", faceUp ? new Color(0.72f, 0.73f, 0.66f, 1f) : new Color(0.16f, 0.08f, 0.04f, 1f));
        Place(rules.rectTransform, new Vector2(0f, -size.y * 0.23f), new Vector2(size.x - 34f, size.y * 0.27f));

        Text titleText = AddText(title.transform, "Text", faceUp ? GetCardTitle(index) : "", font, faceUp ? 17 : 12, Color.black, TextAnchor.MiddleLeft);
        Stretch(titleText.rectTransform, 8f, 8f, 0f, 0f);

        Text typeText = AddText(rules.transform, "Text", faceUp ? "Instant" : "", font, faceUp ? 15 : 10, new Color(0.08f, 0.08f, 0.08f), TextAnchor.UpperLeft);
        Stretch(typeText.rectTransform, 8f, 8f, 6f, 6f);

        Text pip = AddText(card, "CostPip", faceUp && index % 3 == 0 ? "1" : "", font, faceUp ? 18 : 10, Color.white, TextAnchor.MiddleCenter);
        Place(pip.rectTransform, new Vector2(size.x * 0.38f, size.y * 0.41f), new Vector2(size.x * 0.13f, size.x * 0.13f));
        Image pipBg = AddImage(card, "CostPipBg", new Color(0.05f, 0.07f, 0.10f, faceUp ? 0.95f : 0f));
        Place(pipBg.rectTransform, new Vector2(size.x * 0.38f, size.y * 0.41f), new Vector2(size.x * 0.14f, size.x * 0.14f));
        pip.transform.SetAsLastSibling();

        return card;
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        return rect;
    }

    private static Image AddImage(Transform parent, string name, Color color)
    {
        RectTransform rect = CreateRect(parent, name);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text AddText(Transform parent, string name, string value, Font font, int size, Color color, TextAnchor alignment)
    {
        RectTransform rect = CreateRect(parent, name);
        Text text = rect.gameObject.AddComponent<Text>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static Color GetCardFaceColor(int index)
    {
        Color[] colors =
        {
            new Color(0.68f, 0.72f, 0.60f, 1f),
            new Color(0.86f, 0.78f, 0.58f, 1f),
            new Color(0.78f, 0.84f, 0.88f, 1f),
            new Color(0.88f, 0.72f, 0.58f, 1f),
            new Color(0.72f, 0.86f, 0.92f, 1f),
            new Color(0.74f, 0.78f, 0.70f, 1f),
            new Color(0.68f, 0.82f, 0.90f, 1f)
        };
        return colors[index % colors.Length];
    }

    private static Color GetCardArtColor(int index)
    {
        Color[] colors =
        {
            new Color(0.16f, 0.34f, 0.20f, 1f),
            new Color(0.72f, 0.38f, 0.10f, 1f),
            new Color(0.16f, 0.52f, 0.78f, 1f),
            new Color(0.75f, 0.24f, 0.10f, 1f),
            new Color(0.06f, 0.46f, 0.62f, 1f),
            new Color(0.28f, 0.22f, 0.58f, 1f),
            new Color(0.10f, 0.36f, 0.62f, 1f)
        };
        return colors[index % colors.Length];
    }

    private static string GetCardTitle(int index)
    {
        string[] titles = { "Forest Gate", "Spark Burst", "Bright Plain", "Flame Slash", "Three Steps", "Mist Field", "Blue Trick" };
        return titles[index % titles.Length];
    }
}
