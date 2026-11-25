using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class OnlineLobbyPanel
{
    private CanvasGroup browserPageGroup;
    private RectTransform roomList;
    private Text browserStatus, browserCount, browserEmpty;
    private Button refreshRoomsButton, createRoomButton;
    private Text roomSetupHeading;
    private string freeStartingLife = "20";
    private Dropdown roomBracketDropdown;
    private Text roomBracketLabel;
    private Dropdown roomEventDropdown;
    private Text roomEventLabel;
    private Text roomEventHelp;
    private bool? fourSeatFormLayout;
    private bool SetupNoBanlist => cardFormatDropdown.value == DeckFormatLegality.Keys.Length;
    private string SetupCardFormat => SetupNoBanlist ? "commander" : DeckFormatLegality.Keys[cardFormatDropdown.value];
    private static readonly string[] EventKeys = { "", OnlineRoomConfig.Arkenstone, OnlineRoomConfig.Loki, OnlineRoomConfig.Nauctis };
    private static string EventName(string key) => key switch {
        OnlineRoomConfig.Arkenstone => DeckCompanion.Text("霍比特人 · 山中之心","The Hobbit · Heart of the Mountain","ホビット · 山の心"),
        OnlineRoomConfig.Nauctis => DeckCompanion.Text("纳克提斯 · 雄伟大洋（节选）","Nauctis · Grand Marine (excerpt)","ノークティス · 雄大なる海（抜粋）"),
        OnlineRoomConfig.Loki => DeckCompanion.Text("漫威 · 诡计之神","Marvel · God of Mischief","マーベル · いたずらの神"),
        _ => DeckCompanion.Text("普通对局","Regular game","通常対戦")
    };
    private GameObject createChoices;
    private bool creatingRoom, serverRoom, browsing;
    private int browserGeneration;
    private readonly List<OnlineRoomListing> browserRooms=new List<OnlineRoomListing>();
    private Font browserFont;

    private void BuildRoomBrowser(Font font)
    {
        browserFont=GamePreferences.Font??font;
        var content=contentGroup.transform;
        var page=new GameObject("RoomBrowserPage",typeof(RectTransform),typeof(CanvasGroup));page.transform.SetParent(content,false);Stretch(page.GetComponent<RectTransform>());browserPageGroup=page.GetComponent<CanvasGroup>();
        var list=AddPanel(page.transform,"RoomBrowserCard",new Vector2(-200,-28),new Vector2(1160,760),PanelColor);
        AddText(list.transform,"Heading","可加入的房间",new Vector2(-270,326),new Vector2(520,44),28,browserFont,TextAnchor.MiddleLeft,TextColor);
        browserCount=AddText(list.transform,"Count","局域网与服务器",new Vector2(-270,283),new Vector2(520,28),16,browserFont,TextAnchor.MiddleLeft,MutedTextColor);
        refreshRoomsButton=AddButton(list.transform,"RefreshRooms","刷新",new Vector2(463,321),new Vector2(150,46),browserFont,AccentColor);refreshRoomsButton.onClick.AddListener(RefreshBrowser);
        var viewport=AddPanel(list.transform,"RoomListViewport",new Vector2(0,-20),new Vector2(1080,540),FieldColor);viewport.gameObject.AddComponent<RectMask2D>();
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=38;scroll.viewport=viewport.rectTransform;
        roomList=new GameObject("Rooms",typeof(RectTransform)).GetComponent<RectTransform>();roomList.SetParent(viewport.transform,false);roomList.anchorMin=roomList.anchorMax=roomList.pivot=new Vector2(.5f,1);roomList.sizeDelta=new Vector2(1080,540);scroll.content=roomList;
        browserEmpty=AddText(viewport.transform,"Empty","暂无房间",Vector2.zero,new Vector2(700,60),24,browserFont,TextAnchor.MiddleCenter,MutedTextColor);
        browserEmpty.raycastTarget=false;
        browserStatus=AddText(list.transform,"BrowserStatus","点击刷新，查找可用房间。",new Vector2(0,-331),new Vector2(1080,52),16,browserFont,TextAnchor.MiddleLeft,MutedTextColor);
        var identity=AddPanel(page.transform,"BrowserIdentity",new Vector2(600,-28),new Vector2(360,760),PanelColor);
        AddText(identity.transform,"Heading","你的联机身份",new Vector2(0,326),new Vector2(304,40),24,browserFont,TextAnchor.MiddleLeft,TextColor);
        AddText(identity.transform,"NameLabel","游戏内名称",new Vector2(0,265),new Vector2(304,28),16,browserFont,TextAnchor.MiddleLeft,MutedTextColor);
        MoveBrowserField(nameInput,identity.transform,new Vector2(0,221),new Vector2(304,48));nameInput.characterLimit=24;nameInput.onEndEdit.AddListener(_=>SaveBrowserIdentity());
        AddText(identity.transform,"ServerLabel","服务器 IP / 地址（可选）",new Vector2(0,141),new Vector2(304,28),16,browserFont,TextAnchor.MiddleLeft,MutedTextColor);
        MoveBrowserField(addressInput,identity.transform,new Vector2(0,98),new Vector2(304,48));
        AddText(identity.transform,"PortLabel","端口",new Vector2(-82,36),new Vector2(140,28),16,browserFont,TextAnchor.MiddleLeft,MutedTextColor);
        MoveBrowserField(portInput,identity.transform,new Vector2(70,36),new Vector2(164,46));
        AddText(identity.transform,"Help","刷新会查找局域网房间。\n填写服务器地址后，也会查询该服务器。",new Vector2(0,-43),new Vector2(304,86),17,browserFont,TextAnchor.UpperLeft,MutedTextColor);
        createRoomButton=AddButton(identity.transform,"CreateRoom","建立房间 ▾",new Vector2(0,-143),new Vector2(304,52),browserFont,AccentColor);
        createChoices=AddPanel(identity.transform,"CreateChoices",new Vector2(0,-207),new Vector2(304,62),FieldColor).gameObject;
        AddButton(createChoices.transform,"Lan","局域网",new Vector2(-74,0),new Vector2(140,46),browserFont,PanelColor).onClick.AddListener(()=>BeginRoomCreation(false));
        AddButton(createChoices.transform,"Server","服务器",new Vector2(74,0),new Vector2(140,46),browserFont,PanelColor).onClick.AddListener(()=>BeginRoomCreation(true));
        createChoices.SetActive(false);createRoomButton.onClick.AddListener(()=>createChoices.SetActive(!createChoices.activeSelf));

        connectionPageGroup.transform.Find("ConnectionCard").gameObject.SetActive(false);
        BuildCreationForm((RectTransform)connectionPageGroup.transform.Find("MatchSetupCard"));
        BindBrowserText(page.transform);
        DrawBrowserRooms();
    }

    private void BuildCreationForm(RectTransform setup)
    {
        setup.Find("Kicker").gameObject.SetActive(false);
        roomSetupHeading=setup.Find("Heading").GetComponent<Text>();
        AddText(setup,"RoomNameLabel","房间名称",Vector2.zero,new Vector2(580,26),18,browserFont,TextAnchor.MiddleLeft,MutedTextColor);
        MoveBrowserField(roomInput,setup,Vector2.zero,new Vector2(580,50));
        roomBracketLabel=AddText(setup,"CommanderBracketLabel",string.Empty,Vector2.zero,new Vector2(278,26),18,browserFont,TextAnchor.MiddleLeft,MutedTextColor);
        roomBracketLabel.GetComponent<PreferenceText>().Literal=true;
        roomBracketDropdown=AddDropdown(setup,"CommanderBracketDropdown",Vector2.zero,new Vector2(278,50),browserFont,DeckCommander.BracketOptions());
        roomEventLabel=AddText(setup,"CommanderEventLabel","特殊模式",Vector2.zero,new Vector2(580,26),18,browserFont,TextAnchor.MiddleLeft,MutedTextColor);
        roomEventDropdown=AddDropdown(setup,"CommanderEventDropdown",Vector2.zero,new Vector2(580,50),browserFont,EventKeys.Select(EventName).ToList());
        roomEventDropdown.onValueChanged.AddListener(_=>RefreshFormatRules());
        Place(connectionStatusText.rectTransform,Vector2.zero,new Vector2(532,56));
        setup.Find("RulesHelp").gameObject.SetActive(false);
        setup.Find("Divider").gameObject.SetActive(false);

        var rules=AddPanel(setup.parent,"RulesCard",new Vector2(348,-28),new Vector2(864,760),PanelColor).transform;
        roomEventHelp=AddText(rules,"CommanderEventRules","",new Vector2(0,-225),new Vector2(780,65),19,browserFont,TextAnchor.MiddleLeft,TextColor);
        roomEventHelp.GetComponent<PreferenceText>().Literal=true;
        AddText(rules,"Heading","对局规则",new Vector2(0,323),new Vector2(780,56),30,browserFont,TextAnchor.MiddleLeft,TextColor);
        var toggles=new[]{customContentToggle,noShuffleToggle,anteToggle};
        for(int i=0;i<toggles.Length;i++)
        {
            var row=toggles[i].transform;
            row.SetParent(rules,false);
            PlaceFromTop((RectTransform)row,0,92+i*98,780,90);
            Place((RectTransform)row.Find("Label"),new Vector2(-39,20),new Vector2(550,32));
            Place((RectTransform)row.Find("Description"),new Vector2(22,-17),new Vector2(672,44));
            Place((RectTransform)row.Find("State"),new Vector2(285,20),new Vector2(154,32));
        }
        hostButton.transform.SetParent(rules,false);
        Place((RectTransform)hostButton.transform,new Vector2(203,-315),new Vector2(372,58));
        AddButton(rules,"BackToBrowser","返回房间列表",new Vector2(-203,-315),new Vector2(372,58),browserFont,PanelColor).onClick.AddListener(BrowserBack);
        LayoutCreationForm(false);
    }

    private void LayoutCreationForm(bool fourSeats)
    {
        if(fourSeatFormLayout==fourSeats)return;
        fourSeatFormLayout=fourSeats;
        var setup=(RectTransform)roomSetupHeading.transform.parent;
        var rules=(RectTransform)hostButton.transform.parent;
        const float firstRow=94, rowHeight=96, labelHeight=26, fieldHeight=50, labelGap=4;
        float noticeTop=firstRow+(fourSeats?4:3)*rowHeight;
        float height=noticeTop+70+24;
        Place(setup,new Vector2(-448,352-height/2),new Vector2(664,height));
        Place(rules,new Vector2(348,352-height/2),new Vector2(864,height));
        PlaceFromTop(roomSetupHeading.rectTransform,0,20,580,52);
        PlaceFromTop((RectTransform)rules.Find("Heading"),0,20,780,52);
        void Field(Transform label,Transform control,int index,float x,float width)
        {
            float top=firstRow+index*rowHeight;
            PlaceFromTop((RectTransform)label,x,top,width,labelHeight);
            PlaceFromTop((RectTransform)control,x,top+labelHeight+labelGap,width,fieldHeight);
        }
        Field(setup.Find("RoomNameLabel"),roomInput.transform,0,0,580);
        Field(setup.Find("SetsLabel"),cardFormatDropdown.transform,1,fourSeats?-151:0,fourSeats?278:580);
        Field(roomBracketLabel.transform,roomBracketDropdown.transform,1,151,278);
        Field(setup.Find("LifeLabel"),lifeInput.transform,2,-151,278);
        Field(setup.Find("FormatLabel"),formatDropdown.transform,2,151,278);
        Field(roomEventLabel.transform,roomEventDropdown.transform,3,0,580);
        PlaceFromTop((RectTransform)connectionStatusText.transform.parent,0,noticeTop,580,70);
        PlaceFromTop(roomEventHelp.rectTransform,0,288,780,74);
        PlaceFromTop((RectTransform)hostButton.transform,203,height-76,372,52);
        PlaceFromTop((RectTransform)rules.Find("BackToBrowser"),-203,height-76,372,52);
    }
    private static void MoveBrowserField(InputField field,Transform parent,Vector2 position,Vector2 size)
    {
        field.transform.SetParent(parent,false);Place(field.GetComponent<RectTransform>(),position,size);
        foreach(var text in field.GetComponentsInChildren<Text>(true))Place(text.rectTransform,Vector2.zero,size-new Vector2(26,8));
    }
    private void SaveBrowserIdentity()
    {
        if(!ValidatePlayerName())return;
        var profile=OnlineClientProfileStore.Load(profileInput.text);
        OnlineClientProfileStore.SaveConnection(profileInput.text,addressInput.text,ushort.TryParse(portInput.text,out var port)&&port>0?port:profile.port,nameInput.text,profile.roomId);
    }
    private bool BrowserPort(out ushort port)
    {
        if(ushort.TryParse(portInput.text,out port)&&port>0)return true;
        browserStatus.text="请输入 1–65535 之间的端口。";return false;
    }
    private async void RefreshBrowser()
    {
        if(browsing||creatingRoom||OnlineMatchService.Instance?.IsOnline==true)return;
        int generation=++browserGeneration;browsing=true;refreshRoomsButton.interactable=false;createRoomButton.interactable=false;
        browserRooms.Clear();DrawBrowserRooms();browserStatus.text=string.Empty;
        string address=addressInput.text.Trim();int port=ushort.TryParse(portInput.text,out var parsed)?parsed:0;
        var errors=new List<string>();
        async Task Fetch(string host,int number,string source) {
            try {
                var directory=await OnlineRoomDiscovery.Query(host,number);
                if(this==null||generation!=browserGeneration||!gameObject.activeInHierarchy)return;
                foreach(var room in directory.rooms){
                    room.address=host;room.port=number;room.source=room.lanHosted?"局域网":"服务器";room.serverId=directory.instanceId;
                    var existing=browserRooms.FirstOrDefault(r=>r.roomId==room.roomId&&(!string.IsNullOrEmpty(room.serverId)?r.serverId==room.serverId:r.address==host&&r.port==number));
                    if(existing==null)browserRooms.Add(room);
                    
                }
            }catch(Exception){if(source=="服务器")errors.Add("服务器无法连接，请检查地址、端口及服务器状态。");}
        }
        async Task Lan() {
            try {var peers=await OnlineRoomDiscovery.ScanLan();await Task.WhenAll(peers.Select(p=>Fetch(p.Address.ToString(),p.Port,"局域网")));}
            catch(Exception){errors.Add("局域网搜索失败，请检查网络连接。");}
        }
        var tasks=new List<Task>{Lan()};
        if(address.Length>0){if(port>0)tasks.Add(Fetch(address,port,"服务器"));else errors.Add("服务器端口无效。");}
        await Task.WhenAll(tasks);
        if(this==null||generation!=browserGeneration)return;
        browsing=false;refreshRoomsButton.interactable=true;createRoomButton.interactable=true;DrawBrowserRooms();
        browserStatus.text=errors.Count>0?string.Join("\n",errors):browserRooms.Count==0?"没有找到房间。可以建立房间，或填写服务器地址后刷新。":"刷新完成，选择房间加入。";
    }
    private void DrawBrowserRooms()
    {
        foreach(Transform child in roomList){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        browserCount.text=browsing?"搜索中 · 局域网与服务器":$"{browserRooms.Count} 个房间 · 局域网与服务器";
        roomList.anchoredPosition=Vector2.zero;
        browserEmpty.gameObject.SetActive(browserRooms.Count==0);
        browserEmpty.text=browsing?"正在搜索房间…":"暂无房间";
        browserEmpty.GetComponent<PreferenceText>().Apply();
        roomList.sizeDelta=new Vector2(1080,Mathf.Max(540,browserRooms.Count*166+20));
        if(browserRooms.Count==0)return;
        for(int i=0;i<browserRooms.Count;i++) {
            var room=browserRooms[i];var row=AddPanel(roomList,"Room_"+i,new Vector2(0,-87-i*166),new Vector2(1048,154),PanelColor);row.rectTransform.anchorMin=row.rectTransform.anchorMax=new Vector2(.5f,1);
            var accent=room.lanHosted?MenuVisualTheme.LibraryAccent:MenuVisualTheme.SideAccent;
            StyleRoomInset(row,MenuVisualTheme.ArchivePanel);
            var stripe=MenuVisualTheme.Detail(row.transform,"SourceRule",accent).rectTransform;
            stripe.anchorMin=Vector2.zero;stripe.anchorMax=Vector2.up;
            stripe.offsetMin=new Vector2(0,10);stripe.offsetMax=new Vector2(3,-10);
            AddText(row.transform,"SourceBadge",room.source,new Vector2(420,43),new Vector2(156,32),16,browserFont,TextAnchor.MiddleCenter,accent);
            var titleViewport=new GameObject("RoomTitleViewport",typeof(RectTransform),typeof(RectMask2D)).transform;titleViewport.SetParent(row.transform,false);Place((RectTransform)titleViewport,new Vector2(-100,52),new Vector2(792,40));
            var title=AddText(titleViewport.transform,"RoomName",room.roomId,Vector2.zero,new Vector2(792,40),20,browserFont,TextAnchor.MiddleLeft,TextColor);title.supportRichText=false;title.horizontalOverflow=HorizontalWrapMode.Overflow;
            var titleBinding=title.GetComponent<PreferenceText>();titleBinding.Literal=true;titleBinding.FitSingleLineHeight=true;
            MenuTypography.Use(title,MenuTypography.Face.Heading);
            string life=room.startingLife>0?StartingLifeLabel(room.commanderEvent,room.startingLife):DeckCompanion.Text("生命未提供","Life unavailable","ライフ情報なし");
            string format=RoomFormatWithBracket(room.format,room.commanderBracket,room.noCommanderBanlist);
            if(!string.IsNullOrEmpty(room.commanderEvent))format+=" · "+EventName(room.commanderEvent);
            string shuffle=room.rulesVersion>=2?ShuffleRuleLabel(room.noShuffle):DeckCompanion.Text("洗切设置未提供","Shuffle setting unavailable","シャッフル設定不明");
            AddText(row.transform,"Rules",life+" · "+format+" · BO"+room.bestOf+" · "+shuffle,new Vector2(-100,13),new Vector2(792,30),16,browserFont,TextAnchor.MiddleLeft,TextColor);
            string diy=DeckCompanion.Text("DIY ","DIY ","DIY ")+PermissionLabel(room.allowCustomContent);
            string seats=DeckCompanion.Text("玩家 ","Players ","プレイヤー ")+room.players+"/"+room.PlayerCapacity+" · "+DeckCompanion.Text("观战 ","Spectators ","観戦 ")+room.spectators;
            if(room.aiPlayers>0)seats+=" · AI "+room.aiPlayers;
            AddText(row.transform,"Permissions",diy+AnteRuleSuffix(room.rulesVersion>0 && room.useAnte)+" · "+seats,new Vector2(-100,-20),new Vector2(792,30),15,browserFont,TextAnchor.MiddleLeft,MutedTextColor);
            string roomState=room.state=="sideboarding"?"局间换备":room.state=="review"?"已结束 · 查看战场":room.inMatch?"对局中":"等待开局";
            var detail=AddText(row.transform,"Detail",room.hostName+" · "+GamePreferences.T(roomState),new Vector2(-100,-53),new Vector2(792,28),14,browserFont,TextAnchor.MiddleLeft,MutedTextColor);detail.supportRichText=false;
            detail.GetComponent<PreferenceText>().Literal=true;
            bool resuming=HasRoomCredential(room);
            var join=AddButton(row.transform,"Join",resuming?DeckCompanion.Text("返回对局","Rejoin","対戦に戻る"):room.inMatch||room.players>=room.PlayerCapacity?"观战":"加入",new Vector2(420,-16),new Vector2(156,48),browserFont,AccentColor);join.interactable=true;
            MenuVisualTheme.StyleDeckTool(join,resuming||(!room.inMatch&&room.players<room.PlayerCapacity));
            join.onClick.AddListener(()=>JoinListedRoom(room));
        }
        BindBrowserText(roomList);
    }
    private static string PermissionLabel(bool allowed)=>allowed?DeckCompanion.Text("允许","allowed","有効"):DeckCompanion.Text("禁用","disabled","無効");
    private static string ShuffleRuleLabel(bool enabled)=>DeckCompanion.Text("不洗切：","No shuffle: ","シャッフルなし：")+(enabled?DeckCompanion.Text("开","On","有効"):DeckCompanion.Text("关","Off","無効"));
    private void UpdateNoShuffleLabel()
    {
        if(noShuffleStateText==null||noShuffleToggle==null)return;
        void Set(string path,string value){
            var label=noShuffleToggle.transform.Find(path).GetComponent<Text>();
            (label.GetComponent<PreferenceText>()??label.gameObject.AddComponent<PreferenceText>()).Literal=true;
            label.text=value;
        }
        Set("Label",DeckCompanion.Text("不洗切卡组","Do not shuffle decks","デッキをシャッフルしない"));
        Set("Description",DeckCompanion.Text("按编辑器保存顺序抓牌；开局、调度和卡牌效果均不打乱牌库。","Draw in saved order. Never shuffle, including mulligans and card effects.","保存順で引きます。開始時・マリガン・カード効果でも混ぜません。"));
        (noShuffleStateText.GetComponent<PreferenceText>()??noShuffleStateText.gameObject.AddComponent<PreferenceText>()).Literal=true;
        noShuffleStateText.text=noShuffleToggle.isOn?DeckCompanion.Text("已开启","On","有効"):DeckCompanion.Text("已关闭","Off","無効");
        noShuffleStateText.color=noShuffleToggle.isOn?MenuVisualTheme.LibraryAccent:MenuVisualTheme.Stone;
    }
    private static void BindBrowserText(Transform root)
    {
        foreach(var label in root.GetComponentsInChildren<Text>(true))
            (label.GetComponent<PreferenceText>()??label.gameObject.AddComponent<PreferenceText>()).Apply();
    }

    private async void BeginRoomCreation(bool server)
    {
        if(browsing||OnlineMatchService.Instance?.IsOnline==true)return;
        if(!ValidatePlayerName())return;
        createChoices.SetActive(false);if(!BrowserPort(out var port))return;
        int generation=++browserGeneration;serverRoom=server;createRoomButton.interactable=false;
        if(server) {
            if(string.IsNullOrWhiteSpace(addressInput.text)){browserStatus.text="请先填写服务器地址。";createRoomButton.interactable=true;return;}
            browserStatus.text="正在检查服务器连接…";
            try {await OnlineRoomDiscovery.Query(addressInput.text.Trim(),port);}
            catch(Exception){if(this!=null&&generation==browserGeneration){browserStatus.text="无法连接服务器，请检查 IP、端口及服务器状态。";createRoomButton.interactable=true;}return;}
        }
        if(this==null||generation!=browserGeneration||!gameObject.activeInHierarchy)return;
        createRoomButton.interactable=true;creatingRoom=true;
        customContentToggle.SetIsOnWithoutNotify(false);
        anteToggle.SetIsOnWithoutNotify(false);
        roomBracketDropdown.SetValueWithoutNotify(0);
        roomEventDropdown.SetValueWithoutNotify(0);
        cardFormatDropdown.RefreshShownValue();
        SaveBrowserIdentity();
        connectionStatusText.text=server?$"服务器 {addressInput.text.Trim()}:{port} 可访问，设置完成后建立房间。":"在本机建立房间，局域网内的玩家可刷新找到。";Refresh();
    }
    private void CreateSelectedRoom()
    {
        if(!ValidatePlayerName())return;
        if(!BrowserPort(out var port))return;
        if(string.IsNullOrWhiteSpace(roomInput.text)){connectionStatusText.text="请输入房间名称。";return;}
        SaveBrowserIdentity();
        if(serverRoom)OnlineMatchService.EnsureInstance().Connect(addressInput.text,port,nameInput.text,profileInput.text,roomInput.text,ReadRoomConfig(),"create");
        else Host();
        Refresh();
    }
    private void JoinListedRoom(OnlineRoomListing room)
    {
        if(OnlineMatchService.Instance?.IsOnline==true)return;
        if(!ValidatePlayerName())return;
        createChoices.SetActive(false);SaveBrowserIdentity();
        OnlineMatchService.EnsureInstance().Connect(room.address,(ushort)room.port,nameInput.text,profileInput.text,room.roomId,null,HasRoomCredential(room)?"join":room.inMatch||room.players>=room.PlayerCapacity?"spectate":"join");Refresh();
    }
    private bool HasRoomCredential(OnlineRoomListing room)
        => !string.IsNullOrEmpty(OnlineClientProfileStore.GetReconnectToken(profileInput.text,room.address,(ushort)room.port,room.roomId));
    private void BrowserBack()
    {
        if(battlefieldLobbyCanvas!=null){
            ViewSideboardBattlefield();return;
        }
        ++browserGeneration;browsing=false;
        if(creatingRoom){if(OnlineMatchService.Instance!=null&&!OnlineMatchService.Instance.TryLeaveRoom())return;creatingRoom=false;Refresh();return;}
        if(createChoices.activeSelf){createChoices.SetActive(false);return;}
        Close();
    }
}
