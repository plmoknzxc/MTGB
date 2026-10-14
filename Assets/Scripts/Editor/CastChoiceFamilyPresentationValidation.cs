#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CastChoiceFamilyPresentationValidation
{
    const string Output=".tools/cast-choice-families-ui-1004";
    static bool running;
    static void Check(bool b,string reason){if(!b)throw new Exception(reason);}
    public static void Run(){Check(Application.isPlaying,"Requires Play Mode");if(running)return;Directory.CreateDirectory(Output);var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("CastChoiceFamilyValidation").AddComponent<ForgeBattlefieldController>();running=true;ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var it=Validate(ui);string locale=GamePreferences.Language;File.WriteAllText(Output+"/result.txt","RUNNING");try{while(true){object next;try{if(!it.MoveNext())break;next=it.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: 25 native stack snapshots x three locales; paid/unpaid casting methods, counts, repeated original mode identity, X=0/3, no added bullets/leading blank, localized overload effect, fixed font/full content height, chosen hover and art. Snapshot UI, not network or all cards.");}finally{running=false;GamePreferences.SetLanguage(locale);}}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.32f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var nameMethod=typeof(ForgeBattlefieldController).GetMethod("CastingChoiceName",flags);
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}) {
            GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale not ready");
            foreach(string scenario in new[]{"multikicker","multikicker-base","buyback","buyback-base","bargain","bargain-base","gift","gift-base","overload","overload-base","dash","dash-base","escalate","escalate-base","conspire","conspire-base","casualty","casualty-base","squad","squad-base","confluence","xconfluence","xconfluence-base","profit","profit-base"}) {
                string input="Tools/Validation/Reports/CastChoiceFamilies/"+scenario+"-stack-seat1.json";
                var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(input));ui.Present(null,1,false,null,null,"");ui.Present(v,1,false,null,null,"");yield return Frame();
                var e=v.stackItems.First(s=>s.kind=="spell");var tile=GameObject.Find("StackCard_"+Array.IndexOf(v.stackItems,e));Check(tile!=null,"No stack card "+scenario);
                bool chosen=(e.chosenModes?.Length??0)>0||(e.castingChoices?.Length??0)>0;var face=tile.transform.Find("ChosenSpellFace");Check(face!=null&&face.gameObject.activeSelf==chosen||face==null&&!chosen,"Choice face gate "+scenario);
                if(chosen) {
                    var text=face.GetComponentsInChildren<Text>().First(t=>t.name=="Effects");Check(!text.resizeTextForBestFit&&text.transform.Find("Symbols").GetComponent<TMPro.TextMeshProUGUI>().GetPreferredValues(text.transform.Find("Symbols").GetComponent<TMPro.TextMeshProUGUI>().text,text.rectTransform.rect.width,10000).y<=text.rectTransform.rect.height+1,"Truncated text "+scenario);
                    Check(!text.text.StartsWith("•")&&!text.text.StartsWith("・")&&!char.IsWhiteSpace(text.text[0]),"Bullet or blank before selected effect "+scenario);
                    Check(face.Find("CardHeader").GetComponent<RawImage>().texture!=null,"No chosen art "+scenario);
                    if(scenario=="confluence"||scenario=="xconfluence")Check(text.text.Split(new[]{"\n\n"},StringSplitOptions.None).Length==3,"Repeated selected mode lost");
                    if(scenario=="overload")Check(locale=="zh-CN"?text.text.Contains("每个")&&!text.text.Contains("目标"):locale=="en-US"?text.text.Contains("each")&&!text.text.Contains("target"):!text.text.Contains("１体を対象とする")&&text.text.Contains("各クリーチャー"),"Printed targeted effect on overloaded spell");
                    if((e.castingChoices?.Length??0)>0) {
                        var heading=face.GetComponentsInChildren<Text>().First(t=>t.name=="CastingChoices");Check(heading.preferredWidth<=heading.rectTransform.rect.width+1,"Heading clipped "+scenario);
                        if(locale!="en-US")foreach(var cost in e.castingChoices)Check(!heading.text.Contains(cost.kind),"Native enum in localized heading "+scenario);
                        if(scenario=="multikicker"||scenario=="squad")Check(heading.text.Contains("×2"),"Repeat cost count missing");
                        if(scenario=="profit")Check(heading.text.Contains("3")&&!heading.text.Contains("×"),"Casualty threshold mistaken for repetitions");
                    }
                    var pointer=new PointerEventData(EventSystem.current);ExecuteEvents.Execute(tile,pointer,ExecuteEvents.pointerEnterHandler);yield return Frame();
                    var preview=GameObject.Find("InspectPanel");Check(preview!=null&&preview.transform.Find("Art/ChosenSpellFace").gameObject.activeSelf,"Chosen hover absent");
                    Check(preview.transform.Find("Rules").GetComponent<Text>().text.Contains(text.text),"Hover has different effects");ExecuteEvents.Execute(tile,pointer,ExecuteEvents.pointerExitHandler);
                }
                ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-"+scenario+".png");yield return new WaitForEndOfFrame();
            }
            // Enum aliases are naming checks, not actual casting/effect evidence.
            foreach(string kind in new[]{"Foretold","Plotted","MTMtE","Generic","AltCost","ReduceW"}) {
                string label=(string)nameMethod.Invoke(ui,new object[]{kind});Check(locale=="en-US"||!label.Contains(kind)&&!label.Contains("Reduce"),"Unlocalized alias "+kind);
            }
        }
    }
}
#endif
