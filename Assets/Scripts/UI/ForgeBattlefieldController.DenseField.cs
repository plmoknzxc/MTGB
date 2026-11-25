using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    // World meshes sit inside larger input anchors; a small anchor overlap
    // leaves a readable gap between the rendered card frames.
    private const float WorldRoomGap=20;
    // Room art is limited by its .88-high anchor and .78 display aspect;
    // a 1.7-wide input slot leaves a large invisible gutter beside each room.
    private const float WorldRoomWidthFactor=1.13f;
    private readonly Dictionary<string,ScrollRect> denseFieldRows=new Dictionary<string,ScrollRect>();
    private readonly Dictionary<string,float> denseCardWidths=new Dictionary<string,float>();
    private readonly Dictionary<string,string> denseWidthSignatures=new Dictionary<string,string>();
    private readonly Dictionary<string,List<ForgeCardView[]>> denseFieldGroups=new Dictionary<string,List<ForgeCardView[]>>();
    private const float FullWorldCardWidth=176;
    private const float DuelMidlineInset=24;
    private float ArenaTableWidth => worldTable==null
        ? Mathf.Max(320,Mathf.Min(1320,surface.rect.width-560))
        : Mathf.Max(320,surface.rect.width-340);

    private sealed class WorldFieldLanes
    {
        public float supportWidth,cardWidth,rightExtension,rankPitch;
        public int columns,creatures,lands,supportRows;
    }
    private readonly WorldFieldLanes ownFieldLanes=new(),otherFieldLanes=new();
    private WorldFieldLanes FieldLanes(bool own)=>own?ownFieldLanes:otherFieldLanes;
    private bool IsDuelTable => IsRedesignLayout && worldTable!=null && view?.players?.Length==2;
    private static float WorldVisibleGap(float anchorWidth)=>32-anchorWidth*.27f;

    // Compute one split for all five rows of this seat. Use visual groups so
    // a stack of identical permanents occupies exactly one slot.
    private void MeasureWorldField(ForgeCardView[] field,bool own)
    {
        var plan=FieldLanes(own);
        string prefix=own?"Your":"Opponent";
        var land=ArenaVisualGroups(field.Where(c=>c.land&&!c.creature).ToArray(),prefix+"Lands");
        var creatures=ArenaVisualGroups(field.Where(c=>c.creature).ToArray(),prefix+"Creatures");
        var support=field.Where(c=>!c.land&&!c.creature).ToArray();
        var regular=ArenaVisualGroups(support.Where(c=>!c.planeswalker&&!c.battle).ToArray(),prefix+"Support");
        var walkers=ArenaVisualGroups(support.Where(c=>c.planeswalker&&!c.battle).ToArray(),prefix+"Planeswalkers");
        var battles=ArenaVisualGroups(support.Where(c=>c.battle).ToArray(),prefix+"Battles");
        plan.lands=land.Count;
        plan.creatures=creatures.Count;
        int mainCount=Math.Max(plan.lands,plan.creatures);
        float tableWidth=ArenaTableWidth;
        float inset=InformationLaneInset(cardsRoot,own,36-tableWidth*.5f+32);
        float usable=Mathf.Max(136,tableWidth-inset-28);
        float edge=Mathf.Min(410,surface.rect.height*.5f-100);
        float Footprint(ForgeCardView c,bool horizontal){
            float w=c.room?WorldRoomWidthFactor:1,h=c.room?.88f:c.planeswalker?1.26f:c.battle?.8f:1;
            const float angle=12*Mathf.Deg2Rad;
            float sin=Mathf.Sin(angle),cos=Mathf.Cos(angle);
            return horizontal?Mathf.Max(w,w*cos+h*sin):Mathf.Max(h,h*cos+w*sin);
        }
        // Reserve turning room before any permanent taps. Its neighbours keep their slots.
        // WorldVisibleGap subtracts .27 anchor widths per gap.
        float RowScale(List<ForgeCardView[]> groups)=>groups.Sum(g=>Footprint(g[0],true))-.27f*Math.Max(0,groups.Count-1);
        float landScale=RowScale(land),creatureScale=RowScale(creatures);
        float landGaps=32*Math.Max(0,land.Count-1),creatureGaps=32*Math.Max(0,creatures.Count-1);
        float MainSpan(float cardWidth)=>Mathf.Max(landScale*cardWidth+landGaps,creatureScale*cardWidth+creatureGaps);
        float mainDemand=MainSpan(FullWorldCardWidth)+8;
        float LaneSpan(List<ForgeCardView[]> groups,int rows){
            if(groups.Count==0)return 0;
            float span=0;
            for(int i=0;i<groups.Count;i+=rows){
                var a=groups[i][0];var b=groups[Math.Min(i+rows-1,groups.Count-1)][0];
                span+=FullWorldCardWidth*Mathf.Max(Footprint(a,true),Footprint(b,true));
                if(i+rows<groups.Count)span+=a.room||b.room||groups[i+rows][0].room?WorldRoomGap:8;
            }
            return span+64+(groups.Any(g=>g[0].room)?32:8);
        }
        bool paired=walkers.Count>0&&battles.Count>0;
        float specialWidth=Mathf.Max(LaneSpan(walkers,1),LaneSpan(battles,1))*(paired?2:1);
        plan.supportRows=1;
        float regularWidth=LaneSpan(regular,1);
        float panReach=IsDuelTable?0:HorizontalPanLimit(1);
        if(regular.Count>1&&specialWidth==0&&mainDemand+regularWidth>usable+panReach
            &&2*FullWorldCardWidth*regular.Max(g=>Footprint(g[0],false))+16<=edge){
            plan.supportRows=2;
            regularWidth=LaneSpan(regular,2);
        }
        float supportDemand=Mathf.Max(specialWidth,regularWidth);
        // Keep the left edge anchored. Only extend into the pannable area when
        // the occupied rows need it, and measure the settled rows after wrapping.
        plan.rightExtension=Mathf.Clamp(mainDemand+supportDemand-usable,0,panReach);
        usable+=plan.rightExtension;
        // Keep a sparse support lane near its usual position, but lend its
        // unused space to lands/creatures, and vice versa, before shrinking.
        float preferredSide=Mathf.Max(supportDemand,Mathf.Min(500,tableWidth*.38f));
        plan.supportWidth=supportDemand==0?0:Mathf.Min(preferredSide,
            Mathf.Max(Mathf.Min(usable*.40f,supportDemand),usable-mainDemand));
        if(IsDuelTable) {
            // Give the main lanes two ranks before reserving room for a side lane.
            float mainReserve=Mathf.Min(mainDemand,mainDemand*.5f+FullWorldCardWidth*.5f);
            plan.supportWidth=Mathf.Min(supportDemand,Mathf.Max(usable*.4f,usable-mainReserve));
        }
        float mainSpace=Mathf.Max(64,usable-plan.supportWidth-8);
        float width=FullWorldCardWidth;
        float mainFactor=field.Where(c=>c.land||c.creature).Select(c=>Footprint(c,true)).DefaultIfEmpty(1).Max();
        if(!IsDuelTable) {
            if(landScale>0)width=Mathf.Min(width,(mainSpace-landGaps)/landScale);
            if(creatureScale>0)width=Mathf.Min(width,(mainSpace-creatureGaps)/creatureScale);
            width=Mathf.Max(width,112*.88f);
        }
        if(support.Length>0){
            float availableSupport=plan.supportWidth-64;
            foreach(var c in support){
                float lane=paired&&(c.planeswalker||c.battle)?plan.supportWidth*.5f-64:availableSupport;
                width=Mathf.Min(width,(Mathf.Max(80,lane)-(c.room?32:8))/Footprint(c,true));
            }
            float regularHeight=regular.Select(g=>Footprint(g[0],false)).DefaultIfEmpty(0).Max()*plan.supportRows;
            float specialHeight=support.Where(c=>c.battle||c.planeswalker).Select(c=>Footprint(c,false)).DefaultIfEmpty(0).Max();
            int rows=(regular.Count>0?plan.supportRows:0)+(specialHeight>0?1:0);
            width=Mathf.Min(width,(edge-(rows-1)*16-3-rows*8)/(regularHeight+specialHeight));
        }
        int Columns(float w)=>MainSpan(w)<=mainSpace+.05f?Math.Max(1,mainCount):
            Mathf.Max(1,Mathf.FloorToInt((mainSpace+WorldVisibleGap(w)+.05f)/(w*mainFactor+WorldVisibleGap(w))));
        if(IsDuelTable) {
            int Capacity(float w)=>Mathf.Max(1,Mathf.FloorToInt((mainSpace+WorldVisibleGap(w))/(w*mainFactor+WorldVisibleGap(w))));
            int Ranks(int count,int capacity)=>Mathf.CeilToInt(count/(float)capacity);
            float heightFactor=field.Where(c=>c.land||c.creature).Select(c=>Footprint(c,false)).DefaultIfEmpty(1).Max();
            bool Fits(float w) {
                int capacity=Capacity(w),b=Ranks(plan.lands,capacity);
                int a=Mathf.Max(b>0?1:0,Ranks(plan.creatures,capacity));
                return a<=2 && b<=2 && (a+b)*(w*heightFactor+10)<=edge-DuelMidlineInset;
            }
            float high=Mathf.Max(48,width);
            int maxRanks=Mathf.Clamp(plan.creatures,plan.lands>0?1:0,2)+Mathf.Min(2,plan.lands);
            float floor=Mathf.Max(48,Mathf.Min(98.56f,((edge-DuelMidlineInset)/Mathf.Max(1,maxRanks)-10)/heightFactor));
            float low=Mathf.Min(high,floor);
            if(Fits(high))low=high;
            else for(int i=0;i<12;i++) {
                float mid=(low+high)*.5f;
                if(Fits(mid))low=mid;else high=mid;
            }
            plan.cardWidth=low;
            plan.columns=Capacity(low);
            plan.rankPitch=low*heightFactor+10;
            return;
        }
        int columns=Columns(width);
        if(creatures.Count>columns&&land.Count>columns&&mainCount>columns*2)
            width=Mathf.Min(width,84/mainFactor);
        plan.cardWidth=Mathf.Max(48,width);
        plan.columns=Columns(plan.cardWidth);
    }

    // Heights are measured from the shared midline, identically for both seats.
    private float DuelRankCenter(bool own,bool lands,int ranks)
    {
        var plan=FieldLanes(own);
        int creatureRanks=Mathf.Clamp(Mathf.CeilToInt(plan.creatures/(float)plan.columns),1,2);
        return (own?-1:1)*(DuelMidlineInset+((lands?creatureRanks:0)+ranks*.5f)*plan.rankPitch);
    }

    private bool HasTwoLandRows(bool own)
    {
        if(worldTable!=null)return FieldLanes(own).lands>FieldLanes(own).columns;
        var player=view?.players?.FirstOrDefault(p=>own?p.seat==seat:p.seat!=seat);
        string row=own?"YourLands":"OpponentLands";
        int groups=ArenaVisualGroups((player?.battlefield??Array.Empty<ForgeCardView>()).Where(c=>c.land&&!c.creature).ToArray(),row).Count;
        float tableWidth=ArenaTableWidth;
        float available=tableWidth-Mathf.Min(500,tableWidth*.38f)-28;
        int columns=Mathf.Clamp(Mathf.FloorToInt((available+8)/(144*.88f+8)),1,6);
        return groups>columns;
    }
    private float WorldMainLaneGapReduction(bool own,float oldSeparation)
    {
        var plan=FieldLanes(own);
        if(worldTable==null||plan.creatures>plan.columns)return 0;
        // A single rank of lands can occupy the empty creature lane before either hand forces a zoom-out.
        if(plan.creatures==0)return plan.lands<=plan.columns?Mathf.Clamp(oldSeparation-72,0,128):0;
        return Mathf.Clamp(oldSeparation-plan.cardWidth*1.19f-28,0,100);
    }
    private void SetDenseArenaRowVisible(string row,bool visible)
    {
        if(denseFieldRows.TryGetValue(row,out var scroll)&&scroll!=null)scroll.gameObject.SetActive(visible);
        if(!visible)denseFieldGroups.Remove(row);
    }
    // Pack populated regions from each hand towards the midline. All three
    // own independent horizontal offsets and use their measured full card bounds.
    private void ArrangeSupportRegions(bool own)
    {
        string prefix=own?"Your":"Opponent";
        float edge=Mathf.Min(410,surface.rect.height*.5f-100);
        var active=new[]{"Support","Planeswalkers","Battles"}.Select(kind=>prefix+kind)
            .Where(key=>denseFieldRows.TryGetValue(key,out var lane)&&lane!=null&&lane.gameObject.activeSelf).ToArray();
        // Fit the real footprints, including shadows and loyalty/defense shields.
        // A fixed height ratio understated those adornments and crossed midline.
        float tableWidth=ArenaTableWidth;
        float sideWidth=worldTable!=null?FieldLanes(own).supportWidth:Mathf.Min(500,tableWidth*.38f);
        if(worldTable!=null){
            // Keep a visible lane between the last land/creature and the first
            // support card. The player information lane can move lands right,
            // so the fixed-width estimate alone can put a walker on top of them.
            float left=36+tableWidth*.5f+FieldLanes(own).rightExtension-sideWidth;
            float mainRight=float.NegativeInfinity;
            foreach(string kind in new[]{"Lands","Creatures"}) {
                string key=prefix+kind;
                if(denseFieldRows.TryGetValue(key,out var mainLane)&&mainLane!=null&&mainLane.gameObject.activeSelf) {
                    var rect=(RectTransform)mainLane.transform;
                    mainRight=Mathf.Max(mainRight,rect.anchoredPosition.x+rect.rect.width*.5f);
                    continue;
                }
                foreach(var pair in arenaCards.Where(pair=>retainedCards.Contains(pair.Key)
                    && pair.Key.StartsWith(key+":",StringComparison.Ordinal)
                    && pair.Value.root.parent==cardsRoot))
                    mainRight=Mathf.Max(mainRight,pair.Value.restingPosition.x
                        +FieldFootprint(pair.Value,own).xMax);
            }
            left=Mathf.Max(left,mainRight+20);
            bool paired=active.Contains(prefix+"Planeswalkers")&&active.Contains(prefix+"Battles");
            if(own&&IsDuelTable&&active.Length>0){
                float footerLeft=DuelFieldFooterLeft;
                float bottomWidth=denseFieldRows[active[0]].viewport.rect.width;
                if(paired&&active[0]==prefix+"Planeswalkers")
                    bottomWidth=Mathf.Max(bottomWidth,sideWidth*.5f+denseFieldRows[prefix+"Battles"].viewport.rect.width);
                if(left+32+bottomWidth>footerLeft-surface.rect.width*.5f){
                    // Raise the row above the phase controls if this half has room.
                    float occupied=active.Sum(k=>denseFieldRows[k].viewport.rect.height)+(active.Length-1)*16;
                    if(paired)occupied-=Mathf.Min(denseFieldRows[prefix+"Planeswalkers"].viewport.rect.height,
                        denseFieldRows[prefix+"Battles"].viewport.rect.height)+16;
                    edge=Mathf.Min(edge,Mathf.Max(occupied+4,surface.rect.height*.5f-DuelFieldFooterTop-16));
                }
            }
            foreach(var row in paired?new[]{new[]{"Support"},new[]{"Planeswalkers","Battles"}}:new[]{new[]{"Support"},new[]{"Planeswalkers"},new[]{"Battles"}}){
                var keys=row.Select(k=>prefix+k).Where(active.Contains).ToArray();
                if(keys.Length==0)continue;
                float height=keys.Max(k=>denseFieldRows[k].viewport.rect.height);
                for(int i=0;i<keys.Length;i++){
                    var rect=(RectTransform)denseFieldRows[keys[i]].transform;
                    rect.anchoredPosition=new Vector2(left+32+(keys.Length==2?i*sideWidth*.5f:0)+rect.rect.width*.5f,
                        (own?-1:1)*(edge-height*.5f));
                }
                edge-=height+16;
            }
            return;
        }
        for(int pass=0;pass<4&&active.Length>0;pass++) {
            float height=active.Sum(key=>((RectTransform)denseFieldRows[key].transform).rect.height);
            float budget=edge-Math.Max(0,active.Length-1)*16-2;
            if(height<=budget)break;
            float factor=budget/height;bool resized=false;
            foreach(string key in active) {
                float prior=denseCardWidths[key],target=Mathf.Max(48,prior*factor-.5f);
                if(target>=prior-.1f)continue;
                denseCardWidths[key]=target;resized=true;
                LayoutDenseArenaRow((RectTransform)denseFieldRows[key].transform.parent,denseFieldGroups[key],key,own,false,true,tableWidth,sideWidth,sideWidth);
            }
            if(!resized)break;
        }
        foreach(string kind in new[]{"Support","Planeswalkers","Battles"}) {
            if(!denseFieldRows.TryGetValue(prefix+kind,out var lane) || lane==null || !lane.gameObject.activeSelf)continue;
            var rect=(RectTransform)lane.transform;
            float center=edge-rect.rect.height*.5f;
            rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,own?-center:center);
            edge-=rect.rect.height+16;
        }
    }
    // Each overflow region owns its scroll. Original card IDs and grouping rules
    // remain unchanged; the hand, marker lanes and stack are outside these masks.
    private float InformationLaneInset(RectTransform parent,bool own,float left)
    {
        float edge=left-44;
        // HUD clearance belongs to the unpanned table. A new priority snapshot
        // must not push the row away from a player who just dragged it into view.
        float LayoutX(Vector3 worldPoint)=>worldTable!=null&&parent==cardsRoot
            ?surface.InverseTransformPoint(worldPoint).x-surface.rect.center.x:parent.InverseTransformPoint(worldPoint).x;
        void Include(RectTransform rect) {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            foreach(var corner in corners)edge=Mathf.Max(edge,LayoutX(corner));
        }
        string prefix=own?"YourEmblem:":"OpponentEmblem:";
        foreach(var pair in arenaCards)
            if(retainedCards.Contains(pair.Key)&&pair.Key.StartsWith(prefix,StringComparison.Ordinal))Include(pair.Value.root);
        var plate=Node(own?"PlayerPlate":"OpponentPlate");
        if(plate.gameObject.activeInHierarchy)Include((RectTransform)plate);
        if (IsRedesignLayout)
        {
            // The fixed player mana lane includes 32-unit external arrows.
            // Reserve it before the first pool render, including tapped lands.
            var manaEdge = surface.TransformPoint(new Vector3(surface.rect.xMin + 392, 0, 0));
            edge = Mathf.Max(edge, LayoutX(manaEdge));
            if(IsDuelTable && battleSidebar!=null && battleSidebar.gameObject.activeInHierarchy && !sidebarCollapsed)
                edge=Mathf.Max(edge,surface.rect.xMin+SidebarOpenRight);
        }
        var counters=plate.Find("PlayerCounters") as RectTransform;
        if(counters!=null&&counters.gameObject.activeInHierarchy) {
            Include(counters);
            foreach(var control in counters.GetComponentsInChildren<Button>())Include((RectTransform)control.transform);
        }
        foreach(string name in new[]{"StatesPrevious","StatesNext","EmblemsPrevious","EmblemsNext"}) {
            var control=plate.Find(name) as RectTransform;
            if(control!=null&&control.gameObject.activeInHierarchy)Include(control);
        }
        // The battlefield arrow extends 32 units outside its viewport.
        return Mathf.Max(0,edge+44-left);
    }
    private void LayoutDenseArenaRow(RectTransform parent,List<ForgeCardView[]> groups,string row,bool own,bool lands,bool support,float tableWidth,float sideWidth,float available)
    {
        denseFieldGroups[row]=groups;
        const float gap=8;
        int rowCount=Math.Min(groups.Count,row.Contains("Planeswalkers") || row.Contains("Battles") || worldTable!=null&&support?1:2);
        var supportField=support?(view?.players?.FirstOrDefault(p=>own?p.seat==seat:p.seat!=seat)?.battlefield??Array.Empty<ForgeCardView>())
            .Where(c=>!c.land&&!c.creature).ToArray():Array.Empty<ForgeCardView>();
        bool hasSupport=supportField.Any(c=>!c.planeswalker&&!c.battle),hasWalkers=supportField.Any(c=>c.planeswalker&&!c.battle),hasBattles=supportField.Any(c=>c.battle);
        int supportCategories=(hasSupport?1:0)+(hasWalkers?1:0)+(hasBattles?1:0);
        // When categories share this half, use its horizontal room before
        // reserving a second artifact/enchantment row and shrinking every card.
        if(support&&supportCategories>1)rowCount=1;
        if(worldTable!=null&&support&&hasSupport&&supportCategories==1)rowCount=Math.Min(groups.Count,FieldLanes(own).supportRows);
        int preferredColumns=Mathf.Clamp(Mathf.CeilToInt(groups.Count/(float)rowCount),1,support?5:6);
        float rowGap=support?5:gap;
        bool pairedSpecial=worldTable!=null&&support&&hasWalkers&&hasBattles&&(row.Contains("Planeswalkers")||row.Contains("Battles"));
        bool roomRow=worldTable!=null&&groups.Any(g=>g[0].room);
        float edgePadding=roomRow?16:gap*.5f;
        float zoneWidth=support?(pairedSpecial?sideWidth*.5f:sideWidth):available;
        float informationInset=support?0:InformationLaneInset(parent,own,36-tableWidth*.5f+32);
        float availableWidth=Mathf.Max(80,zoneWidth-64-informationInset);
        string signature=availableWidth+"/"+preferredColumns+"/"+GamePreferences.Revision+"/"+groups.Max(g=>g.Length)+"/"+groups.Any(g=>g[0].room)+"/"+groups.Any(g=>g[0].tapped)+"/"+groups.Any(g=>g[0].planeswalker);
        float maximum=worldTable!=null?FieldLanes(own).cardWidth:support?144*.88f:lands?112*.88f:HasTwoLandRows(own)?112*.88f:144*.88f;
        if(support) {
            float footprintWidth=maximum*(groups.Any(g=>g[0].room)?WorldRoomWidthFactor:1);
            preferredColumns=Math.Min(preferredColumns,Math.Max(1,Mathf.FloorToInt((availableWidth+gap)/(footprintWidth+gap))));
        }
        signature+="/"+supportCategories+"/"+rowCount+"/"+preferredColumns+"/"+maximum+"/world:"+(worldTable!=null);
        bool retained=denseWidthSignatures.TryGetValue(row,out var prior)&&prior==signature&&denseCardWidths.ContainsKey(row);
        float cardWidth=worldTable!=null?maximum:retained?denseCardWidths[row]:maximum;
        float height=160,width=availableWidth,stride=80;
        float x=36+(support?tableWidth*.5f-sideWidth*.5f:-(sideWidth+28)*.5f);
        if(worldTable!=null&&!support)x+=FieldLanes(own).rightExtension*.5f;
        float y=support?(own?-156:156):lands?(own?-312:312):(own?-(HasTwoLandRows(own)?112:130):(HasTwoLandRows(own)?112:130));
        if(!denseFieldRows.TryGetValue(row,out var scroll)||scroll==null) {
            // World lanes page and hit-test whole cards in DenseFieldViewport.
            // Only the legacy canvas faces need a moving pixel clip.
            var clip=Panel(parent,"Dense_"+row,new Vector2(x,y),new Vector2(width,height),worldTable==null?new Color(0,0,0,.035f):Color.clear);
            if(worldTable==null)clip.gameObject.AddComponent<RectMask2D>();
            scroll=clip.gameObject.AddComponent<BattlefieldHorizontalScroll>();
            var content=Rect(clip,"Content",Vector2.zero,new Vector2(width,height));
            content.anchorMin=content.anchorMax=new Vector2(0,1);content.pivot=new Vector2(0,1);
            scroll.viewport=clip;scroll.content=content;
            scroll.gameObject.AddComponent<DenseFieldViewport>();
            denseFieldRows[row]=scroll;
        }
        ((BattlefieldHorizontalScroll)scroll).alignNavigationToVisibleCards=true;
        ((BattlefieldHorizontalScroll)scroll).wheelControlsBoard=worldTable!=null;
        ((BattlefieldHorizontalScroll)scroll).LaneTheme=HudTheme;
        ((BattlefieldHorizontalScroll)scroll).navigationLeftCardGap=roomRow?16:4;
        ((BattlefieldHorizontalScroll)scroll).navigationRightCardGap=roomRow?12:4;
        ((BattlefieldHorizontalScroll)scroll).navigationLeftOutsideAllowance=roomRow?12:0;
        scroll.gameObject.SetActive(true);var viewport=(RectTransform)scroll.transform;
        var contentRect=scroll.content;
        Rect footprint=default;
        var footprints=new Rect[groups.Count];
        float[] columnWidths=null,columnStarts=null;
        bool[] columnRooms=null;
        float packedWidth=0;
        // Usually a single pass. A badge/font change can require a smaller card;
        // keep the settled width so unchanged snapshots do not rebuild its face.
        for(int pass=0;pass<4;pass++) {
            float left=float.PositiveInfinity,right=float.NegativeInfinity,bottom=float.PositiveInfinity,top=float.NegativeInfinity;
            for(int i=0;i<groups.Count;i++) {
                var group=groups[i];var card=group[0];string key=row+":"+card.id;
                float rotation=card.tapped?(own?-12:12):0;
                // Match ordinary world permanents to the square creature
                // anchor, so fitting the mesh does not shrink support cards.
                bool sameAsCreature=worldTable!=null&&!card.room;
                var size=new Vector2(card.room?cardWidth*WorldRoomWidthFactor:cardWidth,
                    sameAsCreature?cardWidth*(card.planeswalker?1.26f:card.battle?.80f:1)
                    :worldTable==null&&card.planeswalker?cardWidth*1.3f:cardWidth*.88f);
                var previous=arenaCards.TryGetValue(key,out var existing)?existing.root.anchoredPosition:Vector2.zero;
                DrawArenaCard(contentRect,card,group.Length,key,row+":"+ArenaPresentationGroupKey(card),previous,size,false,rotation,i);
                var bounds=FieldFootprint(arenaCards[key],own);
                footprints[i]=bounds;
                left=Mathf.Min(left,bounds.xMin);right=Mathf.Max(right,bounds.xMax);bottom=Mathf.Min(bottom,bounds.yMin);top=Mathf.Max(top,bounds.yMax);
            }
            footprint=UnityEngine.Rect.MinMaxRect(left,bottom,right,top);stride=footprint.width+gap;
            if(worldTable!=null){
                int count=Mathf.CeilToInt(groups.Count/(float)rowCount);
                columnWidths=new float[count];columnStarts=new float[count];columnRooms=new bool[count];
                for(int i=0;i<footprints.Length;i++){
                    columnWidths[i/rowCount]=Mathf.Max(columnWidths[i/rowCount],footprints[i].width);
                    columnRooms[i/rowCount]|=groups[i][0].room;
                }
                for(int i=0;i<count;i++){
                    columnStarts[i]=packedWidth;
                    packedWidth+=columnWidths[i];
                    // Support cards fill more of their anchors than creatures
                    // and lands; the latter alone can use a compressed gap.
                    if(i+1<count)packedWidth+=columnRooms[i]||columnRooms[i+1]?WorldRoomGap:support?gap:WorldVisibleGap(cardWidth);
                }
                packedWidth+=edgePadding*2;
                stride=packedWidth/count;
                break;
            }
            if(stride*preferredColumns<=availableWidth+.1f||cardWidth<=48||pass==3)break;
            cardWidth=Mathf.Max(48,cardWidth-(stride-availableWidth/preferredColumns)*.9f-1);
        }
        denseCardWidths[row]=cardWidth;
        denseWidthSignatures[row]=signature;
        int visibleColumns=Mathf.Clamp(Mathf.FloorToInt((availableWidth+.1f)/stride),1,preferredColumns);
        width=worldTable!=null?Mathf.Min(availableWidth,packedWidth):visibleColumns*stride;
        height=rowCount*footprint.height+(rowCount+1)*gap;
        if(support && rowCount==1)height=footprint.height+8;
        if(support && rowCount==2) {
            // Pair only the actual heights in this column. A tall walker in a
            // different, currently hidden column must not stretch short cards.
            height=0;
            for(int i=0;i<footprints.Length;i+=2)
                height=Mathf.Max(height,footprints[i].height+(i+1<footprints.Length?footprints[i+1].height:footprints[i].height)+rowGap+8);
        }
        // Align to the lane's left edge, reserving a full arrow hit area at each end.
        if(lands&&!IsDuelTable){
            float extraLandSpan=Mathf.Max(0,height-FieldLanes(own).cardWidth)*.5f;
            float reduction=WorldMainLaneGapReduction(own,312-(HasTwoLandRows(own)?112:130));
            y+=(own?1:-1)*Mathf.Max(0,reduction-extraLandSpan);
        }
        if(IsDuelTable&&!support)y=DuelRankCenter(own,lands,rowCount);
        x+=-zoneWidth*.5f+32+informationInset+width*.5f;
        viewport.anchoredPosition=new Vector2(x,y);viewport.sizeDelta=new Vector2(width,height);
        int columns=Mathf.CeilToInt(groups.Count/(float)rowCount);
        contentRect.sizeDelta=new Vector2(Mathf.Max(width,worldTable!=null?packedWidth:columns*stride),height);
        var horizontalScroll=(BattlefieldHorizontalScroll)scroll;
        horizontalScroll.Configure(stride);
        if(worldTable!=null)horizontalScroll.ConfigureColumns(columnStarts);
        for(int i=0;i<groups.Count;i++) {
            var card=groups[i][0];var item=arenaCards[row+":"+card.id];
            int column=i/rowCount;
            float columnX=worldTable!=null
                ?columnStarts[column]+edgePadding+(columnWidths[column]-footprints[i].width)*.5f-footprints[i].xMin
                :column*stride+gap*.5f-footprint.xMin;
            var position=new Vector2(-contentRect.rect.width*.5f+columnX,-gap-footprint.yMax-(i%rowCount)*(footprint.height+gap));
            if(support && rowCount==1)position.y=-4-footprint.yMax;
            if(support && rowCount==2) {
                int first=i-i%2;
                float pairHeight=footprints[first].height+(first+1<footprints.Length?footprints[first+1].height:footprints[first].height)+rowGap;
                float pairTop=-(height-pairHeight)*.5f;
                position.y=pairTop-footprints[i].yMax-(i%2==0?0:footprints[first].height+rowGap);
            }
            item.pointer.SetLayout(position,card.tapped?(own?-12:12):0,item.face,false,i);
        }
    }
}
