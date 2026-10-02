#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class CardSelectionPresentationValidation
{
    const string Output=".tools/card-selection-presentation";
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run()
    {
        Check(Application.isPlaying,"Enter IntegratedBattlefield Play Mode first.");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("CardSelectionValidation").AddComponent<ForgeBattlefieldController>();
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");ui.StartCoroutine(Validate(ui));
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string test in new[]{"evidence","surveil","order","fetch-foothills"})
        {
            string path="Tools/Validation/Reports/"+(test=="evidence"?"AdvancedInteractions/":"CardSelection/")+test+".jsonl";
            var snapshots=File.ReadLines(path).Select(JsonUtility.FromJson<ForgeWireMessage>)
                .Where(m=>m.type=="request"&&m.request!=null && (test=="evidence"?m.request.selectionPurpose=="collectEvidence":
                test=="surveil"?m.request.kind=="Surveil":test=="order"?m.request.kind=="OrderCards":m.request.kind=="InputSelectEntitiesFromList"||m.request.kind=="InputSelectCardsFromList"||m.request.kind=="SelectCards")).ToArray();
            Check(snapshots.Length>0,"No real snapshots for "+test);
            GameObject panel=null;var tiles=new Dictionary<int,GameObject>();string priorKey=null;int count=0;
            foreach(var snapshot in snapshots)
            {
                var request=snapshot.request;var view=snapshot.views[request.seat-1];view.request=request;view.awaitingInput=true;
                int chosen=-1;ui.Present(view,request.seat,true,i=>chosen=i,null,"");yield return null;
                var current=GameObject.Find("CardSelection");Check(current!=null,"Missing picker: "+test);
                var cards=request.options.Where(o=>o.card!=null&&!string.IsNullOrEmpty(o.card.name)).Select(o=>o.card)
                    .Concat(request.previewCards??Array.Empty<ForgeCardView>()).GroupBy(c=>c.id).Select(g=>g.First()).ToArray();
                string key=string.Join("|",cards.OrderBy(c=>c.id).Select(c=>c.id));
                if(priorKey==key)Check(current==panel,"Rebuilt panel on selection: "+test);
                foreach(var card in cards)
                {
                    var tile=current.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="ChoiceCard_"+card.id)?.gameObject;
                    if(tile==null)continue; // Other pages are intentionally not instantiated.
                    if(priorKey==key&&tiles.TryGetValue(card.id,out var previous))Check(tile==previous,"Rebuilt card: "+test);
                    tiles[card.id]=tile;
                }
                if(test=="evidence")
                {
                    Check(current.transform.Find("Title").GetComponent<Text>().text=="搜证","Wrong evidence title");
                    Check(current.transform.Find("Count").GetComponent<Text>().text==$"法术力值 {request.selectedValue} / 6","Wrong evidence tally");
                    bool confirm=current.transform.Find("SelectionFooter").GetComponentsInChildren<Button>().Any(b=>b.GetComponentInChildren<Text>().text=="确认");
                    Check(confirm==(request.selectedValue>=6),"Premature evidence confirmation");
                }
                if(test=="surveil")foreach(var option in request.options.Where(o=>o.id.StartsWith("scry-")))
                    Check(current.GetComponentsInChildren<Button>().Any(b=>b.name=="Select_"+option.id),"Lost destination control after reset");
                panel=current;priorKey=key;count++;
                if(test=="evidence"&&request.selectedValue==6 || test!="evidence"&&count==Math.Min(3,snapshots.Length))
                {yield return new WaitForSecondsRealtime(.25f);ScreenCapture.CaptureScreenshot(Output+"/"+test+".png");yield return new WaitForEndOfFrame();}
            }
            ui.Present(null,1,false,null,null,"");yield return null;
        }
        var number=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/AdvancedInteractions/x-number.json"));
        int sent=-1;ui.Present(number,number.seat,true,i=>sent=i,null,"");yield return new WaitForSecondsRealtime(.3f);
        var menu=GameObject.Find("CardAbilityChoices");Check(menu!=null,"Missing numeric menu");
        Check(menu.GetComponent<RectTransform>().rect.size==new Vector2(264,154),"Oversized numeric menu");
        Check(menu.transform.Find("NumericValue").GetComponent<Text>().text=="2","Wrong X value");
        Check(menu.transform.Find("Cancel")==null,"Unexpected explicit cancel in numeric menu");
        ScreenCapture.CaptureScreenshot(Output+"/number.png");yield return new WaitForEndOfFrame();
        menu.transform.Find("ConfirmValue").GetComponent<Button>().onClick.Invoke();Check(number.request.options[sent].id=="confirm-number","Wrong numeric confirmation");
        var mixed=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/AdvancedInteractions/graveyards-targets.json"));
        ui.Present(mixed,mixed.seat,true,i=>{},null,"");yield return new WaitForSecondsRealtime(.3f);
        var picker=GameObject.Find("CardSelection");Check(picker!=null,"Missing graveyard target picker");
        var labels=picker.GetComponentsInChildren<Text>();
        Check(labels.Any(t=>t.text=="你的牌")&&labels.Any(t=>t.text=="对手的牌"),"Mixed graveyards lose owner identity");
        ScreenCapture.CaptureScreenshot(Output+"/graveyards.png");yield return new WaitForEndOfFrame();
        foreach(string state in new[]{"locked","unlocked"}) {
            var roomView=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/AdvancedInteractions/rooms-"+state+".json"));
            ui.Present(roomView,roomView.seat,true,i=>{},null,"");yield return new WaitForSecondsRealtime(.3f);
            var art=UnityEngine.Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).FirstOrDefault(i=>i.name=="RoomIllustration");
            Check(art!=null&&Mathf.Approximately(art.rectTransform.eulerAngles.z,270),"Room split face is not upright");
            Check((art.transform.parent.Find("RightDoorLocked")!=null)==(state=="locked"),"Room unlock overlay stale");
            ScreenCapture.CaptureScreenshot(Output+"/room-"+state+".png");yield return new WaitForEndOfFrame();
            if(state=="locked") {
                var card=roomView.players.SelectMany(p=>p.battlefield).First(c=>c.room);
                typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(ui,new object[]{card});
                yield return new WaitForSecondsRealtime(.2f);
                var inspect=GameObject.Find("InspectPanel");var name=inspect.transform.Find("Name").GetComponent<Text>();
                Check(name.resizeTextForBestFit&&name.verticalOverflow==VerticalWrapMode.Truncate,"Long room name overflows inspector");
                ScreenCapture.CaptureScreenshot(Output+"/room-inspect.png");yield return new WaitForEndOfFrame();inspect.SetActive(false);
            }
        }
        var statuses=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/AdvancedInteractions/statuses-field.json"));
        ui.Present(statuses,statuses.seat,true,i=>{},null,"");yield return new WaitForSecondsRealtime(.3f);
        Check(GameObject.Find("Counter_STUN")!=null&&GameObject.Find("Counter_P1P1")!=null,"Permanent counters missing");
        Check(GameObject.Find("Keyword_DEATHTOUCH")!=null,"Current keyword badge missing");
        ScreenCapture.CaptureScreenshot(Output+"/statuses.png");yield return new WaitForEndOfFrame();
        File.WriteAllText(Output+"/result.txt","PASS: real evidence/surveil/order/fetch snapshots; stable panel/card instances; evidence tally, reset and gate; compact real X; mixed graveyard owners; room door unlock rendering and long-name inspector; current permanent counters and keywords.");
        Debug.Log("CardSelectionPresentationValidation PASS");
    }
}
#endif
