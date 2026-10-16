#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;

// A real native match advances ordinary priority/cleanup requests; repeated
// frozen views are tested separately by MatchMemoryValidation.
public static class LongMatchMemoryValidation
{
    private static OnlineMatchSession session;
    private static readonly MethodInfo Borrow=typeof(OnlineMatchSession).GetMethod("GetViewForTransport",BindingFlags.Instance|BindingFlags.NonPublic);
    private static readonly List<double> latency=new();
    private static int actions;
    private static double deadline,submittedAt=-1;
    private static bool conceding;
    private static double maxPoll;
    public static string Result { get; private set; }
    public static void Run()
    {
        if(session!=null)throw new InvalidOperationException("Already running");
        var deck=new DeckDefinition{name="Long-match memory fixture"};
        foreach(var card in CardDatabaseRepository.LoadAll().Where(c=>c.cardName=="Forest"&&!c.forgeAmbiguous).Take(2))
            deck.cards.Add(new DeckEntry{setCode=card.setCode,collectorNumber=card.CollectorNumber,count=250});
        actions=0;maxPoll=0;submittedAt=-1;conceding=false;latency.Clear();Result="RUNNING";
        session=new OnlineMatchSession(Guid.NewGuid().ToString("N"),deck,deck,42);
        deadline=EditorApplication.timeSinceStartup+180;
        EditorApplication.update+=Tick;
    }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Long match stalled after "+actions+" actions");
            var timer=Stopwatch.StartNew();bool changed=session.Poll();timer.Stop();maxPoll=Math.Max(maxPoll,timer.Elapsed.TotalMilliseconds);
            if(session.Failure!=null)throw new InvalidOperationException(session.Failure);
            if(changed&&submittedAt>=0){latency.Add((EditorApplication.timeSinceStartup-submittedAt)*1000);submittedAt=-1;}
            var first=(OnlinePlayerViewState)Borrow.Invoke(session,new object[]{1});
            if(first.outcome.isFinished)
            {
                if(actions<1000)throw new InvalidOperationException("Fixture ended before the long-match boundary");
                using var replay=session.CompleteReplay(new[]{"Player 1","Player 2"},1,1,2,"Long-match memory regression");
                string root=Path.GetFullPath(".tools/server-memory-1015");Directory.CreateDirectory(root);
                var bytes=replay.Encode();string path=Path.Combine(root,"long-match.mtgbr");File.WriteAllBytes(path,bytes);
                var preview=ReplayRepository.ReadPreview(path);
                double P95(IEnumerable<double> values){var ordered=values.OrderBy(x=>x).ToArray();return ordered.Length==0?0:ordered[(int)((ordered.Length-1)*.95)];}
                double early=P95(latency.Take(100)),late=P95(latency.Skip(Math.Max(0,latency.Count-100)));
                if(late>Math.Max(1000,early*3))throw new InvalidOperationException("Late-game response latency regressed: "+late);
                var report=new{success=true,actions,frames=replay.FrameCount,compressedBytes=bytes.Length,earlyP95Ms=early,lateP95Ms=late,maxPollMs=maxPoll,previewDuration=preview.duration,
                    scope="Native priority/cleanup/concession and streaming recording; not a WAN or rules-coverage claim"};
                Result=Newtonsoft.Json.JsonConvert.SerializeObject(report);File.WriteAllText(Path.Combine(root,"long-match.json"),Result);Finish();return;
            }
            if(submittedAt>=0||!session.Ready)return;
            if(actions>=1000)
            {
                if(!conceding){if(!session.ApplyAction(new GameAction{type=GameActionType.Concede,playerId=1}).accepted)throw new InvalidOperationException("Concession rejected");conceding=true;submittedAt=EditorApplication.timeSinceStartup;}
                return;
            }
            for(int seat=1;seat<=2;seat++)
            {
                var view=seat==1?first:(OnlinePlayerViewState)Borrow.Invoke(session,new object[]{seat});
                if(view.forge?.awaitingInput!=true)continue;
                var request=view.forge.request;
                int option=Array.FindIndex(request.options,o=>!o.disabled&&(o.id=="ok"||o.id=="confirm-cards"||o.id=="confirm-order"));
                if(option<0)option=Array.FindIndex(request.options,o=>!o.disabled&&!o.selected);
                if(option<0)throw new InvalidOperationException("No usable option for "+request.kind);
                var action=new GameAction{type=GameActionType.ConfirmChoice,playerId=seat,sourceObjectId=request.requestId,optionId=option};
                if(!session.ApplyAction(action).accepted)throw new InvalidOperationException("Action rejected: "+request.kind);
                actions++;submittedAt=EditorApplication.timeSinceStartup;break;
            }
        }
        catch(Exception error)
        {
            Result="FAIL: "+error;Directory.CreateDirectory(".tools/server-memory-1015");File.WriteAllText(".tools/server-memory-1015/long-match-failure.txt",Result);Finish();
        }
    }
    private static void Finish(){EditorApplication.update-=Tick;session?.Dispose();session=null;UnityEngine.Debug.Log(Result);}
}
#endif
