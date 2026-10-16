using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform targetOrigin;
    private ForgeTargetArrow targetArrow;
    private Sprite castingSprite;
    private ForgeCardView castingPreparedSpell, castingSource;
    private long targetingRequest, castingRequest;
    private int castingCardId=-1;
    private CanvasGroup castingHandGroup, castingDestinationGroup;
    private RectTransform castingDestination;
    private bool returningCast;

    private bool IsTargeting => integrated && canAct && !submitted && !IsCardPickerRequest && view?.awaitingInput==true
        && (view.request.kind=="InputSelectTargets" || view.request.kind=="SelectStackTarget");
    private bool HasCastingPrompt => integrated && view?.awaitingInput==true
        && ((view.request.kind=="InputSelectTargets" || view.request.kind=="SelectStackTarget")
            || castingSource!=null && (view.request.kind.Contains("PayMana") || view.request.sourceCardId==castingCardId && view.request.kind!="InputPassPriority"));
    private bool IsPayingPreview => integrated && canAct && !submitted && castingSource!=null
        && view?.awaitingInput==true && view.request.kind.Contains("PayMana");
    private Vector2 CastingDock => new Vector2(surface.rect.width*.5f-179-
        ((view?.stackCount??0)>0?((RectTransform)Node("StackColumn")).rect.width+20:0),surface.rect.height*.5f-357.5f);

    private void EnsureCastingPreview()
    {
        if(targetOrigin!=null)return;
        targetOrigin=Panel(surface,"TargetOrigin",CastingDock,new Vector2(240,335),Paper);
        var image=targetOrigin.GetComponent<Image>();image.preserveAspect=true;image.color=Color.white;
        var group=targetOrigin.gameObject.AddComponent<CanvasGroup>();
        Label(targetOrigin,"Hint","选择高亮目标",new Vector2(0,193),new Vector2(270,30),20,Paper).raycastTarget=false;
        Button(targetOrigin,"CancelTarget","取消",new Vector2(0,-203),new Vector2(160,38),()=>Choose(OptionIndex("cancel"),targetingRequest),Ink);
        Button(targetOrigin,"ConfirmTarget","确认",new Vector2(-62,-203),new Vector2(114,38),()=>Choose(OptionIndex("ok"),targetingRequest),new Color(.18f,.32f,.49f));
        Button(targetOrigin,"ResetTarget","重选",new Vector2(62,-203),new Vector2(114,38),()=>Choose(OptionIndex("reset-targets"),targetingRequest),Ink);
        Rect(targetOrigin,"PaymentChoices",Vector2.zero,Vector2.zero);
        targetOrigin.gameObject.AddComponent<ForgeTargetDragHandle>().Drop=position=>TryChooseTargetAt(position,targetingRequest);
        var arrowRect=Rect(surface,"TargetArrow",Vector2.zero,surface.rect.size);
        targetArrow=arrowRect.gameObject.AddComponent<ForgeTargetArrow>();targetArrow.raycastTarget=false;
        SetOverlayLayer(arrowRect,DragArrowLayerOffset);
        targetArrow.gameObject.SetActive(false);
    }
    private void RememberCastingOrigin(ForgeOption option)
    {
        if(!integrated || view?.request?.kind!="InputPassPriority" || !option.id.StartsWith("card:"))return;
        var source=view.players.SelectMany(p=>(p.hand ?? System.Array.Empty<ForgeCardView>())
            .Concat(p.battlefield ?? System.Array.Empty<ForgeCardView>())).Concat(AvailableExileCards()).FirstOrDefault(c=>option.id=="card:"+c.id);
        var preparedSource=PreparedSource(option.id);
        if(preparedSource!=null)source=preparedSource.preparedSpell.ToCard();
        // Lands resolve their mana actions in place; only a spell/ability needs a casting preview.
        if(source==null || source.land || source.zone!="Exile" && source.zone!="Graveyard" && preparedSource==null && !view.players.Any(p=>(p.hand??System.Array.Empty<ForgeCardView>()).Any(c=>c.id==source.id)))return;
        ResetCastingVisual();
        castingSource=source;
        if(IsPreparedSpell(source))castingPreparedSpell=source;
        if(IsSpecializedFace(source))TryBattlefieldArt(source,out castingSprite);
        else if(catalog.TryGetValue(source.printingKey ?? "",out var definition) && !source.faceDown)
            CardImageLibrary.TryLoadFullCard(definition,source.backFace?1:0,out castingSprite);
        castingCardId=source.id;castingRequest=requestId;
        EnsureCastingPreview();
        targetOrigin.gameObject.SetActive(true);targetOrigin.SetAsLastSibling();
        var item=arenaCards.Values.FirstOrDefault(c=>c.root.name=="Card_"+(preparedSource?.id??source.id));
        if(item!=null)
        {
            ForgeUiMotion.Pose(item.face,surface,out var position,out var size,out var angle);
            targetOrigin.anchoredPosition=position;targetOrigin.sizeDelta=size;targetOrigin.localRotation=Quaternion.Euler(0,0,angle);
            // Keep the actual hand slot while Forge is deciding. Its identity and rules never change here.
            if(item.root.parent==ownHand)HideCastingHand(item);
        }
        else {targetOrigin.anchoredPosition=CastingDock;targetOrigin.sizeDelta=new Vector2(240,335);}
        targetOrigin.GetComponent<Image>().sprite=castingSprite;
        targetOrigin.Find("Hint").GetComponent<Text>().text="正在施放…";
        targetOrigin.Find("Hint").gameObject.SetActive(true);
        targetOrigin.Find("CancelTarget").gameObject.SetActive(false);
        targetOrigin.Find("ConfirmTarget").gameObject.SetActive(false);targetOrigin.Find("ResetTarget").gameObject.SetActive(false);
        PresentAbilityFace(targetOrigin,null,null,null,false);
        PresentPreparedSpellFace(targetOrigin,castingPreparedSpell,new Vector2(240,335));
        if(castingPreparedSpell==null)PresentComponentFace(targetOrigin,castingSource);
        Clear(targetOrigin.Find("PaymentChoices"));
        ForgeUiMotion.On(targetOrigin).MoveTo(CastingDock,new Vector2(240,335),seconds:.42f,lift:42);
    }
    private void HideCastingHand(ArenaCard item)
    {
        castingHandGroup=item.root.GetComponent<CanvasGroup>();
        if(castingHandGroup==null)castingHandGroup=item.root.gameObject.AddComponent<CanvasGroup>();
        castingHandGroup.alpha=0;castingHandGroup.blocksRaycasts=false;
        item.pointer.SetHandFanExposure(false);
        item.pointer.ResetHover();
    }
    private void PresentTargeting()
    {
        if(HasCastingPrompt)
        {
            EnsureCastingPreview();
            targetingRequest=requestId;
            Sprite sourceSprite=castingSprite;
            var source=!string.IsNullOrEmpty(view.request.sourceCard?.name)?view.request.sourceCard:castingPreparedSpell??castingSource;
            if(IsSpecializedFace(source))TryBattlefieldArt(source,out sourceSprite);
            else if(source!=null && catalog.TryGetValue(source.printingKey??"",out var definition))CardImageLibrary.TryLoadFullCard(definition,source.backFace?1:0,out sourceSprite);
            targetOrigin.GetComponent<Image>().sprite=sourceSprite;
            targetOrigin.GetComponent<Image>().color=sourceSprite==null?Ink:Color.white;
            bool ability=!string.IsNullOrEmpty(view.request.abilityKind) && view.request.abilityKind!="spell";
            PresentAbilityFace(targetOrigin,source,view.request.abilityText,view.request.abilityKind,ability);
            PresentPreparedSpellFace(targetOrigin,source,new Vector2(240,335));
            if(!ability)PresentComponentFace(targetOrigin,source);
            targetOrigin.GetComponent<CanvasGroup>().interactable=canAct && !submitted;
            targetOrigin.GetComponent<CanvasGroup>().blocksRaycasts=canAct && !submitted;
            targetOrigin.Find("Hint").gameObject.SetActive(IsTargeting || view.request.kind.Contains("PayMana")&&!IsManaPaymentDecision);
            var hint=targetOrigin.Find("Hint").GetComponent<Text>();
            hint.text=(view.request.kind=="InputSelectTargets" || view.request.kind=="SelectStackTarget")?"选择高亮目标":"支付费用";
            hint.GetComponent<PreferenceText>().Apply();
            float hintHeight=Mathf.Max(30,Mathf.Ceil(hint.preferredHeight)+4);
            hint.rectTransform.sizeDelta=new Vector2(270,hintHeight);
            hint.rectTransform.anchoredPosition=new Vector2(0,178+hintHeight*.5f);
            // The source card is a preview. Target actions use the same corner
            // dock as hand-cost and mana payment actions.
            targetOrigin.Find("CancelTarget").gameObject.SetActive(false);
            targetOrigin.Find("ConfirmTarget").gameObject.SetActive(false);
            targetOrigin.Find("ResetTarget").gameObject.SetActive(false);
            var paymentChoices=(RectTransform)targetOrigin.Find("PaymentChoices");Clear(paymentChoices);
            if(view.request.kind.Contains("PayMana"))
            {
                int row=0;
                foreach(var option in view.request.options)
                {
                    if(option.id=="cancel" || option.id=="ok" || RepresentedOnBoard(option.id))continue;
                    int index=OptionIndex(option.id);long captured=requestId;
                    Button(paymentChoices,"Pay_"+index,RequestOptionLabel(option),new Vector2(0,-249-row++*44),new Vector2(240,38),()=>Choose(index,captured),Ink);
                }
            }
            targetOrigin.gameObject.SetActive(true);targetOrigin.SetAsLastSibling();
            if(!returningCast && castingDestination==null)ForgeUiMotion.On(targetOrigin).MoveTo(CastingDock,new Vector2(240,335));
            targetArrow.SetVisible(IsTargeting);
            targetArrow.transform.SetAsLastSibling();
            UpdateTargeting();return;
        }
        if(targetArrow!=null)targetArrow.SetVisible(false);
        if(castingCardId<0){HideTargeting();return;}
        if(returningCast || castingDestination!=null)return;
        var stack=stackCards.Values.FirstOrDefault(c=>c.source?.id==castingCardId);
        if(stack!=null){FinishCastingAt(stack.tile,false);return;}
        // A pending/repeated snapshot must not send a spell back before Forge replies.
        if(view?.awaitingInput!=true)return;
        if(view.request.requestId==castingRequest && (service==null || service.HasPendingAction || !canAct))return;
        if(view.request.kind!="InputPassPriority")return;
        var card=arenaCards.Values.FirstOrDefault(c=>c.root.name=="Card_"+castingCardId);
        if(card!=null){FinishCastingAt(card.face,card.root.parent==ownHand);return;}
        ResetCastingVisual();
    }
    private void SetTargetControlLabel(string control,string value)
    {
        var label=targetOrigin.Find(control).GetComponentInChildren<Text>(true);
        var binding=label.GetComponent<PreferenceText>()??label.gameObject.AddComponent<PreferenceText>();
        binding.Literal=true;
        label.text=value;
        binding.Apply();
    }
    private void FinishCastingAt(RectTransform destination,bool toHand)
    {
        if(targetOrigin==null)return;
        returningCast=toHand;castingDestination=destination;
        targetOrigin.Find("Hint").gameObject.SetActive(false);
        targetOrigin.Find("CancelTarget").gameObject.SetActive(false);
        targetOrigin.Find("ConfirmTarget").gameObject.SetActive(false);targetOrigin.Find("ResetTarget").gameObject.SetActive(false);
        Clear(targetOrigin.Find("PaymentChoices"));
        var group=targetOrigin.GetComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;
        var hiddenSurface=destination.name.StartsWith("StackCard_")?destination.parent:destination;
        castingDestinationGroup=hiddenSurface.GetComponent<CanvasGroup>();
        if(castingDestinationGroup==null)castingDestinationGroup=hiddenSurface.gameObject.AddComponent<CanvasGroup>();
        castingDestinationGroup.alpha=0;castingDestinationGroup.blocksRaycasts=false;
        ForgeUiMotion.On(targetOrigin).Follow(destination,toHand?.36f:.24f,ResetCastingVisual);
    }
    private void SuspendTargeting()
    {
        if(targetOrigin!=null)
        {
            var group=targetOrigin.GetComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;
        }
        if(targetArrow!=null)targetArrow.SetVisible(false);
    }
    private void HideTargeting()
    {
        if(targetOrigin!=null)targetOrigin.gameObject.SetActive(false);
        if(targetArrow!=null)targetArrow.SetVisible(false);
    }
    private void ResetCastingVisual()
    {
        if(targetOrigin!=null)ForgeUiMotion.On(targetOrigin).Cancel();
        if(castingHandGroup!=null){castingHandGroup.alpha=1;castingHandGroup.blocksRaycasts=true;}
        if(castingDestinationGroup!=null){castingDestinationGroup.alpha=1;castingDestinationGroup.blocksRaycasts=true;}
        castingHandGroup=castingDestinationGroup=null;castingDestination=null;
        castingCardId=-1;castingSprite=null;castingPreparedSpell=castingSource=null;returningCast=false;
        HideTargeting();
        if(handNavigation!=null&&handFanSlots.Count>0)ApplyHandFanOffset(handNavigation.Offset,false);
    }
    private void UpdateTargeting()
    {
        UpdateAttackArrowAt(Input.mousePosition);
        if(targetOrigin==null || !targetOrigin.gameObject.activeSelf)return;
        if(IsTargeting)UpdateTargetingAt(Input.mousePosition);
    }
    // Also used by pointer-driven scene validation; never submits a rule action.
    public void UpdateTargetingAt(Vector2 mouse)
    {
        if(!IsTargeting || targetArrow==null)return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,mouse,null,out var endpoint);
        int candidate=TargetOptionAt(mouse);
        if(candidate<0)candidate=System.Array.FindIndex(view.request.options,o=>o.selected);
        if(candidate>=0)
        {
            string id=view.request.options[candidate].id;
            foreach(var card in arenaCards.Values)
                if(id=="card:"+card.root.name.Substring(5))endpoint=surface.InverseTransformPoint(card.face.position);
            foreach(var entry in stackCards.Values)
                if(id=="stack:"+entry.entry?.id)endpoint=surface.InverseTransformPoint(entry.tile.TransformPoint(entry.tile.rect.center));
            foreach(var player in view.players)
                if(id=="player:"+player.targetId)endpoint=surface.InverseTransformPoint(Node(player.seat==seat?"PlayerPlate":"OpponentPlate").position);
        }
        targetArrow.SetEndpoints(targetOrigin.anchoredPosition+new Vector2(-targetOrigin.rect.width*.4f,targetOrigin.rect.height*.2f),endpoint,
            candidate>=0 && view.request.options[candidate].selected?Gold:candidate>=0?new Color(.2f,.85f,1):new Color(.22f,.55f,.85f),candidate>=0);
    }
    private int TargetOptionAt(Vector2 position)
    {
        var system=EventSystem.current;if(system==null)return -1;
        var hits=new List<RaycastResult>();
        system.RaycastAll(new PointerEventData(system){position=position},hits);
        if(hits.Count==0)return -1;
        var button=hits[0].gameObject.GetComponentInParent<Button>();
        if(button==null || !button.interactable)return -1;
        if(button.name.StartsWith("StackListTarget_"))return OptionIndex("stack:"+button.name.Substring("StackListTarget_".Length));
        if(button.name.StartsWith("StackCard_"))
        {
            var entry=stackCards.Values.FirstOrDefault(c=>c.tile==button.transform);
            return entry?.entry==null?-1:OptionIndex("stack:"+entry.entry.id);
        }
        if(button.name.StartsWith("Card_"))return OptionIndex("card:"+button.name.Substring(5));
        foreach(var player in view.players)
            if(button.name==(player.seat==seat?"PlayerPlate":"OpponentPlate"))return OptionIndex("player:"+player.targetId);
        return -1;
    }
    public bool TryChooseTargetAt(Vector2 position,long expectedRequest)
    {
        if(!IsTargeting || expectedRequest!=requestId)return false;
        int option=TargetOptionAt(position);if(option<0)return false;
        return TrySubmitPresentedOption(option,expectedRequest);
    }

    private int selectedAttacker=-1;
    private readonly List<int> selectedAttackers=new List<int>();
    private long attackSelectionRequest;
    private ForgeTargetArrow attackArrow;
    private readonly List<ForgeTargetArrow> extraAttackArrows=new List<ForgeTargetArrow>();
    private RectTransform attackHint;
    private readonly List<ForgeTargetArrow> assignedAttackArrows=new List<ForgeTargetArrow>();
    private bool IsDeclaringAttack => integrated && canAct && !submitted && !GameSettingsPanel.IsOpen
        && view?.awaitingInput==true && view.request.seat==seat && view.request.kind=="InputAttack";
    private bool CanSelectAttacker(int id) => IsDeclaringAttack && view.request.options.Any(o=>
        o.attackerId==id && (o.id.StartsWith("attack:") || o.id.StartsWith("attack-remove:")));
    private int AttackOption(string kind,int id) => IsDeclaringAttack && selectedAttackers.Count>0
        && selectedAttackers.All(attacker=>view.request.options.Any(o=>o.id.StartsWith("attack:")
            && o.attackerId==attacker && o.defenderKind==kind && o.defenderId==id && !o.disabled))
        ? System.Array.FindIndex(view.request.options,o=>o.id.StartsWith("attack:") && o.attackerId==selectedAttacker
            && o.defenderKind==kind && o.defenderId==id) : -1;
    private bool AttackCardCandidate(int id) => CanSelectAttacker(id) || AttackOption("card",id)>=0;
    private void ResetAttackSelectionIfNeeded()
    {
        if(!IsDeclaringAttack || attackSelectionRequest!=requestId)ClearAttackSelection();
    }
    private void ClearAttackSelection(){selectedAttackers.Clear();selectedAttacker=-1;}
    private void ChooseAttackDestination(int option,long expectedRequest)
    {
        if(!IsDeclaringAttack || attackSelectionRequest!=expectedRequest || selectedAttackers.Count==0)return;
        ChooseCore(option,expectedRequest,selectedAttackers.Count>1?selectedAttackers.ToArray():null);
    }
    private RectTransform AttackEntity(string kind,int id)
    {
        if(kind=="player")
        {
            var player=view?.players?.FirstOrDefault(p=>p.targetId==id);
            return player==null?null:(RectTransform)Node(player.seat==seat?"PlayerPlate":"OpponentPlate");
        }
        return arenaCards.Values.FirstOrDefault(c=>c.root.name=="Card_"+id)?.root;
    }
    private ForgeTargetArrow NewAttackArrow(string name)
    {
        var arrow=Rect(surface,name,Vector2.zero,surface.rect.size).gameObject.AddComponent<ForgeTargetArrow>();
        SetOverlayLayer((RectTransform)arrow.transform,15);
        arrow.raycastTarget=false;return arrow;
    }
    private void PresentAttackSelection()
    {
        PresentAttackBatchActions();
        bool active=IsDeclaringAttack && selectedAttacker>=0;
        if(active && attackArrow==null) {
            attackArrow=NewAttackArrow("AttackTargetArrow");
            SetOverlayLayer(attackArrow.rectTransform,DragArrowLayerOffset);
        }
        if(active && attackHint==null)
        {
            attackHint=Rect(surface,"AttackTargetHint",Vector2.zero,new Vector2(290,85));
            Label(attackHint,"Hint","拖向或点击高亮的攻击对象",new Vector2(0,26),new Vector2(290,36),17,Paper).raycastTarget=false;
            Button(attackHint,"RecallAttacker","取消此生物攻击",new Vector2(0,-12),new Vector2(170,32),()=>{
                if(IsDeclaringAttack)Choose(OptionIndex("attack-remove:"+selectedAttacker),attackSelectionRequest);
            },Ink);
            Button(attackHint,"BandAttacker","联合",new Vector2(70,-12),new Vector2(130,32),()=>{
                if(IsDeclaringAttack)Choose(OptionIndex("attack-band:"+selectedAttacker),attackSelectionRequest);
            },Ink);
        }
        if(attackArrow!=null){attackArrow.gameObject.SetActive(active);attackArrow.transform.SetAsLastSibling();}
        for(int i=0;i<extraAttackArrows.Count;i++)extraAttackArrows[i].gameObject.SetActive(active&&i<selectedAttackers.Count-1);
        if(attackHint!=null)
        {
            attackHint.gameObject.SetActive(active);
            if(active)
            {
                var source=AttackEntity("card",selectedAttacker);
                attackHint.anchoredPosition=(Vector2)surface.InverseTransformPoint(source.position)+new Vector2(0,source.rect.height*.5f+45);
                attackHint.Find("RecallAttacker").gameObject.SetActive(OptionIndex("attack-remove:"+selectedAttacker)>=0);
                int bandOption=selectedAttackers.Count==1?OptionIndex("attack-band:"+selectedAttacker):-1;
                var band=attackHint.Find("BandAttacker");band.gameObject.SetActive(bandOption>=0);
                var recall=(RectTransform)attackHint.Find("RecallAttacker");
                recall.anchoredPosition=new Vector2(bandOption>=0?-70:0,-12);recall.sizeDelta=new Vector2(bandOption>=0?130:170,32);
                PreferenceText.SetSize(recall.GetComponentInChildren<Text>(),bandOption>=0?14:18);
                PreferenceText.SetSize(band.GetComponentInChildren<Text>(),14);
                recall.GetComponentInChildren<Text>().text=bandOption>=0?TapPaymentText("撤回攻击","Recall","攻撃取消"):TapPaymentText("取消此生物攻击","Recall attacker","攻撃を取り消す");
                if(bandOption>=0)band.GetComponentInChildren<Text>().text=view.request.options[bandOption].selected
                    ?TapPaymentText("取消选组","Deselect","選択解除"):TapPaymentText("联合","Band","バンド");
                var hint=attackHint.Find("Hint").GetComponent<Text>();
                hint.text=bandOption>=0?TapPaymentText("先选联合组，再宣攻加入","Select band, add attackers","バンドを選んで攻撃を追加")
                    :TapPaymentText("拖向或点击高亮的攻击对象","Drag to or click an attack target","攻撃先を選択");
                attackHint.SetAsLastSibling();
            }
        }
        int used=0;
        foreach(var attacker in (view?.players ?? System.Array.Empty<ForgePlayerView>())
            .SelectMany(p=>p.battlefield ?? System.Array.Empty<ForgeCardView>()).Where(c=>c.attacking && !string.IsNullOrEmpty(c.defenderKind)))
        {
            var from=AttackEntity("card",attacker.id);var to=AttackEntity(attacker.defenderKind,attacker.defenderId);
            if(from==null || to==null)continue;
            if(used==assignedAttackArrows.Count)assignedAttackArrows.Add(NewAttackArrow("AssignedAttack_"+used));
            var arrow=assignedAttackArrows[used++];arrow.gameObject.SetActive(true);
            // The dedicated combat Canvas keeps lines above cards and avatars.
            arrow.transform.SetSiblingIndex(cardsRoot.GetSiblingIndex());
            arrow.SetEndpoints(surface.InverseTransformPoint(from.position),surface.InverseTransformPoint(to.position),new Color(1,.48f,.18f));
        }
        for(int i=used;i<assignedAttackArrows.Count;i++)assignedAttackArrows[i].gameObject.SetActive(false);
        if(active)UpdateAttackArrowAt(Input.mousePosition);
    }
    public bool BeginAttackSelection(int attacker,long expectedRequest)
    {
        if(expectedRequest!=requestId || !CanSelectAttacker(attacker))return false;
        if(attackSelectionRequest!=requestId)ClearAttackSelection();
        // Dragging preserves the current group; clicking is the toggle operation.
        if(!selectedAttackers.Contains(attacker))selectedAttackers.Add(attacker);
        selectedAttacker=attacker;attackSelectionRequest=requestId;
        Node("InspectPanel").gameObject.SetActive(false);
        PresentIntegrated(connectionText.text);return true;
    }
    private void ClickAttackCard(int card,long expectedRequest)
    {
        if(!IsDeclaringAttack || requestId!=expectedRequest)return;
        int destination=AttackOption("card",card);
        if(destination>=0){ChooseAttackDestination(destination,expectedRequest);return;}
        if(!CanSelectAttacker(card))return;
        if(selectedAttackers.Remove(card)) {
            selectedAttacker=selectedAttackers.Count>0?selectedAttackers[selectedAttackers.Count-1]:-1;
            PresentIntegrated(connectionText.text);return;
        }
        int recall=OptionIndex("attack-remove:"+card);
        if(recall>=0){Choose(recall,expectedRequest);return;}
        BeginAttackSelection(card,expectedRequest);
    }
    private int AttackOptionAt(Vector2 position)
    {
        var system=EventSystem.current;if(system==null)return -1;
        var hits=new List<RaycastResult>();system.RaycastAll(new PointerEventData(system){position=position},hits);
        var button=hits.Count==0?null:hits[0].gameObject.GetComponentInParent<Button>();
        if(button==null || !button.interactable)return -1;
        if(button.name.StartsWith("Card_") && int.TryParse(button.name.Substring(5),out int card))return AttackOption("card",card);
        foreach(var player in view.players)
            if(button.name==(player.seat==seat?"PlayerPlate":"OpponentPlate"))return AttackOption("player",player.targetId);
        return -1;
    }
    public void UpdateAttackArrowAt(Vector2 position)
    {
        if(attackArrow==null)return;
        if(!IsDeclaringAttack || selectedAttacker<0 || attackSelectionRequest!=requestId)
        {attackArrow.gameObject.SetActive(false);foreach(var arrow in extraAttackArrows)arrow.gameObject.SetActive(false);if(attackHint!=null)attackHint.gameObject.SetActive(false);return;}
        int index=AttackOptionAt(position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,position,null,out var end);
        if(index>=0)
        {
            var option=view.request.options[index];var target=AttackEntity(option.defenderKind,option.defenderId);
            if(target!=null)end=surface.InverseTransformPoint(target.position);
        }
        for(int i=0;i<selectedAttackers.Count;i++) {
            if(i>0&&extraAttackArrows.Count<i){var created=NewAttackArrow("AttackTargetArrow_"+i);SetOverlayLayer(created.rectTransform,DragArrowLayerOffset);extraAttackArrows.Add(created);}
            var arrow=i==0?attackArrow:extraAttackArrows[i-1];
            var source=AttackEntity("card",selectedAttackers[i]);arrow.gameObject.SetActive(source!=null);
            if(source!=null)arrow.SetEndpoints(surface.InverseTransformPoint(source.position),end,
                index>=0?new Color(1,.65f,.2f):new Color(.85f,.42f,.18f),index>=0);
        }
    }
    public bool TryChooseAttackAt(Vector2 position,long expectedRequest)
    {
        if(!IsDeclaringAttack || expectedRequest!=requestId || attackSelectionRequest!=expectedRequest)return false;
        int index=AttackOptionAt(position);
        if(index>=0){ChooseAttackDestination(index,expectedRequest);return submitted;}
        // Releasing over the table cancels only the local arrow, never changes Forge combat.
        ClearAttackSelection();PresentIntegrated(connectionText.text);return false;
    }
}
