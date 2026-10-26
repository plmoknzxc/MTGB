using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Unity presentation of the authenticated seat view. Only server-offered options can be submitted.</summary>
public sealed partial class ForgeBattlefieldController : MonoBehaviour
{
    public const string SceneName = BattlefieldRedesignLayout.SceneName;
    private static bool loading;
    private OnlineMatchService service;
    private Canvas canvas;
    private RectTransform surface, cardsRoot, actionRoot, previewRoot;
    private Text phaseText, promptText, connectionText;
    private ForgeSeatView view;
    private int seat;
    private bool canAct, submitted;
    private long requestId;
    private string renderedKey;
    private (string hash, OnlineClientInteractionState interaction, bool connected, int preferences,
        int bestOf, int ownWins, int otherWins, string players) renderedInputs;
    private string boardPreferenceKey;
    private bool? componentFaceMode;
    private Action<int> submit;
    private Action<int,int[]> submitOrder;
    private Action concede;
    private readonly Dictionary<string, CardDefinition> catalog = new Dictionary<string, CardDefinition>();
    private readonly Dictionary<string, int> pages = new Dictionary<string, int>();
    private static readonly Color Ink = new Color(.055f, .071f, .1f, 1f);
    private static readonly Color Paper = new Color(.9f, .87f, .78f);
    private static readonly Color Gold = new Color(.95f, .68f, .29f);
    private static readonly Color Blue = new Color(.32f, .72f, .92f);

    public static void Open(OnlineMatchService owner)
    {
        if (Application.isBatchMode && !owner.IsClient) return;
        if (loading) return;
        owner.StartCoroutine(Load(owner));
    }

    private static IEnumerator Load(OnlineMatchService owner)
    {
        loading = true;
        try
        {
            if (!IsBattlefieldSceneActive || OnlineLobbyPanel.HasBattlefieldLobby)
                yield return SceneManager.LoadSceneAsync(SceneName);
            if (owner == null || (!owner.IsInMatch && !owner.IsPreparingMatch && owner.EndReview == null))
            {
                if(IsBattlefieldSceneActive) SceneManager.LoadScene("Main");
                yield break;
            }
            var controller = FindFirstObjectByType<ForgeBattlefieldController>();
            if (controller == null) controller = new GameObject("ForgeBattlefield").AddComponent<ForgeBattlefieldController>();
            if (controller.service != owner)
            {
                if (controller.service != null) controller.service.InteractionStateChanged -= controller.InvalidateInteraction;
                controller.service = owner;
                owner.InteractionStateChanged += controller.InvalidateInteraction;
            }
            controller.Build();
        }
        finally { loading = false; }
    }

    private void InvalidateInteraction(OnlineClientInteractionState state) { renderedKey = null; }

    private void OnDestroy()
    {
        ResetZoneMotion();
        if (service != null) service.InteractionStateChanged -= InvalidateInteraction;
    }

    private void OnDisable() { ResetZoneMotion(); }

    private void Update()
    {
        UpdateRedesignLayout();
        UpdateChoiceCancelShortcut();
        UpdateTargeting();
        UpdateUndoShortcut();
        UpdateExpandedGroupDismissal();
        UpdateNoticeExpiry();
        UpdateManaPoolPulse();
        bool faceMode=GamePreferences.UseComponentCardFaces;
        if(componentFaceMode.HasValue&&componentFaceMode.Value!=faceMode&&integrated&&view!=null)
            PresentIntegrated(connectionText.text);
        componentFaceMode=faceMode;
        if (service == null) return; // The editor acceptance harness supplies real local-session views separately.
        if(service.IsPreparingMatch){PresentMatchPreparation();return;}
        if(WaitingForOpeningView){CloseMatchPreparation();return;}
        if(matchPreparationRoot!=null){Destroy(matchPreparationRoot.gameObject);matchPreparationRoot=null;preparationKey=null;
            if(!service.IsInMatch){SceneManager.LoadScene("Main");return;}}
        if (service.EndReview != null) { PresentEndReview(); return; }
        if (endCanvas != null) { Destroy(endCanvas.gameObject); if (leaveReviewButton != null) Destroy(leaveReviewButton.gameObject); CloseReviewCards(); reviewedMatch = null; }
        if (!service.IsInMatch)
        {
            // Host disposal and delivery of its own result can occur on adjacent network ticks.
            if (service.IsOnline) return;
            enabled=false;
            SceneManager.LoadScene("Main");
            return;
        }
        var series=service.LobbyState?.series;
        var inputs=(service.ForgeView?.viewHash ?? "", service.InteractionState, service.IsConnected,
            GamePreferences.Revision, series?.bestOf??0, series?.player1Wins??0, series?.player2Wins??0, PlayerNamesKey());
        if (renderedKey!=null && inputs==renderedInputs) {UpdateAutomaticControl();return;}
        renderedInputs=inputs;
        renderedKey=string.Empty; // null remains the explicit interaction invalidation marker.
        Present(service.ForgeView?.forge, service.IsSpectator ? 1 : service.LocalSeat,
            !service.IsSpectator && service.IsConnected && service.InteractionState == OnlineClientInteractionState.Idle,
            service.SubmitForgeOption, service.IsSpectator ? (Action)null : service.Concede,
            service.IsReconnecting ? "连接中断 · 正在恢复对局" : service.HasPendingAction ? "正在提交操作…" : (service.IsSpectator ? "观战 · " : "房间 ") + service.CurrentRoomId, service.SubmitForgeOrder);
        UpdateAutomaticControl();
    }

    public bool TrySubmitPresentedOption(int index, long expectedRequest)
    {
        if(submitted || !canAct || requestId!=expectedRequest)return false;
        Choose(index,expectedRequest);
        return submitted;
    }

    public void Present(ForgeSeatView state, int localSeat, bool enabledInput, Action<int> onChoice, Action onConcede, string status, Action<int,int[]> onOrder = null)
    {
        Build();
        bool restarted=state!=null&&view!=null&&seat==localSeat&&state.restartRevision!=view.restartRevision;
        if(restarted)ResetRestartPresentation();
        // A server acknowledgement clears its request until Forge publishes the
        // next snapshot. Freeze the existing surface instead of closing/reopening it.
        if(!restarted && integrated && state?.inputPending==true && view!=null && seat==localSeat) {
            canAct=false;submitted=true;
            DisableIntegratedChoices();
            DisableCardSelectionChoices();
            return;
        }
        // Retain the current surface while its response is in flight. The next
        // authoritative request updates it; a submitting notification is not a new scene.
        if(!restarted && integrated && submitted && service!=null && service.HasPendingAction && seat==localSeat) return;
        string preferenceKey=GamePreferences.Revision+"/"+PlayerNamesKey();
        bool pickerUpdate=!restarted && integrated && boardPreferenceKey==preferenceKey && IsCardPickerRequest && state?.awaitingInput==true && state.request?.kind==view.request.kind
            && state.turn==view.turn && state.phase==view.phase && state.stackCount==view.stackCount
            && (!string.IsNullOrEmpty(state.boardKey)?state.boardKey==view.boardKey:
                string.Join("|",(state.players??Array.Empty<ForgePlayerView>()).Select(p=>ServerSnapshotJson.Serialize(p)))==string.Join("|",(view.players??Array.Empty<ForgePlayerView>()).Select(p=>ServerSnapshotJson.Serialize(p))));
        if(integrated && (seat!=localSeat || state==null))ResetCastingVisual();
        if(state==null||seat!=localSeat){RestoreBattlefieldWindows();selectedHandSource="Hand";ResetRedesignTransitions();}
        if(integrated&&(state==null||seat!=localSeat)) {
            hoveredStack=null;detailCard=null;detailCardId=inspectedCardId=0;inspectHoverRevision++;
            Node("InspectPanel").gameObject.SetActive(false);
        }
        if(state==null){nameRequest=0;nameLanguageRevision=-1;pileRequest=0;selectedPileIndex=-1;ResetNotices();}
        if(seat!=localSeat)ResetNotices();
        ObserveStackReadWindow(state,localSeat);
        view = state; seat = localSeat; canAct = enabledInput; submit = onChoice; submitOrder = onOrder; concede = onConcede;
        boardPreferenceKey=preferenceKey;
        requestId = state?.awaitingInput == true ? state.request.requestId : 0;
        submitted = false;
        ObserveNextPhasePause(state,localSeat);
        PresentNotices();
        if(pickerUpdate){SetText("PriorityPill/Text","等待你完成选择");PresentCardSelection();PresentTargetChoiceActions();RefreshBattlefieldViewControls();PresentRedesignLayout();return;}
        if (integrated) { PresentIntegrated(status);RefreshBattlefieldViewControls(); return; }
        Clear(cardsRoot); Clear(actionRoot); Clear(previewRoot);
        connectionText.text = status;
        phaseText.text = state == null || state.turn == 0 ? "准备对局 · 起手选择" : $"第 {state.turn} 回合  ·  {GamePreferences.T(state.turnSeat == seat ? "你的回合" : "对手回合")}  /  {GamePreferences.T(Phase(state.phase))}";
        promptText.text = state == null ? "等待对局状态" : Prompt(state, localSeat);
        foreach (var player in state?.players ?? Array.Empty<ForgePlayerView>()) DrawPlayer(player);
        DrawActions();
        PresentCardSelection();
        Label(previewRoot, "Help", "点击高亮卡牌或头像进行操作\n悬停卡牌查看详情\n拖动手牌到战场也可出牌", new Vector2(0, -285), new Vector2(250, 100), 18, new Color(.6f,.65f,.72f));
    }

    // Hands and inspectors change sibling order while animating. These overlays
    // retain their order independently; only scrollable notices accept input.
    private void SetOverlayLayer(RectTransform root, int offset, bool interactive = false)
    {
        var overlay = root.GetComponent<Canvas>();
        if (overlay == null) overlay = root.gameObject.AddComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingLayerID = canvas.sortingLayerID;
        overlay.sortingOrder = canvas.sortingOrder + offset;
        if (interactive && root.GetComponent<GraphicRaycaster>() == null)
            root.gameObject.AddComponent<GraphicRaycaster>();
    }

    private const int NoticeLayerOffset = 10, DragArrowLayerOffset = 20, InspectLayerOffset = 30, DecisionLayerOffset = 35;

    private void Build()
    {
        if (canvas != null) return;
        if (IsBattlefieldSceneActive) { BuildIntegrated(); return; }
        foreach (var old in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) old.enabled = false;
        var whitebox = GameObject.Find("ArenaWhitebox");
        if (whitebox != null)
            foreach (Transform child in whitebox.transform)
                if (child.name.Contains("Card")) child.gameObject.SetActive(false);
        foreach (var card in CardDatabaseRepository.LoadAll())
            if (!card.forgeAmbiguous && !catalog.ContainsKey(card.PrintingKey)) catalog.Add(card.PrintingKey, card);
        var go = new GameObject("BattlefieldCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        surface = (RectTransform)go.transform;
        Panel(surface, "TopShade", new Vector2(0, 490), new Vector2(1920,100), Ink);
        Panel(surface, "HandShade", new Vector2(0, -428), new Vector2(1920,224), new Color(.025f,.035f,.055f,.84f));
        Label(surface, "Brand", "M T G B   /   B A T T L E", new Vector2(-700,498), new Vector2(440,35), 22, Gold);
        phaseText = Label(surface, "Phase", "", new Vector2(20,496), new Vector2(900,42), 23, Paper);
        connectionText = Label(surface, "Connection", "", new Vector2(-725,460), new Vector2(390,25), 15, Blue);
        Button(surface, "Concede", "认输", new Vector2(850,492), new Vector2(130,40), () => ConfirmConcede(), Ink);
        Panel(surface, "CenterLine", new Vector2(-60,65), new Vector2(1280,2), new Color(.45f,.54f,.65f,.35f));
        promptText = Label(surface, "Prompt", "", new Vector2(-30,65), new Vector2(1100,46), 21, Gold);
        cardsRoot = Rect(surface, "Cards", Vector2.zero, new Vector2(1920,1080));
        previewRoot = Rect(surface, "Inspect", new Vector2(-775,30), new Vector2(280,700));
        var actionPanel = Panel(surface, "ActionPanel", new Vector2(786,20), new Vector2(292,780), Ink);
        Label(actionPanel, "Heading", "当前操作", new Vector2(0,356), new Vector2(258,35), 21, Gold);
        actionRoot = Rect(actionPanel, "Choices", Vector2.zero, new Vector2(280,670));
    }

    private void DrawPlayer(ForgePlayerView player)
    {
        bool own = player.seat == seat;
        int target = OptionIndex("player:" + player.targetId);
        long capturedRequest = requestId;
        float avatarY = own ? -292 : 405;
        var avatar = Button(cardsRoot, "PlayerTarget_" + player.seat, $"{PlayerDisplayName(player.seat)}   {player.life}",
            new Vector2(-60, avatarY), new Vector2(210,52), () => Choose(target, capturedRequest), target >= 0 ? Gold : Ink);
        avatar.interactable = target >= 0 && canAct;
        SetNamedPlayerText(avatar.GetComponentInChildren<Text>(),$"{PlayerDisplayName(player.seat)}   {player.life}");
        Label(cardsRoot, "ZoneCounts_" + player.seat, $"手牌 {player.handCount}    牌库 {player.libraryCount}",
            new Vector2(370,avatarY), new Vector2(300,35), 18, Paper);
        Button(cardsRoot, "Graveyard_" + player.seat, $"坟墓场  {player.graveyard?.Length ?? 0}",
            new Vector2(-455,avatarY), new Vector2(170,40), () => ShowGraveyard(player), Ink);
        if (own)
        {
            DrawRow(player.hand, "Hand", -418, 156, 205, 8);
            string[] colors={"白","蓝","黑","红","绿","无色"};
            string mana=player.manaPool?.Length==6?string.Join("  ",colors.Select((c,i)=>c+" "+player.manaPool[i])):"红 "+player.redMana;
            Label(cardsRoot, "Mana", "法术力池\n" + mana, new Vector2(790,-433), new Vector2(280,65), 17, Gold);
        }
        else
        {
            int visible = Math.Min(player.handCount, 12);
            for (int i = 0; i < visible; i++)
            {
                var back = Panel(cardsRoot, "OpponentHandBack", new Vector2(-60 + (i-(visible-1)*.5f)*36, 458), new Vector2(32,43), new Color(.14f,.20f,.29f));
                Label(back, "Sigil", "◇", Vector2.zero, new Vector2(30,40), 23, Gold);
            }
        }
        var field = player.battlefield ?? Array.Empty<ForgeCardView>();
        DrawRow(field.Where(c => !c.land).ToArray(), own ? "YourCreatures" : "OpponentCreatures", own ? -40 : 168, 116, 152, 9);
        DrawRow(field.Where(c => c.land).ToArray(), own ? "YourLands" : "OpponentLands", own ? -197 : 316, 102, 126, 10);
    }

    private void DrawRow(ForgeCardView[] cards, string row, float y, float width, float height, int pageSize)
    {
        cards = cards ?? Array.Empty<ForgeCardView>();
        int maxPage = Math.Max(0, (cards.Length-1)/pageSize);
        int page = pages.TryGetValue(row, out int saved) ? Math.Min(saved,maxPage) : 0;
        int count = Math.Min(pageSize, cards.Length-page*pageSize);
        for (int i=0;i<count;i++)
        {
            var card = cards[i+page*pageSize];
            var position = new Vector2(-60+(i-(count-1)*.5f)*(width+12), y);
            DrawCard(cardsRoot, card, position, new Vector2(width,height), false, row == "Hand");
        }
        if (maxPage > 0)
        {
            if(page>0) Button(cardsRoot,row+"Previous","‹",new Vector2(-680,y),new Vector2(34,40),()=>Page(row,page-1,maxPage),Ink);
            if(page<maxPage) Button(cardsRoot,row+"Next","›",new Vector2(585,y),new Vector2(34,40),()=>Page(row,page+1,maxPage),Ink);
        }
    }

    private void Page(string row, int page, int max)
    {
        pages[row] = Mathf.Clamp(page,0,max);
        Present(view,seat,canAct,submit,concede,connectionText.text);
    }

    private void DrawCard(Transform parent, ForgeCardView card, Vector2 position, Vector2 size, bool inspect, bool hand)
    {
        catalog.TryGetValue(card.printingKey ?? "", out var definition);
        if (card.faceDown) definition = null;
        int option = inspect ? -1 : OptionIndex("card:" + card.id);
        bool playable = option >= 0 && canAct && !submitted;
        var frame = Panel(parent, "Card_"+card.id, position, size, playable ? Gold : card.attacking ? new Color(.8f,.28f,.18f) : card.blocking ? Blue : new Color(.24f,.28f,.34f));
        if (!inspect && card.tapped) frame.localRotation = Quaternion.Euler(0,0,-14);
        var face = Panel(frame,"Face",Vector2.zero,size-new Vector2(5,5),Ink);
        Sprite fullArt=null;
        bool hasArt = IsSpecializedFace(card)?TryBattlefieldArt(card,out fullArt):definition != null && CardImageLibrary.TryLoadFullCard(definition, out fullArt);
        if (hasArt)
        {
            var img = face.GetComponent<Image>(); img.sprite=fullArt; img.color=Color.white; img.preserveAspect=true;
        }
        else
        {
            Color color = card.land ? new Color(.25f,.19f,.12f) : new Color(.37f,.13f,.10f);
            Panel(face,"ArtPanel",new Vector2(0,size.y*.11f),new Vector2(size.x-14,size.y*.32f),color);
            Label(face,"Emblem",card.faceDown ? "◇" : card.land ? "▲" : card.creature ? "⚔" : "✦",new Vector2(0,size.y*.11f),new Vector2(size.x-15,size.y*.3f),(int)(size.y*.20f),Gold);
            Label(face,"CardName",definition!=null && !card.faceDown ? GamePreferences.CardName(definition) : card.name,new Vector2(0,size.y*.40f),new Vector2(size.x-12,size.y*.15f),inspect?22:14,Paper);
            Label(face,"ManaCost",definition?.manaCost ?? "",new Vector2(0,size.y*.285f),new Vector2(size.x-12,size.y*.08f),inspect?20:12,Gold);
            Label(face,"Type",card.faceDown ? "面朝下的牌" : GamePreferences.CardType(definition),new Vector2(0,-size.y*.115f),new Vector2(size.x-12,size.y*.08f),inspect?18:11,Paper);
            if (inspect || hand)
                Label(face,"Rules",GamePreferences.CardRules(definition),new Vector2(0,-size.y*.275f),new Vector2(size.x-18,size.y*.23f),inspect?18:11,new Color(.7f,.73f,.77f));
        }
        if (card.creature)
            Label(frame,"PowerToughness",$"{card.power}/{card.toughness}",new Vector2(size.x*.29f,-size.y*.42f),new Vector2(size.x*.4f,size.y*.12f),inspect?24:16,Paper);
        if (card.tapped || card.attacking || card.blocking || card.creature&&card.damage>0)
            Label(frame,"State",card.attacking ? "攻击中" : card.blocking ? "阻挡中" : card.creature&&card.damage>0 ? "伤害 "+card.damage : "已横置",
                new Vector2(0,-size.y*.49f),new Vector2(size.x,22),inspect?18:13,Gold);
        if (inspect) return;
        var button = frame.gameObject.AddComponent<Button>(); button.targetGraphic=frame.GetComponent<Image>();
        // Non-selectable cards still support inspection. Choose checks the authoritative offered option.
        long captured = requestId;
        button.onClick.AddListener(()=> {if(!hand||view?.request?.kind!="InputPassPriority")Choose(option,captured);});
        var pointer = frame.gameObject.AddComponent<ForgeBattlefieldCardPointer>();
        pointer.Configure(()=>Inspect(card),()=>Clear(previewRoot),()=>Choose(option,captured),hand && playable);
    }

    private void Inspect(ForgeCardView card)
    {
        if (integrated) { InspectIntegrated(card); return; }
        Clear(previewRoot);
        DrawCard(previewRoot,card,new Vector2(0,60),new Vector2(264,380),true,false);
    }

    private void ShowGraveyard(ForgePlayerView player)
    {
        inspectedCardId=0;
        if(integrated)Node("InspectPanel").gameObject.SetActive(true);
        Clear(previewRoot);
        Label(previewRoot,"GraveyardTitle",(player.seat==seat?"你的":"对手的")+"坟墓场",new Vector2(0,300),new Vector2(270,40),24,Gold);
        var content = Scroll(previewRoot,"GraveyardContents",new Vector2(0,0),new Vector2(280,530));
        string[] grave = player.graveyard ?? Array.Empty<string>();
        content.sizeDelta = new Vector2(260,Math.Max(530,grave.Length*52));
        for(int i=0;i<grave.Length;i++) TopLabel(content,"Grave_"+i,grave[i],i*52,260,48);
    }

    private void DrawActions()
    {
        Button(actionRoot,"Stack",$"堆叠中的咒语 / 异能   {view?.stackCount ?? 0}",new Vector2(0,302),new Vector2(260,44),ShowStack,Ink);
        var request = view?.awaitingInput == true ? view.request : null;
        if (request == null)
        {
            Label(actionRoot,"Waiting","等待对手操作…",new Vector2(0,190),new Vector2(255,70),21,Paper);
            return;
        }
        Label(actionRoot,"RequestTitle",RequestTitle(request.kind),new Vector2(0,235),new Vector2(260,55),24,Gold);
        string message = request.kind == "InputPassPriority" ? (view.stackCount>0 ? "等待结算：\n"+(view.stack?.FirstOrDefault() ?? "点击堆叠查看") : "点击高亮的手牌或永久物。\n没有动作时让过优先权。") : request.kind.Contains("Mulligan") ? "检查下方手牌，选择保留或再调度。" : request.message;
        Label(actionRoot,"RequestMessage",message,new Vector2(0,135),new Vector2(252,125),17,Paper);
        var content = Scroll(actionRoot,"ActionScroll",new Vector2(0,-142),new Vector2(272,410));
        var choices = request.options ?? Array.Empty<ForgeOption>();
        int count=0;
        for(int i=0;i<choices.Length;i++)
        {
            if(request.kind.Contains("PayMana") && choices[i].id=="ok")continue;
            if (RepresentedOnBoard(choices[i].id)) continue;
            int index=i; long captured=requestId;
            var button=Button(content,"Option_"+i,OptionLabel(choices[i]),Vector2.zero,new Vector2(250,58),()=>Choose(index,captured),choices[i].id=="ok"?new Color(.48f,.31f,.12f):new Color(.13f,.18f,.25f));
            var rect=(RectTransform)button.transform; rect.anchorMin=rect.anchorMax=new Vector2(.5f,1); rect.pivot=new Vector2(.5f,1); rect.anchoredPosition=new Vector2(0,-count*68);
            button.interactable=canAct; count++;
        }
        content.sizeDelta=new Vector2(250,Math.Max(410,count*68));
    }

    private bool RepresentedOnBoard(string id)
    {
        // Spending a pool color is already bound to its native mana-pool chip.
        if(integrated && id.StartsWith("mana:",StringComparison.Ordinal))return true;
        if(integrated && view?.request?.kind=="InputPassPriority" && AvailableExileCards().Any(c=>id=="card:"+c.id))return true;
        if(integrated && (id.StartsWith("block:") || id.StartsWith("block-remove:")))return true;
        if(integrated && (view?.stackItems ?? Array.Empty<ForgeStackItem>()).Any(e=>id=="stack:"+e.id))return true;
        foreach(var p in view?.players ?? Array.Empty<ForgePlayerView>())
        {
            if(id=="player:"+p.targetId) return true;
            if((p.hand??Array.Empty<ForgeCardView>()).Concat(p.battlefield??Array.Empty<ForgeCardView>()).Any(c=>id=="card:"+c.id)) return true;
        }
        return false;
    }

    private void ShowStack()
    {
        Clear(previewRoot);
        Label(previewRoot,"StackTitle","堆叠 · 从上到下结算",new Vector2(0,300),new Vector2(270,45),21,Gold);
        var content=Scroll(previewRoot,"StackContents",Vector2.zero,new Vector2(280,530));
        string[] entries=view?.stack??Array.Empty<string>();
        content.sizeDelta=new Vector2(260,Math.Max(530,entries.Length*140));
        for(int i=0;i<entries.Length;i++)TopLabel(content,"Stack_"+i,entries[i],i*140,260,135);
    }

    public int OptionIndex(string id) => view?.awaitingInput == true && view.request?.options != null
        ? Array.FindIndex(view.request.options,o=>o.id==id) : -1;

    public void Choose(int index, long expectedRequest)
        => ChooseCore(index,expectedRequest,null);

    private void ChooseCore(int index,long expectedRequest,int[] attackGroup)
    {
        if(viewingBattlefield || !canAct || submitted || index<0 || requestId!=expectedRequest || view?.awaitingInput!=true || index>=view.request.options.Length) return;
        if(view.request.options[index].disabled)return;
        if(attackGroup!=null && (submitOrder==null || view.request.kind!="InputAttack" || !view.request.options[index].id.StartsWith("attack:")))return;
        if(service!=null && (service.ForgeView?.forge?.request?.requestId!=expectedRequest || !service.CanSubmitRuleAction)) return;
        if(TryStageHandChoice(index,expectedRequest))return;
        RememberCastingOrigin(view.request.options[index]);
        RememberLandChoice(view.request.options[index]);
        submitted=true;
        PreviewStackTargetChoice(view.request.options[index]);
        DisableCardSelectionChoices();
        // Keep the current choice visible until an authoritative snapshot replaces
        // it. Numeric +/- and optional-cost toggles are incremental requests;
        // hiding their surface on every submission makes the whole UI blink.
        // DisableIntegratedChoices and the submitted guard prevent repeat input.
        SuspendTargeting();
        if(integrated)SetText("PriorityPill/Text","正在提交操作…");
        if (integrated) DisableIntegratedChoices();
        foreach(var button in cardsRoot.GetComponentsInChildren<Button>()) button.interactable=false;
        foreach(var button in actionRoot.GetComponentsInChildren<Button>()) button.interactable=false;
        if(attackGroup!=null)submitOrder(index,attackGroup);else submit?.Invoke(index);
    }

    private void ConfirmConcede()
    {
        Clear(actionRoot);
        Label(actionRoot,"ConfirmConcede","确认认输？",new Vector2(0,150),new Vector2(260,60),25,Paper);
        Button(actionRoot,"Confirm","确认认输",new Vector2(0,50),new Vector2(250,55),()=>concede?.Invoke(),new Color(.4f,.12f,.12f));
        Button(actionRoot,"Cancel","继续对局",new Vector2(0,-20),new Vector2(250,55),()=>{Clear(actionRoot);DrawActions();},Ink);
    }

    private static string Prompt(ForgeSeatView v,int local)
    {
        if(!v.awaitingInput)return "等待对手操作";
        var req=v.request;
        if(req.selectionPurpose=="chooseLibraryCostOwner")return TapPaymentText("选择支付用的坟墓场\n选中玩家后确认","Choose a graveyard\nSelect player and confirm.","支払いに使う墓地を選択\nプレイヤーを選んで確認");
        if(req.selectionPurpose=="putLibraryCost")return TapPaymentText("选择作为费用放回的牌","Choose cards for payment","支払いに使うカードを選択");
        if(req.kind=="InputLondonMulligan")return TapPaymentText($"已保留 · 选择回牌库底\n已选 {req.selectedCount} 张，还需 {req.remainingCount} 张",$"Hand kept · choose cards for the bottom\nSelected {req.selectedCount}; {req.remainingCount} remaining",$"手札をキープ・ライブラリーの一番下に置くカードを選択\n選択済み {req.selectedCount} 枚・あと {req.remainingCount} 枚");
        if(req.kind.Contains("Mulligan"))return req.remainingCount>0?TapPaymentText($"保留后需放回 {req.remainingCount} 张\n也可以继续调度，重新抓 7 张",$"Keeping requires returning {req.remainingCount} cards\nOr mulligan and draw 7 again",$"キープ後に {req.remainingCount} 枚を戻します\nまたはマリガンして 7 枚引き直します"):TapPaymentText("保留这手牌，或调度重新抓牌","Keep this hand, or mulligan","この手札をキープ、またはマリガン");
        if(req.kind.Contains("PayMana")) {
            string cost=req.remainingManaCost;
            if(string.IsNullOrEmpty(cost)) {
                var match=System.Text.RegularExpressions.Regex.Match(req.message??"",@"Pay Mana Cost:\s*([^\r\n]+)");
                if(match.Success)cost=match.Groups[1].Value.Trim();
            }
            return TapPaymentText($"支付费用 {cost}\n地牌产费／取消支付",$"Pay {cost}\nTap lands or cancel.",$"コスト {cost} を支払う\n土地をタップ／キャンセル");
        }
        if((req.kind is "InputSelectTargets" or "SelectStackTarget") && (req.options??Array.Empty<ForgeOption>()).Any(o=>o.id.StartsWith("stack:",StringComparison.Ordinal)))return TapPaymentText("选择目标 · 在选择窗口中选择","Choose a target in the selection window","選択ウィンドウで対象を選択");
        if(req.kind.Contains("Targets"))return "选择目标 · 点击高亮卡牌/头像\n也可从待施放牌拖出箭头";
        if(req.kind=="InputPassPriority")return v.stackCount>0?"你有优先权 · 可以响应\n双方连续让过后，结算堆叠顶项":"你有优先权\n可以行动，或让过优先权";
        return RequestTitle(req.kind);
    }
    public bool TryUndoPresentedAction()
    {
        if(GameSettingsPanel.IsOpen || submitted || !canAct || view?.awaitingInput!=true || view.request.kind!="InputPassPriority" || view.request.seat!=seat)return false;
        int index=OptionIndex("cancel");
        if(index<0 || !view.request.options[index].label.StartsWith("Undo",StringComparison.OrdinalIgnoreCase))return false;
        Choose(index,requestId);
        return submitted;
    }
    private void UpdateUndoShortcut()
    {
        if(!Application.isFocused || GameSettingsPanel.IsOpen)return;
        var selected=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        if(selected!=null && (selected.GetComponentInParent<InputField>()!=null || selected.GetComponentInParent<TMPro.TMP_InputField>()!=null))return;
#if ENABLE_INPUT_SYSTEM
        var keyboard=UnityEngine.InputSystem.Keyboard.current;
        if(keyboard!=null && keyboard.zKey.wasPressedThisFrame && !keyboard.ctrlKey.isPressed && !keyboard.altKey.isPressed && !keyboard.leftMetaKey.isPressed && !keyboard.rightMetaKey.isPressed)TryUndoPresentedAction();
#else
        if(Input.GetKeyDown(KeyCode.Z) && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))TryUndoPresentedAction();
#endif
    }
    private string RequestOptionLabel(ForgeOption option)
    {
        if(IsCommanderPerpetualReset) {
            if(option.id=="ok")return TapPaymentText("清除永久改动","Clear changes","変更を消す");
            if(option.id=="cancel")return TapPaymentText("保留永久改动","Keep changes","変更を残す");
        }
        if(view?.request?.selectionPurpose=="forageMethod") {
            if(option.id=="ok")return TapPaymentText("牺牲食品","Sacrifice Food","食物を生け贄");
            if(option.id=="cancel")return TapPaymentText("放逐坟场三张","Exile three from graveyard","墓地３枚を追放");
        }

        string auxiliary=AuxiliaryOptionLabel(option);if(auxiliary!=null)return auxiliary;
        string kind=view?.request?.kind??"";
        // Declining an "unless" card payment accepts its consequence; it does
        // not return to the previous menu. Name the exact native consequence.
        if(option.id=="cancel"&&kind=="InputSelectCardsFromList") {
            var loss=System.Text.RegularExpressions.Regex.Match(view.request.message??"",@"\(or: Player (\d+) loses (\d+) life\.\s*\)\s*$");
            if(loss.Success&&int.TryParse(loss.Groups[1].Value,out int affected)&&affected==seat)
                return TapPaymentText(loss.Groups[2].Value+"点生命",loss.Groups[2].Value+" life",loss.Groups[2].Value+"点のライフ");
        }
        if(kind=="dialog"&&option.rulesSource is ForgeCardView ruleSource&&!ruleSource.faceDown&&!string.IsNullOrEmpty(ruleSource.name)) {
            string localized=PrintedRuleText(ruleSource,option.label);
            if(localized!=option.label)return localized;
        }
        if(kind=="ChooseModes"&&view.request.sourceCard!=null) {
            string mode=PrintedRuleText(view.request.sourceCard,option.label);
            if(mode!=option.label)return mode;
        }
        if(kind=="ChooseOptions"&&view.request.sourceCard is ForgeCardView choiceSource) {
            string prefix=choiceSource.name+" — ";
            if((option.label??"").StartsWith(prefix,StringComparison.Ordinal)) {
                string raw=option.label.Substring(prefix.Length),local=GeneratedPublicRuleText(raw);
                if(local!=raw)return GamePreferences.CardFaceName(choiceSource.name)+" — "+local;
            }
        }
        if(kind=="ChooseOptions"&&option.id.StartsWith("select:",StringComparison.Ordinal)) {
            string source=view.request.sourceCard?.originalName??view.request.sourceCard?.name;
            if(source=="Bronze Tablet"||source=="Tempest Efreet") {
                string label=option.label??"";
                if(label.Contains("Pay 10 life."))return TapPaymentText("支付 10 点生命","Pay 10 life","10点のライフを支払う");
                if(source=="Bronze Tablet"&&label.Contains("Exchange ownership of your card"))return TapPaymentText("交换拥有权","Exchange ownership","所有権を交換");
                if(source=="Bronze Tablet"&&label.Contains("Concede the game."))return TapPaymentText("认输","Concede","投了");
                if(source=="Tempest Efreet"&&label.Contains("Reveal a card at random"))return TapPaymentText("随机展示手牌并交换","Reveal randomly and exchange","手札を無作為公開し交換");
            }
        }
        if(kind=="dialog" && view.request.message=="one: Choose a shared type")
            return GamePreferences.NativeText(option.label);
        if(kind=="dialog" && (view.request.message=="one: Choose a basic land type" || view.request.message=="one: Choose a land type"))
            return SearchInsertionMenu.Label(option.label);
        if(kind=="dialog" && (view.request.message=="one: Select type of counters to remove"||view.request.message=="one: Select type counters to remove"||view.request.message=="one: Select type of counters to add or remove"))
            return CounterChoiceName(option.label);
        if(kind=="dialog" && option.label=="[FINISH TARGETING]" &&
           (view.request.options??Array.Empty<ForgeOption>()).Any(IsCrossZoneTargetHeader) &&
           (view.request.options??Array.Empty<ForgeOption>()).Any(HasChoiceCard))
            return TapPaymentText("确认目标","Confirm targets","対象を確定");
        if(view?.request?.paymentMode=="manual") {
            if(option.id is "ok" or "confirm-cards" or "confirm-number")return TapPaymentText("下一步","Next","次へ");
            if(option.id=="cancel" && (option.label=="Cancel"||option.label=="取消"))return TapPaymentText("取消","Cancel","キャンセル");
        }
        if(view?.request?.selectionPurpose=="chooseLibraryCostOwner"||view?.request?.selectionPurpose=="putLibraryCost") {
            if(option.id=="cancel")return TapPaymentText("取消支付","Cancel payment","支払いをキャンセル");
            if(option.id=="ok")return TapPaymentText("确认选择","Confirm selection","選択を確認");
        }
        if(kind=="InputChooseStartingHand") {
            if(option.id=="ok")return TapPaymentText("查看下一组","View next hand","次の手札を見る");
            if(option.id=="cancel")return TapPaymentText("保留这组起手","Accept this hand","この手札を選ぶ");
        }
        // Preserve native option IDs; these labels describe the two destinations.
        if(kind=="InputConfirm") {
            if(option.label=="Cast")return TapPaymentText("施放","Cast","唱える");
            if(option.label=="Hand")return TapPaymentText("放入手牌","Put in hand","手札に加える");
        }
        if(option.id=="ok")
        {
            if(kind=="InputPassPriority")return view.turnSeat==seat && view.stackCount==0?"结束阶段":"让过";
            if(kind=="InputLondonMulligan")return TapPaymentText("确认放回所选牌","Return selected cards","選択したカードを戻す");
            if(kind=="InputAttack")return "确认攻击";
        }
        if(option.id=="cancel" && kind.Contains("PayMana"))return TapPaymentText("取消支付","Cancel payment","支払いをキャンセル");
        if(option.id=="cancel" && kind.Contains("Targets"))return "取消施放";
        if(kind=="InputLondonMulligan" && option.label=="Auto")return TapPaymentText("自动补选并放回","Auto-select and return","自動選択して戻す");
        if(kind.Contains("Mulligan") && option.label=="Keep")return TapPaymentText("保留起手","Keep hand","手札をキープ");
        if(kind.Contains("Mulligan") && option.label=="Mulligan")return TapPaymentText("再调度","Mulligan","マリガン");
        if(kind=="dialog"||kind=="InputConfirm")return RandomChoiceLabel(option.label);
        return OptionLabel(option);
    }
    public static string RequestTitle(string kind)
    {
        if(kind==null) return "请选择";
        if(kind=="SelectStackTarget") return "选择高亮的堆叠咒语或异能";
        if(kind=="OrderAbilities") return "选择同时触发异能的结算顺序";
        if(kind=="Scry") return "占卜 · 选择置顶、置底及顺序";
        if(kind=="InputLondonMulligan") return "选择放回牌库底的牌";
        if(kind.Contains("Mulligan")) return "保留起手或再调度";
        if(kind.Contains("PayMana")) return "支付法术力 · 点击地牌产费";
        if(kind.Contains("Targets")) return "选择高亮的卡牌或玩家";
        if(kind.Contains("Attack")) return "选择攻击生物与防御者";
        if(kind.Contains("Block")) return "选择阻挡生物及其目标";
        if(kind.Contains("SelectCards")) return "选择卡牌，然后确认";
        return kind=="InputPassPriority" ? "你的优先权" : "选择效果 / 确认";
    }
    public static string OptionLabel(ForgeOption option)
    {
        switch(option.label)
        {
            case "OK": return "确认 / 让过";
            case "Cancel": return "取消";
            case "End Turn": return "让过至回合结束";
            case "Keep": return "保留起手";
            case "Mulligan": return "再调度";
            case "Play land": return "放置地牌";
            case "Yes": return "是";
            case "No": return "否";
            default: return GamePreferences.NativeText(option.label);
        }
    }
    private static string Phase(string phase)
    {
        switch(phase)
        {
            case "UNTAP":return "重置"; case "UPKEEP":return "维持"; case "DRAW":return "抓牌";
            case "MAIN1":return "战斗前主阶段"; case "MAIN2":return "战斗后主阶段";
            case "COMBAT_BEGIN":return "战斗开始"; case "COMBAT_DECLARE_ATTACKERS":return "宣告攻击者";
            case "COMBAT_DECLARE_BLOCKERS":return "宣告阻挡者"; case "COMBAT_DAMAGE":return "战斗伤害";
            case "COMBAT_END":return "战斗结束"; case "END_OF_TURN":return "结束步骤"; case "CLEANUP":return "清除步骤";
            default:return phase=="null"?"起手选择":phase;
        }
    }
    private static RectTransform Rect(Transform parent,string name,Vector2 pos,Vector2 size)
    {var r=IntegratedUiUtility.CreateRect(parent,name);IntegratedUiUtility.Place(r,pos,size);return r;}
    private static RectTransform Panel(Transform parent,string name,Vector2 pos,Vector2 size,Color color)
    {var r=Rect(parent,name,pos,size);var i=r.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=true;return r;}
    private static Text Label(Transform parent,string name,string text,Vector2 pos,Vector2 size,int fontSize,Color color)
    {var t=IntegratedUiUtility.Text(parent,name,text,fontSize,color);IntegratedUiUtility.Place(t.rectTransform,pos,size);t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
    private static Button Button(Transform parent,string name,string text,Vector2 pos,Vector2 size,Action click,Color color)
    {var r=Panel(parent,name,pos,size,color);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();b.onClick.AddListener(()=>click());Label(r,"Label",text,Vector2.zero,size-new Vector2(12,4),18,Paper);if(!string.IsNullOrEmpty(text))IntegratedUiUtility.StyleButton(b,color);return b;}
    private static RectTransform Scroll(Transform parent,string name,Vector2 pos,Vector2 size)
    {
        var r=Rect(parent,name,pos,size);var image=r.gameObject.AddComponent<Image>();image.color=new Color(0,0,0,.03f);
        r.gameObject.AddComponent<RectMask2D>();var scroll=r.gameObject.AddComponent<ScrollRect>();
        var content=Rect(r,"Content",Vector2.zero,size);content.anchorMin=content.anchorMax=new Vector2(.5f,1);content.pivot=new Vector2(.5f,1);
        scroll.viewport=r;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
        return content;
    }
    private static void TopLabel(RectTransform parent,string name,string text,float y,float width,float height)
    {var t=Label(parent,name,text,Vector2.zero,new Vector2(width,height),18,Paper);t.rectTransform.anchorMin=t.rectTransform.anchorMax=new Vector2(.5f,1);t.rectTransform.pivot=new Vector2(.5f,1);t.rectTransform.anchoredPosition=new Vector2(0,-y);}
    private static void Clear(Transform root)
    {if(root==null)return;foreach(var child in root.Cast<Transform>().ToArray()){child.gameObject.SetActive(false);child.SetParent(null,false);Destroy(child.gameObject);}}
}
