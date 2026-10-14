#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Real native requests and results. Layout/grouping and spectator checks are adapters.
public static class StateBranchPresentationValidation
{
    const string Base=".tools/state-branches-1006/ui",Native=".tools/state-branches-1006/native";
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    public static string Output {get;private set;}
    public static bool Running {get;private set;}
    public static Coroutine Handle {get;private set;}
    [Serializable] class Meta {public string folder,status,runtimeHash;}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Hash(string file){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant();}
    static string Folder(string name){var p=Native+"/"+name+"-result.json";var m=JsonUtility.FromJson<Meta>(File.ReadAllText(p));Check(m.status=="PASS"&&m.runtimeHash==JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json")).runtimeHash,"Incomplete/stale native "+name);File.Copy(p,Output+"/inputs/"+name+"-meta.json",true);return m.folder;}
    static ForgeSeatView Read(string name,string label){string p=Path.Combine(Folder(name),label+".json");File.Copy(p,Output+"/inputs/"+name+"-"+label+".json",true);File.AppendAllText(Output+"/input-hashes.tsv",name+"/"+label+"\t"+Hash(p)+"\n");return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p));}
    static ForgeSeatView Selected(string name,string kind){string folder=Folder(name);foreach(string p in Directory.GetFiles(folder,"step-*.json").OrderBy(p=>int.Parse(Path.GetFileNameWithoutExtension(p).Substring(5)))){var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p));if(v.request.kind==kind&&v.request.selectedCount==1)return Read(name,Path.GetFileNameWithoutExtension(p));}throw new Exception("No native selected snapshot "+name);}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Clean(){foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}}
    static void Click(Button button){Check(button!=null&&button.interactable,"Missing/disabled action");var rect=(RectTransform)button.transform;foreach(float y in new[]{.5f,.25f,.75f})foreach(float x in new[]{.5f,.12f,.88f}){var local=new Vector2(Mathf.Lerp(rect.rect.xMin,rect.rect.xMax,x),Mathf.Lerp(rect.rect.yMin,rect.rect.yMax,y));var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(local)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);if(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button){ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);return;}}throw new Exception("Ray blocked "+button.name);}
    static Button CardButton(int id,bool hand){if(hand)return Root.GetComponentsInChildren<Button>().Single(b=>b.name=="Card_"+id&&b.transform.parent.name=="PlayerHandRoot");var tile=Root.GetComponentsInChildren<Transform>().Single(t=>t.name=="ChoiceCard_"+id);return tile.GetComponentsInChildren<Button>().Single();}
    static void Inspect(ForgeBattlefieldController ui,ForgeCardView card){typeof(ForgeBattlefieldController).GetField("inspectOriginalState",Hidden).SetValue(ui,false);typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{card});}
    public static void Run(){Check(Application.isPlaying&&!Running,"Play required/already running");Running=true;Output=Base+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");File.WriteAllText(Base+"/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/producer-hashes.tsv",string.Join("\n",new[]{"Assets/Scripts/Editor/StateBranchPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs","Assets/Scripts/UI/ForgeBattlefieldController.ModifiedCharacteristics.cs","Assets/Scripts/UI/ForgeBattlefieldController.CardSelection.cs","Assets/Scripts/UI/ForgeBattlefieldController.Arena.cs","Assets/Scripts/Networking/ForgeWire.cs"}.Select(p=>p+"\t"+Hash(p))));var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("StateBranchValidation").AddComponent<ForgeBattlefieldController>();Handle=ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var e=Validate(ui);try{while(true){object c;try{if(!e.MoveNext())break;c=e.Current;}catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}yield return c;}File.WriteAllText(Output+"/result.txt","PASS: actual native Recruit hand/discard and Blight creature-picker requests clicked in three locales; second-seat Recruit and Blight death sampled in zh-CN; native selected confirmation, exact ID/index, stale/duplicate/nonacting guards, View/Return font stability, native next-priority cleanup and forced/empty Blight no-picker. Intensify real 3->4->5 and self 1->2 snapshots inspected in three locales with changed values blue, original toggle and same-ID refresh. Equal/different intensity grouping and face-up/face-down spectator filtering are adapters. Native effect assertions and real networking are separate.");}finally{(e as IDisposable)?.Dispose();Running=false;Handle=null;}}
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool moduleEnabled=module!=null&&module.enabled;if(module!=null)module.enabled=false;var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}){
            GamePreferences.SetLanguage(lang);float deadline=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready,"Language unavailable");yield return Frame();
            foreach(string name in lang=="zh-CN"?new[]{"recruit-nonland","recruit-seat-two","blight-select","blight-death"}:new[]{"recruit-nonland","blight-select"}){
                File.AppendAllText(Output+"/steps.log",lang+" "+name+"\n");bool hand=name.StartsWith("recruit");var v=Read(name,"choice");var selected=Selected(name,v.request.kind);int actor=v.request.seat,calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};ui.Present(null,actor,false,null,null,"");ui.Present(v,actor,true,submit,null,"");yield return Frame();Clean();
                Check(ui.OptionIndex("ok")<0&&ui.OptionIndex("cancel")<0,"Unexpected mandatory selection gate");
                var chosen=selected.request.options.First(o=>o.selected&&o.card!=null);int expected=ui.OptionIndex(chosen.id);Check(expected>=0,"Selected identity missing");
                if(hand){var prompt=Root.Find("HandSelectionPrompt").GetComponent<Text>();Check(prompt.text.Contains(lang=="zh-CN"?"弃掉":lang=="ja-JP"?"捨てる":"discard"),"Discard instruction wrong");}
                else{var panel=GameObject.Find("CardSelection");var instruction=panel.transform.Find("Instruction").GetComponent<Text>();int font=instruction.fontSize;Check(instruction.text==(lang=="zh-CN"?"选择一张牌。":lang=="ja-JP"?"カード１枚を選択。":"Choose a card."),"Creature instruction not localized");ui.ViewBattlefield();yield return Frame();Check(!ui.TrySubmitPresentedOption(expected,v.request.requestId),"Hidden picker submits");ui.RestoreBattlefieldWindows();yield return Frame();Check(instruction.fontSize==font,"Restore changed font");}
                Check(!ui.TrySubmitPresentedOption(expected,v.request.requestId-1),"Stale accepted");Click(CardButton(chosen.card.id,hand));Check(calls==1&&last==expected,"Wrong card ID/index");Check(!ui.TrySubmitPresentedOption(expected,v.request.requestId),"Duplicate accepted");
                ui.Present(selected,actor,true,submit,null,"");yield return Frame();Clean();int confirm=ui.OptionIndex("ok");Check(confirm>=0,"Native confirm absent");var action=Root.GetComponentsInChildren<Button>().Single(b=>b.name=="SelectionAction_"+confirm||b.name=="Option_"+confirm||b.name=="HandAction_"+confirm);Click(action);Check(calls==2&&last==confirm,"Native confirmation did not submit");
                ui.Present(v,actor,false,submit,null,"");yield return Frame();Check(!ui.TrySubmitPresentedOption(expected,v.request.requestId),"Nonacting viewer submitted");ui.Present(v,3-actor,false,submit,null,"");yield return Frame();Check(!ui.TrySubmitPresentedOption(expected,v.request.requestId),"Other seat submitted");
                ui.Present(Read(name,"resolved"),actor,true,submit,null,"");yield return Frame();Check(!Root.GetComponentsInChildren<Transform>().Any(t=>t.name=="ChoiceCard_"+chosen.card.id),"Picker survives next priority");
            }
            int stableId=Read("intensity-all","before").players[0].hand.First(c=>c.name=="Static Discharge").id;
            ui.Present(null,1,false,null,null,"");
            foreach(string label in new[]{"before","intensified-1","resolved"}){
                var v=Read("intensity-all",label);ui.Present(v,1,false,null,null,"");yield return Frame();Clean();int amount=label=="before"?3:label=="intensified-1"?4:5;var c=(v.players[0].hand??Array.Empty<ForgeCardView>()).Concat(v.players[0].graveyardCards??Array.Empty<ForgeCardView>()).Single(x=>x.id==stableId);Check(c.intensity==amount,"Same native ID intensity refresh missing");Inspect(ui,c);yield return Frame();var rules=Root.Find("InspectPanel/Rules").GetComponentInChildren<TMPro.TextMeshProUGUI>();Check(rules.text.Contains((lang=="zh-CN"?"强度：":lang=="ja-JP"?"強度：":"Intensity: ")+(amount==3?"3":"<color=#70BFFF>"+amount+"</color>")),"Intensity absent/incorrect color");rules.ForceMeshUpdate();if(amount!=3)Check(rules.textInfo.characterInfo.Take(rules.textInfo.characterCount).Any(x=>x.character==('0'+amount)&&x.isVisible&&x.color.r==112&&x.color.g==191&&x.color.b==255),"Changed intensity not drawn blue");if(lang=="zh-CN"&&label=="resolved"){ScreenCapture.CaptureScreenshot(Output+"/intensity-details.png");yield return Frame();}Click(Root.Find("InspectPanel/OriginalState").GetComponent<Button>());yield return Frame();Check(rules.text.Contains((lang=="zh-CN"?"强度：":lang=="ja-JP"?"強度：":"Intensity: ")+"3")&&!rules.text.Contains("<color=#70BFFF>"+amount+"</color>"),"Original intensity wrong");
            }
            var self=Read("intensity-self","resolved");var aura=self.players[0].battlefield.Single(c=>c.name=="Runaway Growth");ui.Present(self,1,false,null,null,"");yield return Frame();Clean();Inspect(ui,aura);yield return Frame();Check(Root.Find("InspectPanel/Rules").GetComponentInChildren<TMPro.TextMeshProUGUI>().text.Contains("<color=#70BFFF>2</color>"),"Self intensity missing");
            if(lang=="zh-CN"){
                var same=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(aura));same.id++;Check(ForgeBattlefieldController.ArenaGroupKey(aura)==ForgeBattlefieldController.ArenaGroupKey(same),"Equal intensity cannot group");same.intensity++;Check(ForgeBattlefieldController.ArenaGroupKey(aura)!=ForgeBattlefieldController.ArenaGroupKey(same),"Different intensity groups");
                var method=typeof(OnlineSpectatorView).GetMethod("PublicCard",BindingFlags.Static|BindingFlags.NonPublic);var publicCard=(ForgeCardView)method.Invoke(null,new object[]{aura});Check(publicCard.hasIntensity&&publicCard.intensity==2&&publicCard.originalIntensity==1,"Public intensity lost");same.faceDown=true;publicCard=(ForgeCardView)method.Invoke(null,new object[]{same});Check(!publicCard.hasIntensity&&publicCard.intensity==0&&publicCard.originalIntensity==0,"Face-down intensity leaked");
                foreach(string name in new[]{"recruit-no-draw","blight-forced","blight-ineligible"}){ui.Present(Read(name,"resolved"),1,true,_=>{},null,"");yield return Frame();Check(GameObject.Find("CardSelection")==null,"Forced/empty branch invents picker");}
            }
        }}finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(locale);if(module!=null)module.enabled=moduleEnabled;for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
