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
public static class CopyStateRemainderPresentationValidation {
    const string Base=".tools/copy-state-remainder-1009";
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
    static void RunScope(bool only){if(!Application.isPlaying||Running)throw new Exception("Play required/already running");menuOnly=only;Running=true;checks=0;Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/scope.txt",only?"menu-only":"display-and-menu");File.WriteAllText(Output+"/result.txt","RUNNING");foreach(var p in Directory.GetFiles(Base+"/ui-inputs"))File.Copy(p,Output+"/inputs/"+Path.GetFileName(p));foreach(var p in new[]{"Assets/Scripts/Editor/CopyStateRemainderPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Arena.cs","Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs","Assets/Scripts/UI/ForgeBattlefieldController.ModifiedCharacteristics.cs","Assets/Scripts/UI/ForgeBattlefieldController.LandChoices.cs","Assets/Scripts/UI/ForgeBattlefieldController.CopyDetails.cs","Assets/Scripts/Networking/ForgeWire.cs","Assets/Scripts/UI/ForgeBattlefieldController.TokenArt.cs","Assets/Scripts/UI/CardImageLibrary.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("CopyExceptionValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));}
    static IEnumerator Guard(IEnumerator work){var tasks=new Stack<IEnumerator>();tasks.Push(work);try{while(tasks.Count>0){object next;try{var t=tasks.Peek();if(!t.MoveNext()){tasks.Pop();continue;}next=t.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator child)tasks.Push(child);else yield return next;}File.WriteAllText(Output+"/result.txt","PASS_BOUNDED: "+checks+" checks; exact Secondary,PreparedSpell,Room and merged-copy recipients;current-original return and both seats. Original merged-source display gap retained R23;no complete language/live-network PASS.");}finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}}
    [Serializable] public class Samples{public Sample[] samples;}
    [Serializable] public class Sample{public string caseName,label;public int cardId,sourceId;}
    static IEnumerator Validate(ForgeBattlefieldController ui){
        string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{
            float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;yield return Frame();GamePreferences.SetLanguage("en-US");yield return Frame();until=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||GamePreferences.Language!="en-US")&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language=="en-US","English preferences initialized");
            var samples=JsonUtility.FromJson<Samples>(File.ReadAllText(Output+"/inputs/manifest.json")).samples;
            foreach(int seat in new[]{1,2})foreach(var sample in samples){
                string tag=sample.caseName+"-"+sample.label+"-"+seat;var v=Read(sample.caseName+"-"+sample.label+"-seat"+seat);ui.Present(v,seat,false,null,null,"");yield return Frame();Clean(ui);var card=v.players[0].battlefield.Single(c=>c.id==sample.cardId);
                Check(GameObject.Find("Card_"+card.id)!=null,"Exact copied recipient object "+tag);typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{card});yield return Frame();
                string rules=Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>().text;string name=Root.Find("InspectPanel/Name").GetComponent<Text>().text;var art=Root.Find("InspectPanel/Art").GetComponent<Image>();File.WriteAllText(Output+"/detail-"+tag+".txt",name+"\n"+rules);
                Check(string.IsNullOrEmpty(card.name)?string.IsNullOrEmpty(System.Text.RegularExpressions.Regex.Replace(name,"<[^>]+>","")):name.Contains(GamePreferences.CardFaceName(card.name)),"Exact copied current face name "+tag);
                Check(CardImageLibrary.TryLoadPrintingKey(card.currentArtPrintingKey,card.currentArtBackFace?1:0,out var expected)&&expected!=null&&art.sprite!=null&&art.sprite.texture==expected.texture,"Exact native copied current face image "+tag);
                if(card.creature)Check(System.Text.RegularExpressions.Regex.Replace(Root.Find("InspectPanel/Footer").GetComponent<Text>().text,"<[^>]+>","").Contains(card.power+"/"+card.toughness),"Actual copied/current P/T "+tag);
                if(sample.caseName=="room"&&sample.label=="copied-locked")Check(string.IsNullOrEmpty(System.Text.RegularExpressions.Regex.Replace(rules,"<[^>]+>","")),"Locked copied Room has no printed unlocked ability fallback "+tag);
                if(sample.caseName=="merged")Check(rules.Contains("draw a card")&&rules.Contains("<color=#70BFFF>"),"Inherited lower damage trigger shown blue on copied recipient "+tag);
                if(sample.caseName=="room"&&sample.label=="both-unlocked")Check(rules.Contains("When you unlock this door")&&rules.Contains("draw a card"),"Both independently unlocked copied door traits shown "+tag);
                var button=Root.Find("InspectPanel/OriginalState").GetComponent<Button>();Ray(button);yield return Frame();string before=Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>().text;string originalName=sample.caseName=="room"?"Mirrormade":"Clone";Check(Root.Find("InspectPanel/Name").GetComponent<Text>().text.Contains(GamePreferences.CardFaceName(originalName)),"Exact original physical face name "+tag);if(card.creature)Check(Root.Find("InspectPanel/Footer").GetComponent<Text>().text.Contains("0/0"),"Exact original Clone printed P/T "+tag);Check(!before.Contains("<s ")&&!before.Contains("<color=#70BFFF>"),"Original physical scan/rules neutral "+tag);Ray(button);yield return Frame();Check(Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>().text==rules,"Return exact current copied rules "+tag);
                if(sample.caseName=="merged"&&sample.label=="copied-over"&&seat==1){ScreenCapture.CaptureScreenshot(Output+"/merged-copy.png");yield return Frame();}Clean(ui);
            }

        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=input;GamePreferences.SetLanguage(locale);for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
