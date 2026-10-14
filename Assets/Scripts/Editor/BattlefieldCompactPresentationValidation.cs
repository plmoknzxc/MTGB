#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class BattlefieldCompactPresentationValidation
{
    public const string Output=".tools/battlefield-compact-1005";
    static bool running;
    static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    static IEnumerator Frame(float seconds=.5f){yield return new WaitForSecondsRealtime(seconds);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static Transform CanvasRoot=>GameObject.Find("Canvas").transform;
    static void Click(Button button){
        Check(button!=null&&button.interactable,"Disabled "+button?.name);
        var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Blocked "+button.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    static int Quantity(int id){var root=GameObject.Find("Card_"+id);if(root==null)return 0;var label=root.transform.Find("Visual/Count/Value")?.GetComponent<Text>();return label==null?1:int.Parse(label.text.TrimStart('×'));}
    public static void Run(){Check(Application.isPlaying,"Play Mode required");if(running)return;running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/ui-result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("CompactValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){
        string language=GamePreferences.Language;var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();if(banner!=null)banner.enabled=false;
        var test=Validate(ui);try{while(true){object next;try{if(!test.MoveNext())break;next=test.Current;}catch(Exception e){File.WriteAllText(Output+"/ui-result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/ui-result.txt","PASS: zh/en/ja quantities represent all 575 IDs per player without rear card graphics; same-state walkers/battles/lands collapse, changed loyalty/tap state and engine-unsafe remembered objects split; grouped walker expands through a real raycast, original member target ID submitted; support masonry gaps are 8 without reserving shorter cards' unused row height; three-entry stack list removes right blank; one/three/five-card native picker adapters have balanced gutters; prototype cut-corner gradient action, arrow, five phase bars, switch and three control modes; priority strip hidden, stable fonts and view/restore. Expanded quantities and metadata setup are UI adapters, not card effect evidence.");}
        finally{running=false;ui.RestoreBattlefieldWindows();GamePreferences.SetLanguage(language);}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui){
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
            GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale ready");
            var mouse=UnityEngine.InputSystem.Mouse.current;if(mouse!=null){UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(mouse.position,new Vector2(1,1));UnityEngine.InputSystem.InputSystem.Update();}
            ui.Present(null,1,false,null,null,"");var dense=BattlefieldStressPresentationValidation.Fixture();ui.Present(dense,1,true,_=>{},null,"");yield return Frame(1);
            foreach(var p in dense.players){
                Check(p.battlefield.Select(c=>c.id).Distinct().Count()==575,"Fixture identity");Check(p.battlefield.Sum(c=>Quantity(c.id))==575,"Collapsed quantity lost objects");
                foreach(var group in p.battlefield.GroupBy(ForgeBattlefieldController.ArenaGroupKey))Check(Quantity(group.First().id)==group.Count(),"Same state not folded "+group.First().name);
            }
            Check(!CanvasRoot.GetComponentsInChildren<Transform>().Any(t=>t.name=="StackBack"||t.name=="StackArt"),"Rear cards remain visible");
            foreach(var scroll in CanvasRoot.GetComponentsInChildren<ScrollRect>().Where(s=>s.name.StartsWith("Dense_")&&s.name.Contains("Support"))){
                foreach(var col in scroll.content.Cast<RectTransform>().Where(t=>t.name.StartsWith("Card_")).GroupBy(t=>Mathf.Round(t.anchoredPosition.x)).ToArray()){
                    var ordered=col.OrderByDescending(t=>t.anchoredPosition.y+t.rect.height*.5f).ToArray();
                    for(int i=1;i<ordered.Length;i++)Check(Mathf.Abs(ordered[i-1].anchoredPosition.y-ordered[i-1].rect.height*.5f-ordered[i].anchoredPosition.y-ordered[i].rect.height*.5f-8)<.2f,"Support column gap");
                }
            }
            var copies=dense.players[0].battlefield.Where(c=>c.planeswalker).Take(5).ToArray();copies[2].loyalty++;
            var lands=dense.players[0].battlefield.Where(c=>c.land).Take(10).ToArray();lands[2].tapped=true;
            ui.Present(dense,1,true,_=>{},null,"");yield return Frame();Check(Quantity(copies[0].id)==4&&Quantity(copies[2].id)==1,"Loyalty change did not split");Check(Quantity(lands[0].id)==9&&Quantity(lands[2].id)==1,"Tap state did not split");
            copies[2].loyalty--;lands[2].tapped=false;ui.Present(dense,1,true,_=>{},null,"");yield return Frame();Check(Quantity(copies[0].id)==5&&Quantity(lands[0].id)==10,"States did not regroup");
            var grouped=GameObject.Find("Card_"+copies[0].id).GetComponent<RectTransform>();var region=grouped.GetComponentInParent<ScrollRect>();if(region!=null)region.verticalNormalizedPosition=1-Mathf.Clamp01((-grouped.anchoredPosition.y-grouped.rect.height*.5f-60)/(region.content.rect.height-region.viewport.rect.height));yield return Frame();Click(grouped.GetComponent<Button>());yield return Frame();Check(copies.All(c=>Quantity(c.id)==1),"Grouped walker did not expand");
            // Native exported metadata before legend cleanup. This checks UI
            // grouping only, and does not pretend duplicate legendary cards persist.
            var native=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Output+"/native-grouping.json"));var raw=native.players[0].battlefield;
            var state=BattlefieldStressPresentationValidation.Fixture();state.players[0].battlefield=raw;state.players[1].battlefield=Array.Empty<ForgeCardView>();state.request.requestId+=123;ui.Present(state,1,true,_=>{},null,"");yield return Frame();
            var rings=raw.Where(c=>c.name=="Sol Ring").ToArray();Check(Quantity(rings[0].id)==2&&Quantity(rings[2].id)==1,"Unsafe remembered ring folded");var walkers=raw.Where(c=>c.planeswalker).ToArray();Check(Quantity(walkers[0].id)==2&&Quantity(walkers[2].id)==1,"Native loyalty states merged");var battles=raw.Where(c=>c.battle).ToArray();Check(Quantity(battles[0].id)==2&&Quantity(battles[2].id)==1,"Battle protector states merged");
            // A legal individual target must expand and retain the exact member ID.
            int sent=-1;state.request.kind="InputSelectTargets";state.awaitingInput=true;state.request.requestId++;state.request.options=new[]{new ForgeOption{id="card:"+walkers[1].id,label="Target"}};
            ui.Present(state,1,true,i=>sent=i,null,"");yield return Frame();Click(GameObject.Find("Card_"+walkers[1].id).GetComponent<Button>());Check(sent==0,"Wrong grouped member target identity");
            var target=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/StackInteractions/triggers-target-2.json"));var extra=JsonUtility.FromJson<ForgeStackItem>(JsonUtility.ToJson(target.stackItems[0]));extra.id=999999;target.stackItems=target.stackItems.Concat(new[]{extra}).ToArray();target.stack=target.stackItems.Select(e=>e.description).ToArray();target.stackCount=3;
            ui.Present(target,target.seat,true,_=>{},null,"");yield return Frame();var list=(RectTransform)CanvasRoot.Find("StackList");var strip=list.GetComponentInChildren<ScrollRect>();Check(list.rect.width<=844.1f,"Three targets retain oversized panel");Check(strip.content.rect.width-(16+2*264+244)<=20,"Stack list trailing blank");
            var fetch=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/CardSelection/fetch-foothills-choice.json"));foreach(int count in new[]{1,3,5}){
                var v=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(fetch));
                Func<ForgeOption,bool> hasCard=o=>o.card!=null&&!string.IsNullOrEmpty(o.card.name);
                v.request.options=v.request.options.Where(o=>!hasCard(o)).Concat(v.request.options.Where(hasCard).Take(count)).ToArray();
                v.request.previewCards=Array.Empty<ForgeCardView>();ui.Present(v,v.seat,true,_=>{},null,"");yield return Frame();
                var overlay=CanvasRoot.Find("CardSelectionOverlay");Check(overlay!=null&&overlay.gameObject.activeSelf,"Missing native picker sample "+count);
                var scroll=overlay.GetComponentInChildren<ScrollRect>();var tiles=scroll.content.Cast<RectTransform>().Where(t=>t.name.StartsWith("ChoiceCard_")).ToArray();Check(tiles.Length==count,"Picker sample count");float left=tiles.Min(t=>t.anchoredPosition.x),right=scroll.content.rect.width-tiles.Max(t=>t.anchoredPosition.x+t.rect.width);Check(Mathf.Abs(left-right)<=16.1f,"Picker asymmetric blank");
            }
            var ready=DuelLayoutPresentationValidation.DensityFixture(false);ui.Present(ready,ready.seat,true,_=>{},null,"");yield return Frame();Check(!CanvasRoot.Find("PriorityPill").gameObject.activeSelf,"Priority strip remains visible");
            var action=CanvasRoot.Find("CornerCluster/ControlRow/SkipCombatButton");var face=action.Find("ActionFace").GetComponent<ForgeActionButtonGraphic>();Check(face.canvasRenderer.GetMesh().vertexCount==7&&face.canvasRenderer.GetMesh().colors32.Distinct().Count()>1,"Missing cut-corner gradient");Check(action.Find("Direction").GetComponent<StackControlIcon>().Current==StackControlIcon.Kind.Right,"Missing next arrow");Check(CanvasRoot.Find("CornerCluster/ControlRow/PhaseTrack").childCount==5,"Missing phase bars");
            var mode=CanvasRoot.Find("PriorityControlMode").GetComponent<Button>();var original=mode.GetComponentInChildren<Text>().text;for(int i=0;i<3;i++){Click(mode);yield return Frame(.2f);}Check(mode.GetComponentInChildren<Text>().text==original,"Changed three-mode behavior");
            var font=action.GetComponentInChildren<Text>().fontSize;ui.ViewBattlefield();ui.RestoreBattlefieldWindows();yield return Frame();Check(action.GetComponentInChildren<Text>().fontSize==font,"Restored button font changed");
        }
    }
}
#endif
