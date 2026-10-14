#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class FaceDownLifecyclePresentationValidation
{
    const string Output=".tools/facedown-lifecycle-1007/ui";
    [Serializable] class Meta {public string folder,status;}
    static bool running;
    static HashSet<string> completed=new();
    static bool Done(string name)=>completed.Contains(name+" PASS");
    static Transform Panel=>GameObject.Find("Canvas").transform.Find("InspectPanel");
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static ForgeSeatView Read(string name,string label,bool other=false){var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/facedown-lifecycle-1007/native/"+name+"-result.json"));Check(meta.status=="PASS","Native "+name);return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(meta.folder+"/"+label+(other?"-other":"")+".json"));}
    static GameObject Card(int id)=>GameObject.Find("Card_"+id);
    static Transform CurrentBorder(GameObject root)=>root.transform.Find("Visual").Cast<Transform>().Single(t=>t.name=="Border"&&t.gameObject.activeSelf);
    static RaycastResult Ray(GameObject root){var rect=(RectTransform)root.transform;var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<ForgeBattlefieldCardPointer>()==root.GetComponent<ForgeBattlefieldCardPointer>(),"Blocked card "+root.name+": "+string.Join(",",hits.Take(3).Select(h=>h.gameObject.name)));return hits[0];}
    static void Hover(GameObject root){Panel.gameObject.SetActive(false);root.GetComponent<ForgeBattlefieldCardPointer>().ResetHover();var hit=Ray(root);ExecuteEvents.ExecuteHierarchy(hit.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);}
    static void Click(GameObject root){var hit=Ray(root);ExecuteEvents.ExecuteHierarchy(hit.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);}
    static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");
    static void Passed(string text){File.AppendAllText(Output+"/steps.log",text+" PASS\n");completed.Add(text+" PASS");}
    public static void Run()=>Start(false);
    public static void Resume()=>Start(true);
    static void Start(bool resume){Check(Application.isPlaying&&!running,"Play/runner state");running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");if(!resume)File.WriteAllText(Output+"/steps.log","");completed=new HashSet<string>(File.ReadAllLines(Output+"/steps.log"));var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("FaceDownValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var stack=new Stack<IEnumerator>();stack.Push(Validate(ui));try{while(stack.Count>0){object current;try{var e=stack.Peek();if(!e.MoveNext()){(e as IDisposable)?.Dispose();stack.Pop();continue;}current=e.Current;if(current is IEnumerator child){stack.Push(child);continue;}}catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}yield return current;}File.WriteAllText(Output+"/result.txt","PASS actual manifest/cloak/Ring snapshots: both-view private/public art and identity, noncreature action absence, exact turn-up card ray, stable public turn-up/ward/ring and attachment lifecycle; real target ray during flip/duplicate suppression and early disappearance cleanup. Existing payment/choice/language contracts reused; First registered private-face and flip-lifecycle UI contract; no network/full PASS.");}finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui){bool enabled=ui.enabled;ui.enabled=false;string language=GamePreferences.Language;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{
            foreach(string name in new[]{"manifest-creature","manifest-noncreature","cloak-creature","cloak-noncreature","ring-facedown"}){
                string state=name=="ring-facedown"?"after-1":"after-0";
                foreach(bool other in new[]{false,true}){
                    if(Done(name+(other?" public":" private")+" face/art/rules/ring"))continue;
                    ui.Present(null,1,false,null,null,"");Panel.gameObject.SetActive(false);var v=Read(name,state,other);int seat=other?2:1;ui.Present(v,seat,false,null,null,"");yield return Frame();var hidden=v.players[0].battlefield.Single(c=>c.faceDown);var root=Card(hidden.id);
                    Check(root.GetComponentsInChildren<RawImage>().Any(i=>i.name=="FaceDownBack"&&i.texture!=null),"Public battlefield back "+name);
                    Check(!other?!string.IsNullOrEmpty(hidden.knownFacePrintingKey):string.IsNullOrEmpty(hidden.knownFacePrintingKey),"Known identity projection");
                    Check((root.GetComponentsInChildren<RingBearerIcon>().Length>0)==(name=="ring-facedown"),"Hidden Ring marker");
                    Hover(root);yield return Frame();var art=Panel.Find("Art").GetComponent<Image>();
                    if(!other){CardImageLibrary.TryLoadPrintingKey(hidden.knownFacePrintingKey,out var expected);Check(expected!=null&&art.sprite==expected,"Private peek actual printing");Check(Panel.Find("Name").GetComponent<Text>().text==hidden.knownFaceName,"Private peek name");Check(Panel.Find("Rules").GetComponent<Text>().text.Contains(hidden.knownFaceRules.Replace("\\n","\n")),"Private peek rules");}
                    else{CardImageLibrary.TryLoadCardBack(out var expected);Check(expected!=null&&art.sprite==expected,"Opponent detail exposed face");Check(!Panel.Find("Rules").GetComponent<Text>().text.Contains("仅你可见")&&Panel.Find("SetNumber").GetComponent<Text>().text=="","Opponent identity detail exposed");}
                    Check(!Panel.Find("OriginalState").gameObject.activeSelf,"Hidden original toggle");Capture(name+(other?"-public":"-private"));yield return Frame();Passed(name+(other?" public":" private")+" face/art/rules/ring");
                }
                Panel.gameObject.SetActive(false);var action=Read(name,name=="ring-facedown"?"action-2":"action-1");var down=action.players[0].battlefield.Single(c=>c.faceDown);int chosen=-1,calls=0;ui.Present(action,1,true,i=>{chosen=i;calls++;},null,"");yield return Frame();var pointer=Card(down.id);
                if(!Done(name+" native turn-up availability/exact ray")){
                    if(!name.EndsWith("noncreature")){Click(pointer);Check(calls==1&&action.request.options[chosen].id=="card:"+down.id,"Turn-up exact card ray");Click(pointer);Check(calls==1,"Duplicate turn-up");}
                    else{Check(!action.request.options.Any(o=>o.id=="card:"+down.id&&!o.disabled),"Noncreature turn-up offered");Click(pointer);Check(calls==0,"Noncreature turn-up submitted");}
                    Passed(name+" native turn-up availability/exact ray");
                }
                if(!name.EndsWith("noncreature")){
                    foreach(bool other in new[]{false,true}){
                        if(Done(name+(other?" other":" own")+" stable public up/cleanup/ring"))continue;
                        Panel.gameObject.SetActive(false);var up=Read(name,name=="ring-facedown"?"after-2":"after-1",other);ui.Present(up,other?2:1,false,null,null,"");yield return Frame();var current=up.players[0].battlefield.Single(c=>c.id==down.id);var root=Card(current.id);Check(!current.faceDown&&root.transform.Find("RevealBack")==null,"Flip back not removed");Check(CurrentBorder(root).localScale==Vector3.one,"Flip front not restored");Hover(root);yield return Frame();Check(Panel.Find("Name").GetComponent<Text>().text!="Face-down card"&&!Panel.Find("Rules").GetComponent<Text>().text.Contains("仅你可见"),"Private detail stale after public reveal");Check((root.GetComponentsInChildren<RingBearerIcon>().Length>0)==(name=="ring-facedown"),"Public Ring marker lost");Capture(name+(other?"-up-other":"-up-own"));yield return Frame();Passed(name+(other?" other":" own")+" stable public up/cleanup/ring");
                    }
                }
            }
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"})foreach(bool other in new[]{false,true}){
                string label=locale+(other?" public":" private")+" first-frame symbols/font/art";if(Done(label))continue;
                GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Language resource");
                ui.Present(null,1,false,null,null,"");yield return Frame();var v=Read("cloak-creature","after-0",other);ui.Present(v,other?2:1,false,null,null,"");yield return Frame();var card=v.players[0].battlefield.Single(c=>c.faceDown);Hover(Card(card.id));yield return Frame();var rules=Panel.Find("Rules").GetComponentInChildren<TMPro.TextMeshProUGUI>();Check(rules!=null&&!rules.enableAutoSizing&&rules.font==GamePreferences.TmpFont,"Private/public fixed locale font");Check(Panel.Find("Art").GetComponent<Image>().sprite!=null,"Locale face art");Check(!other?!string.IsNullOrEmpty(card.knownFacePrintingKey):!rules.text.Contains("仅你可见"),"Locale private identity");Capture(locale+(other?"-public-first":"-private-first"));yield return Frame();Passed(label);
            }
            GamePreferences.SetLanguage(language);yield return Frame();
            // Same-ID target request arrived after the real turn-up and before
            // actual Unsummon. Click its card while the UI flip is in progress.
            ui.Present(null,1,false,null,null,"");yield return Frame();Panel.gameObject.SetActive(false);var before=Read("manifest-creature","after-0");ui.Present(before,1,false,null,null,"");yield return Frame();int identity=before.players[0].battlefield.Single(c=>c.faceDown).id;var oldRoot=Card(identity);var target=Read("manifest-creature","targets-2-0");int selected=-1,total=0;ui.Present(target,1,true,i=>{selected=i;total++;},null,"");var animated=Card(identity);File.AppendAllText(Output+"/animation.log","same root="+(oldRoot==animated)+" children="+string.Join(",",animated.GetComponentsInChildren<Transform>().Select(t=>t.name))+" scale="+CurrentBorder(animated).localScale+"\n");Check(animated.transform.Find("RevealBack")!=null&&CurrentBorder(animated).localScale.x<1,"Actual flip not started");Click(animated);Check(total==1&&target.request.options[selected].id=="card:"+identity,"Target during flip");Click(animated);Check(total==1,"Animation target duplicate");yield return Frame();Check(animated.transform.Find("RevealBack")==null,"Flip completion");Capture("animation-target");yield return Frame();Passed("actual target ray during flip/exact ID/duplicate/completion");
            // Destroy the battlefield view during a second actual-state flip;
            // the native returned card may already have a separate hand view.
            Panel.gameObject.SetActive(false);ui.Present(null,1,false,null,null,"");ui.Present(before,1,false,null,null,"");yield return Frame();ui.Present(Read("manifest-creature","after-1"),1,false,null,null,"");var departed=Card(identity);Check(departed.transform.Find("RevealBack")!=null,"Early-destroy flip missing");ui.Present(Read("manifest-creature","after-2"),1,false,null,null,"");yield return Frame();Check(departed==null,"Departed battlefield root retained");Check(!UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t=>t.name=="RevealBack"),"Orphan flip back");Capture("animation-departure");yield return Frame();Passed("actual zone departure during flip/old root and orphan cleanup");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(language);if(module!=null)module.enabled=input;for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
