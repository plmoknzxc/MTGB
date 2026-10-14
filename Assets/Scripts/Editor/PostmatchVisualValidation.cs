#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public static class PostmatchVisualValidation
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Out=".tools/postmatch-fixes-1013";
    sealed class Host:MonoBehaviour { }
    static readonly List<string> checks=new();public static bool Running;
    static object Get(object obj,string name)=>obj.GetType().GetField(name,Hidden).GetValue(obj);
    static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Hidden).SetValue(obj,value);
    static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Hidden).Invoke(obj,args);
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
    static ForgeSeatView Read(string file)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(file));
    public static void Run(){Running=true;checks.Clear();new GameObject("PostmatchVisualProbe").AddComponent<Host>().StartCoroutine(Guard(Exercise()));}
    static IEnumerator Guard(IEnumerator task){while(true){bool more;object next;try{more=task.MoveNext();next=more?task.Current:null;}catch(Exception e){checks.Add("FAIL "+e);more=false;next=null;}if(!more)break;yield return next;}File.WriteAllLines(Out+"/ui-result.txt",checks);Running=false;}
    static IEnumerator Exercise()
    {
        while(!GamePreferences.Ready)yield return null;
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("PostmatchBoard").AddComponent<ForgeBattlefieldController>();ui.enabled=false;
        var v=Read(Directory.GetFiles(Out,"view-Surveil-*.json").First());ui.Present(v,v.seat,true,i=>{},null,"");yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();
        var cards=Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,ForgeCardView>>(File.ReadAllText(Out+"/cards.json"));
        var land=cards["Misty Rainforest"];
        Set(ui,"inspectOriginalState",true);Call(ui,"InspectIntegrated",land);
        var rules=GameObject.Find("InspectPanel").transform.Find("Rules").GetComponent<Text>().text;
        Check(rules.Contains("搜寻")&&!rules.Contains("Search")&&!rules.Contains("Pay 1 life"),"Original-state land details reuse existing Chinese rules");
        Set(ui,"inspectOriginalState",false);land.hasCurrentLandState=true;Call(ui,"InspectIntegrated",land);
        rules=GameObject.Find("InspectPanel").transform.Find("Rules").GetComponent<Text>().text;
        Check(rules.Contains("搜寻")&&!rules.Contains("Search"),"Dynamic land details retain Chinese unchanged rules");
        var bauble=cards["Mishra's Bauble"];
        foreach(string text in new[]{"Player 2 looks at the top card of Player 1's library. Draw a card at the beginning of the next turn's upkeep.","Player 2 looks at the top card of Player 1's library.\nDraw a card at the beginning of the next turn's upkeep."}){
            string body=(string)Call(ui,"AbilityEffectText",bauble,text);
            Check(body.Contains("检视")&&body.Contains("抓一张牌")&&!body.Contains("Player")&&!body.Contains("Draw a card"),"Generated target-player look/draw is Chinese with inline or split paragraphs");
        }
        string alternative="- Counter target noncreature spell. If that spell is countered this way, exile it instead of putting it into its owner's graveyard. (If it's not your turn, you may exile a blue card from your hand rather than pay this spell's mana cost.)";
        string translated=(string)Call(ui,"PrintedRuleText",cards["Force of Negation"],alternative);
        Check(translated.Contains("反击")&&!translated.Contains("Counter target")&&!translated.Contains("If it's"),"Existing Chinese rules cover reordered alternative-cost reminder");
        var complete=JsonUtility.FromJson<ForgeRequest>(File.ReadAllText(Out+"/force-complete.json"));
        int option=(int)typeof(ForgeBattlefieldController).GetMethod("CompletedSingleCardOption",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{complete});
        Check(option>=0&&complete.options[option].id=="ok","Actual finished Force selection has exactly one native confirmation to auto-submit");
        var service=OnlineMatchService.EnsureInstance();
        v.request=complete;v.awaitingInput=true;int submitted=0,submittedIndex=-1;
        service.GetType().GetField("<ForgeView>k__BackingField",Hidden).SetValue(service,new OnlinePlayerViewState{forge=v});Set(ui,"service",service);
        ui.Present(v,v.seat,true,i=>{submitted++;submittedIndex=i;},null,"");Call(ui,"UpdateAutomaticControl");
        Check(submitted==1&&submittedIndex==option,"Unity consumer submits the finished native OK without a second user click");
        Call(ui,"UpdateAutomaticControl");Check(submitted==1,"Completed one-card confirmation is not submitted twice");
        Set(ui,"service",null);service.GetType().GetField("<ForgeView>k__BackingField",Hidden).SetValue(service,null);
        Call(service,"ExpectReplay","timeout-probe");Call(service,"TickEndReplay",double.MaxValue);
        Check(!((HashSet<string>)Get(service,"expectedReplays")).Contains("timeout-probe"),"Replay timeout expires after leaving result review");
        complete.minCount=0;Check((int)typeof(ForgeBattlefieldController).GetMethod("CompletedSingleCardOption",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{complete})>=0,"Optional one-card choice completes after selecting its card");
        complete.selectedCount=0;Check((int)typeof(ForgeBattlefieldController).GetMethod("CompletedSingleCardOption",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{complete})<0,"Initial selection remains manual and cancellable");
        Call(ui,"LeaveInspectCard");
        v.request=Read(Directory.GetFiles(Out,"view-Surveil-*.json").First()).request;ui.Present(v,v.seat,true,i=>{},null,"");yield return null;Canvas.ForceUpdateCanvases();
        var panel=(RectTransform)Get(ui,"selectionPanel");
        foreach(var tile in panel.GetComponentsInChildren<RectTransform>().Where(t=>t.name.StartsWith("ChoiceCard_"))) {
            var art=(RectTransform)tile.Find("Artwork");
            foreach(var button in tile.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("Select_"))){var b=(RectTransform)button.transform;Check(b.anchoredPosition.y+b.rect.height*.5f<=art.anchoredPosition.y-art.rect.height*.5f-8,"Surveil destination buttons clear artwork by at least eight pixels");}
        }
        ScreenCapture.CaptureScreenshot(Out+"/surveil.png");yield return null;
        var pointer=new GameObject("HoverDelayProbe",typeof(RectTransform)).AddComponent<ForgeBattlefieldCardPointer>();int entered=0;pointer.Configure(()=>entered++,()=>{},null,false);pointer.OnPointerEnter(new PointerEventData(EventSystem.current));
        yield return new WaitForSecondsRealtime(.3f);Check(entered==0,"Battlefield details do not open immediately");
        yield return new WaitForSecondsRealtime(.5f);Check(entered==1,"Battlefield details open once after menu-style delay");pointer.OnPointerExit(null);pointer.OnPointerEnter(null);pointer.OnPointerExit(null);yield return new WaitForSecondsRealtime(.8f);Check(entered==1,"Leaving before delay cancels hover details");UnityEngine.Object.Destroy(pointer.gameObject);
        var food=new ForgeCardView{id=9901,name="Food",zone="Battlefield",token=true,stackableStateKnown=true,stackablePermanent=true,currentType="Token Artifact - Food",currentRules="{2}, {T}, Sacrifice this artifact: You gain 3 life.",tokenArtKey="food",tokenSourcePrintingKey="ELD/160",ownerSeat=1};
        var other=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(food));other.id=9902;other.tokenSourcePrintingKey="ELD/161";
        Check(ForgeBattlefieldController.ArenaGroupKey(food)==ForgeBattlefieldController.ArenaGroupKey(other),"Equivalent unlinked Food merges across generators");other.tapped=true;Check(ForgeBattlefieldController.ArenaGroupKey(food)!=ForgeBattlefieldController.ArenaGroupKey(other),"Food with different tapped state stays separate");
        var nativeFood=JsonUtility.FromJson<ForgePlayerView>("{\"battlefield\":"+File.ReadAllText(Out+"/food-native.json")+"}").battlefield;
        Check(ForgeBattlefieldController.ArenaGroupKey(nativeFood[0])==ForgeBattlefieldController.ArenaGroupKey(nativeFood[1]),"Actual native Food metadata merges across generators");
        var nativeGroups=(List<ForgeCardView[]>)Call(ui,"ArenaVisualGroups",nativeFood,"YourSupport");
        Check(nativeGroups.Count==1&&nativeGroups[0].Length==2,"Unity grouping consumer displays equivalent native Food as one pair");
        var originalScale=GamePreferences.TextScale;
        GamePreferences.SetScale(1.3f);ui.Present(v,v.seat,true,i=>{},null,"");yield return null;Canvas.ForceUpdateCanvases();
        panel=(RectTransform)Get(ui,"selectionPanel");
        foreach(var tile in panel.GetComponentsInChildren<RectTransform>().Where(t=>t.name.StartsWith("ChoiceCard_"))){var art=(RectTransform)tile.Find("Artwork");foreach(var button in tile.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("Select_"))){var b=(RectTransform)button.transform;Check(b.anchoredPosition.y+b.rect.height*.5f<=art.anchoredPosition.y-art.rect.height*.5f-8,"Surveil buttons clear art at maximum text scale");}}
        GamePreferences.SetScale(originalScale);
        GameObject.Find("InspectPanel")?.SetActive(false);
        v.request=new ForgeRequest{kind="InputPassPriority",requestId=900001,options=Array.Empty<ForgeOption>()};v.phase="UNTAP";ui.Present(v,v.seat,false,null,null,"");string phase=(string)Get(ui,"visiblePhaseKey");v.phase="UPKEEP";ui.Present(v,v.seat,false,null,null,"");Check((string)Get(ui,"visiblePhaseKey")==phase,"Minor steps within one phase-track region do not restart animation");
        var sample=v.players.SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>()).First(c=>c.creature);
        foreach(int count in new[]{6,10,18}){
            var board=Read(Directory.GetFiles(Out,"view-Surveil-*.json").First());board.request=new ForgeRequest{kind="InputPassPriority",requestId=950000+count,options=Array.Empty<ForgeOption>()};
            foreach(var player in board.players){var field=new List<ForgeCardView>();for(int i=0;i<count;i++){var c=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(sample));c.id=player.seat*10000+i;c.ownerSeat=player.seat;c.attacking=c.blocking=false;c.attachedToId=0;c.stackablePermanent=false;field.Add(c);}for(int i=0;i<(count==6?4:count==10?7:14);i++){var c=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(land));c.id=player.seat*10000+100+i;c.ownerSeat=player.seat;c.tapped=i%2==0;c.stackablePermanent=false;field.Add(c);}player.battlefield=field.ToArray();}
            ui.Present(board,board.seat,false,null,null,"");yield return new WaitForSecondsRealtime(.7f);Canvas.ForceUpdateCanvases();
            var dict=(System.Collections.IDictionary)Get(ui,"arenaCards");var ownC=new List<Bounds>();var oppC=new List<Bounds>();var ownL=new List<Bounds>();var oppL=new List<Bounds>();var surface=(RectTransform)Get(ui,"surface");
            foreach(System.Collections.DictionaryEntry entry in dict){var root=(RectTransform)entry.Value.GetType().GetField("root").GetValue(entry.Value);if(!root.gameObject.activeInHierarchy)continue;string key=(string)entry.Key;var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(surface,root);if(key.StartsWith("YourCreatures:"))ownC.Add(bounds);if(key.StartsWith("OpponentCreatures:"))oppC.Add(bounds);if(key.StartsWith("YourLands:"))ownL.Add(bounds);if(key.StartsWith("OpponentLands:"))oppL.Add(bounds);}
            Check(ownC.Count>0&&oppC.Count>0&&oppC.Min(b=>b.min.y)-ownC.Max(b=>b.max.y)>=24,"Both creature sides have clear gap, population "+count);
            Check(ownC.Min(b=>b.min.y)-ownL.Max(b=>b.max.y)>=24&&oppL.Min(b=>b.min.y)-oppC.Max(b=>b.max.y)>=24,"Creatures and lands have clear gap, population "+count+" own="+(ownC.Min(b=>b.min.y)-ownL.Max(b=>b.max.y))+" opponent="+(oppL.Min(b=>b.min.y)-oppC.Max(b=>b.max.y)));
            ScreenCapture.CaptureScreenshot(Out+"/battlefield-"+count+".png");yield return null;
        }

    }
}
#endif
