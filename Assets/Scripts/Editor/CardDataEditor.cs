using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardData))]
public sealed class CardDataEditor : Editor
{
    private static readonly string[] BasicSymbols =
    {
        "{W}", "{U}", "{B}", "{R}", "{G}", "{C}", "{X}", "{S}"
    };

    private static readonly string[] HybridSymbols =
    {
        "{W/U}", "{W/B}", "{U/B}", "{U/R}", "{B/R}", "{B/G}", "{R/G}", "{R/W}", "{G/W}", "{G/U}",
        "{2/W}", "{2/U}", "{2/B}", "{2/R}", "{2/G}",
        "{C/W}", "{C/U}", "{C/B}", "{C/R}", "{C/G}"
    };

    private static readonly string[] PhyrexianSymbols =
    {
        "{W/P}", "{U/P}", "{B/P}", "{R/P}", "{G/P}",
        "{W/U/P}", "{W/B/P}", "{U/B/P}", "{U/R/P}", "{B/R/P}", "{B/G/P}", "{R/G/P}", "{R/W/P}", "{G/W/P}", "{G/U/P}"
    };

    private SerializedProperty cardName;
    private SerializedProperty cardID;
    private SerializedProperty oracleID;
    private SerializedProperty setCode;
    private SerializedProperty collectorNumberText;
    private SerializedProperty cardTypes;
    private SerializedProperty description;
    private SerializedProperty manaCost;
    private SerializedProperty attack;
    private SerializedProperty defence;
    private SerializedProperty artwork;
    private SerializedProperty luaScript;
    private SerializedProperty effectKey;

    private void OnEnable()
    {
        cardName = serializedObject.FindProperty("cardName");
        cardID = serializedObject.FindProperty("cardID");
        oracleID = serializedObject.FindProperty("oracleID");
        setCode = serializedObject.FindProperty("setCode");
        collectorNumberText = serializedObject.FindProperty("collectorNumberText");
        cardTypes = serializedObject.FindProperty("cardTypes");
        description = serializedObject.FindProperty("description");
        manaCost = serializedObject.FindProperty("manaCost");
        attack = serializedObject.FindProperty("attack");
        defence = serializedObject.FindProperty("defence");
        artwork = serializedObject.FindProperty("artwork");
        luaScript = serializedObject.FindProperty("luaScript");
        effectKey = serializedObject.FindProperty("effectKey");

        foreach (Object item in targets)
        {
            if (item is CardData data) data.MigrateLegacyData();
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(cardName);
        EditorGUILayout.PropertyField(cardID, new GUIContent("Internal Card ID"));
        EditorGUILayout.PropertyField(oracleID, new GUIContent("Oracle ID"));
        EditorGUILayout.PropertyField(setCode);
        EditorGUILayout.PropertyField(collectorNumberText, new GUIContent("Collector Number"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Card", EditorStyles.boldLabel);
        CardType selected = (CardType)cardTypes.intValue;
        selected = (CardType)EditorGUILayout.EnumFlagsField("Card Types", selected);
        cardTypes.intValue = (int)selected;
        EditorGUILayout.PropertyField(description);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mana Cost", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(manaCost, GUIContent.none);
        if (GUILayout.Button("Add Symbol", GUILayout.Width(100f))) ShowManaSymbolMenu();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(attack);
        EditorGUILayout.PropertyField(defence);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Artwork", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(artwork);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scripting", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(luaScript);
        EditorGUILayout.PropertyField(effectKey);

        serializedObject.ApplyModifiedProperties();
    }

    private void ShowManaSymbolMenu()
    {
        var menu = new GenericMenu();
        AddSymbols(menu, "Basic/", BasicSymbols);
        for (int i = 0; i <= 20; i++) AddSymbol(menu, $"Generic/{{{i}}}", $"{{{i}}}");
        AddSymbols(menu, "Hybrid/", HybridSymbols);
        AddSymbols(menu, "Phyrexian/", PhyrexianSymbols);
        menu.ShowAsContext();
    }

    private void AddSymbols(GenericMenu menu, string prefix, string[] symbols)
    {
        for (int i = 0; i < symbols.Length; i++) AddSymbol(menu, prefix + symbols[i], symbols[i]);
    }

    private void AddSymbol(GenericMenu menu, string label, string symbol)
    {
        menu.AddItem(new GUIContent(label), false, () =>
        {
            serializedObject.Update();
            manaCost.stringValue = (manaCost.stringValue ?? string.Empty) + symbol;
            serializedObject.ApplyModifiedProperties();
        });
    }
}
