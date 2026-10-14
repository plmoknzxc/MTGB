#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Replays native outcomes only: same-ID reincarnation, conditional expiry, optional number.
public static class DelayedLifecyclePresentationValidation {
    const string Base=".tools/delayed-lifecycle-1009";
    static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    public static bool Running{get;private set;}
    public static string Output{get;private set;}
    static int checks;
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;File.AppendAllText(Output+"/checks.txt",label+"\n");}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Output+"/inputs/"+name+".json"));
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Clean(ForgeBattlefieldController ui){foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}var f=typeof(ForgeBattlefieldController).GetField("inspectHoverRevision",Hidden);f.SetValue(ui,(int)f.GetValue(ui)+1);Root.Find("InspectPanel").gameObject.SetActive(false);}
    static void Ray(Button b){var rect=(RectTransform)b.transform;var p=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(b.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Exact topmost ray "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    public static void Run(){if(!Application.isPlaying||Running)throw new Exception("Play required/already running");Running=true;checks=0;Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");foreach(var p in Directory.GetFiles(Base+"/ui-inputs"))File.Copy(p,Output+"/inputs/"+Path.GetFileName(p));foreach(var p in new[]{"Assets/Scripts/Editor/DelayedLifecyclePresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Arena.cs","Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs","Assets/Scripts/UI/ForgeBattlefieldController.Emblems.cs","Assets/Scripts/UI/ForgeBattlefieldController.TokenArt.cs","Assets/Scripts/UI/ForgeBattlefieldController.Notices.cs","Assets/Scripts/UI/ForgeBattlefieldController.LandChoices.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("DelayedLifecycleValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));}
    static IEnumerator Guard(IEnumerator work){var tasks=new Stack<IEnumerator>();tasks.Push(work);try{while(tasks.Count>0){object next;try{var t=tasks.Peek();if(!t.MoveNext()){tasks.Pop();continue;}next=t.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator child)tasks.Push(child);else yield return next;}File.WriteAllText(Output+"/result.txt","PASS: "+checks+" checks; actual two-seat same-ID return clears old blue departure and new arrangement restores it; conditional token expiry consumes native obligation and removes silver mesh/text from surviving same ID; actual optional remembered-number yes/no rays, stale/duplicate/readonly guards and next-request cleanup. Shared language/target/payment/attack/transport proof reused at original scope; no global language/network PASS.");}finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui){
        string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{GamePreferences.SetLanguage("zh-CN");float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Preferences ready");
            foreach(int seat in new[]{1,2}){
                foreach(string scenario in new[]{"incarnation","final-skip"}){
                    ui.Present(null,seat,false,null,null,"");int exact=0;string previous="";
                    foreach(string label in scenario=="incarnation"?new[]{"first-return","blink-return","second-return","new-expiry-sacrificed"}:new[]{"token-created","end-result","finished"}){
                        var v=Read(scenario+"-"+label+"-seat"+seat);var card=v.players[0].battlefield.SingleOrDefault(c=>scenario=="incarnation"?c.name=="Grizzly Bears":c.token);if(exact==0)exact=card.id;else Check(card==null||card.id==exact,"Same exact native ID "+scenario+" "+label);
                        ui.Present(v,seat,false,null,null,"");yield return Frame();Clean(ui);var obj=GameObject.Find("Card_"+exact);
                        if(card==null){Check(obj==null,"Departed new incarnation UI removed");continue;}
                        Check(obj!=null,"Exact public card visible "+scenario+" "+label);bool timed=card.pendingDepartures?.Length>0;
                        if(scenario=="final-skip"){var glow=obj.GetComponentInChildren<TemporaryCopyGlow>();Check((glow!=null)==timed,"Conditional obligation/frame agree "+label);if(glow!=null)Check(!glow.raycastTarget&&glow.canvasRenderer.GetMesh().vertexCount==480,"Silver mesh exists without blocking");}
                        if(timed)previous=ForgeBattlefieldController.PendingDepartureText(card.pendingDepartures[0]);
                        typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{card});yield return Frame();var rules=Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>();Check(timed?rules.text.Contains("<color=#70BFFF>"+previous+"</color>"):!rules.text.Contains(previous),"Native obligation blue text/current clear "+scenario+" "+label);Clean(ui);
                        if(scenario=="final-skip"&&seat==1){ScreenCapture.CaptureScreenshot(Output+"/"+label+".png");yield return Frame();}
                    }
                }
            }
            foreach(string sample in new[]{"number-accept","number-decline"}){
                var v=Read(sample+"-optional-seat1");int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};ui.Present(null,1,false,null,null,"");ui.Present(v,1,true,submit,null,"");yield return Frame();Clean(ui);var panel=GameObject.Find("CardAbilityChoices");Check(panel!=null,"Actual optional trigger confirmation present");var title=panel.transform.Find("ValueTitle").GetComponent<Text>();Check(!title.resizeTextForBestFit&&title.preferredHeight<=title.rectTransform.rect.height+1&&title.text.Contains("触发式异能"),"Localized fixed optional question fits");int expected=ui.OptionIndex(sample=="number-accept"?"ok":"cancel");Check(expected>=0&&!ui.TrySubmitPresentedOption(expected,v.request.requestId-1)&&calls==0,"Stale optional request rejected");Ray(panel.transform.Find("ValueOption_"+expected).GetComponent<Button>());Check(calls==1&&last==expected&&!ui.TrySubmitPresentedOption(expected,v.request.requestId),"Exact optional native action once");ui.Present(v,1,false,submit,null,"");yield return Frame();Check(!ui.TrySubmitPresentedOption(expected,v.request.requestId),"Readonly optional action rejected");ui.Present(Read(sample+"-mana-result-seat1"),1,true,submit,null,"");yield return Frame();Check(!panel.activeInHierarchy&&calls==1,"Next native priority clears optional window");
            }
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=input;GamePreferences.SetLanguage(locale);for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
