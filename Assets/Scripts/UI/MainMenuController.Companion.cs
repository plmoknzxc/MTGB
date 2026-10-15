using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    private Button companionEntry;
    private bool companionDialogOpen;
    private int companionPage;
    private CardDefinition[][] companionCandidates;
    private int companionStatusRevision;
    private string companionStatusKey;
    private void CreateCompanionDeckEntry()
    {
        if(sideboardCountText==null)return;
        companionEntry=CreateDialogButton(sideboardCountText.transform,"CompanionDeckButton",DeckCompanion.Text("行侣","Companion","相棒"),Vector2.zero,MenuVisualTheme.Raised,RenderCompanionDeck);
        var r=(RectTransform)companionEntry.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(1,.5f);r.anchoredPosition=new Vector2(-164,0);r.sizeDelta=new Vector2(112,38);
        PreferenceText.SetSize(companionEntry.GetComponentInChildren<Text>(),17);
    }
    private void RefreshCompanionEntry()
    {
        if(companionEntry==null)return;
        ((RectTransform)companionEntry.transform).sizeDelta=new Vector2(GamePreferences.Language=="en-US"?160:112,38);
        companionEntry.GetComponentInChildren<Text>().text=DeckCompanion.Text("行侣","Companion","相棒")+(DeckCompanion.Has(currentDeck)?" ✓":"");
        if(companionDialogOpen&&exchangeDialog!=null&&exchangeDialog.name=="CompanionDeck")RenderCompanionDeck();
    }
    private void RenderCompanionDeck()
    {
        if(currentDeck==null)return;
        var panel=PickerPanel("CompanionDeck",DeckCompanion.Text("选择行侣","Choose a companion","相棒を指定"),new Vector2(1120,1020));exchangeDialog.name="CompanionDeck";companionDialogOpen=true;
        string statusKey=JsonUtility.ToJson(currentDeck)+":"+GamePreferences.Revision;
        var oldStatus=panel.Find("CompanionStatus")?.GetComponent<Text>();
        bool checkStatus=oldStatus==null||companionStatusKey!=statusKey;
        var status=PickerText(panel,"CompanionStatus",checkStatus?DeckCompanion.Label(currentDeck,cards):oldStatus.text,18,checkStatus?MenuVisualTheme.Paper:oldStatus.color,TextAnchor.MiddleCenter);Place(status.rectTransform,new Vector2(0,412),new Vector2(1040,56));
        string noteText=DeckCompanion.Has(currentDeck)?DeckCompanion.Condition(DeckRepository.ResolveCard(currentDeck.companion,cards)):
            DeckCommander.IsFormat(currentDeck.format)?DeckCompanion.Text("行侣从游戏外指定，不属于统帅区；只能指定一张。","A companion is designated outside the game, not in the command zone. Choose one.","相棒は統率領域ではなくゲーム外から１枚指定します。"):
            DeckCompanion.Text("行侣属于备牌，占用一个备牌名额；只能指定一张。","A companion is one sideboard card. You may designate one.","相棒はサイドボードの１枚です。指定は１枚までです。");
        var note=PickerRuleText(panel,"CompanionNote",noteText,17,MenuVisualTheme.Muted);Place(note.rectTransform,new Vector2(0,335),new Vector2(1040,80));
        if(companionCandidates==null)companionCandidates=cards.Where(c=>!c.forgeAmbiguous && (c.description??"").IndexOf("Companion",System.StringComparison.OrdinalIgnoreCase)>=0).Where(DeckCompanion.IsCompanion).GroupBy(c=>c.cardName).OrderBy(g=>g.Key).Select(g=>g.ToArray()).ToArray();
        var sideKeys=new System.Collections.Generic.HashSet<string>(currentDeck.sideboard.Select(e=>e.PrintingKey));
        var mainKeys=new System.Collections.Generic.HashSet<string>(currentDeck.cards.Select(e=>e.PrintingKey));
        var choices=companionCandidates.Select(g=>g.FirstOrDefault(c=>sideKeys.Contains(c.PrintingKey))??g.FirstOrDefault(c=>mainKeys.Contains(c.PrintingKey))??g[0]).ToArray();
        const int pageSize=6;
        int pages=Mathf.Max(1,(choices.Length+pageSize-1)/pageSize);companionPage=Mathf.Clamp(companionPage,0,pages-1);
        for(int i=0;i<pageSize&&companionPage*pageSize+i<choices.Length;i++){
            var card=choices[companionPage*pageSize+i];float x=(i%3-1)*340,y=i<3?170:-180;
            var art=PickerImage(panel,"CompanionCard_"+i,Color.white);Place(art.rectTransform,new Vector2(x,y),new Vector2(156,218));PickerArt(art,card);art.preserveAspect=true;
            var label=PickerText(panel,"CompanionName_"+i,GamePreferences.CardName(card),20,MenuVisualTheme.Paper,TextAnchor.MiddleCenter);Place(label.rectTransform,new Vector2(x,y-154),new Vector2(320,80));
            bool selected=DeckCompanion.IsSelected(currentDeck,new DeckEntry{setCode=card.setCode,collectorNumber=card.CollectorNumber});
            PickerButton(panel,"ChooseCompanion_"+i,selected?DeckCompanion.Text("已指定","Designated","指定済み"):DeckCompanion.Text("设为行侣","Designate","相棒に指定"),new Vector2(x,y-219),new Vector2(304,42),()=>{
                if(DeckCompanion.Select(currentDeck,card,cards)){MarkDeckDirty();RefreshDeckGrids();}else SetStatus(DeckCompanion.Text("无法将该牌加入备牌。","Cannot add this card to the sideboard.","このカードをサイドボードに追加できません。"));
            },!selected);
        }
        PickerButton(panel,"ClearCompanion",DeckCompanion.Text("不使用行侣","No companion","相棒を使わない"),new Vector2(-310,-459),new Vector2(300,42),()=>{currentDeck.companion=null;MarkDeckDirty();RefreshDeckGrids();});
        PickerButton(panel,"CloseCompanion",DeckCompanion.Text("完成","Done","完了"),new Vector2(310,-459),new Vector2(300,42),()=>{companionDialogOpen=false;CloseDeckExchange();});
        PickerButton(panel,"CompanionPrevious","‹",new Vector2(-90,-459),new Vector2(50,42),()=>{companionPage--;RenderCompanionDeck();},companionPage>0);
        PickerButton(panel,"CompanionNext","›",new Vector2(90,-459),new Vector2(50,42),()=>{companionPage++;RenderCompanionDeck();},companionPage+1<pages);
        var page=PickerText(panel,"CompanionPage",(companionPage+1)+" / "+pages,16,MenuVisualTheme.Muted,TextAnchor.MiddleCenter);Place(page.rectTransform,new Vector2(0,-459),new Vector2(80,42));
        FinishPicker(panel);
        if(checkStatus){
            companionStatusKey=statusKey;
            int revision=++companionStatusRevision;
            if(DeckCompanion.Has(currentDeck))StartCoroutine(CheckCompanionStatus(status,currentDeck,revision));
        }
    }
    private IEnumerator CheckCompanionStatus(Text status,DeckDefinition deck,int revision)
    {
        string key=JsonUtility.ToJson(deck);status.text=DeckCompanion.Label(deck,cards)+"\n"+DeckCompanion.Text("正在检查行侣条件…","Checking companion restriction…","相棒条件を確認中…");
        yield return new WaitForSecondsRealtime(.45f);
        if(revision!=companionStatusRevision||status==null||!companionDialogOpen||JsonUtility.ToJson(currentDeck)!=key)yield break;
        System.Threading.Tasks.Task<DeckCompanionInspector.Result> task;
        try{task=DeckCompanionInspector.Inspect(deck);}catch(System.Exception){status.text=DeckCompanionInspector.Status(new DeckCompanionInspector.Result{error="engine"},false);yield break;}
        while(!task.IsCompleted){if(revision!=companionStatusRevision||status==null)yield break;yield return null;}
        if(revision!=companionStatusRevision||status==null||JsonUtility.ToJson(currentDeck)!=key)yield break;
        var result=task.IsFaulted?new DeckCompanionInspector.Result{error="engine"}:task.Result;
        status.text=DeckCompanion.Label(deck,cards)+"\n"+DeckCompanionInspector.Status(result,result.Allows(deck));
        status.color=result.error!=null?MenuVisualTheme.Muted:result.Allows(deck)?MenuVisualTheme.Paper:MenuVisualTheme.Accent;
    }
}
