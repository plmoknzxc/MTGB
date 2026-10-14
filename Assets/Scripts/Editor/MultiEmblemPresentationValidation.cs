#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Actual emblem marker/inspect consumers; these are incremental UI proofs.
public static class MultiEmblemPresentationValidation
{
    public static bool Running {get;private set;}
    public static string Output {get;private set;}
    [Serializable] sealed class Meta {public string folder,status;}
    sealed class Host:MonoBehaviour{}
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static Transform Root=>GameObject.Find("Canvas").transform;
    static TMPro.TextMeshProUGUI Rules=>Root.Find("InspectPanel/Rules").GetComponentInChildren<TMPro.TextMeshProUGUI>();
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static ForgeSeatView Read(string p){File.AppendAllText(Output+"/input-hashes.tsv",p+"\t"+Hash(p)+"\n");return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p));}
    static void Clean(ForgeBattlefieldController ui){foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}var f=typeof(ForgeBattlefieldController).GetField("inspectHoverRevision",Hidden);f.SetValue(ui,(int)f.GetValue(ui)+1);}
    static void Inspect(ForgeBattlefieldController ui,ForgeCardView c){typeof(ForgeBattlefieldController).GetField("inspectOriginalState",Hidden).SetValue(ui,false);typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{c});}
    static void Click(Button b){var rect=(RectTransform)b.transform;var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Ray blocked "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    public static void Run(){Check(Application.isPlaying&&!Running,"Play required");Running=true;Output=".tools/multi-emblems-1006/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);File.WriteAllText(".tools/multi-emblems-1006/ui/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/producer-hashes.tsv",string.Join("\n",new[]{"Assets/Scripts/Editor/MultiEmblemPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.EmblemText.cs","Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs","Assets/Scripts/UI/ForgeBattlefieldController.Emblems.cs","Assets/Scripts/UI/ForgeBattlefieldController.Arena.cs","Assets/Scripts/UI/GamePreferences.cs","Assets/Scripts/UI/ForgeBattlefieldController.TokenArt.cs","Assets/Scripts/UI/DeckRelatedTokenArt.cs","Assets/Resources/RelatedTokenArt.json"}.Select(p=>p+"\t"+Hash(p)))+"\n");var go=new GameObject("MultiEmblemValidation");go.AddComponent<Host>().StartCoroutine(Guard(Exercise(),go));}
    static IEnumerator Guard(IEnumerator e,GameObject go){Exception error=null;while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){error=ex;break;}if(!more)break;if(current is IEnumerator child){yield return Nested(child,ex=>error=ex);if(error!=null)break;}else yield return current;}try{(e as IDisposable)?.Dispose();}catch(Exception ex){error??=ex;}File.WriteAllText(Output+"/result.txt",error==null?"PASS: actual same/mixed and stolen-source native emblem snapshots, both public views/three languages, holder lanes and exact IDs/art/count, ray hover localized exact source recipe, unknown/custom negative, source gone and same IDs cross-turn, fixed text; existing UI increment only, no new native/UI first or live transport":"FAIL\n"+error);Running=false;UnityEngine.Object.Destroy(go);}
    static IEnumerator Nested(IEnumerator e,Action<Exception> fail){while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){fail(ex);break;}if(!more)break;if(current is IEnumerator child)yield return Nested(child,fail);else yield return current;}}
    static IEnumerator Exercise(){
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("EmblemBattlefield").AddComponent<ForgeBattlefieldController>();bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool active=module!=null&&module.enabled;if(module!=null)module.enabled=false;string language=GamePreferences.Language;
        try{foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}){
            int rev=GamePreferences.Revision;bool changing=GamePreferences.Language!=lang;GamePreferences.SetLanguage(lang);float deadline=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||changing&&GamePreferences.Revision==rev)&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready,"Locale not ready");
            foreach(string scenario in new[]{"double-emblems","mixed-source-left","stolen-source-emblem"}){
                var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/multi-emblems-1006/native/"+scenario+"-result.json"));Check(meta.status=="PASS","Native failed");
                foreach(string suffix in new[]{"","-other"}){
                    var created=Read(meta.folder+"/emblems-created"+suffix+".json");var later=Read(meta.folder+"/cross-turn"+suffix+".json");var ids=created.players.SelectMany(p=>p.emblems).Select(c=>c.id).ToArray();Check(ids.SequenceEqual(later.players.SelectMany(p=>p.emblems).Select(c=>c.id)),"Native cross-turn IDs differ");
                    ui.Present(null,created.seat,false,null,null,"");
                    foreach(var view in new[]{created,later}){
                        int calls=0;ui.Present(view,view.seat,false,i=>calls++,null,"");yield return Frame();Clean(ui);Root.Find("InspectPanel").gameObject.SetActive(false);
                        var emblems=view.players.SelectMany(p=>p.emblems).ToArray();int holder=emblems[0].ownerSeat;Check(view.players.Single(p=>p.seat==holder).emblems.Length==emblems.Length,"Wrong native holder");
                        var first=Root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Card_"+emblems[0].id);
                        if(scenario=="double-emblems"){Check(!Root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Card_"+emblems[1].id)&&first.GetComponentsInChildren<Text>().Any(t=>t.text=="×2"),"Actual identical emblems count/identity group missing");}
                        foreach(var card in emblems){
                            var tile=Root.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Card_"+card.id);if(tile==null)continue;
                            Clean(ui);Root.Find("InspectPanel").gameObject.SetActive(false);var art=tile.Find("Visual/Border/Face/Illustration")?.GetComponent<RawImage>();string expected=card.name.EndsWith("Ally of Zendikar")?"CardImage_TBFZ_12":"CardImage_TAKH_25";Check(art!=null&&art.texture!=null&&art.texture.name==expected,"Exact actual emblem art missing: "+expected);var pointer=tile.GetComponent<ForgeBattlefieldCardPointer>();pointer.enabled=true;var rect=(RectTransform)tile;var evt=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(evt,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<ForgeBattlefieldCardPointer>()==pointer,"Hover ray missed exact native emblem");ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,evt,ExecuteEvents.pointerEnterHandler);yield return Frame();Check(Root.Find("InspectPanel").gameObject.activeInHierarchy,"Hover detail absent");string text=Rules.text;Check(!string.IsNullOrWhiteSpace(text)&&text!=card.currentRules||lang=="en-US"&&text==card.currentRules,"Existing translation unused "+lang+" "+text);Check(!text.Contains("#70BFFF"),"Printed emblem rule incorrectly temporary blue");Check(Root.Find("InspectPanel/Name").GetComponent<Text>().text.StartsWith(lang=="zh-CN"?"徽记 — ":lang=="ja-JP"?"紋章 — ":"Emblem — "),"Emblem title locale");Check(Rules.preferredHeight<=Rules.rectTransform.rect.height+1,"Emblem hover rule clips");File.AppendAllText(Output+"/details.tsv",lang+"\t"+scenario+"\t"+view.seat+"\t"+view.turn+"\t"+card.id+"\t"+text+"\n");
                            var modified=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(card));modified.currentRules="Exact unknown custom emblem rule.";var formatter=typeof(ForgeBattlefieldController).GetMethod("EmblemLocalizedRules",Hidden);Check((string)formatter.Invoke(ui,new object[]{modified})==modified.currentRules,"Unknown modified emblem mistranslated");modified.name="Exact custom emblem name";Check((string)typeof(ForgeBattlefieldController).GetMethod("EmblemLocalizedName",Hidden).Invoke(ui,new object[]{modified})==modified.name,"Custom name replaced");
                            if(lang=="zh-CN"&&suffix==""&&view==created){ScreenCapture.CaptureScreenshot(Output+"/"+scenario+"-hover-"+card.id+".png");yield return Frame();}
                        }
                        Check(calls==0,"Readonly inspect submitted native response");
                    }
                    File.AppendAllText(Output+"/steps.log",lang+" "+scenario+" seat="+created.seat+" PASS\n");
                }
            }
        }}finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(language);if(module!=null)module.enabled=active;}
    }
}
#endif
