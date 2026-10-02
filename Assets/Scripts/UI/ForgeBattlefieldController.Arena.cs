using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private sealed class ArenaCard
    {
        public RectTransform root, face;
        public Button button;
        public ForgeBattlefieldCardPointer pointer;
        public string appearance;
        public string groupKey;
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
    private static float StackOverhang(ForgeCardView[] group)=>group[0].stackableSupport?Math.Min(group.Length-1,3)*12:0;
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
            Node(plate+"/Avatar").gameObject.SetActive(false);Node(plate+"/FactionBar").gameObject.SetActive(false);
            foreach(var text in Node(plate).GetComponentsInChildren<Text>())
                if(text.text=="LIFE")text.gameObject.SetActive(false);
            ArenaPlace(plate+"/Name",new Vector2(.5f,.5f),new Vector2(-38,18),new Vector2(146,26));
            ArenaPlace(plate+"/Life",new Vector2(.5f,.5f),new Vector2(91,7),new Vector2(70,52));
            ArenaPlace(plate+"/Sub",new Vector2(.5f,.5f),new Vector2(-34,-17),new Vector2(154,22));
            ArenaTextSize(plate+"/Name",19);SceneText(plate+"/Name").alignment=TextAnchor.MiddleLeft;
            ArenaTextSize(plate+"/Life",42);SceneText(plate+"/Life").alignment=TextAnchor.MiddleCenter;
            ArenaTextSize(plate+"/Sub",12);SceneText(plate+"/Sub").alignment=TextAnchor.MiddleLeft;
            var accent=own?new Color(.28f,.72f,.88f,.9f):new Color(.77f,.57f,.34f,.9f);
            Panel(Node(plate),"Edge",new Vector2(-131,0),new Vector2(3,60),accent);
            Panel(Node(plate),"Divider",new Vector2(49,0),new Vector2(1,56),new Color(.5f,.62f,.7f,.25f));
            Label(Node(plate),"LifeCaption","生命",new Vector2(91,-28),new Vector2(65,18),11,new Color(.62f,.69f,.74f));
            foreach(var graphic in Node(plate).GetComponentsInChildren<Graphic>())
                if(graphic.transform!=Node(plate))graphic.raycastTarget=false;
            var name=SceneText(plate+"/Name");
            var marquee=name.GetComponent<PlayerNameMarquee>();
            if(marquee==null)marquee=name.gameObject.AddComponent<PlayerNameMarquee>();
            marquee.Initialize(name);
        }
        ArenaPlace("CornerCluster/ControlRow/SkipCombatButton",new Vector2(.5f,.5f),new Vector2(0,-28),new Vector2(260,50));
        ArenaPlace("GearBox",new Vector2(0,1),new Vector2(82,-48),new Vector2(98,44));
        ArenaPlace("PriorityPill",new Vector2(1,0),new Vector2(-178,181),new Vector2(250,26));
        ArenaPlace("PriorityPill/Text",new Vector2(.5f,.5f),Vector2.zero,new Vector2(240,26));
        ArenaTextSize("PriorityPill/Text",16);SceneText("PriorityPill/Text").alignment=TextAnchor.MiddleCenter;
        SceneText("CornerCluster/ManaRow/Label").alignment=TextAnchor.MiddleCenter;
        ArenaPlace("PlayerHandRoot",new Vector2(.5f,0),new Vector2(0,0),new Vector2(100,100));
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
        else text.fontSize=size;
    }
    private void EndArenaPresentation()
    {
        foreach(var key in arenaCards.Keys.Where(k=>!retainedCards.Contains(k)).ToArray())
        {arenaCards[key].root.gameObject.SetActive(false);Destroy(arenaCards[key].root.gameObject);arenaCards.Remove(key);}
        ownHand.SetAsLastSibling();
        ForgeBattlefieldCardPointer.RestoreHandOrder(ownHand);
        Canvas.ForceUpdateCanvases();
    }
    // Visual grouping is deliberately separate from engine identity: expand before choosing a member.
    public static string ArenaGroupKey(ForgeCardView c)
    {
        if(c.faceDown)return "hidden:"+c.hasStatBaseline+":"+c.basePower+":"+c.baseToughness+":"+string.Join(",",c.grantedKeywords??Array.Empty<string>())+":"+c.id+":"+c.tapped+":"+c.power+":"+c.toughness+":"+c.faceDownKind+":"+c.currentRules+":"+c.damage+":"+c.attacking+":"+c.blocking;
        return string.Join("|",c.printingKey,c.ownerSeat,c.effectMarker,c.markerKind,c.markerStatus,c.helperArtName,c.stackableSupport,c.devotion,c.devotionRequired,c.token,c.copy,c.tokenArtKey,c.tokenSourcePrintingKey,c.name,c.zone,c.land,c.creature,c.tapped,c.attacking,c.blocking,c.hasStatBaseline,c.basePower,c.baseToughness,string.Join(",",c.grantedKeywords??Array.Empty<string>()),c.power,c.toughness,c.damage,c.defenderKind,c.defenderId,c.loyalty,c.defense,c.backFace,c.stateName,c.room,c.leftRoomUnlocked,c.rightRoomUnlocked,c.hasCurrentLandState,c.landTypesChanged,c.originalType,c.originalRules,c.currentType,c.currentRules,
            string.Join(",",c.basicLandTypes??Array.Empty<string>()),
            string.Join(",",(c.counters??Array.Empty<ForgeCardCounter>()).Select(x=>x.kind+":"+x.count)),string.Join(",",c.keywords??Array.Empty<string>()));
    }
    private void LayoutArenaRow(RectTransform parent,ForgeCardView[] cards,string row)
    {
        bool hand=row=="Hand", lands=row.Contains("Lands"), support=row.Contains("Support"), own=row.StartsWith("Your");
        bool choosingIndividual=view?.awaitingInput==true &&
            (view.request.kind.StartsWith("InputSelect") || view.request.kind=="SelectCards" || view.request.kind=="InputAttack" || view.request.kind=="InputBlock");
        var groups=new List<ForgeCardView[]>();
        foreach(var group in cards.GroupBy(c=>hand?c.id.ToString():ArenaGroupKey(c)))
        {
            string key=row+":"+group.Key;
            // The bridge excludes support permanents with per-object linked state.
            var first=group.First();catalog.TryGetValue(first.printingKey??"",out var definition);
            bool simple=first.stackableSupport || definition!=null && (first.land && first.name is "Mountain" or "Island" or "Swamp" or "Forest" or "Plains"
                || first.creature && string.IsNullOrWhiteSpace(definition.description));
            bool hasCandidate=group.Any(c=>OptionIndex("card:"+c.id)>=0 || (AttackCardCandidate(c.id)||BlockCandidate(c.id)));
            if(hand || first.attacking || first.blocking || !simple || choosingIndividual && hasCandidate || expandedGroups.Contains(key))groups.AddRange(group.Select(c=>new[]{c}));
            else groups.Add(group.ToArray());
        }
        float tableWidth=Mathf.Max(320,Mathf.Min(1180,surface.rect.width-420));
        float sideWidth=Mathf.Min(540,tableWidth*.46f), gap=28;
        float available=hand?tableWidth:support?sideWidth:tableWidth-sideWidth-gap;
        float width=hand?182:Mathf.Min(lands?112:136,(available-Math.Max(0,groups.Count-1)*16)/Math.Max(1,groups.Count));
        // Dense fields wrap rather than hiding permanents behind pagination.
        int columns=hand?Math.Max(1,groups.Count):support?(cards.Any(c=>c.room)?Math.Max(1,Mathf.FloorToInt(sideWidth/180)):Math.Max(1,Mathf.FloorToInt((sideWidth+8)/100))):Math.Max(1,Mathf.FloorToInt(available/102));
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
        if(!hand && rows>1)width=Mathf.Min(92,145f/rows-10);
        if(support)width=Mathf.Min(112,(sideWidth-8*(columns-1))/columns);
        // Battlefield tiles expose illustration and name; full rules remain in hover/hand.
        float height=width*(hand?254f/182f:.88f);
        float cardWidth=hand?width:width*.88f;
        float step=hand?Mathf.Min(112,available/Math.Max(1,cards.Length-1)):cardWidth+8;
        float y=hand?46:lands || support?(own?-255:365):(own?-85:225);
        float x=hand?0:support?tableWidth*.5f-sideWidth*.5f:-(sideWidth+gap)*.5f;
        if(lands)x+=-available*.5f+cardWidth*.5f+(Math.Min(columns,groups.Count)-1)*step*.5f;
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
            var position=new Vector2(x+offset*step,y+(hand?-Mathf.Min(25,offset*offset*2):line*(own?(support?1:-1):(support?-1:1))*(support?cardWidth*1.3f+16:Mathf.Min(height+10,145f/Math.Max(1,rows)))));
            if(support)position.x=x-sideWidth*.5f+cardWidth*.5f+supportRows[line].Take(col).Sum(g=>cardWidth+StackOverhang(g)+12)+StackOverhang(groups[i]);
            var member=groups[i][0];string groupKey=row+":"+ArenaGroupKey(member);
            if(!hand && member.attacking)position.y+=own?36:-36;
            float memberHeight=!hand && member.planeswalker?cardWidth*1.3f:height;
            float memberWidth=!hand&&member.room?Mathf.Min(sideWidth-16,cardWidth*1.7f):cardWidth;
            bool compactToken=!hand && member.token && !member.faceDown && !member.creature && !member.planeswalker && !member.battle && !member.land && !member.room
                && ((member.currentType??"").Contains("Artifact") || (member.currentType??"").Contains("Enchantment"));
            if(compactToken){memberWidth=cardWidth*.74f;memberHeight=cardWidth*.65f;}
            if(!hand)position.y+=(own?1:-1)*(memberHeight-height)*.5f;
            DrawArenaCard(parent,member,groups[i].Length,row+":"+member.id,groupKey,position,new Vector2(memberWidth,memberHeight),hand,hand?-offset*3:member.tapped?(own?-12:12):0,i);
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
        item.root.sizeDelta=size;item.face.sizeDelta=size;
        int option=OptionIndex("card:"+card.id);
        if(option<0&&view?.request?.kind=="InputPassPriority"&&HasPreparedSpell(card))option=OptionIndex("card:"+card.preparedSpell.id);
        long captured=requestId;
        bool actionable=canAct && (option>=0 || !hand && (AttackCardCandidate(card.id)||BlockCandidate(card.id))) && !submitted;
        var visibility=item.root.GetComponent<CanvasGroup>();
        if(visibility==null)visibility=item.root.gameObject.AddComponent<CanvasGroup>();
        visibility.alpha=hand && card.zone is "Graveyard" or "Exile" && !actionable?.65f:1f;
        bool paymentUncertain=option>=0 && view.request.options[option].requiresPaymentCheck;
        bool selected=option>=0 && (view.request.options[option].selected || view.request.options[option].label.StartsWith("[已选回牌库底]"));
        bool attackSelected=IsDeclaringAttack && selectedAttacker==card.id || IsBlockingChoice && selectedBlocker==card.id;
        bool playableHand=hand && actionable && !paymentUncertain && view.request.kind=="InputPassPriority";
        bool own=key.StartsWith("Your",StringComparison.Ordinal);
        string appearance=ArenaGroupKey(card)+"/"+size+"/"+count+"/"+actionable+"/"+selected+"/"+paymentUncertain+"/"+playableHand+"/"+attackSelected;
        if(item.appearance!=appearance)
        {
            bool revealed=item.appearance!=null && item.faceDown && !card.faceDown;
            RectTransform oldFace=null;
            if(revealed) {oldFace=Instantiate(item.face,item.root);oldFace.name="RevealBack";foreach(var g in oldFace.GetComponentsInChildren<Graphic>())g.raycastTarget=false;}
            item.faceDown=card.faceDown;
            item.appearance=appearance;Clear(item.face);
            Color accent=selected || attackSelected?Gold:card.attacking?new Color(1,.36f,.14f):card.blocking?Blue:actionable && !paymentUncertain?new Color(.3f,.85f,1):new Color(.38f,.38f,.36f);
            if(playableHand)
            {
                var glow=Rect(item.face,"PlayableGlow",Vector2.zero,size).gameObject.AddComponent<ForgePlayableGlow>();
                glow.raycastTarget=false;
                accent=new Color(.68f,.95f,1);
            }
            if(count>1) for(int layer=Math.Min(count-1,3);layer>0;layer--){
                var back=Panel(item.face,"StackBack",new Vector2(-layer*(card.stackableSupport?12:3),layer*3),size,new Color(.13f,.15f,.17f));
                back.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(140,125,9);
                if(card.stackableSupport && TryBattlefieldArt(card,out var stackedArt)) {
                    var inset=Panel(back,"StackArt",Vector2.zero,size-Vector2.one*5,Ink);
                    if(HasTokenFrame(card))PresentSpecialPermanentFace(inset,stackedArt,size-Vector2.one*5,card);
                    else PresentCompactBattlefieldFace(inset,stackedArt,size-Vector2.one*5,card.tapped);
                }
            }
            if(!hand) {
                var shadow=Panel(item.face,"CardShadow",new Vector2(0,-5),size+new Vector2(5,5),new Color(0,0,0,.6f));
                shadow.GetComponent<Image>().sprite=HasTokenFrame(card)?TokenArch():IntegratedUiUtility.RoundedRect(140,125,10);
            }
            var border=Panel(item.face,"Border",Vector2.zero,size,accent);
            if(!hand)border.GetComponent<Image>().sprite=card.effectMarker?EffectMarkerShape():card.emblem?EmblemShape():HasTokenFrame(card)?TokenArch():IntegratedUiUtility.RoundedRect(140,125,9);
            if(card.emblem||card.effectMarker)border.gameObject.AddComponent<Mask>().showMaskGraphic=true;
            var face=Panel(border,"Face",Vector2.zero,size-Vector2.one*5,Ink);
            catalog.TryGetValue(card.printingKey??"",out var definition);Sprite art=null;
            TryBattlefieldArt(card, out art);
            var image=face.GetComponent<Image>();image.sprite=art;image.preserveAspect=true;
            image.color=art==null?Ink:!hand && card.tapped?new Color(.65f,.68f,.72f):Color.white;
            if(!hand && art!=null && card.faceDown)PresentFaceDownFace(face,art,size-Vector2.one*5);
            if(!hand && art!=null && !card.faceDown) {
                image.preserveAspect=false;
                if(card.emblem||card.effectMarker)PresentSpecialPermanentFace(face,art,size-Vector2.one*5,card);
                else if(card.room)PresentRoomFace(face,art,size-Vector2.one*5,card);
                else if(HasTokenFrame(card) || card.planeswalker)PresentSpecialPermanentFace(face,art,size-Vector2.one*5,card);
                else PresentCompactBattlefieldFace(face,art,size-Vector2.one*5,card.tapped);
            }
            if(hand && card.zone is "Graveyard" or "Exile") {
                PresentPreparedSpellFace(face,card,new Vector2(240,335));
                var spellFace=face.Find("PreparedSpellFace");if(spellFace!=null)spellFace.localScale=Vector3.one*((size.x-5)/240);
                var zone=Panel(item.face,"PlayableZone",new Vector2(-size.x*.5f+17,size.y*.5f-17),new Vector2(27,25),card.zone=="Graveyard"?new Color(.31f,.22f,.4f):new Color(.17f,.31f,.38f));
                zone.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(27,25,5);
                Label(zone,"Label",card.zone=="Graveyard"?"墓":"放",Vector2.zero,new Vector2(25,23),14,Paper);
            }
            if(!hand && art!=null && card.tapped)
            {
                var tap=Rect(item.face,"TappedSymbol",new Vector2(0,size.y*.12f),Vector2.one*size.x*.6f).gameObject.AddComponent<ForgeTappedIndicator>();
                tap.color=new Color(.91f,.92f,.94f,.5f);tap.raycastTarget=false;
            }
            if(art==null)Label(face,"Name","缺失卡图",Vector2.zero,size-Vector2.one*8,12,Paper);
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
                    value.supportRichText=true;value.text=ModifiedStatsText(card,false);value.fontSize=Mathf.Clamp(Mathf.RoundToInt(size.x*.175f),12,24);
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
                string state=card.damage>0?"−"+card.damage:"";
                if(state.Length>0)Label(item.face,"State",state,new Vector2(0,-size.y*.5f-13),new Vector2(size.x,22),12,accent);
                PresentPermanentStatus(item.face,size,card);
            }
            if(count>1)
            {
                float countWidth=Mathf.Max(42,24+count.ToString().Length*13);
                var badge=Panel(item.face,"Count",new Vector2(-size.x*.35f+(countWidth-42)*.5f,-size.y*.43f),new Vector2(countWidth,26),Ink);
                var countLabel=Label(badge,"Value","×"+count,Vector2.zero,new Vector2(countWidth-2,24),19,Paper);
                countLabel.horizontalOverflow=HorizontalWrapMode.Overflow;
            }
            if(selected && view.request.kind=="InputLondonMulligan")Label(item.face,"Selected","回牌库底",new Vector2(0,size.y*.35f),new Vector2(size.x,26),18,Gold);
            if(hand && actionable && paymentUncertain)Label(item.face,"PaymentHint","需确认费用",new Vector2(0,size.y*.34f),new Vector2(size.x,24),14,Gold);
            foreach(var graphic in item.face.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
            item.button.targetGraphic=item.root.GetComponent<Image>();
            if(revealed)StartCoroutine(AnimateFaceUp(oldFace,border));
        }
        item.groupKey=groupKey;
        item.button.interactable=true;item.button.onClick.RemoveAllListeners();
        Action choose=()=> {
            if(!hand && view?.request?.kind=="InputBlock"){ClickBlockCard(card.id,captured);return;}
            if(!hand && view?.request?.kind=="InputAttack"){ClickAttackCard(card.id,captured);return;}
            // Identical basic lands have interchangeable mana actions. Use a member directly;
            // temporary payment/undo prompts must not leave either battlefield expanded.
            if(count>1 && !card.land) {expandedGroups.Add(groupKey);groupExpandedFrame=Time.frameCount;PresentIntegrated(connectionText.text);}
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
            case "WARD":return "守护";case "FLYING":return "飞行";case "TRAMPLE":return "践踏";case "HASTE":return "敏捷";
            case "VIGILANCE":return "警戒";case "DEATHTOUCH":return "死触";case "LIFELINK":return "系命";
            case "REACH":return "延势";case "FIRST_STRIKE":return "先攻";case "DOUBLE_STRIKE":return "连击";
            case "HEXPROOF":return "辟邪";case "INDESTRUCTIBLE":return "不灭";case "MENACE":return "威慑";
            case "STUN":return "晕眩";case "P1P1":return "+1/+1";case "M1M1":return "−1/−1";
            default:return key;
        }
    }
    private static bool HasDedicatedCounter(ForgeCardView card,string kind)
        => card.planeswalker&&string.Equals(kind,"LOYALTY",StringComparison.OrdinalIgnoreCase)
            || card.battle&&string.Equals(kind,"DEFENSE",StringComparison.OrdinalIgnoreCase);
    private static void PresentPermanentStatus(RectTransform face,Vector2 size,ForgeCardView card)
    {
        if(card.devotionRequired>0 && !card.faceDown) {
            var badge=Panel(face,"Devotion",new Vector2(-size.x*.5f+12,0),new Vector2(32,34),Ink);
            var text=Label(badge,"Value",card.devotion+"/"+card.devotionRequired,Vector2.zero,new Vector2(32,34),13,Paper);
            text.horizontalOverflow=HorizontalWrapMode.Overflow;text.raycastTarget=false;
        }
        var counters=(card.counters??Array.Empty<ForgeCardCounter>()).Where(c=>c.count>0&&!HasDedicatedCounter(card,c.kind)).ToArray();
        for(int i=0;i<Mathf.Min(3,counters.Length);i++) {
            var c=counters[i];bool stun=c.kind=="STUN";
            var badge=Panel(face,"Counter_"+c.kind,new Vector2(-size.x*.5f+10,size.y*.25f-i*23),new Vector2(28,22),stun?new Color(.12f,.39f,.51f):new Color(.55f,.39f,.12f));
            badge.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(28,22,6);badge.GetComponent<Image>().raycastTarget=false;
            var count=Label(badge,"Count",c.count.ToString(),Vector2.zero,new Vector2(27,21),16,Paper);count.fontStyle=FontStyle.Bold;count.raycastTarget=false;
        }
        var keywords=(card.keywords??Array.Empty<string>()).Where(k=>StatusName(k)!=k).Distinct().ToArray();
        int keywordSlots=Mathf.Clamp(Mathf.FloorToInt((size.x*.6f-4)/34),1,2);
        for(int i=0;i<Mathf.Min(keywordSlots,keywords.Length);i++) {
            var badge=Panel(face,"Keyword_"+keywords[i],new Vector2(-size.x*.5f+19+i*34,-size.y*.5f+10),new Vector2(32,17),new Color(.08f,.12f,.15f,.95f));
            badge.GetComponent<Image>().raycastTarget=false;
            bool granted=(card.grantedKeywords??Array.Empty<string>()).Contains(keywords[i]);
            var title=Label(badge,"Label",StatusName(keywords[i]),Vector2.zero,new Vector2(32,17),11,granted?GrantedKeywordColor:Paper);
            title.horizontalOverflow=HorizontalWrapMode.Overflow;title.raycastTarget=false;
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
        float w=r.width*.86f,h=r.height*.38f;
        float aspect=art.rectTransform.rect.width/art.rectTransform.rect.height;
        if(w/h>aspect)w=h*aspect;else h=w/aspect;
        art.uvRect=new Rect((r.x+r.width*.5f-w*.5f)/sprite.texture.width,(r.y+r.height*.62f-h*.5f)/sprite.texture.height,w/sprite.texture.width,h/sprite.texture.height);
        var title=Rect(parent,"PrintedName",new Vector2(0,(size.y-titleHeight)*.5f-1),new Vector2(titleWidth,titleHeight)).gameObject.AddComponent<RawImage>();
        title.texture=sprite.texture;title.color=art.color;
        // The name slice ends above the art: including the strip below it duplicated
        // the illustration's upper edge just beneath the battlefield nameplate.
        title.uvRect=new Rect((r.x+r.width*.065f)/sprite.texture.width,(r.y+r.height*.895f)/sprite.texture.height,r.width*.87f/sprite.texture.width,r.height*.052f/sprite.texture.height);
        art.raycastTarget=title.raycastTarget=false;
    }
}
