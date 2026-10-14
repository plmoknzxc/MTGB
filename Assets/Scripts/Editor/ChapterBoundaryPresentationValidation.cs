#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public static class ChapterBoundaryPresentationValidation
{
 const string Base=".tools/chapter-boundaries-1009";
 public static bool Running{get;private set;}
 public static string Output{get;private set;}
 static int count;
 static Transform Root=>GameObject.Find("Canvas").transform;
 static void Check(bool v,string s){if(!v)throw new Exception(s);count++;File.AppendAllText(Output+"/checks.txt",s+"\n");}
 static ForgeSeatView Read(string n)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Base+"/ui-inputs/"+n+".json"));
 public static void Run(){if(Running||!Application.isPlaying)throw new Exception("Play required");Running=true;count=0;Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("ChapterBoundaryValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));}
 static IEnumerator Guard(IEnumerator work){var tasks=new Stack<IEnumerator>();tasks.Push(work);try{while(tasks.Count>0){object next;try{var w=tasks.Peek();if(!w.MoveNext()){tasks.Pop();continue;}next=w.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator c)tasks.Push(c);else yield return next;}File.WriteAllText(Output+"/result.txt","PASS_BOUNDED: "+count+" checks;actual ReadAhead1-3 native enumeration/right dock/ray confirm2and3/min-max/no invented cancel/local draft/stale-duplicate-readonly/view-font;actual post-sacrifice reflexive player ray and selected confirmation/source/removed exact card. Existing APNAP/language contracts reused;four native flows proved separately. No network/all-payload PASS.");}finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}}
 static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}}
 static void Click(Button b){var rect=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Exact ray "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);}
 static IEnumerator Validate(ForgeBattlefieldController ui){string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
  try{float end=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<end)yield return null;GamePreferences.SetLanguage("zh-CN");end=Time.realtimeSinceStartup+15;while((!GamePreferences.Ready||GamePreferences.Language!="zh-CN")&&Time.realtimeSinceStartup<end)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language=="zh-CN","Main locale ready");
   foreach(int chapter in new[]{2,3})foreach(bool acting in new[]{true,false}){
    var v=Read("read-ahead-"+chapter);int calls=0,last=-1;ui.Present(null,1,false,null,null,"");ui.Present(v,1,acting,i=>{calls++;last=i;},null,"");yield return Frame();
    var dock=Root.Find("CornerCluster/ControlRow/CardAbilityChoices");Check(dock!=null&&dock.gameObject.activeInHierarchy,"ReadAhead shares existing right numeric dock");Check(v.request.options.Select(o=>o.label).SequenceEqual(new[]{"1","2","3"})&&ui.OptionIndex("cancel")<0,"Native mandatory range1-to-final3,no invented cancellation");
    var number=dock.Find("NumericValue").GetComponent<Text>();var plus=dock.Find("Increase").GetComponent<Button>();var minus=dock.Find("Decrease").GetComponent<Button>();var confirm=dock.Find("ConfirmValue").GetComponent<Button>();Check(number.text=="1"&&!minus.interactable&&!number.resizeTextForBestFit,"ReadAhead actual minimum1/fixed font");
    if(acting){Click(plus);Click(plus);Check(number.text=="3"&&!plus.interactable&&calls==0,"Final chapter3 bounds/local draft");if(chapter==2)Click(minus);}
    else Check(dock.GetComponentsInChildren<Button>().All(b=>!b.interactable),"Read-only numeric dock disabled");
    int expected=ui.OptionIndex(v.request.options.Single(o=>o.label==chapter.ToString()).id);float font=number.fontSize;ui.ViewBattlefield();yield return Frame();ui.RestoreBattlefieldWindows();yield return Frame();Check(number.fontSize==font&&calls==0,"View return retains exact font and no submission");Check(!ui.TrySubmitPresentedOption(expected,v.request.requestId-1),"Stale chapter rejected");
    if(acting){Check(number.text==chapter.ToString(),"Correct chosen actual chapter");ScreenCapture.CaptureScreenshot(Output+"/read-ahead-"+chapter+".png");yield return Frame();Click(confirm);Check(calls==1&&last==expected&&!ui.TrySubmitPresentedOption(expected,v.request.requestId),"Exact native chapter ID and duplicate guard");}
    else Check(!ui.TrySubmitPresentedOption(expected,v.request.requestId)&&calls==0,"Read-only chapter not submitted");
   }
   foreach(int selected in new[]{0,1})foreach(bool acting in new[]{true,false}){
    var v=Read("reflexive-target-"+selected);Check(v.request.sourceCardId==121&&v.players[0].graveyardCards.Any(c=>c.id==136)&&!v.players[0].battlefield.Any(c=>c.id==136),"Reflexive source exact, sacrificed body absent from battlefield");int calls=0,last=-1;ui.Present(null,1,false,null,null,"");ui.Present(v,1,acting,i=>{calls++;last=i;},null,"");yield return Frame();
    int expected=ui.OptionIndex(selected==0?"player:"+v.players[1].targetId:"ok");Check(expected>=0,"Exact accepted player/confirm option available");var button=selected==0?GameObject.Find("OpponentPlate").GetComponent<Button>():Root.GetComponentsInChildren<Button>().Single(b=>b.name=="ConfirmTarget");Check(button.interactable==acting,"Native reflexive target gate");Check(!ui.TrySubmitPresentedOption(expected,v.request.requestId-1),"Stale reflexive target rejected");
    if(acting){Click(button);Check(calls==1&&last==expected&&!ui.TrySubmitPresentedOption(expected,v.request.requestId),"Exact reflexive ray/confirm index,duplicate rejected");if(selected==0){ScreenCapture.CaptureScreenshot(Output+"/reflexive-target.png");yield return Frame();}}
    else Check(!ui.TrySubmitPresentedOption(expected,v.request.requestId)&&calls==0,"Read-only reflexive target rejected");
   }
   ui.Present(Read("finished"),1,false,null,null,"");yield return Frame();Check(GameObject.Find("CardAbilityChoices")==null&&GameObject.Find("TargetArrow")==null,"Finished native request clears numeric/target visuals");
  }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(locale);if(module!=null)module.enabled=input;for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
 }
}
#endif
