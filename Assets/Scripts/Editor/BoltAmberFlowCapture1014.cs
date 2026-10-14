#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
public static class BoltAmberFlowCapture1014 {
 const string Out=".tools/bolt-amber-flow-1014";const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 sealed class Host:MonoBehaviour{} public static bool Running;static List<string> checks=new();
 static object Get(object o,string n)=>o.GetType().GetField(n,Hidden).GetValue(o);
 static void Check(bool ok,string n){if(!ok)throw new Exception(n);checks.Add("PASS "+n);}
 public static void Run(){if(Running)return;checks.Clear();Running=true;new GameObject("BoltAmberFlowProbe").AddComponent<Host>().StartCoroutine(Guard(Exercise()));}
 static IEnumerator Guard(IEnumerator e){while(true){bool more;object next;try{more=e.MoveNext();next=more?e.Current:null;}catch(Exception ex){checks.Add("FAIL "+ex);more=false;next=null;}if(!more)break;yield return next;}File.WriteAllLines(Out+"/ui-result.txt",checks);Running=false;}
 static IEnumerator Exercise(){
  while(!GamePreferences.Ready)yield return null;
  foreach(var notice in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))notice.enabled=false;
  var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("BoltAmberFlowBoard").AddComponent<ForgeBattlefieldController>();ui.enabled=false;
  string[] names={"bolt-01-hand","bolt-02-select-target","bolt-03-target-selected","bolt-04-mana-payment","bolt-06-on-stack","bolt-07-resolved","amber-01-hand","amber-03-on-stack","amber-04-battlefield","amber-05-select-color","amber-06-mana-produced"};
  foreach(string name in names){
   var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Out+"/"+name+".json"));ui.Present(v,1,v.awaitingInput,i=>{},null,"");yield return new WaitForSecondsRealtime(.75f);Canvas.ForceUpdateCanvases();
   Check(((ForgeSeatView)Get(ui,"view")).request.requestId==v.request.requestId,"Displayed request matches captured stage "+name);
   if(name=="bolt-02-select-target")Check(v.request.options.Any(o=>o.id=="player:1"),"Bolt actual opponent target present");
   if(name=="bolt-03-target-selected")Check(v.request.options.Any(o=>o.id=="ok"&&!o.disabled),"Bolt selected target has native confirm");
   if(name=="bolt-04-mana-payment")Check(v.request.remainingManaCost=="{R}","Bolt native payment requests one red mana");
   if(name=="bolt-06-on-stack")Check(v.stackItems.Any(s=>s.source?.name=="Lightning Bolt"),"Bolt visible native stack spell");
   if(name=="bolt-07-resolved")Check(v.players.Single(p=>p.seat==2).life==17,"Bolt resolved opponent life is 17");
   if(name=="amber-03-on-stack")Check(v.stackItems.Any(s=>s.source?.name=="Mox Amber"),"Amber visible native stack spell");
   if(name=="amber-04-battlefield")Check(v.players.Single(p=>p.seat==1).battlefield.Any(c=>c.name=="Mox Amber"&&!c.tapped),"Amber untapped after native entrance");
   if(name=="amber-05-select-color")Check(v.request.options.Select(o=>o.label).OrderBy(x=>x).SequenceEqual(new[]{"Blue","Green"}),"Amber choices match actual legendary colors blue and green");
   if(name=="amber-06-mana-produced")Check(v.players.Single(p=>p.seat==1).battlefield.Any(c=>c.name=="Mox Amber"&&c.tapped)&&v.players.Single(p=>p.seat==1).manaPool.Sum()==1,"Amber tapped and one mana in actual pool");
   yield return new WaitForEndOfFrame();var tex=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Out+"/"+name+".png",tex.EncodeToPNG());UnityEngine.Object.Destroy(tex);
  }
 }
}
#endif
