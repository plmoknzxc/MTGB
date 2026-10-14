#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

// Explicitly captures static HUD authoring only. It never starts a match or
// writes engine views, hands, permanent cards, counters, emblems or notices.
public static class BattlefieldSceneLayoutSync
{
    public const string Output=".tools/battlefield-row-boundary-1005";
    const string Snapshot="Assets/Editor/BattlefieldLayoutSnapshot.prefab";
    const string Shapes="Assets/UI/BattlefieldHudShapes.asset";
    static readonly string[] Roots={"PlayerPlate","OpponentPlate","PlayerPlateWithZones","OpponentPlateWithZones","ZonePiles_PlayerBottomLeft","ZonePiles_OpponentTopRight","GearBox","CornerCluster","PriorityControlMode"};
    static readonly Dictionary<UnityEngine.Object,UnityEngine.Object> assets=new Dictionary<UnityEngine.Object,UnityEngine.Object>();
    public static void Capture()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Capture requires runtime layout");
        Directory.CreateDirectory(Output);assets.Clear();
        var canvas=GameObject.Find("Canvas").transform;
        var snapshot=new GameObject("BattlefieldLayoutSnapshot",typeof(RectTransform));
        foreach(string name in Roots){
            var copy=UnityEngine.Object.Instantiate(canvas.Find(name).gameObject,snapshot.transform);copy.name=name;
            foreach(string transient in new[]{"PlayerCounters","States","Emblems","StatesPrevious","StatesNext","EmblemsPrevious","EmblemsNext","UndoMana","ManaPoolChips","ScrollingName"})
                foreach(var child in copy.GetComponentsInChildren<RectTransform>(true).Where(t=>t.name==transient).ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
            var nameText=copy.transform.Find("Name")?.GetComponent<Text>();if(nameText!=null)nameText.enabled=true;
            // Keep only static widgets and their preference/layout components.
            foreach(var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if(!(behaviour is Graphic)&&!(behaviour is PreferenceText)&&!(behaviour is CenteredNumeralMesh)&&behaviour.GetType().Namespace!="UnityEngine.UI")UnityEngine.Object.DestroyImmediate(behaviour);
            foreach(var image in copy.GetComponentsInChildren<Image>(true))image.sprite=Persist(image.sprite) as Sprite;
            foreach(var text in copy.GetComponentsInChildren<Text>(true))text.font=Persist(text.font) as Font;
        }
        PrefabUtility.SaveAsPrefabAsset(snapshot,Snapshot);UnityEngine.Object.DestroyImmediate(snapshot);
        var rects=new List<string>();
        foreach(string name in new[]{"PlayerHandRoot","OpponentHandRoot","InspectPanel","StackColumn","PriorityPill"}) {
            var r=(RectTransform)canvas.Find(name);
            rects.Add(name+"|"+JsonUtility.ToJson(new Geometry{min=r.anchorMin,max=r.anchorMax,pivot=r.pivot,pos=r.anchoredPosition,size=r.sizeDelta}));
        }
        File.WriteAllLines(Output+"/geometry.txt",rects);AssetDatabase.SaveAssets();
    }
    static UnityEngine.Object Persist(UnityEngine.Object obj)
    {
        if(obj==null||EditorUtility.IsPersistent(obj))return obj;
        if(assets.TryGetValue(obj,out var saved))return saved;
        if(!AssetDatabase.LoadMainAssetAtPath(Shapes)) {
            Directory.CreateDirectory("Assets/UI");var bank=new Texture2D(1,1);bank.name="HUD shapes";AssetDatabase.CreateAsset(bank,Shapes);
        }
        if(obj is Sprite sprite) {
            var texture=(Texture2D)Persist(sprite.texture);
            var copy=Sprite.Create(texture,sprite.rect,new Vector2(sprite.pivot.x/sprite.rect.width,sprite.pivot.y/sprite.rect.height),sprite.pixelsPerUnit,0,SpriteMeshType.FullRect,sprite.border);
            copy.name=sprite.name;copy.hideFlags=HideFlags.None;AssetDatabase.AddObjectToAsset(copy,Shapes);assets[obj]=copy;return copy;
        }
        if(obj is Texture2D original) {
            // Procedural HUD textures discard their CPU copy. Read back their
            // rendered pixels instead of cloning a non-readable texture.
            var target=RenderTexture.GetTemporary(original.width,original.height,0,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            Texture2D copy;
            try{Graphics.Blit(original,target);RenderTexture.active=target;copy=new Texture2D(original.width,original.height,TextureFormat.RGBA32,false);copy.ReadPixels(new Rect(0,0,original.width,original.height),0,0);copy.Apply();}
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
            copy.name=original.name;copy.filterMode=original.filterMode;AssetDatabase.AddObjectToAsset(copy,Shapes);assets[obj]=copy;return copy;
        }
        if(obj is Font font) {
            var copy=Font.CreateDynamicFontFromOSFont(font.fontNames,font.fontSize>0?font.fontSize:24);copy.name=font.name;
            copy.RequestCharactersInTexture("0123456789",Mathf.RoundToInt(42*GamePreferences.TextScale));
            var material=new Material(copy.material);material.name=font.name+" HUD";
            material.mainTexture=Persist(material.mainTexture) as Texture2D;
            AssetDatabase.AddObjectToAsset(material,Shapes);copy.material=material;
            var serialized=new SerializedObject(copy);serialized.FindProperty("m_Texture").objectReferenceValue=material.mainTexture;serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.AddObjectToAsset(copy,Shapes);assets[obj]=copy;return copy;
        }
        var clone=UnityEngine.Object.Instantiate(obj);clone.name=obj.name;clone.hideFlags=HideFlags.None;AssetDatabase.AddObjectToAsset(clone,Shapes);assets[obj]=clone;return clone;
    }
    [Serializable] public sealed class Geometry {public Vector2 min,max,pivot,pos,size;}
    public static void Apply()
    {
        if(Application.isPlaying||EditorSceneManager.GetActiveScene().path!="Assets/Scenes/IntegratedBattlefield.unity")throw new InvalidOperationException("Apply requires authored battlefield in edit mode");
        var canvas=GameObject.Find("Canvas").transform;var source=AssetDatabase.LoadAssetAtPath<GameObject>(Snapshot);
        foreach(Transform child in source.transform){
            var old=canvas.Find(child.name);int order=old==null?canvas.childCount:old.GetSiblingIndex();
            if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var copy=UnityEngine.Object.Instantiate(child.gameObject,canvas);copy.name=child.name;copy.transform.SetSiblingIndex(order);
        }
        foreach(string plate in new[]{"PlayerPlate","OpponentPlate"}) {
            var shell=canvas.Find(plate+"WithZones");var zones=canvas.Find(plate=="PlayerPlate"?"ZonePiles_PlayerBottomLeft":"ZonePiles_OpponentTopRight");
            int behind=Math.Min(canvas.Find(plate).GetSiblingIndex(),zones.GetSiblingIndex());if(shell.GetSiblingIndex()<behind)behind--;shell.SetSiblingIndex(Math.Max(0,behind));
        }
        canvas.Find("CornerCluster/ControlRow/SkipCombatButton").GetComponent<Button>().interactable=true;
        foreach(string line in File.ReadAllLines(Output+"/geometry.txt")) {
            int split=line.IndexOf('|');string name=line.Substring(0,split);var g=JsonUtility.FromJson<Geometry>(line.Substring(split+1));var r=(RectTransform)canvas.Find(name);
            r.anchorMin=g.min;r.anchorMax=g.max;r.pivot=g.pivot;r.anchoredPosition=g.pos;r.sizeDelta=g.size;
            if(name is "InspectPanel" or "StackColumn" or "PriorityPill")r.gameObject.SetActive(false);
            if(name.EndsWith("HandRoot"))foreach(Transform card in r)card.gameObject.SetActive(false);
        }
        // These authored sample piles are not live permanent cards.
        var whitebox=GameObject.Find("ArenaWhitebox");if(whitebox!=null)foreach(Transform child in whitebox.transform)if(child.name.Contains("Card"))child.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.DeleteAsset(Snapshot);File.WriteAllText(Output+"/scene.txt","APPLIED: static HUD from current runtime; no match payload saved.");
    }
}
#endif
