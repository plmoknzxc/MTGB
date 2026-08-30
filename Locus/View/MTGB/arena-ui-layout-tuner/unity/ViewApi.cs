using System;
using UnityEditor;
using UnityEngine;

public static class ArenaUiLayoutTunerApi
{
    private const string ProfilePath = "Assets/Settings/ArenaMatchLayoutProfile.asset";
    private const string MatchScenePath = "Assets/Scenes/MTGAPrototype.unity";

    [Serializable]
    public sealed class ApplyArgs
    {
        public string json;
        public bool saveScene;
    }

    [Serializable]
    private sealed class Response
    {
        public bool ok;
        public string message;
        public string json;
    }

    public static object Read()
    {
        ArenaMatchLayoutController controller = FindController();
        ArenaMatchLayoutProfile profile = FindProfile(controller);
        if (controller == null && profile == null)
        {
            return Error("未找到对局布局控制器或配置资产。请先打开 MTGAPrototype 对局场景。");
        }

        return Success(
            controller != null ? controller.GetLayoutJson() : profile.GetLayoutJson(),
            controller != null ? "已读取当前对局布局。" : "已读取布局配置资产；进入对局后会自动应用。");
    }

    public static object Apply(ApplyArgs args)
    {
        if (args == null || string.IsNullOrWhiteSpace(args.json))
        {
            throw new Exception("缺少布局 JSON。");
        }

        ArenaMatchLayoutController controller = FindController();
        ArenaMatchLayoutProfile profile = FindProfile(controller);
        if (controller == null && profile == null)
        {
            throw new Exception("未找到对局布局控制器或配置资产。");
        }

        if (profile != null)
        {
            Undo.RecordObject(profile, "调整 Arena 对局布局");
            profile.ApplyLayoutJson(args.json);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }

        if (controller != null)
        {
            Undo.RecordObject(controller, "应用 Arena 对局布局");
            controller.ApplyLayoutJson(args.json);
            EditorUtility.SetDirty(controller);
            if (args.saveScene && !EditorApplication.isPlaying && controller.gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(controller.gameObject.scene);
            }
        }

        string json = profile != null ? profile.GetLayoutJson() : controller.GetLayoutJson();
        return Success(json, EditorApplication.isPlaying
            ? "已实时应用并保存到布局配置资产。"
            : args.saveScene ? "已应用并保存配置与场景。" : "已应用并保存到布局配置资产。");
    }

    public static object Reset()
    {
        ArenaMatchLayoutController controller = FindController();
        ArenaMatchLayoutProfile profile = FindProfile(controller);
        if (controller == null && profile == null)
        {
            throw new Exception("未找到对局布局控制器或配置资产。");
        }

        if (profile != null)
        {
            Undo.RecordObject(profile, "重置 Arena 对局布局");
            profile.ResetLayout();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }

        if (controller != null) controller.ApplyAll();
        return Success(profile != null ? profile.GetLayoutJson() : controller.GetLayoutJson(), "已恢复默认对局布局。");
    }

    private static ArenaMatchLayoutController FindController()
    {
        return UnityEngine.Object.FindFirstObjectByType<ArenaMatchLayoutController>();
    }

    private static ArenaMatchLayoutProfile FindProfile(ArenaMatchLayoutController controller)
    {
        if (controller != null && controller.Profile != null) return controller.Profile;
        return AssetDatabase.LoadAssetAtPath<ArenaMatchLayoutProfile>(ProfilePath);
    }

    private static Response Success(string json, string message)
    {
        return new Response { ok = true, message = message, json = json };
    }

    private static Response Error(string message)
    {
        return new Response { ok = false, message = message, json = string.Empty };
    }
}
