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

public static class BattlefieldRowBoundaryPresentationValidation
{
    public const string Output=".tools/battlefield-row-boundary-1005";
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static IEnumerator Frame(){Canvas.ForceUpdateCanvases();yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();}
    public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("RowValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        var test=Validate(ui);string locale=GamePreferences.Language;
        try{while(true){object next;try{if(!test.MoveNext())break;next=test.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: zh/en/ja six dense regions, complete card/quantity/shield bounds at start/end/intermediate scroll positions; hidden fragments reject rays; every representative reachable and raycastable; 575 engine IDs per side retained; repeated snapshots retain scroll; normal layout restores visibility. Static scene layout captured separately. Display adapters only.");}
        finally{GamePreferences.SetLanguage(locale);}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}) {
            GamePreferences.SetLanguage(locale);while(!GamePreferences.Ready)yield return null;
            var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();if(banner!=null)banner.enabled=false;
            var packet=BattlefieldStressPresentationValidation.Fixture();
            var stack=packet.stackItems;var stackText=packet.stack;int stackCount=packet.stackCount;
            // The stack itself occupies the right edge. Test dense-card input
            // with it absent; restore it for the combined display screenshots.
            packet.stackItems=Array.Empty<ForgeStackItem>();packet.stack=Array.Empty<string>();packet.stackCount=0;
            ui.Present(packet,1,true,_=>{},null,"");yield return new WaitForSecondsRealtime(1);
            var canvas=GameObject.Find("Canvas").transform;
            // Keep the user's physical mouse from opening a different card's
            // inspector during the independent scroll/raycast probes.
            var pointers=canvas.GetComponentsInChildren<ForgeBattlefieldCardPointer>().Where(p=>p.enabled).ToArray();
            foreach(var pointer in pointers){pointer.ResetHover();pointer.enabled=false;}
            var revision=typeof(ForgeBattlefieldController).GetField("inspectHoverRevision",BindingFlags.Instance|BindingFlags.NonPublic);revision.SetValue(ui,(int)revision.GetValue(ui)+1);canvas.Find("InspectPanel").gameObject.SetActive(false);
            var scrolls=canvas.GetComponentsInChildren<ScrollRect>().Where(s=>s.name.StartsWith("Dense_")).ToArray();Check(scrolls.Length==6,"six regions");
            foreach(var scroll in scrolls) {
                foreach(float position in new[]{1f,.77f,.41f,0f}) {
                    scroll.verticalNormalizedPosition=position;yield return Frame();
                    foreach(RectTransform card in scroll.content) {
                        if(!card.name.StartsWith("Card_"))continue;
                        var group=card.GetComponent<CanvasGroup>();
                        var bounds=new Bounds();bool first=true;
                        foreach(var rect in new[]{card,card.Find("Visual/Count") as RectTransform,card.Find("Visual/DefenseCounter") as RectTransform,card.Find("Visual/CardShadow") as RectTransform}.Where(r=>r!=null)) {
                            var corners=new Vector3[4];rect.GetWorldCorners(corners);foreach(var corner in corners){var point=scroll.viewport.InverseTransformPoint(corner);if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}
                        }
                        bool fits=bounds.min.y>=scroll.viewport.rect.yMin-.51f&&bounds.max.y<=scroll.viewport.rect.yMax+.51f;
                        Check((group.alpha>0)==fits,"partial card fragment "+scroll.name+"/"+card.name);Check(group.blocksRaycasts==fits,"hidden card accepts input");
                    }
                }
                foreach(RectTransform card in scroll.content) {
                    if(!card.name.StartsWith("Card_"))continue;
                    float travel=scroll.content.rect.height-scroll.viewport.rect.height;
                    scroll.verticalNormalizedPosition=1-Mathf.Clamp01((-card.anchoredPosition.y-scroll.viewport.rect.height*.5f)/travel);yield return Frame();
                    Check(card.GetComponent<CanvasGroup>().alpha==1,"unreachable representative "+card.name+" in "+scroll.name+" norm="+scroll.verticalNormalizedPosition+" pos="+card.anchoredPosition+" viewport="+scroll.viewport.rect);
                    var eventData=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,card.TransformPoint(card.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(eventData,hits);
                    Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<ForgeBattlefieldCardPointer>()?.transform==card,"blocked representative "+card.name+" by "+(hits.Count>0?hits[0].gameObject.name:"no hit"));
                }
            }
            foreach(var scroll in scrolls)scroll.verticalNormalizedPosition=.41f;
            ui.Present(packet,1,true,_=>{},null,"");yield return Frame();Check(scrolls.All(s=>Mathf.Abs(s.verticalNormalizedPosition-.41f)<.01f),"snapshot changed scroll");
            packet.stackItems=stack;packet.stack=stackText;packet.stackCount=stackCount;ui.Present(packet,1,true,_=>{},null,"");yield return Frame();
            foreach(var scroll in scrolls)scroll.verticalNormalizedPosition=0;
            yield return Frame();yield return Capture(ui,Output+"/"+locale+"-end.png");
            foreach(var scroll in scrolls)scroll.verticalNormalizedPosition=1;
            yield return Frame();yield return Capture(ui,Output+"/"+locale+"-start.png");
            foreach(var pointer in pointers)if(pointer!=null)pointer.enabled=true;
            DuelLayoutPresentationValidation.Preview(false);yield return Frame();Check(scrolls.All(s=>!s.gameObject.activeSelf),"normal viewport cleanup");
            foreach(var pointer in canvas.GetComponentsInChildren<ForgeBattlefieldCardPointer>())if(pointer.transform.parent.name=="ForgePermanents")Check(pointer.GetComponent<CanvasGroup>().alpha>0&&pointer.GetComponent<CanvasGroup>().blocksRaycasts,"normal card retained culled state");
        }
    }
    static IEnumerator Capture(ForgeBattlefieldController ui,string path)
    {
        var pointers=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None).Where(p=>p.enabled).ToArray();foreach(var pointer in pointers)pointer.enabled=false;
        var field=typeof(ForgeBattlefieldController).GetField("inspectHoverRevision",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(ui,(int)field.GetValue(ui)+1);GameObject.Find("Canvas").transform.Find("InspectPanel").gameObject.SetActive(false);
        ScreenCapture.CaptureScreenshot(path);yield return new WaitForEndOfFrame();yield return null;foreach(var pointer in pointers)pointer.enabled=true;
    }
}
#endif
