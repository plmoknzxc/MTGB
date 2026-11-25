#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Uses the presenter's own defaults so authoring and runtime cannot drift.
public static class WorldCardPrefabAuthoring
{
    public const string Folder="Assets/Art/Battlefield/Components";
    public const string PrefabPath=Folder+"/BattlefieldCard.prefab";
    public const string LayoutPath=Folder+"/CardArtLayouts.asset";

    public static void BuildFromRunningTable()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Open IntegratedBattlefield in Play mode first.");
        var table=UnityEngine.Object.FindFirstObjectByType<BattlefieldWorldTable>();
        if(table==null||table.Paper==null)throw new InvalidOperationException("World table is not initialized.");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var root=new GameObject("BattlefieldCard");
        try{
            var card=root.AddComponent<BattlefieldWorldCard>();card.Initialize(table,null,null,null);
            var quad=SaveCopy(table.Quad,"CardQuad.asset");
            var edge=SaveCopy(table.EdgeMesh,"CardThickness.asset");
            var paper=SaveCopy(table.Paper,"CardPaper.mat");
            var shadow=SaveCopy(table.Shadow,"CardShadow.mat");
            foreach(var part in root.GetComponentsInChildren<BattlefieldCardPart>()){
                if(part.TryGetComponent<MeshFilter>(out var mesh)&&part.GetComponent<TMPro.TextMeshPro>()==null){
                    mesh.sharedMesh=part.Binding=="Paper edge"?edge:quad;
                    part.GetComponent<MeshRenderer>().sharedMaterial=part.Binding=="Contact shadow"?shadow:paper;
                }
                part.gameObject.name=part.Binding switch {
                    "Printed face"=>"Artwork", "Unavailable art"=>"CardName",
                    "Current stats"=>"Stats", "Permanent type"=>"CardType", "State rim"=>"Frame",
                    "Name vignette"=>"NameShade", "Name edge inlay"=>"NameAccent", _=>part.Binding};
            }
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            if(AssetDatabase.LoadAssetAtPath<BattlefieldCardArtLayouts>(LayoutPath)==null){
                var layouts=ScriptableObject.CreateInstance<BattlefieldCardArtLayouts>();
                layouts.entries=new[]{
                    new BattlefieldCardArtLayouts.Entry{printingKey="MH2/337",illustration=new Rect(.08f,.46f,.84f,.42f)},
                    new BattlefieldCardArtLayouts.Entry{printingKey="SLD/1878",illustration=new Rect(.02f,.46f,.96f,.42f)}};
                AssetDatabase.CreateAsset(layouts,LayoutPath);
            }
            AssetDatabase.SaveAssets();
        }finally{UnityEngine.Object.DestroyImmediate(root);}
    }

    static T SaveCopy<T>(T source,string file) where T:UnityEngine.Object
    {
        string path=Folder+"/"+file;
        var existing=AssetDatabase.LoadAssetAtPath<T>(path);
        if(existing!=null){EditorUtility.CopySerialized(source,existing);return existing;}
        var copy=UnityEngine.Object.Instantiate(source);copy.name=Path.GetFileNameWithoutExtension(file);
        AssetDatabase.CreateAsset(copy,path);return copy;
    }
    public static void ConnectScene()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Connect the saved scene outside Play mode.");
        var table=UnityEngine.Object.FindFirstObjectByType<BattlefieldWorldTable>();
        if(table==null)throw new InvalidOperationException("World table not found.");
        Undo.RecordObject(table,"Connect component card prefab");
        table.cardPrefab=AssetDatabase.LoadAssetAtPath<BattlefieldWorldCard>(PrefabPath);
        table.artLayouts=AssetDatabase.LoadAssetAtPath<BattlefieldCardArtLayouts>(LayoutPath);
        if(table.cardPrefab==null||table.artLayouts==null)throw new InvalidOperationException("Build the component assets first.");
        EditorUtility.SetDirty(table);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(table.gameObject.scene);
    }
}
#endif
