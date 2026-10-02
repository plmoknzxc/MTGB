using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mtgb.ForgeProof;

public static class ProofBuild
{
    public static void Prepare()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .1f, .13f);
        new GameObject("Forge Proof").AddComponent<ForgeProofPanel>();
        EditorSceneManager.SaveScene(scene, "Assets/ForgeProof.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/ForgeProof.unity", true) };
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultScreenWidth = 1100; PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        AssetDatabase.SaveAssets();
    }
    public static void Build()
    {
        Prepare();
        string target = ForgeProofPanel.Arg("-proof-build");
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/ForgeProof.unity" }, locationPathName = target, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Proof player build failed");
    }
    public static void Play()
    {
        Prepare(); SessionState.SetBool("ForgeProofRunning", true); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    static void Watch()
    {
        EditorApplication.update += () =>
        {
            if (!SessionState.GetBool("ForgeProofRunning", false)) return;
            if (EditorApplication.isPlaying && ForgeProofPanel.Finished)
            {
                SessionState.SetBool("ForgeProofRunning", false);
                bool passed = ForgeProofPanel.Passed;
                EditorApplication.ExitPlaymode();
                EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
            }
        };
    }
}
