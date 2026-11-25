using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal sealed class TidelightsThemeBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPostprocessBuild(BuildReport report)
    {
        if(report.summary.platform!=BuildTarget.StandaloneWindows64 ||
           report.summary.GetSubtarget<StandaloneBuildSubtarget>()==StandaloneBuildSubtarget.Server)return;
        string target=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report.summary.outputPath)),"Themes");
        bool firstInstall=!Directory.Exists(target);
        Directory.CreateDirectory(target);
        string source=Path.GetFullPath("Themes/Tidelights"), shipped=Path.Combine(target,"Tidelights");
        if(!Directory.Exists(source)||(!firstInstall&&!Directory.Exists(shipped))||
           string.Equals(source,shipped,StringComparison.OrdinalIgnoreCase))return;
        Directory.CreateDirectory(shipped);
        Directory.CreateDirectory(Path.Combine(shipped,"audio"));
        foreach(var file in new[]{"art.bundle","theme.json","audio/RainLanterns.ogg","audio/Tidewatch.ogg","audio/CREDITS.txt"})
            File.Copy(Path.Combine(source,file),Path.Combine(shipped,file),true);
    }

    [MenuItem("MTGB/Themes/Rebuild Tidelights artwork")]
    public static void Build()
    {
        const string source="Assets/ThemeSources/Tidelights";
        const string output="Library/ThemeBundles/Tidelights";
        const string name="mtgb-tidelights";
        var files=AssetDatabase.FindAssets("",new[]{source}).Select(AssetDatabase.GUIDToAssetPath)
            .Where(File.Exists).OrderBy(path=>path,StringComparer.Ordinal).ToArray();
        if(files.Length==0)throw new InvalidOperationException("找不到潮灯主题的美术资源。");
        var definitions=new[]{new AssetBundleBuild { assetBundleName=name, assetNames=files,
            addressableNames=files.Select(p=>"art/"+Path.GetFileNameWithoutExtension(p)).ToArray() }};
        Directory.CreateDirectory(output);
        var manifest=BuildPipeline.BuildAssetBundles(output,definitions,
            BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64);
        if(manifest==null)throw new InvalidOperationException("潮灯资源包构建失败。");
        Directory.CreateDirectory("Themes/Tidelights");
        File.Copy(Path.Combine(output,name),"Themes/Tidelights/art.bundle",true);
        Debug.Log("已更新 Themes/Tidelights/art.bundle");
    }
}
