#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class DamageDrawPresentationValidation
{
    const string Output=".tools/damage-draw-ui",Input="Tools/Validation/Reports/DamageDraw/";
    static bool running;
    static void Check(bool b,string s){if(!b)throw new Exception(s);}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Input+name+".json"));
    static ForgeWireMessage[] Wire(string test,Func<ForgeRequest,bool> filter)=>File.ReadLines(Input+test+".jsonl").Select(JsonUtility.FromJson<ForgeWireMessage>).Where(m=>m.type=="request"&&filter(m.request)).ToArray();
    static ForgeSeatView View(ForgeWireMessage m){var v=m.views[m.request.seat-1];v.request=m.request;v.awaitingInput=true;return v;}
    public static void Run(){if(running)return;Check(Application.isPlaying,"Enter IntegratedBattlefield Play Mode.");running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("DamageDrawValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(Validate(ui)));}
    static IEnumerator Guard(IEnumerator iterator)
    {
        string language=GamePreferences.Language;var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=banner!=null&&banner.enabled;if(banner!=null)banner.enabled=false;
        try{while(true){object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: real native damage/draw requests replayed in zh-CN/en-US/ja-JP at current text scale; typed optional source/affected player and accept/decline, localized fixed-size headers and exact unchanged body paragraphs including Dredge, complete body height, wheel/end checks when overflowing, replacement order IDs, source picker without heading cards and exact pointer/confirm indices, private looked-at cards absent from other seat UI, independent stale/duplicate/nonacting guards, stable panel/card/font identity; exact life/damage/token end-state display. Native effect proof separate; not all replacements, devices, animation or live network.");}
        finally{running=false;GamePreferences.SetLanguage(language);if(banner!=null)banner.enabled=enabled;}
    }
    static IEnumerator Rendered(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static IEnumerator Shot(string name){yield return new WaitForSecondsRealtime(.2f);yield return Rendered();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return new WaitForEndOfFrame();}
    static void Click(Button b){var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.TransformPoint(((RectTransform)b.transform).rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Pointer blocked: "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
            int revision=GamePreferences.Revision;bool changed=GamePreferences.Language!=language;GamePreferences.SetLanguage(language);float end=Time.realtimeSinceStartup+8;while((!GamePreferences.Ready||changed&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<end)yield return null;Check(GamePreferences.Ready&&(!changed||revision!=GamePreferences.Revision),"Locale not ready");
            foreach(string test in new[]{"damage-optional-accept","damage-optional-decline","draw-dredge-accept","draw-dredge-decline","draw-optional-skip","draw-optional-draw"}){
                var m=Wire(test,r=>r.selectionPurpose=="optionalReplacement").Single();var v=View(m);var r=v.request;int calls=0,last=-1;Action<int> submit=i=>{calls++;last=i;};
                ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,true,submit,null,"");yield return Rendered();
                var panel=GameObject.Find("AbilityOrder");Check(panel!=null&&GameObject.Find("CardSelection")==null&&GameObject.Find("CardAbilityChoices")==null&&GameObject.Find("DismissAbilityChoices")==null,"Optional effect used duplicate/wrong presenter");
                Check(GameObject.Find("SkipCombatButton")==null,"Duplicate global confirmation remains active");
                Check(panel.transform.Find("Title").GetComponent<Text>().text==(language=="zh-CN"?"使用替代效果？":language=="ja-JP"?"置換効果を適用しますか？":"Apply replacement effect?"),"Optional title language");
                foreach(string name in new[]{"Title","Direction"}){var t=panel.transform.Find(name).GetComponent<Text>();Check(!t.resizeTextForBestFit&&t.preferredHeight<=t.rectTransform.rect.height,"Optional header clipped/shrinks");}
                Check(panel.transform.Find("Direction").GetComponent<Text>().text.Contains(r.replacementAffectedPlayerSeat.ToString()),"Affected native player missing");
                var effect=r.orderedAbilities.Single();var tile=GameObject.Find("OrderedAbility_"+effect.id);var face=tile.transform.Find("Face/AbilityFace");var body=face.Find("EffectClip/Effect").GetComponent<Text>();var nameText=face.Find("Name").GetComponent<Text>();var scroll=face.Find("EffectClip").GetComponent<ScrollRect>();
                Check(effect.source.id==r.sourceCardId&&nameText.text==GamePreferences.CardFaceName(effect.source.name),"Optional source/name mismatch");
                Check(!nameText.resizeTextForBestFit&&!body.resizeTextForBestFit&&nameText.preferredWidth<=nameText.rectTransform.rect.width&&nameText.preferredHeight<=nameText.rectTransform.rect.height,"Optional name/font clipping");
                if(language!="en-US")Check(!body.text.StartsWith("If ")&&!body.text.Contains(" - Dredge "),"Known printed optional paragraph not localized");
                var renderedBody=body.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                Check(renderedBody!=null&&renderedBody.text==CardSymbolText.Format(body.text)&&!renderedBody.enableAutoSizing&&scroll.content==body.rectTransform&&body.rectTransform.rect.height+1>=renderedBody.GetPreferredValues(renderedBody.text,body.rectTransform.rect.width,10000).y,"Optional rendered body lost text");
                int font=body.fontSize;ui.Present(v,v.seat,true,submit,null,"");yield return Rendered();Check(GameObject.Find("AbilityOrder")==panel&&GameObject.Find(tile.name)==tile&&body.fontSize==font,"Unchanged optional request rebuilt or changed font");
                if(scroll.content.rect.height>scroll.viewport.rect.height+1){scroll.verticalNormalizedPosition=1;ExecuteEvents.Execute(scroll.gameObject,new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-4)},ExecuteEvents.scrollHandler);Check(scroll.verticalNormalizedPosition<.999f,"Effect wheel cannot scroll");scroll.verticalNormalizedPosition=0;yield return Rendered();var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,scroll.content);Check(Mathf.Abs(bounds.min.y-scroll.viewport.rect.yMin)<1,"Full effect end not reachable");}
                int index=Array.FindIndex(r.options,o=>o.id==(test.EndsWith("decline")||test=="draw-optional-draw"?"cancel":"ok"));var action=panel.transform.Find(r.options[index].id=="ok"?"ConfirmOrder":"ResetOrder").GetComponent<Button>();
                Check(action.interactable&&panel.transform.Find("ConfirmOrder").GetComponent<Button>().interactable&&panel.transform.Find("ResetOrder").GetComponent<Button>().interactable,"Optional accept/decline missing");
                ui.Choose(index,r.requestId-1);Check(calls==0,"Actionable stale optional submitted");yield return Shot(language+"-"+test+"-optional");Click(action);action.onClick.Invoke();Check(calls==1&&last==index,"Optional pointer/index/duplicate wrong");
                ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,false,submit,null,"");yield return Rendered();Check(GameObject.Find("AbilityOrder").GetComponentsInChildren<Button>().All(b=>!b.interactable),"Nonacting optional active");ui.Choose(index,r.requestId);Check(calls==1,"Nonacting optional submitted");
            }
            // These native paragraphs may all fit at the user's text scale.
            // Overflow is checked above only when present; the replacements
            // suite supplies genuinely long native paragraphs for scroll proof.
            foreach(string test in new[]{"damage-add-first","damage-prevent-first","draw-double","draw-thief-first"}){
                var wire=Wire(test,r=>r.selectionPurpose=="replacementOrder");Check(wire.Length>0,"Native order missing");ui.Present(null,1,false,null,null,"");GameObject panel=null;var tiles=new Dictionary<int,GameObject>();int font=-1;
                for(int step=0;step<wire.Length;step++){
                    var v=View(wire[step]);var r=v.request;int calls=0,last=-1;ui.Present(v,v.seat,true,i=>{calls++;last=i;},null,"");yield return Rendered();var p=GameObject.Find("AbilityOrder");if(panel!=null)Check(p==panel,"Ordering panel rebuilt");panel=p;var title=p.transform.Find("Title").GetComponent<Text>();Check(!title.resizeTextForBestFit&&title.preferredHeight<=title.rectTransform.rect.height,"Order header clips/shrinks");if(font>=0)Check(font==title.fontSize,"Selection changes order font");font=title.fontSize;
                    var choices=r.options.Where(o=>o.id.StartsWith("order:")).ToArray();var entries=(r.orderedAbilities??Array.Empty<ForgeStackItem>()).Concat(choices.Select(o=>o.ability)).ToArray();
                    foreach(var e in entries){var t=GameObject.Find("OrderedAbility_"+e.id);Check(t!=null&&e.source.id>0,"Order source/effect identity missing");if(tiles.TryGetValue(e.id,out var old))Check(t==old,"Order tile rebuilt");tiles[e.id]=t;int at=Array.FindIndex(r.orderedAbilities??Array.Empty<ForgeStackItem>(),x=>x.id==e.id);Check(t.transform.Find("Order").GetComponent<Text>().text==(at<0?"":(at+1).ToString()),"Order number wrong");}
                    int index=choices.Length==0?Array.FindIndex(r.options,o=>o.id=="confirm-order"):Array.FindIndex(r.options,o=>o.ability?.id==wire[step+1].request.orderedAbilities.Last().id);var b=choices.Length==0?p.transform.Find("ConfirmOrder").GetComponent<Button>():GameObject.Find("OrderedAbility_"+r.options[index].ability.id).GetComponent<Button>();
                    if(step==0||test=="damage-add-first"&&choices.Length==0)yield return Shot(language+"-"+test+"-order-"+step);Click(b);Check(calls==1&&last==index,"Order pointer does not map to native next selection");
                }
            }
            var sources=Wire("damage-split-repeat",r=>r.selectionPurpose=="chooseSource");Check(sources.Length==2,"Expected native source select and confirm");GameObject sourcePanel=null;ui.Present(null,1,false,null,null,"");
            foreach(var m in sources){var v=View(m);var r=v.request;int calls=0,last=-1;ui.Present(v,v.seat,true,i=>{calls++;last=i;},null,"");yield return Rendered();var panel=GameObject.Find("CardSelection");Check(panel!=null,"Source picker missing");if(sourcePanel!=null)Check(sourcePanel==panel,"Source picker rebuilt after selection");sourcePanel=panel;
                Check(r.options.Where(o=>o.card!=null&&!string.IsNullOrEmpty(o.card.name)).All(o=>o.card.id>0&&!o.card.name.StartsWith("--")),"Source picker contains heading cards");
                var title=panel.transform.Find("Title").GetComponent<Text>();var instruction=panel.transform.Find("Instruction").GetComponent<Text>();Check(title.text==(language=="zh-CN"?"选择来源":language=="ja-JP"?"発生源を選択":"Choose a source"),"Source title locale wrong");Check(!title.resizeTextForBestFit&&!instruction.resizeTextForBestFit&&title.preferredHeight<=title.rectTransform.rect.height&&instruction.preferredHeight<=instruction.rectTransform.rect.height,"Source text clips/shrinks");
                int index;Button action;if(r.selectedCount==0){index=Array.FindIndex(r.options,o=>o.card?.name=="Pyrohemia");action=GameObject.Find("ChoiceCard_"+r.options[index].card.id).transform.Find("Artwork").GetComponent<Button>();}else{index=Array.FindIndex(r.options,o=>o.id=="confirm-cards");action=GameObject.Find("SelectionAction_"+index).GetComponent<Button>();}
                ui.Choose(index,r.requestId-1);Check(calls==0,"Stale source submitted");yield return Shot(language+"-source-"+r.selectedCount);Click(action);action.onClick.Invoke();Check(calls==1&&last==index,"Source pointer/index/duplicate wrong");
            }
            foreach(string test in new[]{"draw-underrealm-middle","draw-underrealm-seat-two"}){
                var m=Wire(test,r=>r.kind=="InputSelectEntitiesFromList"&&r.selectedCount==0).Single();var v=View(m);int calls=0,last=-1;ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,true,i=>{calls++;last=i;},null,"");yield return Rendered();
                var chosen=Read(test+"-resolved-0").players[v.seat-1].hand.Single(c=>!Read(test+"-before-0").players[v.seat-1].hand.Any(old=>old.id==c.id));int index=Array.FindIndex(v.request.options,o=>o.card?.id==chosen.id||o.id=="card:"+chosen.id);Check(index>=0,"Chosen native looked-at card absent");yield return Shot(language+"-"+test+"-private");Click(GameObject.Find("ChoiceCard_"+chosen.id).transform.Find("Artwork").GetComponent<Button>());Check(calls==1&&last==index,"Private picker pointer/index wrong");
                var selected=Wire(test,r=>r.kind=="InputSelectEntitiesFromList"&&r.selectedCount==1).Single();var confirmation=View(selected);var picker=GameObject.Find("CardSelection");ui.Present(confirmation,v.seat,true,i=>{calls++;last=i;},null,"");yield return Rendered();Check(GameObject.Find("CardSelection")==picker,"Private selection rebuilt before confirmation");int confirm=Array.FindIndex(confirmation.request.options,o=>o.id=="ok");Check(confirm>=0,"Private native confirmation missing");var confirmButton=GameObject.Find("SelectionAction_"+confirm).GetComponent<Button>();Check(confirmButton.interactable,"Private selected card cannot be confirmed");Click(confirmButton);Check(calls==2&&last==confirm,"Private confirmation index wrong");
                int other=3-v.seat;ui.Present(null,other,false,null,null,"");ui.Present(m.views[other-1],other,false,i=>calls++,null,"");yield return Rendered();Check(GameObject.Find("CardSelection")==null,"Other seat sees private looked-at picker");ui.Choose(index,v.request.requestId);Check(calls==2,"Other seat submits private selection");
            }
            foreach(string test in new[]{"damage-creature-survive","damage-split-repeat","draw-dredge-accept","draw-thief-first","draw-token-first","draw-limit-after-first"}){
                var v=Read(test+"-resolved-"+(test=="damage-split-repeat"?"3":test=="draw-limit-after-first"?"1":"0"));ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,true,_=>{},null,"");yield return Rendered();Check(GameObject.Find("AbilityOrder")==null&&GameObject.Find("CardSelection")==null,"Resolved effect leaves picker");
                foreach(var p in v.players){var plate=GameObject.Find(p.seat==v.seat?"PlayerPlate":"OpponentPlate");Check(plate.transform.Find("Life").GetComponent<Text>().text==p.life.ToString(),"Final life display mismatch");}
                if(test=="damage-creature-survive"){var c=v.players.SelectMany(p=>p.battlefield).Single(c=>c.name=="Ancient Brontodon");var card=GameObject.Find("Card_"+c.id);Check(card!=null&&card.GetComponentsInChildren<Text>(true).Any(t=>t.text=="−8"),"Surviving damage marker missing");}
                if(test=="draw-token-first"){var c=v.players.SelectMany(p=>p.battlefield).Single(c=>c.token);Check(GameObject.Find("Card_"+c.id)!=null,"Replacement treasure absent");}
                yield return Shot(language+"-"+test+"-resolved");
            }
            ui.Present(null,1,false,null,null,"");yield return Rendered();
        }
    }
}
#endif
