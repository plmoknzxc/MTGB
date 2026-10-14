#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
public static class ChoiceWindowCapture1014
{
 const string Out=".tools/delayed-marker-review-1014";
 const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 sealed class Host:MonoBehaviour{}
 public static bool Running;
 static List<string> checks=new();
 static object Get(object obj,string field)=>obj.GetType().GetField(field,Hidden).GetValue(obj);
 static object Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,Hidden).Invoke(obj,args);
 static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Out+"/"+name+".json"));
 static void Check(bool ok,string description){if(!ok)throw new Exception(description);checks.Add("PASS "+description);}
 public static void Run(){checks.Clear();Running=true;new GameObject("ChoiceCaptureProbe").AddComponent<Host>().StartCoroutine(Guard(Exercise()));}
 static IEnumerator Guard(IEnumerator task){while(true){bool more;object next;try{more=task.MoveNext();next=more?task.Current:null;}catch(Exception e){checks.Add("FAIL "+e);more=false;next=null;}if(!more)break;yield return next;}File.WriteAllLines(Out+"/ui-capture-result.txt",checks);Running=false;}
 static void Capture(string name,RectTransform panel=null)
 {
  var tex=ScreenCapture.CaptureScreenshotAsTexture();
  if(panel!=null){var corners=new Vector3[4];panel.GetWorldCorners(corners);var a=RectTransformUtility.WorldToScreenPoint(null,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(null,corners[2]);int x=Mathf.Max(0,Mathf.FloorToInt(a.x)-24),y=Mathf.Max(0,Mathf.FloorToInt(a.y)-24),w=Mathf.Min(tex.width-x,Mathf.CeilToInt(b.x)-x+24),h=Mathf.Min(tex.height-y,Mathf.CeilToInt(b.y)-y+24);var crop=new Texture2D(w,h,TextureFormat.RGBA32,false);crop.SetPixels(tex.GetPixels(x,y,w,h));crop.Apply();File.WriteAllBytes(Out+"/"+name+".png",crop.EncodeToPNG());UnityEngine.Object.Destroy(crop);}else if(name.StartsWith("force-")){int h=Mathf.FloorToInt(tex.height*.83f);var focus=new Texture2D(tex.width,h,TextureFormat.RGBA32,false);focus.SetPixels(tex.GetPixels(0,0,tex.width,h));focus.Apply();File.WriteAllBytes(Out+"/"+name+".png",focus.EncodeToPNG());UnityEngine.Object.Destroy(focus);}else File.WriteAllBytes(Out+"/"+name+".png",tex.EncodeToPNG());UnityEngine.Object.Destroy(tex);
 }
 static IEnumerable<int> Markers(ForgeBattlefieldController ui){var dict=(System.Collections.IDictionary)Get(ui,"arenaCards");foreach(System.Collections.DictionaryEntry e in dict){if(!((string)e.Key).Contains("Emblem:"))continue;var r=(RectTransform)e.Value.GetType().GetField("root").GetValue(e.Value);if(r.gameObject.activeInHierarchy)yield return int.Parse(r.name.Substring("Card_".Length));}}
 static IEnumerator Exercise()
 {
  while(!GamePreferences.Ready)yield return null;
  var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ChoiceCaptureBoard").AddComponent<ForgeBattlefieldController>();ui.enabled=false;
  foreach(string name in new[]{"surveil","explore","plan-position"}){
   var v=Read(name);ui.Present(v,v.seat,true,i=>{},null,"");yield return new WaitForSecondsRealtime(.85f);Canvas.ForceUpdateCanvases();
   foreach(var text in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None))if(text.text.Contains("请从联机大厅")||text.text=="返回主菜单")text.transform.parent.gameObject.SetActive(false);
   var panel=(RectTransform)Get(ui,"selectionPanel");Check(panel!=null&&panel.gameObject.activeInHierarchy,name+" actual production choice window visible");
   if(name=="plan-position")Check(v.request.previewCards.Single().name=="Grizzly Bears", "Plan owner choice previews target, not source enchantment");
   yield return new WaitForEndOfFrame();Capture(name,panel);
  }
  var force=Read("force-InputSelectCardsFromList-initial");ui.Present(force,force.seat,true,i=>{},null,"");yield return new WaitForSecondsRealtime(.85f);yield return new WaitForEndOfFrame();Capture("force-choose");
  var complete=Read("force-InputSelectCardsFromList-complete");var service=OnlineMatchService.EnsureInstance();service.GetType().GetField("<ForgeView>k__BackingField",Hidden).SetValue(service,new OnlinePlayerViewState{forge=complete});ui.GetType().GetField("service",Hidden).SetValue(ui,service);int chosen=-1;ui.Present(complete,complete.seat,true,i=>chosen=i,null,"");Call(ui,"UpdateAutomaticControl");Check(chosen>=0&&complete.request.options[chosen].id=="ok","Force selected card advances with native OK automatically");
  ui.GetType().GetField("service",Hidden).SetValue(ui,null);service.GetType().GetField("<ForgeView>k__BackingField",Hidden).SetValue(service,null);
  var after=Read("force-after");ui.Present(after,after.seat,false,null,null,"");yield return new WaitForSecondsRealtime(.85f);Check(after.players.Any(p=>p.exileCards.Any(c=>c.id==36)),"Native payment aftermath has selected blue card in exile");yield return new WaitForEndOfFrame();Capture("force-after");
  var wait=Read("warp-waiting");ui.Present(wait,wait.seat,false,null,null,"");yield return new WaitForSecondsRealtime(.5f);int delayed=wait.players.SelectMany(p=>p.effectMarkers??Array.Empty<ForgeCardView>()).Single(c=>c.name=="Pinnacle Emissary"&&c.markerKind=="delayed").id;Check(Markers(ui).Contains(delayed),"Warp delayed exile visible while waiting");
  var expired=Read("warp-after");ui.Present(expired,expired.seat,false,null,null,"");yield return new WaitForSecondsRealtime(.5f);int permission=expired.players.SelectMany(p=>p.effectMarkers??Array.Empty<ForgeCardView>()).Single(c=>(c.name??"").StartsWith("Warped Pinnacle")).id;Check(!Markers(ui).Contains(delayed)&&!Markers(ui).Contains(permission),"Consumed Warp delay and continuing exile permission do not leave marker ghosts");yield return new WaitForEndOfFrame();Capture("warp-cleared");
  var owner=expired.players.First(p=>p.seat==expired.seat);var marker=new ForgeCardView{id=-9000001,name="Mishra's Bauble",effectMarker=true,markerKind="delayed",ownerSeat=owner.seat,zone="Command",printingKey="2XM/274",currentRules="Draw a card at the beginning of the next turn's upkeep."};owner.effectMarkers=(owner.effectMarkers??Array.Empty<ForgeCardView>()).Append(marker).ToArray();ui.Present(expired,expired.seat,false,null,null,"");yield return null;Check(Markers(ui).Contains(marker.id),"Other next-turn delayed marker remains visible before expiration");owner.effectMarkers=owner.effectMarkers.Where(m=>m.id!=marker.id).ToArray();ui.Present(expired,expired.seat,false,null,null,"");yield return null;Check(!Markers(ui).Contains(marker.id),"Other next-turn delayed marker node removed after authority removal");
 }
}
#endif
