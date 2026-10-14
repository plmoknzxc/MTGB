#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// These are actual native requests; no client-side costs or outcomes are made up.
public static class CostInteractionPresentationValidation
{
    const string Folder=".tools/cost-interactions-1006/ui";
    public static string Output {get;private set;}
    [Serializable] class Meta {public string folder,runtimeHash,status;}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string NativeFolder(string name){var m=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/cost-interactions-1006/native/"+name+"-result.json"));Check(m.status=="PASS"&&m.runtimeHash==JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json")).runtimeHash,"Stale native proof: "+name);return m.folder;}
    static ForgeSeatView Read(string name,string label)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Path.Combine(NativeFolder(name),label+".json")));
    static ForgeSeatView Request(string name,string kind){foreach(var p in Directory.GetFiles(NativeFolder(name),"step-*.json").OrderBy(p=>int.Parse(Path.GetFileNameWithoutExtension(p).Substring(5)))) {var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p));if(v.request.kind==kind)return v;}throw new Exception("Missing native kind "+kind);}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Click(Button b){var rt=(RectTransform)b.transform;var p=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(b.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Ray blocked "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    public static void Run(){Check(Application.isPlaying,"Play required");Output=Folder+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);File.WriteAllText(Folder+"/latest.txt",Output);File.WriteAllText(Folder+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("CostInteractionValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var e=Validate(ui);try{while(true){object n;try{if(!e.MoveNext())break;n=e.Current;}catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}yield return n;}File.WriteAllText(Output+"/result.txt","PASS: native cost requests in zh-CN/en-US/ja-JP; mana/hybrid/Phyrexian/snow symbols render as visible sprites within payment dock; all 15 added native glyph rectangles and energy distinct; actor cancel/early-life/tap-selection rays return exact IDs, duplicate/stale/viewer blocked, committed life and cancellation restore. Request replay, not live networking.");}finally{if(File.Exists(Output+"/result.txt"))File.Copy(Output+"/result.txt",Folder+"/result.txt",true);(e as IDisposable)?.Dispose();}}
    static IEnumerator Validate(ForgeBattlefieldController ui){string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;var banners=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=banners.Select(b=>b.enabled).ToArray();foreach(var b in banners)b.enabled=false;try{
        foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}){int revision=GamePreferences.Revision;bool changed=locale!=lang;GamePreferences.SetLanguage(lang);float until=Time.realtimeSinceStartup+15;while((!GamePreferences.Ready||changed&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale not ready");yield return Frame();
            foreach(string name in new[]{"hybrid-tax","twobrid-tax","snow-tax","xx-tax","hybrid-phy-tax","colorless-hybrid-tax","krrik-tax","citadel-floor","emerge-tax","offering-tax","improvise-phy-mixed"}){
                var v=Read(name,"payment");int actor=v.request.seat,calls=0,last=-1;ui.Present(null,actor,false,null,null,"");ui.Present(v,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();
                var text=Root.Find("ManaPaymentPrompt").GetComponent<TextMeshProUGUI>();text.ForceMeshUpdate();Check(!text.text.Contains("{")&&text.text.Contains(CardSymbolText.Format(v.request.remainingManaCost)),"Literal cost "+name);Check(text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(c=>c.elementType==TMP_TextElementType.Sprite&&c.isVisible),"Missing cost sprite "+name);Check(!text.enableAutoSizing&&text.preferredHeight<=text.rectTransform.rect.height&&text.preferredWidth<=text.rectTransform.rect.width,"Payment overflow "+name);
                var button=Root.Find("CornerCluster/ControlRow/ManaPaymentActions/CancelPayment").GetComponent<Button>();Check(!ui.TrySubmitPresentedOption(0,v.request.requestId-1),"Stale accepted");Click(button);button.onClick.Invoke();Check(calls==1&&last==ui.OptionIndex("cancel"),"Cancel/duplicate ID "+name);
                ui.Present(v,actor,false,i=>calls++,null,"");yield return Frame();Check(!button.interactable,"Viewer payment enabled");
                ui.Present(Read(name,"resolved"),actor,true,i=>calls++,null,"");yield return Frame();Check(!button.gameObject.activeInHierarchy,"Payment dock retained");
                if(lang=="zh-CN"){ui.Present(v,actor,true,i=>{},null,"");yield return Frame();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return Frame();}
            }
            var early=Request("improvise-phy-floor","dialog");int earlyCalls=0,earlyLast=-1;ui.Present(null,early.request.seat,false,null,null,"");ui.Present(early,early.request.seat,true,i=>{earlyCalls++;earlyLast=i;},null,"");yield return Frame();var menu=Root.Find("CardAbilityChoices");Check(menu.gameObject.activeInHierarchy,"Early choice absent");Check(!menu.Find("ValueTitle").GetComponent<Text>().text.StartsWith("one:"),"Early choice untranslated");var life=early.request.options.First(o=>o.label=="1");Click(menu.GetComponentsInChildren<Button>().Single(b=>b.name=="ValueOption_"+ui.OptionIndex(life.id)));Check(earlyCalls==1&&earlyLast==ui.OptionIndex(life.id),"Early life ID");
            var tap=Request("improvise-phy-floor","InputSelectCardsForConvokeOrImprovise");ui.Present(tap,tap.request.seat,true,i=>{earlyCalls++;earlyLast=i;},null,"");yield return Frame();var option=tap.request.options.First(o=>o.card!=null&&!string.IsNullOrEmpty(o.card.name));Click(GameObject.Find("ChoiceCard_"+option.card.id).GetComponentInChildren<Button>());Check(earlyCalls==2&&earlyLast==ui.OptionIndex(option.id),"Tap ID");
            foreach(string name in new[]{"krrik-tax-cancel","delve-tax-cancel","defiler-tax-cancel","improvise-phy-reserved-cancel"}){var v=Read(name,"cancelled");ui.Present(v,v.request.seat,true,i=>{},null,"");yield return Frame();Check(Root.Find("PlayerPlate/Life").GetComponent<Text>().text=="20","Cancel life stale "+name);}
            var glyph=CardSymbolText.Create(Root,"CostGlyphAudit",32,Color.white);glyph.rectTransform.sizeDelta=new Vector2(900,60);glyph.rectTransform.anchoredPosition=Vector2.zero;glyph.text=CardSymbolText.Format("{C/W}{C/U}{C/B}{C/R}{C/G}{W/U/P}{W/B/P}{U/B/P}{U/R/P}{B/R/P}{B/G/P}{R/W/P}{R/G/P}{G/W/P}{G/U/P}{E}");yield return Frame();glyph.ForceMeshUpdate();var sprites=glyph.textInfo.characterInfo.Take(glyph.textInfo.characterCount).ToArray();Check(sprites.Length==16&&sprites.All(c=>c.elementType==TMP_TextElementType.Sprite&&c.isVisible),"Added glyph fallback missing");Check(sprites.Select(c=>c.textElement.unicode).Distinct().Count()==16,"Energy/glyph collision");if(lang=="zh-CN"){ScreenCapture.CaptureScreenshot(Output+"/all-cost-symbols.png");yield return Frame();}UnityEngine.Object.Destroy(glyph.gameObject);yield return Frame();
        }
    }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=input;GamePreferences.SetLanguage(locale);for(int i=0;i<banners.Length;i++)if(banners[i]!=null)banners[i].enabled=flags[i];}}
}
#endif
