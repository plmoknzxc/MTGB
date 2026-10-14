#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public static class TargetChoiceActionsValidation1014 {
 const string Out=".tools/target-cancel-1014";const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 sealed class Host:MonoBehaviour{}public static bool Running;static List<string> checks=new();static Mouse probeMouse;
 static object Get(object o,string n)=>o.GetType().GetField(n,Hidden).GetValue(o);
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Hidden).Invoke(o,a);
 static ForgeSeatView Read(string p)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p));
 static void Check(bool ok,string n){if(!ok)throw new Exception(n);checks.Add("PASS "+n);}
 public static void Run(){if(Running)return;checks.Clear();Running=true;new GameObject("TargetChoiceActionsProbe").AddComponent<Host>().StartCoroutine(Guard(Exercise()));}
 static IEnumerator Guard(IEnumerator e){while(true){bool more;object next;try{more=e.MoveNext();next=more?e.Current:null;}catch(Exception ex){checks.Add("FAIL "+ex);more=false;next=null;}if(!more)break;yield return next;}if(probeMouse!=null){InputSystem.RemoveDevice(probeMouse);probeMouse=null;}File.WriteAllLines(Out+"/ui-result.txt",checks);Running=false;}
 static void Capture(string n){var tex=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Out+"/"+n+".png",tex.EncodeToPNG());UnityEngine.Object.Destroy(tex);}
 static void RightClick(ForgeBattlefieldController ui){if(probeMouse==null)probeMouse=InputSystem.AddDevice<Mouse>();InputSystem.QueueStateEvent(probeMouse,new MouseState{position=new Vector2(15,15)}.WithButton(MouseButton.Right));InputSystem.Update();Call(ui,"UpdateChoiceCancelShortcut");InputSystem.QueueStateEvent(probeMouse,new MouseState{position=new Vector2(15,15)});InputSystem.Update();}
 static IEnumerator Exercise(){
  while(!GamePreferences.Ready)yield return null;foreach(var n in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))n.enabled=false;
  var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("TargetChoiceActionsBoard").AddComponent<ForgeBattlefieldController>();ui.enabled=false;ui.GetType().GetField("service",Hidden).SetValue(ui,null);
  var initial=Read(".tools/bolt-amber-flow-1014/bolt-02-select-target.json");int sent=-1;ui.Present(initial,1,true,i=>sent=i,null,"");yield return new WaitForSecondsRealtime(.65f);
  var dock=(RectTransform)Get(ui,"targetChoiceActions");var next=dock.Find("NextTargetChoice").GetComponent<Button>();var cancel=dock.Find("CancelTargetChoice").GetComponent<Button>();Check(dock.gameObject.activeInHierarchy&&!next.interactable&&cancel.interactable,"Unselected target has disabled Next and enabled Cancel");Check(next.GetComponentInChildren<Text>().text=="下一步"&&cancel.GetComponentInChildren<Text>().text=="取消","Common target dock labels Next and Cancel");Check(((RectTransform)next.transform).anchoredPosition.x<((RectTransform)cancel.transform).anchoredPosition.x,"Next left and Cancel right");
  var preview=(RectTransform)Get(ui,"targetOrigin");Check(new[]{"CancelTarget","ConfirmTarget","ResetTarget"}.All(n=>!preview.Find(n).gameObject.activeSelf),"No source-preview action buttons remain");
  RightClick(ui);Check(sent>=0&&initial.request.options[sent].id=="cancel","Right click outside card cancels target selection");Check(!ui.TryCancelPresentedChoice(),"Pending cancel cannot submit twice");
  var selected=Read(".tools/bolt-amber-flow-1014/bolt-03-target-selected.json");int calls=0;sent=-1;ui.Present(selected,1,true,i=>{sent=i;calls++;},null,"");yield return new WaitForSecondsRealtime(.65f);Check(ReferenceEquals(dock,Get(ui,"targetChoiceActions"))&&next.interactable,"Selected target reuses dock and enables Next");yield return new WaitForEndOfFrame();Capture("bolt-target-next");next.onClick.Invoke();Check(calls==1&&selected.request.options[sent].id=="ok","Next submits exact native target confirmation once");Check(!next.interactable&&!cancel.interactable,"In-flight target controls disabled");
  selected=Read(".tools/lan-replay-flow-1014/force-802.json");sent=-1;ui.Present(selected,1,true,i=>sent=i,null,"");yield return new WaitForSecondsRealtime(.65f);Check(next.interactable&&dock.gameObject.activeInHierarchy,"Stack target uses same corner controls");yield return new WaitForEndOfFrame();Capture("force-target-next");cancel.onClick.Invoke();Check(selected.request.options[sent].id=="cancel","Stack target Cancel submits original cancellation");
  var payment=Read(".tools/bolt-amber-flow-1014/bolt-04-mana-payment.json");sent=-1;ui.Present(payment,1,true,i=>sent=i,null,"");yield return null;Check(!dock.gameObject.activeSelf,"Target dock hidden during mana payment");RightClick(ui);Check(payment.request.options[sent].id=="cancel","Right click at mana payment after Next cancels original cost");
  var cost=Read(".tools/lan-replay-flow-1014/force-803.json");sent=-1;ui.Present(cost,1,true,i=>sent=i,null,"");int index=Array.FindIndex(cost.request.options,o=>o.id=="card:36");Call(ui,"TryStageHandChoice",index,cost.request.requestId);RightClick(ui);Check(sent>=0&&cost.request.options[sent].id=="cancel","Right click cancels locally staged blue-card cost");
  cost=Read(".tools/delayed-marker-review-1014/force-request-800.json");sent=-1;ui.Present(cost,1,true,i=>sent=i,null,"");RightClick(ui);Check(cost.request.options[sent].id=="cancel","Right click cancels casting method choice");
  var color=Read(".tools/bolt-amber-flow-1014/amber-05-select-color.json");sent=-1;ui.Present(color,1,true,i=>sent=i,null,"");RightClick(ui);Check(sent<0&&!ui.TryCancelPresentedChoice(),"Ordinal cancel meaning Blue is never mistaken for cancellation");
  // Layout adapter for a target in a non-battlefield zone. Rule permissions
  // remain the native target request; only the display location is adapted.
  selected=Read(".tools/bolt-amber-flow-1014/bolt-03-target-selected.json");var target=selected.request.options.First(o=>o.card!=null);target.card.zone="Graveyard";selected.request.previewCards=new[]{target.card};sent=-1;ui.Present(selected,1,true,i=>sent=i,null,"");yield return null;var panel=(RectTransform)Get(ui,"selectionPanel");Check(panel.gameObject.activeInHierarchy&&dock.gameObject.activeInHierarchy,"Off-board target picker retains corner Next and Cancel");Check(panel.Find("SelectionFooter").GetComponentsInChildren<Button>().Length==0,"Target picker has no duplicate modal footer actions");Check(dock.GetComponent<Canvas>().overrideSorting&&dock.GetComponent<GraphicRaycaster>()!=null,"Target dock accepts clicks above target picker backdrop");ui.Present(selected,1,true,i=>sent=i,null,"");Check(next.interactable,"Cached off-board target update rebinds corner actions");ui.ViewBattlefield();Check(ui.TryCancelPresentedChoice()&&selected.request.options[sent].id=="cancel","Right cancel also restores temporary battlefield view");
  selected=Read(".tools/bolt-amber-flow-1014/bolt-03-target-selected.json");selected.request.options=selected.request.options.Where(o=>o.id!="cancel").ToArray();selected.request.abilityKind="ability";ui.Present(selected,1,true,i=>{},null,"");Check(!cancel.gameObject.activeSelf&&next.interactable&&Mathf.Approximately(((RectTransform)next.transform).rect.width,184),"Noncancellable ability target gets one full-width Next");Check(!ui.TryCancelPresentedChoice(),"No cancellation synthesized without native option");
  ui.Present(selected,1,false,i=>{},null,"");Check(!next.interactable&&!ui.TryCancelPresentedChoice(),"Read-only target cannot submit Next or cancel");
  var committed=Read(Out+"/bolt-committed.json");sent=-1;ui.Present(committed,1,true,i=>sent=i,null,"");Check(!dock.gameObject.activeSelf&&!ui.TryCancelPresentedChoice()&&sent<0,"Committed spell cannot be cancelled by the shortcut");
 }
}
#endif
