using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Only menu surfaces carry this component. The global preference binder still owns text size and language.
public sealed class MenuTypography : MonoBehaviour
{
    public enum Face { Default, Reading, Heading, Wordmark, Control }

    const string FontPath = "UI/CardTemplates/Fonts/";
    static readonly Dictionary<string,Font> native = new();
    static readonly Dictionary<string,TMP_FontAsset> sdf = new();

    internal static void Scope(Transform root)
    {
        if (root.TryGetComponent<MenuTypography>(out _)) return;
        root.gameObject.AddComponent<MenuTypography>();
        foreach (var binding in root.GetComponentsInChildren<PreferenceText>(true))
            binding.InheritMenuFace();
    }

    internal static void Use(Text text, Face face)
    {
        var binding = text.GetComponent<PreferenceText>() ?? text.gameObject.AddComponent<PreferenceText>();
        binding.SetMenuFace(face);
    }

    static string Key(Face face)
    {
        if (face == Face.Control) return MenuThemeCatalog.Current.ControlFont;
        if (face == Face.Wordmark) return FontPath + "NameEN";
        bool heading = face == Face.Heading;
        return FontPath + (GamePreferences.Language switch
        {
            "en-US" => heading ? "NameEN" : "RulesEN",
            "ja-JP" => heading ? "NameJP" : "RulesJP",
            _ => heading ? "NameCN" : "NameWenKaiCN"
        });
    }

    internal static Font Native(Face face)
    {
        string key = Key(face);
        string cacheKey = face == Face.Control ? MenuThemeCatalog.Current.Id + ":" + key : key;
        if (!native.TryGetValue(cacheKey,out var font))
            native[cacheKey] = font = MenuThemeCatalog.Current.Load<Font>(key);
        return font != null ? font : GamePreferences.Font;
    }

    internal static TMP_FontAsset Sdf(Face face)
    {
        string key = Key(face);
        var source = Native(face);
        if (source == null || source == GamePreferences.Font) return GamePreferences.TmpFont;
        if (face == Face.Control) key = MenuThemeCatalog.Current.Id + ":" + key;
        if (!sdf.TryGetValue(key,out var font))
        {
            font = TMP_FontAsset.CreateFontAsset(source,48,6,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024,1024,AtlasPopulationMode.Dynamic,true);
            if (font == null) return GamePreferences.TmpFont;
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            sdf.Add(key,font);
        }
        var fallback = GamePreferences.TmpFont;
        if (fallback != null && !font.fallbackFontAssetTable.Contains(fallback)) font.fallbackFontAssetTable.Add(fallback);
        return font;
    }
}
