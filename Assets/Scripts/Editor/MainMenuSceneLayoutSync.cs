#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Persist the start screen through the same layout used at runtime. No card
// catalog, local match, saved deck or network service is initialized here.
public static class MainMenuSceneLayoutSync
{
    [MenuItem("MTGB/UI/Sync Main Menu Scene Layout")]
    public static void Apply()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(Application.isPlaying||scene.path!="Assets/Scenes/Main.unity")
            throw new InvalidOperationException("Open Main.unity outside Play mode before syncing its layout.");
        var start=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RectTransform>(true))
            .Single(r=>r.name=="StartScreen");
        MenuVisualTheme.ApplyStartScreen(start);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

}
#endif
