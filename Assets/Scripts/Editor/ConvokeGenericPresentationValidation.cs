#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ConvokeGenericPresentationValidation
{
    const string Output=".tools/convoke-generic-1007/ui";
    [Serializable] class Meta { public string folder,status,runtimeHash; }
    static bool running;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static ForgeSeatView Read(string file) {
        var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/convoke-generic-1007/native/convoke-generic-result.json"));
        var runtime=JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json"));
        Check(meta.status=="PASS"&&meta.runtimeHash==runtime.runtimeHash,"Native proof/runtime identity");
        return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Path.Combine(meta.folder,file+".json")));
    }
    static IEnumerator Frame(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static void Click(Button button) {
        var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Blocked actual option ray");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    public static void Run() {
        Check(Application.isPlaying&&!running,"Play/runner state");running=true;Directory.CreateDirectory(Output);
        File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/steps.log","");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("ConvokeGenericValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator work) {
        try {
            while(true){object next;try{if(!work.MoveNext())break;next=work.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS three-language actual convoke COLORLESS request renders generic {1}, green/white preserved, original index/raycast/stale/duplicate guards and native selected-state return. Separate ordinary-colorless UI adapter renders {C}; no additional native effect or live-network claim.");
        }finally{running=false;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui) {
        string language=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;
        var module=EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();bool inputs=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var banners=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var states=banners.Select(b=>b.enabled).ToArray();foreach(var b in banners)b.enabled=false;
        try {
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}) {
                GamePreferences.SetLanguage(locale);float deadline=Time.realtimeSinceStartup+8;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready,"Locale ready");
                var native=Read("color-Colorless");int calls=0,last=-1;ui.Present(null,1,false,null,null,"");
                ui.Present(Read("step-18"),1,true,_=>{},null,"");yield return Frame();
                ui.Present(native,1,true,i=>{calls++;last=i;},null,"");yield return Frame();
                int generic=Array.FindIndex(native.request.options,o=>o.label=="COLORLESS");Check(generic>=0,"Original native option");
                foreach(var pair in new[]{("GREEN","{G}"),("WHITE","{W}"),("COLORLESS","{1}")}) {
                    int index=Array.FindIndex(native.request.options,o=>o.label==pair.Item1);
                    var symbol=GameObject.Find("Option_"+index).GetComponentsInChildren<TMPro.TMP_Text>().Single(t=>t.name=="Mana");
                    symbol.ForceMeshUpdate();Check(symbol.text==CardSymbolText.Format(pair.Item2)&&symbol.textInfo.characterInfo.Take(symbol.textInfo.characterCount).Any(c=>c.isVisible),"Visible glyph "+pair.Item1);
                }
                Check(ForgeBattlefieldController.ChoiceManaSymbols("COLORLESS")=="{C}"&&ForgeBattlefieldController.ChoiceManaSymbols("Add {C}{C}.",native.request)=="{C}{C}","Ordinary colorless semantics changed");
                Check(!ui.TrySubmitPresentedOption(generic,native.request.requestId-1),"Stale generic selection accepted");
                ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-generic.png");yield return Frame();
                var button=GameObject.Find("Option_"+generic).GetComponent<Button>();Click(button);button.onClick.Invoke();Check(calls==1&&last==generic,"Native index/duplicate guard");
                ui.Present(Read("generic-chosen"),1,true,_=>{},null,"");yield return Frame();
                Check(GameObject.Find("CardAbilityChoices")==null&&GameObject.Find("CardSelection/Count").GetComponent<Text>().text.Contains("1"),"Nested return preserves native selected card");
                // Adapter negative: same labels outside the exact convoke context.
                var ordinary=Read("color-Colorless");ordinary.request.requestId+=1000;ordinary.request.message="one: Choose a mana color (presentation adapter)";
                ui.Present(null,1,false,null,null,"");ui.Present(ordinary,1,true,_=>{},null,"");yield return Frame();
                var colorless=GameObject.Find("Option_"+generic).GetComponentsInChildren<TMPro.TMP_Text>().Single(t=>t.name=="Mana");
                colorless.ForceMeshUpdate();Check(colorless.text==CardSymbolText.Format("{C}"),"Ordinary colorless glyph became generic");
                ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-colorless-adapter.png");yield return Frame();
                File.AppendAllText(Output+"/steps.log",locale+" actual {1}/{G}/{W}, native index/ray/guards/return; ordinary {C} adapter PASS\n");
            }
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=inputs;GamePreferences.SetLanguage(language);for(int i=0;i<banners.Length;i++)if(banners[i]!=null)banners[i].enabled=states[i];}
    }
}
#endif
