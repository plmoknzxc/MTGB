#if UNITY_EDITOR
using System;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

public static class MatchMemoryValidation
{
    private static readonly Type Snapshot=typeof(MatchReplay).Assembly.GetType("ServerSnapshotJson");
    private static string Json(object value)=>(string)Snapshot.GetMethod("Serialize").Invoke(null,new[]{value});
    private static T Read<T>(string json)=>(T)Snapshot.GetMethod("Deserialize").MakeGenericMethod(typeof(T)).Invoke(null,new object[]{json});
    public static string Result { get; private set; }
    [MenuItem("MTGB/验证/性能/长局录像与快照")]
    public static void Run()
    {
        string root=Path.GetFullPath(".tools/server-memory-1015");Directory.CreateDirectory(root);
        string temporary=Path.Combine(root,Guid.NewGuid().ToString("N"));
        using var input=File.OpenRead("Tools/Validation/Fixtures/ReplayMemoryFrame.json.gz");
        using var zip=new System.IO.Compression.GZipStream(input,System.IO.Compression.CompressionMode.Decompress);
        using var text=new StreamReader(zip,Encoding.UTF8);
        var frame=Read<ReplayFrame>(text.ReadToEnd());
        var checks=new System.Collections.Generic.List<string>();
        void Check(bool valid,string label){if(!valid)throw new InvalidOperationException(label);checks.Add(label);}
        try
        {
            var view=new OnlinePlayerViewState{matchId="hash-test",viewerSeat=1,appliedSequence=53,forge=frame.views[0]};
            string LegacyHash()
            {
                var fields=JObject.Parse(Json(view));fields["viewHash"]="";
                using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(fields.ToString(Formatting.None)))).Replace("-","");
            }
            view.SealHash();Check(view.viewHash==LegacyHash(),"Streaming hash equals existing wire hash");
            Check(view.HasValidHash(),"New hash validates own view");
            var copied=Read<OnlinePlayerViewState>(Json(view));Check(copied.HasValidHash(),"Wire reader and copied view retain valid hashes");
            int turn=view.forge.turn;view.forge.turn++;
            Check(!view.HasValidHash()&&copied.HasValidHash(),"Mutation is detected and public copies stay isolated");view.forge.turn=turn;
            var warm=new MatchReplayRecorder("warm",temporary);warm.Record(0,frame.views[0],frame.views[1]);warm.Dispose();
            using var recorder=new MatchReplayRecorder("long-match",temporary);
            recorder.Record(0,frame.views[0],frame.views[1]);
            long before=GC.GetTotalMemory(true);var clock=Stopwatch.StartNew();
            for(int i=1;i<=1000;i++)recorder.Record(i*.01,frame.views[0],frame.views[1]);
            clock.Stop();long retained=GC.GetTotalMemory(true)-before;
            Check(retained<8*1024*1024,"1000 frames retain less than 8 MiB");
            string original=Json(frame.views[0]);
            var request=frame.views[0].request;frame.views[0].request=null;
            using(var recording=recorder.Complete(new[]{"Player 1","Player 2"},2,3,2,"test"))
            {
                var bytes=recording.Encode();var replay=ReplayRepository.Decode(bytes);
                Check(recording.FrameCount==1001&&replay.frames.Count==1001,"Every recorded frame survives streaming encode");
                Check(Json(replay.frames[0].views[0])==original,"Recorded views precede later request mutation");
                Check(replay.gameNumber==2&&replay.bestOf==3&&replay.winnerSeat==2&&replay.Duration==10,"Header and full timeline round trip");
                Check(ReplayRepository.FrameAt(replay,double.MaxValue)==1000,"Timeline seeking remains compatible");
            }
            frame.views[0].request=request;
            Check(Directory.GetFiles(temporary).Length==0,"Completed recording releases temporary file");
            using(var abandoned=new MatchReplayRecorder("abandoned",temporary)){abandoned.Record(0,frame.views[0],frame.views[1]);}
            Check(Directory.GetFiles(temporary).Length==0,"Abandoned recording releases temporary file");
            string denied=Path.Combine(temporary,"file");File.WriteAllText(denied,"fixture");
            using(var failed=new MatchReplayRecorder("failed",denied))
            {
                failed.Record(0,frame.views[0],frame.views[1]);Check(failed.Failure!=null,"Disk failure disables recording without throwing into match");
            }
            using(var limited=new MatchReplayRecorder("limited",temporary))
            {
                typeof(MatchReplayRecorder).GetField("jsonBytes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(limited,256L*1024*1024);
                limited.Record(0,frame.views[0],frame.views[1]);
                Check(limited.Failure!=null,"Recording capacity limit does not throw into match");
            }
            var result=new{success=true,checks,frames=1000,retainedMiB=retained/1048576.0,recordMs=clock.Elapsed.TotalMilliseconds};
            Result=JsonConvert.SerializeObject(result);File.WriteAllText(Path.Combine(root,"after.json"),Result);
            UnityEngine.Debug.Log(Result);
        }
        catch(Exception error){Result="FAIL: "+error;File.WriteAllText(Path.Combine(root,"failure.txt"),Result);throw;}
        finally
        {
            if(Directory.Exists(temporary))Directory.Delete(temporary,true);
        }
    }
}
#endif
