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

public sealed partial class EndReviewValidation : MonoBehaviour
{
    public static bool AutomaticCase;
    static string Output=>ReconnectCase?".tools/bo3-room-1021/native-network-result.txt":".tools/room-browser/end-review-"+(AutomaticCase?"auto":"manual")+"-result.txt";
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
        Directory.CreateDirectory(Path.GetDirectoryName(Output));File.WriteAllText(Output,"RUNNING");lifecycleChecks.Clear();
        var runner=new GameObject("EndReviewValidation").AddComponent<EndReviewValidation>();
        DontDestroyOnLoad(runner);runner.StartCoroutine(runner.Guard(runner.Validate()));
    }
    static void Check(bool yes,string message){if(ReconnectCase){lifecycleChecks.Add((yes?"PASS ":"FAIL ")+message);File.WriteAllLines(".tools/bo3-room-1021/native-network-checks.txt",lifecycleChecks);}if(!yes)throw new Exception(message);}
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
        if(ReconnectCase)UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
        running=false;Destroy(gameObject);
    }
    Probe Connect(string name,string action,string token="")
    {
        var p=new Probe();probes.Add(p);
        var go=new GameObject(name);DontDestroyOnLoad(go);
        var transport=go.AddComponent<UnityTransport>();p.net=go.AddComponent<NetworkManager>();
        p.net.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ConnectionApproval=false};
        transport.SetConnectionData("127.0.0.1",18791);
        var manifest=(OnlineContentManifest)typeof(OnlineMatchService).GetField("localManifest",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(service);
        p.net.OnClientConnectedCallback+=id=> {
            p.net.CustomMessagingManager.RegisterNamedMessageHandler(OnlineProtocol.MessageName,(sender,reader)=> {
                reader.ReadValueSafe(out string json);var e=JsonUtility.FromJson<OnlineEnvelope>(json);OnlineEnvelopeCodec.Decode(e);
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
            p.Send(OnlineProtocol.Hello,new OnlineHelloMessage{displayName=name,roomId="spectator-test",roomAction=action,reconnectToken=token,manifest=manifest,requestedRoomConfig=new OnlineRoomConfig{bestOf=3}});
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
        previousAutoSave=GamePreferences.AutoSaveReplays;GamePreferences.SetAutoSaveReplays(AutomaticCase);
        service=OnlineMatchService.EnsureInstance();
        Check(service.StartLanHost(18791,"你","end-review-validation","spectator-test",new OnlineRoomConfig{bestOf=AutomaticCase?1:3}),"Host failed");
        yield return Wait(()=>service.HasJoinedLobby);
        var second=Connect("对手","join");yield return Wait(()=>second.welcome!=null);
        var observer=Connect("旁观者","spectate");yield return Wait(()=>observer.welcome!=null);
        var forest=CardDatabaseRepository.LoadAll().First(c=>c.cardName=="Forest");
        var mountain=CardDatabaseRepository.LoadAll().First(c=>c.cardName=="Mountain");
        var own=new DeckDefinition{name="Review test",cards=new List<DeckEntry>{new DeckEntry{setCode=forest.setCode,collectorNumber=forest.CollectorNumber,count=60}}};
        var other=new DeckDefinition{name="Private opponent deck",cards=new List<DeckEntry>{new DeckEntry{setCode=mountain.setCode,collectorNumber=mountain.CollectorNumber,count=60}}};
        if(ReconnectCase)other.sideboard.Add(new DeckEntry{setCode=forest.setCode,collectorNumber=forest.CollectorNumber,count=4});
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
        if(ReconnectCase){var tail=ValidateSeriesReconnect(second,observer,own,other);while(tail.MoveNext())yield return tail.Current;yield break;}
        var players=service.ForgeView.forge.players;
        Check(players.Length==2,"Final view lost a player");
        var mine=players.First(p=>p.seat==1);var theirs=players.First(p=>p.seat==2);
        Check(mine.reviewLibrary?.Length==mine.libraryCount&&mine.reviewLibrary.Length>0,"Own remaining library unavailable");
        Check(mine.hand.Length==mine.handCount,"Own hand unavailable");
        Check((theirs.reviewLibrary?.Length??0)==0&&theirs.hand.Length==0,"Opponent private zones leaked");
        Check(service.EndReview.ownDeck.cards.Single().PrintingKey==own.cards.Single().PrintingKey,"Wrong own deck");
        yield return Wait(()=>second.ended!=null&&observer.ended!=null);
        Check(second.ended.finalView.forge.players.First(p=>p.seat==1).reviewLibrary.Length==0,"Other seat sees our library");
        Check(observer.ended.ownDeck==null||observer.ended.ownDeck.cards==null||observer.ended.ownDeck.cards.Count==0,"Observer deck leak");
        Check(observer.ended.finalView.forge.players.All(p=>(p.reviewLibrary?.Length??0)==0&&p.hand.Length==0),"Observer private zones leaked");
        Check(service.EndReplayDecided==AutomaticCase,"Replay decision mode wrong");
        ScreenCapture.CaptureScreenshot("D:/project/MTGB/.tools/room-browser/end-result"+(AutomaticCase?"-auto":"")+".png");yield return new WaitForEndOfFrame();
        var controller=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        var result=GameObject.Find("MatchResult").transform.Find("Result");
        var leave=GameObject.Find("Canvas").GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name=="LeaveReview");
        Check(GameObject.Find("ReviewBar")==null&&!leave.gameObject.activeSelf,"Unexpected top bar/early leave button");
        result.Find("查看战场").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Check(!result.gameObject.activeSelf&&leave.gameObject.activeSelf,"Continue did not enter inspection");
        yield return new WaitForSecondsRealtime(.3f);
        ScreenCapture.CaptureScreenshot("D:/project/MTGB/.tools/room-browser/end-review-battlefield.png");yield return new WaitForEndOfFrame();
        var library=GameObject.Find("Canvas").transform.Find("ZonePiles_PlayerBottomLeft/Pile_牌库").GetComponent<UnityEngine.UI.Button>();
        library.onClick.Invoke();
        var picker=GameObject.Find("CardSelectionOverlay");
        Check(picker!=null&&picker.transform.Find("CardSelection/Title").GetComponent<UnityEngine.UI.Text>().text=="你的剩余牌库","Library did not open shared picker");
        Check(picker.GetComponentsInChildren<UnityEngine.UI.Button>().All(b=>!b.name.StartsWith("Select_")),"Review exposes rule choices");
        picker.transform.Find("CardSelection/NextPage").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return null;
        Check(picker.transform.Find("CardSelection/Page").GetComponent<UnityEngine.UI.Text>().text.StartsWith("2 /"),"Review pagination failed");
        yield return new WaitForSecondsRealtime(.3f);ScreenCapture.CaptureScreenshot("D:/project/MTGB/.tools/room-browser/end-own-deck.png");yield return new WaitForEndOfFrame();
        leave.onClick.Invoke();yield return null;
        Check(!picker.activeSelf&&result.gameObject.activeSelf&&!leave.gameObject.activeSelf&&service.EndReview!=null,"Leave shortcut failed to reopen result");
        result.Find("查看战场").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Check(leave.gameObject.activeSelf&&!result.gameObject.activeSelf,"Repeated inspection failed");
        leave.onClick.Invoke();
        if(!AutomaticCase)service.DecideEndReplay(true);yield return Wait(()=>service.EndReplayStatus=="录像已保存",20);
        Check(service.EndReplayStatus=="录像已保存","Replay save failed: "+service.EndReplayStatus);
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==ForgeBattlefieldController.SceneName,"Saving replay left review");
        service.LeaveEndReview(AutomaticCase);yield return new WaitForSecondsRealtime(1);
        Check(AutomaticCase?UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Main"&&!service.IsOnline:OnlineLobbyPanel.HasBattlefieldLobby&&service.LobbyState.series.betweenGames,"Manual exit/sideboard transition failed");
        File.WriteAllText(Output,"PASS: real host + opponent + observer; real Forge concession; centered result retained in battlefield; own hand/library/deck; opponent/observer privacy; read-only result; "+(AutomaticCase?"automatic replay save and explicit room exit":"manual replay save and explicit sideboard transition"));
    }
}
#endif
