#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Real native snapshots. UI callbacks are captured, not sent to another game.
public static class ManaVariantPresentationValidation
{
    const string UiRoot=".tools/mana-variants-1006/ui";
    public static string Output {get;private set;}=UiRoot;
    const string Native=".tools/mana-variants-1006/native/";
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    [Serializable] class Result { public string folder; public string spell; }
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
    static ForgeSeatView Read(string name,string snapshot)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Path.Combine(JsonUtility.FromJson<Result>(File.ReadAllText(Native+name+"-result.json")).folder,snapshot+".json")));
    static Button Payment(string name)=>Root.Find("CornerCluster/ControlRow/ManaPaymentActions/"+name).GetComponent<Button>();
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static Rect Bounds(Transform t){var p=new Vector3[4];((RectTransform)t).GetWorldCorners(p);return Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));}
    static void Click(Button b){Check(b!=null&&b.interactable,"Inactive button");var r=(RectTransform)b.transform;foreach(float x in new[]{.5f,.2f,.8f})foreach(float y in new[]{.5f,.2f,.8f}){var p=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(new Vector2(Mathf.Lerp(r.rect.xMin,r.rect.xMax,x),Mathf.Lerp(r.rect.yMin,r.rect.yMax,y))))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=b)continue;ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);return;}throw new Exception("Ray blocked "+b.name);}
    static void CleanHover(ForgeBattlefieldController ui){foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}var f=typeof(ForgeBattlefieldController).GetField("inspectHoverRevision",Hidden);f.SetValue(ui,(int)f.GetValue(ui)+1);}
    public static void Run(){Check(Application.isPlaying,"Play required");Output=UiRoot+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);File.WriteAllText(UiRoot+"/latest.txt",Output);File.WriteAllText(UiRoot+"/result.txt","RUNNING");File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ManaVariantValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var e=Validate(ui);try{while(true){object next;try{if(!e.MoveNext())break;next=e.Current;}catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);File.Copy(Output+"/result.txt",UiRoot+"/result.txt",true);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: zh-CN/en-US/ja-JP native snow/hybrid/two-brid snapshots, native payers 1/2; original printed cost sprites visible; actual hand cast, Auto/Cancel and offered source callbacks by ray; stale/duplicate/viewer/inputPending guards; native wrong-color retry and cancel recovery; insufficient spell never submits; dock/phase bounds and fixed fonts; no rule effects or live transport inferred from replay.");File.Copy(Output+"/result.txt",UiRoot+"/result.txt",true);}finally{(e as IDisposable)?.Dispose();}}
    static IEnumerator Validate(ForgeBattlefieldController ui){string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool inputEnabled=module!=null&&module.enabled;if(module!=null)module.enabled=false;var banners=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var bannerFlags=banners.Select(b=>b.enabled).ToArray();foreach(var b in banners)b.enabled=false;try{
        foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}){int revision=GamePreferences.Revision;bool change=locale!=lang&&GamePreferences.Language!=lang;GamePreferences.SetLanguage(lang);float until=Time.realtimeSinceStartup+15;while((!GamePreferences.Ready||change&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready&&(!change||revision!=GamePreferences.Revision),"Locale unavailable");yield return Frame();
            foreach(string name in new[]{"snow-paid","hybrid-green","hybrid-red","hybrid-mixed","hybrid-retry","twobrid-white","twobrid-generic","twobrid-mixed"}){
                var before=Read(name,"before-cast");var payment=Read(name,"payment");int actor=payment.request.seat;int calls=0,last=-1;
                ui.Present(null,actor,false,null,null,"");ui.Present(before,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();CleanHover(ui);
                var spell=before.players.First(p=>p.seat==actor).hand.Single(c=>c.name==JsonUtility.FromJson<Result>(File.ReadAllText(Native+name+"-result.json")).spell);
                Check(calls==0,name+" callback before cast ray");Click(GameObject.Find("Card_"+spell.id).GetComponent<Button>());Check(calls==1&&last==ui.OptionIndex("card:"+spell.id),name+" hand casting option");
                ui.Present(payment,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();CleanHover(ui);
                var auto=Payment("AutoPayment");var cancel=Payment("CancelPayment");Check(auto.gameObject.activeInHierarchy&&cancel.gameObject.activeInHierarchy&&auto.interactable&&cancel.interactable,name+" missing payment controls");
                Check(!Root.Find("ForgeChoices").gameObject.activeSelf&&!Root.Find("CornerCluster/ControlRow/SkipCombatButton").gameObject.activeSelf,name+" duplicate choice/advance");
                var phase=Root.Find("CornerCluster/ControlRow/PhaseTrack");Check(phase.gameObject.activeSelf&&!Bounds(auto.transform).Overlaps(Bounds(phase))&&!Bounds(cancel.transform).Overlaps(Bounds(phase))&&!Bounds(auto.transform).Overlaps(Bounds(cancel.transform)),name+" dock overlaps");
                foreach(var b in new[]{auto,cancel}){var t=b.GetComponentInChildren<Text>();Check(!t.resizeTextForBestFit&&t.preferredWidth<=t.rectTransform.rect.width&&t.preferredHeight<=t.rectTransform.rect.height,name+" clipped/flexible dock text");}
                if(lang=="zh-CN"){ScreenCapture.CaptureScreenshot(Output+"/"+name+"-payment.png");yield return Frame();}
                int start=calls;ui.Choose(ui.OptionIndex("ok"),payment.request.requestId-1);Check(calls==start,name+" stale request submitted");Click(auto);auto.onClick.Invoke();Check(calls==start+1&&last==ui.OptionIndex("ok")&&!auto.interactable&&!cancel.interactable,name+" auto index/duplicate");
                ui.Present(payment,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();start=calls;Click(cancel);cancel.onClick.Invoke();Check(calls==start+1&&last==ui.OptionIndex("cancel"),name+" cancel index/duplicate");
                ui.Present(payment,actor,false,i=>calls++,null,"");yield return Frame();start=calls;auto.onClick.Invoke();cancel.onClick.Invoke();Check(calls==start&&!auto.interactable&&!cancel.interactable,name+" passive submission");
                // Transport-only boundary: mark exact native snapshot pending.
                var pending=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(payment));pending.inputPending=true;ui.Present(pending,actor,false,i=>calls++,null,"");Check(!auto.interactable&&!cancel.interactable,name+" pending submission");
                ui.Present(payment,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();var land=payment.request.options.FirstOrDefault(o=>o.id.StartsWith("card:"));if(land!=null){start=calls;Click(GameObject.Find("Card_"+land.id.Substring(5)).GetComponent<Button>());Check(calls==start+1&&last==ui.OptionIndex(land.id),name+" offered mana source");}
                ui.Present(Read(name,"resolved"),actor,true,i=>calls++,null,"");yield return Frame();Check(!auto.gameObject.activeInHierarchy&&!Root.Find("ManaPaymentPrompt").gameObject.activeSelf,name+" payment UI did not release");
                CleanHover(ui);typeof(ForgeBattlefieldController).GetField("inspectOriginalState",Hidden).SetValue(ui,false);typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{spell});yield return Frame();var cost=Root.Find("InspectPanel/CostRow").GetComponentInChildren<TextMeshProUGUI>();cost.ForceMeshUpdate();Check(cost.text.Contains(CardSymbolText.Format(spell.manaCost))&&!cost.text.Contains("{")&&!cost.enableAutoSizing,name+" literal or resized printed cost");var glyphs=cost.textInfo.characterInfo.Take(cost.textInfo.characterCount).Where(c=>c.elementType==TMP_TextElementType.Sprite).ToArray();int expected=name.StartsWith("snow")?1:name.StartsWith("hybrid")?2:3;Check(glyphs.Length==expected&&glyphs.All(c=>c.isVisible),name+" cost sprites missing");
                if(lang=="zh-CN"&&(name=="snow-paid"||name=="hybrid-mixed"||name=="twobrid-mixed")){ScreenCapture.CaptureScreenshot(Output+"/"+name+"-symbols.png");yield return Frame();}
            }
            foreach(string name in new[]{"snow-paid","hybrid-retry"}){var wrong=Read(name,"wrong-pool-before");var retry=Read(name,"wrong-pool-retry");int actor=wrong.request.seat,calls=0,last=-1;string symbol=name.StartsWith("snow")?"G":"U";ui.Present(null,actor,false,null,null,"");ui.Present(wrong,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();var chip=GameObject.Find("ManaPoolChips").transform.Find("Pool_"+symbol).GetComponent<Button>();Click(chip);Check(calls==1&&last==ui.OptionIndex("mana:"+symbol),"Wrong native color callback");ui.Present(retry,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();Check(retry.request.requestId!=wrong.request.requestId&&retry.request.contextId==wrong.request.contextId&&chip.interactable,"Native retry did not restore pool");ui.Choose(ui.OptionIndex("mana:"+symbol),wrong.request.requestId);Check(calls==1,"Prior retry submitted");Click(Payment("CancelPayment"));Check(calls==2&&last==ui.OptionIndex("cancel"),"Retry cancellation");ui.Present(Read(name,"before-cast"),actor,true,null,null,"");yield return Frame();Check(!Payment("AutoPayment").gameObject.activeInHierarchy,"Cancelled payment dock persisted");}
            foreach(string name in new[]{"snow-insufficient","hybrid-insufficient","twobrid-insufficient"}){var v=Read(name,"insufficient");int calls=0;ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,true,i=>calls++,null,"");yield return Frame();var spell=v.players.First(p=>p.seat==v.seat).hand.Single(c=>c.name==JsonUtility.FromJson<Result>(File.ReadAllText(Native+name+"-result.json")).spell);Check(ui.OptionIndex("card:"+spell.id)<0,"Insufficient cast option invented");GameObject.Find("Card_"+spell.id).GetComponent<Button>().onClick.Invoke();Check(calls==0,"Insufficient spell submitted");}
        }
    }finally{ui.enabled=enabled;if(module!=null)module.enabled=inputEnabled;GamePreferences.SetLanguage(locale);for(int i=0;i<banners.Length;i++)if(banners[i]!=null)banners[i].enabled=bannerFlags[i];}}
}
#endif
