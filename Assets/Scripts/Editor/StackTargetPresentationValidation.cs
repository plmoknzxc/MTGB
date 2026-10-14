#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Native seat snapshots verify the shared read-only stack detail consumer.
public static class StackTargetPresentationValidation
{
    const string Root=".tools/stack-target-preview-1007",Output=Root+"/ui";
    [Serializable] class Meta {public string folder,status;}
    static Transform Panel=>GameObject.Find("Canvas").transform.Find("InspectPanel");
    static bool running;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static ForgeSeatView Read(string name,string state,bool other){var m=JsonUtility.FromJson<Meta>(File.ReadAllText(Root+"/native/"+name+"-result.json"));Check(m.status=="PASS","Native "+name);return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(m.folder+"/"+state+(other?"-other":"")+".json"));}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Hover(GameObject root){Panel.gameObject.SetActive(false);root.GetComponent<ForgeBattlefieldCardPointer>().ResetHover();var rect=(RectTransform)root.transform;var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));var data=new PointerEventData(EventSystem.current){position=point};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Check(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(root.transform),"Unblocked actual stack ray "+root.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerEnterHandler);}
    static void Pass(string name)=>File.AppendAllText(Output+"/steps.log",name+" PASS\n");
    public static void Run(){Check(Application.isPlaying&&!running,"Play/runner state");running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/steps.log","");File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("TargetPreviewValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var frames=new Stack<IEnumerator>();frames.Push(Validate(ui));try{while(frames.Count>0){object next;try{var it=frames.Peek();if(!it.MoveNext()){(it as IDisposable)?.Dispose();frames.Pop();continue;}next=it.Current;if(next is IEnumerator child){frames.Push(child);continue;}}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS native confirmed target snapshots, both seats, exact same-name identities/multiple/exile/player/stack/departed targets, shared hover, three locales; read-only, draft isolation, old/null cleanup and bounded scroll adapters. No full network claim.");}finally{while(frames.Count>0)(frames.Pop() as IDisposable)?.Dispose();running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui){bool enabled=ui.enabled;ui.enabled=false;string language=GamePreferences.Language;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
                GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale readiness");yield return Frame();
                foreach(string name in locale=="zh-CN"?new[]{"grave-single","grave-multiple","exile-single","player","stack","departed"}:new[]{"grave-single","exile-single"})foreach(bool other in locale=="zh-CN"?new[]{false,true}:new[]{true}){
                    ui.Present(null,1,false,null,null,"");yield return Frame();var v=Read(name,name=="stack"?"response":name=="departed"?"departed":"confirmed",other);int calls=0;ui.Present(v,other?2:1,false,_=>calls++,null,"");yield return Frame();Hover(GameObject.Find("StackCard_0"));yield return Frame();
                    var details=Panel.Find("StackTargets");Check(details!=null&&details.gameObject.activeInHierarchy,"Target details missing");var targets=v.stackItems[0].targets;Check(details.Find("Title").GetComponent<Text>().text.EndsWith(" · "+targets.Length),"Target count");var tiles=details.Find("Cards/Content");Check(tiles.childCount==targets.Length,"Target identity count");Check(details.GetComponentsInChildren<Button>().Length==0,"Target preview must be read-only");
                    for(int i=0;i<targets.Length;i++){var t=targets[i];var tile=tiles.GetChild(i);Check(tile.name=="Target_"+t.kind+"_"+t.id,"Target exact ID");var text=tile.Find("Location").GetComponent<Text>();Check(!text.resizeTextForBestFit,"Target font changes");if(t.kind!="player"){string zone=t.zone=="Graveyard"?(locale=="zh-CN"?"坟墓场":locale=="ja-JP"?"墓地":"Graveyard"):t.zone=="Exile"?(locale=="zh-CN"?"放逐区":locale=="ja-JP"?"追放領域":"Exile"):t.zone=="Stack"?(locale=="zh-CN"?"堆叠":locale=="ja-JP"?"スタック":"Stack"):t.zone;Check(text.text.Contains(zone),"Zone locale "+text.text);Check(tile.Find("Art").GetComponent<Image>().sprite!=null,"Target card art");}if(t.leftZone)Check(text.text.Contains("已离开原区域"),"Departed target mark");ExecuteEvents.ExecuteHierarchy(tile.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);}
                    Check(calls==0,"Readonly target preview submitted choice");var corners=new Vector3[4];((RectTransform)Panel).GetWorldCorners(corners);Check(corners.All(p=>p.x>=-1&&p.y>=-1&&p.x<=Screen.width+1&&p.y<=Screen.height+1),"Inspect target panel outside viewport");
                    if(locale=="zh-CN"&&other&&(name=="grave-single"||name=="exile-single"||name=="grave-multiple")){ScreenCapture.CaptureScreenshot(Output+"/opponent-"+name+".png");yield return Frame();}
                    Pass(locale+" "+name+" seat="+(other?2:1)+" exact IDs/zone/read-only/fit");
                    ui.Present(Read(name,"resolved",other),other?2:1,false,null,null,"");yield return Frame();Check(!Panel.Find("StackTargets").gameObject.activeInHierarchy,"Resolved stack retains target details");
                }
            }
            GamePreferences.SetLanguage("zh-CN");yield return Frame();var adapter=Read("grave-single","confirmed",true);ui.Present(adapter,2,false,null,null,"");yield return Frame();Hover(GameObject.Find("StackCard_0"));yield return Frame();var target=adapter.stackItems[0].targets[0];adapter.stackItems[0].targets=Enumerable.Repeat(target,5).Select((t,i)=>new ForgeStackTarget{kind=t.kind,id=t.id+i,card=t.card,ownerSeat=t.ownerSeat,zone=t.zone}).ToArray();ui.Present(adapter,2,false,null,null,"");yield return Frame();var scroll=Panel.Find("StackTargets/Cards").GetComponent<ScrollRect>();Check(scroll.horizontal&&scroll.content.rect.width>scroll.viewport.rect.width,"Many-target horizontal scroll");scroll.horizontalNormalizedPosition=1;yield return Frame();float offset=scroll.content.anchoredPosition.x;ui.Present(adapter,2,false,null,null,"");yield return Frame();Check(Mathf.Abs(scroll.content.anchoredPosition.x-offset)<.1f,"Refresh reset target scroll");Pass("adapter five targets scroll and unchanged refresh position");
            adapter.stackItems[0].targets=null;ui.Present(adapter,2,false,null,null,"");yield return Frame();Check(!Panel.Find("StackTargets").gameObject.activeInHierarchy,"Old wire null targets retained");Pass("adapter old wire null targets cleanup");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(language);if(module!=null)module.enabled=input;for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
