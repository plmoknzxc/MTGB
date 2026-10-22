#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public static class ForgeControlModeValidation
{
    const string Folder="Tools/Validation/Reports/ControlModes/";
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Policy()=>PolicyTo(Folder);
    public static void PolicyTo(string folder)
    {
        Directory.CreateDirectory(folder);
        var s=new ForgeSeatView{awaitingInput=true,seat=1,turnSeat=2,phase="DRAW",canRespond=true,
            request=new ForgeRequest{seat=1,kind="InputPassPriority",contextId=10,options=new[]{new ForgeOption{id="ok"}}}};
        Check(ForgeControlPolicy.AutomaticOption(s,1,ForgeControlMode.On,false)==-1,"ON lost draw priority");
        Check(ForgeControlPolicy.AutomaticOption(s,1,ForgeControlMode.Auto,false)==-1,"AUTO skipped available draw action");
        int phaseChecks=0;
        foreach(string phase in new[]{"UNTAP","UPKEEP","DRAW","MAIN1","COMBAT_BEGIN","DECLARE_ATTACKERS","DECLARE_BLOCKERS","COMBAT_DAMAGE","COMBAT_END","MAIN2","END_OF_TURN"})
        foreach(int player in new[]{1,2})foreach(int turn in new[]{1,2})foreach(bool action in new[]{false,true})foreach(var mode in new[]{ForgeControlMode.On,ForgeControlMode.Auto,ForgeControlMode.Off}) {
            s.phase=phase;s.request.playerSeat=player;s.turnSeat=turn;s.canRespond=action;s.attackStateKnown=true;s.hasLegalAttackers=false;
            bool ownMain=turn==player&&(phase=="MAIN1"||phase=="MAIN2"&&action);
            int expected=mode==ForgeControlMode.On||ownMain||mode==ForgeControlMode.Auto&&action?-1:0;
            Check(ForgeControlPolicy.AutomaticOption(s,1,mode,false)==expected,$"Priority policy {phase}/{player}/{turn}/{action}/{mode}");phaseChecks++;
        }
        s.phase="DRAW";s.request.playerSeat=0;s.turnSeat=2;s.canRespond=true;
        s.stackCount=1;s.stackItems=new[]{new ForgeStackItem{kind="trigger",controllerSeat=2}};
        Check(ForgeControlPolicy.AutomaticOption(s,1,ForgeControlMode.Auto,false)==-1,"AUTO skipped draw trigger");
        foreach(int controller in new[]{1,2}) {
            s.stackItems[0].controllerSeat=controller;s.canRespond=false;
            Check(ForgeControlPolicy.AutomaticOption(s,1,ForgeControlMode.Auto,false)==0,"AUTO stopped on unanswerable trigger");
            s.canRespond=true;
            Check(ForgeControlPolicy.AutomaticOption(s,1,ForgeControlMode.Auto,false)==-1,"AUTO skipped a legal response to trigger");
        }
        Check(ForgeControlPolicy.AutomaticOption(s,1,ForgeControlMode.Off,false)==0,"OFF stopped on trigger");
        s.stackCount=0;s.stackItems=Array.Empty<ForgeStackItem>();s.phase="MAIN1";s.turnSeat=1;s.canRespond=false;
        Check(ForgeControlPolicy.AutomaticOption(s,1,ForgeControlMode.Auto,false)==-1,"AUTO skipped own main phase");
        s.turnSeat=2;
        Check(ForgeControlPolicy.AutomaticOption(s,1,ForgeControlMode.Auto,false)==0,"AUTO stopped without responses");
        s.canRespond=true;Check(ForgeControlPolicy.AutomaticOption(s,1,ForgeControlMode.Auto,false)==-1,"AUTO skipped available response");
        s.canRespond=false;s.request.playerSeat=2;
        Check(ForgeControlPolicy.AutomaticOption(s,1,ForgeControlMode.Auto,false)==-1,"AUTO skipped controlled player's main phase");
        s.request.playerSeat=1;s.turnSeat=1;s.phase="MAIN2";
        foreach(var m in new[]{ForgeControlMode.Auto,ForgeControlMode.Off}){
            s.request.options=new[]{new ForgeOption{id="ok"},new ForgeOption{id="card:1",requiresPaymentCheck=true}};
            Check(ForgeControlPolicy.AutomaticOption(s,1,m,false)==-1,"Second main passed a native action requiring payment choice");
            s.request.options[1].disabled=true;
            Check(ForgeControlPolicy.AutomaticOption(s,1,m,false)==0,"Disabled uncertain action prevented empty second-main pass");
        }
        s.request.options=new[]{new ForgeOption{id="ok"}};
        s.request.playerSeat=0;
        s.request.kind="InputPayManaOfCostPayment";
        foreach(var mode in new[]{ForgeControlMode.On,ForgeControlMode.Auto,ForgeControlMode.Off})
            Check(ForgeControlPolicy.AutomaticOption(s,1,mode,false)==-1,"Payment must wait for explicit action in "+mode);
        Check(ForgeControlPolicy.AutomaticOption(s,1,ForgeControlMode.Auto,true)==-1,"Autopay retry loop");
        s.request.kind="InputSelectTargets";
        Check(ForgeControlPolicy.AutomaticOption(s,1,ForgeControlMode.Off,false)==-1,"OFF chose a mandatory target");
        s.request.kind="InputPassPriority";s.request.playerSeat=0;s.phase="DRAW";s.canRespond=false;
        s.request.options[0].disabled=true;
        Check(ForgeControlPolicy.AutomaticOption(s,1,ForgeControlMode.Auto,false)==-1,"AUTO submitted disabled pass");
        s.request.options[0].disabled=false;s.request.seat=2;
        Check(ForgeControlPolicy.AutomaticOption(s,1,ForgeControlMode.Auto,false)==-1,"AUTO acted for wrong seat");
        s.request.seat=1;s.awaitingInput=false;
        Check(ForgeControlPolicy.AutomaticOption(s,1,ForgeControlMode.Auto,false)==-1,"AUTO acted without input permission");
        File.WriteAllText(Path.Combine(folder,"policy.txt"),$"PASS: {phaseChecks} phase/mode/action/controlled-player combinations; trigger responses, explicit payment, targets, disabled pass and seat guards. Native canRespond excludes mana-only actions; no new rule-effect coverage.");
    }
    public static void Ui()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play mode required");
        var runner=new GameObject("ControlModeValidation").AddComponent<ForgeBattlefieldController>();
        UnityEngine.Object.DontDestroyOnLoad(runner.gameObject);runner.StartCoroutine(Guard(runner));
    }
    static IEnumerator Guard(ForgeBattlefieldController runner)
    {
        var steps=ValidateUi();Exception failure=null;
        while(true){object next=null;bool more=false;try{more=steps.MoveNext();if(more)next=steps.Current;}catch(Exception e){failure=e;}if(failure!=null||!more)break;yield return next;}
        File.WriteAllText(Folder+"ui.txt",failure==null?"PASS: round AUTO/OFF/ON cycle; payment mode buttons removed.":"FAIL "+failure);
        if(failure!=null)Debug.LogException(failure);UnityEngine.Object.Destroy(runner.gameObject);
    }
    static IEnumerator ValidateUi()
    {
        Directory.CreateDirectory(Folder);
        yield return SceneManager.LoadSceneAsync("IntegratedBattlefield");
        foreach(var n in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(n);
        var ui=new GameObject("ModeUiValidation").AddComponent<ForgeBattlefieldController>();
        var state=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Folder+"payment.json"));
        ui.Present(state,1,true,_=>{},()=>{},"验证");yield return null;
        var button=GameObject.Find("PriorityControlMode").GetComponent<Button>();var rect=(RectTransform)button.transform;
        Check(Mathf.Abs(rect.rect.width-rect.rect.height)<.1f && button.GetComponent<Image>().sprite!=null,"Mode button not circular");
        foreach(string text in new[]{"AUTO","OFF","ON","AUTO"}) {
            Check(button.GetComponentInChildren<Text>().text==text,"Mode cycle mismatch");
            Canvas.ForceUpdateCanvases();
            Check(button.GetComponentInChildren<Text>().cachedTextGenerator.lineCount==1,"Mode label wrapped");
            Check(!UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Any(b=>b!=button && b.gameObject.activeInHierarchy && b.GetComponentInChildren<Text>()?.text=="Auto"),"Payment Auto button remains");
            if(text=="ON") {ScreenCapture.CaptureScreenshot(Folder+"on-ui.png");yield return new WaitForEndOfFrame();}
            button.onClick.Invoke();
        }
        UnityEngine.Object.Destroy(ui.gameObject);
    }
}
#endif
