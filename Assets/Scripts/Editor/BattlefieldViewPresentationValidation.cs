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

public static class BattlefieldViewPresentationValidation
{
    const string Output=".tools/battlefield-view-review-1004",Input=Output+"/input/";
    static bool running;
    static void Check(bool b,string s){if(!b)throw new Exception(s);}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Input+name+".json"));
    static Button Find(string name)=>GameObject.Find(name).GetComponent<Button>();
    static RectTransform[] Windows(ForgeBattlefieldController ui)=>((IEnumerable<RectTransform>)typeof(ForgeBattlefieldController).GetMethod("BattlefieldWindows",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ui,null)).Where(w=>w!=null&&w.gameObject.activeInHierarchy).Distinct().ToArray();
    static void Click(Button b){Canvas.ForceUpdateCanvases();var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.TransformPoint(((RectTransform)b.transform).rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked button "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    static IEnumerator Frame(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static IEnumerator Shot(string name){ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForEndOfFrame();}
    public static void Run(){if(running)return;Check(Application.isPlaying,"Requires Play Mode");running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("BattlefieldViewValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    public static void RunResultWindowOnly()
    {
        if(running)return;Check(Application.isPlaying,"Requires Play Mode");running=true;
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("ResultWindowValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(ResultWindowGuard(ui));
    }
    static IEnumerator ResultWindowGuard(ForgeBattlefieldController ui)
    {
        var it=ResultWindow(ui);string path=Output+"/end-result-adapter.txt";File.WriteAllText(path,"RUNNING");
        try {
            while(true){object next;try{if(!it.MoveNext())break;next=it.Current;}catch(Exception e){File.WriteAllText(path,"FAIL "+e);throw;}yield return next;}
            File.WriteAllText(path,"PASS: synthetic higher-sort end-result window hides/restores by real View/Return raycasts; settings-side return stays reachable. This is a UI adapter, not actual match ending or live network.");
        }finally{running=false;ui.RestoreBattlefieldWindows();}
    }
    static IEnumerator ResultWindow(ForgeBattlefieldController ui)
    {
        var v=Read("real-stack");v.awaitingInput=false;v.request=null;ui.Present(null,1,false,null,null,"");ui.Present(v,1,false,null,null,"");
        var go=new GameObject("EndResultAdapter",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;go.GetComponent<Canvas>().sortingOrder=600;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var panel=new GameObject("Result",typeof(RectTransform),typeof(Image));panel.transform.SetParent(go.transform,false);var rect=(RectTransform)panel.transform;rect.sizeDelta=new Vector2(760,350);panel.GetComponent<Image>().color=Color.black;
        var field=typeof(ForgeBattlefieldController).GetField("endDialog",BindingFlags.NonPublic|BindingFlags.Instance);field.SetValue(ui,rect);
        yield return Frame();Click(Find("ViewBattlefield"));yield return Frame();Check(rect.GetComponent<CanvasGroup>().alpha==0&&!rect.GetComponent<CanvasGroup>().blocksRaycasts,"End result still blocks battlefield");Click(Find("ReturnSelection"));yield return Frame();Check(rect.GetComponent<CanvasGroup>().alpha==1&&rect.GetComponent<CanvasGroup>().blocksRaycasts,"End result did not restore");
        field.SetValue(ui,null);UnityEngine.Object.Destroy(go);yield return Frame();
    }
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        string language=GamePreferences.Language;var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=banner!=null&&banner.enabled;if(banner!=null)banner.enabled=false;
        var it=Validate(ui);
        try{while(true){object next;try{if(!it.MoveNext())break;next=it.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: archived actual native modal consumers; raycast View/Return, all registered active windows/blockers hide without deactivation, scroll/input/selection preservation, no hidden submissions, same-context updates and new-context reset, seat/null reset, native permission/stale/duplicate guards after restore; three languages at current scale; native stack remains visible. Notification/history/mulligan fixtures are UI adapters, not effect tests. End-result window registration source-reviewed; full network/end lifecycle not exercised.");}
        finally{ui.RestoreBattlefieldWindows();running=false;GamePreferences.SetLanguage(language);if(banner!=null)banner.enabled=enabled;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            GamePreferences.SetLanguage(language);float end=Time.realtimeSinceStartup+10;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<end)yield return null;Check(GamePreferences.Ready,"Locale unavailable");
            var cases=language=="zh-CN"?new[]{"modes","selected-modes","mandatory-modes","kicker","evoke","cards","piles","allocation","order","names","generic","number","dungeon","value","scry"}:new[]{"modes","cards","names"};
            foreach(string name in cases) {
                var v=Read(name);int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,true,submit,null,"");yield return Frame();
                var roots=Windows(ui);Check(roots.Length>0,"No actual window "+name);
                var input=roots.SelectMany(w=>w.GetComponentsInChildren<InputField>()).FirstOrDefault();if(input!=null)input.text="Boros";
                var scrolls=roots.SelectMany(w=>w.GetComponentsInChildren<ScrollRect>()).Distinct().ToArray();foreach(var s in scrolls){s.verticalNormalizedPosition=.37f;s.horizontalNormalizedPosition=.23f;}
                yield return Frame();var positions=scrolls.Select(s=>s.normalizedPosition).ToArray();string inputText=input?.text;
                yield return Shot(language+"-"+name+"-choice");
                var preview=Find("ViewBattlefield");var label=preview.GetComponentInChildren<Text>();Check(!label.resizeTextForBestFit&&label.preferredWidth<=label.rectTransform.rect.width+1,"View label truncates");Click(preview);yield return Frame();
                foreach(var w in roots){var group=w.GetComponent<CanvasGroup>();Check(w.gameObject.activeInHierarchy&&group.alpha==0&&!group.blocksRaycasts&&!group.interactable,"Window/blocker remains visible "+w.name);}
                var back=Find("ReturnSelection");var backLabel=back.GetComponentInChildren<Text>();Check(backLabel.preferredWidth<=backLabel.rectTransform.rect.width+1,"Return label truncates");
                var gear=(RectTransform)GameObject.Find("SettingsButton").transform;var corners=new Vector3[4];gear.GetWorldCorners(corners);var backCorners=new Vector3[4];((RectTransform)back.transform).GetWorldCorners(backCorners);Check(backCorners[0].x>corners[2].x,"Return overlaps settings");
                int index=Array.FindIndex(v.request.options,o=>!o.disabled);ui.Choose(index,v.request.requestId);Check(calls==0,"Hidden decision submitted");
                yield return Shot(language+"-"+name+"-battlefield");Click(back);yield return Frame();
                foreach(var w in roots)Check(w.GetComponent<CanvasGroup>().alpha>0,"Restore failed "+w.name);
                for(int i=0;i<scrolls.Length;i++)Check(Vector2.Distance(scrolls[i].normalizedPosition,positions[i])<.015f,"Scroll discarded "+name);
                Check(input==null||input.text==inputText,"Filter text discarded");
                yield return Shot(language+"-"+name+"-returned");
                ui.Choose(index,v.request.requestId-1);Check(calls==0,"Stale action accepted after restore");ui.Choose(index,v.request.requestId);ui.Choose(index,v.request.requestId);Check(calls==1&&last==index,"Native/duplicate mapping lost");
                ui.Present(v,v.seat,false,submit,null,"");yield return Frame();ui.Choose(index,v.request.requestId);Check(calls==1,"Nonacting client submits");
            }
        }
        // Two real snapshots of the same engine decision, followed by a different
        // decision. Never silently keep a new required prompt hidden.
        GamePreferences.SetLanguage("zh-CN");while(!GamePreferences.Ready)yield return null;
        var first=Read("modes");var selected=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/ModeChoices/choose-one-selected.json"));
        Check(first.request.contextId==selected.request.contextId&&first.request.requestId!=selected.request.requestId,"Bad context fixture");
        ui.Present(null,1,false,null,null,"");ui.Present(first,1,true,_=>{},null,"");yield return Frame();Click(Find("ViewBattlefield"));ui.Present(selected,1,true,_=>{},null,"");yield return Frame();Check(GameObject.Find("ReturnSelection")!=null&&GameObject.Find("CardAbilityChoices").GetComponent<CanvasGroup>().alpha==0,"Same decision reopened");Click(Find("ReturnSelection"));yield return Frame();Check(GameObject.Find("CardAbilityChoices").transform.Find("Count").GetComponent<Text>().text.Contains("1"),"Updated selection lost");
        Click(Find("ViewBattlefield"));ui.Present(Read("cards"),1,true,_=>{},null,"");yield return Frame();Check(GameObject.Find("ReturnSelection")==null&&GameObject.Find("CardSelectionOverlay").GetComponent<CanvasGroup>()?.alpha!=0,"New prompt hidden");
        Click(Find("ViewBattlefield"));ui.Present(null,2,false,null,null,"");yield return Frame();Check(GameObject.Find("ReturnSelection")==null,"Seat/null reset missing");
        var stack=Read("real-stack");ui.Present(stack,stack.seat,true,_=>{},null,"");yield return Frame();Check(GameObject.Find("StackColumn")!=null&&stack.stackItems.Length>0,"Native stack missing");yield return Shot("zh-CN-real-stack");
        // Synthetic notifications isolate the visibility/timer consumer without
        // rerunning unrelated random rules. Engine input above stays unmodified.
        var note=Read("modes");note.awaitingInput=false;note.request=null;note.notices=new[]{new ForgeNotice{id=99001,kind="notice",message="长通知 / Notice / 通知",value="长通知 / Notice / 通知"}};
        ui.Present(null,1,false,null,null,"");ui.Present(note,1,false,null,null,"");yield return Frame();Click(Find("ViewBattlefield"));yield return new WaitForSecondsRealtime(4.2f);Check(GameObject.Find("ResultToast").GetComponent<CanvasGroup>().alpha==0,"Hidden notice expired/escaped");Click(Find("ReturnSelection"));yield return Frame();Check(GameObject.Find("ResultToast").GetComponent<CanvasGroup>().alpha==1,"Notice not restored");
        Find("ResultHistoryButton").onClick.Invoke();yield return Frame();Check(GameObject.Find("ResultHistory")!=null,"History absent");Click(Find("ViewBattlefield"));yield return Frame();Check(GameObject.Find("ResultHistory").GetComponent<CanvasGroup>().alpha==0,"History not hidden");Click(Find("ReturnSelection"));yield return Frame();Check(GameObject.Find("ResultHistory").GetComponent<CanvasGroup>().alpha==1,"History not restored");yield return Shot("zh-CN-notice-history");
        var mulligan=Read("modes");mulligan.request=new ForgeRequest{kind="InputConfirmMulligan",requestId=99002,seat=1,options=new[]{new ForgeOption{id="ok",label="Keep"},new ForgeOption{id="cancel",label="Mulligan"}}};
        ui.Present(null,1,false,null,null,"");ui.Present(mulligan,1,true,_=>{},null,"");yield return Frame();Click(Find("ViewBattlefield"));yield return Frame();Check(GameObject.Find("MulliganChoices").GetComponent<CanvasGroup>().alpha==0,"Mulligan not hidden");Click(Find("ReturnSelection"));yield return Frame();
    }
}
#endif
