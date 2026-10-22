#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class ShuffleAuditValidation
{
    sealed class Host:MonoBehaviour{}
    public static bool Running {get;private set;}
    static readonly List<string> checks=new();
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static string output;
    static void Check(bool ok,string message){checks.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines(output+"/checks.txt",checks);if(!ok)throw new Exception(message);}
    public static void Run(string directory,string deckPath)
    {
        if(!Application.isPlaying||Running)throw new InvalidOperationException("Play required and one test at a time");
        output=directory;Directory.CreateDirectory(output);checks.Clear();Running=true;
        new GameObject("ShuffleAuditValidation").AddComponent<Host>().StartCoroutine(Guard(Exercise(deckPath)));
    }
    static IEnumerator Guard(IEnumerator test)
    {
        while(true){object step;try{if(!test.MoveNext())break;step=test.Current;}catch(Exception e){File.WriteAllText(output+"/result.txt","FAIL "+e);break;}yield return step;}
        (test as IDisposable)?.Dispose();Running=false;
    }
    static IEnumerator Exercise(string deckPath)
    {
        var deck=JsonUtility.FromJson<DeckDefinition>(File.ReadAllText(deckPath));var paths=new List<string>();
        for(int game=1;game<=2;game++)
        {
            var session=new OnlineMatchSession("shuffle-audit-editor-test",deck,deck,13,new OnlineRoomConfig{firstPlayerSeat=1},"audit-room",game);
            string temp=(string)typeof(OnlineMatchSession).GetField("configPath",Hidden).GetValue(session);
            try
            {
                var config=JObject.Parse(File.ReadAllText(temp));
                Check((string)config["shuffleAuditFile"]==session.ShuffleAuditFile,"Authority automatically supplies private audit path game "+game);
                Check((int)config["gameNumber"]==game&&(string)config["roomId"]=="audit-room","Authority serializes room and BO3 game identity "+game);
                File.Copy(temp,output+"/start-"+game+".json",true);
                float deadline=Time.realtimeSinceStartup+60;
                while(!session.Ready&&session.Failure==null&&Time.realtimeSinceStartup<deadline){session.Poll();yield return null;}
                Check(session.Ready&&session.Failure==null,"Production engine starts with audit enabled game "+game+": "+session.Failure);
                foreach(int seat in new[]{1,2,3})
                {
                    string view=JsonUtility.ToJson(session.GetView(seat));
                    Check(!view.Contains("shuffleAuditFile")&&!view.Contains(session.ShuffleAuditFile)&&!view.Contains("\"seed\"")&&!view.Contains("shuffle_before"),"Player/spectator projection omits audit metadata game="+game+" seat="+seat);
                }
                var timer=Stopwatch.StartNew();session.Dispose();timer.Stop();
                Check(timer.ElapsedMilliseconds<250,"Dispose is nonblocking game "+game+" millis="+timer.ElapsedMilliseconds);
                var shutdown=(Task)typeof(OnlineMatchSession).GetField("workerShutdown",Hidden).GetValue(session);deadline=Time.realtimeSinceStartup+5;
                while(!shutdown.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(shutdown.IsCompleted&&!shutdown.IsFaulted,"Background shutdown completes game "+game);
                Check(!Directory.Exists(Path.GetDirectoryName(temp)),"Temporary session cleanup happens after worker exit game "+game);
                Check(File.Exists(session.ShuffleAuditFile)&&File.Exists(session.ShuffleAuditFile+".status.json"),"Audit survives session disposal game "+game);
                var status=JObject.Parse(File.ReadAllText(session.ShuffleAuditFile+".status.json"));
                Check((bool)status["complete"]&&(string)status["state"]=="closed","Compressed audit finalized game "+game);
                File.Copy(session.ShuffleAuditFile,output+"/game-"+game+".jsonl.gz",true);File.Copy(session.ShuffleAuditFile+".status.json",output+"/game-"+game+".status.json",true);
                paths.Add(session.ShuffleAuditFile);
            }
            finally {session.Dispose();}
        }
        Check(paths.Distinct().Count()==2,"Same room/session label across two games gets distinct audit files");
        var field=typeof(OnlineMatchSession).GetField("ValidationBeforeStart",BindingFlags.Static|BindingFlags.NonPublic);var previous=field.GetValue(null);
        OnlineMatchSession hung=null;
        try
        {
            field.SetValue(null,new Action<ProcessStartInfo,string>((info,path)=>{info.FileName="python";info.Arguments="-c \"import time;time.sleep(60)\"";}));
            hung=new OnlineMatchSession("audit-hung-test",deck,deck,1,new OnlineRoomConfig{firstPlayerSeat=1});
            var timer=Stopwatch.StartNew();hung.Dispose();timer.Stop();Check(timer.ElapsedMilliseconds<250,"Hung worker does not block disposal");
            var shutdown=(Task)typeof(OnlineMatchSession).GetField("workerShutdown",Hidden).GetValue(hung);float deadline=Time.realtimeSinceStartup+5;
            while(!shutdown.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(shutdown.IsCompleted&&!shutdown.IsFaulted,"Hung worker is force-stopped within bounded background timeout");
        }
        finally {field.SetValue(null,previous);hung?.Dispose();}
        File.WriteAllText(output+"/result.txt","PASS "+checks.Count+" actual authority startup / two-game identity / private projections / async disposal / hung-worker checks");
    }
}
#endif
