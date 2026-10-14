#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class TapPaymentPresentationValidation
{
    private const string Output = ".tools/tap-payment-ui";
    private static void Check(bool condition,string message) { if(!condition)throw new Exception(message); }
    public static void Run()
    {
        Check(Application.isPlaying,"Play Mode required");
        Directory.CreateDirectory(Output); File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    private static IEnumerator Guard(IEnumerator routine)
    {
        while(true) {
            object value;
            try { if(!routine.MoveNext())break; value=routine.Current; }
            catch(Exception error) { File.WriteAllText(Output+"/result.txt","FAIL: "+error);throw; }
            yield return value;
        }
        File.WriteAllText(Output+"/result.txt","PASS: real convoke/improvise/waterbend requests; source and remaining cost decoded; selection/tally/reset and stable card instances; pointer option ID, stale/duplicate guards, disabled viewer; Chinese/English/Japanese titles. Request replay, not a live client-server round trip.");
    }
    private static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string test in new[]{"convoke","convoke-zero","convoke-cancel","improvise","waterbend","convoke-only","improvise-only","waterbend-only"}) {
            var records=File.ReadLines("Tools/Validation/Reports/TapPayment/"+test+".jsonl").Select(JsonUtility.FromJson<ForgeWireMessage>)
                .Where(m=>m.type=="request"&&m.request?.kind=="InputSelectCardsForConvokeOrImprovise").ToArray();
            Check(records.Length>0,"No real payment request: "+test);
            GameObject panel=null,firstTile=null;int calls=0,chosen=-1;
            foreach(var record in records) {
                var request=record.request; var view=record.views[request.seat-1]; view.request=request;view.awaitingInput=true;
                ui.Present(view,request.seat,true,i=>{calls++;chosen=i;},null,"");yield return null;
                Canvas.ForceUpdateCanvases();
                var current=GameObject.Find("CardSelection");Check(current!=null,"No payment picker");
                Check(current.transform.Find("Count").GetComponent<Text>().text=="已选 "+request.selectedCount,"Selected count is stale");
                Check(!string.IsNullOrEmpty(request.remainingManaCost)&&current.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("剩余费用 "+request.remainingManaCost)),"Remaining cost missing");
                var first=request.options.First(o=>o.card!=null&&!string.IsNullOrEmpty(o.card.name));
                var tile=GameObject.Find("ChoiceCard_"+first.card.id);Check(tile!=null,"Missing offered permanent");
                if(panel!=null){Check(panel==current,"Payment selection rebuilds panel");Check(tile==firstTile,"Payment selection rebuilds card");}
                panel=current;firstTile=tile;
                if(request.selectedCount==1) {
                    int reset=Array.FindIndex(request.options,o=>o.id=="reset-cards");
                    Check(reset>=0&&current.GetComponentsInChildren<Button>().Any(b=>b.name=="SelectionAction_"+reset),"Reset control missing");
                }
            }
            var state=records[0];var initial=state.views[state.request.seat-1];initial.request=state.request;initial.awaitingInput=true;
            ui.Present(initial,state.request.seat,true,i=>{calls++;chosen=i;},null,"");yield return null;Canvas.ForceUpdateCanvases();
            var option=state.request.options.First(o=>o.card!=null&&!string.IsNullOrEmpty(o.card.name));
            var button=GameObject.Find("ChoiceCard_"+option.card.id).GetComponentInChildren<Button>();
            var rect=(RectTransform)button.transform;
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Payment permanent raycast blocked");
            int before=calls;Check(!ui.TrySubmitPresentedOption(0,state.request.requestId-1),"Stale payment accepted");
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
            button.onClick.Invoke();Check(calls==before+1&&state.request.options[chosen].id==option.id,"Wrong or duplicate payment option");
            ui.Present(initial,state.request.seat,false,i=>calls++,null,"");yield return null;
            Check(!GameObject.Find("ChoiceCard_"+option.card.id).GetComponentInChildren<Button>().interactable,"Waiting viewer can pay");
            ScreenCapture.CaptureScreenshot(Output+"/"+test+".png");yield return new WaitForEndOfFrame();
            ui.Present(null,1,false,null,null,"");yield return null;
        }
        string original=GamePreferences.Language;
        try {
            foreach(string language in new[]{"en-US","ja-JP","zh-CN"}) {
                GamePreferences.SetLanguage(language);
                foreach(string test in new[]{"convoke","improvise","waterbend"}) {
                    var wire=File.ReadLines("Tools/Validation/Reports/TapPayment/"+test+".jsonl").Select(JsonUtility.FromJson<ForgeWireMessage>).First(m=>m.request?.kind=="InputSelectCardsForConvokeOrImprovise");
                    var view=wire.views[wire.request.seat-1];view.request=wire.request;view.awaitingInput=true;
                    ui.Present(view,view.seat,true,_=>{},null,"");yield return new WaitForSecondsRealtime(.15f);
                    string expected=language=="en-US"?(test=="convoke"?"Convoke":test=="improvise"?"Improvise":"Waterbend"):
                        language=="ja-JP"?(test=="convoke"?"召集":test=="improvise"?"即席":"水の技"):(test=="convoke"?"召集":test=="improvise"?"拼造":"截水");
                    Check(GameObject.Find("CardSelection/Title").GetComponent<Text>().text==expected,"Payment title wrong language");
                    ui.Present(null,1,false,null,null,"");yield return null;
                }
            }
        } finally {GamePreferences.SetLanguage(original);}
    }
}
#endif
