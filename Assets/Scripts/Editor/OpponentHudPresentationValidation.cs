#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Focused display checks; the density fixture is not a card-effect assertion.
public static class OpponentHudPresentationValidation
{
    public const string Output=".tools/opponent-hud-1005";
    static bool running;
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static Transform CanvasRoot=>GameObject.Find("Canvas").transform;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static IEnumerator Frame(){Canvas.ForceUpdateCanvases();yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();}
    static Rect Bounds(RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);return Rect.MinMaxRect(corners.Min(c=>c.x),corners.Min(c=>c.y),corners.Max(c=>c.x),corners.Max(c=>c.y));}
    static Rect Painted(Graphic graphic){var mesh=graphic.canvasRenderer.GetMesh();Check(mesh!=null&&mesh.vertexCount>0,"Missing mesh: "+graphic.name);var points=mesh.vertices.Select(graphic.transform.TransformPoint).ToArray();return Rect.MinMaxRect(points.Min(v=>v.x),points.Min(v=>v.y),points.Max(v=>v.x),points.Max(v=>v.y));}
    static void Click(Button button){var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Blocked ray: "+button.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);}
    static void FreezeHover(ForgeBattlefieldController ui){foreach(var pointer in CanvasRoot.GetComponentsInChildren<ForgeBattlefieldCardPointer>(true)){pointer.ResetHover();pointer.enabled=false;}var revision=typeof(ForgeBattlefieldController).GetField("inspectHoverRevision",Hidden);revision.SetValue(ui,(int)revision.GetValue(ui)+1);CanvasRoot.Find("InspectPanel").gameObject.SetActive(false);}
    static void Position(string path,Vector2 expected){Check(Vector2.Distance(((RectTransform)CanvasRoot.Find(path)).anchoredPosition,expected)<.1f,"Position: "+path);}
    static void CounterMeshes(Transform plate,int expected){var row=plate.Find("PlayerCounters");var chips=row.Cast<Transform>().Where(t=>t.name.StartsWith("Counter_")&&t.gameObject.activeSelf).ToArray();Check(chips.Length==expected,"Counter page size");foreach(var chip in chips){var symbol=chip.Find("Symbol").GetComponent<Graphic>();var count=chip.Find("Count").GetComponent<Text>();var a=Painted(symbol);var b=Painted(count);Check(a.xMax<=b.xMin+.1f,"Symbol/numeral overlap: "+chip.name);Check(!symbol.canvasRenderer.cull&&!count.canvasRenderer.cull,"Counter culled");Check(a.xMin>=0&&a.xMax<=Screen.width&&b.yMin>=0&&b.yMax<=Screen.height,"Counter outside screen");}}
    public static void Run(){Check(Application.isPlaying,"Play mode required");if(running)return;running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("OpponentHudValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){string locale=GamePreferences.Language;var test=Validate(ui);try{while(true){object next;try{if(!test.MoveNext())break;next=test.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: actual play mode, zh/en/ja at 1.3 scale; opponent plate/zone/shell all moved up 32 canvas units with gear clearance; both sides five mapped counter types on one page, actual rendered symbol/numeral meshes do not overlap and are not culled; oversized counts page by real pointer rays; states/emblems keep relative offsets and delayed clock visible on next page; repeated snapshots and view/return keep positions; native opponent-player choice submits exact index; 575 unique IDs per side unchanged. Display adapters and archived input replay only, not new card effects or online timing.\n");}finally{running=false;GamePreferences.SetLanguage(locale);foreach(var p in CanvasRoot.GetComponentsInChildren<ForgeBattlefieldCardPointer>(true))p.enabled=true;}}
    static IEnumerator Validate(ForgeBattlefieldController ui){
        GamePreferences.SetScale(1.3f);var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();if(banner!=null)banner.enabled=false;
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
            GamePreferences.SetLanguage(locale);float deadline=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language==locale,"Locale loading");
            var v=BattlefieldStressPresentationValidation.Fixture();ui.Present(null,1,false,null,null,"");ui.Present(v,1,true,_=>{},null,"");yield return Frame();FreezeHover(ui);yield return Frame();
            Position("OpponentPlate",new Vector2(205,-120));Position("ZonePiles_OpponentTopRight",new Vector2(205,-192));Position("OpponentPlateWithZones",new Vector2(205,-144));Position("PlayerPlate",new Vector2(205,72));
            Check(!Bounds((RectTransform)CanvasRoot.Find("OpponentPlateWithZones")).Overlaps(Bounds((RectTransform)CanvasRoot.Find("GearBox"))),"Opponent HUD covers settings");
            foreach(var player in v.players){var plate=CanvasRoot.Find(player.seat==1?"PlayerPlate":"OpponentPlate");CounterMeshes(plate,5);Check(plate.Find("PlayerCounters/Next")==null||!plate.Find("PlayerCounters/Next").gameObject.activeSelf,"Five counters unexpectedly paged");var state=player.effectMarkers.First(c=>!string.IsNullOrEmpty(c.helperArtName));var stateRoot=GameObject.Find("Card_"+state.id).transform;Check(Mathf.Abs(plate.InverseTransformPoint(stateRoot.position).y-(player.seat==1?200:-200))<.1f,"State detached from plate");var emblem=GameObject.Find("Card_"+player.emblems[0].id).transform;Check(Mathf.Abs(plate.InverseTransformPoint(emblem.position).y-(player.seat==1?314:-314))<.1f,"Emblem detached from plate");Check(plate.Find("Name").GetComponentsInChildren<Text>(true).Count(t=>t.name=="ScrollingName")==1,"Duplicated name");}
            ui.Present(v,1,true,_=>{},null,"");ui.ViewBattlefield();ui.RestoreBattlefieldWindows();yield return Frame();Position("OpponentPlate",new Vector2(205,-120));
            ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-dense.png");yield return Frame();
            var opponent=CanvasRoot.Find("OpponentPlate");Click(opponent.Find("EmblemsNext").GetComponent<Button>());yield return Frame();FreezeHover(ui);var delayed=v.players[1].effectMarkers.First(c=>c.markerKind=="delayed");var delayedRoot=GameObject.Find("Card_"+delayed.id).transform;Check(Mathf.Abs(opponent.InverseTransformPoint(delayedRoot.position).y+314)<.1f&&delayedRoot.Find("Visual/DelayedClock").gameObject.activeInHierarchy,"Delayed position/clock");Click(opponent.Find("EmblemsPrevious").GetComponent<Button>());yield return Frame();
            foreach(var player in v.players)foreach(var counter in player.counters)counter.count=999;ui.Present(v,1,true,_=>{},null,"");yield return Frame();FreezeHover(ui);
            foreach(var plateName in new[]{"PlayerPlate","OpponentPlate"}){var plate=CanvasRoot.Find(plateName);var row=plate.Find("PlayerCounters");Check(row.Find("Next").gameObject.activeSelf,"Large counts need paging");CounterMeshes(plate,4);Click(row.Find("Next").GetComponent<Button>());yield return Frame();CounterMeshes(plate,1);Click(plate.Find("PlayerCounters/Previous").GetComponent<Button>());yield return Frame();}
            var target=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/LibraryCosts/opponent-players-1.json"));int picked=-1;ui.Present(target,target.seat,true,i=>picked=i,null,"");yield return Frame();Click(CanvasRoot.Find("OpponentPlate").GetComponent<Button>());var other=target.players.First(p=>p.seat!=target.seat);Check(picked==Array.FindIndex(target.request.options,o=>o.id=="player:"+other.targetId),"Wrong native player index");
            Check(v.players.All(p=>p.battlefield.Length==575&&p.battlefield.Select(c=>c.id).Distinct().Count()==575),"Density IDs lost");
        }
    }
}
#endif
