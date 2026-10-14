#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class FoundryRecastPresentationValidation {
    const string Base=".tools/foundry-recast-1009";
    public static string Output {get;private set;}
    public static bool Running {get;private set;}
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;File.AppendAllText(Output+"/checks.txt",label+"\n");}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Base+"/ui-inputs/"+name+".json"));
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Ray(Button button){
        var rect=(RectTransform)button.transform;
        var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
        Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Actual topmost ray "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);
    }
    public static void Run(bool chineseTextOnly=false){
        if(!Application.isPlaying||Running)throw new Exception("Play required/already running");Running=true;checks=0;
        Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");
        File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        foreach(var p in Directory.GetFiles(Base+"/ui-inputs"))File.Copy(p,Output+"/inputs/"+Path.GetFileName(p));
        foreach(var p in new[]{"Assets/Scripts/Editor/FoundryRecastPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.CardSelection.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("FoundryValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui,chineseTextOnly)));
    }
    static IEnumerator Guard(IEnumerator work){
        var tasks=new Stack<IEnumerator>();tasks.Push(work);
        try{while(tasks.Count>0){object next;try{var t=tasks.Peek();if(!t.MoveNext()){tasks.Pop();continue;}next=t.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator child)tasks.Push(child);else yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: "+checks+" checks; actual original Opt/Fling recast previews, yes/no rays, retry identity, stale/duplicate/readonly guards, battlefield view font/submit restore and next priority cleanup. Native effects/payments separate; no new UI first/network/all-language claim.");}
        finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui,bool chineseTextOnly){
        string language=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;
        var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{
            float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Preferences ready");
            foreach(string lang in chineseTextOnly?new[]{"zh-CN"}:new[]{"zh-CN","en-US","ja-JP"}){
            int revision=GamePreferences.Revision;bool changed=GamePreferences.Language!=lang;GamePreferences.SetLanguage(lang);until=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||changed&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language==lang,"Locale ready "+lang);yield return Frame();
            foreach(string sample in chineseTextOnly?new[]{"opt","fling"}:lang=="zh-CN"?new[]{"opt","retry","fling"}:new[]{"opt"}){
                var v=Read(sample+"-confirm");int calls=0,sent=-1;Action<int> submit=i=>{calls++;sent=i;};
                ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(v,1,true,submit,null,"");yield return Frame();
                var panel=GameObject.Find("CardSelection");Check(panel!=null,"Actual recast modal "+sample);
                Check(v.request.previewCards.Length==1&&v.request.previewCards[0].id==121,"Original preview ID121 "+sample);
                Check(panel.GetComponentsInChildren<Transform>().Any(t=>t.name=="ChoiceCard_121"),"Original choice card present "+sample);
                Check(panel.GetComponentsInChildren<RawImage>().Any(i=>i.texture!=null)||panel.GetComponentsInChildren<Image>().Any(i=>i.sprite!=null&&i.sprite.texture.width>300),"Preview art loaded "+sample);
                var buttons=panel.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("SelectionAction_")).ToArray();Check(buttons.Length==2&&buttons.All(b=>b.interactable),"Exactly two native actions "+sample);
                var title=panel.transform.Find("Title").GetComponent<Text>();var instruction=panel.transform.Find("Instruction").GetComponent<Text>();int font=title.fontSize,bodyFont=instruction.fontSize;
                Check(!title.resizeTextForBestFit&&!instruction.resizeTextForBestFit,"Fixed fonts "+sample);
                Check(instruction.text.StartsWith(lang=="zh-CN"?"免付法术力费用":lang=="ja-JP"?"「":"Cast ")&&!instruction.text.Contains("Do you want to play"),"Localized actual question "+lang+" "+sample);
                Check(instruction.preferredHeight<=instruction.rectTransform.rect.height+1,"Question fits "+lang+" "+sample);
                ui.ViewBattlefield();yield return Frame();Check(!ui.TrySubmitPresentedOption(ui.OptionIndex("ok"),v.request.requestId),"Hidden modal cannot submit "+sample);ui.RestoreBattlefieldWindows();yield return Frame();
                Check(title.fontSize==font&&instruction.fontSize==bodyFont&&calls==0,"Return preserves fonts/no submit "+sample);
                Check(!ui.TrySubmitPresentedOption(ui.OptionIndex("ok"),v.request.requestId-1),"Stale rejected "+sample);
                if(sample!="retry"){ScreenCapture.CaptureScreenshot(Output+"/"+lang+"-"+sample+".png");yield return Frame();}
                string pick=sample=="retry"?"cancel":"ok";int expected=ui.OptionIndex(pick);var button=buttons.Single(b=>b.name=="SelectionAction_"+expected);Ray(button);button.onClick.Invoke();Check(calls==1&&sent==expected,"Exact action/duplicate guard "+sample);
                ui.Present(v,1,false,submit,null,"");yield return Frame();Check(!ui.TrySubmitPresentedOption(expected,v.request.requestId),"Read-only cannot act "+sample);
                ui.Present(Read(sample+"-resolved"),1,true,submit,null,"");yield return Frame();Check(!panel.activeInHierarchy&&calls==1,"Next priority clears modal "+sample);
            }}
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=input;GamePreferences.SetLanguage(language);for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
