#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ForgeBattlefieldValidation
{
    private static OnlineMatchSession session;
    private static ForgeBattlefieldController ui;
    private static double deadline, next;
    private static long answered;
    private static int callbacks;
    private static bool combat;
    private static bool blocking, sawAttack, sawBlock, invalidTargetChecked, mulligan;
    private static readonly bool[] mulliganRequested=new bool[2], kept=new bool[2];
    private static int blockClicks;
    private static bool castCancelled, castReturned;
    private static string ResultName => mulligan ? "mulligan-result" : blocking ? "blocking-result" : combat ? "combat-result" : "result";
    private static Action pendingPointer;
    private const string Pending = "MTGB.BattlefieldValidation.Pending";
    static ForgeBattlefieldValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending,false))
            {SessionState.SetBool(Pending,false);Start();}
            if(state==PlayModeStateChange.ExitingPlayMode) Cleanup();
        };
    }
    [MenuItem("MTGB/验证/战场/基本操作")]
    public static void Run()
    { SessionState.SetBool("MTGB.BattlefieldValidation.Mulligan",false); SessionState.SetBool("MTGB.BattlefieldValidation.Blocking",false); SessionState.SetBool("MTGB.BattlefieldValidation.Combat",false); Begin(); }
    [MenuItem("MTGB/验证/战场/攻击与伤害")]
    public static void RunCombat()
    { SessionState.SetBool("MTGB.BattlefieldValidation.Mulligan",false); SessionState.SetBool("MTGB.BattlefieldValidation.Blocking",false); SessionState.SetBool("MTGB.BattlefieldValidation.Combat",true); Begin(); }
    [MenuItem("MTGB/验证/战场/阻挡")]
    public static void RunBlocking()
    { SessionState.SetBool("MTGB.BattlefieldValidation.Mulligan",false); SessionState.SetBool("MTGB.BattlefieldValidation.Blocking",true); SessionState.SetBool("MTGB.BattlefieldValidation.Combat",true); Begin(); }
    [MenuItem("MTGB/验证/战场/起手调度")]
    public static void RunMulligan()
    { SessionState.SetBool("MTGB.BattlefieldValidation.Mulligan",true); SessionState.SetBool("MTGB.BattlefieldValidation.Blocking",false); SessionState.SetBool("MTGB.BattlefieldValidation.Combat",false); Begin(); }
    private static void Begin()
    {
        if(session!=null) throw new InvalidOperationException("Battlefield validation already running.");
        if(SceneManager.GetActiveScene().name!=ForgeBattlefieldController.SceneName)
            throw new InvalidOperationException("Open IntegratedBattlefield before this validation.");
        Directory.CreateDirectory(".tools/forge-migration/battlefield");
        string resultName=SessionState.GetBool("MTGB.BattlefieldValidation.Blocking",false)?"blocking-result":SessionState.GetBool("MTGB.BattlefieldValidation.Combat",false)?"combat-result":"result";
        if(SessionState.GetBool("MTGB.BattlefieldValidation.Mulligan",false))resultName="mulligan-result";
        File.WriteAllText(".tools/forge-migration/battlefield/"+resultName+".txt","RUNNING");
        if(EditorApplication.isPlaying) Start();
        else {SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    }
    private static void Start()
    {
        try
        {
            combat=SessionState.GetBool("MTGB.BattlefieldValidation.Combat",false);
            mulligan=SessionState.GetBool("MTGB.BattlefieldValidation.Mulligan",false);
            Array.Clear(mulliganRequested,0,2);Array.Clear(kept,0,2);
            blocking=SessionState.GetBool("MTGB.BattlefieldValidation.Blocking",false);
            sawAttack=sawBlock=invalidTargetChecked=false; blockClicks=0;
            castCancelled=castReturned=false;
            File.WriteAllText(".tools/forge-migration/battlefield/"+ResultName+"-trace.jsonl","");
            foreach(var notice in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(notice);
            if(mulligan)
            {
                // Reproduce scene entry with a cold preference binding on the hidden stack.
                // Previous tests started with this header active and missed the deferred Awake.
                var column=GameObject.Find("Canvas").transform.Find("StackColumn");column.gameObject.SetActive(false);
                var header=column.Find("Header").gameObject;
                var previous=header.GetComponent<PreferenceText>();
                if(previous!=null)UnityEngine.Object.DestroyImmediate(previous);
                header.AddComponent<PreferenceText>();
            }
            ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
            if(ui==null)ui=new GameObject("BattlefieldValidation").AddComponent<ForgeBattlefieldController>();
            var deck=DeckRepository.Load(Path.Combine(DeckRepository.DeckRoot,"Starter.mdk"));
            if(blocking)
            {
                // In-memory fixture only: official printings, normal shuffled opening and costs.
                deck=new DeckDefinition { name="Blocking validation", cards=deck.cards
                    .Where(c=>c.PrintingKey=="10E/376" || c.PrintingKey=="10E/224")
                    .Select(c=>new DeckEntry {setCode=c.setCode,collectorNumber=c.collectorNumber,cardId=c.cardId,count=30}).ToList() };
                if(deck.cards.Count!=2)throw new Exception("Blocking fixture requires Starter Mountain and Raging Goblin printings.");
            }
            session=new OnlineMatchSession(Guid.NewGuid().ToString("N"),deck,deck,4);
            deadline=EditorApplication.timeSinceStartup+(blocking?180:90); answered=0; callbacks=0;
            EditorApplication.update+=Tick;
        }
        catch(Exception e){Fail(e);}
    }
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+.3;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Battlefield UI validation timed out.");
            if(pendingPointer!=null) { var click=pendingPointer; pendingPointer=null; click(); return; }
            session.Poll();
            if(session.Failure!=null)throw new Exception(session.Failure);
            for(int seat=1;seat<=2;seat++)
            {
                var view=session.GetView(seat).forge;
                if(view?.players==null || view.players.Length==0)continue;
                if(view.players.Any(p=>p.seat!=seat && p.hand.Length>0))throw new Exception("Opponent hand leaked.");
                if(mulligan && view.turn>0)
                {
                    if(!kept.All(k=>k) || !view.players.All(p=>p.handCount==6))throw new Exception("Mulligan opening hand count is wrong.");
                    File.WriteAllText(".tools/forge-migration/battlefield/"+ResultName+".txt","PASS: both seats redraw seven, click Keep before bottom selection, select and confirm one card through scene raycasts; opening hands 6/6.");
                    ScreenCapture.CaptureScreenshot(".tools/forge-migration/battlefield/mulligan.png");Cleanup();return;
                }
                sawAttack |= view.players.Any(p=>p.battlefield.Any(c=>c.attacking));
                sawBlock |= view.players.Any(p=>p.battlefield.Any(c=>c.blocking));
                bool blockedResolved=blocking && sawAttack && sawBlock && blockClicks==3
                    && view.players.All(p=>p.life==20 && p.graveyard.Any(c=>c.Contains("Raging Goblin")))
                    && view.players.All(p=>!p.battlefield.Any(c=>c.name=="Raging Goblin"));
                if(blockedResolved || (!blocking && view.players[1].life==(combat?19:17)))
                {
                    if(!combat && (!castCancelled || !castReturned))throw new Exception("Casting cancellation was not verified before settlement.");
                    ui.Present(session.GetView(1).forge,1,false,null,null,"战场交互验证通过");
                    ScreenCapture.CaptureScreenshot(".tools/forge-migration/battlefield/"+(blocking?"blocking":combat?"combat":"resolved")+".png");
                    string result=blocking?"PASS: official 60-card fixture; both players cast Raging Goblin; UI attack, block, remove block, reassign block; combat kills both creatures, both graveyards verified; life remains 20/20.":combat?"PASS: real Starter deck; cast Raging Goblin, haste attack via UI, combat damage 20 -> 19.":"PASS: real Starter deck; UI buttons for mulligan, land, Bolt, player target, mana, priority; duplicate clicks rejected; 20 -> 17. Invalid target candidate checked="+invalidTargetChecked;
                    File.AppendAllText(".tools/forge-migration/battlefield/"+ResultName+"-trace.jsonl",JsonUtility.ToJson(view)+"\n");
                    if(!combat)result+=" Cancel through the scene returns Bolt to hand with life unchanged; recast resolves normally.";
                    File.WriteAllText(".tools/forge-migration/battlefield/"+ResultName+".txt",result);
                    Debug.Log("MTGB battlefield UI validation passed: "+result);
                    Cleanup();return;
                }
                if(!view.awaitingInput || view.request.requestId==answered)continue;
                var req=view.request;
                File.AppendAllText(".tools/forge-migration/battlefield/"+ResultName+"-trace.jsonl",JsonUtility.ToJson(view)+"\n");
                if(req.kind=="InputSelectTargets" && !invalidTargetChecked)
                {
                    string before=JsonUtility.ToJson(session.GetView(seat));
                    var rejected=session.ApplyAction(new GameAction{type=GameActionType.ConfirmChoice,playerId=seat,sourceObjectId=req.requestId,optionId=req.options.Length});
                    if(rejected.accepted || before!=JsonUtility.ToJson(session.GetView(seat)))throw new Exception("Invalid target candidate changed state.");
                    invalidTargetChecked=true;
                }
                int pick=Array.FindIndex(req.options,o=>o.id=="ok");if(pick<0)pick=0;
                if(mulligan)
                {
                    if(req.kind=="InputConfirmMulligan")
                    {
                        if(view.players.First(p=>p.seat==seat).handCount!=7)throw new Exception("Keep must be offered on the full seven-card redraw.");
                        if(!mulliganRequested[seat-1]){pick=Array.FindIndex(req.options,o=>o.id=="cancel");mulliganRequested[seat-1]=true;}
                        else kept[seat-1]=true;
                    }
                    else if(req.kind=="InputLondonMulligan")
                    {
                        if(!kept[seat-1])throw new Exception("Bottom selection appeared before Keep.");
                        if(req.remainingCount>0)pick=Array.FindIndex(req.options,o=>o.id.StartsWith("card:") && !o.label.StartsWith("[已选"));
                    }
                }
                else if(blocking)
                {
                    var own=view.players.First(p=>p.seat==seat);
                    if(req.kind=="InputPassPriority" && view.turnSeat==seat && view.phase.Contains("MAIN") && view.stackCount==0)
                    {
                        string wanted=!own.battlefield.Any(c=>c.land)?"Mountain [Hand]":!own.battlefield.Any(c=>c.creature)?"Raging Goblin [Hand]":null;
                        int n=Array.FindIndex(req.options,o=>o.label==wanted);if(n>=0)pick=n;
                    }
                    else if(req.kind.StartsWith("InputPayMana"))
                    {int n=Array.FindIndex(req.options,o=>o.id=="mana:R" || own.battlefield.Any(c=>c.land && !c.tapped && o.id=="card:"+c.id));if(n>=0)pick=n;}
                    else if(req.kind.Contains("Attack") && seat==1 && view.players.All(p=>p.battlefield.Any(c=>c.creature)))
                    {int n=Array.FindIndex(req.options,o=>o.id.StartsWith("attack:") && o.defenderKind=="player" && own.battlefield.Any(c=>c.creature && !c.attacking && o.attackerId==c.id));if(n>=0)pick=n;}
                    else if(req.kind.Contains("Block") && seat==2 && blockClicks<3)
                    {
                        bool assigned=own.battlefield.Any(c=>c.blocking);
                        if(assigned!=(blockClicks==1))throw new Exception("Block toggle state disagrees with prior click.");
                        int n=Array.FindIndex(req.options,o=>own.battlefield.Any(c=>c.creature && o.id=="card:"+c.id));
                        if(n<0)throw new Exception("No legal blocker candidate.");pick=n;blockClicks++;
                    }
                }
                else if(seat==1)
                {
                    var own=view.players[0];
                    if(req.kind=="InputPassPriority" && view.phase.Contains("MAIN"))
                    {
                        if(castCancelled && !castReturned)
                        {
                            if(!own.hand.Any(c=>c.name=="Lightning Bolt") || view.players[1].life!=20 || view.stackCount!=0)
                                throw new Exception("Cancelled spell did not return to hand without changing life/stack.");
                            castReturned=true;
                        }
                        string wanted=own.battlefield.Length==0?"Mountain [Hand]":combat?"Raging Goblin [Hand]":"Lightning Bolt [Hand]";
                        int n=Array.FindIndex(req.options,o=>o.label==wanted);if(n>=0)pick=n;
                    }
                    else if(req.kind=="InputSelectTargets")
                    {
                        int n=Array.FindIndex(req.options,o=>o.id==(!castCancelled?"cancel":"player:"+view.players[1].targetId));
                        if(n>=0)pick=n;
                        if(req.options[pick].id=="cancel")castCancelled=true;
                    }
                    else if(req.kind.StartsWith("InputPayMana"))
                    {int n=Array.FindIndex(req.options,o=>o.id=="mana:R" || own.battlefield.Any(c=>!c.tapped && o.id=="card:"+c.id));if(n>=0)pick=n;}
                    else if(combat && req.kind.Contains("Attack"))
                    {int n=Array.FindIndex(req.options,o=>o.id.StartsWith("attack:") && o.defenderKind=="player" && own.battlefield.Any(c=>c.creature && !c.attacking && o.attackerId==c.id));if(n>=0)pick=n;}
                }
                int actingSeat=seat;
                ui.Present(view,seat,true,index=>{
                    callbacks++;
                    var result=session.ApplyAction(new GameAction{type=GameActionType.ConfirmChoice,playerId=actingSeat,sourceObjectId=req.requestId,optionId=index});
                    if(!result.accepted)throw new Exception("UI option rejected.");
                },null,"真实牌组 · 战场输入验证");
                if(view.stackCount>0 || req.kind.Contains("PayMana"))
                {
                    Directory.CreateDirectory("Tools/Validation/Reports/StackCards");
                    ScreenCapture.CaptureScreenshot("Tools/Validation/Reports/StackCards/"+(view.stackCount>0?"stack":"paying")+".png");
                }
                string id=req.options[pick].id;
                string buttonName=id.StartsWith("card:")?"Card_"+id.Substring(5):"Option_"+pick;
                if(id.StartsWith("attack:"))buttonName="Card_"+req.options[pick].attackerId;
                if(id=="ok")buttonName="SkipCombatButton";
                if(req.kind.Contains("Mulligan") && (id=="ok" || id=="cancel"))buttonName="Mulligan_"+id;
                if(id=="cancel" && req.kind=="InputPassPriority")buttonName="UndoMana";
                if(id=="cancel" && req.kind=="InputSelectTargets")buttonName="CancelTarget";
                if(id.StartsWith("player:"))buttonName=view.players.First(p=>id=="player:"+p.targetId).seat==seat?"PlayerPlate":"OpponentPlate";
                var button=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b=>b.name==buttonName && b.gameObject.activeInHierarchy);
                if(button==null || !button.interactable)throw new Exception("Missing actionable UI button: "+buttonName);
                // Raycast on the next rendered frame so newly created Graphics have valid depths.
                pendingPointer=()=> {
                Canvas.ForceUpdateCanvases();
                if(req.kind=="InputConfirmMulligan")
                {
                    var keep=GameObject.Find("Mulligan_ok");var redraw=GameObject.Find("Mulligan_cancel");
                    if(keep==null || redraw==null || keep.transform.parent!=redraw.transform.parent)throw new Exception("Keep and mulligan are not grouped.");
                    if(GameObject.Find("Canvas").transform.Find("CornerCluster/ControlRow/SkipCombatButton").gameObject.activeSelf)throw new Exception("Keep remains split into the corner.");
                    ScreenCapture.CaptureScreenshot(".tools/forge-migration/battlefield/mulligan-choices.png");
                }
                if(view.stackCount>0)
                {
                    if(view.stackItems==null || view.stackItems.Length!=view.stackCount)throw new Exception("Stack source projection missing");
                    var stackImage=GameObject.Find("StackCard_0")?.GetComponent<Image>();
                    if(stackImage==null || stackImage.sprite==null)throw new Exception("Stack card image missing");
                    var clip=(RectTransform)GameObject.Find("Canvas").transform.Find("StackColumn/Viewport");
                    var corners=new Vector3[4];stackImage.rectTransform.GetWorldCorners(corners);
                    if(corners.Any(c=>!RectTransformUtility.RectangleContainsScreenPoint(clip,RectTransformUtility.WorldToScreenPoint(null,c))))
                        throw new Exception("Stack card is clipped by its viewport");
                }
                if(req.kind.Contains("PayMana"))
                {
                    var paying=GameObject.Find("TargetOrigin")?.GetComponent<Image>();
                    if(paying==null || paying.sprite==null)throw new Exception("Spell image missing during payment");
                }
                var rect=(RectTransform)button.transform;
                var es=UnityEngine.EventSystems.EventSystem.current;
                var pointer=new UnityEngine.EventSystems.PointerEventData(es) {
                    position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),
                    button=UnityEngine.EventSystems.PointerEventData.InputButton.Left
                };
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                es.RaycastAll(pointer,hits);
                if(hits.Count==0 || !hits[0].gameObject.transform.IsChildOf(button.transform))
                    throw new Exception("UI button is occluded: "+buttonName);
                int before=callbacks;
                if(id.StartsWith("attack:"))
                {
                    UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
                    var destination=GameObject.Find("OpponentPlate").transform;
                    pointer.position=RectTransformUtility.WorldToScreenPoint(null,destination.position);
                    Canvas.ForceUpdateCanvases();
                    UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.dragHandler);
                    UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
                    UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
                }
                else if(req.kind=="InputSelectTargets" && id!="cancel")
                {
                    var origin=GameObject.Find("TargetOrigin");var arrow=GameObject.Find("TargetArrow");
                    if(origin==null || arrow==null)throw new Exception("Target arrow or casting origin is missing.");
                    if(arrow.GetComponent<CanvasRenderer>()==null)throw new Exception("Target arrow has no renderer.");
                    if(((RectTransform)origin.transform).anchoredPosition.x<300)throw new Exception("Casting preview is not on the right.");
                    ui.UpdateTargetingAt(pointer.position);Canvas.ForceUpdateCanvases();
                    var arrowMesh=arrow.GetComponent<CanvasRenderer>().GetMesh();
                    if(arrowMesh.vertexCount<400)throw new Exception("Curved targeting glow has not rendered.");
                    if(ui.TryChooseTargetAt(new Vector2(1,1),req.requestId))throw new Exception("Empty-space target accepted.");
                    if(ui.TryChooseTargetAt(pointer.position,req.requestId-1))throw new Exception("Stale drag accepted.");
                    var handle=origin.GetComponent<ForgeTargetDragHandle>();
                    handle.OnBeginDrag(pointer);handle.OnDrag(pointer);handle.OnEndDrag(pointer);handle.OnEndDrag(pointer);
                }
                else
                {
                    UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                    UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                }
                if(callbacks!=before+1)throw new Exception("Duplicate UI click was not blocked.");
                if(session.ApplyAction(new GameAction{type=GameActionType.ConfirmChoice,playerId=actingSeat,sourceObjectId=req.requestId,optionId=pick}).accepted)
                    throw new Exception("Already submitted request accepted twice.");
                answered=req.requestId;
                };
                return;
            }
        }
        catch(Exception e){Fail(e);}
    }
    private static void Fail(Exception e)
    {Debug.LogException(e);Directory.CreateDirectory(".tools/forge-migration/battlefield");File.WriteAllText(".tools/forge-migration/battlefield/"+ResultName+".txt","FAIL: "+e);Cleanup();}
    private static void Cleanup(){EditorApplication.update-=Tick;pendingPointer=null;session?.Dispose();session=null;}
}
#endif
