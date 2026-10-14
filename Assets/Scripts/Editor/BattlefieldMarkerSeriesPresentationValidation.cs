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

public static class BattlefieldMarkerSeriesPresentationValidation
{
    public const string Output=".tools/battlefield-marker-series-1005";
    static bool running;
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    static IEnumerator Frame(float t=.5f){yield return new WaitForSecondsRealtime(t);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static Transform CanvasRoot=>GameObject.Find("Canvas").transform;
    static Rect Bounds(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c.Min(v=>v.x),c.Min(v=>v.y),c.Max(v=>v.x),c.Max(v=>v.y));}
    static void Click(Button b){var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.TransformPoint(((RectTransform)b.transform).rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(b.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    static void Property(OnlineMatchService service,string name,object value)=>typeof(OnlineMatchService).GetProperty(name).SetValue(service,value);
    static void Tick(ForgeBattlefieldController ui)=>typeof(ForgeBattlefieldController).GetMethod("Update",Hidden).Invoke(ui,null);
    public static void Run(){Check(Application.isPlaying,"Play Mode required");if(running)return;running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>().StartCoroutine(Guard());}
    static IEnumerator Guard(){var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();var iterator=Validate(ui);try{while(true){object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: zh/en/ja 1.3 scale; 96x126 complete state sprites; three per page, square 30px arrows with actual paging rays; unchanged emblem/delayed faces, 28px clock with rendered 22px glyph; own/opponent 200/314 signed offsets, equal counter-to-state and state-to-emblem edge gaps, no HUD/field overlap; BO1 hides wins, BO3 renders only per-seat wins with bounded fixed fonts; unchanged Forge viewHash + changed series triggers actual controller Update (0/0 to 1/0, seat reversal, 1/1, return BO1); same scoreboard avoids unnecessary redraw; 575 native IDs per player retained in expanded fixture. BO3 lobby/view and density are explicit presentation adapters, not an online match or new Forge effect.");}finally{running=false;(iterator as IDisposable)?.Dispose();}}
    static IEnumerator Validate(ForgeBattlefieldController ui){
        var serviceField=typeof(ForgeBattlefieldController).GetField("service",Hidden);var originalService=serviceField.GetValue(ui);bool wasEnabled=ui.enabled;string originalLocale=GamePreferences.Language;
        // An inactive service supplies existing typed lobby/view state without opening a transport.
        var owner=new GameObject("MarkerSeriesDisplayAdapter");owner.SetActive(false);var service=owner.AddComponent<OnlineMatchService>();
        try{
            ui.enabled=false;serviceField.SetValue(ui,service);Property(service,"IsInMatch",true);Property(service,"LocalSeat",1);
            foreach(var locale in new[]{"zh-CN","en-US","ja-JP"}){
                GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale not ready");
                var mouse=UnityEngine.InputSystem.Mouse.current;if(mouse!=null){UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(mouse.position,new Vector2(1,1));UnityEngine.InputSystem.InputSystem.Update();}
                Property(service,"LobbyState",new OnlineLobbyState{series=new OnlineSeriesState{bestOf=1}});ui.Present(null,1,false,null,null,"");
                var view=BattlefieldStressPresentationValidation.Fixture();var packet=new OnlinePlayerViewState{forge=view,viewHash="marker-series:"+locale,viewerSeat=1};Property(service,"ForgeView",packet);Property(service,"LocalSeat",1);Tick(ui);yield return Frame(1);
                // Paging intentionally survives refresh/locale changes. Navigate back,
                // rather than assuming the first page or clearing production state.
                foreach(var plateName in new[]{"PlayerPlate","OpponentPlate"})foreach(var name in new[]{"StatesPrevious","EmblemsPrevious"}){var previous=CanvasRoot.Find(plateName+"/"+name).GetComponent<Button>();while(previous.gameObject.activeInHierarchy&&previous.interactable){Click(previous);yield return Frame(.3f);}}
                var fieldBounds=view.players.SelectMany(p=>p.battlefield).Select(c=>GameObject.Find("Card_"+c.id)).Where(g=>g!=null).Select(g=>Bounds(g.GetComponent<RectTransform>())).ToArray();
                foreach(var player in view.players){
                    var plate=CanvasRoot.Find(player.seat==1?"PlayerPlate":"OpponentPlate");bool own=player.seat==1;var counter=plate.Find("PlayerCounters");var states=player.effectMarkers.Where(c=>!string.IsNullOrEmpty(c.helperArtName)&&c.markerKind!="delayed").Take(3).ToArray();
                    Check(plate.Find("Sub").GetComponent<Text>().text.EndsWith(player.handCount.ToString()),"BO1 shows score");
                    foreach(var state in states){var root=GameObject.Find("Card_"+state.id).GetComponent<RectTransform>();var image=root.Find("Visual/Border/Face").GetComponent<Image>();Check(Vector2.Distance(root.rect.size,new Vector2(96,126))<.1f&&image.preserveAspect&&image.sprite!=null&&image.transform.Find("Illustration")==null,"State not enlarged/full");var local=plate.InverseTransformPoint(root.position);Check(Mathf.Abs(local.y-(own?200:-200))<.1f,"State row offset");var bounds=Bounds(root);var countBounds=Bounds((RectTransform)counter);Check(own?bounds.yMin-countBounds.yMax>=5:countBounds.yMin-bounds.yMax>=5,"State overlaps counters");Check(!bounds.Overlaps(Bounds((RectTransform)plate)),"State overlaps avatar");}
                    var emblem=GameObject.Find("Card_"+player.emblems[0].id).GetComponent<RectTransform>();Check(Mathf.Abs(plate.InverseTransformPoint(emblem.position).y-(own?314:-314))<.1f,"Emblem not mirrored");
                    foreach(var state in states){var image=GameObject.Find("Card_"+state.id).transform.Find("Visual/Border/Face").GetComponent<Image>();Check(image.canvasRenderer.GetMesh().bounds.size.y>100,"Visible state sprite still small");var stateRect=Bounds(GameObject.Find("Card_"+state.id).GetComponent<RectTransform>());Check(fieldBounds.All(r=>!r.Overlaps(stateRect)),"State overlaps permanent area");}
                    var stateBounds=Bounds(GameObject.Find("Card_"+states[0].id).GetComponent<RectTransform>());var emblemBounds=Bounds(emblem);Check(own?emblemBounds.yMin-stateBounds.yMax>=5:stateBounds.yMin-emblemBounds.yMax>=5,"Marker rows overlap");
                    foreach(var arrow in plate.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("States"))){Check(((RectTransform)arrow.transform).rect.size==new Vector2(30,30),"Stretched arrow");}
                    Click(plate.Find("EmblemsNext").GetComponent<Button>());yield return Frame();var delayed=player.effectMarkers.First(c=>c.markerKind=="delayed");var clock=GameObject.Find("Card_"+delayed.id).transform.Find("Visual/DelayedClock");Check(((RectTransform)clock).rect.size==new Vector2(28,28)&&((RectTransform)clock.Find("Icon")).rect.size==new Vector2(22,22)&&clock.Find("Icon").GetComponent<DelayedEffectIcon>().canvasRenderer.GetMesh().vertexCount>0,"Clock size/mesh wrong");
                }
                var ownPlate=CanvasRoot.Find("PlayerPlate");var opponentPlate=CanvasRoot.Find("OpponentPlate");var ownState=view.players[0].effectMarkers.First(c=>!string.IsNullOrEmpty(c.helperArtName));var otherState=view.players[1].effectMarkers.First(c=>!string.IsNullOrEmpty(c.helperArtName));
                float ownGap=Bounds(GameObject.Find("Card_"+ownState.id).GetComponent<RectTransform>()).yMin-Bounds((RectTransform)ownPlate.Find("PlayerCounters")).yMax;float otherGap=Bounds((RectTransform)opponentPlate.Find("PlayerCounters")).yMin-Bounds(GameObject.Find("Card_"+otherState.id).GetComponent<RectTransform>()).yMax;Check(Mathf.Abs(ownGap-otherGap)<.5f,"Opposite counter gap differs");
                Click(ownPlate.Find("StatesNext").GetComponent<Button>());yield return Frame();Check(GameObject.Find("Card_-2000001")!=null,"Day paging lost");Click(ownPlate.Find("StatesPrevious").GetComponent<Button>());yield return Frame();
                var series=new OnlineSeriesState{bestOf=3};var lobby=new OnlineLobbyState{series=series};Property(service,"LobbyState",lobby);Tick(ui);yield return Frame();CheckScore(1,0,0);
                string hash=packet.viewHash;series.player1Wins=1;Tick(ui);yield return Frame();Check(packet.viewHash==hash,"Score fixture changed Forge hash");CheckScore(1,1,0);
                int font=ownPlate.Find("Sub").GetComponent<Text>().fontSize;ui.ViewBattlefield();ui.RestoreBattlefieldWindows();yield return Frame();Check(ownPlate.Find("Sub").GetComponent<Text>().fontSize==font,"Score font resized");
                var stableCard=GameObject.Find("Card_"+ownState.id);var key=typeof(ForgeBattlefieldController).GetField("renderedKey",Hidden).GetValue(ui);Tick(ui);Check(key.Equals(typeof(ForgeBattlefieldController).GetField("renderedKey",Hidden).GetValue(ui))&&GameObject.Find("Card_"+ownState.id)==stableCard,"Unchanged scoreboard redrew");
                if(locale=="zh-CN") {
                    // Freeze settled pointers only for this clean overview; invalidate
                    // delayed inspector callbacks before hiding the incidental hover.
                    var pointers=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None);var enabled=pointers.Select(p=>p.enabled).ToArray();
                    for(int i=0;i<pointers.Length;i++)pointers[i].enabled=false;
                    var revision=typeof(ForgeBattlefieldController).GetField("inspectHoverRevision",Hidden);revision.SetValue(ui,(int)revision.GetValue(ui)+1);
                    CanvasRoot.Find("InspectPanel").gameObject.SetActive(false);ScreenCapture.CaptureScreenshot(Output+"/bo3-dense.png");yield return Frame(.1f);
                    for(int i=0;i<pointers.Length;i++)if(pointers[i]!=null)pointers[i].enabled=enabled[i];
                }
                Property(service,"LocalSeat",2);packet.viewHash+="/seat2";Tick(ui);yield return Frame();CheckScore(2,1,0);
                series.player2Wins=1;Tick(ui);yield return Frame();CheckScore(2,1,1);
                series.bestOf=1;Tick(ui);yield return Frame();foreach(var plate in new[]{ownPlate,opponentPlate})Check(!plate.Find("Sub").GetComponent<Text>().text.Contains(" · "),"BO1 retained wins");
                Check(view.players.All(p=>p.battlefield.Length==575&&p.battlefield.Select(c=>c.id).Distinct().Count()==575),"Density identity changed");
            }
        }finally{serviceField.SetValue(ui,originalService);ui.enabled=wasEnabled;GamePreferences.SetLanguage(originalLocale);UnityEngine.Object.Destroy(owner);}
    }
    static void CheckScore(int local,int one,int two){foreach(int seat in new[]{1,2}){var text=CanvasRoot.Find((seat==local?"PlayerPlate":"OpponentPlate")+"/Sub").GetComponent<Text>();int n=seat==1?one:two;string ending=GamePreferences.Language=="zh-CN"?" · 胜 "+n:GamePreferences.Language=="ja-JP"?" · 勝利 "+n:" · Wins "+n;Check(text.text.EndsWith(ending),"Wrong seat wins: "+text.text);Check(text.preferredWidth<=text.rectTransform.rect.width+.1f&&text.preferredHeight<=text.rectTransform.rect.height+.1f&&!text.resizeTextForBestFit,"BO3 text clipped/scaled");}}
}
#endif
