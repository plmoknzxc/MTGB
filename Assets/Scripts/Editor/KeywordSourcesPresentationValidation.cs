#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class KeywordSourcesPresentationValidation
{
    const string Base=".tools/keyword-sources-1009";
    static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    public static bool Running{get;private set;}
    public static string Output{get;private set;}
    static int checks;
    static bool markerOnly;
    static Transform Root=>GameObject.Find("Canvas").transform;
    [Serializable] public class Manifest{public Sample[] samples;public Choice[] choices;}
    [Serializable] public class Sample{public string stem,checkKind;public int cardId,ownerSeat;}
    [Serializable] public class Choice{public string stem,optionId,kind;public int seat;}
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;File.AppendAllText(Output+"/checks.txt",label+"\n");}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Output+"/inputs/"+name+".json"));
    static string Plain(string text)=>Regex.Replace(text??"","<[^>]+>","");
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.18f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Clean(ForgeBattlefieldController ui){foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}var f=typeof(ForgeBattlefieldController).GetField("inspectHoverRevision",Hidden);f.SetValue(ui,(int)f.GetValue(ui)+1);Root.Find("InspectPanel").gameObject.SetActive(false);}
    static void Ray(Button b){var rect=(RectTransform)b.transform;var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(b.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Exact topmost ray "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);}
    static void Reveal(Button b){var scroll=b.GetComponentInParent<ScrollRect>();if(scroll==null)return;Canvas.ForceUpdateCanvases();var p=scroll.viewport.InverseTransformPoint(b.transform.TransformPoint(((RectTransform)b.transform).rect.center));float half=((RectTransform)b.transform).rect.height*.5f;float y=Mathf.Clamp(p.y,scroll.viewport.rect.yMin+half+4,scroll.viewport.rect.yMax-half-4);scroll.content.anchoredPosition+=new Vector2(0,y-p.y);Canvas.ForceUpdateCanvases();}
    public static void Run()=>RunScope(false);
    public static void RunMarkerOnly()=>RunScope(true);
    static void RunScope(bool only){if(!Application.isPlaying||Running)throw new Exception("Play required/already running");markerOnly=only;Running=true;checks=0;Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/scope.txt",only?"player-marker-only":"all-bounded");File.WriteAllText(Output+"/result.txt","RUNNING");foreach(var p in Directory.GetFiles(Base+"/ui-inputs"))File.Copy(p,Output+"/inputs/"+Path.GetFileName(p));foreach(var p in new[]{"Assets/Scripts/Editor/KeywordSourcesPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Arena.cs","Assets/Scripts/UI/ForgeBattlefieldController.Emblems.cs","Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs","Assets/Scripts/UI/ForgeBattlefieldController.ModifiedCharacteristics.cs","Assets/Scripts/UI/PlayerProtectionIcon.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("KeywordSourceValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));}
    static IEnumerator Guard(IEnumerator work){var tasks=new Stack<IEnumerator>();tasks.Push(work);try{while(tasks.Count>0){object next;try{var t=tasks.Peek();if(!t.MoveNext()){tasks.Pop();continue;}next=t.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator child)tasks.Push(child);else yield return next;}File.WriteAllText(Output+"/result.txt","PASS_BOUNDED: "+checks+" executions; "+(markerOnly?"player marker glyph/detail/expiry/next ordinary card and three locales/both seats":"exact current/original keyword sources, removal/expiry/group, three real chooser requests, new player protection marker three locales/both seats")+". No whole-language/scroll/live-network PASS.");}finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui){
        string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{
            float deadline=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<deadline)yield return null;yield return Frame();GamePreferences.SetLanguage("zh-CN");yield return Frame();Check(GamePreferences.Ready&&GamePreferences.Language=="zh-CN","Chinese preference initialized");
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Output+"/inputs/manifest.json"));
            foreach(int seat in new[]{1,2})foreach(var sample in manifest.samples.Where(s=>!markerOnly||s.checkKind.StartsWith("player"))){
                string tag=sample.stem+"-"+seat;var v=Read(sample.stem+"-seat"+seat);ui.Present(v,seat,false,null,null,"");yield return Frame();Clean(ui);var p=v.players.Single(p=>p.seat==sample.ownerSeat);var card=p.battlefield.Concat(p.effectMarkers).Single(c=>c.id==sample.cardId);
                var visual=GameObject.Find("Card_"+card.id);Check(visual!=null,"Exact public card/marker "+tag);
                if(sample.checkKind=="flying"){
                    bool flying=(card.currentKeywordDetails??Array.Empty<string>()).Contains("Flying");bool merged=!flying;
                    Check((GameObject.Find("Card_125")==null)==merged,"Identical Bears split/merge by native state "+tag);
                }
                if(sample.checkKind=="player-expired")Check(GameObject.Find("Card_-3000000")==null,"Player protection marker removed at native expiry "+tag);
                if(sample.checkKind=="player")Check(visual.GetComponentInChildren<PlayerProtectionIcon>()!=null&&!visual.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("缺失卡图")),"Actual shield glyph without missing-art label "+tag);
                typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{card});yield return Frame();string rules=Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>().text;File.WriteAllText(Output+"/detail-"+tag+".txt",rules);
                if(sample.checkKind=="player-expired")Check(!Root.Find("InspectPanel/Art/PlayerProtection").gameObject.activeSelf,"Shield detail cleared for next ordinary card "+tag);
                if(sample.checkKind=="player"){Check(!p.effectMarkers.Any(c=>c.name=="Keyword Effects"),"No internal duplicate keyword placeholder "+tag);Check(!Root.Find("InspectPanel/Art/MissingArt").gameObject.activeSelf&&Root.Find("InspectPanel/Art/PlayerProtection").gameObject.activeSelf,"Actual detail shield replaces missing-card placeholder "+tag);}
                var before=card.originalKeywordDetails??Array.Empty<string>();var after=card.currentKeywordDetails??Array.Empty<string>();
                if(after.Except(before).Any())Check(rules.Contains("<color=#70BFFF>"),"Granted current keyword blue "+tag);
                if(before.Except(after).Any())Check(rules.Contains("<s ")||rules.Contains("<s>"),"Removed intrinsic keyword struck "+tag);
                if(sample.checkKind=="flying"){
                    bool flying=after.Contains("Flying");Check(Regex.Matches(Plain(rules),"飞行").Count==(flying?1:0),"One effective Flying entry through 0/1/2/1/0 sources "+tag);
                }
                if(!card.effectMarker){
                    var button=Root.Find("InspectPanel/OriginalState").GetComponent<Button>();Ray(button);yield return Frame();string original=Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>().text;
                    Check(!original.Contains("<color=#70BFFF>")&&!original.Contains("<s "),"Original printed keyword state neutral "+tag);Ray(button);yield return Frame();Check(Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>().text==rules,"Return exact current source state "+tag);
                }
                if(sample.checkKind=="player"&&seat==1){ScreenCapture.CaptureScreenshot(Output+"/player-protection.png");yield return Frame();}
                if(sample.stem.Contains("debuff-colors-after-4")&&card.name=="Iridescent Angel"&&seat==1){ScreenCapture.CaptureScreenshot(Output+"/protection-colors.png");yield return Frame();}Clean(ui);
            }
            foreach(var choice in manifest.choices.Where(c=>!markerOnly)){
                var v=Read(choice.stem+"-seat"+choice.seat);int calls=0,last=-1;ui.Present(v,choice.seat,true,i=>{calls++;last=i;},null,"");yield return Frame();Clean(ui);int index=Array.FindIndex(v.request.options,o=>o.id==choice.optionId);var menu=GameObject.Find("CardAbilityChoices");Check(menu!=null&&menu.activeInHierarchy,"Real protection chooser visible "+choice.stem);
                var b=menu.GetComponentsInChildren<Button>().Single(b=>b.name=="Option_"+index);Reveal(b);yield return Frame();Ray(b);Check(calls==1&&last==index,"Exact original native chooser option index "+choice.stem);b.onClick.Invoke();Check(calls==1,"Duplicate protected "+choice.stem);
                ui.Present(v,choice.seat,false,i=>calls++,null,"");yield return Frame();Check(menu.GetComponentsInChildren<Button>().All(b=>!b.interactable),"Read-only chooser disabled "+choice.stem);ui.Choose(index,v.request.requestId);Check(calls==1,"Read-only submit guarded "+choice.stem);
                ui.Present(v,choice.seat,true,i=>calls++,null,"");yield return Frame();ui.Choose(index,v.request.requestId-1);Check(calls==1,"Stale chooser response rejected "+choice.stem);
            }
            var marker=manifest.samples.Single(s=>s.checkKind=="player");foreach(string language in new[]{"zh-CN","en-US","ja-JP"})foreach(int seat in new[]{1,2}){
                GamePreferences.SetLanguage(language);yield return Frame();var v=Read(marker.stem+"-seat"+seat);ui.Present(v,seat,false,null,null,"");yield return Frame();Clean(ui);var card=v.players[0].effectMarkers.Single(c=>c.markerKind=="player-protection");typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{card});yield return Frame();string rules=Plain(Root.Find("InspectPanel/Rules").GetComponentInChildren<TextMeshProUGUI>().text);
                Check(rules==(language=="zh-CN"?"你具有反黑保护。":language=="en-US"?"You have protection from black.":"あなたはプロテクション（黒）を持つ。"),"Exact new public marker localized "+language+seat);Check(Root.Find("InspectPanel/TypeLine").GetComponent<Text>().text==(language=="zh-CN"?"玩家状态":language=="en-US"?"Player state":"プレイヤーの状態"),"Actual marker state type "+language+seat);
            }
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=input;GamePreferences.SetLanguage(locale);for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
