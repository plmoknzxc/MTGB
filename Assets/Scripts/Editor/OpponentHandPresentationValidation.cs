#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class OpponentHandPresentationValidation
{
    const string Output=".tools/direct-hand-review-1004";
    static bool running;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/Thoughtseize/"+name+".json"));
    public static void Run()
    {
        if(running)return;Check(Application.isPlaying,"Requires Play Mode");running=true;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("OpponentHandValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator iterator)
    {
        string locale=GamePreferences.Language;
        var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=banner!=null&&banner.enabled;
        if(banner!=null)banner.enabled=false;
        try {
            while(true){object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: zh-CN/en-US/ja-JP actual Thoughtseize/Mind Rot direct hand requests, authorized reveal-only faces and privacy restoration; eligible IDs glow, native index raycast/stale/duplicate/viewer guards; both end cards lift vertically/straighten; explicitly synthetic dense own/opponent hand window fallbacks and 24/zero hidden hand reuse. Screenshot evidence, not a new native effect assertion.");
        }finally{running=false;GamePreferences.SetLanguage(locale);if(banner!=null)banner.enabled=enabled;}
    }
    static IEnumerator Rendered(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static IEnumerator Shot(string name){yield return Rendered();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForEndOfFrame();}
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}) {
            int revision=GamePreferences.Revision;bool changing=GamePreferences.Language!=locale;GamePreferences.SetLanguage(locale);
            float deadline=Time.realtimeSinceStartup+10;while((!GamePreferences.Ready||changing&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(GamePreferences.Ready,"Locale not ready");
            ui.Present(null,1,false,null,null,"");var before=Read("before");ui.Present(before,1,true,_=>{},null,"");yield return Rendered();
            var root=GameObject.Find("OpponentHandRoot").transform;CardImageLibrary.TryLoadCardBack(out var back);
            var cards=root.GetComponentsInChildren<Image>();
            Check(cards.Length==before.players[1].handCount&&cards.All(c=>c.sprite==back&&c.rectTransform.rect.width>=84&&c.rectTransform.rect.height/c.rectTransform.rect.width>1.38f&&!c.raycastTarget),"Opponent hand backs missing/squashed/private face shown");
            yield return Shot(locale+"-opponent-hand");
            var revealed=Read("revealed");ui.Present(revealed,1,true,_=>{},null,"");yield return Rendered();
            Check(GameObject.Find("CardSelection")==null,"Small revealed hand still opens a list");
            foreach(var card in revealed.request.previewCards) {
                var face=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Card_"+card.id).Find("Visual/Border/Face").GetComponent<Image>();
                Check(face.sprite!=null&&face.preserveAspect&&face.rectTransform.rect.height/face.rectTransform.rect.width>1.38f,"Revealed card missing/squashed");
            }
            yield return Shot(locale+"-thoughtseize-revealed");
            var choice=Read("choice");int calls=0,last=-1;ui.Present(choice,1,true,i=>{calls++;last=i;},null,"");yield return Rendered();
            int index=Array.FindIndex(choice.request.options,o=>o.card.name=="Negate");
            Check(GameObject.Find("CardSelection")==null,"Small discard selection opens a window");
            var tile=root.GetComponentsInChildren<Button>().First(b=>b.name=="Card_"+choice.request.options[index].card.id);
            foreach(var card in choice.players[1].hand) {
                bool eligible=choice.request.options.Any(o=>o.card?.id==card.id);
                var visual=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Card_"+card.id);
                Check((visual.Find("Visual/PlayableGlow")!=null)==eligible,"Native nonland eligibility not reflected by glow");
            }
            ui.Choose(index,choice.request.requestId-1);Check(calls==0,"Stale hand choice accepted");
            yield return Shot(locale+"-thoughtseize-discard");
            Click(tile);tile.onClick.Invoke();
            Check(calls==1&&last==index,"Wrong/duplicate discard index");
            ui.Present(choice,1,false,_=>calls++,null,"");yield return Rendered();ui.Choose(index,choice.request.requestId);Check(calls==1,"Viewer can discard");
            // Both ends straighten and lift toward the battlefield, keeping a stable hit area.
            ui.Present(choice,1,true,_=>{},null,"");yield return new WaitForSecondsRealtime(.5f);
            foreach(var card in new[]{choice.players[1].hand.First(),choice.players[1].hand.Last()}) {
                var visual=root.GetComponentsInChildren<ForgeBattlefieldCardPointer>().First(p=>p.name=="Card_"+card.id);
                var face=(RectTransform)visual.transform.Find("Visual");Vector3 oldCenter=face.position;
                visual.OnPointerEnter(new PointerEventData(EventSystem.current));yield return new WaitForSecondsRealtime(.4f);
                Check(Mathf.Abs(Mathf.DeltaAngle(face.eulerAngles.z,180))<1 && Mathf.Abs(face.position.x-oldCenter.x)<2 && face.position.y<oldCenter.y-40,"Opponent end card hover tilts/slides outward");
                yield return Shot(locale+"-opponent-end-"+card.id);visual.OnPointerExit(new PointerEventData(EventSystem.current));yield return new WaitForSecondsRealtime(.4f);
            }
            var manyChoice=Read("choice");var visible=manyChoice.players[1].hand.ToList();
            for(int i=0;i<6;i++){var copy=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(visible[2]));copy.id=50000+i;visible.Add(copy);}
            manyChoice.players[1].hand=visible.ToArray();manyChoice.players[1].handCount=visible.Count;
            ui.Present(manyChoice,1,true,_=>{},null,"");yield return Rendered();
            Check(GameObject.Find("CardSelection")!=null && GameObject.Find("CardSelection").GetComponentsInChildren<Button>().Count(b=>b.name=="Artwork")==2,"Dense hand lacks eligible options / lights ineligible lands");yield return Shot(locale+"-dense-opponent-hand");
            var own=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/CardSelection/hand-choice.json"));
            int ownCalls=0;ui.Present(own,own.seat,true,_=>ownCalls++,null,"");yield return new WaitForSecondsRealtime(.5f);
            Check(GameObject.Find("CardSelection")==null,"Small own hand still opens a list");
            var ownRoot=GameObject.Find("PlayerHandRoot");int ownIndex=Array.FindIndex(own.request.options,o=>o.id.StartsWith("card:"));
            Check(ownIndex>=0,"Missing native own-hand candidate");
            var ownButton=ownRoot.GetComponentsInChildren<Button>().First(b=>b.name=="Card_"+own.request.options[ownIndex].id.Substring(5));
            foreach(var card in new[]{own.players.First(p=>p.seat==own.seat).hand.First(),own.players.First(p=>p.seat==own.seat).hand.Last()}) {
                var visual=ownRoot.GetComponentsInChildren<ForgeBattlefieldCardPointer>().First(p=>p.name=="Card_"+card.id);
                var face=(RectTransform)visual.transform.Find("Visual");Vector3 oldCenter=face.position;
                visual.OnPointerEnter(new PointerEventData(EventSystem.current));yield return new WaitForSecondsRealtime(.4f);
                Check(Mathf.Abs(Mathf.DeltaAngle(face.eulerAngles.z,0))<1 && Mathf.Abs(face.position.x-oldCenter.x)<2 && face.position.y>oldCenter.y+40,"Own end card hover tilts/slides outward");
                yield return Shot(locale+"-own-end-"+card.id);visual.OnPointerExit(new PointerEventData(EventSystem.current));yield return new WaitForSecondsRealtime(.4f);
            }
            yield return Shot(locale+"-own-discard");Click(ownButton);Check(ownCalls==1,"Own hand direct click failed");
            var denseOwn=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(manyChoice));denseOwn.players[0].hand=denseOwn.players[1].hand;denseOwn.players[0].handCount=denseOwn.players[1].handCount;denseOwn.players[1].hand=Array.Empty<ForgeCardView>();denseOwn.players[1].handCount=0;
            ui.Present(denseOwn,1,true,_=>{},null,"");yield return Rendered();Check(GameObject.Find("CardSelection")!=null,"Dense own hand lacks list fallback");yield return Shot(locale+"-dense-own-hand");
            var resolved=Read("resolved");ui.Present(resolved,1,true,null,null,"");yield return Rendered();
            Check(root.GetComponentsInChildren<Image>().All(c=>c.sprite==back),"Opponent private faces persisted");yield return Shot(locale+"-thoughtseize-resolved");
            var many=Read("before");many.players[1].handCount=24;ui.Present(many,1,true,null,null,"");yield return Rendered();
            Check(root.GetComponentsInChildren<Image>().Length==24,"Dense hand disappeared");
            var stable=root.GetChild(0);ui.Present(many,1,true,null,null,"");yield return Rendered();Check(root.GetChild(0)==stable,"Rebuilt opponent hand");
            yield return Shot(locale+"-hand-24-display-fixture");
            many.players[1].handCount=0;ui.Present(many,1,true,null,null,"");yield return Rendered();Check(root.GetComponentsInChildren<Image>().Length==0,"Zero hand retains backs");
            ui.Present(null,1,false,null,null,"");yield return Rendered();Check(root.GetComponentsInChildren<Image>().Length==0,"Reset retains private hand");
        }
    }
    static void Click(Button button)
    {
        var rect=(RectTransform)button.transform;
        foreach(float x in new[]{-.42f,-.25f,0,.25f,.42f})foreach(float y in new[]{.32f,0,-.32f}) {
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center+new Vector2(rect.rect.width*x,rect.rect.height*y))),button=PointerEventData.InputButton.Left};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            if(hits.Count>0 && hits[0].gameObject.GetComponentInParent<Button>()==button){ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);return;}
        }
        throw new Exception("Hand card not reachable: "+button.name);
    }
}
#endif
