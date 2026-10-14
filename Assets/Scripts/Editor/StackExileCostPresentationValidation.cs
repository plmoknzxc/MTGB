#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Existing value-choice consumer increment; all inputs came from the native run.
public static class StackExileCostPresentationValidation
{
    const string Base=".tools/stack-exile-cost-1008";
    public static bool Running{get;private set;}
    public static string Output{get;private set;}
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Output+"/inputs/"+name+".json"));
    public static void Run(){
        Check(Application.isPlaying&&!Running,"Play required/already running");Running=true;checks=0;
        Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");
        File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        foreach(string path in Directory.GetFiles(Base+"/ui-inputs"))File.Copy(path,Output+"/inputs/"+Path.GetFileName(path));
        foreach(string path in new[]{"Assets/Scripts/Editor/StackExileCostPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Notices.cs","Assets/Scripts/UI/ForgeBattlefieldController.LandChoices.cs","Assets/Scripts/UI/ForgeBattlefieldController.cs"})File.Copy(path,Output+"/sources/"+Path.GetFileName(path));
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("StackExileCostValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator routine){
        var tasks=new Stack<IEnumerator>();tasks.Push(routine);
        try{while(tasks.Count>0){object next;try{var task=tasks.Peek();if(!task.MoveNext()){tasks.Pop();continue;}next=task.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator child)tasks.Push(child);else yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: "+checks+" checks; exact native stack-exile choice/cancel in zh-CN/en-US/ja-JP; localized title/name/stack identity/cancel, first-frame bounds and fixed fonts, real rays, original indices, stale/duplicate/nonacting guards, initial-to-retry request replacement and clear. Existing value-choice UI increment only; native outcomes separately proven; no global/network PASS.");}
        finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}
    }
    static IEnumerator Frame(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static void Click(Button button){
        var rect=(RectTransform)button.transform;var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Actual ray blocked "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Validate(ForgeBattlefieldController ui){
        string language=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;
        var module=EventSystem.current.currentInputModule;bool moduleEnabled=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var banners=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=banners.Select(b=>b.enabled).ToArray();foreach(var b in banners)b.enabled=false;
        try{foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
            int revision=GamePreferences.Revision;bool changing=GamePreferences.Language!=locale;GamePreferences.SetLanguage(locale);
            float deadline=Time.realtimeSinceStartup+12;while((!GamePreferences.Ready||changing&&GamePreferences.Revision==revision)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(GamePreferences.Ready&&GamePreferences.Language==locale,"Exact locale ready");
            var initial=Read("initial");var retry=Read("retry");
            Check(initial.request.requestId!=retry.request.requestId&&initial.request.contextId==retry.request.contextId,"Native retry identity/context");
            foreach(string optionId in new[]{"choice:0","cancel"}){
                int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};ui.Present(null,1,false,null,null,"");
                ui.Present(initial,1,true,submit,null,"");yield return Frame();
                var first=GameObject.Find("CardAbilityChoices");Check(first!=null,"Initial selector exists");
                ui.Present(retry,1,true,submit,null,"");yield return Frame();
                var panel=GameObject.Find("CardAbilityChoices");Check(panel==first,"Common selector root reused");
                var controls=panel.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("ValueOption_")).ToArray();
                Check(controls.Length==retry.request.options.Length,"Exactly native options; no stale duplicates");
                var title=panel.GetComponentsInChildren<Text>().Single(t=>t.name=="ValueTitle");
                Check(title.text==(locale=="zh-CN"?"选择要放逐的堆叠咒语":locale=="en-US"?"Choose a spell on the stack to exile":"追放するスタック上の呪文を選択"),"Localized stack-exile title");
                int index=Array.FindIndex(retry.request.options,o=>o.id==optionId);Check(index>=0&&!retry.request.options[index].disabled,"Original legal native option");
                var button=controls.Single(b=>b.name=="ValueOption_"+index);var text=button.GetComponentInChildren<Text>();
                if(optionId=="cancel")Check(text.text==(locale=="zh-CN"?"取消支付":locale=="en-US"?"Cancel payment":"支払いをキャンセル"),"Cancel is payment cancellation, not keep-unchanged");
                else {var entry=retry.stackItems.Single(s=>s.description==retry.request.options[index].label);Check(text.text.Contains(GamePreferences.CardFaceName(entry.source.name))&&text.text.Contains(entry.id.ToString()),"Exact visible native spell and stack identity");}
                foreach(var label in panel.GetComponentsInChildren<Text>())Check(!label.resizeTextForBestFit&&label.preferredHeight<=label.rectTransform.rect.height+1,"First-frame fixed-font text fits "+label.name);
                var sizes=panel.GetComponentsInChildren<Text>().Select(t=>t.fontSize).ToArray();
                Check(!ui.TrySubmitPresentedOption(index,initial.request.requestId)&&calls==0,"Previous actual request rejected");
                if(optionId=="choice:0"){ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-stack-exile.png");yield return Frame();}
                Click(button);ui.Choose(index,retry.request.requestId);Check(calls==1&&last==index,"Actual native index submitted once");
                ui.Present(retry,1,false,submit,null,"");yield return Frame();
                Check(!ui.TrySubmitPresentedOption(index,retry.request.requestId)&&calls==1,"Nonacting submission rejected");
                Check(panel.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("ValueOption_")).All(b=>!b.interactable),"Nonacting controls disabled");
                Check(sizes.SequenceEqual(panel.GetComponentsInChildren<Text>().Select(t=>t.fontSize)),"Fonts stable after permission refresh");
                ui.Present(Read("restored"),1,true,submit,null,"");yield return Frame();
                Check(GameObject.Find("ValueTitle")==null,"Restored priority removes selector");
                File.AppendAllText(Output+"/steps.log",locale+" "+optionId+" PASS\n");
            }
        }}finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=moduleEnabled;GamePreferences.SetLanguage(language);for(int i=0;i<banners.Length;i++)if(banners[i]!=null)banners[i].enabled=flags[i];}
    }
}
#endif
