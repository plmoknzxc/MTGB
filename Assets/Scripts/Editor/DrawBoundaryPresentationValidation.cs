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

// Native ended envelopes through the existing result/readonly consumer.
// This is UI adapter replay; native game outcomes are independently asserted.
public static class DrawBoundaryPresentationValidation
{
    public static bool Running {get;private set;}
    public static string Output {get;private set;}
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    [Serializable] class Meta {public string folder,status;}
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Set(object o,string property,object value)=>o.GetType().GetProperty(property,Flags).GetSetMethod(true).Invoke(o,new[]{value});
    static void Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Flags).Invoke(o,args);
    static void Field(object o,string field,object value)=>o.GetType().GetField(field,Flags).SetValue(o,value);
    static T Field<T>(object o,string field)=>(T)o.GetType().GetField(field,Flags).GetValue(o);
    static void Click(Button b)
    {
        Canvas.ForceUpdateCanvases();var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.TransformPoint(((RectTransform)b.transform).rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
        Check(b.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Result ray blocked: "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);
    }
    public static void Run()
    {
        Check(Application.isPlaying&&!Running,"Play required/already running");Running=true;Output=".tools/draw-boundaries-1007/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(".tools/draw-boundaries-1007/ui/latest.txt",Output);
        foreach(string p in new[]{"Assets/Scripts/Editor/DrawBoundaryPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.EndReview.cs","Assets/Scripts/Networking/OnlineMatchService.EndReview.cs","Assets/Scripts/Networking/OnlineMatchSession.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("DrawBoundaryValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        var tasks=new Stack<IEnumerator>();tasks.Push(Validate(ui));try {while(tasks.Count>0){object next;try{var t=tasks.Peek();if(!t.MoveNext()){tasks.Pop();continue;}next=t.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator nested)tasks.Push(nested);else yield return next;}File.WriteAllText(Output+"/result.txt","PASS: actual empty-loss/Laboratory-Maniac native ended envelopes winner2/1, both final seat projections, Chinese current result title; result/continue/reopen and readonly public exile browsing by ray, no rule choices or callbacks, fixed fonts/bounds and exact public IDs. Adapter replay only; payment/replacement/optional/target/life consumers reuse prior proofs, native draw/expiry/outcome separately tested. No new live network or whole-locale claim.");}finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}
    }
    static IEnumerator Frame(){yield return null;yield return new WaitForSecondsRealtime(.25f);}
    static IEnumerator Shot(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForSecondsRealtime(.15f);}
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        var service=OnlineMatchService.EnsureInstance();Check(!service.IsInMatch&&service.EndReview==null,"Existing match/review must be preserved");bool enabled=ui.enabled,serviceEnabled=service.enabled;ui.enabled=false;service.enabled=false;var oldService=Field<OnlineMatchService>(ui,"service");var oldView=service.ForgeView;int oldSeat=service.LocalSeat;var module=EventSystem.current.currentInputModule;bool moduleEnabled=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        string language=GamePreferences.Language;int revision=GamePreferences.Revision;GamePreferences.SetLanguage("zh-CN");float until=Time.realtimeSinceStartup+15;while((!GamePreferences.Ready||language!="zh-CN"&&GamePreferences.Revision==revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale not ready");
        try {Field(ui,"service",service);foreach(string scenario in new[]{"empty-loss","empty-lab-win"}) {
            var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/draw-boundaries-1007/native/"+scenario+"-result.json"));Check(meta.status=="PASS","Native incomplete");string path=meta.folder+"/ended.json";File.Copy(path,Output+"/inputs/"+scenario+".json");File.AppendAllText(Output+"/inputs.tsv",scenario+"\t"+path+"\n");
            var wire=JsonUtility.FromJson<ForgeWireMessage>(File.ReadAllText(path));Check(wire.type=="ended"&&wire.views.Length==2&&wire.winnerSeat==(scenario=="empty-loss"?2:1),"Native winner changed");
            foreach(int seat in new[]{1,2}) {
                var v=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(wire.views[seat-1]));Check(v.seat==seat&&v.players[0].libraryCount==0,"Wrong final projection");v.request=null;v.awaitingInput=false;v.inputPending=false;
                var state=new OnlinePlayerViewState{matchId=scenario+seat,forge=v,viewerSeat=seat,outcome=new MatchOutcome{isFinished=true,winnerPlayerId=wire.winnerSeat,message="Forge 对局结束。"}};state.SealHash();Set(service,"LocalSeat",seat);Set(service,"ForgeView",state);Call(service,"BeginEndReview",new OnlineMatchEndedMessage{matchId=state.matchId,winnerSeat=wire.winnerSeat,reason="Forge 对局结束。",finalView=state});Call(ui,"PresentEndReview");yield return Frame();
                foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}
                var panel=GameObject.Find("MatchResult").transform.Find("Result");Check(panel.GetComponentsInChildren<Text>().Any(t=>t.text=="玩家 "+wire.winnerSeat+" 获胜"),"Wrong winner label");var fonts=panel.GetComponentsInChildren<Text>(true).Select(t=>t.fontSize).ToArray();Check(panel.GetComponentsInChildren<Text>(true).All(t=>!t.resizeTextForBestFit),"Result resizes text");
                var rect=(RectTransform)panel;var corners=new Vector3[4];rect.GetWorldCorners(corners);var canvas=panel.GetComponentInParent<Canvas>().GetComponent<RectTransform>();Check(corners.All(c=>canvas.rect.Contains(canvas.InverseTransformPoint(c))),"Result outside canvas");yield return Shot(scenario+"-seat"+seat);
                Click(panel.Find("继续查看").GetComponent<Button>());yield return Frame();var back=Field<Button>(ui,"leaveReviewButton");Check(!panel.gameObject.activeSelf&&back.gameObject.activeSelf&&service.EndReview!=null,"Continue ended review failed");
                var publicCards=v.players[0].exileCards;Check(publicCards.Length==60&&publicCards.Select(c=>c.id).Distinct().Count()==60,"Public exile IDs changed");Call(ui,"ShowReviewCards","公开放逐",publicCards);yield return Frame();var browser=Field<RectTransform>(ui,"cardSelectionOverlay");Check(browser.gameObject.activeSelf,"Readonly list missing");Check(!browser.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("Select_")||b.name.StartsWith("Option_")||b.name.StartsWith("Confirm")),"Ended review exposes choices");ui.Choose(0,123456);Check(service.EndReview!=null&&v.request==null&&!v.awaitingInput,"Readonly view submitted a choice");
                Click(back);yield return Frame();Check(!browser.gameObject.activeSelf&&panel.gameObject.activeSelf&&!back.gameObject.activeSelf&&fonts.SequenceEqual(panel.GetComponentsInChildren<Text>(true).Select(t=>t.fontSize)),"Reopen/font identity changed");
                File.AppendAllText(Output+"/steps.log",scenario+" seat"+seat+" winner/readonly/rays/fixed fonts PASS\n");Call(service,"ClearEndReview");UnityEngine.Object.Destroy(Field<Canvas>(ui,"endCanvas").gameObject);UnityEngine.Object.Destroy(back.gameObject);Field(ui,"reviewedMatch",null);yield return Frame();
            }
        }} finally {Call(service,"ClearEndReview");var end=Field<Canvas>(ui,"endCanvas");if(end!=null)UnityEngine.Object.Destroy(end.gameObject);var back=Field<Button>(ui,"leaveReviewButton");if(back!=null)UnityEngine.Object.Destroy(back.gameObject);Field(ui,"reviewedMatch",null);Field(ui,"service",oldService);Set(service,"ForgeView",oldView);Set(service,"LocalSeat",oldSeat);service.enabled=serviceEnabled;ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=moduleEnabled;GamePreferences.SetLanguage(language);}
    }
}
#endif
