#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public static class CardSelectionValidation
{
    const string Folder="Tools/Validation/Reports/CardSelection";
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Run only in Play mode");
        var runner=new GameObject("CardSelectionValidation").AddComponent<ForgeBattlefieldController>();
        UnityEngine.Object.DontDestroyOnLoad(runner.gameObject);runner.StartCoroutine(Guard(runner));
    }
    static IEnumerator Guard(ForgeBattlefieldController runner)
    {
        var steps=Validate();Exception failure=null;
        while(true){object next=null;bool more=false;try{more=steps.MoveNext();if(more)next=steps.Current;}catch(Exception e){failure=e;}
            if(failure!=null||!more)break;yield return next;}
        File.WriteAllText(Folder+"/ui-result.txt",failure==null?"PASS: real engine snapshots rendered; card images; pagination; button hit testing; duplicate/stale submissions blocked; opponent and idle views dismiss window.":"FAIL "+failure);
        if(failure!=null)Debug.LogException(failure);UnityEngine.Object.Destroy(runner.gameObject);
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static IEnumerator Validate()
    {
        yield return SceneManager.LoadSceneAsync(ForgeBattlefieldController.SceneName);
        foreach(var notice in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(notice);
        var ui=new GameObject("SelectionUiValidation").AddComponent<ForgeBattlefieldController>();
        foreach(string name in new[]{"surveil","library","hand","graveyard","exile","order","reveal","multi-library-context","explore-grave","fetch-live"})
        {
            var state=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Folder+"/"+name+(name.EndsWith("context")?".json":"-choice.json")));
            int submissions=0;
            ui.Present(state,state.request.seat,true,_=>submissions++,()=>{},"交互验证");
            yield return new WaitForSecondsRealtime(.4f);
            var overlay=GameObject.Find("CardSelectionOverlay");Check(overlay!=null,"Missing picker: "+name);
            Check(!overlay.GetComponentsInChildren<Transform>().Any(t=>t.name=="ChoiceCard_0"),"Non-card action rendered as empty card");
            if(name=="library")
            {
                var next=overlay.GetComponentsInChildren<Button>().Single(b=>b.name=="NextPage");next.onClick.Invoke();yield return null;
                Check(overlay.GetComponentsInChildren<Text>().Any(t=>t.name=="Page" && t.text.StartsWith("2 /")),"Pagination did not advance");
                overlay.GetComponentsInChildren<Button>().Single(b=>b.name=="PreviousPage").onClick.Invoke();yield return null;
            }
            var images=overlay.GetComponentsInChildren<Image>();Check(images.Any(i=>i.name=="Artwork" && i.sprite!=null),"Missing card artwork: "+name);
            if(name!="surveil")Check(!overlay.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("Select_")),"Redundant per-card select buttons remain");
            if(name=="multi-library-context")
            {
                Check(!overlay.GetComponentsInChildren<Text>().Any(t=>t.name=="Selection"),"Redundant selection captions remain");
                var tiles=overlay.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("ChoiceCard_")).ToArray();
                bool sawDisabled=false;
                foreach(var tile in tiles)
                {
                    var art=tile.Find("Artwork");bool disabled=art.GetComponent<Button>()==null;
                    if(disabled){sawDisabled=true;Check(art.GetComponent<Image>().color.r<.5f,"Ineligible card not dimmed");}
                    else Check(!sawDisabled,"Eligible card placed after ineligible card");
                }
            }
            var button=overlay.GetComponentsInChildren<Button>().First(b=>(b.name=="Artwork" || b.name.StartsWith("Select_") || b.name.StartsWith("SelectionAction_")) && b.interactable);
            var hits=new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position)},hits);
            Check(hits.Any() && hits[0].gameObject.GetComponentInParent<Button>()==button,"Picker button obstructed: "+name);
            ScreenCapture.CaptureScreenshot(Folder+"/"+name+"-ui.png");yield return new WaitForEndOfFrame();
            Check(!ui.TrySubmitPresentedOption(0,state.request.requestId-1),"Stale request accepted");
            button.onClick.Invoke();button.onClick.Invoke();Check(submissions==1,"Duplicate choice submitted");
            ui.Present(state,state.request.seat,false,_=>submissions++,()=>{},"等待服务器");yield return null;
            Check(!overlay.GetComponentsInChildren<Button>().Any(b=>(b.name=="Artwork" || b.name.StartsWith("Select_") || b.name.StartsWith("SelectionAction_"))&&b.interactable),"Disconnected input enabled");
            var hidden=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(state));hidden.awaitingInput=false;hidden.request=null;
            ui.Present(hidden,3-state.request.seat,true,_=>submissions++,()=>{},"等待对手");yield return null;
            Check(!overlay.activeSelf,"Hidden choices linger in opponent view");
        }
        UnityEngine.Object.Destroy(ui.gameObject);
    }
}
#endif
