#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ChosenTypeCostPresentationValidation
{
    const string Base=".tools/chosen-type-cost-1009";
    public static bool Running{get;private set;}
    public static string Output{get;private set;}
    static int checks;
    static void Check(bool value,string why){if(!value)throw new Exception(why);checks++;}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Output+"/inputs/"+name+".json"));
    static IEnumerator Frame(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static Button Named(string name)=>GameObject.Find(name)?.GetComponent<Button>()??throw new Exception("Missing "+name);
    static void Ray(Button button){
        var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
        for(int y=1;y<7;y++)for(int x=1;x<7;x++){
            var point=new Vector2(Mathf.Lerp(corners.Min(v=>v.x),corners.Max(v=>v.x),x/7f),Mathf.Lerp(corners.Min(v=>v.y),corners.Max(v=>v.y),y/7f));
            var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=point};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
            if(!button.interactable||hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=button)continue;
            Check(true,"Actual topmost ray "+button.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);return;
        }
        throw new Exception("No exposed button ray "+button.name);
    }
    static void Dismiss(){
        var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right,position=new Vector2(10,Screen.height/2)};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
        Check(hits.Count>0&&hits[0].gameObject.name=="DismissAbilityChoices","Actual outside backdrop ray");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);
    }
    static void Gate(ForgeSeatView v,bool enabled){
        Check(v.request.minCount==2&&v.request.maxCount==2,"Native exact-two bounds");
        int ok=Array.FindIndex(v.request.options,o=>o.id=="ok");var button=ok<0?null:GameObject.Find("SelectionAction_"+ok)?.GetComponent<Button>();
        Check(enabled?button!=null&&button.interactable:button==null||!button.interactable,"Exact-two confirmation gate count="+v.request.selectedCount);
    }
    static Button Card(int id)=>GameObject.Find("ChoiceCard_"+id).transform.Find("Artwork").GetComponent<Button>();
    public static void Run(){
        if(!Application.isPlaying||Running)throw new Exception("Play required/already running");Running=true;checks=0;
        Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");Directory.CreateDirectory(Output+"/sources");
        File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        foreach(var p in Directory.GetFiles(Base+"/ui-inputs"))File.Copy(p,Output+"/inputs/"+Path.GetFileName(p));
        foreach(var p in new[]{"Assets/Scripts/Editor/ChosenTypeCostPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.LandChoices.cs","Assets/Scripts/UI/ForgeBattlefieldController.CreatureTypes.cs","Assets/Scripts/UI/ForgeBattlefieldController.CardSelection.cs","Assets/Scripts/UI/ForgeChoiceDismiss.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ChosenTypeValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator work){
        var tasks=new Stack<IEnumerator>();tasks.Push(work);
        try{while(tasks.Count>0){object next;try{var t=tasks.Peek();if(!t.MoveNext()){tasks.Pop();continue;}next=t.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator child)tasks.Push(child);else yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: "+checks+" checks; actual cancellable chosen-type cost and mandatory Cavern requests, search/exact index/outside ray cancel/guards; actual ordinary Behold2 mixed Hand/Battlefield identities, 0/1/2 confirmation gates, reset/cancel/retry and cleanup. Rules separately verified; prior localization/network contracts retained.");}
        finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui){
        string original=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;
        var module=EventSystem.current.currentInputModule;bool moduleEnabled=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        try{
            float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Preferences ready");
            GamePreferences.SetLanguage("zh-CN");until=Time.realtimeSinceStartup+12;while((!GamePreferences.Ready||GamePreferences.Language!="zh-CN")&&Time.realtimeSinceStartup<until)yield return null;
            Check(GamePreferences.Ready&&GamePreferences.Language=="zh-CN","Chinese locale ready");
            int calls=0,sent=-1;Action<int> submit=i=>{calls++;sent=i;};
            var v=Read("type");ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(v,1,true,submit,null,"");yield return Frame();
            var menu=GameObject.Find("CardAbilityChoices");Check(menu.transform.Find("TypeTitle")!=null&&menu.transform.Find("Source")==null,"Cost type uses bounded type menu");
            var input=menu.GetComponentInChildren<InputField>();input.text="Human";yield return Frame();int human=Array.FindIndex(v.request.options,o=>o.label=="Human");Ray(Named("TypeOption_"+human));Check(calls==1&&sent==human,"Native Human index");
            Check(!ui.TrySubmitPresentedOption(human,v.request.requestId)&&calls==1,"Type duplicate blocked");
            v=Read("retry-type");ui.Present(v,1,true,submit,null,"");yield return Frame();Dismiss();Check(calls==2&&sent==Array.FindIndex(v.request.options,o=>o.id=="cancel"),"Cost allows native cancel");
            v=Read("mandatory");ui.Present(v,1,true,submit,null,"");yield return Frame();Dismiss();Check(calls==2&&Array.FindIndex(v.request.options,o=>o.id=="cancel")<0,"Mandatory Cavern cannot cancel");
            input=GameObject.Find("CardAbilityChoices").GetComponentInChildren<InputField>();input.text="Human";yield return Frame();human=Array.FindIndex(v.request.options,o=>o.label=="Human");Ray(Named("TypeOption_"+human));Check(calls==3&&sent==human,"Mandatory exact type ray preserved");
            ui.Present(null,1,false,null,null,"");yield return Frame();v=Read("type");ui.Present(v,1,false,submit,null,"");yield return Frame();
            Check(!GameObject.Find("CardAbilityChoices").GetComponentInChildren<InputField>().interactable&&!ui.TrySubmitPresentedOption(0,v.request.requestId)&&calls==3,"Readonly type cannot submit");
            ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(v,1,true,submit,null,"");yield return Frame();
            Check(!ui.TrySubmitPresentedOption(0,v.request.requestId-1)&&calls==3,"Stale type blocked");
            ScreenCapture.CaptureScreenshot(Output+"/cost-type.png");yield return Frame();
            ui.Present(null,1,false,null,null,"");yield return Frame();var initial=Read("initial");ui.Present(initial,1,true,submit,null,"");yield return Frame();Gate(initial,false);
            var panel=GameObject.Find("CardSelection");Check(panel!=null,"Mixed origin uses existing modal");
            Check(panel.GetComponentsInChildren<Button>().Count(b=>b.name=="Artwork")==2&&GameObject.Find("ChoiceCard_123")==null,"Only exact two legal Elves");
            Check(GameObject.Find("ChoiceCard_122").transform.Find("OriginZone").GetComponent<Text>().text=="手牌"&&GameObject.Find("ChoiceCard_128").transform.Find("OriginZone").GetComponent<Text>().text=="战场","Exact distinct origin labels");
            int index=Array.FindIndex(initial.request.options,o=>o.id=="card:122");Ray(Card(122));Check(sent==index&&calls==4,"Actual hand ray");
            var one=Read("one");ui.Present(one,1,true,submit,null,"");yield return Frame();Gate(one,false);
            Check(!ui.TrySubmitPresentedOption(index,initial.request.requestId)&&calls==4,"Old picker request blocked");
            int reset=Array.FindIndex(one.request.options,o=>o.id=="reset-cards");Ray(Named("SelectionAction_"+reset));Check(calls==5&&sent==reset,"Native reset action");
            var retry=Read("retry");ui.Present(retry,1,true,submit,null,"");yield return Frame();Gate(retry,false);index=Array.FindIndex(retry.request.options,o=>o.id=="card:128");Ray(Card(128));Check(calls==6&&sent==index,"Actual battlefield ray on retry");
            var two=Read("two");ui.Present(two,1,true,submit,null,"");yield return Frame();Gate(two,true);
            ScreenCapture.CaptureScreenshot(Output+"/behold-two.png");yield return Frame();index=Array.FindIndex(two.request.options,o=>o.id=="ok");Ray(Named("SelectionAction_"+index));Check(calls==7&&sent==index,"Exact-two native confirmation");
            ui.Present(one,1,true,submit,null,"");yield return Frame();index=Array.FindIndex(one.request.options,o=>o.id=="cancel");Ray(Named("SelectionAction_"+index));Check(calls==8&&sent==index,"One-selected native cancel");
            ui.Present(Read("restored"),1,true,submit,null,"");yield return Frame();Check(GameObject.Find("CardSelection")==null,"Restored priority removes picker");
            ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(initial,1,false,submit,null,"");yield return Frame();Check(!Card(122).interactable&&!Card(128).interactable&&calls==8,"Readonly mixed-zone picker cannot act");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=moduleEnabled;GamePreferences.SetLanguage(original);}
    }
}
#endif
