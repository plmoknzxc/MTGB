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
public static class CopyIdentityPresentationValidation {
    const string Base=".tools/copy-name-duration-1009";
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
    static void RunScope(bool only){if(!Application.isPlaying||Running)throw new Exception("Play required/already running");menuOnly=only;Running=true;checks=0;Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/scope.txt",only?"menu-only":"display-and-menu");File.WriteAllText(Output+"/result.txt","RUNNING");foreach(var p in Directory.GetFiles(Base+"/ui-inputs"))File.Copy(p,Output+"/inputs/"+Path.GetFileName(p));foreach(var p in new[]{"Assets/Scripts/Editor/CopyIdentityPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Arena.cs","Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs","Assets/Scripts/UI/ForgeBattlefieldController.ModifiedCharacteristics.cs","Assets/Scripts/UI/ForgeBattlefieldController.LandChoices.cs","Assets/Scripts/UI/ForgeBattlefieldController.CopyDetails.cs","Assets/Scripts/Networking/ForgeWire.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("CopyExceptionValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));}
    static IEnumerator Guard(IEnumerator work){var tasks=new Stack<IEnumerator>();tasks.Push(work);try{while(tasks.Count>0){object next;try{var t=tasks.Peek();if(!t.MoveNext()){tasks.Pop();continue;}next=t.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator child)tasks.Push(child);else yield return next;}File.WriteAllText(Output+"/result.txt","PASS_BOUNDED: "+checks+" checks; actual name/duration/fulltext native snapshots,original-return rays,both seats and same-ID refresh. English trait semantics sampled; Chinese localization remains R47. No live-network PASS.");}finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui){
        string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{
            float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;yield return Frame();
            GamePreferences.SetLanguage("en-US");yield return Frame();until=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||GamePreferences.Language!="en-US")&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language=="en-US","English preferences initialized");
            foreach(int seat in new[]{1,2})foreach(string c in new[]{"sakashima","volrath","fulltext"}){
                int id=c=="volrath"?124:c=="sakashima"?126:121;
                foreach(string label in c=="sakashima"?new[]{"temporary-copy","finished"}:c=="volrath"?new[]{"first-copy","second-copy","finished"}:new[]{"first-copy","bird","finished"}){
                    var v=Read(c+"-"+label+"-seat"+seat);ui.Present(v,seat,false,null,null,"");yield return Frame();Clean(ui);
                    var card=v.players[0].battlefield.Single(x=>x.id==id);Check(GameObject.Find("Card_"+id)!=null,"Same exact native card object "+c+label+seat);
                    typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{card});yield return Frame();
                    string rules=Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>().text;string name=Root.Find("InspectPanel/Name").GetComponent<Text>().text;
                    File.WriteAllText(Output+"/detail-"+c+"-"+label+"-"+seat+".txt",name+"\n"+Root.Find("InspectPanel/TypeLine").GetComponent<Text>().text+"\n"+rules);
                    Check(name.Contains(GamePreferences.CardFaceName(card.name)),"Exact current native name refresh "+c+label+seat);
                    if(c=="fulltext"){
                        Check(card.hasAbilityChanges==(label!="finished"),"ByText changed trait flag clears on noncreature "+label+seat);
                        if(label=="first-copy")Check(rules.Contains("<color=#70BFFF>")&&rules.Contains("deals 1 damage")&&rules.Contains("Discard a card"),"Actual inherited ping blue and retained discard current "+seat);
                        if(label=="bird")Check(rules.Contains("Add one mana of any color")&&!rules.Contains("deals 1 damage"),"Same-ID Bird replaces prior ping paragraph "+seat);
                        if(label=="finished")Check(!rules.Contains("deals 1 damage")&&!rules.Contains("Add one mana of any color")&&!rules.Contains("<s "),"Full-text restoration removes stale ability fields "+seat);
                        Check(Root.Find("InspectPanel/CostRow").gameObject.activeSelf==(label!="finished"),"Changed cost strip current/restored visibility "+label+seat);
                    }
                    if(c=="sakashima")Check((label=="finished"?!rules.Contains("next end step"):rules.Contains("next end step")),"Inherited return removed at exact duration "+label+seat);
                    if(c=="volrath")Check(card.power==7&&card.toughness==5&&rules.Contains("7/5"),"Retained clone sets seven/five throughout exact host lifecycle "+label+seat);
                    var button=Root.Find("InspectPanel/OriginalState").GetComponent<Button>();Ray(button);yield return Frame();string before=Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>().text;Check(!before.Contains("<s ")&&!before.Contains("<color=#70BFFF>"),"Original baseline neutral "+c+label+seat);Ray(button);yield return Frame();Check(Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>().text==rules,"Return current exact rules "+c+label+seat);
                    if(c=="fulltext"&&label=="first-copy"&&seat==1){ScreenCapture.CaptureScreenshot(Output+"/fulltext-current.png");yield return Frame();}Clean(ui);
                }
            }
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=input;GamePreferences.SetLanguage(locale);for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
