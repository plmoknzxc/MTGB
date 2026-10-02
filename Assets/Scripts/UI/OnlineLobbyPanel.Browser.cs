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
    private GameObject createChoices;
    private bool creatingRoom, serverRoom, browsing;
    private int browserGeneration;
    private readonly List<OnlineRoomListing> browserRooms=new List<OnlineRoomListing>();
    private Font browserFont;

    private void BuildRoomBrowser(Font font)
    {
        browserFont=GamePreferences.Font??font;
        var content=contentGroup.transform;
        content.Find("Subtitle").GetComponent<Text>().text="发现房间，或建立一场新的对局";
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
        browserEmpty.gameObject.AddComponent<PreferenceText>();
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
        createChoices=AddPanel(identity.transform,"CreateChoices",new Vector2(0,-240),new Vector2(304,128),FieldColor).gameObject;
        AddButton(createChoices.transform,"Lan","局域网",new Vector2(0,29),new Vector2(280,48),browserFont,PanelColor).onClick.AddListener(()=>BeginRoomCreation(false));
        AddButton(createChoices.transform,"Server","服务器",new Vector2(0,-29),new Vector2(280,48),browserFont,PanelColor).onClick.AddListener(()=>BeginRoomCreation(true));
        createChoices.SetActive(false);createRoomButton.onClick.AddListener(()=>createChoices.SetActive(!createChoices.activeSelf));

        connectionPageGroup.transform.Find("ConnectionCard").gameObject.SetActive(false);
        var setup=(RectTransform)connectionPageGroup.transform.Find("MatchSetupCard");Place(setup,new Vector2(0,-28),new Vector2(760,760));
        setup.Find("Kicker").gameObject.SetActive(false);Place((RectTransform)setup.Find("Heading"),new Vector2(0,327),new Vector2(648,44));
        setup.Find("Heading").GetComponent<Text>().text="建立房间";
        AddText(setup,"RoomNameLabel","房间名称",new Vector2(-238,257),new Vector2(172,28),17,browserFont,TextAnchor.MiddleLeft,MutedTextColor);
        MoveBrowserField(roomInput,setup,new Vector2(100,257),new Vector2(448,48));
        setup.Find("RulesHelp").gameObject.SetActive(false);Place((RectTransform)setup.Find("ConnectionStatusPanel"),new Vector2(0,-208),new Vector2(648,92));
        hostButton.transform.SetParent(setup,false);Place(hostButton.GetComponent<RectTransform>(),new Vector2(170,-319),new Vector2(308,52));
        AddButton(setup,"BackToBrowser","返回房间列表",new Vector2(-170,-319),new Vector2(308,52),browserFont,PanelColor).onClick.AddListener(BrowserBack);
        BindBrowserText(page.transform);
        DrawBrowserRooms();
    }
    private static void MoveBrowserField(InputField field,Transform parent,Vector2 position,Vector2 size)
    {
        field.transform.SetParent(parent,false);Place(field.GetComponent<RectTransform>(),position,size);
        foreach(var text in field.GetComponentsInChildren<Text>(true))Place(text.rectTransform,Vector2.zero,size-new Vector2(26,8));
    }
    private void SaveBrowserIdentity()
    {
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
        roomList.sizeDelta=new Vector2(1080,Mathf.Max(540,browserRooms.Count*100+20));
        if(browserRooms.Count==0)return;
        for(int i=0;i<browserRooms.Count;i++) {
            var room=browserRooms[i];var row=AddPanel(roomList,"Room_"+i,new Vector2(0,-58-i*100),new Vector2(1048,88),PanelColor);row.rectTransform.anchorMin=row.rectTransform.anchorMax=new Vector2(.5f,1);
            var badge=AddText(row.transform,"SourceBadge",room.source,new Vector2(290,0),new Vector2(100,34),16,browserFont,TextAnchor.MiddleCenter,room.lanHosted?new Color(.46f,.76f,.7f):new Color(.79f,.68f,.93f));
            AddText(row.transform,"DiyBadge",room.allowCustomContent?"DIY 允许":"DIY 禁用",new Vector2(150,18),new Vector2(150,30),15,browserFont,TextAnchor.MiddleCenter,MutedTextColor);
            var title=AddText(row.transform,"RoomName",room.roomId,new Vector2(-224,18),new Vector2(548,30),22,browserFont,TextAnchor.MiddleLeft,TextColor);title.supportRichText=false;
            var detail=AddText(row.transform,"Detail",$"{room.hostName} · {DeckFormatLegality.FormatName(room.format)} · BO{room.bestOf} · {room.players}/2 · {(room.inMatch?"对局中":"等待开局")} · 观战 {room.spectators}",new Vector2(-174,-20),new Vector2(648,28),16,browserFont,TextAnchor.MiddleLeft,MutedTextColor);detail.supportRichText=false;
            var join=AddButton(row.transform,"Join",room.inMatch||room.players>=2?"观战":"加入",new Vector2(423,0),new Vector2(150,46),browserFont,AccentColor);join.interactable=true;
            join.onClick.AddListener(()=>JoinListedRoom(room));
        }
        ApplyRoomPalette(roomList);
        BindBrowserText(roomList);
    }
    private static void BindBrowserText(Transform root)
    {
        foreach(var label in root.GetComponentsInChildren<Text>(true))
            (label.GetComponent<PreferenceText>()??label.gameObject.AddComponent<PreferenceText>()).Apply();
    }

    private async void BeginRoomCreation(bool server)
    {
        if(browsing||OnlineMatchService.Instance?.IsOnline==true)return;
        createChoices.SetActive(false);if(!BrowserPort(out var port))return;
        int generation=++browserGeneration;serverRoom=server;createRoomButton.interactable=false;
        if(server) {
            if(string.IsNullOrWhiteSpace(addressInput.text)){browserStatus.text="请先填写服务器地址。";createRoomButton.interactable=true;return;}
            browserStatus.text="正在检查服务器连接…";
            try {await OnlineRoomDiscovery.Query(addressInput.text.Trim(),port);}
            catch(Exception){if(this!=null&&generation==browserGeneration){browserStatus.text="无法连接服务器，请检查 IP、端口及服务器状态。";createRoomButton.interactable=true;}return;}
        }
        if(this==null||generation!=browserGeneration||!gameObject.activeInHierarchy)return;
        createRoomButton.interactable=true;creatingRoom=true;SaveBrowserIdentity();
        connectionStatusText.text=server?$"服务器 {addressInput.text.Trim()}:{port} 已连接，设置完成后建立房间。":"在本机建立房间，局域网内的玩家可刷新找到。";Refresh();
    }
    private void CreateSelectedRoom()
    {
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
        createChoices.SetActive(false);SaveBrowserIdentity();
        OnlineMatchService.EnsureInstance().Connect(room.address,(ushort)room.port,nameInput.text,profileInput.text,room.roomId,null,room.inMatch||room.players>=2?"spectate":"join");Refresh();
    }
    private void BrowserBack()
    {
        ++browserGeneration;browsing=false;
        if(creatingRoom){creatingRoom=false;if(OnlineMatchService.Instance?.IsOnline==true)OnlineMatchService.Instance.Disconnect();Refresh();return;}
        if(createChoices.activeSelf){createChoices.SetActive(false);return;}
        Close();
    }
}
