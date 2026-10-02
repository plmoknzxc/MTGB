#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class ForgeInteractionValidation
{
    const string Folder="Tools/Validation/Reports/Interactions/";
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Folder+name+".json"));
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run() {
        if(!Application.isPlaying)throw new InvalidOperationException("Play mode required");
        var runner=new GameObject("InteractionTestRunner").AddComponent<ForgeBattlefieldController>();
        UnityEngine.Object.DontDestroyOnLoad(runner);runner.StartCoroutine(Guard(runner));
    }
    static IEnumerator Guard(ForgeBattlefieldController runner) {
        var steps=Validate();Exception failure=null;
        while(true){object next=null;bool more=false;try{more=steps.MoveNext();if(more)next=steps.Current;}catch(Exception e){failure=e;}if(failure!=null||!more)break;yield return next;}
        File.WriteAllText(Folder+"ui-result.txt",failure==null?"PASS: outside left/right click cancels; ability source art/effect; confirm/reselect; picker retains card instances and scroll position.":"FAIL "+failure);
        if(failure!=null)Debug.LogException(failure);UnityEngine.Object.Destroy(runner.gameObject);
    }
    static IEnumerator Validate() {
        yield return SceneManager.LoadSceneAsync("IntegratedBattlefield");
        foreach(var notice in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(notice);
        var ui=new GameObject("InteractionUiValidation").AddComponent<ForgeBattlefieldController>();
        foreach(var mouse in new[]{PointerEventData.InputButton.Left,PointerEventData.InputButton.Right}) {
            var state=Read("mana-choice");int sent=-1;
            ui.Present(state,1,true,i=>sent=i,()=>{},"验证");yield return null;Canvas.ForceUpdateCanvases();
            var data=new PointerEventData(EventSystem.current){position=new Vector2(200,400),button=mouse};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Check(hits.Count>0 && hits[0].gameObject.name=="DismissAbilityChoices","Outside click shield missing");
            ExecuteEvents.Execute(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
            Check(sent==Array.FindIndex(state.request.options,o=>o.id=="cancel"),"Outside cancel not submitted");
            Check(GameObject.Find("CardAbilityChoices")==null,"Cancelled menu remains visible");
        }
        var selected=Read("trigger-selected");ui.Present(selected,1,true,_=>{},()=>{},"验证");yield return new WaitForSecondsRealtime(.5f);
        var origin=GameObject.Find("TargetOrigin");Check(origin!=null,"Missing target source");
        Check(origin.GetComponent<Image>().sprite==null,"Source rules card bleeds through ability face");
        Check(origin.transform.Find("AbilityFace/Art").GetComponent<RawImage>().texture!=null,"Missing ability illustration");
        Check(!string.IsNullOrWhiteSpace(origin.transform.Find("AbilityFace/Effect").GetComponent<Text>().text),"Missing effect text");
        foreach(string button in new[]{"ConfirmTarget","ResetTarget"})Check(origin.transform.Find(button).GetComponent<Button>().interactable,button+" disabled");
        ScreenCapture.CaptureScreenshot(Folder+"trigger-ui.png");yield return new WaitForEndOfFrame();
        var stacked=Read("library-0");
        stacked.awaitingInput=false;
        stacked.request=null;
        // Refresh twice: the second update used to reveal the source fallback label.
        ui.Present(stacked,1,false,_=>{},()=>{},"验证");yield return null;
        ui.Present(stacked,1,false,_=>{},()=>{},"验证");yield return new WaitForSecondsRealtime(.5f);
        var tile=(RectTransform)GameObject.Find("StackCard_0").transform;
        var face=(RectTransform)tile.Find("AbilityFace");var corners=new Vector3[4];face.GetWorldCorners(corners);
        var bounds=tile.rect;bounds.xMin-=.5f;bounds.xMax+=.5f;bounds.yMin-=.5f;bounds.yMax+=.5f;
        Check(corners.All(c=>bounds.Contains((Vector2)tile.InverseTransformPoint(c))),"Ability face outside stack tile");
        Check(!tile.Find("Fallback").gameObject.activeSelf,"Duplicate ability title");
        Check(face.Find("Art").GetComponent<RawImage>().texture!=null,"Stack ability art missing");
        Check(face.Find("Effect").GetComponent<Text>().verticalOverflow==VerticalWrapMode.Truncate,"Preferences removed ability text bounds");
        Check(face.Find("Kind")==null && tile.Find("Details")==null,"Redundant ability labels remain");
        var glow=tile.GetComponentInChildren<ForgeStackGlow>();
        Check(glow!=null && glow.enabled && !glow.raycastTarget,"Active border missing or intercepting input");
        Canvas.ForceUpdateCanvases();
        var glowMesh=glow.canvasRenderer.GetMesh();
        Check(glowMesh!=null && glowMesh.vertexCount>0,"Glow did not render a border");
        var beforeGlow=glowMesh.colors32;
        yield return new WaitForSecondsRealtime(.4f);
        Canvas.ForceUpdateCanvases();
        Check(!beforeGlow.SequenceEqual(glow.canvasRenderer.GetMesh().colors32),"Glow is static");
        var pointer=new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(tile.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
        Check(GameObject.Find("InspectPanel")!=null,"Stack hover did not show details");
        var inspect=(RectTransform)GameObject.Find("InspectPanel").transform;
        Check(inspect.position.x>Screen.width*.5f,"Stack hover still uses fixed left sidebar");
        ExecuteEvents.Execute(tile.gameObject,pointer,ExecuteEvents.pointerExitHandler);
        Check(GameObject.Find("InspectPanel")==null,"Stack hover did not close details");
        ScreenCapture.CaptureScreenshot(Folder+"stack-ui.png");yield return new WaitForEndOfFrame();
        var lower=JsonUtility.FromJson<ForgeStackItem>(JsonUtility.ToJson(stacked.stackItems[0]));lower.id+=10000;
        stacked.stackItems=new[]{stacked.stackItems[0],lower};stacked.stackCount=2;
        ui.Present(stacked,1,false,_=>{},()=>{},"验证");yield return null;
        var nextGlow=GameObject.Find("StackCard_1").GetComponentInChildren<ForgeStackGlow>();
        Check(!nextGlow.enabled,"Inactive stack item glows");
        yield return new WaitForSecondsRealtime(.4f);
        ScreenCapture.CaptureScreenshot(Folder+"stack-layered-ui.png");yield return new WaitForEndOfFrame();
        stacked.stackItems=new[]{lower};stacked.stackCount=1;
        ui.Present(stacked,1,false,_=>{},()=>{},"验证");yield return null;
        Check(nextGlow.enabled,"Glow did not transfer to next stack item");
        var first=Read("library-0");var next=Read("library-1");ui.Present(first,1,true,_=>{},()=>{},"验证");yield return null;
        var overlay=GameObject.Find("CardSelectionOverlay");
        var ids=overlay.GetComponentsInChildren<RectTransform>().Where(t=>t.name.StartsWith("ChoiceCard_")).ToDictionary(t=>t.name,t=>t.GetInstanceID());
        var scroll=overlay.GetComponentInChildren<ScrollRect>();scroll.verticalNormalizedPosition=.4f;yield return null;
        float position=scroll.verticalNormalizedPosition;
        var ack=Read("library-ack");Check(ack.inputPending && !ack.awaitingInput && !(ack.request?.requestId>0),"Not a server acknowledgement");
        ui.Present(ack,1,false,_=>{},()=>{},"验证");yield return null;
        Check(overlay.activeInHierarchy,"Server acknowledgement closed picker");
        Check(overlay.GetComponentsInChildren<Button>().All(b=>!b.interactable),"Pending picker accepts duplicate action");
        Check(!ui.TrySubmitPresentedOption(0,first.request.requestId),"Pending request resubmitted");
        ui.Present(next,1,true,_=>{},()=>{},"验证");yield return null;
        Check(overlay.GetComponentsInChildren<RectTransform>().Where(t=>t.name.StartsWith("ChoiceCard_")).All(t=>ids[t.name]==t.GetInstanceID()),"Selection rebuilt card faces");
        Check(Mathf.Abs(scroll.verticalNormalizedPosition-position)<.01f,"Selection reset scrolling");
        Check(overlay.transform.Find("CardSelection/NextPage").GetComponent<Button>().interactable,"Paging stayed disabled after acknowledgement");
        ScreenCapture.CaptureScreenshot(Folder+"library-ui.png");yield return new WaitForEndOfFrame();
        UnityEngine.Object.Destroy(ui.gameObject);
    }
}
#endif
