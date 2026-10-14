#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class DiscoverPresentationValidation {
    const string Base=".tools/discover-1006/ui";
    public static string Output {get;private set;}
    public static bool Running {get;private set;}
    public static Coroutine Handle {get;private set;}
    [Serializable] class Meta {public string folder,status,runtimeHash;}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Hash(string file){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant();}
    static ForgeSeatView Read(string name,string label) {
        string metaPath=".tools/discover-1006/native/"+name+"-result.json";
        var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(metaPath));
        Check(meta.status=="PASS"&&meta.runtimeHash==JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json")).runtimeHash,"Stale native result");
        string source=Path.Combine(meta.folder,label+".json"),copy=Path.Combine(Output,"inputs",name+"-"+label+".json");
        File.Copy(source,copy,true);File.Copy(metaPath,Path.Combine(Output,"inputs",name+"-meta.json"),true);
        File.AppendAllText(Output+"/input-hashes.tsv",name+"/"+label+"\t"+Hash(source)+"\t"+meta.runtimeHash+"\n");
        return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(copy));
    }
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static string ChoiceContract(ForgeSeatView v) {
        var q=v.request;var c=q.previewCards.Single();
        return string.Join("/",q.kind,q.selectionPurpose,q.minCount,q.maxCount,q.selectedCount,q.remainingCount,q.selectedValue,q.requiredValue,q.remainingManaCost,
            c.zone,c.faceDown,c.planeswalker,c.selection,v.awaitingInput,v.canRespond,
            string.Join("|",q.options.Select(o=>o.id+":"+o.label+":"+o.selected+":"+o.disabled+":"+o.requiresPaymentCheck+":"+o.order+":"+(!string.IsNullOrEmpty(o.card?.name)))));
    }
    static void Click(Button b) {
        var rect=(RectTransform)b.transform;var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(b.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Ray blocked "+b.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    public static void Run(){Check(Application.isPlaying,"Play required");Check(!Running,"Discover validation already running");Running=true;Output=Base+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");File.WriteAllText(Base+"/latest.txt",Output);File.WriteAllText(Base+"/result.txt","RUNNING");File.WriteAllText(Output+"/producer-hashes.tsv",string.Join("\n",new[]{"Assets/Scripts/Editor/DiscoverPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.cs","Assets/Scripts/UI/ForgeBattlefieldController.CardSelection.cs","Assets/Scripts/Networking/ForgeWire.cs"}.Select(p=>p+"\t"+Hash(p))));File.WriteAllText(Output+"/screen.txt",Screen.width+"x"+Screen.height);var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("DiscoverValidation").AddComponent<ForgeBattlefieldController>();Handle=ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var e=Validate(ui);try {while(true){object c;try{if(!e.MoveNext())break;c=e.Current;}catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}yield return c;}File.WriteAllText(Output+"/result.txt","PASS: native Discover Cast/Hand labels, localized instruction and real rays in three locales; zh-CN seat-two and repeated-choice lifecycle; both-seat reveal identity/confirm with readonly cards; native empty next-priority cleanup; stale/duplicate/nonacting guards and View/Return stable title/instruction. Eight found-card requests compared for the same picker contract; x-zero/kicker/floor/activated reuse representative clicks. Native rule results, unlisted long-name layouts and real networking are separate.");}finally{(e as IDisposable)?.Dispose();if(File.Exists(Output+"/result.txt"))File.Copy(Output+"/result.txt",Base+"/result.txt",true);Running=false;Handle=null;}}
    static IEnumerator Validate(ForgeBattlefieldController ui) {
        string original=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;
        var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try {foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool change=lang!=GamePreferences.Language;GamePreferences.SetLanguage(lang);float deadline=Time.realtimeSinceStartup+20;
            while((!GamePreferences.Ready||change&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(GamePreferences.Ready,"Language not ready");yield return Frame();
            var representative=Read("cast","choice-0");
            if(lang=="zh-CN")foreach(string sample in new[]{"hand","repeat","seat-two","x-zero","kicker","floor","activated"})Check(ChoiceContract(Read(sample,"choice-0"))==ChoiceContract(representative),"Different picker contract "+sample);
            foreach(string name in lang=="zh-CN"?new[]{"cast","hand","repeat","seat-two"}:new[]{"cast","hand"}) {
                File.AppendAllText(Output+"/steps.log",lang+" choice "+name+"\n");
                var v=Read(name,"choice-0");int actor=v.request.seat,calls=0,last=-1;
                ui.Present(null,actor,false,null,null,"");ui.Present(v,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();
                var panel=GameObject.Find("CardSelection");Check(panel!=null,"Missing discover picker");
                var card=v.request.previewCards.Single();Check(panel.GetComponentsInChildren<Transform>().Any(t=>t.name=="ChoiceCard_"+card.id),"Wrong found identity");
                Check(panel.GetComponentsInChildren<RawImage>().Any(i=>i.texture!=null)||panel.GetComponentsInChildren<Image>().Any(i=>i.sprite!=null&&i.sprite.texture.width>300),"Missing found card art");
                var cast=panel.transform.Find("SelectionFooter/SelectionAction_"+ui.OptionIndex("ok")).GetComponent<Button>();
                var hand=panel.transform.Find("SelectionFooter/SelectionAction_"+ui.OptionIndex("cancel")).GetComponent<Button>();
                Check(cast.GetComponentInChildren<Text>().text==(lang=="zh-CN"?"施放":lang=="ja-JP"?"唱える":"Cast"),"Wrong Cast label");
                Check(hand.GetComponentInChildren<Text>().text==(lang=="zh-CN"?"放入手牌":lang=="ja-JP"?"手札に加える":"Put in hand"),"Wrong Hand label");
                Check(panel.GetComponentsInChildren<Button>().Count(b=>b.interactable&&b.name.StartsWith("SelectionAction_"))==2,"Extra/missing discover actions");
                var title=panel.transform.Find("Title").GetComponent<Text>();int font=title.fontSize;
                var instruction=panel.transform.Find("Instruction").GetComponent<Text>();int instructionFont=instruction.fontSize;
                Check(instruction.text.StartsWith(lang=="zh-CN"?"发现：":lang=="ja-JP"?"発見：":"Discover:"),"Instruction not localized");
                Check(!instruction.resizeTextForBestFit,"Instruction can resize on restore");
                Check(instruction.preferredHeight<=instruction.rectTransform.rect.height+1,"Instruction clipped");
                ui.ViewBattlefield();yield return Frame();var hidden=panel.GetComponentInParent<CanvasGroup>();Check(hidden!=null&&hidden.alpha==0&&!hidden.blocksRaycasts&&!hidden.interactable&&calls==0,"View does not hide/block picker");Check(!ui.TrySubmitPresentedOption(ui.OptionIndex("ok"),v.request.requestId),"Hidden picker submits");ui.RestoreBattlefieldWindows();yield return Frame();Check(title.fontSize==font&&instruction.fontSize==instructionFont&&calls==0,"Restore changes font/submits");
                Check(!ui.TrySubmitPresentedOption(ui.OptionIndex("ok"),v.request.requestId-1),"Stale accepted");
                bool toHand=name=="hand";var chosen=toHand?hand:cast;int expected=ui.OptionIndex(toHand?"cancel":"ok");
                Click(chosen);chosen.onClick.Invoke();Check(calls==1&&last==expected,"Wrong/duplicate discover choice");
                ui.Present(v,actor,false,i=>calls++,null,"");yield return Frame();Check(panel.GetComponentsInChildren<Button>().All(b=>!b.interactable),"Viewer can act");
                ui.Present(v,3-actor,false,i=>calls++,null,"");yield return Frame();Check(!ui.TrySubmitPresentedOption(expected,v.request.requestId),"Other seat acts");
                if(lang=="zh-CN"&&name=="cast"){ui.Present(v,actor,true,i=>{},null,"");yield return Frame();ScreenCapture.CaptureScreenshot(Output+"/discover-choice.png");yield return Frame();}
                ui.Present(Read(name,"resolved-"+(name=="repeat"?"2":"1")),actor,true,i=>calls++,null,"");yield return Frame();Check(!panel.activeInHierarchy&&calls==1,"Picker survives next priority");
                if(name=="repeat"){
                    var next=Read(name,"choice-1");Check(next.request.requestId!=v.request.requestId&&next.request.previewCards.Single().id!=card.id,"Repeated discover reuses identity");
                    int nextCalls=0,nextOption=-1;ui.Present(next,actor,true,i=>{nextCalls++;nextOption=i;},null,"");yield return Frame();
                    var nextPanel=GameObject.Find("CardSelection");Check(nextPanel.GetComponentsInChildren<Transform>().Any(t=>t.name=="ChoiceCard_"+next.request.previewCards.Single().id),"Next found card missing");
                    Check(!ui.TrySubmitPresentedOption(ui.OptionIndex("ok"),v.request.requestId),"Previous cycle accepted");
                    Click(nextPanel.transform.Find("SelectionFooter/SelectionAction_"+ui.OptionIndex("ok")).GetComponent<Button>());Check(nextCalls==1&&nextOption==ui.OptionIndex("ok"),"Next cycle blocked or wrong option");
                    ui.Present(Read(name,"resolved-2"),actor,true,i=>nextCalls++,null,"");yield return Frame();Check(!nextPanel.activeInHierarchy&&nextCalls==1,"Repeated picker not cleaned");
                }
            }
            if(lang=="zh-CN")foreach(string label in new[]{"step-24","step-26"}) {
                var reveal=Read("cast",label);Check(reveal.request.kind=="RevealCards","Not reveal request");int count=0,index=-1;
                ui.Present(null,reveal.request.seat,false,null,null,"");ui.Present(reveal,reveal.request.seat,true,i=>{count++;index=i;},null,"");yield return Frame();
                var panel=GameObject.Find("CardSelection");Check(panel!=null,"Reveal picker missing");
                foreach(var c in reveal.request.previewCards)Check(panel.GetComponentsInChildren<Transform>().Any(t=>t.name=="ChoiceCard_"+c.id),"Wrong revealed identity");
                Check(panel.GetComponentsInChildren<Button>().Where(b=>b.name=="Artwork").All(b=>!b.interactable),"Reveal art selectable");
                var confirm=panel.transform.Find("SelectionFooter/SelectionAction_0").GetComponent<Button>();Click(confirm);confirm.onClick.Invoke();Check(count==1&&index==0,"Reveal confirm wrong/duplicate");
                File.AppendAllText(Output+"/steps.log","reveal seat "+reveal.request.seat+"\n");
            }
            var empty=Read("empty","resolved-1");ui.Present(empty,empty.seat,true,i=>{},null,"");yield return Frame();Check(!Root.Find("CardSelectionOverlay").gameObject.activeInHierarchy,"Empty discover leaves picker");
        }}finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=input;GamePreferences.SetLanguage(original);for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
