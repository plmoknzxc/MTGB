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

public static class ComponentCardFaceValidation
{
    const string Root=".tools/component-card-1007/";
    const string Output=Root+"ui-current/";
    static bool running;
    static void Check(bool value,string why){if(!value)throw new Exception(why);File.AppendAllText(Output+"steps.log",why+" PASS\n");}
    static ForgeSeatView Read(string scenario,string name)
    {
        var result=JsonUtility.FromJson<NativeResult>(File.ReadAllText(Root+"native/"+scenario+"-result.json"));
        return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(result.folder+"/"+name+".json"));
    }
    [Serializable] sealed class NativeResult { public string folder; }
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.4f);Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();}
    static void Click(GameObject go)
    {
        Check(go!=null,"Click target exists");var rect=(RectTransform)go.transform;
        var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==go.GetComponent<Button>(),"Card components preserve topmost input target "+go.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Shot(string name){yield return new WaitForEndOfFrame();GameObject.Find("InspectPanel")?.SetActive(false);ScreenCapture.CaptureScreenshot(Output+name+".png");yield return new WaitForEndOfFrame();}
    static int VisibleSymbols(TextMeshProUGUI text){text.ForceMeshUpdate();return text.textInfo.characterInfo.Take(text.textInfo.characterCount).Count(c=>c.isVisible&&c.elementType==TMP_TextElementType.Sprite);}
    public static void Run()
    {
        if(running)return;if(!Application.isPlaying)throw new Exception("Play mode required");running=true;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"result.txt","RUNNING");File.WriteAllText(Output+"steps.log","");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ComponentCardFaceValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=banner!=null&&banner.enabled;if(banner!=null)banner.enabled=false;
        var runner=Validate(ui);
        try {
            while(true){object next;try{if(!runner.MoveNext())break;next=runner.Current;}catch(Exception e){File.WriteAllText(Output+"result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"result.txt","PASS: native hand/graveyard/exile/library casts and stack inputs; component glyph/bounds, independent updates, card and zone cast rays, preview/actual cost distinction. Single-card pilot, no all-card or live-network claim.");
        }finally{running=false;GamePreferences.SetLanguage("zh-CN");if(banner!=null)banner.enabled=enabled;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        GamePreferences.SetLanguage("zh-CN");yield return Frame();float end=Time.realtimeSinceStartup+12;while((!GamePreferences.Ready||GamePreferences.Language!="zh-CN")&&Time.realtimeSinceStartup<end)yield return null;
        Check(GamePreferences.Ready&&GamePreferences.Language=="zh-CN","Initial Chinese resources finish loading");
        var stacked=Read("zones","stack-Hand");
        ui.Present(stacked,1,false,null,null,"");yield return Frame();
        Check(GameObject.Find("StackCard_0").GetComponentInChildren<ComponentCardFace>()!=null,"Actual stack uses component face");
        var stackFace=GameObject.Find("StackCard_0").GetComponentInChildren<ComponentCardFace>();
        Check(VisibleSymbols(stackFace.Cost)==2,"Actual adjusted stack cost has two rendered icons");
        Check(string.IsNullOrEmpty(stackFace.PrintedCost.text)&&!stackFace.PrintedCost.gameObject.activeSelf,"Original-cost footer stays hidden");
        Check(GameObject.Find("Card_"+stacked.players[0].battlefield.First(c=>c.name=="Grizzly Bears").id).GetComponentInChildren<ComponentCardFace>()==null,"Battlefield uses original compact card art");
        Check(GameObject.Find("Card_"+stacked.players[0].hand.First(c=>c.name=="Grizzly Bears").id).GetComponentInChildren<ComponentCardFace>()!=null,"Hand uses full component face");
        yield return Shot("battlefield-stack");
        foreach(string origin in new[]{"Hand","Graveyard","Exile","Library"}) {
            var data=Read("zones","before-"+origin);int calls=0,sent=-1;ui.Present(null,1,false,null,null,"");yield return Frame();
            ui.Present(data,1,true,i=>{calls++;sent=i;},null,"");yield return Frame();
            if(origin=="Hand")Click(GameObject.Find("PlayerPlate/Sub"));
            else Click(GameObject.Find("ZonePiles_PlayerBottomLeft/Pile_"+(origin=="Graveyard"?"坟场":origin=="Exile"?"放逐":"牌库")));
            yield return Frame();
            var cast=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b=>b.name=="CastComponentCard"&&b.gameObject.activeInHierarchy);
            Check(cast!=null,"Native "+origin+" browser offers explicit cast action");
            var face=cast.transform.parent.GetComponentInChildren<ComponentCardFace>();Check(face!=null,"Native "+origin+" list uses component face");
            Check(face.Cost.text==CardSymbolText.Format("{2}{G}"),"Native "+origin+" cost display equals actual payment");
            Check(VisibleSymbols(face.Cost)==2,"Native "+origin+" icon geometry generated");
            var id=int.Parse(cast.transform.parent.name.Substring("ChoiceCard_".Length));
            var viewport=cast.GetComponentInParent<ScrollRect>().viewport;
            var corners=new Vector3[4];((RectTransform)cast.transform).GetWorldCorners(corners);
            Check(corners.All(p=>RectTransformUtility.RectangleContainsScreenPoint(viewport,p)),"Native "+origin+" entire cast button stays inside viewport");
            yield return Shot("zone-"+origin.ToLowerInvariant());
            Click(cast.gameObject);Check(calls==1&&data.request.options[sent].id=="card:"+id,"Native "+origin+" exact card ID submitted once");
            cast.onClick.Invoke();Check(calls==1,"Submitted native "+origin+" action cannot duplicate");
        }
        var after=Read("zones","stack-Hand");ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(after,1,false,null,null,"");yield return Frame();
        var canvas=GameObject.Find("Canvas").GetComponent<Canvas>();
        // Comparison shelf is an editor-only view of three real native inputs.
        var shelf=new GameObject("ComponentCostComparison",typeof(RectTransform),typeof(Image));shelf.transform.SetParent(canvas.transform,false);
        var sr=(RectTransform)shelf.transform;sr.sizeDelta=new Vector2(900,495);shelf.GetComponent<Image>().color=new Color(.035f,.055f,.065f,.98f);
        var method=typeof(ForgeBattlefieldController).GetMethod("PresentComponentFace",BindingFlags.NonPublic|BindingFlags.Instance);
        ComponentCardFace stable=null;int costId=0,artId=0;
        string[] cases={"base","raised","reduced"};string[] labels={"正常费用","加一费 · 阻力球","减一费 · 翡翠徽章"};
        for(int i=0;i<cases.Length;i++) {
            var data=Read(cases[i],"before-Hand");var card=data.players[0].hand.Single(c=>c.name=="Grizzly Bears");
            var host=new GameObject(cases[i],typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();host.SetParent(sr,false);host.sizeDelta=new Vector2(260,364);host.anchoredPosition=new Vector2((i-1)*285,-5);
            method.Invoke(ui,new object[]{host,card,false,false});var face=host.GetComponentInChildren<ComponentCardFace>();
            var label=CardSymbolText.Create(sr,"Label_"+i,20,new Color(.9f,.87f,.78f));label.rectTransform.sizeDelta=new Vector2(280,40);label.rectTransform.anchoredPosition=new Vector2((i-1)*285,214);label.alignment=TextAlignmentOptions.Center;label.text=labels[i];
            if(i==0){stable=face;costId=face.Cost.GetInstanceID();artId=face.Illustration.GetInstanceID();}
        }
        yield return Frame();yield return Shot("cost-comparison");
        var picture=stable.Illustration.texture;stable.SetCost("{G}","{1}{G}",true);
        Check(stable.Cost.GetInstanceID()==costId&&stable.Illustration.GetInstanceID()==artId,"Cost-only binding retains component identity");
        Check(stable.Cost.text==CardSymbolText.Format("{G}"),"Cost slot updates independently");
        Check(stable.Illustration.texture==picture,"Cost-only update preserves illustration");
        UnityEngine.Object.Destroy(shelf);yield return Frame();
        foreach(string language in new[]{"en-US","ja-JP","zh-CN"}) {
            GamePreferences.SetLanguage(language);yield return Frame();
            float readyDeadline=Time.realtimeSinceStartup+12;while((GamePreferences.Language!=language||!GamePreferences.Ready)&&Time.realtimeSinceStartup<readyDeadline)yield return null;
            Check(GamePreferences.Language==language&&GamePreferences.Ready,language+" resources finish loading");
            ui.Present(stacked,1,false,null,null,"");yield return Frame();
            var translated=GameObject.Find("StackCard_0").GetComponentInChildren<ComponentCardFace>();
            Check(VisibleSymbols(translated.Cost)==2,language+" cost icons stay visible at current user font scale");
            Check(string.IsNullOrEmpty(translated.PrintedCost.text)&&!translated.PrintedCost.gameObject.activeSelf,language+" original-cost footer stays hidden");
            translated.Title.ForceMeshUpdate();Check(translated.Title.textInfo.characterInfo.Take(translated.Title.textInfo.characterCount).Any(c=>c.isVisible),language+" title generates visible glyphs");
        }
        // Leave the native all-zone position, including a real stack spell, open.
        ui.Present(stacked,1,false,null,null,"");yield return Frame();
    }
}
#endif
