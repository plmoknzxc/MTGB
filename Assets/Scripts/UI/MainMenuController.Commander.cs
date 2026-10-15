using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    bool IsCommandZoneEditor=>DeckCommander.IsFormat(currentDeck?.format);
    List<DeckEntry> SecondaryDeckEntries=>currentDeck?.sideboard;
    RectTransform commandZoneDrop;
    int CommandPrefixCount()=>IsCommandZoneEditor?(currentDeck.commanders?.Count??0)+(commandZoneDrop!=null?1:0):0;
    void RefreshMainDeckHeading()
    {
        if(mainDeckCountText==null)return;
        mainDeckCountText.text=DeckCompanion.Text("主牌  ","Main  ","メイン  ")+DeckRepository.GetTotalCount(currentDeck,false);
        var binding=mainDeckCountText.GetComponent<PreferenceText>();if(binding!=null)binding.Literal=true;
    }
    void RefreshSecondaryDeckHeading()
    {
        if(sideboardCountText==null)return;
        int count=(currentDeck?.sideboard?.Sum(e=>Math.Max(0,e?.count??0))??0)+(IsCommandZoneEditor?currentDeck.commanders.Sum(e=>Math.Max(0,e?.count??0)):0);
        sideboardCountText.text=(IsCommandZoneEditor?DeckCompanion.Text("统帅区/备牌  ","Command / Side  ","統率領域/サイド  "):DeckCompanion.Text("备牌  ","Sideboard  ","サイドボード  "))+count;
        PreferenceText.SetSize(sideboardCountText,IsCommandZoneEditor?20:24);
        var binding=sideboardCountText.GetComponent<PreferenceText>();if(binding!=null)binding.Literal=true;
    }
    void PopulateCommandZone()
    {
        commandZoneDrop=null;
        if(!IsCommandZoneEditor||sideboardContent==null)return;
        PopulateDeckGrid(sideboardContent,currentDeck.commanders,MainMenuCardTileZone.Commander);
        if(currentDeck.commanders.Sum(e=>e.count)>=2)return;
        if(currentDeck.commanders.Count==1&&DeckEditorRules.Get(DeckRepository.ResolveCard(currentDeck.commanders[0],cards))?.partners?.Length is not >0)return;
        var go=new GameObject("CommandZoneDrop",typeof(RectTransform),typeof(Image),typeof(Outline));
        go.transform.SetParent(sideboardContent,false);commandZoneDrop=(RectTransform)go.transform;
        commandZoneDrop.sizeDelta=sideboardContent.GetComponent<GridLayoutGroup>().cellSize;
        go.GetComponent<Image>().color=new Color(.16f,.14f,.08f,.9f);go.GetComponent<Image>().raycastTarget=false;
        go.GetComponent<Outline>().effectColor=MenuVisualTheme.Primary;
        var label=AddText(go.transform,"Label",DeckCompanion.Text("统帅区\n拖入主将","Command zone\nDrop commander","統率領域\n統率者をここへ"),18,MenuVisualTheme.Paper,TextAnchor.MiddleCenter);
        Stretch(label.rectTransform,8,8,8,8);label.raycastTarget=false;
    }
    void RequestCommandZone(CardDefinition card,bool fromMain)
    {
        if(card==null||!IsCommandZoneEditor||currentDeck.commanders.Any(e=>e.PrintingKey==card.PrintingKey))return;
        var existing=currentDeck.commanders.Where(e=>e.count>0).Select(e=>DeckRepository.ResolveCard(e,cards)).ToArray();
        bool legal=DeckEditorRules.CanCommand(card,currentDeck.format);
        bool partner=existing.Length==1&&DeckEditorRules.CanPartner(existing[0],card);
        if(!legal||existing.Length>0&&!partner){SetStatus(DeckCompanion.Text("这张牌不能担任当前主将或共同主将。","This card cannot be this deck's commander or partner.","このデッキの統率者、または共闘の組み合わせとして使えません。"));return;}
        if(!DeckCommander.Select(currentDeck,card,new[]{card.PrintingKey},existing.Length>0,partner?new[]{card.PrintingKey}:Array.Empty<string>(),fromMain))return;
        MarkDeckDirty();RefreshDeckGrids();RefreshSelectedCardInfo();
        SetStatus(DeckCompanion.Text("已放入统帅区并指定为主将。","Placed in the command zone and designated as commander.","統率領域へ移し、統率者に指定しました。"));
    }
    void MoveCommanderOut(CardDefinition card,bool toMain)
    {
        var entry=currentDeck?.commanders?.FirstOrDefault(e=>e.PrintingKey==card?.PrintingKey);
        if(entry==null)return;
        if(toMain)DeckCommander.MoveToMain(currentDeck,entry);else currentDeck.commanders.Remove(entry);
        MarkDeckDirty();RefreshDeckGrids();RefreshSelectedCardInfo();
    }
    void RefreshCommanderEntry()
    {
        RefreshMainDeckHeading();
        RefreshSecondaryDeckHeading();
        if(commandZoneDrop!=null)commandZoneDrop.GetComponentInChildren<Text>().text=DeckCompanion.Text("统帅区\n拖入主将","Command zone\nDrop commander","統率領域\n統率者をここへ");
        if(addSideboardButton!=null)SetText(addSideboardButton.GetComponentInChildren<Text>(),DeckCompanion.Text("加入备牌","Add to sideboard","サイドに追加"));
    }
    IEnumerator AdaptCommanderSideboard(DeckDefinition deck)
    {
        if(DeckCommander.Has(deck)||DeckRepository.GetTotalCount(deck,true)!=1)yield break;
        var entry=deck.sideboard.First(e=>e.count>0);
        if(!DeckEditorRules.CanCommand(DeckRepository.ResolveCard(entry,cards),deck.format))yield break;
        string key=entry.PrintingKey,format=deck.format;
        if(!DeckCommander.TryAdapt(deck,new[]{key}))yield break;
        bool savedMigration=false;
        if(loadedDeckIndex>=0&&loadedDeckIndex<decks.Count){
            var saved=CloneDeck(decks[loadedDeckIndex]);
            if(saved.format==format&&saved.sideboard?.Any(e=>e.PrintingKey==key&&e.count==1)==true&&DeckCommander.TryAdapt(saved,new[]{key})){
                try{DeckRepository.SaveReplacing(decks[loadedDeckIndex].name,saved);decks[loadedDeckIndex]=saved;deckDirectoryRevision=DeckRepository.DirectoryRevision();savedMigration=true;}catch(Exception){}
            }
        }
        if(!savedMigration)MarkDeckDirty();RefreshDeckGrids();
    }
    int CountTileCopies(CardDefinition card,MainMenuCardTileZone zone)=>zone==MainMenuCardTileZone.Commander?
        currentDeck?.commanders?.Where(e=>EntryMatchesCard(e,card)).Sum(e=>Math.Max(0,e.count))??0:CountCardCopies(card,zone==MainMenuCardTileZone.Sideboard);
    void MoveDeckCopies(CardDefinition card,int requested,bool fromSideboard)
    {
        var from=fromSideboard?currentDeck.sideboard:currentDeck.cards;
        var to=fromSideboard?currentDeck.cards:currentDeck.sideboard;
        var source=from.FirstOrDefault(e=>EntryMatchesCard(e,card));if(source==null)return;
        int count=Math.Min(requested,source.count);if(count<=0)return;
        var target=to.FirstOrDefault(e=>EntryMatchesCard(e,card));
        if(target==null){target=DeckCompanion.Copy(source);target.count=0;to.Add(target);}
        target.count+=count;source.count-=count;if(source.count<=0)from.Remove(source);
        // Moving a loaded, over-limit deck between sections must not destroy copies.
        DeckCompanion.ClearMissing(currentDeck);
    }
    void AddDeckRole(MainMenuCardTile tile)
    {
        var old=tile.transform.Find("RoleBackground");
        if(!IsCommandZoneEditor||tile.Zone==MainMenuCardTileZone.Collection||tile.Zone==MainMenuCardTileZone.MainDeck){if(old!=null)old.gameObject.SetActive(false);return;}
        string role=tile.Zone==MainMenuCardTileZone.Commander?DeckCompanion.Text("主将","Commander","統率者"):
            DeckCompanion.IsSelected(currentDeck,new DeckEntry{setCode=tile.Card.setCode,collectorNumber=tile.Card.CollectorNumber})?DeckCompanion.Text("行侣","Companion","相棒"):DeckCompanion.Text("备牌","Sideboard","サイド");
        var bg=old!=null?old.GetComponent<Image>():AddImage(tile.transform,"RoleBackground",Color.white);bg.gameObject.SetActive(true);
        bg.color=tile.Zone==MainMenuCardTileZone.Commander?new Color(.40f,.29f,.08f,.96f):new Color(.08f,.2f,.3f,.96f);
        bg.rectTransform.anchorMin=new Vector2(0,0);bg.rectTransform.anchorMax=new Vector2(1,0);bg.rectTransform.pivot=new Vector2(.5f,0);bg.rectTransform.sizeDelta=new Vector2(-8,24);bg.rectTransform.anchoredPosition=new Vector2(0,4);bg.raycastTarget=false;
        var text=bg.GetComponentInChildren<Text>()??AddText(bg.transform,"DeckRole",role,14,Color.white,TextAnchor.MiddleCenter);text.text=role;Stretch(text.rectTransform,0,0,0,0);text.raycastTarget=false;
    }
}
