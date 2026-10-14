#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class RandomPresentationValidation
{
    const string Output=".tools/random-mechanics-ui";
    const string Input="Tools/Validation/Reports/RandomMechanics/";
    static bool running;
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Input+name+".json"));
    public static void Run()
    {
        if(running)return;if(!Application.isPlaying)throw new Exception("Enter IntegratedBattlefield Play Mode first.");
        running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("RandomValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator iterator)
    {
        string original=GamePreferences.Language;
        // Real requests are replayed without a network service. Hide only the
        // no-match editor banner, as in a real active match; preserve gameplay UI.
        var notice=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool noticeEnabled=notice!=null&&notice.enabled;
        if(notice!=null)notice.enabled=false;
        try {
            while(true) {
                object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}
                catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}
                yield return next;
            }
            File.WriteAllText(Output+"/result.txt","PASS: real native random requests replayed in zh-CN/en-US/ja-JP; direct native indexes, pointer center raycast, binary heads/tails and increase/decrease, optional roll/swap, mandatory Thumb choice, reroll selection and native confirmation, duplicate/stale/viewer guards. Nonblocking native results, fixed fonts, bounded scroll history, expiry and same-ID deduplication; private notices excluded from spectator projection and hidden source absent (adapter contract fixtures). Real rules remain Test-RandomMechanics.py; not an actual network test.");
        }finally{running=false;GamePreferences.SetLanguage(original);if(notice!=null)notice.enabled=noticeEnabled;}
    }
    static IEnumerator Rendered(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Random raycast blocked: "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Shot(string name){yield return new WaitForSecondsRealtime(.35f);yield return Rendered();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForEndOfFrame();}
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool changing=GamePreferences.Language!=language;
            GamePreferences.SetLanguage(language);float deadline=Time.realtimeSinceStartup+8;
            while((!GamePreferences.Ready||changing&&GamePreferences.Revision==revision)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(GamePreferences.Ready&&(!changing||GamePreferences.Revision!=revision),"Locale not ready");
            foreach(string name in new[]{"coin-win-call-choice","coin-lose-call-choice","thumb-win-thumb-choice","xeno-inc-roll-choice","xeno-inc-increment-choice","xeno-dec-increment-choice","swap-power-roll-choice","swap-power-swap-choice","swap-toughness-swap-choice","night-dec-increment-choice"}) {
                var view=Read(name);int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};
                ui.Present(null,view.seat,false,null,null,"");ui.Present(view,view.seat,true,submit,null,"");yield return Rendered();
                var menu=GameObject.Find("CardAbilityChoices");Check(menu!=null,"Value menu absent");
                var buttons=menu.GetComponentsInChildren<Button>();Check(buttons.Length==view.request.options.Length,"Native random option omitted/invented");
                Check(menu.GetComponentsInChildren<Text>().All(t=>!t.resizeTextForBestFit),"Random choice font changes");
                Check(menu.transform.Find("ValueTitle").GetComponent<Text>().text.Length>0,"Random prompt absent");
                foreach(var option in view.request.options) {
                    int index=Array.IndexOf(view.request.options,option);var text=buttons.First(b=>b.name=="ValueOption_"+index).GetComponentInChildren<Text>();
                    if(option.label=="heads"||option.label=="tails")Check(text.text==(language=="zh-CN"?(option.label=="heads"?"正面":"反面"):language=="ja-JP"?(option.label=="heads"?"表":"裏"):(option.label=="heads"?"Heads":"Tails")),"Coin option wrong language");
                    if(option.label=="Increase"||option.label=="Decrease")Check(text.text==(language=="zh-CN"?(option.label=="Increase"?"增加":"减少"):language=="ja-JP"?(option.label=="Increase"?"増加":"減少"):option.label),"Increment option wrong language");
                }
                ui.Choose(0,view.request.requestId-1);Check(calls==0,"Stale random choice accepted while actionable");
                var dismiss=GameObject.Find("DismissAbilityChoices").GetComponent<ForgeChoiceDismiss>();
                dismiss.Dismiss();Check(calls==0,"Outside dismissal silently chose a binary outcome");
                yield return Shot(language+"-"+name);
                int expected=name.Contains("toughness")||name.StartsWith("coin-win")||name.StartsWith("xeno-dec")||name.StartsWith("night-dec")?1:0;
                var choose=buttons.First(b=>b.name=="ValueOption_"+expected);Click(choose);choose.onClick.Invoke();Check(calls==1&&last==expected,"Random index/duplicate mismatch");
                ui.Present(view,view.seat,false,submit,null,"");yield return Rendered();Check(menu.GetComponentsInChildren<Button>().All(b=>!b.interactable),"Viewer can choose random result");ui.Choose(expected,view.request.requestId);Check(calls==1,"Viewer submitted random result");
            }
            var reroll=Read("monitor-reroll-reroll-choice");int selections=0,indexSelected=-1;
            ui.Present(null,1,false,null,null,"");ui.Present(reroll,1,true,i=>{selections++;indexSelected=i;},null,"");yield return Rendered();
            var generic=GameObject.Find("CardAbilityChoices");int candidate=Array.FindIndex(reroll.request.options,o=>o.id.StartsWith("select:"));
            Click(generic.GetComponentsInChildren<Button>().First(b=>b.name=="GenericOption_"+candidate));Check(selections==1&&indexSelected==candidate,"Reroll candidate index wrong");
            var selected=Read("monitor-reroll-reroll-selected");ui.Present(selected,1,true,i=>{selections++;indexSelected=i;},null,"");yield return Rendered();
            int confirmation=Array.FindIndex(selected.request.options,o=>o.id=="confirm-cards");Click(generic.GetComponentsInChildren<Button>().First(b=>b.name=="GenericConfirm"));Check(selections==2&&indexSelected==confirmation,"Native reroll confirmation missing");yield return Shot(language+"-reroll-confirm");
            foreach(string test in new[]{"dice-low","dice-high","pixie","monitor-reroll","xeno-dec","swap-toughness","coin-win","coin-lose","mana-clash"}) {
                var resolved=Read(test+"-resolved");int calls=0;
                ui.Present(null,resolved.seat,false,null,null,"");ui.Present(resolved,resolved.seat,true,i=>calls++,null,"");yield return Rendered();
                var toast=GameObject.Find("RandomResultAnimation");Check(toast!=null&&toast.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),"Result animation blocks game input");
                Check(GameObject.Find("ResultToast")==null,"Random result still opens a text banner");
                var graphic=toast.GetComponentInChildren<RandomResultGraphic>();Check(graphic!=null&&!string.IsNullOrEmpty(graphic.Outcome),"Native visual result absent");
                var first=resolved.notices.First();
                string expected=first.kind=="coin"?first.value:System.Text.RegularExpressions.Regex.Match(first.value,@"rolled \[?([0-9]+)").Groups[1].Value;
                Check(graphic.Outcome==expected,"Animation replaced native outcome (including modified zero)");
                yield return new WaitForSecondsRealtime(1.2f);Canvas.ForceUpdateCanvases();
                Check(graphic.canvasRenderer.GetMesh()?.vertexCount>0,"Random result has no visible mesh");
                yield return Rendered();ScreenCapture.CaptureScreenshot(Output+"/"+language+"-"+test+"-animation.png");yield return new WaitForEndOfFrame();
                var publicView=OnlineSpectatorView.Create(new OnlinePlayerViewState{forge=resolved},3).forge;Check(publicView.notices.Length==resolved.notices.Count(n=>n.publicNotice),"Public random result lost to spectator");
                Click(GameObject.Find("ResultHistoryButton").GetComponent<Button>());Check(calls==0,"Opening result history submitted a game choice");
                var history=GameObject.Find("ResultHistory");Check(history.GetComponentsInChildren<Text>().Count(t=>t.name.StartsWith("Notice_"))==resolved.notices.Length,"Result history omitted entries");
                yield return Shot(language+"-"+test+"-history");Click(history.GetComponentsInChildren<Button>().First(b=>b.name=="CloseHistory"));Check(calls==0,"Closing history submitted a game choice");
                if(test=="dice-low") {
                    yield return new WaitForSecondsRealtime(4.1f);Check(!toast.activeSelf,"Notification failed to expire");
                    ui.Present(resolved,resolved.seat,true,i=>calls++,null,"");yield return Rendered();Check(!toast.activeSelf,"Same notice reappears/restarts on duplicate snapshot");
                }
            }
            var owner=Read("notice-private-resolved");var other=Read("notice-private-other");
            var hidden=other.notices[0].sourceCard;
            Check(other.notices.Length==1&&(hidden==null||hidden.id==0&&hidden.ownerSeat==0&&string.IsNullOrEmpty(hidden.name)&&string.IsNullOrEmpty(hidden.printingKey)&&string.IsNullOrEmpty(hidden.currentRules)&&string.IsNullOrEmpty(hidden.knownFaceName))&&other.notices[0].title=="","Private hidden source disclosed");
            var observer=OnlineSpectatorView.Create(new OnlinePlayerViewState{forge=owner},3).forge;
            Check(observer.notices.Length==0&&!JsonUtility.ToJson(observer).Contains("Owner-only value"),"Private notice leaked to spectator");
            var cap=Read("notice-cap-resolved");ui.Present(null,1,false,null,null,"");ui.Present(cap,1,true,null,null,"");yield return Rendered();var historyButton=GameObject.Find("ResultHistoryButton").GetComponent<Button>();Click(historyButton);
            Check(GameObject.Find("ResultValue").GetComponent<Text>().text==(language=="zh-CN"?"提示":language=="ja-JP"?"通知":"Info"),"Ordinary private notice is incorrectly presented as an error");
            var capped=GameObject.Find("ResultHistory");var scroll=capped.GetComponentInChildren<ScrollRect>();
            Check(capped.GetComponentsInChildren<Text>().Count(t=>t.name.StartsWith("Notice_"))==16&&scroll.content.rect.height>scroll.viewport.rect.height,"Bounded result history cannot scroll");scroll.verticalNormalizedPosition=0;yield return Rendered();yield return Shot(language+"-bounded-private-history");
            ui.Present(Read("coin-win-call-choice"),1,true,null,null,"");yield return Rendered();Check(!capped.activeSelf&&(!historyButton.gameObject.activeSelf||!historyButton.interactable),"History overlaps mandatory decision");
            ui.Present(null,1,false,null,null,"");yield return Rendered();Check(GameObject.Find("ResultToast")==null&&GameObject.Find("ResultHistoryButton")==null,"Old session notifications remain");
        }
    }
}
#endif
