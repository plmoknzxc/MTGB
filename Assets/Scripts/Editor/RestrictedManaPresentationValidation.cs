#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class RestrictedManaPresentationValidation
{
    const string Output=".tools/restricted-mana-1006/ui";
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter IntegratedBattlefield Play Mode.");
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("RestrictedManaValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator task)
    {
        string original=GamePreferences.Language;
        try {
            while(true) {
                object next;
                try {if(!task.MoveNext())break;next=task.Current;}
                catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}
                yield return next;
            }
            File.WriteAllText(Output+"/result.txt","PASS: actual Cavern 356-type request in zh-CN/en-US/ja-JP; bounded searchable/paged text rows, last native index, empty filter, localized/canonical search, Human exact raycast index, mandatory choice, duplicate/stale/viewer guards, fixed fonts; actual ability and ManaColor requests reuse original choice callbacks. Native payment/results separately proved by five engine cases; missing type translations retain canonical names.");
        } finally {GamePreferences.SetLanguage(original);}
    }
    static ForgeSeatView Load(string filename)
    {
        var meta=JsonUtility.FromJson<NativeResult>(File.ReadAllText(".tools/restricted-mana-1006/native/cavern-chosen-result.json"));
        return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(meta.folder+"/"+filename));
    }
    [Serializable] sealed class NativeResult { public string folder; }
    static Button Button(string name)=>GameObject.Find("CardAbilityChoices").GetComponentsInChildren<Button>().First(b=>b.name==name);
    static void Ray(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Choice raycast blocked: "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(var language in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool changing=GamePreferences.Language!=language;
            GamePreferences.SetLanguage(language);float deadline=Time.realtimeSinceStartup+10;
            while(changing&&revision==GamePreferences.Revision&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(!changing||revision!=GamePreferences.Revision,"Locale did not load");
            var view=Load("choice-14-seat1.json");int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};
            ui.Present(view,1,true,submit,null,"");yield return new WaitForSecondsRealtime(.25f);Canvas.ForceUpdateCanvases();
            var menu=GameObject.Find("CardAbilityChoices").GetComponent<RectTransform>();
            Check(menu.transform.Find("TypeTitle")!=null&&menu.transform.Find("Source")==null,"Type still shown as an ability");
            Check(menu.GetComponentsInChildren<RawImage>().Length==0,"Repeated source art in type list");
            Check(menu.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("TypeOption_"))<24,"Type list did not page");
            var canvas=GameObject.Find("Canvas").GetComponent<RectTransform>();var corners=new Vector3[4];menu.GetWorldCorners(corners);
            Check(corners.All(c=>canvas.rect.Contains(canvas.InverseTransformPoint(c))),"Type list leaves canvas");
            Check(menu.GetComponentsInChildren<Text>().All(t=>!t.resizeTextForBestFit),"Type list resizes fonts");
            ScreenCapture.CaptureScreenshot(Output+"/"+language+"-types.png");yield return new WaitForEndOfFrame();
            int pages=0;while(Button("TypeNext").interactable&&pages++<100){Ray(Button("TypeNext"));yield return null;}
            Check(pages<100&&pages>0&&calls==0,"Pagination submitted a type or failed to end");
            Check(Button("TypeOption_"+(view.request.options.Length-1)).interactable,"Last native type omitted");
            Ray(Button("TypePrevious"));yield return null;Check(Button("TypeNext").interactable,"Previous page failed");
            var input=menu.GetComponentInChildren<InputField>();input.text="no-such-type-1006";yield return null;
            Check(!menu.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("TypeOption_")),"Empty filter has selectable type");
            input.text=SearchInsertionMenu.Label("Human").Split('·')[0].Trim();yield return null;
            int human=Array.FindIndex(view.request.options,o=>o.label=="Human");
            Check(Button("TypeOption_"+human).interactable,"Localized Human search omitted native type");
            input.text="Human";yield return null;
            ExecuteEvents.Execute(GameObject.Find("DismissAbilityChoices"),new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right},ExecuteEvents.pointerClickHandler);
            Check(calls==0&&menu.gameObject.activeSelf,"Mandatory type choice dismissed");
            var chosen=Button("TypeOption_"+human);Ray(chosen);chosen.onClick.Invoke();
            Check(calls==1&&last==human,"Type selection/duplicate changed original native index");
            ui.Present(view,1,false,submit,null,"");yield return null;
            Check(!menu.GetComponentInChildren<InputField>().interactable&&menu.GetComponentsInChildren<Button>().All(b=>!b.interactable),"Read-only viewer can filter/choose");
            ui.Choose(human,view.request.requestId);Check(calls==1,"Read-only viewer submitted");
            view.request.requestId+=10000;ui.Present(view,1,true,submit,null,"");yield return null;
            ui.Choose(human,view.request.requestId-1);Check(calls==1,"Stale type request submitted");
            ui.Choose(human,view.request.requestId);Check(calls==2,"Current type blocked after stale rejection");
            foreach(var sample in new[]{"choice-18-seat1.json","choice-20-seat1.json"}) {
                view=Load(sample);ui.Present(view,1,true,submit,null,"");yield return new WaitForSecondsRealtime(.2f);
                int index=Array.FindIndex(view.request.options,o=>sample.Contains("18")?o.label.Contains("one mana of any color"):o.label=="RED");
                int before=calls;Ray(Button("Option_"+index));Check(calls==before+1&&last==index,"Ability/color consumer callback changed");
            }
        }
    }
}
#endif
