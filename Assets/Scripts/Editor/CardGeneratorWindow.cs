using UnityEngine;
using UnityEditor;
using System.IO;

public class CardGeneratorWindow : EditorWindow
{
    // Card Properties
    string cardName = "New Card";
    int cardID = 0;
    string setCode = "MTGB";
    int collectorNumber = 1;
    CardType cardTypes = CardType.Creature;
    string manaCost = string.Empty;
    string description = "Card Description";
    int attack = 1;
    int defence = 1;
    Sprite artwork;

    // Window State
    Vector2 scrollPos;
    
    [MenuItem("Tools/Card Generator")]
    public static void ShowWindow()
    {
        GetWindow<CardGeneratorWindow>("Card Generator");
    }

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        
        GUILayout.Label("Create New Card", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Basic Info
        GUILayout.Label("Basic Info", EditorStyles.label);
        cardName = EditorGUILayout.TextField("Card Name", cardName);
        cardID = EditorGUILayout.IntField("Internal Card ID", cardID);
        setCode = EditorGUILayout.TextField("Set Code", setCode);
        collectorNumber = EditorGUILayout.IntField("Collector Number", collectorNumber);
        cardTypes = (CardType)EditorGUILayout.EnumFlagsField("Card Types", cardTypes);
        manaCost = EditorGUILayout.TextField("Mana Cost", manaCost);
        
        EditorGUILayout.Space();
        
        // Description
        GUILayout.Label("Description", EditorStyles.label);
        description = EditorGUILayout.TextArea(description, GUILayout.Height(60));
        
        EditorGUILayout.Space();

        // Stats
        GUILayout.Label("Stats", EditorStyles.label);
        EditorGUILayout.BeginHorizontal();
        attack = EditorGUILayout.IntField("Attack", attack);
        defence = EditorGUILayout.IntField("Defence", defence);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Artwork
        GUILayout.Label("Artwork", EditorStyles.label);
        artwork = (Sprite)EditorGUILayout.ObjectField("Artwork", artwork, typeof(Sprite), false);

        EditorGUILayout.Space(20);

        // Preview (Simple Text)
        GUILayout.Label("Preview:", EditorStyles.boldLabel);
        GUIStyle cardStyle = new GUIStyle(EditorStyles.helpBox);
        cardStyle.fontSize = 12;
        cardStyle.wordWrap = true;
        
        string previewText = $"Name: {cardName}\nInternal ID: {cardID}\nPrinting: {setCode}/{collectorNumber}\nStats: {attack}/{defence}\n\n{description}";
        GUILayout.Box(previewText, cardStyle, GUILayout.ExpandWidth(true), GUILayout.Height(100));

        if (artwork != null)
        {
            // Simple texture preview if sprite is selected
            Texture2D texture = AssetPreview.GetAssetPreview(artwork);
            if (texture != null)
            {
                GUILayout.Label(texture, GUILayout.Width(100), GUILayout.Height(100));
            }
        }

        EditorGUILayout.Space(20);

        if (GUILayout.Button("Create Card Asset", GUILayout.Height(40)))
        {
            CreateCard();
        }

        EditorGUILayout.EndScrollView();
    }

    void CreateCard()
    {
        // 1. Create Instance
        CardData asset = ScriptableObject.CreateInstance<CardData>();
        asset.cardName = cardName;
        asset.cardID = cardID;
        asset.setCode = setCode.Trim();
        asset.collectorNumber = Mathf.Max(0, collectorNumber);
        asset.collectorNumberText = asset.collectorNumber > 0 ? asset.collectorNumber.ToString() : string.Empty;
        asset.cardTypes = cardTypes;
        asset.manaCost = manaCost.Trim();
        asset.description = description;
        asset.attack = attack;
        asset.defence = defence;
        asset.artwork = artwork;

        // 2. Determine Path
        string folderPath = "Assets/Cards";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileName = $"{cardName}.asset";
        // Sanitize filename
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        
        string fullPath = Path.Combine(folderPath, fileName);
        fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath);

        // 3. Save Asset
        AssetDatabase.CreateAsset(asset, fullPath);
        AssetDatabase.SaveAssets();

        // 4. Focus
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;

        Debug.Log($"Card '{cardName}' created at {fullPath}");
    }
}
