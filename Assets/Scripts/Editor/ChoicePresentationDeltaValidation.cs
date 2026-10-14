#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ChoicePresentationDeltaValidation
{
    public static bool Running;
    public static string Output;
    sealed class Host : MonoBehaviour { }
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> Results=new();
    public static void Configure() { AssetDatabase.DesiredWorkerCount=1; }
    static ForgeSeatView Read(string path)=>(ForgeSeatView)typeof(OnlineMatchSession).Assembly.GetType("ServerSnapshotJson").GetMethod("Deserialize").MakeGenericMethod(typeof(ForgeSeatView)).Invoke(null,new object[]{File.ReadAllText(path)});
    static ForgeSeatView Native(string name,Func<ForgeSeatView,bool> predicate)
    {
        var meta=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(".tools/choice-presentation-1013/native/"+name+"-latest.json"));
        return Directory.GetFiles((string)meta["folder"],"*-seat1.json").Select(Read).First(predicate);
    }
    static void Check(bool pass,string message) { Results.Add((pass?"PASS ":"FAIL ")+message); }
    public static void Run()
    {
        if(!Application.isPlaying||Running)throw new Exception("Play required");
        Running=true;Results.Clear();Output=".tools/choice-presentation-1013/ui-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmss");Directory.CreateDirectory(Output);
        new GameObject("ChoicePresentationDeltaValidation").AddComponent<Host>().StartCoroutine(Guard(Exercise()));
    }
    static IEnumerator Guard(IEnumerator iterator)
    {
        while(true) {
            bool more;object next;
            try { more=iterator.MoveNext();next=more?iterator.Current:null; }
            catch(Exception e) { Results.Add("FAIL "+e);more=false;next=null; }
            if(!more)break;yield return next;
        }
        Running=false;File.WriteAllLines(Output+"/result.txt",Results);Debug.Log("CHOICE PRESENTATION "+(Results.Any(r=>r.StartsWith("FAIL"))?"FAIL":"PASS")+" "+Output);
    }
    static IEnumerator Frame() { yield return null;Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame(); }
    static void Capture(string name) { ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/"+name+".png")); }
    static void RayClick(Button button)
    {
        var rect=(RectTransform)button.transform;var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"destination button is raycast-visible");
        if(hits.Count>0)ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Exercise()
    {
        GamePreferences.SetLanguage("zh-CN");float until=Time.realtimeSinceStartup+30;
        while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;
        Check(GamePreferences.Ready&&GamePreferences.Language=="zh-CN","Chinese resources ready");
        foreach(var notice in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))notice.enabled=false;
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ValidationBattlefield").AddComponent<ForgeBattlefieldController>();
        bool enabled=ui.enabled;ui.enabled=false;float scale=GamePreferences.TextScale;
        var before=Native("tuck-top",v=>v.request.kind=="InputConfirm");
        foreach(string direction in new[]{"tuck-top","tuck-bottom"}) {
            var view=Native(direction,v=>v.request.kind=="InputConfirm");int submissions=0,last=-1;
            ui.Present(view,1,true,i=>{submissions++;last=i;},null,"");yield return Frame();
            var root=GameObject.Find("Canvas");string[] words=root.GetComponentsInChildren<Text>().Where(t=>t.gameObject.activeInHierarchy&&t.enabled).Select(t=>t.text)
                .Concat(root.GetComponentsInChildren<TMP_Text>().Where(t=>t.gameObject.activeInHierarchy&&t.enabled).Select(t=>t.text)).ToArray();
            Check(view.request.previewCards.Single().name=="Air Elemental"&&view.request.previewCards.Single().ownerSeat==1,"destination preview is chooser's Air Elemental");
            Check(words.Any(t=>t.Contains(GamePreferences.CardFaceName("Air Elemental"))&&t.Contains("牌库")),"destination prompt names the affected card");
            Check(!words.Any(t=>System.Text.RegularExpressions.Regex.IsMatch(t,@" \([0-9]+\)")),"no native identity numbers in visible destination UI");
            Capture(direction);string nativeId=direction=="tuck-top"?"ok":"cancel";int expected=ui.OptionIndex(nativeId);
            var button=root.GetComponentsInChildren<Button>().First(b=>b.gameObject.activeInHierarchy&&b.name=="SelectionAction_"+expected);
            RayClick(button);Check(submissions==1&&last==expected,"one destination click submits exactly the native "+nativeId);
        }
        var livingMeta=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(".tools/interaction-fixes-1013/native/living-latest.json"));
        var living=Directory.GetFiles((string)livingMeta["folder"],"*-seat1.json").Select(Read).First(v=>v.request.kind=="InputConfirm");
        living.request.message="Shardless Agent (151) - \n\nDo you want to play Total Containment Failure?";
        ui.Present(living,1,true,_=>{},null,"");yield return Frame();
        string prompt=(string)typeof(ForgeBattlefieldController).GetMethod("ResolutionChoiceInstruction",Flags).Invoke(ui,new object[]{living.request.message,true});
        Check(prompt.Contains(GamePreferences.CardFaceName("Living End"))&&!prompt.Contains("收容大失利"),"confirmation uses displayed card despite alternate name in prompt");Capture("confirmation-name");
        Check(GamePreferences.T("无断片密探 (35)")=="无断片密探","localized source ID removed");
        Check(GamePreferences.T("Shardless Agent (35)")=="Shardless Agent","canonical source ID removed");
        Check(GamePreferences.T("X=3，费用 {2}{U}，3/3，剩余 (2)")=="X=3，费用 {2}{U}，3/3，剩余 (2)","gameplay numbers preserved");
        Check(GamePreferences.T("用户昵称 (35)")=="用户昵称 (35)","unknown/user text preserved");
        var empty=before;empty.awaitingInput=false;empty.request=null;empty.stackItems=Array.Empty<ForgeStackItem>();empty.stack=Array.Empty<string>();empty.stackCount=0;
        ui.Present(empty,1,false,null,null,"");yield return Frame();
        var canvas=GameObject.Find("Canvas").transform;
        var tile=new GameObject("AbilityFontSample",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();tile.SetParent(canvas,false);
        tile.sizeDelta=new Vector2(240,335);tile.anchoredPosition=Vector2.zero;
        var present=typeof(ForgeBattlefieldController).GetMethod("PresentAbilityFace",Flags);
        var card=new ForgeCardView{id=35,name="Mishra's Bauble",printingKey="BRR/34",currentRules="Draw a card at the beginning of the next turn's upkeep.",zone="Graveyard"};
        foreach(float textScale in new[]{1f,1.3f}) {
            GamePreferences.SetScale(textScale);
            string shortText="Draw a card at the beginning of the next turn's upkeep.";
            present.Invoke(ui,new object[]{tile,card,shortText,"trigger",true});yield return Frame();
            var face=tile.Find("AbilityFace");var body=face.Find("EffectClip/Effect/Symbols").GetComponent<TMP_Text>();var title=face.Find("Name").GetComponent<Text>();
            float shortSize=body.fontSize;Check(shortSize>13*textScale&&shortSize<=18*textScale+.01f,"short body grows within bounds at "+textScale+": "+shortSize);
            Check(Mathf.Abs(title.fontSize-Mathf.Round(15*textScale))<.01f,"title size unchanged at "+textScale);Capture("short-body-"+textScale);
            string dense=string.Join("\n",Enumerable.Repeat("在下个回合的维持开始时，抓一张牌，然后选择一个由你操控的生物。",9));
            present.Invoke(ui,new object[]{tile,card,dense,"trigger",true});yield return Frame();
            Check(body.fontSize>=13*textScale-.01f&&body.fontSize<=shortSize,"long body keeps readable floor at "+textScale);
            var scroll=face.Find("EffectClip").GetComponent<ScrollRect>();Check(scroll.content.rect.height>scroll.viewport.rect.height,"long body scrolls instead of shrinking below floor");
            Check(body.text.Contains("选择一个由你操控的生物"),"long body text retained");Capture("long-body-"+textScale);
        }
        UnityEngine.Object.Destroy(tile.gameObject);GamePreferences.SetScale(scale);ui.enabled=enabled;
    }
}
#endif
