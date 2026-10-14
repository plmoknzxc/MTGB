#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public static class BendingLifecyclePresentationValidation
{
    const string Output=".tools/bending-lifecycle-1007/ui";
    [Serializable] class Meta {public string folder,status;}
    static HashSet<string> completed=new();static bool running;
    static Transform Panel=>GameObject.Find("Canvas").transform.Find("InspectPanel");
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static bool Done(string label)=>completed.Contains(label+" PASS");
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static ForgeSeatView Read(string name,string state,bool other=false){var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/bending-lifecycle-1007/native/"+name+"-result.json"));Check(meta.status=="PASS","Native "+name);return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(meta.folder+"/"+state+(other?"-other":"")+".json"));}
    static GameObject Card(int id)=>GameObject.Find("Card_"+id);
    static string Text(string name)=>Panel.Find(name).GetComponent<Text>().text;
    static RaycastResult Ray(GameObject root){var rect=(RectTransform)root.transform;foreach(float y in new[]{.5f,.9f,.75f,.6f})foreach(float x in new[]{.5f,.25f,.75f}){var point=rect.TransformPoint(new Vector3(Mathf.Lerp(rect.rect.xMin,rect.rect.xMax,x),Mathf.Lerp(rect.rect.yMin,rect.rect.yMax,y),0));var screen=RectTransformUtility.WorldToScreenPoint(null,point);if(screen.x<0||screen.y<0||screen.x>Screen.width||screen.y>Screen.height)continue;var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screen},hits);if(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(root.transform))return hits[0];}throw new Exception("No visible unblocked ray point on "+root.name);}
    static void Click(GameObject root){var hit=Ray(root);ExecuteEvents.ExecuteHierarchy(hit.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);}
    static void Hover(GameObject root){Panel.gameObject.SetActive(false);root.GetComponent<ForgeBattlefieldCardPointer>().ResetHover();var hit=Ray(root);ExecuteEvents.ExecuteHierarchy(hit.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);}
    static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");
    static void Passed(string label){File.AppendAllText(Output+"/steps.log",label+" PASS\n");completed.Add(label+" PASS");}
    public static void Run()=>Start(false);public static void Resume()=>Start(true);
    static void Start(bool resume){Check(Application.isPlaying&&!running,"Play/runner state");running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");if(!resume)File.WriteAllText(Output+"/steps.log","");completed=new HashSet<string>(File.ReadAllLines(Output+"/steps.log"));var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("BendingValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var stack=new Stack<IEnumerator>();stack.Push(Validate(ui));try{while(stack.Count>0){object current;try{var e=stack.Peek();if(!e.MoveNext()){(e as IDisposable)?.Dispose();stack.Pop();continue;}current=e.Current;if(current is IEnumerator child){stack.Push(child);continue;}}catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}yield return current;}File.WriteAllText(Output+"/result.txt","PASS actual bending snapshots: land/creature/returned placement and counters; exile readiness/cancel/recast/disappearance and owner/readonly ray permissions; actual multi-zone card target footer and exact original choice ID. Existing characteristic languages/targets/payment contracts reused; UI increment, no network/full PASS.");}finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui){bool enabled=ui.enabled;ui.enabled=false;string language=GamePreferences.Language;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{
            GamePreferences.SetLanguage("zh-CN");yield return Frame();
            foreach(bool other in new[]{false,true}){
                if(new[]{"before","after-0","after-1"}.All(s=>Done("earth "+(other?"other ":"own ")+s+" identity/layout/counters/detail")))continue;
                Vector3 baseline=Vector3.zero;int identity=0;
                foreach(string state in new[]{"before","after-0","after-1"}){
                    string label="earth "+(other?"other ":"own ")+state+" identity/layout/counters/detail";
                    Panel.gameObject.SetActive(false);var v=Read("earth-destroy",state,other);var before=Read("earth-destroy","before",other);identity=before.players[0].battlefield.First(c=>c.name=="Forest").id;
                    ui.Present(v,other?2:1,false,null,null,"");yield return Frame();var root=Card(identity);Check(root!=null,"Earth land view missing");if(state=="before")baseline=root.transform.position;
                    if(Done(label))continue;
                    Check(UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None).Count(p=>p.gameObject.name==root.name)==1,"Duplicate changed-type land");
                    var stats=root.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Stats");var counter=root.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Counter_P1P1");
                    if(state=="after-0"){Check(stats!=null&&stats.Find("Value").GetComponent<Text>().text.Contains("4")&&counter?.Find("Count").GetComponent<Text>().text=="4","Earth 4/4 counter");Check(Vector3.Distance(root.transform.position,baseline)>30,"Animated land not in creature row");}
                    else Check(stats==null&&counter==null,"Returned land retains creature badges");
                    Hover(root);yield return Frame();Check(state=="after-0"?Text("TypeLine").Contains("生物")&&Text("Rules").Contains("敏捷"):!Text("TypeLine").Contains("生物")&&Text("Footer")=="","Earth current characteristics/detail reset");
                    Capture("earth-"+(other?"other-":"own-")+state);yield return Frame();Passed(label);
                }
            }
            if(!Done("earth second death has no stale battlefield view")){Panel.gameObject.SetActive(false);var v=Read("earth-destroy","resolved");int id=Read("earth-destroy","before").players[0].battlefield.First(c=>c.name=="Forest").id;ui.Present(v,1,false,null,null,"");yield return Frame();Check(Card(id)==null,"Dead original land retained");Passed("earth second death has no stale battlefield view");}
            foreach(string state in new[]{"after-0","cancelled"}){
                string label="air "+state+" exile shelf/exact ray/duplicate";if(Done(label))continue;
                Panel.gameObject.SetActive(false);ui.Present(null,1,false,null,null,"");yield return Frame();var v=Read("air-own-cancel",state);var card=v.playableExile.Single(c=>c.name=="Air Elemental");int selected=-1,calls=0;ui.Present(v,1,true,i=>{selected=i;calls++;},null,"");yield return Frame();var root=Card(card.id);Check(root!=null&&root.GetComponentsInChildren<Transform>().Any(t=>t.name=="PlayableZone"),"Exile shelf zone badge");Capture("air-"+state);yield return Frame();Click(root);Check(calls==1&&v.request.options[selected].id=="card:"+card.id,"Exile shelf exact identity");Check(!root.GetComponent<Button>().interactable&&!root.GetComponent<CanvasGroup>().blocksRaycasts,"Submitted hand must block another ray");ui.Choose(selected,v.request.requestId);Check(calls==1,"Duplicate exile cast");Passed(label);
            }
            foreach(string button in new[]{"CancelPayment","AutoPayment"}){
                string label="air actual alternative two payment "+button; if(Done(label))continue;
                Panel.gameObject.SetActive(false);var v=Read("air-own-cancel","payment-1");int selected=-1;ui.Present(v,1,true,i=>selected=i,null,"");yield return Frame();var prompt=GameObject.Find("ManaPaymentPrompt").GetComponent<TMPro.TextMeshProUGUI>();prompt.ForceMeshUpdate();Check(v.request.remainingManaCost=="{2}"&&prompt.textInfo.characterInfo.Take(prompt.textInfo.characterCount).Any(c=>c.elementType==TMPro.TMP_TextElementType.Sprite&&c.textElement is TMPro.TMP_SpriteCharacter sprite&&sprite.name=="2")&&!prompt.text.Contains("{2}"),"Alternative cost numeric sprite");var root=GameObject.Find(button);Capture(button);yield return Frame();Click(root);Check(selected>=0&&v.request.options[selected].id==(button=="CancelPayment"?"cancel":"ok"),"Payment choice exact ID");Passed(label);
            }
            foreach(string name in new[]{"air-own-cancel","air-permission-leaves"}){
                string label=name+" permission view cleanup";if(Done(label))continue;Panel.gameObject.SetActive(false);var before=Read(name,"after-0");int id=before.playableExile.Single(c=>c.name=="Air Elemental").id;var v=Read(name,"resolved");ui.Present(v,1,false,null,null,"");yield return Frame();var root=Card(id);Check(name=="air-own-cancel"?root!=null&&!root.GetComponentsInChildren<Transform>().Any(t=>t.name=="PlayableZone"):root==null,"Stale exile shelf");Passed(label);
            }
            if(!Done("air source departure preserves actual exile shelf")){Panel.gameObject.SetActive(false);var v=Read("air-source-gone","after-1");ui.Present(v,1,false,null,null,"");yield return Frame();var card=v.playableExile.Single(c=>c.name=="Air Elemental");Check(Card(card.id).GetComponentsInChildren<Transform>().Any(t=>t.name=="PlayableZone"),"Source departure lost exile shelf");Passed("air source departure preserves actual exile shelf");}
            foreach(bool other in new[]{false,true}){
                string label="air owner action "+(other?"owner ray":"other readonly");if(Done(label))continue;Panel.gameObject.SetActive(false);var v=Read("air-owner-permission","action-1",other);int id=Read("air-owner-permission","action-1",true).playableExile.Single(c=>c.name=="Air Elemental").id;int selected=-1,calls=0;ui.Present(v,other?2:1,other,i=>{selected=i;calls++;},null,"");yield return Frame();var root=Card(id);Check(other?root!=null:root==null&&v.playableExile.Length==0,"Owner-only exile shelf");Capture(other?"air-owner":"air-readonly");yield return Frame();if(other)Click(root);else ui.Choose(Array.FindIndex(v.request.options,o=>o.id=="card:"+id),v.request.requestId);Check(other?calls==1&&v.request.options[selected].id=="card:"+id:calls==0,"Exile owner/readonly permission");Passed(label);
            }
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
                string label=locale+" actual cross-zone target card/finish/header ray";if(Done(label))continue;
                GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale resources");
                Panel.gameObject.SetActive(false);var v=Read("air-source-gone","dialog-0");var option=v.request.options.Single(o=>o.card?.name=="Air Elemental");int calls=0,selected=-1;ui.Present(v,1,true,i=>{calls++;selected=i;},null,"");yield return Frame();
                Check(!UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(t=>t.gameObject.activeInHierarchy&&t.text.Contains("--CARDS ON BATTLEFIELD:--")),"Informational target header became an action");
                var tile=GameObject.Find("ChoiceCard_"+option.card.id);Check(tile!=null,"Cross-zone target tile");Capture(locale+"-cross-zone");yield return Frame();var art=tile.transform.Find("Artwork");Click(art.gameObject);Check(calls==1&&v.request.options[selected].id==option.id,"Cross-zone card exact original choice ID");Passed(label);
            }
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
                string label=locale+" optional cross-zone finish original ID/localized ray";if(Done(label))continue;
                GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale resources");
                Panel.gameObject.SetActive(false);var v=Read("air-decline","dialog-0");int selected=-1,calls=0;ui.Present(v,1,true,i=>{selected=i;calls++;},null,"");yield return Frame();
                int index=Array.FindIndex(v.request.options,o=>o.label=="[FINISH TARGETING]");var button=GameObject.Find("SelectionAction_"+index);Check(button!=null,"Optional finish missing");
                Check(button.GetComponentInChildren<Text>().text==(locale=="zh-CN"?"确认目标":locale=="ja-JP"?"対象を確定":"Confirm targets"),"Finish locale text");
                Check(GameObject.Find("SelectionAction_0")==null,"Header still actionable");Capture(locale+"-finish");yield return Frame();Click(button);Check(calls==1&&selected==index,"Finish original ID");ui.Choose(index,v.request.requestId);Check(calls==1,"Duplicate finish");Passed(label);
            }
            if(!Done("adjacent ordinary exile target card/original footer ray")){
                GamePreferences.SetLanguage("zh-CN");yield return Frame();Panel.gameObject.SetActive(false);var v=Read("air-permission-leaves","targets-1-0");var option=v.request.options.Single(o=>o.card?.name=="Air Elemental");int selected=-1;ui.Present(v,1,true,i=>selected=i,null,"");yield return Frame();
                var tile=GameObject.Find("ChoiceCard_"+option.card.id);Check(tile!=null,"Adjacent target missing");var footer=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b=>b.gameObject.activeInHierarchy&&b.name.StartsWith("SelectionAction_")).ToArray();Check(footer.Length==v.request.options.Count(o=>o.card==null||string.IsNullOrEmpty(o.card.name)),"Adjacent footer changed");Capture("adjacent-exile-target");yield return Frame();Click(tile.transform.Find("Artwork").gameObject);Check(selected>=0&&v.request.options[selected].id==option.id,"Adjacent exact target ID");Passed("adjacent ordinary exile target card/original footer ray");
            }
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(language);if(module!=null)module.enabled=input;for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
