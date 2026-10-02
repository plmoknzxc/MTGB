#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class IntegratedBattlefieldArtPass
{
    private static Transform canvas;
    private static readonly Color Ink = new Color(.018f, .025f, .028f, 1f);
    private static readonly Color Brass = new Color(.61f, .47f, .27f, .8f);
    [MenuItem("MTGB/Art/Apply Obsidian Sanctum")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().name != "IntegratedBattlefield")
            throw new System.InvalidOperationException("Open IntegratedBattlefield outside Play Mode.");
        canvas = GameObject.Find("Canvas").transform;
        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Obsidian Sanctum art pass");
        var asset = "Assets/Art/Battlefield/ObsidianSanctum.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(asset);
        importer.maxTextureSize = 2048; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        var env = canvas.Find("EnvironmentLayer");
        var background = env.Find("SanctumBackdrop");
        if (background == null)
        {
            var go = new GameObject("SanctumBackdrop", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            Undo.RegisterCreatedObjectUndo(go, "Sanctum backdrop");
            background = go.transform; background.SetParent(env, false);
        }
        background.SetAsFirstSibling();
        var rect = (RectTransform)background;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var raw = background.GetComponent<UnityEngine.UI.RawImage>();
        raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(asset); raw.raycastTarget = false;
        foreach (var image in env.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        { image.raycastTarget = false; image.color = Color.clear; }
        foreach (var text in canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        { text.color = new Color(.88f,.86f,.79f); text.raycastTarget = false; }
        foreach (var outline in canvas.GetComponentsInChildren<UnityEngine.UI.Outline>(true))
        { outline.effectColor = Brass; outline.effectDistance = new Vector2(1,-1); }
        foreach (string name in new[] { "InspectPanel", "PlayerPlate", "OpponentPlate", "PriorityPill" })
            canvas.Find(name).GetComponent<UnityEngine.UI.Image>().color = Ink;
        Place("InspectPanel", new Vector2(0,.5f), new Vector2(0,.5f), new Vector2(28,15), new Vector2(244,580));
        Place("OpponentPlate", new Vector2(0,1), new Vector2(0,1), new Vector2(28,-28), new Vector2(280,76));
        Place("PlayerPlate", Vector2.zero, Vector2.zero, new Vector2(28,28), new Vector2(280,76));
        Place("GearBox", new Vector2(1,1), new Vector2(1,1), new Vector2(-28,-24), new Vector2(98,44));
        canvas.Find("GearBox/SettingsButton").gameObject.SetActive(false);
        Place("GearBox/ConcedeButton", Vector2.one, Vector2.one, Vector2.zero, new Vector2(80,34));
        canvas.Find("GearBox/ConcedeButton/Icon").gameObject.SetActive(false);
        var concede = canvas.Find("GearBox/ConcedeButton");
        if (concede.Find("Label") == null)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            label.transform.SetParent(concede, false);
            var text = label.GetComponent<UnityEngine.UI.Text>();
            text.font = canvas.Find("PlayerPlate/Name").GetComponent<UnityEngine.UI.Text>().font;
            text.text = "认输"; text.fontSize = 15; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.8f,.77f,.69f); text.raycastTarget = false;
        }
        Place("GearBox/ConcedeButton/Label", new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(80,34));
        Place("ZonePiles_OpponentTopRight", new Vector2(1,1), new Vector2(1,1), new Vector2(-28,-90), new Vector2(186,88));
        Place("ZonePiles_PlayerBottomLeft", Vector2.zero, Vector2.zero, new Vector2(28,135), new Vector2(186,88));
        Place("StackColumn", Vector2.one, Vector2.one, new Vector2(-28,-205), new Vector2(300,180));
        Place("StackColumn/Viewport", new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(0,-28), new Vector2(300,152));
        Place("StackColumn/Header", new Vector2(.5f,1), new Vector2(.5f,1), Vector2.zero, new Vector2(300,24));
        Place("CornerCluster", new Vector2(1,0), new Vector2(1,0), new Vector2(-28,28), new Vector2(300,140));
        Place("PriorityPill", new Vector2(1,0), new Vector2(1,0), new Vector2(-28,180), new Vector2(300,30));
        Place("CornerCluster/ControlRow", new Vector2(.5f,0), new Vector2(.5f,0), Vector2.zero, new Vector2(300,105));
        Place("CornerCluster/ControlRow/TurnChip", new Vector2(0,1), new Vector2(0,1), Vector2.zero, new Vector2(48,42));
        Place("CornerCluster/ControlRow/PhaseText", new Vector2(1,1), Vector2.one, new Vector2(0,-1), new Vector2(240,24));
        Place("CornerCluster/ControlRow/RoundText", Vector2.one, Vector2.one, new Vector2(0,-27), new Vector2(240,18));
        Place("CornerCluster/ControlRow/SkipCombatButton", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(142,46));
        Place("CornerCluster/ControlRow/EndTurnButton", new Vector2(1,0), new Vector2(1,0), Vector2.zero, new Vector2(148,46));
        foreach (string button in new[] { "SkipCombatButton", "EndTurnButton" })
        {
            var node = canvas.Find("CornerCluster/ControlRow/" + button);
            var image = node.GetComponent<UnityEngine.UI.Image>(); image.sprite = null;
            image.color = button == "EndTurnButton" ? new Color(.4f,.26f,.105f) : new Color(.1f,.17f,.18f);
            var label = (RectTransform)node.Find("Label"); label.anchorMin = Vector2.zero; label.anchorMax = Vector2.one;
            label.offsetMin = label.offsetMax = Vector2.zero;
            label.GetComponent<UnityEngine.UI.Text>().fontSize = 18;
        }
        canvas.Find("CornerCluster/ControlRow/Divider").gameObject.SetActive(false);
        canvas.Find("CornerCluster/ControlRow/TurnChip").GetComponent<UnityEngine.UI.Image>().color = Ink;
        canvas.Find("CornerCluster/ControlRow/DecisionTimerWidget").gameObject.SetActive(false);
        Place("CornerCluster/ManaRow", new Vector2(.5f,1), new Vector2(.5f,1), Vector2.zero, new Vector2(300,24));
        Place("CornerCluster/ManaRow/Label", new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(300,24));
        Place("PlayerHandRoot", new Vector2(.5f,0), new Vector2(.5f,.5f), new Vector2(0,16), new Vector2(100,100));
        Place("OpponentHandRoot", new Vector2(.5f,1), new Vector2(.5f,.5f), new Vector2(0,-28), new Vector2(100,100));
        string[] fields = { "Header", "SetNumber", "Art", "Name", "CostRow", "TypeLine", "Rules", "Footer" };
        float[] y = { 12, 38, 65, 378, 410, 439, 465, 552 };
        float[] heights = { 22, 18, 300, 28, 22, 22, 82, 18 };
        for (int i=0;i<fields.Length;i++)
            Place("InspectPanel/"+fields[i], new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(0,-y[i]), new Vector2(216,heights[i]));
        foreach(var text in canvas.Find("InspectPanel").GetComponentsInChildren<UnityEngine.UI.Text>())
        {
            text.alignment = TextAnchor.UpperLeft; text.fontSize = text.name == "Name" ? 19 : 14;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        }
        canvas.Find("InspectPanel/Header").GetComponent<UnityEngine.UI.Text>().text = "卡牌详情";
        canvas.Find("InspectPanel/Rules").GetComponent<UnityEngine.UI.Text>().text = "悬停卡牌查看详情";
        canvas.Find("InspectPanel/Footer").GetComponent<UnityEngine.UI.Text>().text = "";
        canvas.Find("InspectPanel/TopBar").GetComponent<UnityEngine.UI.Image>().color = Brass;
        canvas.Find("InspectPanel/Art").GetComponent<UnityEngine.UI.Image>().color = new Color(.02f,.03f,.035f,.45f);
        foreach(string plate in new[] {"PlayerPlate","OpponentPlate"})
        {
            canvas.Find(plate+"/FactionBar").GetComponent<UnityEngine.UI.Image>().color = Brass;
            canvas.Find(plate+"/Avatar").GetComponent<UnityEngine.UI.Image>().color = new Color(.22f,.29f,.28f);
            canvas.Find(plate+"/Life").GetComponent<UnityEngine.UI.Text>().fontSize = 32;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }
    private static void Place(string path, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var r = (RectTransform)canvas.Find(path);
        if(r==null) throw new System.InvalidOperationException(path);
        r.anchorMin=r.anchorMax=anchor; r.pivot=pivot; r.anchoredPosition=position; r.sizeDelta=size;
    }
}
#endif
