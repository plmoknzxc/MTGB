using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class OnlineLobbyPanel
{
    private RectTransform aiControls;
    private Dropdown aiDeckDropdown;
    private Button applyAiButton, removeAiButton;
    private Text aiHelp;
    private string aiSelectedDeckName;

    private void CreateRoomAiControls(Transform parent, Font font)
    {
        aiControls=ReplayWidgets.Rect(parent,"RoomAiControls",0,-10,648,104);
        aiHelp=AddText(aiControls,"Help","",new Vector2(0,28),new Vector2(648,40),16,font,TextAnchor.MiddleLeft,MutedTextColor);
        aiDeckDropdown=AddDropdown(aiControls,"AiDeckDropdown",new Vector2(-164,-26),new Vector2(320,48),font,new List<string>{"AI"});
        aiDeckDropdown.onValueChanged.AddListener(index=>{if(index>=0&&index<availableDecks.Count)aiSelectedDeckName=availableDecks[index].name;});
        applyAiButton=AddButton(aiControls,"ApplyAi","AI",new Vector2(84,-26),new Vector2(152,48),font,AccentColor);
        removeAiButton=AddButton(aiControls,"RemoveAi","AI",new Vector2(248,-26),new Vector2(152,48),font,FieldColor);
        foreach(var button in new[]{applyAiButton,removeAiButton})PreferenceText.SetSize(button.GetComponentInChildren<Text>(),17);
        applyAiButton.onClick.AddListener(()=>{
            int index=aiDeckDropdown.value;
            if(index>=0&&index<availableDecks.Count)OnlineMatchService.Instance?.ConfigureRoomAi(CloneDeck(availableDecks[index]));
        });
        removeAiButton.onClick.AddListener(()=>OnlineMatchService.Instance?.ConfigureRoomAi(null,true));
    }

    private void ReloadAiDeckChoices()
    {
        if(aiDeckDropdown==null)return;
        aiDeckDropdown.ClearOptions();
        aiDeckDropdown.AddOptions(availableDecks.Count>0?availableDecks.Select(d=>d.name).ToList():new List<string>{DeckCompanion.Text("没有已保存卡组","No saved decks","保存済みデッキなし")});
        int index=Mathf.Max(0,availableDecks.FindIndex(d=>d.name==aiSelectedDeckName));
        aiDeckDropdown.SetValueWithoutNotify(index);aiDeckDropdown.RefreshShownValue();
        if(availableDecks.Count>0)aiSelectedDeckName=availableDecks[index].name;
    }

    private void RefreshRoomAiControls(OnlineMatchService service)
    {
        if(aiControls==null)return;
        var lobby=service?.LobbyState;
        bool host=service?.HasJoinedLobby==true&&!service.IsSpectator&&lobby.hostSeat==service.LocalSeat;
        bool editable=host&&!service.IsInMatch&&lobby.series?.betweenGames!=true&&lobby.playDrawChooserSeat==0&&service.EndReview==null;
        var opponent=lobby?.players?.FirstOrDefault(p=>p.seat<=2&&p.seat!=service.LocalSeat);
        bool ai=opponent?.isAi==true;
        aiControls.gameObject.SetActive(host);
        var prep=aiControls.parent;
        Place((RectTransform)prep.Find("SelectedDeckSummary"),new Vector2(0,host?76:36),new Vector2(648,host?64:112));
        Place(deckText.rectTransform,Vector2.zero,new Vector2(600,host?56:82));
        var info=prep.Find("PreparationInfo");
        Place((RectTransform)info,new Vector2(0,host?-146:-127),new Vector2(648,host?132:174));
        Place((RectTransform)info.Find("Title"),new Vector2(0,host?44:55),new Vector2(604,32));
        Place((RectTransform)info.Find("Body"),new Vector2(0,host?-20:-22),new Vector2(604,host?88:98));
        bool free=opponent==null||ai;
        aiDeckDropdown.interactable=editable&&free&&lobby?.aiPending!=true&&availableDecks.Count>0;
        applyAiButton.interactable=aiDeckDropdown.interactable;
        removeAiButton.interactable=editable&&(ai||lobby?.aiPending==true);
        SetAiLabel(applyAiButton,ai?DeckCompanion.Text("更换 AI 卡组","Set AI deck","AIデッキ変更"):DeckCompanion.Text("添加 AI","Add AI","AIを追加"));
        SetAiLabel(removeAiButton,DeckCompanion.Text("移除 AI","Remove AI","AIを外す"));
        aiHelp.text=lobby?.aiPending==true?DeckCompanion.Text("正在检查 AI 卡组…","Checking AI deck…","AIデッキ確認中…"):
            !free?DeckCompanion.Text("对面席位已有玩家","Opponent seat is occupied","対戦席に参加者がいます"):
            DeckCompanion.Text("AI 使用所选保存卡组；BO3 保留原牌组，不主动换备。","AI uses this saved deck; no automatic sideboarding in BO3.","AIは選択デッキを使用。BO3でも自動サイド変更は行いません。");
        var binding=aiHelp.GetComponent<PreferenceText>();if(binding!=null)binding.Literal=true;
    }

    private static void SetAiLabel(Button button,string label)
    {
        var text=button.GetComponentInChildren<Text>();var binding=text.GetComponent<PreferenceText>();
        if(binding!=null)binding.Literal=true;text.text=label;
    }
}
