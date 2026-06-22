using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ArenaPrototypeSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/MTGAPrototype.unity";

    [MenuItem("Tools/MTGB/Build MTGA Prototype Scene")]
    public static void BuildScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Directory.CreateDirectory("Assets/Scenes");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ArenaPrototypeSceneFactory.Build(false, true);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();

        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Debug.Log($"MTGB prototype scene generated: {ScenePath}");
        EditorUtility.DisplayDialog("MTGB", $"Generated scene:\n{ScenePath}", "OK");
    }

    [MenuItem("Tools/MTGB/Open MTGA Prototype Scene")]
    public static void OpenScene()
    {
        if (!File.Exists(ScenePath))
        {
            BuildScene();
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void AddSceneToBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Any(scene => scene.path == ScenePath)) return;

        EditorBuildSettings.scenes = scenes
            .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) })
            .ToArray();
    }
}
