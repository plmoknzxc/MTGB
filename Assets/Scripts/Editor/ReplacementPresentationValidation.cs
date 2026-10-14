#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ReplacementPresentationValidation
{
    const string Output=".tools/replacements-ui",Input="Tools/Validation/Reports/Replacements/";
    static bool running;
    static void Check(bool b,string message){if(!b)throw new Exception(message);}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Input+name+".json"));
    static ForgeSeatView View(ForgeWireMessage m){var v=m.views[m.request.seat-1];v.request=m.request;v.awaitingInput=true;return v;}
    public static void Run()
    {
        if(running)return;Check(Application.isPlaying,"Enter IntegratedBattlefield Play Mode first.");
        running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("ReplacementValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator iterator)
    {
        string language=GamePreferences.Language;var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=banner!=null&&banner.enabled;
        if(banner!=null)banner.enabled=false;
        try {
            while(true){object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: actual replacement request replay in zh-CN/en-US/ja-JP; native source/effect identities, affected player, first-only/full confirmation, reset and cached order, stable cards/panel/fonts, translated exact unchanged paragraphs with scrollable complete body, actual pointer/index and independent stale/duplicate/nonacting guards; exact counter/token/exile end states. Native effect proof remains separate; not all replacements, languages, network or animation timing.");
        }finally{running=false;GamePreferences.SetLanguage(language);if(banner!=null)banner.enabled=enabled;}
    }
    static IEnumerator Rendered(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static IEnumerator Shot(string name){yield return new WaitForSecondsRealtime(.3f);yield return Rendered();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForEndOfFrame();}
    static void Click(Button button)
    {
        var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Replacement pointer blocked: "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool changed=GamePreferences.Language!=language;GamePreferences.SetLanguage(language);float end=Time.realtimeSinceStartup+8;
            while((!GamePreferences.Ready||changed&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<end)yield return null;
            Check(GamePreferences.Ready&&(!changed||revision!=GamePreferences.Revision),"Language not ready");
            foreach(string test in new[]{"counter-double-first","counter-scales-first","counter-other-player","counter-seat-two","counter-single-choice","counter-repeat","token-manufactor","zone-rest-first"}) {
                var wire=File.ReadLines(Input+test+".jsonl").Select(JsonUtility.FromJson<ForgeWireMessage>).Where(m=>m.type=="request"&&m.request?.selectionPurpose=="replacementOrder").ToArray();
                Check(wire.Length>0,"No native replacement requests: "+test);
                ui.Present(null,1,false,null,null,"");GameObject panel=null;var tiles=new Dictionary<int,GameObject>();int titleSize=-1;int step=0;
                foreach(var m in wire) {
                    var v=View(m);var r=v.request;int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};
                    ui.Present(v,v.seat,true,submit,null,"");yield return Rendered();
                    var overlay=GameObject.Find("AbilityOrder");Check(overlay!=null,"Replacement order UI missing");
                    Check(GameObject.Find("CardSelection")==null,"Replacement rendered as ordinary card picker");
                    if(panel!=null)Check(overlay==panel,"Order panel rebuilt after selection");panel=overlay;
                    var title=overlay.transform.Find("Title").GetComponent<Text>();var direction=overlay.transform.Find("Direction").GetComponent<Text>();
                    Check(title.text==(language=="zh-CN"?"排列替代效果":language=="ja-JP"?"置換効果の順序":"Order replacement effects"),"Wrong replacement title/locale");
                    Check(!title.resizeTextForBestFit&&!direction.resizeTextForBestFit&&title.preferredHeight<=title.rectTransform.rect.height&&direction.preferredHeight<=direction.rectTransform.rect.height,"Replacement title/instruction clips or shrinks");
                    if(titleSize>=0)Check(titleSize==title.fontSize,"Replacement title font changes");titleSize=title.fontSize;
                    var options=r.options.Where(o=>o.id.StartsWith("order:")).ToArray();var entries=(r.orderedAbilities??Array.Empty<ForgeStackItem>()).Concat(options.Select(o=>o.ability)).ToArray();
                    Check(entries.All(e=>e!=null&&e.kind=="replacement"&&e.source?.id>0)&&entries.Select(e=>e.id).Distinct().Count()==entries.Length,"Source/effect identity lost");
                    foreach(var e in entries) {
                        var tile=GameObject.Find("OrderedAbility_"+e.id);Check(tile!=null,"Replacement tile absent");
                        if(tiles.TryGetValue(e.id,out var previous))Check(previous==tile,"Replacement tile rebuilt while ordering");tiles[e.id]=tile;
                        var face=tile.transform.Find("Face/AbilityFace");var name=face.Find("Name").GetComponent<Text>();var body=face.Find("EffectClip/Effect").GetComponent<Text>();var scroll=face.Find("EffectClip").GetComponent<ScrollRect>();
                        Check(name.text==GamePreferences.CardFaceName(e.source.name),"Source name mismatched language");
                        Check(!name.resizeTextForBestFit&&!body.resizeTextForBestFit&&name.preferredWidth<=name.rectTransform.rect.width,"Source name shrinks or clips");
                        var renderedBody=body.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                        Check(renderedBody!=null&&renderedBody.text==CardSymbolText.Format(body.text)&&!renderedBody.enableAutoSizing&&scroll.content==body.rectTransform&&scroll.vertical&&body.rectTransform.rect.height+1>=renderedBody.GetPreferredValues(renderedBody.text,body.rectTransform.rect.width,10000).y,"Rendered effect body truncated instead of scrollable");
                        if(language!="en-US")Check(!body.text.StartsWith("If ",StringComparison.Ordinal),"Known unchanged replacement paragraph not translated: "+e.source.name);
                        int at=Array.FindIndex(r.orderedAbilities??Array.Empty<ForgeStackItem>(),x=>x.id==e.id);string expected=at>=0?(at+1).ToString():"";
                        Check(tile.transform.Find("Order").GetComponent<Text>().text==expected,"Order badge mismatch");
                    }
                    var confirm=overlay.transform.Find("ConfirmOrder").GetComponent<Button>();var reset=overlay.transform.Find("ResetOrder").GetComponent<Button>();
                    int ci=Array.FindIndex(r.options,o=>o.id=="confirm-order");Check(confirm.interactable==(ci>=0),"Confirmation gate mismatch");
                    Check((ci>=0)==(r.selectedCount>=r.minCount&&r.selectedCount<=r.maxCount),"Confirmation does not honor native bounds");
                    int ri=Array.FindIndex(r.options,o=>o.id=="reset-order");Check(reset.interactable==(ri>=0),"Reset gate mismatch");
                    Button action;int index;
                    if(test=="counter-repeat"&&step==3){Check(r.selectedCount==2&&ri>=0,"Cached native order not preserved");action=reset;index=ri;}
                    else if(test=="counter-single-choice"&&r.selectedCount==1){Check(options.Length==1&&ci>=0,"First replacement alone cannot confirm");action=confirm;index=ci;}
                    else if(options.Length==0){action=confirm;index=ci;}
                    else {var option=options[0];action=GameObject.Find("OrderedAbility_"+option.ability.id).GetComponent<Button>();index=Array.FindIndex(r.options,o=>o.id==option.id);}
                    ui.Choose(index,r.requestId-1);Check(calls==0,"Actionable stale replacement submitted");
                    if(test=="counter-double-first" || test=="counter-single-choice"&&r.selectedCount==1 || test=="counter-repeat"&&step==3 || (test!="counter-single-choice"&&test!="counter-repeat"&&step==0))yield return Shot(language+"-"+test+"-"+step);
                    Click(action);action.onClick.Invoke();Check(calls==1&&last==index,"Pointer/index/duplicate guard wrong");
                    ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,false,submit,null,"");yield return Rendered();
                    var blocked=GameObject.Find("AbilityOrder").GetComponentsInChildren<Button>(true);Check(blocked.All(b=>!b.interactable),"Nonacting player can order");ui.Choose(index,r.requestId);Check(calls==1,"Nonacting submission");
                    // Return to the same active panel before the next native request.
                    ui.Present(v,v.seat,true,submit,null,"");yield return Rendered();panel=GameObject.Find("AbilityOrder");tiles=entries.ToDictionary(e=>e.id,e=>GameObject.Find("OrderedAbility_"+e.id));step++;
                }
            }
            foreach(string test in new[]{"counter-zero-target","counter-repeat","token-double","token-manufactor","token-other-player","zone-rest-first"}) {
                var v=Read(test+"-resolved-"+(test=="counter-repeat"?"1":"0"));ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,true,_=>{},null,"");yield return Rendered();
                Check(GameObject.Find("AbilityOrder")==null,"Resolved effect leaves ordering panel");
                if(test=="counter-repeat") {
                    var bear=v.players.SelectMany(p=>p.battlefield).Single(c=>c.name=="Grizzly Bears");var card=GameObject.Find("Card_"+bear.id);Check(card!=null,"Counter target absent");
                    Check(card.GetComponentsInChildren<Text>(true).Any(t=>t.transform.parent.name=="Counter_P1P1"&&t.text=="7"),"Final seven counters not displayed");
                }
                foreach(var group in v.players.SelectMany(p=>p.battlefield).Where(c=>c.token).GroupBy(c=>new{c.name,c.ownerSeat,c.printingKey,c.tokenArtKey,c.creature,c.tapped})) {
                    var visible=group.Select(c=>GameObject.Find("Card_"+c.id)).Where(c=>c!=null).ToArray();Check(visible.Length>0,"Token group absent: "+group.Key.name);
                    if(visible.Length<group.Count())Check(visible.Any(c=>c.GetComponentsInChildren<Text>(true).Any(t=>t.transform.parent.name=="Count"&&t.text=="×"+group.Count())),"Collapsed token group loses count");
                }
                if(test=="zone-rest-first")foreach(var c in v.players.SelectMany(p=>p.exileCards).Where(c=>c.name=="Grizzly Bears"||c.name=="Lightning Bolt"))Check(GameObject.Find("Card_"+c.id)==null,"Exiled object remains on battlefield");
                yield return Shot(language+"-"+test+"-resolved");
            }
            ui.Present(null,1,false,null,null,"");yield return Rendered();
        }
    }
}
#endif
