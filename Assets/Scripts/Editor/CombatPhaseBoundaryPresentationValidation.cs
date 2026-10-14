#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CombatPhaseBoundaryPresentationValidation
{
    const string Base=".tools/combat-phase-boundaries-1008/",Output=Base+"ui/";
    [Serializable] class NativeResult { public string folder,status,runtimeHash; }
    static bool running;
    static int checks;
    static Transform CanvasRoot=>GameObject.Find("Canvas").transform;
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);checks++;}
    static ForgeSeatView Read(string path)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));
    static ForgeSeatView Native(string test,string name) {
        var result=JsonUtility.FromJson<NativeResult>(File.ReadAllText(Base+"native/"+test+"-result.json"));
        Check(result.status=="PASS","Native case not completed");
        return Read(result.folder+"/"+name+".json");
    }
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static IEnumerator Shot(string name){ScreenCapture.CaptureScreenshot(Output+name+".png");yield return new WaitForEndOfFrame();}
    static void Click(Button button) {
        Check(button!=null&&button.interactable&&button.gameObject.activeInHierarchy,"Unavailable button");
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Blocked ray "+button.name+": "+string.Join(",",hits.Take(3).Select(h=>h.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static Button Payment(string name)=>CanvasRoot.Find("CornerCluster/ControlRow/ManaPaymentActions/"+name).GetComponent<Button>();
    static void Step(string name)=>File.AppendAllText(Output+"steps.log",name+" PASS\n");
    public static void Run() {
        Check(Application.isPlaying&&!running,"Play/running state");running=true;checks=0;Directory.CreateDirectory(Output);File.WriteAllText(Output+"result.txt","RUNNING");File.WriteAllText(Output+"steps.log","");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("CombatPhaseValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui) {
        string language=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;
        var input=EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();bool inputEnabled=input!=null&&input.enabled;if(input!=null)input.enabled=false;
        var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var states=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try {
            var work=Validate(ui);while(true){object next;try{if(!work.MoveNext())break;next=work.Current;}catch(Exception e){File.WriteAllText(Output+"result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"result.txt","PASS "+checks+" actual native input replays: real source/defender rays to player, opposing planeswalker and own battle; exact option IDs, duplicate/stale/nonacting guards; fresh second-combat selection; both payer seats combat tax dock auto/cancel rays, native cost symbols/three locale labels and priority cleanup. Engine results registered separately; no multiplayer/live-network/full PASS.");
        } finally {ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(input!=null)input.enabled=inputEnabled;GamePreferences.SetLanguage(language);for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=states[i];running=false;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui) {
        GamePreferences.SetLanguage("zh-CN");float deadline=Time.realtimeSinceStartup+8;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready,"Locale ready");
        var original=Read("Tools/Validation/Reports/AttackDefenders/initial.json");
        foreach(string targetKind in new[]{"player","planeswalker","battle"}) {
            int calls=0,last=-1;ui.Present(null,1,false,null,null,"");ui.Present(original,1,true,i=>{calls++;last=i;},null,"");yield return Frame();
            var attacker=original.players[0].battlefield.First(c=>c.name=="Goblin Piker"&&GameObject.Find("Card_"+c.id)!=null);
            var target=targetKind=="player"?null:targetKind=="planeswalker"?original.players[1].battlefield.Single(c=>c.planeswalker):original.players[0].battlefield.Single(c=>c.battle);
            var destination=target==null?GameObject.Find("OpponentPlate"):GameObject.Find("Card_"+target.id);
            string id="attack:"+attacker.id+":"+(target==null?"player:"+original.players[1].targetId:"card:"+target.id);int index=ui.OptionIndex(id);Check(index>=0,"Exact defender option");
            Click(GameObject.Find("Card_"+attacker.id).GetComponent<Button>());yield return Frame();Check(calls==0,"Source click submitted");
            Check(!ui.TrySubmitPresentedOption(index,original.request.requestId-1)&&calls==0,"Stale attack submitted");
            yield return Shot("select-"+targetKind);Click(destination.GetComponent<Button>());destination.GetComponent<Button>().onClick.Invoke();Check(calls==1&&last==index,"Exact defender ray/duplicate");Step("defender-"+targetKind);
        }
        foreach(string test in new[]{"relentless-main2","celebrant-exert"}) {
            var first=Native(test,"attack-1");var second=Native(test,"attack-2");int calls=0,last=-1;
            ui.Present(null,1,false,null,null,"");ui.Present(first,1,true,_=>calls++,null,"");yield return Frame();
            var bear=first.players[0].battlefield.Single(c=>c.name=="Grizzly Bears");Click(GameObject.Find("Card_"+bear.id).GetComponent<Button>());yield return Frame();Check(calls==0,"First combat local draft");
            ui.Present(second,1,true,i=>{calls++;last=i;},null,"");yield return Frame();Check(GameObject.Find("AttackTargetHint")==null,"First combat draft leaked into second");
            Check(!ui.TryChooseAttackAt(new Vector2(3,3),first.request.requestId)&&calls==0,"First combat stale drag accepted");
            Click(GameObject.Find("Card_"+bear.id).GetComponent<Button>());yield return Frame();Click(GameObject.Find("OpponentPlate").GetComponent<Button>());
            Check(calls==1&&second.request.options[last].attackerId==bear.id&&second.request.options[last].defenderId==second.players[1].targetId,"Second combat exact declaration");Step(test+"-fresh-selection");yield return Shot(test+"-second-combat");
        }
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}) {
            GamePreferences.SetLanguage(locale);Check(GamePreferences.Language==locale,"Locale fallback");
            foreach(string test in new[]{"attack-tax-auto","block-tax-pay"}) {
                var payment=Native(test,"payment-0");int local=payment.request.seat;var metadata=JsonUtility.FromJson<NativeResult>(File.ReadAllText(Base+"native/"+test+"-result.json"));
                Check(metadata.runtimeHash==JsonUtility.FromJson<NativeResult>(File.ReadAllText("ForgeRuntime/runtime.json")).runtimeHash,"Current tax runtime identity");
                foreach(string action in new[]{"ok","cancel"}) {
                    int calls=0,last=-1;ui.Present(null,local,false,null,null,"");ui.Present(payment,local,true,i=>{calls++;last=i;},null,"");yield return Frame();
                    var auto=Payment("AutoPayment");var cancel=Payment("CancelPayment");Check(auto.interactable==(ui.OptionIndex("ok")>=0)&&cancel.interactable==(ui.OptionIndex("cancel")>=0),"Native payment gates");
                    Check(auto.GetComponentInChildren<Text>().text==(locale=="zh-CN"?"自动支付":locale=="en-US"?"Auto pay":"自動支払い"),"Actual localized payment label");
                    var prompt=CanvasRoot.Find("ManaPaymentPrompt").GetComponent<TMPro.TextMeshProUGUI>();Check(prompt.gameObject.activeSelf&&prompt.text.Contains(CardSymbolText.Format(payment.request.remainingManaCost)),"Actual cost symbols");
                    Check(CanvasRoot.Find("CornerCluster/ControlRow/PhaseTrack").gameObject.activeSelf&&!CanvasRoot.Find("ForgeChoices").gameObject.activeSelf,"Payment phase/duplicate UI");
                    var button=action=="ok"?auto:cancel;int index=ui.OptionIndex(action);Check(index>=0,"Exact tax action available");
                    Check(!ui.TrySubmitPresentedOption(index,payment.request.requestId-1)&&calls==0,"Stale payment accepted");
                    if(action=="ok")yield return Shot(locale+"-"+test);
                    Click(button);button.onClick.Invoke();Check(calls==1&&last==index,"Exact tax ray/duplicate");
                    ui.Present(payment,local,false,_=>calls++,null,"");yield return Frame();auto.onClick.Invoke();cancel.onClick.Invoke();Check(calls==1&&!auto.interactable&&!cancel.interactable,"Read-only payer can act");
                }
                ui.Present(Native(test,"resolved"),local,false,null,null,"");yield return Frame();Check(!Payment("AutoPayment").gameObject.activeInHierarchy&&!CanvasRoot.Find("ManaPaymentPrompt").gameObject.activeSelf,"Combat payment cleanup");Step(locale+"-"+test+"-ray-cleanup");
            }
        }
    }
}
#endif
