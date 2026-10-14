#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class AssistPaymentPresentationValidation
{
    const string Output=".tools/assist-payment-1009/ui";
    const string Current=".tools/assist-payment-1009/stack-cancel/1791487633307861800/";
    const string Partial=".tools/assist-payment-1009/helper-cancel/1791487295860311500/";
    static bool skipNumeric;
    static ForgeSeatView Read(string path,int seat)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path+"-seat"+seat+".json"));
    static void Check(bool value,string why){if(!value)throw new Exception(why);}
    static void Click(GameObject root)=>typeof(CompanionPresentationValidation).GetMethod("Click",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{root});
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static IEnumerator Shot(string name){ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return Frame();}
    public static void Run(bool paymentOnly=false){skipNumeric=paymentOnly;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("AssistValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        var e=Validate(ui);while(true){object next;try{if(!e.MoveNext())break;next=e.Current;}catch(Exception x){File.WriteAllText(Output+"/result.txt","FAIL "+x);throw;}yield return next;}
        File.WriteAllText(Output+"/result.txt","PASS helper numeric exact choice3, helper-only land exact ray, nonoperator readonly, partial helper cancel, caster cancel and original stack restore. Actual native snapshots replay; no live-network claim.");
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        bool enabled=ui.enabled;ui.enabled=false;string language=GamePreferences.Language;var module=EventSystem.current?.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        try{
            float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Language ready");GamePreferences.SetLanguage("zh-CN");yield return Frame();
            var v=Read(Current+"helper-amount",2);int selected=-1,calls=0;ui.Present(v,2,true,i=>{selected=i;calls++;},null,"");yield return Frame();
            if(!skipNumeric){
            var dock=GameObject.Find("CardAbilityChoices");Check(dock!=null&&v.request.playerSeat==2,"Helper amount actor/dock");
            for(int i=0;i<3;i++)Click(dock.transform.Find("Increase").gameObject);
            Check(calls==0&&dock.transform.Find("NumericValue").GetComponent<Text>().text=="3"&&!dock.transform.Find("Increase").GetComponent<Button>().interactable,"Amount draft/range");yield return Shot("helper-amount-3");Click(dock.transform.Find("ConfirmValue").gameObject);Check(calls==1&&v.request.options[selected].id=="choice:3","Wrong helper amount");
            File.WriteAllText(Output+"/numeric-result.txt","PASS native helper amount draft0..3 and exact choice:3 ray");}
            v=Read(Current+"payment-32",2);selected=-1;ui.Present(v,2,true,i=>selected=i,null,"");yield return Frame();
            Check(v.request.seat==2&&v.request.playerSeat==2&&ui.OptionIndex("card:123")<0&&ui.OptionIndex("card:125")>=0,"Helper payer ownership");yield return Shot("helper-payment");Click(GameObject.Find("Card_125"));yield return Frame();if(selected<0)Click(GameObject.Find("Card_125"));Check(selected>=0&&v.request.options[selected].id=="card:125","Wrong helper mana ray");
            var other=Read(Current+"payment-32",1);calls=0;ui.Present(other,1,false,i=>calls++,null,"");yield return Frame();ui.Choose(ui.OptionIndex("card:125"),other.request.requestId);Check(calls==0,"Caster can act for helper");
            v=Read(Partial+"payment-26",2);selected=-1;ui.Present(v,2,true,i=>selected=i,null,"");yield return Frame();yield return Shot("helper-partial-cancel");Click(GameObject.Find("CancelPayment"));Check(selected>=0&&v.request.options[selected].id=="cancel","Helper cancellation ray");
            v=Read(Current+"payment-38",1);selected=-1;ui.Present(v,1,true,i=>selected=i,null,"");yield return Frame();Check(v.request.remainingManaCost=="{U}"&&ui.OptionIndex("card:123")>=0,"Caster remainder/seat restore");yield return Shot("caster-remainder-cancel");Click(GameObject.Find("CancelPayment"));Check(selected>=0&&v.request.options[selected].id=="cancel","Caster cancel ray");
            v=Read(Current+"cancel-restored-stack",1);ui.Present(v,1,false,null,null,"");yield return Frame();Check(v.stackItems.Length==1&&v.stackItems[0].source.name=="Shock"&&GameObject.Find("CancelPayment")?.activeInHierarchy!=true&&GameObject.Find("CardAbilityChoices")==null,"Stale payment or restored stack projection");yield return Shot("restored-original-stack");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(language);if(module!=null)module.enabled=input;}
    }
}
#endif
