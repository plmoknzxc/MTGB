#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class BeholdExilePresentationValidation
{
    const string Base=".tools/behold-exile-1008";
    public static bool Running{get;private set;}
    public static string Output{get;private set;}
    static int checks;
    static void Check(bool value,string why){if(!value)throw new Exception(why);checks++;}
    static IEnumerator Frame(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Output+"/inputs/"+name+".json"));
    static Button Named(string name)=>GameObject.Find(name)?.GetComponent<Button>()??throw new Exception("Missing "+name);
    static void Click(Button button){
        var c=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(c);
        for(int y=1;y<7;y++)for(int x=1;x<7;x++){
            var point=new Vector2(Mathf.Lerp(c.Min(v=>v.x),c.Max(v=>v.x),x/7f),Mathf.Lerp(c.Min(v=>v.y),c.Max(v=>v.y),y/7f));
            var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=point};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
            if(!button.interactable||hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=button)continue;
            Check(true,"Exposed actual button ray "+button.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);return;
        }
        throw new Exception("No exposed ray "+button.name);
    }
    public static void Run(string[] languages=null){
        if(!Application.isPlaying||Running)throw new Exception("Play required/already running");Running=true;checks=0;
        Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");
        File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        foreach(string p in Directory.GetFiles(Base+"/ui-inputs"))File.Copy(p,Output+"/inputs/"+Path.GetFileName(p));
        foreach(string p in new[]{"Assets/Scripts/Editor/BeholdExilePresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.CardSelection.cs","Assets/Scripts/UI/ForgeBattlefieldController.StackTargets.cs","Assets/Scripts/UI/ForgeBattlefieldController.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("BeholdValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui,languages??new[]{"zh-CN","en-US","ja-JP"})));
    }
    static IEnumerator Guard(IEnumerator work){
        var tasks=new Stack<IEnumerator>();tasks.Push(work);
        try{while(tasks.Count>0){object next;try{var t=tasks.Peek();if(!t.MoveNext()){tasks.Pop();continue;}next=t.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator child)tasks.Push(child);else yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: "+checks+" checks; actual typed BeholdExile input, mixed-zone exact same-name identities/zone labels, hand and battlefield card rays, selected/reset/confirm/cancel/stale/duplicate/readonly lifecycle and three-language text/geometry. Existing same-zone revealed hand has no extra origin row. No new UI first/global/network claim.");}
        finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui,string[] languages){
        string original=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;
        var module=EventSystem.current.currentInputModule;bool moduleEnabled=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var banners=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=banners.Select(b=>b.enabled).ToArray();foreach(var b in banners)b.enabled=false;
        try{
            float initialized=Time.realtimeSinceStartup+20;
            while(!GamePreferences.Ready&&Time.realtimeSinceStartup<initialized)yield return null;
            Check(GamePreferences.Ready,"Initial scene preferences initialized before requesting a locale");
            foreach(string language in languages){
                int revision=GamePreferences.Revision;bool change=language!=GamePreferences.Language;GamePreferences.SetLanguage(language);float until=Time.realtimeSinceStartup+12;
                while((!GamePreferences.Ready||change&&GamePreferences.Revision==revision)&&Time.realtimeSinceStartup<until)yield return null;
                Check(GamePreferences.Ready&&GamePreferences.Language==language,"Exact language ready "+language);
                var initial=Read("initial");var board=Read("board-selected");var reset=Read("reset");var hand=Read("hand-selected");
                Check(initial.request.selectionPurpose=="beholdExile"&&initial.request.sourceCardId==121&&initial.request.kind=="InputSelectCardsFromList","Typed native context and preserved wire contract");
                int calls=0,sent=-1;Action<int> submit=i=>{calls++;sent=i;};
                ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(initial,1,true,submit,null,"");yield return Frame();
                var panel=GameObject.Find("CardSelection");Check(panel!=null,"Mixed zones stay in shared modal");
                var cards=initial.request.options.Where(o=>o.card!=null&&o.card.id>0).Select(o=>o.card).ToArray();Check(cards.Length==2&&cards[0].name==cards[1].name,"Actual same-name original candidates");
                foreach(var card in cards){
                    var tile=panel.transform.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="ChoiceCard_"+card.id);
                    var label=tile.Find("OriginZone").GetComponent<Text>();string expected=card.zone=="Hand"?(language=="zh-CN"?"手牌":language=="ja-JP"?"手札":"Hand"):(language=="zh-CN"?"战场":language=="ja-JP"?"戦場":"Battlefield");
                    Check(label.text==expected&&!label.raycastTarget&&!label.resizeTextForBestFit,"Localized nonblocking fixed origin "+card.id);
                    Check(label.preferredHeight<=label.rectTransform.rect.height+2&&label.preferredWidth<=label.rectTransform.rect.width+2,"Origin text fits "+card.id);
                    Check(tile.Find("Artwork").GetComponent<Image>().sprite!=null,"Actual original printed face "+card.id);
                    Check(tile.Find("Artwork").GetComponent<Button>().interactable,"Both exact origins selectable "+card.id);
                }
                var prompt=panel.transform.Find("Instruction").GetComponent<Text>();
                Check(!prompt.resizeTextForBestFit&&prompt.preferredHeight<=prompt.rectTransform.rect.height+2,"Payment instruction fixed size and fits");
                Check(prompt.text.Contains(language=="zh-CN"?"放逐":language=="ja-JP"?"追放":"exile")&&!prompt.text.Contains("to reveal"),"Correct actual exile cost instruction");
                ScreenCapture.CaptureScreenshot(Output+"/"+language+"-mixed.png");yield return Frame();
                int boardIndex=Array.FindIndex(initial.request.options,o=>o.id=="card:130");Click(GameObject.Find("ChoiceCard_130").transform.Find("Artwork").GetComponent<Button>());Check(calls==1&&sent==boardIndex,"Original battlefield choice index");
                Check(!ui.TrySubmitPresentedOption(boardIndex,initial.request.requestId)&&calls==1,"Duplicate blocked");
                ui.Present(board,1,true,submit,null,"");yield return Frame();Check(board.request.selectedCount==1,"Actual selected battlefield state");
                Check(!ui.TrySubmitPresentedOption(boardIndex,initial.request.requestId)&&calls==1,"Stale prior input blocked");
                int resetIndex=Array.FindIndex(board.request.options,o=>o.id=="reset-cards");Click(Named("SelectionAction_"+resetIndex));Check(calls==2&&sent==resetIndex,"Native reset index");
                ui.Present(reset,1,true,submit,null,"");yield return Frame();int handIndex=Array.FindIndex(reset.request.options,o=>o.id=="card:122");Click(GameObject.Find("ChoiceCard_122").transform.Find("Artwork").GetComponent<Button>());Check(calls==3&&sent==handIndex,"Original hand choice index");
                ui.Present(hand,1,true,submit,null,"");yield return Frame();int ok=Array.FindIndex(hand.request.options,o=>o.id=="ok");Click(Named("SelectionAction_"+ok));Check(calls==4&&sent==ok,"Native confirmation index");
                var retry=Read("retry");ui.Present(retry,1,true,submit,null,"");yield return Frame();int cancel=Array.FindIndex(retry.request.options,o=>o.id=="cancel");Click(Named("SelectionAction_"+cancel));Check(calls==5&&sent==cancel,"Native cancel index");
                var restored=Read("restored");ui.Present(restored,1,true,submit,null,"");yield return Frame();Check(GameObject.Find("CardSelection")==null,"Restored priority removes window");
                ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(initial,1,false,submit,null,"");yield return Frame();
                Check(!GameObject.Find("ChoiceCard_122").transform.Find("Artwork").GetComponent<Button>().interactable&&!ui.TrySubmitPresentedOption(boardIndex,initial.request.requestId)&&calls==5,"Informational readonly view cannot submit");
                ui.Present(null,1,false,null,null,"");yield return Frame();var ordinary=Read("ordinary");ui.Present(ordinary,1,true,submit,null,"");yield return Frame();
                var plainPrompt=GameObject.Find("CardSelection").transform.Find("Instruction").GetComponent<Text>();
                Check(ordinary.request.selectionPurpose=="behold"&&!plainPrompt.text.Contains(language=="zh-CN"?"放逐":language=="ja-JP"?"追放":"exile"),"Ordinary Behold stays reveal/controlled permanent choice");
                Check(plainPrompt.preferredHeight<=plainPrompt.rectTransform.rect.height+2,"Ordinary typed instruction fits");
                File.AppendAllText(Output+"/steps.log",language+" exact origins/payment/pick/reset/confirm/cancel/guards PASS\n");
            }
            ui.Present(null,2,false,null,null,"");yield return Frame();var same=Read("same-zone");ui.Present(same,2,true,i=>{},null,"");yield return Frame();
            Check(GameObject.Find("CardSelection")!=null&&!GameObject.Find("CardSelection").GetComponentsInChildren<Text>().Any(t=>t.name=="OriginZone"),"Unchanged same-zone revealed hand keeps existing layout");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=moduleEnabled;GamePreferences.SetLanguage(original);for(int i=0;i<banners.Length;i++)if(banners[i]!=null)banners[i].enabled=flags[i];}
    }
}
#endif
