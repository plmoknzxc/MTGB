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

// Bounded copy-trait projection and native card-ID replay; R20 remainder stays open.
public static class SplitCopyPresentationValidation {
    const string Base=".tools/split-copy-1009";
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
    public static void RunRemaining()=>RunScope(true);
    public static void RunMenuOnly()=>RunRemaining(); // Historical replay entry.
    static void RunScope(bool only){if(!Application.isPlaying||Running)throw new Exception("Play required/already running");menuOnly=only;Running=true;checks=0;Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/scope.txt",only?"menu-only":"display-and-menu");File.WriteAllText(Output+"/result.txt","RUNNING");foreach(var p in Directory.GetFiles(Base+"/ui-inputs"))File.Copy(p,Output+"/inputs/"+Path.GetFileName(p));foreach(var p in new[]{"Assets/Scripts/Editor/SplitCopyPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Stack.cs","Assets/Scripts/UI/ForgeBattlefieldController.Prepared.cs","Assets/Scripts/UI/ForgeBattlefieldController.Arena.cs","Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs","Assets/Scripts/UI/ForgeBattlefieldController.ModifiedCharacteristics.cs","Assets/Scripts/UI/ForgeBattlefieldController.LandChoices.cs","Assets/Scripts/UI/ForgeBattlefieldController.CopyDetails.cs","Assets/Scripts/Networking/ForgeWire.cs","Assets/Scripts/UI/ForgeBattlefieldController.TokenArt.cs","Assets/Scripts/UI/CardImageLibrary.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("CopyExceptionValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));}
    static IEnumerator Guard(IEnumerator work){var tasks=new Stack<IEnumerator>();tasks.Push(work);try{while(tasks.Count>0){object next;try{var t=tasks.Peek();if(!t.MoveNext()){tasks.Pop();continue;}next=t.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator child)tasks.Push(child);else yield return next;}File.WriteAllText(Output+"/result.txt","PASS_BOUNDED: "+checks+" checks; native split copy chosen-half name/rules/cost,original scan,exact target,stack lifecycle and both seats; no complete language/live-network PASS.");}finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}}
    [Serializable] public class Samples{public Sample[] samples;}
    [Serializable] public class Sample{public string caseName,label;public int cardId,sourceId;}
    static IEnumerator Validate(ForgeBattlefieldController ui){
        string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{
            float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;yield return Frame();GamePreferences.SetLanguage("en-US");yield return Frame();until=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||GamePreferences.Language!="en-US")&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language=="en-US","English preferences initialized");
            foreach(int seat in new[]{1,2})foreach(string label in new[]{"original-ice","copied-ice","copy-resolved","finished"}){
                var v=Read(label+"-seat"+seat);ui.Present(v,seat,false,null,null,"");yield return new WaitForSecondsRealtime(1.4f);yield return Frame();Clean(ui);
                var stack=(IDictionary)typeof(ForgeBattlefieldController).GetField("stackCards",Hidden).GetValue(ui);Check(stack.Count==v.stackItems.Length,"Actual stack count "+label+seat);
                foreach(var entry in v.stackItems){
                    string tag=label+"-"+seat+"-"+entry.id;Check(entry.source.stateName=="RightSplit"&&entry.source.name=="Ice"&&entry.source.manaCost=="{1}{U}","Exact chosen split source "+tag);
                    var visual=stack["id:"+entry.id];Check(visual!=null,"Exact native stack identity visual "+tag);
                    typeof(ForgeBattlefieldController).GetMethod("ShowStackHover",Hidden).Invoke(ui,new[]{visual});yield return Frame();
                    string rules=Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>().text;string name=Root.Find("InspectPanel/Name").GetComponent<Text>().text;
                    File.WriteAllText(Output+"/detail-"+tag+".txt",name+"\n"+rules);Check(name.Contains(GamePreferences.CardFaceName("Ice"))&&!name.Contains(GamePreferences.CardFaceName("Fire")),"Chosen-half hover name "+tag);Check((rules.Contains("Tap target permanent")&&rules.Contains("Draw a card")||rules.Contains("横置目标永久物")&&rules.Contains("抓一张牌"))&&!rules.Contains("2 damage")&&!rules.Contains("2点伤害"),"Exact chosen-half rules excludes uncast Fire "+tag);
                    Check((string)typeof(ForgeBattlefieldController).GetMethod("StackManaCost",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{entry.source})=="{1}{U}","Chosen-half current stack cost "+tag);
                    var art=Root.Find("InspectPanel/Art").GetComponent<Image>();Check(CardImageLibrary.TryLoadPrintingKey(entry.source.printingKey,out var expected)&&art.sprite!=null&&art.sprite.texture==expected.texture,"Original full split scan retained "+tag);
                    var targets=typeof(ForgeBattlefieldController).GetField("inspectedStackTargets",Hidden).GetValue(ui) as ForgeStackTarget[];
                    Check(targets!=null&&targets.Length==1&&targets[0].id==entry.targets[0].id,"Hover retains exact native target "+tag);
                    File.WriteAllText(Output+"/detail-"+tag+".txt",name+"\n"+rules);Clean(ui);
                }
                if(label=="copied-ice"&&seat==1){ScreenCapture.CaptureScreenshot(Output+"/split-copy.png");yield return Frame();}
                if(label=="finished")Check(v.players[0].graveyardCards.Any(c=>c.name=="Fire // Ice"&&c.stateName=="Original")&&stack.Count==0,"Physical split card restored and resolved copy removed "+seat);
            }
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=input;GamePreferences.SetLanguage(locale);for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
