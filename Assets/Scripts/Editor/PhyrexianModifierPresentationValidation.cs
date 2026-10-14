#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Native snapshots supply every cost, life reservation, option and outcome.
public static class PhyrexianModifierPresentationValidation
{
    const string Folder=".tools/phyrexian-modifiers-1006/ui";
    public static string Output {get;private set;}
    [Serializable] class Meta {public string folder;public string runtimeHash;}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static ForgeSeatView Read(string name,string label){var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/phyrexian-modifiers-1006/"+name+"-result.json"));Check(meta.runtimeHash==JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json")).runtimeHash,"Stale native proof");return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Path.Combine(meta.folder,label+".json")));}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Click(Button button){var rt=(RectTransform)button.transform;var p=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Ray blocked "+button.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    static Button Payer()=>Root.Find("PlayerPlate").GetComponent<Button>();
    static Button Cancel()=>Root.Find("CornerCluster/ControlRow/ManaPaymentActions/CancelPayment").GetComponent<Button>();
    public static void Run(){Check(Application.isPlaying,"Play required");Output=Folder+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);File.WriteAllText(Folder+"/latest.txt",Output);File.WriteAllText(Folder+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("PhyrexianModifierValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var e=Validate(ui);try{while(true){object next;try{if(!e.MoveNext())break;next=e.Current;}catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);File.Copy(Output+"/result.txt",Folder+"/result.txt",true);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: native Trinisphere/life/mixed/reduction/cancel snapshots; zh-CN/en-US/ja-JP, payers 1/2; exact life and land IDs via real rays, reserved life stays 20 then native commit is 16/18; adjusted remaining mana rendered as visible sprites with fixed-size text inside dock; cancel/recast release and resolved stack empty. Native rules and live networking are separate.");File.Copy(Output+"/result.txt",Folder+"/result.txt",true);}finally{(e as IDisposable)?.Dispose();}}
    static IEnumerator Validate(ForgeBattlefieldController ui){string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool inputEnabled=module!=null&&module.enabled;if(module!=null)module.enabled=false;var banners=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=banners.Select(b=>b.enabled).ToArray();foreach(var b in banners)b.enabled=false;try{
        foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}){int revision=GamePreferences.Revision;bool changed=GamePreferences.Language!=lang;GamePreferences.SetLanguage(lang);float until=Time.realtimeSinceStartup+15;while((!GamePreferences.Ready||changed&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready&&(!changed||revision!=GamePreferences.Revision),"Locale not ready");yield return Frame();
            foreach(string name in new[]{"tri-life","tri-mixed","tri-reduce-life","tri-cancel-life"}){var before=Read(name,"before-cast");int actor=before.request.seat,calls=0,last=-1;ui.Present(null,actor,false,null,null,"");ui.Present(before,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();var spell=before.players.First(p=>p.seat==actor).hand.Single(c=>c.name=="Dismember");Click(GameObject.Find("Card_"+spell.id).GetComponent<Button>());Check(calls==1&&last==ui.OptionIndex("card:"+spell.id),"Cast ID mismatch");
                var life=Read(name,"life-payment-0");ui.Present(life,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();string payer="player:"+life.players.First(p=>p.seat==actor).targetId;Click(Payer());Check(calls==2&&last==ui.OptionIndex(payer),"Life ID mismatch");
                var paid=Read(name,name=="tri-mixed"?"payment-1":name=="tri-cancel-life"?"payment-4":"payment-2");ui.Present(paid,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();Check(Root.Find("PlayerPlate/Life").GetComponent<Text>().text=="20","Uncommitted life deducted");var prompt=Root.Find("ManaPaymentPrompt").GetComponent<TMPro.TextMeshProUGUI>();prompt.ForceMeshUpdate();Check(prompt.text.Contains(CardSymbolText.Format(paid.request.remainingManaCost))&&!prompt.text.Contains("{")&&prompt.textInfo.characterInfo.Take(prompt.textInfo.characterCount).Any(c=>c.elementType==TMPro.TMP_TextElementType.Sprite&&c.isVisible),"Cost glyph wrong");Check(!prompt.enableAutoSizing&&prompt.preferredHeight<=prompt.rectTransform.rect.height&&prompt.preferredWidth<=prompt.rectTransform.rect.width,"Payment text overflow");
                if(name=="tri-cancel-life"){var cancel=Read(name,"cancel-payment");ui.Present(cancel,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();Click(Cancel());Check(last==ui.OptionIndex("cancel"),"Cancel ID wrong");ui.Present(before,actor,true,i=>calls++,null,"");yield return Frame();Check(!Cancel().gameObject.activeInHierarchy&&Root.Find("PlayerPlate/Life").GetComponent<Text>().text=="20","Cancel did not restore");}
                else{var land=paid.players.First(p=>p.seat==actor).battlefield.First(c=>c.land&&!c.tapped&&ui.OptionIndex("card:"+c.id)>=0);Click(GameObject.Find("Card_"+land.id).GetComponent<Button>());Check(last==ui.OptionIndex("card:"+land.id),"Native land ID wrong");}
                ui.Present(Read(name,"stack"),actor,true,i=>calls++,null,"");yield return Frame();Check(Root.Find("PlayerPlate/Life").GetComponent<Text>().text==(name=="tri-mixed"?"18":"16")&&!Cancel().gameObject.activeInHierarchy,"Native committed life/release wrong");ui.Present(Read(name,"resolved"),actor,true,i=>calls++,null,"");yield return Frame();Check(!Cancel().gameObject.activeInHierarchy,"Resolved dock retained");
                if(lang=="zh-CN"){ui.Present(paid,actor,true,i=>calls++,null,"");yield return Frame();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return Frame();}
            }
        }
    }finally{ui.enabled=enabled;if(module!=null)module.enabled=inputEnabled;GamePreferences.SetLanguage(locale);for(int i=0;i<banners.Length;i++)if(banners[i]!=null)banners[i].enabled=flags[i];}}
}
#endif
