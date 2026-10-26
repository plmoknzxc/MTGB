using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class OnlineLobbyPanel
{
    private List<DeckEntry> SideboardDisplayEntries=>selectedDeck?.format=="duelcommander"?selectedDeck.commanders:selectedDeck?.sideboard;
    private void RefreshFormatRules()
    {
        if(cardFormatDropdown==null||lifeInput==null)return;
        string format=DeckFormatLegality.OnlineKeys[cardFormatDropdown.value];
        bool command=format is "duelcommander" or "brawl",editable=cardFormatDropdown.interactable;
        if(command)lifeInput.SetTextWithoutNotify(format=="brawl"?"25":"20");
        lifeInput.interactable=editable&&!command;
        if(format=="brawl")formatDropdown.SetValueWithoutNotify(0);
        formatDropdown.interactable=editable&&format!="brawl";
        if(command&&customContentToggle!=null)customContentToggle.SetIsOnWithoutNotify(false);
        if(customContentToggle!=null)customContentToggle.interactable=editable&&!command;
        UpdateAnteLabel();
    }
    private async void MoveCommanderCard(DeckEntry entry,bool toCommand)
    {
        var deck=selectedDeck;
        try{await DeckEditorRules.LoadAsync();}
        catch(Exception){sideboardStatusText.text=DeckCompanion.Text("无法读取主将资料，请重试。","Commander data unavailable; retry.","統率者データを読み込めません。");return;}
        if(this==null||deck!=selectedDeck||ready||OnlineMatchService.Instance?.IsSpectator==true||OnlineMatchService.Instance?.LobbyState?.series?.betweenGames!=true)return;
        var database=CardDatabaseRepository.LoadAll();
        var source=toCommand?deck.cards:deck.commanders;
        var destination=toCommand?deck.commanders:deck.cards;
        entry=source.Find(e=>SamePrinting(e,entry));if(entry==null)return;
        var card=DeckRepository.ResolveCard(entry,database);
        if(toCommand&&(!DeckEditorRules.CanCommand(card,"duelcommander")||deck.commanders.Count>=2
            ||deck.commanders.Count==1&&!DeckEditorRules.CanPartner(DeckRepository.ResolveCard(deck.commanders[0],database),card))){
            sideboardStatusText.text=DeckCompanion.Text("请先将旧主将移回主牌，再放入新主将；两位主将必须能合法共同担任。","Return the old commander to the deck before replacing it. Two commanders must form a legal pair.","旧統率者をメインへ戻してから変更してください。2枚は適正な組み合わせが必要です。");return;
        }
        var existing=destination.Find(e=>SamePrinting(e,entry));
        if(existing==null){existing=CloneEntry(entry);existing.count=0;destination.Add(existing);}
        existing.count++;entry.count--;if(entry.count==0)source.Remove(entry);
        DeckCopyOrder.Synchronize(deck);RefreshDeckSummary();RefreshSideboardControls(OnlineMatchService.Instance?.LobbyState?.series);
    }
}
