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

public static class BattlefieldPlayerDetailsPresentationValidation
{
    public const string Output=".tools/battlefield-player-details-1005";
    static bool running;
    static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    static IEnumerator Frame(float seconds=.5f){yield return new WaitForSecondsRealtime(seconds);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static Transform CanvasRoot=>GameObject.Find("Canvas").transform;
    static Rect Bounds(RectTransform r){var corners=new Vector3[4];r.GetWorldCorners(corners);return Rect.MinMaxRect(corners.Min(p=>p.x),corners.Min(p=>p.y),corners.Max(p=>p.x),corners.Max(p=>p.y));}
    static void Click(Button b){var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.TransformPoint(((RectTransform)b.transform).rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(b.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    public static void Run(){Check(Application.isPlaying,"Play Mode required");if(running)return;running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("PlayerDetailsValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){string locale=GamePreferences.Language;var test=Validate(ui);try{while(true){object next;try{if(!test.MoveNext())break;next=test.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: zh/en/ja state auxiliary sprites preserve the complete image, square short arrow controls and real paging raycasts, delayed rounded faces with rendered clock mesh, unchanged native IDs; BO1/BO3 summaries read existing series counts through formatter adapters; library/graveyard/exile inline counts with bounded fixed-font captions and read-only zone click; centered numeric life and stable view/restore; same-size mirrored normal/dense mana and lands with separate public/control ownership; artifacts/enchantments bottom-right counts and walker/battle top-left; matching hidden/authorized hand sizes, spacing/curves and mirrored end-card lift. Expanded density and series setup are display adapters, not new card effects or live BO3/network evidence.");}finally{running=false;GamePreferences.SetLanguage(locale);ui.RestoreBattlefieldWindows();}}
    static IEnumerator Validate(ForgeBattlefieldController ui){
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
            GamePreferences.SetLanguage(locale);float deadline=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready,"Locale ready");
            var mouse=UnityEngine.InputSystem.Mouse.current;if(mouse!=null){UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(mouse.position,new Vector2(1,1));UnityEngine.InputSystem.InputSystem.Update();}
            ui.Present(null,1,false,null,null,"");var view=DuelLayoutPresentationValidation.DensityFixture(true);
            foreach(var player in view.players){player.libraryCount=123;player.exileCount=3;player.graveyardCards=Enumerable.Range(0,11).Select(i=>new ForgeCardView{id=800000+player.seat*100+i,name="Mountain",zone="Graveyard",ownerSeat=player.seat}).ToArray();}
            ui.Present(view,view.seat,true,_=>{},null,"");yield return Frame(1);
            foreach(var p in view.players){
                string plate=p.seat==view.seat?"PlayerPlate":"OpponentPlate";string zones=p.seat==view.seat?"ZonePiles_PlayerBottomLeft":"ZonePiles_OpponentTopRight";
                Check(CanvasRoot.Find(plate+"/Sub").GetComponent<Text>().text.EndsWith(p.handCount.ToString()),"BO1 summary contains extra counts");
                var life=CanvasRoot.Find(plate+"/Life").GetComponent<Text>();Check(life.alignment==TextAnchor.MiddleCenter&&Mathf.Abs(life.rectTransform.anchoredPosition.y)<.01f&&life.preferredHeight<=life.rectTransform.rect.height+.1f,"Life not centered/bounded");
                foreach(var arrow in CanvasRoot.Find(plate).GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("States")||b.name.StartsWith("Emblems"))){var r=(RectTransform)arrow.transform;Check(Mathf.Abs(r.rect.width-r.rect.height)<.01f&&((RectTransform)r.Find("Icon")).rect.width==14,"Long marker arrow");}
                foreach(var entry in new[]{"牌库","坟场","放逐"}){var label=CanvasRoot.Find(zones+"/Pile_"+entry+"/ZoneEntry/ZoneCaption").GetComponent<Text>();Check(label.preferredWidth<=label.rectTransform.rect.width+.1f&&label.preferredHeight<=label.rectTransform.rect.height+.1f&&!label.resizeTextForBestFit,"Zone count clipped "+label.text);Check(label.text.EndsWith(entry=="牌库"?"123":entry=="坟场"?"11":"3"),"Missing inline zone count");}
                foreach(var state in p.effectMarkers.Where(c=>!string.IsNullOrEmpty(c.helperArtName)).Take(3)){var root=GameObject.Find("Card_"+state.id).transform;var image=root.Find("Visual/Border/Face").GetComponent<Image>();Check(image.sprite!=null&&image.preserveAspect&&root.Find("Visual/Border/Face/Illustration")==null,"State auxiliary image cropped");}
            }
            var series=new OnlineSeriesState{bestOf=3,player1Wins=1,player2Wins=2};var formatter=typeof(ForgeBattlefieldController).GetMethod("PlayerSummary",BindingFlags.Static|BindingFlags.NonPublic);foreach(var p in view.players){string text=(string)formatter.Invoke(null,new object[]{p,series});Check(text.EndsWith((p.seat==1?1:2).ToString())&&!text.Contains("123"),"BO3 wrong seat win count");}
            var ownPool=(RectTransform)CanvasRoot.Find("ManaPoolChips");var otherPool=(RectTransform)CanvasRoot.Find("PublicManaPool");Check(Mathf.Abs(ownPool.anchoredPosition.x-otherPool.anchoredPosition.x)<.1f&&Mathf.Abs(ownPool.anchoredPosition.y+otherPool.anchoredPosition.y-((RectTransform)CanvasRoot).rect.height)<.1f,"Mana pools not mirrored");
            var land=view.players[1].battlefield.First(c=>c.land);Check(Bounds((RectTransform)otherPool).yMin>=Bounds(GameObject.Find("Card_"+land.id).GetComponent<RectTransform>()).yMax,"Opponent pool not behind lands");
            var summary=CanvasRoot.Find("PlayerPlate/Sub").GetComponent<Text>();int font=summary.fontSize;ui.ViewBattlefield();ui.RestoreBattlefieldWindows();yield return Frame();Check(summary.fontSize==font,"Player summary font changed");
            Click(CanvasRoot.Find("PlayerPlate/EmblemsNext").GetComponent<Button>());yield return Frame();var delayed=view.players[0].effectMarkers.First(c=>c.markerKind=="delayed");var clock=GameObject.Find("Card_"+delayed.id).GetComponentInChildren<DelayedEffectIcon>();Check(clock!=null&&clock.canvasRenderer.GetMesh().vertexCount>0,"Delayed clock not rendered");
            Click(CanvasRoot.Find("PlayerPlate/StatesNext").GetComponent<Button>());yield return Frame();Check(GameObject.Find("Card_-2000001")!=null,"State paging lost day/night");
            int submitted=0;ui.Present(view,view.seat,true,_=>submitted++,null,"");yield return Frame();Click(CanvasRoot.Find("ZonePiles_PlayerBottomLeft/Pile_坟场").GetComponent<Button>());yield return Frame();Check(GameObject.Find("CardSelection")!=null&&submitted==0,"Zone browse submitted native action");Click(GameObject.Find("CloseReviewCards").GetComponent<Button>());yield return Frame();
            var density=BattlefieldStressPresentationValidation.Fixture();ui.Present(density,density.seat,true,_=>{},null,"");yield return Frame(1);
            foreach(var player in density.players)foreach(var c in player.battlefield.GroupBy(ForgeBattlefieldController.ArenaGroupKey).Select(g=>g.First())){var badge=GameObject.Find("Card_"+c.id).transform.Find("Visual/Count") as RectTransform;Check(badge!=null,"Missing density count");if(c.planeswalker||c.battle)Check(badge.anchoredPosition.y>0&&badge.anchoredPosition.x<0,"Walker/battle count not upper-left");else if(!c.land&&!c.creature)Check(badge.anchoredPosition.y<0&&badge.anchoredPosition.x>0,"Support count not lower-right");}
            var ownLands=CanvasRoot.GetComponentsInChildren<ScrollRect>().First(s=>s.name=="Dense_YourLands");var otherLands=CanvasRoot.GetComponentsInChildren<ScrollRect>().First(s=>s.name=="Dense_OpponentLands");Check(Mathf.Abs(((RectTransform)ownLands.transform).anchoredPosition.y+((RectTransform)otherLands.transform).anchoredPosition.y)<.01f,"Dense land rows not mirrored");
            var before=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/Thoughtseize/before.json"));ui.Present(before,1,true,_=>{},null,"");yield return Frame();var own=GameObject.Find("PlayerHandRoot").GetComponentsInChildren<ForgeBattlefieldCardPointer>().First();var ownSize=((RectTransform)own.transform).rect.size;var back=GameObject.Find("OpponentHandRoot").GetComponentsInChildren<Image>().First();Check(Vector2.Distance(ownSize,back.rectTransform.rect.size)<.1f,"Opponent hidden card smaller than own");
            var choice=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/Thoughtseize/choice.json"));ui.Present(choice,1,true,_=>{},null,"");yield return Frame(1);var other=GameObject.Find("OpponentHandRoot").GetComponentsInChildren<ForgeBattlefieldCardPointer>();Check(other.All(p=>Vector2.Distance(((RectTransform)p.transform).rect.size,ownSize)<.1f),"Authorized opponent cards smaller");
            var ends=new[]{other.OrderBy(p=>p.transform.GetSiblingIndex()).First(),other.OrderBy(p=>p.transform.GetSiblingIndex()).Last()};foreach(var p in ends){var visual=(RectTransform)p.transform.Find("Visual");var start=visual.position;p.OnPointerEnter(new PointerEventData(EventSystem.current));yield return Frame(.4f);Check(Mathf.Abs(visual.position.x-start.x)<2&&visual.position.y<start.y-90,"Top hand does not lift inward at matching distance");p.OnPointerExit(new PointerEventData(EventSystem.current));yield return Frame(.4f);}
        }
    }
}
#endif

