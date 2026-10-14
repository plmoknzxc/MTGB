#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Only the actual delayed WrappedAbility/source-zone and independent copy lifecycle delta.
public static class DelayedCopyPresentationValidation
{
    const string Base=".tools/delayed-trigger-copy-1009";
    public static bool Running{get;private set;}
    public static string Output{get;private set;}
    static int checks;
    static bool remembered;
    static void Check(bool condition,string why){if(!condition)throw new Exception(why);checks++;}
    static ForgeSeatView Read(string file)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Output+"/inputs/"+file));
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.55f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Click(Button button){var rect=(RectTransform)button.transform;var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Actual topmost target ray "+button.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);}
    public static void Run()=>Start(false);
    public static void RunRemembered()=>Start(true);
    static void Start(bool rememberedOnly){
        Check(Application.isPlaying&&!Running,"Play required/already running");Running=true;checks=0;
        remembered=rememberedOnly;
        Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        foreach(string p in Directory.GetFiles(Base+"/ui-inputs"))File.Copy(p,Output+"/inputs/"+Path.GetFileName(p));
        foreach(string p in new[]{"Assets/Scripts/Editor/DelayedCopyPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Stack.cs","Assets/Scripts/UI/ForgeBattlefieldController.AbilityFace.cs","Assets/Scripts/UI/ForgeBattlefieldController.StackList.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("DelayedCopyIncrement").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator routine){var tasks=new Stack<IEnumerator>();tasks.Push(routine);try{while(tasks.Count>0){object next;try{var task=tasks.Peek();if(!task.MoveNext()){tasks.Pop();continue;}next=task.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator child)tasks.Push(child);else yield return next;}File.WriteAllText(Output+"/result.txt","PASS: "+checks+" checks; "+(remembered?"actual nonempty remembered-token131 expiry original/copy, LKI source illustration and native remembered payload visible/reachable for both seats; independent copy removal and original valid-no-op cleanup. Earlier actual target rays retained, not repeated.":"actual delayed-trigger original/copy share graveyard source but have independent UI roots; both seats readable source art/effect with fixed symbol font, exact original target ray and native selected/confirm response, stale/duplicate/read-only guard, copy removal retains original and final cleanup.")+" Existing generic language/list/payment contracts reused; no full language/network claim.");}finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui){
        string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{
            GamePreferences.SetLanguage("zh-CN");float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Preferences ready");
            foreach(int seat in new[]{1,2}){
                var v=Read((remembered?"remembered-":"")+"copied-trigger-seat"+seat+".json");Check(v.stackItems.Length==2&&v.stackItems.All(s=>s.kind=="trigger"&&s.source.id==121&&s.source.zone==(remembered?"":"Graveyard")),"Exact actual delayed source/zone");Check(v.stackItems[0].copied&&!v.stackItems[1].copied&&v.stackItems[0].id!=v.stackItems[1].id,"Exact actual copy identity");
                int calls=0;ui.Present(null,seat,false,null,null,"");ui.Present(v,seat,false,_=>calls++,null,"");yield return Frame();
                var roots=new[]{GameObject.Find("StackCard_0"),GameObject.Find("StackCard_1")};Check(roots.All(r=>r!=null)&&roots[0]!=roots[1],"Independent stack card roots");
                foreach(var root in roots){var face=root.transform.Find("AbilityFace");Check(face!=null&&face.gameObject.activeInHierarchy,"Actual trigger face visible");Check(face.Find("Art").GetComponent<RawImage>().texture!=null,"Source illustration loaded");Check(face.Find("Name").GetComponent<Text>().text==GamePreferences.CardFaceName(remembered?"Zektar Shrine Expedition":"Mishra's Bauble"),"Actual source title");var effect=face.Find("EffectClip/Effect").GetComponent<Text>();var symbols=effect.transform.Find("Symbols").GetComponent<TextMeshProUGUI>();symbols.ForceMeshUpdate();Check(!effect.resizeTextForBestFit&&!symbols.enableAutoSizing&&symbols.text.Contains(remembered?"131":"2"),"Fixed effect plus actual native payload");Check(symbols.textInfo.characterCount>0&&symbols.GetPreferredValues(symbols.text,208,10000).y<=effect.rectTransform.rect.height+1,"Whole trigger effect reachable");Check(!root.GetComponent<Button>().interactable||!ui.TrySubmitPresentedOption(0,v.request.requestId),"Readonly trigger not submitting");}
                Check(calls==0,"Readonly both seats no submission");ScreenCapture.CaptureScreenshot(Output+"/seat"+seat+"-copied.png");yield return Frame();
                if(seat==1){var original=roots[1];ui.Present(Read((remembered?"remembered-":"")+"copy-resolved-seat1.json"),1,false,null,null,"");yield return Frame();Check(GameObject.Find("StackCard_0")==original&&GameObject.Find("StackCard_1")==null,"Copy removal retains exact original UI root");ui.Present(Read((remembered?"remembered-":"")+"both-resolved-seat1.json"),1,false,null,null,"");yield return Frame();Check(!GameObject.Find("Canvas").transform.Find("StackColumn").gameObject.activeSelf,"Both resolved clears stack");}
            }
            if(remembered)yield break;
            var target=Read("stack-target-62-seat1.json");int submitted=0,last=-1;ui.Present(null,1,false,null,null,"");ui.Present(target,1,true,i=>{submitted++;last=i;},null,"");yield return Frame();
            int index=Array.FindIndex(target.request.options,o=>o.id=="stack:3");Check(index>=0&&!ui.TrySubmitPresentedOption(index,target.request.requestId-1)&&submitted==0,"Stale trigger request rejected");Click(GameObject.Find("StackCard_0").GetComponent<Button>());ui.Choose(index,target.request.requestId);Check(submitted==1&&last==index,"Exact original stack3 submitted once");
            var selected=Read("stack-target-64-seat1.json");Check(selected.request.selectedCount==1&&selected.request.options.Any(o=>o.id=="stack:3"&&o.selected)&&selected.request.options.Any(o=>o.id=="ok"),"Native selected and confirm gate");ui.Present(selected,1,true,i=>{submitted++;last=i;},null,"");yield return Frame();
            int ok=Array.FindIndex(selected.request.options,o=>o.id=="ok");Check(ui.TrySubmitPresentedOption(ok,selected.request.requestId)&&submitted==2&&last==ok,"Native confirmation exactly once");Check(!ui.TrySubmitPresentedOption(ok,selected.request.requestId)&&submitted==2,"Duplicate confirm rejected");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=input;GamePreferences.SetLanguage(locale);for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
