using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class OnlineLobbyPanel
{
    private Button spectatorButton;
    private Text spectatorsText;
    private static readonly Color LobbySurface = new Color(.13f,.14f,.17f);
    private static readonly Color LobbyField = new Color(.085f,.095f,.12f);
    private static readonly Color LobbyGold = new Color(.74f,.59f,.36f);
    private static readonly Color LobbyInk = new Color(.10f,.085f,.065f);
    private static readonly Color LobbyPaper = new Color(.94f,.91f,.85f);

    private void BuildSpectatorControls()
    {
        var summary=leaveRoomButton.transform.parent;
        spectatorButton=AddButton(summary,"SpectatorSeat","前往观战席",new Vector2(170,-308),new Vector2(308,50),browserFont,LobbySurface);
        spectatorButton.onClick.AddListener(()=> {
            var service=OnlineMatchService.Instance;
            if(service!=null)service.SetSpectator(!service.IsSpectator);
        });
        spectatorsText=AddText(summary,"Spectators","观战席 · 暂无人",new Vector2(0,-169),new Vector2(648,32),16,browserFont,TextAnchor.MiddleLeft,MutedTextColor);
        spectatorsText.supportRichText=false;
        AddText(readyButton.transform.parent,"SpectatorInfo","观战席不占用玩家名额。\n\n双方开局后自动进入战场。\n你可以查看公开局面，双方手牌和私有选择不会显示。",new Vector2(0,65),new Vector2(648,220),22,browserFont,TextAnchor.MiddleLeft,LobbyPaper).gameObject.SetActive(false);
        ApplyRoomPalette(transform);
    }

    private void RefreshSpectatorControls(OnlineMatchService service)
    {
        if(spectatorButton==null)return;
        bool spectator=service?.IsSpectator==true;
        spectatorButton.interactable=service?.HasJoinedLobby==true && service.LobbyState?.series?.betweenGames!=true;
        SetButtonLabel(spectatorButton,spectator?"加入玩家席":"前往观战席");
        var names=service?.LobbyState?.players?.Where(p=>p.seat>2).Select(p=>p.displayName).ToArray()??new string[0];
        spectatorsText.text=names.Length==0?"观战席 · 暂无人":$"观战席 · {names.Length} 人   "+string.Join("、",names);
        deckDropdown.interactable=!spectator&&!ready;
        var prep=readyButton.transform.parent;
        prep.Find("Heading").GetComponent<Text>().text=spectator?"观战席":"选择出战牌组";
        prep.Find("ReadyHelp").GetComponent<Text>().text=spectator?"双方开始后自动进入观战，仅展示公开信息。":"需要更换牌组时，请先取消准备。";
        foreach(string name in new[]{"DeckLabel","DeckDropdown","SelectedDeckSummary","PreparationInfo","ReadyButton"})prep.Find(name).gameObject.SetActive(!spectator);
        prep.Find("SpectatorInfo").gameObject.SetActive(spectator);
        prep.Find("Kicker").GetComponent<Text>().text=spectator?"观战":"准备";
        Place(startMatchButton.GetComponent<RectTransform>(),new Vector2(spectator?0:170,-260),new Vector2(spectator?648:308,58));
        foreach(var button in new[]{readyButton,startMatchButton,sideboardReadyButton,sideboardStartButton})
            button.GetComponentInChildren<Text>(true).color=button.interactable?LobbyInk:new Color(.68f,.69f,.73f);
    }

    private static void ApplyRoomPalette(Transform root)
    {
        foreach(var image in root.GetComponentsInChildren<Image>(true))
        {
            if(image.sprite!=null)continue;
            string name=image.name;
            image.color=name=="HeaderBand"||name=="PlayersPanel"?Color.clear:
                name.Contains("Input")||name.Contains("Dropdown")||name=="RoomListViewport"||name.StartsWith("Seat")||name=="SelectedDeckSummary"?LobbyField:LobbySurface;
            if(image.transform==root)image.color=PageColor;
            var edge=image.GetComponent<Outline>();
            if(edge!=null)edge.effectColor=new Color(.30f,.29f,.30f,.65f);
        }
        foreach(var text in root.GetComponentsInChildren<Text>(true))
        {
            if(text.name=="SourceBadge")continue;
            text.color=text.name.Contains("Heading")||text.name=="RoomName"||text.name=="PlayerName"||text.name=="RoomTitle"?LobbyPaper:
                text.name=="Kicker"||text.name=="SeatNumber"?LobbyGold:new Color(.70f,.71f,.75f);
            if(text.GetComponentInParent<InputField>()!=null)text.color=LobbyPaper;
        }
        foreach(var button in root.GetComponentsInChildren<Button>(true))
        {
            bool primary=button.name=="CreateRoom"||button.name=="HostButton"||button.name=="ReadyButton"||button.name=="StartMatchButton"||button.name=="Join"||button.name=="SideboardReadyButton";
            var image=button.GetComponent<Image>();if(image!=null)image.color=Color.white;
            var colors=button.colors;
            colors.normalColor=primary?LobbyGold:new Color(.21f,.23f,.28f);
            colors.highlightedColor=primary?new Color(.88f,.73f,.49f):new Color(.30f,.33f,.39f);
            colors.pressedColor=primary?new Color(.61f,.47f,.28f):new Color(.16f,.18f,.22f);
            colors.selectedColor=colors.normalColor;colors.disabledColor=new Color(.18f,.19f,.22f);button.colors=colors;
            var caption=button.GetComponentInChildren<Text>(true);if(caption!=null)caption.color=primary?LobbyInk:LobbyPaper;
        }
    }
}
