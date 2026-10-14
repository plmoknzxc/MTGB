#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class StackInteractionValidation
{
    const string Folder="Tools/Validation/Reports/StackInteractions";
    static bool running;
    public static void Run()
    {
        if(running)return;running=true;
        foreach(var notice in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(notice);
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("StackValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator test)
    {
        File.WriteAllText(Folder+"/ui-result.txt","RUNNING");
        while(true)
        {
            object next;
            try {if(!test.MoveNext())break;next=test.Current;}
            catch(Exception e){running=false;Debug.LogException(e);File.WriteAllText(Folder+"/ui-result.txt","FAIL "+e);yield break;}
            yield return next;
        }
        running=false;
    }
    static ForgeSeatView Load(string file)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Folder+"/"+file+".json"));
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static Vector2 Point(RectTransform rect)=>RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        int submitted=0;string last=null;
        Action<ForgeSeatView> present=s=>ui.Present(s,s.seat,true,i=>{submitted++;last=s.request.options[i].id;},null,"堆叠交互验证 · Forge 实际快照");
        var state=Load("spells-three");present(state);yield return new WaitForSecondsRealtime(.7f);
        var canvas=GameObject.Find("Canvas").transform;
        var scroll=canvas.Find("StackColumn/Viewport").GetComponent<ScrollRect>();
        Check(scroll.content.childCount==3,"Three actual stack entries missing");
        for(int i=0;i<3;i++)
        {
            scroll.verticalNormalizedPosition=1-i*.5f;Canvas.ForceUpdateCanvases();yield return null;
            var tile=GameObject.Find("StackCard_"+i).GetComponent<RectTransform>();
            Check(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,Point(tile)),"Entry unreachable by scrolling");
            ExecuteEvents.Execute(tile.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);yield return null;
            var details=canvas.Find("InspectPanel");
            var rules=details.Find("Rules").GetComponent<Text>();
            Check(details.gameObject.activeSelf && rules.text.Contains(state.stackItems[i].kind=="spell" ? GamePreferences.CardRules(CardDatabaseRepository.LoadAll().First(c=>c.PrintingKey==state.stackItems[i].source.printingKey)) : state.stackItems[i].description),"Specific stack effect missing");
            ExecuteEvents.Execute(tile.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
        }
        float offset=scroll.content.anchoredPosition.y;present(state);yield return null;
        Check(Mathf.Abs(offset-scroll.content.anchoredPosition.y)<1,"Refresh resets stack scroll");
        ScreenCapture.CaptureScreenshot(Folder+"/multiple.png");yield return new WaitForEndOfFrame();
        var target=Load("triggers-target-2");present(target);yield return new WaitForSecondsRealtime(.7f);
        var card=GameObject.Find("StackCard_0").GetComponent<RectTransform>();
        var point=Point(card);ui.UpdateTargetingAt(point);Canvas.ForceUpdateCanvases();
        var arrow=GameObject.Find("TargetArrow").GetComponent<ForgeTargetArrow>();
        Check(arrow.canvasRenderer.GetMesh().vertexCount>395,"Stack target arrow has no valid endpoint ring");
        Check(!ui.TryChooseTargetAt(Vector2.zero,target.request.requestId),"Empty target accepted");
        Check(!ui.TryChooseTargetAt(point,target.request.requestId-1),"Stale target accepted");
        Check(ui.TryChooseTargetAt(point,target.request.requestId),"Stack target pointer rejected");
        Check(last=="stack:"+target.stackItems[0].id && submitted==1,"Wrong stack instance submitted");
        Check(!ui.TryChooseTargetAt(point,target.request.requestId),"Duplicate target accepted");
        present(target);yield return new WaitForSecondsRealtime(.4f);
        var inspectCard=GameObject.Find("StackCard_0");
        ExecuteEvents.Execute(inspectCard,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
        Check(submitted==1&&canvas.Find("InspectPanel").gameObject.activeSelf,"Inspecting target submitted it or omitted its details");
        ExecuteEvents.Execute(inspectCard,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
        yield return new WaitForSecondsRealtime(.2f);
        scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();yield return null;
        var bottom=(RectTransform)GameObject.Find("StackCard_1").transform;
        // The stack is fanned: the earlier entry's center is covered by the top
        // entry. Choose its exposed left strip through the actual raycaster.
        var exposed=RectTransformUtility.WorldToScreenPoint(null,bottom.TransformPoint(new Vector2(bottom.rect.xMin+8,bottom.rect.center.y)));
        Check(ui.TryChooseTargetAt(exposed,target.request.requestId) && last=="stack:"+target.stackItems[1].id && submitted==2,"Scrolled lower stack target rejected");
        var firstOrder=File.ReadLines(Folder+"/triggers.jsonl").Select(line=>JsonUtility.FromJson<ForgeWireMessage>(line)).First(m=>m.request?.kind=="OrderAbilities");
        var ordering=firstOrder.views[0];ordering.request=firstOrder.request;ordering.awaitingInput=true;
        present(ordering);yield return new WaitForSecondsRealtime(.3f);
        var chooseFirst=canvas.Find("AbilityOrder").GetComponentsInChildren<Button>().First(b=>b.name=="OrderedAbility_"+firstOrder.request.options.First(o=>o.id=="order:0").ability.id);
        chooseFirst.onClick.Invoke();Check(last=="order:0" && submitted==3,"Initial ability selection not submitted");
        var order=Load("triggers-order");present(order);yield return new WaitForSecondsRealtime(.3f);
        Check(!canvas.Find("ForgeChoices").gameObject.activeSelf,"Order uses generic sidebar");
        var panel=canvas.Find("AbilityOrder");Check(panel.gameObject.activeSelf,"Order panel missing");
        foreach(var entry in order.request.orderedAbilities.Select((ability,index)=>new {ability,index}))
            Check(panel.GetComponentsInChildren<Transform>().First(t=>t.name=="OrderedAbility_"+entry.ability.id).Find("Order").GetComponent<Text>().text==(entry.index+1).ToString(),"Resolution order unclear");
        ScreenCapture.CaptureScreenshot(Folder+"/order.png");yield return new WaitForEndOfFrame();
        panel.Find("ConfirmOrder").GetComponent<Button>().onClick.Invoke();
        Check(last=="confirm-order" && submitted==4,"Order not explicitly confirmed");
        // Presentation-only pressure check. Engine behavior is covered by the separate real-card run.
        state.stackItems=Enumerable.Range(0,12).Select(i=>new ForgeStackItem{id=10000+i,kind="trigger",controllerSeat=1,source=state.stackItems[0].source,description="Ability "+i+"\n"+string.Join("\n",Enumerable.Repeat("Long effect line",45))}).ToArray();
        state.stack=state.stackItems.Select(e=>e.description).ToArray();state.stackCount=12;present(state);yield return new WaitForSecondsRealtime(.5f);
        scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();yield return null;
        var lastCard=GameObject.Find("StackCard_11").GetComponent<RectTransform>();
        Check(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,Point(lastCard)),"12th entry unreachable");
        ExecuteEvents.Execute(lastCard.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);yield return null;
        var effect=canvas.Find("InspectPanel/Rules/RuleScroll").GetComponent<ScrollRect>();
        Check(effect.content.rect.height>effect.viewport.rect.height,"Long effect cannot scroll");
        effect.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();yield return null;
        Check(effect.content.anchoredPosition.y>0,"Long effect scroll stuck");
        ExecuteEvents.Execute(lastCard.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
        present(Load("triggers-resolved"));yield return null;
        Check(!panel.gameObject.activeSelf && !canvas.Find("StackColumn").gameObject.activeSelf,"Resolved stack/order remains");
        const string result="PASS: three real Forge entries scroll and show individual effects; stack target arrow, correct instance, stale/empty/duplicate guards; separate inspect during targeting; explicit trigger order confirmation; 12-entry presentation stress and long effect scrolling.";
        File.WriteAllText(Folder+"/ui-result.txt",result);Debug.Log(result);
    }
}
#endif
