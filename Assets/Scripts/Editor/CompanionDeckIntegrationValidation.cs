#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CompanionDeckIntegrationValidation
{
    const string Output=".tools/companion-editor-1015/csharp";
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static readonly List<string> steps=new();
    public static bool Running{get;private set;}
    static void Check(bool ok,string label){steps.Add((ok?"PASS ":"FAIL ")+label);}
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Hidden).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,Hidden).SetValue(o,v);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Hidden).Invoke(o,a);
    static DeckDefinition Clone(DeckDefinition d)=>JsonUtility.FromJson<DeckDefinition>(JsonUtility.ToJson(d));
    static CardDefinition Card(string key)=>CardDatabaseRepository.LoadAll().First(c=>c.PrintingKey==key);
    static DeckEntry Entry(string key,int count=1){var c=Card(key);return new DeckEntry{setCode=c.setCode,collectorNumber=c.CollectorNumber,cardId=c.cardID,count=count};}
    static DeckDefinition Deck()=>new DeckDefinition{name="Companion integration fixture",cards=new List<DeckEntry>{Entry("10E/376",60)},sideboard=new List<DeckEntry>{Entry("IKO/222"),Entry("IKO/226")},companion=Entry("IKO/222")};
    static void Save(){Directory.CreateDirectory(Output);File.WriteAllLines(Output+"/steps.txt",steps);File.WriteAllText(Output+"/result.txt",steps.Any(s=>s.StartsWith("FAIL"))?"FAIL":"PASS "+steps.Count+" bounded data/UI/authority checks");}
    public static void Data()
    {
        steps.Clear();Directory.CreateDirectory(Output);var cards=CardDatabaseRepository.LoadAll();var exchange=new DeckTextExchange(cards);var d=Deck();
        Check(cards.Where(DeckCompanion.IsCompanion).Select(c=>c.cardName).Distinct().Count()==10,"all ten pinned companion identities available");
        foreach(var format in new[]{DeckTextExchange.Format.MTGA,DeckTextExchange.Format.MTGB}){
            string text=exchange.Export(d,format);var read=exchange.Import(text);Check(read.Success&&DeckCompanion.Has(read.Deck)&&read.Deck.companion.PrintingKey=="IKO/222",format+" preserves designation");
            Check(read.Success&&read.Deck.cards.Sum(e=>e.count)==60&&read.Deck.sideboard.Sum(e=>e.count)==2,format+" companion metadata does not duplicate physical card");
        }
        string file=Path.GetFullPath(Output+"/saved.mdk");File.WriteAllText(file,DeckRepository.SerializeLineDeck(d));var loaded=DeckRepository.Load(file);
        Check(DeckCompanion.Has(loaded)&&loaded.companion.PrintingKey=="IKO/222"&&loaded.sideboard.Sum(e=>e.count)==2,"repository reads saved designation and physical counts");
        Check(DeckCompanion.Has(Clone(d)),"network/JSON cloning preserves chosen printing");
        var unmarked=new DeckDefinition();Check(!DeckCompanion.Has(Clone(unmarked))&&!DeckRepository.SerializeLineDeck(Clone(unmarked)).Contains("#companion"),"JsonUtility null placeholder is not a companion");
        Check(!exchange.Import("Companion\n2 Jegantha, the Wellspring\nDeck\n60 Mountain").Success,"reject multi-copy companion designation");
        Check(!exchange.Import("Companion\n1 Jegantha, the Wellspring\n1 Lurrus of the Dream-Den\nDeck\n60 Mountain").Success,"reject two companions");
        Check(!exchange.Import("Companion\n1 Shock\nDeck\n60 Mountain").Success,"reject noncompanion designation");
        var missing=exchange.Import("Companion\n1 Jegantha, the Wellspring\nDeck\n60 Mountain");Check(missing.Success&&missing.Deck.sideboard.Sum(e=>e.count)==1,"companion-only external section creates one sideboard card");
        bool lossy=false;try{exchange.Export(d,DeckTextExchange.Format.MO);}catch(ArgumentException){lossy=true;}Check(lossy,"MO export refuses silent designation loss");
        var bad=Clone(d);bad.companion=Entry("10E/376");Check(!DeckCompanion.ValidateReference(bad,cards,out _),"server rejects main-only/noncompanion reference");
        bad=Clone(d);bad.sideboard.Clear();Check(!DeckCompanion.ValidateReference(bad,cards,out _),"server rejects missing sideboard reference");
        DeckRepository.RemoveCard(d,Card("IKO/222"),1,true);Check(!DeckCompanion.Has(d)&&d.sideboard.Count==1,"removing last marked sideboard copy clears designation");
        var changed=OnlineSeriesDeckChanges.Remove(Deck(),1,new[]{new ForgeMatchRemoval{cardId=999,ownerSeat=1,printingKey="IKO/222",fromSideboard=true,fromSection="Sideboard"}});Check(!DeckCompanion.Has(changed),"match-only permanent removal clears unavailable companion");
        Save();
    }
    public static void Ui()
    {
        if(Running)return;Running=true;
        if(!Application.isPlaying)throw new Exception("Main Play mode required");
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();if(menu==null)throw new Exception("Main menu required");
        menu.StartCoroutine(Guard(UiAndAuthority(menu)));
    }
    public static void UiLayoutDelta(bool onlySideboard=false)
    {
        if(Running)return;Running=true;
        if(!Application.isPlaying)throw new Exception("Main Play mode required");
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();steps.Clear();menu.StartCoroutine(Guard(LayoutDelta(menu,onlySideboard)));
    }
    static IEnumerator Guard(IEnumerator e){File.WriteAllText(Output+"/result.txt","RUNNING");while(true){object next;try{if(!e.MoveNext())break;next=e.Current;}catch(Exception ex){steps.Add("FAIL "+ex);break;}yield return next;}(e as IDisposable)?.Dispose();Save();Running=false;}
    static void Click(GameObject go){var r=(RectTransform)go.transform;var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);if(hits.Count==0||!hits[0].gameObject.transform.IsChildOf(go.transform))throw new Exception("Blocked ray "+go.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    static string InspectorKey(DeckDefinition d){var t=typeof(DeckCompanionInspector).GetNestedType("Config",BindingFlags.NonPublic);var c=Activator.CreateInstance(t,true);var p=new DeckDefinition{name="Companion check",cards=d.cards,sideboard=d.sideboard};t.GetField("decks").SetValue(c,new[]{p,p});return JsonUtility.ToJson(c);}
    static IEnumerator Authority()
    {
        var go=new GameObject("CompanionAuthorityFixture");go.SetActive(false);var service=go.AddComponent<OnlineMatchService>();
        var registry=Field<OnlineRoomRegistry>(service,"roomRegistry");registry.TryGetOrCreate("companion-test",out var room,out _);room.config.formatId="none";
        var member=new OnlineRoomMember{seat=1,clientId=321,connected=true,displayName="Test"};room.members[1]=member;
        var locationType=typeof(OnlineMatchService).GetNestedType("ClientLocation",BindingFlags.NonPublic);var location=Activator.CreateInstance(locationType,true);locationType.GetField("seat").SetValue(location,1);locationType.GetField("roomId").SetValue(location,"companion-test");((IDictionary)Field<object>(service,"clientLocations"))[(ulong)321]=location;
        var cache=(Dictionary<string,Task<DeckCompanionInspector.Result>>)typeof(DeckCompanionInspector).GetField("cache",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);var original=cache.ToArray();var deck=Deck();string key=InspectorKey(deck);
        try{
            var pending=new TaskCompletionSource<DeckCompanionInspector.Result>();cache[key]=pending.Task;
            Call(service,"HandleDeck",(ulong)321,new OnlineDeckMessage{deck=Clone(deck)});Call(service,"HandleReady",(ulong)321,new OnlineReadyMessage{ready=true});
            Check(member.deck==null&&!member.ready,"ready packet waits for authoritative companion inspection");
            pending.SetResult(new DeckCompanionInspector.Result{legal=new[]{"IKO/222"}});yield return null;yield return null;
            Check(member.ready&&DeckCompanion.Has(member.deck),"validated deck commits then applies queued readiness");
            pending=new TaskCompletionSource<DeckCompanionInspector.Result>();cache[key]=pending.Task;Call(service,"HandleDeck",(ulong)321,new OnlineDeckMessage{deck=Clone(deck)});
            var clear=Clone(deck);clear.companion=null;Call(service,"HandleDeck",(ulong)321,new OnlineDeckMessage{deck=clear});pending.SetResult(new DeckCompanionInspector.Result{legal=new[]{"IKO/222"}});yield return null;yield return null;
            Check(!DeckCompanion.Has(member.deck)&&!member.ready,"superseded native result cannot restore old designation/readiness");
            pending=new TaskCompletionSource<DeckCompanionInspector.Result>();cache[key]=pending.Task;Call(service,"HandleDeck",(ulong)321,new OnlineDeckMessage{deck=Clone(deck)});Call(service,"HandleReady",(ulong)321,new OnlineReadyMessage{ready=true});pending.SetResult(new DeckCompanionInspector.Result{legal=Array.Empty<string>()});yield return null;yield return null;
            Check(!DeckCompanion.Has(member.deck)&&!member.ready,"illegal native result never commits deck or queued readiness");
            room.series.bestOf=3;room.series.betweenGames=true;member.registeredDeck=Clone(clear);pending=new TaskCompletionSource<DeckCompanionInspector.Result>();cache[key]=pending.Task;Call(service,"HandleDeck",(ulong)321,new OnlineDeckMessage{deck=Clone(deck)});pending.SetResult(new DeckCompanionInspector.Result{legal=new[]{"IKO/222"}});yield return null;yield return null;
            Check(DeckCompanion.Has(member.deck)&&member.deck.cards.Sum(e=>e.count)==60&&member.deck.sideboard.Sum(e=>e.count)==2,"BO3 designation change preserves exact registered physical pool");
        }finally{cache.Clear();foreach(var e in original)cache[e.Key]=e.Value;registry.Dispose();UnityEngine.Object.Destroy(go);}
    }
    static IEnumerator UiAndAuthority(MainMenuController menu)
    {
        while(!GamePreferences.Ready||Field<List<CardDefinition>>(menu,"cards").Count==0)yield return null;
        var original=Field<DeckDefinition>(menu,"currentDeck");bool dirty=Field<bool>(menu,"hasUnsavedDeckChanges");string language=GamePreferences.Language;float scale=GamePreferences.TextScale;
        try{
            Call(menu,"ShowDeckBuilder");Set(menu,"currentDeck",Deck());Call(menu,"RefreshDeckGrids");yield return null;
            Click(Field<Button>(menu,"companionEntry").gameObject);yield return null;
            var dialog=Field<GameObject>(menu,"exchangeDialog");Check(dialog.name=="CompanionDeck"&&dialog.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("ChooseCompanion_"))==10,"editor companion button opens all ten candidates");
            var task=DeckCompanionInspector.Inspect(Deck());while(!task.IsCompleted)yield return null;Check(!task.IsFaulted&&task.Result.error==null&&task.Result.Allows(Deck()),"C# inspector invokes actual Forge restriction path");
            yield return new WaitForSecondsRealtime(.7f);ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/companion-editor.png"));yield return new WaitForEndOfFrame();
            Click(dialog.transform.Find("Panel/ClearCompanion").gameObject);yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();Check(!DeckCompanion.Has(Field<DeckDefinition>(menu,"currentDeck")),"actual clear click keeps sideboard cards but removes designation");
            dialog=Field<GameObject>(menu,"exchangeDialog");var choose=dialog.transform.Find("Panel/ChooseCompanion_0").gameObject;Click(choose);yield return null;Check(DeckCompanion.Has(Field<DeckDefinition>(menu,"currentDeck")),"actual candidate click designates one existing sideboard printing");
            Call(menu,"CloseDeckExchange");Set(menu,"companionDialogOpen",false);
            var authority=Authority();try{while(authority.MoveNext())yield return authority.Current;}finally{(authority as IDisposable)?.Dispose();}
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
                GamePreferences.SetLanguage(locale);yield return null;while(!GamePreferences.Ready||GamePreferences.Language!=locale)yield return null;GamePreferences.SetScale(1.3f);
                Set(menu,"currentDeck",Deck());Call(menu,"RenderCompanionDeck");yield return new WaitForSecondsRealtime(.6f);Canvas.ForceUpdateCanvases();dialog=Field<GameObject>(menu,"exchangeDialog");
                var panel=dialog.transform.Find("Panel");var note=panel.Find("CompanionNote").GetComponent<Text>();
                Check(!string.IsNullOrWhiteSpace(note.text)&&!(locale=="zh-CN"&&note.text.Contains("Companion"))&&!(locale=="ja-JP"&&note.text.Contains("Companion")),locale+" condition text localized");
                Check(panel.GetComponentsInChildren<Text>().Where(t=>t.name.StartsWith("CompanionName_")).All(t=>t.preferredHeight<=t.rectTransform.rect.height),locale+" candidate names fit large-font bounds");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/editor-"+locale+".png"));yield return new WaitForEndOfFrame();Call(menu,"CloseDeckExchange");Set(menu,"companionDialogOpen",false);
            }
        }finally{Set(menu,"currentDeck",original);Set(menu,"hasUnsavedDeckChanges",dirty);Set(menu,"companionDialogOpen",false);Call(menu,"CloseDeckExchange");Call(menu,"RefreshDeckGrids");GamePreferences.SetLanguage(language);GamePreferences.SetScale(scale);}
    }
    static IEnumerator LayoutDelta(MainMenuController menu,bool onlySideboard)
    {
        while(!GamePreferences.Ready||Field<List<CardDefinition>>(menu,"cards").Count==0)yield return null;
        var original=Field<DeckDefinition>(menu,"currentDeck");bool dirty=Field<bool>(menu,"hasUnsavedDeckChanges");string language=GamePreferences.Language;float scale=GamePreferences.TextScale;
        Canvas lobbyCanvas=null;OnlineMatchService service=null;OnlineLobbyState oldLobby=null;int oldSeat=0;
        try{
            Call(menu,"ShowDeckBuilder");
            foreach(string locale in onlySideboard?Array.Empty<string>():new[]{"zh-CN","en-US","ja-JP"}){
                GamePreferences.SetLanguage(locale);yield return null;while(!GamePreferences.Ready||GamePreferences.Language!=locale)yield return null;GamePreferences.SetScale(1.3f);
                Set(menu,"currentDeck",Deck());Call(menu,"RenderCompanionDeck");yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
                var panel=Field<GameObject>(menu,"exchangeDialog").transform.Find("Panel");var labels=panel.GetComponentsInChildren<Text>().Where(t=>t.name.StartsWith("CompanionName_")).ToArray();
                Check(labels.All(t=>t.preferredHeight<=t.rectTransform.rect.height),locale+" expanded candidate names fit large-font bounds");
                foreach(var t in labels){int i=int.Parse(t.name.Split('_').Last());var b=(RectTransform)panel.Find("ChooseCompanion_"+i);Check(t.rectTransform.anchoredPosition.y-t.rectTransform.rect.height*.5f>b.anchoredPosition.y+b.rect.height*.5f,locale+" name/button gap "+i);}
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/final-editor-"+locale+".png"));yield return new WaitForEndOfFrame();Call(menu,"CloseDeckExchange");Set(menu,"companionDialogOpen",false);
            }
            GamePreferences.SetLanguage("zh-CN");yield return null;while(!GamePreferences.Ready)yield return null;
            service=OnlineMatchService.EnsureInstance();if(service.IsOnline)throw new Exception("Isolated sideboard UI required");oldLobby=service.LobbyState;oldSeat=service.LocalSeat;
            service.GetType().GetProperty("LobbyState").SetValue(service,new OnlineLobbyState{series=new OnlineSeriesState{bestOf=3,gameNumber=2,betweenGames=true}});service.GetType().GetProperty("LocalSeat").SetValue(service,1);
            lobbyCanvas=ReplayWidgets.Canvas("CompanionSideboardFixture",700);
            var lobby=(OnlineLobbyPanel)typeof(OnlineLobbyPanel).GetMethod("Create",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{lobbyCanvas.transform});lobby.enabled=false;Set(lobby,"selectedDeck",Deck());Set(lobby,"sideboardGameNumber",-1); // Show() resets this after assigning the registered deck.
            foreach(string field in new[]{"connectionPageGroup","roomPageGroup","browserPageGroup"}){var g=Field<CanvasGroup>(lobby,field);g.alpha=0;g.interactable=g.blocksRaycasts=false;}
            var side=Field<CanvasGroup>(lobby,"sideboardPageGroup");side.gameObject.SetActive(true);side.alpha=1;side.interactable=side.blocksRaycasts=true;
            Call(lobby,"RefreshSideboardControls",service.LobbyState.series);yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            var tile=side.GetComponentsInChildren<OnlineSideboardCardTile>().First(t=>t.IsSideboard&&t.Entry.PrintingKey=="IKO/226");
            Click(tile.transform.Find("SetCompanion").gameObject);yield return null;Check(Field<DeckDefinition>(lobby,"selectedDeck").companion.PrintingKey=="IKO/226","BO3 actual tile click changes chosen companion");
            Click(tile.transform.Find("SetCompanion").gameObject);yield return null;Check(!DeckCompanion.Has(Field<DeckDefinition>(lobby,"selectedDeck")),"BO3 second tile click cancels companion without changing pool");
            Click(tile.transform.Find("SetCompanion").gameObject);yield return null;
            Check(Field<DeckDefinition>(lobby,"selectedDeck").cards.Sum(e=>e.count)==60&&Field<DeckDefinition>(lobby,"selectedDeck").sideboard.Sum(e=>e.count)==2,"BO3 designation clicks preserve 60+2 physical pool");
            Call(lobby,"ResetSideboardChanges");yield return null;Check(Field<DeckDefinition>(lobby,"selectedDeck").companion.PrintingKey=="IKO/222","BO3 reset restores baseline companion");
            Set(lobby,"ready",true);Call(lobby,"RefreshSideboardControls",service.LobbyState.series);
            Check(side.GetComponentsInChildren<Button>().Where(b=>b.name=="SetCompanion").All(b=>!b.interactable),"BO3 ready locks designation controls");
            Set(lobby,"ready",false);service.GetType().GetProperty("LocalSeat").SetValue(service,3);Call(lobby,"RefreshSideboardControls",service.LobbyState.series);
            Check(side.GetComponentsInChildren<Button>().Where(b=>b.name=="SetCompanion").All(b=>!b.interactable),"spectators cannot designate companions");
            service.GetType().GetProperty("LocalSeat").SetValue(service,1);Call(lobby,"RefreshSideboardControls",service.LobbyState.series);
            var status=Field<Text>(lobby,"sideboardStatusText");var readyRect=(RectTransform)Field<Button>(lobby,"sideboardReadyButton").transform;
            Check(status.gameObject.activeInHierarchy&&status.text.Contains("行侣"),"BO3 companion status is actually visible");
            Check(status.rectTransform.anchoredPosition.y+status.rectTransform.rect.height*.5f<readyRect.anchoredPosition.y-readyRect.rect.height*.5f,"BO3 status below footer buttons without overlap");
            yield return new WaitForSecondsRealtime(.7f);ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/sideboard-companion.png"));yield return new WaitForEndOfFrame();
        }finally{if(lobbyCanvas!=null)UnityEngine.Object.Destroy(lobbyCanvas.gameObject);if(service!=null){service.GetType().GetProperty("LobbyState").SetValue(service,oldLobby);service.GetType().GetProperty("LocalSeat").SetValue(service,oldSeat);}Set(menu,"currentDeck",original);Set(menu,"hasUnsavedDeckChanges",dirty);Set(menu,"companionDialogOpen",false);Call(menu,"CloseDeckExchange");Call(menu,"RefreshDeckGrids");GamePreferences.SetLanguage(language);GamePreferences.SetScale(scale);}
    }
}
#endif
