#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Bounded visual regression: native notice replays, not new rules evidence.
public static class RandomFacesPresentationValidation
{
    const string Output=".tools/random-faces-review-1004";
    static bool running;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static ForgeSeatView Read(string path)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));
    public static void Run()
    {
        if(running)return;Check(Application.isPlaying,"Requires Play Mode");running=true;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("RandomFacesValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard());
    }
    static IEnumerator Guard()
    {
        string original=GamePreferences.Language;
        var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=banner!=null&&banner.enabled;
        if(banner!=null)banner.enabled=false;
        var iterator=Validate();
        try {
            while(true){object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: zh-CN/en-US/ja-JP heads/tails engraved marks, fixed font, native win/lose kept separate, die 20 and modified 0 preserved; visible numbered-face meshes and rolling/settled screenshots; all result graphics nonblocking. Actual native blue pool request count/index/raycast and paid-state decrement. 1560x898. Replay evidence only; no new rules, RNG, all dice types or network claim.");
        }finally{running=false;GamePreferences.SetLanguage(original);if(banner!=null)banner.enabled=enabled;}
    }
    static IEnumerator Rendered(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static IEnumerator Shot(string name){yield return Rendered();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForEndOfFrame();}
    static IEnumerator Validate()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        UnityEngine.InputSystem.Mouse.current?.WarpCursorPosition(new Vector2(4,4));
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool changing=GamePreferences.Language!=locale;GamePreferences.SetLanguage(locale);
            float deadline=Time.realtimeSinceStartup+10;while((!GamePreferences.Ready||changing&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(GamePreferences.Ready,"Locale not ready");
            foreach(string sample in new[]{"heads","tails","coin-win","coin-lose","dice-high","xeno-dec"}) {
                var view=Read("Tools/Validation/Reports/RandomMechanics/"+(sample=="heads"||sample=="tails"?"mana-clash":sample)+"-resolved.json");
                var notice=sample=="heads"||sample=="tails"?view.notices.First(n=>n.value==sample):view.notices.First();view.notices=new[]{notice};
                ui.Present(null,view.seat,false,null,null,"");int calls=0;ui.Present(view,view.seat,true,_=>calls++,null,"");yield return Rendered();
                var graphic=GameObject.Find("RandomResultAnimation").GetComponentInChildren<RandomResultGraphic>();
                string expected=notice.kind=="coin"?notice.value:System.Text.RegularExpressions.Regex.Match(notice.value,@"rolled \[?([0-9]+)").Groups[1].Value;
                Check(graphic.Outcome==expected,"Native result changed");
                if(sample=="dice-high") {yield return new WaitForSecondsRealtime(.45f);yield return Shot(locale+"-die-rolling");}
                yield return new WaitForSecondsRealtime(sample=="dice-high"?.8f:1.25f);yield return Rendered();
                Check(graphic.Progress>=.67f&&graphic.canvasRenderer.GetMesh()?.vertexCount>0,"Result never visibly settled");
                Check(GameObject.Find("RandomResultAnimation").GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),"Animation blocks input");
                var mark=graphic.transform.Find("CoinFace");
                if(sample=="heads"||sample=="tails") {
                    string text=locale=="zh-CN"?(sample=="heads"?"正":"反"):locale=="ja-JP"?(sample=="heads"?"表":"裏"):(sample=="heads"?"H":"T");
                    Check(mark!=null&&mark.GetComponent<Text>().text==text&&!mark.GetComponent<Text>().resizeTextForBestFit,"Coin engraving missing/wrong language");
                }else if(notice.kind=="coin")Check(mark==null,"Win/lose was inferred as a coin side");
                else Check(graphic.canvasRenderer.GetMesh().vertexCount>FacesWithoutDigitsVertices,"Numbered die face geometry absent");
                Check(calls==0,"Passive animation submitted an option");yield return Shot(locale+"-"+sample);
            }
        }
        GamePreferences.SetLanguage("zh-CN");while(!GamePreferences.Ready)yield return null;
        var payment=Read("Tools/Validation/Reports/ManaPoolPayments/blue-payment.json");
        ui.Present(null,payment.seat,false,null,null,"");int paid=0,last=-1;ui.Present(payment,payment.seat,true,i=>{paid++;last=i;},null,"");
        yield return new WaitForSecondsRealtime(.6f);yield return Rendered();
        var blue=GameObject.Find("ManaPoolChips").transform.Find("Pool_U").GetComponent<Button>();
        Check(blue.interactable&&blue.transform.Find("Count").GetComponent<Text>().text=="2","Actual blue pool missing");
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,blue.transform.position),button=PointerEventData.InputButton.Left};
        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==blue,"Pool raycast blocked");
        yield return Shot("zh-CN-current-mana-pool");ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
        Check(paid==1&&last==Array.FindIndex(payment.request.options,o=>o.id=="mana:U"),"Pool submitted wrong option");
        var after=Read("Tools/Validation/Reports/ManaPoolPayments/blue-paid-scry.json");ui.Present(after,after.seat,true,null,null,"");yield return Rendered();
        Check(blue.transform.Find("Count").GetComponent<Text>().text=="1","Native paid count wrong");ui.Present(null,1,false,null,null,"");
    }
    // Twenty triangles, each with border and inset, contain at most 120 vertices.
    const int FacesWithoutDigitsVertices=120;
}
#endif
