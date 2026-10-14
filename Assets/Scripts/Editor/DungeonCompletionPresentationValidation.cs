#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class DungeonCompletionPresentationValidation
{
    public static bool Running {get;private set;}
    public static string Output {get;private set;}
    [Serializable] sealed class Meta {public string folder,status,runtimeHash;}
    sealed class Host:MonoBehaviour{}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    public static void Run(){Check(Application.isPlaying&&!Running,"Play required");Running=true;Output=".tools/undercity-paths-1006/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");File.WriteAllText(".tools/undercity-paths-1006/ui/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/producer-hashes.tsv",string.Join("\n",new[]{"Assets/Scripts/Editor/DungeonCompletionPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.DungeonChoices.cs","Assets/Scripts/UI/ForgeBattlefieldController.CardSelection.cs","Assets/Scripts/UI/ForgeBattlefieldController.BattlefieldView.cs","Assets/Scripts/UI/ForgeBattlefieldController.Arena.cs"}.Select(p=>p+"\t"+Hash(p)))+"\n");var go=new GameObject("DungeonCompletionValidation");go.AddComponent<Host>().StartCoroutine(Guard(Exercise(),go));}
    static IEnumerator Guard(IEnumerator e,GameObject go){Exception error=null;while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){error=ex;break;}if(!more)break;if(current is IEnumerator nested){yield return Nested(nested,ex=>error=ex);if(error!=null)break;}else yield return current;}try{(e as IDisposable)?.Dispose();}catch(Exception ex){error??=ex;}File.WriteAllText(Output+"/result.txt",error==null?"PASS: native Undercity legal successors/known Archive alias and ordinary room branch, completed/reentered helper identities, three-language full maps and exact known localized room labels, fixed font/fit, raycast native option mapping and readonly/duplicate guards, battlefield view-return; native effects and network separately. Existing choice consumers incremental, no UI first.":"FAIL\n"+error);Running=false;UnityEngine.Object.Destroy(go);}
    static IEnumerator Nested(IEnumerator e,Action<Exception> fail){while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){fail(ex);break;}if(!more)break;if(current is IEnumerator child)yield return Nested(child,fail);else yield return current;}}
    static void Click(Button b){var rect=(RectTransform)b.transform;var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);}
    static IEnumerator Exercise(){
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("DungeonBattlefield").AddComponent<ForgeBattlefieldController>();bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool active=module!=null&&module.enabled;if(module!=null)module.enabled=false;string language=GamePreferences.Language;
        var choices=new List<string>();var states=new List<string>();
        try{
            foreach(string name in new[]{"forge-trap-reenter","well-arena-empty","well-stash"}) {var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/undercity-paths-1006/native/"+name+"-result.json"));Check(meta.status=="PASS","Native case failed");choices.AddRange(Directory.GetFiles(meta.folder,"*-choice-*.json").Where(p=>!p.EndsWith("-other.json")));states.AddRange(Directory.GetFiles(meta.folder,"resolved-*.json").Where(p=>!p.EndsWith("-other.json")));}
            var normal=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/dungeon-paths-1006/native/mine-fungi-result.json"));choices.Add(normal.folder+"/room-choice-2.json");
            foreach(string p in choices.Concat(states)){File.Copy(p,Output+"/inputs/"+Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(p)))+"-"+Path.GetFileName(p));File.AppendAllText(Output+"/input-hashes.tsv",p+"\t"+Hash(p)+"\n");}
            foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}) {
                int rev=GamePreferences.Revision;bool changing=GamePreferences.Language!=lang;GamePreferences.SetLanguage(lang);float deadline=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||changing&&GamePreferences.Revision==rev)&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready,"Locale not ready");yield return Frame();
                foreach(string path in choices)foreach(bool acting in new[]{true,false}) {
                    var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));int calls=0,last=-1;ui.Present(view,view.request.seat,acting,i=>{calls++;last=i;},null,"");yield return Frame();foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}
                    bool room=view.request.selectionPurpose=="dungeonRoom";Check(room||view.request.selectionPurpose=="dungeon","Unexpected sample");Button first=null;int firstIndex=-1;
                    if(room) {
                        var map=Root.GetComponentsInChildren<Transform>().Single(t=>t.name=="DungeonMap").GetComponent<Image>();Check(map.sprite!=null&&map.preserveAspect,"Room full map missing/distorted");
                        var source=view.request.sourceCard;string original=(source.originalRules??source.currentRules??"").Replace("\\n","\n");string localized=GamePreferences.FeatureRules(new CardFeatureDefinition{cardName=source.name,description=original}).Replace("\\n","\n");var originalLines=original.Split('\n');var localizedLines=localized.Split('\n');
                        foreach(var option in view.request.options) {
                            int index=ui.OptionIndex(option.id);var button=Root.GetComponentsInChildren<Transform>().Single(t=>t.name=="DungeonRoom_"+index).GetComponent<Button>();var text=button.GetComponentInChildren<Text>();Check(button.interactable==acting&&!text.resizeTextForBestFit,"Room permissions/font");Check(text.preferredHeight<=text.rectTransform.rect.height+1,"Room clips "+text.text);string roomName=option.label.Split('—')[0].Trim();if(source.name=="Undercity"&&roomName=="Archive")roomName="Archives";int line=Array.FindIndex(originalLines,s=>s.StartsWith(roomName+" —",StringComparison.Ordinal));string expected=line>=0&&originalLines.Length==localizedLines.Length&&localizedLines[line]!=originalLines[line]?localizedLines[line]:option.label;Check(text.text==expected,"Wrong exact localized branch");if(first==null){first=button;firstIndex=index;}
                        }
                    }else {
                        Check(view.request.options.Length==3&&!view.request.options.Any(o=>o.card.name=="Undercity"),"Ordinary venture candidate legality");foreach(var option in view.request.options){var tile=Root.GetComponentsInChildren<Transform>().Single(t=>t.name=="ChoiceCard_"+option.card.id);Check(tile.Find("Artwork").GetComponent<Image>().sprite!=null,"Dungeon face missing");if(first==null){first=tile.Find("Artwork").GetComponent<Button>();firstIndex=ui.OptionIndex(option.id);}}
                    }
                    var fonts=Root.GetComponentsInChildren<Text>().ToDictionary(t=>t,t=>t.fontSize);ui.ViewBattlefield();yield return Frame();ui.RestoreBattlefieldWindows();yield return Frame();Check(fonts.All(p=>p.Key!=null&&p.Key.fontSize==p.Value)&&calls==0,"Room view return font/submission");if(acting){Click(first);Check(calls==1&&last==firstIndex&&!ui.TrySubmitPresentedOption(firstIndex,view.request.requestId),"Native room option/duplicate mapping");}else{first.onClick.Invoke();Check(calls==0&&!ui.TrySubmitPresentedOption(firstIndex,view.request.requestId),"Readonly branch submitted");}File.AppendAllText(Output+"/steps.log",lang+" "+path+" acting="+acting+" PASS\n");
                    if(lang=="zh-CN"&&acting&&room&&Path.GetFileName(path)=="room-choice-3.json"){ScreenCapture.CaptureScreenshot(Output+"/"+Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)))+"-room.png");yield return Frame();}
                }
                foreach(string path in states){var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));ui.Present(v,v.seat,false,null,null,"");yield return Frame();foreach(var d in v.players.SelectMany(p=>p.effectMarkers).Where(c=>c.markerKind=="dungeon")){var tile=Root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Card_"+d.id);Check(tile.Find("Visual/Border/Face").GetComponent<Image>().sprite!=null,"Current dungeon helper missing");}File.AppendAllText(Output+"/steps.log",lang+" state "+path+" PASS\n");}
            }
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(language);if(module!=null)module.enabled=active;}
    }
}
#endif
