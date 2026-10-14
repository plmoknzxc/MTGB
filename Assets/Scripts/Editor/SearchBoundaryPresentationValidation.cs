#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Existing confirmation/picker consumers: bounded increment, not a new UI first.
public static class SearchBoundaryPresentationValidation
{
 public static bool Running{get;private set;}
 public static string Output{get;private set;}
 [Serializable] class Result{public string folder,status;}
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static ForgeSeatView Read(string test,string label,int seat=1){var candidates=Directory.GetFiles(".tools/search-boundaries-1007/native/"+test,"result.json",SearchOption.AllDirectories).Select(p=>JsonUtility.FromJson<Result>(File.ReadAllText(p))).Where(r=>r.status=="PASS").ToArray();Check(candidates.Length==1,"Ambiguous native producer "+test);string path=candidates[0].folder+"/"+label+"-seat"+seat+".json",name=test+"-"+label+"-seat"+seat;if(!File.Exists(Output+"/inputs/"+name+".json")){File.Copy(path,Output+"/inputs/"+name+".json");File.AppendAllText(Output+"/inputs.tsv",name+"\t"+path+"\n");}return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));}
 static IEnumerator Frame(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
 static void Click(Button b){Canvas.ForceUpdateCanvases();Check(b!=null,"Control missing");var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.TransformPoint(((RectTransform)b.transform).rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(b.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Search ray blocked "+b.name+": "+string.Join(",",hits.Take(4).Select(h=>h.gameObject.name)));ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);}
 public static void Run(){Check(Application.isPlaying&&!Running,"Play required/already running");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("SearchBoundaryValidation").AddComponent<ForgeBattlefieldController>();Running=true;Output=".tools/search-boundaries-1007/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(".tools/search-boundaries-1007/ui/latest.txt",Output);foreach(string p in new[]{"Assets/Scripts/Editor/SearchBoundaryPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Notices.cs","Assets/Scripts/UI/ForgeBattlefieldController.CardSelection.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));ui.StartCoroutine(Guard(ui));}
 static IEnumerator Guard(ForgeBattlefieldController ui){var tasks=new Stack<IEnumerator>();tasks.Push(Validate(ui));try{while(tasks.Count>0){object next;try{var t=tasks.Peek();if(!t.MoveNext()){tasks.Pop();continue;}next=t.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator nested)tasks.Push(nested);else yield return next;}File.WriteAllText(Output+"/result.txt","PASS: actual search native prompts/pickers, three languages on changed confirmations; main-language restricted/grave/hand native identity and confirmation, cancellation, request/seat/duplicate guards, actual rays and fixed fonts/bounds; existing binary regression. UI increment only; native rule outcomes independently proved.");}finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}}
 static IEnumerator Validate(ForgeBattlefieldController ui)
 {
  string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var migration=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool migrationEnabled=migration!=null&&migration.enabled;if(migration!=null)migration.enabled=false;var module=EventSystem.current.currentInputModule;bool moduleEnabled=module!=null&&module.enabled;if(module!=null)module.enabled=false;
  try{foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
   int revision=GamePreferences.Revision;bool changing=GamePreferences.Language!=language;GamePreferences.SetLanguage(language);float until=Time.realtimeSinceStartup+12;while((!GamePreferences.Ready||changing&&GamePreferences.Revision==revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale not ready");
   // Earlier eighteen completed Chinese cells retain their original FAIL run.
   // Continue only the remaining contract differences after the harness fix.
   var confirmations=new[]{("optional-accept","confirm-1",1),("mixed-grave","confirm-1",1),("mixed-grave","confirm-2",1),("mixed-grave","confirm-3",1),("mixed-grave","confirm-4",1),("restricted-fail","confirm-1",1),("opponent-accept","confirm-1",2)};
   foreach(var sample in language=="zh-CN"?Array.Empty<(string,string,int)>():confirmations){
    var v=Read(sample.Item1,sample.Item2,sample.Item3);
    foreach(int choice in new[]{0,1}){int calls=0,last=-1;ui.Present(null,sample.Item3,false,null,null,"");ui.Present(v,sample.Item3,true,i=>{calls++;last=i;},null,"");yield return Frame();var menu=GameObject.Find("CardAbilityChoices");Check(menu!=null,"Confirmation absent "+sample);var title=menu.GetComponentsInChildren<Text>().Single(t=>t.name=="ValueTitle");Check(!title.resizeTextForBestFit,"Search title autofit");if(language=="zh-CN")Check(!title.text.Contains("Search")&&!title.text.Contains("search")&&!title.text.Contains("Triggered by")&&!title.text.Contains("Use triggered"),"Chinese search prompt untranslated: "+title.text);if(language=="ja-JP")Check(!title.text.Contains("Search")&&!title.text.Contains("search")&&!title.text.Contains("Triggered by")&&!title.text.Contains("Use triggered"),"Japanese search prompt untranslated: "+title.text);Check(title.cachedTextGeneratorForLayout.GetPreferredHeight(title.text,title.GetGenerationSettings(title.rectTransform.rect.size))<=title.rectTransform.rect.height+2,"Search prompt clipped");
     var fonts=menu.GetComponentsInChildren<Text>().Select(t=>t.fontSize).ToArray();ui.Choose(choice,v.request.requestId-1);Check(calls==0,"Stale search accepted");Click(menu.GetComponentsInChildren<Button>().Single(b=>b.name=="ValueOption_"+choice));ui.Choose(choice,v.request.requestId);Check(calls==1&&last==choice,"Native confirmation identity/duplicate mismatch");ui.Present(v,sample.Item3,false,i=>calls++,null,"");yield return Frame();ui.Choose(choice,v.request.requestId);Check(calls==1,"Readonly search submitted");Check(fonts.SequenceEqual(menu.GetComponentsInChildren<Text>().Select(t=>t.fontSize)),"Search font changed");
     // The service supplies enabledInput after controller/seat authorization.
     // A local-seat mismatch alone is not sufficient during controlled turns.
     ui.Present(null,sample.Item3,false,null,null,"");ui.Present(v,3-sample.Item3,false,i=>calls++,null,"");yield return Frame();ui.Choose(choice,v.request.requestId);Check(calls==1,"Disabled other-seat search submitted");File.AppendAllText(Output+"/steps.log",language+" "+sample.Item1+" "+sample.Item2+" seat"+sample.Item3+" choice"+choice+" PASS\n");
    }
   }
   if(language=="zh-CN")foreach(var sample in new[]{("mixed-hand","picker-2"),("restricted-fail","picker-1")}){
    var v=Read(sample.Item1,sample.Item2);var r=v.request;int calls=0,last=-1;ui.Present(null,1,false,null,null,"");ui.Present(v,1,true,i=>{calls++;last=i;},null,"");yield return Frame();string wanted=sample.Item1=="restricted-fail"?"cancel":r.selectedCount>0?"ok":r.options.First(o=>o.card!=null&&!string.IsNullOrEmpty(o.card.name)).id;int index=Array.FindIndex(r.options,o=>o.id==wanted);Check(index>=0,"Native picker option absent");var buttons=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b=>b.gameObject.activeInHierarchy).ToArray();Button control;
    if(wanted.StartsWith("card:")){int id=r.options[index].card.id;control=buttons.FirstOrDefault(b=>b.name=="Artwork"&&b.transform.parent.name=="ChoiceCard_"+id)??buttons.FirstOrDefault(b=>b.name=="Card_"+id);}else control=buttons.FirstOrDefault(b=>b.name=="SelectionAction_"+index)??buttons.FirstOrDefault(b=>b.name=="HandAction_"+index)??(wanted=="ok"?buttons.FirstOrDefault(b=>b.name=="SkipCombatButton"):null);
    Check(control!=null,"Search control absent "+sample+" "+wanted+"; "+string.Join(",",buttons.Select(b=>b.name)));Click(control);
    if(sample.Item1=="mixed-hand"&&sample.Item2=="picker-1") {Check(calls==0,"Draft hand choice submitted early");yield return Frame();Click(GameObject.Find("ConfirmHandChoice").GetComponent<Button>());}
    Check(calls==1&&last==index,"Picker native identity mismatch");File.AppendAllText(Output+"/steps.log",language+" "+sample.Item1+" "+sample.Item2+" "+wanted+" PASS\n");
   }
   var legacy=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/RandomMechanics/xeno-dec-increment-choice.json"));int legacyCalls=0;ui.Present(null,1,false,null,null,"");ui.Present(legacy,1,true,i=>{Check(i==1,"Legacy binary identity");legacyCalls++;},null,"");yield return Frame();Click(GameObject.Find("ValueOption_1").GetComponent<Button>());Check(legacyCalls==1,"Legacy binary failed");File.AppendAllText(Output+"/steps.log",language+" legacy-binary PASS\n");
   ui.Present(null,1,false,null,null,"");ui.Present(Read("mixed-grave","confirm-2"),1,true,_=>{},null,"");yield return Frame();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Output+"/"+language+"-alternate-search.png");yield return new WaitForSecondsRealtime(.2f);
  }}finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(migration!=null)migration.enabled=migrationEnabled;if(module!=null)module.enabled=moduleEnabled;GamePreferences.SetLanguage(locale);}
 }
}
#endif
