#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Native archived choices, with separately identified dense display adapters.
public static class BattlefieldLayerPresentationValidation
{
    public const string Output=".tools/battlefield-layers-1005";
    static bool running;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/"+name+".json"));
    static IEnumerator Frame(float delay=.4f){yield return new WaitForSecondsRealtime(delay);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Click(Button b){
        Check(b!=null&&b.interactable,"Disabled "+b?.name);
        var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.TransformPoint(((RectTransform)b.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    public static void Run(){
        Check(Application.isPlaying,"Play Mode required");if(running)return;running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("LayerValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui){
        var language=GamePreferences.Language;var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();if(banner!=null)banner.enabled=false;
        var test=Validate(ui);
        try{while(true){object next;try{if(!test.MoveNext())break;next=test.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: zh/en/ja fixed stack dock across priority, casting, crowded targets, collapse and restore; only the front card has an aligned animated glow; five player counters fit on one row, actual width overflow pages and restores; both mana pools remain below new/cached selection windows and are restored with stable typography; native selection raycasts/IDs; dense support row gaps use actual card heights. This is Unity presentation evidence, not new effect validation.");}
        finally{running=false;ui.RestoreBattlefieldWindows();GamePreferences.SetLanguage(language);}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui){
        foreach(var locale in new[]{"zh-CN","en-US","ja-JP"}){
            GamePreferences.SetLanguage(locale);var until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale ready");
            ui.Present(null,1,false,null,null,"");var dense=BattlefieldStressPresentationValidation.Fixture();ui.Present(dense,1,true,_=>{},null,"");yield return Frame(1);
            var canvas=GameObject.Find("Canvas").transform;var column=(RectTransform)canvas.Find("StackColumn");var dock=column.anchoredPosition;
            Check(Vector2.Distance(dock,new Vector2(-28,-300))<.1f,"Wrong stack dock");
            foreach(var name in new[]{"PlayerPlate","OpponentPlate"}){
                var row=canvas.Find(name+"/PlayerCounters");Check(row.Find("Next")==null&&row.Cast<Transform>().Count(t=>t.gameObject.activeSelf)==5,"Five counters paginated");
                var right=row.Cast<RectTransform>().Where(t=>t.gameObject.activeSelf).Max(t=>t.anchoredPosition.x+t.rect.width/2);Check(right<=140,"Counters exceed lane");
            }
            var overflow=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(dense));foreach(var p in overflow.players)foreach(var c in p.counters)c.count=999999;
            ui.Present(overflow,1,true,_=>{},null,"");yield return Frame();var own=canvas.Find("PlayerPlate/PlayerCounters");Check(own.Find("Next")!=null,"True width overflow lacks arrow");
            for(int step=0;step<5&&canvas.Find("PlayerPlate/PlayerCounters/Next").GetComponent<Button>().interactable;step++){Click(canvas.Find("PlayerPlate/PlayerCounters/Next").GetComponent<Button>());yield return Frame();}
            Check(canvas.Find("PlayerPlate/PlayerCounters/Counter_TICKET").gameObject.activeSelf,"Last counter unreachable");
            ui.Present(dense,1,true,_=>{},null,"");yield return Frame();Check(canvas.Find("PlayerPlate/PlayerCounters/Next")==null,"Stale counter page");
            foreach(var scroll in canvas.GetComponentsInChildren<ScrollRect>().Where(s=>s.name.Contains("Dense_")&&s.name.Contains("Support"))){
                var rows=scroll.content.Cast<RectTransform>().Where(t=>t.name.StartsWith("Card_")).GroupBy(t=>Mathf.Round((-t.anchoredPosition.y-t.rect.height*.5f)*10)/10).OrderBy(g=>g.Key).ToArray();
                for(int i=1;i<rows.Length;i++){float gap=rows[i].Key-rows[i-1].Key-rows[i-1].Max(t=>t.rect.height);Check(Mathf.Abs(gap-12)<.2f,"Support gap "+gap);}
            }
            var target=Read("StackInteractions/triggers-target-2");var extra=JsonUtility.FromJson<ForgeStackItem>(JsonUtility.ToJson(target.stackItems[0]));extra.id=999999;target.stackItems=target.stackItems.Concat(new[]{extra}).ToArray();target.stack=target.stackItems.Select(e=>e.description).ToArray();target.stackCount=3;
            ui.Present(target,target.seat,true,_=>{},null,"");yield return Frame();Check(Vector2.Distance(column.anchoredPosition,dock)<.1f,"Target moved stack");
            Click(canvas.Find("StackColumn/ViewStack").GetComponent<Button>());yield return Frame();Click(canvas.Find("StackList/Close").GetComponent<Button>());yield return Frame();
            var tile=(RectTransform)GameObject.Find("StackCard_0").transform;var glow=tile.Find("ActiveStackGlow").GetComponent<ForgeStackGlow>();
            Check(glow.enabled&&glow.transform.GetSiblingIndex()==tile.childCount-1,"Front glow hidden behind face");
            Check(Vector3.Distance(tile.rect.center,tile.InverseTransformPoint(glow.rectTransform.TransformPoint(glow.rectTransform.rect.center)))<.1f&&glow.rectTransform.rect.size==tile.rect.size,"Glow rectangle displaced");
            Check(glow.canvasRenderer.GetMesh().vertexCount>0&&tile.parent.GetSiblingIndex()==tile.parent.parent.childCount-1,"Top glow drawn behind rear card");
            Check(UnityEngine.Object.FindObjectsByType<ForgeStackGlow>(FindObjectsSortMode.None).Count(g=>g.enabled&&g.gameObject.activeInHierarchy)==1,"Rear card glow");
            var preview=(RectTransform)canvas.Find("TargetOrigin");Check(preview.TransformPoint(new Vector3(preview.rect.xMax,0)).x<tile.TransformPoint(new Vector3(tile.rect.xMin,0)).x,"Casting preview covers stack");
            ui.ViewBattlefield();ui.RestoreBattlefieldWindows();yield return Frame();Check(Vector2.Distance(column.anchoredPosition,dock)<.1f,"Restore moved stack");
            var fetch=Read("CardSelection/fetch-foothills-choice");foreach(var p in fetch.players)p.manaPool=new[]{12,8,5,23,6,10};int sent=-1;
            for(int i=0;i<3;i++){
                ui.Present(fetch,fetch.seat,true,x=>sent=x,null,"");yield return Frame();var overlay=canvas.Find("CardSelectionOverlay");
                foreach(var poolName in new[]{"ManaPoolChips","PublicManaPool"})Check(canvas.Find(poolName).GetSiblingIndex()<overlay.GetSiblingIndex(),"Pool above cached picker "+i);
                var count=canvas.Find("ManaPoolChips/Pool_W/Count").GetComponent<Text>();int font=count.fontSize;
                ui.ViewBattlefield();yield return Frame(.1f);ui.RestoreBattlefieldWindows();yield return Frame(.1f);Check(count.fontSize==font,"Pool restore typography");
            }
            var legal=canvas.Find("CardSelectionOverlay").GetComponentsInChildren<Button>().First(b=>b.interactable&&b.name=="Artwork");Click(legal);Check(sent>=0&&fetch.request.options[sent].id.StartsWith("card:"),"Native card selection identity");
            Check(Vector2.Distance(column.anchoredPosition,dock)<.1f,"Picker moved stack");
        }
    }
}
#endif
