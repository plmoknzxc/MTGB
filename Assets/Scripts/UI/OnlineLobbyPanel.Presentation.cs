using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class OnlineLobbyPanel
{
    private Button spectatorButton;
    private Text spectatorsText,spectatorCount;
    private GameObject spectatorPrivacy;
    private static readonly Color LobbySurface = new Color(.13f,.14f,.17f);
    private static readonly Color LobbyField = new Color(.085f,.095f,.12f);
    private static readonly Color LobbyGold = new Color(.74f,.59f,.36f);
    private static readonly Color LobbyInk = new Color(.10f,.085f,.065f);
    private static readonly Color LobbyPaper = new Color(.94f,.91f,.85f);

    private void BuildSpectatorControls()
    {
        var summary=leaveRoomButton.transform.parent;
        spectatorButton=AddButton(summary,"SpectatorSeat","前往观战席",new Vector2(170,-338),new Vector2(308,50),browserFont,LobbySurface);
        spectatorButton.onClick.AddListener(()=> {
            var service=OnlineMatchService.Instance;
            if(service!=null)service.SetSpectator(!service.IsSpectator);
        });
        Place((RectTransform)leaveRoomButton.transform,new Vector2(-170,-338),new Vector2(308,50));
        Place((RectTransform)summary.Find("PlayersLabel"),new Vector2(0,78),new Vector2(648,36));
        var players=summary.Find("PlayersPanel");Place((RectTransform)players,new Vector2(0,-36),new Vector2(648,168));
        for(int i=0;i<2;i++){
            var seat=players.Find("Seat"+(i+1));Place((RectTransform)seat,new Vector2(0,i==0?46:-46),new Vector2(648,72));
            Place(seatNames[i].rectTransform,new Vector2(18,16),new Vector2(500,36));PreferenceText.SetSize(seatNames[i],19);
            Place(seatStates[i].rectTransform,new Vector2(18,-20),new Vector2(500,26));PreferenceText.SetSize(seatStates[i],14);
        }
        var observers=AddPanel(summary,"SpectatorList",new Vector2(0,-198),new Vector2(648,108),LobbyField);
        AddText(observers.transform,"SpectatorHeading",DeckCompanion.Text("观战席","Spectators","観戦席"),new Vector2(-160,36),new Vector2(284,34),18,browserFont,TextAnchor.MiddleLeft,LobbyPaper);
        spectatorCount=AddText(observers.transform,"SpectatorCount","0",new Vector2(234,36),new Vector2(136,34),16,browserFont,TextAnchor.MiddleRight,LobbyGold);
        var viewport=AddPanel(observers.transform,"SpectatorViewport",new Vector2(0,-19),new Vector2(604,60),Color.clear);
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.viewport=viewport.rectTransform;scroll.scrollSensitivity=28;
        spectatorsText=AddText(viewport.transform,"Spectators",DeckCompanion.Text("暂无观战者","No spectators yet","観戦者はいません"),Vector2.zero,new Vector2(604,60),14,browserFont,TextAnchor.UpperLeft,MutedTextColor);
        spectatorsText.supportRichText=false;spectatorsText.raycastTarget=false;
        var namesRect=spectatorsText.rectTransform;namesRect.anchorMin=new Vector2(0,1);namesRect.anchorMax=new Vector2(1,1);namesRect.pivot=new Vector2(.5f,1);namesRect.anchoredPosition=Vector2.zero;namesRect.sizeDelta=new Vector2(0,60);
        var literal=spectatorsText.gameObject.AddComponent<PreferenceText>();literal.Literal=true;literal.SetBaseSize(14);
        spectatorsText.gameObject.AddComponent<LayoutElement>().minHeight=60;
        spectatorsText.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=namesRect;
        var status=summary.Find("StatusPanel");Place((RectTransform)status,new Vector2(0,-282),new Vector2(648,34));
        Place(statusText.rectTransform,Vector2.zero,new Vector2(648,34));statusText.resizeTextForBestFit=true;statusText.resizeTextMinSize=12;statusText.resizeTextMaxSize=Mathf.RoundToInt(17*GamePreferences.TextScale);
        var prep=readyButton.transform.parent;
        AddText(prep,"SpectatorInfo",DeckCompanion.Text("双方开局后，自动进入战场","The battlefield opens when the match starts","対戦開始時に戦場へ自動移動します"),new Vector2(0,168),new Vector2(604,92),24,browserFont,TextAnchor.MiddleCenter,LobbyPaper).gameObject.SetActive(false);
        spectatorPrivacy=AddPanel(prep,"SpectatorPrivacy",new Vector2(0,-3),new Vector2(648,190),LobbyField).gameObject;
        AddText(spectatorPrivacy.transform,"SpectatorPrivacyTitle",DeckCompanion.Text("公开观战","Public view","公開情報の観戦"),new Vector2(0,52),new Vector2(584,34),20,browserFont,TextAnchor.MiddleLeft,LobbyGold);
        AddText(spectatorPrivacy.transform,"SpectatorPrivacyBody",DeckCompanion.Text("可查看战场、堆叠及公开区域。\n双方手牌和私有选择会保持隐藏。","See the battlefield, stack and public zones.\nHands and private choices stay hidden.","戦場、スタック、公開領域を確認できます。\n手札と非公開の選択は表示されません。"),new Vector2(0,-24),new Vector2(584,104),18,browserFont,TextAnchor.MiddleLeft,MutedTextColor);
        spectatorPrivacy.SetActive(false);
        ApplyRoomPalette(transform);
    }

    private void RefreshSpectatorControls(OnlineMatchService service)
    {
        if(spectatorButton==null)return;
        bool spectator=service?.IsSpectator==true;
        var summary=leaveRoomButton.transform.parent;
        summary.Find("SpectatorList/SpectatorHeading").GetComponent<Text>().text=DeckCompanion.Text("观战席","Spectators","観戦席");
        readyButton.transform.parent.Find("SpectatorInfo").GetComponent<Text>().text=DeckCompanion.Text("双方开局后，自动进入战场","The battlefield opens when the match starts","対戦開始時に戦場へ自動移動します");
        spectatorPrivacy.transform.Find("SpectatorPrivacyTitle").GetComponent<Text>().text=DeckCompanion.Text("公开观战","Public view","公開情報の観戦");
        spectatorPrivacy.transform.Find("SpectatorPrivacyBody").GetComponent<Text>().text=DeckCompanion.Text("可查看战场、堆叠及公开区域。\n双方手牌和私有选择会保持隐藏。","See the battlefield, stack and public zones.\nHands and private choices stay hidden.","戦場、スタック、公開領域を確認できます。\n手札と非公開の選択は表示されません。");
        spectatorButton.interactable=service?.HasJoinedLobby==true && service.LobbyState?.series?.betweenGames!=true;
        SetButtonLabel(spectatorButton,spectator?"加入玩家席":"前往观战席");
        var names=service?.LobbyState?.players?.Where(p=>p.seat>2).Select(p=>p.displayName).ToArray()??new string[0];
        spectatorCount.text=names.Length+DeckCompanion.Text(" 人"," viewers"," 人");
        spectatorsText.text=names.Length==0?DeckCompanion.Text("暂无观战者","No spectators yet","観戦者はいません"):string.Join("\n",names);
        spectatorPrivacy.SetActive(spectator);
        deckDropdown.interactable=!spectator&&!ready;
        var prep=readyButton.transform.parent;
        prep.Find("Heading").GetComponent<Text>().text=spectator?"观战席":"选择出战牌组";
        prep.Find("ReadyHelp").GetComponent<Text>().text=spectator?DeckCompanion.Text("可在左侧切换席位或离开房间。","Change seats or leave using the left panel.","左側で席の変更または退出ができます。"):"需要更换牌组时，请先取消准备。";
        foreach(string name in new[]{"DeckLabel","DeckDropdown","SelectedDeckSummary","PreparationInfo","ReadyButton"})prep.Find(name).gameObject.SetActive(!spectator);
        prep.Find("SpectatorInfo").gameObject.SetActive(spectator);
        prep.Find("Kicker").GetComponent<Text>().text=spectator?"观战":"准备";
        Place(startMatchButton.GetComponent<RectTransform>(),new Vector2(spectator?0:170,-260),new Vector2(spectator?648:308,58));
        foreach(var button in new[]{readyButton,startMatchButton,sideboardReadyButton,sideboardStartButton})
            button.GetComponentInChildren<Text>(true).color=button.interactable?LobbyInk:new Color(.68f,.69f,.73f);
    }

    private static void ApplyRoomPalette(Transform root)
    {
        foreach(var image in root.GetComponentsInChildren<Image>(true))
        {
            if(image.sprite!=null)continue;
            string name=image.name;
            bool plain=name=="PlayersPanel"||name=="SpectatorViewport"||name=="StatusPanel";
            image.color=name=="HeaderBand"||plain?Color.clear:
                name=="SpectatorList"||name=="PreparationInfo"?LobbyField:
                name.Contains("Input")||name.Contains("Dropdown")||name=="RoomListViewport"||name.StartsWith("Seat")||name=="SelectedDeckSummary"?LobbyField:LobbySurface;
            if(image.transform==root)image.color=PageColor;
            var edge=image.GetComponent<Outline>();
            if(edge!=null){edge.effectColor=new Color(.30f,.29f,.30f,.65f);if(plain||name=="SpectatorList")edge.enabled=false;}
            if(name=="Background" && image.GetComponentInParent<Toggle>()!=null)
            {
                image.color=new Color(.10f,.16f,.23f);
                ObsidianArenaTheme.EnsureOutline(image,new Color(.65f,.70f,.78f),new Vector2(2,-2));
            }
            if(name=="ConnectionStatusPanel")
            {
                image.color=LobbyField;
                ObsidianArenaTheme.EnsureOutline(image,new Color(.43f,.48f,.57f),new Vector2(1,-1));
            }
        }
        foreach(var text in root.GetComponentsInChildren<Text>(true))
        {
            if(text.name=="SourceBadge")continue;
            text.color=text.name.Contains("Heading")||text.name=="RoomName"||text.name=="PlayerName"||text.name=="RoomTitle"?LobbyPaper:
                text.name=="Kicker"||text.name=="SeatNumber"||text.name=="SpectatorCount"||text.name=="SpectatorPrivacyTitle"?LobbyGold:new Color(.70f,.71f,.75f);
            if(text.GetComponentInParent<InputField>()!=null)text.color=LobbyPaper;
        }
        foreach(var button in root.GetComponentsInChildren<Button>(true))
        {
            bool primary=button.name=="CreateRoom"||button.name=="HostButton"||button.name=="ReadyButton"||button.name=="StartMatchButton"||button.name=="Join"||button.name=="SideboardReadyButton";
            var image=button.GetComponent<Image>();if(image!=null)image.color=Color.white;
            var colors=button.colors;
            colors.normalColor=primary?LobbyGold:new Color(.21f,.23f,.28f);
            colors.highlightedColor=primary?new Color(.88f,.73f,.49f):new Color(.30f,.33f,.39f);
            colors.pressedColor=primary?new Color(.61f,.47f,.28f):new Color(.16f,.18f,.22f);
            colors.selectedColor=colors.normalColor;colors.disabledColor=new Color(.18f,.19f,.22f);button.colors=colors;
            var caption=button.GetComponentInChildren<Text>(true);if(caption!=null)caption.color=primary?LobbyInk:LobbyPaper;
        }
        foreach(var toggle in root.GetComponentsInChildren<Toggle>(true))
        {
            if(toggle.GetComponentInParent<Dropdown>()!=null)continue;
            // Keep the outline readable even when the option is disabled.
            // A Selectable tint would also multiply the outline into the dark fill.
            toggle.transition=Selectable.Transition.None;
            var colors=toggle.colors;
            colors.normalColor=new Color(.22f,.29f,.38f);
            colors.highlightedColor=new Color(.32f,.42f,.54f);
            colors.pressedColor=new Color(.16f,.23f,.32f);
            colors.selectedColor=colors.normalColor;
            colors.disabledColor=new Color(.19f,.22f,.27f);
            toggle.colors=colors;
            if(toggle.graphic!=null)toggle.graphic.color=new Color(.97f,.82f,.52f);
        }
        // Popup rows are selection controls, not the room's checkbox style.
        foreach(var dropdown in root.GetComponentsInChildren<Dropdown>(true))DropdownChevron.Apply(dropdown);
        root.GetComponent<OnlineLobbyPanel>()?.RefreshAnteAvailabilityVisual();
        foreach(var image in root.GetComponentsInChildren<Image>(true))
            if(image.name=="PreparationInfo"||image.name=="Seat1"||image.name=="Seat2")EnsureRoomFrame(image);
    }

    private static void EnsureRoomFrame(Image image)
    {
        var outline=image.GetComponent<Outline>();if(outline!=null)outline.enabled=false;
        for(int side=0;side<4;side++) {
            string name="RoomFrame"+side;
            var edge=image.transform.Find(name) as RectTransform;
            if(edge==null){edge=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();edge.SetParent(image.transform,false);}
            bool horizontal=side<2,far=side%2==0;
            edge.anchorMin=horizontal?new Vector2(0,far?1:0):new Vector2(far?0:1,0);
            edge.anchorMax=horizontal?new Vector2(1,far?1:0):new Vector2(far?0:1,1);
            edge.pivot=horizontal?new Vector2(.5f,far?1:0):new Vector2(far?0:1,.5f);
            edge.sizeDelta=horizontal?new Vector2(0,1.5f):new Vector2(1.5f,0);edge.anchoredPosition=Vector2.zero;
            var graphic=edge.GetComponent<Image>();graphic.color=new Color(.36f,.39f,.45f);graphic.raycastTarget=false;edge.SetAsLastSibling();
        }
    }
}
