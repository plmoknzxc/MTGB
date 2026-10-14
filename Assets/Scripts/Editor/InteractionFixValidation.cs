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

public static class InteractionFixValidation
{
    public static bool Running;
    public static string Output;
    sealed class Host:MonoBehaviour {}
    [Serializable] sealed class Meta {public string folder;}
    static ForgeSeatView ReadView(string path) => (ForgeSeatView)typeof(OnlineMatchSession).Assembly.GetType("ServerSnapshotJson").GetMethod("Deserialize").MakeGenericMethod(typeof(ForgeSeatView)).Invoke(null,new object[]{File.ReadAllText(path)});
    public static void Run(string label) {
        if(!Application.isPlaying||Running)throw new Exception("Play required");
        Running=true;Output=".tools/interaction-fixes-1013/ui/"+label+"-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmss");
        Directory.CreateDirectory(Output);new GameObject("InteractionFixValidation").AddComponent<Host>().StartCoroutine(Guard(Exercise()));
    }
    static IEnumerator Guard(IEnumerator e) {
        while(true) {
            object next;bool more;
            try {more=e.MoveNext();next=more?e.Current:null;}
            catch(Exception ex){File.WriteAllText(Output+"/result.txt","FAIL\n"+ex);Running=false;more=false;next=null;}
            if(!more)yield break;yield return next;
        }
    }
    static IEnumerator Exercise() {
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ValidationBattlefield").AddComponent<ForgeBattlefieldController>();
        bool prior=ui.enabled;ui.enabled=false;
        var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);
        foreach(var n in notices)n.enabled=false;
        GamePreferences.SetLanguage("zh-CN");
        float until=Time.realtimeSinceStartup+25;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;
        var lines=new List<string>();
        foreach(string scenario in new[]{"living","subtlety","surveil","kappa","pinnacle","bauble"}) {
            if(!File.Exists(".tools/interaction-fixes-1013/native/"+scenario+"-latest.json"))continue;
            var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/interaction-fixes-1013/native/"+scenario+"-latest.json"));
            var files=Directory.GetFiles(meta.folder,"*-seat1.json").OrderBy(p=>int.Parse(Path.GetFileName(p).Split('-')[0]));
            var seen=new HashSet<string>();
            foreach(string path in files) {
                var v=ReadView(path);
                string kind=v.request.kind;
                bool inspectStack=(scenario=="kappa"||scenario=="pinnacle"||scenario=="bauble")&&v.stackItems.Any(s=>s.kind!="spell");
                bool inspectMarker=scenario=="bauble"&&v.players.Any(p=>(p.effectMarkers??Array.Empty<ForgeCardView>()).Length>0);
                bool inspectSupport=scenario=="bauble"&&v.players.Any(p=>p.battlefield.Any(c=>!c.creature&&!c.land));
                if(kind!="OrderAbilities"&&kind!="SelectStackTarget"&&kind!="Surveil"&&kind!="InputConfirm"&&!inspectStack&&!inspectMarker&&!inspectSupport)continue;
                string captureKey=kind+(scenario=="bauble"&&inspectStack?(v.turn>=2?"-delayed":"-activated"):"");
                if(!seen.Add(captureKey))continue;
                var timer=System.Diagnostics.Stopwatch.StartNew();
                int submissions=0;int[] submittedOrder=null;
                try {ui.Present(v,v.request.seat,true,i=>lines.Add("SUBMIT "+i),null,"",(i,order)=>{submissions++;submittedOrder=order;lines.Add("ORDER "+string.Join(",",order));});}
                catch(Exception e){lines.Add("FAIL "+scenario+" "+kind+" "+e);continue;}
                timer.Stop();lines.Add("PRESENT "+scenario+" "+kind+" "+timer.Elapsed.TotalMilliseconds.ToString("F2")+"ms");
                yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();
                Canvas.ForceUpdateCanvases();
                var root=GameObject.Find("Canvas").transform;
                if(kind=="OrderAbilities"&&v.request.options.Any(o=>o.id=="submit-order")) {
                    var panel=root.Find("AbilityOrder");var tiles=panel.GetComponentsInChildren<AbilityOrderDrag>().OrderBy(t=>((RectTransform)t.transform).anchoredPosition.x).ToArray();
                    var first=tiles[0];var last=tiles[tiles.Length-1];
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,last.transform.TransformPoint(((RectTransform)last.transform).rect.center)),button=PointerEventData.InputButton.Left};
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                    if(hits.Count==0||hits[0].gameObject.GetComponentInParent<AbilityOrderDrag>()!=last)throw new Exception("Ordering drag blocked");
                    var dragTimer=System.Diagnostics.Stopwatch.StartNew();
                    ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.beginDragHandler);
                    pointer.position=RectTransformUtility.WorldToScreenPoint(null,first.transform.TransformPoint(((RectTransform)first.transform).rect.center));
                    ExecuteEvents.Execute(last.gameObject,pointer,ExecuteEvents.dragHandler);ExecuteEvents.Execute(last.gameObject,pointer,ExecuteEvents.endDragHandler);
                    dragTimer.Stop();
                    if(submissions!=0)throw new Exception("Drag submitted to engine");
                    var confirm=panel.Find("ConfirmOrder").GetComponent<Button>();confirm.onClick.Invoke();confirm.onClick.Invoke();
                    if(submissions!=1||submittedOrder[0]!=last.Id)throw new Exception("Order submission mismatch or duplicate");
                    if(panel.GetComponentsInChildren<Text>().Any(t=>t.name=="Order"&&t.gameObject.activeInHierarchy&&t.text.Length>0))throw new Exception("Numbered order marker remained");
                    lines.Add("PASS DRAG "+scenario+" "+dragTimer.Elapsed.TotalMilliseconds.ToString("F2")+"ms, no request while dragging, one confirmation");
                }
                var texts=root.GetComponentsInChildren<Text>().Where(t=>t.gameObject.activeInHierarchy&&t.enabled).Select(t=>t.name+": "+t.text);
                var tmp=root.GetComponentsInChildren<TMPro.TMP_Text>().Where(t=>t.gameObject.activeInHierarchy&&t.enabled).Select(t=>t.name+": "+t.text);
                File.WriteAllLines(Output+"/"+scenario+"-"+captureKey+"-text.txt",texts.Concat(tmp));
                var origin=root.Find("TargetOrigin");
                if(origin!=null&&origin.gameObject.activeInHierarchy)lines.Add("ORIGIN sprite="+(origin.GetComponent<Image>().sprite!=null)+" ability="+(origin.Find("AbilityFace")?.gameObject.activeInHierarchy??false));
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/"+scenario+"-"+captureKey+".png"));
                if(inspectStack||inspectMarker||inspectSupport) {
                    foreach(var card in v.players.SelectMany(p=>p.battlefield).Where(c=>!c.creature&&!c.land)) {
                        var cardRect=GameObject.Find("Card_"+card.id)?.GetComponent<RectTransform>();
                        if(cardRect!=null)lines.Add("SUPPORT width="+cardRect.rect.width.ToString("F1"));
                    }
                }
            }
        }
        File.WriteAllLines(Output+"/result.txt",lines);Running=false;ui.enabled=prior;
        UnityEngine.Object.Destroy(GameObject.Find("InteractionFixValidation"));
    }
}
#endif
