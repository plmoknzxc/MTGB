using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Binds the authored scene to Forge without rebuilding its Canvas or environment.
public sealed partial class ForgeBattlefieldController
{
    private bool integrated;
    private Transform authored;
    private RectTransform ownHand, otherHand, stackContent;
    private Button advanceButton, endTurnButton, undoManaButton;
    private RectTransform mulliganChoices;
    private Button exitSpectatorButton;
    private bool IsMulliganChoice => view?.awaitingInput==true && (view.request.kind.Contains("Mulligan") || view.request.kind=="InputChooseStartingHand");

    private Transform Node(string path)
    {
        var node = authored.Find(path);
        if (node == null) throw new InvalidOperationException("IntegratedBattlefield missing UI node: " + path);
        return node;
    }
    private Text SceneText(string path) => Node(path).GetComponent<Text>();
    private void SetText(string path, string value) { SceneText(path).text = value; }
    private Button SceneButton(string path)
    {
        var node = Node(path);
        var button = node.GetComponent<Button>() ?? node.gameObject.AddComponent<Button>();
        button.targetGraphic = node.GetComponent<Graphic>();
        if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
        button.onClick.RemoveAllListeners();
        if(path.StartsWith("CornerCluster/ControlRow/"))IntegratedUiUtility.StyleButton(button,Ink);
        return button;
    }
    private void BuildIntegrated()
    {
        authored = GameObject.Find("Canvas").transform;
        canvas = authored.GetComponent<Canvas>();
        surface = (RectTransform)authored;
        foreach (var card in CardDatabaseRepository.LoadAll())
            if (!card.forgeAmbiguous && !catalog.ContainsKey(card.PrintingKey)) catalog.Add(card.PrintingKey, card);
        ownHand = (RectTransform)Node("PlayerHandRoot");
        otherHand = (RectTransform)Node("OpponentHandRoot");
        stackContent = (RectTransform)Node("StackColumn/Viewport/Content");
        cardsRoot = Rect(authored, "ForgePermanents", Vector2.zero, new Vector2(1920, 1080));
        cardsRoot.SetSiblingIndex(Node("InspectPanel").GetSiblingIndex());
        // Runtime additions follow the existing corner and inspector anchors when the scene is edited.
        var panel = Panel(surface, "ForgeChoices", new Vector2(-60, 20), new Vector2(300, 290), Ink);
        promptText = Label(panel, "Prompt", "", new Vector2(0, 112), new Vector2(276, 70), 16, Gold);
        actionRoot = Scroll(panel, "Choices", new Vector2(0, -30), new Vector2(286, 220));
        // Build can precede the opening projection. A real request reveals it.
        panel.gameObject.SetActive(false);
        previewRoot = Rect(Node("InspectPanel"), "ForgeZoneContents", Vector2.zero, new Vector2(262, 530));
        connectionText = SceneText("CornerCluster/ControlRow/RoundText");
        phaseText = SceneText("CornerCluster/ControlRow/PhaseText");
        advanceButton = SceneButton("CornerCluster/ControlRow/SkipCombatButton");
        endTurnButton = SceneButton("CornerCluster/ControlRow/EndTurnButton");
        undoManaButton=Button(Node("CornerCluster/ControlRow"),"UndoMana","撤回 Z",new Vector2(84,146),new Vector2(112,30),()=>{},Ink);
        PreferenceText.SetSize(undoManaButton.GetComponentInChildren<Text>(),12);
        Node("GearBox/ConcedeButton").gameObject.SetActive(false);
        var settings=SceneButton("GearBox/SettingsButton");settings.gameObject.SetActive(true);settings.interactable=true;
        foreach(Transform child in settings.transform)child.gameObject.SetActive(false);
        var settingsRect=(RectTransform)settings.transform;settingsRect.anchorMin=settingsRect.anchorMax=settingsRect.pivot=new Vector2(.5f,.5f);settingsRect.anchoredPosition=Vector2.zero;settingsRect.sizeDelta=new Vector2(44,44);
        IntegratedUiUtility.StyleButton(settings,Ink);
        var iconNode=settings.transform.Find("GearIcon") as RectTransform;
        if(iconNode==null)iconNode=Rect(settings.transform,"GearIcon",Vector2.zero,new Vector2(28,28));
        iconNode.gameObject.SetActive(true);
        var icon=iconNode.GetComponent<ForgeGearIcon>()??iconNode.gameObject.AddComponent<ForgeGearIcon>();icon.color=Paper;icon.raycastTarget=false;
        settings.onClick.AddListener(()=>GameSettingsPanel.Show(surface,concede));
        // No rule-backed timer/settings or exile payload is currently supplied. Do not show fake state.
        Node("CornerCluster/ControlRow/DecisionTimerWidget").gameObject.SetActive(false);
        integrated = true;
        PresentControlMode();
        PresentSpectatorExit();
    }
    private void PresentSpectatorExit()
    {
        bool spectating=service?.IsSpectator==true;
        if(exitSpectatorButton==null&&spectating){
            exitSpectatorButton=Button(Node("GearBox"),"ExitSpectator",TapPaymentText("退出观战","Leave spectating","観戦を終了"),new Vector2(110,0),new Vector2(152,44),()=>{
                if(service?.IsSpectator!=true)return;
                service.Disconnect();enabled=false;UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
            },Ink);
            PreferenceText.SetSize(exitSpectatorButton.GetComponentInChildren<Text>(),19);
            SetOverlayLayer((RectTransform)exitSpectatorButton.transform,1,true);
        }
        if(exitSpectatorButton==null)return;
        exitSpectatorButton.gameObject.SetActive(spectating);
        var label=exitSpectatorButton.GetComponentInChildren<Text>();
        label.text=TapPaymentText("退出观战","Leave spectating","観戦を終了");
        label.horizontalOverflow=HorizontalWrapMode.Overflow;
        float width=Mathf.Max(152,Mathf.Ceil(label.preferredWidth)+24);
        var rect=(RectTransform)exitSpectatorButton.transform;
        rect.sizeDelta=new Vector2(width,44);
        // Reserve the existing 220px view/return/results slot beside settings.
        rect.anchoredPosition=new Vector2(276+width*.5f,0);
        label.rectTransform.sizeDelta=new Vector2(width-16,40);
    }
    private void PresentIntegrated(string status)
    {
        PresentSpectatorExit();
        PresentControlMode();
        BeginZoneMotion();
        BeginArenaPresentation();
        Clear(actionRoot);
        connectionText.text = status;
        phaseText.text = view == null || view.turn == 0 ? "起手选择" : Phase(view.phase);
        SetText("CornerCluster/ControlRow/TurnChip/TurnNumber", (view?.turn ?? 0).ToString());
        SetText("CornerCluster/ControlRow/TurnChip/TurnLabel",TapPaymentText("第 "+(view?.turn??0)+" 回合 · ","Turn "+(view?.turn??0)+" · ","ターン "+(view?.turn??0)+" · ")+(view?.turnSeat==seat?TapPaymentText("你的回合","Your turn","あなたのターン"):TapPaymentText("对手回合","Opponent turn","対戦相手のターン")));
        SetText("PriorityPill/Text", view?.awaitingInput==true ? view.request.kind=="InputPassPriority"?"你有优先权":"等待你完成选择" : "等待对手操作");
        if(service?.IsSpectator==true) { SetText("PriorityPill/Text","观战 · 仅公开信息"); string name=PlayerDisplayName(view?.turnSeat??0);SetNamedPlayerText(SceneText("CornerCluster/ControlRow/TurnChip/TurnLabel"),TapPaymentText(name+"的回合",name+"'s turn",name+"のターン")); }
        if(IsDeclaringAttack)SetText("PriorityPill/Text","拖动生物选择攻击对象");
        // The former room/status line is hidden in the compact phase dock.
        // Preserve actionable transport feedback in the existing status slot.
        if(service?.IsReconnecting==true||status=="连接中断 · 正在恢复对局")
            SetText("PriorityPill/Text",TapPaymentText("正在重新连接…","Reconnecting…","再接続中…"));
        else if(service?.HasPendingAction==true||status=="正在提交操作…")
            SetText("PriorityPill/Text",TapPaymentText("正在提交操作…","Submitting action…","操作を送信中…"));
        promptText.text = view == null ? "等待对局状态" : Prompt(view, seat);
        foreach (var p in view?.players ?? Array.Empty<ForgePlayerView>())
        {
            bool own = p.seat == seat;
            string plate = own ? "PlayerPlate" : "OpponentPlate";
            var playerLabel=SceneText(plate+"/Name");
            SetNamedPlayerText(playerLabel,PlayerDisplayName(p.seat),p.seat);
            SetText(plate + "/Life", p.life.ToString());
            SetText(plate + "/Sub", PlayerSummary(p,service?.LobbyState?.series));
            PresentPlayerLife(plate);
            PresentPlayerSummary(plate,p,service?.LobbyState?.series);
            var avatar = SceneButton(plate);
            // Authored zone piles can overlap the plate. The target surface must remain reachable.
            avatar.transform.SetAsLastSibling();
            int target = IsDeclaringAttack?AttackOption("player",p.targetId):OptionIndex("player:" + p.targetId); long captured = requestId;
            avatar.interactable = target >= 0 && canAct;
            avatar.onClick.RemoveAllListeners();
            avatar.onClick.AddListener(() => {if(IsDeclaringAttack)ChooseAttackDestination(target,captured);else Choose(target,captured);});
            // Keep scene names stable for authoring; validation resolves the plate by seat.
            var avatarColors = avatar.colors; avatarColors.normalColor = target >= 0 && canAct ? Gold : Color.white; avatarColors.disabledColor=Color.white; avatar.colors = avatarColors;
            string zones = own ? "ZonePiles_PlayerBottomLeft" : "ZonePiles_OpponentTopRight";
            PresentZonePiles(p,zones);
            if (p.seat==DisplayedHandSeat)
            {
                IntegratedRow(ownHand, VisibleHand(p).Concat(p.seat==ActionPlayerSeat?AvailableExileCards():Array.Empty<ForgeCardView>()).ToArray(), "Hand", 100, 124, 172, 7);
            }
            else PresentOtherHand(p);
            var field = p.battlefield ?? Array.Empty<ForgeCardView>();
            PresentEmblems(p,own);
            PresentPlayerCounters(p,own);
            IntegratedRow(cardsRoot, field.Where(c => c.creature).ToArray(), own ? "YourCreatures" : "OpponentCreatures", own ? -70 : 135, 104, 140, 8);
            IntegratedRow(cardsRoot, field.Where(c => c.land && !c.creature).ToArray(), own ? "YourLands" : "OpponentLands", own ? -230 : 295, 92, 116, 9);
            var support = field.Where(c => !c.land && !c.creature).ToArray();
            IntegratedRow(cardsRoot, support.Where(c => !c.planeswalker && !c.battle).ToArray(), own ? "YourSupport" : "OpponentSupport", own ? -230 : 295, 104, 140, 8);
            IntegratedRow(cardsRoot, support.Where(c => c.planeswalker && !c.battle).ToArray(), own ? "YourPlaneswalkers" : "OpponentPlaneswalkers", 0, 104, 140, 5);
            IntegratedRow(cardsRoot, support.Where(c => c.battle).ToArray(), own ? "YourBattles" : "OpponentBattles", 0, 104, 140, 5);
            ArrangeSupportRegions(own);
        }
        PresentManaPool();
        PresentStackCards();
        BindIntegratedAction(advanceButton, "ok", "确认", "Label");
        StyleAdvanceButton();
        advanceButton.gameObject.SetActive(!IsMulliganChoice && CompletedSingleCardOption(view?.request)<0 && OptionIndex("ok")>=0 && view?.request?.kind.Contains("PayMana")!=true);
        RefreshPriorityAdvanceVisibility();
        if(view?.request?.selectionPurpose=="optionalReplacement")advanceButton.gameObject.SetActive(false);
        endTurnButton.gameObject.SetActive(false);
        PresentManaPaymentActions();
        int undo=OptionIndex("cancel");
        undoManaButton.gameObject.SetActive(view?.request?.kind=="InputPassPriority" && undo>=0 && view.request.options[undo].label.StartsWith("Undo"));
        undoManaButton.GetComponentInChildren<Text>().text=TapPaymentText("撤回 Z","Undo Z","戻す Z");
        undoManaButton.onClick.RemoveAllListeners();undoManaButton.onClick.AddListener(()=>TryUndoPresentedAction());undoManaButton.interactable=canAct;
        SetText("CornerCluster/ControlRow/EndTurnButton/Finish", "");
        int count = 0;
        bool localLandChoices=PrepareLandChoices();
        if(localLandChoices)advanceButton.gameObject.SetActive(false);
        bool handControls=PresentHandChoiceActions();
        foreach (var option in view?.awaitingInput == true ? view.request.options : Array.Empty<ForgeOption>())
        {
            if(CompletedSingleCardOption(view.request)>=0)continue;
            if(IsManaPaymentDecision && (option.id=="ok" || option.id=="cancel"))continue;
            if(view.request.selectionPurpose=="optionalReplacement")continue;
            if(localLandChoices || HasTargetChoice)continue;
            if(handControls&&!HasChoiceCard(option))continue;
            if(IsCardPickerRequest)continue;
            if(view.request.kind=="InputAttack" || view.request.kind=="OrderAbilities" || view.request.kind=="AssignAmount" || IsMulliganChoice)continue;
            if(option.id=="ok" || view.request.kind=="InputPassPriority")continue;
            if(IsTargeting && (option.id=="cancel" || option.id=="reset-targets"))continue;
            if(IsPayingPreview)continue;
            if (RepresentedOnBoard(option.id)) continue;
            int index = OptionIndex(option.id); long captured = requestId;
            var button = Button(actionRoot, "Option_" + index, RequestOptionLabel(option), Vector2.zero, new Vector2(286,48), () => Choose(index, captured), Ink);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0,-count*56);count++;
            button.interactable = canAct;
        }
        actionRoot.sizeDelta = new Vector2(286, Math.Max(264, count * 56));
        EndArenaPresentation();
        EndZoneMotion();
        // Hand cards rise above the board, but must not cover the priority mode.
        // Modal presenters below still place their blockers above both.
        if(controlModeButton!=null)controlModeButton.transform.SetAsLastSibling();
        if(pauseNextPhaseButton!=null)pauseNextPhaseButton.transform.SetAsLastSibling();
        PresentTargeting();
        PresentTargetChoiceActions();
        if(IsTargeting)advanceButton.gameObject.SetActive(false);
        PresentAttackSelection();
        PresentBlocking();
        PresentMulliganChoices();
        PresentAbilityOrder();
        PresentAllocation();
        if(localLandChoices){if(choiceDismiss!=null && choiceDismiss.gameObject.activeSelf)choiceDismiss.SetAsLastSibling();landChoiceMenu.SetAsLastSibling();}
        Node("StackColumn").gameObject.SetActive((view?.stackCount ?? 0)>0);
        actionRoot.parent.parent.gameObject.SetActive(view?.request?.kind!="InputBlock" && view?.request?.kind!="AssignAmount" && view?.request?.kind!="OrderAbilities" && !IsMulliganChoice && view?.request?.kind!="InputAttack" && !localLandChoices && !IsPayingPreview && (count>0 || !IsTargeting && view?.awaitingInput==true && view.request.kind!="InputPassPriority"));
        if(view?.request?.selectionPurpose=="optionalReplacement")actionRoot.parent.parent.gameObject.SetActive(false);
        if(HasTargetChoice)actionRoot.parent.parent.gameObject.SetActive(false);
        if(IsManaPaymentDecision && count==0)actionRoot.parent.parent.gameObject.SetActive(false);
        if(DirectHandSelection && count==0)actionRoot.parent.parent.gameObject.SetActive(false);
        // Card pickers own their complete surface; an empty generic panel must
        // not remain behind their narrower confirmation window.
        if(IsCardPickerRequest) {
            actionRoot.parent.parent.gameObject.SetActive(false);
            advanceButton.gameObject.SetActive(false);
        }
        Canvas.ForceUpdateCanvases();
        PresentCardSelection();
        PresentChoiceHint();
        PresentStackList();
        CompleteSelectedAttackDeclaration();
        if(Node("InspectPanel").gameObject.activeSelf && inspectedCardId!=0) {
            var latest=(view?.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>(p.battlefield??Array.Empty<ForgeCardView>()).Concat(p.emblems??Array.Empty<ForgeCardView>()).Concat(VisibleEffectMarkers(p))).Concat(view?.globalEffects??Array.Empty<ForgeCardView>()).FirstOrDefault(c=>c.id==inspectedCardId);
            if(latest!=null){int revision=inspectHoverRevision;InspectIntegrated(latest);inspectHoverRevision=revision;}
            else { inspectedCardId=0; Node("InspectPanel").gameObject.SetActive(false); }
        }
    }
    private void PresentMulliganChoices()
    {
        if(mulliganChoices!=null){mulliganChoices.gameObject.SetActive(false);Clear(mulliganChoices);}
        if(!IsMulliganChoice)return;
        if(mulliganChoices==null)mulliganChoices=Panel(surface,"MulliganChoices",new Vector2(0,-135),new Vector2(520,154),Ink);
        mulliganChoices.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(520,154,12);
        string prompt=view.request.kind=="InputChooseStartingHand"?TapPaymentText("选择要保留的起手；其余起手将洗回牌库。","Choose a starting hand; the other hands will be shuffled into your library.","最初の手札を選択。他の手札はライブラリーに戻して切り直します。"):Prompt(view,seat);
        var promptLabel=Label(mulliganChoices,"Prompt",prompt,Vector2.zero,new Vector2(488,60),19,Paper);
        promptLabel.raycastTarget=false;
        float promptHeight=Mathf.Max(60,promptLabel.preferredHeight+8),panelHeight=promptHeight+94;
        mulliganChoices.sizeDelta=new Vector2(520,panelHeight);
        promptLabel.rectTransform.sizeDelta=new Vector2(488,promptHeight);
        promptLabel.rectTransform.anchoredPosition=new Vector2(0,panelHeight*.5f-16-promptHeight*.5f);
        var options=view.request.options.Where(o=>!RepresentedOnBoard(o.id)).ToArray();
        for(int i=0;i<options.Length;i++)
        {
            var option=options[i];int index=OptionIndex(option.id);long captured=requestId;
            var button=Button(mulliganChoices,"Mulligan_"+option.id,RequestOptionLabel(option),
                new Vector2((i-(options.Length-1)*.5f)*238,-panelHeight*.5f+38),new Vector2(226,46),()=>Choose(index,captured),option.id=="ok"?new Color(.25f,.34f,.39f):Ink);
            button.interactable=canAct && !submitted;
        }
        mulliganChoices.gameObject.SetActive(true);mulliganChoices.SetAsLastSibling();
    }
    private void IntegratedRow(RectTransform parent, ForgeCardView[] cards, string row, float y, float width, float height, int pageSize)
    {
        LayoutArenaRow(parent, cards ?? Array.Empty<ForgeCardView>(), row);
    }
    private void BindIntegratedAction(Button button, string id, string label, string child)
    {
        int index = OptionIndex(id);
        long captured = requestId;
        button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => Choose(index, captured));
        button.interactable = canAct && index >= 0;
        button.transform.Find(child).GetComponent<Text>().text = label;
        if(id=="ok" && index>=0) button.transform.Find(child).GetComponent<Text>().text=RequestOptionLabel(view.request.options[index]);
    }
    private void DisableIntegratedChoices()
    {
        if(landChoiceMenu!=null)foreach(var button in landChoiceMenu.GetComponentsInChildren<Button>())button.interactable=false;
        if(handChoiceActions!=null)foreach(var button in handChoiceActions.GetComponentsInChildren<Button>())button.interactable=false;
        if(attackBatchActions!=null)foreach(var button in attackBatchActions.GetComponentsInChildren<Button>())button.interactable=false;
        if(targetChoiceActions!=null)foreach(var button in targetChoiceActions.GetComponentsInChildren<Button>())button.interactable=false;
        foreach(var button in manaPoolButtons)if(button!=null){button.interactable=false;button.GetComponent<Image>().raycastTarget=false;}
        foreach (var b in ownHand.GetComponentsInChildren<Button>()) b.interactable = false;
        foreach (var b in otherHand.GetComponentsInChildren<Button>()) b.interactable = false;
        Node("PlayerPlate").GetComponent<Button>().interactable = false;
        Node("OpponentPlate").GetComponent<Button>().interactable = false;
        advanceButton.interactable = endTurnButton.interactable = undoManaButton.interactable = false;
        if(manaPaymentActions!=null)foreach(var button in manaPaymentActions.GetComponentsInChildren<Button>())button.interactable=false;
        if(mulliganChoices!=null)foreach(var button in mulliganChoices.GetComponentsInChildren<Button>())button.interactable=false;
        if(allocationPanel!=null)foreach(var button in allocationPanel.GetComponentsInChildren<Button>())button.interactable=false;
        if(abilityOrder!=null)foreach(var button in abilityOrder.GetComponentsInChildren<Button>())button.interactable=false;
        if(stackList!=null)foreach(var button in stackList.GetComponentsInChildren<Button>())if(button.name!="Close")button.interactable=false;
    }
    private const string ChangedBlue="#70BFFF", LostGray="#565C65";
    private static string SafeDetail(string value)=>(value??"").Replace("<","＜").Replace(">","＞");
    private static string ColoredDetail(string value,string color)=>"<color="+color+">"+SafeDetail(value)+"</color>";
    private static string LostDetail(string value)=>"<color="+LostGray+"><s color=#00000000>"+SafeDetail(value)+"</s></color>";
    public static string ChangedTypeText(string original,string current)
    {
        if(string.IsNullOrEmpty(original))return SafeDetail(CurrentLandTypeLabel(current));
        var before=CardTypeText.Parts(original).ToArray();
        var after=CardTypeText.Parts(current).ToArray();
        var parts=after.Select(t=>before.Contains(t)?SafeDetail(CurrentLandTypeLabel(t)):ColoredDetail(CurrentLandTypeLabel(t),ChangedBlue));
        var lost=before.Where(t=>!after.Contains(t)).Select(t=>LostDetail(CurrentLandTypeLabel(t))).ToArray();
        return string.Join(" ",parts)+(lost.Length>0?"\n"+string.Join(" ",lost):"");
    }
    private string LocalizedChangedRules(ForgeCardView card,string original,string current)
        =>ChangedRulesText(PrintedRuleText(card,original),PrintedRuleText(card,current));
    public static string ChangedRulesText(string original,string current)
    {
        // Forge includes struck-out prior words in its ability text. They are
        // history, not part of the current rule; our comparison adds its own loss markup.
        current=System.Text.RegularExpressions.Regex.Replace(current??"",@"<strike>[^<\r\n]*</strike>","");
        string Normalize(string text)=>System.Text.RegularExpressions.Regex.Replace(text??"",@"[\s().]+","").ToLowerInvariant();
        string[] Lines(string text)=>(text??"").Replace("\\n","\n").Replace("\r","").Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray();
        if(original==null)return SafeDetail(current);
        var before=Lines(original);var after=Lines(current);var matched=new bool[after.Length];
        var retained=new System.Collections.Generic.List<string>();
        var lost=new System.Collections.Generic.List<string>();
        foreach(var line in before){
            int index=Array.FindIndex(after,x=>Normalize(x)==Normalize(line));
            if(index>=0){matched[index]=true;retained.Add(SafeDetail(line));}
            else lost.Add(LostDetail(line));
        }
        var parts=new System.Collections.Generic.List<string>();
        for(int i=0;i<after.Length;i++)if(!matched[i])parts.Add(ColoredDetail(after[i],ChangedBlue));
        parts.AddRange(retained);
        parts.AddRange(lost);
        return string.Join("\n",parts);
    }
    private int detailCardId;
    private bool inspectOriginalState;
    private ForgeCardView detailCard;
    private int inspectedCardId, inspectHoverRevision;
    private void LeaveInspectCard() { if(this!=null && isActiveAndEnabled)StartCoroutine(CloseInspectWhenOutside(inspectHoverRevision)); }
    private System.Collections.IEnumerator CloseInspectWhenOutside(int revision)
    {
        yield return new WaitForSecondsRealtime(.15f);
        var panel=(RectTransform)Node("InspectPanel");
        while(revision==inspectHoverRevision && panel.gameObject.activeSelf) {
            var mouse=UnityEngine.InputSystem.Mouse.current;
            if(mouse==null || !RectTransformUtility.RectangleContainsScreenPoint(panel,mouse.position.ReadValue(),canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera)) {
                panel.gameObject.SetActive(false);yield break;
            }
            yield return null;
        }
    }
    private static string CurrentLandTypeLabel(string type)=>CardTypeText.Line(type);
    private bool inspectDualFace;
    private void InspectIntegrated(ForgeCardView card)=>InspectIntegratedCore(card,true);
    private void InspectIntegratedCore(ForgeCardView card,bool showBothFaces)
    {
        inspectedStackTargets=null;
        if(detailCardId!=card.id){detailCardId=card.id;inspectOriginalState=false;}
        detailCard=card;
        inspectHoverRevision++;
        inspectedCardId=card.zone is "Battlefield" or "Command"?card.id:0;
        Node("InspectPanel").gameObject.SetActive(true);
        SetOverlayLayer((RectTransform)Node("InspectPanel"),InspectLayerOffset,true);
        Node("InspectPanel").SetAsLastSibling();
        // Reference 02:17: inspect beside the card, not in a fixed left sidebar.
        var inspected=arenaCards.Values.FirstOrDefault(c=>c.root.name=="Card_"+card.id)?.face;
        if(inspected==null)inspected=stackCards.Values.FirstOrDefault(c=>c.source?.id==card.id)?.tile;
        if(inspected!=null) {
            var panel=(RectTransform)Node("InspectPanel");
            Vector2 p=surface.InverseTransformPoint(inspected.TransformPoint(inspected.rect.center));
            float gap=inspected.rect.width*.5f+panel.rect.width*.5f+24;
            float right=surface.rect.xMax-panel.rect.width*.5f-12;
            p.x=p.x+gap<=right?p.x+gap:p.x-gap;
            p.x=Mathf.Clamp(p.x,surface.rect.xMin+panel.rect.width*.5f+12,right);
            p.y=Mathf.Clamp(p.y,surface.rect.yMin+panel.rect.height*.5f+12,surface.rect.yMax-panel.rect.height*.5f-12);
            panel.anchorMin=panel.anchorMax=new Vector2(.5f,.5f);panel.anchoredPosition=p;
        }
        Clear(previewRoot);
        catalog.TryGetValue(card.printingKey ?? "", out var definition);
        if (card.faceDown) definition = null;
        inspectDualFace=showBothFaces&&!card.faceDown&&!card.copy&&!card.merged
            &&definition?.features?.Any(f=>f!=null&&f.faceIndex==1)==true&&definition.forgeSplitType is "Modal" or "Transform" or "Meld";
        // Permanent copies keep a physical print separate from their current
        // characteristics. Stack copies retain the existing chosen-X cost path.
        bool publicCopy=(card.copy||card.merged)&&!card.faceDown&&card.zone=="Battlefield";
        bool specialized=!card.faceDown&&(card.stateName??"").StartsWith("Specialize",StringComparison.Ordinal);
        var physicalFeature=publicCopy&&card.backFace?definition?.features?.FirstOrDefault(f=>f!=null&&f.faceIndex==1&&f.role==CardFeatureRole.Primary):null;
        string physicalName=physicalFeature?.cardName??definition?.cardName;
        string physicalType=physicalFeature!=null?GamePreferences.FeatureType(physicalFeature,card.originalType):GamePreferences.CardType(definition);
        string physicalRules=physicalFeature!=null?GamePreferences.FeatureRules(physicalFeature):GamePreferences.CardRules(definition);
        string physicalCost=physicalFeature?.manaCost??definition?.manaCost;
        CardDefinition copiedDefinition=null;
        if(publicCopy)catalog.TryGetValue(card.currentArtPrintingKey??"",out copiedDefinition);
        var copiedFeature=copiedDefinition?.features?.FirstOrDefault(f=>f!=null&&f.cardName==card.name);
        string copiedType=CopyTypeText(card.currentType,copiedDefinition,copiedFeature);
        string copiedRules=card.stateName=="EmptyRoom"?"":copiedFeature!=null?GamePreferences.FeatureRules(copiedFeature):copiedDefinition!=null?GamePreferences.CardRules(copiedDefinition):(card.currentRules??"").Replace("\\n","\n");
        if(publicCopy){
            copiedType=definition!=null&&copiedType!=physicalType?ColoredDetail(copiedType,ChangedBlue):SafeDetail(copiedType);
            copiedRules=definition!=null&&copiedRules!=physicalRules?ColoredDetail(copiedRules,ChangedBlue):SafeDetail(copiedRules);
        }
        SetText("InspectPanel/Header", "卡牌详情");
        SetText("InspectPanel/SetNumber", card.faceDown ? "" : card.printingKey ?? "");
        SceneText("InspectPanel/Name").supportRichText=true;
        SetText("InspectPanel/Name", definition != null && !card.faceDown && !card.backFace && !IsPreparedSpell(card) ? GamePreferences.CardName(definition) : card.name);
        SceneText("InspectPanel/TypeLine").supportRichText=true;
        // Live land text expands subtype mana abilities and omits printed
        // reminders. Compare it only when native types or traits really changed.
        SetText("InspectPanel/TypeLine", card.faceDown ? "面朝下" : publicCopy ? copiedType : card.hasCurrentLandState || (!string.IsNullOrEmpty(card.currentType) && card.originalType!=card.currentType) ? ChangedTypeText(card.originalType,card.currentType) : VisibleCardType(card));
        SetText("InspectPanel/Rules", publicCopy && card.hasCopyAbilityChanges ? CopyRulesText(card,copiedDefinition,copiedFeature) : card.hasAbilityChanges ? LocalizedChangedRules(card,card.originalAbilityText,card.currentAbilityText) : card.hasCurrentLandState && card.landTypesChanged || !card.hasCurrentLandState && card.originalRules!=null && card.currentRules!=card.originalRules ? LocalizedChangedRules(card,card.originalRules,card.currentRules) : publicCopy ? copiedRules : card.backFace || IsPreparedSpell(card) || card.token ? PrintedRuleText(card,card.currentRules?.Replace("\\n", "\n")) : GamePreferences.CardRules(definition));
        if(specialized && !card.hasAbilityChanges && card.currentRules==card.originalRules) {
            var nativeFace=new CardFeatureDefinition { cardName=card.name,description=(card.currentRules??"").Replace("\\n","\n") };
            SetText("InspectPanel/Rules",GamePreferences.FeatureRules(nativeFace));
        }
        var meldFeature=!card.faceDown&&card.stateName=="Meld"?definition?.features?.FirstOrDefault(f=>f!=null&&f.faceIndex==1&&f.cardName==card.name):null;
        if(meldFeature!=null&&!card.hasAbilityChanges&&card.currentRules==card.originalRules) {
            SetText("InspectPanel/Name",GamePreferences.FeatureName(meldFeature));
            SetText("InspectPanel/TypeLine",GamePreferences.FeatureType(meldFeature,card.currentType));
            SetText("InspectPanel/Rules",GamePreferences.FeatureRules(meldFeature));
        }
        var spellFeature=!card.faceDown&&(IsPreparedSpell(card)||card.stateName=="Secondary"&&!card.creature&&!card.land||card.zone=="Stack"&&!card.room&&card.stateName is "LeftSplit" or "RightSplit")
            ?definition?.features?.FirstOrDefault(f=>f!=null&&f.cardName==card.name):null;
        if(spellFeature!=null) {
            SetText("InspectPanel/Name",GamePreferences.CardFaceName(card.name));
            SetText("InspectPanel/TypeLine",GamePreferences.FeatureType(spellFeature,card.currentType));
            SetText("InspectPanel/Rules",GamePreferences.FeatureRules(spellFeature));
        }
        if(card.emblem || card.effectMarker || card.faceDown) {
            SetText("InspectPanel/TypeLine",card.emblem?TapPaymentText("徽记","Emblem","紋章"):card.effectMarker?(card.markerKind=="dungeon"?TapPaymentText("地城","Dungeon","ダンジョン"):TapPaymentText("延迟 / 持续效果","Delayed / ongoing effect","遅延／継続する効果")):"面朝下的生物");
            SetText("InspectPanel/Name",card.emblem?EmblemLocalizedName(card):card.effectMarker?MarkerName(card):"面朝下的牌");
            SetText("InspectPanel/Rules",card.emblem?EmblemLocalizedRules(card):card.faceDown?FaceDownRules(card):MarkerRules(card));
            if(card.markerKind=="game"||card.markerKind=="player-protection"||IsSpeedMarker(card))SetText("InspectPanel/TypeLine",card.markerKind=="game"?TapPaymentText("游戏状态","Game state","ゲームの状態"):TapPaymentText("玩家状态","Player state","プレイヤーの状態"));
        }
        if(inspectOriginalState && !card.faceDown){
            SetText("InspectPanel/TypeLine",SafeDetail((publicCopy||specialized)&&definition!=null?physicalType:CurrentLandTypeLabel(card.originalType??card.currentType)));
            SetText("InspectPanel/Rules",SafeDetail(PrintedRuleText(card,((publicCopy||specialized)&&definition!=null?physicalRules:card.originalRules??GamePreferences.CardRules(definition)).Replace("\\n","\n"))));
        }
        if(!card.faceDown&&!card.emblem&&!card.effectMarker&&(!string.IsNullOrEmpty(card.originalName)||publicCopy&&definition!=null)) {
            string baselineName=(publicCopy||specialized)&&definition!=null?physicalName:card.originalName;
            string nativeName=inspectOriginalState?baselineName:card.name;
            string label=GamePreferences.CardFaceName(nativeName);
            SetText("InspectPanel/Name",!inspectOriginalState&&card.name!=baselineName?ColoredDetail(label,ChangedBlue):SafeDetail(label));
        }
        if(card.genericEffectName)SetText("InspectPanel/Name",AbilitySourceName(card));
        var originalButton=Node("InspectPanel").Find("OriginalState")?.GetComponent<Button>();
        if(originalButton==null){
            originalButton=Button(Node("InspectPanel"),"OriginalState",TapPaymentText("原始状态","Original state","元の状態"),Vector2.zero,new Vector2(100,28),()=>{inspectOriginalState=!inspectOriginalState;InspectIntegrated(detailCard);},new Color(.12f,.16f,.2f));
            var r=(RectTransform)originalButton.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(1,1);r.anchoredPosition=new Vector2(-16,-16);
            PreferenceText.SetSize(originalButton.GetComponentInChildren<Text>(),13);
        }
        originalButton.gameObject.SetActive(!card.faceDown&&!card.emblem&&!card.effectMarker&&card.originalRules!=null);
        originalButton.GetComponentInChildren<Text>().text=inspectOriginalState?TapPaymentText("返回变化","Current state","現在の状態"):TapPaymentText("原始状态","Original state","元の状態");
        var originalLabel=originalButton.GetComponentInChildren<Text>();
        PreferenceText.SetSize(originalLabel,13);
        originalLabel.horizontalOverflow=HorizontalWrapMode.Overflow;
        var originalSize=new Vector2(Mathf.Max(100,Mathf.Ceil(originalLabel.preferredWidth)+12),Mathf.Max(28,Mathf.Ceil(originalLabel.preferredHeight)+4));
        ((RectTransform)originalButton.transform).sizeDelta=originalSize;
        originalLabel.rectTransform.sizeDelta=originalSize-new Vector2(8,4);
        var componentsButton=Node("InspectPanel").Find("MeldComponents")?.GetComponent<Button>();
        if(componentsButton==null)componentsButton=Button(Node("InspectPanel"),"MeldComponents","",Vector2.zero,new Vector2(268,32),()=>OpenMeldComponents(detailCard.id),new Color(.12f,.2f,.27f));
        componentsButton.gameObject.SetActive(HasMeldComponents(card));
        componentsButton.GetComponentInChildren<Text>().text=TapPaymentText("查看融合原件（2）","View meld components (2)","合体の構成カードを見る（2）");
        PreferenceText.SetSize(componentsButton.GetComponentInChildren<Text>(),14);
        // A copy's baseline belongs to its copied characteristics. The physical
        // original's complete printed rules already contain its own starting value.
        string intensity=inspectOriginalState&&publicCopy&&definition!=null?"":IntensityDetail(card,inspectOriginalState);
        if(!string.IsNullOrEmpty(intensity))SceneText("InspectPanel/Rules").text+="\n\n"+intensity;
        var statuses=new System.Collections.Generic.List<string>();
        string additionalNames=AdditionalNamesDetail(card);
        if(!string.IsNullOrEmpty(additionalNames))statuses.Add(additionalNames);
        if(card.detained)statuses.Add(ColoredDetail(TapPaymentText("已拘留：不能攻击、阻挡或启动异能。","Detained: cannot attack, block, or activate abilities.","留置されている：攻撃もブロックも起動型能力の起動もできない。"),ChangedBlue));
        if((card.exertedBySeats?.Length??0)>0)statuses.Add(ColoredDetail(TapPaymentText("已耗竭：不会在下列玩家的下一次重置步骤重置：","Exerted: won't untap during the next untap step of player(s): ","督励済み：次のプレイヤーの次のアンタップ・ステップにアンタップしない：")+string.Join(", ",card.exertedBySeats),ChangedBlue));
        if(!card.faceDown&&card.currentKeywordDetails!=null)statuses.AddRange(KeywordDetailChanges(card,card.hasAbilityChanges));
        else if(!card.faceDown&&card.keywords!=null)statuses.AddRange(card.keywords.Where(k=>StatusName(k)!=k).Select(k=>(card.grantedKeywords??Array.Empty<string>()).Contains(k)?ColoredDetail(StatusName(k),ChangedBlue):StatusName(k)));
        if(card.powerBaseChanged||card.toughnessBaseChanged)statuses.Add("<nobr>"+TapPaymentText("基础攻防：","Base P/T: ","基本のP/T：")+(card.powerBaseChanged?ColoredDetail(card.effectiveBasePower.ToString(),ChangedBlue):card.effectiveBasePower.ToString())+"/"+(card.toughnessBaseChanged?ColoredDetail(card.effectiveBaseToughness.ToString(),ChangedBlue):card.effectiveBaseToughness.ToString())+"</nobr>");
        if(card.hasColorBaseline&&card.currentColors!=card.originalColors)statuses.Add(ColoredDetail(TapPaymentText("颜色：","Color: ","色：")+ColorDetail(card.currentColors),ChangedBlue));
        // Put native attribute state before long printed rules so the marker is
        // visible immediately even when the full card text needs scrolling.
        var attributeStates=AttributeStateLabels(card);
        if(!inspectOriginalState&&attributeStates.Length>0) {
            var rules=SceneText("InspectPanel/Rules");
            rules.text=string.Join(" · ",attributeStates.Select(s=>ColoredDetail(s,ChangedBlue)))+"\n\n"+rules.text;
        }
        if(HasPreparedSpell(card))statuses.Add(TapPaymentText("已备法","Prepared","準備済み")+" · "+GamePreferences.CardFaceName(card.preparedSpell.name));
        if(ShouldShowSpentMana(card))statuses.Add(TapPaymentText("施放时已支付：","Mana spent to cast: ","唱えるために支払ったマナ：")+card.spentMana);
        if(card.qualificationRequired>0)statuses.Add((card.qualificationKind=="sacrificedThisTurn"?TapPaymentText("本回合已牺牲永久物：","Permanents sacrificed this turn: ","このターン生け贄に捧げたパーマネント："):TapPaymentText("达成进度：","Progress: ","達成状況："))+card.qualificationCount+" / "+card.qualificationRequired);
        if(statuses.Count>0&&!inspectOriginalState) {
            var text=SceneText("InspectPanel/Rules");
            text.text+=(string.IsNullOrWhiteSpace(text.text)?"":"\n\n")+TapPaymentText("当前状态：","Current state: ","現在の状態：")+string.Join(" · ",statuses);
        }
        if(!inspectOriginalState&&!card.faceDown) {
            foreach(var departure in card.pendingDepartures??Array.Empty<ForgePendingDeparture>())
                SceneText("InspectPanel/Rules").text+=(string.IsNullOrWhiteSpace(SceneText("InspectPanel/Rules").text)?"":"\n")+ColoredDetail(PendingDepartureText(departure),ChangedBlue);
        }
        if(!inspectOriginalState) {
            string attachments=AttachmentDescription(card);
            if(attachments.Length>0)SceneText("InspectPanel/Rules").text+="\n\n"+attachments;
            string exile=TemporaryExileDescription(card);
            if(exile.Length>0)SceneText("InspectPanel/Rules").text+="\n\n"+exile;
        }
        SceneText("InspectPanel/Footer").supportRichText=true;
        bool displayCreature=card.creature;
        if(inspectOriginalState)displayCreature=publicCopy&&definition!=null
            ? ((physicalFeature?.cardTypes??definition.cardTypes)&CardType.Creature)!=0
            : !string.IsNullOrEmpty(card.originalType)?card.originalType.Split(new[]{' ','-'},StringSplitOptions.RemoveEmptyEntries).Contains("Creature")
            : definition?.HasType(CardType.Creature)??card.creature;
        string damageSuffix=card.creature&&displayCreature?" · "+TapPaymentText("伤害","Damage","ダメージ")+" "+card.damage:"";
        SetText("InspectPanel/Footer", displayCreature ? (inspectOriginalState&&card.hasStatBaseline?card.basePower+"/"+card.baseToughness:ModifiedStatsText(card,true))+damageSuffix : "");
        if(inspectOriginalState&&(publicCopy||specialized)&&definition!=null&&displayCreature) {
            var printedStats=physicalFeature??definition.features?.FirstOrDefault(f=>f!=null&&f.faceIndex==0&&f.role==CardFeatureRole.Primary);
            string printedPower=!string.IsNullOrEmpty(printedStats?.powerExpression)?printedStats.powerExpression:(printedStats?.attack??definition.attack).ToString();
            string printedToughness=!string.IsNullOrEmpty(printedStats?.toughnessExpression)?printedStats.toughnessExpression:(printedStats?.defence??definition.defence).ToString();
            SetText("InspectPanel/Footer",printedPower+"/"+printedToughness+damageSuffix);
        }
        if(card.planeswalker)SceneText("InspectPanel/Footer").text+=(card.creature?" · ":"")+TapPaymentText("忠诚值","Loyalty","忠誠度")+" "+card.loyalty;
        if(card.battle)SceneText("InspectPanel/Footer").text+=(card.creature||card.planeswalker?" · ":"")+TapPaymentText("布防值","Defense","守備値")+" "+card.defense;
        var art = Node("InspectPanel/Art").GetComponent<Image>();
        Sprite sprite = null;
        if(card.stateName=="Meld")TryBattlefieldArt(card,out sprite);
        else if(inspectOriginalState && definition!=null)
            CardImageLibrary.TryLoadFullCard(definition,card.backFace?1:0,out sprite);
        else TryBattlefieldArt(card, out sprite);
        if(card.faceDown && !string.IsNullOrEmpty(card.knownFacePrintingKey)) {
            CardImageLibrary.TryLoadPrintingKey(card.knownFacePrintingKey,out sprite);
            SetText("InspectPanel/Name",GamePreferences.CardFaceName(card.knownFaceName));
            SetText("InspectPanel/Rules",FaceDownRules(card)+"\n\n"+TapPaymentText("仅你可见的原牌：","Original card (visible only to you):","あなたにのみ見える元のカード：")+"\n"+KnownFaceRules(card));
        }
        string sectorDetail = inspectOriginalState ? "" : SectorDetail(card);
        if (sectorDetail.Length > 0) SceneText("InspectPanel/Rules").text = sectorDetail + "\n\n" + SceneText("InspectPanel/Rules").text;
        string auxiliaryDetail=inspectOriginalState?"":AuxiliaryDetail(card);
        if(auxiliaryDetail.Length>0)SceneText("InspectPanel/Rules").text=auxiliaryDetail+"\n\n"+SceneText("InspectPanel/Rules").text;
        art.sprite = sprite; art.color = sprite == null ? Ink : Color.white; art.preserveAspect = true;
        var reverse=Node("InspectPanel").Find("ReverseArt") as RectTransform;
        if(reverse==null&&inspectDualFace)reverse=Panel(Node("InspectPanel"),"ReverseArt",Vector2.zero,new Vector2(240,320),Ink);
        if(reverse!=null) {
            reverse.gameObject.SetActive(inspectDualFace);
            if(inspectDualFace) {
                CardImageLibrary.TryLoadFullCard(definition,0,out var front);
                CardImageLibrary.TryLoadFullCard(definition,1,out var back);
                sprite=front;art.sprite=front;art.color=front==null?Ink:Color.white;
                var backImage=reverse.GetComponent<Image>();backImage.sprite=back;backImage.color=back==null?Ink:Color.white;backImage.preserveAspect=true;backImage.raycastTarget=false;
                var label=reverse.Find("MissingArt") as RectTransform;
                if(label==null)label=Label(reverse,"MissingArt","缺失卡图",Vector2.zero,new Vector2(180,40),18,Paper).rectTransform;
                label.gameObject.SetActive(back==null);
            }
        }
        var chosenFace=art.transform.Find("ChosenSpellFace");if(chosenFace!=null)chosenFace.gameObject.SetActive(false);
        var resolvedCost=art.transform.Find("ResolvedManaCost");if(resolvedCost!=null)resolvedCost.gameObject.SetActive(false);
        var missing=art.transform.Find("MissingArt");
        if(missing==null)missing=Label((RectTransform)art.transform,"MissingArt","缺失卡图",Vector2.zero,new Vector2(180,30),18,Paper).transform;
        bool playerProtection=card.markerKind=="player-protection";
        missing.gameObject.SetActive(sprite==null&&!playerProtection);
        var protection=art.transform.Find("PlayerProtection");
        if(playerProtection&&protection==null) {
            protection=Rect((RectTransform)art.transform,"PlayerProtection",Vector2.zero,new Vector2(60,76));
            var icon=protection.gameObject.AddComponent<PlayerProtectionIcon>();icon.color=GrantedKeywordColor;icon.raycastTarget=false;
        }
        if(protection!=null)protection.gameObject.SetActive(playerProtection);
        Clear(Node("InspectPanel/CostRow"));
        var mana=CardSymbolText.Create(Node("InspectPanel/CostRow"),"Mana",20,Paper);
        mana.richText=true;
        string detailCost=publicCopy||specialized?inspectOriginalState?physicalCost??card.originalManaCost??"":card.manaCost??"":inspectOriginalState&&!string.IsNullOrEmpty(card.originalManaCost)?card.originalManaCost:IsPreparedSpell(card)||spellFeature!=null||card.zone=="Stack"?StackManaCost(card):card.originalManaCost!=null&&card.manaCost!=card.originalManaCost?card.manaCost:definition?.manaCost??card.manaCost??"";
        if(string.Equals(detailCost,"no cost",StringComparison.OrdinalIgnoreCase))detailCost="";
        mana.text=CardSymbolText.Format(detailCost);
        bool changedDetailCost=!inspectOriginalState&&(publicCopy&&definition!=null?card.manaCost!=physicalCost:card.originalManaCost!=null&&card.manaCost!=card.originalManaCost);
        if(changedDetailCost) {
            mana.tintAllSprites=true;
            mana.text="<color="+ChangedBlue+">"+mana.text+"</color>";
        }
        // A full scan already includes its printed cost. Reserve this row for
        // changed characteristics or missing artwork, using the same flat icons
        // as component cards instead of the old TMP sprite scale/baseline.
        Node("InspectPanel/CostRow").gameObject.SetActive((sprite==null||changedDetailCost)&&!string.IsNullOrEmpty(detailCost));
        var costStrip=mana.gameObject.AddComponent<TemplateManaCost>();costStrip.Bind(detailCost,22,false);
        mana.enabled=!costStrip.Complete;
        if(changedDetailCost)foreach(var icon in mana.GetComponentsInChildren<RawImage>())icon.color=new Color(.25f,.65f,1f);
        mana.rectTransform.anchorMin=Vector2.zero;mana.rectTransform.anchorMax=Vector2.one;
        mana.rectTransform.offsetMin=mana.rectTransform.offsetMax=Vector2.zero;
        mana.alignment=TMPro.TextAlignmentOptions.Left;
        LayoutInspectPanel(card,inspected);
        PresentPreparedSpellFace((RectTransform)art.transform,card,((RectTransform)art.transform).rect.size);
        // The inspector keeps the original scan; editable faces belong to the
        // hand, public-zone lists and stack, where their values can change.
        PresentComponentFace((RectTransform)art.transform,null);
        var devotionBadge=art.transform.Find("Devotion") as RectTransform;
        if(devotionBadge==null && card.devotionRequired>0) {
            devotionBadge=Panel((RectTransform)art.transform,"Devotion",Vector2.zero,new Vector2(54,44),Ink);
            devotionBadge.anchorMin=devotionBadge.anchorMax=new Vector2(0,.5f);
            devotionBadge.anchoredPosition=new Vector2(20,0);
            Label(devotionBadge,"Value","",Vector2.zero,new Vector2(54,44),20,Paper).horizontalOverflow=HorizontalWrapMode.Overflow;
        }
        if(devotionBadge!=null) {
            devotionBadge.gameObject.SetActive(!card.faceDown && card.devotionRequired>0);
            devotionBadge.Find("Value").GetComponent<Text>().text=card.devotion+"/"+card.devotionRequired;
        }
    }
    private float PresentInspectCounterLegend(RectTransform content,ForgeCardView card,float width,float top)
    {
        var legend=content.Find("CounterLegend") as RectTransform??Rect(content,"CounterLegend",Vector2.zero,Vector2.zero);
        Clear(legend);
        var counters=inspectOriginalState?Array.Empty<ForgeCardCounter>():(card.counters??Array.Empty<ForgeCardCounter>()).Where(c=>c.count>0&&!HasDedicatedCounter(card,c.kind)).ToArray();
        legend.gameObject.SetActive(counters.Length>0);if(counters.Length==0)return 0;
        var title=Label(legend,"Title",TapPaymentText("指示物","Counters","カウンター"),Vector2.zero,new Vector2(width,24),15,Paper);title.alignment=TextAnchor.MiddleLeft;
        var labels=new System.Collections.Generic.List<Text>();
        foreach(var counter in counters) {
            var label=Label(legend,"CounterLabel_"+counter.kind,PermanentCounterName(counter)+" ×"+counter.count,Vector2.zero,new Vector2(width-34,28),15,new Color(.44f,.75f,1f));
            label.alignment=TextAnchor.MiddleLeft;label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Overflow;label.raycastTarget=false;labels.Add(label);
        }
        int columns=labels.All(t=>t.preferredWidth<=width*.5f-34)?2:1;
        float rowHeight=Mathf.Max(28,labels.Max(t=>t.preferredHeight)+4),cell=width/columns;
        float height=24+Mathf.Ceil(counters.Length/(float)columns)*rowHeight+8;
        legend.anchorMin=legend.anchorMax=legend.pivot=new Vector2(0,1);legend.anchoredPosition=new Vector2(0,-top);legend.sizeDelta=new Vector2(width,height);
        title.rectTransform.anchorMin=title.rectTransform.anchorMax=title.rectTransform.pivot=new Vector2(0,1);title.rectTransform.anchoredPosition=Vector2.zero;
        for(int i=0;i<counters.Length;i++) {
            var counter=counters[i];float x=i%columns*cell,y=24+i/columns*rowHeight+rowHeight*.5f;
            var label=labels[i];label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(0,1);label.rectTransform.pivot=new Vector2(0,.5f);
            label.rectTransform.anchoredPosition=new Vector2(x+32,-y);label.rectTransform.sizeDelta=new Vector2(cell-34,rowHeight);
            var tile=Panel(legend,"CounterIcon_"+counter.kind,Vector2.zero,new Vector2(24,24),new Color(.025f,.045f,.065f,.94f));
            tile.anchorMin=tile.anchorMax=new Vector2(0,1);tile.anchoredPosition=new Vector2(x+12,-y);tile.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(24,24,6);tile.GetComponent<Image>().raycastTarget=false;
            bool positive=counter.kind=="P1P1",negative=counter.kind=="M1M1",lore=counter.kind=="LORE";
            if(positive||negative||lore) {
                var sign=Label(tile,"Symbol",positive?"+":negative?"−":counter.count.ToString(),Vector2.zero,new Vector2(24,24),18,positive?new Color(.32f,.78f,1f):negative?new Color(1f,.32f,.32f):Gold);
                sign.fontStyle=FontStyle.Bold;sign.horizontalOverflow=HorizontalWrapMode.Overflow;sign.verticalOverflow=VerticalWrapMode.Overflow;sign.raycastTarget=false;
                if(lore)tile.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(24,24,12);
            } else {
                var icon=Rect(tile,"TypeIcon",Vector2.zero,new Vector2(20,20)).gameObject.AddComponent<PermanentCounterIcon>();icon.Bind(counter.kind);icon.color=counter.kind=="STUN"?new Color(.32f,.78f,1f):Paper;icon.raycastTarget=false;
            }
        }
        return height;
    }
    private void LayoutInspectPanel(ForgeCardView card,RectTransform inspected)
    {
        var panel=(RectTransform)Node("InspectPanel");
        float width=inspectDualFace?548:300,inner=width-32;
        // Enlarge the entire detail together, including artwork, symbols and
        // preference-sized text. Position it using its displayed bounds.
        float detailScale=Mathf.Min(1.2f,(surface.rect.width-24)/width);
        panel.localScale=Vector3.one*detailScale;
        float displayedWidth=width*detailScale;
        var backing=Node("InspectPanel/Rules").GetComponent<Text>();backing.enabled=false;
        var content=Node("InspectPanel/Rules").Find("RuleScroll/Content") as RectTransform;
        if(content==null)content=Scroll(Node("InspectPanel/Rules"),"RuleScroll",Vector2.zero,new Vector2(inner,100));
        var rules=content.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if(rules==null)rules=CardSymbolText.Create(content,"SymbolRules",18,Paper);
        LostAbilityStrikeGraphic.Attach(rules);
        rules.font=GamePreferences.TmpFont!=null?GamePreferences.TmpFont:TMPro.TMP_Settings.defaultFontAsset;
        rules.lineSpacing=4;rules.alignment=TMPro.TextAlignmentOptions.TopLeft;
        rules.richText=true;rules.text=CardSymbolText.Format(backing.text);rules.enableAutoSizing=false;rules.fontStyle=TMPro.FontStyles.Normal;
        rules.tintAllSprites=!inspectOriginalState&&(card.hasAbilityChanges||card.hasCurrentLandState&&card.landTypesChanged);
        (rules.GetComponent<PreferenceText>()??rules.gameObject.AddComponent<PreferenceText>()).SetBaseSize(18);
        rules.rectTransform.anchorMin=rules.rectTransform.anchorMax=rules.rectTransform.pivot=new Vector2(0,1);
        rules.rectTransform.anchoredPosition=Vector2.zero;
        float textHeight=Mathf.Ceil(rules.GetPreferredValues(rules.text,inner,10000).y)+24;
        float preferred=textHeight+PresentInspectCounterLegend(content,card,inner,textHeight);
        float artHeight=card.markerKind=="player-protection"||card.emblem && Node("InspectPanel/Art").GetComponent<Image>().sprite==null?80:card.room?172:320;
        float costHeight=Node("InspectPanel/CostRow").gameObject.activeSelf?28:0;
        var typeBacking=SceneText("InspectPanel/TypeLine");typeBacking.enabled=false;
        var typeText=typeBacking.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if(typeText==null)typeText=CardSymbolText.Create(typeBacking.transform,"SymbolType",15,new Color(.67f,.73f,.78f));
        LostAbilityStrikeGraphic.Attach(typeText);
        typeText.font=rules.font;typeText.richText=true;typeText.enableAutoSizing=false;
        typeText.text=typeBacking.text;typeText.alignment=TMPro.TextAlignmentOptions.TopLeft;
        (typeText.GetComponent<PreferenceText>()??typeText.gameObject.AddComponent<PreferenceText>()).SetBaseSize(15);
        typeText.rectTransform.anchorMin=Vector2.zero;typeText.rectTransform.anchorMax=Vector2.one;
        typeText.rectTransform.offsetMin=typeText.rectTransform.offsetMax=Vector2.zero;
        float typeHeight=Mathf.Max(24,Mathf.Ceil(typeText.GetPreferredValues(typeText.text,inner,10000).y)+4);
        var nameLabel=SceneText("InspectPanel/Name");
        var originalButton=(RectTransform)Node("InspectPanel/OriginalState");
        float originalHeight=originalButton.gameObject.activeSelf?originalButton.rect.height+8:0;
        float nameWidth=inner;
        nameLabel.rectTransform.sizeDelta=new Vector2(nameWidth,34);
        nameLabel.horizontalOverflow=HorizontalWrapMode.Wrap;nameLabel.resizeTextForBestFit=false;
        (nameLabel.GetComponent<PreferenceText>()??nameLabel.gameObject.AddComponent<PreferenceText>()).SetBaseSize(22);
        float nameHeight=Mathf.Max(34,Mathf.Ceil(nameLabel.preferredHeight)+4);
        float footerHeight=string.IsNullOrEmpty(SceneText("InspectPanel/Footer").text)?0:28;
        Node("InspectPanel/Footer").gameObject.SetActive(footerHeight>0);
        float targetHeight=StackTargetDetailsHeight();
        float componentHeight=HasMeldComponents(card)?Mathf.Max(32,Mathf.Ceil(Node("InspectPanel/MeldComponents").GetComponentInChildren<Text>().preferredHeight)+6):0;
        float fixedHeight=16+nameHeight+originalHeight+costHeight+artHeight+12+componentHeight+(componentHeight>0?8:0)+typeHeight+12+footerHeight+16+targetHeight;
        float available=Mathf.Min((surface.rect.height-24)/detailScale,720);
        if(fixedHeight+preferred>available){float shrink=Mathf.Min(90,fixedHeight+preferred-available);artHeight-=shrink;fixedHeight-=shrink;}
        float rulesHeight=Mathf.Clamp(preferred,28,Mathf.Max(28,available-fixedHeight));
        float height=fixedHeight+rulesHeight;
        float displayedHeight=height*detailScale;
        panel.sizeDelta=new Vector2(width,height);
        var background=panel.GetComponent<Image>();background.color=new Color(.065f,.075f,.095f,1f);
        background.sprite=IntegratedUiUtility.RoundedRect(340,500,12);background.type=Image.Type.Simple;background.material=null;
        var opacity=panel.GetComponent<CanvasGroup>();if(opacity!=null)opacity.alpha=1;
        var outline=panel.GetComponent<Outline>();if(outline!=null)outline.enabled=false;
        Node("InspectPanel/Header").gameObject.SetActive(false);
        Node("InspectPanel/SetNumber").gameObject.SetActive(false);
        float y=16;
        void Place(string name,float h) {
            var rect=(RectTransform)Node("InspectPanel/"+name);
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);
            rect.anchoredPosition=new Vector2(0,-y);rect.sizeDelta=new Vector2(inner,h);y+=h;
            var text=rect.GetComponent<Text>();if(text==null)return;
            text.alignment=TextAnchor.UpperLeft;text.horizontalOverflow=HorizontalWrapMode.Wrap;
            text.verticalOverflow=VerticalWrapMode.Truncate;text.resizeTextForBestFit=false;
            (text.GetComponent<PreferenceText>()??text.gameObject.AddComponent<PreferenceText>()).SetBaseSize(name=="Name"?22:15);
            text.verticalOverflow=VerticalWrapMode.Truncate;text.fontStyle=FontStyle.Normal;
            text.color=name=="TypeLine"?new Color(.67f,.73f,.78f):Paper;
        }
        Place("Name",nameHeight);
        if(originalHeight>0){
            originalButton.anchorMin=originalButton.anchorMax=originalButton.pivot=new Vector2(1,1);
            originalButton.anchoredPosition=new Vector2(-16,-y);y+=originalHeight;
        }
        Place("CostRow",costHeight);
        var art=(RectTransform)Node("InspectPanel/Art");
        art.anchorMin=art.anchorMax=new Vector2(.5f,1);art.pivot=new Vector2(.5f,.5f);
        float artWidth=inspectDualFace?(inner-12)*.5f:inner;
        art.anchoredPosition=new Vector2(inspectDualFace?-(artWidth+12)*.5f:0,-y-artHeight*.5f);
        art.sizeDelta=card.room?new Vector2(artHeight,inner):new Vector2(artWidth,artHeight);
        if(inspectDualFace) {
            var reverse=(RectTransform)Node("InspectPanel/ReverseArt");
            reverse.anchorMin=reverse.anchorMax=new Vector2(.5f,1);reverse.pivot=new Vector2(.5f,.5f);
            reverse.anchoredPosition=new Vector2((artWidth+12)*.5f,-y-artHeight*.5f);reverse.sizeDelta=new Vector2(artWidth,artHeight);
        }
        art.localRotation=Quaternion.Euler(0,0,card.room?-90:0);y+=artHeight+12;
        if(componentHeight>0){Place("MeldComponents",componentHeight);y+=8;}
        Place("TypeLine",typeHeight);y+=12;Place("Rules",rulesHeight);Place("Footer",footerHeight);
        PresentStackTargetDetails(panel,y,targetHeight);
        var viewport=(RectTransform)content.parent;viewport.sizeDelta=new Vector2(inner,rulesHeight);
        content.sizeDelta=new Vector2(inner,Mathf.Max(preferred,rulesHeight));content.anchoredPosition=Vector2.zero;
        rules.rectTransform.sizeDelta=new Vector2(inner,textHeight);
        rules.ForceMeshUpdate();
        Vector2 p=panel.anchoredPosition;
        if(inspected!=null){p=surface.InverseTransformPoint(inspected.TransformPoint(inspected.rect.center));
            float gap=inspected.rect.width*.5f+displayedWidth*.5f+24;
            p.x+=p.x+gap<=surface.rect.xMax-displayedWidth*.5f-12?gap:-gap;}
        panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);
        p.x=Mathf.Clamp(p.x,surface.rect.xMin+displayedWidth*.5f+12,surface.rect.xMax-displayedWidth*.5f-12);
        p.y=Mathf.Clamp(p.y,surface.rect.yMin+displayedHeight*.5f+12,surface.rect.yMax-displayedHeight*.5f-12);
        panel.anchoredPosition=p;
    }
    private void ConfirmIntegratedConcede()
    {
        actionRoot.parent.parent.gameObject.SetActive(true);
        Clear(actionRoot);
        TopLabel(actionRoot, "Confirmation", "确认认输？", 0, 280, 40);
        var confirm = Button(actionRoot, "ConfirmConcede", "确认认输", Vector2.zero, new Vector2(260, 45), () => concede?.Invoke(), Ink);
        var cancel = Button(actionRoot, "CancelConcede", "继续对局", Vector2.zero, new Vector2(260, 45), () => PresentIntegrated(connectionText.text), Ink);
        int offset = 55;
        foreach (var button in new[] { confirm, cancel })
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -offset); offset += 55;
        }
    }
}
