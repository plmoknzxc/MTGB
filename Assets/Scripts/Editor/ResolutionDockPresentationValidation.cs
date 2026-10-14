#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Focused delta: dock decisions, duplicate panel, known-zone browsing and hand memory.
public static class ResolutionDockPresentationValidation {
    const string Output=".tools/resolution-dock-1005";
    static bool running;
    static Transform Root=>GameObject.Find("Canvas").transform;
    static ForgeSeatView Read(string path)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));
    static ForgeSeatView Original(string name)=>Read(".tools/resolution-ui-1005/native/"+name+".json");
    static ForgeSeatView Memory(string name)=>Read(Output+"/native/"+name+".json");
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static Rect Bounds(Transform t){var corners=new Vector3[4];((RectTransform)t).GetWorldCorners(corners);return Rect.MinMaxRect(corners.Min(c=>c.x),corners.Min(c=>c.y),corners.Max(c=>c.x),corners.Max(c=>c.y));}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static IEnumerator Locale(string locale){int rev=GamePreferences.Revision;bool changed=locale!=GamePreferences.Language;GamePreferences.SetLanguage(locale);float end=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||changed&&GamePreferences.Revision==rev)&&Time.realtimeSinceStartup<end)yield return null;yield return Frame();}
    static void Click(Button button){
        var rect=Bounds(button.transform);var points=new List<Vector2>{rect.center,button.transform.position};
        // Fanned hands overlap by design. Click a truly exposed part, never
        // bypass raycasts by invoking a hidden card's listener directly.
        for(int y=1;y<6;y++)for(int x=1;x<6;x++)points.Add(new Vector2(Mathf.Lerp(rect.xMin,rect.xMax,x/6f),Mathf.Lerp(rect.yMin,rect.yMax,y/6f)));
        foreach(var point in points){var pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=button)continue;ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);return;}
        throw new Exception("No exposed clickable point for "+button.name);
    }
    static void CleanPanels(){Check(!Root.Find("ForgeChoices").gameObject.activeInHierarchy,"Empty generic panel remains behind picker");Check(Root.Find("PlayerLibraryTop")==null&&Root.Find("OpponentLibraryTop")==null,"Unwanted library-top tile");}
    static void DockBounds(Transform dock){var phase=Bounds(Root.Find("CornerCluster/ControlRow/PhaseTrack"));var auto=Bounds(Root.Find("PriorityControlMode"));foreach(var button in dock.GetComponentsInChildren<Button>()){var b=Bounds(button.transform);Check(!b.Overlaps(phase)&&!b.Overlaps(auto),"Dock overlaps phase/AUTO: "+button.name);}Check(!Root.Find("CornerCluster/ControlRow/SkipCombatButton").gameObject.activeInHierarchy,"Duplicate advance");}
    static IEnumerator Shot(string locale,string name){if(locale=="zh-CN"){yield return Frame();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForEndOfFrame();}}
    public static void Run(){if(running)return;running=true;Check(Application.isPlaying,"Enter play mode");Directory.CreateDirectory(Output);File.WriteAllText(Output+"/ui-result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("ResolutionDockValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));}
    static IEnumerator Guard(IEnumerator inner){foreach(var notice in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))notice.enabled=false;try{while(true){object current;try{if(!inner.MoveNext())break;current=inner.Current;}catch(Exception e){File.WriteAllText(Output+"/ui-result.txt","FAIL "+e);throw;}yield return current;}File.WriteAllText(Output+"/ui-result.txt","PASS: three-language real-request replay; optional hand draft/confirm/cancel with exact native card IDs and stale/duplicate/nonacting guards; no direct optional prompt; picker has no underlying generic panel or duplicate advance; native X plus/confirm and local energy limits in shared dock clear of phase/AUTO; known library browsed read-only without physical top tiles; native revealed hand persists then becomes hidden after Brainstorm; spectator hand/top privacy. No live-networking or whole-pool claim.");}finally{running=false;GamePreferences.SetLanguage("zh-CN");}}
    public static void RunHandBrowserOnly(){if(running)return;Check(Application.isPlaying,"Enter play mode");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("KnownHandValidation").AddComponent<ForgeBattlefieldController>();running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/hand-browser-result.txt","RUNNING");ui.StartCoroutine(HandBrowserGuard(ui));}
    static IEnumerator HandBrowserGuard(ForgeBattlefieldController ui){foreach(var notice in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))notice.enabled=false;try{var e=HandBrowsers(ui);while(true){object c;try{if(!e.MoveNext())break;c=e.Current;}catch(Exception ex){File.WriteAllText(Output+"/hand-browser-result.txt","FAIL "+ex);throw;}yield return c;}File.WriteAllText(Output+"/hand-browser-result.txt","PASS: three-language known/unknown hand browser, exact public card count, readonly raycasts, live knowledge invalidation, no leftover card backs in fully known hand; optional-list draft count/reset without native submission.");}finally{running=false;GamePreferences.SetLanguage("zh-CN");}}
    static IEnumerator HandBrowsers(ForgeBattlefieldController ui){foreach(string locale in new[]{"zh-CN","ja-JP","en-US"}){
        yield return Locale(locale);
        var many=Memory("growth-many-yes-select-0");int draftCalls=0;ui.Present(null,1,false,null,null,"");ui.Present(many,1,true,_=>draftCalls++,null,"");yield return Frame();
        var manyPanel=GameObject.Find("CardSelection");var land=many.request.options.First(o=>o.card!=null&&!string.IsNullOrEmpty(o.card.name)&&!o.disabled);
        var pick=manyPanel.GetComponentsInChildren<Transform>().First(t=>t.name=="ChoiceCard_"+land.card.id).Find("Artwork").GetComponent<Button>();Click(pick);yield return Frame();
        Check(manyPanel.transform.Find("Count").GetComponent<Text>().text.Contains("1")&&manyPanel.transform.Find("SelectionFooter/ConfirmHandChoice").GetComponent<Button>().interactable&&draftCalls==0,"Draft count not refreshed");yield return Shot(locale,"hand-list-confirm");
        Click(pick);yield return Frame();Check(manyPanel.transform.Find("Count").GetComponent<Text>().text.Contains("0")&&!manyPanel.transform.Find("SelectionFooter/ConfirmHandChoice").GetComponent<Button>().interactable&&draftCalls==0,"Draft clear not refreshed");
        int calls=0;var known=Memory("hand-known-priority");ui.Present(null,1,false,null,null,"");ui.Present(known,1,false,_=>calls++,null,"");yield return Frame();
        Check(!Root.Find("OpponentHandRoot").Cast<Transform>().Any(t=>t.name.StartsWith("OtherHand_")&&t.gameObject.activeSelf),"Back remnants cover known hand");
        Click(Root.Find("OpponentPlate/Sub").GetComponent<Button>());yield return Frame();var panel=GameObject.Find("CardSelection");Check(panel!=null,"Hand browser missing");
        Check(panel.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("ChoiceCard_"))==known.players[1].handCount&&calls==0,"Known hand count/readonly");yield return Shot(locale,"known-hand-browser");
        ui.Present(Memory("hand-mixed-priority"),1,false,_=>calls++,null,"");yield return Frame();panel=GameObject.Find("CardSelection");Check(panel!=null&&!panel.GetComponentsInChildren<Transform>().Any(t=>t.name.StartsWith("ChoiceCard_"))&&calls==0,"Open hand browser retains concealed identities");yield return Shot(locale,"hidden-hand-browser");Click(panel.transform.Find("SelectionFooter/CloseReviewCards").GetComponent<Button>());
        Click(Root.Find("PlayerPlate/Sub").GetComponent<Button>());yield return Frame();panel=GameObject.Find("CardSelection");Check(panel.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("ChoiceCard_"))==7&&calls==0,"Own hand browser");Click(panel.transform.Find("SelectionFooter/CloseReviewCards").GetComponent<Button>());
    }}
    static IEnumerator Validate(ForgeBattlefieldController ui){
        foreach(string locale in new[]{"zh-CN","ja-JP","en-US"}) {
            yield return Locale(locale);
            int sent=-1,calls=0;var growth=Original("growth-yes-select-0");
            ui.Present(null,1,false,null,null,"");ui.Present(growth,1,true,i=>{sent=i;calls++;},null,"");yield return Frame();CleanPanels();
            Check(GameObject.Find("HandSelectionPrompt")==null,"Optional hand prompt still shown");
            var dock=Root.Find("CornerCluster/ControlRow/HandChoiceActions");DockBounds(dock);
            var confirm=dock.Find("ConfirmHandChoice").GetComponent<Button>();var cancel=dock.Find("HandAction_"+ui.OptionIndex("cancel")).GetComponent<Button>();
            Check(!confirm.interactable&&cancel.interactable&&Bounds(cancel.transform).xMax<Bounds(confirm.transform).xMin,"Optional hand initial controls");
            var option=growth.request.options.First(o=>o.card!=null&&!string.IsNullOrEmpty(o.card.name)&&!o.disabled);int index=ui.OptionIndex(option.id);
            ui.Choose(index,growth.request.requestId-1);Check(calls==0&&!confirm.interactable,"Stale draft accepted");
            Click(Root.GetComponentsInChildren<Button>().First(b=>b.name=="Card_"+option.card.id));yield return Frame();
            confirm=dock.Find("ConfirmHandChoice").GetComponent<Button>();Check(calls==0&&confirm.interactable,"Hand click must draft, not submit");
            yield return Shot(locale,"hand-confirm-dock");Click(confirm);confirm.onClick.Invoke();Check(calls==1&&sent==index,"Confirmed wrong/duplicate native card option");
            ui.Present(growth,1,true,i=>{sent=i;calls++;},null,"");yield return Frame();cancel=dock.Find("HandAction_"+ui.OptionIndex("cancel")).GetComponent<Button>();Click(cancel);Check(calls==2&&sent==ui.OptionIndex("cancel"),"Cancel draft ID");
            ui.Present(growth,1,false,_=>calls++,null,"");yield return Frame();Check(dock.GetComponentsInChildren<Button>().All(b=>!b.interactable),"Nonacting hand controls enabled");
            ui.Choose(index,growth.request.requestId);Check(calls==2,"Nonacting draft submitted");

            var many=Memory("growth-many-yes-select-0");calls=0;ui.Present(null,1,false,null,null,"");ui.Present(many,1,true,i=>{sent=i;calls++;},null,"");yield return Frame();CleanPanels();
            var list=GameObject.Find("CardSelection");Check(list!=null&&many.players[0].handCount>10,"Large optional hand missing list");
            var listConfirm=list.transform.Find("SelectionFooter/ConfirmHandChoice").GetComponent<Button>();Check(!listConfirm.interactable,"List confirms empty draft");
            var land=many.request.options.First(o=>o.card!=null&&!string.IsNullOrEmpty(o.card.name)&&!o.disabled);int landIndex=ui.OptionIndex(land.id);
            Click(list.GetComponentsInChildren<Transform>().First(t=>t.name=="ChoiceCard_"+land.card.id).Find("Artwork").GetComponent<Button>());yield return Frame();
            listConfirm=list.transform.Find("SelectionFooter/ConfirmHandChoice").GetComponent<Button>();Check(calls==0&&listConfirm.interactable,"List must draft before confirmation");yield return Shot(locale,"hand-list-confirm");Click(listConfirm);Check(calls==1&&sent==landIndex,"List confirmation native option");

            foreach(string name in new[]{"explore-keep-confirm-0","brain-two-order-2"}) {
                var v=Original(name);calls=0;ui.Present(null,1,false,null,null,"");ui.Present(v,1,true,i=>{sent=i;calls++;},null,"");yield return Frame();CleanPanels();
                var panel=GameObject.Find("CardSelection");Check(panel!=null,"Missing picker: "+name);var prompt=panel.transform.Find("Instruction").GetComponent<Text>();int font=prompt.fontSize;
                ui.ViewBattlefield();yield return Frame();ui.RestoreBattlefieldWindows();yield return Frame();Check(font==prompt.fontSize&&calls==0,"Picker restore changes font/submits");
                yield return Shot(locale,name.StartsWith("explore")?"explore-clean-window":name.StartsWith("genesis")?"genesis-confirm-window":"order-clean-window");
                int action=ui.OptionIndex(name=="brain-two-order-2"?"confirm-cards":"ok");
                Click(panel.transform.Find("SelectionFooter/SelectionAction_"+action).GetComponent<Button>());Check(calls==1&&sent==action,"Picker confirmation ID");
            }

            var genesis=Original("genesis-yes-confirm-0");calls=0;ui.Present(null,1,false,null,null,"");ui.Present(genesis,1,true,i=>{sent=i;calls++;},null,"");yield return Frame();CleanPanels();
            var yesNo=GameObject.Find("CardAbilityChoices");Check(yesNo!=null&&((RectTransform)yesNo.transform).rect.height<200,"Land entry confirmation missing/oversize");
            yield return Shot(locale,"genesis-confirm-window");Click(yesNo.transform.Find("ValueOption_"+ui.OptionIndex("ok")).GetComponent<Button>());Check(calls==1&&sent==ui.OptionIndex("ok"),"Land entry confirmation ID");

            foreach(string name in new[]{"wrath-two-number-0","wrath-two-number-3","wrath-two-select-0"}) {
                var v=Original(name);calls=0;ui.Present(null,1,false,null,null,"");ui.Present(v,1,true,i=>{sent=i;calls++;},null,"");yield return Frame();CleanPanels();
                var number=Root.Find("CornerCluster/ControlRow/CardAbilityChoices");Check(number!=null&&number.gameObject.activeInHierarchy,"Number chooser not docked");DockBounds(number);
                Check(GameObject.Find("DismissAbilityChoices")==null&&GameObject.Find("ViewBattlefield")==null,"Dock unnecessarily masks battlefield");
                var plus=number.Find("Increase").GetComponent<Button>();var minus=number.Find("Decrease").GetComponent<Button>();
                if(name=="wrath-two-number-0") {
                    Check(!minus.interactable,"Native lower bound ignored");int plusIndex=ui.OptionIndex("number-plus");ui.Choose(plusIndex,v.request.requestId-1);Check(calls==0,"Stale number accepted");Click(plus);Check(calls==1&&sent==plusIndex,"Native increment ID");
                } else if(name=="wrath-two-number-3") {
                    yield return Shot(locale,"x-number-dock");var accept=number.Find("ConfirmValue").GetComponent<Button>();Click(accept);accept.onClick.Invoke();Check(calls==1&&sent==ui.OptionIndex("confirm-number"),"Wrong/duplicate X confirmation");
                } else {
                    Check(!minus.interactable,"Energy lower bound ignored");for(int n=0;n<3;n++)Click(plus);yield return Frame();Check(!plus.interactable&&number.Find("NumericValue").GetComponent<Text>().text=="3"&&calls==0,"Energy upper bound/local draft");
                    yield return Shot(locale,"energy-number-dock");Click(number.Find("ConfirmValue").GetComponent<Button>());Check(calls==1&&v.request.options[sent].label=="3","Energy confirmation ID");
                }
                ui.Present(v,1,false,_=>calls++,null,"");yield return Frame();Check(number.GetComponentsInChildren<Button>().All(b=>!b.interactable),"Nonacting numeric dock enabled");
            }

            var pay=Read(".tools/payment-dock-1005/native/tax-auto-payment.json");calls=0;ui.Present(pay,1,true,i=>{sent=i;calls++;},null,"");yield return Frame();CleanPanels();DockBounds(Root.Find("CornerCluster/ControlRow/ManaPaymentActions"));yield return Shot(locale,"payment-dock");Click(Root.Find("CornerCluster/ControlRow/ManaPaymentActions/AutoPayment").GetComponent<Button>());Check(calls==1&&sent==ui.OptionIndex("ok"),"Payment dock ID");
            var shock=Read(".tools/payment-dock-1005/native/shock-yes-confirm.json");ui.Present(shock,1,true,_=>{},null,"");yield return Frame();CleanPanels();Check(((RectTransform)GameObject.Find("CardAbilityChoices").transform).rect.height<200,"Shockland oversize");
            yield return Shot(locale,"shockland-compact");

            var top=Read(Output+"/native/explore-keep-resolved.json");calls=0;ui.Present(null,1,false,null,null,"");ui.Present(top,1,true,_=>calls++,null,"");yield return Frame();CleanPanels();Click(Root.Find("ZonePiles_PlayerBottomLeft/Pile_牌库").GetComponent<Button>());yield return Frame();
            var browser=GameObject.Find("CardSelection");Check(browser!=null&&browser.GetComponentsInChildren<Transform>().Any(t=>t.name=="ChoiceCard_"+top.players[0].libraryTop.id)&&calls==0,"Known top not in readonly region browser");
            yield return Shot(locale,"known-library-browser");Click(browser.transform.Find("SelectionFooter/CloseReviewCards").GetComponent<Button>());
            var hidden=Original("genesis-yes-before");ui.Present(hidden,1,true,_=>calls++,null,"");yield return Frame();Click(Root.Find("ZonePiles_PlayerBottomLeft/Pile_牌库").GetComponent<Button>());yield return Frame();browser=GameObject.Find("CardSelection");Check(!browser.GetComponentsInChildren<Transform>().Any(t=>t.name.StartsWith("ChoiceCard_")),"Unseen library identities leaked");Click(browser.transform.Find("SelectionFooter/CloseReviewCards").GetComponent<Button>());

            var known=Memory("hand-known-priority");ui.Present(known,1,false,null,null,"");yield return Frame();Check(known.players[1].hand.Length==known.players[1].handCount&&Root.Find("OpponentHandRoot").GetComponentsInChildren<Button>().Length==known.players[1].handCount,"Known opponent hand not rendered");yield return Shot(locale,"hand-known-after-thoughtseize");
            var mixed=Memory("hand-mixed-priority");ui.Present(mixed,1,false,null,null,"");yield return Frame();Check(mixed.players[1].hand.Length==0&&Root.Find("OpponentHandRoot").GetComponentsInChildren<Button>().Length==0,"Old known identities survive hidden return");yield return Shot(locale,"hand-hidden-after-brainstorm");
            var spectator=OnlineSpectatorView.Create(new OnlinePlayerViewState{forge=known},0).forge;Check(spectator.players.All(p=>(p.hand?.Length??0)==0),"Known private hands leak to spectator");
        }
    }
}
#endif
