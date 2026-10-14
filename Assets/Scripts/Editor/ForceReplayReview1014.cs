#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
public static class ForceReplayReview1014 {
 const string Out=".tools/lan-replay-flow-1014";const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 sealed class Host:MonoBehaviour{} public static bool Running;static List<string> checks=new();
 static object Get(object o,string n)=>o.GetType().GetField(n,Hidden).GetValue(o);
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Hidden).Invoke(o,a);
 static void Check(bool ok,string n){if(!ok)throw new Exception(n);checks.Add("PASS "+n);}
 static ForgeSeatView Read(int id)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Out+"/force-"+id+".json"));
 public static void Run(){Running=true;checks.Clear();new GameObject("ForceReplayReviewProbe").AddComponent<Host>().StartCoroutine(Guard(Exercise()));}
 static IEnumerator Guard(IEnumerator e){while(true){bool more;object next;try{more=e.MoveNext();next=more?e.Current:null;}catch(Exception ex){checks.Add("FAIL "+ex);more=false;next=null;}if(!more)break;yield return next;}File.WriteAllLines(Out+"/ui-result.txt",checks);Running=false;}
 static void Capture(string name){var tex=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Out+"/"+name+".png",tex.EncodeToPNG());UnityEngine.Object.Destroy(tex);}
 static IEnumerator Exercise(){
  while(!GamePreferences.Ready)yield return null;
  ReplayBrowser.Show();var browser=UnityEngine.Object.FindFirstObjectByType<ReplayBrowser>();var delete=(Button)Get(browser,"delete");Check(delete!=null&&!delete.interactable,"Delete button visible and disabled without selected replay");
  string file=Path.Combine(ReplayRepository.Root,"__delete_probe_"+Guid.NewGuid().ToString("N")+".mtgbr");Directory.CreateDirectory(ReplayRepository.Root);File.WriteAllText(file,"invalid owned test replay");browser.RefreshFiles();browser.Select(file);yield return new WaitForSecondsRealtime(.3f);Check(delete.interactable&&((Text)Get(browser,"details")).text=="无法打开录像","Damaged replay can still be deleted");
  delete.onClick.Invoke();var dialog=(RectTransform)Get(browser,"deleteDialog");Check(dialog!=null,"Delete opens confirmation for selected file");dialog.GetComponentsInChildren<Button>().Single(b=>b.name=="取消").onClick.Invoke();Check(File.Exists(file),"Cancel preserves exact replay file");
  using(var locked=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)){delete.onClick.Invoke();dialog=(RectTransform)Get(browser,"deleteDialog");dialog.GetComponentsInChildren<Button>().Single(b=>b.name=="确认删除").onClick.Invoke();Check(File.Exists(file)&&((Text)Get(browser,"status")).text.StartsWith("删除失败"),"Locked replay gives failure and preserves file");}
  delete.onClick.Invoke();dialog=(RectTransform)Get(browser,"deleteDialog");dialog.GetComponentsInChildren<Button>().Single(b=>b.name=="确认删除").onClick.Invoke();yield return null;Check(!File.Exists(file)&&Get(browser,"selectedPath")==null&&Get(browser,"selected")==null,"Confirmed delete removes file and clears stale selection");Check(!delete.interactable&&!((Button)Get(browser,"play")).interactable,"Deleted replay actions disabled");Check(!ReplayRepository.ListFiles().Contains(file),"Deleted replay removed from actual file list");
  string outside=Path.GetFullPath(Out+"/keep-outside.mtgbr");File.WriteAllText(outside,"owned outside fixture");bool rejected=false;try{ReplayRepository.Delete(outside);}catch(InvalidDataException){rejected=true;}Check(rejected&&File.Exists(outside),"Replay delete rejects file outside repository");
  file=Path.Combine(ReplayRepository.Root,"__delete_probe_"+Guid.NewGuid().ToString("N")+".mtgbr");File.WriteAllText(file,"pending owned fixture");browser.RefreshFiles();browser.Select(file);ReplayRepository.Delete(file);yield return new WaitForSecondsRealtime(.4f);Check(Get(browser,"selectedPath")==null&&Get(browser,"selected")==null&&((Text)Get(browser,"details")).text=="选择一场对局","Delete during async loading cannot restore deleted selection");
  yield return new WaitForEndOfFrame();Capture("replay-delete");UnityEngine.Object.Destroy(browser.gameObject);yield return null;
  var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ForceReviewBoard").AddComponent<ForgeBattlefieldController>();ui.enabled=false;
  foreach(int id in new[]{801,802,803}){
   var v=Read(id);ui.Present(v,v.seat,true,i=>{},null,"");yield return new WaitForSecondsRealtime(.8f);
   foreach(var t in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None))if(t.text.Contains("请从联机大厅")||t.text=="返回主菜单")t.transform.parent.gameObject.SetActive(false);
   if(id==801)Check(v.request.options.Any(o=>o.id=="stack:61"&&o.label=="Mishra's Bauble"),"Native Force target is exact stack spell 61");
   if(id==802)Check(v.request.options.Any(o=>o.id=="ok")&&v.request.options.Any(o=>o.id=="reset-targets"),"Target selection allows confirm or reset before payment");
   yield return new WaitForEndOfFrame();Capture(id==801?"force-02-select-target":id==802?"force-03-target-selected":"force-04-select-blue-cost");
  }
  var cost=Read(803);int sent=-1;ui.Present(cost,cost.seat,true,i=>sent=i,null,"");int choice=Array.FindIndex(cost.request.options,o=>o.id=="card:36");Check((bool)Call(ui,"TryStageHandChoice",choice,cost.request.requestId),"Blue cost card selection is staged locally");Check(sent<0,"Staging blue cost makes no premature request");yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Capture("force-05-cost-selected");Call(ui,"CommitHandChoice",cost.request.requestId);Check(sent==choice,"Confirm submits exact blue card 36 once");
  var complete=Read(805);var service=OnlineMatchService.EnsureInstance();service.GetType().GetField("<ForgeView>k__BackingField",Hidden).SetValue(service,new OnlinePlayerViewState{forge=complete});ui.GetType().GetField("service",Hidden).SetValue(ui,service);int confirmed=-1;ui.Present(complete,complete.seat,true,i=>confirmed=i,null,"");Call(ui,"UpdateAutomaticControl");Check(confirmed>=0&&complete.request.options[confirmed].id=="ok","Native completed cost auto-acknowledges without a second payment dialog");ui.GetType().GetField("service",Hidden).SetValue(ui,null);service.GetType().GetField("<ForgeView>k__BackingField",Hidden).SetValue(service,null);
  var after=Read(806);ui.Present(after,after.seat,false,null,null,"");yield return new WaitForSecondsRealtime(.8f);Check(after.players.Any(p=>p.exileCards.Any(c=>c.id==36)),"Paid blue card in authoritative exile");Check(after.stackItems.Any(s=>s.source?.name=="Force of Negation"),"Force spell is now on native stack");yield return new WaitForEndOfFrame();Capture("force-06-cast-complete");
 }
}
#endif
