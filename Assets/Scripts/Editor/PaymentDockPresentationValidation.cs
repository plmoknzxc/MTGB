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

// Replays focused real Forge requests; does not replace native result assertions.
public static class PaymentDockPresentationValidation
{
    [Serializable] class SourceRefundMeta { public string folder,status,runtimeHash; }
    public static void RunSourceRefunds() {
        Check(Application.isPlaying&&!running,"Play/validation state");running=true;
        const string output=".tools/mana-source-refunds-1007/ui";Directory.CreateDirectory(output);File.WriteAllText(output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("RefundDockValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(SourceRefundGuard(ui,output));
    }
    static ForgeSeatView SourceRefundRead(string test,string file) {
        var m=JsonUtility.FromJson<SourceRefundMeta>(File.ReadAllText(".tools/mana-source-refunds-1007/native/"+test+"-result.json"));
        var rt=JsonUtility.FromJson<SourceRefundMeta>(File.ReadAllText("ForgeRuntime/runtime.json"));Check(m.status=="PASS"&&m.runtimeHash==rt.runtimeHash,"Native refund identity");
        return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(m.folder+"/"+file+".json"));
    }
    static IEnumerator SourceRefundGuard(ForgeBattlefieldController ui,string output) {
        bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();bool inputs=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var states=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        string language=GamePreferences.Language;GamePreferences.SetLanguage("zh-CN");
        try {
            var work=SourceRefundSteps(ui,output);
            while(true){object next;try{if(!work.MoveNext())break;next=work.Current;}catch(Exception e){File.WriteAllText(output+"/result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(output+"/result.txt","PASS zh-CN actual native nested filter-cost -> parent payment -> cancellation priority cleanup, original indices/old-request/duplicate guards via real ray; life and sacrifice mana-source confirmation -> payment cleanup. Historical three-language payment/pool/confirmation contracts retained; no new UI first or live-network claim.");
        } finally {ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=inputs;GamePreferences.SetLanguage(language);for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=states[i];running=false;}
    }
    static IEnumerator SourceRefundSteps(ForgeBattlefieldController ui,string output) {
        float deadline=Time.realtimeSinceStartup+8;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready,"Locale ready");
        File.WriteAllText(output+"/steps.log","");
        var nested=SourceRefundRead("filter-chain","nested-filter-payment");ui.Present(nested,1,true,_=>{},null,"");yield return Frame();
        var prompt=Root.Find("ManaPaymentPrompt").GetComponent<TMPro.TextMeshProUGUI>();Check(prompt.text.Contains(CardSymbolText.Format("{1}")),"Nested mana cost");
        var parent=SourceRefundRead("filter-chain","generated");int calls=0,last=-1;ui.Present(parent,1,true,i=>{calls++;last=i;},null,"");yield return Frame();Check(prompt.text.Contains(CardSymbolText.Format("{B}")),"Parent cost refresh");
        Check(!ui.TrySubmitPresentedOption(ui.OptionIndex("cancel"),nested.request.requestId),"Old nested input accepted");
        var cancel=Payment("CancelPayment");Click(cancel);cancel.onClick.Invoke();Check(calls==1&&last==ui.OptionIndex("cancel"),"Parent cancel index/duplicate");
        ui.Present(SourceRefundRead("filter-chain","cancelled"),1,true,_=>{},null,"");yield return Frame();Check(!cancel.gameObject.activeInHierarchy&&!prompt.gameObject.activeSelf,"Refund priority cleanup");File.AppendAllText(output+"/steps.log","Nested filter/parent/cancel ray and cleanup PASS\n");
        foreach(var test in new[]{"life-source","sacrifice-source"}) {
            var decision=SourceRefundRead(test,"step-20");calls=0;last=-1;ui.Present(decision,1,true,i=>{calls++;last=i;},null,"");yield return Frame();int index=ui.OptionIndex("ok");
            var menu=GameObject.Find("CardAbilityChoices");Check(menu!=null,"Source confirmation missing");Click(menu.GetComponentsInChildren<Button>().Single(b=>b.name=="ValueOption_"+index));Check(calls==1&&last==index,"Source confirmation index");
            ui.Present(SourceRefundRead(test,"generated"),1,true,_=>{},null,"");yield return Frame();Check(GameObject.Find("CardAbilityChoices")==null&&Payment("CancelPayment").gameObject.activeInHierarchy,"Source confirmation/payment cleanup");File.AppendAllText(output+"/steps.log",test+" confirmation ray/payment cleanup PASS\n");
        }
    }
    const string Output=".tools/resolution-ui-1005/payment";
    static bool running;
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static ForgeSeatView Read(string file)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(".tools/payment-dock-1005/native/"+file+".json"));
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.18f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static Button Payment(string name)=>Root.Find("CornerCluster/ControlRow/ManaPaymentActions/"+name).GetComponent<Button>();
    static Rect Bounds(RectTransform rect){var c=new Vector3[4];rect.GetWorldCorners(c);return Rect.MinMaxRect(c.Min(v=>v.x),c.Min(v=>v.y),c.Max(v=>v.x),c.Max(v=>v.y));}
    static FieldInfo Field(string name)=>typeof(ForgeBattlefieldController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
    static void CheckAutomaticPayment(ForgeBattlefieldController ui) {
        var original=Field("service").GetValue(ui);bool wasEnabled=ui.enabled;
        bool created=OnlineMatchService.Instance==null;
        var owner=OnlineMatchService.EnsureInstance();bool ownerEnabled=owner.enabled;var mode=owner.ControlMode;var ownerView=owner.ForgeView;
        var serviceView=typeof(OnlineMatchService).GetProperty("ForgeView");
        ui.enabled=false;owner.enabled=false;Field("service").SetValue(ui,owner);
        try {
            var tax=Read("tax-auto-payment");
            var update=typeof(ForgeBattlefieldController).GetMethod("UpdateAutomaticControl",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(var control in new[]{ForgeControlMode.Auto,ForgeControlMode.Off})foreach(int origin in new[]{-1,tax.stackItems.Single(s=>s.source.name=="Opt").source.id}) {
                int calls=0;owner.ControlMode=control;serviceView.SetValue(owner,new OnlinePlayerViewState{forge=tax});ui.Present(tax,1,true,_=>calls++,null,"");
                Field("castingCardId").SetValue(ui,origin);Field("automaticRequest").SetValue(ui,tax.request.requestId);Field("automaticReadyAt").SetValue(ui,0f);
                update.Invoke(ui,null);Check(calls==0,"Resolution payment skipped explicit decision / "+control);
            }
            ui.Present(null,1,false,null,null,"");var cast=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/ManaPoolPayments/blue-payment.json"));
            int submitted=0;owner.ControlMode=ForgeControlMode.Auto;serviceView.SetValue(owner,new OnlinePlayerViewState{forge=cast});ui.Present(cast,1,true,_=>submitted++,null,"");
            Field("castingCardId").SetValue(ui,999999);Field("automaticRequest").SetValue(ui,cast.request.requestId);Field("automaticReadyAt").SetValue(ui,0f);Field("attemptedPaymentContext").SetValue(ui,-1L);
            update.Invoke(ui,null);Check(submitted==1,"Local casting auto payment regressed");
            ui.Present(cast,1,true,_=>submitted++,null,"");update.Invoke(ui,null);Check(submitted==1,"Same cost retried automatically");
        } finally {
            ui.Present(null,1,false,null,null,"");Field("service").SetValue(ui,original);ui.enabled=wasEnabled;owner.ControlMode=mode;owner.enabled=ownerEnabled;serviceView.SetValue(owner,ownerView);
            if(created)UnityEngine.Object.Destroy(owner.gameObject);
        }
    }
    static void Click(Button button){
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<Button>()==button,"Blocked click "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    public static void Run(){
        if(running)return;Check(Application.isPlaying,"Enter battlefield play mode");running=true;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/ui-result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("PaymentDockValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator iterator){
        string original=GamePreferences.Language;
        foreach(var notice in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))notice.enabled=false;
        try {
            while(true){object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}catch(Exception e){File.WriteAllText(Output+"/ui-result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"/ui-result.txt","PASS: zh-CN/en-US/ja-JP native shockland yes/no and resolution payment replay; exact auto/cancel IDs with real raycasts; insufficient-cost disabled auto; stale/duplicate/nonacting/pending guards; actions replace advance in right dock, symbols instead of braces, fixed fonts/bounds, no duplicate payment window; view switching plus ordinary priority recovery; actual automatic-control invocation preserves resolution choices and local-casting payment with no duplicate retry. Native rules checked separately; no full-pool or multiplayer claim.");
        }finally{running=false;GamePreferences.SetLanguage(original);}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui){
        CheckAutomaticPayment(ui);yield return null;
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool change=locale!=GamePreferences.Language;GamePreferences.SetLanguage(locale);
            float deadline=Time.realtimeSinceStartup+10;while((!GamePreferences.Ready||change&&GamePreferences.Revision==revision)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(GamePreferences.Ready,"Locale not ready");
            var shock=Read("shock-yes-confirm");int calls=0,last=-1;
            ui.Present(null,1,false,null,null,"");ui.Present(shock,1,true,i=>{calls++;last=i;},null,"");yield return Frame();
            var menu=GameObject.Find("CardAbilityChoices");Check(menu!=null && menu.GetComponentsInChildren<Button>().Count()==2,"Shockland choice changed");
            ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-single-shockland.png");yield return new WaitForEndOfFrame();
            var label=menu.GetComponentsInChildren<Text>().First();int labelSize=label.fontSize;
            ui.ViewBattlefield();yield return Frame();Check(menu.GetComponent<CanvasGroup>().alpha==0,"Single decision not hidden for battlefield view");
            ui.RestoreBattlefieldWindows();yield return Frame();Check(menu.GetComponent<CanvasGroup>().alpha==1&&label.fontSize==labelSize&&calls==0,"Decision/font changed on battlefield return");
            foreach(string option in new[]{"ok","cancel"}) {
                ui.Present(shock,1,true,i=>{calls++;last=i;},null,"");yield return Frame();
                int index=ui.OptionIndex(option);Click(GameObject.Find("CardAbilityChoices").GetComponentsInChildren<Button>().Single(b=>b.name=="ValueOption_"+index));
                Check(last==index,"Wrong shockland option");
            }
            foreach(string file in new[]{"tax-auto-payment","tax-cancel-payment","tax-insufficient-payment","tax-manual-payment"}) {
                var payment=Read(file);calls=0;last=-1;ui.Present(null,1,false,null,null,"");ui.Present(payment,1,true,i=>{calls++;last=i;},null,"");yield return Frame();
                var auto=Payment("AutoPayment");var cancel=Payment("CancelPayment");
                Check(auto.gameObject.activeInHierarchy&&cancel.gameObject.activeInHierarchy,"Payment dock missing");
                Check(auto.interactable==(ui.OptionIndex("ok")>=0),"Client invented automatic affordability");
                Check(cancel.interactable==(ui.OptionIndex("cancel")>=0),"Cancel permission differs");
                Check(!Root.Find("ForgeChoices").gameObject.activeSelf,"Payment opens generic window");
                Check(!Root.Find("CornerCluster/ControlRow/SkipCombatButton").gameObject.activeSelf,"Advance overlaps payment");
                Check(Root.Find("CornerCluster/ControlRow/PhaseTrack").gameObject.activeSelf,"Phase track missing during payment");
                var a=Bounds((RectTransform)auto.transform);var c=Bounds((RectTransform)cancel.transform);var mode=Bounds((RectTransform)Root.Find("PriorityControlMode"));
                Check(!a.Overlaps(c)&&!a.Overlaps(mode)&&!c.Overlaps(mode),"Payment controls overlap");
                Check(Mathf.Abs(a.center.y-c.center.y)<1&&a.xMin>c.xMax,"Payment buttons must be left/right");
                foreach(var button in new[]{auto,cancel}) {
                    var t=button.GetComponentInChildren<Text>();Check(!t.resizeTextForBestFit&&t.preferredWidth<t.rectTransform.rect.width&&t.preferredHeight<t.rectTransform.rect.height,"Payment label clipped/resized "+locale);
                    Check(button.targetGraphic.canvasRenderer.GetMesh()?.vertexCount>0,"Payment face not rendered");
                }
                var prompt=Root.Find("ManaPaymentPrompt").GetComponent<TMPro.TextMeshProUGUI>();Check(!prompt.text.Contains("{3}")&&!prompt.enableAutoSizing&&prompt.text.Contains(CardSymbolText.Format("{3}")),"Cost not symbolized");
                ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-"+file+".png");yield return new WaitForEndOfFrame();
                int autoIndex=ui.OptionIndex("ok");if(autoIndex<0){auto.onClick.Invoke();Check(calls==0,"Unavailable auto submitted");}
                else {Click(auto);auto.onClick.Invoke();Check(calls==1&&last==autoIndex,"Auto ID/duplicate guard");Check(!auto.interactable&&!cancel.interactable,"Submitted payment stays clickable");}
                ui.Present(payment,1,true,i=>{calls++;last=i;},null,"");yield return Frame();int before=calls;
                ui.Choose(ui.OptionIndex("cancel"),payment.request.requestId-1);Check(calls==before,"Stale payment submitted");
                Click(cancel);cancel.onClick.Invoke();Check(calls==before+1&&last==ui.OptionIndex("cancel"),"Cancel ID/duplicate guard");
                ui.Present(payment,1,false,i=>calls++,null,"");yield return Frame();auto.onClick.Invoke();cancel.onClick.Invoke();Check(calls==before+1&&!auto.interactable&&!cancel.interactable,"Nonacting viewer can pay");
                ui.Present(payment,1,true,i=>calls++,null,"");var pending=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(payment));pending.inputPending=true;
                ui.Present(pending,1,false,i=>calls++,null,"");Check(!auto.interactable&&!cancel.interactable,"Pending payment accepts clicks");
            }
            var recovered=Read("tax-auto-resolved");ui.Present(recovered,recovered.seat,true,_=>{},null,"");yield return Frame();
            Check(!Payment("AutoPayment").gameObject.activeInHierarchy&&!Root.Find("ManaPaymentPrompt").gameObject.activeSelf,"Payment UI stays after resolution");
            Check(Root.Find("CornerCluster/ControlRow/SkipCombatButton").gameObject.activeSelf,"Priority button not restored");
            Check(Root.Find("CornerCluster/ControlRow/PhaseTrack").gameObject.activeSelf,"Phase track not restored");
        }
    }
}
#endif
