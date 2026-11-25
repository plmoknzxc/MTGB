#if MTGB_LOKI_NETWORK_TEST
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Opt-in standalone test player. Each process drives only its own real client.
public sealed class LokiNetworkClientValidation : MonoBehaviour
{
    [Serializable] sealed class Command { public int number; public string action, value; public long request, at; }
    [Serializable] sealed class Card { public int id; public string name; }
    [Serializable] sealed class Entry { public int id, controller; public string name; }
    [Serializable] sealed class Seat { public int seat, life, handCount; public bool eliminated; public Card[] hand, field; }
    [Serializable] sealed class State {
        public int pid, local, lobby, decks, ready, turn, active, priority, loki, choiceSeat, command;
        public long sequence, request, actedAt;
        public int afterStack, voteWindow, voters, votes, combatSeat;
        public string combatChoice, voteButton;
        public bool voteEnabled, advanceVisible;
        public int winner, seriesWinner, connectedSeats;
        public bool ended, aborted, seriesFinished;
        public string resultTitle, endMatchId;
        public string priorityLabel, priorityHint;
        public string scene, match, phase, kind, status, error, actionResult;
        public bool gate, canAct;
        public string[] options, rendered;
        public Entry[] stack;
        public Seat[] players;
    }
    string folder, client, room, status, error, actionResult;
    ushort port;
    int completed, pid;
    long actedAt;
    float nextPoll;
    Command pending;
    OnlineMatchService service;
    static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartClient() {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-loki-network-test");
        if(i<0 || i+3>=args.Length) return;
        var go=new GameObject("Loki network test"); DontDestroyOnLoad(go);
        var runner=go.AddComponent<LokiNetworkClientValidation>();
        runner.client=args[i+2]; runner.folder=Path.Combine(args[i+1],runner.client);
        runner.room="loki-"+Path.GetFileName(args[i+1]); runner.port=ushort.Parse(args[i+3]);
        Directory.CreateDirectory(runner.folder);
    }
    void Start() {
        Application.runInBackground=true; Application.targetFrameRate=30;
        pid=System.Diagnostics.Process.GetCurrentProcess().Id;
        service=OnlineMatchService.EnsureInstance();
        service.StatusChanged+=s=>status=s;
        service.ControlMode=ForgeControlMode.On;
        var config=new OnlineRoomConfig{formatId="commander",commanderEvent=OnlineRoomConfig.Loki,noShuffle=true,startingLife=40};
        // Keep the client identity distinct even when the room name is truncated.
        string profile = client + "-" + room;
        bool ok=client=="one" ? service.StartLanHost(port,client,profile,room,config)
            : service.Connect("127.0.0.1",port,client,profile,room,config,"join");
        if(!ok)error="Connection failed: "+status;
    }
    static Card Describe(ForgeCardView c) => new Card{id=c.id,name=string.IsNullOrEmpty(c.originalName)?c.name:c.originalName};
    void Update() {
        if(service==null)return;
        try {
            if(pending!=null && Now>=pending.at) {
                var cmd=pending; pending=null; completed=cmd.number; actedAt=Now; actionResult=Execute(cmd);
            }
            if(Time.unscaledTime<nextPoll)return;
            nextPoll=Time.unscaledTime+.075f;
            string path=Path.Combine(folder,"command.json");
            if(pending==null&&File.Exists(path)){
                var cmd=JsonUtility.FromJson<Command>(File.ReadAllText(path));
                if(cmd.number>completed)pending=cmd;
            }
            WriteState();
        } catch(IOException){ /* The coordinator may briefly hold a snapshot open on Windows. */ }
        catch(Exception e){error=e.ToString(); WriteState();}
    }
    string Execute(Command cmd) {
        var view=service.ForgeView?.forge;
        switch(cmd.action){
        case "deck":
            var d=new DeckDefinition{name="Loki network test",format="commander"};
            d.commanders.Add(new DeckEntry{setCode="TSP",collectorNumber="171",count=1});
            d.cards.Add(new DeckEntry{setCode="10E",collectorNumber="336",count=1});
            d.cards.Add(new DeckEntry{setCode="FUT",collectorNumber="103",count=1});
            d.cards.Add(new DeckEntry{setCode="10E",collectorNumber="376",count=97});
            service.SubmitDeck(d); break;
        case "ready":service.SetReady(true);break;
        case "start":service.RequestStartMatch();break;
        case "vote":
        case "unvote":
            var vote = GameObject.Find("HeroStepVote")?.GetComponent<Button>();
            string caption = vote?.GetComponentInChildren<Text>().text;
            if (vote == null || !vote.IsInteractable() || (cmd.action == "unvote"
                ? caption != "取消结束" : caption != "结束主阶段" && caption != "结束当前步骤"))
                throw new InvalidOperationException("Expected an available vote button, saw " + caption);
            vote.onClick.Invoke();
            break;
        case "pass":
            if (view?.request?.requestId != cmd.request || view.request.kind != "InputPassPriority")
                throw new InvalidOperationException("Priority request changed before passing.");
            var button = GameObject.Find("SkipCombatButton")?.GetComponent<Button>();
            if (button == null || !button.IsInteractable() || button.GetComponentInChildren<Text>().text != "让过")
                throw new InvalidOperationException("The visible Pass button was missing or disabled.");
            button.onClick.Invoke();
            break;
        case "pick":
            if(view?.request?.requestId!=cmd.request)throw new Exception("Decision expired: "+cmd.request);
            int option=Array.FindIndex(view.request.options,o=>o.id==cmd.value&&!o.disabled);
            if(option<0)throw new Exception("Option unavailable: "+cmd.value);
            service.SubmitForgeOption(option);break;
        case "drag":
            var own=view.players.Single(p=>p.seat==service.LocalSeat);
            int id=own.hand.Single(c=>Describe(c).name==cmd.value).id;
            var pointer=FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)
                .Single(p=>p.name=="Card_"+id && p.transform.parent.name=="PlayerHandRoot");
            var rect=(RectTransform)pointer.transform;
            var raycaster=pointer.GetComponentInParent<Canvas>().GetComponent<GraphicRaycaster>();
            var press=RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera,rect.TransformPoint(rect.rect.center));
            var release=new Vector2(press.x, Screen.height*.78f);
            var e=new PointerEventData(EventSystem.current){pressPosition=press,position=release,button=PointerEventData.InputButton.Left,
                pointerPressRaycast=new RaycastResult{module=raycaster,gameObject=pointer.gameObject}};
            pointer.OnBeginDrag(e);
            bool dragging=pointer.IsCastingDrag;
            pointer.OnDrag(e);pointer.OnEndDrag(e);
            return dragging?"cast drag":"unavailable drag";
        case "capture":
            string screenshot = cmd.value switch {
                "" => "stack.png", "next-hero" => "next-hero.png", "unanswered" => "unanswered.png",
                "votes" => "votes.png", "combat" => "combat.png",
                _ => throw new InvalidOperationException("Unknown screenshot")
            };
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, screenshot));
            break;
        case "finish":service.Concede();break;
        case "quit":Application.Quit();break;
        default:throw new Exception("Unknown command "+cmd.action);
        }
        return cmd.action;
    }
    void WriteState(){
        var message=service.ForgeView;var v=message?.forge;var r=v?.request;
        var members=service.LobbyState?.players;
        var state=new State{pid=pid,local=service.LocalSeat,lobby=members?.Count??0,
            connectedSeats=members?.Where(p=>p.connected && p.seat>=1 && p.seat<=4).Sum(p=>1<<(p.seat-1))??0,
            decks=members?.Count(p=>p.hasDeck)??0,ready=members?.Count(p=>p.ready)??0,
            turn=v?.turn??0,active=v?.turnSeat??0,priority=v?.prioritySeat??0,loki=v?.lokiSeat??0,
            request=r?.requestId??0,choiceSeat=r?.seat??0,kind=r?.kind,sequence=message?.appliedSequence??0,
            match=message?.matchId,phase=v?.phase,scene=SceneManager.GetActiveScene().name,status=status,error=error,
            command=completed,actedAt=actedAt,actionResult=actionResult,gate=service.CanSubmitRuleAction,
            canAct=v?.awaitingInput==true && r!=null,
            options=r?.options?.Where(o=>!o.disabled).Select(o=>o.id+"|"+o.label).ToArray(),
            players=v?.players?.Select(p=>new Seat{seat=p.seat,life=p.life,handCount=p.handCount,eliminated=p.eliminated,
                hand=p.hand?.Select(Describe).ToArray(),field=p.battlefield?.Select(Describe).ToArray()}).ToArray(),
            stack=v?.stackItems?.Select(e=>new Entry{id=e.id,controller=e.controllerSeat,name=e.source==null?e.description:Describe(e.source).name}).ToArray()};
        if(state.stack?.Length>0)state.rendered=Enumerable.Range(0,state.stack.Length)
            .Select(i=>GameObject.Find("StackEntry_"+i)).Where(go=>go!=null)
            .Select(go=>string.Join("|",go.GetComponentsInChildren<Text>().Select(t=>t.text).Where(s=>!string.IsNullOrWhiteSpace(s)))).ToArray();
        if(service.EndReview is OnlineMatchEndedMessage ended){
            state.ended=true;
            state.aborted=ended.aborted;
            state.winner=ended.winnerSeat;
            state.seriesWinner=ended.series?.winnerSeat??0;
            state.seriesFinished=ended.series?.finished==true;
            state.endMatchId=ended.matchId;
            var result=GameObject.Find("MatchResult/Result");
            if(result!=null)state.resultTitle=result.GetComponentsInChildren<Text>()
                .Select(t=>t.text).FirstOrDefault(t=>t.Contains("获胜"));
        }
        state.afterStack = v?.heroAfterStackSeat ?? 0;
        state.voteWindow = v?.heroVoteWindow ?? 0;
        state.voters = v?.heroVoters ?? 0;
        state.votes = v?.heroVotes ?? 0;
        state.combatSeat = v?.heroCombatSeat ?? 0;
        state.combatChoice = v?.heroCombatChoice;
        var vote = GameObject.Find("HeroStepVote")?.GetComponent<Button>();
        state.voteButton = vote?.GetComponentInChildren<Text>().text;
        state.voteEnabled = vote != null && vote.IsInteractable();
        state.advanceVisible = GameObject.Find("SkipCombatButton") != null;
        var panel = GameObject.Find("LokiPriority");
        if (panel != null) {
            state.priorityLabel = panel.transform.Find("PriorityOwner").GetComponent<Text>().text;
            state.priorityHint = panel.transform.Find("PriorityHint").GetComponent<Text>().text;
        }
        string file=Path.Combine(folder,"state.json");File.WriteAllText(file+".tmp",JsonUtility.ToJson(state));
        if(File.Exists(file))File.Replace(file+".tmp",file,null);else File.Move(file+".tmp",file);
    }
}
#endif
