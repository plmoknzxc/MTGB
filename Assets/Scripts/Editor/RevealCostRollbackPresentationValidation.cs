#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Increment for the actual missing source after sacrifice and opponent hand reveal.
public static class RevealCostRollbackPresentationValidation
{
    const string Base=".tools/reveal-cost-rollback-1008";
    public static bool Running{get;private set;}
    public static string Output{get;private set;}
    static int checks;
    static void Check(bool v,string why){if(!v)throw new Exception(why);checks++;}
    static IEnumerator Frame(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static void Click(Button button){
        var rt=(RectTransform)button.transform;var corners=new Vector3[4];rt.GetWorldCorners(corners);
        var rect=Rect.MinMaxRect(corners.Min(c=>c.x),corners.Min(c=>c.y),corners.Max(c=>c.x),corners.Max(c=>c.y));
        for(int y=1;y<7;y++)for(int x=1;x<7;x++){
            var point=new Vector2(Mathf.Lerp(rect.xMin,rect.xMax,x/7f),Mathf.Lerp(rect.yMin,rect.yMax,y/7f));
            var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=point};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
            if(!button.interactable||hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=button)continue;
            Check(true,"Actual exposed button ray "+button.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);return;
        }
        throw new Exception("No exposed actual ray "+button.name);
    }
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Output+"/inputs/"+name+".json"));
    static Button Named(string name)=>GameObject.Find(name)?.GetComponent<Button>()??throw new Exception("Missing button "+name);
    public static void Run(){
        Check(Application.isPlaying&&!Running,"Play required/already running");Running=true;checks=0;
        Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");
        File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        foreach(string p in Directory.GetFiles(Base+"/ui-inputs"))File.Copy(p,Output+"/inputs/"+Path.GetFileName(p));
        foreach(string p in new[]{"Assets/Scripts/Editor/RevealCostRollbackPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.HandActions.cs","Assets/Scripts/UI/ForgeBattlefieldController.CardSelection.cs","Assets/Scripts/UI/ForgeBattlefieldController.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("RevealRollbackValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator work){
        var tasks=new Stack<IEnumerator>();tasks.Push(work);
        try{while(tasks.Count>0){object next;try{var t=tasks.Peek();if(!t.MoveNext()){tasks.Pop();continue;}next=t.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator child)tasks.Push(child);else yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: "+checks+" checks; remaining opponent increment only: partly known hand modal contains exactly two authorized informational card faces, native confirm index via actual ray, duplicate rejection, knowledge-preserving refund and restored-priority cleanup. Source-absent actor card/cancel/stale/duplicate steps retained from 20261008T0648304925564Z/steps.log; not replayed. Current zh-CN contract increment; prior languages/permissions, number/order/payment controls reused. No new UI first/global/network claim.");}
        finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui){
        var language=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;
        var module=EventSystem.current.currentInputModule;bool moduleEnabled=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var banners=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=banners.Select(b=>b.enabled).ToArray();foreach(var b in banners)b.enabled=false;
        try{
            int rev=GamePreferences.Revision;bool changing=language!="zh-CN";GamePreferences.SetLanguage("zh-CN");float until=Time.realtimeSinceStartup+12;while((!GamePreferences.Ready||changing&&GamePreferences.Revision==rev)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language=="zh-CN","Exact language ready");
            int calls=0,sent=-1;
            var reveal=Read("revealed");Check(reveal.request.seat==2&&reveal.request.previewCards.Length==2&&reveal.players[0].hand.Length==2,"Actual opponent-authorized exact revealed hand");
            calls=0;ui.Present(null,2,false,null,null,"");ui.Present(reveal,2,true,i=>{calls++;sent=i;},null,"");yield return Frame();
            Check(reveal.players[0].handCount==4&&GameObject.Find("CardSelection")!=null,"Partly known hand uses existing authorized subset modal");
            var tiles=GameObject.Find("CardSelection").GetComponentsInChildren<RectTransform>().Where(t=>t.name.StartsWith("ChoiceCard_")).ToArray();Check(tiles.Length==2,"No hidden hand identities added to modal");
            foreach(var c in reveal.request.previewCards){var tile=tiles.Single(t=>t.name=="ChoiceCard_"+c.id);Check(tile.GetComponentsInChildren<Button>().Length==0,"Revealed card is informational, not selectable");Check(tile.Find("Artwork").GetComponent<Image>().sprite!=null,"Actual revealed face loaded");}
            int confirm=Array.FindIndex(reveal.request.options,o=>o.id=="confirm-cards");ScreenCapture.CaptureScreenshot(Output+"/opponent-reveal.png");yield return Frame();Click(Named("SelectionAction_"+confirm));Check(calls==1&&sent==confirm,"Opponent native reveal acknowledgement index");
            Check(!ui.TrySubmitPresentedOption(confirm,reveal.request.requestId)&&calls==1,"Duplicate reveal acknowledgement rejected");
            var after=Read("cancelled-viewer");Check(after.players[0].hand.Select(c=>c.id).OrderBy(i=>i).SequenceEqual(reveal.request.previewCards.Select(c=>c.id).OrderBy(i=>i)),"Already revealed identities preserved after native refund");
            ui.Present(after,2,false,i=>calls++,null,"");yield return Frame();Check(GameObject.Find("CardSelection")==null&&GameObject.Find("SelectionAction_"+confirm)==null,"Native reveal UI clears at restored priority");
            File.AppendAllText(Output+"/steps.log","opponent exact two-card reveal ray and knowledge-preserving refund PASS\n");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=moduleEnabled;GamePreferences.SetLanguage(language);for(int i=0;i<banners.Length;i++)if(banners[i]!=null)banners[i].enabled=flags[i];}
    }
}
#endif
