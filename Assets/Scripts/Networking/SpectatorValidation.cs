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

public sealed class SpectatorValidation : MonoBehaviour
{
    const string Output=".tools/room-browser/spectator-result.txt";
    readonly List<Probe> probes=new List<Probe>();
    OnlineMatchService service;
    sealed class Probe
    {
        public NetworkManager net;
        public OnlineWelcomeMessage welcome;
        public OnlineLobbyState lobby;
        public OnlinePlayerViewState view;
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
        var runner=new GameObject("SpectatorValidation").AddComponent<SpectatorValidation>();
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
    void Cleanup(){running=false;foreach(var p in probes){p.net.Shutdown();Destroy(p.net.gameObject);}service?.Disconnect();Destroy(gameObject);}
    Probe Connect(string name,string action)
    {
        var p=new Probe();probes.Add(p);
        var go=new GameObject(name);DontDestroyOnLoad(go);
        var transport=go.AddComponent<UnityTransport>();p.net=go.AddComponent<NetworkManager>();
        p.net.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ConnectionApproval=false};
        transport.SetConnectionData("127.0.0.1",18790);
        var manifest=(OnlineContentManifest)typeof(OnlineMatchService).GetField("localManifest",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(service);
        p.net.OnClientConnectedCallback+=id=> {
            p.net.CustomMessagingManager.RegisterNamedMessageHandler(OnlineProtocol.MessageName,(sender,reader)=> {
                reader.ReadValueSafe(out string json);var e=JsonUtility.FromJson<OnlineEnvelope>(json);
                if(e.type==OnlineProtocol.Welcome)p.welcome=JsonUtility.FromJson<OnlineWelcomeMessage>(e.payload);
                if(e.type==OnlineProtocol.Lobby)p.lobby=JsonUtility.FromJson<OnlineLobbyState>(e.payload);
                if(e.type==OnlineProtocol.Error)p.error=e.payload;
                if(e.type==OnlineProtocol.ActionRejected)p.rejection=e.payload;
                if(e.type==OnlineProtocol.ReplayChunk)p.replay=true;
                if(e.type==OnlineProtocol.StartMatch)p.view=JsonUtility.FromJson<OnlineStartMatchMessage>(e.payload).view;
                if(e.type==OnlineProtocol.StateUpdate)p.view=JsonUtility.FromJson<OnlineStateUpdateMessage>(e.payload).view;
                if(e.type==OnlineProtocol.ResyncState)p.view=JsonUtility.FromJson<OnlineResyncMessage>(e.payload).view;
                if(p.view!=null&&(e.type==OnlineProtocol.StartMatch||e.type==OnlineProtocol.ResyncState))
                    p.Send(OnlineProtocol.HashReport,new OnlineHashReportMessage{appliedSequence=p.view.appliedSequence,stateHash=p.view.viewHash});
            });
            p.Send(OnlineProtocol.Hello,new OnlineHelloMessage{displayName=name,roomId="spectator-test",roomAction=action,manifest=manifest,requestedRoomConfig=new OnlineRoomConfig()});
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
        // Inject unique markers into every private channel, and check the serialized payload.
        var secret=new ForgeCardView{id=77,name="SECRET",printingKey="SECRET",currentRules="SECRET",faceDown=true,preparedSpell=new ForgePreparedSpellView{name="SECRET"}};
        var source=new OnlinePlayerViewState{matchId="privacy",forge=new ForgeSeatView {
            awaitingInput=true,canRespond=true,request=new ForgeRequest{message="SECRET"},playableExile=new[]{secret},playableGraveyard=new[]{secret},
            players=new[]{new ForgePlayerView{hand=new[]{secret},battlefield=new[]{secret},handCount=1}},
            stack=new[]{"SECRET"},stackItems=new[]{new ForgeStackItem{source=secret,description="SECRET",hasXValue=true,xValue=77}}
        }};
        var publicView=OnlineSpectatorView.Create(source,3);
        Check(!JsonUtility.ToJson(publicView).Contains("SECRET"),"Spectator payload leaked private data");
        Check(source.forge.request.message=="SECRET"&&source.forge.players[0].hand.Length==1,"Projection mutated player view");
        service=OnlineMatchService.EnsureInstance();service.StatusChanged+=s=>File.AppendAllText(".tools/room-browser/spectator-trace.txt",s+"\n");Check(service.StartDedicatedServer(18790),"Server start failed");
        var first=Connect("Player A","create");yield return Wait(()=>first.welcome!=null);Check(first.welcome?.seat==1,"First seat: "+first.error+" connected="+first.net.IsConnectedClient);
        var second=Connect("Player B","join");yield return Wait(()=>second.welcome!=null);Check(second.welcome?.seat==2,"Second seat: "+second.error);
        second.Send(OnlineProtocol.SetSpectator,new OnlineReadyMessage{ready=true});yield return Wait(()=>second.welcome.seat>2);Check(second.welcome.seat>2,"Could not move to spectator seat");
        second.Send(OnlineProtocol.SetReady,new OnlineReadyMessage{ready=true});yield return new WaitForSecondsRealtime(.3f);Check(!second.lobby.players.First(p=>p.seat==second.welcome.seat).ready,"Spectator became ready");
        second.Send(OnlineProtocol.SetSpectator,new OnlineReadyMessage{ready=false});yield return Wait(()=>second.welcome.seat==2);Check(second.welcome.seat==2,"Could not return to player seat");
        var observer=Connect("Observer","spectate");yield return Wait(()=>observer.welcome!=null);Check(observer.welcome?.seat>2,"Observer rejected: "+observer.error);
        var forest=CardDatabaseRepository.LoadAll().First(c=>c.cardName=="Forest");
        var deck=new DeckDefinition{name="Validation",cards=new List<DeckEntry>{new DeckEntry{setCode=forest.setCode,collectorNumber=forest.CollectorNumber,count=60}}};
        first.Send(OnlineProtocol.SubmitDeck,new OnlineDeckMessage{deck=deck});second.Send(OnlineProtocol.SubmitDeck,new OnlineDeckMessage{deck=deck});
        yield return Wait(()=>first.lobby.players.Count(p=>p.seat<=2&&p.hasDeck)==2);
        Check(first.lobby.players.Count(p=>p.seat<=2&&p.hasDeck)==2,"Deck rejected: "+first.error+second.error);
        first.Send(OnlineProtocol.SetReady,new OnlineReadyMessage{ready=true});second.Send(OnlineProtocol.SetReady,new OnlineReadyMessage{ready=true});
        yield return Wait(()=>first.lobby.players.Count(p=>p.ready)==2);first.Send(OnlineProtocol.RequestStartMatch,new OnlineReadyMessage());
        yield return Wait(()=>observer.view?.forge?.players?.Length==2,65);
        Check(observer.view?.forge?.players?.Length==2,"Observer did not enter running match: "+first.error);
        Check(observer.view.forge.players.All(p=>p.hand.Length==0)&&!observer.view.forge.awaitingInput,"Live observer hand/request leak");
        var late=Connect("Late observer","join");yield return Wait(()=>late.view?.forge?.players?.Length==2);
        Check(late.welcome?.seat>2&&late.view?.forge?.players?.Length==2,"Running room rejected late join: "+late.error);
        late.Send(OnlineProtocol.SubmitAction,new OnlineActionMessage{matchId=late.view.matchId,requestId=1,action=new GameAction{type=GameActionType.Concede,playerId=1}});
        yield return Wait(()=>late.rejection!=null);Check(late.rejection!=null,"Forged spectator action was not rejected");
        var requestPlayer=first.view.forge.awaitingInput?first:second;var request=requestPlayer.view.forge.request;
        Check(request!=null&&request.options.Length>0,"No playable request");
        long sequence=late.view.appliedSequence;
        requestPlayer.Send(OnlineProtocol.SubmitAction,new OnlineActionMessage{matchId=requestPlayer.view.matchId,requestId=1,action=new GameAction{type=GameActionType.ConfirmChoice,playerId=requestPlayer.welcome.seat,sourceObjectId=request.requestId,optionId=0}});
        yield return Wait(()=>late.view.appliedSequence>sequence);
        Check(late.view.appliedSequence>sequence,"Late spectator did not receive live updates");
        Check(late.view.forge.players.All(p=>p.hand.Length==0),"Update exposed hands");
        yield return new WaitForSecondsRealtime(.4f);
        var query=OnlineRoomDiscovery.Query("127.0.0.1",18790);while(!query.IsCompleted)yield return null;
        Check(query.Result.rooms.Single().players==2&&query.Result.rooms.Single().spectators==2,"Directory counts: "+query.Result.rooms.Single().players+" players / "+query.Result.rooms.Single().spectators+" spectators");
        Check(!query.Result.rooms.Single().lanHosted,"Dedicated server classified as LAN");
        first.Send(OnlineProtocol.SubmitAction,new OnlineActionMessage{matchId=first.view.matchId,requestId=2,action=new GameAction{type=GameActionType.Concede,playerId=1}});
        yield return new WaitForSecondsRealtime(2);
        Check(!observer.replay&&!late.replay,"Full replay sent to spectator");
        File.WriteAllText(Output,"PASS: private-payload projection; real UDP clients; player/observer switching; observer cannot ready; game starts with observers; late join; spoofed action rejected; live updates; 2 players + 2 observers directory; no full replay sent to observers.");
    }
}
#endif

