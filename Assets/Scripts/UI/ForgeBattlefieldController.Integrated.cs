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
    private bool IsMulliganChoice => view?.awaitingInput==true && view.request.kind.Contains("Mulligan");

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
        previewRoot = Rect(Node("InspectPanel"), "ForgeZoneContents", Vector2.zero, new Vector2(262, 530));
        connectionText = SceneText("CornerCluster/ControlRow/RoundText");
        phaseText = SceneText("CornerCluster/ControlRow/PhaseText");
        advanceButton = SceneButton("CornerCluster/ControlRow/SkipCombatButton");
        endTurnButton = SceneButton("CornerCluster/ControlRow/EndTurnButton");
        undoManaButton=Button(Node("CornerCluster/ControlRow"),"UndoMana","撤回 Z",new Vector2(-190,-28),new Vector2(96,34),()=>{},Ink);
        Node("GearBox/ConcedeButton").gameObject.SetActive(false);
        var settings=SceneButton("GearBox/SettingsButton");settings.gameObject.SetActive(true);settings.interactable=true;
        foreach(Transform child in settings.transform)child.gameObject.SetActive(false);
        var settingsRect=(RectTransform)settings.transform;settingsRect.anchorMin=settingsRect.anchorMax=settingsRect.pivot=new Vector2(.5f,.5f);settingsRect.anchoredPosition=Vector2.zero;settingsRect.sizeDelta=new Vector2(44,44);
        var settingsImage=settings.GetComponent<Image>();settingsImage.sprite=IntegratedUiUtility.RoundedRect(44,44,8);settingsImage.color=Ink;
        var icon=Rect(settings.transform,"GearIcon",Vector2.zero,new Vector2(28,28)).gameObject.AddComponent<ForgeGearIcon>();icon.color=Paper;icon.raycastTarget=false;
        settings.onClick.AddListener(()=>GameSettingsPanel.Show(surface,concede));
        // No rule-backed timer/settings or exile payload is currently supplied. Do not show fake state.
        Node("CornerCluster/ControlRow/DecisionTimerWidget").gameObject.SetActive(false);
        integrated = true;
    }
    private void PresentIntegrated(string status)
    {
        PresentControlMode();
        BeginArenaPresentation();
        Clear(otherHand); Clear(actionRoot);
        connectionText.text = status;
        phaseText.text = view == null || view.turn == 0 ? "起手选择" : Phase(view.phase);
        SetText("CornerCluster/ControlRow/TurnChip/TurnNumber", (view?.turn ?? 0).ToString());
        SetText("CornerCluster/ControlRow/TurnChip/TurnLabel", view?.turnSeat == seat ? "你的回合" : "对手回合");
        SetText("PriorityPill/Text", view?.awaitingInput==true ? view.request.kind=="InputPassPriority"?"你有优先权":"等待你完成选择" : "等待对手操作");
        if(service?.IsSpectator==true) { SetText("PriorityPill/Text","观战 · 仅公开信息"); SetText("CornerCluster/ControlRow/TurnChip/TurnLabel", "玩家 " + view?.turnSeat + " 回合"); }
        if(IsDeclaringAttack)SetText("PriorityPill/Text","拖动生物选择攻击对象");
        promptText.text = view == null ? "等待对局状态" : Prompt(view, seat);
        foreach (var p in view?.players ?? Array.Empty<ForgePlayerView>())
        {
            bool own = p.seat == seat;
            string plate = own ? "PlayerPlate" : "OpponentPlate";
            string playerName=service?.LobbyState?.players?.FirstOrDefault(member=>member.seat==p.seat)?.displayName;
            SetText(plate + "/Name", string.IsNullOrWhiteSpace(playerName)?own?"你":"对手":playerName);
            SetText(plate + "/Life", p.life.ToString());
            SetText(plate + "/Sub", "手牌 " + p.handCount + " · 牌库 " + p.libraryCount);
            var avatar = SceneButton(plate);
            // Authored zone piles can overlap the plate. The target surface must remain reachable.
            avatar.transform.SetAsLastSibling();
            int target = IsDeclaringAttack?AttackOption("player",p.targetId):OptionIndex("player:" + p.targetId); long captured = requestId;
            avatar.interactable = target >= 0 && canAct;
            avatar.onClick.AddListener(() => Choose(target, captured));
            // Keep scene names stable for authoring; validation resolves the plate by seat.
            var avatarColors = avatar.colors; avatarColors.normalColor = target >= 0 && canAct ? Gold : Color.white; avatarColors.disabledColor=Color.white; avatar.colors = avatarColors;
            string zones = own ? "ZonePiles_PlayerBottomLeft" : "ZonePiles_OpponentTopRight";
            PresentZonePiles(p,zones);
            if (own)
            {
                IntegratedRow(ownHand, (p.hand??Array.Empty<ForgeCardView>()).Concat(AvailableExileCards()).ToArray(), "Hand", 100, 124, 172, 7);
                string[] colors = { "白", "蓝", "黑", "红", "绿", "无色" };
                string mana = p.manaPool?.Length == 6 ? string.Join("  ", colors.Select((c, i) => c + p.manaPool[i])) : "红 " + p.redMana;
                SetText("CornerCluster/ManaRow/Label", mana);
                var manaRect = SceneText("CornerCluster/ManaRow/Label").rectTransform;
                manaRect.sizeDelta = new Vector2(300, 22);
                SceneText("CornerCluster/ManaRow/Label").fontSize = 14;
            }
            else for (int i = 0; i < Math.Min(p.handCount, 12); i++)
                Panel(otherHand, "HiddenCard", new Vector2((i - (Math.Min(p.handCount, 12) - 1) * .5f) * 34, -25), new Vector2(30, 42), Ink);
            var field = p.battlefield ?? Array.Empty<ForgeCardView>();
            PresentEmblems(p,own);
            IntegratedRow(cardsRoot, field.Where(c => c.creature).ToArray(), own ? "YourCreatures" : "OpponentCreatures", own ? -70 : 135, 104, 140, 8);
            IntegratedRow(cardsRoot, field.Where(c => c.land && !c.creature).ToArray(), own ? "YourLands" : "OpponentLands", own ? -230 : 295, 92, 116, 9);
            IntegratedRow(cardsRoot, field.Where(c => !c.land && !c.creature).ToArray(), own ? "YourSupport" : "OpponentSupport", own ? -230 : 295, 104, 140, 8);
        }
        PresentStackCards();
        BindIntegratedAction(advanceButton, "ok", "确认", "Label");
        advanceButton.gameObject.SetActive(!IsMulliganChoice && OptionIndex("ok")>=0 && view?.request?.kind.Contains("PayMana")!=true);
        endTurnButton.gameObject.SetActive(false);
        int undo=OptionIndex("cancel");
        undoManaButton.gameObject.SetActive(view?.request?.kind=="InputPassPriority" && undo>=0 && view.request.options[undo].label.StartsWith("Undo"));
        undoManaButton.onClick.RemoveAllListeners();undoManaButton.onClick.AddListener(()=>TryUndoPresentedAction());undoManaButton.interactable=canAct;
        SetText("CornerCluster/ControlRow/EndTurnButton/Finish", "");
        int count = 0;
        bool localLandChoices=PrepareLandChoices();
        foreach (var option in view?.awaitingInput == true ? view.request.options : Array.Empty<ForgeOption>())
        {
            if(localLandChoices)continue;
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
        PresentTargeting();
        if(IsTargeting)advanceButton.gameObject.SetActive(false);
        PresentAttackSelection();
        PresentBlocking();
        PresentMulliganChoices();
        PresentAbilityOrder();
        PresentAllocation();
        if(localLandChoices){if(choiceDismiss!=null && choiceDismiss.gameObject.activeSelf)choiceDismiss.SetAsLastSibling();landChoiceMenu.SetAsLastSibling();}
        Node("StackColumn").gameObject.SetActive((view?.stackCount ?? 0)>0);
        actionRoot.parent.parent.gameObject.SetActive(view?.request?.kind!="InputBlock" && view?.request?.kind!="AssignAmount" && view?.request?.kind!="OrderAbilities" && !IsMulliganChoice && view?.request?.kind!="InputAttack" && !localLandChoices && !IsPayingPreview && (count>0 || !IsTargeting && view?.awaitingInput==true && view.request.kind!="InputPassPriority"));
        Canvas.ForceUpdateCanvases();
        PresentCardSelection();
        if(Node("InspectPanel").gameObject.activeSelf && inspectedCardId!=0) {
            var latest=(view?.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>(p.battlefield??Array.Empty<ForgeCardView>()).Concat(p.emblems??Array.Empty<ForgeCardView>()).Concat(p.effectMarkers??Array.Empty<ForgeCardView>())).Concat(view?.globalEffects??Array.Empty<ForgeCardView>()).FirstOrDefault(c=>c.id==inspectedCardId);
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
        Label(mulliganChoices,"Prompt",Prompt(view,seat),new Vector2(0,36),new Vector2(488,60),19,Paper).raycastTarget=false;
        var options=view.request.options.Where(o=>!RepresentedOnBoard(o.id)).ToArray();
        for(int i=0;i<options.Length;i++)
        {
            var option=options[i];int index=OptionIndex(option.id);long captured=requestId;
            var button=Button(mulliganChoices,"Mulligan_"+option.id,RequestOptionLabel(option),
                new Vector2((i-(options.Length-1)*.5f)*238,-39),new Vector2(226,46),()=>Choose(index,captured),option.id=="ok"?new Color(.25f,.34f,.39f):Ink);
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
        foreach (var b in ownHand.GetComponentsInChildren<Button>()) b.interactable = false;
        Node("PlayerPlate").GetComponent<Button>().interactable = false;
        Node("OpponentPlate").GetComponent<Button>().interactable = false;
        advanceButton.interactable = endTurnButton.interactable = undoManaButton.interactable = false;
        if(mulliganChoices!=null)foreach(var button in mulliganChoices.GetComponentsInChildren<Button>())button.interactable=false;
        if(allocationPanel!=null)foreach(var button in allocationPanel.GetComponentsInChildren<Button>())button.interactable=false;
        if(abilityOrder!=null)foreach(var button in abilityOrder.GetComponentsInChildren<Button>())button.interactable=false;
    }
    private const string ChangedBlue="#70BFFF", LostGray="#565C65";
    private static string SafeDetail(string value)=>(value??"").Replace("<","＜").Replace(">","＞");
    private static string ColoredDetail(string value,string color)=>"<color="+color+">"+SafeDetail(value)+"</color>";
    private static string LostDetail(string value)=>"<color="+LostGray+"><s color=#00000000>"+SafeDetail(value)+"</s></color>";
    public static string ChangedTypeText(string original,string current)
    {
        if(string.IsNullOrEmpty(original))return SafeDetail(CurrentLandTypeLabel(current));
        var before=original.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
        var after=(current??"").Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
        var parts=after.Select(t=>before.Contains(t)?SafeDetail(CurrentLandTypeLabel(t)):ColoredDetail(CurrentLandTypeLabel(t),ChangedBlue));
        var lost=before.Where(t=>!after.Contains(t)).Select(t=>LostDetail(CurrentLandTypeLabel(t))).ToArray();
        return string.Join(" ",parts)+(lost.Length>0?"\n"+string.Join(" ",lost):"");
    }
    public static string ChangedRulesText(string original,string current)
    {
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
    private static string LandTypeLabel(string value) => value switch {
        "Plains"=>"平原", "Island"=>"海岛", "Swamp"=>"沼泽", "Mountain"=>"山脉", "Forest"=>"树林", _=>value
    };
    private static string CurrentLandTypeLabel(string type)
    {
        return System.Text.RegularExpressions.Regex.Replace(type??"", @"\b(Plains|Island|Swamp|Mountain|Forest|Basic|Land|Creature|Enchantment|Artifact|Legendary|Snow)\b", m=>m.Value switch {
            "Basic"=>"基本", "Land"=>"地", "Creature"=>"生物", "Enchantment"=>"结界", "Artifact"=>"神器", "Legendary"=>"传奇", "Snow"=>"雪境", _=>LandTypeLabel(m.Value)
        });
    }
    private void InspectIntegrated(ForgeCardView card)
    {
        if(detailCardId!=card.id){detailCardId=card.id;inspectOriginalState=false;}
        detailCard=card;
        inspectHoverRevision++;
        inspectedCardId=card.zone is "Battlefield" or "Command"?card.id:0;
        Node("InspectPanel").gameObject.SetActive(true);
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
        SetText("InspectPanel/Header", "卡牌详情");
        SetText("InspectPanel/SetNumber", card.faceDown ? "" : card.printingKey ?? "");
        SetText("InspectPanel/Name", definition != null && !card.faceDown && !card.backFace && !IsPreparedSpell(card) ? GamePreferences.CardName(definition) : card.name);
        SceneText("InspectPanel/TypeLine").supportRichText=true;
        SetText("InspectPanel/TypeLine", card.faceDown ? "面朝下" : card.hasCurrentLandState || (!string.IsNullOrEmpty(card.currentType) && card.originalType!=card.currentType) ? ChangedTypeText(card.originalType,card.currentType) : card.backFace || IsPreparedSpell(card) || card.token ? card.currentType : GamePreferences.CardType(definition));
        SetText("InspectPanel/Rules", card.hasCurrentLandState ? ChangedRulesText(card.originalRules,card.currentRules) : card.backFace || IsPreparedSpell(card) || card.token ? card.currentRules?.Replace("\\n", "\n") : GamePreferences.CardRules(definition));
        if(card.emblem || card.effectMarker || card.faceDown) {
            SetText("InspectPanel/TypeLine",card.emblem?"徽记":card.effectMarker?(card.markerKind=="dungeon"?"地城":"延迟 / 持续效果"):"面朝下的生物");
            SetText("InspectPanel/Name",(card.emblem||card.effectMarker)?card.name:"面朝下的牌");
            SetText("InspectPanel/Rules",string.IsNullOrEmpty(card.markerStatus)?card.currentRules??"":card.markerStatus+"\n\n"+(card.currentRules??""));
        }
        if(inspectOriginalState && !card.faceDown){
            SetText("InspectPanel/TypeLine",SafeDetail(CurrentLandTypeLabel(card.originalType??card.currentType)));
            SetText("InspectPanel/Rules",SafeDetail((card.originalRules??GamePreferences.CardRules(definition)).Replace("\\n","\n")));
        }
        var originalButton=Node("InspectPanel").Find("OriginalState")?.GetComponent<Button>();
        if(originalButton==null){
            originalButton=Button(Node("InspectPanel"),"OriginalState","原始状态",Vector2.zero,new Vector2(90,28),()=>{inspectOriginalState=!inspectOriginalState;InspectIntegrated(detailCard);},new Color(.12f,.16f,.2f));
            var r=(RectTransform)originalButton.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(1,1);r.anchoredPosition=new Vector2(-16,-16);
            originalButton.GetComponentInChildren<Text>().fontSize=13;
        }
        originalButton.gameObject.SetActive(!card.faceDown&&!card.emblem&&!card.effectMarker&&card.originalRules!=null);
        originalButton.GetComponentInChildren<Text>().text=inspectOriginalState?"返回变化":"原始状态";
        var statuses=new System.Collections.Generic.List<string>();
        if(card.counters!=null)statuses.AddRange(card.counters.Where(c=>c.count>0&&!HasDedicatedCounter(card,c.kind)).Select(c=>(StatusName(c.kind)==c.kind?c.label:StatusName(c.kind))+" ×"+c.count));
        if(!card.faceDown&&card.keywords!=null)statuses.AddRange(card.keywords.Where(k=>StatusName(k)!=k).Select(k=>(card.grantedKeywords??Array.Empty<string>()).Contains(k)?ColoredDetail(StatusName(k),"#5CB8FF"):StatusName(k)));
        if(HasPreparedSpell(card))statuses.Add("已备法 · "+card.preparedSpell.name);
        if(statuses.Count>0&&!inspectOriginalState)Node("InspectPanel/Rules").GetComponent<Text>().text+="\n\n当前状态："+string.Join(" · ",statuses);
        SceneText("InspectPanel/Footer").supportRichText=true;
        SetText("InspectPanel/Footer", card.creature ? ModifiedStatsText(card,true)+$" · 伤害 {card.damage}" : "");
        if(card.planeswalker)SceneText("InspectPanel/Footer").text+=(card.creature?" · ":"")+GamePreferences.T("忠诚值")+" "+card.loyalty;
        if(card.battle)SceneText("InspectPanel/Footer").text+=(card.creature||card.planeswalker?" · ":"")+GamePreferences.T("布防值")+" "+card.defense;
        var art = Node("InspectPanel/Art").GetComponent<Image>();
        Sprite sprite = null;
        TryBattlefieldArt(card, out sprite);
        if(card.faceDown && !string.IsNullOrEmpty(card.knownFacePrintingKey)) {
            CardImageLibrary.TryLoadPrintingKey(card.knownFacePrintingKey,out sprite);
            SetText("InspectPanel/Name",card.knownFaceName);
            SetText("InspectPanel/Rules",(card.currentRules??"")+"\n\n仅你可见的原牌：\n"+(card.knownFaceRules??"").Replace("\\n","\n"));
        }
        art.sprite = sprite; art.color = sprite == null ? Ink : Color.white; art.preserveAspect = true;
        var missing=art.transform.Find("MissingArt");
        if(missing==null)missing=Label((RectTransform)art.transform,"MissingArt","缺失卡图",Vector2.zero,new Vector2(180,30),18,Paper).transform;
        missing.gameObject.SetActive(sprite==null);
        Clear(Node("InspectPanel/CostRow"));
        var mana=CardSymbolText.Create(Node("InspectPanel/CostRow"),"Mana",20,Paper);
        mana.text=CardSymbolText.Format(IsPreparedSpell(card)?card.manaCost:definition?.manaCost??"");
        mana.rectTransform.sizeDelta=new Vector2(308,24);
        mana.alignment=TMPro.TextAlignmentOptions.Left;
        LayoutInspectPanel(card,inspected);
        PresentPreparedSpellFace((RectTransform)art.transform,card,((RectTransform)art.transform).rect.size);
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
    private void LayoutInspectPanel(ForgeCardView card,RectTransform inspected)
    {
        var panel=(RectTransform)Node("InspectPanel");
        const float width=340,inner=308;
        var backing=Node("InspectPanel/Rules").GetComponent<Text>();backing.enabled=false;
        var content=Node("InspectPanel/Rules").Find("RuleScroll/Content") as RectTransform;
        if(content==null)content=Scroll(Node("InspectPanel/Rules"),"RuleScroll",Vector2.zero,new Vector2(inner,100));
        var rules=content.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if(rules==null)rules=CardSymbolText.Create(content,"SymbolRules",18,Paper);
        LostAbilityStrikeGraphic.Attach(rules);
        rules.font=GamePreferences.TmpFont!=null?GamePreferences.TmpFont:TMPro.TMP_Settings.defaultFontAsset;
        rules.lineSpacing=4;rules.alignment=TMPro.TextAlignmentOptions.TopLeft;
        rules.richText=true;rules.text=CardSymbolText.Format(backing.text);rules.enableAutoSizing=false;
        (rules.GetComponent<PreferenceText>()??rules.gameObject.AddComponent<PreferenceText>()).SetBaseSize(18);
        rules.rectTransform.anchorMin=rules.rectTransform.anchorMax=rules.rectTransform.pivot=new Vector2(0,1);
        rules.rectTransform.anchoredPosition=Vector2.zero;
        float preferred=Mathf.Ceil(rules.GetPreferredValues(rules.text,inner,10000).y)+24;
        float artHeight=card.emblem && Node("InspectPanel/Art").GetComponent<Image>().sprite==null?80:card.room?172:250;
        var mana=Node("InspectPanel/CostRow").GetComponentInChildren<TMPro.TextMeshProUGUI>();
        float costHeight=mana!=null&&!string.IsNullOrEmpty(mana.text)?28:0;
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
        float footerHeight=card.creature?28:0;
        float fixedHeight=16+34+costHeight+artHeight+12+typeHeight+12+footerHeight+16;
        float available=surface.rect.height-24;
        if(fixedHeight+preferred>available){float shrink=Mathf.Min(90,fixedHeight+preferred-available);artHeight-=shrink;fixedHeight-=shrink;}
        float rulesHeight=Mathf.Clamp(preferred,28,Mathf.Max(28,available-fixedHeight));
        float height=fixedHeight+rulesHeight;
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
            text.fontSize=name=="Name"?22:15;text.color=name=="TypeLine"?new Color(.67f,.73f,.78f):Paper;
        }
        Place("Name",34);
        if(Node("InspectPanel/OriginalState").gameObject.activeSelf) {var nameRect=(RectTransform)Node("InspectPanel/Name");nameRect.sizeDelta=new Vector2(inner-100,34);nameRect.anchoredPosition+=Vector2.left*50;}
        Place("CostRow",costHeight);
        var art=(RectTransform)Node("InspectPanel/Art");
        art.anchorMin=art.anchorMax=new Vector2(.5f,1);art.pivot=new Vector2(.5f,.5f);
        art.anchoredPosition=new Vector2(0,-y-artHeight*.5f);
        art.sizeDelta=card.room?new Vector2(artHeight,inner):new Vector2(inner,artHeight);
        art.localRotation=Quaternion.Euler(0,0,card.room?-90:0);y+=artHeight+12;
        Place("TypeLine",typeHeight);y+=12;Place("Rules",rulesHeight);Place("Footer",footerHeight);
        var viewport=(RectTransform)content.parent;viewport.sizeDelta=new Vector2(inner,rulesHeight);
        content.sizeDelta=new Vector2(inner,Mathf.Max(preferred,rulesHeight));content.anchoredPosition=Vector2.zero;
        rules.rectTransform.sizeDelta=new Vector2(inner,Mathf.Max(preferred,rulesHeight));
        rules.ForceMeshUpdate();
        Vector2 p=panel.anchoredPosition;
        if(inspected!=null){p=surface.InverseTransformPoint(inspected.TransformPoint(inspected.rect.center));
            float gap=inspected.rect.width*.5f+width*.5f+24;
            p.x+=p.x+gap<=surface.rect.xMax-width*.5f-12?gap:-gap;}
        panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);
        p.x=Mathf.Clamp(p.x,surface.rect.xMin+width*.5f+12,surface.rect.xMax-width*.5f-12);
        p.y=Mathf.Clamp(p.y,surface.rect.yMin+height*.5f+12,surface.rect.yMax-height*.5f-12);
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
