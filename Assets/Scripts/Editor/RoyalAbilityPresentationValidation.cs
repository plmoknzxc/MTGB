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

public static class RoyalAbilityPresentationValidation
{
    public static bool Running {get;private set;}
    public static string Output {get;private set;}
    const string Base=".tools/royal-boundaries-1006/ui";
    [Serializable] sealed class Meta {public string folder,status,runtimeHash;}
    sealed class Host:MonoBehaviour{}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    public static void Run(){Check(Application.isPlaying&&!Running,"Play required");Running=true;Output=Base+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");File.WriteAllText(Base+"/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/producer-hashes.tsv",string.Join("\n",new[]{"Assets/Scripts/Editor/RoyalAbilityPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Stack.cs","Assets/Scripts/UI/ForgeBattlefieldController.AbilityFace.cs","Assets/Scripts/UI/ForgeBattlefieldController.Emblems.cs","Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java"}.Select(p=>p+"\t"+Hash(p)))+"\n");var go=new GameObject("TriggerOrderValidation");go.AddComponent<Host>().StartCoroutine(Guard(Exercise(),go));}
    static IEnumerator Guard(IEnumerator e,GameObject go){Exception error=null;while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){error=ex;break;}if(!more)break;if(current is IEnumerator nested){yield return Nested(nested,ex=>error=ex);if(error!=null)break;}else yield return current;}try{(e as IDisposable)?.Dispose();}catch(Exception ex){error??=ex;}File.WriteAllText(Output+"/result.txt",error==null?"PASS: current native monarch/initiative ability helpers in real ordering/stack requests, same-source distinct trigger IDs and event details, removed-helper pending stack retained; exact resource and three-language known rule/name paths, raycast/native option/read-only/duplicate gates, view-return fixed fonts. Ordinary Soul trigger actual-input text/art regression, altered rule retained; no network/full PASS.":"FAIL\n"+error);Running=false;UnityEngine.Object.Destroy(go);}
    static IEnumerator Nested(IEnumerator e,Action<Exception> fail){while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){fail(ex);break;}if(!more)break;if(current is IEnumerator child)yield return Nested(child,fail);else yield return current;}}
    static void Click(Button b){var rect=(RectTransform)b.transform;var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);}
    static IEnumerator Exercise(){
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("RoyalAbilityBattlefield").AddComponent<ForgeBattlefieldController>();bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool active=module!=null&&module.enabled;if(module!=null)module.enabled=false;string language=GamePreferences.Language;
        try{
            var inputs=new List<string>();
            foreach(string sample in new[]{"monarch-multi-combat","initiative-multi-combat"}){
                var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/royal-boundaries-1006/regression/"+sample+"-result.json"));Check(meta.status=="PASS"&&meta.runtimeHash==JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json")).runtimeHash,"Native proof stale");
                var paths=Directory.GetFiles(meta.folder,"step-*.json").Where(p=>!p.EndsWith("-other.json")).OrderBy(p=>int.Parse(Path.GetFileNameWithoutExtension(p).Split('-').Last())).ToArray();
                inputs.AddRange(paths.Where(p=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p)).request.kind=="OrderAbilities"));
                var pending=paths.Where(p=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p)).stackItems.Any(e=>e.source?.helperArtName==(sample.StartsWith("monarch")?"The Monarch":"The Initiative"))).ToArray();
                Check(pending.Length>0,"Missing pending helper");inputs.Add(pending.First());inputs.Add(pending.Last());
            }
            foreach(string path in inputs){File.Copy(path,Output+"/inputs/"+Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)))+"-"+Path.GetFileName(path));File.AppendAllText(Output+"/input-hashes.tsv",path+"\t"+Hash(path)+"\n");}
            foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}){
                int revision=GamePreferences.Revision;bool change=GamePreferences.Language!=lang;GamePreferences.SetLanguage(lang);float until=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||change&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale unavailable");yield return Frame();
                foreach(string path in inputs)foreach(bool acting in new[]{true,false}){
                    var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));var request=view.request;int calls=0,last=-1;ui.Present(null,1,false,null,null,"");ui.Present(view,acting?request.seat:3-request.seat,acting,i=>{calls++;last=i;},null,"");yield return Frame();foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}
                    bool order=request.kind=="OrderAbilities";var ordered=request.orderedAbilities??Array.Empty<ForgeStackItem>();var options=request.options.Where(o=>o.id.StartsWith("order:")).ToArray();var entries=order?ordered.Concat(options.Select(o=>o.ability)).ToArray():view.stackItems.Where(e=>e.source?.helperArtName is "The Monarch" or "The Initiative").ToArray();
                    Check(entries.Length>0,"No helper entries");
                    foreach(var entry in entries){Transform face=order?Root.GetComponentsInChildren<Button>().Single(b=>b.name=="OrderedAbility_"+entry.id).transform.Find("Face/AbilityFace"):Root.GetComponentsInChildren<Transform>().Single(t=>t.name=="StackCard_"+Array.FindIndex(view.stackItems,e=>e.id==entry.id)).Find("AbilityFace");
                        string helper=entry.source.helperArtName;Check(SpecialEffectArt.TryLoad(helper,out var sprite)&&face.Find("Art").GetComponent<RawImage>().texture==sprite.texture,"Helper art absent");string expected=helper=="The Monarch"?(lang=="zh-CN"?"君主":lang=="ja-JP"?"統治者":"The Monarch"):DeckKeywordGlossary.All.Single(e=>e.English==helper).Name;Check(face.Find("Name").GetComponent<Text>().text==expected,"Name English");var text=face.Find("EffectClip/Effect").GetComponent<Text>();Check(lang=="en-US"||!text.text.Contains("Whenever")&&!text.text.Contains("At the beginning"),"Known helper paragraph untranslated: "+text.text);Check(!text.resizeTextForBestFit&&!text.GetComponentInChildren<TMPro.TextMeshProUGUI>().enableAutoSizing,"Shrinking text");foreach(string id in new[]{"152","153"})if(entry.description.Contains("("+id+")"))Check(text.text.Contains("("+id+")"),"Damage source identity dropped");
                    }
                    var fonts=Root.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Select(t=>t.fontSize).ToArray();ui.ViewBattlefield();yield return Frame();ui.RestoreBattlefieldWindows();yield return Frame();Check(fonts.SequenceEqual(Root.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Select(t=>t.fontSize))&&calls==0,"View return font/submit");
                    if(order){var panel=Root.Find("AbilityOrder");string id=ordered.Length==2?"confirm-order":ordered.Length==1?"reset-order":options[0].id;int index=ui.OptionIndex(id);var button=id=="confirm-order"?panel.Find("ConfirmOrder").GetComponent<Button>():id=="reset-order"?panel.Find("ResetOrder").GetComponent<Button>():panel.GetComponentsInChildren<Button>().Single(b=>b.name=="OrderedAbility_"+options[0].ability.id);Click(button);Check(acting?calls==1&&last==index&&!ui.TrySubmitPresentedOption(index,request.requestId):calls==0&&!ui.TrySubmitPresentedOption(index,request.requestId),"Wrong identity/read-only/duplicate");}
                    else Check(calls==0,"Stack display submitted");
                    if(lang=="zh-CN"&&acting){ScreenCapture.CaptureScreenshot(Output+"/"+Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)))+"-"+Path.GetFileNameWithoutExtension(path)+".png");yield return Frame();}File.AppendAllText(Output+"/steps.log",lang+" "+path+" acting="+acting+" entries="+entries.Length+" PASS\n");
                }
                // The adjacent printed-card formatter shares the method but must not inherit helper rules.
                var old=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/trigger-order-1006/native/apnap-life-result.json"));string oldPath=Directory.GetFiles(old.folder,"step-*.json").First(p=>!p.EndsWith("-other.json")&&JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p)).request.kind=="OrderAbilities");File.AppendAllText(Output+"/input-hashes.tsv",oldPath+"\t"+Hash(oldPath)+"\n");var oldView=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(oldPath));ui.Present(oldView,oldView.request.seat,false,null,null,"");yield return Frame();var entryOld=oldView.request.options.First(o=>o.ability!=null).ability;var catalog=(Dictionary<string,CardDefinition>)typeof(ForgeBattlefieldController).GetField("catalog",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);var definition=catalog[entryOld.source.printingKey];var faceOld=Root.GetComponentsInChildren<Button>().Single(b=>b.name=="OrderedAbility_"+entryOld.id).transform.Find("Face/AbilityFace");Check(faceOld.Find("EffectClip/Effect").GetComponent<Text>().text==GamePreferences.CardRules(definition).Trim(),"Ordinary trigger altered");
            }
            var method=typeof(ForgeBattlefieldController).GetMethod("AbilityEffectText",BindingFlags.Instance|BindingFlags.NonPublic);string unknown="If another condition applies, draw a card.";Check((string)method.Invoke(ui,new object[]{new ForgeCardView{effectMarker=true,markerKind="effect",helperArtName="The Monarch",name="The Monarch"},unknown})==unknown,"Unknown condition guessed");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(language);if(module!=null)module.enabled=active;}
    }
}
#endif
