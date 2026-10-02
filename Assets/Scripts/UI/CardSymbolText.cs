using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

// Coordinates from the pinned Forge FSkinProp mana atlas, measured from its top-left.
public static class CardSymbolText
{
    private static readonly Dictionary<string, int> indices = new Dictionary<string, int>();
    private static readonly Regex symbol = new Regex(@"\{([^{}]+)\}");
    private static TMP_SpriteAsset atlas;

    public static TMP_SpriteAsset Atlas
    {
        get
        {
            if (atlas != null) return atlas;
            indices.Clear();
            var texture = Resources.Load<Texture2D>("UI/ManaSymbols/ForgeSymbols");
            atlas = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            JsonUtility.FromJsonOverwrite("{\"m_Version\":\"1.1.0\"}", atlas);
            atlas.name = "CardSymbols";
            atlas.spriteSheet = texture;
            atlas.material = Resources.Load<Material>("UI/ManaSymbols/ForgeSymbols");
            Add("W",412,84); Add("U",330,84); Add("B",166,2);
            Add("R",330,2); Add("G",166,84); Add("C",248,2);
            Add("T",166,412); Add("Q",248,412); Add("S",412,2);
            for (int n=0; n<=17; n++) Add(n.ToString(), 2 + n%2*82, 2 + n/2*82);
            Add("18",166,658); Add("19",248,658); Add("20",330,658);
            Add("X",248,576); Add("Y",330,576); Add("Z",412,576);
            Add("B/G",166,166); Add("B/R",248,166); Add("G/U",166,248);
            Add("G/W",248,248); Add("R/G",166,330); Add("R/W",248,330);
            Add("U/B",330,330); Add("U/R",412,330); Add("W/B",330,412); Add("W/U",412,412);
            Add("2/B",166,494); Add("2/G",248,494); Add("2/R",330,494); Add("2/U",412,494); Add("2/W",166,576);
            Add("U/P",330,248); Add("W/P",412,248); Add("R/P",412,166); Add("G/P",330,166); Add("B/P",248,84);
            atlas.UpdateLookupTables();
            return atlas;
        }
    }

    private static void Add(string token, int x, int y)
    {
        int index = atlas.spriteGlyphTable.Count;
        indices[token] = index;
        var glyph = new TMP_SpriteGlyph((uint)index, new GlyphMetrics(80,80,0,68,86),
            new GlyphRect(x, atlas.spriteSheet.height-y-80,80,80), 1, 0);
        atlas.spriteGlyphTable.Add(glyph);
        atlas.spriteCharacterTable.Add(new TMP_SpriteCharacter((uint)(0xE000+index), atlas, glyph) { name=token, scale=1 });
    }

    public static string Format(string text)
    {
        _ = Atlas;
        return symbol.Replace(text ?? string.Empty, match =>
        {
            string token = match.Groups[1].Value.Trim().ToUpperInvariant();
            if (!indices.TryGetValue(token, out int index)) return match.Value;
            return char.ConvertFromUtf32(0xE000+index);
        });
    }

    public static TextMeshProUGUI Create(Transform parent, string name, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        // Dynamic card details must use the locale font on their first frame,
        // before the periodic preference binding reaches newly created labels.
        text.font = GamePreferences.TmpFont != null ? GamePreferences.TmpFont : TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.color = color;
        text.richText = false;
        text.raycastTarget = false;
        text.spriteAsset = Atlas;
        return text;
    }
}
