#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class MarkerFieldsPresentationValidation
{
    const string Base=".tools/marker-fields-1009";
    public static bool Running{get;private set;}
    public static string Output{get;private set;}
    static int checks;
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool v,string s){if(!v)throw new Exception(s);checks++;File.AppendAllText(Output+"/checks.txt",s+"\n");}
    static ForgeSeatView Read(string n)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Base+"/ui-inputs/"+n+".json"));
    public static void Run(){if(Running||!Application.isPlaying)throw new Exception("Play required");Running=true;checks=0;Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("MarkerFieldsValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));}
    static IEnumerator Guard(IEnumerator work){var tasks=new Stack<IEnumerator>();tasks.Push(work);try{while(tasks.Count>0){object next;try{var w=tasks.Peek();if(!w.MoveNext()){tasks.Pop();continue;}next=w.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator c)tasks.Push(c);else yield return next;}File.WriteAllText(Output+"/result.txt","PASS_BOUNDED: "+checks+" checks;actual Kick effect art/source/full native text/sameID expiry both seats;actual Adventure permission artwork/removal;Prepared internal marker absent while actual spell/options retained;actual nondeparture clock without temporary-token frame. Private-source no-identity/art fixture is adapter only. No new effect/language/network PASS.");}finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Clean(){foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}Root.Find("InspectPanel").gameObject.SetActive(false);}
    static IEnumerator Hover(ForgeCardView card,string key){
        Clean();var tile=Root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Card_"+card.id);var pointer=tile.GetComponent<ForgeBattlefieldCardPointer>();pointer.enabled=true;
        var rect=(RectTransform)tile;var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<ForgeBattlefieldCardPointer>()==pointer,"Exact marker hover ray "+card.id);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerEnterHandler);yield return Frame();
        Check(Root.Find("InspectPanel").gameObject.activeInHierarchy,"Marker detail visible "+card.id);
        var rules=Root.Find("InspectPanel/Rules").GetComponentInChildren<TMPro.TextMeshProUGUI>();rules.ForceMeshUpdate();
        Check(rules.text==CardSymbolText.Format(card.currentRules),"Complete native marker rule "+card.id);
        Check(!Root.Find("InspectPanel/OriginalState").gameObject.activeSelf,"Effect is not a modifiable ordinary card");
        if(key!=null){Check(CardImageLibrary.TryLoadPrintingKey(key,out var expected),"Expected physical source image "+key);
            var art=tile.Find("Visual/Border/Face/Illustration").GetComponent<RawImage>();Check(art.texture==expected.texture,"Actual marker source texture "+key);
            Check(Root.Find("InspectPanel/Art").GetComponent<Image>().sprite.texture==expected.texture,"Actual hover source texture "+key);Check(!Root.Find("InspectPanel/Art/MissingArt").gameObject.activeSelf,"Public source has no missing-art placeholder");}
        var scroll=rules.GetComponentInParent<ScrollRect>();Check(rules.GetPreferredValues(rules.text,rules.rectTransform.rect.width,10000).y<=rules.rectTransform.rect.height+1,"Full detail text geometry");if(scroll!=null){scroll.verticalNormalizedPosition=0;yield return Frame();}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui){var language=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;var banners=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=banners.Select(b=>b.enabled).ToArray();foreach(var b in banners)b.enabled=false;
        try{float end=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<end)yield return null;GamePreferences.SetLanguage("zh-CN");end=Time.realtimeSinceStartup+15;while((!GamePreferences.Ready||GamePreferences.Language!="zh-CN")&&Time.realtimeSinceStartup<end)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language=="zh-CN","Main locale ready");
            foreach(int seat in new[]{1,2}){
                var v=Read("kick-"+seat);int calls=0;ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,false,_=>calls++,null,"");yield return Frame();var card=v.players.SelectMany(p=>p.effectMarkers).First(c=>c.markerKind=="effect");
                Check(card.effectSourceName=="Kick in the Door"&&card.effectSourcePrintingKey=="AFR/153","Exact actual source identity and printing");yield return Hover(card,"AFR/153");Check(Root.Find("InspectPanel/Name").GetComponent<Text>().text.Contains(GamePreferences.CardFaceName("Kick in the Door")),"Generic effect title names source");
                if(seat==1){ScreenCapture.CaptureScreenshot(Output+"/public-effect.png");yield return Frame();}
                ui.Present(v,v.seat,false,_=>calls++,null,"");yield return Frame();Check(Root.GetComponentsInChildren<Transform>().Count(t=>t.name=="Card_"+card.id)==1,"Same ID does not duplicate marker");
                ui.Present(Read("expired-"+seat),v.seat,false,_=>calls++,null,"");yield return Frame();Check(!Root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Card_"+card.id),"Actual expiry removes marker");Check(calls==0,"Read-only marker viewing submits nothing");
                v=Read("prepared-copied-prepared-seat"+seat);ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,false,null,null,"");yield return Frame();Check(!v.players.SelectMany(p=>p.effectMarkers).Any(c=>c.name.EndsWith("Prepared Spell")),"Native nonrender helper omitted");var prepared=v.players.SelectMany(p=>p.battlefield).Single(c=>c.id==121).preparedSpell;Check(prepared.id==145&&prepared.name=="Twofold Intent","Actual prepared spell retained");Check(v.request.options.Any(o=>o.id=="card:145"),"Native prepared cast entry retained");
                ui.Present(Read("prepared-spent-prepared-seat"+seat),v.seat,false,null,null,"");yield return Frame();Check(!Root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Card_146"),"Consumed internal helper remains absent");
            }
            var adventure=Read("adventure-exile-ready");ui.Present(null,adventure.seat,false,null,null,"");ui.Present(adventure,adventure.seat,false,null,null,"");yield return Frame();var permission=adventure.players.SelectMany(p=>p.effectMarkers).Single(c=>c.name.EndsWith("Adventure"));Check(permission.printingKey=="CLB/781","Adventure keeps physical full-scan identity");yield return Hover(permission,"CLB/781");ScreenCapture.CaptureScreenshot(Output+"/adventure-permission.png");yield return Frame();
            ui.Present(Read("adventure-resolved"),adventure.seat,false,null,null,"");yield return Frame();Check(!Root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Card_"+permission.id),"Casting body removes actual Adventure permission");
            var delayed=Read("nondeparture");ui.Present(null,delayed.seat,false,null,null,"");ui.Present(delayed,delayed.seat,false,null,null,"");yield return Frame();var marker=delayed.players.SelectMany(p=>p.effectMarkers).Single(c=>c.markerKind=="delayed");var tile=Root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Card_"+marker.id);Check(tile.GetComponentInChildren<DelayedEffectIcon>()!=null,"Actual nondeparture delayed clock");Check(tile.GetComponentInChildren<TemporaryCopyGlow>()==null&&(marker.pendingDepartures?.Length??0)==0,"No dying-copy frame for draw trigger");
            // Presentation adapter, not a new hidden-source effect result.
            var hidden=Read("kick-1");var anon=new ForgeCardView{id=999901,name="持续效果",effectMarker=true,markerKind="effect",ownerSeat=1,zone="Command",currentType="Effect",currentRules="来源牌为非公开信息。"};hidden.players[0].effectMarkers=new[]{anon};ui.Present(null,1,false,null,null,"");ui.Present(hidden,1,false,null,null,"");yield return Frame();yield return Hover(anon,null);Check(Root.Find("InspectPanel/Name").GetComponent<Text>().text=="持续效果"&&Root.Find("InspectPanel/Art").GetComponent<Image>().sprite==null,"ADAPTER anonymous source has no identity/art leak");
            Check(anon.printingKey==null&&anon.effectSourceName==null&&anon.effectSourcePrintingKey==null,"ADAPTER anonymous protocol identity absent");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=input;GamePreferences.SetLanguage(language);for(int i=0;i<banners.Length;i++)if(banners[i]!=null)banners[i].enabled=flags[i];}
    }
}
#endif
