using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class OnlineLobbyPanel
{
    private static string StartingLifeLabel(string mode, int life) => mode == OnlineRoomConfig.Loki
        ? DeckCompanion.Text("洛基 60 · 英雄共享 60","Loki 60 · Heroes share 60","ロキ60 · 英雄は60を共有")
        : DeckCompanion.Text("初始生命 ","Starting life ","初期ライフ ")+life;
    private List<DeckEntry> SideboardDisplayEntries=>selectedDeck?.format=="duelcommander"?selectedDeck.commanders:selectedDeck?.sideboard;
    private void OnSetupFormatChanged()
    {
        if (DeckCommander.IsFormat(SetupCardFormat)) {
            if (lifeInput.interactable) freeStartingLife = lifeInput.text;
        } else if (!lifeInput.interactable) {
            lifeInput.SetTextWithoutNotify(freeStartingLife);
        }
        RefreshFormatRules();
    }

    private void RefreshFormatRules()
    {
        if(cardFormatDropdown==null||lifeInput==null)return;
        string format=OnlineMatchService.Instance?.LobbyState?.config?.formatId ?? SetupCardFormat;
        bool showBracket=format=="commander";
        roomEventLabel.gameObject.SetActive(showBracket);
        roomEventDropdown.gameObject.SetActive(showBracket);
        if (!showBracket) roomEventDropdown.SetValueWithoutNotify(0);
        roomEventHelp.text=EventKeys[roomEventDropdown.value] switch {
            OnlineRoomConfig.Arkenstone => DeckCompanion.Text("以战斗伤害夺宝。持有者的生物得 +1/+1，结束步骤抓一张牌。\n持有者在维持时得 1 点胜利点，累计 3 点获胜。","Claim the Arkenstone through combat damage. Its holder buffs their creatures and draws at end step. Earn a point each upkeep. Three points win.","戦闘ダメージでアーケン石を奪い合う。所持者のクリーチャーは+1/+1。終了時に1枚引き、アップキープに1点を得る。計3点で勝利。"),
            OnlineRoomConfig.Nauctis => DeckCompanion.Text("节选从广袤海域开始。各自坟墓场有至少20张永久物牌时进入雄伟大洋。","This excerpt starts in the preceding region. Reach the Grand Marine with 20 permanent cards in your own graveyard.","直前の海域から開始。自分の墓地にパーマネント・カードが20枚以上あると次の海域へ進む。"),
            OnlineRoomConfig.Loki => DeckCompanion.Text("洛基以 60 点生命先手，每回合选择诡计。英雄共享 60 点生命和回合，可替队友阻挡。\n清栈后轮到发起者的下家。英雄全员确认前可撤回，确认后洛基仍可响应。","Loki starts at 60, goes first and chooses tricks. Heroes share 60 life, turns and blocks. Once a hero’s stack clears, the next hero acts. Heroes vote to end each step and may cancel before unanimity. Loki can still respond.","ロキはライフ60点で先攻し、毎ターン策略を選ぶ。英雄はライフ60点とターンを共有し、仲間を守れる。スタックが空になったら起点の次の英雄が行動する。英雄全員が終了に同意するまで撤回でき、ロキも対応できる。"),
            _ => ""
        };
        roomEventDropdown.interactable=cardFormatDropdown.interactable;
        roomEventHelp.gameObject.SetActive(showBracket && roomEventDropdown.value!=0);
        LayoutCreationForm(showBracket);
        roomBracketLabel.gameObject.SetActive(showBracket);
        roomBracketDropdown.gameObject.SetActive(showBracket);
        roomSetupHeading.text = showBracket
            ? DeckCompanion.Text("建立四人房间","Create a four-player room","4人部屋を作成")
            : DeckCompanion.Text("建立双人房间","Create a two-player room","2人部屋を作成");
        bool command=DeckCommander.IsFormat(format),editable=cardFormatDropdown.interactable;
        if(command)lifeInput.SetTextWithoutNotify(format=="commander"?"40":format=="brawl"?"25":"20");
        if(showBracket && EventKeys[roomEventDropdown.value]==OnlineRoomConfig.Loki)lifeInput.SetTextWithoutNotify("60");
        lifeInput.interactable=editable&&!command;
        if(format is "commander" or "brawl")formatDropdown.SetValueWithoutNotify(0);
        formatDropdown.interactable=editable&&format!="brawl"&&format!="commander";
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
