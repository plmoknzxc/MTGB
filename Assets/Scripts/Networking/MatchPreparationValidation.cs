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

public sealed class MatchPreparationValidation : MonoBehaviour
{
    const string Output=".tools/room-browser/preparation-result.txt";
    readonly List<Probe> probes=new List<Probe>();
    OnlineMatchService service;
    sealed class Probe
    {
        public NetworkManager net;
        public OnlineWelcomeMessage welcome;
        public OnlineLobbyState lobby;
        public OnlinePlayerViewState view; public OnlineMatchEndedMessage ended;
        public string rejection,error;
        public bool replay;
        public void Send<T>(string type,T payload)
        {
            string json=JsonUtility.ToJson(new OnlineEnvelope{type=type,payload=JsonUtility.ToJson(payload)});
            using var writer=new FastBufferWriter(json.Length*2+32,Allocator.Temp);writer.WriteValueSafe(json);
            net.CustomMessagingManager.SendNamedMessage(OnlineProtocol.MessageName,0,writer,NetworkDelivery.ReliableFragmentedSequenced);
        }
    }
    static bool running;
    public static void Run()
    {
        if(running)return;running=true;
        Directory.CreateDirectory(".tools/room-browser");File.WriteAllText(Output,"RUNNING");
        var runner=new GameObject("MatchPreparationValidation").AddComponent<MatchPreparationValidation>();
        DontDestroyOnLoad(runner);runner.StartCoroutine(runner.Guard(runner.Validate()));
    }
    static void Check(bool yes,string message){if(!yes)throw new Exception(message);}
    IEnumerator Guard(IEnumerator routine)
    {
        while(true) {
            object step;
            try {if(!routine.MoveNext()){Cleanup();yield break;}step=routine.Current;}
            catch(Exception e){File.WriteAllText(Output,"FAIL "+e);Cleanup();throw;}
            yield return step;
        }
    }
    void Cleanup(){StartCoroutine(FinishCleanup());}
    IEnumerator FinishCleanup()
    {
        foreach(var p in probes) if(p.net!=null) p.net.Shutdown();
        while(probes.Any(p=>p.net!=null&&p.net.ShutdownInProgress)) yield return null;
        foreach(var p in probes) if(p.net!=null) Destroy(p.net.gameObject);
        service?.Disconnect();
        while(service!=null&&service.IsDisconnecting) yield return null;
        running=false;Destroy(gameObject);
    }
    Probe Connect(string name,string action)
    {
        var p=new Probe();probes.Add(p);
        var go=new GameObject(name);DontDestroyOnLoad(go);
        var transport=go.AddComponent<UnityTransport>();p.net=go.AddComponent<NetworkManager>();
        p.net.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ConnectionApproval=false};
        transport.SetConnectionData("127.0.0.1",18791);
        var manifest=(OnlineContentManifest)typeof(OnlineMatchService).GetField("localManifest",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(service);
        p.net.OnClientConnectedCallback+=id=> {
            p.net.CustomMessagingManager.RegisterNamedMessageHandler(OnlineProtocol.MessageName,(sender,reader)=> {
                reader.ReadValueSafe(out string json);var e=JsonUtility.FromJson<OnlineEnvelope>(json);
                if(e.type==OnlineProtocol.Welcome)p.welcome=JsonUtility.FromJson<OnlineWelcomeMessage>(e.payload);
                if(e.type==OnlineProtocol.Lobby)p.lobby=JsonUtility.FromJson<OnlineLobbyState>(e.payload);
                if(e.type==OnlineProtocol.MatchEnded)p.ended=JsonUtility.FromJson<OnlineMatchEndedMessage>(e.payload);
                if(e.type==OnlineProtocol.Error)p.error=e.payload;
                if(e.type==OnlineProtocol.ActionRejected)p.rejection=e.payload;
                if(e.type==OnlineProtocol.ReplayChunk)p.replay=true;
                if(e.type==OnlineProtocol.StartMatch)p.view=JsonUtility.FromJson<OnlineStartMatchMessage>(e.payload).view;
                if(e.type==OnlineProtocol.StateUpdate)p.view=JsonUtility.FromJson<OnlineStateUpdateMessage>(e.payload).view;
                if(e.type==OnlineProtocol.ResyncState)p.view=JsonUtility.FromJson<OnlineResyncMessage>(e.payload).view;
                if(p.view!=null&&(e.type==OnlineProtocol.StartMatch||e.type==OnlineProtocol.ResyncState))
                    p.Send(OnlineProtocol.HashReport,new OnlineHashReportMessage{appliedSequence=p.view.appliedSequence,stateHash=p.view.viewHash});
            });
            p.Send(OnlineProtocol.Hello,new OnlineHelloMessage{displayName=name,roomId="spectator-test",roomAction=action,manifest=manifest,requestedRoomConfig=new OnlineRoomConfig{bestOf=3}});
        };
        Check(p.net.StartClient(),"Probe could not connect");return p;
    }
    IEnumerator Wait(Func<bool> condition,float seconds=12)
    {
        float end=Time.realtimeSinceStartup+seconds;
        while(!condition()&&Time.realtimeSinceStartup<end)yield return null;
    }
    void Update(){if(Time.frameCount%150==0)foreach(var p in probes)if(p.net.IsConnectedClient)p.Send(OnlineProtocol.Heartbeat,new OnlineHeartbeatMessage());}
    IEnumerator Validate()
    {
        service=OnlineMatchService.EnsureInstance();
        string lastStatus=""; service.StatusChanged += value => lastStatus=value;
        Check(service.Connect("127.0.0.1",18792,"Timeout test","preparation-validation","closed-room",null,"join"),"Connect start failed");
        float started=Time.realtimeSinceStartup;
        yield return Wait(()=>!service.IsOnline&&!service.IsDisconnecting,10);
        Check(!service.IsOnline&&!service.IsDisconnecting,"Closed host left client connecting");
        Check(Time.realtimeSinceStartup-started<9,"Admission timeout exceeded bound");
        Check(lastStatus.Contains("超时")||lastStatus.Contains("中断"),"Missing failure explanation: "+lastStatus);
        Check(service.StartDedicatedServer(18791),"Server start failed");
        var missing=Connect("Missing room","join");
        yield return Wait(()=>missing.error!=null||!missing.net.IsClient,8);
        Check(missing.welcome==null,"Missing room was silently created");
        var first=Connect("Player A","create");yield return Wait(()=>first.welcome!=null);
        var second=Connect("Player B","join");yield return Wait(()=>second.welcome!=null);
        Check(first.welcome?.seat==1&&second.welcome?.seat==2,"Player admission failed");
        var forest=CardDatabaseRepository.LoadAll().First(c=>c.cardName=="Forest");
        var deck=new DeckDefinition{name="Validation",cards=new List<DeckEntry>{new DeckEntry{setCode=forest.setCode,collectorNumber=forest.CollectorNumber,count=60}}};
        first.Send(OnlineProtocol.SubmitDeck,new OnlineDeckMessage{deck=deck});second.Send(OnlineProtocol.SubmitDeck,new OnlineDeckMessage{deck=deck});
        yield return Wait(()=>first.lobby.players.Count(p=>p.hasDeck)==2);
        first.Send(OnlineProtocol.SetReady,new OnlineReadyMessage{ready=true});second.Send(OnlineProtocol.SetReady,new OnlineReadyMessage{ready=true});
        yield return Wait(()=>first.lobby.players.Count(p=>p.ready)==2);
        first.Send(OnlineProtocol.RequestStartMatch,new OnlineReadyMessage());
        yield return Wait(()=>first.lobby.playDrawChooserSeat!=0);
        int chooser=first.lobby.playDrawChooserSeat;Check(chooser==1||chooser==2,"No play/draw choice");
        Check(first.view==null&&second.view==null,"Opening hand exposed before play/draw choice");
        var choosing=chooser==1?first:second;var other=chooser==1?second:first;
        other.Send(OnlineProtocol.ChoosePlayDraw,new OnlinePlayDrawMessage{firstPlayerSeat=other.welcome.seat});
        yield return new WaitForSecondsRealtime(.3f);Check(first.view==null,"Non-chooser selected play/draw");
        choosing.Send(OnlineProtocol.ChoosePlayDraw,new OnlinePlayDrawMessage{firstPlayerSeat=other.welcome.seat});
        yield return Wait(()=>first.view?.forge?.players?.Length==2,65);
        Check(first.view?.forge?.players?.Length==2,"Game did not start after choice");
        yield return Wait(()=>other.view?.forge?.awaitingInput==true,10);
        Check(other.view.forge.awaitingInput,"Chosen first player did not receive first mulligan decision");
        var registry=(OnlineRoomRegistry)typeof(OnlineMatchService).GetField("roomRegistry",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(service);
        var room=registry.Rooms.Single();
        Check(room.series.nextChooserSeat==chooser,"Draw chooser state lost");
        typeof(OnlineMatchService).GetMethod("EndRoomMatch",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(service,new object[]{room,1,"Validation result",false});
        yield return Wait(()=>first.lobby.series.betweenGames);
        first.view=null;second.view=null;
        first.Send(OnlineProtocol.SetReady,new OnlineReadyMessage{ready=true});
        yield return new WaitForSecondsRealtime(.3f);Check(room.playDrawChooserSeat==0,"Started before both ready");
        second.Send(OnlineProtocol.SetReady,new OnlineReadyMessage{ready=true});
        yield return Wait(()=>first.lobby.playDrawChooserSeat==2);
        Check(room.playDrawChooserSeat==2&&room.match==null,"Both ready did not automatically advance to loser's choice");
        Check(first.view==null&&second.view==null,"Next opening hand exposed before choice");
        second.Send(OnlineProtocol.SetReady,new OnlineReadyMessage{ready=false});
        yield return Wait(()=>first.lobby.playDrawChooserSeat==0);Check(room.playDrawChooserSeat==0,"Cancel readiness left stale choice");
        second.Send(OnlineProtocol.SetReady,new OnlineReadyMessage{ready=true});
        yield return Wait(()=>first.lobby.playDrawChooserSeat==2);
        second.Send(OnlineProtocol.ChoosePlayDraw,new OnlinePlayDrawMessage{firstPlayerSeat=2});
        yield return Wait(()=>second.view?.forge?.players?.Length==2,65);
        Check(room.match!=null&&room.series.gameNumber==2&&!room.series.betweenGames,"Next game did not start automatically");
        var drawSeries=new OnlineSeriesState{bestOf=3,nextChooserSeat=2};drawSeries.RecordGameResult(0);Check(drawSeries.nextChooserSeat==2,"Draw changed chooser");
        File.WriteAllText(Output,"PASS: closed-host timeout/recovery; absent room rejection; real UDP BO3 choice; no opening hand before choice; wrong player rejected; chosen player first mulligan; both-ready auto advance; previous loser chooses; cancel/re-ready; automatic game 2; drawn game preserves chooser.");
    }
}
#endif
