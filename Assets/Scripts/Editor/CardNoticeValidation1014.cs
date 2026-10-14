#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public static class CardNoticeValidation1014 {
 const string Out=".tools/card-notices-1014"; const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 sealed class Host:MonoBehaviour{} public static bool Running;static List<string> checks=new();
 static object Get(object o,string n)=>o.GetType().GetField(n,Hidden).GetValue(o);
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Hidden|BindingFlags.Public).Invoke(o,a);
 static ForgeSeatView Read(string n)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Out+"/ui-"+n+".json"));
 static void Check(bool ok,string n){if(!ok)throw new Exception(n);checks.Add("PASS "+n);}
 static void Capture(string n){var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Out+"/"+n+".png",t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
 public static void Run(){if(Running||!Application.isPlaying)throw new Exception("Play required and no active probe");checks.Clear();Running=true;new GameObject("CardNoticeProbe").AddComponent<Host>().StartCoroutine(Guard(Exercise()));}
 static IEnumerator Guard(IEnumerator e){while(true){bool more;object next;try{more=e.MoveNext();next=more?e.Current:null;}catch(Exception ex){checks.Add("FAIL "+ex);more=false;next=null;}if(!more)break;yield return next;}GamePreferences.SetScale(1);File.WriteAllLines(Out+"/ui-result.txt",checks);Running=false;}
 static IEnumerator Exercise(){
  while(!GamePreferences.Ready)yield return null;
  foreach(var n in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))n.enabled=false;
  var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("CardNoticeBoard").AddComponent<ForgeBattlefieldController>();ui.enabled=false;ui.GetType().GetField("service",Hidden).SetValue(ui,null);int calls=0;
  foreach(float scale in new[]{1f,1.3f}) {
   Call(ui,"ResetNotices");GamePreferences.SetScale(scale);var forest=Read("forestcycling");ui.Present(forest,2,false,i=>calls++,null,"");yield return new WaitForSecondsRealtime(.8f);
   var windows=(Dictionary<long,RectTransform>)Get(ui,"cardNoticeWindows");Check(windows.Count>0,"Native forestcycling result creates persistent notice "+scale);
   long noticeId=forest.notices.First(n=>n.kind=="card-move"&&n.toZone=="Hand").id;
   var original=windows[noticeId];Check(original.GetComponentsInChildren<Button>().All(b=>b.name=="Close"),"No acknowledgement/payment button in informational window "+scale);
   var description=original.Find("Description").GetComponent<Text>();Check(description.text.Contains("牌库")&&description.text.Contains("手牌")&&description.text.Contains(GamePreferences.CardFaceName("Generous Ent")),"Cause/from/to are localized "+scale+": "+description.text);
   Check(description.preferredHeight<=description.rectTransform.rect.height+1,"Description fits user scale "+scale);
   Check(original.GetComponentsInChildren<Image>().Any(i=>i.sprite!=null&&i.name.StartsWith("Card_")),"Exact native moved card has artwork "+scale);
   Vector2 old=original.anchoredPosition;var header=original.Find("Header").GetComponent<ForgeCardNoticeDrag>();var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,header.transform.position)};
   header.OnPointerDown(e);e.position+=new Vector2(120,60);header.OnDrag(e);Check(original.anchoredPosition!=old,"Window header can move "+scale);Vector2 moved=original.anchoredPosition;
   ui.Present(forest,2,false,i=>calls++,null,"");yield return null;Check(original.anchoredPosition==moved,"Snapshot refresh retains dragged position "+scale);
   var mill=Read("mill");foreach(var note in mill.notices)note.id+=10000;ui.Present(mill,2,false,i=>calls++,null,"");yield return null;
   Check(windows.Count>=2,"Several independent notices coexist (notice-ID layout adapter) "+scale);
   var others=windows.Values.Where(w=>w!=original).ToArray();ui.ViewBattlefield();Check(windows.Values.All(w=>w.GetComponent<CanvasGroup>().alpha==0),"View battlefield hides every notice "+scale);ui.RestoreBattlefieldWindows();Check(windows.Values.All(w=>w.GetComponent<CanvasGroup>().alpha==1),"Restore brings back windows "+scale);
   Check(windows.Values.Select(w=>w.GetComponent<Outline>().effectColor).Distinct().Count()>1,"Overlapping windows have distinct visible edge accents "+scale);
   Check(windows.Values.All(w=>w.GetComponents<Shadow>().Any(s=>s.GetType()==typeof(Shadow)&&s.effectDistance==new Vector2(8,-8))),"Separate drop shadow is retained beside colored border "+scale);
   yield return new WaitForEndOfFrame();if(scale==1)Capture("movable-notices");
   original.SetAsLastSibling();var close=original.Find("Header/Close").GetComponent<Button>();
   var click=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,close.transform.position),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(click,hits);
   Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==close,"X receives the actual top UI ray "+scale);ExecuteEvents.Execute(close.gameObject,click,ExecuteEvents.pointerClickHandler);
   yield return null;Check(!windows.ContainsKey(noticeId)&&others.All(w=>w!=null),"X closes only its own notice "+scale);
   ui.Present(forest,2,false,i=>calls++,null,"");yield return null;Check(!windows.ContainsKey(noticeId),"Closed notification does not reopen on replay "+scale);
   Check(calls==0,"Drag/hide/close send no game reply "+scale);
   var cost=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(".tools/cost-unification-1014/exile-2.json"));cost.notices=Array.Empty<ForgeNotice>();ui.Present(cost,cost.request.seat,true,i=>calls++,null,"");yield return null;
   var hint=(RectTransform)Get(ui,"choiceHintPanel");ui.ViewBattlefield();Check(hint.GetComponent<CanvasGroup>().alpha==0,"Shared payment hint hides with View battlefield "+scale);ui.RestoreBattlefieldWindows();Check(hint.GetComponent<CanvasGroup>().alpha==1,"Payment hint restoration preserves text "+scale);
   Check(calls==0,"Hiding payment hint does not submit/cancel cost "+scale);
  }
  Call(ui,"ResetNotices");Check(((Dictionary<long,RectTransform>)Get(ui,"cardNoticeWindows")).Count==0,"Restart/match reset clears notice windows and close history");
 }
}
#endif
