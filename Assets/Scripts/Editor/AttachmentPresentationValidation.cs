#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class AttachmentPresentationValidation
{
    const string Output=".tools/attachments-1007/ui";
    static bool running;
    [Serializable] class Meta {public string folder,status;}
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Call(ForgeBattlefieldController ui,string method,params object[] args)=>typeof(ForgeBattlefieldController).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,args);
    static ForgeSeatView Read(string name,string label="resolved",bool other=false){var r=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/attachments-1007/native/"+name+"-result.json"));Check(r.status=="PASS","Native result "+name);return JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(r.folder+"/"+label+(other?"-other":"")+".json"));}
    static ForgeCardView[] Cards(ForgeSeatView v)=>v.players.SelectMany(p=>p.battlefield).ToArray();
    static GameObject Card(int id)=>GameObject.Find("Card_"+id);
    static Transform Panel=>GameObject.Find("Canvas").transform.Find("InspectPanel");
    static void Hover(ForgeBattlefieldController ui,int id){Panel.gameObject.SetActive(false);var root=Card(id);Check(root!=null,"Missing hover card "+id);root.GetComponent<ForgeBattlefieldCardPointer>().ResetHover();var r=(RectTransform)root.transform;var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<ForgeBattlefieldCardPointer>()==root.GetComponent<ForgeBattlefieldCardPointer>(),"Blocked hover ray "+id+": "+string.Join(",",hits.Take(5).Select(h=>h.gameObject.name+"/"+h.gameObject.transform.parent.name)));ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerEnterHandler);Call(ui,"RefreshAttachmentLinks");}
    static ForgeTargetArrow[] Links()=>UnityEngine.Object.FindObjectsByType<ForgeTargetArrow>(FindObjectsSortMode.None).Where(a=>a.name.StartsWith("AttachmentLink_")&&a.gameObject.activeInHierarchy).ToArray();
    static void LinkTo(ForgeBattlefieldController ui,int id,string kind,int target){
        var links=Links();Check(links.Length==1,"Expected one exact attachment arrow");
        var from=Card(id).GetComponent<RectTransform>();RectTransform to;
        if(kind=="card")to=Card(target).GetComponent<RectTransform>();
        else {
            var v=(ForgeSeatView)typeof(ForgeBattlefieldController).GetField("view",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
            var player=v.players.Single(p=>p.targetId==target);
            to=GameObject.Find("Canvas").transform.Find(player.seat==1?"PlayerPlate":"OpponentPlate").GetComponent<RectTransform>();
        }
        var rect=links[0].rectTransform;
        Vector2 actual=(Vector2)typeof(ForgeTargetArrow).GetField("to",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(links[0]);
        Vector2 expected=rect.parent.InverseTransformPoint(to.TransformPoint(to.rect.center));
        Check(Vector2.Distance(actual,expected)<2,"Wrong link endpoint "+kind+" "+target);
        Check(!links[0].raycastTarget,"Read-only link intercepts pointer");
        Check(Vector2.Distance((Vector2)typeof(ForgeTargetArrow).GetField("from",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(links[0]),(Vector2)rect.parent.InverseTransformPoint(from.TransformPoint(from.rect.center)))<2,"Wrong link source");
    }
    static string Rules=>Panel.Find("Rules").GetComponent<Text>().text;
    static string Plain(string value)=>System.Text.RegularExpressions.Regex.Replace(value??"","<[^>]*>","");
    static string Mana(string value)=>CardSymbolText.Format(string.Equals(value,"no cost",StringComparison.OrdinalIgnoreCase)?"":value);
    static bool Completed(string language,string name)=>File.ReadAllLines(Output+"/steps.log").Any(line=>line.StartsWith(language+" "+name+" ",StringComparison.Ordinal)&&line.EndsWith(" PASS",StringComparison.Ordinal));
    static void Click(Button button){var rect=(RectTransform)button.transform;var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Blocked original/return ray");ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);}
    static IEnumerator CopyDetail(ForgeBattlefieldController ui,ForgeCardView card,string label){
        var catalog=(Dictionary<string,CardDefinition>)typeof(ForgeBattlefieldController).GetField("catalog",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
        var physical=catalog[card.printingKey];var effective=catalog[card.currentArtPrintingKey];var feature=effective.features?.FirstOrDefault(f=>f!=null&&f.cardName==card.name);
        string type=feature!=null?GamePreferences.FeatureType(feature,card.currentType):GamePreferences.CardType(effective);
        string rules=feature!=null?GamePreferences.FeatureRules(feature):GamePreferences.CardRules(effective);
        Check(Plain(Panel.Find("Name").GetComponent<Text>().text)==GamePreferences.CardFaceName(card.name),"Copied name "+label);
        Check(Plain(Panel.Find("TypeLine").GetComponent<Text>().text)==type,"Copied type "+label);
        Check(Plain(Rules).StartsWith(rules,StringComparison.Ordinal),"Copied rules "+label);
        var mana=Panel.Find("CostRow").GetComponentInChildren<TMPro.TextMeshProUGUI>();
        Check(Plain(mana.text)==Mana(card.manaCost),"Copied effective mana "+label);
        var art=Panel.Find("Art").GetComponent<Image>();Check(CardImageLibrary.TryLoadFullCard(effective,card.currentArtBackFace?1:0,out var currentArt)&&art.sprite.texture==currentArt.texture,"Copied detail art "+label);
        ScreenCapture.CaptureScreenshot(Output+"/"+GamePreferences.Language+"-"+label+"-current.png");yield return Frame();
        var toggle=Panel.Find("OriginalState").GetComponent<Button>();Click(toggle);yield return Frame();
        Check(Plain(Panel.Find("Name").GetComponent<Text>().text)==GamePreferences.CardName(physical),"Physical original name "+label);
        Check(Plain(Panel.Find("TypeLine").GetComponent<Text>().text)==GamePreferences.CardType(physical),"Physical original type "+label);
        Check(Plain(Rules)==GamePreferences.CardRules(physical),"Physical original rules "+label);
        mana=Panel.Find("CostRow").GetComponentInChildren<TMPro.TextMeshProUGUI>();Check(Plain(mana.text)==CardSymbolText.Format(physical.manaCost),"Physical original mana "+label);
        Check(CardImageLibrary.TryLoadFullCard(physical,card.backFace?1:0,out var physicalArt)&&art.sprite.texture==physicalArt.texture,"Physical original art "+label);
        ScreenCapture.CaptureScreenshot(Output+"/"+GamePreferences.Language+"-"+label+"-original.png");yield return Frame();
        Click(toggle);yield return Frame();Check(Plain(Rules).StartsWith(rules,StringComparison.Ordinal)&&art.sprite.texture==currentArt.texture,"Return current copy "+label);
    }
    public static void Run(bool resume=false){Check(Application.isPlaying&&!running,"Play/runner state");running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");if(!resume)File.WriteAllText(Output+"/steps.log","");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("AttachmentValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var stack=new Stack<IEnumerator>();stack.Push(Validate(ui));try{while(stack.Count>0){object current;try{var e=stack.Peek();if(!e.MoveNext()){(e as IDisposable)?.Dispose();stack.Pop();continue;}current=e.Current;if(current is IEnumerator child){stack.Push(child);continue;}}catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}yield return current;}File.WriteAllText(Output+"/result.txt","PASS three-language real attachment snapshots: exact hover rays and read-only link endpoints/inverse host, same-ID retarget, removal cleanup, cross-controller host, copied weapon/aura, player target, detail labels and hidden-face adapter. Existing R21 grouping/target and payment contracts reused; no new UI first/network claim.");}finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui){bool enabled=ui.enabled;ui.enabled=false;string locale=GamePreferences.Language;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(n=>n.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
            GamePreferences.SetLanguage(language);float until=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale ready");
            string prefix=language=="zh-CN"?"附着于：":language=="ja-JP"?"エンチャント／装備先：":"Attached to: ";
            foreach(string name in new[]{"equip-retarget","aura-detach","equipment-detach","control-host","copy-equipment","aura-player","aura-player-self","copy-aura"}){
                if(Completed(language,name))continue;
                var v=Read(name,name.StartsWith("aura-player",StringComparison.Ordinal)||name=="copy-aura"||name=="copy-equipment"?"resolved":"after-0");ui.Present(null,1,false,null,null,"");ui.Present(v,1,false,null,null,"");yield return Frame();
                var source=Cards(v).First(c=>c.attachedToKind is "card" or "player");if(name=="copy-aura"||name=="copy-equipment")source=Cards(v).Single(c=>c.copy);
                Hover(ui,source.id);yield return Frame();Call(ui,"RefreshAttachmentLinks");LinkTo(ui,source.id,source.attachedToKind,source.attachedToId);Check(Rules.Contains(prefix),"Missing localized relation "+language);
                var tmp=Panel.Find("Rules").GetComponentInChildren<TMPro.TextMeshProUGUI>();Check(tmp!=null&&!tmp.enableAutoSizing&&tmp.text.Contains(prefix),"Rendered detail relation/size");
                ScreenCapture.CaptureScreenshot(Output+"/"+language+"-"+name+".png");yield return Frame();
                if(name=="equip-retarget"||name=="control-host"){
                    var next=Read(name,"after-1");ui.Present(next,1,false,null,null,"");yield return Frame();Call(ui,"RefreshAttachmentLinks");yield return Frame();var latest=Cards(next).Single(c=>c.id==source.id);LinkTo(ui,latest.id,latest.attachedToKind,latest.attachedToId);
                    if(name=="control-host")Check(next.players[1].battlefield.Any(c=>c.id==latest.attachedToId),"Cross-controller host missing");
                    ScreenCapture.CaptureScreenshot(Output+"/"+language+"-"+name+"-changed.png");yield return Frame();
                }
                if(name is "copy-aura" or "copy-equipment")yield return CopyDetail(ui,source,name);
                if(source.attachedToKind=="card"){
                    var host=Cards(name=="equip-retarget"||name=="control-host"?Read(name,"after-1"):v).Single(c=>c.id==(name=="equip-retarget"?Cards(Read(name,"after-1")).Single(c=>c.id==source.id).attachedToId:source.attachedToId));
                    Hover(ui,host.id);yield return Frame();Call(ui,"RefreshAttachmentLinks");Check(Links().Length==1&&Rules.Contains(language=="zh-CN"?"附着物：":language=="ja-JP"?"ついているカード：":"Attachments: "),"Inverse host relation missing");
                }
                Panel.gameObject.SetActive(false);Call(ui,"RefreshAttachmentLinks");yield return Frame();Check(Links().Length==0,"Closed detail leaves arrows");
                if(name is "equip-retarget" or "aura-detach" or "equipment-detach"){
                    ui.Present(Read(name),1,false,null,null,"");yield return Frame();Call(ui,"RefreshAttachmentLinks");Check(Links().Length==0,"Removed attachment/host leaves arrow");
                }
                File.AppendAllText(Output+"/steps.log",language+" "+name+" exact relation/detail/refresh/cleanup PASS\n");
            }
            // Resource identity stays hidden while the public relation remains.
            if(!Completed(language,"hidden host")){
            var hidden=Read("equipment-detach","after-0");var attached=Cards(hidden).Single(c=>c.attachedToId>0);var target=Cards(hidden).Single(c=>c.id==attached.attachedToId);target.faceDown=true;target.name="SECRET_FACE";target.printingKey="";target.originalName=null;target.currentRules="";target.originalRules=null;
            ui.Present(hidden,1,false,null,null,"");yield return Frame();Hover(ui,attached.id);yield return Frame();Check(!Rules.Contains("SECRET_FACE")&&Rules.Contains(language=="zh-CN"?"面朝下卡牌":language=="ja-JP"?"裏向きのカード":"Face-down card"),"Hidden host identity leaked");
            File.AppendAllText(Output+"/steps.log",language+" hidden host adapter PASS\n");Panel.gameObject.SetActive(false);Call(ui,"RefreshAttachmentLinks");yield return Frame();
            }
            // R22 source snapshots are reused without rerunning or overwriting its proof.
            foreach(string name in new[]{"steel-copy","steel-reverse-copy"}){
                if(Completed(language,name))continue;
                var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/noncreature-copies-1007/native/"+name+"-result.json"));Check(meta.status=="PASS","R22 historical result");
                var snapshot=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(meta.folder+"/resolved.json"));ui.Present(null,1,false,null,null,"");ui.Present(snapshot,1,false,null,null,"");yield return Frame();
                var copy=Cards(snapshot).Single(c=>c.copy);Hover(ui,copy.id);yield return Frame();yield return CopyDetail(ui,copy,name);Panel.gameObject.SetActive(false);Call(ui,"RefreshAttachmentLinks");
                File.AppendAllText(Output+"/steps.log",language+" "+name+" current/physical copy detail regression PASS\n");
            }
        }}finally{ui.Present(null,1,false,null,null,"");Call(ui,"RefreshAttachmentLinks");ui.enabled=enabled;GamePreferences.SetLanguage(locale);if(module!=null)module.enabled=input;for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
