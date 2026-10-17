using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private readonly Dictionary<string,ScrollRect> denseFieldRows=new Dictionary<string,ScrollRect>();
    private readonly Dictionary<string,float> denseCardWidths=new Dictionary<string,float>();
    private readonly Dictionary<string,string> denseWidthSignatures=new Dictionary<string,string>();
    private bool HasTwoLandRows(bool own)
    {
        var player=view?.players?.FirstOrDefault(p=>own?p.seat==seat:p.seat!=seat);
        string row=own?"YourLands":"OpponentLands";
        int groups=ArenaVisualGroups((player?.battlefield??Array.Empty<ForgeCardView>()).Where(c=>c.land&&!c.creature).ToArray(),row).Count;
        float tableWidth=Mathf.Max(320,Mathf.Min(1320,surface.rect.width-560));
        float available=tableWidth-Mathf.Min(500,tableWidth*.38f)-28;
        int columns=Mathf.Clamp(Mathf.FloorToInt((available+8)/(144*.88f+8)),1,6);
        return groups>columns;
    }
    private void SetDenseArenaRowVisible(string row,bool visible)
    {
        if(denseFieldRows.TryGetValue(row,out var scroll)&&scroll!=null)scroll.gameObject.SetActive(visible);
    }
    // Pack populated regions from each hand towards the midline. All three
    // own independent horizontal offsets and use their measured full card bounds.
    private void ArrangeSupportRegions(bool own)
    {
        string prefix=own?"Your":"Opponent";
        float edge=Mathf.Min(370,surface.rect.height*.5f-140);
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
    private void LayoutDenseArenaRow(RectTransform parent,List<ForgeCardView[]> groups,string row,bool own,bool lands,bool support,float tableWidth,float sideWidth,float available)
    {
        const float gap=8;
        int rowCount=Math.Min(groups.Count,row.Contains("Planeswalkers") || row.Contains("Battles")?1:2);
        int preferredColumns=Mathf.Clamp(Mathf.CeilToInt(groups.Count/(float)rowCount),1,support?5:6);
        float rowGap=support?5:gap;
        float zoneWidth=support?sideWidth:available;
        float availableWidth=Mathf.Max(80,zoneWidth-64);
        string signature=availableWidth+"/"+preferredColumns+"/"+GamePreferences.Revision+"/"+groups.Max(g=>g.Length)+"/"+groups.Any(g=>g[0].room)+"/"+groups.Any(g=>g[0].tapped)+"/"+groups.Any(g=>g[0].planeswalker);
        // Support lanes share the vertical space between midline and hand.
        // Reserve a row for every other populated support category on this side.
        int supportRows=0;
        if(support){var player=view?.players?.FirstOrDefault(p=>p.seat==(own?seat:(seat==1?2:1)));var field=player?.battlefield??Array.Empty<ForgeCardView>();
            supportRows=(field.Any(c=>c.battle)?1:0)+(field.Any(c=>c.planeswalker)?1:0)+(field.Any(c=>!c.land&&!c.creature&&!c.battle&&!c.planeswalker)?2:0);}
        float maximum=support?Mathf.Min(144*.88f,supportRows>2?272f/supportRows/1.3f:144*.88f):lands?112*.88f:HasTwoLandRows(own)?112*.88f:144*.88f;
        signature+="/"+maximum;
        bool retained=denseWidthSignatures.TryGetValue(row,out var prior)&&prior==signature&&denseCardWidths.ContainsKey(row);
        float cardWidth=retained?denseCardWidths[row]:maximum;
        float height=160,width=availableWidth,stride=80;
        float x=36+(support?tableWidth*.5f-sideWidth*.5f:-(sideWidth+28)*.5f);
        float y=support?(own?-156:156):lands?(own?-312:312):(own?-(HasTwoLandRows(own)?112:130):(HasTwoLandRows(own)?112:130));
        if(!denseFieldRows.TryGetValue(row,out var scroll)||scroll==null) {
            var clip=Panel(parent,"Dense_"+row,new Vector2(x,y),new Vector2(width,height),new Color(0,0,0,.035f));
            clip.gameObject.AddComponent<RectMask2D>();
            scroll=clip.gameObject.AddComponent<BattlefieldHorizontalScroll>();
            var content=Rect(clip,"Content",Vector2.zero,new Vector2(width,height));
            content.anchorMin=content.anchorMax=new Vector2(0,1);content.pivot=new Vector2(0,1);
            scroll.viewport=clip;scroll.content=content;
            scroll.GetComponent<Image>().color=new Color(0,0,0,.035f);
            scroll.gameObject.AddComponent<DenseFieldViewport>();
            denseFieldRows[row]=scroll;
        }
        scroll.gameObject.SetActive(true);var viewport=(RectTransform)scroll.transform;
        var contentRect=scroll.content;
        Rect footprint=default;
        var footprints=new Rect[groups.Count];
        // Usually a single pass. A badge/font change can require a smaller card;
        // keep the settled width so unchanged snapshots do not rebuild its face.
        for(int pass=0;pass<4;pass++) {
            float left=float.PositiveInfinity,right=float.NegativeInfinity,bottom=float.PositiveInfinity,top=float.NegativeInfinity;
            for(int i=0;i<groups.Count;i++) {
                var group=groups[i];var card=group[0];string key=row+":"+card.id;
                float rotation=card.tapped?(own?-12:12):0;
                var size=new Vector2(card.room?cardWidth*1.7f:cardWidth,card.planeswalker?cardWidth*1.3f:cardWidth*.88f);
                var previous=arenaCards.TryGetValue(key,out var existing)?existing.root.anchoredPosition:Vector2.zero;
                DrawArenaCard(contentRect,card,group.Length,key,row+":"+ArenaGroupKey(card),previous,size,false,rotation,i);
                var bounds=DenseFieldViewport.LayoutFootprint(arenaCards[key].root,rotation);
                footprints[i]=bounds;
                left=Mathf.Min(left,bounds.xMin);right=Mathf.Max(right,bounds.xMax);bottom=Mathf.Min(bottom,bounds.yMin);top=Mathf.Max(top,bounds.yMax);
            }
            footprint=UnityEngine.Rect.MinMaxRect(left,bottom,right,top);stride=footprint.width+gap;
            if(stride*preferredColumns<=availableWidth+.1f||cardWidth<=48||pass==3)break;
            cardWidth=Mathf.Max(48,cardWidth-(stride-availableWidth/preferredColumns)*.9f-1);
        }
        denseCardWidths[row]=cardWidth;
        denseWidthSignatures[row]=signature;
        int visibleColumns=Mathf.Clamp(Mathf.FloorToInt((availableWidth+.1f)/stride),1,preferredColumns);
        width=visibleColumns*stride;
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
        x+=-zoneWidth*.5f+32+width*.5f;
        viewport.anchoredPosition=new Vector2(x,y);viewport.sizeDelta=new Vector2(width,height);
        int columns=Mathf.CeilToInt(groups.Count/(float)rowCount);
        contentRect.sizeDelta=new Vector2(Mathf.Max(width,columns*stride),height);
        ((BattlefieldHorizontalScroll)scroll).Configure(stride);
        for(int i=0;i<groups.Count;i++) {
            var card=groups[i][0];var item=arenaCards[row+":"+card.id];
            var position=new Vector2(-contentRect.rect.width*.5f+(i/rowCount)*stride+gap*.5f-footprint.xMin,-gap-footprint.yMax-(i%rowCount)*(footprint.height+gap));
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
