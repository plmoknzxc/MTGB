#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CardNamePresentationValidation
{
    const string Output=".tools/card-names-ui";
    static bool running;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run()
    {
        if(running)return;
        if(!Application.isPlaying)throw new Exception("Enter IntegratedBattlefield Play Mode first.");
        running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/timing.txt","");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("CardNameValidation").AddComponent<ForgeBattlefieldController>();
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
            File.WriteAllText(Output+"/result.txt","PASS: real full-pool/land/split/Cancel/revealed-name requests replayed in zh-CN/en-US/ja-JP; bounded distinct page rows, filtering and paging, local art or missing label, exact native option index and confirmation, fixed font, mandatory dismissal, duplicate/stale/viewer guards. Optional cancellation is a separate synthetic adapter case. Engine effect assertions remain Test-CardNames.py.");
        } finally {running=false;GamePreferences.SetLanguage(original);}
    }
    static GameObject Menu=>GameObject.Find("CardAbilityChoices");
    static Button Find(string name)=>Menu.transform.Find(name).GetComponent<Button>();
    static Button[] Rows()=>Menu.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("NameOption_")).ToArray();
    static InputField Search=>Menu.transform.Find("NameSearch").GetComponent<InputField>();
    static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Name choice raycast blocked: "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Shot(string name)
    {
        Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForEndOfFrame();
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool changing=GamePreferences.Language!=language;
            GamePreferences.SetLanguage(language);float deadline=Time.realtimeSinceStartup+8;
            while((!GamePreferences.Ready||changing&&GamePreferences.Revision==revision)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(GamePreferences.Ready&&(!changing||GamePreferences.Revision!=revision),"Locale not loaded");
            Check(GamePreferences.CardFaceName("Shock")== (language=="zh-CN"?"电震":language=="ja-JP"?"ショック":"Shock"),"Known name not translated in current language");
            foreach(string test in new[]{"therapy-hit","therapy-split","therapy-cancel","needle-mana","arbiter"}) {
                var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/CardNames/"+test+"-name-choice.json"));
                int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};
                ui.Present(null,1,false,null,null,"");
                float start=Time.realtimeSinceStartup;ui.Present(view,1,true,submit,null,"");
                float elapsed=Time.realtimeSinceStartup-start;
                yield return new WaitForSecondsRealtime(.15f);Canvas.ForceUpdateCanvases();
                Check(Menu!=null&&Menu.activeInHierarchy,"Name picker absent");
                var rows=Rows();Check(rows.Length>0&&rows.Length<=8,"Name list creates an unbounded number of buttons");
                Check(rows.Select(b=>view.request.options[int.Parse(b.name.Substring(11))].cardName).Distinct().Count()==rows.Length,"Duplicate names on page");
                Check(!Find("NameConfirm").interactable,"No name selected but confirmation enabled");
                Check(Menu.GetComponentsInChildren<Text>().All(t=>!t.resizeTextForBestFit),"Name font auto-resizes");
                File.AppendAllText(Output+"/timing.txt",language+" "+test+" candidates="+view.request.options.Length+" initialSeconds="+elapsed.ToString("F3")+" buttons="+rows.Length+"\n");
                Check(elapsed<8,"Name UI initial construction exceeds eight seconds on this editor");
                yield return Shot(language+"-"+test+"-initial");
                if(test!="arbiter") {
                    Check(Find("NameNext").interactable,"Full list cannot page");
                    var first=rows.Select(b=>b.name).ToArray();Click(Find("NameNext"));yield return null;
                    Check(!Rows().Select(b=>b.name).SequenceEqual(first),"Page did not advance");
                    Check(Find("NamePrevious").interactable,"Cannot return to previous page");Click(Find("NamePrevious"));yield return null;
                    Check(Rows().Select(b=>b.name).SequenceEqual(first),"Previous page changed original indexes");
                } else Check(!Find("NameNext").interactable,"Revealed subset expanded to full pool");
                Search.text="NO_SUCH_NAME_98342";yield return null;
                Check(Rows().Length==0&&!Find("NameConfirm").interactable,"Empty filtered list actionable");
                string canonical=test=="therapy-split"?"Fire":test=="therapy-cancel"?"Cancel":test=="needle-mana"?"Mountain":"Shock";
                Search.text=canonical;yield return null;
                int expected=Array.FindIndex(view.request.options,o=>o.cardName==canonical);
                Check(expected>=0,"Native candidate missing");
                var row=Rows().FirstOrDefault(b=>b.name=="NameOption_"+expected);Check(row!=null,"Filter lost exact candidate");
                Check(row.GetComponentInChildren<Text>().text==GamePreferences.CardFaceName(canonical),"Displayed name differs from current exact localization");
                Click(row);yield return null;Check(calls==0,"Row selection submits before explicit confirmation");
                Check(Find("NameConfirm").interactable,"Selected candidate cannot confirm");
                var art=Menu.transform.Find("NamePreview").GetComponent<Image>();
                var missing=art.transform.Find("MissingArt").GetComponent<Text>();
                Check(art.sprite!=null||!string.IsNullOrWhiteSpace(missing.text),"Local image missing without indicator");
                Check(Menu.transform.Find("NameCanonical").GetComponent<Text>().text==canonical,"Preview canonical name differs");
                yield return Shot(language+"-"+test+"-selected");
                string localized=GamePreferences.CardFaceName(canonical);Search.text=localized;yield return null;
                Check(Rows().Any(b=>b.name=="NameOption_"+expected),"Localized filter loses candidate");
                var dismiss=GameObject.Find("DismissAbilityChoices").GetComponent<ForgeChoiceDismiss>();
                dismiss.Dismiss();Menu.GetComponent<ForgeChoiceDismiss>().Dismiss();Check(calls==0,"Mandatory naming has invented cancellation");
                Check(Menu.transform.Find("NameCancel")==null,"Real card Cancel becomes cancellation button");
                ui.Choose(expected,view.request.requestId-1);Check(calls==0,"Stale ID submitted while actionable");
                var confirm=Find("NameConfirm");Click(confirm);confirm.onClick.Invoke();
                Check(calls==1&&last==expected,"Exact original index or duplicate guard failed");
                ui.Present(view,1,false,submit,null,"");yield return null;
                Check(Menu.GetComponentsInChildren<Button>().All(b=>!b.interactable)&&!Search.interactable,"Viewer can change selection");
                ui.Choose(expected,view.request.requestId);Check(calls==1,"Viewer submitted");
            }
            // Adapter-only optional-name contract: cancellation is supplied by the server.
            var optional=new ForgeSeatView{seat=1,awaitingInput=true,players=Array.Empty<ForgePlayerView>(),request=new ForgeRequest{
                requestId=990008,seat=1,kind="dialog",selectionPurpose="cardName",options=new[]{new ForgeOption{id="choice:0",cardName="Cancel",label="Cancel"},new ForgeOption{id="cancel",label="Cancel"}}}};
            int cancelCalls=0,cancelIndex=-1;ui.Present(null,1,false,null,null,"");ui.Present(optional,1,true,i=>{cancelCalls++;cancelIndex=i;},null,"");yield return null;
            Check(Rows().Length==1,"Optional card Cancel omitted");Click(Find("NameCancel"));Check(cancelCalls==1&&cancelIndex==1,"Native optional cancellation mapped to card name");
        }
    }
}
#endif
