#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Only new multi-candidate/same-name list differences, not another UI first.
public static class StackRestoreListIncrementValidation
{
    const string Base=".tools/stack-target-restore-1008";
    public static bool Running{get;private set;}
    public static string Output{get;private set;}
    static int checks;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
    public static void Run(){
        Check(Application.isPlaying&&!Running,"Play required/already running");Running=true;checks=0;
        Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");
        File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        foreach(string p in Directory.GetFiles(Base+"/ui-inputs"))File.Copy(p,Output+"/inputs/"+Path.GetFileName(p));
        foreach(string p in new[]{"Assets/Scripts/Editor/StackRestoreListIncrementValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Notices.cs","Assets/Scripts/UI/ForgeBattlefieldController.LandChoices.cs","Assets/Scripts/UI/ForgeBattlefieldController.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("StackRestoreListIncrement").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator routine){
        var tasks=new Stack<IEnumerator>();tasks.Push(routine);
        try{while(tasks.Count>0){object next;try{var task=tasks.Peek();if(!task.MoveNext()){tasks.Pop();continue;}next=task.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator child)tasks.Push(child);else yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: "+checks+" checks; actual copied-spell cost list in zh-CN/en-US/ja-JP; exact native three-option count, unique original stack labels for same-name Opt/copy, all nine actual copy-choice/cancel button rays/native indices once; earlier zh-CN ordinary two-spell choices remain separately audited in first failed run, stale and clear checks. Existing list UI increment only; previous three-language/permission lifecycle evidence reused. No new native/global/network claim.");}
        finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}
    }
    static IEnumerator Frame(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static void Click(Button button){var rect=(RectTransform)button.transform;var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Actual native option ray "+button.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);}
    static IEnumerator Validate(ForgeBattlefieldController ui){
        string language=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;
        var module=EventSystem.current.currentInputModule;bool moduleEnabled=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var banners=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=banners.Select(b=>b.enabled).ToArray();foreach(var b in banners)b.enabled=false;
        try{
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
            int revision=GamePreferences.Revision;bool changing=GamePreferences.Language!=locale;GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+12;while((!GamePreferences.Ready||changing&&GamePreferences.Revision==revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language==locale,"Exact actual language ready");
            foreach(string scenario in new[]{"two-spells-copy-restore"}){
                var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Output+"/inputs/"+scenario+".json"));Check(view.request.options.Length==3&&view.stackItems.Length==2,"Actual three options/two spells");
                foreach(var option in view.request.options){
                    int calls=0,last=-1,index=Array.IndexOf(view.request.options,option);ui.Present(null,1,false,null,null,"");ui.Present(view,1,true,i=>{calls++;last=i;},null,"");yield return Frame();
                    var menu=GameObject.Find("CardAbilityChoices");var buttons=menu.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("ValueOption_")).ToArray();Check(buttons.Length==3,"Exactly native candidates/cancel");
                    var labels=buttons.Select(b=>b.GetComponentInChildren<Text>().text).ToArray();Check(labels.Distinct().Count()==3,"Same-name spells distinguished by native stack numbers");
                    for(int i=0;i<view.request.options.Length;i++){
                        var actual=view.request.options[i];var text=buttons.Single(b=>b.name=="ValueOption_"+i).GetComponentInChildren<Text>();
                        if(actual.id=="cancel")Check(text.text==(locale=="zh-CN"?"取消支付":locale=="en-US"?"Cancel payment":"支払いをキャンセル"),"Cancel payment identity");
                        else{var entry=view.stackItems.Single(s=>s.description+(s.copied?" (Copied Spell)":"")==actual.label);string middle=locale=="zh-CN"?(entry.copied?"（复制品 · 堆叠 ":"（堆叠 "):locale=="en-US"?(entry.copied?" (Copy · Stack ":" (Stack "):(entry.copied?"（コピー · スタック ":"（スタック ");Check(text.text==GamePreferences.CardFaceName(entry.source.name)+middle+entry.id+(locale=="en-US"?")":"）"),"Exact native source/copy and stack identity");}
                        Check(!text.resizeTextForBestFit&&text.preferredHeight<=text.rectTransform.rect.height+1,"Fixed text fits");
                    }
                    Check(!ui.TrySubmitPresentedOption(index,view.request.requestId-1)&&calls==0,"Stale option rejected");
                    if(index==0){ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-"+scenario+".png");yield return Frame();}
                    Click(buttons.Single(b=>b.name=="ValueOption_"+index));ui.Choose(index,view.request.requestId);Check(calls==1&&last==index,"Original native option index submitted exactly once");
                    ui.Present(null,1,false,null,null,"");yield return Frame();Check(GameObject.Find("ValueTitle")==null,"List cleared");
                    File.AppendAllText(Output+"/steps.log",locale+" "+scenario+" "+option.id+" index="+index+" PASS\n");
                }
            }
        }}finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=moduleEnabled;GamePreferences.SetLanguage(language);for(int i=0;i<banners.Length;i++)if(banners[i]!=null)banners[i].enabled=flags[i];}
    }
}
#endif
