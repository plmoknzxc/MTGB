#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class HorizontalBattlefieldPresentationValidation
{
    public static string Output;
    static int checks;
    static bool runField,running;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
    static IEnumerator Frame(){Canvas.ForceUpdateCanvases();yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();}
    public static void Run(bool verifyField=true)
    {
        if(running)return;running=true;
        Output=".tools/horizontal-pool-1010/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);
        File.WriteAllText(".tools/horizontal-pool-1010/ui-latest.txt",Output);
        File.Copy("Assets/Scripts/Editor/HorizontalBattlefieldPresentationValidation.cs",Output+"/driver.cs");File.WriteAllText(Output+"/result.txt","RUNNING");checks=0;runField=verifyField;
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("HorizontalValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        var e=Validate(ui);try {
            while(true){object next;try{if(!e.MoveNext())break;next=e.Current;}catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS "+checks+" bounded horizontal field/pool checks. Dense fixture is display-only; restricted payment snapshots come from actual native flow. Not a live-network assertion.");
        }finally{running=false;(e as IDisposable)?.Dispose();}
    }
    static void Ray(Button button)
    {
        var rect=(RectTransform)button.transform;var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        Check(button.interactable,"Disabled native pool button "+button.name);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Pool ray blocked "+button.name+" by "+string.Join("|",hits.Select(h=>h.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    static bool Complete(ScrollRect scroll,RectTransform card)
    {
        var points=new List<Vector3>();
        foreach(var rect in new[]{card,card.Find("Visual/Count") as RectTransform,card.Find("Visual/DefenseCounter") as RectTransform,card.Find("Visual/CardShadow") as RectTransform}.Where(r=>r!=null&&r.gameObject.activeSelf)) {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);points.AddRange(corners.Select(c=>scroll.viewport.InverseTransformPoint(c)));
        }
        var area=scroll.viewport.rect;return points.All(p=>p.x>=area.xMin-.51f&&p.x<=area.xMax+.51f&&p.y>=area.yMin-.51f&&p.y<=area.yMax+.51f);
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool physical=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        string language=GamePreferences.Language;float scale=GamePreferences.TextScale;
        try {
            foreach(var banner in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))banner.enabled=false;
            var packet=BattlefieldStressPresentationValidation.Fixture();packet.stackItems=Array.Empty<ForgeStackItem>();packet.stack=Array.Empty<string>();packet.stackCount=0;
            ui.Present(packet,1,true,_=>{},null,"");yield return new WaitForSecondsRealtime(1);
            var canvas=GameObject.Find("Canvas").transform;
            foreach(var pointer in canvas.GetComponentsInChildren<ForgeBattlefieldCardPointer>()){pointer.ResetHover();pointer.enabled=false;}
            canvas.Find("InspectPanel").gameObject.SetActive(false);
            var lanes=canvas.GetComponentsInChildren<BattlefieldHorizontalScroll>().Where(s=>s.name.StartsWith("Dense_")).ToArray();
            if(runField) {
            Check(lanes.Length==10,"Ten battlefield lanes including separate walker/battle lanes");
            foreach(var lane in lanes) {
                Check(lane.horizontal&&!lane.vertical&&!lane.inertia,"Horizontal clamped no inertia");
                Check(Mathf.Abs(lane.content.rect.height-lane.viewport.rect.height)<.1f,"No vertical travel");
                var cards=lane.content.Cast<Transform>().Where(t=>t.name.StartsWith("Card_")).Cast<RectTransform>().ToArray();
                if(lane.name.EndsWith("Support")) {
                    Check(cards.GroupBy(c=>Mathf.Round(c.anchoredPosition.x)).All(g=>g.Count()<=2),"Two members per support column");
                } else if(lane.name.EndsWith("Planeswalkers") || lane.name.EndsWith("Battles"))
                    Check(cards.Select(c=>Mathf.Round(c.anchoredPosition.y)).Distinct().Count()==1,"Single row "+lane.name);
                else Check(cards.Select(c=>Mathf.Round(c.anchoredPosition.y)).Distinct().Count()==2,"Two rows "+lane.name);
                bool support=lane.name.EndsWith("Support") || lane.name.EndsWith("Planeswalkers") || lane.name.EndsWith("Battles");
                Check(Mathf.RoundToInt(lane.viewport.rect.width/lane.columnWidth)==(support?5:6),"Aligned visible columns "+lane.name);
                Check(lane.transform.parent.Find(lane.name+"_Navigation/Next").GetComponent<RectTransform>().rect.size==new Vector2(32,40),"Visible arrow target");
                foreach(float norm in new[]{0f,.37f,1f}) {
                    lane.horizontalNormalizedPosition=norm;yield return Frame();
                    foreach(var card in cards){bool fits=Complete(lane,card);var group=card.GetComponent<CanvasGroup>();Check((group.alpha>0)==fits,"No card fragment "+card.name);Check(group.blocksRaycasts==fits,"No invisible hit surface");}
                }
                foreach(var card in cards) {
                    float travel=lane.content.rect.width-lane.viewport.rect.width;
                    lane.content.anchoredPosition=new Vector2(-Mathf.Clamp(card.anchoredPosition.x+lane.content.rect.width*.5f-lane.viewport.rect.width*.5f,0,travel),0);yield return Frame();
                    Check(card.GetComponent<CanvasGroup>().alpha==1,"Reach every ID "+lane.name+"/"+card.name);
                }
                lane.horizontalNormalizedPosition=0;yield return Frame();
                var point=RectTransformUtility.WorldToScreenPoint(null,lane.viewport.TransformPoint(lane.viewport.rect.center));
                var drag=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=point,pressPosition=point};
                lane.OnBeginDrag(drag);drag.position=point+new Vector2(-12,0);lane.OnDrag(drag);
                Check(-lane.content.anchoredPosition.x<lane.columnWidth*.4f,"Small drag did not multiply distance");lane.OnEndDrag(drag);yield return Frame();
                Check(Mathf.Abs(lane.content.anchoredPosition.x)<.5f,"Small drag returns to original column");
                lane.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-1)});yield return Frame();
                Check(Mathf.Abs(-lane.content.anchoredPosition.x-Mathf.Min(lane.columnWidth,lane.content.rect.width-lane.viewport.rect.width))<.5f,"Wheel one column");
                lane.horizontalNormalizedPosition=1;
            }
            yield return Frame();ScreenCapture.CaptureScreenshot(Output+"/field-end.png");yield return Frame();
            foreach(var lane in lanes)lane.horizontalNormalizedPosition=.37f;
            var before=lanes.Select(l=>l.content.anchoredPosition.x).ToArray();ui.Present(packet,1,true,_=>{},null,"");yield return Frame();
            for(int i=0;i<lanes.Length;i++)Check(Mathf.Abs(lanes[i].content.anchoredPosition.x-before[i])<.2f,"Snapshot retains offset");
            foreach(var lane in lanes)lane.horizontalNormalizedPosition=0;yield return Frame();ScreenCapture.CaptureScreenshot(Output+"/field-start.png");yield return Frame();
            }
            // Current native snapshots: both ordinary and restricted same-color
            // mana exist; exact options differ, cancellation restores the bucket.
            foreach(string file in Directory.GetFiles(".tools/horizontal-pool-1010/native-ui","*.json")) {
                File.WriteAllText(Output+"/current-snapshot.txt",file);File.Copy(file,Output+"/snapshot-"+Path.GetFileName(file),true);
                var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(file));int calls=0,last=-1;
                ui.Present(null,1,false,null,null,"");
                ui.Present(v,1,true,i=>{calls++;last=i;},null,"");yield return Frame();
                var pool=canvas.Find("ManaPoolChips").GetComponent<BattlefieldHorizontalScroll>();
                foreach(var entry in v.players.First(p=>p.seat==1).manaPoolEntries) {
                    string name=entry.restricted?"Pool_Restricted_"+entry.id:"Pool_"+(entry.snow?"S_":"")+entry.symbol;
                    var chip=pool.content.Find(name);Check(chip!=null&&chip.Find("Count").GetComponent<Text>().text==entry.count.ToString(),"Native split count");
                    Check((chip.Find("Symbol").GetComponent<RestrictedManaGlow>()!=null)==entry.restricted,"Restricted glyph contour");
                    Check(chip.Find("RestrictionShield")==null&&chip.Find("Symbol").GetComponent<Text>().fontSize==Mathf.RoundToInt(22*GamePreferences.TextScale),"Same ordinary glyph size, no shield");
                }
                var option=v.request.options.FirstOrDefault(o=>o.id.Contains(":restricted:"));
                if(option!=null) {
                    var b=pool.content.Find("Pool_Restricted_"+option.id).GetComponent<Button>();Ray(b);b.onClick.Invoke();Check(calls==1&&last==ui.OptionIndex(option.id),"Exact bucket and duplicate guard");
                    ui.Present(v,1,false,i=>calls++,null,"");yield return Frame();Check(pool.GetComponentsInChildren<Button>().All(b=>!b.interactable),"Passive cannot pay");
                    ui.Present(v,1,true,i=>calls++,null,"");v.inputPending=true;ui.Present(v,1,true,i=>calls++,null,"");yield return Frame();Check(pool.GetComponentsInChildren<Button>().All(b=>!b.interactable),"Pending cannot pay");v.inputPending=false;
                }
                ui.Present(v,2,false,i=>calls++,null,"");yield return Frame();Check(canvas.Find("PublicManaPool").GetComponentsInChildren<Button>().All(b=>!b.interactable),"Other viewer cannot pay");
            }
            ui.Present(packet,1,true,_=>{},null,"");yield return Frame();
            foreach(var lane in lanes)lane.horizontalNormalizedPosition=0;
            var entries=new List<ForgeManaPoolEntry>();foreach(bool snow in new[]{false,true})foreach(string color in new[]{"W","U","B","R","G","C"})entries.Add(new ForgeManaPoolEntry{id="mana:"+(snow?"S:":"")+color,symbol=color,count=100,snow=snow});
            entries.Add(new ForgeManaPoolEntry{id="mana:R:restricted:fixture",symbol="R",count=2,restricted=true,sourceName="Cavern of Souls"});
            foreach(var player in packet.players)player.manaPoolEntries=entries.ToArray();ui.Present(packet,1,true,_=>{},null,"");yield return Frame();
            foreach(string name in new[]{"ManaPoolChips","PublicManaPool"}) {
                var pool=canvas.Find(name).GetComponent<BattlefieldHorizontalScroll>();Check(pool.viewport.rect.width<=300,"Short mana pool");Check(pool.content.rect.width>pool.viewport.rect.width&&!pool.vertical,"Mana horizontal overflow");
                pool.horizontalNormalizedPosition=1;yield return Frame();Check(pool.content.Find("Pool_Restricted_mana:R:restricted:fixture").gameObject.activeSelf,"Last restricted bucket retained");
            }
            ScreenCapture.CaptureScreenshot(Output+"/field-and-pools.png");yield return Frame();
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}) {
                GamePreferences.SetLanguage(locale);while(!GamePreferences.Ready)yield return null;
                ui.Present(packet,1,true,_=>{},null,"");yield return Frame();
                foreach(string name in new[]{"ManaPoolChips","PublicManaPool"}) {
                    var pool=canvas.Find(name).GetComponent<BattlefieldHorizontalScroll>();pool.horizontalNormalizedPosition=1;yield return Frame();
                    foreach(var glyph in pool.GetComponentsInChildren<Text>().Where(t=>t.name=="Symbol"))Check(glyph.font==Resources.Load<Font>("UI/ManaSymbols/ManaGlyphs")&&!glyph.resizeTextForBestFit,"Glyph font survives "+locale);
                    Check(Mathf.Abs(pool.content.rect.height-pool.viewport.rect.height)<.1f&&pool.viewport.rect.height==34,"Single mana row clear of hand");
                    var glow=pool.content.GetComponentInChildren<RestrictedManaGlow>();Check(glow.GetComponent<Text>().canvasRenderer.GetMesh().vertexCount>0,"Actual contour glyph vertices");
                }
                // Explicit display fixture for the shared multi-object chooser.
                // Rule effects/legality remain covered by the original native jobs.
                var choice=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(packet));
                choice.request=new ForgeRequest{requestId=90001,contextId=70001,seat=1,kind="ChooseOptions",minCount=0,maxCount=2,message="Choose options",options=Enumerable.Range(0,12).Select(i=>new ForgeOption{id="select:"+i,label="Option "+i}).Concat(new[]{new ForgeOption{id="confirm-cards",label="Confirm"},new ForgeOption{id="reset-cards",label="Reset"}}).ToArray()};choice.awaitingInput=true;
                int calls=0,last=-1;ui.Present(choice,1,true,i=>{calls++;last=i;},null,"");yield return Frame();
                var selector=canvas.Find("CardAbilityChoices").GetComponentInChildren<BattlefieldHorizontalScroll>();
                Check(selector!=null&&selector.horizontal&&!selector.vertical,"Generic options horizontal");
                selector.horizontalNormalizedPosition=1;yield return Frame();float offset=selector.content.anchoredPosition.x;
                var lastButton=selector.content.Find("GenericOption_11").GetComponent<Button>();Ray(lastButton);Check(calls==1&&last==11,"Generic last exact ordinal");
                choice.request.requestId++;choice.request.selectedCount=1;choice.request.options[11].selected=true;
                ui.Present(choice,1,true,i=>calls++,null,"");yield return Frame();selector=canvas.Find("CardAbilityChoices").GetComponentInChildren<BattlefieldHorizontalScroll>();
                Check(Mathf.Abs(selector.content.anchoredPosition.x-offset)<.5f,"Chooser selection retains horizontal position");
                ui.Present(choice,1,false,i=>calls++,null,"");yield return Frame();Check(canvas.Find("CardAbilityChoices").GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("Generic")).All(b=>!b.interactable),"Passive chooser cannot select");
            }
            GamePreferences.SetLanguage(language);while(!GamePreferences.Ready)yield return null;
            ui.Present(packet,1,true,_=>{},null,"");yield return Frame();
            foreach(var lane in lanes)lane.horizontalNormalizedPosition=0;
            foreach(string name in new[]{"ManaPoolChips","PublicManaPool"})canvas.Find(name).GetComponent<BattlefieldHorizontalScroll>().horizontalNormalizedPosition=1;
            yield return Frame();ScreenCapture.CaptureScreenshot(Output+"/final.png");yield return Frame();
            DuelLayoutPresentationValidation.Preview(false);yield return Frame();Check(lanes.All(l=>!l.gameObject.activeSelf),"Normal lanes restore");
        }finally{ui.enabled=enabled;if(module!=null)module.enabled=physical;GamePreferences.SetLanguage(language);}
    }
}
#endif
