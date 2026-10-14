#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ContinuousStatePresentationValidation
{
    const string Output=".tools/continuous-state-1007/ui";
    [Serializable] class Meta {public string folder,status;}
    static bool running;
    static Transform Panel=>GameObject.Find("Canvas").transform.Find("InspectPanel");
    static GameObject Card(int id)=>GameObject.Find("Card_"+id);
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static ForgeSeatView Read(string name,string label){var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/continuous-state-1007/native/"+name+"-result.json"));Check(meta.status=="PASS","Native "+name);return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(meta.folder+"/"+label+".json"));}
    static void Hover(int id){Panel.gameObject.SetActive(false);var root=Card(id);Check(root!=null,"Missing "+id);root.GetComponent<ForgeBattlefieldCardPointer>().ResetHover();var rect=(RectTransform)root.transform;var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<ForgeBattlefieldCardPointer>()==root.GetComponent<ForgeBattlefieldCardPointer>(),"Blocked hover "+id+": "+string.Join(",",hits.Take(3).Select(h=>h.gameObject.name)));ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerEnterHandler);}
    static void Click(Button button){var rect=(RectTransform)button.transform;var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Blocked original/return");ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);}
    static string Text(string node)=>Panel.Find(node).GetComponent<Text>().text;
    static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Output+"/"+GamePreferences.Language+"-"+name+".png");
    public static void Run(){Check(Application.isPlaying&&!running,"Play/runner state");running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/steps.log","");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ContinuousValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var stack=new Stack<IEnumerator>();stack.Push(Validate(ui));try{while(stack.Count>0){object current;try{var e=stack.Peek();if(!e.MoveNext()){(e as IDisposable)?.Dispose();stack.Pop();continue;}current=e.Current;if(current is IEnumerator child){stack.Push(child);continue;}}catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}yield return current;}File.WriteAllText(Output+"/result.txt","PASS actual devotion same-ID type/layout transitions and badge/detail in three locales; primary-language actual base/boost/counter/expiry and timestamp detail with original/return rays, dependency land rules/identity and multi-source refresh. Existing payment/target and full characteristic-language contracts reused. UI increment only, no full/network PASS.");}finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui){bool enabled=ui.enabled;ui.enabled=false;string language=GamePreferences.Language;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
                GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Language");
                foreach(string name in new[]{"devotion-mono","devotion-dual"}){
                    ui.Present(null,1,false,null,null,"");Vector3 original=Vector3.zero;int identity=0;
                    foreach(string state in new[]{"before","after-0","after-1"}){
                        Panel.gameObject.SetActive(false);var v=Read(name,state);ui.Present(v,1,false,null,null,"");yield return Frame();var god=v.players[0].battlefield.Single(c=>c.devotionRequired>0);Check(identity==0||identity==god.id,"God identity changed");identity=god.id;var root=Card(god.id);Check(root!=null&&UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None).Count(p=>p.gameObject.name==root.name)==1,"Duplicated/missing changed type card");
                        var badge=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Devotion");Check(badge.Find("Value").GetComponent<Text>().text==god.devotion+"/"+god.devotionRequired,"Battlefield devotion badge");
                        var position=root.transform.position;if(state=="before")original=position;else Check(state=="after-0"?Vector3.Distance(position,original)>30:Vector3.Distance(position,original)<3,"Type transition placement "+name+" "+state);
                        Hover(god.id);yield return Frame();Check(Panel.Find("Art/Devotion/Value").GetComponent<Text>().text==god.devotion+"/"+god.devotionRequired,"Detail devotion badge");
                        Check(god.creature?!string.IsNullOrEmpty(Text("Footer")):string.IsNullOrEmpty(Text("Footer")),"Noncreature god footer");
                        var type=Panel.Find("TypeLine").GetComponentInChildren<TMPro.TextMeshProUGUI>();Check(type!=null&&!type.enableAutoSizing,"Type fixed size");Check(god.creature?!type.text.Contains("<s "):type.text.Contains("<s "),"Current removed creature type display");
                        Capture(name+"-"+state);yield return Frame();File.AppendAllText(Output+"/steps.log",locale+" "+name+" "+state+" identity/type/layout/badges/hover PASS\n");
                    }
                }
            }
            GamePreferences.SetLanguage("zh-CN");yield return Frame();ui.Present(null,1,false,null,null,"");
            foreach(string state in new[]{"before","after-0","after-1","after-2","expired"}){
                Panel.gameObject.SetActive(false);var v=Read("pt-counters-duration",state);ui.Present(v,1,false,null,null,"");yield return Frame();var bear=v.players[0].battlefield.Single(c=>c.name=="Grizzly Bears");Hover(bear.id);yield return Frame();
                var footer=Text("Footer");Check(state=="before"?footer.StartsWith("2/2"):footer.Contains("#63E69B")&&!footer.Contains("#70BFFF"),"Ordinary boost/counter color "+state);
                bool hasCounter=state is "after-2" or "expired";var counter=Card(bear.id).GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Counter_P1P1");Check(hasCounter?counter!=null&&counter.Find("Count").GetComponent<Text>().text=="1":counter==null,"Counter after layer/expiry");
                Capture("pt-"+state);yield return Frame();
                var toggle=Panel.Find("OriginalState").GetComponent<Button>();Click(toggle);yield return Frame();Check(Text("Footer").StartsWith("2/2"),"Original printed base after boost/layer");Click(toggle);yield return Frame();Check(Text("Footer")==footer,"Return current stats after layer");
                File.AppendAllText(Output+"/steps.log","zh-CN pt "+state+" current/original stats/counter/color PASS\n");
            }
            foreach(string name in new[]{"timestamp-frog-first","timestamp-ovine-first"}){
                Panel.gameObject.SetActive(false);ui.Present(null,1,false,null,null,"");var v=Read(name,"resolved");ui.Present(v,1,false,null,null,"");yield return Frame();var bear=v.players[0].battlefield.Single(c=>c.name=="Grizzly Bears");Hover(bear.id);yield return Frame();Check(Text("Footer").Contains("#70BFFF")&&!Text("Footer").Contains("#63E69B"),"Base rewrite color "+name);Capture(name);yield return Frame();File.AppendAllText(Output+"/steps.log","zh-CN "+name+" rewritten base color PASS\n");
            }
            foreach(string name in new[]{"dependency-urborg-first","dependency-moon-first"}){
                ui.Present(null,1,false,null,null,"");foreach(string state in new[]{"after-0","after-1"}){
                    Panel.gameObject.SetActive(false);var v=Read(name,state);ui.Present(v,1,false,null,null,"");yield return Frame();var land=v.players[0].battlefield.Single(c=>c.name=="Urborg, Tomb of Yawgmoth");Hover(land.id);yield return Frame();Check(Text("TypeLine").Contains(state=="after-0"?"山脉":"沼泽"),"Dependency current land type");Check(!string.IsNullOrEmpty(Text("Rules")),"Dependency current land rules");Capture(name+"-"+state);yield return Frame();File.AppendAllText(Output+"/steps.log","zh-CN "+name+" "+state+" current land detail/hover PASS\n");
                }
            }
            ui.Present(null,1,false,null,null,"");foreach(string state in new[]{"after-0","after-1","after-2"}){
                Panel.gameObject.SetActive(false);var v=Read("multiple-sources",state);ui.Present(v,1,false,null,null,"");yield return Frame();var bear=v.players[0].battlefield.Single(c=>c.name=="Grizzly Bears");Hover(bear.id);yield return Frame();Check(Text("Footer").Contains("#63E69B")&&!Text("Footer").Contains("#70BFFF"),"Multi-source boost color");Capture("multiple-"+state);yield return Frame();File.AppendAllText(Output+"/steps.log","zh-CN multiple "+state+" source removal/current boost PASS\n");
            }
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(language);if(module!=null)module.enabled=input;for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
