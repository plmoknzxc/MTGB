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

// Presentation evidence consumes unchanged, real native snapshots. The shelf
// only enlarges the same production components for visual review.
public static class TemplateCardFaceValidation
{
    const string Root=".tools/component-card-1008/",Output=Root+"ui-current/";
    static string native;
    static bool running;
    [Serializable] sealed class Result { public string folder; }
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(native+"/"+name+".json"));
    static void Check(bool value,string why){if(!value)throw new Exception(why);File.AppendAllText(Output+"steps.log",why+" PASS\n");}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.4f);Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();}
    static IEnumerator Shot(string name){GameObject.Find("InspectPanel")?.SetActive(false);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Output+name+".png");yield return new WaitForEndOfFrame();}
    static void Click(GameObject go)
    {
        Check(go!=null,"Click target exists");var r=(RectTransform)go.transform;
        var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==go.GetComponent<Button>(),"Template preserves topmost input ray "+go.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    static void Glyphs(TemplateCardFace face,string label)
    {
        Check(face!=null&&face.Illustration.texture!=null,label+" has illustration");
        Check(face.transform.GetComponentsInChildren<RawImage>().Any(i=>i.name=="Frame"&&i.texture!=null),label+" uses imported PSD frame");
        foreach(var t in face.GetComponentsInChildren<TextMeshProUGUI>()) {
            if(!t.enabled||string.IsNullOrEmpty(t.text))continue;t.ForceMeshUpdate();
            Check(!t.isTextOverflowing,label+" text fits "+t.name);
            Check(t.textInfo.characterCount>0,label+" renders glyphs "+t.name);
        }
        Check(face.Cost.GetComponent<TemplateManaCost>().Complete&&face.Cost.GetComponentsInChildren<RawImage>().Length==2,label+" cost has two independent icons");
    }
    public static void Run()
    {
        if(running)return;if(!Application.isPlaying)throw new Exception("Play mode required");running=true;
        native=JsonUtility.FromJson<Result>(File.ReadAllText(Root+"native-result.json")).folder;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"steps.log","");File.WriteAllText(Output+"result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("TemplateFaceValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        var notice=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool prior=notice!=null&&notice.enabled;if(notice!=null)notice.enabled=false;
        var test=Validate(ui);
        try {
            while(true){object next;try{if(!test.MoveNext())break;next=test.Current;}catch(Exception e){File.WriteAllText(Output+"result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"result.txt","PASS: two PSD 2D faces, native hand/main/adventure costs, real stack/field and public zone views, production cast rays/IDs, component reuse and three localized layouts. Two-card pilot; no all-card or live-network claim.");
        }finally{running=false;GamePreferences.SetLanguage("zh-CN");if(notice!=null)notice.enabled=prior;}
    }
    static IEnumerator Language(string language)
    {
        GamePreferences.SetLanguage(language);yield return Frame();float end=Time.realtimeSinceStartup+15;
        while((!GamePreferences.Ready||GamePreferences.Language!=language)&&Time.realtimeSinceStartup<end)yield return null;
        Check(GamePreferences.Ready&&GamePreferences.Language==language,language+" resources ready");
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        yield return Language("zh-CN");var stacked=Read("stack-adventure");var before=Read("before-hand");
        ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(stacked,1,true,i=>{},null,"");yield return Frame();
        var face=GameObject.Find("StackCard_0").GetComponentInChildren<TemplateCardFace>();
        Check(face.Title.text==GamePreferences.CardFaceName("Stomp"),"Actual adventure stack uses selected Stomp face");Glyphs(face,"Stomp stack");
        Check(face.Cost.text==CardSymbolText.Format("{2}{R}")&&face.AdventureCost==null,"Stomp stack has adventure cost and no creature fee");
        Check(!face.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.name=="Stats"),"Instant face has no creature power/toughness");
        Check(GameObject.Find("StackCard_1").GetComponentInChildren<TemplateCardFace>()!=null,"Second real stack object has Visionary template");
        foreach(string name in new[]{"Elvish Visionary","Bonecrusher Giant"}) {
            var card=stacked.players[0].battlefield.First(c=>c.name==name);
            Check(GameObject.Find("Card_"+card.id).GetComponentInChildren<TemplateCardFace>()==null,name+" battlefield keeps original compact card art");
        }
        yield return Shot("battlefield-stack");
        foreach(string zone in new[]{"手牌","坟场","放逐","牌库"}) {
            int calls=0,sent=-1;ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(before,1,true,i=>{calls++;sent=i;},null,"");yield return Frame();
            if(zone=="手牌")Click(GameObject.Find("PlayerPlate/Sub"));else Click(GameObject.Find("ZonePiles_PlayerBottomLeft/Pile_"+zone));yield return Frame();
            var faces=UnityEngine.Object.FindObjectsByType<TemplateCardFace>(FindObjectsSortMode.None).Where(f=>f.gameObject.activeInHierarchy&&f.GetComponentInParent<ScrollRect>()!=null).ToArray();
            Check(faces.Any(f=>f.Title.text==GamePreferences.CardFaceName("Elvish Visionary")),zone+" shows actual Visionary");
            if(zone!="牌库")Check(faces.Any(f=>f.AdventureCost!=null),zone+" shows actual Adventure creature");
            foreach(var f in faces)Glyphs(f,zone+" "+f.Title.text);
            if(zone!="牌库") {
                var adventure=faces.FirstOrDefault(f=>f.AdventureCost!=null&&f.Cost.text==CardSymbolText.Format("{3}{R}"));
                Check(adventure!=null&&adventure.AdventureCost.text==CardSymbolText.Format("{2}{R}"),zone+" permitted card has independent native creature/adventure fees");
            }
            var casts=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b=>b.name=="CastComponentCard"&&b.gameObject.activeInHierarchy).ToArray();
            Check(casts.Length>0,zone+" native permission offers cast action");yield return Shot("zone-"+(zone=="手牌"?"hand":zone=="坟场"?"graveyard":zone=="放逐"?"exile":"library-visionary"));
            var cast=casts.First();int id=int.Parse(cast.transform.parent.name.Substring("ChoiceCard_".Length));Click(cast.gameObject);
            Check(calls==1&&before.request.options[sent].id=="card:"+id,zone+" exact native card ID sent once");cast.onClick.Invoke();Check(calls==1,zone+" duplicate submission blocked");
        }
        ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(Read("effects-resolved"),1,true,i=>{},null,"");yield return Frame();
        Click(GameObject.Find("ZonePiles_PlayerBottomLeft/Pile_牌库"));yield return Frame();
        Check(UnityEngine.Object.FindObjectsByType<TemplateCardFace>(FindObjectsSortMode.None).Any(f=>f.AdventureCost!=null&&f.GetComponentInParent<ScrollRect>()!=null),"Actual next revealed library top is Giant");yield return Shot("zone-library-giant");
        ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(Read("stack-giant"),1,false,null,null,"");yield return Frame();
        var giant=GameObject.Find("StackCard_0").GetComponentInChildren<TemplateCardFace>();Check(giant.AdventureCost!=null&&giant.Cost.text==CardSymbolText.Format("{3}{R}"),"Creature cast from adventure exile uses main cost");Glyphs(giant,"Giant stack");yield return Shot("stack-giant");
        ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(stacked,1,false,null,null,"");yield return Frame();
        var canvas=GameObject.Find("Canvas").GetComponent<Canvas>();
        var shelf=new GameObject("TemplateComparison",typeof(RectTransform),typeof(Image));shelf.transform.SetParent(canvas.transform,false);
        var sr=(RectTransform)shelf.transform;sr.sizeDelta=new Vector2(1280,600);shelf.GetComponent<Image>().color=new Color(.025f,.04f,.05f,.98f);
        var method=typeof(ForgeBattlefieldController).GetMethod("PresentComponentFace",BindingFlags.NonPublic|BindingFlags.Instance);
        var cards=new[]{before.players[0].hand.First(c=>c.name=="Bonecrusher Giant"),stacked.stackItems.First(s=>s.source.name=="Stomp").source,Read("stack-giant").stackItems.First(s=>s.source.name=="Bonecrusher Giant").source,Read("stack-visionary").stackItems.First(s=>s.source.name=="Elvish Visionary").source};
        string[] labels={"手牌 · 本体 / 历险费用","堆叠 · 施放历险","堆叠 · 施放本体","堆叠 · 进场抓牌生物"};
        TemplateCardFace stable=null;int costId=0,artId=0;
        for(int i=0;i<cards.Length;i++) {
            var host=new GameObject("Case"+i,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();host.SetParent(sr,false);host.sizeDelta=new Vector2(290,405);host.anchoredPosition=new Vector2((i-1.5f)*308,-10);method.Invoke(ui,new object[]{host,cards[i],false,false});
            var f=host.GetComponentInChildren<TemplateCardFace>();if(i==0){stable=f;costId=f.Cost.GetInstanceID();artId=f.Illustration.GetInstanceID();}
            var label=CardSymbolText.Create(sr,"Label"+i,20,new Color(.9f,.88f,.8f));label.rectTransform.sizeDelta=new Vector2(300,40);label.rectTransform.anchoredPosition=new Vector2((i-1.5f)*308,230);label.alignment=TextAlignmentOptions.Center;label.text=labels[i];
        }
        yield return Frame();foreach(var f in shelf.GetComponentsInChildren<TemplateCardFace>())Glyphs(f,"Comparison "+f.Title.text);yield return Shot("faces-comparison");
        float preference=GamePreferences.TextScale;var printedSizes=shelf.GetComponentsInChildren<TextMeshProUGUI>().Where(t=>t.GetComponentInParent<TemplateCardFace>()!=null).ToDictionary(t=>t,t=>t.fontSize);
        GamePreferences.SetScale(preference<1.2f?1.3f:.85f);yield return Frame();
        Check(printedSizes.All(p=>Mathf.Approximately(p.Key.fontSize,p.Value)),"Preference scale changes preserve fixed paper typography");
        GamePreferences.SetScale(preference);yield return Frame();
        // Same native state updates only the display components, never a scan.
        var picture=stable.Illustration.texture;method.Invoke(ui,new object[]{stable.transform.parent as RectTransform,cards[0],false,false});yield return Frame();
        Check(stable.Cost.GetInstanceID()==costId&&stable.Illustration.GetInstanceID()==artId&&stable.Illustration.texture==picture,"Rebinding retains cost/art components and original illustration");
        foreach(string language in new[]{"en-US","ja-JP","zh-CN"}) {
            yield return Language(language);
            for(int i=0;i<cards.Length;i++)method.Invoke(ui,new object[]{sr.Find("Case"+i) as RectTransform,cards[i],false,false});yield return Frame();
            foreach(var f in shelf.GetComponentsInChildren<TemplateCardFace>())Glyphs(f,language+" "+f.Title.text);
        }
        var first=sr.Find("Case0") as RectTransform;
        var readonlyGiant=before.players[0].exileCards.First(c=>c.name=="Bonecrusher Giant"&&!before.playableExile.Any(p=>p.id==c.id));
        method.Invoke(ui,new object[]{first,readonlyGiant,false,false});yield return Frame();
        Check(stable.Cost.GetInstanceID()==costId&&stable.Illustration.GetInstanceID()==artId,"Native printed/adjusted cost rebinding retains the same slots");
        Check(stable.Cost.text==CardSymbolText.Format("{2}{R}")&&stable.AdventureCost.text==CardSymbolText.Format("{1}{R}"),"Read-only exile displays printed main/adventure costs");
        method.Invoke(ui,new object[]{first,cards[0],false,false});yield return Frame();
        Check(stable.Cost.text==CardSymbolText.Format("{3}{R}")&&stable.AdventureCost.text==CardSymbolText.Format("{2}{R}"),"Permitted native hand restores both adjusted costs without repainting art");
        var previous=JsonUtility.FromJson<Result>(File.ReadAllText(".tools/component-card-1007/native/base-result.json"));
        var bear=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(previous.folder+"/before-Hand.json")).players[0].hand.First(c=>c.name=="Grizzly Bears");
        method.Invoke(ui,new object[]{first,bear,false,false});yield return Frame();Check(first.GetComponentInChildren<ComponentCardFace>()!=null,"Existing one-card pilot can replace a template in the same host");
        method.Invoke(ui,new object[]{first,cards[0],false,false});yield return Frame();Check(first.GetComponentInChildren<TemplateCardFace>()!=null,"Template can replace existing one-card pilot in the same host");
        var live=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Root+"live-proof/casting-options.json"));
        UnityEngine.Object.Destroy(shelf);yield return Frame();ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(live,1,true,i=>{},null,"");yield return Frame();
        var options=UnityEngine.Object.FindObjectsByType<TemplateCardFace>(FindObjectsSortMode.None).Where(f=>f.GetComponentInParent<ScrollRect>()!=null&&f.gameObject.activeInHierarchy).ToArray();
        Check(options.Length==2&&options.Any(f=>f.AdventureCost==null&&f.Cost.text==CardSymbolText.Format("{2}{R}"))&&options.Any(f=>f.AdventureCost!=null&&f.Cost.text==CardSymbolText.Format("{3}{R}")),"Actual native casting dialog separates main/adventure costs");
        foreach(var f in options)Glyphs(f,"Native casting choice "+f.Title.text);yield return Shot("casting-options");
        ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(stacked,1,true,i=>{},null,"");yield return Frame();
    }
}
#endif
