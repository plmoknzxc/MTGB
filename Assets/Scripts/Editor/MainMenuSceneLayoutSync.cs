#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Persist the start screen through the same layout used at runtime. No card
// catalog, local match, saved deck or network service is initialized here.
public static class MainMenuSceneLayoutSync
{
    const string ShapePath="Assets/UI/MenuNavigationShape.asset";

    [MenuItem("MTGB/UI/Sync Main Menu Scene Layout")]
    public static void Apply()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(Application.isPlaying||scene.path!="Assets/Scenes/Main.unity")
            throw new InvalidOperationException("Open Main.unity outside Play mode before syncing its layout.");
        var start=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RectTransform>(true))
            .Single(r=>r.name=="StartScreen");
        MenuVisualTheme.ApplyStartScreen(start);
        var sprite=PersistentNavigationShape();
        foreach(var button in start.GetComponentsInChildren<Button>(true))
            if(button.name is "OnlineMatchButton" or "LocalAiButton" or "SettingsButton" or "DeckEditorButton")
                button.GetComponent<Image>().sprite=sprite;
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
    }

    static Sprite PersistentNavigationShape()
    {
        var saved=AssetDatabase.LoadAllAssetsAtPath(ShapePath).OfType<Sprite>().FirstOrDefault();
        if(saved!=null)return saved;
        var original=IntegratedUiUtility.RoundedRect(520,76,8);
        var target=RenderTexture.GetTemporary(original.texture.width,original.texture.height,0,RenderTextureFormat.ARGB32);
        var previous=RenderTexture.active;
        Texture2D texture;
        try {
            Graphics.Blit(original.texture,target);RenderTexture.active=target;
            texture=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
        }
        finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        texture.name="Menu navigation shape";texture.filterMode=FilterMode.Bilinear;
        Directory.CreateDirectory("Assets/UI");AssetDatabase.CreateAsset(texture,ShapePath);
        var sprite=Sprite.Create(texture,original.rect,new Vector2(.5f,.5f),original.pixelsPerUnit,0,SpriteMeshType.FullRect,original.border);
        sprite.name="Menu navigation rounded rectangle";AssetDatabase.AddObjectToAsset(sprite,ShapePath);
        return sprite;
    }
}
#endif
