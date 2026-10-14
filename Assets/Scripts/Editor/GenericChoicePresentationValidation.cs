#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class GenericChoicePresentationValidation
{
    const string Output=".tools/generic-choices-ui";
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter IntegratedBattlefield Play Mode first.");
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("GenericChoiceValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator iterator)
    {
        string original=GamePreferences.Language;
        try {
            while(true) {
                object next;
                try {if(!iterator.MoveNext())break;next=iterator.Current;}
                catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}
                yield return next;
            }
            File.WriteAllText(Output+"/result.txt","PASS: real color/villain request replay in zh-CN/en-US/ja-JP; bounds, selected state, count, raycast, option index, duplicate/stale/viewer guards, fixed font, long option scrolling. The effect result is separately asserted by Test-GenericChoices.py.");
        } finally {GamePreferences.SetLanguage(original);}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool changing=GamePreferences.Language!=language;
            GamePreferences.SetLanguage(language);float deadline=Time.realtimeSinceStartup+8;
            while(changing&&GamePreferences.Revision==revision&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(!changing||GamePreferences.Revision!=revision,"Locale not loaded");
            foreach(string name in new[]{"colors-choices-0-0","colors-choices-0-4","villain-choices-0-0","villain-repeat-choices-1-2"}) {
                var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/GenericChoices/"+name+".json"));
                int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};
                ui.Present(view,view.seat,true,submit,null,"");yield return new WaitForSecondsRealtime(.18f);Canvas.ForceUpdateCanvases();
                var menu=GameObject.Find("CardAbilityChoices");
                Check(menu!=null&&menu.activeInHierarchy,"Generic picker absent");
                var confirm=menu.transform.Find("GenericConfirm").GetComponent<Button>();
                Check(confirm.interactable==(ui.OptionIndex("confirm-cards")>=0),"Confirmation gate differs from Forge");
                Check(menu.transform.Find("GenericProgress").GetComponent<Text>().text.Contains(view.request.selectedCount.ToString()),"Selected count absent");
                var choices=menu.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("GenericOption_")).ToArray();
                Check(choices.Length==view.request.options.Count(o=>o.id.StartsWith("select:")),"Options omitted");
                Check(menu.GetComponentsInChildren<Text>().All(t=>!t.resizeTextForBestFit),"Generic choices auto-resize fonts");
                ScreenCapture.CaptureScreenshot(Output+"/"+language+"-"+name+".png");yield return new WaitForEndOfFrame();
                var chosen=choices.First(b=>b.interactable);int index=int.Parse(chosen.name.Substring("GenericOption_".Length));
                var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,chosen.transform.TransformPoint(((RectTransform)chosen.transform).rect.center)),button=PointerEventData.InputButton.Left};
                var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==chosen,"Generic choice raycast blocked");
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);chosen.onClick.Invoke();
                Check(calls==1&&last==index,"Duplicate guard or option index failed");
                ui.Present(view,view.seat,false,submit,null,"");yield return null;
                Check(menu.GetComponentsInChildren<Button>().All(b=>!b.interactable),"Non-acting viewer can choose");
                ui.Choose(index,view.request.requestId);Check(calls==1,"Non-acting viewer submitted");
                ui.Present(view,view.seat,true,submit,null,"");yield return null;
                ui.Choose(index,view.request.requestId-1);Check(calls==1,"Stale request submitted while actionable");
                ui.Choose(index,view.request.requestId);Check(calls==2,"Current request blocked after stale rejection");
            }
            // Long descriptions and many choices use the same panel without hiding a required action.
            var large=new ForgeSeatView{seat=1,phase="MAIN1",awaitingInput=true,players=Array.Empty<ForgePlayerView>(),
                request=new ForgeRequest{requestId=900000,seat=1,kind="ChooseOptions",message="Long choice regression",minCount=1,maxCount=1,
                    options=Enumerable.Range(0,18).Select(i=>new ForgeOption{id="select:"+i,label=string.Concat(Enumerable.Repeat("完整选项 description 選択肢 ",12))}).ToArray()}};
            ui.Present(large,1,true,_=>{},null,"");yield return null;Canvas.ForceUpdateCanvases();
            var scroll=GameObject.Find("CardAbilityChoices").GetComponentsInChildren<ScrollRect>().First();
            Check(scroll.content.rect.height>scroll.viewport.rect.height,"Long choices do not scroll");
            Check(!GameObject.Find("CardAbilityChoices").transform.Find("GenericConfirm").GetComponent<Button>().interactable,"Client invented confirmation");
        }
    }
}
#endif
