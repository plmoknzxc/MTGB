#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class KnowledgeLifecyclePresentationValidation
{
    const string Output=".tools/knowledge-lifecycle-1007/ui";
    static bool running;
    public static void RunHoverOnly()
    {
        if(!Application.isPlaying)throw new Exception("Play mode required");
        if(running)return;running=true;
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("KnowledgeHoverValidation").AddComponent<ForgeBattlefieldController>();
        bool enabled=ui.enabled;ui.enabled=false;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/hover-result.txt","RUNNING");
        IEnumerator Validate() {
            var data=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(".tools/knowledge-lifecycle-1007/inputs/private-peek-reveal-0-1.json"));
            ui.Present(data,1,true,i=>{},null,"");yield return new WaitForSecondsRealtime(.5f);
            foreach(var card in data.request.previewCards) {
                var root=GameObject.Find("Card_"+card.id);ExecuteEvents.Execute(root,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);yield return new WaitForSecondsRealtime(.6f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
                var face=(RectTransform)root.transform.Find("Visual/Border/Face");var corners=new Vector3[4];face.GetWorldCorners(corners);
                if(corners.Any(p=>p.x<0||p.x>Screen.width||p.y<0||p.y>Screen.height))throw new Exception("Revealed hand hover clipped "+card.id);
                if(face.GetComponent<Image>().sprite==null||root.transform.Find("Visual").localScale.x<1.3f)throw new Exception("Revealed hand hover unreadable "+card.id);
                ScreenCapture.CaptureScreenshot(Output+"/private-hover-"+card.id+".png");yield return new WaitForEndOfFrame();
                ExecuteEvents.Execute(root,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);yield return new WaitForSecondsRealtime(.2f);
            }
        }
        IEnumerator Guard() {
            try {
                var run=Validate();while(true) {object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception e){File.WriteAllText(Output+"/hover-result.txt","FAIL "+e);throw;}yield return next;}
                File.WriteAllText(Output+"/hover-result.txt","PASS actual private Peek direct opponent hand: both authorized card faces hover fully inside viewport with full artwork and enlarged scale. Separate UI increment; background migration banner excluded.");
            } finally {ui.Present(null,1,false,null,null,"");ui.enabled=enabled;running=false;}
        }
        ui.StartCoroutine(Guard());
    }
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Play mode required");
        if(running)return;running=true;
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("KnowledgeValidation").AddComponent<ForgeBattlefieldController>();
        bool previous=ui.enabled;ui.enabled=false;
        var modules=UnityEngine.Object.FindObjectsByType<BaseInputModule>(FindObjectsSortMode.None);var enabled=modules.Select(m=>m.enabled).ToArray();foreach(var m in modules)m.enabled=false;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/steps.log","");
        void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        void Pass(string message)=>File.AppendAllText(Output+"/steps.log",message+" PASS\n");
        ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(".tools/knowledge-lifecycle-1007/inputs/"+name+".json"));
        IEnumerator Frame(){yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
        void Click(GameObject root) {
            Check(root!=null,"Missing click root");var rect=(RectTransform)root.transform;var position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var e=new PointerEventData(EventSystem.current){position=position,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
            Check(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(root.transform),"Blocked ray "+root.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);
        }
        int[] VisibleCards()=>GameObject.Find("CardSelection").GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("ChoiceCard_")).Select(t=>int.Parse(t.name.Substring(11))).OrderBy(i=>i).ToArray();
        void SameCards(IEnumerable<int> expected)=>Check(VisibleCards().SequenceEqual(expected.OrderBy(i=>i)),"Wrong visible card identities");
        IEnumerator Validate() {
            GamePreferences.SetLanguage("zh-CN");float until=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Language not ready");
            var reveal=Read("private-peek-reveal-0-1");int calls=0,sent=-1;
            ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(reveal,1,true,i=>{calls++;sent=i;},null,"");yield return Frame();
            Check(GameObject.Find("CardSelection")==null,"Short known hand should use direct hand presentation");
            foreach(var c in reveal.request.previewCards)Check(GameObject.Find("Card_"+c.id)!=null,"Direct revealed hand identity missing");
            Check(GameObject.Find("CloseReviewCards")==null,"Native reveal has external dismiss");
            ScreenCapture.CaptureScreenshot(Output+"/private-reveal.png");yield return Frame();
            Check(!ui.TrySubmitPresentedOption(0,reveal.request.requestId-1),"Stale reveal accepted");
            Click(GameObject.Find("HandAction_0"));Check(calls==1&&reveal.request.options[sent].id=="confirm-cards","Reveal confirmation wrong native option");
            Check(!ui.TrySubmitPresentedOption(sent,reveal.request.requestId),"Duplicate reveal accepted");Pass("private Peek exact native reveal IDs, ray acknowledgement, stale/duplicate guards");
            ui.Present(Read("private-peek-after-0"),1,true,i=>calls++,null,"");yield return Frame();Check(GameObject.Find("CardSelection")==null,"Acknowledged reveal persists over priority");Pass("native priority removes transient reveal window");
            ui.Present(null,2,false,null,null,"");yield return Frame();ui.Present(Read("private-peek-reveal-0-1-other"),2,false,i=>calls++,null,"");yield return Frame();Check(GameObject.Find("CardSelection")==null,"Private reveal shown to other seat");Pass("other recipient cannot see private reveal window");
            var order=Read("private-index-order-1");calls=0;sent=-1;ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(order,1,true,i=>{calls++;sent=i;},null,"");yield return Frame();
            var candidate=order.request.options.First(o=>o.card!=null&&!o.selected&&!o.disabled);
            Click(GameObject.Find("ChoiceCard_"+candidate.card.id).transform.Find("Artwork").gameObject);Check(calls==1&&order.request.options[sent].id==candidate.id,"Private order submits wrong identity");Pass("private Index order uses existing native ordering UI and exact ray ID");
            ui.Present(Read("private-index-after-0"),1,true,i=>calls++,null,"");yield return Frame();Check(GameObject.Find("CardSelection")==null,"Private order window persists");Pass("private look window clears after native order completes");
            foreach(int local in new[]{1,2}) {
                var publicReveal=Read("public-mulch-reveal-0-"+local+(local==2?"-other":""));calls=0;sent=-1;
                ui.Present(null,local,false,null,null,"");yield return Frame();ui.Present(publicReveal,local,true,i=>{calls++;sent=i;},null,"");yield return Frame();
                SameCards(publicReveal.request.previewCards.Select(c=>c.id));Check(GameObject.Find("CloseReviewCards")==null,"Public reveal dismiss outside native acknowledgement");
                if(local==1){ScreenCapture.CaptureScreenshot(Output+"/public-reveal.png");yield return Frame();}
                Click(GameObject.Find("SelectionAction_0"));Check(calls==1&&publicReveal.request.options[sent].id=="confirm-cards","Public reveal confirmation index");
                ui.Present(Read("public-mulch-after-0"+(local==2?"-other":"")),local,false,i=>calls++,null,"");yield return Frame();Check(GameObject.Find("CardSelection")==null,"Public reveal does not expire");
                Pass("actual Mulch seat "+local+" exact public reveal IDs, acknowledgement ray and next priority cleanup");
            }
            foreach(int local in new[]{1,2}) {
                string suffix=local==2?"-other":"";var publicView=Read("public-source-draw-after-0"+suffix);var remembered=Read("public-source-draw-after-1"+suffix);var drawn=Read("public-source-draw-after-2"+suffix);
                calls=0;ui.Present(null,local,false,null,null,"");yield return Frame();ui.Present(publicView,local,false,i=>calls++,null,"");yield return Frame();
                Click(GameObject.Find("ZonePiles_"+(local==1?"PlayerBottomLeft":"OpponentTopRight")+"/Pile_牌库"));yield return Frame();SameCards(new[]{publicView.players[0].libraryTop.id});Check(calls==0,"Library browse submits input");Pass("seat "+local+" public library top browsed read-only with exact identity");
                ui.Present(remembered,local,false,i=>calls++,null,"");yield return Frame();SameCards(new[]{remembered.players[0].libraryTop.id});Check(calls==0,"Remembered browse submits");Pass("seat "+local+" existing browser retains known top after source leaves");
                ui.Present(drawn,local,false,i=>calls++,null,"");yield return Frame();SameCards(Array.Empty<int>());Check(GameObject.Find("CardSelection").transform.Find("Empty")!=null&&calls==0,"Draw transition does not clear browser");Pass("seat "+local+" open library browser clears when known top drawn");
                Click(GameObject.Find("CloseReviewCards"));yield return Frame();
                ui.Present(null,local,false,null,null,"");yield return Frame();var before=Read("public-shuffle-after-0"+suffix);ui.Present(before,local,false,i=>calls++,null,"");yield return Frame();
                Click(GameObject.Find("ZonePiles_"+(local==2?"PlayerBottomLeft":"OpponentTopRight")+"/Pile_牌库"));yield return Frame();SameCards(new[]{before.players[1].libraryTop.id});
                ui.Present(Read("public-shuffle-after-1"+suffix),local,false,i=>calls++,null,"");yield return Frame();SameCards(Array.Empty<int>());Check(calls==0,"Shuffle browse submits");Pass("seat "+local+" native shuffle invalidates open target library browser");
                Click(GameObject.Find("CloseReviewCards"));yield return Frame();
            }
            foreach(string state in new[]{"public-source-draw-after-0","public-source-draw-after-1","public-source-draw-after-2","public-shuffle-after-1","private-peek-after-0","private-index-after-0"}) {
                var native=Read(state);var projected=OnlineSpectatorView.Create(new OnlinePlayerViewState{forge=native},3).forge;
                Check(projected.request==null&&projected.players.All(p=>p.hand.Length==0),"Spectator private request/hand leak");
                for(int i=0;i<2;i++)Check((projected.players[i].libraryTop!=null)==native.players[i].libraryTopPublic,"Spectator confuses private memory with public top permission");
                Pass("spectator projection adapter "+state+" only current native public-top authorization");
            }
            foreach(string state in new[]{"public-mulch-after-0-other","public-source-draw-after-2-other"}) {
                var data=Read(state);calls=0;ui.Present(null,2,false,null,null,"");yield return Frame();ui.Present(data,2,false,i=>calls++,null,"");yield return Frame();
                Click(GameObject.Find("OpponentPlate").transform.Find("Sub").gameObject);yield return Frame();SameCards(data.players[0].hand.Select(c=>c.id));
                Check(data.players[0].hand.Length>0&&calls==0,"Revealed cards lost when entering hand");Click(GameObject.Find("CloseReviewCards"));yield return Frame();Pass("native "+state+" public card identities inherited by readonly known-hand browser");
            }
            var indexed=Read("private-index-after-0");ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(indexed,1,false,i=>calls++,null,"");yield return Frame();
            Click(GameObject.Find("ZonePiles_PlayerBottomLeft/Pile_牌库"));yield return Frame();SameCards(new[]{indexed.players[0].libraryTop.id});Click(GameObject.Find("CloseReviewCards"));yield return Frame();Pass("private Index ordered top persists in actor readonly library browser");
        }
        IEnumerator Guard() {
            var frames=new Stack<IEnumerator>();frames.Push(Validate());
            try {
                while(frames.Count>0) {
                    object next;try{var f=frames.Peek();if(!f.MoveNext()){(f as IDisposable)?.Dispose();frames.Pop();continue;}next=f.Current;if(next is IEnumerator child){frames.Push(child);continue;}}
                    catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}
                    yield return next;
                }
                File.WriteAllText(Output+"/result.txt","PASS bounded R17 native-input reveal/order/private recipient; readonly library source-loss/draw/shuffle live refresh both seats; spectator sanitization adapters. Existing consumer languages/guards reused; no live-network or full-hidden-zone claim.");
            } finally {ui.Present(null,1,false,null,null,"");ui.enabled=previous;for(int i=0;i<modules.Length;i++)if(modules[i]!=null)modules[i].enabled=enabled[i];running=false;}
        }
        ui.StartCoroutine(Guard());
    }
}
#endif
