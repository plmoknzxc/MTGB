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
            b.GetComponent<Image>().color=DeckCompanion.IsSelected(selectedDeck,tile.Entry)?AccentColor:FieldColor;
        }
        string key=JsonUtility.ToJson(selectedDeck)+"/"+GamePreferences.Revision;
        if(key!=companionStatusKey){companionStatusKey=key;companionStatus="";if(DeckCompanion.Has(selectedDeck))StartCoroutine(CheckSideboardCompanion(key));}
        if(sideboardStatusText!=null){
            sideboardStatusText.gameObject.SetActive(true);
            sideboardStatusText.rectTransform.anchoredPosition=new Vector2(0,-457);sideboardStatusText.rectTransform.sizeDelta=new Vector2(1640,62);
            string instructions=selectedDeck?.format=="duelcommander"?DeckCompanion.Text("可用原牌组内的合法主将替换；主牌与主将合计 100 张，标识色须保持不变。","Choose a legal commander from your registered 100 cards. Color identity must stay the same.","登録済みの100枚から統率者を変更できます。固有色は変更できません。"):"";
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
