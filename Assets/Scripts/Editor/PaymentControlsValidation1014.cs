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
public static class PaymentControlsValidation1014 {
 const string Out=".tools/payment-controls-1014";const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 sealed class Host:MonoBehaviour{}public static bool Running;static List<string> checks=new();static Mouse probeMouse;
 static object Get(object o,string n)=>o.GetType().GetField(n,Hidden).GetValue(o);
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Hidden|BindingFlags.Public).Invoke(o,a);
 static ForgeSeatView Read(string p)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p));
 static void Check(bool ok,string n){if(!ok)throw new Exception(n);checks.Add("PASS "+n);}
 public static void Run(){if(Running)return;checks.Clear();Running=true;new GameObject("PaymentControlsProbe").AddComponent<Host>().StartCoroutine(Guard(Exercise()));}
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
  var selected=Read(".tools/bolt-amber-flow-1014/bolt-03-target-selected.json");int calls=0;sent=-1;ui.Present(selected,1,true,i=>{sent=i;calls++;},null,"");yield return new WaitForSecondsRealtime(.65f);Check(ReferenceEquals(dock,Get(ui,"targetChoiceActions"))&&next.interactable,"Selected target reuses dock and enables Next");Call(ui,"UpdateTargetingAt",new Vector2(15,15));yield return new WaitForEndOfFrame();Capture("bolt-target-next");next.onClick.Invoke();Check(calls==1&&selected.request.options[sent].id=="ok","Next submits exact native target confirmation once");Check(!next.interactable&&!cancel.interactable,"In-flight target controls disabled");
  selected=Read(".tools/lan-replay-flow-1014/force-802.json");sent=-1;ui.Present(selected,1,true,i=>sent=i,null,"");yield return new WaitForSecondsRealtime(.65f);Check(next.interactable&&dock.gameObject.activeInHierarchy,"Stack target uses same corner controls");Call(ui,"UpdateTargetingAt",new Vector2(15,15));yield return new WaitForEndOfFrame();Capture("force-target-next");cancel.onClick.Invoke();Check(selected.request.options[sent].id=="cancel","Stack target Cancel submits original cancellation");
  var payment=Read(".tools/bolt-amber-flow-1014/bolt-04-mana-payment.json");sent=-1;ui.Present(payment,1,true,i=>sent=i,null,"");yield return null;Check(!dock.gameObject.activeSelf,"Target dock hidden during mana payment");RightClick(ui);Check(payment.request.options[sent].id=="cancel","Right click at mana payment after Next cancels original cost");
  var cost=Read(".tools/lan-replay-flow-1014/force-803.json");sent=-1;ui.Present(cost,1,true,i=>sent=i,null,"");int index=Array.FindIndex(cost.request.options,o=>o.id=="card:36");Call(ui,"TryStageHandChoice",index,cost.request.requestId);RightClick(ui);Check(sent>=0&&cost.request.options[sent].id=="cancel","Right click cancels locally staged blue-card cost");
  cost=Read(".tools/delayed-marker-review-1014/force-request-800.json");sent=-1;ui.Present(cost,1,true,i=>sent=i,null,"");RightClick(ui);Check(cost.request.options[sent].id=="cancel","Right click cancels casting method choice");
  var color=Read(".tools/bolt-amber-flow-1014/amber-05-select-color.json");sent=-1;ui.Present(color,1,true,i=>sent=i,null,"");RightClick(ui);Check(sent<0&&!ui.TryCancelPresentedChoice(),"Ordinal cancel meaning Blue is never mistaken for cancellation");
  // Layout adapter for a target in a non-battlefield zone. Rule permissions
  // remain the native target request; only the display location is adapted.
  selected=Read(".tools/bolt-amber-flow-1014/bolt-03-target-selected.json");var target=selected.request.options.First(o=>o.card!=null);target.card.zone="Graveyard";selected.request.previewCards=new[]{target.card};sent=-1;ui.Present(selected,1,true,i=>sent=i,null,"");yield return null;var panel=(RectTransform)Get(ui,"selectionPanel");Check(panel.gameObject.activeInHierarchy&&dock.gameObject.activeInHierarchy,"Off-board target picker retains corner Next and Cancel");Check(panel.Find("SelectionFooter").GetComponentsInChildren<Button>().Length==0,"Target picker has no duplicate modal footer actions");Check(dock.GetComponent<Canvas>().overrideSorting&&dock.GetComponent<GraphicRaycaster>()!=null,"Target dock accepts clicks above target picker backdrop");ui.Present(selected,1,true,i=>sent=i,null,"");Check(next.interactable,"Cached off-board target update rebinds corner actions");ui.ViewBattlefield();Check(ui.TryCancelPresentedChoice()&&selected.request.options[sent].id=="cancel","Right cancel also restores temporary battlefield view");
  selected=Read(".tools/bolt-amber-flow-1014/bolt-03-target-selected.json");selected.request.options=selected.request.options.Where(o=>o.id!="cancel").ToArray();selected.request.abilityKind="ability";ui.Present(selected,1,true,i=>{},null,"");Check(!cancel.gameObject.activeSelf&&next.interactable&&Mathf.Approximately(((RectTransform)next.transform).rect.width,184),"Noncancellable ability target gets one full-width Next");Check(!ui.TryCancelPresentedChoice(),"No cancellation synthesized without native option");
  ui.Present(selected,1,false,i=>{},null,"");Check(!next.interactable&&!ui.TryCancelPresentedChoice(),"Read-only target cannot submit Next or cancel");
  foreach(string name in new[]{"bolt","draw","rites","convoke","convoke-unavailable","waterbend","waterbend-unavailable"}) {
   var p=Read(Out+"/"+name+"-payment.json");sent=-1;ui.Present(p,1,true,i=>sent=i,null,"");yield return null;
   var payDock=(RectTransform)Get(ui,"manaPaymentActions");var pay=payDock.Find("AutoPayment").GetComponent<Button>();var decline=payDock.Find("CancelPayment").GetComponent<Button>();
   Check(payDock.gameObject.activeInHierarchy&&pay.GetComponentInChildren<Text>().text==(p.request.paymentMode=="manual"?"下一步":"自动支付"),name+" uses native payment mode");
   Check(((RectTransform)pay.transform).anchoredPosition.x<((RectTransform)decline.transform).anchoredPosition.x,name+" payment proceeds on left and cancels on right");
   if(pay.interactable){pay.onClick.Invoke();Check(sent>=0&&p.request.options[sent].id=="ok",name+" submits exact payment option");}
   ui.Present(p,1,true,i=>sent=i,null,"");sent=-1;RightClick(ui);Check(sent>=0&&p.request.options[sent].id=="cancel",name+" right click cancels native payment");
  }
  foreach(string name in new[]{"convoke","waterbend"}) {
   var p=Read(Out+"/"+name+"-tap-0.json");sent=-1;ui.Present(p,1,true,i=>sent=i,null,"");yield return null;
   var footer=(RectTransform)Get(ui,"selectionFooter");var actions=footer.GetComponentsInChildren<Button>();var ok=actions.First(b=>b.name=="SelectionAction_"+Array.FindIndex(p.request.options,o=>o.id=="ok"));var decline=actions.FirstOrDefault(b=>b.name=="SelectionAction_"+Array.FindIndex(p.request.options,o=>o.id=="cancel"));
   Check(ok.GetComponentInChildren<Text>().text=="下一步"&&(decline==null||((RectTransform)ok.transform).anchoredPosition.x<((RectTransform)decline.transform).anchoredPosition.x),name+" tap-selection Next follows native cancellation availability");
   Check(!actions.Any(b=>b.GetComponentInChildren<Text>().text=="自动支付"),name+" never auto-selects tap cost permanents");
   RightClick(ui);Check(decline==null?sent<0:sent>=0&&p.request.options[sent].id=="cancel",name+" tap-selection does not invent native cancellation");
  }
  cost=Read(".tools/lan-replay-flow-1014/force-803.json");sent=-1;ui.Present(cost,1,true,i=>sent=i,null,"");yield return null;
  var handDock=(RectTransform)Get(ui,"handChoiceActions");var handNext=handDock.Find("ConfirmHandChoice").GetComponent<Button>();var handCancel=handDock.GetComponentsInChildren<Button>().First(b=>b!=handNext);
  Check(handNext.GetComponentInChildren<Text>().text=="下一步"&&((RectTransform)handNext.transform).anchoredPosition.x<((RectTransform)handCancel.transform).anchoredPosition.x,"Force exile draft has Next on left, Cancel on right");
  int blue=Array.FindIndex(cost.request.options,o=>o.card?.name=="Niv-Mizzet, Parun"||o.id=="card:36");Call(ui,"TryStageHandChoice",blue,cost.request.requestId);yield return null;handNext=((RectTransform)Get(ui,"handChoiceActions")).Find("ConfirmHandChoice").GetComponent<Button>();Check(sent<0&&handNext.interactable,"Force blue-card selection stays reversible local draft");handNext.onClick.Invoke();Check(cost.request.options[sent].id==cost.request.options[blue].id,"Force Next commits precisely the selected blue card");
  cost=Read(".tools/lan-replay-flow-1014/force-805.json");cost.request.minCount=cost.request.maxCount=2;cost.request.remainingCount=1;sent=-1;ui.Present(cost,1,true,i=>sent=i,null,"");yield return null;
  handDock=(RectTransform)Get(ui,"handChoiceActions");var three=handDock.GetComponentsInChildren<Button>();var ordered=three.OrderBy(b=>((RectTransform)b.transform).anchoredPosition.x).ToArray();
  Check(ordered.Length==3&&ordered[0].GetComponentInChildren<Text>().text=="下一步"&&ordered[1].GetComponentInChildren<Text>().text=="重新选择"&&ordered[2].GetComponentInChildren<Text>().text=="取消","Multi-card hand adapter keeps Next / Reset / Cancel in common dock");
  Check(Mathf.Approximately(handDock.rect.width,279),"Three hand controls fit the widened dock");ordered[2].onClick.Invoke();Check(sent>=0&&cost.request.options[sent].id=="cancel","Multi-card hand adapter uses exact native Cancel");
  cost=Read(".tools/lan-replay-flow-1014/force-803.json");var player=cost.players.First(p=>p.seat==1);var original=player.hand;var extra=Enumerable.Range(0,11).Select(i=>{var c=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(original[0]));c.id=9000+i;return c;}).ToArray();player.hand=original.Concat(extra).ToArray();player.handCount=player.hand.Length;sent=-1;ui.Present(cost,1,true,i=>sent=i,null,"");yield return null;
  var largeFooter=(RectTransform)Get(ui,"selectionFooter");var footerNext=largeFooter.Find("ConfirmHandChoice").GetComponent<Button>();var footerCancel=largeFooter.GetComponentsInChildren<Button>().First(b=>b!=footerNext);
  Check(footerNext.GetComponentInChildren<Text>().text=="下一步"&&((RectTransform)footerNext.transform).anchoredPosition.x<((RectTransform)footerCancel.transform).anchoredPosition.x,"Large-hand adapter uses left Next and right Cancel in picker footer");RightClick(ui);Check(sent>=0&&cost.request.options[sent].id=="cancel","Large-hand adapter right click cancels the exact original intent");
  var fallback=Read(Out+"/force-fallback-payment.json");sent=-1;ui.Present(fallback,1,true,i=>sent=i,null,"");yield return null;var fallbackDock=(RectTransform)Get(ui,"manaPaymentActions");var fallbackPay=fallbackDock.Find("AutoPayment").GetComponent<Button>();
  Check(fallbackPay.interactable&&fallbackPay.GetComponentInChildren<Text>().text=="自动支付","Unpayable Force blue-card alternative falls back to ordinary Auto pay");fallbackPay.onClick.Invoke();Check(sent>=0&&fallback.request.options[sent].id=="ok","Force fallback Auto pay submits exact native payment");
  foreach(int count in new[]{0,1,2}) {
   var p=Read(Out+"/multiple-many-targets-"+count+".json");sent=-1;ui.Present(p,1,true,i=>sent=i,null,"");yield return new WaitForSecondsRealtime(.85f);Canvas.ForceUpdateCanvases();Call(ui,"UpdateTargetingAt",new Vector2(15,15));
   Check(((ForgeSeatView)Get(ui,"view")).request.requestId==p.request.requestId,"Multi-target capture request identity "+count);
   Check(p.request.options.Count(o=>o.selected&&o.card!=null)==count,"Multi-target native selected-card count "+count);
   Check(!next.interactable&&next.GetComponentInChildren<Text>().text=="下一步","Native six-target selection enables Next only at its required count "+count);
   Check(next.GetComponentInChildren<Text>().color.r>.5f,"Disabled Next retains readable label "+count);
   yield return new WaitForEndOfFrame();Capture("multiple-"+count+"-targets");
   if(count==2){RightClick(ui);Check(sent>=0&&p.request.options[sent].id=="cancel","Partially selected six-target spell cancels the native intent");}
  }
  var committed=Read(".tools/target-cancel-1014/bolt-committed.json");sent=-1;ui.Present(committed,1,true,i=>sent=i,null,"");Check(!dock.gameObject.activeSelf&&!ui.TryCancelPresentedChoice()&&sent<0,"Committed spell cannot be cancelled by the shortcut");
 }
 public static void RunTail(){if(Running)return;checks.Clear();Running=true;new GameObject("PaymentControlsTailProbe").AddComponent<Host>().StartCoroutine(Guard(ExerciseTail()));}
 static IEnumerator ExerciseTail(){
  while(!GamePreferences.Ready)yield return null;
  foreach(var n in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))n.enabled=false;
  var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("PaymentControlsTailBoard").AddComponent<ForgeBattlefieldController>();ui.enabled=false;ui.GetType().GetField("service",Hidden).SetValue(ui,null);int sent=-1;
  var cost=Read(".tools/lan-replay-flow-1014/force-803.json");sent=-1;ui.Present(cost,1,true,i=>sent=i,null,"");yield return null;
  var handDock=(RectTransform)Get(ui,"handChoiceActions");var handNext=handDock.Find("ConfirmHandChoice").GetComponent<Button>();var handCancel=handDock.GetComponentsInChildren<Button>().First(b=>b!=handNext);
  Check(handNext.GetComponentInChildren<Text>().text=="下一步"&&((RectTransform)handNext.transform).anchoredPosition.x<((RectTransform)handCancel.transform).anchoredPosition.x,"Force exile draft has Next on left, Cancel on right");
  int blue=Array.FindIndex(cost.request.options,o=>o.card?.name=="Niv-Mizzet, Parun"||o.id=="card:36");Call(ui,"TryStageHandChoice",blue,cost.request.requestId);yield return null;handNext=((RectTransform)Get(ui,"handChoiceActions")).Find("ConfirmHandChoice").GetComponent<Button>();Check(sent<0&&handNext.interactable,"Force blue-card selection stays reversible local draft");handNext.onClick.Invoke();Check(cost.request.options[sent].id==cost.request.options[blue].id,"Force Next commits precisely the selected blue card");
  cost=Read(".tools/lan-replay-flow-1014/force-805.json");cost.request.minCount=cost.request.maxCount=2;cost.request.remainingCount=1;sent=-1;ui.Present(cost,1,true,i=>sent=i,null,"");yield return null;
  handDock=(RectTransform)Get(ui,"handChoiceActions");var three=handDock.GetComponentsInChildren<Button>();var ordered=three.OrderBy(b=>((RectTransform)b.transform).anchoredPosition.x).ToArray();
  Check(ordered.Length==3&&ordered[0].GetComponentInChildren<Text>().text=="下一步"&&ordered[1].GetComponentInChildren<Text>().text=="重新选择"&&ordered[2].GetComponentInChildren<Text>().text=="取消","Multi-card hand adapter keeps Next / Reset / Cancel in common dock");
  Check(Mathf.Approximately(handDock.rect.width,279),"Three hand controls fit the widened dock");ordered[2].onClick.Invoke();Check(sent>=0&&cost.request.options[sent].id=="cancel","Multi-card hand adapter uses exact native Cancel");
  cost=Read(".tools/lan-replay-flow-1014/force-803.json");var player=cost.players.First(p=>p.seat==1);var original=player.hand;var extra=Enumerable.Range(0,11).Select(i=>{var c=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(original[0]));c.id=9000+i;return c;}).ToArray();player.hand=original.Concat(extra).ToArray();player.handCount=player.hand.Length;sent=-1;ui.Present(cost,1,true,i=>sent=i,null,"");yield return null;
  var largeFooter=(RectTransform)Get(ui,"selectionFooter");var footerNext=largeFooter.Find("ConfirmHandChoice").GetComponent<Button>();var footerCancel=largeFooter.GetComponentsInChildren<Button>().First(b=>b!=footerNext);
  Check(footerNext.GetComponentInChildren<Text>().text=="下一步"&&((RectTransform)footerNext.transform).anchoredPosition.x<((RectTransform)footerCancel.transform).anchoredPosition.x,"Large-hand adapter uses left Next and right Cancel in picker footer");RightClick(ui);Check(sent>=0&&cost.request.options[sent].id=="cancel","Large-hand adapter right click cancels the exact original intent");
  var fallback=Read(Out+"/force-fallback-payment.json");sent=-1;ui.Present(fallback,1,true,i=>sent=i,null,"");yield return null;var fallbackDock=(RectTransform)Get(ui,"manaPaymentActions");var fallbackPay=fallbackDock.Find("AutoPayment").GetComponent<Button>();
  Check(fallbackPay.interactable&&fallbackPay.GetComponentInChildren<Text>().text=="自动支付","Unpayable Force blue-card alternative falls back to ordinary Auto pay");fallbackPay.onClick.Invoke();Check(sent>=0&&fallback.request.options[sent].id=="ok","Force fallback Auto pay submits exact native payment");
  foreach(int count in new[]{0,1,2}) {
   var p=Read(Out+"/multiple-many-targets-"+count+".json");sent=-1;ui.Present(p,1,true,i=>sent=i,null,"");yield return new WaitForSecondsRealtime(.85f);Canvas.ForceUpdateCanvases();Call(ui,"UpdateTargetingAt",new Vector2(15,15));
   var next=((RectTransform)Get(ui,"targetChoiceActions")).Find("NextTargetChoice").GetComponent<Button>();
   Check(((ForgeSeatView)Get(ui,"view")).request.requestId==p.request.requestId,"Multi-target capture request identity "+count);
   Check(p.request.options.Count(o=>o.selected&&o.card!=null)==count,"Multi-target native selected-card count "+count);
   Check(!next.interactable&&next.GetComponentInChildren<Text>().text=="下一步","Native six-target selection enables Next only at its required count "+count);
   Check(next.GetComponentInChildren<Text>().color.r>.5f,"Disabled Next retains readable label "+count);
   yield return new WaitForEndOfFrame();Capture("multiple-"+count+"-targets");
   if(count==2){RightClick(ui);Check(sent>=0&&p.request.options[sent].id=="cancel","Partially selected six-target spell cancels the native intent");}
  }
  var committed=Read(".tools/target-cancel-1014/bolt-committed.json");sent=-1;ui.Present(committed,1,true,i=>sent=i,null,"");Check(!((RectTransform)Get(ui,"targetChoiceActions")).gameObject.activeSelf&&!ui.TryCancelPresentedChoice()&&sent<0,"Committed spell cannot be cancelled by the shortcut");
 }
 public static void RunCostDraft(){if(Running)return;checks.Clear();Running=true;new GameObject("SingleNonmanaDraftProbe").AddComponent<Host>().StartCoroutine(Guard(ExerciseCostDraft()));}
 static IEnumerator ExerciseCostDraft(){
  while(!GamePreferences.Ready)yield return null;
  var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("SingleNonmanaDraftBoard").AddComponent<ForgeBattlefieldController>();ui.enabled=false;ui.GetType().GetField("service",Hidden).SetValue(ui,null);
  var p=Read(Out+"/rites-cost-0.json");int sent=-1;ui.Present(p,1,true,i=>sent=i,null,"");yield return null;
  var footer=(RectTransform)Get(ui,"selectionFooter");var next=footer.Find("ConfirmHandChoice").GetComponent<Button>();
  Check(p.request.paymentMode=="manual"&&!next.interactable&&next.GetComponentInChildren<Text>().text=="下一步","Native sacrifice cost starts with manual Next disabled");
  int index=Array.FindIndex(p.request.options,o=>o.card?.name=="Grizzly Bears");Check(!ui.TrySubmitPresentedOption(index,p.request.requestId)&&sent<0&&(string)Get(ui,"stagedHandChoice")==p.request.options[index].id,"Selecting sacrifice keeps a local reversible draft");yield return null;
  footer=(RectTransform)Get(ui,"selectionFooter");next=footer.Find("ConfirmHandChoice").GetComponent<Button>();Check(next.interactable,"Sacrifice draft enables Next without paying cost");
  Check(!ui.TrySubmitPresentedOption(index,p.request.requestId)&&sent<0&&Get(ui,"stagedHandChoice")==null,"Clicking drafted sacrifice again deselects locally");yield return null;
  next=((RectTransform)Get(ui,"selectionFooter")).Find("ConfirmHandChoice").GetComponent<Button>();Check(!next.interactable,"Deselecting sacrifice disables Next");
  ui.TrySubmitPresentedOption(index,p.request.requestId);yield return null;RightClick(ui);Check(sent>=0&&p.request.options[sent].id=="cancel","Right click while sacrifice drafted sends original cancellation");
  p=Read(Out+"/rites-cost-0.json");p.request.requestId+=1000;sent=-1;ui.Present(p,1,true,i=>sent=i,null,"");yield return null;ui.TrySubmitPresentedOption(index,p.request.requestId);yield return null;
  next=((RectTransform)Get(ui,"selectionFooter")).Find("ConfirmHandChoice").GetComponent<Button>();next.onClick.Invoke();Check(sent==index,"Sacrifice Next commits the selected native card exactly once");
 }
}
#endif
