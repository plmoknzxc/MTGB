#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Production session constructor/disposal with configs captured from real LAN lobby.
public static class OpeningSessionBoundaryValidation
{
    [Serializable] sealed class Room { public OnlineRoomConfig config; }
    static OnlineMatchSession session;
    static int round,decisions;
    static long lastSequence;
    static double deadline;
    public static bool Running { get; private set; }
    public static string Output { get; private set; }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run()
    {
        Check(!Running,"Already running");Running=true;round=0;
        Output=".tools/opening-boundaries-1007/session/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);
        File.WriteAllText(".tools/opening-boundaries-1007/session/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        try{Start();EditorApplication.update+=Tick;}catch(Exception e){Finish(e);throw;}
    }
    static void Start()
    {
        var ui=File.ReadAllText(".tools/opening-boundaries-1007/ui/latest.txt").Trim();Check(File.ReadAllText(ui+"/result.txt").StartsWith("PASS"),"Actual lobby proof missing");
        var json=File.ReadAllText(ui+"/room-"+round+".json");File.WriteAllText(Output+"/room-"+round+".json",json);
        var config=JsonUtility.FromJson<Room>(json).config;config.firstPlayerSeat=round+1;
        var deck=JsonUtility.FromJson<DeckDefinition>("{\"name\":\"Opening proof\",\"cards\":[{\"setCode\":\"10E\",\"collectorNumber\":\"376\",\"count\":60}],\"sideboard\":[]}");
        decisions=0;lastSequence=-1;deadline=EditorApplication.timeSinceStartup+45;
        session=new OnlineMatchSession("opening-session-"+Guid.NewGuid().ToString("N"),deck,deck,4,config);
    }
    static void Tick()
    {
        try {
            Check(EditorApplication.timeSinceStartup<deadline,"Opening session timeout");session.Poll();Check(session.Failure==null,"Native session failure: "+session.Failure);
            if(!session.Ready||session.AppliedSequence==lastSequence)return;lastSequence=session.AppliedSequence;
            var a=session.GetView(1);var b=session.GetView(2);int life=round==0?27:37;
            Check(a.matchId==session.MatchId&&b.matchId==session.MatchId,"Session identity mixed");
            Check(a.forge.players.All(p=>p.life==life)&&b.forge.players.All(p=>p.life==life),"Lobby life not passed through production constructor");
            Check(a.forge.players[1].hand.Length==0&&b.forge.players[0].hand.Length==0,"Production private hand leak");
            var view=a.forge.awaitingInput?a:b;var q=view.forge.request;Check(q!=null&&q.requestId>0,"No actionable opening request");
            File.WriteAllText(Output+"/round"+round+"-step"+decisions+"-seat1.json",JsonUtility.ToJson(a,true));File.WriteAllText(Output+"/round"+round+"-step"+decisions+"-seat2.json",JsonUtility.ToJson(b,true));
            if(round==0&&decisions==2){
                Check(q.seat==1&&q.kind=="InputConfirmMulligan","Cancelled opening failed to redraw proper seat");
                var old=session;var action=new GameAction{type=GameActionType.ConfirmChoice,playerId=q.seat,sourceObjectId=q.requestId,optionId=0};old.Dispose();Check(!old.Poll()&&!old.ApplyAction(action).accepted,"Disposed session still accepts input");
                File.AppendAllText(Output+"/steps.log","real mulligan then production Dispose; old input rejected PASS\n");round=1;Start();return;
            }
            if(round==1&&q.kind=="InputPassPriority"){
                Check(q.seat==2&&view.forge.turn==1&&view.forge.phase=="UPKEEP"&&decisions==2,"Restart first-seat/phase wrong");
                Check(a.forge.players[0].hand.Length==7&&b.forge.players[1].hand.Length==7&&a.forge.players.All(p=>p.libraryCount==53),"Restart inherited old mulligan state");
                File.AppendAllText(Output+"/steps.log","new production session life37/firstSeat2/private hands7/libraries53/UPKEEP PASS\n");Finish(null);return;
            }
            Check(q.kind=="InputConfirmMulligan"&&q.seat==(round==0?(decisions==0?1:2):(decisions==0?2:1)),"Unexpected decision order");
            string id=round==0&&decisions==0?"cancel":"ok";int option=Array.FindIndex(q.options,o=>o.id==id);Check(option>=0,"Missing native opening option");
            Check(session.ApplyAction(new GameAction{type=GameActionType.ConfirmChoice,playerId=q.seat,sourceObjectId=q.requestId,optionId=option}).accepted,"Production response rejected");decisions++;
        }catch(Exception e){Finish(e);Debug.LogException(e);}
    }
    static void Finish(Exception error)
    {
        EditorApplication.update-=Tick;session?.Dispose();session=null;Running=false;
        File.WriteAllText(Output+"/result.txt",error==null?"PASS: actual LAN life27/BO3 config through production OnlineMatchSession; real mulligan, dispose at pending opening, old inputs rejected; fresh actual LAN life37/BO1 config starts seat2, hand7/library53/privacy and first upkeep priority. Sequential session integration; no new GUI/transport or rule-first count.":"FAIL "+error);
    }
}
#endif
