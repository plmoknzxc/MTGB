#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// Explicit Editor validation: real loopback clients, authority service and Forge AI.
public sealed class OnlineAiValidation : MonoBehaviour
{
    static string Root;
    const ushort Port=18794;
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    OnlineMatchService service; OnlineRoomState room;
    readonly List<Probe> probes=new(); readonly List<string> checks=new();
    float heartbeat; bool cleaned;
    sealed class Probe
    {
        public NetworkManager net; public OnlineWelcomeMessage welcome;
        public OnlineLobbyState lobby; public OnlinePlayerViewState view;
        public OnlineMatchEndedMessage ended; public string error,rejection; public bool replay;
        public long sequence=1,lastNative;
        public void Send<T>(string type,T payload)
        {
            var envelope=OnlineEnvelopeCodec.Encode(type,JsonUtility.ToJson(payload));
            string json=JsonUtility.ToJson(envelope);
            using var writer=new FastBufferWriter(json.Length*2+32,Allocator.Temp);writer.WriteValueSafe(json);
            net.CustomMessagingManager.SendNamedMessage(OnlineProtocol.MessageName,0,writer,NetworkDelivery.ReliableFragmentedSequenced);
        }
        public void Choose(int option)
        {
            var r=view.forge.request;lastNative=r.requestId;
            Send(OnlineProtocol.SubmitAction,new OnlineActionMessage{matchId=view.matchId,requestId=sequence++,action=new GameAction{type=GameActionType.ConfirmChoice,playerId=welcome.seat,sourceObjectId=r.requestId,optionId=option}});
        }
        public void Concede()=>Send(OnlineProtocol.SubmitAction,new OnlineActionMessage{matchId=view.matchId,requestId=sequence++,action=new GameAction{type=GameActionType.Concede,playerId=welcome.seat}});
    }
    static object Field(object value,string name)=>value.GetType().GetField(name,Private).GetValue(value);
    public static void Run()
    {
        Root="D:/project/MTGB/.tools/online-ai-1027/run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"); Directory.CreateDirectory(Root); File.WriteAllText("D:/project/MTGB/.tools/online-ai-1027/latest.txt",Root);File.WriteAllText(Root+"/network-result.txt","RUNNING");
        var runner=new GameObject("OnlineAiValidation").AddComponent<OnlineAiValidation>();DontDestroyOnLoad(runner.gameObject);
        runner.StartCoroutine(runner.Guard(runner.Validate()));
    }
    void Check(bool ok,string message)
    {
        checks.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines(Root+"/network-checks.txt",checks);
        if(!ok)throw new Exception(message);
    }
    IEnumerator Guard(IEnumerator routine)
    {
        while(true){object step;try{if(!routine.MoveNext()){StartCoroutine(Cleanup());yield break;}step=routine.Current;}
            catch(Exception error){File.WriteAllText(Root+"/network-result.txt","FAIL "+error);StartCoroutine(Cleanup());yield break;}
            yield return step;}
    }
    IEnumerator Cleanup()
    {
        if(cleaned)yield break;cleaned=true;
        foreach(var p in probes)if(p.net!=null)p.net.Shutdown();
        while(probes.Any(p=>p.net!=null&&p.net.ShutdownInProgress))yield return null;
        foreach(var p in probes)if(p.net!=null)Destroy(p.net.gameObject);
        service?.Disconnect();while(service!=null&&service.IsDisconnecting)yield return null;
        Destroy(gameObject);
    }
    IEnumerator Wait(Func<bool> predicate,float seconds=12)
    {
        float end=Time.realtimeSinceStartup+seconds;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;
    }
    void Update(){if(Time.unscaledTime<heartbeat)return;heartbeat=Time.unscaledTime+1;
        foreach(var p in probes)if(p.net!=null&&p.net.IsConnectedClient)p.Send(OnlineProtocol.Heartbeat,new OnlineHeartbeatMessage());}
    Probe Connect(string name,string action,string token="")
    {
        var p=new Probe();probes.Add(p);var go=new GameObject(name);DontDestroyOnLoad(go);
        var transport=go.AddComponent<UnityTransport>();p.net=go.AddComponent<NetworkManager>();
        p.net.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ConnectionApproval=false};
        transport.SetConnectionData("127.0.0.1",Port);
        var manifest=(OnlineContentManifest)Field(service,"localManifest");
        p.net.OnClientConnectedCallback+=id=>{
            p.net.CustomMessagingManager.RegisterNamedMessageHandler(OnlineProtocol.MessageName,(sender,reader)=>{
                reader.ReadValueSafe(out string json);var e=JsonUtility.FromJson<OnlineEnvelope>(json);OnlineEnvelopeCodec.Decode(e);
                File.AppendAllText(Root+"/"+name+"-wire.jsonl",JsonUtility.ToJson(e)+"\n");
                if(e.type==OnlineProtocol.Welcome)p.welcome=JsonUtility.FromJson<OnlineWelcomeMessage>(e.payload);
                if(e.type==OnlineProtocol.Lobby)p.lobby=JsonUtility.FromJson<OnlineLobbyState>(e.payload);
                if(e.type==OnlineProtocol.Error)p.error=e.payload;
                if(e.type==OnlineProtocol.ActionRejected)p.rejection=e.payload;
                if(e.type==OnlineProtocol.ReplayChunk)p.replay=true;
                if(e.type==OnlineProtocol.MatchEnded)p.ended=ServerSnapshotJson.Deserialize<OnlineMatchEndedMessage>(e.payload);
                if(e.type==OnlineProtocol.StartMatch)p.view=ServerSnapshotJson.Deserialize<OnlineStartMatchMessage>(e.payload).view;
                if(e.type==OnlineProtocol.StateUpdate)p.view=ServerSnapshotJson.Deserialize<OnlineStateUpdateMessage>(e.payload).view;
                if(e.type==OnlineProtocol.ResyncState)p.view=ServerSnapshotJson.Deserialize<OnlineResyncMessage>(e.payload).view;
                if(p.view!=null&&(e.type==OnlineProtocol.StartMatch||e.type==OnlineProtocol.ResyncState))p.Send(OnlineProtocol.HashReport,new OnlineHashReportMessage{appliedSequence=p.view.appliedSequence,stateHash=p.view.viewHash});
            });
            p.Send(OnlineProtocol.Hello,new OnlineHelloMessage{displayName=name,roomId="ai-1027",roomAction=action,reconnectToken=token,manifest=manifest,requestedRoomConfig=new OnlineRoomConfig{bestOf=3,startingLife=8}});
        };
        Check(p.net.StartClient(),name+" real UDP client started");return p;
    }
    DeckDefinition Deck(bool ai)
    {
        var cards=CardDatabaseRepository.LoadAll();var result=new DeckDefinition{name=ai?"AI validation red":"Human validation lands",format="none"};
        void Add(string name,int count){var c=cards.First(c=>c.cardName==name&&!c.forgeAmbiguous&&!c.customContent);result.cards.Add(new DeckEntry{setCode=c.setCode,collectorNumber=c.CollectorNumber,count=count});}
        if(!ai)Add("Forest",60);
        else {Add("Mountain",24);foreach(string n in new[]{"Goblin Guide","Monastery Swiftspear","Raging Goblin","Shock","Lightning Bolt","Lava Spike","Chain Lightning","Incinerate","Lightning Strike"})Add(n,4);}
        return result;
    }
    IEnumerator Validate()
    {
        service=OnlineMatchService.EnsureInstance();Check(!service.IsOnline,"Isolated service before validation");
        Check(service.StartDedicatedServer(Port),"Authority server started");
        var human=Connect("Human","create");yield return Wait(()=>human.welcome!=null);Check(human.welcome?.seat==1,"Human obtains host seat1");
        var registry=(OnlineRoomRegistry)Field(service,"roomRegistry");Check(registry.TryGet("ai-1027",out room),"Authority room exists");
        var observer=Connect("Observer","spectate");yield return Wait(()=>observer.welcome!=null);Check(observer.welcome?.seat>2,"Observer obtains spectator identity");
        var own=Deck(false);var ai=Deck(true);string aiOriginal=JsonUtility.ToJson(ai);
        observer.Send(OnlineProtocol.ConfigureAi,new OnlineAiMessage{deck=ai});yield return Wait(()=>observer.error!=null);
        Check(observer.error!=null&&!room.members.Values.Any(m=>m.isAi),"Spectator cannot add AI");
        human.Send(OnlineProtocol.SubmitDeck,new OnlineDeckMessage{deck=own});
        human.Send(OnlineProtocol.ConfigureAi,new OnlineAiMessage{deck=ai});
        yield return Wait(()=>human.lobby?.players.Any(p=>p.isAi)==true,25);
        Check(room.members.TryGetValue(2,out var bot)&&bot.isAi&&bot.ready&&bot.sceneReady,"Validated bot ready without client synchronization");
        Check(human.lobby.players.Single(p=>p.isAi).seat==2&&OnlineRoomListing.FromRoom(room,false).aiPlayers==1,"Lobby and directory expose AI identity");
        human.error=null;human.Send(OnlineProtocol.ConfigureAi,new OnlineAiMessage{deck=new DeckDefinition()});yield return Wait(()=>human.error!=null);
        Check(human.error!=null&&room.members[2]==bot,"Invalid AI deck rejected without losing previous deck");
        human.Send(OnlineProtocol.ConfigureAi,new OnlineAiMessage{remove=true});yield return Wait(()=>!room.members.ContainsKey(2));Check(!room.members.ContainsKey(2),"Host removes AI");
        human.Send(OnlineProtocol.ConfigureAi,new OnlineAiMessage{deck=ai});yield return Wait(()=>room.members.ContainsKey(2));bot=room.members[2];
        var stranger=Connect("LateJoin","join");yield return Wait(()=>stranger.welcome!=null);Check(stranger.welcome?.seat>2&&room.members[2]==bot,"Joining cannot claim AI seat");
        room.series.nextChooserSeat=2; // chooser fixture, not an engine result
        human.Send(OnlineProtocol.SetReady,new OnlineReadyMessage{ready=true});yield return Wait(()=>room.members[1].ready);
        human.Send<object>(OnlineProtocol.RequestStartMatch,null);
        yield return Wait(()=>human.view?.forge?.awaitingInput==true,60);
        Check(room.match!=null&&room.playDrawChooserSeat==0&&human.view?.forge?.players?.Length==2,"AI chooses play and real game reaches human input");
        Check(human.view.forge.players.Single(p=>p.seat==2).hand.Length==0,"Human cannot see AI private hand");
        Check(observer.view!=null&&observer.view.forge.players.All(p=>(p.hand?.Length??0)==0),"Observer receives neither private hand");
        human.error=null;human.Send(OnlineProtocol.ConfigureAi,new OnlineAiMessage{remove=true});yield return Wait(()=>human.error!=null);
        Check(human.error!=null&&room.members[2]==bot,"Cannot remove AI during game");
        string token=human.welcome.reconnectToken;human.net.Shutdown();yield return Wait(()=>!room.members[1].connected);
        Check(room.members[1].reconnectWindow.IsActive&&room.members[2]==bot,"Human disconnection retains bot and match");
        human=Connect("Resumed","join",token);yield return Wait(()=>human.welcome!=null&&human.view!=null,20);
        Check(human.welcome?.seat==1&&human.welcome.resumingMatch,"Reconnect restores human participant");
        human.sequence=human.welcome.lastRequestId+1;yield return Wait(()=>room.members[1].sceneReady);
        float openingEnd=Time.realtimeSinceStartup+40;
        while((human.view?.forge?.turn??0)<1&&Time.realtimeSinceStartup<openingEnd){
            var r=human.view?.forge?.request;
            if(human.view?.forge?.awaitingInput==true&&r!=null&&r.requestId!=human.lastNative){
                int keep=Array.FindIndex(r.options,o=>!o.disabled&&(o.id=="ok"||o.id=="keep"));
                if(keep<0)keep=Array.FindIndex(r.options,o=>!o.disabled&&!o.selected);
                if(keep>=0)human.Choose(keep);
            }
            yield return new WaitForSecondsRealtime(.25f);
        }
        Check(human.view.forge.turn>=1,"Real opening completed before concession");
        human.Send(OnlineProtocol.SubmitAction,new OnlineActionMessage{matchId=human.view.matchId,requestId=human.sequence++,action=new GameAction{type=GameActionType.Concede,playerId=2}});
        yield return Wait(()=>human.ended!=null,25);
        Check(human.ended?.winnerSeat==2,"Spoofed seat is rebound to human: AI cannot be forced to concede");
        Check(human.ended?.winnerSeat==2&&room.series.betweenGames&&room.series.gameNumber==2,"Native concession advances BO3");
        Check(bot.ready&&!bot.reviewing&&room.hostSeat==1,"AI automatically ready and cannot become host");
        Check(JsonUtility.ToJson(bot.deck)==aiOriginal,"AI retains original deck between games");
        yield return Wait(()=>human.replay&&observer.ended!=null,20);
        Check(human.replay&&observer.ended.ownDeck==null,"Human gets replay; observer gets no deck");
        human.ended=null;human.view=null;human.lastNative=0;human.Send<object>(OnlineProtocol.LeaveReview,null);
        human.Send(OnlineProtocol.SetReady,new OnlineReadyMessage{ready=true});yield return Wait(()=>room.playDrawChooserSeat==1,15);
        Check(room.playDrawChooserSeat==1,"Human loser receives next play/draw choice");
        human.Send(OnlineProtocol.ChoosePlayDraw,new OnlinePlayDrawMessage{firstPlayerSeat=2});
        yield return Wait(()=>human.view?.forge?.awaitingInput==true,60);
        bool sawCreature=false,sawAiTurn=false;float end=Time.realtimeSinceStartup+180;int steps=0;float nextAction=0;
        while(human.ended==null&&Time.realtimeSinceStartup<end&&steps<450)
        {
            var v=human.view?.forge;var r=v?.request;
            if(v!=null){sawAiTurn|=v.turnSeat==2;sawCreature|=v.players.Any(p=>p.seat==2&&p.battlefield.Any(c=>c.creature));}
            if(v?.awaitingInput==true&&r!=null&&r.seat==1&&r.requestId!=human.lastNative&&Time.unscaledTime>=nextAction)
            {
                int choice=Array.FindIndex(r.options,o=>!o.disabled&&(o.id=="ok"||o.id=="pass"||o.id=="keep"));
                if(choice<0)choice=Array.FindIndex(r.options,o=>!o.disabled&&!o.selected);
                if(choice>=0){human.Choose(choice);steps++;nextAction=Time.unscaledTime+.25f;}
            }
            yield return null;
        }
        Check(human.ended!=null&&!human.ended.aborted&&human.ended.winnerSeat==2,"AI naturally wins second game without human proxy choices");
        Check(sawAiTurn&&sawCreature,"Real AI turn and creature casting observed");
        Check(room.series.finished&&room.series.player2Wins==2,"BO3 finishes at AI two wins");
        File.WriteAllText(Root+"/final-view.json",ServerSnapshotJson.Serialize(human.ended));
        human.Send<object>(OnlineProtocol.LeaveReview,null);yield return Wait(()=>!room.members[1].reviewing);
        human.Send(OnlineProtocol.ConfigureAi,new OnlineAiMessage{remove=true});yield return Wait(()=>!room.members.ContainsKey(2));
        Check(!room.members.ContainsKey(2),"AI can be removed after completed series");
        human.Send(OnlineProtocol.ConfigureAi,new OnlineAiMessage{deck=ai});yield return Wait(()=>room.members.ContainsKey(2));
        foreach(var p in probes)if(p.net!=null)p.net.Shutdown();yield return Wait(()=>registry.Count==0,20);
        Check(registry.Count==0,"Last human leaves: orphan AI room removed");
        File.WriteAllText(Root+"/network-result.txt","PASS "+checks.Count+" checks; real dedicated authority + loopback human/spectators; native BO3, reconnect, natural AI win.");
    }
}
#endif
