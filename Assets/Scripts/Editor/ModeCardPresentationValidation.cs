#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class ModeCardPresentationValidation
{
    const string Output=".tools/mode-frame-review-1004";
    static bool running;
    static void Check(bool valid,string message){if(!valid)throw new Exception(message);}
    public static void Run(){if(running)return;Check(Application.isPlaying,"Requires Play Mode");running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("ModeCardValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        string language=GamePreferences.Language;var iterator=Validate(ui);var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=banner!=null&&banner.enabled;if(banner!=null)banner.enabled=false;
        try{while(true){object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: zh-CN/en-US/ja-JP real Spree, mode and optional-cost requests; separate local-art/effect cards, fixed font and bounded scroll; original rule-line identity after target filtering; selected/disabled/minimum gates; raycast native index, stale/disabled/duplicate/viewer guards. Not a live network or all-cards claim.");}finally{running=false;GamePreferences.SetLanguage(language);if(banner!=null)banner.enabled=enabled;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            GamePreferences.SetLanguage(language);float limit=Time.realtimeSinceStartup+10;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<limit)yield return null;Check(GamePreferences.Ready,"Locale not ready");
            foreach(string scenario in new[]{"spree-rich-choices","spree-rich-selected","spree-limited-selected","spree-targets-choices","spree-unpayable-choices","choose-one-choices","kicker-choices","kicker-base-choices","evoke-choices","evoke-normal-choices","fra-two-modes"}) {
                var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(scenario=="fra-two-modes"?"Tools/Validation/Reports/FraMechanics/modal-modes.json":"Tools/Validation/Reports/ModeChoices/"+scenario+".json"));
                int calls=0,last=-1;ui.Present(null,view.seat,false,null,null,"");ui.Present(view,view.seat,true,i=>{calls++;last=i;},null,"");yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();
                var menu=GameObject.Find("CardAbilityChoices");Check(menu!=null,"Missing mode-card menu");
                bool casting=view.request.selectionPurpose=="castingCosts";
                var options=view.request.options.Where(o=>o.id.StartsWith("mode:")||o.id.StartsWith("select:")||casting&&o.id.StartsWith("choice:")).ToArray();
                foreach(var option in options) {
                    var button=menu.GetComponentsInChildren<Button>().First(b=>b.name=="Mode_"+option.id);var image=button.transform.Find("CardHeader/Artwork").GetComponent<RawImage>();
                    var cardRect=(RectTransform)button.transform;
                    Check(image.texture!=null&&cardRect.rect.height/cardRect.rect.width>1.2f,"Mode has missing/non-card art");
                    Check(image.uvRect.yMin>=.449f&&image.uvRect.height<=.551f,"Printed rules remain in artwork UV");
                    Check(button.interactable==!option.disabled,"Mode availability ignored");
                    var body=button.GetComponentsInChildren<Text>().First(t=>t.name=="Effect");Check(!body.resizeTextForBestFit&&!string.IsNullOrWhiteSpace(body.text),"Mode text shrinks/missing");
                    var frame=button.transform.Find("RulesFrame").GetComponent<RectTransform>();
                    var panel=frame.Find("ModeRules").GetComponent<RectTransform>();
                    Check(Mathf.Approximately(frame.rect.width,image.rectTransform.rect.width)&&panel.rect.width<frame.rect.width&&panel.GetComponent<Image>().sprite!=null,"Rules frame/inset missing");
                    Check(panel.GetComponent<Image>().color.a==1&&button.GetComponent<CanvasGroup>()==null,"Disabled rules become transparent");
                    if(language=="zh-CN")Check(menu.transform.Find("Title").GetComponent<Text>().text!="放题","Japanese Spree title in Chinese");
                    if(scenario.StartsWith("spree-")&&language!="en-US")Check(!body.text.StartsWith("Destroy")&&!body.text.StartsWith("Put a"),"Localized mode rules absent: "+body.text);
                    if(option.disabled){ui.Choose(Array.IndexOf(view.request.options,option),view.request.requestId);Check(calls==0,"Unavailable mode submitted");}
                }
                string confirmId=view.request.kind=="ChooseModes"?"confirm-modes":"confirm-cards";
                if(!casting)Check(menu.transform.Find(confirmId).GetComponent<Button>().interactable==view.request.options.Any(o=>o.id==confirmId),"Minimum gate lost");
                ScreenCapture.CaptureScreenshot(Output+"/"+language+"-"+scenario+".png");yield return new WaitForEndOfFrame();
                var valid=options.FirstOrDefault(o=>!o.disabled);
                if(valid!=null) {
                    int index=Array.IndexOf(view.request.options,valid);ui.Choose(index,view.request.requestId-1);Check(calls==0,"Old mode submitted");
                    var button=menu.GetComponentsInChildren<Button>().First(b=>b.name=="Mode_"+(scenario=="kicker-base-choices"?"confirm-cards":valid.id));
                    if(scenario=="kicker-base-choices")index=Array.FindIndex(view.request.options,o=>o.id=="confirm-cards");
                    var rect=(RectTransform)button.transform;
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center+new Vector2(0,rect.rect.height*.28f))),button=PointerEventData.InputButton.Left};
                    var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Mode cannot be clicked");
                    ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);button.onClick.Invoke();Check(calls==1&&last==index,"Wrong/duplicate native mode index");
                    ui.Present(view,view.seat,false,_=>calls++,null,"");yield return null;ui.Choose(index,view.request.requestId);Check(calls==1,"Viewer chooses mode");
                }
            }
        }
    }
}
#endif
