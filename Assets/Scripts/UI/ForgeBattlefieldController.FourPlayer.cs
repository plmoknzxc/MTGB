using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The dedicated four-seat scene keeps the ordinary duel layout untouched.
// This is presentation only: the match service still needs four participant
// seats and a four-player Forge session before it can be entered from a room.
public sealed partial class ForgeBattlefieldController
{
    public const string FourPlayerSceneName="IntegratedBattlefieldCommander4";
    private bool IsFourPlayerScene=>SceneManager.GetActiveScene().name==FourPlayerSceneName;
    private bool fourPlayerScenePrepared;
    private readonly Dictionary<int,RectTransform> fourStatusRows=new();
    private readonly Dictionary<int,Button> fourStatusTabs=new();
    private readonly Dictionary<int,List<SidebarMarkerEntry>> fourStatusEntries=new();
    private readonly Dictionary<int,int> fourStatusCounts=new();
    private readonly Dictionary<int,string> fourStatusCounterText=new();
    private int fourStatusSelectedSeat;
    private static readonly Color[] FourSeatColors={
        new Color(93/255f,185/255f,245/255f), // local blue
        new Color(241/255f,124/255f,124/255f), // clockwise rival
        new Color(.72f,.59f,.96f), // seat 3: violet
        new Color(.70f,.83f,.32f)  // seat 4: lime
    };
    private Color FourSeatColor(int playerSeat) {
        int origin=service?.IsSpectator==true?1:Mathf.Clamp(seat,1,4);
        return playerSeat is >=1 and <=4 ? FourSeatColors[(playerSeat-origin+4)%4] : HudMuted;
    }

    private void PrepareFourPlayerScene(bool enabled)
    {
        if(!enabled) {
            if(fourPlayerScenePrepared) {
                Node("OpponentPlate").gameObject.SetActive(true);
                Node("ZonePiles_OpponentTopRight").gameObject.SetActive(true);
                otherHand.gameObject.SetActive(true);
                var shell=authored.Find("OpponentPlateWithZones");if(shell!=null)shell.gameObject.SetActive(true);
                foreach(Transform child in authored)
                    if(child.name.StartsWith("FourSeat_",StringComparison.Ordinal)||child.name=="FourLocalPager")child.gameObject.SetActive(false);
                if(fourTargetList!=null)fourTargetList.gameObject.SetActive(false);
                if(fourWindowEdges!=null)fourWindowEdges.gameObject.SetActive(false);
                fourPlayerScenePrepared=false;
            }
            return;
        }
        fourFieldLocations.Clear();
        foreach(var page in fourWindowPages)page.count=0;
        fourWindowMode=GamePreferences.ResizableFourSeats;
        bool firstFourSeatFrame=!fourPlayerScenePrepared;
        fourPlayerScenePrepared=true;
        // Cached plates move when the viewer changes seats. Hide any seat that
        // is no longer an opponent before its old buttons can receive input.
        for(int playerSeat=1;playerSeat<=4;playerSeat++){
            if(playerSeat!=seat && view?.players?.Any(p=>p.seat==playerSeat)==true)continue;
            authored.Find("FourSeat_"+playerSeat)?.gameObject.SetActive(false);
        }
        Node("OpponentPlate").gameObject.SetActive(false);
        Node("ZonePiles_OpponentTopRight").gameObject.SetActive(false);
        otherHand.gameObject.SetActive(false);
        opponentFan.slots.Clear();
        if(redesignOtherBackplate!=null)redesignOtherBackplate.gameObject.SetActive(false);
        if(otherSourceTabs!=null)otherSourceTabs.gameObject.SetActive(false);
        var oldShell=authored.Find("OpponentPlateWithZones");if(oldShell!=null)oldShell.gameObject.SetActive(false);
        // The duel journal's single "opponent" slot would combine or stale
        // three independent players. Their status is reachable from each plate.
        if(sidebarOpponentStatusTab!=null)sidebarOpponentStatusTab.gameObject.SetActive(false);
        if(firstFourSeatFrame){sidebarCollapsed=true;sidebarSlideProgress=1;}
        if(firstFourSeatFrame||fourStatusSelectedSeat==0)SelectFourPlayerStatusSeat(seat);
    }

    private void FinishFourPlayerScene()
    {
        LayoutFourWindowEdges();
        PresentFourTargetList();
        PresentLokiPriority();
        otherHand.gameObject.SetActive(false);
        if(redesignOtherBackplate!=null)redesignOtherBackplate.gameObject.SetActive(false);
        if(otherSourceTabs!=null)otherSourceTabs.gameObject.SetActive(false);
        if(sidebarOpponentStatusTab!=null)sidebarOpponentStatusTab.gameObject.SetActive(false);
    }

    private RectTransform lokiPriorityPanel;
    private Text lokiPriorityOwner, lokiPriorityHint;
    private Button lokiVoteButton;

    private void PresentLokiPriority()
    {
        bool visible = view?.commanderEvent == OnlineRoomConfig.Loki && view.turn > 0 && view.prioritySeat > 0;
        if (!visible)
        {
            if (lokiPriorityPanel != null) lokiPriorityPanel.gameObject.SetActive(false);
            return;
        }
        if (lokiPriorityPanel == null)
        {
            lokiPriorityPanel = Panel(Node("CornerCluster/ControlRow"), "LokiPriority", Vector2.zero, new Vector2(240, 56), HudInk);
            lokiPriorityPanel.GetComponent<Image>().raycastTarget = false;
            lokiPriorityOwner = FourSeatCaption(lokiPriorityPanel, "PriorityOwner", 10, 12, 220, 25, 18);
            lokiPriorityHint = FourSeatCaption(lokiPriorityPanel, "PriorityHint", 10, -13, 220, 23, 16);
            lokiVoteButton = Button(lokiPriorityPanel, "HeroStepVote", "", new Vector2(0, -26),
                new Vector2(200, 38), () => service?.SetHeroStepReady((view.heroVotes & (1 << (seat - 1))) == 0), HudInk);
            PreferenceText.SetSize(lokiVoteButton.GetComponentInChildren<Text>(), 19);
        }
        lokiPriorityPanel.gameObject.SetActive(true);
        bool ballot = view.heroVoteWindow != 0;
        int bit = 1 << (seat - 1);
        bool canVote = ballot && (view.heroVoters & bit) != 0 && submit != null && service?.IsSpectator == false;
        float extra = canVote ? 46 : 0;
        lokiPriorityPanel.sizeDelta = new Vector2(224, 56 + extra);
        float dockTop = phaseDockBacking != null ? phaseDockBacking.anchoredPosition.y + phaseDockBacking.rect.yMax : PhaseDockTop;
        if (undoManaButton != null && undoManaButton.gameObject.activeSelf)
        {
            var undo = (RectTransform)undoManaButton.transform;
            dockTop = Mathf.Max(dockTop, undo.anchoredPosition.y + undo.rect.yMax);
        }
        lokiPriorityPanel.anchoredPosition = new Vector2(36, dockTop + 6 + lokiPriorityPanel.rect.height / 2);
        FourSeatPlace(lokiPriorityOwner.rectTransform, 12, 12 + extra / 2, 200, 25);
        FourSeatPlace(lokiPriorityHint.rectTransform, 12, -13 + extra / 2, 200, 23);
        lokiVoteButton.gameObject.SetActive(canVote);
        if (canVote)
        {
            bool locked = view.heroVoters == view.heroVotes;
            bool ready = (view.heroVotes & bit) != 0;
            MenuVisualTheme.StyleDeckTool(lokiVoteButton, primary: !ready);
            lokiVoteButton.interactable = !locked && service?.CanSubmitRuleAction == true;
            lokiVoteButton.GetComponentInChildren<Text>().text = locked ? TapPaymentText("全员已确认","All confirmed","全員確認済み")
                : ready ? TapPaymentText("取消结束","Cancel vote","終了を取り消す")
                : (view.phase == "MAIN1" || view.phase == "MAIN2") ? TapPaymentText("结束主阶段","End main phase","メイン終了")
                : TapPaymentText("结束当前步骤","End step","ステップ終了");
        }
        string teamTurn = view.turnSeat == view.lokiSeat
            ? TapPaymentText("洛基回合", "Loki’s turn", "ロキのターン")
            : TapPaymentText("英雄回合", "Heroes’ turn", "英雄のターン");
        SetNamedPlayerText(SceneText("CornerCluster/ControlRow/TurnChip/TurnLabel"), teamTurn);
        lokiPriorityOwner.color = FourSeatColor(view.prioritySeat);
        lokiPriorityOwner.text = TapPaymentText("优先权 · ", "Priority · ", "優先権 · ") + PlayerDisplayName(view.prioritySeat);
        if (view.heroCombatSeat > 0)
        {
            string action = view.heroCombatChoice == "attack" ? TapPaymentText("宣告攻击","Attackers","攻撃指定")
                : view.heroCombatChoice == "block" ? TapPaymentText("宣告阻挡","Blockers","ブロック指定")
                : TapPaymentText("分配伤害","Assign damage","ダメージ割り振り");
            lokiPriorityOwner.text = action + " · " + PlayerDisplayName(view.heroCombatSeat);
            lokiPriorityOwner.color = FourSeatColor(view.heroCombatSeat);
            lokiPriorityHint.text = TapPaymentText("等待战斗选择完成","Awaiting combat choice","戦闘の選択を待機中");
        }
        else if (ballot)
        {
            int count = view.players.Count(p => (view.heroVotes & (1 << (p.seat - 1))) != 0);
            int total = view.players.Count(p => (view.heroVoters & (1 << (p.seat - 1))) != 0);
            lokiPriorityHint.text = TapPaymentText("英雄确认结束 ","Heroes ready ","終了に同意 ") + count + " / " + total;
        }
        else if (view.heroAfterStackSeat > 0)
            lokiPriorityHint.text = TapPaymentText("清栈后 · ", "After stack · ", "スタック解決後 · ") + PlayerDisplayName(view.heroAfterStackSeat);
        else
            lokiPriorityHint.text = view.stackCount > 0
                ? TapPaymentText("全员让过后结算栈顶", "All pass to resolve the top", "全員パスで一番上を解決")
                : TapPaymentText("全员让过后推进步骤", "All pass to advance", "全員パスで次へ");
    }

    private const int FourSeatColumns = 4;
    private const int FourSeatPageSize = FourSeatColumns * 2;
    private readonly Dictionary<int, (int seat, int page, ForgeCardView card)> fourFieldLocations = new();
    private RectTransform fourTargetList, fourTargetContent;

    private void PresentFourPlayerTable()
    {
        foreach(var page in fourWindowPages)page.plate=null;
        foreach(var player in view.players){
            var page=fourWindowPages[(player.seat-seat+4)%4];
            page.own=player.seat==seat;
            page.plate=page.own?FourLocalPager():FourPlayerPlate(player);
            page.plate.gameObject.SetActive(true);
            if(!page.own)PresentFourSeatControls(page.plate,player);
        }
        FitFourSeatHeaders();
        foreach(var player in view.players)PresentFourPlayerSeat(player,player.seat==seat);
    }

    private void PresentFourPlayerSeat(ForgePlayerView player, bool own)
    {
        int slot = (player.seat - seat + 4) % 4;
        var bounds = FourSeatBounds(slot);
        var pageView = fourWindowPages[slot];
        string rowKey = (own ? "YourSeat" : "OpponentSeat") + player.seat;
        var field = ArenaVisualGroups(player.battlefield ?? Array.Empty<ForgeCardView>(), rowKey);
        string key = "four-seat:" + player.seat;
        var plate=pageView.plate;
        float header=plate.rect.height;
        int capacity=FourSeatCapacity(bounds,header);
        // Each page keeps separate creature and support ranks, including short pages.
        var front = new Queue<ForgeCardView[]>(field.Where(g => g[0].creature));
        var back = new Queue<ForgeCardView[]>(field.Where(g => !g[0].creature).OrderByDescending(g => g[0].land));
        var groups = new List<ForgeCardView[]>(field.Count);
        while (front.Count + back.Count > 0) {
            int takeFront=Math.Min(front.Count,Math.Max((capacity+1)/2,capacity-back.Count));
            int takeBack=Math.Min(back.Count,capacity-takeFront);
            for(int i=0;i<takeFront;i++)groups.Add(front.Dequeue());
            for(int i=0;i<takeBack;i++)groups.Add(back.Dequeue());
        }
        int last = Mathf.Max(0, (groups.Count - 1) / capacity);
        int saved=pages.GetValueOrDefault(key);
        if(fourSeatCapacities.TryGetValue(player.seat,out int oldCapacity) && capacity!=oldCapacity)
            saved=saved*oldCapacity/capacity;
        int page=Mathf.Clamp(saved,0,last);
        if(pageView.pageIndex!=page||pageView.seat!=player.seat||!fourWindowMode)pageView.pan=Vector2.zero;
        pageView.pageIndex=page;pageView.seat=player.seat;
        pageView.pan=ClampFourPan(slot,pageView.pan);
        fourSeatCapacities[player.seat]=capacity;
        pageView.capacity=capacity;
        pages[key] = page;
        if (own) {
            plate.Find("SeatAccent").GetComponent<Image>().color=FourSeatColor(seat);
            var title = plate.Find("Name").GetComponent<Text>();
            title.color=FourSeatColor(seat);
            title.text = player.eliminated ? TapPaymentText("已出局","Eliminated","敗退") : TapPaymentText("我的战场", "Your battlefield", "自分の戦場");
            if(!player.eliminated && view.commanderEvent==OnlineRoomConfig.Loki)title.text=LokiRole(player.seat);
            plate.Find("Count").GetComponent<Text>().text = (player.battlefield?.Length ?? 0).ToString();
        } else {
            plate.Find("SeatAccent").GetComponent<Image>().color=FourSeatColor(player.seat);
            plate.Find("Life").GetComponent<Text>().text = player.life.ToString();
            plate.Find("Avatar").GetComponent<PlayerAvatarGraphic>().SetAvatar(FourPlayerPortrait(player.seat));
            int targetIndex = IsDeclaringAttack ? AttackOption("player",player.targetId) : OptionIndex("player:"+player.targetId);
            var target = plate.GetComponent<Button>();
            ShowSelectedPlayer(plate,targetIndex);
            target.interactable = false;
        }
        var pageText = plate.Find("Page").GetComponent<Text>();
        pageText.text = (page+1) + " / " + (last+1);
        pageText.gameObject.SetActive(last>0);
        foreach (int delta in new[]{-1,1}) {
            var button = plate.Find(delta < 0 ? "Previous" : "Next").GetComponent<Button>();
            button.gameObject.SetActive(last>0);
            button.interactable = delta<0 ? page>0 : page<last;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(()=>{pages[key]=page+delta;PresentIntegrated(connectionText.text);});
        }
        int first = page * capacity;
        int shown = Mathf.Min(capacity,groups.Count-first);
        int frontCount = groups.Skip(first).Take(shown).Count(g => g[0].creature);
        pageView.count = shown;
        pageView.frontCount = frontCount;
        var grid = new FourSeatGrid(bounds,slot,frontCount,shown,fourWindowMode,header);
        var clip=FourPanViewport(bounds,pageView);
        for(int i=0;i<groups.Count;i++) {
            var group=groups[i];
            foreach(var member in group)
                fourFieldLocations[member.id]=(player.seat,i/capacity,member);
            if(i<first || i>=first+shown)continue;
            int local=i-first;
            var card=group[0];
            string cardKey=rowKey+":"+card.id;
            DrawArenaCard(cardsRoot,card,group.Length,cardKey,rowKey+":"+ArenaPresentationGroupKey(card),
                grid.Position(local)+pageView.pan,grid.cell,false,card.tapped?12:0,local);
            pageView.items[local]=arenaCards[cardKey];
            if(pageView.items[local].world!=null)pageView.items[local].world.SeatViewport=clip;
        }
    }

    private RectTransform FourLocalPager()
    {
        var existing=authored.Find("FourLocalPager") as RectTransform;
        if(existing!=null)return existing;
        var bar=Panel(authored,"FourLocalPager",Vector2.zero,Vector2.one,HudInk);
        bar.GetComponent<Image>().raycastTarget=false;
        FourSeatStripe(bar,FourSeatColor(seat));
        FourSeatCaption(bar,"Name",18,0,130,36,24);
        FourSeatCaption(bar,"Count",148,0,46,36,20).color=HudMuted;
        FourSeatCaption(bar,"Tax",184,0,140,36,15).gameObject.SetActive(false);
        AddFourPageControls(bar,0,0);
        return bar;
    }

    private void AddFourPageControls(RectTransform host,float x,float y)
    {
        var label=Label(host,"Page","",new Vector2(x,y),new Vector2(64,28),17,Paper);
        label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
        foreach(int delta in new[]{-1,1}) {
            var btn=Button(host,delta<0?"Previous":"Next",delta<0?"‹":"›",new Vector2(x+delta*50,y),new Vector2(34,30),()=>{},Ink);
            MenuVisualTheme.StyleDeckTool(btn);
        }
    }

    private static void FourSeatPlace(RectTransform item, float left, float y, float width, float height)
    {
        var edge = new Vector2(0,.5f);
        RedesignPlace(item,edge,edge,new Vector2(left,y),new Vector2(width,height));
    }

    private Text FourSeatCaption(RectTransform host,string name,float x,float y,float width,float height,int size)
    {
        var label=Label(host,name,"",Vector2.zero,Vector2.one,size,HudText);
        FourSeatPlace(label.rectTransform,x,y,width,height);
        label.alignment=TextAnchor.MiddleLeft;
        label.raycastTarget=false;
        label.resizeTextForBestFit=true;
        label.resizeTextMinSize=Mathf.Min(14,size);
        label.resizeTextMaxSize=size;
        return label;
    }

    private void FourSeatStripe(RectTransform plate,Color accent)
    {
        var stripe=Panel(plate,"SeatAccent",Vector2.zero,Vector2.one,accent);
        FourSeatPlace(stripe,0,0,3,48);
        stripe.GetComponent<Image>().raycastTarget=false;
    }

    private static readonly (string node,string key,string zh,string en,string ja)[] FourPlateZones={
        ("Library","牌库","牌库","Library","山札"),
        ("Grave","坟场","坟墓场","Graveyard","墓地"),("Exile","放逐","放逐","Exile","追放")
    };

    private static readonly (string node,string key,string zh,string en,string ja)[] FourPlateSources={
        ("Hand","手牌","手牌","Hand","手札"),
        ("PlayableGrave","可用坟场","坟场","Graveyard","墓地"),
        ("PlayableExile","可用放逐","放逐","Exile","追放"),
        ("Command","指挥官","统帅","Commander","統率者"),("Companion","行侣","行侣","Companion","相棒"),
        ("OtherCommand","可用统帅区","统帅区","Command zone","統率領域")
    };

    private static string FourSourceKey(ForgeCardView card)=>RedesignSource(card) switch {
        "Command"=>"指挥官","Companion"=>"行侣","Graveyard"=>"可用坟场","Exile"=>"可用放逐","CommandZone"=>"可用统帅区",_=>null
    };

    private IEnumerable<ForgeCardView> FourPublicSources(ForgePlayerView player)
        =>PublicHandSources(player,(player.usableSourceIds??Array.Empty<int>()).ToHashSet())
            .GroupBy(c=>c.id).Select(group=>group.First());

    private ForgeCardView[] FourSourceCards(ForgePlayerView player,string key)
        =>key=="手牌"?OtherHandCards(player):FourPublicSources(player).Where(c=>FourSourceKey(c)==key).ToArray();

    private string LokiRole(int seat) => seat==view.lokiSeat ? TapPaymentText("洛基","Loki","ロキ") : TapPaymentText("英雄","Hero","ヒーロー");

    private void PresentFourSeatControls(RectTransform plate,ForgePlayerView player)
    {
        string name=PlayerDisplayName(player.seat);
        if(view.commanderEvent==OnlineRoomConfig.Loki)name+=" · "+LokiRole(player.seat);
        SetNamedPlayerText(plate.Find("Name").GetComponent<Text>(),name,player.seat);
        var commander=plate.Find("CommanderName").GetComponent<Text>();
        commander.text=player.eliminated ? TapPaymentText("已出局","Eliminated","敗退")
            : string.Join(" / ",(player.commanders??Array.Empty<ForgeCardView>()).Where(c=>c!=null).Select(ExileCardName));
        commander.gameObject.SetActive(commander.text.Length>0);
        foreach(var zone in FourPlateZones){
            int count=zone.key switch {
                "牌库"=>player.libraryCount,
                "放逐"=>Math.Max(player.exileCount,ZoneCards(player,zone.key).Length),
                _=>ZoneCards(player,zone.key).Length
            };
            var button=plate.Find(zone.node).GetComponent<Button>();
            var label=button.GetComponentInChildren<Text>();
            label.text=TapPaymentText(zone.zh,zone.en,zone.ja)+" "+count;
            button.interactable=count>0&&!IsCardPickerRequest;
            button.GetComponent<ZonePileHover>().Configure(label,button.transform.Find("FocusRule").GetComponent<Image>(),
                button.interactable,false,HudText,HudMuted,FourSeatColor(player.seat));
        }
        var sources=FourPublicSources(player).ToLookup(FourSourceKey);
        foreach(var source in FourPlateSources){
            int count=source.key=="手牌"?player.handCount:sources[source.key].Count();
            var button=plate.Find(source.node).GetComponent<Button>();
            button.gameObject.SetActive(count>0);
            if(count==0)continue;
            string role=TapPaymentText(source.zh,source.en,source.ja);
            var label=button.GetComponentInChildren<Text>();
            label.text=role+" "+count;
            button.interactable=!IsCardPickerRequest;
        }
        int statusCount=fourStatusCounts.GetValueOrDefault(player.seat);
        var status=plate.Find("Status");
        status.gameObject.SetActive(statusCount>0);
        if(statusCount>0)status.Find("Label").GetComponent<Text>().text=TapPaymentText("状态 ","Status ","状態 ")+statusCount;
    }

    private string FourPlayerPortrait(int playerSeat)
    {
        var member = service?.LobbyState?.players?.FirstOrDefault(p => p != null && p.seat == playerSeat);
        if (PlayerAvatarGraphic.Valid(member?.avatarId)) return member.avatarId;
        if (playerSeat == seat && service?.IsSpectator != true && replayPlayerNames == null) return GamePreferences.AvatarId;
        var portraits = PlayerAvatarGraphic.Catalog;
        return portraits[(playerSeat - 1) % portraits.Length].Id;
    }

    private string FourCommanderNames(ForgePlayerView player) =>
        string.Join("\n",(player.commanders??Array.Empty<ForgeCardView>()).Where(c=>c!=null).Select(ExileCardName));

    private RectTransform FourPlayerPlate(ForgePlayerView player)
    {
        string id="FourSeat_"+player.seat;
        var existing=authored.Find(id) as RectTransform;
        if(existing!=null)return existing;
        var plate=Panel(authored,id,Vector2.zero,Vector2.one,HudInk);
        var target=plate.gameObject.AddComponent<Button>();
        target.targetGraphic=plate.GetComponent<Image>();target.transition=Selectable.Transition.None;
        FourSeatStripe(plate,FourSeatColor(player.seat));
        var portrait=Rect(plate,"Avatar",Vector2.zero,Vector2.one);
        FourSeatPlace(portrait,9,0,46,46);
        portrait.gameObject.AddComponent<PlayerAvatarGraphic>().raycastTarget=false;
        FourSeatCaption(plate,"Life",62,0,48,46,30);
        FourSeatCaption(plate,"Name",118,11,132,27,22);
        var commander=FourSeatCaption(plate,"CommanderName",0,0,160,24,15);
        commander.resizeTextForBestFit=false;
        commander.horizontalOverflow=HorizontalWrapMode.Wrap;
        commander.color=HudMuted;
        int playerSeat=player.seat;
        foreach(var zone in FourPlateZones.Concat(FourPlateSources)){
            var button=Button(plate,zone.node,"",Vector2.zero,Vector2.one,()=>OpenZoneBrowser(playerSeat,zone.key),Ink);
            var label=button.GetComponentInChildren<Text>();
            bool total=Array.Exists(FourPlateZones,z=>z.key==zone.key);
            if(total){
                MenuTypography.Use(label,MenuTypography.Face.Control);
                label.fontStyle=FontStyle.Normal;
                label.raycastTarget=false;
            }else MenuVisualTheme.StyleDeckTool(button);
            PreferenceText.SetSize(label,14);
            label.supportRichText=true;
            label.resizeTextForBestFit=false;
            if(total){
                button.transition=Selectable.Transition.None;
                button.GetComponent<Image>().color=Color.clear;
                var root=(RectTransform)button.transform;
                var divider=Panel(root,"Separator",Vector2.zero,new Vector2(1,13),HudRule);
                divider.anchorMin=divider.anchorMax=new Vector2(1,.5f);
                divider.GetComponent<Image>().raycastTarget=false;
                divider.gameObject.SetActive(zone.node!=FourPlateZones[FourPlateZones.Length-1].node);
                var focus=Panel(root,"FocusRule",Vector2.zero,Vector2.zero,Color.clear);
                focus.anchorMin=new Vector2(.12f,0);focus.anchorMax=new Vector2(.88f,0);
                focus.anchoredPosition=new Vector2(0,2);focus.sizeDelta=new Vector2(0,2);
                focus.GetComponent<Image>().raycastTarget=false;
                button.gameObject.AddComponent<ZonePileHover>();
            }
        }
        var status=Button(plate,"Status","",Vector2.zero,new Vector2(72,24),()=>{
            sidebarCollapsed=false;SelectSidebarTab(SidebarPage.Status);SelectFourPlayerStatusSeat(player.seat);
        },Ink);
        MenuVisualTheme.StyleDeckTool(status);
        PreferenceText.SetSize(status.GetComponentInChildren<Text>(),16);
        AddFourPageControls(plate,0,0);
        return plate;
    }

    private bool fourBrowseTargets;
    private Button fourTargetTab;
    private void PresentFourTargetList()
    {
        if(UsesTargetWindow&&!viewingBattlefield){
            if(fourTargetList!=null)fourTargetList.gameObject.SetActive(false);
            fourBrowseTargets=false;
            return;
        }
        var request=view?.request;
        bool choosing=view?.awaitingInput==true && request?.kind is ("InputSelectTargets" or "SelectStackTarget");
        bool browser=choosing && fourWindowMode && canAct;
        if(!browser)fourBrowseTargets=false;
        var rows=new List<(int seat,int cardId,string name,int count)>();
        int candidates=0,selected=0;
        if(choosing)for(int i=0;i<(request.options?.Length??0);i++){
            var option=request.options[i];
            if(option?.id==null)continue;
            int playerSeat=0,cardId=0;string name=null;
            if(option.id.StartsWith("card:",StringComparison.Ordinal) && int.TryParse(option.id.Substring(5),out int id)){
                if(fourFieldLocations.TryGetValue(id,out var loc)){
                    playerSeat=loc.seat;cardId=id;name=ExileCardName(loc.card);
                }else if(browser){playerSeat=option.card?.ownerSeat??0;name=option.card!=null?ExileCardName(option.card):option.label;}
                else continue;
            }else if(option.id.StartsWith("player:",StringComparison.Ordinal) && int.TryParse(option.id.Substring(7),out int target)){
                var player=view.players.FirstOrDefault(p=>p.targetId==target);
                if(player==null)continue;
                playerSeat=player.seat;
            }else if(browser && option.id.StartsWith("stack:",StringComparison.Ordinal) && int.TryParse(option.id.Substring(6),out int stackId)){
                var entry=view.stackItems?.FirstOrDefault(x=>x.id==stackId);
                playerSeat=entry?.controllerSeat??0;name=entry?.source!=null?ExileCardName(entry.source):option.label;
            }else continue;
            int count=option.selected||option.chosenCount>0?Mathf.Max(1,option.chosenCount):0;
            selected+=count;
            if(!option.disabled)candidates++;
            if(fourBrowseTargets ? (!option.disabled || count>0) : count>0)
                rows.Add((playerSeat,cardId,name,count));
        }
        if(rows.Count==0 && !(browser && candidates>0)){
            if(fourTargetList!=null)fourTargetList.gameObject.SetActive(false);
            return;
        }
        if(fourBrowseTargets)rows=rows.OrderBy(r=>r.seat).ToList();
        if(fourTargetList==null) {
            fourTargetList=Panel(authored,"FourSelectedTargets",Vector2.zero,new Vector2(260,160),new Color(.07f,.1f,.13f,.97f));
            fourTargetList.anchorMin=fourTargetList.anchorMax=Vector2.right;fourTargetList.pivot=Vector2.right;
            fourTargetList.anchoredPosition=new Vector2(-22,240);
            var title=Label(fourTargetList,"Title","",Vector2.zero,new Vector2(236,26),16,HudAccent);
            title.rectTransform.anchorMin=title.rectTransform.anchorMax=new Vector2(.5f,1);
            title.rectTransform.anchoredPosition=new Vector2(0,-15);
            title.alignment=TextAnchor.MiddleLeft;title.raycastTarget=false;
            fourTargetContent=Scroll(fourTargetList,"List",new Vector2(0,-14),new Vector2(248,150));
        }
        if(fourTargetTab==null && browser){
            fourTargetTab=Button(fourTargetList,"SwitchTargets","",Vector2.zero,new Vector2(92,27),()=>{
                fourBrowseTargets=!fourBrowseTargets;
                fourTargetContent.anchoredPosition=Vector2.zero;
                PresentFourTargetList();
            },Ink);
            var tab=(RectTransform)fourTargetTab.transform;tab.anchorMin=tab.anchorMax=Vector2.one;
            tab.pivot=Vector2.one;tab.anchoredPosition=new Vector2(-4,-2);
            MenuVisualTheme.StyleDeckTool(fourTargetTab);
            PreferenceText.SetSize(fourTargetTab.GetComponentInChildren<Text>(),15);
        }
        if(fourTargetTab!=null){
            fourTargetTab.gameObject.SetActive(browser);
            fourTargetTab.GetComponentInChildren<Text>().text=fourBrowseTargets
                ?TapPaymentText("已选目标","Selected","選択済み"):TapPaymentText("候选目标","Candidates","候補");
        }
        fourTargetList.gameObject.SetActive(true);
        float h=Mathf.Min(rows.Count,4)*35;
        fourTargetList.Find("Title").GetComponent<Text>().text=fourBrowseTargets
            ?TapPaymentText("候选目标","Candidates","候補")+" · "+candidates
            :TapPaymentText("已选目标","Selected","選択済み")+" · "+selected;
        fourTargetList.sizeDelta=new Vector2(260,h+38);
        ((RectTransform)fourTargetContent.parent).sizeDelta=new Vector2(248,h);
        fourTargetContent.sizeDelta=new Vector2(248,rows.Count*35);
        for(int i=0;i<Mathf.Max(rows.Count,fourTargetContent.childCount);i++) {
            var b=i<fourTargetContent.childCount?fourTargetContent.GetChild(i).GetComponent<Button>():
                Button(fourTargetContent,"Locate"+i,"",Vector2.zero,new Vector2(248,32),()=>{},Ink);
            b.gameObject.SetActive(i<rows.Count);
            if(i>=rows.Count)continue;
            var choice=rows[i];int id=choice.cardId;
            var rect=(RectTransform)b.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);
            rect.anchoredPosition=new Vector2(0,-17-i*35);
            var t=b.GetComponentInChildren<Text>();PreferenceText.SetSize(t,15);
            string owner=choice.seat>0?PlayerNamesRichText(PlayerDisplayName(choice.seat),choice.seat):"";
            string cardName=SafeDetail(choice.name);
            string caption=string.IsNullOrEmpty(owner)?cardName:string.IsNullOrEmpty(cardName)?owner:owner+" · "+cardName;
            t.supportRichText=true;
            t.text=(fourBrowseTargets && choice.count>0?"✓ ":"")+caption+(choice.count>1?" ×"+choice.count:"")+(!fourBrowseTargets && id!=0?"  ›":"");
            b.interactable=id!=0;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(()=>{if(fourFieldLocations.TryGetValue(id,out var now)){
                pages["four-seat:"+now.seat]=now.page;
                fourWindowPages[(now.seat-seat+4)%4].pan=Vector2.zero;
                PresentIntegrated(connectionText.text);
            }});
        }
    }


    private void BuildFourPlayerStatusSidebar()
    {
        if(!IsFourPlayerScene)return;
        for(int playerSeat=1;playerSeat<=4;playerSeat++){
            int selected=playerSeat;
            var row=Rect(sidebarStatusContent,"SeatStatus_"+playerSeat,Vector2.zero,new Vector2(416,122));
            BuildStatusRow(row,playerSeat==seat);
            var counters=Label(row,"SeatCounters","",Vector2.zero,new Vector2(382,30),16,Paper);
            counters.alignment=TextAnchor.MiddleLeft;counters.raycastTarget=false;
            fourStatusRows[playerSeat]=row;
            fourStatusEntries[playerSeat]=new List<SidebarMarkerEntry>();
            var tab=CreateSidebarTab("SeatStatusTab_"+playerSeat,"",()=>SelectFourPlayerStatusSeat(selected),filter:true);
            fourStatusTabs[playerSeat]=tab;
            var tabText=tab.transform.Find("Label").GetComponent<Text>();
            tabText.fontSize=17;tabText.resizeTextForBestFit=true;
            tabText.resizeTextMinSize=12;tabText.resizeTextMaxSize=17;
            var stripe=Panel((RectTransform)tab.transform,"SeatStripe",Vector2.zero,new Vector2(4,26),new Color(.55f,.67f,.7f,.7f));
            stripe.anchorMin=stripe.anchorMax=new Vector2(0,.5f);stripe.pivot=new Vector2(0,.5f);
            stripe.anchoredPosition=new Vector2(2,0);stripe.GetComponent<Image>().raycastTarget=false;
        }
        sidebarOwnStatus.gameObject.SetActive(false);
        sidebarOpponentStatus.gameObject.SetActive(false);
        sidebarOwnStatusTab.gameObject.SetActive(false);
        sidebarOpponentStatusTab.gameObject.SetActive(false);
    }

    private void LayoutFourPlayerStatusSidebar(float filtersTop)
    {
        if(fourStatusRows.Count==0)return;
        for(int playerSeat=1;playerSeat<=4;playerSeat++){
            var entries=fourStatusEntries[playerSeat];
            var row=fourStatusRows[playerSeat];
            float extra=FourPlayerCounterHeight(playerSeat);
            LayoutSidebarStatus(row,8,SidebarStatusHeight(entries)+extra,entries);
            var counters=(RectTransform)row.Find("SeatCounters");
            counters.gameObject.SetActive(extra>0);
            if(extra>0){
                RedesignPlace(counters,Vector2.up,Vector2.up,new Vector2(16,-4),new Vector2(382,extra-6));
                counters.GetComponent<Text>().text=fourStatusCounterText[playerSeat];
                ((RectTransform)row.Find("MarkerHost")).anchoredPosition=new Vector2(0,-8-extra);
            }
            var tab=(RectTransform)fourStatusTabs[playerSeat].transform;
            int slot=playerSeat-1;
            RedesignPlace(tab,Vector2.up,new Vector2(0,1),new Vector2(12+slot%2*216,2-filtersTop-slot/2*34),new Vector2(208,30));
            RedesignPlace((RectTransform)tab.Find("Label"),new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(190,27));
            RedesignPlace((RectTransform)tab.Find("ActiveRule"),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,1),new Vector2(184,2));
        }
        int selected=fourStatusEntries.ContainsKey(fourStatusSelectedSeat)?fourStatusSelectedSeat:1;
        float selectedExtra=FourPlayerCounterHeight(selected);
        sidebarStatusContent.sizeDelta=new Vector2(sidebarStatusView.rect.width,16+SidebarStatusHeight(fourStatusEntries[selected])+selectedExtra);
    }

    private float FourPlayerCounterHeight(int playerSeat)
    {
        if(!fourStatusCounterText.TryGetValue(playerSeat,out string text)||string.IsNullOrEmpty(text)||
            !fourStatusRows.TryGetValue(playerSeat,out var row))return 0;
        var label=row.Find("SeatCounters").GetComponent<Text>();
        label.text=text;
        return Mathf.Max(36,Mathf.Ceil(label.preferredHeight)+10);
    }

    private void SelectFourPlayerStatusSeat(int playerSeat)
    {
        if(!fourStatusRows.ContainsKey(playerSeat))return;
        fourStatusSelectedSeat=playerSeat;
        foreach(var pair in fourStatusRows)pair.Value.gameObject.SetActive(pair.Key==playerSeat);
        foreach(var pair in fourStatusTabs){
            bool selected=pair.Key==playerSeat;
            if(HudTheme!=null){
                PaintJournalTab(pair.Value,selected,filter:true);
                continue;
            }
            pair.Value.GetComponent<Image>().color=selected?
                new Color(.16f,.27f,.33f,.95f):new Color(.09f,.14f,.17f,.78f);
            pair.Value.transform.Find("ActiveRule").gameObject.SetActive(selected);
            pair.Value.transform.Find("Label").GetComponent<Text>().color=selected?Paper:new Color(.76f,.8f,.82f);
        }
        UpdateFourPlayerStatusTabLabels();
        LayoutBattleSidebar();
        sidebarStatusView.GetComponent<ScrollRect>().verticalNormalizedPosition=1;
    }

    private void UpdateFourPlayerStatusTabLabels()
    {
        foreach(var pair in fourStatusTabs){
            int playerSeat=pair.Key;
            string name=PlayerDisplayName(playerSeat);
            int count=fourStatusCounts.TryGetValue(playerSeat,out int value)?value:0;
            var label=pair.Value.transform.Find("Label").GetComponent<Text>();
            label.text=playerSeat+" · "+name+" · "+count;
            label.color=FourSeatColor(playerSeat);
            pair.Value.transform.Find("SeatStripe").GetComponent<Image>().color=FourSeatColor(playerSeat);
        }
    }
}
