#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class StackChoicePresentationValidation
{
    const string Output=".tools/stack-choice-review-1004";
    static bool running;
    static void Check(bool b,string s){if(!b)throw new Exception(s);}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.45f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Click(Button b){var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.TransformPoint(((RectTransform)b.transform).rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked corner control");ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    public static void Run(){if(running)return;Check(Application.isPlaying,"Requires Play Mode");running=true;Directory.CreateDirectory(Output);var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("StackChoiceValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){string locale=GamePreferences.Language;var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=banner!=null&&banner.enabled;if(banner!=null)banner.enabled=false;var it=Validate(ui);File.WriteAllText(Output+"/result.txt","RUNNING");try{while(true){object next;try{if(!it.MoveNext())break;next=it.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: actual stack snapshots; three locales; chosen-only modal effects/original identity after filtering; paid Kicker/Evoke/Entwine/Replicate; plain casting; art crop, opaque rules, fixed font and bounded scroll; same-seat/opponent stack visibility; stable settings-side View/Return slot by raycasts; shared button contrast/border; synthetic end-review corner slot. Not all cards, copied modes, live network, or actual end lifecycle.");}finally{running=false;GamePreferences.SetLanguage(locale);if(banner!=null)banner.enabled=enabled;ui.RestoreBattlefieldWindows();}}
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}) {
            GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale not ready");
            foreach(string scenario in new[]{"spree-rich","spree-targets","choose-one","kicker","kicker-base","evoke","evoke-normal","entwine","entwine-base","replicate","offspring","offspring-base"}) {
                string path="Tools/Validation/Reports/ModeChoices/"+scenario+"-stack.json";
                var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,true,_=>{},null,"");yield return Frame();
                var entry=v.stackItems.First(s=>s.kind=="spell");var stack=GameObject.Find("StackCard_"+Array.IndexOf(v.stackItems,entry));Check(stack!=null,"Stack missing "+scenario);
                bool choices=(entry.chosenModes?.Length??0)>0||(entry.castingChoices?.Length??0)>0;
                var face=stack.transform.Find("ChosenSpellFace");Check(face!=null&&face.gameObject.activeSelf==choices||face==null&&!choices,"Stack choices visibility "+scenario);
                if(choices) {
                    var body=face.GetComponentsInChildren<Text>().First(t=>t.name=="Effects");
                    Check(!body.resizeTextForBestFit&&!string.IsNullOrWhiteSpace(body.text)&&body.transform.Find("Symbols").GetComponent<TMPro.TextMeshProUGUI>().GetPreferredValues(body.transform.Find("Symbols").GetComponent<TMPro.TextMeshProUGUI>().text,body.rectTransform.rect.width,10000).y<=body.rectTransform.rect.height+1,"Rules clipped or shrinking "+scenario);
                    Check(face.Find("RulesFrame/ChosenRules").GetComponent<Image>().color.a==1,"Transparent rules");
                    var art=face.Find("CardHeader").GetComponent<RawImage>();Check(art.texture!=null&&art.uvRect.yMin>=.449f,"Missing/corrupt card crop "+scenario);
                    if((entry.chosenModes?.Length??0)>0) {
                        Check(!body.text.StartsWith("•")&&!body.text.StartsWith("・")&&!body.text.StartsWith("\n"),"Added bullet or leading blank line");
                        var method=typeof(ForgeBattlefieldController).GetMethod("ModeRules",BindingFlags.NonPublic|BindingFlags.Instance);
                        string expected=string.Join("\n\n",entry.chosenModes.Select(m=>((string)method.Invoke(ui,new object[]{entry.source,new ForgeOption{order=m.order,label=m.description},true})).TrimStart(' ','\t','\r','\n','•','・').TrimEnd()));
                        Check(body.text==expected,"Printed unchosen modes shown");
                    }
                    if(locale=="zh-CN"&&scenario=="spree-targets")Check(body.text.Contains("+1/+1")&&!body.text.Contains("消灭目标"),"Filtered mode mismatched");
                    if((entry.castingChoices?.Length??0)>0) {
                        var heading=face.GetComponentsInChildren<Text>().First(t=>t.name=="CastingChoices");Check(!string.IsNullOrEmpty(heading.text)&&heading.preferredWidth<=heading.rectTransform.rect.width+1,"Cost badge clipped");
                        if(locale=="zh-CN")Check(heading.text.Contains(scenario=="kicker"?"增幅":scenario=="evoke"?"呼魂":scenario=="entwine"?"打包":scenario=="offspring"?"繁生":"覆诵"),"Cost name not localized "+heading.text);
                        if(locale=="ja-JP")Check(!heading.text.Contains("Kicker")&&!heading.text.Contains("Evoke")&&!heading.text.Contains("REPLICATE"),"English badge in Japanese");
                    }
                    var pointer=new PointerEventData(EventSystem.current);ExecuteEvents.Execute(stack,pointer,ExecuteEvents.pointerEnterHandler);yield return Frame();
                    Check(GameObject.Find("InspectPanel")!=null&&GameObject.Find("InspectPanel").transform.Find("Art/ChosenSpellFace").gameObject.activeSelf,"Chosen stack hover absent");
                    var rules=GameObject.Find("InspectPanel").transform.Find("Rules").GetComponent<Text>();Check(rules.text.Contains(body.text),"Hover still shows all printed alternatives");
                    ExecuteEvents.Execute(stack,pointer,ExecuteEvents.pointerExitHandler);yield return Frame();
                }
                ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-"+scenario+".png");yield return new WaitForEndOfFrame();
            }
            var prompt=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(".tools/battlefield-view-review-1004/input/modes.json"));ui.Present(null,1,false,null,null,"");Check(GameObject.Find("InspectPanel")==null,"Stale inspect survives reset");ui.Present(prompt,1,true,_=>{},null,"");yield return Frame();
            var view=GameObject.Find("ViewBattlefield").GetComponent<Button>();var rect=(RectTransform)view.transform;Vector3 before=rect.position;
            var gear=(RectTransform)GameObject.Find("SettingsButton").transform;var vc=new Vector3[4];var gc=new Vector3[4];rect.GetWorldCorners(vc);gear.GetWorldCorners(gc);Check(vc[0].x>gc[2].x&&Mathf.Abs(vc[1].y-gc[1].y)<5,"View not beside settings");
            foreach(var button in GameObject.Find("CardAbilityChoices").GetComponentsInChildren<Button>().Where(b=>!b.name.StartsWith("Mode_"))) {
                var image=button.GetComponent<Image>();Check(image.color.g>=.26f&&image.color.b>=.36f&&button.GetComponent<Outline>()!=null,"Low contrast control "+button.name);
                var label=button.GetComponentInChildren<Text>();Check(label.color.r>.9f,"Dim caption");
            }
            ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-corner-choice.png");yield return new WaitForEndOfFrame();Click(view);yield return Frame();var back=GameObject.Find("ReturnSelection").GetComponent<Button>();Check(Vector3.Distance(back.transform.position,before)<.1f,"Return moved corner slot");Click(back);yield return Frame();
        }
        // End-review button is a visibility/placement adapter, not an ended match.
        ui.Present(null,1,false,null,null,"");var canvas=GameObject.Find("Canvas").transform;var end=ReplayWidgets.Panel(canvas,"EndReviewAdapter",0,0,300,100,Color.black);end.gameObject.SetActive(false);
        var leave=ReplayWidgets.Button(canvas,"离开战场",0,0,220,()=>{});leave.name="LeaveReview";
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;typeof(ForgeBattlefieldController).GetField("endDialog",flags).SetValue(ui,end);typeof(ForgeBattlefieldController).GetField("leaveReviewButton",flags).SetValue(ui,leave);
        yield return Frame();Check(leave.gameObject.activeSelf&&((RectTransform)leave.transform).anchoredPosition==((RectTransform)GameObject.Find("Canvas").transform.Find("ReturnSelection")).anchoredPosition,"Leave not in shared slot");
        typeof(ForgeBattlefieldController).GetField("endDialog",flags).SetValue(ui,null);typeof(ForgeBattlefieldController).GetField("leaveReviewButton",flags).SetValue(ui,null);UnityEngine.Object.Destroy(end.gameObject);UnityEngine.Object.Destroy(leave.gameObject);
    }
}
#endif
