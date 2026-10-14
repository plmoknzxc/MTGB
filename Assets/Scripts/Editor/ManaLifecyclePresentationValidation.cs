#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Only new prompt formatting; effect assertions belong to the native driver.
public static class ManaLifecyclePresentationValidation
{
    public static string Output {get;private set;}
    public static bool Running {get;private set;}
    [Serializable] class Meta {public string folder,status;}
    static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);}
    static Button Option(int index)=>GameObject.Find("CardAbilityChoices").GetComponentsInChildren<Button>().Single(b=>b.name=="ValueOption_"+index);
    static void Click(Button b)
    {
        Canvas.ForceUpdateCanvases();var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.TransformPoint(((RectTransform)b.transform).rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Check(b.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Prompt ray blocked");ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    public static void Run()
    {
        Check(Application.isPlaying&&!Running,"Play required");Running=true;Output=".tools/mana-lifecycle-1006/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);File.WriteAllText(".tools/mana-lifecycle-1006/ui/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ManaLifecycleValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        string lang=GamePreferences.Language;var task=Validate(ui);
        try{while(true){object next;try{if(!task.MoveNext())break;next=task.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: actual native sacrifice/optional-trigger/three-life confirmation in zh-CN/en-US/ja-JP; precise localized source text, compact bounded prompt, exact Yes/No rays, stale/duplicate/nonacting guards, view/return fixed fonts. Increment of existing InputConfirm family; native effects and transport separate.");}finally{GamePreferences.SetLanguage(lang);Running=false;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        var m=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/mana-lifecycle-1006/native/nonspell-upkeep-result.json"));Check(m.status=="PASS","Native result missing");
        var prompts=Directory.GetFiles(m.folder,"*-seat1.json").Select(p=>(path:p,view:JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p)))).Where(p=>p.view.request?.kind=="InputConfirm").GroupBy(p=>p.view.request.message).Select(g=>g.First()).ToList();
        var life=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/mana-lifecycle-1006/native/graveyard-source-result.json"));Check(life.status=="PASS","Life proof missing");prompts.Add(Directory.GetFiles(life.folder,"*-seat1.json").Select(p=>(path:p,view:JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p)))).First(p=>p.view.request?.kind=="InputConfirm"));Check(prompts.Count==3,"Need sacrifice/trigger/life actual requests");
        for(int i=0;i<prompts.Count;i++)File.Copy(prompts[i].path,Output+"/native-confirm-"+i+".json");
        bool enabled=ui.enabled;ui.enabled=false;var input=EventSystem.current.currentInputModule;bool inputEnabled=input!=null&&input.enabled;if(input!=null)input.enabled=false;
        try{foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool changed=language!=GamePreferences.Language;GamePreferences.SetLanguage(language);float until=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||changed&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale not ready");
            foreach(var item in prompts) {
                var v=item.view;int count=0,last=-1;Action<int> submit=i=>{count++;last=i;};
                foreach(string id in new[]{"ok","cancel"}) {
                    ui.Present(null,1,false,null,null,"");ui.Present(v,1,true,submit,null,"");yield return new WaitForSecondsRealtime(.4f);
                    var panel=GameObject.Find("CardAbilityChoices");var title=panel.transform.Find("ValueTitle").GetComponent<Text>();string expected=v.request.message.StartsWith("Sacrifice")?(language=="zh-CN"?"牺牲":language=="ja-JP"?"生け贄":"Sacrifice"):v.request.message.StartsWith("Use triggered")?(language=="zh-CN"?"触发式异能":language=="ja-JP"?"誘発型能力":"triggered ability"):(language=="zh-CN"?"3 点生命":language=="ja-JP"?"3点のライフ":"3 life");
                    Check(title.text.Contains(expected)&&!title.resizeTextForBestFit&&title.preferredHeight<=((RectTransform)title.transform).rect.height+1,"Prompt untranslated/resized/clipped");int font=title.fontSize;ui.ViewBattlefield();yield return null;Check(!ui.TrySubmitPresentedOption(ui.OptionIndex(id),v.request.requestId),"Hidden prompt submits");ui.RestoreBattlefieldWindows();yield return null;Check(font==title.fontSize,"View changes font");
                    if(v.request.message.StartsWith("Use triggered")) {
                        var effect=UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).First(t=>t.name=="Effect"&&t.transform.parent.name=="EffectClip"&&t.text.Contains(language=="zh-CN"?"阶段牌手":language=="ja-JP"?"フェイズのプレイヤー":"Phase: Player"));
                        Check(effect.text.Contains(language=="zh-CN"?"维持":language=="ja-JP"?"アップキープ":"upkeep")&&!effect.resizeTextForBestFit,"Printed phase trigger rule untranslated/resized");
                        File.WriteAllText(Output+"/phase-rule-"+language+".txt",effect.text);
                    }
                    int index=ui.OptionIndex(id);Check(!ui.TrySubmitPresentedOption(index,v.request.requestId-1),"Stale prompt");
                    if(id=="cancel"&&language=="zh-CN"){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Output+"/prompt-"+prompts.IndexOf(item)+".png");yield return new WaitForSecondsRealtime(.2f);}
                    int before=count;Click(Option(index));Check(count==before+1&&last==index&&!ui.TrySubmitPresentedOption(index,v.request.requestId),"Prompt index/duplicate");
                }
                ui.Present(null,1,false,null,null,"");ui.Present(v,1,false,submit,null,"");yield return null;Check(!Option(ui.OptionIndex("ok")).interactable&&!ui.TrySubmitPresentedOption(ui.OptionIndex("ok"),v.request.requestId),"Nonacting prompt submits");
            }
        }}finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(input!=null)input.enabled=inputEnabled;}
    }
}
#endif
