#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Native board seed with explicit priority/series display adapters. This tests
// the presentation and automatic submit seam, not new Forge rule outcomes.
public sealed class PhaseAndPlayerPlateValidation
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Hidden).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,Hidden).SetValue(o,v);
    static void Prop(object o,string n,object v)=>o.GetType().GetProperty(n).SetValue(o,v);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Hidden).Invoke(o,args);
    readonly List<string> steps=new();readonly List<string> failures=new();
    string output;ForgeBattlefieldController ui;OnlineMatchService owner;int submitted;long sequence=20000;bool supplement,priorityOnly;
    void Check(bool ok,string why){steps.Add((ok?"PASS ":"FAIL ")+why);if(!ok)failures.Add(why);}
    public static void Run(string output){
        Start(output,false);
    }
    public static void RunSupplement(string output)=>Start(output,true);
    public static void RunPriority(string output)=>Start(output,false,true);
    static void Start(string output,bool supplement,bool priorityOnly=false){
        if(!Application.isPlaying)throw new Exception("Play mode required");
        var go=new GameObject("PhasePlateValidation");UnityEngine.Object.DontDestroyOnLoad(go);
        var r=new PhaseAndPlayerPlateValidation{output=output,supplement=supplement,priorityOnly=priorityOnly,ui=go.AddComponent<ForgeBattlefieldController>()};r.ui.enabled=false;Directory.CreateDirectory(output);r.ui.StartCoroutine(r.Guard());
    }
    IEnumerator Guard(){
        File.WriteAllText(output+"/result.txt","RUNNING");var e=priorityOnly?PriorityAutomation():supplement?Supplement():Exercise();
        while(true){object next;try{if(!e.MoveNext())break;next=e.Current;}catch(Exception ex){failures.Add(ex.ToString());break;}yield return next;}
        (e as IDisposable)?.Dispose();File.WriteAllLines(output+"/steps.txt",steps);
        File.WriteAllText(output+"/result.txt",failures.Count==0?"PASS "+steps.Count(s=>s.StartsWith("PASS "))+" bounded UI/automatic-control checks":string.Join("\n",failures.Prepend("FAIL")));
        UnityEngine.Object.Destroy(ui.gameObject);
    }
    ForgeSeatView Priority(string phase,int turnSeat=1,bool response=false,int stack=0){
        var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(".tools/ui-browsing-boundary-1014/attack-after/initial.json"));
        v.turn=1;v.phase=phase;v.turnSeat=turnSeat;v.canRespond=response;v.stackCount=stack;v.stackItems=Array.Empty<ForgeStackItem>();v.notices=Array.Empty<ForgeNotice>();
        v.attackStateKnown=true;v.hasLegalAttackers=false;v.awaitingInput=true;
        v.request=new ForgeRequest{requestId=++sequence,seat=1,playerSeat=1,kind="InputPassPriority",options=new[]{new ForgeOption{id="ok",label="OK"}}};return v;
    }
    void Present(ForgeSeatView v){Prop(owner,"ForgeView",new OnlinePlayerViewState{forge=v});ui.Present(v,1,true,i=>submitted++,null,"");}
    static Rect Bounds(RectTransform r,Transform space){var c=new Vector3[4];r.GetWorldCorners(c);var p=c.Select(x=>space.InverseTransformPoint(x)).ToArray();return Rect.MinMaxRect(p.Min(x=>x.x),p.Min(x=>x.y),p.Max(x=>x.x),p.Max(x=>x.y));}
    static void Click(Button b){var r=(RectTransform)b.transform;var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);if(hits.Count==0||!hits[0].gameObject.transform.IsChildOf(b.transform))throw new Exception("Blocked mode button");ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    IEnumerator PriorityAutomation(){
        while(!GamePreferences.Ready)yield return null;
        owner=OnlineMatchService.EnsureInstance();if(owner.IsOnline||owner.IsInMatch)throw new Exception("Isolated test required");
        var oldView=owner.ForgeView;int oldSeat=owner.LocalSeat;var oldMode=owner.ControlMode;bool oldEnabled=owner.enabled;
        try{
            owner.enabled=false;Prop(owner,"LocalSeat",1);Set(ui,"service",owner);owner.ControlMode=ForgeControlMode.Auto;
            foreach(string phase in new[]{"UPKEEP","DRAW","COMBAT_BEGIN","DECLARE_ATTACKERS","COMBAT_END","END_OF_TURN"})foreach(int turn in new[]{1,2}){
                var v=Priority(phase,turn,true);int before=submitted;Present(v);
                Check(Field<Button>(ui,"advanceButton").gameObject.activeSelf,"Auto retains Next with an available action: "+turn+" "+phase);
                Call(ui,"UpdateAutomaticControl");Set(ui,"automaticReadyAt",0f);Set(ui,"phaseVisibleUntil",0f);Call(ui,"UpdateAutomaticControl");
                Check(submitted==before,"Auto sends no pass with available action: "+turn+" "+phase);
            }
            Present(Priority("DRAW",2));int prior=submitted;Call(ui,"UpdateAutomaticControl");Set(ui,"automaticReadyAt",0f);Set(ui,"phaseVisibleUntil",0f);
            Check(!Field<Button>(ui,"advanceButton").gameObject.activeSelf,"No-action automatic priority hides Next");
            Call(ui,"UpdateAutomaticControl");Check(submitted==prior+1,"No-action automatic priority submits once");Call(ui,"UpdateAutomaticControl");Check(submitted==prior+1,"Automatic pass is not duplicated");
            Present(Priority("UPKEEP",2));prior=submitted;Call(ui,"UpdateAutomaticControl");
            Click(Field<Button>(ui,"controlModeButton"));Click(Field<Button>(ui,"controlModeButton"));
            Check(owner.ControlMode==ForgeControlMode.On&&Field<Button>(ui,"advanceButton").gameObject.activeSelf,"Auto to Manual restores Next through actual button clicks");
            Set(ui,"automaticReadyAt",0f);Set(ui,"phaseVisibleUntil",0f);Call(ui,"UpdateAutomaticControl");Check(submitted==prior,"Manual cancels a previously scheduled automatic pass");
            owner.ControlMode=ForgeControlMode.Auto;var changed=Priority("UPKEEP",2);Present(changed);Call(ui,"UpdateAutomaticControl");changed.canRespond=true;Present(changed);
            Set(ui,"automaticReadyAt",0f);Set(ui,"phaseVisibleUntil",0f);Call(ui,"UpdateAutomaticControl");Check(submitted==prior&&Field<Button>(ui,"advanceButton").gameObject.activeSelf,"Newly available action in same request prevents pending pass");
            owner.ControlMode=ForgeControlMode.Off;Present(Priority("DRAW",2,true));Call(ui,"UpdateAutomaticControl");Set(ui,"automaticReadyAt",0f);Set(ui,"phaseVisibleUntil",0f);Call(ui,"UpdateAutomaticControl");Check(submitted==prior+1,"Explicit Skip still passes an available action");
            owner.ControlMode=ForgeControlMode.Auto;Present(Priority("UPKEEP",2,true));Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(output+"/automatic-action-stops.png");yield return new WaitForEndOfFrame();
        }finally{Set(ui,"service",null);Prop(owner,"ForgeView",oldView);Prop(owner,"LocalSeat",oldSeat);owner.ControlMode=oldMode;owner.enabled=oldEnabled;}
    }
    IEnumerator Supplement(){
        while(!GamePreferences.Ready)yield return null;
        owner=OnlineMatchService.EnsureInstance();if(owner.IsOnline||owner.IsInMatch)throw new Exception("Isolated test required");
        var oldLobby=owner.LobbyState;var oldView=owner.ForgeView;int oldSeat=owner.LocalSeat;var oldMode=owner.ControlMode;
        try{
            owner.enabled=false;Prop(owner,"LocalSeat",1);Set(ui,"service",owner);
            Prop(owner,"LobbyState",new OnlineLobbyState{players=new List<OnlineLobbyPlayer>{new OnlineLobbyPlayer{seat=1,displayName="林间旅人"},new OnlineLobbyPlayer{seat=2,displayName="旅する者"}},series=new OnlineSeriesState{bestOf=3,player1Wins=1,player2Wins=2}});
            owner.ControlMode=ForgeControlMode.Auto;Present(Priority("COMBAT_BEGIN",1,true));Check(Field<Button>(ui,"advanceButton").gameObject.activeSelf,"Auto retains own empty combat with an available response");
            var attackers=Priority("COMBAT_BEGIN",1,true);attackers.hasLegalAttackers=true;Present(attackers);Check(Field<Button>(ui,"advanceButton").gameObject.activeSelf,"Auto retains combat with legal attackers and actions");
            Present(Priority("DRAW"));Call(ui,"UpdateAutomaticControl");Click(Field<Button>(ui,"controlModeButton"));Click(Field<Button>(ui,"controlModeButton"));
            Check(owner.ControlMode==ForgeControlMode.On&&Field<Button>(ui,"advanceButton").gameObject.activeSelf,"Auto→Skip→Manual physical clicks restore Next immediately");yield return new WaitForSecondsRealtime(.3f);Call(ui,"UpdateAutomaticControl");Check(submitted==0,"Auto→Manual stops before scheduled submit");
            Present(Priority("MAIN1"));yield return new WaitForSecondsRealtime(.4f);Canvas.ForceUpdateCanvases();var authored=Field<Transform>(ui,"authored");
            foreach(string plate in new[]{"PlayerPlate","OpponentPlate"}){var name=authored.Find(plate+"/Name").GetComponent<Text>();var display=authored.Find(plate+"/Name/ScrollingName").GetComponent<Text>();Check(display.text==name.text&&display.preferredWidth<=name.rectTransform.rect.width&&display.preferredHeight<=name.rectTransform.rect.height,"CJK nickname fits "+plate);}
            ScreenCapture.CaptureScreenshot(output+"/main-player-plates.png");yield return new WaitForEndOfFrame();
            owner.ControlMode=ForgeControlMode.Auto;Present(Priority("DRAW"));yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(output+"/automatic-no-next.png");yield return new WaitForEndOfFrame();
            Set(ui,"viewingBattlefield",true);owner.ControlMode=ForgeControlMode.Off;Click(Field<Button>(ui,"controlModeButton"));
            Check(Field<Button>(ui,"advanceButton").gameObject.activeSelf,"Manual restores priority button even while viewing battlefield");
            ui.RestoreBattlefieldWindows();Check(Field<Button>(ui,"advanceButton").gameObject.activeSelf,"returning from battlefield retains manual Next");
        }finally{Set(ui,"service",null);Prop(owner,"LobbyState",oldLobby);Prop(owner,"ForgeView",oldView);Prop(owner,"LocalSeat",oldSeat);owner.ControlMode=oldMode;owner.enabled=true;}
    }
    IEnumerator Exercise(){
        while(!GamePreferences.Ready)yield return null;
        owner=OnlineMatchService.EnsureInstance();if(owner.IsOnline||owner.IsInMatch)throw new Exception("Isolated test required");
        var oldLobby=owner.LobbyState;var oldView=owner.ForgeView;int oldSeat=owner.LocalSeat;var oldMode=owner.ControlMode;float oldScale=GamePreferences.TextScale;string oldLanguage=GamePreferences.Language;
        try{
            owner.enabled=false;Prop(owner,"LocalSeat",1);Set(ui,"service",owner);
            Prop(owner,"LobbyState",new OnlineLobbyState{players=new List<OnlineLobbyPlayer>{new OnlineLobbyPlayer{seat=1,displayName="Player"},new OnlineLobbyPlayer{seat=2,displayName="Player gjpq"}},series=new OnlineSeriesState{bestOf=3}});
            foreach(var mode in new[]{ForgeControlMode.Auto,ForgeControlMode.Off})foreach(string phase in new[]{"UNTAP","UPKEEP","DRAW","COMBAT_BEGIN","COMBAT_END","END_OF_TURN"}){
                owner.ControlMode=mode;Present(Priority(phase));Check(!Field<Button>(ui,"advanceButton").gameObject.activeSelf,mode+" "+phase+" hides Next before any automatic update");
            }
            foreach(var mode in new[]{ForgeControlMode.On,ForgeControlMode.Auto,ForgeControlMode.Off})foreach(string phase in new[]{"MAIN1","MAIN2"}){
                owner.ControlMode=mode;Present(Priority(phase));Check(Field<Button>(ui,"advanceButton").gameObject.activeSelf,mode+" own "+phase+" retains manual Next");
            }
            owner.ControlMode=ForgeControlMode.Auto;Present(Priority("MAIN1",2,true));Check(Field<Button>(ui,"advanceButton").gameObject.activeSelf,"Auto keeps offered response window");
            owner.ControlMode=ForgeControlMode.Off;var delay=Priority("DRAW");Present(delay);Call(ui,"UpdateAutomaticControl");Click(Field<Button>(ui,"controlModeButton"));
            Check(owner.ControlMode==ForgeControlMode.On&&Field<Button>(ui,"advanceButton").gameObject.activeSelf,"physical Skip→Manual click restores Next immediately");
            yield return new WaitForSecondsRealtime(.3f);Call(ui,"UpdateAutomaticControl");Check(submitted==0,"Manual interrupts scheduled pass before submit");
            owner.ControlMode=ForgeControlMode.Off;Present(Priority("DRAW"));Call(ui,"UpdateAutomaticControl");yield return new WaitForSecondsRealtime(.3f);Call(ui,"UpdateAutomaticControl");steps.Add("DEBUG auto "+submitted+" mode="+owner.ControlMode+" now="+Time.unscaledTime+" ready="+Field<float>(ui,"automaticReadyAt")+" phase="+Field<float>(ui,"phaseVisibleUntil")+" uiSubmitted="+Field<bool>(ui,"submitted")+" request="+Field<long>(ui,"requestId")+" gate="+owner.CanSubmitRuleAction);Check(submitted==1,"Skip still submits one eligible pass");
            Click(Field<Button>(ui,"controlModeButton"));Present(Priority("UPKEEP"));Call(ui,"UpdateAutomaticControl");yield return new WaitForSecondsRealtime(.3f);Call(ui,"UpdateAutomaticControl");Check(submitted==1,"Manual stops next request after previously submitted pass");
            var authored=Field<Transform>(ui,"authored");Present(Priority("MAIN1"));yield return new WaitForSecondsRealtime(.3f);
            var track=authored.Find("CornerCluster/ControlRow/PhaseTrack");var cursor=(RectTransform)track.Find("CurrentPhase");float start=cursor.anchoredPosition.x;Present(Priority("MAIN2"));
            Check(Mathf.Abs(cursor.anchoredPosition.x-start)<.01f,"phase retarget starts from rendered position");yield return new WaitForSecondsRealtime(.06f);
            Check(cursor.anchoredPosition.x>start&&cursor.anchoredPosition.x<35,"phase marker has intermediate moving frame");
            steps.Add("DEBUG cursor start="+start+" now="+cursor.anchoredPosition.x+" active="+cursor.gameObject.activeInHierarchy+" moving="+cursor.GetComponent<ForgeUiMotion>()?.IsMoving+" phase="+Field<ForgeSeatView>(ui,"view").phase);
            ScreenCapture.CaptureScreenshot(output+"/phase-moving.png");yield return new WaitForEndOfFrame();
            Present(Priority("MAIN2"));yield return new WaitForSecondsRealtime(.3f);Check(Mathf.Abs(cursor.anchoredPosition.x-35)<.01f&&!cursor.GetComponent<ForgeUiMotion>().IsMoving,"same phase request does not restart movement");
            foreach(string language in new[]{"zh-CN","en-US","ja-JP"})foreach(float scale in new[]{1f,1.3f}){
                GamePreferences.SetLanguage(language);while(!GamePreferences.Ready||GamePreferences.Language!=language)yield return null;GamePreferences.SetScale(scale);
                yield return null;
                var v=Priority("MAIN1");foreach(var p in v.players)p.handCount=127;
                owner.LobbyState.series.bestOf=3;owner.LobbyState.series.player1Wins=1;owner.LobbyState.series.player2Wins=2;Present(v);yield return new WaitForSecondsRealtime(.6f);Canvas.ForceUpdateCanvases();
                foreach(string plate in new[]{"PlayerPlate","OpponentPlate"}){
                    var name=authored.Find(plate+"/Name").GetComponent<Text>();var display=authored.Find(plate+"/Name/ScrollingName").GetComponent<Text>();var hand=authored.Find(plate+"/Sub").GetComponent<Text>();var row=(RectTransform)authored.Find(plate+"/SeriesWins");
                    var mesh=display.cachedTextGenerator.verts;float ymin=mesh.Min(x=>x.position.y),ymax=mesh.Max(x=>x.position.y);var local=display.rectTransform.rect;
                    Check(display.text==name.text&&display.preferredWidth<=name.rectTransform.rect.width&&ymin>=local.yMin-1&&ymax<=local.yMax+1,language+"/"+scale+" "+plate+" full normal name mesh inside viewport");
                    Check(hand.fontSize==Mathf.RoundToInt(16*scale)&&hand.preferredWidth<=hand.rectTransform.rect.width,language+"/"+scale+" "+plate+" larger hand count fits 127");
                    steps.Add("DEBUG plate "+language+"/"+scale+" actual="+GamePreferences.Language+"/"+GamePreferences.TextScale+" font="+hand.fontSize+" width="+hand.preferredWidth+" rect="+hand.rectTransform.rect+" name="+display.text+" diamonds="+row.gameObject.activeSelf);
                    Check(row.Cast<Transform>().Count(t=>t.gameObject.activeSelf)==2&&row.gameObject.activeSelf&&!Bounds(hand.rectTransform,authored).Overlaps(Bounds(row,authored)),language+"/"+scale+" "+plate+" two BO3 diamonds separate from hand count");
                    int wins=plate=="PlayerPlate"?1:2;Check(row.GetComponentsInChildren<Image>().Count(x=>x.color.r>.9f)==wins,language+"/"+scale+" "+plate+" lights exact wins");
                }
                ScreenCapture.CaptureScreenshot(output+"/plate-"+language+"-"+scale+".png");yield return new WaitForEndOfFrame();
            }
            owner.LobbyState.series.bestOf=5;
            foreach(int wins in new[]{0,1,2,3}){owner.LobbyState.series.player1Wins=wins;Present(Priority("MAIN1"));var row=authored.Find("PlayerPlate/SeriesWins");Check(row.GetComponentsInChildren<Image>().Count(x=>x.color.r>.9f)==wins,"BO5 diamond count "+wins);}
            owner.LobbyState.series.bestOf=1;Present(Priority("MAIN1"));Check(!authored.Find("PlayerPlate/SeriesWins").gameObject.activeSelf,"BO1 has no series markers");
            owner.LobbyState.players[0].displayName="很长的玩家名字 Long player name 123456789";Present(Priority("MAIN1"));yield return null;
            var marquee=authored.Find("PlayerPlate/Name").GetComponent<PlayerNameMarquee>();marquee.OnPointerEnter(new PointerEventData(EventSystem.current));yield return new WaitForSecondsRealtime(.7f);
            var longName=authored.Find("PlayerPlate/Name/ScrollingName").GetComponent<Text>();Check(longName.text==owner.LobbyState.players[0].displayName&&longName.rectTransform.anchoredPosition.x<0,"long literal name remains complete and scrolls on hover");
        }finally{Set(ui,"service",null);Prop(owner,"LobbyState",oldLobby);Prop(owner,"ForgeView",oldView);Prop(owner,"LocalSeat",oldSeat);owner.ControlMode=oldMode;owner.enabled=true;GamePreferences.SetLanguage(oldLanguage);GamePreferences.SetScale(oldScale);}
    }
}
#endif
