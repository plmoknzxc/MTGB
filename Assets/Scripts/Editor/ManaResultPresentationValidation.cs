#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Native input archives plus explicitly labelled transport/display boundaries.
// Does not claim new card-effect or random-distribution verification.
public static class ManaResultPresentationValidation
{
    static string Output=>poolOnly?".tools/mana-glyph-review-1004":".tools/mana-result-ui";
    static bool poolOnly;
    static bool running;
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static Rect ScreenBounds(RectTransform rect)
    {
        var corners=new Vector3[4];rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners.Min(c=>c.x),corners.Min(c=>c.y),corners.Max(c=>c.x),corners.Max(c=>c.y));
    }
    static ForgeSeatView Read(string file)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(file));
    static readonly FieldInfo Expiry=typeof(ForgeBattlefieldController).GetField("noticeExpires",BindingFlags.Instance|BindingFlags.NonPublic);
    static readonly FieldInfo Active=typeof(ForgeBattlefieldController).GetField("activeNotice",BindingFlags.Instance|BindingFlags.NonPublic);
    public static void Run()=>Start(false);
    public static void RunPoolOnly()=>Start(true);
    static void Start(bool onlyPool)
    {
        if(running)return;if(!Application.isPlaying)throw new Exception("Enter IntegratedBattlefield Play Mode first.");
        poolOnly=onlyPool;
        running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("ManaResultValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator iterator)
    {
        string language=GamePreferences.Language;var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=banner!=null&&banner.enabled;
        if(banner!=null)banner.enabled=false;
        try {
            while(true){object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"/result.txt",poolOnly?"PASS: zh-CN/en-US/ja-JP native pool payment/retry/control-release replay; exact color indexes with raycast/stale/duplicate/viewer guards; white counts below/right of rendered shield glyphs, fixed fonts, six-count/empty/increase boundaries, no row backgrounds, bounds clear of controls. Pool-only incremental review; random-result assertions were not rerun.":"PASS: native Mana Clash IDs consumed once in order across repeated/rolling snapshots; procedural flip/tumble motion and native final die value, no random text banner, nonblocking graphics, fixed fonts, expiry/null-session reset; native turn-control pool owner/release. Native blue pool raycast submits exact offered index with stale/duplicate/viewer guards. Explicit transport/display fixtures: private block rejection interrupts/resumes flips (IDs remapped); all color indexes, unavailable/empty colors, six counts, no backgrounds/outlines, decrement/zero/increase pulse; pool separated from priority/controls. zh-CN/en-US/ja-JP; no new effect, network timing or dice-distribution claim.");
        }finally{running=false;GamePreferences.SetLanguage(language);if(banner!=null)banner.enabled=enabled;}
    }
    static IEnumerator Rendered(){yield return new WaitForEndOfFrame();yield return null;yield return null;Canvas.ForceUpdateCanvases();}
    static IEnumerator Shot(string name){yield return Rendered();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForEndOfFrame();}
    static void Click(Button button)
    {
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Pool cannot be clicked: "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool change=GamePreferences.Language!=language;GamePreferences.SetLanguage(language);
            float deadline=Time.realtimeSinceStartup+8;while((!GamePreferences.Ready||change&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(GamePreferences.Ready&&(!change||revision!=GamePreferences.Revision),"Locale not ready");
            var native=poolOnly?null:Read("Tools/Validation/Reports/RandomMechanics/mana-clash-resolved.json");
            var die=poolOnly?null:Read("Tools/Validation/Reports/RandomMechanics/dice-high-resolved.json");
            if(!poolOnly) {
            Check(native.notices.Length>1,"Native batch must contain multiple results");
            var batch=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(native));
            batch.notices=native.notices.Take(3).ToArray();int calls=0;
            ui.Present(null,batch.seat,false,null,null,"");ui.Present(batch,batch.seat,true,_=>calls++,null,"");yield return Rendered();
            var toast=GameObject.Find("RandomResultAnimation");
            Check(toast!=null&&toast.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),"Results block input");
            Check(toast.GetComponentsInChildren<Text>().All(t=>!t.resizeTextForBestFit),"Result font changes");
            Check(GameObject.Find("ResultToast")==null,"Random result still opens text banner");
            var coinGraphic=toast.GetComponentInChildren<RandomResultGraphic>();
            Check(coinGraphic.Coin&&coinGraphic.Outcome==native.notices[0].value,"Coin side replaced with another result");
            float firstProgress=coinGraphic.Progress;yield return new WaitForSecondsRealtime(.2f);
            Check(coinGraphic.Progress>firstProgress&&coinGraphic.rectTransform.anchoredPosition.y>0,"Coin does not rise/flip");
            Canvas.ForceUpdateCanvases();Check(coinGraphic.GetComponent<CanvasRenderer>()!=null&&coinGraphic.canvasRenderer.GetMesh()?.vertexCount>0,"Coin has no rendered mesh");
            Check(((ForgeNotice)Active.GetValue(ui)).id==native.notices[0].id,"Coalesced snapshot skipped first result");
            float expiry=(float)Expiry.GetValue(ui);ui.Present(batch,batch.seat,true,_=>calls++,null,"");
            Check((float)Expiry.GetValue(ui)==expiry,"Duplicate restarted result timer");
            yield return Shot(language+"-coin-batch");
            // Simulate a later bounded rolling snapshot: preserve exact native
            // identities/values, omit the consumed head and add two new IDs.
            batch.notices=native.notices.Skip(1).Take(4).ToArray();ui.Present(batch,batch.seat,true,_=>calls++,null,"");
            for(int i=1;i<5;i++) {
                Expiry.SetValue(ui,Time.unscaledTime-1);yield return Rendered();
                Check(((ForgeNotice)Active.GetValue(ui))?.id==native.notices[i].id,"Result omitted/repeated/out of order at "+i);
            }
            Expiry.SetValue(ui,Time.unscaledTime-1);yield return Rendered();Check(!toast.activeSelf,"Queue did not expire");
            ui.Present(batch,batch.seat,true,_=>calls++,null,"");yield return Rendered();Check(!toast.activeSelf,"Consumed batch replayed");
            // Transport/display fixture: actual private native block rejection
            // delivered during actual public flips; IDs remapped for one stream.
            ui.Present(null,batch.seat,false,null,null,"");batch.notices=native.notices.Take(3).ToArray();
            ui.Present(batch,batch.seat,true,_=>calls++,null,"");yield return Rendered();
            var blocked=File.ReadLines("Tools/Validation/Reports/CombatAllocation/menace.jsonl").Select(JsonUtility.FromJson<ForgeWireMessage>)
                .First(m=>m.type=="request"&&m.request.kind=="InputBlock"&&m.views[m.request.seat-1].notices.Length>0);
            var rejection=blocked.views[blocked.request.seat-1];rejection.request=blocked.request;rejection.awaitingInput=true;
            var error=rejection.notices.Single(n=>n.kind=="notice");error.id=batch.notices.Last().id+1;
            rejection.notices=batch.notices.Concat(new[]{error}).ToArray();ui.Present(rejection,rejection.seat,true,_=>calls++,null,"");yield return Rendered();
            Check(((ForgeNotice)Active.GetValue(ui))?.id==error.id,"Private action error delayed behind random results");
            yield return Shot(language+"-error-during-results");
            foreach(var expected in batch.notices) {
                Expiry.SetValue(ui,Time.unscaledTime-1);yield return Rendered();
                Check(((ForgeNotice)Active.GetValue(ui))?.id==expected.id,"Interrupted result omitted/reordered");
            }
            ui.Present(null,die.seat,false,null,null,"");ui.Present(die,die.seat,true,_=>calls++,null,"");yield return Rendered();
            yield return new WaitForSecondsRealtime(1.3f);
            Check(GameObject.Find("RandomResultAnimation").GetComponentInChildren<RandomResultGraphic>().Outcome=="20","Die value differs from native roll");
            Check(!GameObject.Find("RandomResultAnimation").GetComponentInChildren<RandomResultGraphic>().Coin,"Die drawn as coin");
            var dieGraphic=GameObject.Find("RandomResultAnimation").GetComponentInChildren<RandomResultGraphic>();
            Check(dieGraphic.canvasRenderer.GetMesh()?.vertexCount>0,"Die has no rendered mesh");
            yield return Shot(language+"-die-20");
            ui.Present(null,die.seat,false,null,null,"");yield return Rendered();Check(GameObject.Find("ResultToast")==null,"Reset leaves prior result");
            Check(calls==0,"Passive result submitted a game option");
            }

            var controlled=Read("Tools/Validation/Reports/PlayerControl/turn-one-casting-Opt.json");
            ui.Present(controlled,controlled.seat,true,null,null,"");yield return Rendered();
            var pool=GameObject.Find("ManaPoolChips").transform;
            Check(((RectTransform)pool).rect.width<=420&&((RectTransform)pool).rect.height<=34,"Pool too large");
            Check(pool.Find("Pool_U/Count").GetComponent<Text>().text=="2","Wrong action-player pool");
            // Display-only all-color/count boundary, independent of rule proof.
            var owner=controlled.players.First(p=>p.seat==controlled.request.playerSeat);
            owner.manaPool=new[]{1,2,3,4,12,999};ui.Present(controlled,controlled.seat,true,null,null,"");yield return Rendered();
            string[] symbols={"W","U","B","R","G","C"};
            for(int i=0;i<6;i++) {
                var chip=pool.Find("Pool_"+symbols[i]);var text=chip.Find("Count").GetComponent<Text>();
                Check(chip.gameObject.activeSelf&&text.text==owner.manaPool[i].ToString()&&!text.resizeTextForBestFit,"Pool count hidden/wrong/font shrinks");
                Check(chip.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),"Passive mana blocks actions");
                Check(chip.GetComponent<Image>().color.a==0&&chip.GetComponent<Outline>()==null,"Pool still has tile/frame");
                var symbol=chip.Find("Symbol").GetComponent<Text>();Check(symbol!=null&&symbol.font!=null&&symbol.canvasRenderer.GetMesh()?.vertexCount>0,"Pool vector glyph not rendered");
                Check(symbol.font==Resources.Load<Font>("UI/ManaSymbols/ManaGlyphs")&&symbol.GetComponent<PreferenceText>().KeepFont,"Locale replaced icon font");
                Check(symbol.text==(i==5?"\uE904":char.ConvertFromUtf32(0xE600+i)),"Wrong mana glyph / energy in pool");
                Check(chip.GetComponentsInChildren<Image>().Length==1,"Mana contains decorative border/image");
                Check(text.color==Color.white&&text.rectTransform.anchoredPosition.y<symbol.rectTransform.anchoredPosition.y,"Pool digits colored or positioned beside/above glyph");
            }
            var corners=new Vector3[4];((RectTransform)pool).GetWorldCorners(corners);float bottom=corners[0].y,top=corners[1].y;
            Check(corners[0].x>=0&&corners[3].x<=Screen.width-12,"Mana counts run off screen");
            var poolBounds=ScreenBounds((RectTransform)pool);
            foreach(string name in new[]{"PriorityControlMode","PriorityPill","ControlRow"}) {
                var control=GameObject.Find(name)?.GetComponent<RectTransform>();
                Check(control==null||!poolBounds.Overlaps(ScreenBounds(control)),"Pool overlaps "+name);
            }
            yield return new WaitForSecondsRealtime(.5f);yield return Shot(language+"-pool-six-colors");
            owner.manaPool=new[]{0,3,0,0,0,0};ui.Present(controlled,controlled.seat,true,null,null,"");
            yield return new WaitForSecondsRealtime(.18f);
            Check(pool.Find("Pool_U").localScale.x>1,"New mana has no increase feedback");
            Check(pool.Cast<Transform>().Count(t=>t.gameObject.activeSelf)==1,"Zero colors remain displayed");
            yield return new WaitForSecondsRealtime(.5f);Check(Mathf.Approximately(pool.Find("Pool_U").localScale.x,1),"Pool pulse never settles");
            owner.manaPool=new int[6];ui.Present(controlled,controlled.seat,true,null,null,"");yield return Rendered();Check(!pool.gameObject.activeSelf,"Empty pool shows stale amounts");
            var released=Read("Tools/Validation/Reports/PlayerControl/turn-one-released.json");ui.Present(released,released.seat,true,null,null,"");yield return Rendered();
            var releasedOwner=released.players.First(p=>p.seat==released.seat);
            Check(pool.gameObject.activeSelf==releasedOwner.manaPool.Any(v=>v>0),"Control release retains prior pool");

            // Actual InputPayMana snapshot: no invented color permission.
            var payment=Read("Tools/Validation/Reports/ManaPoolPayments/blue-payment.json");int paid=0,last=-1;
            var beforeCast=Read("Tools/Validation/Reports/ManaPoolPayments/pool-retry-casting-Opt.json");
            ui.Present(null,payment.seat,false,null,null,"");int castCalls=0;
            ui.Present(beforeCast,beforeCast.seat,true,_=>castCalls++,null,"");yield return Rendered();
            var opt=beforeCast.players.First(p=>p.seat==beforeCast.request.playerSeat).hand.First(c=>c.name=="Opt");
            Click(GameObject.Find("Card_"+opt.id).GetComponent<Button>());Check(castCalls==1,"Actual cast click missing");
            ui.Present(payment,payment.seat,true,i=>{paid++;last=i;},null,"");yield return new WaitForSecondsRealtime(.5f);yield return Rendered();
            Check((bool)typeof(ForgeBattlefieldController).GetProperty("IsPayingPreview",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui),"Pool not tested with actual casting preview");
            Check(GameObject.Find("TargetOrigin").transform.Find("PaymentChoices").childCount==0,"Duplicate text mana payment buttons");
            var blue=pool.Find("Pool_U").GetComponent<Button>();Check(blue.interactable,"Native blue mana not clickable");
            ui.Choose(ui.OptionIndex("mana:U"),payment.request.requestId-1);Check(paid==0,"Stale pool request submitted");
            yield return Shot(language+"-manual-pool-payment");Click(blue);blue.onClick.Invoke();
            Check(paid==1&&last==Array.FindIndex(payment.request.options,o=>o.id=="mana:U"),"Pool chose wrong option/duplicated submission");
            Check(!blue.interactable&&!blue.GetComponent<Image>().raycastTarget,"Submitted pool still intercepts input");
            var paidState=Read("Tools/Validation/Reports/ManaPoolPayments/blue-paid-scry.json");
            ui.Present(paidState,paidState.seat,true,null,null,"");yield return Rendered();
            Check(pool.Find("Pool_U/Count").GetComponent<Text>().text=="1","Native paid pool not refreshed");
            ui.Present(payment,payment.seat,false,_=>paid++,null,"");yield return Rendered();
            Check(!blue.interactable,"Viewer can spend controlled player's mana");blue.onClick.Invoke();Check(paid==1,"Viewer pool submission");
            var wrong=Read("Tools/Validation/Reports/ManaPoolPayments/wrong-color-before.json");
            var retry=Read("Tools/Validation/Reports/ManaPoolPayments/wrong-color-retry.json");
            int retryCalls=0;ui.Present(wrong,wrong.seat,true,_=>retryCalls++,null,"");yield return Rendered();Click(blue);
            Check(retryCalls==1&&!blue.interactable,"Wrong-color response did not freeze submission");
            Check(retry.request.requestId!=wrong.request.requestId&&retry.request.contextId==wrong.request.contextId,"Native retry is not same-context fresh input");
            ui.Present(retry,retry.seat,true,_=>retryCalls++,null,"");yield return Rendered();
            Check(blue.interactable&&pool.Find("Pool_U/Count").GetComponent<Text>().text=="1","Native retry does not reopen pool");
            ui.Choose(ui.OptionIndex("mana:U"),wrong.request.requestId);Check(retryCalls==1,"Old payment accepted after native retry");
            yield return Shot(language+"-pool-payment-retry");
            // Transport fixture covers all six literal IDs, ordering and absent
            // native options; this is not six additional Forge effect tests.
            var payOwner=payment.players.First(p=>p.seat==payment.request.playerSeat);payOwner.manaPool=new[]{1,1,1,1,1,1};
            payment.request.options=payment.request.options.Concat(symbols.Where(s=>s!="U").Select(s=>new ForgeOption{id="mana:"+s,label="Pay {"+s+"}"})).ToArray();
            foreach(string s in symbols) {
                int chosen=-1;ui.Present(payment,payment.seat,true,i=>chosen=i,null,"");yield return Rendered();
                Click(pool.Find("Pool_"+s).GetComponent<Button>());Check(chosen==Array.FindIndex(payment.request.options,o=>o.id=="mana:"+s),"Color ID mapping wrong: "+s);
            }
            payment.request.options=payment.request.options.Where(o=>o.id!="mana:R").ToArray();ui.Present(payment,payment.seat,true,null,null,"");yield return Rendered();
            Check(!pool.Find("Pool_R").GetComponent<Button>().interactable,"Count invents unoffered payment permission");
            payOwner.manaPool[1]=0;ui.Present(payment,payment.seat,true,null,null,"");yield return Rendered();Check(!blue.gameObject.activeSelf&&!blue.interactable,"Empty offered color still clickable");

            if(language=="zh-CN" && !poolOnly) {
                // Sample actual Update motion, not synthetic render timestamps.
                foreach(string kind in new[]{"coin","die"}) {
                    var animation=kind=="coin"?native:die;animation.notices=animation.notices.Take(1).ToArray();
                    ui.Present(null,1,false,null,null,"");ui.Present(animation,animation.seat,true,null,null,"");
                    for(int frame=0;frame<18;frame++) {
                        yield return new WaitForSecondsRealtime(.08f);yield return new WaitForEndOfFrame();
                        ScreenCapture.CaptureScreenshot(Output+"/motion-"+kind+"-"+frame.ToString("D2")+".png");
                    }
                }
            }
        }
        ui.Present(null,1,false,null,null,"");yield return Rendered();
    }
}
#endif
