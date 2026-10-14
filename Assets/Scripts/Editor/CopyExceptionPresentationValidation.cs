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

// Supplementary native-input replay. Copy intrinsic-text gaps remain owned by R23.
public static class CopyExceptionPresentationValidation {
    const string Base=".tools/copy-exceptions-1009";
    static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    public static bool Running{get;private set;}
    public static string Output{get;private set;}
    static int checks;
    static bool menuOnly;
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;File.AppendAllText(Output+"/checks.txt",label+"\n");}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Output+"/inputs/"+name+".json"));
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Clean(ForgeBattlefieldController ui){foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}var f=typeof(ForgeBattlefieldController).GetField("inspectHoverRevision",Hidden);f.SetValue(ui,(int)f.GetValue(ui)+1);Root.Find("InspectPanel").gameObject.SetActive(false);}
    static void Ray(Button b){var rect=(RectTransform)b.transform;var p=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(b.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Exact topmost ray "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    public static void Run()=>RunScope(false);
    public static void RunMenuOnly()=>RunScope(true);
    static void RunScope(bool only){if(!Application.isPlaying||Running)throw new Exception("Play required/already running");menuOnly=only;Running=true;checks=0;Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/scope.txt",only?"menu-only":"display-and-menu");File.WriteAllText(Output+"/result.txt","RUNNING");foreach(var p in Directory.GetFiles(Base+"/ui-inputs"))File.Copy(p,Output+"/inputs/"+Path.GetFileName(p));foreach(var p in new[]{"Assets/Scripts/Editor/CopyExceptionPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Arena.cs","Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs","Assets/Scripts/UI/ForgeBattlefieldController.ModifiedCharacteristics.cs","Assets/Scripts/UI/ForgeBattlefieldController.LandChoices.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("CopyExceptionValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));}
    static IEnumerator Guard(IEnumerator work){var tasks=new Stack<IEnumerator>();tasks.Push(work);try{while(tasks.Count>0){object next;try{var t=tasks.Peek();if(!t.MoveNext()){tasks.Pop();continue;}next=t.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator child)tasks.Push(child);else yield return next;}File.WriteAllText(Output+"/result.txt","PASS_BOUNDED: "+checks+" checks; "+(menuOnly?"copied recipient exact native ID and +1 ability index/ray only.":"exact native copied creature P/T/current keyword changes and original-state return; copied Jace exact current loyalty badges and recipient activation ID/ray. R23 known removed-CDA oracle-description gap recorded separately.")+" No full-copy-text/language/network PASS.");}finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui){
        string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{GamePreferences.SetLanguage("zh-CN");float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Preferences ready");
            if(!menuOnly)foreach(int seat in new[]{1,2}){
                foreach(string label in new[]{"first-copy","counter","frog","recopy","source-left","finished"}){
                    var v=Read("ratadrabik-"+label+"-seat"+seat);ui.Present(null,seat,false,null,null,"");ui.Present(v,seat,false,null,null,"");yield return Frame();Clean(ui);
                    foreach(var card in v.players[0].battlefield.Where(c=>c.token)){
                        var obj=GameObject.Find("Card_"+card.id);Check(obj!=null,"Exact native token present "+seat+" "+label+" "+card.id);var value=obj.transform.Find("Visual/Stats/Value").GetComponent<Text>();Check(value.text==ForgeBattlefieldController.ModifiedStatsText(card,false),"Native P/T and tint "+label+" "+card.id);
                        typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{card});yield return Frame();var rules=Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>();
                        if(label=="frog"&&card.powerBaseChanged){Check(rules.text.Contains("<color=#70BFFF>")&&rules.text.Contains("1</color>"),"Actual rewritten base 1/1 blue detail");var original=Root.Find("InspectPanel/OriginalState").GetComponent<Button>();Ray(original);yield return Frame();var footer=Root.Find("InspectPanel/Footer").GetComponent<Text>();Check(footer.text.StartsWith("2/2"),"Copied CardState original2/2,not external Frog1/1/counter");Ray(original);yield return Frame();}
                        if(label=="finished"&&card.power==3)Check(value.text.Contains("16894C"),"Counter growth remains green after external SetPT expires");Clean(ui);
                    }
                }
                foreach(string label in new[]{"first-copy","source-left","recopy"}){
                    var v=Read("dino-"+label+"-seat"+seat);ui.Present(null,seat,false,null,null,"");ui.Present(v,seat,false,null,null,"");yield return Frame();Clean(ui);var card=v.players[0].battlefield.First(c=>c.token);var obj=GameObject.Find("Card_"+card.id);Check(obj!=null&&obj.transform.Find("Visual/Stats/Value").GetComponent<Text>().text=="6/6","Actual replacement copy6/6 persists "+label);
                    typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{card});yield return Frame();var rules=Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>();Check(rules.text.Contains("<color=#70BFFF>践踏</color>"),"Explicit added trample uses blue keyword export");if(label=="first-copy"&&seat==1){File.WriteAllText(Output+"/known-gap-R23.txt","Native CDA is absent (read-only observer) and actual recopy stays6/6 while grave count changes. Export currentRules/Inspect Rules still contain the original removed P/T-defining oracle sentence. R23.01 owns copy-intrinsic text synchronization with Evil Twin/Dollhouse. No display PASS for this sentence.\n"+rules.text);ScreenCapture.CaptureScreenshot(Output+"/dino-cda-gap.png");yield return Frame();}Clean(ui);
                }
                foreach(string label in new[]{"first-copy","token-two","recopy","clone-two","finished"}){
                    var v=Read("jace-"+label+"-seat"+seat);ui.Present(null,seat,false,null,null,"");ui.Present(v,seat,false,null,null,"");yield return Frame();Clean(ui);foreach(var card in v.players[0].battlefield.Where(c=>c.planeswalker)){var obj=GameObject.Find("Card_"+card.id);Check(obj!=null&&obj.transform.Find("Visual/DefenseCounter/Value").GetComponent<Text>().text==card.loyalty.ToString(),"Exact independent Jace current loyalty "+label+" "+card.id);}
                }
            }
            var action=Read("jace-first-copy-seat1");var token=action.players[0].battlefield.Single(c=>c.token);int calls=0,last=-1;ui.Present(null,1,false,null,null,"");ui.Present(action,1,true,i=>{calls++;last=i;},null,"");yield return Frame();Clean(ui);Ray(GameObject.Find("Card_"+token.id).GetComponent<Button>());Check(calls==1&&last==ui.OptionIndex("card:"+token.id),"Actual copied recipient exact native card ID selected");
            var menu=Read("jace-token-menu-seat1");calls=0;ui.Present(menu,1,true,i=>{calls++;last=i;},null,"");yield return Frame();Clean(ui);int choice=ui.OptionIndex("choice:0");var panel=GameObject.Find("CardAbilityChoices");Ray(panel.GetComponentsInChildren<Button>().Single(b=>b.name=="Option_"+choice));Check(calls==1&&last==choice,"Copied +1 actual native ability index ray");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=input;GamePreferences.SetLanguage(locale);for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
