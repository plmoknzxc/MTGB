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
    private DeckDefinition selectedAiDeck;

    private void CreateRoomAiControls(Transform parent, Font font)
    {
        aiControls=ReplayWidgets.Rect(parent,"RoomAiControls",0,-149,648,214);
        var divider=MenuVisualTheme.Detail(aiControls,"Divider",MenuVisualTheme.Etching);
        Place(divider.rectTransform,new Vector2(0,103),new Vector2(648,2));
        AddText(aiControls,"AiHeading",DeckCompanion.Text("AI 对手","AI opponent","AI 対戦相手"),new Vector2(0,62),new Vector2(648,42),21,font,TextAnchor.MiddleLeft,TextColor);
        aiHelp=AddText(aiControls,"Help","",new Vector2(0,-67),new Vector2(648,78),17,font,TextAnchor.MiddleLeft,MutedTextColor);
        aiHelp.supportRichText=false;
        aiHelp.resizeTextForBestFit=true; aiHelp.resizeTextMinSize=14; aiHelp.resizeTextMaxSize=22;
        aiDeckDropdown=AddDropdown(aiControls,"AiDeckDropdown",new Vector2(-164,2),new Vector2(320,48),font,new List<string>{"AI"});
        aiDeckDropdown.onValueChanged.AddListener(OnAiDeckSelected);
        applyAiButton=AddButton(aiControls,"ApplyAi","AI",new Vector2(84,2),new Vector2(152,48),font,AccentColor);
        removeAiButton=AddButton(aiControls,"RemoveAi","AI",new Vector2(248,2),new Vector2(152,48),font,FieldColor);
        foreach(var button in new[]{applyAiButton,removeAiButton})PreferenceText.SetSize(button.GetComponentInChildren<Text>(),17);
        applyAiButton.onClick.AddListener(()=>{
            aiRejection=null;
            if(selectedAiDeck!=null)OnlineMatchService.Instance?.ConfigureRoomAi(selectedAiDeck);
            Refresh();
        });
        removeAiButton.onClick.AddListener(()=>{
            aiRejection=null;
            OnlineMatchService.Instance?.ConfigureRoomAi(null,true);
            Refresh();
        });
    }

    private void OnAiDeckSelected(int index)
    {
        if(suppressDeckSelection || !aiDeckDropdown.IsInteractable() || index<0 || index>=availableDecks.Count)return;
        selectedAiDeck=availableDecks[index];
        aiRejection=null;
        var service=OnlineMatchService.Instance;
        if(service?.LobbyState?.players?.Any(player=>player.isAi)==true)
            service.ConfigureRoomAi(selectedAiDeck);
        Refresh();
    }

    private void ReloadAiDeckChoices()
    {
        if(aiDeckDropdown==null)return;
        aiDeckDropdown.ClearOptions();
        aiDeckDropdown.AddOptions(availableDecks.Select(d=>d.name).ToList());
    }

    private void SyncAiDeckChoice(OnlineMatchService service, bool ai, bool pending)
    {
        bool confirmed=ai && (!pending || aiRejection!=null);
        int index=confirmed
            ? availableDecks.FindIndex(d=>d.name==service.AcceptedRoomAiDeckName && service.MatchesAcceptedRoomAiDeck(d))
            : availableDecks.FindIndex(d=>SameDeckName(d,selectedAiDeck));
        if(index<0 && !confirmed && !pending && availableDecks.Count>0)index=0;

        var options=aiDeckDropdown.options;
        // A restored room can refer to a deck that was renamed, edited or removed locally.
        // Give it its own display option so the first saved deck can still be selected.
        if(index<0)
        {
            index=availableDecks.Count;
            string display=confirmed
                ? DeckCompanion.Text("当前 AI 卡组","Current AI deck","現在のAIデッキ")
                : selectedAiDeck!=null && pending ? selectedAiDeck.name : DeckCompanion.Text("没有已保存卡组","No saved decks","保存済みデッキなし");
            if(confirmed && !string.IsNullOrEmpty(service.AcceptedRoomAiDeckName))
                display+=" · "+service.AcceptedRoomAiDeckName;
            if(options.Count==index)options.Add(new Dropdown.OptionData(display));
            else options[index].text=display;
            if(!confirmed && !pending)selectedAiDeck=null;
        }
        else
        {
            if(options.Count>availableDecks.Count)options.RemoveAt(availableDecks.Count);
            selectedAiDeck=availableDecks[index];
        }
        aiDeckDropdown.SetValueWithoutNotify(index);
        aiDeckDropdown.RefreshShownValue();
    }

    private void RefreshRoomAiControls(OnlineMatchService service)
    {
        if(aiControls==null)return;
        var lobby=service?.LobbyState;
        bool host=service?.HasJoinedLobby==true&&lobby?.config?.PlayerCapacity==2&&!service.IsSpectator&&lobby.hostSeat==service.LocalSeat;
        bool editable=host&&!service.IsInMatch&&lobby.series?.betweenGames!=true&&lobby.playDrawChooserSeat==0&&service.EndReview==null;
        var opponent=lobby?.players?.FirstOrDefault(p=>p.seat<=2&&p.seat!=service.LocalSeat);
        bool ai=opponent?.isAi==true;
        bool pending=service?.RoomAiChangePending==true;
        bool free=opponent==null||ai;
        aiControls.gameObject.SetActive(host&&(free||pending));
        if(!host){aiRejection=null;return;}
        SyncAiDeckChoice(service,ai,pending);
        aiControls.Find("Divider").GetComponent<Image>().color=MenuVisualTheme.Etching;
        aiControls.Find("AiHeading").GetComponent<Text>().text=DeckCompanion.Text("AI 对手","AI opponent","AI 対戦相手");
        aiDeckDropdown.interactable=editable&&free&&!pending&&availableDecks.Count>0;
        applyAiButton.gameObject.SetActive(!ai&&!pending);
        applyAiButton.interactable=aiDeckDropdown.interactable;
        removeAiButton.gameObject.SetActive(ai||pending);
        removeAiButton.interactable=editable&&(ai||pending);
        SetAiLabel(applyAiButton,DeckCompanion.Text("添加 AI","Add AI","AIを追加"));
        SetAiLabel(removeAiButton,DeckCompanion.Text("移除 AI","Remove AI","AIを外す"));
        aiHelp.text=aiRejection!=null?LocalizeDeckRejection(aiRejection):pending?DeckCompanion.Text("正在检查 AI 卡组…","Checking AI deck…","AIデッキ確認中…"):string.Empty;
        aiHelp.gameObject.SetActive(!string.IsNullOrEmpty(aiHelp.text));
        aiHelp.color=aiRejection!=null?MenuVisualTheme.Warning:MenuVisualTheme.Stone;
        var binding=aiHelp.GetComponent<PreferenceText>();if(binding!=null)binding.Literal=true;
    }

    // The AI row spans both columns. Its controls share the deck selector's edges.
    private float LayoutRoomAiRow(float top)
    {
        float innerWidth = RoomSheetWidth - 2 * RoomInset;
        float height = aiHelp.gameObject.activeSelf ? 134 : 70;
        PlaceFromTop(aiControls, -RoomDeckCenter, RoomInset + top, innerWidth, height);
        PlaceFromTop((RectTransform)aiControls.Find("Divider"), 0, 0, innerWidth, 1);
        PlaceFromTop((RectTransform)aiControls.Find("AiHeading"), RoomSeatsCenter, 18, RoomSeatsWidth, 52);
        const float buttonWidth = 190, gap = 14;
        float selectorWidth = RoomDeckWidth - buttonWidth - gap;
        float left = RoomDeckCenter - RoomDeckWidth * .5f;
        PlaceFromTop((RectTransform)aiDeckDropdown.transform, left + selectorWidth * .5f, 18, selectorWidth, 52);
        var action = removeAiButton.gameObject.activeSelf ? removeAiButton : applyAiButton;
        PlaceFromTop((RectTransform)action.transform, left + selectorWidth + gap + buttonWidth * .5f, 18, buttonWidth, 52);
        if (aiHelp.gameObject.activeSelf)
            PlaceFromTop(aiHelp.rectTransform, RoomDeckCenter, 76, RoomDeckWidth, 58);
        return top + height;
    }

    private static void SetAiLabel(Button button,string label)
    {
        var text=button.GetComponentInChildren<Text>(true);var binding=text.GetComponent<PreferenceText>();
        if(binding!=null)binding.Literal=true;text.text=label;
    }
}
