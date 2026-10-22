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

// Replays an unselected/selected pair captured from the same native target
// request. The native rule outcome is verified separately by its flow driver.
public static class TargetPickerModalValidation
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    public static bool Running {get;private set;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()=>Running=false;
    sealed class Host:MonoBehaviour{}
    static object Get(ForgeBattlefieldController ui,string name)=>typeof(ForgeBattlefieldController).GetField(name,Hidden).GetValue(ui);
    static void Check(bool ok,string message,List<string> checks){checks.Add((ok?"PASS ":"FAIL ")+message);if(!ok)throw new Exception(message);}
    static Button Action(ForgeBattlefieldController ui,ForgeSeatView v,string id)
    {
        int index=Array.FindIndex(v.request.options,o=>o.id==id);
        return index<0?null:((RectTransform)Get(ui,"selectionFooter")).Find("SelectionAction_"+index)?.GetComponent<Button>();
    }
    static void Click(Button button,List<string> checks)
    {
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position)};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Actual ray reaches "+button.name,checks);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    public static void Run(string initialPath,string selectedPath,string output)
    {
        if(Running)throw new InvalidOperationException("Validation already running");
        if(!Application.isPlaying)throw new InvalidOperationException("Play required");
        Directory.CreateDirectory(output);Running=true;
        new GameObject("TargetPickerModalValidation").AddComponent<Host>().StartCoroutine(Guard(Exercise(initialPath,selectedPath,output),output));
    }
    static IEnumerator Guard(IEnumerator routine,string output)
    {
        while(true){object current;try{if(!routine.MoveNext())break;current=routine.Current;}catch(Exception e){File.AppendAllText(output+"/result.txt","FAIL "+e);break;}yield return current;}
        (routine as IDisposable)?.Dispose();Running=false;
    }
    static IEnumerator Exercise(string initialPath,string selectedPath,string output)
    {
        var checks=new List<string>();string language=GamePreferences.Language;float scale=GamePreferences.TextScale;
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("TargetPickerBoard").AddComponent<ForgeBattlefieldController>();
        bool enabled=ui.enabled;ui.enabled=false;
        var catalog=(Dictionary<string,CardDefinition>)Get(ui,"catalog");foreach(var c in CardDatabaseRepository.LoadAll())if(!c.forgeAmbiguous)catalog.TryAdd(c.PrintingKey,c);
        int clicks=0;
        try{
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"})foreach(float size in new[]{1f,1.3f}){
                GamePreferences.SetLanguage(locale);while(!GamePreferences.Ready)yield return null;GamePreferences.SetScale(size);
                var initial=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(initialPath));
                var selected=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(selectedPath));
                int sent=-1;ui.Present(null,initial.seat,false,null,null,"");ui.Present(initial,initial.seat,true,i=>{sent=i;clicks++;},null,"");yield return new WaitForSecondsRealtime(.4f);
                Check(Action(ui,initial,"ok")==null||!Action(ui,initial,"ok").interactable,"Mandatory unselected request cannot confirm: "+locale+size,checks);
                ui.Present(selected,selected.seat,true,i=>{sent=i;clicks++;},null,"");yield return new WaitForSecondsRealtime(.4f);Canvas.ForceUpdateCanvases();
                var panel=(RectTransform)Get(ui,"selectionPanel");var cachedPanel=panel;
                var dock=(RectTransform)Get(ui,"targetChoiceActions");Check(dock==null||!dock.gameObject.activeInHierarchy,"Modal owns target controls",checks);
                var confirm=Action(ui,selected,"ok");var reset=Action(ui,selected,"reset-targets");
                Check(confirm!=null&&reset!=null&&confirm.interactable&&reset.interactable,"Native confirm and reset available",checks);
                typeof(ForgeBattlefieldController).GetMethod("RefreshBattlefieldViewControls",Hidden).Invoke(ui,null);
                int beforeViewing=clicks;
                Click((Button)Get(ui,"viewBattlefieldButton"),checks);
                var overlay=(RectTransform)Get(ui,"cardSelectionOverlay");var group=overlay.GetComponent<CanvasGroup>();
                Check(group!=null&&group.alpha==0&&!group.blocksRaycasts&&clicks==beforeViewing,"View hides modal without submitting",checks);
                // Activating ReturnSelection registers its graphics at the end
                // of this frame, before the next physical input event.
                yield return null;Canvas.ForceUpdateCanvases();
                Click((Button)Get(ui,"returnSelectionButton"),checks);
                Check(group.alpha==1&&group.blocksRaycasts&&ReferenceEquals(panel,Get(ui,"selectionPanel"))&&selected.request.selectedCount==1,"Return restores same modal and selected target",checks);
                foreach(var text in panel.GetComponentsInChildren<Text>().Where(t=>t.gameObject.activeInHierarchy&&t.name=="Instruction")){
                    Check(locale=="en-US"||!BattleTextDiagnostics.HasEnglishProse(text.text),"Localized target instruction: "+locale,checks);
                }
                ScreenCapture.CaptureScreenshot(output+"/"+locale+"-"+size.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png");yield return new WaitForEndOfFrame();
                Click(reset,checks);Check(sent>=0&&selected.request.options[sent].id=="reset-targets","Reset submits original ordinal",checks);
                ui.Present(initial,initial.seat,true,i=>sent=i,null,"");yield return null;
                Check(ReferenceEquals(cachedPanel,Get(ui,"selectionPanel")),"Selection/reset reuses modal",checks);
                ui.Present(selected,selected.seat,true,i=>{sent=i;clicks++;},null,"");yield return null;yield return null;
                int before=clicks;confirm=Action(ui,selected,"ok");Click(confirm,checks);
                Check(clicks==before+1&&selected.request.options[sent].id=="ok","Confirm submits original ordinal once",checks);
                confirm.onClick.Invoke();Check(clicks==before+1,"In-flight duplicate cannot submit",checks);
                ui.Present(selected,selected.seat,false,i=>clicks++,null,"");yield return null;
                Check(!Action(ui,selected,"ok").interactable,"Nonacting view cannot confirm",checks);
            }
            File.WriteAllLines(output+"/result.txt",checks);
        }finally{GamePreferences.SetLanguage(language);GamePreferences.SetScale(scale);ui.enabled=enabled;}
    }
}
#endif
