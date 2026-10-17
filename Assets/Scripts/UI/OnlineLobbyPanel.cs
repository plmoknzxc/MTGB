using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed partial class OnlineLobbyPanel : MonoBehaviour
{
    private static readonly Color PageColor = new Color(.065f,.065f,.08f);
    private static readonly Color PanelColor = new Color(.13f,.14f,.17f);
    private static readonly Color FieldColor = new Color(.085f,.095f,.12f);
    private static readonly Color TextColor = ObsidianArenaTheme.Text;
    private static readonly Color MutedTextColor = ObsidianArenaTheme.MutedText;
    private static readonly Color AccentColor = new Color(.74f,.59f,.36f);
    private static readonly Color OpponentColor = ObsidianArenaTheme.RedDeep;

    private MainMenuController owner;
    private DeckDefinition selectedDeck;
    private InputField addressInput;
    private InputField portInput;
    private InputField nameInput;
    private InputField profileInput;
    private InputField roomInput;
    private InputField lifeInput;
    private Dropdown cardFormatDropdown;
    private Dropdown formatDropdown;
    private Dropdown deckDropdown;
    private RectTransform mainDeckContent;
    private RectTransform sideboardContent;
    private RectTransform mainDeckDropArea;
    private RectTransform sideboardDropArea;
    private Toggle customContentToggle;
    private Toggle anteToggle;
    private Text anteStateText;
    private Text customContentStateText;
    private Text connectionBadgeText;
    private Text connectionStatusText;
    private Text statusText;
    private readonly Text[] seatNames = new Text[2];
    private readonly Text[] seatStates = new Text[2];
    private Text deckText;
    private Text roomTitleText;
    private Text roomRulesText;
    private Text seriesText;
    private Text sideboardSeriesText;
    private Text sideboardDeckText;
    private Text sideboardStatusText;
    private Button hostButton;
    private Button connectButton;
    private Button readyButton;
    private Button startMatchButton;
    private Button sideboardReadyButton;
    private Button sideboardStartButton;
    private Button leaveRoomButton;
    private Button resetSideboardButton;
    private CanvasGroup contentGroup;
    private CanvasGroup connectionPageGroup;
    private CanvasGroup roomPageGroup;
    private CanvasGroup sideboardPageGroup;
    private Coroutine transitionRoutine;
    private readonly List<DeckDefinition> availableDecks = new List<DeckDefinition>();
    private DeckDefinition sideboardBaselineDeck;
    private int sideboardGameNumber = -1;
    private OnlineSideboardCardTile draggedSideboardTile;
    private RectTransform sideboardDragGhost;
    private bool suppressDeckSelection;
    private bool ready;
    private bool closing;
    private string deckDirectoryRevision;
    private float nextDeckScan;

    private void Update(){if(Time.unscaledTime<nextDeckScan)return;nextDeckScan=Time.unscaledTime+1;
        string revision=DeckRepository.DirectoryRevision();if(revision==deckDirectoryRevision)return;
        ReloadDeckChoices();RefreshDeckSummary();Refresh();}
    private void OnApplicationFocus(bool focused){if(focused)nextDeckScan=0;}

    private Canvas battlefieldLobbyCanvas;
    internal static bool HasBattlefieldLobby => FindFirstObjectByType<OnlineLobbyPanel>()?.battlefieldLobbyCanvas != null;
    internal static void OpenSideboarding()
    {
        // Stay on the current scene while editing the match's deck. Loading the
        // main menu rebuilt its full menus and catalog just to show this panel.
        var canvas=ReplayWidgets.Canvas("SideboardingCanvas",700);
        var panel=Create(canvas.transform);
        panel.battlefieldLobbyCanvas=canvas;
        Show(null,null);
    }

    public static OnlineLobbyPanel Show(MainMenuController owner, DeckDefinition deck)
    {
        OnlineLobbyPanel panel = FindFirstObjectByType<OnlineLobbyPanel>();
        if (panel == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) throw new InvalidOperationException("The main menu scene has no Canvas for the online lobby.");
            panel = Create(canvas.transform);
        }

        panel.owner = owner;
        panel.owner?.SetOnlineLobbyVisible(true);
        OnlineClientProfile profile = OnlineClientProfileStore.Load(
            OnlineClientProfileStore.ResolveCurrentProfileId());
        var seriesReview = OnlineMatchService.Instance?.EndReview;
        panel.selectedDeck = CloneDeck(seriesReview?.series?.finished == false && seriesReview.ownDeck != null ? seriesReview.ownDeck : OnlineMatchService.Instance?.LocalRegisteredDeck ?? profile.deck ?? deck);
        // Create can refresh before Show supplies the deck. Recapture the current
        // series pool after loading it, including any match-only removals.
        panel.sideboardGameNumber = -1;
        panel.ReloadDeckChoices();
        panel.ready = false;
        panel.closing = false;
        panel.gameObject.SetActive(true);
        panel.transform.SetAsLastSibling();
        panel.Subscribe();
        panel.creatingRoom=false;
        panel.Refresh();
        if(OnlineMatchService.Instance?.HasJoinedLobby!=true)panel.RefreshBrowser();
        panel.PlayEnterTransition();
        return panel;
    }

    private static OnlineLobbyPanel Create(Transform parent)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject root = new GameObject(
            "OnlineLobbyPanel", typeof(RectTransform), typeof(Image), typeof(OnlineLobbyPanel));
        root.transform.SetParent(parent, false);
        Stretch(root.GetComponent<RectTransform>());
        Image pageBackground = root.GetComponent<Image>();
        pageBackground.color = PageColor;
        pageBackground.raycastTarget = true;

        OnlineLobbyPanel panel = root.GetComponent<OnlineLobbyPanel>();
        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
        content.transform.SetParent(root.transform, false);
        Stretch(content.GetComponent<RectTransform>());
        panel.contentGroup = content.GetComponent<CanvasGroup>();

        AddPanel(content.transform, "HeaderBand", new Vector2(0f, 470f), new Vector2(1920f, 140f),
            ObsidianArenaTheme.Surface);
        AddPanel(content.transform, "HeaderBlueRule", new Vector2(-480f, 400f), new Vector2(960f, 3f),
            ObsidianArenaTheme.Blue);
        AddPanel(content.transform, "HeaderRedRule", new Vector2(480f, 400f), new Vector2(960f, 3f),
            ObsidianArenaTheme.Red);
        Button backButton = AddButton(content.transform, "BackButton", "返回", new Vector2(-852f, 470f),
            new Vector2(132f, 50f), font, ObsidianArenaTheme.SurfaceRaised);
        AddText(content.transform, "Title", "联机对战", new Vector2(-540f, 484f), new Vector2(420f, 56f),
            40, font, TextAnchor.MiddleLeft, TextColor);
        AddText(content.transform, "Subtitle", "局域网房间 · 双方准备后由房主开始", new Vector2(-440f, 442f),
            new Vector2(620f, 32f), 18, font, TextAnchor.MiddleLeft, MutedTextColor);
        panel.connectionBadgeText = AddText(content.transform, "ConnectionBadge", "● 未连接",
            new Vector2(760f, 470f), new Vector2(250f, 46f), 20, font, TextAnchor.MiddleCenter, MutedTextColor);

        GameObject connectionPage = new GameObject("ConnectionPage", typeof(RectTransform), typeof(CanvasGroup));
        connectionPage.transform.SetParent(content.transform, false);
        Stretch(connectionPage.GetComponent<RectTransform>());
        panel.connectionPageGroup = connectionPage.GetComponent<CanvasGroup>();

        Image connectionCard = AddPanel(connectionPage.transform, "ConnectionCard", new Vector2(-485f, -5f),
            new Vector2(780f, 790f), PanelColor);
        AddText(connectionCard.transform, "Kicker", "LAN CONNECTION", new Vector2(-15f, 332f),
            new Vector2(590f, 28f), 15, font, TextAnchor.MiddleLeft, ObsidianArenaTheme.Blue);
        AddText(connectionCard.transform, "Heading", "连接到局域网房间", new Vector2(-15f, 287f),
            new Vector2(590f, 48f), 30, font, TextAnchor.MiddleLeft, TextColor);

        string profileId = OnlineClientProfileStore.ResolveCurrentProfileId();
        OnlineClientProfile profile = OnlineClientProfileStore.Load(profileId);
        AddFieldLabel(connectionCard.transform, "ProfileLabel", "客户端配置", new Vector2(-280f, 214f), font);
        panel.profileInput = AddInput(connectionCard.transform, "ProfileInput", profileId,
            new Vector2(40f, 214f), new Vector2(430f, 48f), font, "player-1");
        AddFieldLabel(connectionCard.transform, "NameLabel", "玩家名称", new Vector2(-280f, 142f), font);
        panel.nameInput = AddInput(connectionCard.transform, "NameInput", profile.displayName,
            new Vector2(40f, 142f), new Vector2(430f, 48f), font, "Player 1");
        AddFieldLabel(connectionCard.transform, "EndpointLabel", "主机地址", new Vector2(-280f, 70f), font);
        panel.addressInput = AddInput(connectionCard.transform, "AddressInput", profile.address,
            new Vector2(-25f, 70f), new Vector2(300f, 48f), font, "127.0.0.1");
        panel.portInput = AddInput(connectionCard.transform, "PortInput", profile.port.ToString(),
            new Vector2(230f, 70f), new Vector2(170f, 48f), font, "7777");
        panel.portInput.contentType = InputField.ContentType.IntegerNumber;
        AddFieldLabel(connectionCard.transform, "RoomLabel", "房间号", new Vector2(-280f, -2f), font);
        panel.roomInput = AddInput(connectionCard.transform, "RoomInput", profile.roomId,
            new Vector2(40f, -2f), new Vector2(430f, 48f), font, "main");

        panel.hostButton = AddButton(connectionCard.transform, "HostButton", "创建局域网主机",
            new Vector2(-120f, -92f), new Vector2(330f, 58f), font, AccentColor);
        panel.connectButton = AddButton(connectionCard.transform, "ConnectButton", "加入已有主机",
            new Vector2(235f, -92f), new Vector2(330f, 58f), font, OpponentColor);
        AddText(connectionCard.transform, "ConnectionHelp",
            "同一台电脑测试可使用 127.0.0.1；其他电脑请输入建主者的局域网 IP。",
            new Vector2(0f, -157f), new Vector2(650f, 54f), 16, font, TextAnchor.MiddleLeft, MutedTextColor);

        Image deckCard = AddPanel(connectionCard.transform, "DeckStepHint", new Vector2(0f, -270f),
            new Vector2(650f, 142f), ObsidianArenaTheme.Field);
        AddText(deckCard.transform, "DeckLabel", "下一步", new Vector2(0f, 40f),
            new Vector2(560f, 28f), 16, font, TextAnchor.MiddleLeft, MutedTextColor);
        AddText(deckCard.transform, "DeckValue", "连接成功后进入独立房间页，再选择牌组并准备。", new Vector2(0f, -8f),
            new Vector2(560f, 58f), 19, font, TextAnchor.MiddleLeft, TextColor);

        Image roomCard = AddPanel(connectionPage.transform, "MatchSetupCard", new Vector2(415f, -5f),
            new Vector2(940f, 790f), PanelColor);
        AddText(roomCard.transform, "Kicker", "MATCH SETUP", new Vector2(-20f, 332f),
            new Vector2(720f, 28f), 15, font, TextAnchor.MiddleLeft, ObsidianArenaTheme.Red);
        AddText(roomCard.transform, "Heading", "房间与对局设置", new Vector2(-20f, 287f),
            new Vector2(720f, 48f), 30, font, TextAnchor.MiddleLeft, TextColor);

        AddFieldLabel(roomCard.transform, "LifeLabel", "初始生命", new Vector2(-360f, 216f), font);
        panel.lifeInput = AddInput(roomCard.transform, "LifeInput", "20", new Vector2(-205f, 216f),
            new Vector2(120f, 46f), font, "20");
        panel.lifeInput.contentType = InputField.ContentType.IntegerNumber;
        AddText(roomCard.transform, "FormatLabel", "比赛局数", new Vector2(-60f, 216f),
            new Vector2(90f, 34f), 17, font, TextAnchor.MiddleLeft, MutedTextColor);
        panel.formatDropdown = AddDropdown(roomCard.transform, "FormatDropdown", new Vector2(120f, 216f),
            new Vector2(260f, 46f), font, new List<string> { "BO1 · 一局胜负", "BO3 · 三局两胜" });
        AddText(roomCard.transform, "FirstPlayerLabel", "先后手", new Vector2(-60f, 216f),
            new Vector2(120f, 34f), 17, font, TextAnchor.MiddleLeft, MutedTextColor).gameObject.SetActive(false);
        AddText(roomCard.transform, "FirstPlayerValue", "每局随机决定", new Vector2(190f, 216f),
            new Vector2(340f, 40f), 20, font, TextAnchor.MiddleLeft, TextColor).gameObject.SetActive(false);

        AddFieldLabel(roomCard.transform, "SetsLabel", "赛制", new Vector2(-360f, 148f), font);
        panel.cardFormatDropdown = AddDropdown(roomCard.transform, "CardFormatDropdown",
            new Vector2(90f, 148f), new Vector2(620f, 46f), font, new List<string>());

        panel.customContentToggle = AddToggle(roomCard.transform, "CustomToggle", "允许 DIY 卡牌",
            "开启后，允许使用在数据编辑器中标记为 DIY 的卡牌。", new Vector2(0f, 63f),
            new Vector2(780f, 72f), font, out panel.customContentStateText);
        panel.anteToggle = AddToggle(roomCard.transform,"AnteToggle","赌注（仅本场比赛）",
            "归属变化影响 BO3 后续局，不修改保存的牌组或收藏。",new Vector2(0f,-46f),
            new Vector2(780f,72f),font,out panel.anteStateText);
        panel.anteToggle.onValueChanged.AddListener(_=>panel.UpdateAnteLabel());
        panel.cardFormatDropdown.onValueChanged.AddListener(_=>panel.UpdateAnteLabel());

        AddPanel(roomCard.transform, "Divider", new Vector2(0f, 8f), new Vector2(780f, 2f),
            ObsidianArenaTheme.LineSoft);
        AddText(roomCard.transform, "RulesHelp",
            "主牌至少 60 张 · 备牌最多 15 张 · 非基本牌合计最多 4 张\n每局重新随机先后手；BO3 局间可在主牌与备牌间一换一。",
            new Vector2(0f, -142f), new Vector2(780f, 72f), 18, font,
            TextAnchor.MiddleLeft, MutedTextColor);
        Image connectionStatusPanel = AddPanel(roomCard.transform, "ConnectionStatusPanel",
            new Vector2(0f, -230f), new Vector2(780f, 78f), ObsidianArenaTheme.Field);
        panel.connectionStatusText = AddText(connectionStatusPanel.transform, "Status",
            "创建主机，或输入局域网主机地址加入房间。", Vector2.zero,
            new Vector2(720f, 58f), 17, font, TextAnchor.MiddleLeft, MutedTextColor);

        GameObject roomPage = new GameObject("RoomPage", typeof(RectTransform), typeof(CanvasGroup));
        roomPage.transform.SetParent(content.transform, false);
        Stretch(roomPage.GetComponent<RectTransform>());
        panel.roomPageGroup = roomPage.GetComponent<CanvasGroup>();

        Image roomSummary = AddPanel(roomPage.transform, "RoomSummaryCard", new Vector2(-485f, -5f),
            new Vector2(780f, 790f), PanelColor);
        AddText(roomSummary.transform, "Kicker", "LAN ROOM", new Vector2(-15f, 332f),
            new Vector2(590f, 28f), 15, font, TextAnchor.MiddleLeft, ObsidianArenaTheme.Blue);
        panel.roomTitleText = AddText(roomSummary.transform, "RoomTitle", "房间",
            new Vector2(-15f, 282f), new Vector2(590f, 54f), 32, font,
            TextAnchor.MiddleLeft, TextColor);
        panel.roomRulesText = AddText(roomSummary.transform, "RoomRules", string.Empty,
            new Vector2(-15f, 220f), new Vector2(620f, 66f), 17, font,
            TextAnchor.MiddleLeft, MutedTextColor);
        panel.seriesText = AddText(roomSummary.transform, "SeriesStatus", "等待本局开始",
            new Vector2(-15f, 132f), new Vector2(620f, 62f), 22, font,
            TextAnchor.MiddleLeft, ObsidianArenaTheme.Blue);
        AddText(roomSummary.transform, "PlayersLabel", "房间席位", new Vector2(-15f, 62f),
            new Vector2(620f, 38f), 20, font, TextAnchor.MiddleLeft, TextColor);
        Image playersPanel = AddPanel(roomSummary.transform, "PlayersPanel", new Vector2(0f, -38f),
            new Vector2(650f, 132f), ObsidianArenaTheme.Field);
        for (int i = 0; i < 2; i++)
        {
            Image seat = AddPanel(playersPanel.transform, "Seat" + (i + 1),
                new Vector2(0f, i == 0 ? 55f : -55f), new Vector2(648f, 96f), FieldColor);
            AddText(seat.transform, "SeatNumber", "0" + (i + 1), new Vector2(-276f, 0f),
                new Vector2(48f, 48f), 26, font, TextAnchor.MiddleCenter, MutedTextColor);
            panel.seatNames[i] = AddText(seat.transform, "PlayerName", "等待玩家加入",
                new Vector2(18f, 19f), new Vector2(500f, 34f), 22, font, TextAnchor.MiddleLeft, TextColor);
            panel.seatNames[i].supportRichText = false;
            panel.seatStates[i] = AddText(seat.transform, "PlayerState", "空位",
                new Vector2(18f, -20f), new Vector2(500f, 28f), 16, font, TextAnchor.MiddleLeft, MutedTextColor);
        }
        Image roomStatusPanel = AddPanel(roomSummary.transform, "StatusPanel", new Vector2(0f, -190f),
            new Vector2(650f, 92f), ObsidianArenaTheme.Field);
        panel.statusText = AddText(roomStatusPanel.transform, "Status", "等待玩家加入。", Vector2.zero,
            new Vector2(590f, 68f), 17, font, TextAnchor.MiddleLeft, MutedTextColor);
        panel.leaveRoomButton = AddButton(roomSummary.transform, "LeaveRoomButton", "离开房间",
            new Vector2(0f, -315f), new Vector2(300f, 58f), font, ObsidianArenaTheme.SurfaceRaised);

        Image preparationCard = AddPanel(roomPage.transform, "DeckPreparationCard", new Vector2(415f, -5f),
            new Vector2(940f, 790f), PanelColor);
        AddText(preparationCard.transform, "Kicker", "MATCH PREPARATION", new Vector2(-20f, 332f),
            new Vector2(720f, 28f), 15, font, TextAnchor.MiddleLeft, ObsidianArenaTheme.Red);
        AddText(preparationCard.transform, "Heading", "选择牌组并准备", new Vector2(-20f, 287f),
            new Vector2(720f, 48f), 30, font, TextAnchor.MiddleLeft, TextColor);
        AddFieldLabel(preparationCard.transform, "DeckLabel", "使用牌组", new Vector2(-350f, 222f), font);
        panel.deckDropdown = AddDropdown(preparationCard.transform, "DeckDropdown",
            new Vector2(90f, 222f), new Vector2(620f, 48f), font, new List<string> { "尚无可用牌组" });
        Image selectedDeckPanel = AddPanel(preparationCard.transform, "SelectedDeckSummary",
            new Vector2(0f, 145f), new Vector2(780f, 82f), ObsidianArenaTheme.Field);
        panel.deckText = AddText(selectedDeckPanel.transform, "DeckValue", "尚未选择牌组", Vector2.zero,
            new Vector2(720f, 60f), 19, font, TextAnchor.MiddleLeft, TextColor);

        panel.readyButton = AddButton(preparationCard.transform, "ReadyButton", "提交牌组并准备",
            new Vector2(-190f, -42f), new Vector2(390f, 64f), font, AccentColor);
        panel.startMatchButton = AddButton(preparationCard.transform, "StartMatchButton", "等待双方准备",
            new Vector2(245f, -42f), new Vector2(300f, 64f), font, OpponentColor);
        AddText(preparationCard.transform, "ReadyHelp",
            "牌组只会提交给当前房间；双方准备后，由房主确认开始比赛。",
            new Vector2(0f, -107f), new Vector2(760f, 46f), 15, font,
            TextAnchor.MiddleCenter, MutedTextColor);

        Image preparationInfo = AddPanel(preparationCard.transform, "PreparationInfo",
            new Vector2(0f, -230f), new Vector2(780f, 146f), ObsidianArenaTheme.Field);
        AddText(preparationInfo.transform, "Title", "比赛开始后",
            new Vector2(0f, 38f), new Vector2(710f, 30f), 18, font,
            TextAnchor.MiddleLeft, TextColor);
        AddText(preparationInfo.transform, "Body",
            "BO3 每局结束后会自动进入独立换备界面；完成换备并准备后开始下一局。",
            new Vector2(0f, -12f), new Vector2(710f, 62f), 16, font,
            TextAnchor.MiddleLeft, MutedTextColor);

        GameObject sideboardPage = new GameObject("SideboardPage", typeof(RectTransform), typeof(CanvasGroup));
        sideboardPage.transform.SetParent(content.transform, false);
        Stretch(sideboardPage.GetComponent<RectTransform>());
        panel.sideboardPageGroup = sideboardPage.GetComponent<CanvasGroup>();

        Image sideboardHeader = AddPanel(sideboardPage.transform, "SideboardHeader",
            new Vector2(0f, 286f), new Vector2(1720f, 176f), PanelColor);
        AddText(sideboardHeader.transform, "Kicker", "BETWEEN GAMES",
            new Vector2(-520f, 48f), new Vector2(680f, 28f), 15, font,
            TextAnchor.MiddleLeft, ObsidianArenaTheme.Blue);
        AddText(sideboardHeader.transform, "Heading", "局间换备",
            new Vector2(-520f, 0f), new Vector2(680f, 54f), 34, font,
            TextAnchor.MiddleLeft, TextColor);
        panel.sideboardSeriesText = AddText(sideboardHeader.transform, "SeriesStatus", string.Empty,
            new Vector2(430f, 4f), new Vector2(720f, 58f), 24, font,
            TextAnchor.MiddleRight, ObsidianArenaTheme.Blue);

        Image mainDeckCard = AddPanel(sideboardPage.transform, "MainDeckCard",
            new Vector2(-430f, -45f), new Vector2(820f, 450f), PanelColor);
        AddText(mainDeckCard.transform, "Heading", "主牌 · 拖到右侧移入备牌",
            new Vector2(-20f, 164f), new Vector2(700f, 42f), 26, font,
            TextAnchor.MiddleLeft, TextColor);
        panel.mainDeckContent = AddScrollGrid(mainDeckCard.transform, "MainDeckScroll", "MainDeckContent",
            new Vector2(0f, -30f), new Vector2(740f, 330f), new Vector2(112f, 157f),
            new Vector2(10f, 12f), out panel.mainDeckDropArea);

        Image sideboardCard = AddPanel(sideboardPage.transform, "SideboardCard",
            new Vector2(430f, -45f), new Vector2(820f, 450f), PanelColor);
        AddText(sideboardCard.transform, "Heading", "备牌 · 拖到左侧移回主牌",
            new Vector2(-20f, 164f), new Vector2(700f, 42f), 26, font,
            TextAnchor.MiddleLeft, TextColor);
        panel.sideboardContent = AddScrollGrid(sideboardCard.transform, "SideboardScroll", "SideboardContent",
            new Vector2(0f, -30f), new Vector2(740f, 330f), new Vector2(112f, 157f),
            new Vector2(10f, 12f), out panel.sideboardDropArea);

        panel.sideboardDeckText = AddText(sideboardPage.transform, "DeckSummary", string.Empty,
            new Vector2(-500f, -314f), new Vector2(720f, 44f), 18, font,
            TextAnchor.MiddleLeft, TextColor);
        panel.sideboardStatusText = AddText(sideboardPage.transform, "SideboardStatus",
            "拖动一张卡牌到另一侧；每次拖动移动一张，牌池总数保持不变。",
            new Vector2(0f, -356f), new Vector2(1040f, 42f), 16, font,
            TextAnchor.MiddleCenter, MutedTextColor);
        panel.resetSideboardButton = AddButton(sideboardPage.transform, "ResetSideboardButton", "撤销本次换备",
            new Vector2(250f, -314f), new Vector2(240f, 54f), font, ObsidianArenaTheme.SurfaceRaised);
        panel.sideboardReadyButton = AddButton(sideboardPage.transform, "SideboardReadyButton", "完成换备并准备",
            new Vector2(520f, -314f), new Vector2(270f, 54f), font, AccentColor);
        panel.sideboardStartButton = AddButton(sideboardPage.transform, "SideboardStartButton", "等待双方准备",
            new Vector2(800f, -314f), new Vector2(260f, 54f), font, OpponentColor);

        panel.readyButton.interactable = false;
        panel.hostButton.onClick.AddListener(panel.CreateSelectedRoom);
        panel.connectButton.onClick.AddListener(panel.Connect);
        panel.readyButton.onClick.AddListener(panel.ToggleReady);
        panel.startMatchButton.onClick.AddListener(panel.StartMatch);
        panel.sideboardReadyButton.onClick.AddListener(panel.ToggleReady);
        panel.sideboardStartButton.onClick.AddListener(panel.StartMatch);
        panel.leaveRoomButton.onClick.AddListener(panel.LeaveRoom);
        panel.deckDropdown.onValueChanged.AddListener(panel.OnDeckSelected);
        panel.resetSideboardButton.onClick.AddListener(panel.ResetSideboardChanges);
        panel.profileInput.onEndEdit.AddListener(panel.LoadProfile);
        panel.customContentToggle.onValueChanged.AddListener(panel.OnCustomContentChanged);
        backButton.onClick.AddListener(panel.BrowserBack);
        SetPageVisible(panel.connectionPageGroup, true);
        SetPageVisible(panel.roomPageGroup, false);
        SetPageVisible(panel.sideboardPageGroup, false);
        panel.UpdateCustomContentLabel();
        MenuVisualTheme.ApplyLobby(root.transform);
        panel.BuildRoomBrowser(font);
        panel.BuildSpectatorControls();
        panel.BuildPlayDrawControls();
        panel.RefreshLanguage();
        return panel;
    }

    private void OnDestroy() => Unsubscribe();

    private void Subscribe()
    {
        GamePreferences.Changed -= RefreshLanguage;
        GamePreferences.Changed += RefreshLanguage;
        OnlineMatchService service = OnlineMatchService.EnsureInstance();
        service.StatusChanged -= OnStatus;
        service.StatusChanged += OnStatus;
        service.LobbyChanged -= OnLobby;
        service.LobbyChanged += OnLobby;
        service.ConnectionChanged -= OnConnection;
        service.ConnectionChanged += OnConnection;
    }

    private void Unsubscribe()
    {
        GamePreferences.Changed -= RefreshLanguage;
        if (OnlineMatchService.Instance == null) return;
        OnlineMatchService.Instance.StatusChanged -= OnStatus;
        OnlineMatchService.Instance.LobbyChanged -= OnLobby;
        OnlineMatchService.Instance.ConnectionChanged -= OnConnection;
    }

    private void Connect()
    {
        OnlineMatchService service = OnlineMatchService.EnsureInstance();
        if (service.IsOnline)
        {
            service.Disconnect();
            ready = false;
            Refresh();
            return;
        }

        ushort port = ushort.TryParse(portInput.text, out ushort parsed) ? parsed : (ushort)7777;
        service.Connect(addressInput.text, port, nameInput.text, profileInput.text, roomInput.text, ReadRoomConfig());
        Refresh();
    }

    private void Host()
    {
        OnlineMatchService service = OnlineMatchService.EnsureInstance();
        if (service.IsOnline)
        {
            service.Disconnect();
            ready = false;
            Refresh();
            return;
        }

        ushort port = ushort.TryParse(portInput.text, out ushort parsed) ? parsed : (ushort)7777;
        service.StartLanHost(port, nameInput.text, profileInput.text, roomInput.text, ReadRoomConfig());
        Refresh();
    }

    private void LeaveRoom()
    {
        OnlineMatchService service = OnlineMatchService.Instance;
        if (service != null && service.IsOnline && !service.IsInMatch) service.Disconnect();
        creatingRoom=false;
        ready = false;
        sideboardBaselineDeck = null;
        sideboardGameNumber = -1;
        Refresh();
    }

    private void ReloadDeckChoices()
    {
        deckDirectoryRevision=DeckRepository.DirectoryRevision();
        availableDecks.Clear();
        IReadOnlyList<DeckDefinition> savedDecks = DeckRepository.LoadAll();
        for (int i = 0; i < savedDecks.Count; i++)
        {
            if (savedDecks[i] != null) availableDecks.Add(CloneDeck(savedDecks[i]));
        }

        bool locked=ready || OnlineMatchService.Instance?.LobbyState?.series?.betweenGames==true;
        if(!locked){var saved=availableDecks.Find(item=>SameDeckName(item,selectedDeck));selectedDeck=CloneDeck(saved??(availableDecks.Count>0?availableDecks[0]:null));}

        if (deckDropdown == null) return;
        suppressDeckSelection = true;
        deckDropdown.ClearOptions();
        var options = new List<string>();
        for (int i = 0; i < availableDecks.Count; i++)
        {
            DeckDefinition deck = availableDecks[i];
            options.Add($"{deck.name}　主牌 {DeckRepository.GetTotalCount(deck, false)} / 备牌 {DeckRepository.GetTotalCount(deck, true)}");
        }
        if (options.Count == 0) options.Add("尚无可用牌组");
        deckDropdown.AddOptions(options);
        int selectedIndex = selectedDeck == null
            ? 0
            : Mathf.Max(0, availableDecks.FindIndex(item => SameDeckName(item, selectedDeck)));
        deckDropdown.SetValueWithoutNotify(selectedIndex);
        suppressDeckSelection = false;
    }

    private void OnDeckSelected(int index)
    {
        if (suppressDeckSelection || index < 0 || index >= availableDecks.Count) return;
        OnlineSeriesState series = OnlineMatchService.Instance?.LobbyState?.series;
        if (ready || (series != null && series.betweenGames))
        {
            ReloadDeckChoices();
            return;
        }

        selectedDeck = CloneDeck(availableDecks[index]);
        sideboardBaselineDeck = null;
        RefreshDeckSummary();
        RefreshSideboardControls(series);
    }

    internal void BeginSideboardCardDrag(OnlineSideboardCardTile tile, PointerEventData eventData)
    {
        OnlineSeriesState series = OnlineMatchService.Instance?.LobbyState?.series;
        if (tile == null || selectedDeck == null || series == null || !series.betweenGames || ready) return;
        draggedSideboardTile = tile;
        tile.SetDragging(true);

        GameObject ghost = new GameObject("SideboardDragGhost", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        ghost.transform.SetParent(contentGroup.transform, false);
        sideboardDragGhost = ghost.GetComponent<RectTransform>();
        sideboardDragGhost.sizeDelta = tile.GetComponent<RectTransform>().rect.size;
        Image image = ghost.GetComponent<Image>();
        image.sprite = tile.CardSprite;
        image.color = image.sprite == null ? ObsidianArenaTheme.SurfaceRaised : Color.white;
        image.preserveAspect = false;
        CanvasGroup group = ghost.GetComponent<CanvasGroup>();
        group.alpha = 0.82f;
        group.blocksRaycasts = false;
        UpdateSideboardCardDrag(eventData);
    }

    internal void UpdateSideboardCardDrag(PointerEventData eventData)
    {
        if (sideboardDragGhost == null || contentGroup == null || eventData == null) return;
        RectTransform root = contentGroup.GetComponent<RectTransform>();
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            root, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            sideboardDragGhost.anchoredPosition = localPoint;
        }
    }

    internal void EndSideboardCardDrag(OnlineSideboardCardTile tile, PointerEventData eventData)
    {
        if (tile != null && tile == draggedSideboardTile && eventData != null)
        {
            bool droppedOnMain = mainDeckDropArea != null && RectTransformUtility.RectangleContainsScreenPoint(
                mainDeckDropArea, eventData.position, eventData.pressEventCamera);
            bool droppedOnSideboard = sideboardDropArea != null && RectTransformUtility.RectangleContainsScreenPoint(
                sideboardDropArea, eventData.position, eventData.pressEventCamera);
            if (tile.IsSideboard && droppedOnMain) MoveOneCard(tile.Entry, false);
            else if (!tile.IsSideboard && droppedOnSideboard) MoveOneCard(tile.Entry, true);
        }
        ClearSideboardDrag();
    }

    private void MoveOneCard(DeckEntry entry, bool toSideboard)
    {
        OnlineSeriesState series = OnlineMatchService.Instance?.LobbyState?.series;
        if (selectedDeck == null || series == null || !series.betweenGames || ready || entry == null) return;
        List<DeckEntry> source = toSideboard ? selectedDeck.cards : selectedDeck.sideboard;
        List<DeckEntry> destination = toSideboard ? selectedDeck.sideboard : selectedDeck.cards;
        entry = source.Find(item => SamePrinting(item, entry));
        if (entry == null || entry.count <= 0) return;
        DeckEntry destinationEntry = destination.Find(item => SamePrinting(item, entry));
        if (destinationEntry == null)
        {
            destinationEntry = CloneEntry(entry);
            destinationEntry.count = 0;
            destination.Add(destinationEntry);
        }
        destinationEntry.count++;
        entry.count--;
        if (entry.count <= 0) source.Remove(entry);
        DeckCompanion.ClearMissing(selectedDeck);
        RefreshDeckSummary();
        RefreshSideboardControls(series);
    }

    private void ClearSideboardDrag()
    {
        if (draggedSideboardTile != null) draggedSideboardTile.SetDragging(false);
        draggedSideboardTile = null;
        if (sideboardDragGhost != null) Destroy(sideboardDragGhost.gameObject);
        sideboardDragGhost = null;
    }

    private void ResetSideboardChanges()
    {
        if (sideboardBaselineDeck == null || ready) return;
        selectedDeck = CloneDeck(sideboardBaselineDeck);
        RefreshDeckSummary();
        RefreshSideboardControls(OnlineMatchService.Instance?.LobbyState?.series);
    }

    private OnlineRoomConfig ReadRoomConfig()
    {
        return new OnlineRoomConfig
        {
            startingLife = int.TryParse(lifeInput.text, out int life) ? life : 20,
            firstPlayerSeat = 0,
            bestOf = formatDropdown != null && formatDropdown.value == 1 ? 3 : 1,
            formatId = DeckFormatLegality.OnlineKeys[cardFormatDropdown.value],
            minimumMainDeckSize = 60,
            maximumSideboardSize = 15,
            allowCustomContent = customContentToggle != null && customContentToggle.isOn,
            useAnte = anteToggle != null && anteToggle.isOn && OnlineRoomConfig.AllowsAnte(
                DeckFormatLegality.OnlineKeys[cardFormatDropdown.value],customContentToggle != null && customContentToggle.isOn),
            allowedSetCodes = new List<string>()
        };
    }

    private void ToggleReady()
    {
        OnlineMatchService service = OnlineMatchService.Instance;
        if (service == null || !service.HasJoinedLobby || selectedDeck == null) return;
        if (!ready)
        {
            if (service.LobbyState?.series == null || service.LobbyState.series.finished || service.LobbyState.series.gameNumber <= 1) OnlineClientProfileStore.SaveDeck(profileInput.text, selectedDeck);
            service.SubmitDeck(selectedDeck);
        }
        ready = !ready;
        service.SetReady(ready);
        Refresh();
    }

    private void StartMatch()
    {
        OnlineMatchService.Instance?.RequestStartMatch();
    }

    private void Close()
    {
        if (closing) return;
        closing = true;
        OnlineMatchService service = OnlineMatchService.Instance;
        if (service != null && service.IsOnline && !service.IsInMatch) service.Disconnect();
        Unsubscribe();
        if (Application.isPlaying && contentGroup != null)
        {
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(ExitTransition());
            return;
        }
        CompleteClose();
    }

    private void CompleteClose()
    {
        transitionRoutine = null;
        gameObject.SetActive(false);
        owner?.SetOnlineLobbyVisible(false);
        closing = false;
    }

    private void OnStatus(string value)
    {
        if(browserStatus!=null)browserStatus.text=value;
        if (connectionStatusText != null) connectionStatusText.text = value;
        if (statusText != null) statusText.text = value;
        Refresh();
    }

    private void OnLobby(OnlineLobbyState lobby) => Refresh();

    private void LoadProfile(string value)
    {
        OnlineClientProfile profile = OnlineClientProfileStore.Load(value);
        profileInput.SetTextWithoutNotify(profile.id);
        addressInput.SetTextWithoutNotify(profile.address);
        portInput.SetTextWithoutNotify(profile.port.ToString());
        nameInput.SetTextWithoutNotify(profile.displayName);
        roomInput.SetTextWithoutNotify(profile.roomId);
        bool betweenGames=OnlineMatchService.Instance?.LobbyState?.series?.betweenGames==true;
        if(!betweenGames || selectedDeck==null)selectedDeck=CloneDeck(OnlineMatchService.Instance?.LocalRegisteredDeck ?? profile.deck ?? selectedDeck);
        ReloadDeckChoices();
        ready = false;
        Refresh();
    }

    private void OnConnection(bool connected)
    {
        if (!connected) ready = false;
        Refresh();
    }

    private void OnCustomContentChanged(bool value) { UpdateCustomContentLabel(); UpdateAnteLabel(); }

    private void Refresh()
    {
        OnlineMatchService service = OnlineMatchService.Instance;
        bool connected = service != null && service.IsConnected;
        bool joinedRoom = service != null && service.HasJoinedLobby;
        bool online = service != null && service.IsOnline;
        bool disconnecting = service != null && service.IsDisconnecting;
        bool canStartConnection = !online && !disconnecting;
        OnlineLobbyState lobby = service?.LobbyState;
        OnlineSeriesState series = lobby?.series;
        bool betweenGames = joinedRoom && !service.IsSpectator && series != null && series.betweenGames && !series.finished;
        bool roomHost = joinedRoom && lobby != null && lobby.hostSeat == service.LocalSeat;
        bool allPlayersReady = lobby?.players != null
            && lobby.players.Exists(p=>p.seat==1) && lobby.players.Exists(p=>p.seat==2)
            && lobby.players.TrueForAll(player => player.seat>2 || (player.connected && player.hasDeck && player.ready));
        if(!allPlayersReady)sideboardStartRequested=-1;

        SetPageVisible(connectionPageGroup, !joinedRoom && creatingRoom);
        SetPageVisible(browserPageGroup, !joinedRoom && !creatingRoom);
        SetPageVisible(roomPageGroup, joinedRoom && !betweenGames);
        SetPageVisible(sideboardPageGroup, betweenGames);

        if (connected && lobby?.players != null)
        {
            OnlineLobbyPlayer local = lobby.players.Find(item => item != null && item.seat == service.LocalSeat);
            if (local != null) ready = local.ready;
        }

        if (hostButton != null)
        {
            hostButton.interactable = canStartConnection;
            SetButtonLabel(hostButton, serverRoom ? "在服务器建立" : "建立局域网房间");
        }
        if (connectButton != null)
        {
            connectButton.interactable = canStartConnection;
            SetButtonLabel(connectButton, "加入已有主机");
        }
        if (readyButton != null)
        {
            readyButton.interactable = joinedRoom && !service.IsSpectator && selectedDeck != null && !betweenGames;
            string readyLabel = "提交牌组并准备";
            if (series != null && series.finished) readyLabel = "准备开始新比赛";
            if (ready) readyLabel = "取消准备";
            SetButtonLabel(readyButton, readyLabel);
        }
        if (startMatchButton != null)
        {
            startMatchButton.interactable = roomHost && allPlayersReady && !betweenGames;
            SetButtonLabel(startMatchButton, roomHost
                ? allPlayersReady ? "开始比赛" : "等待双方准备"
                : "等待房主开始");
        }
        if (sideboardReadyButton != null)
        {
            sideboardReadyButton.interactable = betweenGames && selectedDeck != null;
            SetButtonLabel(sideboardReadyButton, ready
                ? "取消准备"
                : $"准备第 {series?.gameNumber ?? 1} 局");
        }
        if (sideboardStartButton != null)
        {
            sideboardStartButton.interactable = roomHost && allPlayersReady && betweenGames;
            SetButtonLabel(sideboardStartButton, roomHost
                ? allPlayersReady ? $"开始第 {series?.gameNumber ?? 1} 局" : "等待双方准备"
                : "等待房主开始");
        }

        SetConnectionFieldsInteractable(canStartConnection);
        SetRoomSettingsInteractable(canStartConnection);
        RefreshConnectionBadge(connected);
        RefreshDeckSummary();

        if (lobby?.config != null) ApplyAuthoritativeRoomConfig(lobby.config);
        UpdateCustomContentLabel();
        RefreshRoomSummary(lobby);
        RefreshSideboardControls(series);
        if(betweenGames&&roomHost&&allPlayersReady&&lobby.playDrawChooserSeat==0&&sideboardStartRequested!=series.gameNumber){
            sideboardStartRequested=series.gameNumber;service.RequestStartMatch();
        }
        RefreshSpectatorControls(service);
        RefreshMatchPreparation(service, betweenGames);
        // Create refreshes the panel before Show creates the service and subscribes.
        RefreshPlayerSeats(lobby, service?.LocalSeat ?? 0);
    }
    private void RefreshPlayerSeats(OnlineLobbyState lobby,int localSeat)
    {
        for (int seat = 1; seat <= 2; seat++)
        {
            OnlineLobbyPlayer player = lobby?.players?.Find(item => item != null && item.seat == seat && item.connected);
            var nameBinding=seatNames[seat-1].GetComponent<PreferenceText>()??seatNames[seat-1].gameObject.AddComponent<PreferenceText>();
            nameBinding.Literal=player!=null;
            seatNames[seat - 1].text = player == null ? "等待玩家加入" : player.displayName;
            string role = GamePreferences.T(lobby != null && lobby.hostSeat == seat ? "房主" : "玩家");
            if (player != null && localSeat == seat) role += " · "+GamePreferences.T("你");
            seatStates[seat - 1].text = player == null ? "空位 · 等待连接" :
                role + "   /   " + GamePreferences.T(player.ready ? "已准备" : player.hasDeck ? "牌组已提交 · 未准备" : "正在选择牌组");
            seatStates[seat - 1].color = player != null && player.ready
                ? new Color(.42f, .8f, .7f) : MenuVisualTheme.Muted;
        }
    }

    private void SetConnectionFieldsInteractable(bool interactable)
    {
        if (profileInput != null) profileInput.interactable = interactable;
        if (nameInput != null) nameInput.interactable = interactable;
        if (addressInput != null) addressInput.interactable = interactable;
        if (portInput != null) portInput.interactable = interactable;
        if (roomInput != null) roomInput.interactable = interactable;
    }

    private void SetRoomSettingsInteractable(bool interactable)
    {
        if (lifeInput != null) lifeInput.interactable = interactable;
        if (formatDropdown != null) formatDropdown.interactable = interactable;
        if (cardFormatDropdown != null) cardFormatDropdown.interactable = interactable;
        if (customContentToggle != null) customContentToggle.interactable = interactable;
        UpdateAnteLabel();
    }

    private void RefreshConnectionBadge(bool connected)
    {
        if (connectionBadgeText == null) return;
        connectionBadgeText.text = connected ? "● 已连接" : "● 未连接";
        connectionBadgeText.color = connected ? ObsidianArenaTheme.Success : MutedTextColor;
    }

    private void RefreshDeckSummary()
    {
        if (selectedDeck == null)
        {
            if (deckText != null) deckText.text = "尚未选择牌组";
            if (sideboardDeckText != null) sideboardDeckText.text = "尚未选择牌组";
            return;
        }
        int mainCount = DeckRepository.GetTotalCount(selectedDeck, false);
        int sideboardCount = DeckRepository.GetTotalCount(selectedDeck, true);
        if (deckText != null) deckText.text = $"{selectedDeck.name}\n主牌 {mainCount} · 备牌 {sideboardCount}";
        if (sideboardDeckText != null)
            sideboardDeckText.text = $"{selectedDeck.name}　主牌 {mainCount} · 备牌 {sideboardCount}";
    }

    private void RefreshRoomSummary(OnlineLobbyState lobby)
    {
        if (roomTitleText != null)
            roomTitleText.text = lobby == null || string.IsNullOrWhiteSpace(lobby.roomId)
                ? "房间"
                : $"房间 {lobby.roomId}";

        OnlineRoomConfig config = lobby?.config;
        if (roomRulesText != null)
        {
            if (config == null) roomRulesText.text = string.Empty;
            else
            {
                string format = RoomFormatLabel(config.formatId);
                roomRulesText.text = "BO"+config.bestOf+" · "+DeckCompanion.Text("初始生命 ","Starting life ","初期ライフ ")+config.startingLife
                    +"\n"+format+" · DIY "+PermissionLabel(config.allowCustomContent)+" · "+DeckCompanion.Text("赌注 ","Ante ","アンティ ")+PermissionLabel(config.useAnte);
            }
        }

        if (seriesText == null) return;
        OnlineSeriesState series = lobby?.series;
        if (series == null)
        {
            seriesText.text = "等待比赛开始";
            return;
        }
        if (series.finished)
            seriesText.text = "比赛结束";
        else if (series.betweenGames)
            seriesText.text = $"第 {series.gameNumber} 局换备阶段";
        else
            seriesText.text = $"第 {series.gameNumber} 局";
    }

    private void RefreshSideboardControls(OnlineSeriesState series)
    {
        bool betweenGames = series != null && series.betweenGames && !series.finished;
        if (betweenGames && sideboardGameNumber != series.gameNumber)
        {
            sideboardGameNumber = series.gameNumber;
            sideboardBaselineDeck = CloneDeck(selectedDeck);
            sideboardGridSignature = null;
            resetSideboardScroll = true;
        }
        else if (!betweenGames)
        {
            sideboardGameNumber = -1;
            sideboardBaselineDeck = null;
        }

        if (deckDropdown != null) deckDropdown.interactable = !ready && !betweenGames;
        if (betweenGames && OnlineMatchService.Instance?.IsSpectator != true) RefreshSideboardCardGrids();
        RefreshSideboardQuantityControls();
        if (resetSideboardButton != null)
            resetSideboardButton.interactable = betweenGames && !ready && sideboardBaselineDeck != null;
        if (sideboardStatusText != null)
        {
            sideboardStatusText.text = betweenGames
                ? "选择一张牌，在主牌与备牌之间移动；总卡池必须保持不变。"
                : string.Empty;
            sideboardStatusText.color = betweenGames ? ObsidianArenaTheme.Blue : MutedTextColor;
        }
        if (sideboardSeriesText != null)
        {
            sideboardSeriesText.text = betweenGames
                ? $"第 {series.gameNumber} 局"
                : string.Empty;
        }
        RefreshSideboardCompanion(series);
    }

    private int sideboardStartRequested=-1;
    private void RefreshSideboardCardGrids()
    {
        string signature = JsonUtility.ToJson(selectedDeck);
        if (signature == sideboardGridSignature) return;
        sideboardGridSignature = signature;
        IReadOnlyList<CardDefinition> cardDatabase = CardDatabaseRepository.LoadAll();
        PopulateSideboardGrid(mainDeckContent, selectedDeck?.cards, false, cardDatabase, "主牌为空");
        PopulateSideboardGrid(sideboardContent, selectedDeck?.sideboard, true, cardDatabase, "备牌为空");
        foreach (var content in new[] { mainDeckContent, sideboardContent })
        {
            if(content==null)continue;
            var scroll = content.GetComponentInParent<ScrollRect>();
            if(scroll==null)continue;
            scroll.StopMovement();
            if (resetSideboardScroll) content.anchoredPosition = Vector2.zero;
        }
        resetSideboardScroll = false;
    }

    private void PopulateSideboardGrid(
        RectTransform parent,
        List<DeckEntry> entries,
        bool isSideboard,
        IReadOnlyList<CardDefinition> cardDatabase,
        string emptyLabel)
    {
        if (parent == null) return;
        bool createdAny = false;
        var existing=parent.GetComponentsInChildren<OnlineSideboardCardTile>(true).Where(t=>t.gameObject.activeSelf&&t.transform.parent==parent).ToDictionary(t=>t.name);
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                DeckEntry entry = entries[i];
                CardDefinition card = DeckRepository.ResolveCard(entry, cardDatabase);
                if (entry == null || entry.count <= 0 || card == null) continue;
                string name=$"{(isSideboard ? "Sideboard" : "MainDeck")}_{entry.PrintingKey}";
                if(existing.TryGetValue(name,out var tile)) {
                    existing.Remove(name);tile.Configure(this,entry,card,tile.CardSprite,isSideboard);
                    tile.transform.Find("CountBadge/Count").GetComponent<Text>().text=$"×{entry.count}";
                    tile.transform.SetAsLastSibling();
                }else CreateSideboardCardTile(parent, entry, card, isSideboard);
                createdAny = true;
            }
        }
        foreach(var tile in existing.Values){tile.gameObject.SetActive(false);Destroy(tile.gameObject);}
        foreach(Transform child in parent)if(child.name=="Empty"){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        if (createdAny) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Image empty = AddPanel(parent, "Empty", Vector2.zero, new Vector2(112f, 157f),
            ObsidianArenaTheme.Field);
        AddText(empty.transform, "Label", emptyLabel, Vector2.zero, new Vector2(94f, 80f),
            15, font, TextAnchor.MiddleCenter, MutedTextColor);
    }

    private void CreateSideboardCardTile(
        RectTransform parent,
        DeckEntry entry,
        CardDefinition card,
        bool isSideboard)
    {
        GameObject tileObject = new GameObject(
            $"{(isSideboard ? "Sideboard" : "MainDeck")}_{entry.PrintingKey}",
            typeof(RectTransform), typeof(Image), typeof(Outline), typeof(OnlineSideboardCardTile));
        tileObject.transform.SetParent(parent, false);
        RectTransform rect = tileObject.GetComponent<RectTransform>();
        GridLayoutGroup grid = parent.GetComponent<GridLayoutGroup>();
        rect.sizeDelta = grid != null ? grid.cellSize : new Vector2(112f, 157f);

        Image face = tileObject.GetComponent<Image>();
        // Reuse the art already displayed in the other zone when moving a printing.
        Sprite sprite = GetComponentsInChildren<OnlineSideboardCardTile>()
            .FirstOrDefault(t => SamePrinting(t.Entry, entry) && t.CardSprite != null)?.CardSprite;
        if(sprite==null&&CardImageLibrary.TryReuseFullCard(card,out var reused))sprite=reused;
        if (sprite == null && CardImageLibrary.TryLoadFullCard(card, out Sprite loaded)) sprite = loaded;
        face.sprite = sprite;
        face.color = sprite == null ? ObsidianArenaTheme.SurfaceRaised : Color.white;
        face.preserveAspect = false;
        face.raycastTarget = true;

        Outline outline = tileObject.GetComponent<Outline>();
        outline.effectColor = ObsidianArenaTheme.LineSoft;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (sprite == null)
        {
            AddText(tileObject.transform, "Fallback", $"{card.cardName}\n{card.manaCost}", Vector2.zero,
                rect.sizeDelta - new Vector2(16f, 20f), 13, font, TextAnchor.MiddleCenter, TextColor);
        }

        Image countBadge = AddPanel(tileObject.transform, "CountBadge",
            new Vector2(rect.sizeDelta.x * 0.32f, -rect.sizeDelta.y * 0.39f),
            new Vector2(34f, 26f), new Color(0f, 0f, 0f, 0.82f));
        AddText(countBadge.transform, "Count", $"×{entry.count}", Vector2.zero,
            new Vector2(34f, 26f), 15, font, TextAnchor.MiddleCenter, Color.white);

        var tile = tileObject.GetComponent<OnlineSideboardCardTile>();
        tile.Configure(this, entry, card, sprite, isSideboard);
        BuildSideboardQuantityControls(tile, countBadge.gameObject, rect.sizeDelta);
        BuildSideboardCompanionControl(tileObject.transform,entry,card,isSideboard,rect.sizeDelta);
    }

    private static void ClearGrid(RectTransform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    private void ApplyAuthoritativeRoomConfig(OnlineRoomConfig config)
    {
        if (config == null) return;
        lifeInput?.SetTextWithoutNotify(config.startingLife.ToString());
        cardFormatDropdown?.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(DeckFormatLegality.OnlineKeys, config.formatId ?? "none")));
        customContentToggle?.SetIsOnWithoutNotify(config.allowCustomContent);
        anteToggle?.SetIsOnWithoutNotify(config.useAnte);
        UpdateAnteLabel();
        formatDropdown?.SetValueWithoutNotify(config.bestOf == 3 ? 1 : 0);
    }

    private void UpdateCustomContentLabel()
    {
        if (customContentStateText == null || customContentToggle == null) return;
        customContentStateText.text = customContentToggle.isOn ? "已允许" : "已禁用";
        customContentStateText.color = customContentToggle.isOn
            ? ObsidianArenaTheme.Blue
            : MutedTextColor;
    }

    private void RefreshLanguage()
    {
        UpdateAnteLabel();
        SetLocalizedOptions(formatDropdown, new[] { "BO1 · 一局胜负", "BO3 · 三局两胜" });
        var names = new string[DeckFormatLegality.OnlineKeys.Length];
        for (int i = 0; i < names.Length; i++) names[i] = RoomFormatLabel(DeckFormatLegality.OnlineKeys[i]);
        SetLocalizedOptions(cardFormatDropdown, names);
        Refresh();
        if(roomList!=null)DrawBrowserRooms();
    }
    private static string RoomFormatLabel(string key) => key=="none"
        ? "N/A"
        : GamePreferences.T(DeckFormatLegality.FormatName(key));

    private void UpdateAnteLabel()
    {
        if(anteToggle==null)return;
        bool allowed=cardFormatDropdown!=null && cardFormatDropdown.value<DeckFormatLegality.OnlineKeys.Length
            && OnlineRoomConfig.AllowsAnte(DeckFormatLegality.OnlineKeys[cardFormatDropdown.value],customContentToggle?.isOn==true);
        if(!allowed)anteToggle.SetIsOnWithoutNotify(false);
        anteToggle.interactable=allowed&&cardFormatDropdown.interactable;
        string language=GamePreferences.Language;
        void Set(string path,string zh,string en,string ja) {
            var label=anteToggle.transform.Find(path).GetComponent<Text>();
            var binding=label.GetComponent<PreferenceText>()??label.gameObject.AddComponent<PreferenceText>();binding.Literal=true;
            label.text=language=="zh-CN"?zh:language=="ja-JP"?ja:en;
        }
        Set("Label","赌注（仅本场比赛）","Ante (this match only)","アンティ（このマッチのみ）");
        if(allowed)Set("Description","归属变化影响 BO3 后续局，不修改保存的牌组或收藏。","Ownership changes carry into later BO3 games; saved decks and collection stay unchanged.","所有権の変更はBO3の次ゲームに引き継ぎます。保存デッキとコレクションは変更しません。");
        else Set("Description","仅 N/A 赛制且允许 DIY 时可开启。","Requires N/A format and DIY enabled.","N/A形式でDIYを許可した場合のみ有効にできます。");
        Set("State",anteToggle.isOn?"已允许":"已禁用",anteToggle.isOn?"Enabled":"Disabled",anteToggle.isOn?"有効":"無効");
        RefreshAnteAvailabilityVisual();
    }
    private void RefreshAnteAvailabilityVisual()
    {
        if(anteToggle==null)return;
        bool available=anteToggle.IsInteractable();
        var background=anteToggle.transform.Find("Background")?.GetComponent<Image>();
        if(background==null)return;
        background.color=available?new Color(.10f,.16f,.23f):new Color(.12f,.12f,.13f);
        ObsidianArenaTheme.EnsureOutline(background,available?new Color(.65f,.70f,.78f):new Color(.38f,.38f,.40f),new Vector2(2,-2));
        if(anteToggle.graphic!=null)anteToggle.graphic.color=available?LobbyGold:new Color(.45f,.45f,.47f);
    }
    private static void SetLocalizedOptions(Dropdown dropdown, string[] names)
    {
        if (dropdown == null) return;
        int selected = dropdown.value;
        dropdown.Hide(); dropdown.ClearOptions();
        var options = new List<string>();
        foreach (string name in names) options.Add(GamePreferences.T(name));
        dropdown.AddOptions(options); dropdown.SetValueWithoutNotify(selected); dropdown.RefreshShownValue();
        dropdown.captionText.verticalOverflow = VerticalWrapMode.Overflow;
        dropdown.captionText.resizeTextForBestFit = true;
        dropdown.captionText.resizeTextMinSize = 14; dropdown.captionText.resizeTextMaxSize = 23;
    }

    private void PlayEnterTransition()
    {
        if (contentGroup == null) return;
        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        if (!Application.isPlaying)
        {
            contentGroup.alpha = 1f;
            contentGroup.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            return;
        }
        transitionRoutine = StartCoroutine(EnterTransition());
    }

    private IEnumerator EnterTransition()
    {
        RectTransform content = contentGroup.GetComponent<RectTransform>();
        const float duration = 0.18f;
        float elapsed = 0f;
        contentGroup.alpha = 0f;
        content.anchoredPosition = new Vector2(36f, 0f);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            contentGroup.alpha = eased;
            content.anchoredPosition = new Vector2(Mathf.Lerp(36f, 0f, eased), 0f);
            yield return null;
        }
        contentGroup.alpha = 1f;
        content.anchoredPosition = Vector2.zero;
        transitionRoutine = null;
    }

    private IEnumerator ExitTransition()
    {
        RectTransform content = contentGroup.GetComponent<RectTransform>();
        const float duration = 0.14f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = progress * progress;
            contentGroup.alpha = 1f - eased;
            content.anchoredPosition = new Vector2(Mathf.Lerp(0f, 24f, eased), 0f);
            yield return null;
        }
        CompleteClose();
    }

    private static bool SameDeckName(DeckDefinition left, DeckDefinition right)
    {
        if(!string.IsNullOrEmpty(left?.sourcePath)&&!string.IsNullOrEmpty(right?.sourcePath))return string.Equals(left.sourcePath,right.sourcePath,StringComparison.OrdinalIgnoreCase);
        return left != null && right != null
            && string.Equals(left.name, right.name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SamePrinting(DeckEntry left, DeckEntry right)
    {
        if (left == null || right == null) return false;
        if (!string.IsNullOrWhiteSpace(left.PrintingKey) || !string.IsNullOrWhiteSpace(right.PrintingKey))
            return string.Equals(left.PrintingKey, right.PrintingKey, StringComparison.OrdinalIgnoreCase);
        return left.cardId == right.cardId;
    }

    private static DeckEntry CloneEntry(DeckEntry source)
    {
        return source == null ? null : JsonUtility.FromJson<DeckEntry>(JsonUtility.ToJson(source));
    }

    private static void AddFieldLabel(Transform parent, string name, string value, Vector2 position, Font font)
    {
        AddText(parent, name, value, position, new Vector2(180f, 34f), 17, font,
            TextAnchor.MiddleLeft, MutedTextColor);
    }

    private static Image AddPanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), position, size);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        if (name != "HeaderBlueRule" && name != "HeaderRedRule" && name != "Divider")
            ObsidianArenaTheme.EnsureOutline(image, ObsidianArenaTheme.LineSoft, new Vector2(1f, -1f));
        return image;
    }

    private static Text AddText(
        Transform parent,
        string name,
        string value,
        Vector2 position,
        Vector2 size,
        int fontSize,
        Font font,
        TextAnchor alignment,
        Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        Place(rect, position, size);
        Text text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.text = value;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        PreferenceText.SetSize(text, fontSize);
        return text;
    }

    private static InputField AddInput(
        Transform parent,
        string name,
        string value,
        Vector2 position,
        Vector2 size,
        Font font,
        string placeholder)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), position, size);
        Image background = go.GetComponent<Image>();
        background.color = FieldColor;

        Text text = AddText(go.transform, "Text", value, Vector2.zero, size - new Vector2(26f, 8f),
            19, font, TextAnchor.MiddleLeft, TextColor);
        Text placeholderText = AddText(go.transform, "Placeholder", placeholder, Vector2.zero,
            size - new Vector2(26f, 8f), 17, font, TextAnchor.MiddleLeft,
            new Color(MutedTextColor.r, MutedTextColor.g, MutedTextColor.b, 0.62f));
        placeholderText.fontStyle = FontStyle.Italic;

        InputField input = go.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholderText;
        input.text = value;
        input.customCaretColor = true;
        input.caretColor = TextColor;
        ObsidianArenaTheme.StyleField(input);
        return input;
    }

    private static RectTransform AddScrollGrid(
        Transform parent,
        string name,
        string contentName,
        Vector2 position,
        Vector2 size,
        Vector2 cellSize,
        Vector2 spacing,
        out RectTransform dropArea)
    {
        Image shell = AddPanel(parent, name, position, size, ObsidianArenaTheme.Field);
        dropArea = shell.rectTransform;
        ScrollRect scroll = shell.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(shell.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewportRect.offsetMin = new Vector2(10f, 10f);
        viewportRect.offsetMax = new Vector2(-10f, -10f);
        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        viewportImage.raycastTarget = true;

        GameObject content = new GameObject(
            contentName, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = cellSize;
        grid.spacing = spacing;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, Mathf.FloorToInt((size.x - 28f) / (cellSize.x + spacing.x)));

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        return contentRect;
    }

    private static Dropdown AddDropdown(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Font font,
        List<string> options)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Dropdown));
        root.transform.SetParent(parent, false);
        Place(root.GetComponent<RectTransform>(), position, size);
        Image background = root.GetComponent<Image>();
        background.color = FieldColor;
        ObsidianArenaTheme.EnsureOutline(background, ObsidianArenaTheme.LineSoft, new Vector2(1f, -1f));

        Text caption = AddText(root.transform, "Label", string.Empty, new Vector2(-18f, 0f),
            size - new Vector2(60f, 8f), 17, font, TextAnchor.MiddleLeft, TextColor);
        Text arrow = AddText(root.transform, "Arrow", "▾", new Vector2(size.x * 0.5f - 25f, 0f),
            new Vector2(40f, size.y), 22, font, TextAnchor.MiddleCenter, MutedTextColor);
        arrow.raycastTarget = false;

        GameObject template = new GameObject("Template", typeof(RectTransform), typeof(Image),
            typeof(ScrollRect), typeof(Canvas));
        template.transform.SetParent(root.transform, false);
        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, -2f);
        templateRect.sizeDelta = new Vector2(0f, 210f);
        template.GetComponent<Image>().color = ObsidianArenaTheme.SurfaceRaised;
        Canvas templateCanvas = template.GetComponent<Canvas>();
        templateCanvas.overrideSorting = true;
        templateCanvas.sortingOrder = 20;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(template.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.GetComponent<Image>().color = ObsidianArenaTheme.SurfaceRaised;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, size.y);

        GameObject item = new GameObject("Item", typeof(RectTransform), typeof(Image), typeof(Toggle));
        item.transform.SetParent(content.transform, false);
        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 0.5f);
        itemRect.anchorMax = new Vector2(1f, 0.5f);
        itemRect.sizeDelta = new Vector2(0f, size.y);
        Text itemText = AddText(item.transform, "Item Label", "Option", Vector2.zero,
            new Vector2(size.x - 24f, size.y), 17, font, TextAnchor.MiddleLeft, TextColor);
        itemText.rectTransform.anchorMin = Vector2.zero;
        itemText.rectTransform.anchorMax = Vector2.one;
        itemText.rectTransform.offsetMin = new Vector2(40f, 2f);
        itemText.rectTransform.offsetMax = new Vector2(-12f, -2f);
        Text selectedMark = AddText(item.transform, "Checkmark", "✓", Vector2.zero,
            new Vector2(24f, 30f), 18, font, TextAnchor.MiddleCenter, TextColor);
        selectedMark.rectTransform.anchorMin = selectedMark.rectTransform.anchorMax = new Vector2(0f, .5f);
        selectedMark.rectTransform.anchoredPosition = new Vector2(20f, 0f);
        Toggle itemToggle = item.GetComponent<Toggle>();
        itemToggle.targetGraphic = item.GetComponent<Image>();
        itemToggle.graphic = selectedMark;

        ScrollRect scroll = template.GetComponent<ScrollRect>();
        scroll.content = contentRect;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.horizontal = false;
        scroll.scrollSensitivity = 30;

        Dropdown dropdown = root.GetComponent<Dropdown>();
        dropdown.targetGraphic = background;
        dropdown.template = templateRect;
        dropdown.captionText = caption;
        dropdown.itemText = itemText;
        dropdown.options.Clear();
        dropdown.AddOptions(options ?? new List<string>());
        DropdownChevron.Apply(dropdown);
        template.SetActive(false);
        return dropdown;
    }

    private static Button AddButton(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        Vector2 size,
        Font font,
        Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), position, size);
        Image image = go.GetComponent<Image>();
        image.color = color;
        Button button = go.GetComponent<Button>();
        ObsidianArenaTheme.StyleButton(button, color);
        AddText(go.transform, "Label", label, Vector2.zero, size, 20, font,
            TextAnchor.MiddleCenter, TextColor);
        return button;
    }

    private static Toggle AddToggle(
        Transform parent,
        string name,
        string label,
        string description,
        Vector2 position,
        Vector2 size,
        Font font,
        out Text stateText)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Toggle));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), position, size);
        go.GetComponent<Image>().color = FieldColor;

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(go.transform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(34f, 34f);
        backgroundRect.anchoredPosition = new Vector2(34f, 0f);
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.color = ObsidianArenaTheme.SurfaceRaised;
        ObsidianArenaTheme.EnsureOutline(backgroundImage, ObsidianArenaTheme.Line, new Vector2(1f, -1f));

        Text checkmark = AddText(background.transform, "Checkmark", "✓", Vector2.zero,
            new Vector2(34f, 34f), 25, font, TextAnchor.MiddleCenter, ObsidianArenaTheme.Blue);
        AddText(go.transform, "Label", label, new Vector2(92f, 12f), new Vector2(350f, 30f),
            19, font, TextAnchor.MiddleLeft, TextColor);
        AddText(go.transform, "Description", description, new Vector2(252f, -17f),
            new Vector2(670f, 26f), 14, font, TextAnchor.MiddleLeft, MutedTextColor);
        stateText = AddText(go.transform, "State", "已禁用", new Vector2(323f, 0f),
            new Vector2(110f, 32f), 17, font, TextAnchor.MiddleRight, MutedTextColor);

        Toggle toggle = go.GetComponent<Toggle>();
        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkmark;
        toggle.toggleTransition = Toggle.ToggleTransition.Fade;
        toggle.SetIsOnWithoutNotify(false);
        return toggle;
    }

    private static void SetButtonLabel(Button button, string value)
    {
        Text label = button != null ? button.GetComponentInChildren<Text>(true) : null;
        if (label != null) label.text = value;
    }

    private static void SetPageVisible(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
        group.gameObject.SetActive(visible);
    }

    private static List<string> ParseSetCodes(string value)
    {
        var result = new List<string>();
        string[] values = (value ?? string.Empty).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < values.Length; i++)
        {
            string normalized = values[i].Trim().ToUpperInvariant();
            if (normalized.Length > 0 && !result.Contains(normalized)) result.Add(normalized);
        }
        return result;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static DeckDefinition CloneDeck(DeckDefinition source)
    {
        if(source==null)return null;
        var clone=JsonUtility.FromJson<DeckDefinition>(JsonUtility.ToJson(source));clone.sourcePath=source.sourcePath;return clone;
    }
}
