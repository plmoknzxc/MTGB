#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Applies to every player build, including direct BuildPipeline and server builds.
public sealed class CardFontBuildGuard : IPreprocessBuildWithReport
{
    private const string FontRoot = "Assets/Resources/UI/CardTemplates/Fonts";
    private const string PolicyPath = "Tools/CardRendering/banned-fonts.json";
    [Serializable] private sealed class DeniedFont { public string family, sha256; public string[] aliases; }
    [Serializable] private sealed class Policy { public DeniedFont[] fonts; }
    [Serializable] private sealed class ApprovedFile { public string file, sha256; }
    [Serializable] private sealed class Manifest { public ApprovedFile[] files; }
    public int callbackOrder => -10000;

    public void OnPreprocessBuild(BuildReport report) => Validate();

    [MenuItem("MTGB/校验/字体许可与禁用字体")]
    public static void Validate()
    {
        var policy = ReadPolicy();
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(FontRoot + "/open-fonts.json"));
        if (manifest?.files == null || manifest.files.Length == 0)
            throw new BuildFailedException("缺少已审核字体清单。");
        foreach (var item in manifest.files)
        {
            if (string.IsNullOrEmpty(item.file) || Path.GetFileName(item.file) != item.file
                || string.IsNullOrEmpty(item.sha256))
                throw new BuildFailedException("字体清单包含无效路径或哈希。");
            string path = Path.Combine(FontRoot, item.file);
            if (!File.Exists(path) || Hash(path) != item.sha256)
                throw new BuildFailedException("字体／许可证不匹配已审核版本：" + path);
        }
        foreach (string path in Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories).Where(IsFont))
        {
            ValidateFile(path, policy);
            if (path.Replace('\\', '/').StartsWith(FontRoot + "/", StringComparison.Ordinal)
                && !manifest.files.Any(item => item.file == Path.GetFileName(path)))
                throw new BuildFailedException("模板目录存在未审核字体：" + path);
            var font = AssetDatabase.LoadAssetAtPath<Font>(path.Replace('\\', '/'));
            if (font != null)
                foreach (string name in font.fontNames)
                    if (policy.fonts.Any(item => item.aliases.Any(alias => name.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0)))
                        throw new BuildFailedException("禁止使用旧字体家族：" + path + " (" + name + ")");
        }
        Debug.Log("MTGB 字体许可校验通过。");
    }

    public static void ValidateFile(string path) => ValidateFile(path, ReadPolicy());
    private static void ValidateFile(string path, Policy policy)
    {
        string hash = Hash(path);
        var denied = policy.fonts.FirstOrDefault(item => item.sha256 == hash);
        if (denied != null)
            throw new BuildFailedException("禁止重新引入旧字体：" + denied.family + "，文件：" + path);
    }
    private static Policy ReadPolicy()
    {
        var policy = JsonUtility.FromJson<Policy>(File.ReadAllText(PolicyPath));
        if (policy?.fonts == null || policy.fonts.Length != 5
            || policy.fonts.Any(item => string.IsNullOrEmpty(item.sha256) || item.aliases == null || item.aliases.Length == 0))
            throw new BuildFailedException("缺少完整禁用字体清单。");
        return policy;
    }
    internal static bool IsFont(string path)
        => new[] { ".otf", ".ttf", ".ttc", ".woff", ".woff2" }.Contains(Path.GetExtension(path).ToLowerInvariant());
    private static string Hash(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
}

public sealed class BannedFontImportGuard : AssetPostprocessor
{
    private void OnPreprocessAsset()
    {
        if (CardFontBuildGuard.IsFont(assetPath)) CardFontBuildGuard.ValidateFile(assetPath);
    }
}
#endif
