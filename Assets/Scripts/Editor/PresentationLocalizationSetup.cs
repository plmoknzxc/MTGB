using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public static class PresentationLocalizationSetup
{
    [MenuItem("MTGB/Localization/Rebuild presentation tables")]
    public static void Build()
    {
        const string root="Assets/Localization";
        var settings=LocalizationEditorSettings.ActiveLocalizationSettings;
        if(settings==null) {settings=ScriptableObject.CreateInstance<LocalizationSettings>();AssetDatabase.CreateAsset(settings,root+"/LocalizationSettings.asset");LocalizationEditorSettings.ActiveLocalizationSettings=settings;}
        string[] codes={"zh-CN","en-US","ja-JP"};
        foreach(string code in codes)
        {
            if(LocalizationEditorSettings.GetLocales().Any(x=>x.Identifier.Code==code)) continue;
            var locale=Locale.CreateLocale(code);AssetDatabase.CreateAsset(locale,root+"/"+code+".asset");LocalizationEditorSettings.AddLocale(locale);
        }
        var strings=LocalizationEditorSettings.GetStringTableCollection("Interface") ?? LocalizationEditorSettings.CreateStringTableCollection("Interface",root);
        foreach(string line in File.ReadLines(root+"/Interface.tsv"))
        {
            var parts=line.TrimStart('\uFEFF').Split('\t');if(parts.Length!=3 && parts.Length!=4) continue;
            foreach(var table in strings.StringTables)
            {
                int index=System.Array.IndexOf(codes,table.LocaleIdentifier.Code);if(index<0) continue;
                table.AddEntry(parts[0].Replace("\\n","\n"),parts[index+(parts.Length==4?1:0)].Replace("\\n","\n"));EditorUtility.SetDirty(table);
            }
        }
        // Rebuilding keys can leave old locale rows without a shared key. Unity exposes
        // those rows with a null Key, so remove them before the tables are saved.
        foreach(var table in strings.StringTables)
        {
            var orphanIds=table.Values.Where(entry=>entry==null || strings.SharedData.GetEntry(entry.KeyId)==null)
                .Where(entry=>entry!=null).Select(entry=>entry.KeyId).ToArray();
            foreach(long id in orphanIds) table.RemoveEntry(id);
            if(orphanIds.Length>0) EditorUtility.SetDirty(table);
        }
        var assets=LocalizationEditorSettings.GetAssetTableCollection("Presentation") ?? LocalizationEditorSettings.CreateAssetTableCollection("Presentation",root);
        foreach(string code in codes)
        {
            string suffix=code=="ja-JP"?"jp":"sc";
            var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJK"+suffix+"-Regular.otf");
            string path="Assets/Fonts/NotoSansCJK"+suffix+"-SDF.asset";
            var tmp=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if(tmp==null)
            {
                tmp=TMP_FontAsset.CreateFontAsset(font,32,5,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
                tmp.name="NotoSansCJK"+suffix;tmp.isMultiAtlasTexturesEnabled=true;
                AssetDatabase.CreateAsset(tmp,path);
                foreach(var texture in tmp.atlasTextures) AssetDatabase.AddObjectToAsset(texture,tmp);
                AssetDatabase.AddObjectToAsset(tmp.material,tmp);tmp.material.mainTexture=tmp.atlasTexture;
                EditorUtility.SetDirty(tmp);EditorUtility.SetDirty(tmp.material);
            }
            assets.AddAssetToTable(new LocaleIdentifier(code),"Font",font);
            assets.AddAssetToTable(new LocaleIdentifier(code),"TmpFont",tmp);
        }
        foreach(var t in assets.AssetTables) EditorUtility.SetDirty(t);
        EditorUtility.SetDirty(strings);EditorUtility.SetDirty(strings.SharedData);EditorUtility.SetDirty(assets);EditorUtility.SetDirty(assets.SharedData);EditorUtility.SetDirty(settings);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,strings);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,assets);
        AssetDatabase.SaveAssets();
    }
}


