#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Editor-only file transport for the live native fixture. Production card
// components and choices remain exactly the same as the normal match consumer.
public static class ComponentCardFaceDemo
{
    static string Root=".tools/component-card-1007/demo/";
    static long stamp;
    static ForgeBattlefieldController ui;
    static ForgeBattlefieldMigrationNotice notice;
    static bool noticeEnabled;
    public static bool Running { get; private set; }
    public static void Start(string root=".tools/component-card-1007/demo/")
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play mode required");
        if(Running)Stop();Root=root.EndsWith("/")?root:root+"/";
        ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ComponentFaceDemo").AddComponent<ForgeBattlefieldController>();
        Directory.CreateDirectory(Root);stamp=0;Running=true;
        notice=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();
        noticeEnabled=notice!=null&&notice.enabled;if(notice!=null)notice.enabled=false;
        AssemblyReloadEvents.beforeAssemblyReload-=Stop;AssemblyReloadEvents.beforeAssemblyReload+=Stop;
        EditorApplication.playModeStateChanged-=ModeChanged;EditorApplication.playModeStateChanged+=ModeChanged;
        EditorApplication.update-=Poll;EditorApplication.update+=Poll;
    }
    public static void Stop()
    {
        if(!Running)return;
        EditorApplication.update-=Poll;Running=false;
        AssemblyReloadEvents.beforeAssemblyReload-=Stop;EditorApplication.playModeStateChanged-=ModeChanged;
        if(notice!=null)notice.enabled=noticeEnabled;
        Directory.CreateDirectory(Root);File.WriteAllText(Root+"stop.json","{}");
    }
    static void ModeChanged(PlayModeStateChange mode){if(mode==PlayModeStateChange.ExitingPlayMode)Stop();}
    static void Poll()
    {
        if(!Application.isPlaying||ui==null){Stop();return;}
        string path=Root+"view.json";if(!File.Exists(path))return;
        long next=File.GetLastWriteTimeUtc(path).Ticks;if(next==stamp)return;
        ForgeSeatView state;
        try{state=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));}
        catch(IOException){return;}
        stamp=next;
        ui.Present(state,1,state.awaitingInput,index=>{
            if(state.request==null||index<0||index>=state.request.options.Length)return;
            string temporary=Root+"action.tmp",target=Root+"action.json";
            File.WriteAllText(temporary,JsonUtility.ToJson(new DemoAction{requestId=state.request.requestId,option=state.request.options[index].id}));
            if(File.Exists(target))File.Delete(target);File.Move(temporary,target);
        },Stop,"");
    }
    [Serializable] sealed class DemoAction { public long requestId; public string option; }
}
#endif
