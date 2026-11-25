using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class OnlineLobbyPanel
{
    // Room servers send the canonical card name inside the rejection text.
    private static readonly Regex RoomCardRejection = new Regex(
        @"^卡牌 (?<card>.+?) (?:不符合房间赛制(?: (?<format>.+))?|(?<pool>不属于法禁的纸牌卡池))。$");

    private static string LocalizeDeckRejection(string reason)
    {
        if (string.IsNullOrEmpty(reason)) return string.Empty;
        var match = RoomCardRejection.Match(reason);
        if (!match.Success) return GamePreferences.T(reason);

        string card = GamePreferences.CardFaceName(match.Groups["card"].Value);
        if (match.Groups["pool"].Success)
            return string.Format(DeckCompanion.Text("卡牌 {0} 不属于法禁的纸牌卡池。",
                "Card {0} is outside the Duel Commander paper card pool.", "カード {0} はデュエルコマンダーの紙のカードプールに含まれません。"), card);
        if (match.Groups["format"].Success)
            return string.Format(GamePreferences.T("卡牌 {0} 不符合房间赛制 {1}。"), card, GamePreferences.T(match.Groups["format"].Value));
        return string.Format(GamePreferences.T("卡牌 {0} 不符合房间赛制。"), card);
    }

    private Button spectatorButton;
    private Text spectatorHeading,spectatorCount;
    private readonly Text[] seatRoles = new Text[4];
    private readonly Image[] seatBadges = new Image[4];
    private bool showingCancelStyle;
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
        for(int i=0;i<seatNames.Length;i++){
            var seat=players.Find("Seat"+(i+1));Place((RectTransform)seat,new Vector2(0,i==0?46:-46),new Vector2(648,72));
            Place(seatNames[i].rectTransform,new Vector2(-78,14),new Vector2(310,36));
            PreferenceText.SetSize(seatNames[i],20);
            seatNames[i].resizeTextForBestFit=true;
            seatNames[i].resizeTextMinSize=14;
            seatNames[i].resizeTextMaxSize=26;
            seatRoles[i]=AddText(seat,"Role","",new Vector2(-78,-17),new Vector2(310,27),14,browserFont,TextAnchor.MiddleLeft,MutedTextColor);
            seatBadges[i]=AddPanel(seat,"ReadyBadge",new Vector2(203,0),new Vector2(200,48),LobbyField);
            seatBadges[i].raycastTarget=false;
            seatStates[i].transform.SetParent(seatBadges[i].transform,false);
            Place(seatStates[i].rectTransform,Vector2.zero,new Vector2(188,42));
            seatStates[i].alignment=TextAnchor.MiddleCenter;
            PreferenceText.SetSize(seatStates[i],20);
            seatStates[i].fontStyle=FontStyle.Bold;
        }
        var observers=AddPanel(summary,"SpectatorCountBadge",Vector2.zero,new Vector2(228,46),LobbyField);
        observers.raycastTarget=false;
        spectatorHeading=AddText(observers.transform,"SpectatorHeading","",new Vector2(-34,0),new Vector2(132,36),18,browserFont,TextAnchor.MiddleLeft,LobbyPaper);
        spectatorCount=AddText(observers.transform,"SpectatorCount","0",new Vector2(69,0),new Vector2(62,36),16,browserFont,TextAnchor.MiddleRight,LobbyGold);
        var status=summary.Find("StatusPanel");Place((RectTransform)status,new Vector2(0,-282),new Vector2(648,34));
        Place(statusText.rectTransform,Vector2.zero,new Vector2(648,34));statusText.resizeTextForBestFit=true;statusText.resizeTextMinSize=12;statusText.resizeTextMaxSize=Mathf.RoundToInt(17*GamePreferences.TextScale);
        var prep=readyButton.transform.parent;
        AddText(prep,"SpectatorInfo",DeckCompanion.Text("开局后，自动进入战场","The battlefield opens when the match starts","対戦開始時に戦場へ自動移動します"),new Vector2(0,168),new Vector2(604,92),24,browserFont,TextAnchor.MiddleCenter,LobbyPaper).gameObject.SetActive(false);
        spectatorPrivacy=AddPanel(prep,"SpectatorPrivacy",new Vector2(0,-3),new Vector2(648,190),LobbyField).gameObject;
        AddText(spectatorPrivacy.transform,"SpectatorPrivacyTitle",DeckCompanion.Text("公开观战","Public view","公開情報の観戦"),new Vector2(0,52),new Vector2(584,34),20,browserFont,TextAnchor.MiddleLeft,LobbyGold);
        AddText(spectatorPrivacy.transform,"SpectatorPrivacyBody",DeckCompanion.Text("可查看战场、堆叠及公开区域。\n各玩家的手牌和私有选择会保持隐藏。","See the battlefield, stack and public zones.\nHands and private choices stay hidden.","戦場、スタック、公開領域を確認できます。\n手札と非公開の選択は表示されません。"),new Vector2(0,-24),new Vector2(584,104),18,browserFont,TextAnchor.MiddleLeft,MutedTextColor);
        spectatorPrivacy.SetActive(false);
        ApplySideboardPalette(sideboardPageGroup.transform);
    }

    private static void StyleRoomInset(Image image,Color fill)
    {
        image.color=fill;
        ObsidianArenaTheme.EnsureOutline(image,MenuVisualTheme.Etching,new Vector2(1,-1));
    }

    private static void StyleRoomSection(Transform panel,Text heading,Color accent)
    {
        MenuVisualTheme.StyleArchiveSection(panel.GetComponent<Image>(),accent);
        MenuTypography.Use(heading,MenuTypography.Face.Heading);
        PreferenceText.SetSize(heading,30);
        heading.color=accent;
    }

    private void StyleRoomPages()
    {
        var header=contentGroup.transform;
        header.Find("Subtitle").gameObject.SetActive(false);
        var rule=header.Find("HeaderBand").GetComponent<Image>();
        rule.color=MenuVisualTheme.MistBlue;
        rule.raycastTarget=false;
        rule.GetComponent<Outline>().enabled=false;
        var title=header.Find("Title").GetComponent<Text>();
        title.color=MenuVisualTheme.ArchivePaper;
        MenuTypography.Use(title,MenuTypography.Face.Heading);
        MenuTypography.Use(connectionBadgeText,MenuTypography.Face.Control);
        var badgePlate=MenuVisualTheme.Detail(header,"ConnectionBadgePlate",new Color32(12,25,37,238));
        Place(badgePlate.rectTransform,new Vector2(659,433),new Vector2(242,48));
        badgePlate.transform.SetSiblingIndex(connectionBadgeText.transform.GetSiblingIndex());
        connectionBadgeText.alignment=TextAnchor.MiddleCenter;
        MenuVisualTheme.StyleDeckTool(header.Find("BackButton").GetComponent<Button>());

        foreach(var page in new[]{browserPageGroup,connectionPageGroup,roomPageGroup})
        {
            MenuTypography.Scope(page.transform);
            BindBrowserText(page.transform);
            foreach(var button in page.GetComponentsInChildren<Button>(true))
            {
                bool primary=button==createRoomButton || button==hostButton || button==readyButton || button==startMatchButton;
                MenuVisualTheme.StyleDeckTool(button,primary);
                DisabledButtonVisual.Bind(button);
                var label=button.GetComponentInChildren<Text>(true);
                Stretch(label.rectTransform);
                label.rectTransform.offsetMin=new Vector2(9,2);
                label.rectTransform.offsetMax=new Vector2(-9,-2);
            }
            foreach(var input in page.GetComponentsInChildren<InputField>(true))
            {
                MenuVisualTheme.StyleSearchInput(input);
                input.textComponent.supportRichText=false;
            }
            foreach(var dropdown in page.GetComponentsInChildren<Dropdown>(true))
                DropdownChevron.Apply(dropdown);
        }
        var list=browserPageGroup.transform.Find("RoomBrowserCard");
        var identity=browserPageGroup.transform.Find("BrowserIdentity");
        StyleRoomSection(list,list.Find("Heading").GetComponent<Text>(),MenuVisualTheme.MainAccent);
        StyleRoomSection(identity,identity.Find("Heading").GetComponent<Text>(),MenuVisualTheme.LibraryAccent);
        StyleRoomInset(roomList.parent.GetComponent<Image>(),MenuVisualTheme.Recess);
        StyleRoomInset(createChoices.GetComponent<Image>(),MenuVisualTheme.Recess);
        MenuTypography.Use(browserEmpty,MenuTypography.Face.Heading);
        browserEmpty.color=MenuVisualTheme.MistBlue;

        var setup=connectionPageGroup.transform.Find("MatchSetupCard");
        var rules=connectionPageGroup.transform.Find("RulesCard");
        StyleRoomSection(setup,setup.Find("Heading").GetComponent<Text>(),MenuVisualTheme.MainAccent);
        StyleRoomSection(rules,rules.Find("Heading").GetComponent<Text>(),MenuVisualTheme.SideAccent);
        StyleRoomInset(connectionStatusText.transform.parent.GetComponent<Image>(),MenuVisualTheme.Recess);
        foreach(var toggle in new[]{customContentToggle,anteToggle,noShuffleToggle})
        {
            StyleRoomInset(toggle.GetComponent<Image>(),MenuVisualTheme.ArchivePanel);
            var box=toggle.transform.Find("Background").GetComponent<Image>();
            box.color=MenuVisualTheme.Recess;
            ObsidianArenaTheme.EnsureOutline(box,MenuVisualTheme.MistBlue,new Vector2(2,-2));
            toggle.transition=Selectable.Transition.None;
            toggle.graphic.color=MenuVisualTheme.LibraryAccent;
            var label=toggle.transform.Find("Label").GetComponent<Text>();
            MenuTypography.Use(label,MenuTypography.Face.Control);
            label.color=MenuVisualTheme.ArchivePaper;
            toggle.transform.Find("Description").GetComponent<Text>().color=MenuVisualTheme.Stone;
        }
        var summary=roomTitleText.transform.parent;
        var prep=(RectTransform)deckDropdown.transform.parent;
        summary.Find("Kicker").gameObject.SetActive(false);
        prep.Find("Kicker").gameObject.SetActive(false);
        StyleRoomSection(summary,roomTitleText,MenuVisualTheme.MainAccent);
        roomTitleText.supportRichText=false;
        roomTitleText.GetComponent<PreferenceText>().FitSingleLineHeight=true;
        var prepTitle=prep.Find("Heading").GetComponent<Text>();
        StyleRoomSection(prep,prepTitle,MenuVisualTheme.SideAccent);
        // Keep the panel's top aligned with the room summary when its contents change height.
        prep.anchoredPosition += Vector2.up * ((1f - prep.pivot.y) * prep.rect.height);
        prep.pivot = new Vector2(.5f, 1f);
        seriesText.color=MenuVisualTheme.MainAccent;
        summary.Find("PlayersPanel").GetComponent<Image>().color=Color.clear;
        statusText.transform.parent.GetComponent<Image>().color=Color.clear;
        statusText.transform.parent.GetComponent<Outline>().enabled=false;
        foreach(var name in seatNames)
        {
            var seat=name.transform.parent;
            StyleRoomInset(seat.GetComponent<Image>(),MenuVisualTheme.Recess);
            EnsureRoomFrame(seat.GetComponent<Image>());
            var number=seat.Find("SeatNumber").GetComponent<Text>();
            number.color=MenuVisualTheme.MainAccent;
            MenuTypography.Use(number,MenuTypography.Face.Heading);
        }
        StyleRoomInset(spectatorCount.transform.parent.GetComponent<Image>(),MenuVisualTheme.ArchivePanel);
        spectatorCount.color=MenuVisualTheme.LibraryAccent;
        deckNotice.raycastTarget=false;
        deckNoticeTitle.fontStyle=FontStyle.Bold;
        deckNoticeReason.resizeTextForBestFit=true;
        deckNoticeReason.resizeTextMinSize=14;
        deckNoticeReason.resizeTextMaxSize=24;
        var stripe=deckNoticeStripe.rectTransform;
        stripe.anchorMin=Vector2.zero; stripe.anchorMax=Vector2.up;
        stripe.pivot=new Vector2(0,.5f); stripe.sizeDelta=new Vector2(5,0); stripe.anchoredPosition=Vector2.zero;
        StyleRoomInset(spectatorPrivacy.GetComponent<Image>(),MenuVisualTheme.Recess);
        spectatorPrivacy.transform.Find("SpectatorPrivacyTitle").GetComponent<Text>().color=MenuVisualTheme.LibraryAccent;
        BuildPlayerRoomSurface(prep);
    }

    private void RefreshSpectatorControls(OnlineMatchService service)
    {
        if(spectatorButton==null)return;
        bool spectator=service?.IsSpectator==true;
        var summary=leaveRoomButton.transform.parent;
        spectatorHeading.text=DeckCompanion.Text("观战席","Spectators","観戦席");
        readyButton.transform.parent.Find("SpectatorInfo").GetComponent<Text>().text=DeckCompanion.Text("开局后，自动进入战场","The battlefield opens when the match starts","対戦開始時に戦場へ自動移動します");
        spectatorPrivacy.transform.Find("SpectatorPrivacyTitle").GetComponent<Text>().text=DeckCompanion.Text("公开观战","Public view","公開情報の観戦");
        spectatorPrivacy.transform.Find("SpectatorPrivacyBody").GetComponent<Text>().text=DeckCompanion.Text("可查看战场、堆叠及公开区域。\n各玩家的手牌和私有选择会保持隐藏。","See the battlefield, stack and public zones.\nHands and private choices stay hidden.","戦場、スタック、公開領域を確認できます。\n手札と非公開の選択は表示されません。");
        spectatorButton.interactable=service?.HasJoinedLobby==true && service.LobbyState?.series?.betweenGames!=true;
        SetButtonLabel(spectatorButton,spectator?"加入玩家席":"前往观战席");
        int viewers=service?.LobbyState?.players?.Count(player=>player!=null&&player.seat>(service.LobbyState.config?.PlayerCapacity??2)&&player.connected)??0;
        spectatorCount.text=viewers+DeckCompanion.Text(" 人",""," 人");
        spectatorPrivacy.SetActive(spectator);
        deckDropdown.interactable=!spectator&&!ready&&!submittingRoomDeck&&availableDecks.Count>0;
        var prep=(RectTransform)readyButton.transform.parent;
        prep.Find("Heading").GetComponent<Text>().text=spectator?"观战席":"选择出战牌组";
        deckDropdown.gameObject.SetActive(!spectator);
        deckNotice.gameObject.SetActive(!spectator);
        readyButton.gameObject.SetActive(!spectator);
        bool playerView = !spectator && service?.HasJoinedLobby == true;
        SetRoomRoleLayout(playerView, (RectTransform)summary, prep);
        if(!spectator)RefreshDeckNotice(playerView);
        float buttonTop = playerView ? LayoutPlayerRoomFooter() : prep.rect.height - 90f;
        float deckWidth = playerView ? RoomDeckWidth : 648;
        float buttonGap = playerView ? 20 : 32;
        float buttonWidth = (deckWidth - buttonGap) * .5f;
        float buttonOffset = (buttonWidth + buttonGap) * .5f;
        PlaceFromTop((RectTransform)readyButton.transform, -buttonOffset, buttonTop, buttonWidth, 58);
        PlaceFromTop((RectTransform)startMatchButton.transform, spectator ? 0 : buttonOffset, buttonTop, spectator ? deckWidth : buttonWidth, 58);
        prep.Find("SpectatorInfo").gameObject.SetActive(spectator);
        prep.Find("Kicker").GetComponent<Text>().text=spectator?"观战":"准备";

    }

    private static void PlaceFromTop(RectTransform rect, float x, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -top);
    }

    private void RefreshDeckNotice(bool playerView)
    {
        bool rejected=!string.IsNullOrEmpty(deckRejection);
        var accent=rejected ? MenuVisualTheme.Warning : ready ? MenuVisualTheme.LibraryAccent :
            submittingRoomDeck ? MenuVisualTheme.MainAccent : MenuVisualTheme.Stone;
        float width=playerView ? RoomDeckWidth : 648;
        deckNotice.color=Color.Lerp(MenuVisualTheme.Recess,accent,rejected||ready ? .18f : .055f);
        deckNotice.GetComponent<Outline>().effectColor=Color.Lerp(MenuVisualTheme.Etching,accent,.65f);
        deckNoticeStripe.color=deckNoticeTitle.color=accent;
        deckNoticeTitle.text=rejected ? DeckCompanion.Text("牌组未通过校验","Deck rejected","デッキを登録できません") :
            ready ? DeckCompanion.Text("牌组已提交 · 已准备","Deck submitted · Ready","デッキ提出済み · 準備完了") :
            submittingRoomDeck ? DeckCompanion.Text("正在提交牌组…","Submitting deck…","デッキ提出中…") :
            selectedDeck==null ? GamePreferences.T("尚未选择牌组") : GamePreferences.T("未准备");
        deckNoticeReason.gameObject.SetActive(rejected);
        deckNoticeReason.text=LocalizeDeckRejection(deckRejection);
        deckNoticeReason.color=MenuVisualTheme.ArchivePaper;
        float noticeWidth = playerView && !rejected ? Mathf.Min(width, Mathf.Ceil(deckNoticeTitle.preferredWidth) + 36) : width;
        PlaceFromTop(deckNotice.rectTransform, (noticeWidth-width)*.5f, playerView ? RoomInset + 312 : 193,
            noticeWidth, rejected ? 134 : playerView ? 46 : 64);
        Place(deckNoticeTitle.rectTransform,new Vector2(0,rejected?41:0),new Vector2(noticeWidth-(rejected?52:32),40));
        Place(deckNoticeReason.rectTransform,new Vector2(0,-22),new Vector2(width-52,78));
        if(showingCancelStyle!=ready)
        {
            MenuVisualTheme.StyleDeckTool(readyButton,!ready);
            showingCancelStyle=ready;
        }
    }

    private void RefreshSeatBadge(int index, OnlineLobbyPlayer player, string role)
    {
        bool confirmed=player?.ready==true && player.hasDeck;
        seatRoles[index].text=player==null?string.Empty:role;
        var badge=seatBadges[index];
        badge.gameObject.SetActive(player!=null);
        if(player==null)return;
        var accent=confirmed?MenuVisualTheme.LibraryAccent:MenuVisualTheme.Stone;
        badge.color=Color.Lerp(MenuVisualTheme.Recess,accent,confirmed ? .32f : .09f);
        badge.GetComponent<Outline>().effectColor=confirmed?accent:MenuVisualTheme.Etching;
        var state=seatStates[index];
        state.text=confirmed ? GamePreferences.T("已准备") : player.hasDeck
            ? DeckCompanion.Text("已提交 · 未准备","Submitted · Not ready","提出済 · 準備前") : GamePreferences.T("未准备");
        state.color=confirmed?MenuVisualTheme.ArchivePaper:MenuVisualTheme.Stone;
        state.resizeTextForBestFit=true; state.resizeTextMinSize=14; state.resizeTextMaxSize=Mathf.RoundToInt(20*GamePreferences.TextScale);
    }

    private static void ApplySideboardPalette(Transform root)
    {
        foreach(var image in root.GetComponentsInChildren<Image>(true))
        {
            if(image.sprite!=null)continue;
            image.color=LobbySurface;
            var edge=image.GetComponent<Outline>();
            if(edge!=null)edge.effectColor=new Color(.30f,.29f,.30f,.65f);
        }
        foreach(var text in root.GetComponentsInChildren<Text>(true))
        {
            text.color=text.name.Contains("Heading")?LobbyPaper:text.name=="Kicker"?LobbyGold:new Color(.70f,.71f,.75f);
        }
        foreach(var button in root.GetComponentsInChildren<Button>(true))
        {
            bool primary=button.name=="SideboardReadyButton";
            var image=button.GetComponent<Image>();if(image!=null)image.color=Color.white;
            var colors=button.colors;
            colors.normalColor=primary?LobbyGold:new Color(.21f,.23f,.28f);
            colors.highlightedColor=primary?new Color(.88f,.73f,.49f):new Color(.30f,.33f,.39f);
            colors.pressedColor=primary?new Color(.61f,.47f,.28f):new Color(.16f,.18f,.22f);
            colors.selectedColor=colors.normalColor;colors.disabledColor=new Color(.18f,.19f,.22f);button.colors=colors;
            var caption=button.GetComponentInChildren<Text>(true);if(caption!=null)caption.color=primary?LobbyInk:LobbyPaper;
        }
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
            var graphic=edge.GetComponent<Image>();graphic.color=MenuVisualTheme.Etching;graphic.raycastTarget=false;edge.SetAsLastSibling();
        }
    }
}
