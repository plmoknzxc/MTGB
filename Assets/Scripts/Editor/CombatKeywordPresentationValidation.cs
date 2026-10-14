#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CombatKeywordPresentationValidation
{
    const string Root=".tools/combat-keywords-1007/";
    static string Output=Root+"ui/";
    static bool running;
    static readonly string[] Cases={"protection-prevent","protection-block","indestructible","infect-creature","infect-death","infect-player","wither-creature","wither-death","banding-defense"};
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Step(string name){File.AppendAllText(Output+"steps.log",name+" PASS\n");}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Root+"inputs/"+name+".json"));
    static IEnumerator Frame(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static IEnumerator Shot(string name){yield return Frame();ScreenCapture.CaptureScreenshot(Output+name+".png");yield return new WaitForEndOfFrame();}
    static void Click(Button b)
    {
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.TransformPoint(((RectTransform)b.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked pointer "+b.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    public static void Run()=>Start(false);
    public static void RunCountersOnly()=>Start(true);
    static void Start(bool countersOnly)
    {
        if(running)return;Check(Application.isPlaying,"Enter Play Mode");running=true;
        Output=Root+(countersOnly?"counter-fix-ui/":"ui/");
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"result.txt","RUNNING");File.WriteAllText(Output+"steps.log","");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("CombatKeywordValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui,countersOnly),countersOnly));
    }
    static IEnumerator Guard(IEnumerator test,bool countersOnly)
    {
        string language=GamePreferences.Language;
        var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=banner!=null&&banner.enabled;if(banner!=null)banner.enabled=false;
        try {
            while(true){object next;try{if(!test.MoveNext())break;next=test.Current;}catch(Exception e){File.WriteAllText(Output+"result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"result.txt",countersOnly?"PASS: original infect/wither native result inputs, zh-CN/current scale; three recipient frames, exact red PT and counter count with nonempty generated numeric glyphs after compact-counter overflow fix; screenshot verification separate. No new native effect start or unrelated UI regression.":"PASS: original native R29 inputs, zh-CN/current text scale; exact surviving/dead card identities, red counter-modified PT, creature -1/-1 counter and player poison amounts, both seats' poison/PT layout; native defender-owned banding damage allocation all add/confirm rays, legal gates, stale/duplicate/nonacting guards and resumed overlay cleanup. Existing consumers, not new UI firsts; attack-band construction remains a confirmed separate gap; no complete R29/all-combat/live-network claim.");
        } finally {running=false;GamePreferences.SetLanguage(language);if(banner!=null)banner.enabled=enabled;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui,bool countersOnly)
    {
        GamePreferences.SetLanguage("zh-CN");float end=Time.realtimeSinceStartup+8;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<end)yield return null;Check(GamePreferences.Ready,"Language unavailable");
        foreach(string name in Cases.Where(c=>!countersOnly||c=="infect-creature"||c=="wither-creature")) {
            var original=Read(name+"-resolved");
            foreach(int local in name=="infect-creature"||name=="infect-player"?new[]{1,2}:new[]{1}) {
                var recipient=local==1?original:Read(name+"-resolved-seat2");
                ui.Present(null,local,false,null,null,"");ui.Present(recipient,local,false,null,null,"");yield return Frame();
                Check(GameObject.Find("DamageAllocation")==null,"Resolved assignment remains");
                foreach(var p in original.players) {
                    var plate=GameObject.Find(p.seat==local?"PlayerPlate":"OpponentPlate");
                    Check(plate.transform.Find("Life").GetComponent<Text>().text==p.life.ToString(),"Life mismatch");
                    foreach(var c in p.battlefield.Where(c=>c.creature)) {
                        var card=GameObject.Find("Card_"+c.id);Check(card!=null,"Native survivor missing "+c.id);
                        var stats=card.GetComponentsInChildren<Text>().Single(t=>t.name=="Value"&&t.transform.parent.name=="Stats");
                        Check(stats.text==ForgeBattlefieldController.ModifiedStatsText(c,false),"Native PT mismatch");
                        foreach(var counter in c.counters??Array.Empty<ForgeCardCounter>()) {
                            var badge=card.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Counter_"+counter.kind);
                            var number=badge.Find("Count").GetComponent<Text>();
                            Check(number.text==counter.count.ToString(),"Creature counter amount");
                            Check(number.cachedTextGenerator.vertexCount>0,"Creature counter has no rendered numeric glyph");
                            if(counter.kind=="M1M1")Check(stats.text.Contains("D12D46")&&!stats.text.Contains("216D9E"),"Ordinary counter decrease must stay red, not base-rewrite blue");
                        }
                    }
                    foreach(var c in p.graveyardCards.Where(c=>c.creature))Check(GameObject.Find("Card_"+c.id)==null,"Dead creature visible");
                    foreach(var counter in p.counters??Array.Empty<ForgeCardCounter>()) {
                        var chip=plate.transform.Find("PlayerCounters/Counter_"+counter.kind);Check(chip!=null,"Player counter missing");
                        Check(chip.Find("Count").GetComponent<Text>().text==counter.count.ToString(),"Player counter amount");
                        var symbol=chip.Find("Symbol").GetComponent<UnityEngine.UI.Image>();Check(symbol!=null&&symbol.sprite!=null&&symbol.preserveAspect,"Poison icon missing/stretched");
                    }
                }
                Step(name+"-resolved-seat"+local);
                if(countersOnly||name=="infect-creature"||name=="infect-player"||name=="indestructible")yield return Shot(name+"-seat"+local);
            }
        }
        if(countersOnly)yield break;
        var frames=Directory.GetFiles(Root+"inputs","banding-defense-allocation-*.json").Select(p=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p))).OrderBy(v=>v.request.requestId).ToArray();
        Check(frames.Length==7,"Expected six additions and confirm");ui.Present(null,2,false,null,null,"");
        foreach(var v in frames) {
            var r=v.request;Check(r.seat==2&&r.selectionPurpose=="combatDamage","Wrong native decider");int calls=0,last=-1;
            ui.Present(v,2,true,i=>{calls++;last=i;},null,"");yield return Frame();var panel=GameObject.Find("DamageAllocation");Check(panel!=null,"Missing allocation");
            var hero=r.allocations.Single(a=>a.card!=null&&a.card.name=="Benalish Hero");
            foreach(var row in r.allocations) {
                var tile=panel.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Recipient_"+row.id);
                Check(tile.Find("Amount").GetComponent<Text>().text==row.amount.ToString(),"Assignment amount");
                Check(tile.Find("Plus").GetComponent<Button>().interactable==r.options.Any(o=>o.id=="amount-add:"+row.id),"Legal increment gate");
            }
            bool complete=hero.amount==6;string action=complete?"confirm-amount":"amount-add:"+hero.id;int option=Array.FindIndex(r.options,o=>o.id==action);
            Check(option>=0,"Missing native action");
            var button=complete?panel.transform.Find("Confirm").GetComponent<Button>():panel.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Recipient_"+hero.id).Find("Plus").GetComponent<Button>();
            Check(panel.transform.Find("Confirm").GetComponent<Button>().interactable==complete,"Early confirm");
            ui.Choose(option,r.requestId-1);Check(calls==0,"Stale submit");Click(button);button.onClick.Invoke();Check(calls==1&&last==option,"Ray/duplicate mismatch");
            if(complete)yield return Shot("banding-defense-confirm");
            ui.Present(v,2,false,i=>{calls++;last=i;},null,"");yield return Frame();ui.Choose(option,r.requestId);Check(calls==1,"Nonacting submit");
            Step("banding-defense-allocation-"+hero.amount);
        }
        var resumed=Read("banding-defense-resolved");ui.Present(resumed,2,false,null,null,"");yield return Frame();Check(GameObject.Find("DamageAllocation")==null,"Assignment did not clear");Step("banding-defense-cleanup");
    }
}
#endif
