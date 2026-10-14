#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class MultipartCostPresentationValidation
{
    public const string Output=".tools/multipart-cost-1007/ui";
    [Serializable] class Meta { public string folder,status,runtimeHash; }
    static bool running;
    static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    static string Folder(string test) {
        var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/multipart-cost-1007/native/"+test+"-result.json"));
        var current=JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json"));
        Check(meta.status=="PASS"&&meta.runtimeHash==current.runtimeHash,"Stale native input "+test);
        return meta.folder;
    }
    static ForgeSeatView Read(string test,string name) => JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Path.Combine(Folder(test),name+".json")));
    static IEnumerator Frame() { yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases(); }
    static void Click(Button button) {
        var rt=(RectTransform)button.transform;
        var p=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Ray blocked "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    public static void Run() {
        if(running)return;Check(Application.isPlaying,"Enter battlefield Play Mode");running=true;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("MultipartCostValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator work) {
        try {
            while(true) {
                object next;try { if(!work.MoveNext())break;next=work.Current; }
                catch(Exception e) { File.WriteAllText(Output+"/result.txt","FAIL "+e);throw; }
                yield return next;
            }
            File.WriteAllText(Output+"/result.txt","PASS: current native multipart-cost inputs, zh-CN. Actual sacrifice-picker and payment-dock cancellation rays, exact original indices, stale/duplicate/nonacting guards and restored priority cleanup. Native mandatory-after-life confirm remains available without cancel. Non-card cost-order options and confirmation ray checked separately. Native fixture enables cost-order full-control solely for that engine branch; this is not a frontend full-control toggle or live network test.");
        } finally { running=false; }
    }
    static IEnumerator Validate(ForgeBattlefieldController ui) {
        var locale=GamePreferences.Language;var enabled=ui.enabled;ui.enabled=false;
        var module=EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();var input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var banners=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=banners.Select(b=>b.enabled).ToArray();foreach(var b in banners)b.enabled=false;
        try {
            GamePreferences.SetLanguage("zh-CN");float end=Time.realtimeSinceStartup+8;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<end)yield return null;Check(GamePreferences.Ready,"Language not ready");
            foreach(var test in new[]{"discard-before-sacrifice","spell-discard-before-sacrifice","replacement-discard-before-sacrifice","exile-before-mana","sacrifice-before-mana","return-before-mana"}) {
                var v=Read(test,"committed-before-cancel");int calls=0,last=-1;
                ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,true,i=>{calls++;last=i;},null,"");yield return Frame();
                int cancel=Array.FindIndex(v.request.options,o=>o.id=="cancel");Check(cancel>=0,"Native cancel absent");
                var root=GameObject.Find("Canvas").transform;
                var button=v.request.kind.Contains("PayMana")?root.Find("CornerCluster/ControlRow/ManaPaymentActions/CancelPayment").GetComponent<Button>():GameObject.Find("SelectionAction_"+cancel).GetComponent<Button>();
                Check(!ui.TrySubmitPresentedOption(cancel,v.request.requestId-1),"Stale cancel submitted");
                if(test=="discard-before-sacrifice"||test=="exile-before-mana") { ScreenCapture.CaptureScreenshot(Output+"/"+test+".png");yield return Frame(); }
                Click(button);button.onClick.Invoke();Check(calls==1&&last==cancel,"Cancel index/duplicate "+test);
                ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,false,i=>calls++,null,"");yield return Frame();
                Check(!ui.TrySubmitPresentedOption(cancel,v.request.requestId)&&calls==1,"Viewer cancel submitted");
                ui.Present(Read(test,"cancelled"),v.seat,true,i=>{},null,"");yield return Frame();
                var picker=GameObject.Find("CardSelection");Check(picker==null||!picker.activeInHierarchy,"Picker remains after cancellation");
                var payment=root.Find("CornerCluster/ControlRow/ManaPaymentActions");Check(payment==null||!payment.gameObject.activeInHierarchy,"Payment dock remains after cancellation");
                File.AppendAllText(Output+"/steps.log",test+" cancel/index/guards/priority cleanup PASS\n");
            }
            var forced=Read("life-discard-forced-completion","mandatory-after-life");int forcedCalls=0,forcedLast=-1;
            ui.Present(null,1,false,null,null,"");ui.Present(forced,1,true,i=>{forcedCalls++;forcedLast=i;},null,"");yield return Frame();
            Check(!forced.request.options.Any(o=>o.id=="cancel"),"Native mandatory request changed");int ok=Array.FindIndex(forced.request.options,o=>o.id=="ok");
            Click(GameObject.Find("SelectionAction_"+ok).GetComponent<Button>());Check(forcedCalls==1&&forcedLast==ok,"Mandatory cost confirm");
            File.AppendAllText(Output+"/steps.log","mandatory life -> discard -> sacrifice confirmation PASS\n");
            var folder=Folder("exile-before-mana");
            foreach(string step in new[]{"step-14","step-18"}) {
                var order=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Path.Combine(folder,step+".json")));int calls=0,last=-1;
                ui.Present(null,1,false,null,null,"");ui.Present(order,1,true,i=>{calls++;last=i;},null,"");yield return Frame();
                string id=step=="step-14"?"select:1":"confirm-cards";int index=Array.FindIndex(order.request.options,o=>o.id==id);Check(index>=0,"Ordering option absent");
                var b=GameObject.Find("Option_"+index).GetComponent<Button>();
                if(step=="step-14") { ScreenCapture.CaptureScreenshot(Output+"/cost-order.png");yield return Frame(); }
                Click(b);Check(calls==1&&last==index,"Cost order string index "+step);
                File.AppendAllText(Output+"/steps.log",step+" string cost ordering PASS\n");
            }
        } finally {
            ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=input;
            GamePreferences.SetLanguage(locale);for(int i=0;i<banners.Length;i++)if(banners[i]!=null)banners[i].enabled=flags[i];
        }
    }
}
#endif
