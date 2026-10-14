#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Display/policy regression evidence, separate from native outcome tests.
public static class LiveGameplayFixValidation
{
    public static bool Running;
    public static string Output;
    [UnityEditor.MenuItem("MTGB/Validation/Live Gameplay Fixes")]
    public static void RunMenu()=>Run("final");
    public static string VerifyHeadings()
    {
        var native=Directory.GetDirectories(".tools/live-gameplay-fixes-1013/native").Where(d=>File.Exists(d+"/result.json")).OrderByDescending(d=>d).First(d=>File.ReadAllText(d+"/result.json").Contains("\"PASS\""));
        var message=JsonUtility.FromJson<ForgeWireMessage>(File.ReadAllText(native+"/pinnacle-options.json"));var view=message.views[0];view.request=message.request;view.awaitingInput=true;
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("HeadingProbe").AddComponent<ForgeBattlefieldController>();ui.enabled=false;ui.Present(view,1,true,_=>{},null,"");
        var panel=(RectTransform)typeof(ForgeBattlefieldController).GetField("landChoiceMenu",Private).GetValue(ui);
        var names=panel.GetComponentsInChildren<Text>().Where(t=>t.name=="FaceName").Select(t=>t.text).ToArray();
        var types=panel.GetComponentsInChildren<Text>().Where(t=>t.name=="Type").Select(t=>t.text).ToArray();
        var modes=panel.GetComponentsInChildren<Text>().Where(t=>t.name=="Mode").Select(t=>t.text).ToArray();
        bool pass=names.Length>0&&names.All(s=>s==GamePreferences.CardFaceName("Pinnacle Emissary"))&&types.Length>0&&types.All(s=>s.Contains("生物")&&!s.Contains("creature"))&&modes.Contains("跃迁");
        string result=(pass?"PASS ":"FAIL ")+"visible choice headings: names="+string.Join(",",names)+" types="+string.Join(",",types)+" modes="+string.Join(",",modes);
        File.WriteAllText(".tools/live-gameplay-fixes-1013/headings-result.txt",result);return result;
    }
    public static void EditorBootstrap(){UnityEditor.AssetDatabase.DesiredWorkerCount=1;typeof(UnityEditor.AssetDatabase).GetMethod("ForceToDesiredWorkerCount")?.Invoke(null,null);UnityEditor.SessionState.SetBool("LiveFix.Boot",true);AttachBootstrap();UnityEditor.EditorApplication.isPlaying=true;}
    [UnityEditor.InitializeOnLoadMethod]
    static void AttachBootstrap(){if(!UnityEditor.SessionState.GetBool("LiveFix.Boot",false))return;UnityEditor.EditorApplication.update-=BootstrapTick;UnityEditor.EditorApplication.update+=BootstrapTick;}
    static void BootstrapTick(){
        if(!Application.isPlaying||UnityEditor.EditorApplication.isCompiling)return;
        Application.runInBackground=true;UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        if(Output==null){Run("final");return;}
        if(!Running){UnityEditor.SessionState.SetBool("LiveFix.Boot",false);UnityEditor.EditorApplication.update-=BootstrapTick;UnityEditor.EditorApplication.isPlaying=false;}
    }
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    sealed class Host:MonoBehaviour { }
    static readonly List<string> Checks=new();
    static void Check(bool pass,string description)=>Checks.Add((pass?"PASS ":"FAIL ")+description);
    static object Call(object owner,string method,params object[] args)=>owner.GetType().GetMethod(method,Private).Invoke(owner,args);
    static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,Private).SetValue(owner,value);
    static IEnumerator Frame(){yield return null;Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();}
    public static void Run(string phase)
    {
        if(!Application.isPlaying||Running)throw new Exception("Play required");
        Running=true;Checks.Clear();Output=".tools/live-gameplay-fixes-1013/"+phase+"-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmss");Directory.CreateDirectory(Output);
        new GameObject("LiveGameplayFixValidation").AddComponent<Host>().StartCoroutine(Guard(Exercise()));
    }
    static IEnumerator Guard(IEnumerator task)
    {
        while(true){bool more;object next;try{more=task.MoveNext();next=more?task.Current:null;}catch(Exception e){Checks.Add("FAIL "+e);more=false;next=null;}if(!more)break;yield return next;}
        File.WriteAllLines(Output+"/result.txt",Checks);Running=false;Debug.Log("LIVE GAMEPLAY "+Output);
    }
    static ForgeCardView Card(CardDefinition definition,int id)=>new(){id=id,name=definition.cardName,originalName=definition.cardName,printingKey=definition.PrintingKey,zone="Battlefield",ownerSeat=1,currentRules=definition.description,originalRules=definition.description,currentType=definition.GetTypeLine(),power=definition.attack,toughness=definition.defence,creature=true};
    static IEnumerator Exercise()
    {
        float until=Time.realtimeSinceStartup+30;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;
        foreach(var notice in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))notice.enabled=false;
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("LiveGameplayBoard").AddComponent<ForgeBattlefieldController>();ui.enabled=false;
        var data=CardDatabaseRepository.LoadAll();var creature=data.First(d=>d.cardName=="Pinnacle Emissary");
        var request=new ForgeRequest{kind="InputPassPriority",seat=1,playerSeat=1,requestId=100,options=new[]{new ForgeOption{id="ok",label="OK"}}};
        var view=new ForgeSeatView{seat=1,turn=3,turnSeat=1,prioritySeat=1,phase="MAIN1",awaitingInput=true,request=request,stackItems=Array.Empty<ForgeStackItem>(),players=new[]{new ForgePlayerView{seat=1,life=20,hand=Array.Empty<ForgeCardView>(),battlefield=Array.Empty<ForgeCardView>()},new ForgePlayerView{seat=2,life=20,hand=Array.Empty<ForgeCardView>(),battlefield=Array.Empty<ForgeCardView>()}}};
        foreach(var phase in new[]{"MAIN1","MAIN2"})foreach(var mode in new[]{ForgeControlMode.On,ForgeControlMode.Auto,ForgeControlMode.Off}){
            view.phase=phase;Check(ForgeControlPolicy.AutomaticOption(view,1,mode,false)<0,"own "+phase+" requires manual end in "+mode);
        }
        ui.Present(view,1,true,_=>{},null,"");
        Check(GameObject.Find("PriorityControlMode").GetComponentInChildren<Text>().text=="自动","approved mode label is 自动");
        float hold=(float)typeof(ForgeBattlefieldController).GetField("phaseVisibleUntil",Private).GetValue(ui);
        Check(hold>Time.unscaledTime,"new phase has a visible transition hold");
        var track=GameObject.Find("PhaseTrack");Check(track.GetComponentsInChildren<ForgeUiMotion>().Any(m=>m.IsMoving),"phase bars animate during phase transition");
        yield return Frame();
        var ent=data.First(d=>d.cardName=="Generous Ent");var entCard=Card(ent,701);
        string search="Player 1 searches their library for a Forest card, reveals it, puts it into their hand, then shuffles.";
        string summary=(string)Call(ui,"AbilityEffectText",entCard,search);
        Check(!summary.Contains("searches their library")&&summary.Contains("搜寻"),"cycling search summary is Chinese with accurate recipient");
        string genericPrompt=(string)Call(ui,"NativeDisplayText","Select a card from your library");
        Check(!genericPrompt.Contains("Select a card"),"library selection instruction is translated");
        var tile=new GameObject("CastSummaryProbe",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();tile.SetParent(GameObject.Find("Canvas").transform,false);tile.sizeDelta=new Vector2(260,400);
        var source=Card(creature,702);source.zone="Hand";
        view.request=new ForgeRequest{kind="dialog",message="getAbilityToPlay",seat=1,options=new[]{new ForgeOption{id="choice:0",label="Pinnacle Emissary - Creature 3 / 3",card=source}}};Set(ui,"view",view);
        Call(ui,"DrawAbilityChoice",tile,source,view.request.options[0],tile.sizeDelta);yield return Frame();
        string rendered=tile.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Symbols").text;
        Check(rendered.Contains("每当你施放神器咒语")&&!rendered.Contains("Creature 3 / 3"),"ordinary cast summary shows actual translated effect");
        UnityEngine.Object.Destroy(tile.gameObject);
        view.request=request;view.phase="MAIN1";
        view.players[0].battlefield=Enumerable.Range(0,5).Select(i=>Card(creature,800+i)).ToArray();
        ui.Present(view,1,true,_=>{},null,"");yield return Frame();
        var cards=(IDictionary)typeof(ForgeBattlefieldController).GetField("arenaCards",Private).GetValue(ui);
        var widths=new List<float>();
        foreach(DictionaryEntry pair in cards)if(((string)pair.Key).StartsWith("YourCreatures:")) {
            var root=(RectTransform)pair.Value.GetType().GetField("root").GetValue(pair.Value);widths.Add(root.rect.width);
        }
        Check(widths.Count==5&&widths.All(w=>w>=115),"five creatures retain readable normal size: "+string.Join(",",widths));
        var arrow=(ForgeTargetArrow)Call(ui,"NewAttackArrow","CombatLayerProbe");
        var canvas=GameObject.Find("Canvas").GetComponent<Canvas>();var layer=arrow.GetComponent<Canvas>();
        Check(layer!=null&&layer.overrideSorting&&layer.sortingOrder>canvas.sortingOrder,"combat arrows stay above card and avatar layers");
        Check(!arrow.raycastTarget,"combat arrows do not block target clicks");UnityEngine.Object.Destroy(arrow.gameObject);
        var service=OnlineMatchService.EnsureInstance();var prior=service.LobbyState;
        var stateField=typeof(OnlineMatchService).GetField("<LobbyState>k__BackingField",Private);
        stateField.SetValue(service,new OnlineLobbyState{series=new OnlineSeriesState{gameNumber=2,betweenGames=true},players=new List<OnlineLobbyPlayer>()});
        var saved=new DeckDefinition{name="另一个客户端的牌组",cards=new List<DeckEntry>(),sideboard=new List<DeckEntry>()};
        OnlineClientProfileStore.SaveDeck("livefix-owned",saved);
        var lobby=(OnlineLobbyPanel)typeof(OnlineLobbyPanel).GetMethod("Create",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{GameObject.Find("Canvas").transform});
        var own=new DeckDefinition{name="本客户端实际出战牌组",cards=new List<DeckEntry>(),sideboard=new List<DeckEntry>()};
        Set(lobby,"selectedDeck",own);Call(lobby,"LoadProfile","livefix-owned");
        var current=(DeckDefinition)typeof(OnlineLobbyPanel).GetField("selectedDeck",Private).GetValue(lobby);
        Check(current?.name==own.name,"between-games reload keeps this client's registered deck, not shared profile deck");
        Set(lobby,"selectedDeck",new DeckDefinition{name="livefix-deleted-never-on-disk",cards=new List<DeckEntry>(),sideboard=new List<DeckEntry>()});
        stateField.SetValue(service,null);Call(lobby,"ReloadDeckChoices");
        var available=(List<DeckDefinition>)typeof(OnlineLobbyPanel).GetField("availableDecks",Private).GetValue(lobby);
        Check(!available.Any(d=>d.name=="livefix-deleted-never-on-disk"),"deleted local deck is not resurrected in choices");
        stateField.SetValue(service,prior);UnityEngine.Object.Destroy(lobby.gameObject);
        // Replay actual native choice/stack payloads through the same display boundaries.
        var native=Directory.GetDirectories(".tools/live-gameplay-fixes-1013/native").Where(d=>File.Exists(d+"/result.json")).OrderByDescending(d=>d).First(d=>File.ReadAllText(d+"/result.json").Contains("\"PASS\""));
        foreach(string file in new[]{"forest-stack","forest-picker","pinnacle-options","all-draft"}){
            var message=JsonUtility.FromJson<ForgeWireMessage>(File.ReadAllText(native+"/"+file+".json"));var snapshot=message.views[0];snapshot.request=message.request;snapshot.awaitingInput=true;
            int chosen=-1;ui.Present(snapshot,1,true,index=>chosen=index,null,"");yield return Frame();
            if(file=="forest-stack"){
                var stack=snapshot.stackItems.Single(s=>s.source.name=="Generous Ent");var body=(string)Call(ui,"AbilityEffectText",stack.source,stack.description);
                Check(body.Contains("搜寻")&&!body.Contains("searches"),"real native cycling stack translates rule and recipient");
            }
            if(file=="forest-picker"){
                string prompt=(string)Call(ui,"NativeDisplayText",message.request.message);
                Check(!prompt.Contains("Select a card")&&!prompt.Contains("Search your library")&&prompt.Contains("牌库"),"real multiline native library picker is all Chinese");
            }
            if(file=="pinnacle-options"){
                string text=string.Join("\n",UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Select(t=>t.text));
                Check(!text.Contains("Creature 3 / 3")&&!text.Contains("You may cast")&&text.Contains("每当你施放神器咒语"),"normal and alternative cast modes use Chinese visible rules");
            }
            if(file=="all-draft"){
                var all=GameObject.Find("AttackBatchActions").transform.Find("All").GetComponent<Button>();var reset=all.transform.parent.Find("Recall").GetComponent<Button>();
                Check(all.interactable&&reset.interactable,"native draft offers both batch attack and cancel controls");reset.onClick.Invoke();Check(chosen==Array.FindIndex(snapshot.request.options,o=>o.id=="reset-attacks"),"cancel button submits native batch option exactly once");
            }
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/"+file+".png"));yield return Frame();
        }
        // A profile reload cannot replace an in-session registered deck.
        var registration=typeof(OnlineMatchService).GetField("clientRegisteredDeck",Private);var priorDeck=registration.GetValue(service);
        registration.SetValue(service,new DeckDefinition{name="本机独立出战牌组"});
        var safe=service.LocalRegisteredDeck;safe.name="外部修改";Check(service.LocalRegisteredDeck.name=="本机独立出战牌组","session registered deck is an isolated clone");registration.SetValue(service,priorDeck);
        // External rename/delete tests touch only an owned temporary .mdk.
        string ownPath=Path.Combine(DeckRepository.DeckRoot,"livefix-owned-file.mdk"),renamed=Path.Combine(DeckRepository.DeckRoot,"livefix-renamed-file.mdk");
        if(File.Exists(ownPath)||File.Exists(renamed))throw new Exception("Owned fixture path already exists");
        try{
            string before=DeckRepository.DirectoryRevision();File.WriteAllText(ownPath,"#name livefix-owned-file\n#main\n1 10E/376\n");
            Check(DeckRepository.DirectoryRevision()!=before,"file add changes directory revision");File.Move(ownPath,renamed);
            Check(DeckRepository.Load(renamed).name=="livefix-renamed-file","external file rename overrides stale internal deck name");
            string revision=DeckRepository.DirectoryRevision();File.Delete(renamed);Check(DeckRepository.DirectoryRevision()!=revision&&!DeckRepository.LoadAll().Any(d=>d.name=="livefix-renamed-file"),"external delete immediately removes deck from repository");
        }finally{if(File.Exists(ownPath))File.Delete(ownPath);if(File.Exists(renamed))File.Delete(renamed);}
        ui.Present(view,1,true,_=>{},null,"");yield return Frame();
        var artifactDefinition=data.First(d=>d.cardName=="Mishra's Bauble");
        var artifact=Card(artifactDefinition,901);artifact.creature=false;artifact.currentType="Artifact";
        var enchantmentDefinition=data.First(d=>d.cardName=="Glorious Anthem");var enchantment=Card(enchantmentDefinition,902);enchantment.creature=false;enchantment.currentType="Enchantment";
        view.players[0].battlefield=new[]{artifact,enchantment};ui.Present(view,1,true,_=>{},null,"");yield return Frame();
        cards=(IDictionary)typeof(ForgeBattlefieldController).GetField("arenaCards",Private).GetValue(ui);widths.Clear();
        foreach(DictionaryEntry pair in cards)if(((string)pair.Key).StartsWith("YourSupport:"))widths.Add(((RectTransform)pair.Value.GetType().GetField("root").GetValue(pair.Value)).rect.width);
        Check(widths.Count==2&&widths.All(w=>w>=115),"two artifact/enchantment permanents retain normal size: "+string.Join(",",widths));
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/support-size.png"));yield return Frame();
        // Both pregame actors see the battlefield; private hands have not been dealt.
        var localField=typeof(OnlineMatchService).GetField("<LocalSeat>k__BackingField",Private);int priorSeat=(int)localField.GetValue(service);
        stateField.SetValue(service,new OnlineLobbyState{playDrawChooserSeat=1,config=new OnlineRoomConfig{startingLife=20},players=new List<OnlineLobbyPlayer>{new(){seat=1,displayName="甲"},new(){seat=2,displayName="乙"}}});
        Set(ui,"service",service);localField.SetValue(service,1);Call(ui,"PresentMatchPreparation");yield return Frame();
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==ForgeBattlefieldController.SceneName&&GameObject.Find("BattlefieldPlayDraw")!=null,"play/draw is presented in battlefield scene");
        var firstBoard=(ForgeSeatView)typeof(ForgeBattlefieldController).GetField("view",Private).GetValue(ui);Check(firstBoard.players.All(p=>p.hand.Length==0&&p.handCount==0),"preparation board exposes no opening hands before choice");
        Check(GameObject.Find("BattlefieldPlayDraw").GetComponentsInChildren<Button>().Length==2,"chooser alone sees play/draw actions");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/play-draw.png"));yield return Frame();
        localField.SetValue(service,2);Call(ui,"PresentMatchPreparation");yield return Frame();
        Check(GameObject.Find("BattlefieldPlayDraw").GetComponentsInChildren<Button>().Length==0,"other seat sees waiting state without choice actions");
        stateField.SetValue(service,prior);localField.SetValue(service,priorSeat);Set(ui,"service",null);
        UnityEngine.Object.Destroy(GameObject.Find("BattlefieldPlayDraw"));Set(ui,"matchPreparationRoot",null);Set(ui,"preparationKey",null);
        view.players[0].battlefield=Enumerable.Range(0,5).Select(i=>Card(creature,800+i)).ToArray();ui.Present(view,1,true,_=>{},null,"");yield return Frame();
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/board-size.png"));
    }
}
#endif
