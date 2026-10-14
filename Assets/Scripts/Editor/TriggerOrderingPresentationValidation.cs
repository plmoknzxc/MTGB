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

public static class TriggerOrderingPresentationValidation
{
    public static bool Running {get;private set;}
    public static string Output {get;private set;}
    const string Base=".tools/trigger-order-1006/ui";
    [Serializable] sealed class Meta {public string folder,status,runtimeHash;}
    sealed class Host:MonoBehaviour{}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    public static void Run(){Check(Application.isPlaying&&!Running,"Play required");Running=true;Output=Base+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");File.WriteAllText(Base+"/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/producer-hashes.tsv",string.Join("\n",new[]{"Assets/Scripts/Editor/TriggerOrderingPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Stack.cs","Assets/Scripts/UI/ForgeBattlefieldController.AbilityFace.cs"}.Select(p=>p+"\t"+Hash(p)))+"\n");var go=new GameObject("TriggerOrderValidation");go.AddComponent<Host>().StartCoroutine(Guard(Exercise(),go));}
    static IEnumerator Guard(IEnumerator e,GameObject go){Exception error=null;while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){error=ex;break;}if(!more)break;if(current is IEnumerator nested){yield return Nested(nested,ex=>error=ex);if(error!=null)break;}else yield return current;}try{(e as IDisposable)?.Dispose();}catch(Exception ex){error??=ex;}File.WriteAllText(Output+"/result.txt",error==null?"PASS: actual both-controller Soul trigger ordering requests, all partial/reset/full-order transitions, exact native option IDs by raycast; chosen tiles disabled, confirm only after all, reset only after partial; observer cannot submit, no duplicate; three locales, source refresh between seats, exact source art/rules, fixed fonts through view/return. Actual APNAP/life/targets separately asserted by native driver; no network claim.":"FAIL\n"+error);Running=false;UnityEngine.Object.Destroy(go);}
    static IEnumerator Nested(IEnumerator e,Action<Exception> fail){while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){fail(ex);break;}if(!more)break;if(current is IEnumerator child)yield return Nested(child,fail);else yield return current;}}
    static void Click(Button b){var rect=(RectTransform)b.transform;var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);}
    static IEnumerator Exercise(){
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("TriggerOrderBattlefield").AddComponent<ForgeBattlefieldController>();bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool active=module!=null&&module.enabled;if(module!=null)module.enabled=false;string language=GamePreferences.Language;var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None).Select(n=>(n,n.enabled)).ToArray();foreach(var n in notices)n.n.enabled=false;
        try{
            var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/trigger-order-1006/native/apnap-life-result.json"));Check(meta.status=="PASS"&&meta.runtimeHash==JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json")).runtimeHash,"Native proof stale");
            var inputs=Directory.GetFiles(meta.folder,"step-*.json").Where(p=>!p.EndsWith("-other.json")).OrderBy(p=>int.Parse(Path.GetFileNameWithoutExtension(p).Split('-').Last())).Where(p=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p)).request.kind=="OrderAbilities").ToArray();Check(inputs.Length>=10,"Missing actual transition inputs");foreach(string path in inputs){File.Copy(path,Output+"/inputs/"+Path.GetFileName(path));File.AppendAllText(Output+"/input-hashes.tsv",path+"\t"+Hash(path)+"\n");}
            foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}){
                int revision=GamePreferences.Revision;bool change=GamePreferences.Language!=lang;GamePreferences.SetLanguage(lang);float until=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||change&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale unavailable");yield return Frame();
                foreach(string path in inputs)foreach(bool acting in new[]{true,false}){
                    var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));var request=view.request;int calls=0,last=-1;ui.Present(null,1,false,null,null,"");ui.Present(view,acting?request.seat:3-request.seat,acting,i=>{calls++;last=i;},null,"");yield return Frame();foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}
                    var panel=Root.Find("AbilityOrder");Check(panel!=null&&panel.gameObject.activeInHierarchy,"Ordering panel absent");Check(panel.Find("Title").GetComponent<Text>().text==(lang=="zh-CN"?"排列结算顺序":lang=="en-US"?"Order ability resolution":"能力の解決順序"),"Title locale/cache");
                    var ordered=request.orderedAbilities??Array.Empty<ForgeStackItem>();var options=request.options.Where(o=>o.id.StartsWith("order:")).ToArray();var entries=ordered.Concat(options.Select(o=>o.ability)).ToArray();Check(entries.Length==2&&entries.All(e=>view.players[request.seat-1].battlefield.Any(c=>c.id==e.source.id)),"Source seat mismatch");
                    var catalog=(Dictionary<string,CardDefinition>)typeof(ForgeBattlefieldController).GetField("catalog",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
                    foreach(var entry in entries){var tile=panel.GetComponentsInChildren<Button>().Single(b=>b.name=="OrderedAbility_"+entry.id);bool chosen=ordered.Any(e=>e.id==entry.id);Check(tile.interactable==(!chosen&&acting),"Chosen/observer gate");var face=tile.transform.Find("Face/AbilityFace");var definition=catalog[entry.source.printingKey];Check(CardImageLibrary.TryLoadFullCard(definition,entry.source.backFace?1:0,out var sprite)&&face.Find("Art").GetComponent<RawImage>().texture==sprite.texture,"Source artwork/locale preference");Check(face.Find("Name").GetComponent<Text>().text==GamePreferences.CardFaceName(entry.source.name),"Source name");var label=face.Find("EffectClip/Effect").GetComponent<Text>();var symbols=label.GetComponentInChildren<TMPro.TextMeshProUGUI>();Check(!label.resizeTextForBestFit&&!symbols.enableAutoSizing&&!label.text.Contains("Zone Changer")&&label.text==GamePreferences.CardRules(definition).Trim(),"Font/debug metadata/printed trigger");}
                    var confirm=panel.Find("ConfirmOrder").GetComponent<Button>();var reset=panel.Find("ResetOrder").GetComponent<Button>();Check(confirm.interactable==(acting&&request.options.Any(o=>o.id=="confirm-order"))&&reset.interactable==(acting&&request.options.Any(o=>o.id=="reset-order")),"Confirm/reset availability");
                    var fonts=panel.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Select(t=>t.fontSize).ToArray();ui.ViewBattlefield();yield return Frame();Check(panel.GetComponentInParent<CanvasGroup>().alpha==0,"View battlefield visibility");ui.RestoreBattlefieldWindows();yield return Frame();Check(fonts.SequenceEqual(panel.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Select(t=>t.fontSize))&&calls==0,"View return changes font/submits");
                    string id=ordered.Length==2?"confirm-order":ordered.Length==1?"reset-order":options[0].id;int expected=ui.OptionIndex(id);Button button=id=="confirm-order"?confirm:id=="reset-order"?reset:panel.GetComponentsInChildren<Button>().Single(b=>b.name=="OrderedAbility_"+options[0].ability.id);Click(button);Check(acting?calls==1&&last==expected&&!ui.TrySubmitPresentedOption(expected,request.requestId):calls==0&&!ui.TrySubmitPresentedOption(expected,request.requestId),"Native identity/read-only/duplicate");
                    if(lang=="zh-CN"&&acting&&ordered.Length==2){ScreenCapture.CaptureScreenshot(Output+"/seat-"+request.seat+".png");yield return Frame();}File.AppendAllText(Output+"/steps.log",lang+" seat="+request.seat+" request="+request.requestId+" acting="+acting+" ordered="+ordered.Length+" "+id+" PASS\n");
                }
            }
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(language);if(module!=null)module.enabled=active;foreach(var n in notices)if(n.n!=null)n.n.enabled=n.enabled;}
    }
}
#endif
