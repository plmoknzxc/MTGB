#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CombatAllocationPresentationValidation
{
    const string Output=".tools/combat-allocation-ui",Input="Tools/Validation/Reports/CombatAllocation/";
    static bool running;
    static readonly string[] Cases={"trample-reset-release","trample-overassign","trample-save-blocker","trample-seat-two","damaged-blocker","deathtouch-trample","deathtouch","first-strike","double-strike-blocked","double-strike-open","menace","block-multiple","as-unblocked-accept","as-unblocked-decline","planeswalker","many-blockers"};
    static void Check(bool b,string s){if(!b)throw new Exception(s);}
    static ForgeSeatView Read(string file)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Input+file+".json"));
    static ForgeWireMessage[] Wire(string name,Func<ForgeRequest,bool> filter)=>File.ReadLines(Input+name+".jsonl").Select(JsonUtility.FromJson<ForgeWireMessage>).Where(m=>m.type=="request"&&filter(m.request)).ToArray();
    static ForgeSeatView View(ForgeWireMessage m){var v=m.views[m.request.seat-1];v.request=m.request;v.awaitingInput=true;return v;}
    public static void Run(){if(running)return;Check(Application.isPlaying,"Enter IntegratedBattlefield Play Mode.");running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("CombatAllocationValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));}
    static IEnumerator Guard(IEnumerator test)
    {
        string language=GamePreferences.Language;var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=banner!=null&&banner.enabled;if(banner!=null)banner.enabled=false;
        try{while(true){object next;try{if(!test.MoveNext())break;next=test.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: actual native combat requests in zh-CN/en-US/ja-JP at current scale: source/recipient identities and native lethal values, exact plus/minus/reset/confirm mapping and gates, stable panels/cards/fonts, fixed-size localized labels; first-strike/double-strike context transition remains actionable, ordinary wheel and last of eleven recipients reachable; invalid menace confirmation gets fresh input with private localized error and can finish blocking; independent stale/duplicate/nonacting guards; exact resolved life, survivor count/expansion, death and aura-return displays. Native effects separate; not all combat, all languages/text/art, devices, animation or live network.");}
        finally{running=false;GamePreferences.SetLanguage(language);if(banner!=null)banner.enabled=enabled;}
    }
    static IEnumerator Rendered(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static IEnumerator Shot(string name){yield return new WaitForSecondsRealtime(.15f);yield return Rendered();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForEndOfFrame();}
    static void Click(Button b){var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.TransformPoint(((RectTransform)b.transform).rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Pointer blocked: "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);}
    static string NextAction(ForgeRequest r,ForgeRequest next)
    {
        if(next==null||r.contextId!=next.contextId)return "confirm-amount";
        if(next.selectedCount==0&&r.selectedCount>0)return "reset-amount";
        for(int i=0;i<r.allocations.Length;i++)if(next.allocations[i].amount>r.allocations[i].amount)return "amount-add:"+r.allocations[i].id;
        for(int i=0;i<r.allocations.Length;i++)if(next.allocations[i].amount<r.allocations[i].amount)return "amount-sub:"+r.allocations[i].id;
        throw new Exception("No native assignment transition");
    }
    static Button Action(GameObject panel,string id)=>id.StartsWith("amount-")?panel.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Recipient_"+id.Split(':')[1]).Find(id.StartsWith("amount-add:")?"Plus":"Minus").GetComponent<Button>():panel.transform.Find(id=="reset-amount"?"Reset":"Confirm").GetComponent<Button>();
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
            int revision=GamePreferences.Revision;bool changed=GamePreferences.Language!=language;GamePreferences.SetLanguage(language);float end=Time.realtimeSinceStartup+8;while((!GamePreferences.Ready||changed&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<end)yield return null;Check(GamePreferences.Ready&&(!changed||revision!=GamePreferences.Revision),"Locale not ready");
            foreach(string name in Cases){
                var wire=Wire(name,r=>r.kind=="AssignAmount");ui.Present(null,1,false,null,null,"");GameObject oldPanel=null;long oldContext=-1;var tiles=new Dictionary<int,GameObject>();int font=-1;
                for(int step=0;step<wire.Length;step++){
                    var v=View(wire[step]);var r=v.request;int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};ui.Present(v,v.seat,true,submit,null,"");yield return Rendered();
                    var panel=GameObject.Find("DamageAllocation");Check(panel!=null,"Allocation missing");if(oldPanel!=null)Check(panel==oldPanel,"Allocation outer panel changed");
                    var title=panel.transform.Find("Title").GetComponent<Text>();Check(title.text==(language=="zh-CN"?"分配战斗伤害":language=="ja-JP"?"戦闘ダメージの割り振り":"Assign combat damage"),"Allocation title locale");
                    Check(panel.transform.Find("Source").GetComponent<Text>().text.EndsWith(GamePreferences.CardFaceName(r.sourceCard.name)),"Wrong assignment source");
                    foreach(string label in new[]{"Title","Source","Remaining"}){var t=panel.transform.Find(label).GetComponent<Text>();Check(!t.resizeTextForBestFit&&t.preferredHeight<=t.rectTransform.rect.height,"Header clips/shrinks: "+label);}
                    if(font>=0)Check(title.fontSize==font,"Assignment changes title font");font=title.fontSize;
                    foreach(var row in r.allocations){var tile=panel.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Recipient_"+row.id).gameObject;
                        if(oldContext==r.contextId&&tiles.TryGetValue(row.id,out var old))Check(tile==old,"Recipient recreated in same draft");tiles[row.id]=tile;
                        Check(tile.transform.Find("Amount").GetComponent<Text>().text==row.amount.ToString(),"Wrong damage amount");
                        var label=tile.transform.Find("Name").GetComponent<Text>();string expected=row.targetKind=="card"?GamePreferences.CardFaceName(row.card.name):language=="zh-CN"?$"玩家 {row.playerSeat}":language=="ja-JP"?$"プレイヤー {row.playerSeat}":$"Player {row.playerSeat}";Check(label.text==expected,"Wrong/localization missing recipient");
                        foreach(string field in new[]{"Name","Amount","Lethal"}){var t=tile.transform.Find(field).GetComponent<Text>();Check(!t.resizeTextForBestFit&&t.preferredHeight<=t.rectTransform.rect.height,"Recipient text clips/shrinks: "+field+" "+t.text);}
                        var lethal=tile.transform.Find("Lethal").GetComponent<Text>();Check(row.hasLethal?lethal.text.EndsWith(row.lethal.ToString()):lethal.text=="","Native lethal number missing/spurious");
                        foreach(string direction in new[]{"Plus","Minus"}){string id=(direction=="Plus"?"amount-add:":"amount-sub:")+row.id;Check(tile.transform.Find(direction).GetComponent<Button>().interactable==r.options.Any(o=>o.id==id),"Illegal/missing amount action");}
                    }
                    var confirm=panel.transform.Find("Confirm").GetComponent<Button>();Check(confirm.interactable==r.options.Any(o=>o.id=="confirm-amount"),"Confirmation ignores native amount gate");Check(panel.transform.Find("Reset").GetComponent<Button>().interactable,"Reset unavailable");Check(!panel.transform.Find("Skip").gameObject.activeInHierarchy,"Unexpected skip in listed native scenarios");
                    string action=NextAction(r,step+1<wire.Length?wire[step+1].request:null);int index=Array.FindIndex(r.options,o=>o.id==action);Check(index>=0,"Native transition not an available option: "+action);var button=Action(panel,action);
                    if(oldContext!=r.contextId)yield return Shot(language+"-"+name+"-"+v.phase);
                    if(name=="many-blockers"&&step==0){var scroll=panel.GetComponentsInChildren<ScrollRect>().Single();Check(r.allocations.Length==11&&scroll.content.rect.width>scroll.viewport.rect.width+1,"Eleven recipients did not overflow");scroll.horizontalNormalizedPosition=0;ExecuteEvents.Execute(scroll.gameObject,new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,8)},ExecuteEvents.scrollHandler);Check(scroll.horizontalNormalizedPosition>0,"Ordinary wheel cannot move horizontal recipient list");scroll.horizontalNormalizedPosition=1;yield return Rendered();var lastTile=tiles[r.allocations.Last().id].GetComponent<RectTransform>();var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,lastTile);Check(bounds.center.x>=scroll.viewport.rect.xMin&&bounds.center.x<=scroll.viewport.rect.xMax,"Last native recipient unreachable");yield return Shot(language+"-many-blockers-last");scroll.horizontalNormalizedPosition=0;yield return Rendered();}
                    ui.Choose(index,r.requestId-1);Check(calls==0,"Actionable stale assignment submitted");Click(button);button.onClick.Invoke();Check(calls==1&&last==index,"Assignment pointer/index/duplicate wrong");
                    ui.Present(v,v.seat,false,submit,null,"");yield return Rendered();Check(!Action(GameObject.Find("DamageAllocation"),action).interactable,"Nonacting assignment active");ui.Choose(index,r.requestId);Check(calls==1,"Nonacting assignment submitted");oldPanel=panel;oldContext=r.contextId;
                }
            }
            var block=Wire("menace",r=>r.kind=="InputBlock");Check(block.Length==4,"Native invalid-block retry sequence missing");int submitted=0,lastBlock=-1;Action<int> blockSubmit=i=>{submitted++;lastBlock=i;};ui.Present(null,block[1].request.seat,false,null,null,"");var before=View(block[1]);ui.Present(before,before.seat,true,blockSubmit,null,"");yield return Rendered();var done=GameObject.Find("SkipCombatButton").GetComponent<Button>();Check(done.interactable,"Illegal proposal cannot be submitted to native validator");Click(done);Check(submitted==1&&before.request.options[lastBlock].id=="ok","Block confirmation wrong");
            var retry=View(block[2]);ui.Present(retry,retry.seat,true,blockSubmit,null,"");yield return Rendered();Check(done.interactable,"New request stayed submitted/disabled after native rejection");var toast=GameObject.Find("ResultToast");Check(toast!=null,"Private block error not shown");var error=toast.transform.Find("ResultText").GetComponent<Text>();Check(!error.text.Contains("cannot be blocked with")&&error.text.Contains(GamePreferences.CardFaceName("Boggart Brute")),"Pinned block error not localized with source");Check(error.preferredHeight<=error.rectTransform.rect.height,"Block error clipped");yield return Shot(language+"-menace-retry");
            var choice=retry.request.options.First(o=>o.id.StartsWith("block:")&&!o.selected);Click(GameObject.Find("Card_"+choice.defenderId).GetComponent<Button>());yield return Rendered();Click(GameObject.Find("Card_"+choice.attackerId).GetComponent<Button>());Check(submitted==2&&retry.request.options[lastBlock].id==choice.id,"Retry blocker pair cannot be chosen");var complete=View(block[3]);ui.Present(complete,complete.seat,true,blockSubmit,null,"");yield return Rendered();Click(done);Check(submitted==3&&complete.request.options[lastBlock].id=="ok","Corrected blocking cannot finish");yield return Shot(language+"-menace-corrected");
            int other=3-retry.seat;ui.Present(null,other,false,null,null,"");ui.Present(block[2].views[other-1],other,false,null,null,"");yield return Rendered();Check(GameObject.Find("ResultToast")==null,"Private validation error visible to other seat");
            foreach(string name in new[]{"deathtouch-trample","first-strike","double-strike-blocked","double-strike-open","block-multiple","as-unblocked-accept","planeswalker","many-blockers"}){
                var v=Read(name+"-resolved");ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,true,_=>{},null,"");yield return Rendered();Check(GameObject.Find("DamageAllocation")==null,"Resolved combat leaves assignment overlay");foreach(var p in v.players){var plate=GameObject.Find(p.seat==v.seat?"PlayerPlate":"OpponentPlate");Check(plate.transform.Find("Life").GetComponent<Text>().text==p.life.ToString(),"Resolved combat life display");
                    foreach(var group in p.battlefield.Where(c=>c.creature).GroupBy(c=>c.name+"/"+c.power+"/"+c.toughness+"/"+c.damage)){
                        var members=group.ToArray();var visible=members.Select(c=>GameObject.Find("Card_"+c.id)).FirstOrDefault(g=>g!=null);Check(visible!=null,"Survivor group absent");
                        if(members.Any(c=>GameObject.Find("Card_"+c.id)==null)){Check(visible.GetComponentsInChildren<Text>().Any(t=>t.text=="×"+members.Length),"Collapsed survivor count wrong");Click(visible.GetComponent<Button>());yield return Rendered();}
                        foreach(var c in members)Check(GameObject.Find("Card_"+c.id)!=null,"Expanded exact survivor ID missing");
                    }
                }
                foreach(var c in v.players.SelectMany(p=>p.graveyardCards).Where(c=>c.creature||c.planeswalker))Check(GameObject.Find("Card_"+c.id)==null,"Dead combatant remains in play");if(name=="double-strike-blocked"||name=="many-blockers"){var aura=v.players.SelectMany(p=>p.hand).Single(c=>c.name=="Rancor");Check(GameObject.Find("Card_"+aura.id)!=null,"Returning aura missing from hand");}yield return Shot(language+"-"+name+"-resolved");
            }
            ui.Present(null,1,false,null,null,"");yield return Rendered();
        }
    }
}
#endif
