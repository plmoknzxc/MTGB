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

public static class SagaChapterPresentationValidation
{
    public static bool Running {get;private set;}
    public static string Output {get;private set;}
    const string Base=".tools/saga-chapters-1006/ui";
    [Serializable] sealed class Meta {public string folder,status,runtimeHash;}
    sealed class Host:MonoBehaviour{}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    public static void Run(){Check(Application.isPlaying&&!Running,"Play required");Running=true;Output=Base+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");File.WriteAllText(Base+"/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/producer-hashes.tsv",string.Join("\n",new[]{"Assets/Scripts/Editor/SagaChapterPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.CardSelection.cs","Assets/Scripts/UI/ForgeBattlefieldController.HandActions.cs"}.Select(p=>p+"\t"+Hash(p)))+"\n");var go=new GameObject("SagaChapterValidation");go.AddComponent<Host>().StartCoroutine(Guard(Exercise(),go));}
    static IEnumerator Guard(IEnumerator e,GameObject go){Exception error=null;while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){error=ex;break;}if(!more)break;if(current is IEnumerator nested){yield return Nested(nested,ex=>error=ex);if(error!=null)break;}else yield return current;}try{(e as IDisposable)?.Dispose();}catch(Exception ex){error??=ex;}File.WriteAllText(Output+"/result.txt",error==null?"PASS: actual Fable chapterII hand-choice requests from both seats and zero/two discards: exact card and confirm option IDs via raycast, native selection counts, 0–2 localized instruction and font stability in three locales; same-recipient disabled submission and duplicate guard. Three actual chapter outcomes proved separately by native driver; no opponent-private-view/network claim.":"FAIL\n"+error);Running=false;UnityEngine.Object.Destroy(go);}
    static IEnumerator Nested(IEnumerator e,Action<Exception> fail){while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){fail(ex);break;}if(!more)break;if(current is IEnumerator child)yield return Nested(child,fail);else yield return current;}}
    static void Click(Button b){var rect=(RectTransform)b.transform;var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);}
    static IEnumerator Exercise(){
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("SagaChapterBattlefield").AddComponent<ForgeBattlefieldController>();bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool active=module!=null&&module.enabled;if(module!=null)module.enabled=false;string language=GamePreferences.Language;
        try{
            foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}){
                int revision=GamePreferences.Revision;bool change=GamePreferences.Language!=lang;GamePreferences.SetLanguage(lang);float until=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||change&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale unavailable");yield return Frame();
                foreach(string test in new[]{"chapter-draw","chapter-decline","chapter-seat2"}){
                    var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/saga-chapters-1006/native/"+test+"-result.json"));Check(meta.status=="PASS"&&meta.runtimeHash==JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json")).runtimeHash,"Native proof stale");var paths=Directory.GetFiles(meta.folder,"step-*.json").Where(p=>!p.EndsWith("-other.json")).OrderBy(p=>int.Parse(Path.GetFileNameWithoutExtension(p).Split('-').Last())).Where(p=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p)).request.kind=="InputSelectCardsFromList").ToArray();Check(paths.Length==(test=="chapter-decline"?1:3),"Native zero/partial/full states");
                    foreach(string path in paths)foreach(bool acting in new[]{true,false}){
                        string copied=Output+"/inputs/"+test+"-"+Path.GetFileName(path);if(!File.Exists(copied)){File.Copy(path,copied);File.AppendAllText(Output+"/input-hashes.tsv",path+"\t"+Hash(path)+"\n");}
                        var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));var request=view.request;Check(request.minCount==0&&request.maxCount==2,"Native optional limit");int calls=0,last=-1;ui.Present(null,request.seat,false,null,null,"");ui.Present(view,request.seat,acting,i=>{calls++;last=i;},null,"");yield return Frame();foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}
                        var prompt=Root.Find("HandSelectionPrompt").GetComponent<Text>();Check(prompt.gameObject.activeSelf&&prompt.text==(lang=="zh-CN"?"可弃至多 2 张牌；确认以继续。":lang=="en-US"?"You may discard up to 2 cards; confirm to continue.":"最大 2 枚まで捨ててもよい。選択後に確認。"),"Optional discard instruction");Check(!prompt.resizeTextForBestFit,"Prompt auto shrink");
                        var dock=Root.Find("CornerCluster/ControlRow/HandChoiceActions");Check(dock!=null&&dock.gameObject.activeInHierarchy,"Hand confirm dock absent");int ok=ui.OptionIndex("ok");var confirm=dock.Find("HandAction_"+ok).GetComponent<Button>();Check(confirm.interactable==acting&&!request.options[ok].disabled,"Zero/two confirmation gate");
                        var hand=view.players[request.seat-1].hand;foreach(var o in request.options.Where(o=>o.id.StartsWith("card:"))){var button=Root.GetComponentsInChildren<Button>().Single(b=>b.name=="Card_"+o.card.id);Check(!acting||o.disabled||button.interactable,"Legal hand card cannot be clicked");Check(hand.Any(c=>c.id==o.card.id),"Exact hand source ID");}
                        float font=prompt.fontSize;ui.ViewBattlefield();yield return Frame();ui.RestoreBattlefieldWindows();yield return Frame();Check(prompt.fontSize==font&&calls==0,"Hand view/return font or submission");
                        var option=request.selectedCount==0&&test!="chapter-decline"?request.options.First(o=>o.id.StartsWith("card:")&&!o.selected):request.options[ok];int expected=ui.OptionIndex(option.id);var target=option.id=="ok"?confirm:Root.GetComponentsInChildren<Button>().Single(b=>b.name=="Card_"+option.card.id);Click(target);Check(acting?calls==1&&last==expected&&!ui.TrySubmitPresentedOption(expected,request.requestId):calls==0&&!ui.TrySubmitPresentedOption(expected,request.requestId),"Exact hand/confirm native ID, disabled or duplicate");
                        File.AppendAllText(Output+"/steps.log",lang+" "+test+" count="+request.selectedCount+" acting="+acting+" "+option.id+" PASS\n");if(lang=="zh-CN"&&acting&&test=="chapter-draw"&&request.selectedCount==2){ScreenCapture.CaptureScreenshot(Output+"/discard-two.png");yield return Frame();}
                    }
                }
            }
            var sample=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Output+"/inputs/chapter-draw-step-92.json"));var flags=BindingFlags.Instance|BindingFlags.NonPublic;var field=typeof(ForgeBattlefieldController).GetField("view",flags);var text=typeof(ForgeBattlefieldController).GetProperty("DiscardSelectionInstruction",flags);sample.request.minCount=sample.request.maxCount=2;field.SetValue(ui,sample);Check((string)text.GetValue(ui)=="オーナーが捨てるカードを 2 枚選択。","Mandatory discard wording changed");sample.request.minCount=1;Check((string)text.GetValue(ui)=="オーナーが捨てるカードを 1–2 枚選択。","Nonzero range wording");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(language);if(module!=null)module.enabled=active;}
    }
}
#endif
