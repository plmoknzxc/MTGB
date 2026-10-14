#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Native modifier snapshots verify the shared renderer, not additional spell resolutions.
public static class CharacteristicChangePresentationValidation
{
    public const string Output=".tools/characteristic-colors-1006/ui";
    const string Native="Tools/Validation/Reports/ModifiedCharacteristics/";
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static Transform Root=>GameObject.Find("Canvas").transform;
    static TMPro.TextMeshProUGUI Rules=>Root.Find("InspectPanel/Rules").GetComponentInChildren<TMPro.TextMeshProUGUI>();
    static Text Label(string name)=>Root.Find("InspectPanel/"+name).GetComponent<Text>();
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Inspect(ForgeBattlefieldController ui,ForgeCardView card){typeof(ForgeBattlefieldController).GetField("inspectOriginalState",Hidden).SetValue(ui,false);typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{card});}
    static void Clean(ForgeBattlefieldController ui){foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}var f=typeof(ForgeBattlefieldController).GetField("inspectHoverRevision",Hidden);f.SetValue(ui,(int)f.GetValue(ui)+1);}
    static void Click(Button button){var p=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Original-state ray blocked");ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    public static void Run(){Check(Application.isPlaying,"Play required");Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("CharacteristicValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var iterator=Validate(ui);try{while(true){object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: native modifier projection in zh/en/ja and both seats; name/cost/color/trait/keyword differences blue, removed ability gray strike; base-only P/T blue, normal boosts red/green; original-state ray toggle, same-ID restoration, parameter-sensitive Ward and spectator face-down filtering. Snapshot replay and Ward adapters verify presentation, not spell casting or live networking.");}finally{(iterator as IDisposable)?.Dispose();}}
    static IEnumerator Validate(ForgeBattlefieldController ui){var locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;foreach(var n in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))n.enabled=false;try{
        foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}){GamePreferences.SetLanguage(lang);float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale unavailable");foreach(int seat in new[]{1,2}){
            var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Native+"field.json"));v.awaitingInput=false;v.request=null;ui.Present(null,seat,false,null,null,"");ui.Present(v,seat,false,null,null,"");yield return Frame();Clean(ui);
            var named=v.players[0].battlefield.Single(c=>c.name=="Silvercoat Lion");var fixedCard=v.players[0].battlefield.Single(c=>c.name=="Elvish Mystic");
            Inspect(ui,named);yield return Frame();
            Check(Label("Name").text.Contains("#70BFFF"),"Rename is not blue");
            Check(Rules.text.Contains(CardSymbolText.Format("<color=#70BFFF>{T}: Draw a card.</color>"))&&Rules.text.Contains(CardSymbolText.Format("<color=#565C65><s color=#00000000>{T}: Add {G}.")),"Added/lost ability diff missing");
            Check(Rules.text.Contains("#70BFFF")&&Rules.text.Contains("2"),"Added Ward missing");
            var cost=Root.Find("InspectPanel/CostRow").GetComponentInChildren<TMPro.TextMeshProUGUI>();cost.ForceMeshUpdate();
            Check(cost.richText&&cost.tintAllSprites&&cost.text.Contains("#70BFFF")&&!cost.text.Contains("{U}"),"Changed cost did not render colored glyphs");
            var glyphs=cost.textInfo.characterInfo.Take(cost.textInfo.characterCount).Where(c=>c.elementType==TMPro.TMP_TextElementType.Sprite).ToArray();
            Check(glyphs.Length==2&&glyphs.All(c=>c.isVisible&&c.color.r==112&&c.color.g==191&&c.color.b==255),"Changed mana glyph drawing/color wrong");
            if(lang=="zh-CN"&&seat==1){ScreenCapture.CaptureScreenshot(Output+"/changed-details.png");yield return Frame();}
            Click(Root.Find("InspectPanel/OriginalState").GetComponent<Button>());yield return Frame();
            Check(!Label("Name").text.Contains("#70BFFF")&&!Rules.text.Contains("Draw a card")&&!Rules.text.Contains("#70BFFF"),"Original state contains modifications");
            Inspect(ui,fixedCard);yield return Frame();Check(Label("Footer").text.StartsWith("1/<color=#70BFFF>5</color>"),"Base rewrite did not isolate toughness");
            if(lang=="zh-CN"&&seat==1){ScreenCapture.CaptureScreenshot(Output+"/base-toughness.png");yield return Frame();}
            Click(Root.Find("InspectPanel/OriginalState").GetComponent<Button>());yield return Frame();Check(Label("Footer").text.StartsWith("1/1"),"Original P/T wrong");
            var mixed=JsonUtility.FromJson<ForgeCardView>(File.ReadAllText(Native+"base-plus-boost.json"));Inspect(ui,mixed);yield return Frame();Check(Label("Footer").text.Contains("#63E69B")&&!Label("Footer").text.Contains("#70BFFF")&&Rules.text.Contains("<color=#70BFFF>5</color>"),"Boost over rewritten base did not retain green while base stays blue");
            foreach(var name in new[]{"Runeclaw Bear","Balduvian Bears","Vampire Nighthawk"}){var c=v.players[0].battlefield.Single(x=>x.name==name);Inspect(ui,c);yield return Frame();if(name=="Runeclaw Bear")Check(Label("Footer").text.Contains("#63E69B")&&!Label("Footer").text.Contains("#70BFFF"),"Boost turned blue");if(name=="Balduvian Bears")Check(Label("Footer").text.Contains("#FF7184")&&!Label("Footer").text.Contains("#70BFFF"),"Reduction turned blue");}
            // Full keyword parameters, not only the WARD enum, determine identity.
            var ward=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(named));ward.originalKeywordDetails=new[]{"Ward:2"};ward.currentKeywordDetails=new[]{"Ward:4"};Inspect(ui,ward);yield return Frame();Check(Rules.text.Contains("<color=#70BFFF>")&&Rules.text.Contains("<s color=#00000000>"),"Changed keyword parameter lost");
            for(int i=0;i<v.players[0].battlefield.Length;i++){var c=v.players[0].battlefield[i];if(c.id==named.id)v.players[0].battlefield[i]=JsonUtility.FromJson<ForgeCardView>(File.ReadAllText(Native+"restored.json"));if(c.id==fixedCard.id)v.players[0].battlefield[i]=JsonUtility.FromJson<ForgeCardView>(File.ReadAllText(Native+"base-restored.json"));}
            ui.Present(v,seat,false,null,null,"");yield return Frame();Clean(ui);var restored=v.players[0].battlefield.Single(c=>c.id==named.id);Inspect(ui,restored);yield return Frame();Check(!Label("Name").text.Contains("#70BFFF")&&!Rules.text.Contains("Draw a card"),"Same-ID restoration kept old detail");
            var publicCard=(ForgeCardView)typeof(OnlineSpectatorView).GetMethod("PublicCard",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{named});Check(publicCard.originalName==named.originalName&&publicCard.currentAbilityText==named.currentAbilityText,"Face-up spectator baseline lost");named.faceDown=true;publicCard=(ForgeCardView)typeof(OnlineSpectatorView).GetMethod("PublicCard",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{named});Check(publicCard.originalName==null&&publicCard.originalManaCost==null&&publicCard.currentAbilityText==null&&publicCard.currentKeywordDetails==null&&!publicCard.hasColorBaseline,"Face-down private changes leaked");
            ui.Present(null,seat,false,null,null,"");
        }}
    }finally{ui.enabled=enabled;GamePreferences.SetLanguage(locale);}}
}
#endif
