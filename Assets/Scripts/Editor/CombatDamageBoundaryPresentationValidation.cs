#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CombatDamageBoundaryPresentationValidation
{
    const string Root=".tools/combat-damage-boundaries-1008/",Output=Root+"ui/";
    static bool running;static int checks;
    static void Check(bool value,string reason){if(!value)throw new Exception(reason);checks++;}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Click(Button button)
    {
        Check(button!=null&&button.interactable&&button.gameObject.activeInHierarchy,"Unavailable pointer button");
        var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Pointer blocked: "+button.name+" hits="+string.Join(",",hits.Take(4).Select(h=>h.gameObject.name+"/"+h.gameObject.GetComponentInParent<Button>()?.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    public static void Run()
    {
        if(running)return;Check(Application.isPlaying,"Enter Play Mode");running=true;checks=0;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("CombatBoundaryValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator work)
    {
        string language=GamePreferences.Language;var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=banner!=null&&banner.enabled;if(banner!=null)banner.enabled=false;
        try {
            while(true){object next;try{if(!work.MoveNext())break;next=work.Current;}catch(Exception e){File.WriteAllText(Output+"result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"result.txt","PASS "+checks+" assertions: actual native skip/shared-lethal and divided assignment requests; exact source/recipient IDs, three locales, pointer actions, same-context gate updates, fresh context/source transitions, stale/duplicate/nonacting guards, resolved overlay cleanup. Native results separate; no live network/all combat claim.");
        } finally {running=false;GamePreferences.SetLanguage(language);if(banner!=null)banner.enabled=enabled;}
    }
    static ForgeWireMessage[] Read(string name)
    {
        // Frozen producer chosen by successful result pointer; retain full wire as input proof.
        var pointer=JsonUtility.FromJson<ResultPointer>(File.ReadAllText(Root+"native/"+name+"-result.json"));
        Check(pointer.status=="PASS","Native input not PASS");
        string path=pointer.folder+"/trace.jsonl";File.Copy(path,Output+name+"-input.jsonl",true);
        return File.ReadLines(path).Select(JsonUtility.FromJson<ForgeWireMessage>).Where(m=>m.type=="request"&&m.request.kind=="AssignAmount").GroupBy(m=>m.request.requestId).Select(g=>g.First()).ToArray();
    }
    [Serializable] class ResultPointer{public string status,folder;}
    static Button ButtonFor(GameObject panel,string id)
    {
        if(id.StartsWith("amount-add:"))return panel.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Recipient_"+id.Split(':')[1]).Find("Plus").GetComponent<Button>();
        return panel.transform.Find(id=="skip-amount"?"Skip":"Confirm").GetComponent<Button>();
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        var shared=Read("skip-shared");var divide=Read("divide");
        var catalog=(Dictionary<string,CardDefinition>)typeof(ForgeBattlefieldController).GetField("catalog",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(ui);
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}) {
            GamePreferences.SetLanguage(locale);float end=Time.realtimeSinceStartup+10;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<end)yield return null;
            Check(GamePreferences.Ready&&GamePreferences.Language==locale,"Exact locale ready");
            foreach(var sequence in new[]{shared,divide}) {
                ui.Present(null,1,false,null,null,"");yield return Frame();GameObject outer=null;long context=-1;int titleSize=-1;
                for(int step=0;step<sequence.Length;step++) {
                    var m=sequence[step];var r=m.request;var v=m.views[r.seat-1];v.request=r;v.awaitingInput=true;
                    int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};ui.Present(v,r.seat,true,submit,null,"");yield return Frame();
                    var panel=GameObject.Find("DamageAllocation");Check(panel!=null,"Missing assignment panel");if(outer!=null)Check(outer==panel,"Outer panel replaced");outer=panel;
                    Check(panel.GetComponentsInChildren<RectTransform>().Count(t=>t.name.StartsWith("Recipient_"))==r.allocations.Length,"Stale extra allocation recipient survived request clear/context reuse");
                    var title=panel.transform.Find("Title").GetComponent<Text>();if(titleSize>=0)Check(titleSize==title.fontSize,"Title resized between requests");titleSize=title.fontSize;
                    Check(panel.transform.Find("Source").GetComponent<Text>().text.EndsWith(GamePreferences.CardFaceName(r.sourceCard.name)),"Wrong current source");
                    foreach(var row in r.allocations) {
                        var tile=panel.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Recipient_"+row.id);
                        Check(tile.Find("Amount").GetComponent<Text>().text==row.amount.ToString(),"Wrong assigned amount");
                        var art=tile.Find("Art").GetComponent<RawImage>();
                        if(row.card!=null&&row.card.id>0) {
                            catalog.TryGetValue(row.card.printingKey??"",out var definition);
                            Check(definition!=null&&CardImageLibrary.TryLoadFullCard(definition,row.card.backFace?1:0,out var sprite)&&sprite!=null&&art.texture==sprite.texture,"Stale recipient card art");
                        } else Check(art.texture==null&&art.color.a==0,"Player recipient inherited card art");
                        Check(tile.Find("Lethal").GetComponent<Text>().text==(row.hasLethal?(locale=="zh-CN"?"致命伤害 ":locale=="en-US"?"Lethal damage ":"致死ダメージ ")+row.lethal:""),"Wrong native shared lethal");
                        Check(tile.Find("Plus").GetComponent<Button>().interactable==r.options.Any(o=>o.id=="amount-add:"+row.id),"Trample/divide native gate mismatch");
                    }
                    bool skip=r.options.Any(o=>o.id=="skip-amount");var skipButton=panel.transform.Find("Skip").GetComponent<Button>();Check(skipButton.gameObject.activeInHierarchy==skip,"Skip visibility");
                    if(skip) {
                        Check(skipButton.GetComponentInChildren<Text>().text==(locale=="zh-CN"?"稍后分配":locale=="en-US"?"Assign later":"後で割り振る"),"Skip locale");
                        var text=skipButton.GetComponentInChildren<Text>();Check(!text.resizeTextForBestFit&&text.cachedTextGenerator.vertexCount>0&&text.preferredHeight<=text.rectTransform.rect.height+1,"Skip label clips/shrinks");
                        Check(((RectTransform)skipButton.transform).anchoredPosition.y<-150,"Skip overlaps remaining damage header");
                        Check(((RectTransform)panel.transform.Find("Confirm")).anchoredPosition.x<-160&&Math.Abs(((RectTransform)panel.transform.Find("Reset")).anchoredPosition.x)<1,"Three separate footer actions");
                    }
                    string action;
                    if(step+1>=sequence.Length||sequence[step+1].request.contextId!=r.contextId)action=skip&&r.selectedCount==0?"skip-amount":"confirm-amount";
                    else {var next=sequence[step+1].request;int row=Array.FindIndex(next.allocations,a=>a.amount>r.allocations.Single(b=>a.id==b.id).amount);Check(row>=0,"Missing native increment transition");action="amount-add:"+next.allocations[row].id;}
                    int index=Array.FindIndex(r.options,o=>o.id==action);Check(index>=0,"Unavailable native transition "+action);var button=ButtonFor(panel,action);
                    if(context!=r.contextId) {ScreenCapture.CaptureScreenshot(Output+locale+"-"+(sequence==shared?"shared":"divide")+"-"+r.contextId+".png");yield return new WaitForEndOfFrame();}
                    ui.Choose(index,r.requestId-1);Check(calls==0,"Stale assignment accepted");Click(button);button.onClick.Invoke();Check(calls==1&&last==index,"Pointer option/duplicate guard");
                    ui.Present(v,r.seat,false,submit,null,"");yield return Frame();Check(!ButtonFor(panel,action).interactable,"Nonacting button enabled");ui.Choose(index,r.requestId);Check(calls==1,"Nonacting submission accepted");
                    context=r.contextId;
                }
                ui.Present(null,1,false,null,null,"");yield return Frame();Check(GameObject.Find("DamageAllocation")==null,"Stale assignment remains after clear");
            }
        }
    }
}
#endif
