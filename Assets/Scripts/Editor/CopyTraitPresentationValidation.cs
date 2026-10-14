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
public static class CopyTraitPresentationValidation {
    const string Base=".tools/copy-traits-1009";
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
    static void RunScope(bool only){if(!Application.isPlaying||Running)throw new Exception("Play required/already running");menuOnly=only;Running=true;checks=0;Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/scope.txt",only?"menu-only":"display-and-menu");File.WriteAllText(Output+"/result.txt","RUNNING");foreach(var p in Directory.GetFiles(Base+"/ui-inputs"))File.Copy(p,Output+"/inputs/"+Path.GetFileName(p));foreach(var p in new[]{"Assets/Scripts/Editor/CopyTraitPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Arena.cs","Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs","Assets/Scripts/UI/ForgeBattlefieldController.ModifiedCharacteristics.cs","Assets/Scripts/UI/ForgeBattlefieldController.LandChoices.cs","Assets/Scripts/UI/ForgeBattlefieldController.CopyDetails.cs","Assets/Scripts/Networking/ForgeWire.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("CopyExceptionValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));}
    static IEnumerator Guard(IEnumerator work){var tasks=new Stack<IEnumerator>();tasks.Push(work);try{while(tasks.Count>0){object next;try{var t=tasks.Peek();if(!t.MoveNext()){tasks.Pop();continue;}next=t.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator child)tasks.Push(child);else yield return next;}File.WriteAllText(Output+"/result.txt","PASS_BOUNDED: "+checks+" checks; current copied type,added/removed intrinsic rules,keyword exception baselines,original/return rays,three languages and exact copied recipient and same-name opposing target card rays. Only copy trait paragraphs sampled in three languages; existing keyword/state headings stay R47. No all-mechanism/live-network PASS.");}finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui){
        string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{
            float initialUntil=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<initialUntil)yield return null;yield return Frame();
            foreach(string lang in menuOnly?new[]{"en-US","ja-JP"}:new[]{"zh-CN","en-US","ja-JP"}){
                GamePreferences.SetLanguage(lang);yield return Frame();float until=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||GamePreferences.Language!=lang)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language==lang,"Preferences ready "+lang);
                foreach(int seat in lang=="zh-CN"?new[]{1,2}:new[]{1})
                foreach(string c in lang=="zh-CN"?new[]{"dino","evil","dollhouse","inchblade","flesh","vesuvan"}:new[]{"dino","evil","inchblade","jace"}){
                    var v=Read(c+"-first-copy-seat"+seat);ui.Present(null,seat,false,null,null,"");ui.Present(v,seat,false,null,null,"");yield return Frame();Clean(ui);
                    var card=v.players[0].battlefield.First(x=>x.copy);Check(GameObject.Find("Card_"+card.id)!=null,"Exact native copied card "+c+seat+lang);
                    typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{card});yield return Frame();
                    string rules=Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>().text;
                    string type=Root.Find("InspectPanel/TypeLine").GetComponent<Text>().text;
                    File.WriteAllText(Output+"/detail-"+c+"-"+seat+"-"+lang+".txt",type+"\n"+rules);
                    if(c=="dino"){
                        Check(type.Contains(lang=="zh-CN"?"恐龙":lang=="en-US"?"Dinosaur":"恐竜")&&!type.Contains("Horror")&&!type.Contains("惊惧兽"),"Current replaced Dinosaur type "+seat+lang);
                        Check(System.Text.RegularExpressions.Regex.Matches(rules,"<s ").Count==1&&rules.Contains("#565C65"),"Removed native PT CDA exactly one gray strike "+seat+lang);
                        Check(rules.Contains("#70BFFF")&&rules.Contains("践踏"),"Actual added keyword remains blue (existing StatusName label tracked R47) "+seat+lang);
                    }
                    if(c=="jace")Check(type.Contains(lang=="en-US"?"Planeswalker":"プレインズウォーカー")&&!type.Contains("Legendary")&&!type.Contains("伝説の"),"Copied nonlegendary planeswalker preserves localized printed subtype "+lang);
                    if(c=="evil")Check(rules.Contains("#70BFFF")&&rules.Contains(lang=="zh-CN"?"同名":lang=="en-US"?"same name":"同じ名前"),"Actual retained destroy ability localized blue "+seat+lang);
                    if(c=="dollhouse")Check(rules.Contains("<color=#70BFFF>你每操控一个组构体")&&card.power==2,"Added static blue and actual2/2,printed0/0 baseline "+seat);
                    if(c=="inchblade")Check(System.Text.RegularExpressions.Regex.Matches(rules,"<s ").Count==1&&!card.copyCurrentAbilityText.Contains("becomes attached"),"Only removed attached trigger gray,static retained "+seat+lang);
                    if(c=="flesh")Check(!card.hasCopyAbilityChanges&&card.originalKeywordDetails.Contains("Vanishing:3")&&card.currentKeywordDetails.Count(x=>x=="Vanishing:3")==1,"Existing Vanishing not added or duplicated "+seat);
                    if(c=="vesuvan")Check(rules.Contains("#70BFFF")&&rules.Contains("维持"),"Actual retained own upkeep trigger blue "+seat);
                    var original=Root.Find("InspectPanel/OriginalState").GetComponent<Button>();Ray(original);yield return Frame();
                    string originalRules=Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>().text;
                    Check(!originalRules.Contains("<s ")&&!originalRules.Contains("<color=#70BFFF>"),"Original scan/rules not delta colored "+c+seat+lang);
                    Ray(original);yield return Frame();Check(Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>().text==rules,"Return current exact rules "+c+seat+lang);
                    if(lang=="zh-CN"&&seat==1&&c=="dino"){ScreenCapture.CaptureScreenshot(Output+"/dino-current-fixed.png");yield return Frame();}Clean(ui);
                }
            }
            GamePreferences.SetLanguage("zh-CN");yield return Frame();
            foreach(string name in new[]{"dollhouse-recopy","dollhouse-count-changed","vesuvan-upkeep-recopy","vesuvan-finished","flesh-second-copy"}){
                var v=Read(name+"-seat1");ui.Present(v,1,false,null,null,"");yield return Frame();Clean(ui);var card=v.players[0].battlefield.First(x=>x.copy&&(name!="flesh-second-copy"||x.name=="Grizzly Bears"));
                typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{card});yield return Frame();
                string rules=Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>().text;
                Check(rules.Contains("#70BFFF"),"Actual changed intrinsic/keyword survives refresh "+name);
                if(name=="flesh-second-copy")Check(card.originalKeywordDetails.Length==0&&card.currentKeywordDetails.SequenceEqual(new[]{"Vanishing:3"}),"IfNew adds only missing Vanishing,blue comparison uses copied printed Bear");
                if(name.StartsWith("dollhouse"))Check(Root.Find("InspectPanel/Footer").GetComponent<Text>().text.Contains(card.power.ToString()),"Same-copy count update actual footer "+name);
                if(name.StartsWith("vesuvan"))Check(Root.Find("InspectPanel/Name").GetComponent<Text>().text.Contains(GamePreferences.CardFaceName(card.name)),"Same ID current copied name refreshed "+name);
                Clean(ui);
            }
            var action=Read("evil-haste-seat1");var recipient=action.players[0].battlefield.Single(c=>c.token);int calls=0,last=-1;
            ui.Present(null,1,false,null,null,"");ui.Present(action,1,true,i=>{calls++;last=i;},null,"");yield return Frame();Clean(ui);
            Ray(GameObject.Find("Card_"+recipient.id).GetComponent<Button>());Check(calls==1&&last==ui.OptionIndex("card:"+recipient.id),"Actual copied retained destroy ability exact recipient native ID ray");
            var target=Read("evil-destroy-target-seat1");var victim=target.players[1].battlefield.Single(c=>c.name=="Grizzly Bears");calls=0;
            ui.Present(target,1,true,i=>{calls++;last=i;},null,"");yield return Frame();Clean(ui);Ray(GameObject.Find("Card_"+victim.id).GetComponent<Button>());Check(calls==1&&last==ui.OptionIndex("card:"+victim.id),"Actual retained ability correct same-name opposing target native ID ray");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=input;GamePreferences.SetLanguage(locale);for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
