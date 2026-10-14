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
public static class NameBanBoundaryPresentationValidation
{
    const string Base=".tools/name-ban-union-1009";
    [Serializable] class Meta {public string status,directory,runtimeHash;}
    public static bool Running {get;private set;}
    public static string Output {get;private set;}
    static void Check(bool v,string label){if(!v)throw new Exception(label);File.AppendAllText(Output+"/checks.txt",label+"\n");}
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}}
    static void Click(Button b){Check(b!=null&&b.interactable,"Enabled original action "+b?.name);var r=(RectTransform)b.transform;foreach(float x in new[]{.5f,.2f,.8f})foreach(float y in new[]{.5f,.2f,.8f}){var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(new Vector2(Mathf.Lerp(r.rect.xMin,r.rect.xMax,x),Mathf.Lerp(r.rect.yMin,r.rect.yMax,y)))),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);if(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b){ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);return;}}throw new Exception("Ray blocked "+b.name);}
    static ForgeSeatView Read(string d,string label){string p=d+"/"+label+"-seat1.json";File.Copy(p,Output+"/inputs/"+label+".json",true);File.AppendAllText(Output+"/input-hashes.tsv",p+"\t"+Hash(p)+"\n");return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p));}
    public static void Run(){if(!Application.isPlaying||Running)throw new Exception("Play required/already running");Running=true;Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");File.WriteAllText(Base+"/ui-latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");foreach(string p in new[]{"Assets/Scripts/Editor/NameBanBoundaryPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.LandChoices.cs","Assets/Scripts/UI/ForgeBattlefieldController.GenericChoices.cs","Assets/Scripts/UI/ForgeChoiceDismiss.cs"})File.AppendAllText(Output+"/sources.tsv",p+"\t"+Hash(p)+"\n");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("NameBanValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var it=Validate(ui);try{while(true){object next;try{if(!it.MoveNext())break;next=it.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS_BOUNDED: actual mandatory Card type and color1/1 choice/native confirmation rays; readonly/stale/duplicate/dismissal guards, fixed font and View Return; current exact blocked/restored priority options. Name/tax/payment/control selectors reuse existing proofs; native outcomes separate, principal locale only.");}finally{(it as IDisposable)?.Dispose();Running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{
            var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(Base+"/type-color-latest.json"));Check(meta.status=="PASS"&&meta.runtimeHash==JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json")).runtimeHash,"Exact current native producer/runtime");
            foreach(string label in new[]{"choice-0","choice-1","confirmed-choice-1"}){
                var v=Read(meta.directory,label);int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};ui.Present(null,1,false,null,null,"");ui.Present(v,1,true,submit,null,"");yield return Frame();var menu=GameObject.Find("CardAbilityChoices");Check(menu!=null&&menu.activeInHierarchy,label+" actual selector");
                string id=label=="choice-0"?v.request.options.Single(o=>o.label=="Instant").id:label=="choice-1"?v.request.options.Single(o=>o.label=="RED").id:"confirm-cards";int index=ui.OptionIndex(id);
                Check(index>=0,label+" exact native option");Check(!v.request.options.Any(o=>o.id=="cancel"),label+" mandatory cannot invent cancellation");GameObject.Find("DismissAbilityChoices").GetComponent<ForgeChoiceDismiss>().Dismiss();Check(calls==0,label+" mandatory backdrop no response");
                Check(!ui.TrySubmitPresentedOption(index,v.request.requestId-1),label+" stale guard");Check(menu.GetComponentsInChildren<Text>().All(t=>!t.resizeTextForBestFit),label+" fixed fonts");
                if(label=="choice-1")Check(!menu.transform.Find("GenericConfirm").GetComponent<Button>().interactable,"Color min1 native confirm absent before select");
                if(label=="confirmed-choice-1")Check(v.request.selectedCount==1&&menu.transform.Find("GenericConfirm").GetComponent<Button>().interactable,"Actual selected color1/1 confirmation gate");
                ui.ViewBattlefield();yield return Frame();Check(!ui.TrySubmitPresentedOption(index,v.request.requestId),label+" hidden choice guard");ui.RestoreBattlefieldWindows();yield return Frame();menu=GameObject.Find("CardAbilityChoices");Check(menu.GetComponentsInChildren<Text>().All(t=>!t.resizeTextForBestFit),label+" returned fixed font");
                var b=label=="choice-0"?menu.transform.Find("Choices/Viewport/Content/Option_"+index)?.GetComponent<Button>():null;
                if(b==null)b=menu.GetComponentsInChildren<Button>().Single(x=>x.name==(label=="choice-0"?"Option_"+index:label=="choice-1"?"GenericOption_"+index:"GenericConfirm"));Click(b);Check(calls==1&&last==index,label+" ray submits exact native index");Check(!ui.TrySubmitPresentedOption(index,v.request.requestId),label+" duplicate guard");
                ui.Present(v,1,false,submit,null,"");yield return Frame();Check(!ui.TrySubmitPresentedOption(index,v.request.requestId)&&calls==1,label+" readonly guard");
                if(label!="choice-0")ScreenCapture.CaptureScreenshot(Output+"/"+label+".png");yield return new WaitForEndOfFrame();
            }
            var blocked=Read(meta.directory,"completed-2");ui.Present(blocked,1,true,null,null,"");yield return Frame();Check(ui.OptionIndex("card:124")<0,"Current native own Shock blocked by chosen Instant");
            var restored=Read(meta.directory,"completed-4");ui.Present(restored,1,true,null,null,"");yield return Frame();Check(ui.OptionIndex("card:124")>=0,"Same exact Shock restored after Archon removal");Check(!GameObject.Find("CardAbilityChoices")?.activeInHierarchy??true,"Priority clears mandatory chooser");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=input;for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
