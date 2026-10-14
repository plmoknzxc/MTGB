#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CompanionPresentationValidation
{
    const string Output=".tools/companion-payment-1009/ui";
    [Serializable] class NativeCase { public string @case,directory,status,runtimeHash; }
    [Serializable] class NativeResults { public NativeCase[] results; }
    static bool running;
    static readonly List<string> steps=new();
    static NativeCase Native(string name)=>JsonUtility.FromJson<NativeResults>(File.ReadAllText(".tools/companion-payment-1009/result.json")).results.Single(x=>x.@case==name);
    static void Check(bool value,string why){if(!value)throw new Exception(why);}
    static ForgeSeatView Read(string name,string state,int seat=1)
    {
        var n=Native(name);Check(n.status=="PASS_PAYMENT_TO_HAND","Native payment result");
        return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(n.directory+"/"+state+"-seat"+seat+".json"));
    }
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Passed(string text){steps.Add(text);File.AppendAllText(Output+"/steps.log",text+" PASS\n");}
    static GameObject Card(int id)=>GameObject.Find("Card_"+id);
    static void Click(GameObject root)
    {
        Check(root!=null,"Missing click surface");var rect=(RectTransform)root.transform;
        foreach(float y in new[]{.9f,.75f,.5f})foreach(float x in new[]{.5f,.25f,.75f}) {
            var p=rect.TransformPoint(new Vector3(Mathf.Lerp(rect.rect.xMin,rect.rect.xMax,x),Mathf.Lerp(rect.rect.yMin,rect.rect.yMax,y),0));
            var hits=new List<RaycastResult>();var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,p),button=PointerEventData.InputButton.Left};
            EventSystem.current.RaycastAll(data,hits);
            if(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(root.transform)) {
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);return;
            }
        }
        throw new Exception("No visible unblocked ray on "+root.name);
    }
    static IEnumerator Capture(string name){ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return Frame();}
    public static void Run()
    {
        Check(Application.isPlaying&&!running,"Play/runner state");running=true;steps.Clear();Directory.CreateDirectory(Output);
        File.WriteAllText(Output+"/steps.log","");File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("CompanionValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        var stack=new Stack<IEnumerator>();stack.Push(Validate(ui));
        try {
            while(stack.Count>0) {
                object current;
                try {var e=stack.Peek();if(!e.MoveNext()){(e as IDisposable)?.Dispose();stack.Pop();continue;}current=e.Current;if(current is IEnumerator child){stack.Push(child);continue;}}
                catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}
                yield return current;
            }
            File.WriteAllText(Output+"/result.txt","PASS "+steps.Count+" bounded Companion UI checks. Actual native entry/payment/cancel/hand snapshots; exact ray IDs and guards. No live-network or all-command-mode claim.");
        }finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();running=false;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        bool enabled=ui.enabled;ui.enabled=false;string language=GamePreferences.Language;
        var module=EventSystem.current?.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        try {
            float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Language ready");
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}) {
                GamePreferences.SetLanguage(locale);until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;
                Check(GamePreferences.Ready,"Locale resources");ui.Present(null,1,false,null,null,"");yield return Frame();
                var v=Read("pay","command-entry");var card=v.playableCommand.Single(c=>c.name=="Jegantha, the Wellspring");int selected=-1,calls=0;
                ui.Present(v,1,true,i=>{selected=i;calls++;},null,"");yield return Frame();var root=Card(card.id);Check(root!=null,"Command card has no surface");
                var badge=root.transform.Find("Face/PlayableZone");if(badge==null)badge=root.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="PlayableZone");
                Check(badge!=null&&badge.GetComponentInChildren<Text>().text==(locale=="zh-CN"?"命":locale=="en-US"?"C":"統"),"Command zone badge");
                int option=Array.FindIndex(v.request.options,o=>o.id=="card:"+card.id);Check(option>=0,"Native command option missing");
                ui.Choose(option,v.request.requestId-1);Check(calls==0,"Stale command request accepted");
                yield return Capture(locale+"-command-entry");Click(root);Check(calls==1&&v.request.options[selected].id=="card:"+card.id,"Command ray submitted wrong ID");
                ui.Choose(option,v.request.requestId);Check(calls==1,"Duplicate command submission");Passed(locale+" command surface/zone/exact ray/stale/duplicate");
                ui.Present(Read("pay","resolved"),1,false,null,null,"");yield return Frame();root=Card(card.id);
                Check(root!=null&&!root.GetComponentsInChildren<Transform>().Any(t=>t.name=="PlayableZone"),"Command badge retained after entering Hand");
                Check(GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t=>t.name=="Card_"+card.id)==1,"Duplicate companion after transfer");
                yield return Capture(locale+"-resolved-hand");Passed(locale+" same identity hand transfer removes command badge");
            }
            GamePreferences.SetLanguage("zh-CN");yield return Frame();
            var entry=Read("cancel-retry","command-entry");int id=entry.playableCommand.Single().id;int choice=-1,count=0;
            ui.Present(entry,1,false,i=>count++,null,"");yield return Frame();ui.Choose(Array.FindIndex(entry.request.options,o=>o.id=="card:"+id),entry.request.requestId);Check(count==0,"Readonly command submitted");Passed("readonly command action rejected");
            var other=Read("pay","command-entry",2);ui.Present(other,2,false,i=>count++,null,"");yield return Frame();Check((other.playableCommand?.Length??0)==0&&Card(id)==null,"Companion action shelf leaked to nonoperator");Passed("other recipient has no command action shelf");
            var n=Native("cancel-retry");var path=Directory.GetFiles(n.directory,"payment-*-seat1.json").Select(p=>new {path=p,view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p))}).First(x=>x.view.players[0].battlefield.Any(c=>c.tapped));
            var payment=path.view;ui.Present(payment,1,true,i=>choice=i,null,"");yield return Frame();var cancel=GameObject.Find("CancelPayment");Check(cancel!=null,"Native command payment has no cancel control");
            yield return Capture("partial-payment-cancel");Click(cancel);Check(choice>=0&&payment.request.options[choice].id=="cancel","Partial-payment cancel ray");Passed("actual partial command payment cancel ray");
            var restored=Read("cancel-retry","cancel-restored");choice=-1;ui.Present(restored,1,true,i=>choice=i,null,"");yield return Frame();yield return Capture("cancel-restored-command");Click(Card(id));Check(choice>=0&&restored.request.options[choice].id=="card:"+id,"Refunded command entry cannot retry");Passed("cancel-restored native command exact retry ray");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(language);if(module!=null)module.enabled=input;}
    }
}
#endif
