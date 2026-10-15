using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private sealed class ArenaCard
    {
        public RectTransform root, face, cardFace;
        public Button button;
        public ForgeBattlefieldCardPointer pointer;
        public string appearance;
        public string groupKey;
        public int representedCount;
        public bool faceDown;
    }
    private readonly Dictionary<string,ArenaCard> arenaCards=new Dictionary<string,ArenaCard>();
    private readonly HashSet<string> retainedCards=new HashSet<string>();
    private readonly HashSet<string> expandedGroups=new HashSet<string>();
    private bool arenaInitialized;
    private static Font battlefieldNumeralFont;
    private int arenaViewer;
    private long groupRequest;
    private int groupExpandedFrame=-1;
    private void UpdateExpandedGroupDismissal()
    {
        if(expandedGroups.Count==0 || groupExpandedFrame==Time.frameCount || !integrated)return;
        bool left,right;Vector2 position;
#if ENABLE_INPUT_SYSTEM
        var mouse=UnityEngine.InputSystem.Mouse.current;if(mouse==null)return;
        left=mouse.leftButton.wasPressedThisFrame;right=mouse.rightButton.wasPressedThisFrame;position=mouse.position.ReadValue();
#else
        left=Input.GetMouseButtonDown(0);right=Input.GetMouseButtonDown(1);position=Input.mousePosition;
#endif
        if(!left&&!right)return;
        HandleExpandedGroupPointer(position,right);
    }
    private void HandleExpandedGroupPointer(Vector2 position,bool right)
    {
        if(!right) {
            var system=UnityEngine.EventSystems.EventSystem.current;
            var hits=new List<UnityEngine.EventSystems.RaycastResult>();
            if(system!=null)system.RaycastAll(new UnityEngine.EventSystems.PointerEventData(system){position=position},hits);
            var hit=hits.Count>0?hits[0].gameObject.transform:null;
            if(hit!=null && arenaCards.Values.Any(c=>expandedGroups.Contains(c.groupKey) && (hit==c.root || hit.IsChildOf(c.root))))return;
        }
        CollapseExpandedGroups();
    }
    private void CollapseExpandedGroups()
    {
        if(expandedGroups.Count==0)return;
        expandedGroups.Clear();PresentIntegrated(connectionText.text);
    }
    private static float StackOverhang(ForgeCardView[] group)=>0;
    private void BeginArenaPresentation()
    {
        ResetAttackSelectionIfNeeded();
        retainedCards.Clear();
        if(groupRequest!=requestId){expandedGroups.Clear();groupRequest=requestId;}
        if(arenaViewer!=seat) { foreach(var c in arenaCards.Values) {c.root.gameObject.SetActive(false);Destroy(c.root.gameObject);} arenaCards.Clear(); expandedGroups.Clear(); arenaViewer=seat; }
        if(arenaInitialized)return;
        arenaInitialized=true;
        Node("InspectPanel").gameObject.SetActive(false);
        ArenaPlace("PlayerPlate",new Vector2(0,0),new Vector2(166,82),new Vector2(268,92));
        ArenaPlace("OpponentPlate",new Vector2(1,1),new Vector2(-166,-76),new Vector2(268,92));
        foreach(string plate in new[]{"PlayerPlate","OpponentPlate"})
        {
            bool own=plate=="PlayerPlate";
            var image=Node(plate).GetComponent<Image>();
            image.sprite=IntegratedUiUtility.RoundedRect(268,92,8);image.color=new Color(.045f,.065f,.085f,.97f);
            var outline=Node(plate).GetComponent<Outline>();
            if(outline!=null)outline.effectColor=new Color(.3f,.4f,.48f,.5f);
            Node(plate+"/Avatar").gameObject.SetActive(true);Node(plate+"/FactionBar").gameObject.SetActive(false);
            ArenaPlace(plate+"/Avatar",new Vector2(.5f,.5f),new Vector2(-135,0),new Vector2(66,66));
            var portrait=Node(plate+"/Avatar").GetComponent<Image>();
            portrait.sprite=IntegratedUiUtility.RoundedRect(70,70,35);
            portrait.color=own?new Color(.12f,.26f,.34f):new Color(.29f,.22f,.16f);
            Node(plate+"/Avatar/Mark").gameObject.SetActive(false);
            var portraitNode=Node(plate+"/Avatar").Find("Portrait") as RectTransform;
            if(portraitNode==null)portraitNode=Rect(Node(plate+"/Avatar"),"Portrait",Vector2.zero,new Vector2(62,62));
            var portraitMark=portraitNode.GetComponent<PlayerAvatarGraphic>()??portraitNode.gameObject.AddComponent<PlayerAvatarGraphic>();
            portraitMark.color=own?new Color(.65f,.84f,.93f):new Color(.95f,.8f,.58f);portraitMark.raycastTarget=false;
            var portraitRim=portrait.gameObject.GetComponent<Outline>()??portrait.gameObject.AddComponent<Outline>();
            portraitRim.effectColor=portraitMark.color;portraitRim.effectDistance=new Vector2(1,-1);
            foreach(var text in Node(plate).GetComponentsInChildren<Text>())
                if(text.text=="LIFE")text.gameObject.SetActive(false);
            ArenaPlace(plate+"/Name",new Vector2(.5f,.5f),new Vector2(-25,18),new Vector2(148,42));
            ArenaPlace(plate+"/Life",new Vector2(.5f,.5f),new Vector2(91,0),new Vector2(80,68));
            ArenaPlace(plate+"/Sub",new Vector2(.5f,.5f),new Vector2(-18,-17),new Vector2(142,28));
            ArenaTextSize(plate+"/Name",19);SceneText(plate+"/Name").alignment=TextAnchor.MiddleLeft;
            ArenaTextSize(plate+"/Life",42);SceneText(plate+"/Life").alignment=TextAnchor.MiddleCenter;
            ArenaTextSize(plate+"/Sub",12);SceneText(plate+"/Sub").alignment=TextAnchor.MiddleLeft;
            var accent=own?new Color(.28f,.72f,.88f,.9f):new Color(.77f,.57f,.34f,.9f);
            if(Node(plate).Find("Edge")==null)Panel(Node(plate),"Edge",new Vector2(-169,0),new Vector2(3,60),accent);
            if(Node(plate).Find("Divider")==null)Panel(Node(plate),"Divider",new Vector2(49,0),new Vector2(1,56),new Color(.5f,.62f,.7f,.25f));
            if(Node(plate).Find("LifeCaption")==null)Label(Node(plate),"LifeCaption","生命",new Vector2(91,-28),new Vector2(65,18),11,new Color(.62f,.69f,.74f));
            foreach(var graphic in Node(plate).GetComponentsInChildren<Graphic>())
                if(graphic.transform!=Node(plate))graphic.raycastTarget=false;
            var name=SceneText(plate+"/Name");
            var marquee=name.GetComponent<PlayerNameMarquee>();
            if(marquee==null)marquee=name.gameObject.AddComponent<PlayerNameMarquee>();
            marquee.Initialize(name);
        }
        ApplyDuelLayout();
        ArenaPlace("GearBox",new Vector2(0,1),new Vector2(80,-44),new Vector2(98,44));
        ArenaPlace("PriorityPill",new Vector2(1,0),new Vector2(-180,226),new Vector2(270,38));
        ArenaPlace("PriorityPill/Text",new Vector2(.5f,.5f),Vector2.zero,new Vector2(240,34));
        ArenaTextSize("PriorityPill/Text",16);SceneText("PriorityPill/Text").alignment=TextAnchor.MiddleCenter;
        SceneText("CornerCluster/ManaRow/Label").alignment=TextAnchor.MiddleCenter;
        ArenaPlace("PlayerHandRoot",new Vector2(.5f,0),new Vector2(0,0),new Vector2(100,100));
        ArenaPlace("OpponentHandRoot",new Vector2(.5f,1),Vector2.zero,new Vector2(100,100));
        ArenaPlace("ZonePiles_PlayerBottomLeft",new Vector2(0,0),new Vector2(160,202),new Vector2(186,88));
        ArenaPlace("ZonePiles_OpponentTopRight",new Vector2(1,1),new Vector2(-410,-76),new Vector2(186,88));
        ArenaPlace("InspectPanel",new Vector2(0,.5f),new Vector2(142,0),new Vector2(244,580));
        var backdrop=Node("EnvironmentLayer/SanctumBackdrop").GetComponent<RawImage>();
        if(backdrop!=null)backdrop.color=new Color(.68f,.72f,.75f,1);
    }
    private void ArenaPlace(string path,Vector2 anchor,Vector2 position,Vector2 size)
    {
        var r=(RectTransform)Node(path);r.anchorMin=r.anchorMax=anchor;r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;
    }
    private void ArenaTextSize(string path,int size)
    {
        var text=SceneText(path);
        var preference=text.GetComponent<PreferenceText>();
        if(preference!=null)preference.SetBaseSize(size);
        else PreferenceText.SetSize(text,size);
    }
    private void EndArenaPresentation()
    {
        PresentFlipHighlights(activeNotice);
        foreach(var key in arenaCards.Keys.Where(k=>!retainedCards.Contains(k)).ToArray())
        {arenaCards[key].root.gameObject.SetActive(false);Destroy(arenaCards[key].root.gameObject);arenaCards.Remove(key);}
        otherHand.SetAsLastSibling();
        ForgeBattlefieldCardPointer.RestoreHandOrder(otherHand);
        ownHand.SetAsLastSibling();
        ForgeBattlefieldCardPointer.RestoreHandOrder(ownHand);
        // Let the Canvas settle once per rendered frame, rather than forcing
        // all text and layout to rebuild on every priority snapshot.
    }
    // Visual grouping is deliberately separate from engine identity: expand before choosing a member.
    public static string ArenaGroupKey(ForgeCardView c)
    {
        bool simpleToken=c.token&&!c.creature&&!c.copy&&!c.merged&&!c.effectMarker&&c.stackableStateKnown&&c.stackablePermanent;
        if(c.faceDown)return "hidden:"+c.sector+":"+c.detained+":"+c.ringBearer+":"+c.hasStatBaseline+":"+c.basePower+":"+c.baseToughness+":"+string.Join(",",c.grantedKeywords??Array.Empty<string>())+":"+c.id+":"+c.tapped+":"+c.power+":"+c.toughness+":"+c.faceDownKind+":"+string.Join(",",c.exertedBySeats??Array.Empty<int>())+":"+c.exertedThisTurn+":"+c.currentRules+":"+c.damage+":"+c.attacking+":"+c.blocking;
        return string.Join("|",c.contraption,c.sprocket,c.attraction,string.Join(",",c.attractionLights??Array.Empty<int>()),c.functionalVariant,c.sector,c.chosenSector,c.attachedToKind,c.attachedToId,string.Join(",",c.attachedByIds??Array.Empty<int>()),simpleToken?null:c.printingKey,simpleToken?null:c.currentArtPrintingKey,simpleToken?false:c.currentArtBackFace,simpleToken?null:c.effectSourcePrintingKey,simpleToken?null:c.effectSourceName,simpleToken?false:c.effectSourceBackFace,c.genericEffectName,c.ringBearer,c.ownerSeat,c.effectMarker,c.markerKind,c.markerStatus,c.helperArtName,c.stackableSupport,c.stackableStateKnown,c.stackablePermanent,c.protectingPlayerSeat,c.devotion,c.devotionRequired,c.token,c.copy,c.merged,simpleToken?null:c.tokenArtKey,simpleToken?null:c.tokenSourcePrintingKey,c.name,c.zone,c.land,c.creature,c.tapped,string.Join(",",c.exertedBySeats??Array.Empty<int>()),c.exertedThisTurn,c.detained,c.harnessed,c.solved,c.suspected,c.saddled,c.plotted,c.attacking,c.blocking,c.hasStatBaseline,c.basePower,c.baseToughness,c.powerBaseChanged,c.toughnessBaseChanged,c.effectiveBasePower,c.effectiveBaseToughness,c.hasIntensity,c.intensity,c.originalIntensity,c.originalName,c.originalManaCost,c.manaCost,c.originalColors,c.currentColors,c.hasAbilityChanges,c.originalAbilityText,c.currentAbilityText,c.hasCopyAbilityChanges,c.copyOriginalAbilityText,c.copyCurrentAbilityText,string.Join(",",c.originalKeywordDetails??Array.Empty<string>()),string.Join(",",c.currentKeywordDetails??Array.Empty<string>()),string.Join(",",c.grantedKeywords??Array.Empty<string>()),c.power,c.toughness,c.damage,c.defenderKind,c.defenderId,c.loyalty,c.defense,c.backFace,c.stateName,c.room,c.leftRoomUnlocked,c.rightRoomUnlocked,c.hasCurrentLandState,c.landTypesChanged,c.originalType,c.originalRules,c.currentType,c.currentRules,
            string.Join(",",c.basicLandTypes??Array.Empty<string>()),string.Join(";",(c.pendingDepartures??Array.Empty<ForgePendingDeparture>()).Select(d=>d.action+":"+d.phase+":"+d.yourTurn+":"+d.condition)),
            string.Join(",",(c.counters??Array.Empty<ForgeCardCounter>()).Select(x=>x.kind+":"+x.count)),string.Join(",",c.keywords??Array.Empty<string>()));
    }
    private List<ForgeCardView[]> ArenaVisualGroups(ForgeCardView[] cards,string row)
    {
        bool choosingIndividual=view?.awaitingInput==true &&
            (view.request.kind.StartsWith("InputSelect") || view.request.kind=="SelectCards" || view.request.kind=="InputAttack" || view.request.kind=="InputBlock");
        bool hand=row=="Hand";
        var groups=new List<ForgeCardView[]>();
        foreach(var group in cards.GroupBy(c=>hand?c.id.ToString():ArenaGroupKey(c)))
        {
            string key=row+":"+group.Key;
            // The bridge excludes support permanents with per-object linked state.
            var first=group.First();catalog.TryGetValue(first.printingKey??"",out var definition);
            bool simple=first.stackableStateKnown?first.stackablePermanent:first.stackableSupport || definition!=null && (first.land && first.name is "Mountain" or "Island" or "Swamp" or "Forest" or "Plains"
                || first.creature && string.IsNullOrWhiteSpace(definition.description));
            bool hasCandidate=group.Any(c=>OptionIndex("card:"+c.id)>=0 || (AttackCardCandidate(c.id)||BlockCandidate(c.id)));
            if(hand || first.attacking || first.blocking || !simple || choosingIndividual && hasCandidate || expandedGroups.Contains(key))groups.AddRange(group.Select(c=>new[]{c}));
            else groups.Add(group.ToArray());
        }
        return groups;
    }
    private void LayoutArenaRow(RectTransform parent,ForgeCardView[] cards,string row)
    {
        bool hand=row=="Hand", lands=row.Contains("Lands"), support=row.Contains("Support") || row.Contains("Planeswalkers") || row.Contains("Battles"), own=row.StartsWith("Your");
        var groups=ArenaVisualGroups(cards,row);
        // Support categories keep the same lane at low and high density. Walkers
        // and battles never fall back into the artifact/enchantment packing.
        if(!hand && groups.Count==0) { SetDenseArenaRowVisible(row,false);return; }
        float tableWidth=Mathf.Max(320,Mathf.Min(1320,surface.rect.width-560));
        float sideWidth=Mathf.Min(500,tableWidth*.38f), gap=28;
        float available=hand?tableWidth:support?sideWidth:tableWidth-sideWidth-gap;
        float width=hand?182:lands?112:130;
        // Dense fields wrap rather than hiding permanents behind pagination.
        int columns=hand?Math.Max(1,groups.Count):support?(cards.Any(c=>c.room)?Math.Max(1,Mathf.FloorToInt(sideWidth/180)):Math.Max(1,Math.Min(groups.Count,Mathf.FloorToInt((sideWidth+8)/132)))):Mathf.Clamp(Mathf.FloorToInt((available+8)/(width*.88f+8)),1,6);
        // Artifacts/enchantments share rows, tokens first in reading order.
        // Storage is bottom-up for our side, so reverse those rows after packing.
        var supportRows=new List<List<ForgeCardView[]>>();
        if(support) {
            var members=groups.Where(g=>!g[0].planeswalker).OrderByDescending(g=>g[0].token).ToList();
            float tileWidth=Mathf.Min(112,(sideWidth-8*(columns-1))/columns)*.88f;
            var packed=new List<ForgeCardView[]>();float used=0;
            foreach(var member in members) {
                float footprint=tileWidth+StackOverhang(member);
                if(packed.Count>0 && (packed.Count>=columns || used+12+footprint>sideWidth)) {
                    supportRows.Add(packed);packed=new List<ForgeCardView[]>();used=0;
                }
                used+=(packed.Count>0?12:0)+footprint;packed.Add(member);
            }
            if(packed.Count>0)supportRows.Add(packed);
            if(own)supportRows.Reverse();
            var walkers=groups.Where(g=>g[0].planeswalker).ToList();
            {
                members=walkers;
                for(int start=0;start<members.Count;start+=columns)
                    supportRows.Add(members.Skip(start).Take(columns).ToList());
            }
            groups=supportRows.SelectMany(r=>r).ToList();
        }
        int rows=hand?1:Mathf.CeilToInt(groups.Count/(float)columns);
        if(support)rows=supportRows.Count;
        if(!hand && (support || rows>2 || groups.Count>12)) {
            LayoutDenseArenaRow(parent,groups,row,own,lands,support,tableWidth,sideWidth,available);
            return;
        }
        if(!hand)SetDenseArenaRowVisible(row,false);
        if(!hand && !lands && !support && rows>1)width=Mathf.Min(width,100);
        if(!hand && lands && rows>1)width=Mathf.Min(width,62.5f);
        if(!hand && !lands && !support && rows>1 && HasTwoLandRows(own))width=Mathf.Min(width,89);
        if(support)width=Mathf.Min(112,(sideWidth-8*(columns-1))/columns);
        // Battlefield tiles expose illustration and name; full rules remain in hover/hand.
        float height=width*(hand?254f/182f:.88f);
        float cardWidth=hand?width:width*.88f;
        float step=hand?Mathf.Min(112,(available-cardWidth-32)/Math.Max(1,cards.Length-1)):cardWidth+8;
        float y=hand?46:lands || support?(own?-(rows>1?292:304):(rows>1?292:304)):(own?-(rows>1?68:102):(rows>1?68:102));
        float x=hand?0:36+(support?tableWidth*.5f-sideWidth*.5f:-(sideWidth+gap)*.5f);
        if(lands)x+=-available*.5f+cardWidth*.5f+(Math.Min(columns,groups.Count)-1)*step*.5f;
        var supportOffsets=new float[rows];float supportUsed=0;
        if(support)for(int line=0;line<rows;line++) {
            supportOffsets[line]=supportUsed;
            supportUsed+=supportRows[line].Max(g=>g[0].planeswalker?cardWidth*1.3f:height)+8;
        }
        for(int i=0;i<groups.Count;i++)
        {
            int line=i/columns, col=i%columns;
            int n=Math.Min(columns,groups.Count-line*columns);
            if(support) {
                int remaining=i;line=0;
                while(remaining>=supportRows[line].Count){remaining-=supportRows[line].Count;line++;}
                col=remaining;n=supportRows[line].Count;
            }
            float offset=col-((support?columns:n)-1)*.5f;
            float fan=offset/Mathf.Max(1,(n-1)*.5f);
            var position=new Vector2(x+offset*step,y+(hand?-fan*fan*28:(support?supportOffsets[line]*(own?1:-1):line*(own?-1:1)*(height+10))));
            if(support)position.x=x-sideWidth*.5f+cardWidth*.5f+supportRows[line].Take(col).Sum(g=>cardWidth+StackOverhang(g)+12)+StackOverhang(groups[i]);
            var member=groups[i][0];string groupKey=row+":"+ArenaGroupKey(member);
            if(!hand && member.attacking)position.y+=own?36:-36;
            float memberHeight=!hand && member.planeswalker?cardWidth*1.3f:height;
            float memberWidth=!hand&&member.room?Mathf.Min(sideWidth-16,cardWidth*1.7f):cardWidth;
            bool compactToken=!hand && member.token && !member.faceDown && !member.creature && !member.planeswalker && !member.battle && !member.land && !member.room
                && ((member.currentType??"").Contains("Artifact") || (member.currentType??"").Contains("Enchantment"));
            if(compactToken){memberWidth=cardWidth*.74f;memberHeight=cardWidth*.65f;}
            if(!hand)position.y+=(own?1:-1)*(memberHeight-height)*.5f;
            DrawArenaCard(parent,member,groups[i].Length,row+":"+member.id,groupKey,position,new Vector2(memberWidth,memberHeight),hand,hand?-fan*Mathf.Min(14,(n-1)*1.5f):member.tapped?(own?-12:12):0,i);
        }
    }
    private void DrawArenaCard(RectTransform parent,ForgeCardView card,int count,string key,string groupKey,Vector2 position,Vector2 size,bool hand,float rotation,int order)
    {
        retainedCards.Add(key);
        if(!arenaCards.TryGetValue(key,out var item))
        {
            item=new ArenaCard();item.root=Panel(parent,"Card_"+card.id,position,size,Color.clear);
            item.face=Rect(item.root,"Visual",Vector2.zero,size);
            item.button=item.root.gameObject.AddComponent<Button>();item.button.transition=Selectable.Transition.None;
            item.pointer=item.root.gameObject.AddComponent<ForgeBattlefieldCardPointer>();arenaCards.Add(key,item);
        }
        if(item.root.parent!=parent)item.root.SetParent(parent,false);
        bool denseParent=parent.parent!=null&&parent.parent.name.StartsWith("Dense_",StringComparison.Ordinal);
        item.root.anchorMin=item.root.anchorMax=new Vector2(.5f,denseParent?1:.5f);
        item.root.sizeDelta=size;item.face.sizeDelta=size;
        int option=OptionIndex("card:"+card.id);
        if(hand && DirectHandSelection)option=Array.FindIndex(view.request.options,o=>HasChoiceCard(o)&&o.card.id==card.id);
        if(option<0&&view?.request?.kind=="InputPassPriority"&&HasPreparedSpell(card))option=OptionIndex("card:"+card.preparedSpell.id);
        long captured=requestId;
        bool actionable=canAct && (option>=0 && !view.request.options[option].disabled || !hand && (AttackCardCandidate(card.id)||BlockCandidate(card.id))) && !submitted;
        var visibility=item.root.GetComponent<CanvasGroup>();
        if(visibility==null)visibility=item.root.gameObject.AddComponent<CanvasGroup>();
        visibility.alpha=hand && card.zone is "Graveyard" or "Exile" or "Command" && !actionable?.65f:1f;
        visibility.blocksRaycasts=true;
        bool paymentUncertain=option>=0 && view.request.options[option].requiresPaymentCheck;
        bool selected=option>=0 && (view.request.options[option].selected || view.request.options[option].label.StartsWith("[已选回牌库底]") || IsStagedHandChoice(view.request.options[option].id));
        bool attackSelected=IsDeclaringAttack && selectedAttackers.Contains(card.id) || IsBlockingChoice && selectedBlocker==card.id;
        bool playableHand=hand && actionable && !paymentUncertain && view.request.kind=="InputPassPriority";
        bool own=key.StartsWith("Your",StringComparison.Ordinal);
        string appearance=ArenaGroupKey(card)+"/"+size+"/"+count+"/"+actionable+"/"+selected+"/"+paymentUncertain+"/"+playableHand+"/"+attackSelected
            +"/band:"+string.Join(",",card.attackingBandIds??Array.Empty<int>())
            +"/"+card.qualificationKind+":"+card.qualificationCount+":"+card.qualificationRequired+":"+card.qualificationEarned+"/"+GamePreferences.Revision;
        if(item.appearance!=appearance)
        {
            bool revealed=item.appearance!=null && item.faceDown && !card.faceDown;
            RectTransform oldFace=null;
            if(revealed) {oldFace=Instantiate(item.face,item.root);oldFace.name="RevealBack";foreach(var g in oldFace.GetComponentsInChildren<Graphic>())g.raycastTarget=false;}
            item.faceDown=card.faceDown;
            item.appearance=appearance;Clear(item.face);
            Color accent=selected || attackSelected?Gold:card.attacking?new Color(1,.36f,.14f):card.blocking?Blue:actionable && !paymentUncertain?new Color(.3f,.85f,1):new Color(.38f,.38f,.36f);
            if(HasTemporaryTokenFrame(card)&&!selected&&!attackSelected&&!actionable&&!card.attacking&&!card.blocking)accent=new Color(.86f,.9f,.95f);
            if(selected || attackSelected || playableHand || hand && DirectHandSelection && actionable)
            {
                var glow=Rect(item.face,selected||attackSelected?"SelectedGlow":"PlayableGlow",Vector2.zero,size).gameObject.AddComponent<ForgePlayableGlow>();
                glow.selected=selected||attackSelected;
                glow.raycastTarget=false;
                accent=selected||attackSelected?new Color(1,.83f,.32f):new Color(.68f,.95f,1);
            }
            bool stateArt=card.effectMarker&&!string.IsNullOrEmpty(card.helperArtName)&&card.markerKind!="delayed";
            if(!hand&&!stateArt) {
                var shadow=Panel(item.face,"CardShadow",new Vector2(0,-5),size+new Vector2(5,5),new Color(0,0,0,.6f));
                shadow.GetComponent<Image>().sprite=HasTokenFrame(card)?TokenFrame(card):IntegratedUiUtility.RoundedRect(140,125,10);
            }
            var border=Panel(item.face,"Border",Vector2.zero,size,accent);
            if(!hand)border.GetComponent<Image>().sprite=card.markerKind=="delayed"?IntegratedUiUtility.RoundedRect(76,68,12):stateArt?IntegratedUiUtility.RoundedRect(76,82,6):card.effectMarker?EffectMarkerShape():card.emblem?EmblemShape():HasTokenFrame(card)?TokenFrame(card):IntegratedUiUtility.RoundedRect(140,125,9);
            if(stateArt)border.GetComponent<Image>().color=Color.clear;
            if(card.emblem||card.effectMarker&&!stateArt)border.gameObject.AddComponent<Mask>().showMaskGraphic=true;
            var face=Panel(border,"Face",Vector2.zero,size-Vector2.one*5,Ink);
            item.cardFace=face;
            catalog.TryGetValue(card.printingKey??"",out var definition);Sprite art=null;
            TryBattlefieldArt(card, out art);
            var image=face.GetComponent<Image>();image.sprite=art;image.preserveAspect=true;
            image.color=art==null?Ink:!hand && card.tapped?new Color(.65f,.68f,.72f):Color.white;
            if(!hand && art!=null && card.faceDown)PresentFaceDownFace(face,art,size-Vector2.one*5);
            if(!hand && art!=null && !card.faceDown) {
                image.preserveAspect=false;
                if(stateArt){image.preserveAspect=true;image.color=Color.white;}
                else if(card.emblem||card.effectMarker)PresentSpecialPermanentFace(face,art,size-Vector2.one*5,card);
                else if(card.room)PresentRoomFace(face,art,size-Vector2.one*5,card);
                else if(HasTokenFrame(card) || card.planeswalker)PresentSpecialPermanentFace(face,art,size-Vector2.one*5,card);
                else PresentCompactBattlefieldFace(face,art,size-Vector2.one*5,card.tapped);
            }
            if(card.markerKind=="delayed"){
                var clock=Panel(item.face,"DelayedClock",new Vector2(size.x*.5f-11,-size.y*.5f+12),new Vector2(28,28),Ink);
                clock.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(28,28,14);
                var glyph=Rect(clock,"Icon",Vector2.zero,new Vector2(22,22)).gameObject.AddComponent<DelayedEffectIcon>();glyph.color=Gold;glyph.raycastTarget=false;
            }
            if(!hand&&HasTemporaryTokenFrame(card)) {
                var expiryGlow=Rect(item.face,"TemporaryCopyGlow",Vector2.zero,size).gameObject.AddComponent<TemporaryCopyGlow>();
                expiryGlow.raycastTarget=false;
            }
            if(hand && card.zone is "Graveyard" or "Exile" or "Command") {
                PresentPreparedSpellFace(face,card,new Vector2(240,335));
                var spellFace=face.Find("PreparedSpellFace");if(spellFace!=null)spellFace.localScale=Vector3.one*((size.x-5)/240);
                var zone=Panel(item.face,"PlayableZone",new Vector2(-size.x*.5f+17,size.y*.5f-17),new Vector2(27,25),card.zone=="Graveyard"?new Color(.31f,.22f,.4f):new Color(.17f,.31f,.38f));
                zone.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(27,25,5);
                Label(zone,"Label",card.zone=="Command"?TapPaymentText("命","C","統"):card.zone=="Graveyard"?"墓":"放",Vector2.zero,new Vector2(25,23),14,Paper);
            }
            if(hand)PresentComponentFace(face,card);
            if(!hand && art!=null && card.tapped)
            {
                var tap=Rect(item.face,"TappedSymbol",new Vector2(0,size.y*.12f),Vector2.one*size.x*.6f).gameObject.AddComponent<ForgeTappedIndicator>();
                tap.color=new Color(.91f,.92f,.94f,.5f);tap.raycastTarget=false;
            }
            if(card.markerKind=="player-protection") {
                image.color=Color.clear;
                var protection=Rect(face,"PlayerProtection",Vector2.zero,new Vector2(56,70)).gameObject.AddComponent<PlayerProtectionIcon>();
                protection.color=GrantedKeywordColor;protection.raycastTarget=false;
            }
            else if(IsSprocketMarker(card))PresentSprocketMarker(face,size,card);
            else if(art==null)Label(face,"Name","缺失卡图",Vector2.zero,size-Vector2.one*8,12,Paper);
            string speedValue=SpeedMarkerValue(card);
            if(!hand&&speedValue.Length>0) {
                float badgeSize=40;
                var badge=Panel(item.face,"SpeedStatus",new Vector2(size.x*.5f-badgeSize*.5f-3,-size.y*.5f+badgeSize*.5f+3),Vector2.one*badgeSize,Ink);
                badge.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(28,26,6);
                var value=Label(badge,"Value",speedValue,Vector2.zero,Vector2.one*(badgeSize-2),20,Paper);
                badgeSize=Mathf.Max(28,Mathf.Ceil(value.preferredHeight)+6,Mathf.Ceil(value.preferredWidth)+6);
                badge.sizeDelta=Vector2.one*badgeSize;
                badge.anchoredPosition=new Vector2(size.x*.5f-badgeSize*.5f-3,-size.y*.5f+badgeSize*.5f+3);
                value.rectTransform.sizeDelta=Vector2.one*(badgeSize-2);
            }
            if(!hand)
            {
                if(card.creature)
                {
                    float statsWidth=size.x*.38f,statsHeight=Mathf.Min(23,size.y*.2f);
                    var stats=Panel(item.face,"Stats",new Vector2(size.x*.5f-statsWidth*.5f-4,-size.y*.5f+statsHeight*.5f+4),new Vector2(statsWidth,statsHeight),new Color(.52f,.53f,.5f));
                    stats.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(60,26,5);
                    var inset=Panel(stats,"Inset",Vector2.zero,new Vector2(statsWidth-3,statsHeight-3),new Color(.94f,.94f,.88f));
                    inset.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(60,26,5);
                    // Numerals use a regular serif face, independently of the CJK UI font.
                    if(battlefieldNumeralFont==null)battlefieldNumeralFont=Font.CreateDynamicFontFromOSFont(new[]{"Times New Roman","Liberation Serif","Georgia"},24);
                    var value=Rect(stats,"Value",new Vector2(0,1),new Vector2(statsWidth-3,statsHeight)).gameObject.AddComponent<Text>();
                    value.font=battlefieldNumeralFont!=null?battlefieldNumeralFont:GamePreferences.Font;
                    var numeralPreferences=value.gameObject.AddComponent<PreferenceText>();
                    numeralPreferences.KeepFont=true;
                    value.font=battlefieldNumeralFont!=null?battlefieldNumeralFont:GamePreferences.Font;
                    value.supportRichText=true;value.text=ModifiedStatsText(card,false);PreferenceText.SetSize(value,Mathf.Clamp(Mathf.RoundToInt(size.x*.175f),12,24));
                    value.color=new Color(.05f,.06f,.065f);
                    value.fontStyle=FontStyle.Bold;value.alignment=TextAnchor.MiddleCenter;
                    value.resizeTextForBestFit=true;value.resizeTextMinSize=9;value.resizeTextMaxSize=value.fontSize;
                    value.horizontalOverflow=HorizontalWrapMode.Overflow;value.verticalOverflow=VerticalWrapMode.Truncate;
                }
                if(card.planeswalker || card.battle)
                {
                    float badgeSize=Mathf.Clamp(size.x*.34f,24,36);
                    var counter=Rect(item.face,"DefenseCounter",new Vector2(0,-size.y*.5f+2),new Vector2(badgeSize,badgeSize));
                    counter.gameObject.AddComponent<ForgeLoyaltyShield>().raycastTarget=false;
                    var value=Label(counter,"Value",(card.planeswalker?card.loyalty:card.defense).ToString(),new Vector2(0,2),new Vector2(badgeSize-4,badgeSize-4),22,Color.white);
                    value.fontStyle=FontStyle.Bold;value.resizeTextForBestFit=true;value.resizeTextMinSize=12;value.resizeTextMaxSize=22;
                }
                if(card.attacking) {
                    var marker=Rect(item.face,"AttackMarker",new Vector2(0,(own?1:-1)*(size.y*.5f+8)),new Vector2(size.x*1.06f,Mathf.Min(40,size.x*.32f))).gameObject.AddComponent<ForgeAttackMarker>();
                    marker.rectTransform.localRotation=Quaternion.Euler(0,0,own?0:180);
                    marker.raycastTarget=false;
                }
                string state=card.creature&&card.damage>0?"−"+card.damage:"";
                if(state.Length>0)Label(item.face,"State",state,new Vector2(0,-size.y*.5f-13),new Vector2(size.x,22),12,accent);
                PresentPermanentStatus(item.face,size,card);
                PresentSectorBadge(item.face,size,card);
                PresentAuxiliaryBadge(item.face,size,card);
                if(card.attacking&&(card.attackingBandIds?.Length??0)>1) {
                    var badge=Panel(item.face,"AttackBandBadge",new Vector2(0,size.y*.5f-27),new Vector2(Mathf.Min(size.x,80),24),Ink);
                    var group=Label(badge,"AttackBand",TapPaymentText("联合 ","Band ","バンド ")+card.attackingBandIds.Min(),
                        Vector2.zero,((RectTransform)badge).sizeDelta,11,Gold);
                    group.fontStyle=FontStyle.Bold;group.verticalOverflow=VerticalWrapMode.Overflow;
                }
            }
            if(count>1)
            {
                float countWidth=Mathf.Max(42,24+count.ToString().Length*13);
                bool supportCount=!card.creature&&!card.land&&!card.planeswalker&&!card.battle;
                float countX=supportCount?size.x*.5f-countWidth*.5f:-size.x*.35f+(countWidth-42)*.5f;
                var badge=Panel(item.face,"Count",new Vector2(countX,card.planeswalker||card.battle?size.y*.5f-12:-size.y*.43f),new Vector2(countWidth,26),Ink);
                var countLabel=Label(badge,"Value","×"+count,Vector2.zero,new Vector2(countWidth-2,24),19,Paper);
                countLabel.horizontalOverflow=HorizontalWrapMode.Overflow;
                // The locale font's line height can exceed its point size. Fit the
                // first frame too, before periodic preference binding runs.
                float countHeight=Mathf.Max(24,Mathf.Ceil(countLabel.preferredHeight)+2);
                countLabel.rectTransform.sizeDelta=new Vector2(countWidth-2,countHeight);
                badge.sizeDelta=new Vector2(countWidth,countHeight+2);
            }
            if(selected && view.request.kind=="InputLondonMulligan")Label(item.face,"Selected",TapPaymentText("回牌库底","Return to bottom","一番下に戻す"),new Vector2(0,size.y*.35f),new Vector2(size.x,26),18,Gold);
            foreach(var graphic in item.face.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
            item.button.targetGraphic=item.root.GetComponent<Image>();
            if(revealed)StartCoroutine(AnimateFaceUp(oldFace,border));
        }
        // Costs can change while a hand/zone card retains its layout identity.
        // Clear destroys old children at end of frame. Use the new face's
        // identity directly instead of finding an old same-named child.
        var componentHost=item.cardFace;
        if(componentHost!=null){PresentComponentFace(componentHost,hand?card:null);PresentLegacyCost(componentHost,hand?card:null);}
        item.groupKey=groupKey;
        item.representedCount=count;
        item.button.interactable=true;item.button.onClick.RemoveAllListeners();
        Action choose=()=> {
            if(!hand && view?.request?.kind=="InputBlock"){ClickBlockCard(card.id,captured);return;}
            if(!hand && view?.request?.kind=="InputAttack"){ClickAttackCard(card.id,captured);return;}
            // A grouped permanent is a visual summary. Expand before choosing
            // an exact member, including lands; a new native request collapses it.
            if(count>1) {expandedGroups.Add(groupKey);groupExpandedFrame=Time.frameCount;PresentIntegrated(connectionText.text);}
            else if(actionable)Choose(option,captured);
        };
        item.button.onClick.AddListener(()=>choose());
        item.pointer.Configure(()=> {if(!hand && !IsDeclaringAttack && !IsBlockingChoice)InspectIntegrated(card);},LeaveInspectCard,choose,hand && actionable && view?.request?.kind=="InputPassPriority");
        if(IsBlockingChoice)item.pointer.ConfigureCombat(!hand&&BlockCandidate(card.id)?p=>BeginBlock(card.id,captured):null,UpdateBlockArrow,p=>DropBlock(p,captured));
        else item.pointer.ConfigureCombat(!hand && CanSelectAttacker(card.id)?p=>BeginAttackSelection(card.id,captured):null,
            UpdateAttackArrowAt,p=>TryChooseAttackAt(p,captured));
        item.pointer.SetLayout(position,rotation,item.face,hand,order);

    }

    private static string StatusName(string key)
    {
        switch(key) {
            case "WARD":return TapPaymentText("守护","Ward","護法");case "FLYING":return TapPaymentText("飞行","Flying","飛行");case "TRAMPLE":return TapPaymentText("践踏","Trample","トランプル");case "HASTE":return TapPaymentText("敏捷","Haste","速攻");
            case "VIGILANCE":return TapPaymentText("警戒","Vigilance","警戒");case "DEATHTOUCH":return TapPaymentText("死触","Deathtouch","接死");case "LIFELINK":return TapPaymentText("系命","Lifelink","絆魂");
            case "REACH":return TapPaymentText("延势","Reach","到達");case "FIRST_STRIKE":return TapPaymentText("先攻","First strike","先制攻撃");case "DOUBLE_STRIKE":return TapPaymentText("连击","Double strike","二段攻撃");
            case "HEXPROOF":return TapPaymentText("辟邪","Hexproof","呪禁");case "INDESTRUCTIBLE":return TapPaymentText("不灭","Indestructible","破壊不能");case "MENACE":return TapPaymentText("威慑","Menace","威迫");
            case "DEFENDER":return TapPaymentText("守军","Defender","防衛");case "EXALTED":return TapPaymentText("颂威","Exalted","賛美");case "FEAR":return TapPaymentText("恐惧","Fear","畏怖");
            case "FLASH":return TapPaymentText("闪现","Flash","瞬速");case "HORSEMANSHIP":return TapPaymentText("马术","Horsemanship","馬術");case "INTIMIDATE":return TapPaymentText("威吓","Intimidate","威嚇");
            case "LANDWALK":return TapPaymentText("地行者","Landwalk","土地渡り");case "SHADOW":return TapPaymentText("次元幽影","Shadow","シャドー");case "SHROUD":return TapPaymentText("帷幕","Shroud","被覆");
            case "WITHER":return TapPaymentText("干枯","Wither","萎縮");case "ANNIHILATOR":return TapPaymentText("歼灭","Annihilator","滅殺");case "TOXIC":return TapPaymentText("下毒","Toxic","毒性");case "PROTECTION":return TapPaymentText("保护","Protection","プロテクション");
            case "STUN":return TapPaymentText("晕眩","Stun","麻痺");case "P1P1":return "+1/+1";case "M1M1":return "−1/−1";
            default:return key;
        }
    }
    private static bool HasDedicatedCounter(ForgeCardView card,string kind)
        => card.planeswalker&&string.Equals(kind,"LOYALTY",StringComparison.OrdinalIgnoreCase)
            || card.battle&&string.Equals(kind,"DEFENSE",StringComparison.OrdinalIgnoreCase);
    private static string PermanentCounterName(ForgeCardCounter counter)
    {
        switch(counter.kind) {
            case "STUN":return TapPaymentText("晕眩","Stun","麻痺");
            case "SHIELD":return TapPaymentText("护盾","Shield","盾");
            case "CHARGE":return TapPaymentText("充电","Charge","蓄積");
            case "OIL":return TapPaymentText("油","Oil","油");
            case "TIME":return TapPaymentText("时间","Time","時間");
            case "AGE":return TapPaymentText("年龄","Age","経年");
            case "QUEST":return TapPaymentText("探索","Quest","探索");
            case "VERSE":return TapPaymentText("诗歌","Verse","詩句");
            case "LORE":return TapPaymentText("学问","Lore","伝承");
            default:return StatusName(counter.kind)!=counter.kind?StatusName(counter.kind):string.IsNullOrEmpty(counter.label)?counter.kind:counter.label;
        }
    }
    // Native attributes are state, not keyword abilities. Keep the complete list
    // in the inspector and a compact single badge on the battlefield face.
    private static string[] AttributeStateLabels(ForgeCardView card)
    {
        if(card.faceDown)return Array.Empty<string>();
        var labels=new List<string>();
        if(card.harnessed)labels.Add(TapPaymentText("∞ 已启用","Harnessed","装着済み"));
        if(card.solved)labels.Add(TapPaymentText("已侦结","Solved","解明完了"));
        if(card.suspected)labels.Add(TapPaymentText("疑犯","Suspected","容疑あり"));
        if(card.saddled)labels.Add(TapPaymentText("已乘驾","Saddled","騎乗済み"));
        if(card.plotted)labels.Add(TapPaymentText("已谋划","Plotted","計画済み"));
        return labels.ToArray();
    }
    private static void PresentPermanentStatus(RectTransform face,Vector2 size,ForgeCardView card)
    {
        if(card.ringBearer&&card.creature) {
            float diameter=Mathf.Clamp(size.x*.25f,16,22);
            var badge=Panel(face,"RingBearer",new Vector2(0,size.y*.5f-diameter*.5f-2),Vector2.one*(diameter+2),Ink);
            badge.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(24,24,12);badge.GetComponent<Image>().raycastTarget=false;
            var icon=Rect(badge,"Icon",Vector2.zero,Vector2.one*diameter).gameObject.AddComponent<RingBearerIcon>();icon.color=Gold;icon.raycastTarget=false;
        }
        if(card.detained) {
            var badge=Panel(face,"Detained",new Vector2(size.x*.5f-18,size.y*.5f-10-((card.exertedBySeats?.Length??0)>0?20:0)),new Vector2(34,18),new Color(.08f,.12f,.15f,.95f));
            badge.GetComponent<Image>().raycastTarget=false;
            var label=Label(badge,"Label",TapPaymentText("拘留","Detained","留置"),Vector2.zero,new Vector2(34,18),10,GrantedKeywordColor);
            label.horizontalOverflow=HorizontalWrapMode.Overflow;label.raycastTarget=false;
            float width=Mathf.Max(34,Mathf.Ceil(label.preferredWidth)+4);
            badge.sizeDelta=new Vector2(width,18);
            badge.anchoredPosition=new Vector2(size.x*.5f-width*.5f-1,size.y*.5f-10-((card.exertedBySeats?.Length??0)>0?20:0));
            label.rectTransform.sizeDelta=new Vector2(width,18);
        }
        if((card.exertedBySeats?.Length??0)>0) {
            var badge=Panel(face,"Exerted",new Vector2(size.x*.5f-18,size.y*.5f-10),new Vector2(34,18),new Color(.08f,.12f,.15f,.95f));
            badge.GetComponent<Image>().raycastTarget=false;
            var label=Label(badge,"Label",TapPaymentText("耗竭","Exerted","督励"),Vector2.zero,new Vector2(34,18),10,GrantedKeywordColor);
            label.horizontalOverflow=HorizontalWrapMode.Overflow;label.raycastTarget=false;
        }
        var attributes=AttributeStateLabels(card);
        if(attributes.Length>0) {
            float offset=(card.detained?20:0)+((card.exertedBySeats?.Length??0)>0?20:0);
            string text=attributes[0]+(attributes.Length>1?" +"+(attributes.Length-1):"");
            float width=Mathf.Max(34,size.x-2);
            var badge=Panel(face,"AttributeState",new Vector2(0,size.y*.5f-10-offset),new Vector2(width,18),new Color(.08f,.12f,.15f,.95f));
            badge.GetComponent<Image>().raycastTarget=false;
            var label=Label(badge,"Label",text,Vector2.zero,new Vector2(width-4,18),10,GrantedKeywordColor);
            label.resizeTextForBestFit=true;label.resizeTextMinSize=8;label.resizeTextMaxSize=10;
            label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Overflow;label.raycastTarget=false;
        }
        if(card.qualificationRequired>0 && !card.faceDown) {
            var badge=Panel(face,"Qualification",new Vector2(-size.x*.5f+15,-size.y*.16f),new Vector2(40,26),Ink);
            badge.GetComponent<Image>().raycastTarget=false;
            var value=Label(badge,"Value",card.qualificationCount+"/"+card.qualificationRequired,Vector2.zero,new Vector2(40,26),13,card.qualificationEarned?Gold:Paper);
            value.horizontalOverflow=HorizontalWrapMode.Overflow;value.raycastTarget=false;
        }
        if(card.devotionRequired>0 && !card.faceDown) {
            var badge=Panel(face,"Devotion",new Vector2(-size.x*.5f+12,0),new Vector2(32,34),Ink);
            var text=Label(badge,"Value",card.devotion+"/"+card.devotionRequired,Vector2.zero,new Vector2(32,34),13,Paper);
            text.horizontalOverflow=HorizontalWrapMode.Overflow;text.raycastTarget=false;
        }
        var counters=(card.counters??Array.Empty<ForgeCardCounter>()).Where(c=>c.count>0&&!HasDedicatedCounter(card,c.kind)).ToArray();
        float counterTop=counters.Length>1?size.y*.5f-9:size.y*.25f+14;
        for(int i=0;i<Mathf.Min(3,counters.Length);i++) {
            var c=counters[i];bool stun=c.kind=="STUN",positive=c.kind=="P1P1",negative=c.kind=="M1M1",lore=c.kind=="LORE";
            bool overflow=i==2&&counters.Length>3;
            string value=overflow?(counters.Length-2).ToString():(positive?"+":negative?"−":"")+c.count;
            if(overflow)stun=positive=negative=lore=false;
            Color numberColor=positive?new Color(.32f,.78f,1f):negative?new Color(1f,.32f,.32f):lore?new Color(.98f,.85f,.57f):Paper;
            // Signed counters stay readable over either light or dark artwork;
            // lore has its own chapter medallion instead of the generic gold box.
            float width=positive||negative?Mathf.Max(32,12+value.Length*11):lore?28:30+value.Length*9;
            var badgeSize=new Vector2(width,lore?28:positive||negative?24:20);
            var badge=Panel(face,overflow?"Counter_Overflow":"Counter_"+c.kind,new Vector2(-size.x*.5f+width*.5f-5,counterTop-badgeSize.y*.5f),badgeSize,
                lore?new Color(.67f,.53f,.3f):new Color(.025f,.045f,.065f,.94f));
            badge.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(width,badgeSize.y,lore?14:7);badge.GetComponent<Image>().raycastTarget=false;
            if(lore) {
                var inset=Panel(badge,"ChapterInset",Vector2.zero,badgeSize-Vector2.one*3,new Color(.09f,.14f,.18f));
                inset.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(25,25,13);inset.GetComponent<Image>().raycastTarget=false;
            }
            var count=Label(badge,"Count",value,Vector2.zero,badgeSize-Vector2.one*2,positive||negative?18:lore?17:16,numberColor);count.fontStyle=positive||negative||lore?FontStyle.Bold:FontStyle.Normal;count.raycastTarget=false;
            if(!positive&&!negative&&!lore) {
                width=Mathf.Ceil(count.preferredWidth)+34;badgeSize.x=width;badge.sizeDelta=badgeSize;
                badge.anchoredPosition=new Vector2(-size.x*.5f+width*.5f-5,badge.anchoredPosition.y);
                badge.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(width,badgeSize.y,7);
                count.rectTransform.sizeDelta=new Vector2(width-28,badgeSize.y-2);count.rectTransform.anchoredPosition=new Vector2(12,0);
                var icon=Rect(badge,"TypeIcon",new Vector2(-width*.5f+12,0),new Vector2(20,20)).gameObject.AddComponent<PermanentCounterIcon>();
                icon.Bind(overflow?"MORE":c.kind);icon.color=stun?new Color(.32f,.78f,1f):Paper;icon.raycastTarget=false;
            }
            counterTop-=badgeSize.y+2;
            // CJK font line metrics can exceed this compact badge even when the
            // numeral itself fits. Preserve the chosen size and render its glyph.
            count.horizontalOverflow=HorizontalWrapMode.Overflow;count.verticalOverflow=VerticalWrapMode.Overflow;
        }
        var keywords=(card.keywords??Array.Empty<string>()).Where(k=>KeywordIcons.TryLoad(k,out _)||StatusName(k)!=k).Distinct().ToArray();
        float used=0,available=Mathf.Max(12,size.x*(card.creature ? .62f : 1f)-9);
        foreach(string keyword in keywords) {
            bool hasIcon=KeywordIcons.TryLoad(keyword,out var sprite);
            string parameter=KeywordIcons.Parameter(card,keyword,out bool manaParameter);
            int glyphCount=manaParameter?System.Text.RegularExpressions.Regex.Matches(parameter,@"\{[^{}]+\}").Count:0;
            float parameterWidth=parameter.Length==0?0:manaParameter?glyphCount*12:Mathf.Max(12,parameter.Length*8);
            float iconSize=20;
            float width=hasIcon?iconSize+2+(parameterWidth>0?parameterWidth+2:0):32;
            if(used+width>available&&used>0)break;
            // Keep the power/toughness plate clear; complicated payment details
            // remain fully readable in the existing inspector.
            if(width>available&&hasIcon) {
                manaParameter=false;
                if(available<24) {parameter="";parameterWidth=0;iconSize=Mathf.Min(20,available-2);width=iconSize+2;}
                else {parameter="…";parameterWidth=8;iconSize=Mathf.Min(20,available-12);width=iconSize+12;}
            }
            var badge=Panel(face,"Keyword_"+keyword,new Vector2(-size.x*.5f+3+used+width*.5f,-size.y*.5f+12),new Vector2(width,22),new Color(.08f,.12f,.15f,.95f));
            badge.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(width,22,4);
            badge.GetComponent<Image>().raycastTarget=false;
            bool granted=(card.grantedKeywords??Array.Empty<string>()).Contains(keyword);
            if(hasIcon) {
                // Preserve the atlas colors. A blue rim marks gained abilities
                // without washing out the glyph's distinctive original colors.
                if(granted)badge.GetComponent<Image>().color=GrantedKeywordColor;
                var icon=Rect(badge,"Icon",new Vector2(-width*.5f+iconSize*.5f+1,0),Vector2.one*iconSize).gameObject.AddComponent<UnityEngine.UI.Image>();
                icon.sprite=sprite;icon.preserveAspect=true;icon.raycastTarget=false;
                if(parameter.Length>0) {
                    var value=Rect(badge,"Parameter",new Vector2(width*.5f-parameterWidth*.5f-1,0),new Vector2(parameterWidth,20));
                    if(manaParameter)value.gameObject.AddComponent<TemplateManaCost>().Bind(parameter,12,false,0);
                    else {
                        var number=Label(value,"Value",parameter,Vector2.zero,new Vector2(parameterWidth,20),12,granted?new Color(.02f,.08f,.13f):Paper);
                        number.horizontalOverflow=HorizontalWrapMode.Overflow;number.verticalOverflow=VerticalWrapMode.Overflow;number.raycastTarget=false;
                    }
                }
            }else {
                var title=Label(badge,"Label",StatusName(keyword),Vector2.zero,new Vector2(32,20),11,granted?GrantedKeywordColor:Paper);
                title.horizontalOverflow=HorizontalWrapMode.Overflow;title.raycastTarget=false;
            }
            used+=width+2;
        }
    }
    private static void PresentRoomFace(RectTransform parent,Sprite sprite,Vector2 size,ForgeCardView card)
    {
        var background=parent.GetComponent<Image>();background.sprite=IntegratedUiUtility.RoundedRect(200,108,7);background.color=Ink;
        parent.gameObject.AddComponent<Mask>().showMaskGraphic=true;
        var art=Rect(parent,"RoomIllustration",Vector2.zero,new Vector2(size.y,size.x)).gameObject.AddComponent<RawImage>();
        art.rectTransform.localRotation=Quaternion.Euler(0,0,-90);art.texture=sprite.texture;art.raycastTarget=false;
        var r=sprite.rect;
        // Room printings are sideways split cards: retain both names and illustrations,
        // excluding the rules column. Rotate the paired face upright as one image.
        art.uvRect=new Rect((r.x+r.width*.045f)/sprite.texture.width,(r.y+r.height*.035f)/sprite.texture.height,r.width*.56f/sprite.texture.width,r.height*.93f/sprite.texture.height);
        art.color=card.tapped?new Color(.65f,.68f,.72f):Color.white;
        foreach(var door in new[]{(-1,card.leftRoomUnlocked),(1,card.rightRoomUnlocked)})if(!door.Item2) {
            var locked=Panel(parent,door.Item1<0?"LeftDoorLocked":"RightDoorLocked",new Vector2(door.Item1*size.x*.25f,0),new Vector2(size.x*.5f,size.y),new Color(.02f,.035f,.045f,.68f));
            locked.GetComponent<Image>().raycastTarget=false;
            var caption=Label(locked,"Locked","未解锁",Vector2.zero,new Vector2(size.x*.5f-4,20),Mathf.Clamp(Mathf.RoundToInt(size.x*.065f),9,14),Paper);caption.raycastTarget=false;
        }
    }
    private static void PresentCompactBattlefieldFace(RectTransform parent,Sprite sprite,Vector2 size,bool tapped)
    {
        parent.GetComponent<Image>().sprite=null;
        parent.GetComponent<Image>().color=Ink;
        parent.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(136,120,7);
        parent.gameObject.AddComponent<Mask>().showMaskGraphic=true;
        var r=sprite.rect;
        float titleWidth=size.x-4;
        // Match the cropped strip's pixel aspect ratio; a fixed height stretched
        // letters and mana symbols vertically on compact battlefield cards.
        float titleHeight=titleWidth*(r.height*.052f)/(r.width*.87f);
        float footer=Mathf.Min(13,size.y*.11f);
        var art=Rect(parent,"Illustration",new Vector2(0,(footer-titleHeight)*.5f),new Vector2(size.x-8,size.y-titleHeight-footer-4)).gameObject.AddComponent<RawImage>();
        art.texture=sprite.texture;art.color=tapped?new Color(.65f,.68f,.72f):Color.white;
        // Preserve the illustration's proportions and its inset frame.
        CardArtCrop.Illustration(art,sprite);
        var title=Rect(parent,"PrintedName",new Vector2(0,(size.y-titleHeight)*.5f-1),new Vector2(titleWidth,titleHeight)).gameObject.AddComponent<RawImage>();
        title.texture=sprite.texture;title.color=art.color;
        // The name slice ends above the art: including the strip below it duplicated
        // the illustration's upper edge just beneath the battlefield nameplate.
        title.uvRect=new Rect((r.x+r.width*.065f)/sprite.texture.width,(r.y+r.height*.895f)/sprite.texture.height,r.width*.87f/sprite.texture.width,r.height*.052f/sprite.texture.height);
        art.raycastTarget=title.raycastTarget=false;
    }
}
