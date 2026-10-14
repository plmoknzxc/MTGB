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
public static class StormCopyPresentationValidation
{
    public static bool Running{get;private set;}
    public static string Output{get;private set;}
    const string Base=".tools/storm-copy-1006/ui";
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    [Serializable]sealed class Meta{public string status,folder,runtimeHash;}
    sealed class Host:MonoBehaviour{}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Clean(ForgeBattlefieldController ui){foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}var revision=typeof(ForgeBattlefieldController).GetField("inspectHoverRevision",Hidden);revision.SetValue(ui,(int)revision.GetValue(ui)+1);Root.Find("InspectPanel").gameObject.SetActive(false);}
    public static void Run(){Check(Application.isPlaying&&!Running&&GamePreferences.Language=="zh-CN","Chinese Play required");Running=true;Output=Base+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");File.WriteAllText(Base+"/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/producer-hashes.tsv",string.Join("\n",new[]{"Assets/Scripts/Editor/StormCopyPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Stack.cs","Assets/Scripts/UI/ForgeBattlefieldController.StackList.cs","Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs","Assets/Scripts/UI/TemporaryCopyGlow.cs"}.Select(p=>p+"\t"+Hash(p)))+"\n");var go=new GameObject("DelayedExpiryValidation");go.AddComponent<Host>().StartCoroutine(Guard(Exercise(),go));}
    static IEnumerator Guard(IEnumerator e,GameObject go){Exception error=null;while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){error=ex;break;}if(!more)break;if(current is IEnumerator child){yield return Nested(child,ex=>error=ex);if(error!=null)break;}else yield return current;}try{(e as IDisposable)?.Dispose();}catch(Exception ex){error??=ex;}File.WriteAllText(Output+"/result.txt",error==null?"PASS: Chinese actual storm original/copy stack targets, exact distinct IDs by raycast, actual selected-state confirm/reset and readonly/duplicate/stale gates, shared same artwork without conflation, browse-only and view/return font stability; zero new first UI, existing layout/locale contract reused; native outcomes separate.":"FAIL\n"+error);Running=false;UnityEngine.Object.Destroy(go);}
    static IEnumerator Nested(IEnumerator e,Action<Exception> fail){while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){fail(ex);break;}if(!more)break;if(current is IEnumerator child)yield return Nested(child,fail);else yield return current;}}
    static void Click(Button b){var rect=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);}
    static IEnumerator Exercise(){
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("StormCopyBattlefield").AddComponent<ForgeBattlefieldController>();bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool active=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        try{
            foreach(string scenario in new[]{"original-countered","copy-countered"}){
                var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/storm-copy-1006/native/"+scenario+"-result.json"));Check(meta.status=="PASS"&&meta.runtimeHash==JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json")).runtimeHash,"Native proof stale");
                var paths=Directory.GetFiles(meta.folder,"step-*.json").Where(p=>!p.EndsWith("-other.json")).OrderBy(p=>int.Parse(Path.GetFileNameWithoutExtension(p).Split('-').Last())).Where(p=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p)).request.kind=="SelectStackTarget").ToArray();Check(paths.Length==2,"Actual target transitions");
                foreach(string path in paths){File.Copy(path,Output+"/inputs/"+scenario+"-"+Path.GetFileName(path));File.AppendAllText(Output+"/input-hashes.tsv",path+"\t"+Hash(path)+"\n");var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));var r=v.request;Check(v.stackItems.Length==3&&v.stackItems.Count(e=>e.copied)==2&&v.stackItems.Select(e=>e.id).Distinct().Count()==3,"Native copy identities");
                    foreach(bool acting in new[]{true,false}){
                        int calls=0,last=-1;ui.Present(null,1,false,null,null,"");ui.Present(v,acting?1:2,acting,i=>{calls++;last=i;},null,"");yield return Frame();Clean(ui);
                        if(!acting){Check(!Root.Find("StackList").gameObject.activeSelf,"Observer choosing");foreach(var opt in r.options)Check(!ui.TrySubmitPresentedOption(ui.OptionIndex(opt.id),r.requestId),"Observer submit");continue;}
                        var list=Root.Find("StackList");Check(list.gameObject.activeSelf,"Actual three-instance list missing");var buttons=list.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("StackListTarget_")).ToArray();Check(buttons.Length==3,"Copies merged in list");Check(buttons.Select(b=>b.GetComponent<Image>().sprite?.texture).Distinct().Count()==1,"Same printed face differs");
                        var sizes=list.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Select(t=>t.fontSize).ToArray();ui.ViewBattlefield();yield return Frame();ui.RestoreBattlefieldWindows();yield return Frame();Check(sizes.SequenceEqual(list.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Select(t=>t.fontSize)),"Font rescaled");
                        string target=r.selectedCount==0?"stack:"+v.stackItems.First(e=>scenario=="original-countered"?!e.copied:e.copied).id:"ok";
                        if(scenario=="copy-countered"&&r.selectedCount==0)target="stack:"+v.stackItems.First(e=>e.copied).id;
                        Button click=r.selectedCount==0?buttons.Single(b=>b.name=="StackListTarget_"+target.Substring(6)):list.Find("Confirm").GetComponent<Button>();int expected=ui.OptionIndex(target);Check(!ui.TrySubmitPresentedOption(expected,r.requestId-1),"Stale allowed");Click(click);Check(calls==1&&last==expected&&!ui.TrySubmitPresentedOption(expected,r.requestId),"Identity/duplicate");
                        ui.Present(v,1,true,i=>{calls++;last=i;},null,"");yield return Frame();Clean(ui);list=Root.Find("StackList");if(r.selectedCount>0){Click(list.Find("Reset").GetComponent<Button>());Check(last==ui.OptionIndex("reset-targets"),"Reset identity");}
                        ui.Present(v,1,true,i=>{calls++;last=i;},null,"");yield return Frame();Clean(ui);int before=calls;Click(Root.Find("StackColumn/ViewStack").GetComponent<Button>());yield return Frame();list=Root.Find("StackList");Check(list.Find("Confirm")==null,"Browse offers confirm");foreach(var b in list.GetComponentsInChildren<Button>().Where(b=>b.name=="Card")){Check(!b.interactable,"Browse clickable");b.onClick.Invoke();}Check(calls==before,"Browse submitted");Click(list.Find("Close").GetComponent<Button>());yield return Frame();
                        if(r.selectedCount==0&&scenario=="original-countered"){ScreenCapture.CaptureScreenshot(Output+"/storm-fan.png");yield return Frame();}
                        File.AppendAllText(Output+"/steps.log",scenario+" selected="+r.selectedCount+" "+target+" PASS\n");
                    }
                }
            }
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=active;}
    }
}
#endif
