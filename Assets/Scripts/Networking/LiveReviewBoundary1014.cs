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

public sealed class LiveReviewBoundary1014 : MonoBehaviour
{
    public static bool AutomaticCase;
    static string Output=>".tools/live-fixes-1014/end-review-"+(AutomaticCase?"auto":"manual")+"-result.txt";
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
    bool previousAutoSave;
    static bool running;
    public static void Run()
    {
        if(running)return;running=true;
        Directory.CreateDirectory(".tools/live-fixes-1014");File.WriteAllText(Output,"RUNNING");File.WriteAllText(Output+".checks","");
        var runner=new GameObject("LiveReviewBoundary1014").AddComponent<LiveReviewBoundary1014>();
        DontDestroyOnLoad(runner);runner.StartCoroutine(runner.Guard(runner.Validate()));
    }
    static void Check(bool yes,string message){File.AppendAllText(Output+".checks",(yes?"PASS ":"FAIL ")+message+"\n");if(!yes)throw new Exception(message);}
    IEnumerator Guard(IEnumerator routine)
    {
        while(true) {
            object step;
            try {if(!routine.MoveNext()){Cleanup();yield break;}step=routine.Current;}
            catch(Exception e){File.WriteAllText(Output,"FAIL "+e);Cleanup();throw;}
            yield return step;
        }
    }
    void Cleanup(){GamePreferences.SetAutoSaveReplays(previousAutoSave);StartCoroutine(FinishCleanup());}
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
        transport.SetConnectionData("127.0.0.1",18794);
        var manifest=(OnlineContentManifest)typeof(OnlineMatchService).GetField("localManifest",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(service);
        p.net.OnClientConnectedCallback+=id=> {
            p.net.CustomMessagingManager.RegisterNamedMessageHandler(OnlineProtocol.MessageName,(sender,reader)=> {
                reader.ReadValueSafe(out string json);var e=JsonUtility.FromJson<OnlineEnvelope>(json);OnlineEnvelopeCodec.Decode(e);
                if(e.type==OnlineProtocol.Welcome)p.welcome=JsonUtility.FromJson<OnlineWelcomeMessage>(e.payload);
                if(e.type==OnlineProtocol.Lobby)p.lobby=JsonUtility.FromJson<OnlineLobbyState>(e.payload);
                if(e.type==OnlineProtocol.MatchEnded)p.ended=ServerSnapshotJson.Deserialize<OnlineMatchEndedMessage>(e.payload);
                if(e.type==OnlineProtocol.Error)p.error=e.payload;
                if(e.type==OnlineProtocol.ActionRejected)p.rejection=e.payload;
                if(e.type==OnlineProtocol.ReplayChunk)p.replay=true;
                if(e.type==OnlineProtocol.StartMatch)p.view=ServerSnapshotJson.Deserialize<OnlineStartMatchMessage>(e.payload).view;
                if(e.type==OnlineProtocol.StateUpdate)p.view=ServerSnapshotJson.Deserialize<OnlineStateUpdateMessage>(e.payload).view;
                if(e.type==OnlineProtocol.ResyncState)p.view=ServerSnapshotJson.Deserialize<OnlineResyncMessage>(e.payload).view;
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
        if(!condition())throw new TimeoutException("Network boundary condition timed out after "+seconds+" seconds");
    }
    void Update(){if(Time.frameCount%150==0)foreach(var p in probes)if(p.net.IsConnectedClient)p.Send(OnlineProtocol.Heartbeat,new OnlineHeartbeatMessage());}
    IEnumerator Validate()
    {
        previousAutoSave=GamePreferences.AutoSaveReplays;GamePreferences.SetAutoSaveReplays(AutomaticCase);
        service=OnlineMatchService.EnsureInstance();
        Check(service.StartLanHost(18794,"你","end-review-validation","spectator-test",new OnlineRoomConfig{bestOf=AutomaticCase?1:3}),"Host failed");
        yield return Wait(()=>service.HasJoinedLobby);
        var second=Connect("对手","join");yield return Wait(()=>second.welcome!=null);
        var observer=Connect("旁观者","spectate");yield return Wait(()=>observer.welcome!=null);
        var forest=CardDatabaseRepository.LoadAll().First(c=>c.cardName=="Forest");
        var mountain=CardDatabaseRepository.LoadAll().First(c=>c.cardName=="Mountain");
        var own=new DeckDefinition{name="Review test",cards=new List<DeckEntry>{new DeckEntry{setCode=forest.setCode,collectorNumber=forest.CollectorNumber,count=60}}};
        var other=new DeckDefinition{name="Private opponent deck",cards=new List<DeckEntry>{new DeckEntry{setCode=mountain.setCode,collectorNumber=mountain.CollectorNumber,count=60}}};
        service.SubmitDeck(own);second.Send(OnlineProtocol.SubmitDeck,new OnlineDeckMessage{deck=other});
        yield return Wait(()=>service.LobbyState.players.Count(p=>p.hasDeck)==2);
        service.SetReady(true);second.Send(OnlineProtocol.SetReady,new OnlineReadyMessage{ready=true});
        yield return Wait(()=>service.LobbyState.players.Count(p=>p.ready)==2);service.RequestStartMatch();
        if(!AutomaticCase) {
            yield return Wait(()=>service.LobbyState.playDrawChooserSeat!=0);
            if(service.LobbyState.playDrawChooserSeat==1)service.ChoosePlayDraw(true);
            else second.Send(OnlineProtocol.ChoosePlayDraw,new OnlinePlayDrawMessage{firstPlayerSeat=1});
        }
        yield return Wait(()=>service.ForgeView?.forge?.players?.Length==2&&UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()!=null,65);
        yield return Wait(()=>service.InteractionState==OnlineClientInteractionState.Idle);
        Check(service.ForgeView?.forge?.players?.Length==2,"No live view");
        Check(service.ForgeView.forge.players.All(p=>(p.reviewLibrary?.Length??0)==0),"Library exposed before game ended");
        service.Concede();
        yield return Wait(()=>service.EndReview!=null,20);
        Check(service.EndReview!=null,"No end review");
        yield return new WaitForSecondsRealtime(1);
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==ForgeBattlefieldController.SceneName,"Auto-left battlefield");
        Check(!service.IsInMatch&&service.EndReview.winnerSeat==2,"Wrong result");
        Check(!service.CanSubmitRuleAction,"Result screen permits rule actions");
        var players=service.ForgeView.forge.players;
        Check(players.Length==2,"Final view lost a player");
        var mine=players.First(p=>p.seat==1);var theirs=players.First(p=>p.seat==2);
        Check(mine.reviewLibrary?.Length==mine.libraryCount&&mine.reviewLibrary.Length>0,"Own remaining library unavailable");
        Check(mine.hand.Length==mine.handCount,"Own hand unavailable");
        Check((theirs.reviewLibrary?.Length??0)==0&&theirs.hand.Length==0,"Opponent private zones leaked");
        Check(service.EndReview.ownDeck.cards.Single().PrintingKey==own.cards.Single().PrintingKey,"Wrong own deck");
        yield return Wait(()=>second.ended!=null&&observer.ended!=null);
        Check((second.ended.finalView.forge.players.First(p=>p.seat==1).reviewLibrary?.Length??0)==0,"Other seat sees our library");
        Check(observer.ended.ownDeck==null||observer.ended.ownDeck.cards==null||observer.ended.ownDeck.cards.Count==0,"Observer deck leak");
        Check(observer.ended.finalView.forge.players.All(p=>(p.reviewLibrary?.Length??0)==0&&p.hand.Length==0),"Observer private zones leaked");
        var late=Connect("局后旁观者","join");yield return Wait(()=>late.welcome!=null&&late.ended!=null,15);
        Check(late.welcome.seat>2,"Late join became a player during review");
        Check(late.ended.finalView.viewerSeat==late.welcome.seat&&late.ended.finalView.HasValidHash(),"Late spectator view/seat/hash invalid");
        Check(late.ended.ownDeck==null&&late.ended.finalView.forge.players.All(p=>p.hand.Length==0&&(p.reviewLibrary?.Length??0)==0),"Late review leaked private zones/deck");
        var directory=OnlineRoomDiscovery.Query("127.0.0.1",18794);yield return Wait(()=>directory.IsCompleted);
        Check(!directory.IsFaulted&&directory.Result.rooms.Single().inMatch&&directory.Result.rooms.Single().state=="sideboarding","Sideboard room listed as waiting");
        second.net.Shutdown();yield return Wait(()=>service.LobbyState.players.Count(p=>p.seat<=2)==1,15);
        var afterQuit=OnlineRoomDiscovery.Query("127.0.0.1",18794);yield return Wait(()=>afterQuit.IsCompleted);
        Check(!afterQuit.IsFaulted&&afterQuit.Result.rooms.Single().inMatch&&afterQuit.Result.rooms.Single().state=="review","A departed opponent turned the retained board into a waiting room");
        var afterQuitViewer=Connect("退场后旁观者","join");yield return Wait(()=>afterQuitViewer.welcome!=null&&afterQuitViewer.ended!=null,15);
        Check(afterQuitViewer.welcome.seat>2&&afterQuitViewer.ended.finalView.forge.players.All(p=>p.hand.Length==0&&(p.reviewLibrary?.Length??0)==0),"Late join after opponent exit lost review or exposed private cards");
        Check(service.EndReplayDecided==AutomaticCase,"Replay decision mode wrong");
        ScreenCapture.CaptureScreenshot("D:/project/MTGB/.tools/live-fixes-1014/end-result"+(AutomaticCase?"-auto":"")+".png");yield return new WaitForEndOfFrame();
        var controller=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        var result=(RectTransform)typeof(ForgeBattlefieldController).GetField("endDialog",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(controller);
        var leave=(UnityEngine.UI.Button)typeof(ForgeBattlefieldController).GetField("leaveReviewButton",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(controller);
        Check(GameObject.Find("ReviewBar")==null&&!leave.gameObject.activeSelf,"Unexpected top bar/early leave button");
        result.Find("继续查看").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Check(!result.gameObject.activeSelf&&leave.gameObject.activeSelf,"Continue did not enter inspection");
        yield return new WaitForSecondsRealtime(.3f);
        ScreenCapture.CaptureScreenshot("D:/project/MTGB/.tools/live-fixes-1014/end-review-battlefield.png");yield return new WaitForEndOfFrame();
        leave.onClick.Invoke();yield return null;
        Check(result.gameObject.activeSelf&&!leave.gameObject.activeSelf&&service.EndReview!=null,"Leave shortcut failed to reopen result");
        result.Find("继续查看").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Check(leave.gameObject.activeSelf&&!result.gameObject.activeSelf,"Repeated inspection failed");
        leave.onClick.Invoke();
        if(!AutomaticCase)service.DecideEndReplay(true);yield return Wait(()=>service.EndReplayStatus=="录像已保存",20);
        Check(service.EndReplayStatus=="录像已保存","Replay save failed: "+service.EndReplayStatus);
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==ForgeBattlefieldController.SceneName,"Saving replay left review");
        result.Find(service.EndReview.series?.betweenGames == true ? "前往换备" : "返回房间").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();yield return new WaitForSecondsRealtime(1);
        Check(service.IsOnline&&service.HasJoinedLobby,"Leaving battlefield disconnected the room");
        Check(AutomaticCase?UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Main"&&!service.IsOnline:OnlineLobbyPanel.HasBattlefieldLobby&&service.LobbyState.series.betweenGames,"Manual exit/sideboard transition failed");
        File.WriteAllText(Output,"PASS: real host + opponent + observer; real Forge concession; centered result retained in battlefield; own hand/library/deck; opponent/observer privacy; read-only result; "+(AutomaticCase?"automatic replay save and explicit room exit":"manual replay save; actual leave-battlefield button retains room and enters sideboarding; late native review spectator, private projection, directory state"));
    }
}
#endif
