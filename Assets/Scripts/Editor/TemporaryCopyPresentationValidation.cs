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

// Replays real bridge snapshots. Additional marker variants test layout only.
public static class TemporaryCopyPresentationValidation
{
    public const string Output=".tools/temporary-copy-1006/ui";
    static readonly BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    [Serializable] sealed class NativeResult { public string folder,status; }
    static ForgeSeatView Load(string scenario,string name) {
        var result=JsonUtility.FromJson<NativeResult>(File.ReadAllText(".tools/temporary-copy-1006/native/"+scenario+"-result.json"));
        Check(result.status=="PASS","Native snapshot missing");
        return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Path.Combine(result.folder,name+".json")));
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static IEnumerator Frame(float seconds=.35f){yield return new WaitForSecondsRealtime(seconds);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static GameObject Card(int id)=>GameObject.Find("Card_"+id);
    static void Click(Button button) {
        var p=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Ray blocked: "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    static void Inspect(ForgeBattlefieldController ui,ForgeCardView card)=>typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{card});
    static void CleanPointers(ForgeBattlefieldController ui) {
        foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}
        var revision=typeof(ForgeBattlefieldController).GetField("inspectHoverRevision",Hidden);revision.SetValue(ui,(int)revision.GetValue(ui)+1);
        Root.Find("InspectPanel").gameObject.SetActive(false);
    }
    public static void Run() {
        Check(Application.isPlaying,"Play Mode required");Directory.CreateDirectory(Output);
        File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui) {
        var iterator=Validate(ui);try {
            while(true){object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: zh/en/ja, both seats; real native snapshots distinguish timed vs permanent copies; silver mesh animates and never blocks rays; delayed markers fold with x2 and expand to distinct native IDs by ray; original-state excludes scheduled instruction; expiry ordering/stack retains frame; sacrifice/exile remove copies; countered sacrifice removes frame/text from surviving copy. Identical emblems fold/expand; different delayed conditions split and last page is ray-accessible. Emblem/condition/paging adapters are presentation checks, not effect proof.");
        } finally {(iterator as IDisposable)?.Dispose();}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui) {
        var originalLocale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;
        try {
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}) {
                GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+20;
                while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;
                Check(GamePreferences.Ready,"Language not ready");
                foreach(int seat in new[]{1,2}) {
                    ui.Present(null,seat,false,null,null,"");var v=Load("reflection-twice","copies");ui.Present(v,seat,false,null,null,"");yield return Frame(1);CleanPointers(ui);
                    var timed=v.players[0].battlefield.Where(c=>c.token&&c.pendingDepartures?.Length>0).ToArray();
                    var permanent=v.players[0].battlefield.Single(c=>c.token&&c.pendingDepartures?.Length==0);
                    var glow=UnityEngine.Object.FindObjectsByType<TemporaryCopyGlow>(FindObjectsSortMode.None).Single();
                    Check(glow.canvasRenderer.GetMesh().vertexCount==480&&!glow.raycastTarget,"Silver edge missing or interactive");
                    var before=glow.canvasRenderer.GetMesh().colors32;yield return Frame(.22f);
                    Check(!before.SequenceEqual(glow.canvasRenderer.GetMesh().colors32),"Edge does not flow");
                    Check(Card(permanent.id).GetComponentInChildren<TemporaryCopyGlow>()==null,"Permanent copy has temporary frame");
                    var markers=v.players[0].effectMarkers.Where(c=>c.markerKind=="delayed").ToArray();
                    var representative=markers.Select(c=>Card(c.id)).Single(g=>g!=null);
                    Check(representative.GetComponentsInChildren<Text>().Any(t=>t.text=="×2"),"Identical delays not folded");
                    Click(representative.GetComponent<Button>());yield return Frame();CleanPointers(ui);
                    Check(markers.All(c=>Card(c.id)!=null),"Expanded delays lost native identity");
                    typeof(ForgeBattlefieldController).GetMethod("CollapseExpandedGroups",Hidden).Invoke(ui,null);yield return Frame();CleanPointers(ui);
                    Inspect(ui,timed[0]);yield return Frame();
                    var rules=Root.Find("InspectPanel/Rules").GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    var text=ForgeBattlefieldController.PendingDepartureText(timed[0].pendingDepartures[0]);
                    Check(rules.richText&&rules.text.Contains("<color=#70BFFF>"+text+"</color>"),"Departure is not blue");
                    Check(!rules.text.StartsWith("\n"),"Empty native rules added blank lines");
                    Check(locale=="en-US"||!text.Contains("sacrifice"),"Departure translation missing");
                    if(locale=="zh-CN"&&seat==1){ScreenCapture.CaptureScreenshot(Output+"/copy-blue-text.png");yield return Frame();}
                    Click(Root.Find("InspectPanel/OriginalState").GetComponent<Button>());yield return Frame();
                    Check(!rules.text.Contains(text),"Scheduled action became original ability");
                    CleanPointers(ui);
                    if(locale=="zh-CN"&&seat==1){ScreenCapture.CaptureScreenshot(Output+"/copy-and-folded-delays.png");yield return Frame();}
                    ui.Present(Load("reflection-twice","expiry-stack"),seat,false,null,null,"");yield return Frame();CleanPointers(ui);
                    Check(UnityEngine.Object.FindObjectsByType<TemporaryCopyGlow>(FindObjectsSortMode.None).Length>0,"Stack expiry lost border");
                    ui.Present(Load("reflection-twice","after-expiry"),seat,false,null,null,"");yield return Frame();CleanPointers(ui);
                    Check(UnityEngine.Object.FindObjectsByType<TemporaryCopyGlow>(FindObjectsSortMode.None).Length==0&&Card(permanent.id)!=null,"Resolved expiry kept frame");
                    var counter=Load("counter-trigger","after-expiry");ui.Present(counter,seat,false,null,null,"");yield return Frame();CleanPointers(ui);
                    var survivor=counter.players[0].battlefield.Single(c=>c.token);Check(Card(survivor.id)!=null&&Card(survivor.id).GetComponentInChildren<TemporaryCopyGlow>()==null,"Countered expiry kept silver frame");
                    Inspect(ui,survivor);yield return Frame();Check(!Root.Find("InspectPanel/Rules").GetComponentInChildren<TMPro.TextMeshProUGUI>().text.Contains(TapExpiryText()),"Countered expiry kept blue instruction");CleanPointers(ui);
                    var adapter=Load("reflection-twice","copies");var player=adapter.players[0];
                    var different=player.effectMarkers.Where(c=>c.markerKind=="delayed").ToArray();different[1].markerGroupKey+="/different-condition";
                    ui.Present(adapter,seat,false,null,null,"");yield return Frame();CleanPointers(ui);
                    Check(different.All(c=>Card(c.id)!=null),"Different delays incorrectly folded");
                    var sample=different[0];player.effectMarkers=Array.Empty<ForgeCardView>();
                    player.emblems=Enumerable.Range(0,5).Select(i=>{var c=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(sample));c.id=-1900100-i;c.emblem=true;c.effectMarker=false;c.markerKind="";c.name="Emblem";c.currentRules=i<2?"Same emblem":"Different emblem "+i;return c;}).ToArray();
                    ui.Present(adapter,seat,false,null,null,"");yield return Frame();CleanPointers(ui);
                    var first=Card(player.emblems[0].id);Check(first!=null&&Card(player.emblems[1].id)==null&&first.GetComponentsInChildren<Text>().Any(t=>t.text=="×2"),"Same emblems not folded");
                    var plate=Root.Find(seat==1?"PlayerPlate":"OpponentPlate");var next=plate.Find("EmblemsNext").GetComponent<Button>();Check(next.gameObject.activeInHierarchy&&next.interactable,"Grouped marker paging missing");
                    Click(first.GetComponent<Button>());yield return Frame();CleanPointers(ui);Check(Card(player.emblems[0].id)!=null&&Card(player.emblems[1].id)!=null,"Emblem expansion lost identity");
                    next=plate.Find("EmblemsNext").GetComponent<Button>();Click(next);yield return Frame();CleanPointers(ui);Check(Card(player.emblems[4].id)!=null,"Last emblem page inaccessible");
                    typeof(ForgeBattlefieldController).GetMethod("CollapseExpandedGroups",Hidden).Invoke(ui,null);ui.Present(null,seat,false,null,null,"");
                    ui.Present(Load("exile","copies"),seat,false,null,null,"");yield return Frame();CleanPointers(ui);
                    Check(UnityEngine.Object.FindObjectsByType<TemporaryCopyGlow>(FindObjectsSortMode.None).Length==1,"Self-exile trigger lacks frame");
                    ui.Present(Load("exile","after-expiry"),seat,false,null,null,"");yield return Frame();CleanPointers(ui);Check(UnityEngine.Object.FindObjectsByType<TemporaryCopyGlow>(FindObjectsSortMode.None).Length==0,"Exiled token frame persisted");
                }
            }
        } finally {ui.enabled=enabled;GamePreferences.SetLanguage(originalLocale);}
    }
    static string TapExpiryText()=>ForgeBattlefieldController.PendingDepartureText(new ForgePendingDeparture{action="sacrifice",phase="End of turn"});
}
#endif
