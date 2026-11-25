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
    static bool four, combat;
    static string commanderEvent;
    int declaredAttacker, defendingSeat, declaredBlocker;
    bool checkedDamage;
    static string Output=>!string.IsNullOrEmpty(commanderEvent)?$".tools/room-browser/{commanderEvent}-result.txt":combat?".tools/room-browser/four-combat-result.txt":four?".tools/room-browser/four-room-result.txt":".tools/room-browser/spectator-result.txt";
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
        public long sequence;
        public OnlineMatchEndedMessage ended;
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
        StartValidation(false);
    }
    public static void RunFour() => StartValidation(true);
    public static void RunFourCombat() => StartValidation(true,true);
    public static void RunLoki() => StartValidation(true,false,OnlineRoomConfig.Loki);
    public static void RunArkenstone() => StartValidation(true,false,OnlineRoomConfig.Arkenstone);
    public static void RunNauctis() => StartValidation(true,false,OnlineRoomConfig.Nauctis);
    static void StartValidation(bool commander,bool checkCombat=false,string mode=null)
    {
        if(running || OnlineMatchService.Instance?.IsOnline==true)return;
        four=commander;combat=checkCombat;commanderEvent=mode;running=true;
        Directory.CreateDirectory(".tools/room-browser");File.WriteAllText(Output,"RUNNING");
        var runner=new GameObject("SpectatorValidation").AddComponent<SpectatorValidation>();
        DontDestroyOnLoad(runner);runner.StartCoroutine(runner.Guard(four?runner.ValidateFour():runner.Validate()));
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
    void Cleanup(){StartCoroutine(ReleasePeers());}
    IEnumerator ReleasePeers()
    {
        foreach(var p in probes)if(p.net!=null)p.net.Shutdown();
        while(probes.Any(p=>p.net!=null&&p.net.ShutdownInProgress))yield return null;
        foreach(var p in probes)if(p.net!=null)Destroy(p.net.gameObject);
        service?.Disconnect();
        while(service!=null&&service.IsDisconnecting)yield return null;
        running=false;Destroy(gameObject);
    }
    Probe Connect(string name,string action)
    {
        var p=new Probe();probes.Add(p);
        var go=new GameObject(name);DontDestroyOnLoad(go);
        var transport=go.AddComponent<UnityTransport>();
        transport.MaxPacketQueueSize=OnlineMatchService.TransportPacketQueueCapacity;
        p.net=go.AddComponent<NetworkManager>();
        p.net.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ConnectionApproval=false};
        transport.SetConnectionData("127.0.0.1",18790);
        var manifest=(OnlineContentManifest)typeof(OnlineMatchService).GetField("localManifest",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(service);
        p.net.OnClientConnectedCallback+=id=> {
            p.net.CustomMessagingManager.RegisterNamedMessageHandler(OnlineProtocol.MessageName,(sender,reader)=> {
                reader.ReadValueSafe(out string json);var e=JsonUtility.FromJson<OnlineEnvelope>(json);OnlineEnvelopeCodec.Decode(e);
                if(e.type==OnlineProtocol.Welcome)p.welcome=JsonUtility.FromJson<OnlineWelcomeMessage>(e.payload);
                if(e.type==OnlineProtocol.Lobby)p.lobby=JsonUtility.FromJson<OnlineLobbyState>(e.payload);
                if(e.type==OnlineProtocol.Error)p.error=e.payload;
                if(e.type==OnlineProtocol.ActionRejected)p.rejection=e.payload;
                if(e.type==OnlineProtocol.ReplayChunk)p.replay=true;
                if(e.type==OnlineProtocol.MatchEnded)p.ended=ServerSnapshotJson.Deserialize<OnlineMatchEndedMessage>(e.payload);
                if(e.type==OnlineProtocol.StartMatch)p.view=ServerSnapshotJson.Deserialize<OnlineStartMatchMessage>(e.payload).view;
                if(e.type==OnlineProtocol.StateUpdate)p.view=ServerSnapshotJson.Deserialize<OnlineStateUpdateMessage>(e.payload).view;
                if(e.type==OnlineProtocol.ResyncState)p.view=ServerSnapshotJson.Deserialize<OnlineResyncMessage>(e.payload).view;
                if(p.view!=null&&(e.type==OnlineProtocol.StartMatch||e.type==OnlineProtocol.ResyncState))
                    p.Send(OnlineProtocol.HashReport,new OnlineHashReportMessage{appliedSequence=p.view.appliedSequence,stateHash=p.view.viewHash});
            });
            p.Send(OnlineProtocol.Hello,new OnlineHelloMessage{displayName=name,roomId="spectator-test",roomAction=action,manifest=manifest,supportsFourSeats=four,supportsCommanderEvents=true,supportsNauctis=true,requestedRoomConfig=new OnlineRoomConfig{formatId=four?"commander":"none",commanderEvent=commanderEvent}});
        };
        Check(p.net.StartClient(),"Probe could not connect");return p;
    }
    IEnumerator Wait(Func<bool> condition,float seconds=12)
    {
        float end=Time.realtimeSinceStartup+seconds;
        while(!condition()&&Time.realtimeSinceStartup<end)yield return null;
    }
    void Update(){if(Time.frameCount%150==0)foreach(var p in probes)if(p.net.IsConnectedClient)p.Send(OnlineProtocol.Heartbeat,new OnlineHeartbeatMessage());}
    IEnumerator ValidateFour()
    {
        service=OnlineMatchService.EnsureInstance();
        Check(!service.IsOnline && service.StartDedicatedServer(18790),"Could not start isolated loopback room");
        typeof(OnlineMatchService).GetField("serverReplayDirectory",BindingFlags.Instance|BindingFlags.NonPublic)
            .SetValue(service,Path.GetFullPath(".tools/room-browser"));
        for(int i=0;i<5;i++){
            var p=Connect("QA "+(i+1),i==0?"create":i==4?"spectate":"join");
            yield return Wait(()=>p.welcome!=null||p.error!=null);
            Check(p.welcome?.seat==i+1 && p.welcome.playerCapacity==4,"Seat "+(i+1)+": "+p.error);
        }
        File.WriteAllText(Output,"RUNNING: four player seats and spectator connected");
        var players=probes.Take(4).ToArray();
        var spectator=probes[4];
        var host=players[0];
        var deck=new DeckDefinition{name="Commander validation",format="commander",
            cards=new List<DeckEntry>{new DeckEntry{setCode="10E",collectorNumber="364",count=99}},
            commanders=new List<DeckEntry>{new DeckEntry{setCode="CHK",collectorNumber="19",count=1}}};
        foreach(var p in players)p.Send(OnlineProtocol.SubmitDeck,new OnlineDeckMessage{deck=deck});
        yield return Wait(()=>host.lobby.players.Count(p=>p.hasDeck)==4||players.Any(p=>p.error!=null),90);
        Check(host.lobby.players.Count(p=>p.hasDeck)==4,"Deck submission: "+string.Join(", ",players.Select(p=>p.error)));
        foreach(var p in players)p.Send(OnlineProtocol.SetReady,new OnlineReadyMessage{ready=true});
        yield return Wait(()=>host.lobby.players.Count(p=>p.ready)==4);
        Check(host.lobby.players.Count(p=>p.ready)==4,"All four should be ready");
        host.Send(OnlineProtocol.RequestStartMatch,new OnlineReadyMessage());
        File.WriteAllText(Output,"RUNNING: decks accepted, all ready, starting game");
        yield return Wait(()=>probes.All(p=>p.view?.forge?.players?.Length==4)||host.ended!=null,90);
        Check(probes.All(p=>p.view?.forge?.players?.Length==4),"Missing opening view: "+host.error+" "+host.ended?.reason);
        bool loki=commanderEvent==OnlineRoomConfig.Loki;
        int lokiSeat=host.view.forge.lokiSeat;
        bool checkedTeamInput=false;
        if(commanderEvent!=null) Check(probes.All(p=>p.view.forge.commanderEvent==commanderEvent && p.view.forge.lokiSeat==lokiSeat),"Activity or role missing from participant or observer view");
        foreach(var p in players){
            var v=p.view.forge;
            Check(v.seat==p.welcome.seat && v.players.All(a=>a.life==(loki?60:40) && a.commanders.Length==1),"Wrong opening seat, life or commander");
            Check(v.players.Single(a=>a.seat==v.seat).hand.Length==7 && v.players.Where(a=>a.seat!=v.seat).All(a=>a.hand.Length==0),"Incorrect hand projection");
        }
        Check(spectator.view.forge.players.All(p=>p.hand.Length==0)&&spectator.view.forge.request==null,"Spectator saw hidden information");
        var keeps=new HashSet<int>();var conceded=new HashSet<int>();var handled=new HashSet<long>();
        for(int step=0;step<(combat?900:100) && host.ended==null;step++){
            Probe actor=null;
            yield return Wait(()=>host.ended!=null || (actor=players.FirstOrDefault(p=>p.view.forge.request!=null&&!handled.Contains(p.view.forge.request.requestId)))!=null
                || players.Any(p=>p.error!=null||p.rejection!=null),15);
            if(host.ended!=null)break;
            Check(actor!=null,"No new choice: "+string.Join(", ",players.Select(p=>p.error??p.rejection)));
            var r=actor.view.forge.request;
            handled.Add(r.requestId);
            int s=actor.welcome.seat;
            if(combat && !checkedDamage && declaredBlocker!=0 && actor.view.forge.phase=="MAIN2") {
                var board=actor.view.forge.players.SelectMany(p=>p.battlefield).ToArray();
                Check(board.All(c=>c.id!=declaredAttacker && c.id!=declaredBlocker),"Both 2/2 combatants must leave the battlefield");
                Check(actor.view.forge.players.All(p=>p.life==40),"Blocked combat should preserve player life");
                checkedDamage=true;
            }
            if(commanderEvent==OnlineRoomConfig.Nauctis && keeps.Count==4 && conceded.Count==0 && r.kind=="InputPassPriority") {
                bool HasRegions(Probe p)=>p.view.forge.players.All(a=>a.effectMarkers?.Any(m=>m.name=="广袤海域")==true);
                yield return Wait(()=>probes.All(HasRegions),10);
                Check(probes.All(HasRegions),"Each participant and the spectator must see all four starting regions");
            }
            bool quit=(!combat || checkedDamage) && keeps.Count==4 && r.kind=="InputPassPriority" &&
                (loki ? checkedTeamInput && s==lokiSeat && actor.view.forge.players[s-1].battlefield.Any(c=>c.name.StartsWith("Illusion")) : s!=4) && conceded.Add(s);
            if(loki&&quit){
                bool HasTricks(Probe p){
                    var tricks=p.view.forge.players[lokiSeat-1].effectMarkers?.FirstOrDefault(m=>m.lokiTricks!=null)?.lokiTricks;
                    return tricks?.Length==10&&tricks.All(t=>!string.IsNullOrEmpty(t.rules))
                        &&tricks.Count(t=>t.marked)==1&&tricks.Any(t=>t.name=="壮丽幻象"&&t.marked);
                }
                yield return Wait(()=>probes.All(HasTricks),10);
                Check(probes.All(HasTricks),"Loki's public trick board differs between participants and the spectator");
            }
            int option=Array.FindIndex(r.options,o=>!o.disabled&&o.id=="keep");
            if(r.kind=="InputConfirmMulligan")keeps.Add(s);
            if(option<0)option=Array.FindIndex(r.options,o=>!o.disabled&&o.id=="ok");
            if(option<0)option=Array.FindIndex(r.options,o=>!o.disabled);
            if(loki && r.kind=="ChooseOptions" && r.sourceCard?.name=="洛基的诡计")
                option=r.selectedCount==0?Array.FindIndex(r.options,o=>o.label.Contains("壮丽幻象")):Array.FindIndex(r.options,o=>!o.disabled&&o.id=="confirm-cards");
            if(combat && !checkedDamage && keeps.Count==4) {
                option=PickFourCombat(actor,option);
                if(r.kind=="InputBlock" && declaredBlocker!=0 && r.options[option].id=="ok") {
                    bool SawBlock(Probe p)=>p.view.forge.players.SelectMany(a=>a.battlefield)
                        .Any(c=>c.id==declaredBlocker && c.blockedAttackerIds?.Contains(declaredAttacker)==true);
                    yield return Wait(()=>probes.All(SawBlock),10);
                    Check(probes.All(SawBlock),"Block assignment missing on a player or spectator");
                }
            }
            if(combat && (step%25==0 || r.kind is "InputAttack" or "InputBlock"))
                File.WriteAllText(Output,$"RUNNING turn={actor.view.forge.turn} seat={s} {r.kind} {r.options[option].id} A={declaredAttacker} B={declaredBlocker} steps={step}");
            Probe competing=null;
            if(loki && !checkedTeamInput && s!=lokiSeat && r.kind=="InputPassPriority"){
                competing=players.First(p=>p!=actor && p.welcome.seat!=lokiSeat);
                Check(competing.view.forge.request==null,"Both heroes received the same decision");
                competing.Send(OnlineProtocol.SubmitAction,new OnlineActionMessage{matchId=actor.view.matchId,requestId=++competing.sequence,
                    action=new GameAction{type=GameActionType.ConfirmChoice,playerId=competing.welcome.seat,sourceObjectId=r.requestId,optionId=option}});
            }
            actor.Send(OnlineProtocol.SubmitAction,new OnlineActionMessage{matchId=actor.view.matchId,requestId=++actor.sequence,
                action=new GameAction{type=quit?GameActionType.Concede:GameActionType.ConfirmChoice,playerId=s,sourceObjectId=r.requestId,optionId=option}});
            if(competing!=null){
                yield return Wait(()=>competing.rejection!=null && players.Any(p=>p.view.forge.request!=null && p.view.forge.request.requestId!=r.requestId));
                Check(competing.rejection!=null && actor.rejection==null && players.Any(p=>p.view.forge.request!=null && p.view.forge.request.requestId!=r.requestId),
                    "Same-frame team inputs did not reject the out-of-turn hero and advance the authorized decision");
                competing.rejection=null;
                checkedTeamInput=true;
            }
        }
        yield return Wait(()=>probes.All(p=>p.ended!=null)&&players.All(p=>p.replay),20);
        int winner=host.ended?.winnerSeat??0;
        Check(keeps.Count==4 && (loki?winner>0 && winner!=lokiSeat:winner==4) && probes.All(p=>p.ended?.winnerSeat==winner&&!p.ended.aborted),"Unexpected result: keeps="+string.Join(",",keeps)+", conceded="+string.Join(",",conceded)+", end="+host.ended?.reason+", winner="+winner);
        Check(players.All(p=>p.replay)&&!spectator.replay,"Wrong replay recipients");
        Check(probes.All(p=>p.ended.finalView.forge.players.All(seat=>seat.eliminated==(loki?seat.seat==lokiSeat:seat.seat!=4))),
            "Players and observer disagree about the eliminated seats");
        if(commanderEvent!=null){
            Check(probes.All(p=>p.ended.finalView.forge.commanderEvent==commanderEvent && p.ended.finalView.forge.lokiSeat==lokiSeat),"Final activity missing");
            File.WriteAllText(Output,$"PASS {commanderEvent}: four UDP players and one observer, opening hands, life, roles, results and replay recipients. Loki seat={lokiSeat}, winning seat={winner}, requests={handled.Count}."+ (checkedTeamInput?" Verified simultaneous hero inputs.":""));
            yield break;
        }
        Check(!combat || checkedDamage,"Combat did not resolve");
        File.WriteAllText(Output,(combat?$"PASS: attack {declaredAttacker} against seat {defendingSeat}, block {declaredBlocker} visible on all five clients, both creatures died, four players still at 40. ":"")+"PASS: four UDP players + observer, 4 accepted decks, 4 ready, 4 private 7-card hands, 40 life, all four keep, remaining seat 4 wins, replay sent only to players. Requests: "+handled.Count);
    }
    int PickFourCombat(Probe actor,int fallback)
    {
        var v=actor.view.forge;
        var r=v.request;
        var own=v.players.Single(p=>p.seat==r.seat);
        int Find(string id)=>Array.FindIndex(r.options,o=>!o.disabled && o.id==id);
        if(r.kind=="InputPassPriority" && r.seat==v.turnSeat && v.phase=="MAIN1" && v.stackCount==0) {
            var land=(own.hand??Array.Empty<ForgeCardView>()).FirstOrDefault(c=>c.land && Find("card:"+c.id)>=0);
            if(land!=null)return Find("card:"+land.id);
            if(own.battlefield.Any(c=>c.land && !c.tapped)) {
                var commander=(own.commanders??Array.Empty<ForgeCardView>()).FirstOrDefault(c=>Find("card:"+c.id)>=0);
                if(commander!=null)return Find("card:"+commander.id);
            }
        } else if(r.kind.Contains("PayMana")) {
            var land=own.battlefield.FirstOrDefault(c=>c.land && !c.tapped && Find("card:"+c.id)>=0);
            if(land!=null)return Find("card:"+land.id);
        } else if(r.kind=="InputAttack" && declaredAttacker==0) {
            var offers=r.options.Where(o=>!o.disabled && o.id.StartsWith("attack:") && o.defenderKind=="player").ToArray();
            var target=v.players.FirstOrDefault(p=>p.seat!=r.seat && p.battlefield.Any(c=>c.creature && !c.tapped) && offers.Any(o=>o.defenderId==p.targetId));
            if(target!=null) {
                Check(offers.Select(o=>o.defenderId).Distinct().Count()==3,"Expected three opposing player targets");
                var offer=offers.First(o=>o.defenderId==target.targetId);declaredAttacker=offer.attackerId;defendingSeat=target.seat;
                return Find(offer.id);
            }
        } else if(r.kind=="InputBlock" && r.seat==defendingSeat && declaredBlocker==0) {
            var block=r.options.FirstOrDefault(o=>!o.disabled && o.id.StartsWith("block:") && o.attackerId==declaredAttacker);
            if(block!=null) {
                declaredBlocker=block.defenderId;
                return Find(block.id);
            }
        }
        return fallback;
    }

    IEnumerator Validate()
    {
        // Inject unique markers into every private channel, and check the serialized payload.
        var secret=new ForgeCardView{id=77,name="SECRET",printingKey="SECRET",knownFaceName="SECRET",knownFaceRules="SECRET",knownFacePrintingKey="SECRET",currentRules="Ward {2}",faceDown=true,preparedSpell=new ForgePreparedSpellView{name="SECRET"}};
        var source=new OnlinePlayerViewState{matchId="privacy",forge=new ForgeSeatView {
            awaitingInput=true,canRespond=true,request=new ForgeRequest{message="SECRET"},playableExile=new[]{secret},playableGraveyard=new[]{secret},
            players=new[]{new ForgePlayerView{hand=new[]{secret},battlefield=new[]{secret},handCount=1}},
            stack=new[]{"SECRET"},stackItems=new[]{new ForgeStackItem{source=secret,description="SECRET",hasXValue=true,xValue=77,targets=new[]{
                new ForgeStackTarget{kind="player",playerSeat=4},new ForgeStackTarget{kind="card",zone="Hand",card=new ForgeCardView{id=42,name="SECRET"}}}}}
        }};
        var publicView=OnlineSpectatorView.Create(source,3);
        Check(!ServerSnapshotJson.Serialize(publicView).Contains("SECRET"),"Spectator payload leaked private data");
        var targets=publicView.forge.stackItems[0].targets;
        Check(targets.Length==2&&targets[0].playerSeat==4&&targets[1].card.faceDown,"Spectator lost target links or exposed a hidden card");
        Check(publicView.forge.players[0].battlefield[0].currentRules=="Ward {2}","Public face-down ability missing");
        Check(source.forge.request.message=="SECRET"&&source.forge.players[0].hand.Length==1,"Projection mutated player view");
        service=OnlineMatchService.EnsureInstance();service.StatusChanged+=s=>File.AppendAllText(".tools/room-browser/spectator-trace.txt",s+"\n");Check(service.StartDedicatedServer(18790),"Server start failed");
        typeof(OnlineMatchService).GetField("serverReplayDirectory",BindingFlags.Instance|BindingFlags.NonPublic)
            .SetValue(service,Path.GetFullPath(".tools/room-browser"));
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

