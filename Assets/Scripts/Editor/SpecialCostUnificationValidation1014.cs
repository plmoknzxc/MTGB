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
public static class SpecialCostUnificationValidation1014 {
 const string Out=".tools/cost-unification-1014";const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 sealed class Host:MonoBehaviour{}public static bool Running;static List<string> checks=new();static Mouse probeMouse;
 static object Get(object o,string n)=>o.GetType().GetField(n,Hidden).GetValue(o);
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Hidden|BindingFlags.Public).Invoke(o,a);
 static ForgeSeatView Read(string p)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p));
 static void Check(bool ok,string n){if(!ok)throw new Exception(n);checks.Add("PASS "+n);}
 public static void Run(){if(Running)return;checks.Clear();Running=true;new GameObject("PaymentControlsProbe").AddComponent<Host>().StartCoroutine(Guard(Exercise()));}
 static IEnumerator Guard(IEnumerator e){while(true){bool more;object next;try{more=e.MoveNext();next=more?e.Current:null;}catch(Exception ex){checks.Add("FAIL "+ex);more=false;next=null;}if(!more)break;yield return next;}if(probeMouse!=null){InputSystem.RemoveDevice(probeMouse);probeMouse=null;}File.WriteAllLines(Out+"/ui-result.txt",checks);Running=false;}
 static void Capture(string n){var tex=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Out+"/"+n+".png",tex.EncodeToPNG());UnityEngine.Object.Destroy(tex);}
 static void RightClick(ForgeBattlefieldController ui){if(probeMouse==null)probeMouse=InputSystem.AddDevice<Mouse>();InputSystem.QueueStateEvent(probeMouse,new MouseState{position=new Vector2(15,15)}.WithButton(MouseButton.Right));InputSystem.Update();probeMouse.MakeCurrent();Call(ui,"UpdateChoiceCancelShortcut");InputSystem.QueueStateEvent(probeMouse,new MouseState{position=new Vector2(15,15)});InputSystem.Update();}
 static IEnumerator Exercise(){
  while(!GamePreferences.Ready)yield return null;
  foreach(var n in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))n.enabled=false;
  var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("CostUnificationBoard").AddComponent<ForgeBattlefieldController>();ui.enabled=false;ui.GetType().GetField("service",Hidden).SetValue(ui,null);
  var counter=Read(Out+"/counter-type-scalar.json");int sent=-1;ui.Present(counter,counter.request.seat,true,i=>sent=i,null,"");yield return null;
  var menu=(RectTransform)Get(ui,"landChoiceMenu");var counterButtons=menu.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy).ToArray();
  Check(counterButtons.Length==2&&!counterButtons.Any(b=>b.GetComponentInChildren<Text>()?.text=="下一步"),"Counter-type scalar picker has only native type choices, no duplicate Next");
  var dock=(RectTransform)Get(ui,"targetChoiceActions");var next=dock.Find("NextTargetChoice").GetComponent<Button>();Check(dock.gameObject.activeInHierarchy&&!next.interactable,"Counter type uses one shared disabled Next before selection");
  var amount=Read(Out+"/number-original.json");amount.request.paymentMode="manual";amount.request.kind="dialog";amount.request.message="one: Select number of charge counters to remove";amount.request.options=new[]{new ForgeOption{id="choice:0",label="1"},new ForgeOption{id="choice:1",label="2"}};amount.request.requestId+=99999;sent=-1;
  ui.Present(amount,amount.request.seat,true,i=>sent=i,null,"");yield return null;menu=(RectTransform)Get(ui,"landChoiceMenu");
  Check(menu.Find("ConfirmValue").GetComponentInChildren<Text>().text=="下一步","Numeric cost with a localized value title still uses the shared numeric dock");
  menu.Find("ConfirmValue").GetComponent<Button>().onClick.Invoke();Check(sent>=0&&amount.request.options[sent].id=="choice:0","Numeric cost adapter submits one original integer option, no second confirmation");
  Check(!ui.TryCancelPresentedChoice(),"No cancel invented for mandatory native numeric list");
  var forage=Read(Out+"/forage-method.json");int forageSent=-1;ui.Present(forage,1,true,i=>forageSent=i,null,"");yield return null;
  var choices=((RectTransform)Get(ui,"landChoiceMenu")).GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy).ToArray();
  Check(choices.Any(b=>b.GetComponentInChildren<Text>()?.text=="牺牲食品")&&choices.Any(b=>b.GetComponentInChildren<Text>()?.text=="放逐坟场三张"),"Forage shows real payment methods rather than Yes / No");
  RightClick(ui);Check(forageSent<0&&!ui.TryCancelPresentedChoice(),"Forage graveyard branch is not mistaken for cancellation");
  var exile=Read(Out+"/exile-2.json");int exileSent=-1;ui.Present(exile,exile.request.seat,true,i=>exileSent=i,null,"");yield return null;
  var handDock=(RectTransform)Get(ui,"handChoiceActions");var actions=handDock.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy).OrderBy(b=>((RectTransform)b.transform).anchoredPosition.x).ToArray();
  Check(actions.First().GetComponentInChildren<Text>().text=="下一步"&&actions.Last().GetComponentInChildren<Text>().text=="取消"&&actions.First().interactable,"Actual acting seat gets enabled Next and Cancel for full exile draft");
  RightClick(ui);Check(exileSent>=0&&exile.request.options[exileSent].id=="cancel","Actual acting seat can right-cancel full exile draft");
  var ordinal=Read(".tools/bolt-amber-flow-1014/amber-05-select-color.json");int selected=-1;ui.Present(ordinal,1,true,i=>selected=i,null,"");yield return null;RightClick(ui);Check(selected<0,"Blue remains a mode, not cancellation");
  var spell=Read(".tools/lan-replay-flow-1014/force-803.json");spell.request.options.First(o=>o.id=="cancel").label="取消施放";selected=-1;ui.Present(spell,1,true,i=>selected=i,null,"");yield return null;RightClick(ui);Check(selected>=0&&spell.request.options[selected].id=="cancel","Explicit cancel-casting caption shares right-click shortcut");
 }
}
#endif
