#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Actual new text/context paths on existing confirm/picker/replacement/stack UI.
public static class ManaSpentEffectsPresentationValidation
{
    public static string Output {get;private set;}
    public static bool Running {get;private set;}
    [Serializable] class Meta {public string folder,status;}
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Click(Button b)
    {
        Canvas.ForceUpdateCanvases();var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.TransformPoint(((RectTransform)b.transform).rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Check(b.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Spent-mana ray blocked: "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    static ForgeSeatView Native(string name,string scenario,Func<ForgeSeatView,bool> matches)
    {
        var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/mana-spent-effects-1006/native/"+scenario+"-result.json"));Check(meta.status=="PASS","Native scenario incomplete");
        var file=Directory.GetFiles(meta.folder,"request-*-seat1.json").Select(path=>(path,view:JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path)))).First(x=>matches(x.view));
        File.Copy(file.path,Output+"/inputs/"+name+".json");File.AppendAllText(Output+"/inputs.tsv",name+"\t"+file.path+"\n");return file.view;
    }
    public static void Run()=>RunSelected("zh-CN,en-US,ja-JP","color,riot,foundry,stack");
    public static void RunSelected(string languages,string parts)
    {
        Check(Application.isPlaying&&!Running,"Play required/already running");Running=true;Output=".tools/mana-spent-effects-1006/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");File.WriteAllText(".tools/mana-spent-effects-1006/ui/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        Directory.CreateDirectory(Output+"/sources");foreach(string file in new[]{"Assets/Scripts/Editor/ManaSpentEffectsPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.CardSelection.cs","Assets/Scripts/UI/ForgeBattlefieldController.AbilityFace.cs","Assets/Scripts/UI/ForgeBattlefieldController.Notices.cs"})File.Copy(file,Output+"/sources/"+Path.GetFileName(file));
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ManaSpentValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui,languages,parts));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui,string languages,string parts)
    {
        File.WriteAllText(Output+"/selected.txt",languages+"\n"+parts+"\n");var pending=new Stack<IEnumerator>();pending.Push(Validate(ui,languages,parts));
        try {
            while(pending.Count>0){object next;try{var task=pending.Peek();if(!task.MoveNext()){pending.Pop();continue;}next=task.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator nested)pending.Push(nested);else yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: selected locales="+languages+" parts="+parts+"; actual Domri two-color card preview/native Riot entry and Foundry optional replacement; exact ok/cancel rays, native source/art and event identities, bounded fixed-size text/scrolling, hidden/stale/duplicate/nonacting guards. Actual Jade/Foundry trigger stack paragraphs translate with event tail retained when selected. Existing payment/scry/state/network proofs reused; no live-network or full effects claim.");
        } finally {while(pending.Count>0)(pending.Pop() as IDisposable)?.Dispose();Running=false;}
    }
    static IEnumerator Frame(){yield return null;yield return new WaitForSecondsRealtime(.35f);}
    static IEnumerator Shot(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForSecondsRealtime(.2f);}
    static IEnumerator Validate(ForgeBattlefieldController ui,string languages,string parts)
    {
        var color=Native("color","riot-counter",v=>v.request.kind=="InputConfirm"&&v.request.message.EndsWith("Select Mana to Produce"));
        var riot=Native("riot","riot-counter",v=>v.request.kind=="InputConfirm"&&v.request.message.StartsWith("Put 1 +1/+1"));
        var foundry=Native("foundry","foundry-exile",v=>v.request.selectionPurpose=="optionalReplacement");
        var jade=Native("jade-stack","jade-next-turn",v=>v.stackItems.Any(s=>s.kind!="spell"&&s.source.name=="Jade Orb of Dragonkind"));
        var forge=Native("foundry-stack","foundry-exile",v=>v.stackItems.Any(s=>s.kind!="spell"&&s.source.name=="Forger's Foundry"));
        bool enabled=ui.enabled;ui.enabled=false;string locale=GamePreferences.Language;var module=EventSystem.current.currentInputModule;bool active=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None).Select(n=>(n,n.enabled)).ToArray();foreach(var n in notices)n.n.enabled=false;
        var selected=parts.Split(',');try {foreach(string lang in languages.Split(',')) {
            int revision=GamePreferences.Revision;bool changed=lang!=GamePreferences.Language;GamePreferences.SetLanguage(lang);float until=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||changed&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale unavailable");
            foreach(var item in new[]{(name:"color",view:color),(name:"riot",view:riot),(name:"foundry",view:foundry)}) {
                if(!selected.Contains(item.name))continue;
                var v=item.view;int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};
                foreach(string id in new[]{"ok","cancel"}) {
                    ui.Present(null,1,false,null,null,"");ui.Present(v,1,true,submit,null,"");yield return Frame();
                    foreach(var pointer in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){pointer.ResetHover();pointer.enabled=false;}
                    var panel=GameObject.Find(item.name=="color"?"CardSelection":item.name=="foundry"?"AbilityOrder":"CardAbilityChoices");Check(panel!=null,"Missing actual contract panel: "+item.name);
                    string expected=item.name=="color"?(lang=="zh-CN"?"法术力颜色":lang=="ja-JP"?"マナの色":"mana color"):item.name=="riot"?(lang=="zh-CN"?"指示物":lang=="ja-JP"?"カウンター":"counter(s)"):(lang=="zh-CN"?"坟墓场":lang=="ja-JP"?"墓地":"graveyard");
                    var text=panel.GetComponentsInChildren<Text>().FirstOrDefault(t=>t.text.Contains(expected));Check(text!=null,"Missing translated text: "+item.name+"/"+lang);Check(!text.resizeTextForBestFit,"Choice text autofit: "+item.name);
                    if(item.name=="riot")Check(text.text.Contains(GamePreferences.CardFaceName("Bloodlust Inciter")),"Stack source name not localized");
                    Check(text.preferredHeight<=text.rectTransform.rect.height+1||text.GetComponentInParent<ScrollRect>()!=null,"Choice text clipped without scroll: "+item.name);
                    if(item.name=="foundry") {
                        var source=v.request.orderedAbilities[0].source;Check(source.effectSourceName=="Forger's Foundry"&&source.genericEffectName,"Effect source identity");var art=panel.GetComponentsInChildren<RawImage>().First(a=>a.name=="Art");Check(art.texture!=null,"Effect source art missing");Check(panel.transform.Find("Direction").GetComponent<Text>().text.Contains(GamePreferences.CardFaceName("Opt")),"Affected spell missing");
                    }
                    var fonts=panel.GetComponentsInChildren<Text>().Select(t=>t.fontSize).ToArray();ui.ViewBattlefield();yield return Frame();Check(!ui.TrySubmitPresentedOption(ui.OptionIndex(id),v.request.requestId),"Hidden choice submits");ui.RestoreBattlefieldWindows();yield return Frame();Check(fonts.SequenceEqual(panel.GetComponentsInChildren<Text>().Select(t=>t.fontSize)),"Return changes fonts");
                    if(id=="cancel")yield return Shot(lang+"-"+item.name);
                    int index=ui.OptionIndex(id);Check(!ui.TrySubmitPresentedOption(index,v.request.requestId-1),"Stale choice submits");
                    string button=item.name=="color"?"SelectionAction_"+index:item.name=="foundry"?(id=="ok"?"ConfirmOrder":"ResetOrder"):"ValueOption_"+index;
                    var b=panel.GetComponentsInChildren<Button>().Single(x=>x.name==button);int before=calls;Click(b);Check(calls==before+1&&last==index&&!ui.TrySubmitPresentedOption(index,v.request.requestId),"Exact option/duplicate failed");
                }
                ui.Present(null,1,false,null,null,"");ui.Present(v,1,false,submit,null,"");yield return Frame();Check(!ui.TrySubmitPresentedOption(ui.OptionIndex("ok"),v.request.requestId),"Nonacting choice submits");
            }
            foreach(var item in new[]{(name:"jade-stack",view:jade,source:"Jade Orb of Dragonkind"),(name:"foundry-stack",view:forge,source:"Forger's Foundry")}) {
                if(!selected.Contains("stack"))continue;
                ui.Present(null,1,false,null,null,"");ui.Present(item.view,1,true,_=>{},null,"");yield return Frame();
                var ability=item.view.stackItems.First(s=>s.kind!="spell"&&s.source.name==item.source);string body=(string)typeof(ForgeBattlefieldController).GetMethod("AbilityEffectText",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{ability.source,ability.description});
                string expected=item.name=="jade-stack"?(lang=="zh-CN"?"辟邪":lang=="ja-JP"?"呪禁":"hexproof"):(lang=="zh-CN"?"坟墓场":lang=="ja-JP"?"墓地":"graveyard");
                Check(body.Contains(expected)&&body.Contains("[Card:")&&body.Contains("Activator: Player 1"),"Trigger translated paragraph/event identity missing: "+item.name);
                Check(UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t=>t.gameObject.activeInHierarchy&&t.name=="Effect"&&t.text==body),"Translated stack body not rendered");yield return Shot(lang+"-"+item.name);
            }
            string unknown="When you spend this mana to cast a different unknown spell, keep this text. ([Unknown]) [Card: Opt (121), Activator: Player 1]";
            Check((string)typeof(ForgeBattlefieldController).GetMethod("AbilityEffectText",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{new ForgeCardView{name="Unknown"},unknown})==unknown,"Unknown paragraph was replaced");
            File.AppendAllText(Output+"/steps.log",lang+" parts="+parts+" exact buttons/guards/panels/paragraphs PASS\n");
        }}finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(locale);if(module!=null)module.enabled=active;foreach(var n in notices)if(n.n!=null)n.n.enabled=n.enabled;}
    }
}
#endif
