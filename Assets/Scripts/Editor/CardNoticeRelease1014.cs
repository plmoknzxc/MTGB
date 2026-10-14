#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;

// One explicitly requested release, with a reload-safe platform handoff.
public static class CardNoticeRelease1014
{
    const string Pending="MTGB.CardNoticeRelease1014.Pending";
    const string Out=".tools/card-notice-layout-1014";
    static bool running;
    [InitializeOnLoadMethod] static void Reload(){if(SessionState.GetString(Pending,"")!="")EditorApplication.delayCall+=Resume;}
    public static void Queue(string job)
    {
        if(EditorApplication.isPlaying||running||SessionState.GetString(Pending,"")!="")throw new InvalidOperationException("Build requires idle edit mode");
        if(job!="client"&&job!="server"&&job!="restore")throw new ArgumentException("Unknown release job");
        Directory.CreateDirectory(Out);SessionState.SetString(Pending,job);File.WriteAllText(Out+"/"+job+"-build.txt","Queued");EditorApplication.delayCall+=Resume;
    }
    static void Resume()
    {
        string job=SessionState.GetString(Pending,"");if(job==""||running)return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Resume;return;}
        try {
            bool server=job=="server";var target=server?BuildTarget.StandaloneLinux64:BuildTarget.StandaloneWindows64;
            EditorUserBuildSettings.standaloneBuildSubtarget=server?StandaloneBuildSubtarget.Server:StandaloneBuildSubtarget.Player;
            if(EditorUserBuildSettings.activeBuildTarget!=target){
                File.WriteAllText(Out+"/"+job+"-build.txt","Switching platform");
                if(!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone,target))throw new InvalidOperationException("Platform switch failed");
                EditorApplication.delayCall+=Resume;return;
            }
            running=true;SessionState.EraseString(Pending);File.WriteAllText(Out+"/"+job+"-build.txt","Building");
            if(server)DedicatedServerBuildMenu.Build("Builds/LinuxServerUpdate1014/MTGBServer",target,Path.GetFullPath("Builds/LinuxServerInteractionFixV19/ForgeRuntime/jre"));
            else if(job=="client")ForgeBuildMenu.BuildReleasePlayer("Builds/ClientUpdate1014");
            File.WriteAllText(Out+"/"+job+"-build.txt","Succeeded");
        }catch(Exception e){SessionState.EraseString(Pending);File.WriteAllText(Out+"/"+job+"-build.txt","Failed\n"+e);UnityEngine.Debug.LogException(e);}
        finally{running=false;}
    }
}
#endif
