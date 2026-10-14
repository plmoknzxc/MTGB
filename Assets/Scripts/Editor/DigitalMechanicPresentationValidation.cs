#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Replays real native inputs; engine effect assertions live in Test-DigitalMechanics.py.
public static class DigitalMechanicPresentationValidation
{
    const string Output=".tools/digital-mechanics-ui";
    static bool running;
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run()
    {
        if(running)return;
        Check(Application.isPlaying,"Enter IntegratedBattlefield Play Mode first.");
        running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("DigitalMechanicValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator steps)
    {
        string original=GamePreferences.Language;
        try {
            while(true) {
                object next;
                try {if(!steps.MoveNext())break;next=steps.Current;}
                catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}
                yield return next;
            }
            File.WriteAllText(Output+"/result.txt","PASS: real Conjure/Draft/Seek/perpetual snapshots in zh-CN/en-US/ja-JP; exact native choice IDs and explicit confirmation; three draft objects with full metadata and local art or missing label; duplicate/stale/viewer guards; current modified stats before/after expiry and recast; private hand projection. Four real name-prohibition response snapshots verify AUTO policy and rendered mode-button raycast cycle. Replays do not verify network/timer delivery or every translated rule/art.");
        } finally {running=false;GamePreferences.SetLanguage(original);}
    }
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/DigitalMechanics/"+name+".json"));
    static void Click(Button button)
    {
        Check(button!=null&&button.interactable,"Button unavailable");Canvas.ForceUpdateCanvases();
        var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Choice raycast blocked: "+button.name+" active="+button.gameObject.activeInHierarchy+" depth="+button.GetComponent<Image>().depth+" position="+p.position+" hits="+string.Join(",",hits.Take(4).Select(h=>h.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Shot(string name)
    {Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForEndOfFrame();}
    static IEnumerator Rendered()
    {
        // A coroutine started by EditorApplication.update can run before the
        // newly registered graphics receive their first Canvas render depth.
        yield return new WaitForEndOfFrame();yield return null;
        Canvas.ForceUpdateCanvases();
    }
    static void ModalBlocksMode()
    {
        var mode=GameObject.Find("PriorityControlMode").GetComponent<Button>();
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,mode.transform.TransformPoint(((RectTransform)mode.transform).rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()!=mode,"Mode button bypasses modal choice blocker");
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            bool changing=GamePreferences.Language!=language;int revision=GamePreferences.Revision;
            GamePreferences.SetLanguage(language);float deadline=Time.realtimeSinceStartup+8;
            while((!GamePreferences.Ready||changing&&GamePreferences.Revision==revision)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(GamePreferences.Ready&&(!changing||GamePreferences.Revision!=revision),"Locale not loaded");
            Check(GamePreferences.CardFaceName("Shock")== (language=="zh-CN"?"电震":language=="ja-JP"?"ショック":"Shock"),"Current locale name not ready");
            foreach(string test in new[]{"conjure-ionize","conjure-electrolyze"}) {
                var view=Read(test+"-choice");int calls=0,last=-1;
                ui.Present(null,1,false,null,null,"");ui.Present(view,1,true,i=>{calls++;last=i;},null,"");yield return Rendered();
                var menu=GameObject.Find("CardAbilityChoices");Check(menu!=null,"Conjure name picker missing");
                ModalBlocksMode();
                var rows=menu.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("NameOption_")).ToArray();
                Check(rows.Length==2,"Conjure subset became full card pool");
                string canonical=test.EndsWith("ionize")?"Ionize":"Electrolyze";
                int expected=Array.FindIndex(view.request.options,o=>o.cardName==canonical);
                var row=rows.Single(b=>b.name=="NameOption_"+expected);
                Check(row.GetComponentInChildren<Text>().text==GamePreferences.CardFaceName(canonical),"Conjure candidate name not localized");
                Click(row);yield return Rendered();Check(calls==0,"Conjure selected before explicit confirmation");
                Check(menu.GetComponentsInChildren<Text>().All(t=>!t.resizeTextForBestFit),"Conjure name font resizes");
                var art=menu.transform.Find("NamePreview").GetComponent<Image>();
                Check(art.sprite!=null||!string.IsNullOrWhiteSpace(art.transform.Find("MissingArt").GetComponent<Text>().text),"Conjure missing local art without indicator");
                yield return Shot(language+"-"+test);
                ui.Choose(expected,view.request.requestId-1);Check(calls==0,"Conjure stale request accepted");
                var confirm=menu.transform.Find("NameConfirm").GetComponent<Button>();Click(confirm);confirm.onClick.Invoke();Check(calls==1&&last==expected,"Conjure index or duplicate guard incorrect");
                ui.Present(null,1,false,null,null,"");ui.Present(view,1,false,i=>calls++,null,"");yield return Rendered();
                Check(GameObject.Find("CardAbilityChoices").GetComponentsInChildren<Button>().All(b=>!b.interactable),"Conjure viewer can choose");
            }
            foreach(string test in new[]{"draft-first","draft-last"}) {
                var view=Read(test+"-choice");int calls=0,last=-1;
                ui.Present(null,1,false,null,null,"");ui.Present(view,1,true,i=>{calls++;last=i;},null,"");yield return Rendered();
                var picker=GameObject.Find("CardSelection");Check(picker!=null,"Draft picker missing");
                ModalBlocksMode();
                var options=view.request.options.Where(o=>o.card!=null).ToArray();Check(options.Length==3,"Draft lost candidates");
                foreach(var option in options) {
                    var card=option.card;Check(!string.IsNullOrEmpty(card.printingKey)&&!string.IsNullOrEmpty(card.currentRules)&&!string.IsNullOrEmpty(card.currentType),"Draft metadata incomplete");
                    var tile=picker.GetComponentsInChildren<Transform>().Single(t=>t.name=="ChoiceCard_"+card.id);
                    var image=tile.Find("Artwork").GetComponent<Image>();var missing=image.transform.Find("CardName").GetComponent<Text>();
                    Check(image.sprite!=null||missing.gameObject.activeInHierarchy&&!string.IsNullOrWhiteSpace(missing.text),"Draft lacks local art and missing fallback");
                }
                Check(!picker.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("SelectionAction_")),"Mandatory draft has premature confirmation/cancellation");
                var chosen=options[test.EndsWith("last")?2:0];int expected=Array.IndexOf(view.request.options,chosen);
                ui.Choose(expected,view.request.requestId-1);Check(calls==0,"Draft stale ID accepted");
                var face=picker.GetComponentsInChildren<Transform>().Single(t=>t.name=="ChoiceCard_"+chosen.card.id).Find("Artwork").GetComponent<Button>();
                yield return Shot(language+"-"+test+"-choices");Click(face);face.onClick.Invoke();Check(calls==1&&last==expected,"Draft native option index/duplicate guard incorrect");
                var selected=Read(test+"-selected");calls=0;ui.Present(selected,1,true,i=>{calls++;last=i;},null,"");yield return Rendered();
                Check(GameObject.Find("PriorityPill").transform.Find("Text").GetComponent<Text>().text=="等待你完成选择","Draft update retains submitting status after new native request");
                int confirm=Array.FindIndex(selected.request.options,o=>o.id=="confirm-cards");Check(confirm>=0,"Draft explicit confirmation missing");
                Click(GameObject.Find("CardSelection").GetComponentsInChildren<Button>().Single(b=>b.name=="SelectionAction_"+confirm));Check(calls==1&&last==confirm,"Draft confirmation maps wrong index");
                ui.Present(null,1,false,null,null,"");ui.Present(view,1,false,i=>calls++,null,"");yield return Rendered();
                Check(GameObject.Find("CardSelection").GetComponentsInChildren<Button>().All(b=>!b.interactable),"Draft viewer can choose");
            }
            foreach(string test in new[]{"perpetual-same","perpetual-split"})foreach(string state in new[]{"before","both-modifiers","expired","recast"}) {
                var view=Read(test+"-"+state);ui.Present(null,1,false,null,null,"");ui.Present(view,1,true,_=>{},null,"");yield return Rendered();
                foreach(var card in view.players[0].battlefield.Where(c=>c.name=="Grizzly Bears"||c.name=="Runeclaw Bear")) {
                    string expected=card.power==2?"2/2":(card.power<0?"<color=#D12D46>-2</color>/2":"<color=#D12D46>0</color>/2");
                    var root=GameObject.Find("Card_"+card.id);Check(root!=null,"Perpetual battlefield object missing");
                    var stat=root.GetComponentsInChildren<Text>().Single(t=>t.name=="Value"&&t.transform.parent.name=="Stats");
                    Check(stat.text==expected,"Displayed perpetual/temporary stat or color incorrect: "+state+" "+stat.text);
                }
                yield return Shot(language+"-"+test+"-"+state);
            }
            foreach(string test in new[]{"seek","seek-empty","conjure-ionize","conjure-electrolyze","draft-first","draft-last"}) {
                var view=Read(test+"-resolved");ui.Present(null,1,false,null,null,"");ui.Present(view,1,true,_=>{},null,"");yield return Rendered();
                Check(GameObject.Find("CardSelection")==null&&GameObject.Find("CardAbilityChoices")==null,"Completed digital effect leaves choice UI open");
                var other=OnlineSpectatorView.Create(new OnlinePlayerViewState{forge=view},3).forge;
                Check(other.players.All(p=>(p.hand??Array.Empty<ForgeCardView>()).Length==0),"Spectator sees digital private cards");
                if(test.StartsWith("seek"))yield return Shot(language+"-"+test+"-resolved");
            }
            foreach(string test in new[]{"mage-only","mage-other","needle-only","needle-other"}) {
                var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/CardNames/"+test+"-response.json"));
                bool other=test.EndsWith("other");int ok=Array.FindIndex(view.request.options,o=>o.id=="ok");
                Check(view.canRespond==other,"Native name prohibition response projection incorrect");
                Check(ForgeControlPolicy.AutomaticOption(view,2,ForgeControlMode.Auto,false)==(other?-1:ok),"AUTO does not respect native response availability");
                Check(ForgeControlPolicy.AutomaticOption(view,2,ForgeControlMode.On,false)==-1&&ForgeControlPolicy.AutomaticOption(view,2,ForgeControlMode.Off,false)==ok,"ON/OFF policy incorrect");
                ui.Present(null,2,false,null,null,"");ui.Present(view,2,true,_=>{},null,"");yield return Rendered();
                var mode=GameObject.Find("PriorityControlMode").GetComponent<Button>();
                for(int i=0;i<3&&mode.GetComponentInChildren<Text>().text!="AUTO";i++)Click(mode);
                foreach(string label in new[]{"AUTO","OFF","ON"}) {
                    Check(mode.GetComponentInChildren<Text>().text==label,"Rendered mode cycle incorrect");
                    Click(mode);
                }
                Check(mode.GetComponentInChildren<Text>().text=="AUTO","Mode cycle did not return to AUTO");
                yield return Shot(language+"-"+test+"-AUTO");
            }
            ui.Present(null,1,false,null,null,"");yield return null;
        }
    }
}
#endif
