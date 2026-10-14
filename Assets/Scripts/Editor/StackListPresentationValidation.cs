#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class StackListPresentationValidation
{
    const string Output=".tools/stack-list-ui-1004";
    static bool running;
    static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    static IEnumerator Frame(float seconds=.4f){yield return new WaitForSecondsRealtime(seconds);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static ForgeSeatView Load(string path)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));
    static void Click(Button b)
    {
        Check(b!=null&&b.interactable,"Disabled click "+b?.name);
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.TransformPoint(((RectTransform)b.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked click "+b.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static void Shot(string name)=>ScreenCapture.CaptureScreenshot(Output+"/"+GamePreferences.Language+"-"+name+".png");
    public static void Run()
    {
        Check(Application.isPlaying,"Play Mode required");if(running)return;running=true;Directory.CreateDirectory(Output);
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("StackListValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        string language=GamePreferences.Language;
        var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();if(banner!=null)banner.enabled=false;
        var test=Validate(ui);File.WriteAllText(Output+"/result.txt","RUNNING");
        try {
            while(true){object next;try{if(!test.MoveNext())break;next=test.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: three locales, fixed-size TMP sprite mana in chosen body/heading/ability; 0/1/2/3/7 stack threshold; raycast icon toggle + actual intermediate slide; hidden input guard; read-only browse has no submission even while targeting; horizontal end reach/refresh retention; native stack target identity, stale/duplicate/nonacting/disabled guards; cancel/reset/confirm; battlefield view/restore; request context and resolved cleanup. Three-entry target/7-entry stress fixtures are layout adapters based on archived real requests, not additional engine scenarios.");
        }finally{running=false;GamePreferences.SetLanguage(language);ui.RestoreBattlefieldWindows();}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}) {
            GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale readiness");
            int sent=0;string last=null;
            Action<ForgeSeatView,bool> present=(v,enabled)=>ui.Present(v,v?.seat??1,enabled,i=>{sent++;last=v.request.options[i].id;},null,"");
            ui.Present(null,1,false,null,null,"");
            var real=Load("Tools/Validation/Reports/StackInteractions/spells-three.json");
            foreach(int count in new[]{0,1,2,3}) {
                var v=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(real));v.stackItems=v.stackItems.Take(count).ToArray();v.stack=v.stack.Take(count).ToArray();v.stackCount=count;present(v,false);yield return Frame();
                var column=GameObject.Find("Canvas").transform.Find("StackColumn");
                Check(column.gameObject.activeSelf==(count>0),"Empty stack visibility");
                Check(column.Find("Header").GetComponent<Text>().text=="","Stack count heading remains");
                Check(column.Find("ViewStack").gameObject.activeSelf==(count>=3),"View threshold "+count);
                Check(column.Find("CollapseStack").GetComponentsInChildren<Text>().Length==0,"Text arrow control");
                if(count>0)foreach(var icon in column.GetComponentsInChildren<StackControlIcon>())Check(icon.canvasRenderer!=null&&icon.canvasRenderer.GetMesh().vertexCount>0,"Empty control icon mesh");
            }
            var seven=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(real));
            seven.stackItems=Enumerable.Range(0,7).Select(i=>{var e=JsonUtility.FromJson<ForgeStackItem>(JsonUtility.ToJson(real.stackItems[i%3]));e.id=70000+i;return e;}).ToArray();seven.stack=seven.stackItems.Select(e=>e.description).ToArray();seven.stackCount=7;
            present(seven,false);yield return Frame(.7f);Shot("seven");yield return new WaitForEndOfFrame();
            var canvas=GameObject.Find("Canvas").transform;var viewport=(RectTransform)canvas.Find("StackColumn/Viewport");
            var collapse=canvas.Find("StackColumn/CollapseStack").GetComponent<Button>();Click(collapse);yield return Frame(.07f);
            float mid=viewport.anchoredPosition.x;Check(mid>0&&viewport.GetComponent<ForgeUiMotion>().IsMoving,"No intermediate right slide");
            yield return Frame();Check(viewport.anchoredPosition.x>mid&&!viewport.GetComponent<CanvasGroup>().blocksRaycasts,"Stack remains clickable/visible");
            Check(collapse.GetComponentInChildren<StackControlIcon>().Current==StackControlIcon.Kind.Left,"Arrow did not reverse");Shot("collapsed");yield return new WaitForEndOfFrame();
            present(seven,false);yield return Frame();Check(!viewport.GetComponent<CanvasGroup>().blocksRaycasts,"Snapshot reopened collapsed stack");
            Click(canvas.Find("StackColumn/ViewStack").GetComponent<Button>());yield return Frame();
            var list=canvas.Find("StackList");Check(list.gameObject.activeSelf&&list.Find("Confirm")==null,"Browse offers selection");
            Check(Vector2.Distance(((RectTransform)list).anchoredPosition,Vector2.zero)<.1f,"Stack list not centered");
            Check(list.GetComponentsInChildren<Button>().Where(b=>b.name=="Card").All(b=>!b.interactable&&b.onClick.GetPersistentEventCount()==0),"Browse card is selectable");
            foreach(var b in list.GetComponentsInChildren<Button>().Where(b=>b.name=="Card"))b.onClick.Invoke();Check(sent==0,"Browse submitted");
            var scroll=list.GetComponentInChildren<ScrollRect>();Check(scroll.horizontal&&!scroll.vertical,"Not horizontal");scroll.horizontalNormalizedPosition=1;yield return Frame(.1f);
            var end=(RectTransform)scroll.content.Find("Entry_70006/Card");Check(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,RectTransformUtility.WorldToScreenPoint(null,end.TransformPoint(end.rect.center))),"Last card unreachable");
            present(seven,false);yield return Frame(.1f);Check(scroll.horizontalNormalizedPosition>.99f,"Refresh resets browse scroll");
            Shot("browse");yield return new WaitForEndOfFrame();
            ui.ViewBattlefield();yield return Frame(.1f);Check(list.GetComponent<CanvasGroup>().alpha==0&&!list.GetComponent<CanvasGroup>().blocksRaycasts,"Browse blocks battlefield view");ui.RestoreBattlefieldWindows();yield return Frame(.1f);Check(list.GetComponent<CanvasGroup>().alpha==1&&scroll.horizontalNormalizedPosition>.99f,"Browse restore loses position");
            Click(list.Find("Close").GetComponent<Button>());yield return Frame(.1f);Click(collapse);yield return Frame();Check(Mathf.Abs(viewport.anchoredPosition.x)<.1f&&viewport.GetComponent<CanvasGroup>().blocksRaycasts,"Restore stack failed");
            // Real two-entry target request plus one explicitly illegal display
            // object exercises crowded layout without inventing a legal option.
            var target=Load("Tools/Validation/Reports/StackInteractions/triggers-target-2.json");
            var extra=JsonUtility.FromJson<ForgeStackItem>(JsonUtility.ToJson(target.stackItems[0]));extra.id=999999;
            target.stackItems=target.stackItems.Concat(new[]{extra}).ToArray();target.stack=target.stack.Concat(new[]{extra.description}).ToArray();target.stackCount=3;
            present(target,true);yield return Frame();list=canvas.Find("StackList");Check(list.gameObject.activeSelf,"Crowded target list absent");
            var invalid=list.Find("Cards/Content/Entry_999999/StackListTarget_999999").GetComponent<Button>();Check(!invalid.interactable,"Illegal target enabled");invalid.onClick.Invoke();Check(sent==0,"Illegal target submitted");
            Shot("targets");yield return new WaitForEndOfFrame();
            Click(canvas.Find("StackColumn/ViewStack").GetComponent<Button>());yield return Frame();list=canvas.Find("StackList");Check(list.Find("Confirm")==null,"View becomes target selection");foreach(var b in list.GetComponentsInChildren<Button>().Where(b=>b.name=="Card"))b.onClick.Invoke();Check(sent==0,"Browse during targeting submitted");
            Click(list.Find("Close").GetComponent<Button>());yield return Frame();
            ui.Present(null,target.seat,false,null,null,"");present(target,true);yield return Frame();list=canvas.Find("StackList");
            var legal=list.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("StackListTarget_")&&b.interactable);var point=RectTransformUtility.WorldToScreenPoint(null,legal.transform.TransformPoint(((RectTransform)legal.transform).rect.center));
            Check(!ui.TryChooseTargetAt(point,target.request.requestId-1),"Stale target allowed");Click(legal);Check(sent==1&&last=="stack:"+legal.name.Substring("StackListTarget_".Length),"Wrong native stack identity");legal.onClick.Invoke();Check(sent==1,"Duplicate submission");
            present(target,true);yield return Frame();list=canvas.Find("StackList");if(list.Find("Cancel").gameObject.activeSelf){Click(list.Find("Cancel").GetComponent<Button>());Check(last=="cancel","Cancel changed identity");}
            var selected=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(target));selected.request.options=selected.request.options.Concat(new[]{new ForgeOption{id="ok",label="Confirm"},new ForgeOption{id="reset-targets",label="Reset"}}).GroupBy(o=>o.id).Select(g=>g.First()).ToArray();
            present(selected,true);yield return Frame();Click(canvas.Find("StackList/Reset").GetComponent<Button>());Check(last=="reset-targets","Reset identity");present(selected,true);yield return Frame();Click(canvas.Find("StackList/Confirm").GetComponent<Button>());Check(last=="ok","Confirm identity");
            present(target,false);yield return Frame();Check(!canvas.Find("StackList").gameObject.activeSelf,"Nonacting target list");
            // Native chosen-rule samples, plus typed symbol stress in a source
            // ability and a cost reduction heading (display-only adapters).
            foreach(string sample in new[]{"offspring","kicker","replicate","spree-rich"}) {
                var v=Load("Tools/Validation/Reports/ModeChoices/"+sample+"-stack.json");ui.Present(null,v.seat,false,null,null,"");present(v,false);yield return Frame();
                var tile=GameObject.Find("StackCard_0");foreach(var source in tile.GetComponentsInChildren<Text>().Where(t=>t.name=="Effects"||t.name=="CastingChoices"))CheckSymbols(source);
                if(sample=="offspring"){Shot("offspring-symbols");yield return new WaitForEndOfFrame();}
            }
            var stress=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(real));stress.stackItems=new[]{new ForgeStackItem{id=888888,kind="trigger",source=real.stackItems[0].source,description="{2}{W}{U}{B}{R}{G}{C}{X}{T}{W/U}{2/B}{G/P}"}};stress.stack=stress.stackItems.Select(e=>e.description).ToArray();stress.stackCount=1;present(stress,false);yield return Frame();
            CheckSymbols(GameObject.Find("StackCard_0").GetComponentsInChildren<Text>().First(t=>t.name=="Effect"));
            var reducing=Load("Tools/Validation/Reports/ModeChoices/offspring-stack.json");reducing.stackItems[0].castingChoices=new[]{new ForgeCastingChoice{kind="ReduceW",count=1}};present(reducing,false);yield return Frame();CheckSymbols(GameObject.Find("StackCard_0").GetComponentsInChildren<Text>().First(t=>t.name=="CastingChoices"));
            present(Load("Tools/Validation/Reports/StackInteractions/triggers-resolved.json"),false);yield return Frame();Check(!canvas.Find("StackColumn").gameObject.activeSelf&&!canvas.Find("StackList").gameObject.activeSelf,"Resolved stack/list remains");
        }
    }
    static void CheckSymbols(Text source)
    {
        var text=source.transform.Find("Symbols").GetComponent<TextMeshProUGUI>();text.ForceMeshUpdate();
        Check(!source.enabled&&!text.enableAutoSizing&&!Regex.IsMatch(text.text,@"\{[^{}]+\}"),"Literal mana or resizing in "+source.name);
        Check(text.font==GamePreferences.TmpFont,"Wrong locale font");
        int expected=Regex.Matches(source.text,@"\{[^{}]+\}").Count;
        Check(text.textInfo.characterInfo.Take(text.textInfo.characterCount).Count(c=>c.elementType==TMP_TextElementType.Sprite)==expected,"Missing sprite mana "+source.name+" "+expected);
        Check(text.GetPreferredValues(text.text,text.rectTransform.rect.width,10000).y<=text.rectTransform.rect.height+1,"Symbol rules truncated "+source.name);
    }
}
#endif

