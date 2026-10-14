#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CounterCostPresentationValidation
{
    const string Base=".tools/counter-cost-boundary-1008";
    static bool running;static int checks;static string output;static bool splitOnly;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
    public static void Run(bool onlySplit=false){
        if(running)return;Check(Application.isPlaying,"Play required");running=true;checks=0;
        splitOnly=onlySplit;output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(output);
        File.WriteAllText(Base+"/ui-current.txt",output);File.WriteAllText(output+"/result.txt","RUNNING");
        File.Copy("Assets/Scripts/Editor/CounterCostPresentationValidation.cs",output+"/driver.cs");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("CounterCostValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator routine){
        try{while(true){object next;try{if(!routine.MoveNext())break;next=routine.Current;}catch(Exception e){File.WriteAllText(output+"/result.txt","FAIL "+e);throw;}yield return next;}
        File.WriteAllText(output+"/result.txt","PASS: "+checks+" checks; "+(splitOnly?"zh-CN two-card exhausted identity/read-only count; cached0-to1 update and shortened Chinese instruction":"exact native counter picker/type dialog; three languages")+"; actual rays, additive count, cancel, duplicate/stale/nonacting guards and lifecycle. No global/network PASS.");}
        finally{running=false;}
    }
    static IEnumerator Frame(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static void Click(Button button){
        var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Button ray "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Validate(ForgeBattlefieldController ui){
        var language=GamePreferences.Language;var enabled=ui.enabled;ui.enabled=false;
        var module=EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();var moduleEnabled=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var banners=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=banners.Select(b=>b.enabled).ToArray();foreach(var b in banners)b.enabled=false;
        try{
          foreach(string locale in splitOnly?new[]{"zh-CN"}:new[]{"zh-CN","en-US","ja-JP"}){
            GamePreferences.SetLanguage(locale);float deadline=Time.realtimeSinceStartup+10;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(GamePreferences.Ready&&GamePreferences.Language==locale,"Exact locale ready");
            foreach(string test in splitOnly?new[]{"remove-two-counter-split-cancel-0","remove-two-counter-split-cancel-1"}:new[]{"remove-any-counter-cancel-0","remove-two-counter-cancel-0","remove-two-counter-cancel-1","choose-counter-type"}){
                var path=Base+"/ui-inputs/"+test+".json";File.Copy(path,output+"/"+locale+"-"+test+".json");
                var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));bool picker=view.request.kind=="InputSelectCardToRemoveCounter";
                var offered=picker?view.request.options.Where(o=>!o.disabled&&(o.id=="cancel"||o.id.StartsWith("card:"))).Select(o=>o.id).ToArray():view.request.options.Select(o=>o.id).ToArray();
                foreach(string id in offered){
                    int calls=0,last=-1,index=Array.FindIndex(view.request.options,o=>o.id==id);Check(index>=0&&!view.request.options[index].disabled,"Exact legal native option");
                    ui.Present(null,1,false,null,null,"");
                    Transform cachedPanel=null;
                    if(splitOnly&&view.request.selectedCount==1){
                        var initial=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Base+"/ui-inputs/remove-two-counter-split-cancel-0.json"));
                        ui.Present(initial,1,true,i=>{calls++;last=i;},null,"");yield return Frame();cachedPanel=GameObject.Find("CardSelection").transform;
                    }
                    ui.Present(view,1,true,i=>{calls++;last=i;},null,"");yield return Frame();
                    Button button;
                    if(picker){
                        var panel=GameObject.Find("CardSelection").transform;
                        if(cachedPanel!=null)Check(panel==cachedPanel,"Same-context cached panel retained");
                        var title=panel.Find("Title").GetComponent<Text>();var count=panel.Find("Count").GetComponent<Text>();var prompt=panel.Find("Instruction").GetComponent<Text>();
                        Check(title.text.Contains(locale=="zh-CN"?"指示物":locale=="ja-JP"?"カウンター":"counters"),"Counter title localized: "+title.text);
                        Check(count.text.Contains(view.request.selectedCount.ToString())&&count.text.Contains(view.request.maxCount.ToString()),"Native total counter count, not card count");
                        Check(count.preferredHeight<=count.rectTransform.rect.height+.1f&&prompt.preferredHeight<=prompt.rectTransform.rect.height+.1f,"Counter count/instruction fit first frame");
                        var cards=panel.GetComponentsInChildren<Button>(true).Where(b=>b.name=="Artwork").ToArray();
                        Check(cards.Length==view.request.options.Count(o=>o.card!=null&&!string.IsNullOrEmpty(o.card.name)),"Exact native card identity count");
                        Check(cards.All(b=>b.GetComponent<Image>().sprite!=null),"Real local card artwork");
                        if(view.request.selectedCount==1){Check(cards.Length==(splitOnly?2:1)&&view.request.maxCount==2,"Exact one/two-card count");Check(panel.GetComponentsInChildren<Text>().Any(t=>t.name=="CounterCount"&&t.text.Contains("1")),"Per-card additive count visible");}
                        foreach(var exhausted in view.request.options.Where(o=>o.disabled)){
                            var face=cards.Single(b=>b.transform.parent.name=="ChoiceCard_"+exhausted.card.id);
                            Check(!face.interactable&&!ui.TrySubmitPresentedOption(Array.IndexOf(view.request.options,exhausted),view.request.requestId),"Exhausted identity disabled/submit refused");
                        }
                        Check(!view.request.options.Any(o=>o.id=="ok"||o.id=="reset-cards"),"Native premature confirm/reset absent");
                        button=id=="cancel"?GameObject.Find("SelectionAction_"+index)?.GetComponent<Button>():cards.Single(b=>b.transform.parent.name=="ChoiceCard_"+view.request.options[index].card.id);
                    }else {
                        button=GameObject.Find("ValueOption_"+index)?.GetComponent<Button>();
                        var heading=GameObject.Find("ValueTitle").GetComponent<Text>();
                        Check(heading.text==(locale=="zh-CN"?"选择要移去的指示物类型":locale=="ja-JP"?"取り除くカウンターの種類を選択":"Choose a counter type to remove"),"Type prompt localized");
                        Check(button!=null&&button.GetComponentInChildren<Text>().text==(view.request.options[index].label=="CHARGE"?(locale=="zh-CN"?"充电":locale=="ja-JP"?"蓄積":"Charge"):(locale=="zh-CN"?"晕眩":locale=="ja-JP"?"麻痺":"Stun")),"Native type option localized, original identity retained");
                    }
                    Check(button!=null,"Offered choice control exists "+id);Check(!ui.TrySubmitPresentedOption(index,view.request.requestId-1),"Stale rejected");
                    ScreenCapture.CaptureScreenshot(output+"/"+locale+"-"+test+"-"+index+".png");yield return Frame();
                    Click(button);button.onClick.Invoke();Check(calls==1&&last==index,"Original index once");
                    ui.Present(view,1,false,i=>calls++,null,"");yield return Frame();Check(!ui.TrySubmitPresentedOption(index,view.request.requestId)&&calls==1,"Nonacting rejected");
                    ui.Present(null,1,false,null,null,"");yield return Frame();Check(GameObject.Find("CardSelection")==null,"Reset removes active counter selector");
                    File.AppendAllText(output+"/steps.log",locale+" "+test+" "+id+" index="+index+" PASS\n");
                }
            }
          }
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=moduleEnabled;GamePreferences.SetLanguage(language);for(int i=0;i<banners.Length;i++)if(banners[i]!=null)banners[i].enabled=flags[i];}
    }
}
#endif
