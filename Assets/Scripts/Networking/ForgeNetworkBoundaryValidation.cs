#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Unity.Netcode;

/// <summary>Explicit command-line validation only. Uses the real service/transport/worker.</summary>
public sealed class ForgeNetworkBoundaryValidation : MonoBehaviour
{
    [Serializable] sealed class NativeStart {public string session,auditDir;public int seed,startingLife,firstPlayerSeat;public DeckDefinition[] decks;public Spec own,other;}
    [Serializable] sealed class Spec {public string[] Hand,Battlefield,Library;}
    [Serializable] sealed class Command {public int id,option;public string type,optionId,matchId,order;public long nativeId,clientId;public bool hold,drop;public GameAction action;}
    [Serializable] sealed class CommandResult {public int id;public bool ok,manualAccepted;public string error,state;public long beforeClientId,afterClientId;}
    [Serializable] sealed class Member {public int seat;public bool connected,ready,restoring;public long lastClientId;public double deadline;}
    [Serializable] sealed class Snapshot {public string role,state,hash,matchId;public int seat;public bool connected,inMatch;public double time;public long nextClientId;public OnlinePlayerViewState view,first,second;public Member[] members;public OnlineSeriesState series;public OnlineMatchEndedMessage ended;public int nativePid;}
    OnlineMatchService service;string role,root,room,profile;ushort port;bool submitted,starting,dropUpdates,holdReconnect;long nextState;int lastCommand;string lastKey;OnlineMatchEndedMessage ended;double nextLobbyAction;
    static object Field(object x,string n)=>x.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(x);
    static void Set(object x,string n,object v)=>x.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(x,v);
    static string Arg(string n)=>OnlineClientProfileStore.ReadArgument(n);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(!string.IsNullOrEmpty(Arg("-forge-boundaries")))new GameObject("ForgeNetworkBoundaryValidation").AddComponent<ForgeNetworkBoundaryValidation>();}
    void Start(){
        DontDestroyOnLoad(gameObject);role=Arg("-forge-boundaries");root=Arg("-forge-boundary-root");port=ushort.Parse(Arg("-forge-port"));room=Arg("-forge-room");profile="boundary-"+room+"-"+role;Directory.CreateDirectory(root);
        service=OnlineMatchService.EnsureInstance();service.ControlMode=ForgeControlMode.On;
        service.StatusChanged+=s=>File.AppendAllText(Path.Combine(root,role+"-status.txt"),Time.realtimeSinceStartupAsDouble+" "+s+"\n");service.MatchEnded+=m=>ended=m;
        if(role=="server"){
            OnlineMatchSession.ValidationBeforeStart=PrepareNative;if(!service.StartDedicatedServer(port))throw new Exception("Boundary server failed");
        }else{
            OnlineClientProfileStore.SaveReconnectToken(profile,string.Empty,room);
            OnlineMatchService.ValidationDropClientEnvelope=(s,e)=>{
                if(s!=service)return false;
                File.AppendAllText(Path.Combine(root,role+"-wire.jsonl"),JsonUtility.ToJson(e)+"\n");
                return dropUpdates&&e.type==OnlineProtocol.StateUpdate;
            };
            service.Connect("127.0.0.1",port,role,profile,room,new OnlineRoomConfig{bestOf=1,startingLife=40});
        }
    }
    void PrepareNative(System.Diagnostics.ProcessStartInfo info,string path){
        var config=JsonUtility.FromJson<NativeStart>(File.ReadAllText(path));config.auditDir=Path.Combine(root,"native-audit");Directory.CreateDirectory(config.auditDir);config.startingLife=40;config.firstPlayerSeat=1;
        config.own=new Spec{Hand=new[]{"Shock","Fact or Fiction","Mindslaver","Opt","Divination"},Battlefield=new[]{"Grizzly Bears","Hill Giant","War Mammoth"},Library=new[]{"Island","Plains","Swamp","Mountain","Forest","Island","Island"}};
        config.other=new Spec{Hand=new[]{"Divination","Opt","Brainstorm","Shock"},Battlefield=new[]{"Wall of Air","Merfolk Looter","Hill Giant"},Library=new[]{"Island","Forest","Island","Mountain","Plains","Swamp","Island"}};
        File.WriteAllText(path,JsonUtility.ToJson(config));File.Copy(path,Path.Combine(root,"native-start.json"),true);
        string classes=Arg("-forge-boundary-classes");if(!Directory.Exists(classes)||classes.Contains("\""))throw new Exception("Invalid fixture classpath");
        if(!info.Arguments.Contains(" mtgb.bridge.ForgeBridge "))throw new Exception("Native launcher changed");
        info.Arguments=info.Arguments.Replace("-cp \"","-Xmx768m -XX:ActiveProcessorCount=4 -cp \""+classes+";").Replace(" mtgb.bridge.ForgeBridge "," mtgb.bridge.CounterFlowFixture ");
        File.WriteAllText(Path.Combine(root,"native-command.txt"),info.Arguments);
    }
    void Raw<T>(string type,T value){typeof(OnlineMatchService).GetMethod("SendToServer",BindingFlags.Instance|BindingFlags.NonPublic).MakeGenericMethod(typeof(T)).Invoke(service,new object[]{type,value});}
    void Update(){
        if(service==null)return;
        try{
            if(holdReconnect&&!service.IsConnected)Set(service,"automaticReconnectEnabled",false);
            if(role!="server"&&service.HasJoinedLobby&&!service.IsInMatch&&ended==null&&Time.realtimeSinceStartupAsDouble>=nextLobbyAction){
                nextLobbyAction=Time.realtimeSinceStartupAsDouble+0.15;
                if(!submitted){var deck=new DeckDefinition{name="Boundary initial fixture",cards=new List<DeckEntry>{new DeckEntry{setCode="10E",collectorNumber="376",count=60}}};service.SubmitDeck(deck);submitted=true;}
                var own=service.LobbyState.players.FirstOrDefault(p=>p.seat==service.LocalSeat);if(own?.hasDeck==true&&!own.ready)service.SetReady(true);
                if(!starting&&service.LocalSeat==service.LobbyState.hostSeat&&service.LobbyState.players.Count==2&&service.LobbyState.players.All(p=>p.ready)){service.RequestStartMatch();starting=true;}
            }
            string command=Path.Combine(root,role+"-command.json");if(File.Exists(command)){
                Command c=null;try{c=JsonUtility.FromJson<Command>(File.ReadAllText(command));}catch(IOException){}
                if(c!=null&&c.id>lastCommand){lastCommand=c.id;Execute(c);}
            }
            if(Time.frameCount%3==0)SaveState();
        }catch(Exception e){File.WriteAllText(Path.Combine(root,role+"-fatal.txt"),e.ToString());enabled=false;}
    }
    void Execute(Command c){
        var result=new CommandResult{id=c.id,ok=true,beforeClientId=(long)Field(service,"nextRequestId")};
        try{
            if(c.type=="choose"||c.type=="race"){
                var r=service.ForgeView?.forge?.request;if(r==null||r.requestId!=c.nativeId)throw new Exception("Changed native request");
                int option=Array.FindIndex(r.options,o=>o.id==c.optionId&&!o.disabled);if(option<0)throw new Exception("Original option unavailable");
                var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
                if(c.type=="race"){
                    service.ControlMode=ForgeControlMode.Off;Set(ui,"automaticRequest",r.requestId);Set(ui,"automaticReadyAt",Time.unscaledTime-1);
                    var auto=typeof(ForgeBattlefieldController).GetMethod("UpdateAutomaticControl",BindingFlags.Instance|BindingFlags.NonPublic);
                    if(c.order=="auto-first")auto.Invoke(ui,null);
                    result.manualAccepted=ui.TrySubmitPresentedOption(option,r.requestId);
                    auto.Invoke(ui,null);service.ControlMode=ForgeControlMode.On;
                }else result.manualAccepted=ui.TrySubmitPresentedOption(option,r.requestId);
                if(c.type=="choose"&&!result.manualAccepted)throw new Exception("Presented option not submitted");
            }else if(c.type=="raw"){
                Raw(OnlineProtocol.SubmitAction,new OnlineActionMessage{matchId=c.matchId??service.MatchId,requestId=c.clientId,action=c.action});Set(service,"nextRequestId",Math.Max((long)Field(service,"nextRequestId"),c.clientId+1));
            }else if(c.type=="drop-updates")dropUpdates=c.drop;
            else if(c.type=="disconnect"){
                holdReconnect=c.hold;if(c.hold)Set(service,"automaticReconnectEnabled",false);
                ((NetworkManager)Field(service,"networkManager")).Shutdown();
            }else if(c.type=="reconnect"){holdReconnect=false;service.Connect("127.0.0.1",port,role,profile,room,new OnlineRoomConfig{bestOf=1,startingLife=40});}
            else if(c.type=="resync")Raw(OnlineProtocol.RequestResync,new OnlineHashReportMessage());
            else if(c.type=="quit"){service.Disconnect();Application.Quit(0);}
            else throw new Exception("Unknown test command");
        }catch(Exception e){result.ok=false;result.error=e.ToString();}
        result.afterClientId=(long)Field(service,"nextRequestId");result.state=service.InteractionState.ToString();File.WriteAllText(Path.Combine(root,role+"-result-"+c.id+".json"),JsonUtility.ToJson(result,true));SaveState();
    }
    void SaveState(){
        var s=new Snapshot{role=role,seat=service.LocalSeat,connected=service.IsConnected,inMatch=service.IsInMatch,state=service.InteractionState.ToString(),time=Time.realtimeSinceStartupAsDouble,view=service.ForgeView,hash=service.ForgeView?.viewHash,matchId=service.MatchId,ended=ended,nextClientId=(long)Field(service,"nextRequestId")};
        if(role=="server"){
            var registry=(OnlineRoomRegistry)Field(service,"roomRegistry");var r=registry.Rooms.FirstOrDefault(x=>x.roomId==OnlineRoomRegistry.NormalizeRoomId(room));
            if(r!=null){s.first=r.match?.GetView(1);s.second=r.match?.GetView(2);s.series=r.series;s.members=r.members.Values.Select(m=>new Member{seat=m.seat,connected=m.connected,ready=m.sceneReady,restoring=m.restoring,lastClientId=m.requestGate.LastRequestId,deadline=m.reconnectWindow.Deadline}).ToArray();if(r.match!=null)s.nativePid=((System.Diagnostics.Process)Field(r.match,"process")).Id;}
        }
        string json=JsonUtility.ToJson(s);File.WriteAllText(Path.Combine(root,role+"-state.tmp"),json);File.Copy(Path.Combine(root,role+"-state.tmp"),Path.Combine(root,role+"-state.json"),true);
        string key=(s.view?.viewHash??s.first?.viewHash??"")+s.state+s.connected+s.inMatch+JsonUtility.ToJson(s.series)+string.Join("|",(s.members??Array.Empty<Member>()).Select(m=>JsonUtility.ToJson(m)));if(key!=lastKey){lastKey=key;File.WriteAllText(Path.Combine(root,role+"-snapshot-"+(nextState++)+".json"),json);}
    }
    void OnDestroy(){if(role=="server")OnlineMatchSession.ValidationBeforeStart=null;else OnlineMatchService.ValidationDropClientEnvelope=null;}
}
#endif
