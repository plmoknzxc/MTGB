using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform fourCombatRoot, fourCombatDialog;
    private ScrollRect combatSourcesScroll, combatTargetsScroll;
    private readonly List<int> combatSources = new();
    private ForgeSeatView combatRenderedView;
    private long combatRequestId;
    private int combatRevision, combatGeneration;
    private string combatContext;
    // Zero is the original pile. A split gets its own identity until this declaration ends.
    private readonly Dictionary<int,int> combatPileIds = new();
    private int combatNextPile, combatFocus;
    private (string kind,int id)? combatFocusLane;
    private (int id,int pile,(string kind,int id)? destination,int nextFocus,(string kind,int id)? focusLane)? combatPileChange;
    private bool combatRenderedSubmitted;
    private bool combatShowUnavailable;
    private bool FourCombatDecision => IsFourPlayerScene && view?.awaitingInput == true
        && view.request?.seat == seat && view.request.kind is ("InputAttack" or "InputBlock");

    private void PresentFourCombat(bool refresh = false)
    {
        if (FourCombatDecision) {
            advanceButton.gameObject.SetActive(false);
            endTurnButton.gameObject.SetActive(false);
        }
        if (!FourCombatDecision || !canAct || service?.IsSpectator == true) {
            if (fourCombatRoot != null) fourCombatRoot.gameObject.SetActive(false);
            combatSources.Clear();
            combatRenderedView = null;
            if(view?.inputPending!=true)combatContext = null;
            return;
        }
        const float inset=28, gap=14, chipWidth=176, sourceWidth=180;
        float extra = Mathf.Max(0, GamePreferences.TextScale-1);
        float nameHeight = Mathf.Ceil(34+24*extra), statHeight = Mathf.Ceil(28+18*extra);
        float headHeight=100+nameHeight,dockHeight=Mathf.Ceil(68+28*extra);
        float lowerHeight=76+nameHeight+statHeight;
        if (fourCombatRoot == null) {
            fourCombatRoot = Panel(surface,"FourCombatChoice",Vector2.zero,Vector2.zero,new Color(.025f,.025f,.05f,.57f));
            fourCombatRoot.anchorMin=Vector2.zero; fourCombatRoot.anchorMax=Vector2.one;
            fourCombatRoot.offsetMin=fourCombatRoot.offsetMax=Vector2.zero;
            SetOverlayLayer(fourCombatRoot,DecisionLayerOffset,true);
            fourCombatDialog=Panel(fourCombatRoot,"Dialog",Vector2.zero,Vector2.one,HudInk);
            fourCombatDialog.gameObject.AddComponent<CanvasGroup>();
        }
        fourCombatRoot.gameObject.SetActive(true);
        void Fit() => fourCombatDialog.localScale = Vector3.one * Mathf.Clamp01(Mathf.Min(
            (surface.rect.width-36)/fourCombatDialog.rect.width, (surface.rect.height-28)/fourCombatDialog.rect.height));
        fourCombatDialog.GetComponent<CanvasGroup>().interactable=!submitted;
        if (!refresh && combatRenderedView==view && combatRequestId==requestId && combatRevision==GamePreferences.Revision && combatRenderedSubmitted==submitted) { Fit(); return; }

        var request=view.request;
        bool attack=request.kind=="InputAttack";
        int actor=request.playerSeat>0?request.playerSeat:seat;
        string context=$"{service?.MatchId}/{seat}/{actor}/{view.turnSeat}/{view.turn}/{view.restartRevision}/{request.kind}";
        bool continuing=context==combatContext;
        float poolOffset=continuing && combatSourcesScroll!=null ? -combatSourcesScroll.content.anchoredPosition.x : 0;
        float targetsOffset=continuing && combatTargetsScroll!=null ? -combatTargetsScroll.content.anchoredPosition.x : 0;
        if (!continuing) {
            combatPileIds.Clear();combatNextPile=combatFocus=0;combatFocusLane=null;combatPileChange=null;
            combatShowUnavailable=false;
        }
        bool freshSnapshot=combatRenderedView!=view || combatRequestId!=requestId;
        if (!continuing || combatRequestId!=requestId) combatSources.Clear();
        combatRenderedView=view; combatContext=context; combatRequestId=requestId;
        combatRevision=GamePreferences.Revision; combatRenderedSubmitted=submitted;
        long expected=requestId;
        int generation=++combatGeneration;
        bool Live()=>generation==combatGeneration && FourCombatDecision && canAct && !submitted &&
            requestId==expected && !viewingBattlefield && !GameSettingsPanel.IsOpen;

        var players=view.players??Array.Empty<ForgePlayerView>();
        var field=players.SelectMany(p=>(p.battlefield??Array.Empty<ForgeCardView>()).Where(c=>c!=null).Select(c=>(card:c,p.seat))).ToDictionary(x=>x.card.id);
        var options=request.options??Array.Empty<ForgeOption>();
        var rules=request.combatDeclaration;
        string prefix=attack?"attack:":"block:", removePrefix=attack?"attack-remove:":"block-remove:";
        var pairs=options.Where(o=>o?.id!=null && !o.disabled && o.id.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
        int Source(ForgeOption o)=>attack?o.attackerId:o.defenderId;
        (string kind,int id) Destination(ForgeOption o)=>attack?(o.defenderKind,o.defenderId):("card",o.attackerId);
        var matches=pairs.ToLookup(Destination);
        var removals=options.Where(o=>o?.id!=null && !o.disabled && o.id.StartsWith(removePrefix,StringComparison.Ordinal))
            .Select(o=>int.TryParse(o.id.Substring(removePrefix.Length),out int id)?id:0).Where(field.ContainsKey).ToHashSet();
        var sources=pairs.Select(Source).Concat(removals).Where(field.ContainsKey).Distinct().ToList();
        combatSources.RemoveAll(id=>!sources.Contains(id));
        var assigned=new Dictionary<(string kind,int id),List<int>>();
        void Remember((string kind,int id) key,int id) {
            if(!assigned.TryGetValue(key,out var list))assigned[key]=list=new List<int>();
            if(!list.Contains(id))list.Add(id);
        }
        foreach(int id in sources) {
            var c=field[id].card;
            if(attack && c.attacking)Remember((c.defenderKind,c.defenderId),id);
            if(!attack)foreach(int attacker in c.blockedAttackerIds??Array.Empty<int>())Remember(("card",attacker),id);
        }
        if(!attack)foreach(var o in pairs.Where(o=>o.selected))Remember(Destination(o),Source(o));
        (int seat,string name) DefendedBy(ForgeCardView attacker) {
            if(attacker.defenderKind=="player") {
                var defender=players.FirstOrDefault(p=>p.targetId==attacker.defenderId);
                return defender!=null?(defender.seat,PlayerDisplayName(defender.seat)):(0,null);
            }
            if(attacker.defenderKind=="card" && field.TryGetValue(attacker.defenderId,out var defenderCard)) {
                var c=defenderCard.card;
                return (c.battle?c.protectingPlayerSeat:defenderCard.seat,CardName(c));
            }
            return (0,null);
        }
        // A blocker running out of capacity removes native options, not incoming attackers.
        var defended=attack?new Dictionary<int,(int seat,string name)>():field.Values.Where(v=>v.card.attacking)
            .ToDictionary(v=>v.card.id,v=>DefendedBy(v.card));
        var incoming=defended.Where(pair=>pair.Value.seat==actor).Select(pair=>(kind:"card",id:pair.Key));
        var allDefenders=(rules?.destinations??Array.Empty<ForgeCombatDestination>()).Select(d=>(kind:d.kind,id:d.id));
        var targets=matches.Select(g=>g.Key).Concat(assigned.Keys).Concat(incoming).Concat(allDefenders).Distinct().Select(key=>{
            var p=key.kind=="player"?players.FirstOrDefault(a=>a.targetId==key.id):null;
            field.TryGetValue(key.id,out var tile);
            var card=key.kind=="card"?tile.card:null;
            int owner=p?.seat??(attack && card?.battle==true?card.protectingPlayerSeat:tile.seat);
            return (key,owner,player:p,card);
        }).Where(g=>g.player!=null || g.card!=null).OrderBy(g=>g.owner>0?(g.owner-actor+4)%4:4)
            .ThenBy(g=>g.player!=null?0:1).ThenBy(g=>g.key.id).ToArray();
        var placed=assigned.Values.SelectMany(ids=>ids).ToHashSet();
        var free=sources.Where(id=>!placed.Contains(id)).ToArray();
        if(freshSnapshot && combatPileChange is {} change) {
            bool accepted=change.destination.HasValue
                ? assigned.TryGetValue(change.destination.Value,out var atTarget) && atTarget.Contains(change.id)
                : field.ContainsKey(change.id) && !placed.Contains(change.id);
            if(accepted) {
                combatPileIds[change.id]=change.pile;
                combatFocus=change.nextFocus;combatFocusLane=change.focusLane;
            }
            combatPileChange=null;
        }
        string Identity(int id) {
            var c=field[id].card;
            return c.faceDown || string.IsNullOrEmpty(c.name)?"#"+id:c.name;
        }
        int Pile(int id)=>combatPileIds.TryGetValue(id,out var pile)?pile:0;
        var duplicateNames=sources.GroupBy(Identity).Where(g=>g.Count()>1).SelectMany(g=>g).ToHashSet();
        int[][] Group(IEnumerable<int> ids)=>ids.OrderBy(id=>id).GroupBy(id=>(Identity(id),Pile(id))).Select(g=>g.ToArray()).ToArray();
        var freeGroups=Group(free);
        var unavailable=Group(field.Values.Where(v=>v.seat==actor && v.card.creature && !sources.Contains(v.card.id)).Select(v=>v.card.id));
        int shownGroups=freeGroups.Length+(combatShowUnavailable?unavailable.Length:0);
        var groupsAt=assigned.ToDictionary(kv=>kv.Key,kv=>Group(kv.Value));
        int GroupCount((string kind,int id) key)=>groupsAt.TryGetValue(key,out var groups)?groups.Length:0;
        float editHeight=combatFocusLane.HasValue && duplicateNames.Contains(combatFocus) && assigned.TryGetValue(combatFocusLane.Value,out var focusedIds)
            && focusedIds.Contains(combatFocus)?36:0;
        float upperHeight=32+headHeight+8+dockHeight+editHeight+6;
        bool singleReserve=shownGroups==1 && freeGroups.Length==1;
        float poolHeading=102+upperHeight+12,poolTop=poolHeading+(singleReserve?10:36);
        if(singleReserve)lowerHeight=Mathf.Ceil(94+22*extra);
        float requestedWidth = Mathf.Max(shownGroups*(sourceWidth+gap)-gap,
            targets.Sum(g=>Mathf.Max(210,GroupCount(g.key)*(chipWidth+4)+4)+gap)-gap);
        float width=Mathf.Clamp(requestedWidth+2*inset,660,1176), stripWidth=width-2*inset;
        if(shownGroups==0)lowerHeight=Mathf.Ceil(76+28*extra);
        assigned.TryGetValue(("card",rules?.cardId??0),out var issueBlockers);
        string issue=CombatDeclarationHint(rules,field.TryGetValue(rules?.cardId??0,out var subject)?CardName(subject.card):null,issueBlockers?.Count??0);
        float noticeHeight=string.IsNullOrEmpty(issue)?0:Mathf.Ceil(46+20*extra);
        float height=poolTop+lowerHeight+82+noticeHeight;
        fourCombatDialog.sizeDelta=new Vector2(width,height);
        Fit();
        bool multi=attack && submitOrder!=null;
        string T(string zh,string en,string ja)=>TapPaymentText(zh,en,ja);
        string CardName(ForgeCardView c)=>CombatCardName(c);
        string Stats(ForgeCardView c)=>c.power+" / "+c.toughness;
        void Send(string id,int[] group=null){if(Live())ChooseCore(OptionIndex(id),expected,group);}
        void Draft(int[] ids,(string kind,int id)? lane) {
            if(!Live())return;
            bool close=combatFocusLane==lane && ids.Contains(combatFocus);
            combatFocus=close?0:ids[0];combatFocusLane=lane;
            combatSources.Clear();
            if(!close)combatSources.AddRange(ids);
            PresentFourCombat(true);
        }
        Clear(fourCombatDialog);
        StyleHudSurface(fourCombatDialog,HudInk,HudEdge);
        var header=Panel(fourCombatDialog,"Header",Vector2.zero,Vector2.one,HudRaised);
        CombatPlace(header,2,2,width-4,58);
        if(HudTheme!=null)DuelSurface(header,BattlefieldHudGraphic.Surface.JournalHeader,HudAccent);
        CombatText(header,"Title",attack?T("宣告攻击","Declare attackers","攻撃クリーチャー指定"):T("宣告阻挡","Declare blockers","ブロック・クリーチャー指定"),inset,0,width-80,58,28,HudText,true);
        if(unavailable.Length>0) {
            string unavailableLabel=attack?T("不能攻击","Cannot attack","攻撃不可"):T("不能阻挡","Cannot block","ブロック不可");
            CombatButton(header,"Unavailable",unavailableLabel+"  "+unavailable.Sum(group=>group.Length)+(combatShowUnavailable?"  ▴":"  ▾"),
                width-200,8,168,combatShowUnavailable,()=>{
                    if(!Live())return;
                    combatShowUnavailable=!combatShowUnavailable;
                    PresentFourCombat(true);
                    float travel=combatSourcesScroll.content.rect.width-combatSourcesScroll.viewport.rect.width;
                    if(combatShowUnavailable && travel>0)
                        combatSourcesScroll.horizontalNormalizedPosition=Mathf.Clamp01(freeGroups.Length*(sourceWidth+gap)/travel);
                });
        }
        var recallArea=Panel(fourCombatDialog,"ReturnToReserve",Vector2.zero,Vector2.one,Color.clear);
        CombatPlace(recallArea,inset,poolHeading,stripWidth,poolTop+lowerHeight-poolHeading);
        string seatCap=rules?.seatLimit>0?(attack?T($"   ·   至多 {rules.seatLimit} 只攻击",$"   ·   max {rules.seatLimit} attackers",$"   ·   攻撃は最大{rules.seatLimit}体")
            :T($"   ·   至多 {rules.seatLimit} 只阻挡",$"   ·   max {rules.seatLimit} blockers",$"   ·   ブロックは最大{rules.seatLimit}体")):"";
        var targetHeading=CombatText(fourCombatDialog,"TargetHeading",(attack?T("攻击对象","Attack destinations","攻撃先"):T("来袭生物","Incoming attackers","攻撃クリーチャー"))+"   "+targets.Length+seatCap,inset,64,stripWidth-100,36,18,HudText);
        float rosterLabel=singleReserve?Mathf.Min(172,stripWidth*.24f):0;
        var sourceHeading=CombatText(fourCombatDialog,"SourceHeading",T("待命生物","Available creatures","待機中")+"   "+free.Length,inset,
            singleReserve?poolTop+(lowerHeight-36)*.5f:poolHeading,singleReserve?rosterLabel:stripWidth-100,36,18,HudText);
        CombatRule(fourCombatDialog,"RosterDivider",inset,poolHeading-4,stripWidth,1,HudRule);
        float colWidth=Mathf.Clamp((stripWidth-(targets.Length-1)*gap)/Mathf.Max(1,targets.Length),168,226);
        float Column((string kind,int id) key)=>Mathf.Max(colWidth,GroupCount(key)*(chipWidth+4)+4);
        float targetWidth=targets.Sum(g=>Column(g.key))+Mathf.Max(0,targets.Length-1)*gap;
        var destStrip=CombatStrip("Destinations",inset,102,stripWidth,upperHeight,targetWidth,targetsOffset,out combatTargetsScroll);
        var pool=CombatStrip("Sources",inset+rosterLabel,poolTop,stripWidth-rosterLabel,lowerHeight,shownGroups*(sourceWidth+gap)-gap,poolOffset,out combatSourcesScroll);
        var poolFace=recallArea.GetComponent<Image>();
        Color restingPool=Color.clear;combatSourcesScroll.viewport.GetComponent<Image>().color=restingPool;
        var draft=combatSources.ToArray();
        CombatScrollButtons(targetHeading.rectTransform,combatTargetsScroll,colWidth+gap);
        CombatScrollButtons(sourceHeading.rectTransform,combatSourcesScroll,sourceWidth+gap);

        var wells=new List<(RectTransform root,Image well,RectTransform blocked,Func<int[],bool> accepts,Action<int[]> assign)>();
        Color restingWell=HudInk;
        void Attach(RectTransform tile,int[] ids,(string kind,int id)? from) {
            var drag=tile.gameObject.AddComponent<FourCombatCardDrag>();
            drag.board=fourCombatDialog;drag.destinations=combatTargetsScroll;drag.live=Live;
            int[] members=null;
            bool OverPool(Transform hit)=>hit!=null && (hit==recallArea || hit.IsChildOf(combatSourcesScroll.viewport));
            bool RecallPair(int id)=>!attack && from.HasValue && matches[from.Value].Any(o=>Source(o)==id && o.selected);
            bool CanRecall()=>from.HasValue && (ids.All(RecallPair) ||
                ids.All(id=>removals.Contains(id) && (attack || assigned.Values.Count(group=>group.Contains(id))==1)));
            void Paint(Transform hit,bool active) {
                foreach(var w in wells){
                    bool eligible=active?w.accepts(members):draft.Length>0 && w.accepts(draft);
                    bool over=active && hit!=null && hit.IsChildOf(w.root);
                    w.well.color=Color.Lerp(restingWell,HudAccent,eligible?(over?.32f:.14f):0);
                    w.blocked.gameObject.SetActive(over && !eligible);
                    if(over && !eligible)w.blocked.SetAsLastSibling();
                }
                poolFace.color=active && CanRecall()?Color.Lerp(HudInk,HudAccent,OverPool(hit)?.30f:.10f):restingPool;
            }
            drag.begin=()=>{members=!from.HasValue && multi && ids.All(combatSources.Contains)?combatSources.ToArray():ids;Paint(null,true);};
            drag.hover=hit=>Paint(hit,true);
            drag.finish=()=>{if(generation==combatGeneration && poolFace!=null)Paint(null,false);};
            drag.drop=hit=>{
                if(!Live() || members==null)return;
                if(OverPool(hit) && CanRecall()) {
                    int id=ids[0];
                    string pick=ids.All(RecallPair)?matches[from.Value].First(o=>Source(o)==id && o.selected).id:removePrefix+id;
                    Send(pick,ids.Length>1?ids:null);
                    return;
                }
                foreach(var w in wells)
                    if(hit.IsChildOf(w.root) && w.accepts(members)){w.assign(members);return;}
            };
        }
        void PlacePile(RectTransform parent,int[] ids,(string kind,int id)? lane,float left,float top,float tileWidth,float baseHeight) {
            int id=ids[0];var c=field[id].card;
            bool focused=ids.Contains(combatFocus) && lane==combatFocusLane;
            bool editing=focused && duplicateNames.Contains(id);
            bool compact=lane.HasValue;
            string stats=ids.All(n=>field[n].card.power==c.power && field[n].card.toughness==c.toughness)?Stats(c):T("攻防各异","Mixed stats","個別のP/T");
            var tile=CombatSourceTile(parent,(compact?"Assigned_":"Source_")+id,left,top,tileWidth,baseHeight,c,CardName(c),stats,compact,
                focused || ids.All(combatSources.Contains),FourSeatColor(actor),()=>Draft(ids,lane));
            Attach(tile,ids,lane);
            if(!duplicateNames.Contains(id))return;
            if(!editing && ids.Length==1)return;
            float barWidth=editing?108:40,barY=compact?(editing?baseHeight+1:3):5;
            var bar=Panel(tile,"Quantity",Vector2.zero,Vector2.one,Color.Lerp(HudInk,Color.black,.55f));
            CombatPlace(bar,compact && !editing?4:(tileWidth-barWidth)*(compact?.5f:1)-(compact?0:5),barY,barWidth,editing?32:22);
            CombatRule(bar,"Rim",0,0,barWidth,1,HudEdge);
            bar.GetComponent<Image>().raycastTarget=false;
            var count=CombatText(bar,"Count","×"+ids.Length,editing?31:0,0,editing?46:barWidth,editing?32:22,editing?17:15,HudText);
            count.alignment=TextAnchor.MiddleCenter;
            if(!editing)return;
            int donor=free.Where(n=>Identity(n)==Identity(id) && (compact || Pile(n)!=Pile(id)) &&
                (!compact || matches[lane.Value].Any(o=>Source(o)==n && !o.selected)))
                .OrderBy(n=>Pile(n)==0?1:0).ThenBy(n=>n).FirstOrDefault();
            int release=ids[ids.Length-1];
            void Adjust(bool add) {
                if(!Live())return;
                if(add) {
                    if(donor==0)return;
                    if(compact) {
                        combatPileChange=(donor,Pile(id),lane,combatFocus,lane);
                        Send(matches[lane.Value].First(o=>Source(o)==donor && !o.selected).id);return;
                    }
                    combatPileIds[donor]=Pile(id);
                } else {
                    if(compact && !removals.Contains(release) || !compact && ids.Length<2)return;
                    int split=--combatNextPile;
                    if(compact) {
                        combatPileChange=(release,split,null,ids[0],ids.Length>1?lane:null);Send(removePrefix+release);return;
                    }
                    combatPileIds[release]=split;combatFocus=ids[0];
                }
                combatSources.Clear();
                combatSources.AddRange(free.Where(n=>Identity(n)==Identity(combatFocus) && Pile(n)==Pile(combatFocus)).OrderBy(n=>n));
                PresentFourCombat(true);
            }
            void Step(string name,string label,float at,bool allowed,bool add) {
                var key=Panel(bar,name,Vector2.zero,Vector2.one,Color.white);CombatPlace(key,at,1,31,31);
                var b=key.gameObject.AddComponent<Button>();b.targetGraphic=key.GetComponent<Image>();
                var colors=b.colors;colors.normalColor=HudRaised;colors.highlightedColor=HudSelected;colors.pressedColor=HudAccent;
                colors.disabledColor=Color.Lerp(HudInk,Color.black,.3f);b.colors=colors;
                var glyph=CombatText(key,"Label",label,0,0,31,30,20,allowed?HudText:Color.Lerp(HudInk,HudMuted,.5f));
                glyph.alignment=TextAnchor.MiddleCenter;b.onClick.AddListener(()=>Adjust(add));b.interactable=allowed;
            }
            Step("Minus","−",0,compact?removals.Contains(release):ids.Length>1,false);
            Step("Plus","+",77,donor!=0,true);
        }
        var repeatedTargets=targets.Where(g=>g.card!=null).GroupBy(g=>(g.owner, label:CardName(g.card)))
            .Where(group=>group.Count()>1).SelectMany(group=>group.OrderBy(g=>g.key.id).Select((g,i)=>(g.key,label:$"{i+1}/{group.Count()}")))
            .ToDictionary(x=>x.key,x=>x.label);
        float x=Mathf.Max(0,(stripWidth-targetWidth)*.5f);
        foreach(var g in targets) {
            float col=Column(g.key);
            string kind=g.key.kind+"_"+g.key.id;
            var lane=Panel(destStrip,"Target_"+kind,Vector2.zero,Vector2.one,Color.clear);
            CombatPlace(lane,x,0,col,upperHeight-3);x+=col+gap;
            
            string title=g.player!=null?PlayerDisplayName(g.owner):CardName(g.card);
            string detail=g.player!=null?T("生命 ","Life ","ライフ ")+g.player.life:g.card.battle?T("布防 ","Defense ","守備 ")+g.card.defense:
                g.card.planeswalker?T("忠诚 ","Loyalty ","忠誠度 ")+g.card.loyalty:Stats(g.card);
            var face=Panel(lane,"Face",Vector2.zero,Vector2.one,HudRaised);CombatPlace(face,(col-colWidth)*.5f,32,colWidth,headHeight);
            // Keep the destination visible while scrolling along a wide row of assigned creatures.
            if(col>colWidth+1) {
                var scroller=combatTargetsScroll;
                float laneLeft=lane.anchoredPosition.x;
                void KeepHeader(Vector2 _) {
                    float first=Mathf.Clamp(-scroller.content.anchoredPosition.x-laneLeft,0,col);
                    float last=Mathf.Clamp(-scroller.content.anchoredPosition.x+stripWidth-laneLeft,0,col);
                    float left=Mathf.Clamp((first+last-colWidth)*.5f,0,col-colWidth);
                    face.anchoredPosition=new Vector2(left,face.anchoredPosition.y);
                    
                }
                scroller.onValueChanged.AddListener(KeepHeader);KeepHeader(Vector2.zero);
            }
            if(g.card!=null) {
                CombatArt(face,g.card,0,1,1,colWidth-2,headHeight-2);
                CombatShade(face,colWidth,headHeight);
                var stat=CombatText(face,"Detail",detail,0,0,colWidth,nameHeight,16,HudText);
                stat.horizontalOverflow=HorizontalWrapMode.Overflow;
                float statWidth=Mathf.Clamp(Mathf.Ceil(stat.preferredWidth)+6,44,colWidth*.48f);
                CombatPlace(stat.rectTransform,colWidth-statWidth-7,headHeight-nameHeight-3,statWidth,nameHeight);
                stat.alignment=TextAnchor.MiddleRight;FitCombatLabel(stat);
                var caption=CombatText(face,"Name",title,9,headHeight-nameHeight-4,colWidth-statWidth-16,nameHeight,20,HudText,true);
                FitCombatLabel(caption);
                bool numbered=repeatedTargets.TryGetValue(g.key,out var ordinal);
                if(numbered) {
                    var index=Panel(face,"Ordinal",Vector2.zero,Vector2.one,Color.Lerp(HudInk,Color.black,.65f));
                    CombatPlace(index,5,5,40,statHeight);
                    var seq=CombatText(index,"Text",ordinal,0,0,40,statHeight,14,HudText);seq.alignment=TextAnchor.MiddleCenter;
                }
                if(!attack && defended.TryGetValue(g.key.id,out var victim) && !string.IsNullOrEmpty(victim.name)) {
                    float left=numbered?47:5;
                    var text=CombatText(face,"DefenseTarget","→ "+victim.name,left+5,5,colWidth-left-14,statHeight,14,HudText);
                    FitCombatLabel(text);
                    var backing=CombatRule(face,"DefenseTargetInk",left,5,Mathf.Min(colWidth-left-4,text.preferredWidth+10),statHeight,Color.Lerp(HudInk,Color.black,.65f));
                    backing.SetSiblingIndex(text.transform.GetSiblingIndex());
                }
            } else {
                float portrait=Mathf.Min(88,headHeight-16);
                CombatArt(face,null,g.player.seat,8,(headHeight-portrait)/2,portrait,portrait);
                float left=portrait+16;
                var caption=CombatText(face,"Name",title,left,headHeight/2-nameHeight,colWidth-left-5,nameHeight,21,HudText,true);
                FitCombatLabel(caption);
                CombatText(face,"Detail",detail,left,headHeight/2,colWidth-left-5,statHeight,16,HudMuted);
            }
            CombatRule(face,"LowerEdge",0,headHeight-1,colWidth,1,HudEdge);
            var well=Panel(lane,"DropWell",Vector2.zero,Vector2.one,restingWell);
            CombatPlace(well,0,32+headHeight+8,col,dockHeight+editHeight);
            var offers=matches[g.key].ToDictionary(Source);
            assigned.TryGetValue(g.key,out var already);
            var limit=attack?null:rules?.limits?.FirstOrDefault(l=>l.id==g.key.id);
            if(limit!=null && !limit.unblockable) {
                string bounds=limit.min>1 && limit.max>=limit.min ? T($"阻挡 {limit.min}–{limit.max} 只",$"{limit.min}–{limit.max} blockers",$"ブロック {limit.min}～{limit.max}体")
                    :limit.min>1?T($"阻挡至少 {limit.min} 只",$"Block with {limit.min}+",$"ブロック {limit.min}体以上")
                    :limit.max>=0?T($"阻挡至多 {limit.max} 只",$"Max {limit.max} blocker(s)",$"ブロック {limit.max}体まで"):null;
                string required=limit.requiredIfAble switch {
                    "two"=>T("若能，须用至少 2 只阻挡","Must block with 2+ if able","可能なら2体以上でブロック"),
                    "exactlyOne"=>T("若能，须用恰好 1 只阻挡","Must block with exactly 1 if able","可能ならちょうど1体でブロック"),
                    "all"=>T("所有能阻挡的生物须阻挡","All able creatures must block","可能な全クリーチャーでブロック"),
                    "one"=>T("若能，必须阻挡","Must be blocked if able","可能ならブロックが必要"),
                    _=>null
                };
                string caption=string.Join("\n",new[]{bounds,required}.Where(s=>!string.IsNullOrEmpty(s)));
                float lines=bounds!=null && required!=null?2:1;
                var badge=CombatRule(face,"BlockRange",4,statHeight+8,colWidth-8,statHeight*lines,Color.Lerp(HudInk,Color.black,.65f));
                FitCombatLabel(CombatText(badge,"Text",caption,5,0,colWidth-18,statHeight*lines,15,HudText));
            }
            bool Accept(int[] ids) {
                if(ids.Length==0 || ids.Length>1 && submitOrder==null || !ids.All(offers.ContainsKey))return false;
                int added=ids.Count(n=>already?.Contains(n)!=true);
                if(attack || added==0)return added>0;
                var limits=offers[ids[0]];
                return (limits.blockTargetSlots<=0 || added<=limits.blockTargetSlots) &&
                    (limits.blockSeatSlots<=0 || ids.Count(n=>!removals.Contains(n))<=limits.blockSeatSlots);
            }
            void Assign(int[] ids){
                if(!Accept(ids))return;
                var pending=ids.Where(n=>already?.Contains(n)!=true).ToArray();
                Send(offers[pending[0]].id,pending.Length>1?pending:null);
            }
            var wellFace=well.GetComponent<Image>();
            if(draft.Length>0 && Accept(draft))wellFace.color=Color.Lerp(restingWell,HudAccent,.14f);
            var blocked=CombatRule(well,"InvalidDrop",2,well.rect.height*.5f-18,col-4,36,HudInk);
            var blockedLabel=CombatText(blocked,"Reason",T("此处无法指派","Cannot assign here","ここには指定できません"),4,0,col-12,36,16,HudText);
            blockedLabel.alignment=TextAnchor.MiddleCenter;FitCombatLabel(blockedLabel);
            blocked.gameObject.SetActive(false);
            wells.Add((lane,wellFace,blocked,Accept,Assign));
            var targetButton=lane.gameObject.AddComponent<Button>();targetButton.transition=Selectable.Transition.None;
            targetButton.onClick.AddListener(()=>{if(Live() && combatSources.Count>0)Assign(combatSources.ToArray());});
            if(already==null || already.Count==0) {
                if(offers.Count==0 || draft.Length>0 && !Accept(draft)) {
                    string reason=limit?.unblockable==true?T("不能被阻挡","Unblockable","ブロックされない")
                        :attack && offers.Count==0?T("无法攻击此对象","Cannot attack here","この攻撃先は選べません")
                        :draft.Length>0?T("所选生物无法指派","Selection cannot be assigned","この組は指定不可")
                        :T("暂无可指派","None available","割り当て不可");
                    var caption=CombatText(well,"NoAssignment",reason,4,0,col-8,well.rect.height,15,HudMuted);
                    caption.alignment=TextAnchor.MiddleCenter;FitCombatLabel(caption);
                    continue;
                }
                float middle=well.rect.height*.5f;
                CombatRule(well,"LeftTick",10,middle-6,1,12,HudRule);
                CombatRule(well,"RightTick",col-11,middle-6,1,12,HudRule);
                CombatRule(well,"MarkH",col*.5f-7,middle,15,1,HudMuted);
                CombatRule(well,"MarkV",col*.5f,middle-7,1,15,HudMuted);
            } else {
                CombatRule(lane,"AssignmentLink",col*.5f-1,32+headHeight,2,8,HudAccent);
                var groups=groupsAt[g.key];
                for(int i=0;i<groups.Length;i++) {
                    float left=(col-groups.Length*(chipWidth+4)+4)*.5f+i*(chipWidth+4);
                    PlacePile(well,groups[i],g.key,left,4,chipWidth,dockHeight-8);
                }
            }
        }
        float bannerLeft=Mathf.Max(0,(stripWidth-targetWidth)*.5f);
        foreach(var ownerGroup in targets.GroupBy(g=>g.owner)) {
            float bandWidth=ownerGroup.Sum(g=>Column(g.key)+gap)-gap;
            Color accent=ownerGroup.Key>0?FourSeatColor(ownerGroup.Key):HudAccent;
            string ownerName=ownerGroup.Key>0?PlayerDisplayName(ownerGroup.Key):T("战役","Battle","バトル");
            var banner=Rect(destStrip,"Owner_"+ownerGroup.Key,Vector2.zero,Vector2.one);
            CombatPlace(banner,bannerLeft,1,bandWidth,28);
            CombatRule(banner,"SeatLine",0,25,bandWidth,1,HudRule);
            var ink=CombatText(banner,"Seat",ownerName,9,0,Mathf.Min(bandWidth-12,230),24,15,Color.Lerp(HudText,accent,.48f));
            var mark=CombatRule(banner,"SeatMark",1,7,3,12,accent);
            float initial=bannerLeft;
            var scroller=combatTargetsScroll;
            void PinOwner(Vector2 _) {
                float left=Mathf.Clamp(-scroller.content.anchoredPosition.x-initial,0,Mathf.Max(0,bandWidth-ink.rectTransform.rect.width-10));
                ink.rectTransform.anchoredPosition=new Vector2(left+9,0);mark.anchoredPosition=new Vector2(left+1,-7);
            }
            scroller.onValueChanged.AddListener(PinOwner);PinOwner(Vector2.zero);
            bannerLeft+=bandWidth+gap;
        }
        if(targets.Length==0)CombatText(destStrip,"Empty",T("没有合法对象","No legal targets","適正な対象なし"),12,50,stripWidth-24,48,18,HudMuted);
        float poolLeft=0;
        for(int i=0;i<freeGroups.Length;i++)
            PlacePile(pool,freeGroups[i],null,poolLeft+i*(sourceWidth+gap),2,sourceWidth,lowerHeight-4);
        if(combatShowUnavailable)for(int i=0;i<unavailable.Length;i++) {
            var group=unavailable[i];var card=field[group[0]].card;
            string status=attack?T("不能攻击","Cannot attack","攻撃不可"):T("不能阻挡","Cannot block","ブロック不可");
            var tile=CombatSourceTile(pool,"Unavailable_"+card.id,(freeGroups.Length+i)*(sourceWidth+gap),2,sourceWidth,lowerHeight-4,
                card,CardName(card)+(group.Length>1?" ×"+group.Length:""),status,false,false,HudMuted,null);
            tile.GetComponent<Button>().interactable=false;
        }
        if(shownGroups==0){
            string empty=sources.Count>0?T("拖回此处可撤回指派","Drop here to recall","ここに戻して指定を取り消す")
                :attack?T("没有可攻击的生物","No eligible attackers","攻撃できるクリーチャーなし"):T("没有可阻挡的生物","No eligible blockers","ブロックできるクリーチャーなし");
            var label=CombatText(pool,"Empty",empty,10,0,stripWidth-20,lowerHeight,18,HudMuted);label.alignment=TextAnchor.MiddleCenter;
        }

        var foot=Panel(fourCombatDialog,"Footer",Vector2.zero,Vector2.one,Color.Lerp(HudInk,Color.black,.12f));
        if(noticeHeight>0) {
            var notice=Panel(fourCombatDialog,"DeclarationIssue",Vector2.zero,Vector2.one,HudRaised);
            CombatPlace(notice,inset,height-68-noticeHeight,stripWidth,noticeHeight);
            CombatRule(notice,"Mark",0,4,3,noticeHeight-8,HudAccent);
            FitCombatLabel(CombatText(notice,"Reason",issue,12,0,stripWidth-24,noticeHeight,18,HudText));
        }
        CombatRule(foot,"Rule",inset,0,stripWidth,1,HudRule);
        CombatPlace(foot,2,height-64,width-4,62);
        var actions=new List<Button>();
        float ax=18;
        void ActionButton(string id,string label,Action action,bool enabled=true){
            var b=CombatButton(foot,id,label,ax,10,112,false,action);b.interactable=enabled;ax+=120;actions.Add(b);
        }
        if(multi && sources.Count>1)
            ActionButton("SelectAll",T("全选生物","Select all","すべて選択"),()=>{if(!Live())return;combatSources.Clear();combatSources.AddRange(sources.Where(id=>pairs.Any(o=>Source(o)==id)));PresentFourCombat(true);});
        if(combatSources.Count>0)ActionButton("ClearDraft",T("取消选择","Deselect","選択解除"),()=>{if(Live()){combatSources.Clear();combatFocus=0;PresentFourCombat(true);}});
        if(combatSources.Count==1) {
            int id=combatSources[0];string band="attack-band:"+id;
            if(removals.Contains(id))ActionButton("Recall",attack?T("撤回攻击","Recall","攻撃を撤回"):T("撤回阻挡","Unblock","ブロックを撤回"),()=>Send(removePrefix+id));
            if(attack && OptionIndex(band)>=0)ActionButton("Band",options[OptionIndex(band)].selected?T("取消联合","Unselect band","バンド解除"):T("联合","Band","バンド"),()=>Send(band),!options[OptionIndex(band)].disabled);
        }
        if(attack && OptionIndex("reset-attacks")>=0)ActionButton("Reset",T("全部撤回","Recall all","全て撤回"),()=>Send("reset-attacks"),!options[OptionIndex("reset-attacks")].disabled);
        if(OptionIndex("cancel")>=0)ActionButton("Cancel",RequestOptionLabel(options[OptionIndex("cancel")]),()=>Send("cancel"),!options[OptionIndex("cancel")].disabled);
        if(ax>width-195 && actions.Count>0){
            float step=(width-215)/actions.Count;
            for(int i=0;i<actions.Count;i++) {
                CombatPlace((RectTransform)actions[i].transform,18+i*step,10,step-5,42);
                actions[i].GetComponentInChildren<Text>().rectTransform.sizeDelta=new Vector2(step-10,38);
            }
        }
        if(width-ax-220>=280) {
            string hint=attack?T("把生物拖到攻击对象下方","Drag a creature below its attack destination","攻撃先の下にクリーチャーをドラッグ"):
                T("把阻挡生物拖到来袭生物下方","Drag a blocker below an incoming attacker","攻撃クリーチャーの下へブロッカーをドラッグ");
            CombatText(foot,"Instruction",hint,ax+6,4,width-ax-220,54,16,HudMuted);
        }
        bool engaged=placed.Count>0;
        string confirm=attack?(engaged?T("确认攻击","Confirm attacks","攻撃を確定"):T("不攻击","No attacks","攻撃しない")):
            (engaged?T("确认阻挡","Confirm blocks","ブロックを確定"):T("不阻挡","No blocks","ブロックしない"));
        bool pendingSelection=combatSources.Any(id=>!placed.Contains(id));
        var done=CombatButton(foot,"Confirm",confirm,width-194,10,170,noticeHeight==0,()=>{if(!pendingSelection && noticeHeight==0)Send("ok");});
        int ok=OptionIndex("ok");
        done.interactable=ok>=0 && !options[ok].disabled && !pendingSelection && noticeHeight==0;
        if(!done.interactable)done.GetComponentInChildren<Text>().color=HudMuted;
    }

    private string CombatCardName(ForgeCardView card)=>card.faceDown?
        TapPaymentText("面朝下的牌","Face-down card","裏向きのカード"):ExileCardName(card);

    private string CombatDeclarationHint(ForgeCombatDeclaration rules,string card,int blockers) {
        if(string.IsNullOrEmpty(rules?.issue))return null;
        string T(string zh,string en,string ja)=>TapPaymentText(zh,en,ja);
        string who=string.IsNullOrEmpty(card)?"":card+" · ";
        return who+(rules.issue switch {
            "mustAttack"=>T("若能攻击，本次须参与攻击","Must attack this combat if able","可能ならこの戦闘で攻撃"),
            "attackRequirement"=>T("仍有攻击要求，请调整参战生物或攻击对象","Adjust the attackers or their destinations to meet the requirements","攻撃要件を満たすよう攻撃クリーチャーか攻撃先を調整"),
            "NOT_ALONE"=>T("须与其他生物一起攻击","Needs another attacker","他のクリーチャーと一緒に攻撃"),
            "ONLY_ALONE"=>T("只能单独攻击","Must attack alone","単独でのみ攻撃可能"),
            "NEED_TWO_OTHERS"=>T("还需至少两只其他生物一起攻击","Needs two other attackers","他に2体以上の攻撃が必要"),
            "NEED_GREATER_POWER"=>T("须有力量更大的生物一起攻击","Needs an attacker with greater power","より大きいパワーの攻撃クリーチャーが必要"),
            "NEED_BLACK_OR_GREEN"=>T("须有另一只黑色或绿色生物一起攻击","Needs another black or green attacker","他に黒か緑の攻撃クリーチャーが必要"),
            "attackMax"=>T($"本次至多可用 {rules.seatLimit} 只生物攻击",$"At most {rules.seatLimit} attackers",$"攻撃は最大{rules.seatLimit}体"),
            "mustBlock"=>T("若能阻挡，须指派给符合要求的攻击者","Must block a qualifying attacker if able","可能なら該当する攻撃クリーチャーをブロック"),
            "blockRequirement"=>T("仍有来袭生物必须被阻挡，请调整指派","Required blocks remain. Adjust the blockers","必要なブロックを満たすよう指定を調整"),
            "blockTogether"=>T("须与其他生物一起阻挡","Needs another blocking creature","他のクリーチャーと一緒にブロック"),
            "blockWithTwo"=>T("还需至少两只其他生物一起阻挡","Needs two other blocking creatures","他に2体以上のブロックが必要"),
            "blockWithGreater"=>T("须有力量更大的生物一起阻挡","Needs a blocker with greater power","より大きいパワーのブロッカーが必要"),
            "blockMin"=>T($"已指派 {blockers} / {rules.amount} 只，再加 {rules.amount-blockers} 只或撤回阻挡",$"{blockers} / {rules.amount} blockers. Add {rules.amount-blockers} more or withdraw the block",$"現在{blockers} / {rules.amount}体。あと{rules.amount-blockers}体加えるかブロックを解除"),
            "blockMax"=>T($"至多能被 {rules.amount} 只生物阻挡",$"At most {rules.amount} blockers",$"ブロックは最大{rules.amount}体"),
            _=>T("当前指派不符合战斗限制，请调整后确认","Adjust the declaration to meet combat restrictions","戦闘の制限を満たすよう指定を変更")
        });
    }

    private static void CombatPlace(RectTransform r,float left,float top,float width,float height)
    {
        r.anchorMin=r.anchorMax=r.pivot=Vector2.up;
        r.sizeDelta=new Vector2(width,height);
        r.anchoredPosition=new Vector2(left,-top);
    }
    private Text CombatText(RectTransform parent,string name,string text,float x,float y,float width,float height,int size,Color color,bool heading=false)
    {
        var label=Label(parent,name,text,Vector2.zero,Vector2.one,size,color);
        CombatPlace(label.rectTransform,x,y,width,height);
        MenuTypography.Use(label,heading?MenuTypography.Face.Heading:MenuTypography.Face.Control);
        label.alignment=TextAnchor.MiddleLeft;
        label.raycastTarget=false;
        return label;
    }
    private Button CombatButton(RectTransform parent,string name,string caption,float left,float top,float width,bool primary,Action action)
    {
        var b=Button(parent,name,caption,Vector2.zero,new Vector2(width,42),action,HudRaised);
        CombatPlace((RectTransform)b.transform,left,top,width,42);
        MenuVisualTheme.StyleArchiveControl(b,primary);
        return b;
    }
    private RectTransform CombatStrip(string name,float left,float top,float width,float height,float used,float restore,out ScrollRect scroll)
    {
        var content=Scroll(fourCombatDialog,name,Vector2.zero,new Vector2(width,height));
        scroll=content.parent.GetComponent<ScrollRect>();
        CombatPlace(scroll.viewport,left,top,width,height);
        scroll.horizontal=true;scroll.vertical=false;scroll.inertia=false;scroll.scrollSensitivity=48;
        CombatPlace(content,0,0,Mathf.Max(width,used),height);
        content.anchoredPosition=Vector2.left*Mathf.Clamp(restore,0,Mathf.Max(0,used-width));
        return content;
    }

    private void CombatScrollButtons(RectTransform heading,ScrollRect scroll,float step)
    {
        float travel=scroll.content.rect.width-scroll.viewport.rect.width;
        if(travel<1)return;
        var previous=CombatButton(fourCombatDialog,scroll.name+"Prev","‹",scroll.viewport.anchoredPosition.x+scroll.viewport.rect.width-80,-heading.anchoredPosition.y,36,false,
            ()=>scroll.horizontalNormalizedPosition=Mathf.Clamp01(scroll.horizontalNormalizedPosition-step/travel));
        var next=CombatButton(fourCombatDialog,scroll.name+"Next","›",scroll.viewport.anchoredPosition.x+scroll.viewport.rect.width-38,-heading.anchoredPosition.y,36,false,
            ()=>scroll.horizontalNormalizedPosition=Mathf.Clamp01(scroll.horizontalNormalizedPosition+step/travel));
        foreach(var b in new[]{previous,next}) {
            ((RectTransform)b.transform).sizeDelta=new Vector2(36,30);
            b.GetComponentInChildren<Text>().gameObject.SetActive(false);
            var glyph=Rect((RectTransform)b.transform,"Chevron",Vector2.zero,new Vector2(24,24)).gameObject.AddComponent<BattlefieldScrollChevron>();
            glyph.direction=b==previous?-1:1;glyph.SmoothEdges=true;glyph.raycastTarget=false;
        }
        var prevGlyph=previous.GetComponentInChildren<BattlefieldScrollChevron>();
        var nextGlyph=next.GetComponentInChildren<BattlefieldScrollChevron>();
        void Sync(Vector2 pos){
            previous.interactable=pos.x>.001f;next.interactable=pos.x<.999f;
            prevGlyph.color=previous.interactable?HudText:HudMuted;nextGlyph.color=next.interactable?HudText:HudMuted;
        }
        scroll.onValueChanged.AddListener(Sync);Sync(new Vector2(Mathf.Clamp01(-scroll.content.anchoredPosition.x/travel),0));
    }

    private void CombatArt(RectTransform parent,ForgeCardView card,int player,float x,float y,float width,float height)
    {
        if(card!=null && TryBattlefieldArt(card,out var sprite) && sprite!=null){
            var art=Rect(parent,"Art",Vector2.zero,Vector2.one).gameObject.AddComponent<RawImage>();
            CombatPlace(art.rectTransform,x,y,width,height);
            if(card.faceDown)CardArtCrop.Fill(art,sprite,new Rect(0,0,1,1));
            else {
                var layout=worldTable?.artLayouts?.Find(sprite);
                var crop=BattlefieldCardArtLayouts.IllustrationPixels(card,sprite,card.currentType,layout,width/height,out bool rotated,out _);
                if(rotated) {
                    var box=art.rectTransform;
                    box.pivot=new Vector2(.5f,.5f);box.anchoredPosition=new Vector2(x+width/2,-y-height/2);
                    box.sizeDelta=new Vector2(height,width);box.localRotation=Quaternion.Euler(0,0,-90);
                }
                art.texture=sprite.texture;
                // Leave a small bleed inside the printed ink so the card frame stays out of the thumbnail.
                var bleed=crop.size*.014f;crop.min+=bleed;crop.max-=bleed;
                art.uvRect=new Rect(crop.x/sprite.texture.width,crop.y/sprite.texture.height,crop.width/sprite.texture.width,crop.height/sprite.texture.height);
            }
            art.raycastTarget=false;
        } else if(player>0) {
            var portrait=PlayerAvatarGraphic.Resolve(FourPlayerPortrait(player));
            var art=Rect(parent,"Avatar",Vector2.zero,Vector2.one).gameObject.AddComponent<RawImage>();
            CombatPlace(art.rectTransform,x,y,width,height);
            art.texture=portrait.Load();
            var uv=portrait.UV;float visible=uv.height*Mathf.Min(1,height/width);
            art.uvRect=new Rect(uv.x,Mathf.Clamp(uv.center.y+uv.height*.10f-visible*.5f,uv.yMin,uv.yMax-visible),uv.width,visible);
            art.raycastTarget=false;
        }
    }

    private static Texture2D combatShadowRamp;
    private static void CombatShade(RectTransform parent,float width,float height)
    {
        if(combatShadowRamp==null) {
            combatShadowRamp=IntegratedUiUtility.VerticalGradientTexture(1,64,new (float,Color)[]{
                (0,new Color(.035f,.026f,.045f,.98f)),(.18f,new Color(.04f,.03f,.05f,.85f)),
                (.48f,new Color(.04f,.03f,.05f,.35f)),(.76f,new Color(0,0,0,0)),(1,Color.clear)});
            combatShadowRamp.hideFlags=HideFlags.HideAndDontSave;
        }
        var shade=Rect(parent,"ArtShade",Vector2.zero,Vector2.one).gameObject.AddComponent<RawImage>();
        CombatPlace(shade.rectTransform,0,0,width,height);shade.texture=combatShadowRamp;shade.raycastTarget=false;
    }
    private static RectTransform CombatRule(RectTransform parent,string name,float x,float y,float width,float height,Color tint)
    {
        var line=Panel(parent,name,Vector2.zero,Vector2.one,tint);
        CombatPlace(line,x,y,width,height);line.GetComponent<Image>().raycastTarget=false;
        return line;
    }
    private static void FitCombatLabel(Text label)
    {
        label.resizeTextForBestFit=true;label.resizeTextMinSize=13;label.resizeTextMaxSize=label.fontSize;
    }

    private RectTransform CombatSourceTile(RectTransform parent,string id,float left,float top,float width,float height,
        ForgeCardView card,string name,string stats,bool compact,bool selected,Color accent,Action click)
    {
        var tile=Panel(parent,id,Vector2.zero,Vector2.one,Color.white);
        CombatPlace(tile,left,top,width,height);
        var button=tile.gameObject.AddComponent<Button>();
        button.targetGraphic=tile.GetComponent<Image>();
        var colors=button.colors;
        colors.normalColor=selected?Color.Lerp(HudRaised,HudAccent,.15f):HudRaised;
        colors.highlightedColor=Color.Lerp(HudRaised,HudAccent,.30f);
        colors.selectedColor=colors.normalColor;colors.pressedColor=HudSelected;button.colors=colors;
        if(click!=null)button.onClick.AddListener(()=>click());
        if(compact) {
            float artWidth=Mathf.Min(70,width*.40f);
            CombatArt(tile,card,0,2,2,artWidth-2,height-4);
            float x=artWidth+9;
            var title=CombatText(tile,"Name",name,x,3,width-x-4,height*.54f,17,HudText,true);
            FitCombatLabel(title);
            var statsLabel=CombatText(tile,"Detail",stats,x,height*.54f,width-x-6,height*.40f,15,HudMuted);
            FitCombatLabel(statsLabel);
        } else {
            CombatArt(tile,card,0,1,1,width-2,height-2);
            CombatShade(tile,width,height);
            float statWidth=Mathf.Clamp(stats.Length*8+6,42,width*.34f);
            var title=CombatText(tile,"Name",name,9,height*.62f,width-statWidth-18,height*.36f,20,HudText,true);
            FitCombatLabel(title);
            var statsLabel=CombatText(tile,"Detail",stats,width-statWidth-7,height*.62f,statWidth,height*.36f,15,HudText);
            statsLabel.alignment=TextAnchor.MiddleRight;FitCombatLabel(statsLabel);
        }
        CombatRule(tile,"Edge",0,height-(selected?3:1),width,selected?3:1,selected?accent:HudEdge);
        return tile;
    }
}
