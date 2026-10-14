#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

// Scoped rebuild after a managed-only wire correction. Native runtimes in
// these previously verified outputs are reused; never touches the desktop.
public static class InteractionSnapshotBuildDelta
{
    public static void Run()
    {
        bool server=Array.IndexOf(Environment.GetCommandLineArgs(),"-snapshot-server")>=0;
        string root=Path.GetFullPath(server?"Builds/LinuxServerInteractionFixV19":"Builds/ClientInteractionFixV19");
        if(!File.Exists(Path.Combine(root,"ForgeRuntime/runtime.json")))throw new InvalidOperationException("Verified runtime output missing");
        var addressables=AddressableAssetSettingsDefaultObject.Settings;
        var previous=addressables.BuildAddressablesWithPlayerBuild;
        BuildReport report;
        try {
        if(server)addressables.BuildAddressablesWithPlayerBuild=AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
        report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=server?new[]{"Assets/Scenes/DedicatedServer.unity"}:new[]{"Assets/Scenes/Main.unity","Assets/Scenes/IntegratedBattlefield.unity"},
            locationPathName=Path.Combine(root,server?"MTGBServer":"MTGB.exe"),
            target=server?BuildTarget.StandaloneLinux64:BuildTarget.StandaloneWindows64,
            subtarget=(int)(server?StandaloneBuildSubtarget.Server:StandaloneBuildSubtarget.Player),options=BuildOptions.None
        });
        } finally {addressables.BuildAddressablesWithPlayerBuild=previous;}
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Snapshot delta build failed");
        // Deployment excludes presentation-only StreamingAssets. Keep local
        // generated files; deletion was blocked by automatic approval.
        UnityEngine.Debug.Log("SNAPSHOT DELTA BUILT "+root);
    }
}
#endif
