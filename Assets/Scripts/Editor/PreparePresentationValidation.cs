#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class PreparePresentationValidation
{
    const string Output=".tools/prepare-ui";
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/Prepare/"+name+".json"));
    public static void Run() {
        Check(Application.isPlaying,"Play Mode required");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("PrepareValidation").AddComponent<ForgeBattlefieldController>();
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator run) {
        while(true) {
            object next;
            try {if(!run.MoveNext())yield break;next=run.Current;}
            catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL: "+e);throw;}
            yield return next;
        }
    }
    static IEnumerator Validate(ForgeBattlefieldController ui) {
        var ready=Read("ready");var source=ready.players[0].battlefield.Single(c=>c.name=="Emeritus of Woe");
        Check(source.preparedSpell?.name=="Demonic Tutor","Prepared payload lost by Unity JSON decoder");
        int chosen=-1;ui.Present(ready,1,true,i=>chosen=i,null,"");yield return new WaitForSecondsRealtime(.5f);
        var tab=GameObject.Find("Card_"+source.preparedSpell.id);Check(tab!=null,"No playable prepared spell");
        Check(GameObject.Find("PreparedSpellTab")==null,"Redundant creature tab remains");
        Check(GameObject.Find("CardAbilityChoices")==null,"Single prepared spell added unnecessary choice");
        ScreenCapture.CaptureScreenshot(Output+"/ready.png");yield return new WaitForSecondsRealtime(.25f);
        ExecuteEvents.Execute(tab,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
        yield return new WaitForSecondsRealtime(.4f);
        var handFace=tab.transform.Find("Visual/Border/Face/PreparedSpellFace");
        Check(handFace.Find("Name").GetComponent<Text>().text=="Demonic Tutor","Hand face uses creature name");
        Check(handFace.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("Search your library")),"Hand spell rules missing");
        ExecuteEvents.Execute(tab,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
        GameObject.Find("Card_"+source.id).GetComponent<Button>().onClick.Invoke();
        Check(chosen>=0&&ready.request.options[chosen].id=="card:"+source.preparedSpell.id,"Creature click did not select prepared spell");
        File.WriteAllText(Output+"/result.txt","PAYMENT");
        var payment=Read("payment");ui.Present(payment,1,true,i=>{},null,"");yield return new WaitForSecondsRealtime(.5f);
        var face=GameObject.Find("TargetOrigin/PreparedSpellFace");Check(face!=null,"Payment preview is the creature");
        Check(face.transform.Find("Name").GetComponent<Text>().text=="Demonic Tutor","Wrong payment spell name");
        var cost=face.transform.Find("Cost").GetComponent<TMPro.TextMeshProUGUI>();cost.ForceMeshUpdate();
        Check(cost.textInfo.lineCount==1,"Mana cost wrapped");
        ScreenCapture.CaptureScreenshot(Output+"/payment.png");yield return new WaitForSecondsRealtime(.25f);
        File.WriteAllText(Output+"/result.txt","STACK");
        var stack=Read("stack");ui.Present(stack,1,false,i=>{},null,"");yield return new WaitForSecondsRealtime(1);
        Check(GameObject.Find("StackCard_0/PreparedSpellFace")!=null,"Prepared spell stack face missing");
        Check(GameObject.Find("Card_"+source.id)!=null,"Source disappeared when casting its spell");
        ScreenCapture.CaptureScreenshot(Output+"/stack.png");yield return new WaitForSecondsRealtime(.25f);
        File.WriteAllText(Output+"/result.txt","RESOLVED");
        ui.Present(Read("resolved"),1,true,i=>{},null,"");yield return new WaitForSecondsRealtime(.2f);
        Check(GameObject.Find("Card_"+source.preparedSpell.id)==null,"Spent preparation still visible");
        var number=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/AdvancedInteractions/x-number.json"));
        int sent=-1;ui.Present(number,1,true,i=>sent=i,null,"");yield return new WaitForSecondsRealtime(.2f);
        var menu=GameObject.Find("CardAbilityChoices");Check(menu!=null,"No X picker");
        Check(menu.transform.Find("IncreaseFive").GetComponentInChildren<Text>().text=="+5","Wrong quick step");
        Check(!menu.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("10")),"Old ten step remains");
        ScreenCapture.CaptureScreenshot(Output+"/x-five.png");yield return new WaitForSecondsRealtime(.25f);
        menu.transform.Find("IncreaseFive").GetComponent<Button>().onClick.Invoke();
        Check(sent>=0&&number.request.options[sent].id=="number-plus-five","Five step command mismatch");
        File.WriteAllText(Output+"/result.txt","PASS: real Forge JSON decoding, playable exile shelf/click/hover; redundant source tab removed, distinct spell payment and stack faces, one-line mana cost, preparation removed after cast, X +5 command.");
        Debug.Log("PreparePresentationValidation PASS");
    }
}
#endif
