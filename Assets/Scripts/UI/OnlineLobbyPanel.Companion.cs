using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class OnlineLobbyPanel
{
    private string companionStatusKey;
    private string companionStatus;
    private void BuildSideboardCompanionControl(Transform parent,DeckEntry entry,CardDefinition card,bool side,Vector2 size)
    {
        if(!side||selectedDeck?.format=="duelcommander"||!DeckCompanion.IsCompanion(card))return;
        var button=AddButton(parent,"SetCompanion",DeckCompanion.Text("行侣","Comp.","相棒"),new Vector2(-20,-size.y*.5f+16),new Vector2(64,28),browserFont,FieldColor);
        MenuVisualTheme.StyleDeckTool(button);
        PreferenceText.SetSize(button.GetComponentInChildren<Text>(),12);
        button.onClick.AddListener(()=>{
            if(ready||OnlineMatchService.Instance?.IsSpectator==true)return;
            selectedDeck.companion=DeckCompanion.IsSelected(selectedDeck,entry)?null:DeckCompanion.Copy(entry);
            RefreshDeckSummary();RefreshSideboardControls(OnlineMatchService.Instance?.LobbyState?.series);
        });
    }
    private void RefreshSideboardCompanion(OnlineSeriesState series)
    {
        if(series?.betweenGames!=true||sideboardPageGroup==null)return;
        foreach(var tile in sideboardPageGroup.GetComponentsInChildren<OnlineSideboardCardTile>()){
            var b=tile.transform.Find("SetCompanion")?.GetComponent<Button>();if(b==null)continue;
            b.interactable=!ready&&OnlineMatchService.Instance?.IsSpectator!=true;
            bool chosen=DeckCompanion.IsSelected(selectedDeck,tile.Entry);
            var theme=MenuThemeCatalog.Current;
            if(b.GetComponent<Image>().color!=(chosen?theme.ActionEdge:theme.ControlEdge))MenuVisualTheme.StyleDeckTool(b,chosen);
        }
        string key=JsonUtility.ToJson(selectedDeck)+"/"+GamePreferences.Revision;
        if(key!=companionStatusKey){companionStatusKey=key;companionStatus="";if(DeckCompanion.Has(selectedDeck))StartCoroutine(CheckSideboardCompanion(key));}
        if(sideboardStatusText!=null){
            sideboardStatusText.gameObject.SetActive(true);
            sideboardStatusText.rectTransform.anchoredPosition=new Vector2(0,-425);sideboardStatusText.rectTransform.sizeDelta=new Vector2(1488,76);
            sideboardStatusText.color=ready?MenuVisualTheme.LibraryAccent:MenuVisualTheme.Stone;
            string instructions=selectedDeck?.format=="duelcommander"?DeckCompanion.Text("可用原牌组内的合法主将替换；主牌与主将合计 100 张，标识色须保持不变。","Choose a legal commander from your registered 100 cards. Color identity must stay the same.","登録済みの100枚から統率者を変更できます。固有色は変更できません。"):"";
            if(string.IsNullOrEmpty(instructions)){
                instructions=ready ? DeckCompanion.Text("牌组已提交 · 已准备","Deck submitted · Ready","デッキ提出済み · 準備完了") : DeckCompanion.Text("拖动卡牌换区，或点选卡牌调整数量。","Drag cards between zones or select a card to change its quantity.","カードをドラッグして移動するか、選択して枚数を調整できます。");
            }
            sideboardStatusText.text=instructions+(string.IsNullOrEmpty(instructions)?"":"\n")+DeckCompanion.Label(selectedDeck,CardDatabaseRepository.LoadAll())+(DeckCompanion.Has(selectedDeck)?"\n"+(string.IsNullOrEmpty(companionStatus)?DeckCompanion.Text("正在检查行侣条件…","Checking companion restriction…","相棒条件を確認中…"):companionStatus):"");
        }
    }
    private IEnumerator CheckSideboardCompanion(string key)
    {
        yield return new WaitForSecondsRealtime(.65f);if(companionStatusKey!=key)yield break;
        var deck=CloneDeck(selectedDeck);System.Threading.Tasks.Task<DeckCompanionInspector.Result> task;
        try{task=DeckCompanionInspector.Inspect(deck);}catch(System.Exception){companionStatus=DeckCompanionInspector.Status(new DeckCompanionInspector.Result{error="engine"},false);yield break;}
        while(!task.IsCompleted){if(companionStatusKey!=key)yield break;yield return null;}
        if(companionStatusKey!=key)yield break;
        var result=task.IsFaulted?new DeckCompanionInspector.Result{error="engine"}:task.Result;
        companionStatus=DeckCompanionInspector.Status(result,result.Allows(deck));RefreshSideboardCompanion(OnlineMatchService.Instance?.LobbyState?.series);
    }
}
