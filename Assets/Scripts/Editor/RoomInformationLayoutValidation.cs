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

public static class RoomInformationLayoutValidation
{
    const string Output=".tools/room-info-layout-1015";
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static readonly List<string> steps=new();
    public static bool Running{get;private set;}
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Hidden).GetValue(o);
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Hidden).Invoke(o,args);
    static void Check(bool ok,string name){steps.Add((ok?"PASS ":"FAIL ")+name);}
    public static string CheckAnteVisibility()
    {
        var canvas=new GameObject("Ante visibility test",typeof(Canvas));
        try{
            var create=typeof(OnlineLobbyPanel).GetMethod("Create",BindingFlags.NonPublic|BindingFlags.Static);
            var panel=(OnlineLobbyPanel)create.Invoke(null,new object[]{canvas.transform});
            var format=Field<Dropdown>(panel,"cardFormatDropdown");
            var diy=Field<Toggle>(panel,"customContentToggle");
            var ante=Field<Toggle>(panel,"anteToggle");
            void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
            bool EnabledInConfig()=>((OnlineRoomConfig)Call(panel,"ReadRoomConfig")).useAnte;
            Require(!ante.gameObject.activeSelf&&!EnabledInConfig(),"Fresh form exposed ante");
            format.value=Array.IndexOf(DeckFormatLegality.Keys,"none");
            diy.isOn=true;
            Require(ante.gameObject.activeSelf&&!ante.isOn,"Eligible form should offer unchecked ante");
            ante.isOn=true;
            Require(EnabledInConfig(),"Checked ante missing from room config");
            format.value=Array.IndexOf(DeckFormatLegality.Keys,"modern");
            Require(!ante.gameObject.activeSelf&&!ante.isOn&&!EnabledInConfig(),"Format change retained ante");
            format.value=Array.IndexOf(DeckFormatLegality.Keys,"none");
            ante.isOn=true;
            diy.isOn=false;
            Require(!ante.gameObject.activeSelf&&!ante.isOn&&!EnabledInConfig(),"Disabling DIY retained ante");
            var rooms=Field<List<OnlineRoomListing>>(panel,"browserRooms");
            var label=DeckCompanion.Text("赌注开启","Ante enabled","アンティ有効");
            var word=DeckCompanion.Text("赌注","Ante","アンティ");
            foreach(bool enabled in new[]{false,true}){
                var config=new OnlineRoomConfig{formatId="none",allowCustomContent=true,useAnte=enabled};
                Call(panel,"RefreshRoomSummary",new OnlineLobbyState{config=config});
                var summary=Field<Text>(panel,"roomRulesText").text;
                Require(enabled?summary.Contains(label):!summary.Contains(word),"Incorrect in-room ante text");
                rooms.Add(new OnlineRoomListing{roomId="test",rulesVersion=3,format="none",allowCustomContent=true,useAnte=enabled});
            }
            rooms.Add(new OnlineRoomListing{roomId="legacy"});
            Call(panel,"DrawBrowserRooms");
            var list=Field<RectTransform>(panel,"roomList");
            for(int i=0;i<rooms.Count;i++){
                var flags=list.Find("Room_"+i+"/Permissions").GetComponent<Text>().text;
                Require(i==1?flags.Contains(label):!flags.Contains(word),"Incorrect directory ante text for row "+i);
            }
            return "PASS: fresh form, eligible opt-in, format and DIY reset, enabled/disabled room summary, current and legacy listings";
        }finally{UnityEngine.Object.DestroyImmediate(canvas);}
    }
    public static void Run()
    {
        if(Running)return;Running=true;steps.Clear();Directory.CreateDirectory(Output);
        UnityEngine.Object.FindFirstObjectByType<MainMenuController>().StartCoroutine(Guard());
    }
    static IEnumerator Guard()
    {
        File.WriteAllText(Output+"/result.txt","RUNNING");var run=Exercise();
        while(true){object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception e){steps.Add("FAIL "+e);break;}yield return next;}
        (run as IDisposable)?.Dispose();File.WriteAllLines(Output+"/result.txt",steps);Running=false;
    }
    public static void LocaleDelta()
    {
        if(Running)return;Running=true;steps.Clear();
        UnityEngine.Object.FindFirstObjectByType<MainMenuController>().StartCoroutine(DeltaGuard());
    }
    static IEnumerator DeltaGuard()
    {
        File.WriteAllText(Output+"/locale-result.txt","RUNNING");var run=LocaleUi();
        while(true){object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception e){steps.Add("FAIL "+e);break;}yield return next;}
        (run as IDisposable)?.Dispose();File.WriteAllLines(Output+"/locale-result.txt",steps);Running=false;
    }
    static IEnumerator LocaleUi()
    {
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();while(!GamePreferences.Ready||!Field<bool>(menu,"dataReady"))yield return null;
        string language=GamePreferences.Language;float scale=GamePreferences.TextScale;var service=OnlineMatchService.EnsureInstance();
        int oldSeat=service.LocalSeat;var oldLobby=service.LobbyState;OnlineLobbyPanel panel=null;
        try{
            Call(menu,"ShowDeckBuilder");var dropdown=Field<Dropdown>(menu,"deckDropdown");string original=dropdown.captionText.text;
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"})foreach(float size in new[]{1f,1.3f}){
                GamePreferences.SetLanguage(locale);GamePreferences.SetScale(size);yield return null;while(!GamePreferences.Ready||GamePreferences.Language!=locale)yield return null;
                GamePreferences.BindScene();yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
                var label=dropdown.captionText;
                Check(label.text==original&&label.cachedTextGenerator.vertexCount>0,locale+" "+size+" loaded font caption mesh; font="+label.font.name+" fitted="+label.resizeTextMaxSize);
                Click(dropdown.gameObject);yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
                Check(dropdown.transform.Find("Dropdown List").GetComponentsInChildren<Text>().Where(t=>t.name!="Checkmark").Any(t=>t.cachedTextGenerator.vertexCount>0),locale+" "+size+" loaded font popup mesh");
                dropdown.Hide();yield return new WaitForSecondsRealtime(.2f);
            }
            panel=OnlineLobbyPanel.Show(menu,Field<DeckDefinition>(menu,"currentDeck"));panel.enabled=false;
            var projection=new OnlineLobbyState{roomId="观战布局示例",hostSeat=3,config=new OnlineRoomConfig{startingLife=40,bestOf=3,formatId="none",useAnte=true,allowCustomContent=true},series=new OnlineSeriesState{bestOf=3}};
            for(int i=1;i<=15;i++)projection.players.Add(new OnlineLobbyPlayer{seat=i,connected=true,displayName=i<3?"玩家 "+i:"观战者 "+i+" · 较长的显示名称"});
            typeof(OnlineMatchService).GetProperty("LocalSeat").SetValue(service,3);typeof(OnlineMatchService).GetProperty("LobbyState").SetValue(service,projection);
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
                GamePreferences.SetLanguage(locale);GamePreferences.SetScale(1.3f);yield return null;while(!GamePreferences.Ready||GamePreferences.Language!=locale)yield return null;
                Call(panel,"Refresh");
                var browser=Field<CanvasGroup>(panel,"browserPageGroup");browser.gameObject.SetActive(false);
                var room=Field<CanvasGroup>(panel,"roomPageGroup");room.gameObject.SetActive(true);room.alpha=1;room.blocksRaycasts=true;room.interactable=true;
                GamePreferences.BindScene();yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
                var heading=Field<Text>(panel,"spectatorHeading");
                var body=Field<GameObject>(panel,"spectatorPrivacy").transform.Find("SpectatorPrivacyBody").GetComponent<Text>();
                var info=Field<Button>(panel,"readyButton").transform.parent.Find("SpectatorInfo").GetComponent<Text>();
                Check(heading.text==DeckCompanion.Text("观战席","Spectators","観戦席"),locale+" fully loaded spectator heading: "+heading.text);
                Check(body.preferredHeight<=body.rectTransform.rect.height+1&&info.preferredHeight<=info.rectTransform.rect.height+1,locale+" fully loaded spectator paragraphs fit");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/spectator-final-"+locale+".png"));yield return new WaitForEndOfFrame();
            }
            GamePreferences.SetLanguage("zh-CN");yield return null;while(!GamePreferences.Ready)yield return null;
            typeof(OnlineMatchService).GetProperty("LocalSeat").SetValue(service,oldSeat);typeof(OnlineMatchService).GetProperty("LobbyState").SetValue(service,oldLobby);
            Call(panel,"Refresh");panel.enabled=true;
            var rooms=Field<List<OnlineRoomListing>>(panel,"browserRooms");rooms.Clear();
            rooms.Add(new OnlineRoomListing{roomId="周末自由构筑 · 局域网房间",hostName="Player",source="局域网",lanHosted=true,startingLife=40,rulesVersion=1,format="none",bestOf=3,players=1,allowCustomContent=true,useAnte=true});
            rooms.Add(new OnlineRoomListing{roomId="服务器 · 标准练习",hostName="Player 2",source="服务器",startingLife=20,rulesVersion=1,format="standard",bestOf=1,players=2,inMatch=true,state="playing"});
            typeof(OnlineLobbyPanel).GetField("browsing",Hidden).SetValue(panel,false);
            typeof(OnlineLobbyPanel).GetField("browserGeneration",Hidden).SetValue(panel,Field<int>(panel,"browserGeneration")+1);
            Field<Button>(panel,"refreshRoomsButton").interactable=Field<Button>(panel,"createRoomButton").interactable=true;
            Field<Text>(panel,"browserStatus").text="刷新完成，选择房间加入。";
            Call(panel,"DrawBrowserRooms");yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            var rows=Field<RectTransform>(panel,"roomList");Check(rows.Find("Room_0/RoomTitleViewport").GetComponent<Image>()==null,"room headings remain labels without input-style boxes");
            Check(rows.GetComponentsInChildren<Text>().Where(t=>t.name=="RoomName").All(t=>t.cachedTextGenerator.vertexCount>0),"final room heading meshes visible");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/rooms-final.png"));yield return new WaitForEndOfFrame();
        }finally{
            typeof(OnlineMatchService).GetProperty("LocalSeat").SetValue(service,oldSeat);typeof(OnlineMatchService).GetProperty("LobbyState").SetValue(service,oldLobby);
            GamePreferences.SetLanguage(language);GamePreferences.SetScale(scale);if(panel!=null)Call(panel,"Close");Call(menu,"ShowStartScreenNow");
        }
    }
    static void Click(GameObject go)
    {
        var rect=(RectTransform)go.transform;var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        if(hits.Count==0||!hits[0].gameObject.transform.IsChildOf(go.transform))throw new Exception("Blocked ray: "+go.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    static Rect Bounds(RectTransform target,Transform space)
    {
        var corners=new Vector3[4];target.GetWorldCorners(corners);var p=corners.Select(space.InverseTransformPoint).ToArray();
        return Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));
    }
    static IEnumerator Exercise()
    {
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        while(!GamePreferences.Ready||!Field<bool>(menu,"dataReady"))yield return null;
        string language=GamePreferences.Language;float scale=GamePreferences.TextScale;
        var service=OnlineMatchService.EnsureInstance();if(service.IsOnline)throw new Exception("Requires no active user connection");
        OnlineLobbyPanel panel=null;const ushort port=18887;
        try{
            Call(menu,"ShowDeckBuilder");
            var dropdown=Field<Dropdown>(menu,"deckDropdown");string caption=dropdown.captionText.text;
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"})foreach(float textScale in new[]{1f,1.3f}){
                GamePreferences.SetLanguage(locale);GamePreferences.SetScale(textScale);yield return null;while(!GamePreferences.Ready||GamePreferences.Language!=locale)yield return null;
                GamePreferences.BindScene();yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
                var text=dropdown.captionText;Check(text.text==caption&&text.cachedTextGenerator.vertexCount>0,locale+" "+textScale+" deck caption renders instead of disappearing");
                Check(text.resizeTextMaxSize<=text.fontSize&&text.resizeTextMinSize==text.resizeTextMaxSize,locale+" "+textScale+" fits height without shrinking long names");
                Click(dropdown.gameObject);yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
                var popup=dropdown.transform.Find("Dropdown List");
                Check(popup!=null&&popup.GetComponentsInChildren<Text>().Where(t=>t.name!="Checkmark").Any(t=>t.cachedTextGenerator.vertexCount>0),locale+" "+textScale+" dropdown options render");
                dropdown.Hide();yield return new WaitForSecondsRealtime(.2f);
            }
            GamePreferences.SetLanguage("zh-CN");GamePreferences.SetScale(1.3f);yield return null;
            GamePreferences.BindScene();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/deck-after.png"));
            var config=new OnlineRoomConfig{startingLife=40,bestOf=3,formatId="modern",allowCustomContent=true,useAnte=true};
            bool started=service.StartLanHost(port,"Layout host","room-info-layout-1015","布局验证房间",config);Check(started,"real LAN host starts");if(!started)yield break;
            float deadline=Time.realtimeSinceStartup+20;while(!service.HasJoinedLobby&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(service.HasJoinedLobby,"real host joins configured room");if(!service.HasJoinedLobby)yield break;
            var query=OnlineRoomDiscovery.Query("127.0.0.1",port);while(!query.IsCompleted)yield return null;
            if(query.IsFaulted)throw query.Exception;var room=query.Result.rooms.Single();
            Check(room.startingLife==40&&!room.useAnte&&room.allowCustomContent&&room.bestOf==3&&room.format=="modern"&&room.rulesVersion>=3,"actual directory advertises Modern with ante disabled and full room rules");
            Check(room.players==1&&room.spectators==0&&room.lanHosted,"directory read leaves real seat count unchanged");
            File.WriteAllText(Output+"/directory.json",JsonUtility.ToJson(query.Result,true));
            var scan=OnlineRoomDiscovery.ScanLan();while(!scan.IsCompleted)yield return null;
            Check(!scan.IsFaulted&&scan.Result.Any(p=>p.Port==port),"UDP discovery locates metadata host");
            panel=OnlineLobbyPanel.Show(menu,Field<DeckDefinition>(menu,"currentDeck"));yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            string rules=Field<Text>(panel,"roomRulesText").text;
            Check(rules.Contains("40")&&!rules.Contains("赌注")&&rules.Contains("现行禁限牌表"),"room summary shows enforced rules and hides disabled ante");
            var profile=Field<InputField>(panel,"profileInput");
            profile.SetTextWithoutNotify(service.CurrentProfileId);
            var fixture=new DeckDefinition{name="房间校验",format="modern"};
            fixture.cards.Add(new DeckEntry{setCode="10E",collectorNumber="368",count=60});
            typeof(OnlineLobbyPanel).GetField("selectedDeck",Hidden).SetValue(panel,fixture);
            Call(panel,"Refresh");
            var readyButton=Field<Button>(panel,"readyButton");
            Click(readyButton.gameObject);
            deadline=Time.realtimeSinceStartup+5;while(!service.LobbyState.players.Any(p=>p.seat==service.LocalSeat&&p.ready)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(Field<Text>(panel,"deckNoticeTitle").text.Contains("已准备")&&Field<Text[]>(panel,"seatStates")[service.LocalSeat-1].text=="已准备","accepted submission is visible by the deck and on the player's seat");
            Click(readyButton.gameObject);
            deadline=Time.realtimeSinceStartup+5;while(service.LobbyState.players.Any(p=>p.seat==service.LocalSeat&&p.ready)&&Time.realtimeSinceStartup<deadline)yield return null;
            fixture.cards[0].count=59;
            Click(readyButton.gameObject);
            var reason=Field<Text>(panel,"deckNoticeReason");
            deadline=Time.realtimeSinceStartup+5;while(string.IsNullOrEmpty(reason.text)&&Time.realtimeSinceStartup<deadline)yield return null;
            var player=service.LobbyState.players.Single(p=>p.seat==service.LocalSeat);
            Check(reason.gameObject.activeInHierarchy&&reason.text.Contains("59")&&!Field<Text>(panel,"statusText").text.Contains("59"),"rejected deck shows its cause beside the selector");
            Check(!player.ready&&!player.hasDeck&&!Field<Button>(panel,"startMatchButton").interactable,"invalid replacement cannot ready the previously accepted deck");
            Click(Field<Button>(panel,"spectatorButton").gameObject);deadline=Time.realtimeSinceStartup+5;while(!service.IsSpectator&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(service.IsSpectator,"real spectator seat button retains action");
            var original=service.LobbyState;var projection=JsonUtility.FromJson<OnlineLobbyState>(JsonUtility.ToJson(original));
            for(int i=4;i<16;i++)projection.players.Add(new OnlineLobbyPlayer{seat=i,displayName="观战者 "+i+" · 较长的显示名称",connected=true});
            projection.players.Add(new OnlineLobbyPlayer{seat=16,displayName="已离开",connected=false});
            typeof(OnlineMatchService).GetProperty("LobbyState").SetValue(service,projection);Call(panel,"Refresh");
            yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            var summary=Field<Button>(panel,"leaveRoomButton").transform.parent;
            var count=Field<Text>(panel,"spectatorCount");
            var badge=(RectTransform)count.transform.parent;
            Check(!Bounds(badge,summary).Overlaps(Bounds((RectTransform)summary.Find("PlayersPanel"),summary))&&!Bounds(badge,summary).Overlaps(Bounds((RectTransform)summary.Find("StatusPanel"),summary)),"spectator count has separate space from players and status");
            Check(count.text=="13 人","counter includes connected spectators only");
            Check(badge.GetComponentsInChildren<Text>(true).Length==2&&badge.GetComponentInChildren<ScrollRect>(true)==null,"spectator count fits one row without a name list");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/spectator-after.png"));yield return new WaitForEndOfFrame();
            foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
                GamePreferences.SetLanguage(locale);yield return null;while(!GamePreferences.Ready||GamePreferences.Language!=locale)yield return null;Call(panel,"Refresh");GamePreferences.BindScene();yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
                var privacy=Field<GameObject>(panel,"spectatorPrivacy");var body=privacy.transform.Find("SpectatorPrivacyBody").GetComponent<Text>();
                var info=Field<Button>(panel,"readyButton").transform.parent.Find("SpectatorInfo").GetComponent<Text>();
                Check(body.preferredHeight<=body.rectTransform.rect.height+1&&info.preferredHeight<=info.rectTransform.rect.height+1,locale+" spectator explanation fits at large text size");
                var heading=Field<Text>(panel,"spectatorHeading");
                Check(heading.text==DeckCompanion.Text("观战席","Spectators","観戦席"),locale+" spectator labels refresh in place");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/spectator-"+locale+".png"));yield return new WaitForEndOfFrame();
            }
            GamePreferences.SetLanguage("zh-CN");while(!GamePreferences.Ready||GamePreferences.Language!="zh-CN")yield return null;
            typeof(OnlineMatchService).GetProperty("LobbyState").SetValue(service,original);
            Call(panel,"Refresh");yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            Click(Field<Button>(panel,"spectatorButton").gameObject);deadline=Time.realtimeSinceStartup+5;while(service.IsSpectator&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(!service.IsSpectator,"real return-to-player button retains action");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/player-room-after.png"));yield return new WaitForEndOfFrame();
            service.Disconnect();while(service.IsOnline||service.IsDisconnecting)yield return null;
            Call(panel,"Refresh");
            var rooms=Field<List<OnlineRoomListing>>(panel,"browserRooms");rooms.Clear();room.source="局域网";rooms.Add(room);
            rooms.Add(new OnlineRoomListing{roomId="服务器 · 标准练习",hostName="Server player",source="服务器",startingLife=20,rulesVersion=1,format="standard",bestOf=1,players=2,inMatch=true,state="playing"});
            rooms.Add(new OnlineRoomListing{roomId="旧版服务器",hostName="Old server",source="服务器",format="modern",bestOf=3});
            Call(panel,"DrawBrowserRooms");yield return null;yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
            var rows=Field<RectTransform>(panel,"roomList");var first=rows.Find("Room_0");
            Check(first.Find("Rules").GetComponent<Text>().text.Contains("40")&&first.Find("Rules").GetComponent<Text>().text.Contains("BO3")&&!first.Find("Permissions").GetComponent<Text>().text.Contains("赌注"),"LAN row hides inactive ante");
            Check(rows.Find("Room_1/Permissions").GetComponent<Text>().text.Contains("DIY 禁用")&&!rows.Find("Room_1/Permissions").GetComponent<Text>().text.Contains("赌注"),"server row hides inactive ante");
            Check(rows.Find("Room_2/Rules").GetComponent<Text>().text.Contains("未提供")&&!rows.Find("Room_2/Permissions").GetComponent<Text>().text.Contains("赌注"),"older directory omits unavailable ante");
            string joinCaption=rows.Find("Room_1/Join/Label").GetComponent<Text>().text;
            Check(joinCaption=="观战","active room keeps spectate entry: "+joinCaption);
            Check(!Bounds((RectTransform)first.Find("Rules"),first).Overlaps(Bounds((RectTransform)first.Find("Permissions"),first)),"rule and permission rows do not overlap");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/rooms-after.png"));yield return new WaitForEndOfFrame();
        }finally{
            if(service.IsOnline)service.Disconnect();GamePreferences.SetLanguage(language);GamePreferences.SetScale(scale);
            if(panel!=null)Call(panel,"Close");Call(menu,"ShowStartScreenNow");
        }
    }
}
#endif
