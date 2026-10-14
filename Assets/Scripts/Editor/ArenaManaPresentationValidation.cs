#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Increment of the existing compact InputConfirm contract, not another UI family.
public static class ArenaManaPresentationValidation
{
    public static string Output {get;private set;}
    public static bool Running {get;private set;}
    [Serializable] sealed class Result {public string folder,status;}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static Button Option(int i)=>GameObject.Find("CardAbilityChoices").GetComponentsInChildren<Button>().Single(b=>b.name=="ValueOption_"+i);
    static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Blocked confirmation ray");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    public static void Run()
    {
        Check(Application.isPlaying&&!Running,"Play required/already running");Running=true;
        Output=".tools/arena-mana-1006/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);File.WriteAllText(".tools/arena-mana-1006/ui/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ArenaManaValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        var task=Validate(ui);string language=GamePreferences.Language;
        try {
            while(true){object next;try{if(!task.MoveNext())break;next=task.Current;}catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: actual Arena of Glory paid-cost exert confirmation in zh-CN/en-US/ja-JP; localized exert prompt, bounded compact Yes/No exact-index raycasts, stale/duplicate/nonacting guards, view/return fixed fonts. Existing mana/ability and native exert state consumers reused; no new UI family, native effect or live-network claim.");
        } finally {GamePreferences.SetLanguage(language);Running=false;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        var meta=JsonUtility.FromJson<Result>(File.ReadAllText(".tools/arena-mana-1006/native/cancel-cost-result.json"));Check(meta.status=="PASS","Native cost proof missing");
        string input=meta.folder+"/exert-confirm-cancel-seat1.json";File.Copy(input,Output+"/native-confirm.json");var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(input));
        bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool moduleEnabled=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        try {foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool change=lang!=GamePreferences.Language;GamePreferences.SetLanguage(lang);float until=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||change&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale unavailable");
            int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};
            foreach(string id in new[]{"ok","cancel"}) {
                ui.Present(null,1,false,null,null,"");ui.Present(view,1,true,submit,null,"");yield return new WaitForSecondsRealtime(.5f);
                var panel=GameObject.Find("CardAbilityChoices");var title=panel.transform.Find("ValueTitle").GetComponent<Text>();
                Check(title.text.Contains(lang=="zh-CN"?"耗竭":lang=="ja-JP"?"督励":"Exert"),"Unlocalized exert prompt");Check(!title.resizeTextForBestFit&&title.preferredHeight<=((RectTransform)title.transform).rect.height+1,"Prompt size/clipping");
                Check(((RectTransform)panel.transform).rect.width<=400&&((RectTransform)panel.transform).rect.height<=180,"Confirmation not compact");
                int font=title.fontSize;ui.ViewBattlefield();yield return null;Check(!ui.TrySubmitPresentedOption(ui.OptionIndex(id),view.request.requestId),"Hidden confirmation submits");ui.RestoreBattlefieldWindows();yield return null;Check(font==title.fontSize,"View/return resizes text");
                if(id=="cancel") {yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Output+"/"+lang+"-confirm.png");yield return new WaitForSecondsRealtime(.2f);}
                int index=ui.OptionIndex(id);Check(!ui.TrySubmitPresentedOption(index,view.request.requestId-1),"Stale confirmation submits");int before=calls;Click(Option(index));Check(calls==before+1&&last==index&&!ui.TrySubmitPresentedOption(index,view.request.requestId),"Confirmation index/duplicate wrong");
            }
            ui.Present(null,1,false,null,null,"");ui.Present(view,1,false,submit,null,"");yield return null;Check(!Option(ui.OptionIndex("ok")).interactable&&!ui.TrySubmitPresentedOption(ui.OptionIndex("ok"),view.request.requestId),"Nonacting confirmation submits");
        }}finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=moduleEnabled;}
    }
}
#endif
