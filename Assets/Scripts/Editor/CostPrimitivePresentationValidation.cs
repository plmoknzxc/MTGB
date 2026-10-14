#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CostPrimitivePresentationValidation
{
    const string Base=".tools/cost-primitives-1008";
    static bool running;
    static int checks;
    static string output;
    static bool libraryOnly;
    static void Check(bool ok,string why) { if(!ok)throw new Exception(why);checks++; }
    public static void Run(bool onlyLibrary=false) {
        if(running)return;Check(Application.isPlaying,"Play required");running=true;checks=0;libraryOnly=onlyLibrary;
        output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(output);
        File.WriteAllText(Base+"/ui-current.txt",output);File.WriteAllText(output+"/result.txt","RUNNING");
        File.Copy("Assets/Scripts/Editor/CostPrimitivePresentationValidation.cs",output+"/driver.cs");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("CostPrimitiveValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator routine) {
        try {
            while(true) { object next;try { if(!routine.MoveNext())break;next=routine.Current; }catch(Exception e){File.WriteAllText(output+"/result.txt","FAIL "+e);throw;}yield return next; }
            File.WriteAllText(output+"/result.txt","PASS: "+checks+" checks; "+(libraryOnly?"two actual library-cost confirmations in en-US/ja-JP; zh-CN retained from 20261008T0245214087133Z":"three actual native cost confirmations zh-CN")+"; both original indices via real rays, duplicate/stale/nonacting guards, compact centered dialog and reset lifecycle. No global/network PASS.");
        } finally { running=false; }
    }
    static IEnumerator Frame() { yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases(); }
    static void Click(Button button) {
        var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Button ray "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Validate(ForgeBattlefieldController ui) {
        var language=GamePreferences.Language;var enabled=ui.enabled;ui.enabled=false;
        var module=EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();var moduleEnabled=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var banners=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=banners.Select(b=>b.enabled).ToArray();foreach(var b in banners)b.enabled=false;
        try {
          foreach(string locale in libraryOnly?new[]{"en-US","ja-JP"}:new[]{"zh-CN"}) {
            GamePreferences.SetLanguage(locale);float deadline=Time.realtimeSinceStartup+10;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language==locale,"Exact language ready: "+locale);
            foreach(string test in libraryOnly?new[]{"exile-top-before-mana","mill-before-mana"}:new[]{"counter-before-mana","exile-top-before-mana","mill-before-mana"}) {
                string path=Base+"/ui-inputs/"+test+".json";File.Copy(path,output+"/"+locale+"-"+test+".json");
                var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));Check(view.request.kind=="InputConfirm"&&view.request.options.Length==2,"Exact two native decisions");
                foreach(string id in new[]{"ok","cancel"}) {
                    int calls=0,last=-1,index=Array.FindIndex(view.request.options,o=>o.id==id);Check(index>=0,"Native index "+id);
                    ui.Present(null,1,false,null,null,"");ui.Present(view,1,true,i=>{calls++;last=i;},null,"");yield return Frame();
                    var button=GameObject.Find("ValueOption_"+index)?.GetComponent<Button>();Check(button!=null,"Compact native confirmation button");
                    var title=GameObject.Find("ValueTitle").GetComponent<Text>();Check(!string.IsNullOrWhiteSpace(title.text),"Visible instruction");
                    if(test=="counter-before-mana")Check(title.text.Contains("指示物")&&!title.text.Contains("Remove"),"Existing counter translation branch");
                    if(libraryOnly) {
                        string expected=test=="exile-top-before-mana"?(locale=="zh-CN"?"放逐":locale=="ja-JP"?"追放":"Exile"):(locale=="zh-CN"?"磨":locale=="ja-JP"?"切削":"Mill");
                        Check(title.text.Contains(expected)&&title.text.Contains(test=="exile-top-before-mana"?"10":"4")&&!title.text.Contains("{0}")&&!title.text.Contains("(or:"),"Exact localized quantity/title: "+title.text);
                    }
                    var menu=(RectTransform)button.transform.parent;Check(menu.rect.width<=400.1f&&menu.anchoredPosition==Vector2.zero,"Centered compact dialog");
                    Check(!ui.TrySubmitPresentedOption(index,view.request.requestId-1),"Stale request rejected");
                    if(id=="ok") {ScreenCapture.CaptureScreenshot(output+"/"+locale+"-"+test+".png");yield return Frame();}
                    Click(button);button.onClick.Invoke();Check(calls==1&&last==index,"Original decision index once");
                    ui.Present(null,1,false,null,null,"");yield return Frame();Check(!menu.gameObject.activeInHierarchy,"Reset hides confirmation");
                    ui.Present(view,1,false,i=>calls++,null,"");yield return Frame();Check(!ui.TrySubmitPresentedOption(index,view.request.requestId)&&calls==1,"Nonacting submission rejected");
                    File.AppendAllText(output+"/steps.log",locale+" "+test+" "+id+" index="+index+" PASS; title="+title.text.Replace("\n"," / ")+"\n");
                }
            }
          }
        } finally {ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=moduleEnabled;GamePreferences.SetLanguage(language);for(int i=0;i<banners.Length;i++)if(banners[i]!=null)banners[i].enabled=flags[i];}
    }
}
#endif
