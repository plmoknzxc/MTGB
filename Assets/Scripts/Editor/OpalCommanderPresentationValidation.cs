#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class OpalCommanderPresentationValidation
{
    const string Output=".tools/opal-commander-1009/ui";
    const string Current=".tools/opal-commander-1009/prior-2/1791488398265659000/";
    static void Check(bool v,string why){if(!v)throw new Exception(why);}
    static ForgeSeatView Read(string folder,int step)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(folder+"step-"+step+"-seat1.json"));
    static void Click(GameObject root)=>typeof(CompanionPresentationValidation).GetMethod("Click",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{root});
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static IEnumerator Shot(string name){ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return Frame();}
    public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("OpalValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        var e=Validate(ui);while(true){object next;try{if(!e.MoveNext())break;next=e.Current;}catch(Exception x){File.WriteAllText(Output+"/result.txt","FAIL "+x);throw;}yield return next;}
        File.WriteAllText(Output+"/result.txt","PASS native own Command exact ray127; native costs4W; resolved counter1/3 and green PT3/3,5/5 versus original2/2; Command shelf removed and hover callback/original button ray. No four-seat/network claim.");
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        bool enabled=ui.enabled;ui.enabled=false;string lang=GamePreferences.Language;var module=EventSystem.current?.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        try{
            float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Preferences ready");GamePreferences.SetLanguage("zh-CN");yield return Frame();
            var v=Read(Current,18);int selected=-1;ui.Present(v,1,true,i=>selected=i,null,"");yield return Frame();var card=v.playableCommand.Single(c=>c.id==127);var root=GameObject.Find("Card_127");Check(root!=null&&card.zone=="Command","Commander missing surface");yield return Shot("commander-entry");Click(root);Check(selected>=0&&v.request.options[selected].id=="card:127","Commander wrong ray");
            v=Read(Current,20);Check(v.request.remainingManaCost=="{4}{W}","Native tax not4W");ui.Present(v,1,true,null,null,"");yield return Frame();yield return Shot("commander-tax-payment");
            foreach(var sample in new[]{(".tools/opal-commander-1009/prior-0/1791488285040570900/",26,123,1),(Current,34,127,3)}){
                v=Read(sample.Item1,sample.Item2);card=v.players[0].battlefield.Single(c=>c.id==sample.Item3);ui.Present(v,1,false,null,null,"");yield return Frame();root=GameObject.Find("Card_"+card.id);Check(root!=null,"Resolved commander missing");var counter=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Counter_P1P1");Check(counter.GetComponentInChildren<Text>().text==sample.Item4.ToString(),"Wrong displayed counter");Check(!root.GetComponentsInChildren<Transform>().Any(t=>t.name=="PlayableZone"),"Command badge retained");Check(ForgeBattlefieldController.ModifiedStatsText(card,false).Contains("16894C")&&!ForgeBattlefieldController.ModifiedStatsText(card,false).Contains("216D9E"),"Counter change should be green, not base rewrite blue");
                var pointer=root.GetComponentInChildren<ForgeBattlefieldCardPointer>();Check(pointer!=null,"No hover pointer");var data=new PointerEventData(EventSystem.current);ExecuteEvents.Execute(pointer.gameObject,data,ExecuteEvents.pointerEnterHandler);yield return Frame();var panel=GameObject.Find("InspectPanel");Check(panel!=null&&panel.transform.Find("Footer").GetComponent<Text>().text.Contains("63E69B"),"Current PT details not green");Check(panel.transform.Find("Rules").GetComponent<Text>().text.Contains("×"+sample.Item4),"Current counter detail absent");yield return Shot("counter-"+sample.Item4+"-current");Click(panel.transform.Find("OriginalState").gameObject);yield return Frame();Check(panel.transform.Find("Footer").GetComponent<Text>().text.StartsWith("2/2")&&!panel.transform.Find("Rules").GetComponent<Text>().text.Contains("×"+sample.Item4),"Original details retained modified counter/PT");yield return Shot("counter-"+sample.Item4+"-original");
            }
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(lang);if(module!=null)module.enabled=input;}
    }
}
#endif

