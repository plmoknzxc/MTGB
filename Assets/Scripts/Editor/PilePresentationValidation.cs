#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PilePresentationValidation
{
    const string Output=".tools/piles-ui";
    static bool running;
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static GameObject Menu=>GameObject.Find("PileSelection");
    static Button Find(string name)=>Menu.GetComponentsInChildren<Button>().First(b=>b.name==name);
    public static void Run()
    {
        if(running)return;if(!Application.isPlaying)throw new Exception("Enter IntegratedBattlefield Play Mode first.");
        running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("PileValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator iterator)
    {
        string original=GamePreferences.Language;
        try {
            while(true) {
                object next;
                try{if(!iterator.MoveNext())break;next=iterator.Current;}
                catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}
                yield return next;
            }
            File.WriteAllText(Output+"/result.txt","PASS: real two-pile requests in zh-CN/en-US/ja-JP, both seats, empty/all/one-hidden/both-hidden/mixed-owner piles; center raycast and whole-pile explicit confirmation, native index, duplicate/stale/viewer guards, stable same-request selection, fixed fonts, local art or missing labels and completed cleanup. Real Portal search/select/confirmation restricted to chosen pile; twenty-card scrolling is a separate synthetic adapter case. Native effects remain Test-Piles.py.");
        }finally{running=false;GamePreferences.SetLanguage(original);}
    }
    static IEnumerator Rendered(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Pile raycast blocked: "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Shot(string name){ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForEndOfFrame();}
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool changing=GamePreferences.Language!=language;
            GamePreferences.SetLanguage(language);float deadline=Time.realtimeSinceStartup+8;
            while((!GamePreferences.Ready||changing&&GamePreferences.Revision==revision)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(GamePreferences.Ready&&(!changing||GamePreferences.Revision!=revision),"Locale not ready");
            foreach(string test in new[]{"fof-left","fof-empty","fof-all","fof-seat2","favor-hidden","parley-right","portal-left","portal-right","portal-empty"}) {
                var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/Piles/"+test+"-choice.json"));
                int calls=0,last=-1;Action<int> submit=index=>{calls++;last=index;};
                ui.Present(null,view.seat,false,null,null,"");ui.Present(view,view.seat,true,submit,null,"");yield return Rendered();
                Check(Menu!=null,"Missing two-pile menu");
                Check(!Find("PileConfirm").interactable,"Pile confirmation enabled without selection");
                Check(Menu.GetComponentsInChildren<Button>().Count()==3,"Cards masquerade as selectable pile options");
                Check(Menu.GetComponentsInChildren<Text>().All(t=>!t.resizeTextForBestFit),"Pile font resizes");
                Check(Menu.transform.Find("Title").GetComponent<Text>().text==(language=="zh-CN"?"选择一堆牌":language=="ja-JP"?"束を選択":"Choose a pile"),"Pile title wrong language");
                foreach(var pile in view.request.piles) {
                    var column=Menu.transform.Find("Pile_"+pile.pileId);
                    Check(column!=null,"Pile column missing");
                    var content=column.Find("PileScroll/Content");
                    Check(column.Find("Count").GetComponent<Text>().text.Contains(pile.cardCount.ToString()),"Pile count missing");
                    var arts=content.GetComponentsInChildren<Image>().Where(i=>i.name.StartsWith("PileCard_")).ToArray();
                    Check(arts.Length==(pile.concealed?0:pile.cardCount),"Hidden identity displayed or visible card omitted");
                    foreach(var art in arts) {
                        Check(art.sprite!=null||art.transform.Find("MissingArt")?.GetComponent<Text>().text.Length>0,"Local art absent without label");
                        int id=int.Parse(art.name.Substring(9));var card=pile.cards.First(c=>c.id==id);
                        Check(art.transform.Find("CardName").GetComponent<Text>().text==GamePreferences.CardFaceName(card.name),"Wrong localized pile name");
                    }
                    Check(!pile.concealed||content.Find("HiddenPile")!=null,"No face-down presentation");
                    Check(pile.cardCount!=0||pile.concealed||content.Find("EmptyPile")!=null,"Empty pile absent");
                }
                yield return Shot(language+"-"+test+"-initial");
                int expected=test.EndsWith("right")?1:0;var select=Find("SelectPile_"+expected);Click(select);
                Check(calls==0&&Find("PileConfirm").interactable,"Pile choice submitted before confirmation");
                ui.Present(view,view.seat,true,submit,null,"");yield return Rendered();
                Check(Find("PileConfirm").interactable&&Find("SelectPile_"+expected)==select,"Same request lost selection/rebuilt the menu");
                ui.Choose(expected,view.request.requestId-1);Check(calls==0,"Stale request accepted while actionable");
                yield return Shot(language+"-"+test+"-selected");
                var confirm=Find("PileConfirm");Click(confirm);confirm.onClick.Invoke();Check(calls==1&&last==expected,"Wrong native pile index or duplicate submission");
                ui.Present(view,view.seat,false,submit,null,"");yield return Rendered();
                Check(Menu.GetComponentsInChildren<Button>().All(b=>!b.interactable),"Viewer can select a pile");ui.Choose(expected,view.request.requestId);Check(calls==1,"Viewer submitted");
                var resolved=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/Piles/"+test+"-resolved.json"));
                ui.Present(resolved,resolved.seat,true,submit,null,"");yield return Rendered();Check(Menu==null,"Completed pile picker remained visible");
            }
            foreach(string test in new[]{"portal-left","portal-right"}) {
                var search=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/Piles/"+test+"-search.json"));
                int calls=0,last=-1;Action<int> submit=index=>{calls++;last=index;};
                ui.Present(null,1,false,null,null,"");ui.Present(search,1,true,submit,null,"");yield return Rendered();
                var overlay=GameObject.Find("CardSelectionOverlay");
                var options=search.request.options.Where(o=>o.card!=null&&!string.IsNullOrEmpty(o.card.name)).ToArray();
                Check(overlay.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("ChoiceCard_"))==options.Length,"Portal UI search candidates differ from native chosen pile");
                int expected=Array.FindIndex(search.request.options,o=>o.id==options[0].id);
                var tile=overlay.GetComponentsInChildren<Transform>().First(t=>t.name=="ChoiceCard_"+options[0].card.id);
                var choose=tile.Find("Artwork").GetComponent<Button>();Click(choose);Check(calls==1&&last==expected,"Portal search card index mismatch");yield return Shot(language+"-"+test+"-search");
                var selected=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/Piles/"+test+"-search-selected.json"));
                ui.Present(selected,1,true,submit,null,"");yield return Rendered();
                int confirm=Array.FindIndex(selected.request.options,o=>o.id=="ok");
                var confirmButton=overlay.GetComponentsInChildren<Button>().First(b=>b.name=="SelectionAction_"+confirm);Click(confirmButton);Check(calls==2&&last==confirm,"Portal search native confirmation mismatch");yield return Shot(language+"-"+test+"-search-confirm");
            }
            // Large-pile layout stress remains a separate synthetic adapter case.
            var sample=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/Piles/fof-left-choice.json"));
            sample.request.requestId=980101;
            var original=sample.request.piles[0].cards[0];sample.request.piles[0].cards=Enumerable.Range(0,20).Select(i=>{var card=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(original));card.id=900000+i;return card;}).ToArray();sample.request.piles[0].cardCount=20;
            ui.Present(null,1,false,null,null,"");ui.Present(sample,1,true,null,null,"");yield return Rendered();
            var scroll=Menu.transform.Find("Pile_1/PileScroll").GetComponent<ScrollRect>();
            Check(scroll.content.rect.height>scroll.viewport.rect.height,"Large pile does not scroll");float before=scroll.content.anchoredPosition.y;scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();Check(scroll.content.anchoredPosition.y>before,"Pile cannot scroll to its end");yield return Shot(language+"-synthetic-scroll-bottom");
        }
        ui.Present(null,1,false,null,null,"");
    }
}
#endif
