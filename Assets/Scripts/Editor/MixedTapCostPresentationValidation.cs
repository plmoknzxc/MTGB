#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class MixedTapCostPresentationValidation
{
    const string Output=".tools/mixed-tap-costs-1007/ui";
    [Serializable] class Meta { public string folder,status,runtimeHash; }
    static bool running;
    static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    static ForgeSeatView Read(string test,string name) {
        var m=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/mixed-tap-costs-1007/native/"+test+"-result.json"));
        var rt=JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json"));
        Check(m.status=="PASS"&&m.runtimeHash==rt.runtimeHash,"Native identity "+test);
        return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Path.Combine(m.folder,name+".json")));
    }
    static IEnumerator Frame() { yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases(); }
    static void Click(Button b) {
        var r=(RectTransform)b.transform;var p=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Check(b.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked ray "+b.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    public static void Run() {
        Check(Application.isPlaying&&!running,"Play/validation state");running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/steps.log","");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("MixedTapValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator work) {
        try {
            while(true) {object next;try {if(!work.MoveNext())break;next=work.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS zh-CN actual mixed-tap native replay: nested convoke color icons/indices/rays/guards and return to preserved selection, multicolor artifact and overlapping artifact-creature confirmation, unpayable cancellation/priority cleanup. Shared prior three-language picker proof reused; no new UI first, effect or live-network claim.");
        } finally {running=false;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui) {
        var language=GamePreferences.Language;var active=ui.enabled;ui.enabled=false;
        var module=EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();bool inputs=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var banners=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var states=banners.Select(b=>b.enabled).ToArray();foreach(var b in banners)b.enabled=false;
        try {
            GamePreferences.SetLanguage("zh-CN");float deadline=Time.realtimeSinceStartup+8;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready,"Language ready");
            foreach(var color in new[]{"Green","White"}) {
                var v=Read("convoke-color-switch","color-"+color);int calls=0,last=-1;
                ui.Present(Read("convoke-color-switch",color=="Green"?"step-18":"step-24"),1,true,_=>{},null,"");yield return Frame();
                ui.Present(v,1,true,i=>{calls++;last=i;},null,"");yield return Frame();
                int index=Array.FindIndex(v.request.options,o=>o.label.ToUpperInvariant()==color.ToUpperInvariant());Check(index>=0,"Color identity");
                var button=GameObject.Find("Option_"+index).GetComponent<Button>();var symbol=button.GetComponentsInChildren<TMPro.TMP_Text>().First(t=>t.name=="Mana");
                symbol.ForceMeshUpdate();Check(symbol.text==CardSymbolText.Format(color=="Green"?"{G}":"{W}")&&symbol.textInfo.characterCount>0,"Color glyph");
                Check(!ui.TrySubmitPresentedOption(index,v.request.requestId-1),"Stale color accepted");
                ScreenCapture.CaptureScreenshot(Output+"/color-"+color+".png");yield return Frame();Click(button);button.onClick.Invoke();Check(calls==1&&last==index,"Color index/duplicate");
                var selected=Read("convoke-color-switch",color.ToLowerInvariant()+"-chosen");ui.Present(selected,1,true,_=>{},null,"");yield return Frame();
                Check(GameObject.Find("CardSelection")!=null&&GameObject.Find("CardAbilityChoices")==null,"Nested menu cleanup");
                Check(GameObject.Find("CardSelection/Count").GetComponent<Text>().text.Contains("1"),"Selected count preserved");
                File.AppendAllText(Output+"/steps.log",color+" color icon/ray/index/guards/selection return PASS\n");
            }
            foreach(var test in new[]{"improvise-multicolor","waterbend-overlap"}) {
                var v=Read(test,"tap-selected");int calls=0,last=-1;ui.Present(null,1,false,null,null,"");ui.Present(v,1,true,i=>{calls++;last=i;},null,"");yield return Frame();
                int count=test=="improvise-multicolor"?5:2;Check(v.request.selectedCount==count,"Native tally");
                Check(v.request.options.Where(o=>o.card!=null&&o.selected).Select(o=>o.card.id).Distinct().Count()==count,"Unique native resources");
                int index=Array.FindIndex(v.request.options,o=>o.id=="ok");Click(GameObject.Find("SelectionAction_"+index).GetComponent<Button>());Check(calls==1&&last==index,"Tap confirmation "+test);
                File.AppendAllText(Output+"/steps.log",test+" selected native identities/confirmation ray PASS\n");
            }
            foreach(var test in new[]{"convoke-wrong-color","improvise-colored-short"}) {
                var v=Read(test,"unpayable-payment");int calls=0,last=-1;ui.Present(null,1,false,null,null,"");ui.Present(v,1,true,i=>{calls++;last=i;},null,"");yield return Frame();
                int index=Array.FindIndex(v.request.options,o=>o.id=="cancel");var b=GameObject.Find("CancelPayment").GetComponent<Button>();Click(b);Check(calls==1&&last==index,"Unpayable cancel ray");
                ui.Present(Read(test,"unpayable-cancelled"),1,true,_=>{},null,"");yield return Frame();Check(GameObject.Find("CardSelection")==null&&GameObject.Find("ManaPaymentActions")==null,"Cancelled input cleanup");
                File.AppendAllText(Output+"/steps.log",test+" cancellation ray/priority cleanup PASS\n");
            }
        } finally {ui.Present(null,1,false,null,null,"");ui.enabled=active;if(module!=null)module.enabled=inputs;GamePreferences.SetLanguage(language);for(int i=0;i<banners.Length;i++)if(banners[i]!=null)banners[i].enabled=states[i];}
    }
}
#endif
