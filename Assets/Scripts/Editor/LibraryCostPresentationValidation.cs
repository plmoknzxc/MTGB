#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class LibraryCostPresentationValidation
{
    const string Output=".tools/library-costs-ui",Input="Tools/Validation/Reports/LibraryCosts/";
    static bool running;
    static void Check(bool b,string message){if(!b)throw new Exception(message);}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Input+name+".json"));
    static ForgeSeatView WireView(ForgeWireMessage m){var v=m.views[m.request.seat-1];v.request=m.request;v.awaitingInput=true;return v;}
    public static void Run()
    {
        if(running)return;Check(Application.isPlaying,"Enter IntegratedBattlefield Play Mode first.");
        running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("LibraryCostValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator iterator)
    {
        string language=GamePreferences.Language;var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=banner!=null&&banner.enabled;
        if(banner!=null)banner.enabled=false;
        try {
            while(true){object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: native same-graveyard upkeep replay in zh-CN/en-US/ja-JP; legal player selector/confirm/cancel; exact source/card identities, native count/confirmation gate, stable card tiles, real pointer index, actionable stale/duplicate and nonacting guards; opponent orders own cards; fixed localized payment instructions fit existing text scale; paid/sacrificed/age-two end state clears choices. Engine effects are separately proved by Test-LibraryCosts.py; no actual network or animation claim.");
        }finally{running=false;GamePreferences.SetLanguage(language);if(banner!=null)banner.enabled=enabled;}
    }
    static IEnumerator Rendered(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static IEnumerator Shot(string name){yield return new WaitForSecondsRealtime(.3f);yield return Rendered();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForEndOfFrame();}
    static void Click(Button button)
    {
        var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Library cost pointer blocked: "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool changed=GamePreferences.Language!=language;GamePreferences.SetLanguage(language);float end=Time.realtimeSinceStartup+8;
            while((!GamePreferences.Ready||changed&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<end)yield return null;
            Check(GamePreferences.Ready&&(!changed||revision!=GamePreferences.Revision),"Language resources not ready");
            foreach(string test in new[]{"own","opponent","seat-two","cancel-player"}) {
                var view=Read(test+"-players-1");int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};
                ui.Present(null,view.seat,false,null,null,"");ui.Present(view,view.seat,true,submit,null,"");yield return Rendered();
                Check(view.request.selectionPurpose=="chooseLibraryCostOwner"&&view.request.sourceCard.name=="Jötun Grunt","Wrong native cost/source metadata");
                var prompt=GameObject.Find("Prompt").GetComponent<Text>();Check(!prompt.resizeTextForBestFit&&prompt.preferredHeight<=prompt.rectTransform.rect.height,"Player-payment instruction clipped or shrinks");
                int index=Array.FindIndex(view.request.options,o=>o.id=="cancel");Check(index>=0,"Cannot decline graveyard choice");
                var cancel=GameObject.Find("Option_"+index).GetComponent<Button>();Check(cancel.GetComponentInChildren<Text>().text==(language=="zh-CN"?"取消支付":language=="ja-JP"?"支払いをキャンセル":"Cancel payment"),"Wrong cancellation meaning/locale");
                ui.Choose(index,view.request.requestId-1);Check(calls==0,"Stale player cost submitted");yield return Shot(language+"-"+test+"-players");Click(cancel);cancel.onClick.Invoke();Check(calls==1&&last==index,"Cancel mapping/duplicate guard");
                ui.Present(null,view.seat,false,null,null,"");ui.Present(view,view.seat,false,submit,null,"");yield return Rendered();
                Check(!GameObject.Find("Option_"+index).GetComponent<Button>().interactable,"Nonacting player can pay");ui.Choose(index,view.request.requestId);Check(calls==1,"Nonacting submission");
                if(test!="cancel-player") {
                    calls=0;ui.Present(null,view.seat,false,null,null,"");ui.Present(view,view.seat,true,submit,null,"");yield return Rendered();
                    int selectedSeat=test=="opponent"?2:view.seat;var player=view.players.Single(p=>p.seat==selectedSeat);
                    int pi=Array.FindIndex(view.request.options,o=>o.id=="player:"+player.targetId);Check(pi>=0,"Native legal player missing");
                    Click(GameObject.Find(selectedSeat==view.seat?"PlayerPlate":"OpponentPlate").GetComponent<Button>());Check(calls==1&&last==pi,"Player selector pointer/native index wrong");
                    var selected=Read(test+"-players-selected-1");calls=0;ui.Present(selected,selected.seat,true,submit,null,"");yield return Rendered();
                    int ok=Array.FindIndex(selected.request.options,o=>o.id=="ok");Check(ok>=0,"Selected player cannot confirm");
                    yield return Shot(language+"-"+test+"-player-confirm");Click(GameObject.Find("SkipCombatButton").GetComponent<Button>());Check(calls==1&&last==ok,"Player choice confirmation cannot submit");
                }
            }
            var wire=File.ReadLines(Input+"own.jsonl").Select(JsonUtility.FromJson<ForgeWireMessage>).Where(m=>m.type=="request"&&m.request?.selectionPurpose=="putLibraryCost").ToArray();
            Check(wire.Length==3,"Unexpected native two-card payment requests");GameObject panel=null,cardTile=null;int font=-1;
            ui.Present(null,1,false,null,null,"");
            foreach(var m in wire) {
                var view=WireView(m);int calls=0,last=-1;
                ui.Present(view,view.seat,true,i=>{calls++;last=i;},null,"");yield return Rendered();
                var picker=GameObject.Find("CardSelection");Check(picker!=null,"Cost card picker absent");
                var instruction=picker.transform.Find("Instruction").GetComponent<Text>();Check(!instruction.resizeTextForBestFit&&instruction.preferredHeight<=instruction.rectTransform.rect.height,"Card cost instruction clipped/shrinks");
                if(font>=0)Check(font==instruction.fontSize,"Selection changes instruction font");font=instruction.fontSize;
                Check(picker.transform.Find("Count").GetComponent<Text>().text.Contains(view.request.selectedCount+" / 2"),"Cost count/gate absent");
                Check(picker.transform.Find("Title").GetComponent<Text>().preferredWidth+picker.transform.Find("Count").GetComponent<Text>().preferredWidth+40<=((RectTransform)picker.transform).rect.width,"Cost title overlaps count");
                int cancel=Array.FindIndex(view.request.options,o=>o.id=="cancel");var button=GameObject.Find("SelectionAction_"+cancel).GetComponent<Button>();
                int confirm=Array.FindIndex(view.request.options,o=>o.id=="ok");Check((confirm>=0)==(view.request.selectedCount==2),"Premature cost confirmation");
                var option=view.request.options.First(o=>o.card!=null&&!string.IsNullOrEmpty(o.card.name));var tile=GameObject.Find("ChoiceCard_"+option.card.id);
                if(panel!=null)Check(picker==panel&&tile==cardTile,"Native selection rebuilt payment picker/card");
                // Identity stability asserted again without clearing the current view.
                ui.Present(view,view.seat,true,i=>{calls++;last=i;},null,"");yield return Rendered();Check(GameObject.Find("CardSelection")==picker&&GameObject.Find(tile.name)==tile,"Payment picker rebuild on unchanged request");
                button=GameObject.Find("SelectionAction_"+cancel).GetComponent<Button>();
                ui.Choose(cancel,view.request.requestId-1);Check(calls==0,"Actionable stale card cost submitted");yield return Shot(language+"-cards-"+view.request.selectedCount);
                Button action=button;int actionIndex=cancel;
                if(view.request.selectedCount==0){action=tile.transform.Find("Artwork").GetComponent<Button>();actionIndex=Array.FindIndex(view.request.options,o=>o.card?.id==option.card.id);}
                else if(view.request.selectedCount==2){action=GameObject.Find("SelectionAction_"+confirm).GetComponent<Button>();actionIndex=confirm;}
                Click(action);action.onClick.Invoke();Check(calls==1&&last==actionIndex,"Card cost select/cancel/confirm pointer/index/duplicate");panel=picker;cardTile=tile;
            }
            var orders=File.ReadLines(Input+"opponent.jsonl").Select(JsonUtility.FromJson<ForgeWireMessage>).Where(m=>m.type=="request"&&m.request?.kind=="OrderCards").ToArray();
            var orderView=WireView(orders.Last());Check(orderView.seat==2&&orderView.request.playerSeat==2,"Wrong owner ordering seat");int orderCalls=0,orderLast=-1;
            ui.Present(null,2,false,null,null,"");ui.Present(orderView,2,true,i=>{orderCalls++;orderLast=i;},null,"");yield return Rendered();
            var order=GameObject.Find("CardSelection");var text=order.transform.Find("Instruction").GetComponent<Text>();Check(!text.resizeTextForBestFit&&text.preferredHeight<=text.rectTransform.rect.height,"Order instruction clips/shrinks");
            int ci=Array.FindIndex(orderView.request.options,o=>o.id=="confirm-cards");yield return Shot(language+"-opponent-order");Click(GameObject.Find("SelectionAction_"+ci).GetComponent<Button>());Check(orderCalls==1&&orderLast==ci,"Owner ordering confirmation cannot submit");
            foreach(string test in new[]{"own","cancel-card","insufficient","second-age"}) {
                var view=Read(test+"-resolved-"+(test=="second-age"?"2":"1"));ui.Present(null,view.seat,false,null,null,"");ui.Present(view,view.seat,true,_=>{},null,"");yield return Rendered();
                Check(GameObject.Find("CardSelection")==null&&GameObject.Find("CardAbilityChoices")==null,"Resolved upkeep leaves phantom choices");
                var grave=view.players[view.seat-1].graveyardCards.FirstOrDefault(c=>c.name=="Jötun Grunt");if(grave!=null)Check(GameObject.Find("Card_"+grave.id)==null,"Sacrificed source remains on battlefield");
                if(test=="second-age")Check(GameObject.Find("Counter_AGE")!=null,"Second upkeep age counter missing");yield return Shot(language+"-"+test+"-resolved");
            }
            ui.Present(null,1,false,null,null,"");yield return Rendered();
        }
    }
}
#endif
