using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    private enum DeckViewSort { Original, Cost, Type, Shuffle }
    private DeckViewSort deckViewSort;
    private readonly System.Random deckSortRandom=new System.Random();
    private readonly Dictionary<DeckEntry,int> deckShuffleOrder=new Dictionary<DeckEntry,int>();
    private DeckDefinition shuffledDeck;
    private Button deckCostSort,deckTypeSort,deckShuffle;
    private (float,float,int,string,string,string,string,string,int,int,int) deckHeaderLayoutKey;
    private float deckHeaderLayoutHeight;

    private void CreateDeckSortControls()
    {
        if(mainDeckCountText==null)return;
        Button Add(string name,Action action){
            var button=CreateDialogButton(mainDeckCountText.transform,name,"",Vector2.zero,MenuVisualTheme.Raised,action);
            var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=new Vector2(1,.5f);rect.pivot=new Vector2(1,.5f);rect.sizeDelta=new Vector2(80,38);
            PreferenceText.SetSize(button.GetComponentInChildren<Text>(),16);return button;
        }
        deckCostSort=Add("DeckSortCost",()=>SetDeckViewSort(DeckViewSort.Cost));
        deckTypeSort=Add("DeckSortType",()=>SetDeckViewSort(DeckViewSort.Type));
        deckShuffle=Add("DeckShuffle",()=>SetDeckViewSort(DeckViewSort.Shuffle));
        RefreshDeckSortControls();
    }
    private void RefreshDeckSortControls()
    {
        if(deckCostSort==null)return;
        void Label(Button button,string value,bool active){
            var label=button.GetComponentInChildren<Text>();var binding=label.GetComponent<PreferenceText>();binding.Literal=true;label.text=value;
            button.GetComponent<Image>().color=active?MenuVisualTheme.Primary:MenuVisualTheme.Raised;
        }
        Label(deckCostSort,DeckCompanion.Text("按费用","Mana value","マナ総量"),deckViewSort==DeckViewSort.Cost);
        Label(deckTypeSort,DeckCompanion.Text("按类型","Card type","タイプ"),deckViewSort==DeckViewSort.Type);
        Label(deckShuffle,DeckCompanion.Text("打乱","Shuffle","混ぜる"),deckViewSort==DeckViewSort.Shuffle&&ReferenceEquals(shuffledDeck,currentDeck));
    }
    // The count/format stay together; sorting stays at the right edge. A narrow
    // panel gets a second row, with the card viewport reserving its full height.
    private float LayoutDeckHeader(float width,float minimumRowHeight)
    {
        if(mainDeckCountText==null||deckFormatDropdown==null||deckCostSort==null)return minimumRowHeight;
        float gap=Mathf.Clamp(width*.012f,6,12);
        var cost=deckCostSort.GetComponentInChildren<Text>();var type=deckTypeSort.GetComponentInChildren<Text>();var shuffle=deckShuffle.GetComponentInChildren<Text>();
        var key=(width,minimumRowHeight,GamePreferences.Revision,mainDeckCountText.text,deckFormatDropdown.captionText.text,cost.text,type.text,shuffle.text,mainDeckCountText.fontSize,deckFormatDropdown.captionText.fontSize,cost.fontSize);
        if(key==deckHeaderLayoutKey)return deckHeaderLayoutHeight;
        float rowHeight=Mathf.Ceil(Mathf.Max(minimumRowHeight,38,mainDeckCountText.fontSize*1.25f+6,cost.fontSize*1.25f+6,deckFormatDropdown.captionText.fontSize*1.25f+6));
        float costWidth=Mathf.Ceil(Mathf.Max(60,cost.preferredWidth+20));
        float typeWidth=Mathf.Ceil(Mathf.Max(60,type.preferredWidth+20));
        float shuffleWidth=Mathf.Ceil(Mathf.Max(56,shuffle.preferredWidth+20));
        float sortingWidth=costWidth+typeWidth+shuffleWidth+gap*2;
        float countWidth=Mathf.Ceil(mainDeckCountText.preferredWidth);
        float formatWidth=Mathf.Ceil(Mathf.Max(96,deckFormatDropdown.captionText.preferredWidth+46));
        bool secondRow=countWidth+gap*2+formatWidth+sortingWidth+8>width;
        float formatRoom=width-countWidth-gap-(secondRow?0:sortingWidth+gap+8);
        formatWidth=Mathf.Min(formatWidth,Mathf.Max(1,formatRoom));
        mainDeckCountText.alignment=secondRow?TextAnchor.UpperLeft:TextAnchor.MiddleLeft;
        void Position(RectTransform rect,bool right,float x,float y,float itemWidth){
            rect.anchorMin=rect.anchorMax=new Vector2(right?1:0,1);rect.pivot=new Vector2(right?1:0,.5f);
            var position=new Vector2(x,y);var size=new Vector2(itemWidth,rowHeight-4);
            if(rect.anchoredPosition!=position)rect.anchoredPosition=position;
            if(rect.sizeDelta!=size)rect.sizeDelta=size;
        }
        Position((RectTransform)deckFormatDropdown.transform,false,countWidth+gap,-rowHeight*.5f,formatWidth);
        float sortY=-rowHeight*.5f-(secondRow?rowHeight+gap:0);
        Position((RectTransform)deckShuffle.transform,true,-8,sortY,shuffleWidth);
        Position((RectTransform)deckTypeSort.transform,true,-8-shuffleWidth-gap,sortY,typeWidth);
        Position((RectTransform)deckCostSort.transform,true,-8-shuffleWidth-gap-typeWidth-gap,sortY,costWidth);
        FitDeckFormatPopup(width,formatWidth);
        deckHeaderLayoutKey=key;
        return deckHeaderLayoutHeight=rowHeight*(secondRow?2:1)+(secondRow?gap:0);
    }
    private void SetDeckViewSort(DeckViewSort mode)
    {
        if(currentDeck==null)return;
        if(mode==DeckViewSort.Shuffle){
            var main=DeckDisplayEntries(currentDeck.cards).ToList();var side=DeckDisplayEntries(SecondaryDeckEntries).ToList();
            deckShuffleOrder.Clear();shuffledDeck=currentDeck;ShuffleDeckEntries(main);ShuffleDeckEntries(side);
        }
        deckViewSort=mode;
        ReorderDeckTiles(mainDeckContent,currentDeck.cards);ReorderDeckTiles(sideboardContent,SecondaryDeckEntries);
        RefreshDeckSortControls();
    }
    private void ShuffleDeckEntries(List<DeckEntry> entries)
    {
        entries.RemoveAll(e=>e==null);
        var previous=entries.ToArray();
        for(int i=entries.Count-1;i>0;i--){int j=deckSortRandom.Next(i+1);var item=entries[i];entries[i]=entries[j];entries[j]=item;}
        if(entries.Count>1&&entries.SequenceEqual(previous)){var first=entries[0];entries.RemoveAt(0);entries.Add(first);}
        for(int i=0;i<entries.Count;i++)deckShuffleOrder[entries[i]]=i;
    }
    private IEnumerable<DeckEntry> DeckDisplayEntries(List<DeckEntry> entries)
    {
        if(entries==null)return Enumerable.Empty<DeckEntry>();
        if(deckViewSort==DeckViewSort.Original)return entries;
        if(deckViewSort==DeckViewSort.Shuffle)return ReferenceEquals(shuffledDeck,currentDeck)?entries.OrderBy(e=>e!=null&&deckShuffleOrder.TryGetValue(e,out int rank)?rank:int.MaxValue):entries;
        var resolved=entries.Select(e=>(entry:e,card:DeckRepository.ResolveCard(e,cards)));
        if(deckViewSort==DeckViewSort.Cost)return resolved.OrderBy(p=>p.card==null?2:p.card.HasType(CardType.Land)?1:0).ThenByDescending(p=>DeckSortManaValue(p.card)).ThenBy(p=>DeckSortType(p.card)).ThenBy(p=>p.card?.cardName,StringComparer.OrdinalIgnoreCase).ThenBy(p=>p.card?.PrintingKey,StringComparer.OrdinalIgnoreCase).Select(p=>p.entry);
        return resolved.OrderBy(p=>DeckSortType(p.card)).ThenBy(p=>p.card?.cardName,StringComparer.OrdinalIgnoreCase).ThenBy(p=>p.card?.PrintingKey,StringComparer.OrdinalIgnoreCase).Select(p=>p.entry);
    }
    private static int DeckSortType(CardDefinition card)
    {
        if(card==null)return 9;
        if(card.HasType(CardType.Land))return 7;
        CardType[] order={CardType.Creature,CardType.Planeswalker,CardType.Battle,CardType.Instant,CardType.Sorcery,CardType.Enchantment,CardType.Artifact};
        for(int i=0;i<order.Length;i++)if(card.HasType(order[i]))return i;
        return 8;
    }
    private static float DeckSortManaValue(CardDefinition card)
    {
        if(card==null)return float.MaxValue;
        var metadata=CardSearchMetadata.Get(card);if(metadata!=null)return metadata.manaValue;
        // DIY/fallback: X is zero, hybrid/Phyrexian is one, {2/W} is two.
        float value=0;
        foreach(Match match in Regex.Matches(card.manaCost??"",@"\{([^{}]+)\}")){
            string token=match.Groups[1].Value.ToUpperInvariant();
            if(token=="X"||token=="Y"||token=="Z")continue;
            if(float.TryParse(token,NumberStyles.Float,CultureInfo.InvariantCulture,out float number)){value+=Math.Max(0,number);continue;}
            value+=token.StartsWith("2/")?2f:token.Length==2&&token[0]=='H'?0.5f:1f;
        }
        return value;
    }
    private void ReorderDeckTiles(RectTransform parent,List<DeckEntry> entries)
    {
        if(parent==null)return;
        var tiles=parent.GetComponentsInChildren<MainMenuCardTile>().Where(t=>t.transform.parent==parent&&(parent!=sideboardContent||t.Zone==MainMenuCardTileZone.Sideboard)).GroupBy(t=>t.Card.PrintingKey).ToDictionary(g=>g.Key,g=>new Queue<Transform>(g.Select(t=>t.transform)));
        int index=parent==sideboardContent?CommandPrefixCount():0;
        foreach(var entry in DeckDisplayEntries(entries)){
            var card=DeckRepository.ResolveCard(entry,cards);
            if(card!=null&&tiles.TryGetValue(card.PrintingKey,out var queue)&&queue.Count>0)queue.Dequeue().SetSiblingIndex(index++);
        }
    }
}
