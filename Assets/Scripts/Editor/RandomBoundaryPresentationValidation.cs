#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class RandomBoundaryPresentationValidation
{
    const string Output=".tools/random-boundaries-ui",Input="Tools/Validation/Reports/RandomBoundaries/";
    static bool running;
    static void Check(bool b,string message){if(!b)throw new Exception(message);}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Input+name+".json"));
    public static void Run()
    {
        if(running)return;if(!Application.isPlaying)throw new Exception("Enter IntegratedBattlefield Play Mode first.");
        running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("RandomBoundaryValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator iterator)
    {
        string language=GamePreferences.Language;var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=banner!=null&&banner.enabled;
        if(banner!=null)banner.enabled=false;
        try {
            while(true){object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: real native boundary request replay in zh-CN/en-US/ja-JP; mana cancellation pointer/native index, localized payment meaning, actionable stale and duplicate guards, nonacting viewer, next request enabled after cancellation, explicit keep confirmation without mana. Native Fly animated/EOT/zone/recast snapshots: creature stats and flying removed at EOT, Whacker stats retained then new ETB stats, Xenosquirrels death and no phantom choices. No actual network/animation timing claim; effects proved separately by Test-RandomBoundaries.py.");
        }finally{running=false;GamePreferences.SetLanguage(language);if(banner!=null)banner.enabled=enabled;}
    }
    static IEnumerator Rendered(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static IEnumerator Shot(string name){yield return new WaitForSecondsRealtime(.35f);yield return Rendered();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForEndOfFrame();}
    static void Click(Button button)
    {
        var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Boundary button raycast blocked: "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool changed=GamePreferences.Language!=language;GamePreferences.SetLanguage(language);float end=Time.realtimeSinceStartup+8;
            while((!GamePreferences.Ready||changed&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<end)yield return null;
            Check(GamePreferences.Ready&&(!changed||revision!=GamePreferences.Revision),"Language not ready");
            foreach(string test in new[]{"monitor-no-mana","monitor-cancel-retry"}) {
                var view=Read(test+"-cancel-mana");int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};
                ui.Present(null,1,false,null,null,"");ui.Present(view,1,true,submit,null,"");yield return Rendered();
                int cancel=Array.FindIndex(view.request.options,o=>o.id=="cancel");Check(cancel>=0,"Native cost cannot cancel");
                var button=GameObject.Find("Option_"+cancel).GetComponent<Button>();
                Check(button.interactable&&button.GetComponentInChildren<Text>().text==(language=="zh-CN"?"取消支付":language=="ja-JP"?"支払いをキャンセル":"Cancel payment"),"In-resolution payment incorrectly says cancel the spell or wrong locale");
                var prompt=GameObject.Find("Prompt").GetComponent<Text>();
                Check(prompt.text==(language=="zh-CN"?"支付费用 {1}\n地牌产费／取消支付":language=="ja-JP"?"コスト {1} を支払う\n土地をタップ／キャンセル":"Pay {1}\nTap lands or cancel."),"Payment prompt invents casting status or omits native cost/locale");
                Check(!prompt.resizeTextForBestFit&&prompt.preferredHeight<=prompt.rectTransform.rect.height,"Payment prompt is clipped or shrinks");
                ui.Choose(cancel,view.request.requestId-1);Check(calls==0,"Actionable stale payment accepted");yield return Shot(language+"-"+test+"-cancel");
                Click(button);button.onClick.Invoke();Check(calls==1&&last==cancel,"Cancel index or duplicate guard wrong");
                var next=Read(test+"-reroll-2");calls=0;ui.Present(next,1,true,submit,null,"");yield return Rendered();
                var menu=GameObject.Find("CardAbilityChoices");Check(menu!=null,"Cancellation cannot return to reroll");
                var confirm=menu.GetComponentsInChildren<Button>().Single(b=>b.name=="GenericConfirm");Check(confirm.interactable,"Next native keep request remains disabled");
                int expected=Array.FindIndex(next.request.options,o=>o.id=="confirm-cards");Check(expected>=0,"Keep missing native confirmation");
                yield return Shot(language+"-"+test+"-return");Click(confirm);Check(calls==1&&last==expected,"Keep confirmation native index wrong");
                ui.Present(null,1,false,null,null,"");ui.Present(view,1,false,submit,null,"");yield return Rendered();
                Check(!GameObject.Find("Option_"+cancel).GetComponent<Button>().interactable,"Nonacting player can cancel payment");ui.Choose(cancel,view.request.requestId);Check(calls==1,"Nonacting player submitted cost choice");
            }
            foreach(string state in new[]{"animated","expired","returned","recast"}) {
                var view=Read("swap-duration-"+state);ui.Present(null,1,false,null,null,"");ui.Present(view,1,true,_=>{},null,"");yield return Rendered();
                var fly=view.players[0].battlefield.Single(c=>c.name=="Ebony Fly");var root=GameObject.Find("Card_"+fly.id);Check(root!=null,"Fly battlefield representation absent");
                var stats=root.GetComponentsInChildren<Text>().Where(t=>t.name=="Value"&&t.transform.parent.name=="Stats").ToArray();
                Check(stats.Length==(fly.creature?1:0),"Expired Fly still displays creature body");
                Check(root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Keyword_FLYING")==fly.creature,"Fly gained flying persists after EOT or is missing");
                foreach(var card in view.players[0].battlefield.Where(c=>c.name=="Vedalken Squirrel-Whacker"||c.name=="Ebony Fly"&&c.creature)) {
                    var value=GameObject.Find("Card_"+card.id).GetComponentsInChildren<Text>().Single(t=>t.name=="Value"&&t.transform.parent.name=="Stats");
                    Check(Regex.Replace(value.text,"<[^>]*>","")==card.power+"/"+card.toughness,"Body differs from native state after expiry/recast");
                }
                yield return Shot(language+"-swap-"+state);
            }
            foreach(string test in new[]{"xeno-last","night-no-pay","night-limit","monitor-limit"}) {
                string label=test.EndsWith("limit")?"resolved-3":"resolved-1";var view=Read(test+"-"+label);
                ui.Present(null,1,false,null,null,"");ui.Present(view,1,true,_=>{},null,"");yield return Rendered();
                Check(GameObject.Find("CardAbilityChoices")==null,"Completed resource boundary leaves a phantom decision");
                if(test=="xeno-last") {
                    var dead=view.players[0].graveyardCards.Single(c=>c.name=="Xenosquirrels");Check(GameObject.Find("Card_"+dead.id)==null,"Dead Xenosquirrels still has a battlefield object");
                }
                yield return Shot(language+"-"+test+"-resolved");
            }
            ui.Present(null,1,false,null,null,"");yield return Rendered();
        }
    }
}
#endif
