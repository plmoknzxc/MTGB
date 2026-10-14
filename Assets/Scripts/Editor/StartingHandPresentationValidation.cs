#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class StartingHandPresentationValidation
{
    const string Output=".tools/starting-hand-ui";
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter IntegratedBattlefield Play Mode first.");
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("StartingHandValidation").AddComponent<ForgeBattlefieldController>();
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
            File.WriteAllText(Output+"/result.txt","PASS: actual Backup Plan and London mulligan requests in zh-CN/en-US/ja-JP; separate next/accept controls, London selection/confirmation gates and localized tally, raycast, IDs, independently actionable stale and viewer/duplicate guards, hand refresh; actual adapter zero-count and display-label requests. Engine hand recycling and callback return values are separate assertions.");
        } finally {GamePreferences.SetLanguage(original);}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool changing=GamePreferences.Language!=language;
            GamePreferences.SetLanguage(language);float deadline=Time.realtimeSinceStartup+8;
            while(changing&&GamePreferences.Revision==revision&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(!changing||GamePreferences.Revision!=revision,"Language resources not loaded");
            foreach(string name in new[]{"accept-hand-0","rotate-two-hand-0","rotate-two-hand-1","rotate-two-hand-2","rotate-cycle-hand-2","zero-choices-0","display-choices-1"}) {
                var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/OpeningContracts/"+name+".json"));
                int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};
                ui.Present(view,1,true,submit,null,"");yield return new WaitForSecondsRealtime(.18f);Canvas.ForceUpdateCanvases();
                Button button;
                if(view.request.kind=="InputChooseStartingHand") {
                    var menu=GameObject.Find("MulliganChoices");Check(menu!=null&&menu.activeInHierarchy,"Extra hand controls missing");
                    var next=menu.transform.Find("Mulligan_ok").GetComponent<Button>();
                    var accept=menu.transform.Find("Mulligan_cancel").GetComponent<Button>();
                    Check(next.interactable&&accept.interactable,"Next/accept not independently actionable");
                    Check(next.GetComponentInChildren<Text>().text==LocaleText(language,"查看下一组","View next hand","次の手札を見る"),"Next label is wrong");
                    Check(accept.GetComponentInChildren<Text>().text==LocaleText(language,"保留这组起手","Accept this hand","この手札を選ぶ"),"Accept label mistaken for cancel");
                    Check(menu.GetComponentsInChildren<Text>().All(t=>!t.resizeTextForBestFit),"Extra hand fonts resize");
                    button=name.EndsWith("hand-2")?accept:next;
                    Check(view.players[0].hand.Length==7,"Hand data omitted");
                    var hand=GameObject.Find("PlayerHandRoot").transform;
                    var visible=Enumerable.Range(0,hand.childCount).Select(i=>hand.GetChild(i)).Where(t=>t.gameObject.activeInHierarchy&&t.name.StartsWith("Card_")).Select(t=>t.name).OrderBy(n=>n).ToArray();
                    Check(visible.SequenceEqual(view.players[0].hand.Select(c=>"Card_"+c.id).OrderBy(n=>n)),"Displayed hand identities did not refresh");
                } else {
                    var menu=GameObject.Find("CardAbilityChoices");Check(menu!=null&&menu.activeInHierarchy,"Adapter selector missing");
                    button=menu.transform.Find("GenericConfirm").GetComponent<Button>();Check(button.interactable,"Legal confirmation hidden");
                    if(name.StartsWith("zero"))Check(menu.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("GenericOption_")).All(b=>!b.interactable),"Zero cap offers selectable objects");
                }
                ScreenCapture.CaptureScreenshot(Output+"/"+language+"-"+name+".png");yield return new WaitForEndOfFrame();
                int expected=view.request.kind=="InputChooseStartingHand"?ui.OptionIndex(button.name=="Mulligan_ok"?"ok":"cancel"):ui.OptionIndex("confirm-cards");
                var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
                var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Extra-hand raycast blocked");
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);button.onClick.Invoke();
                Check(calls==1&&last==expected,"Wrong option or duplicate submitted");
                ui.Present(view,1,false,submit,null,"");yield return null;
                var activeMenu=GameObject.Find(view.request.kind=="InputChooseStartingHand"?"MulliganChoices":"CardAbilityChoices");
                Check(activeMenu.GetComponentsInChildren<Button>().All(b=>!b.interactable),"Non-acting viewer can submit");
                ui.Choose(expected,view.request.requestId);Check(calls==1,"Non-acting viewer submitted");
                ui.Present(view,1,true,submit,null,"");yield return null;
                ui.Choose(expected,view.request.requestId-1);Check(calls==1,"Stale request submitted while actionable");
                ui.Choose(expected,view.request.requestId);Check(calls==2,"Actionable current request is blocked");
            }
            foreach(int selected in new[]{0,1,2}) {
                var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText($"Tools/Validation/Reports/MulliganRounds/rounds-2-2-2-seat-2-selected-{selected}.json"));
                int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};
                ui.Present(null,2,false,null,null,"");ui.Present(view,2,true,submit,null,"");yield return new WaitForSecondsRealtime(.18f);Canvas.ForceUpdateCanvases();
                var menu=GameObject.Find("MulliganChoices");Check(menu!=null&&menu.activeInHierarchy,"London controls absent");
                Check(view.request.remainingCount==2-selected,"Wrong London replay tally");
                var prompt=menu.transform.Find("Prompt").GetComponent<Text>();
                string tally=LocaleText(language,$"已选 {selected} 张，还需 {2-selected} 张",$"Selected {selected}; {2-selected} remaining",$"選択済み {selected} 枚・あと {2-selected} 枚");
                Check(prompt.text.Contains(tally)&&!prompt.resizeTextForBestFit,"London tally/locale/font wrong");
                foreach(var card in view.request.options.Where(o=>o.id.StartsWith("card:")&&o.label.StartsWith("[已选"))) {
                    var mark=GameObject.Find("Card_"+card.id.Substring(5)).transform.Find("Visual/Selected").GetComponent<Text>();
                    Check(mark.text==LocaleText(language,"回牌库底","Return to bottom","一番下に戻す")&&!mark.resizeTextForBestFit,"London selected card mark/locale wrong");
                }
                Check((ui.OptionIndex("ok")>=0)==(selected==2),"London confirmation appears before selection complete");
                int index=selected==2?ui.OptionIndex("ok"):Array.FindIndex(view.request.options,o=>o.id.StartsWith("card:")&&!o.label.StartsWith("[已选"));
                Check(index>=0,"London legal selection absent");
                var button=GameObject.Find(selected==2?"Mulligan_ok":"Card_"+view.request.options[index].id.Substring(5)).GetComponent<Button>();
                if(selected==2)Check(button.GetComponentInChildren<Text>().text==LocaleText(language,"确认放回所选牌","Return selected cards","選択したカードを戻す"),"London confirm locale wrong");
                var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};bool clicked=false;
                var rect=((RectTransform)button.transform).rect;
                foreach(float x in new[]{0f,-.45f,.45f}) {
                    pointer.position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(rect.center+new Vector2(rect.width*x,0)));
                    var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                    if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=button)continue;
                    ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);clicked=true;break;
                }
                Check(clicked&&calls==1&&last==index,"London action blocked or original index lost");
                ScreenCapture.CaptureScreenshot(Output+$"/{language}-london-{selected}.png");yield return new WaitForEndOfFrame();
            }
        }
    }
    static string LocaleText(string language,string zh,string en,string ja)=>language=="zh-CN"?zh:language=="ja-JP"?ja:en;
}
#endif
