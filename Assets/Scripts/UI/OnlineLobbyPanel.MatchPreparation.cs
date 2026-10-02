using UnityEngine;
using UnityEngine.UI;

public sealed partial class OnlineLobbyPanel
{
    private string sideboardGridSignature;
    private bool resetSideboardScroll;
    private GameObject playDrawOverlay;
    private Text playDrawTitle;
    private Button playFirstButton, drawFirstButton;

    private void BuildPlayDrawControls()
    {
        var side = sideboardPageGroup.transform;
        var header = side.Find("SideboardHeader");
        header.Find("Kicker").gameObject.SetActive(false);
        header.Find("Heading").gameObject.SetActive(false);
        header.GetComponent<Image>().color = Color.clear;
        Place((RectTransform)header, new Vector2(160, 432), new Vector2(1300, 64));
        Place(sideboardSeriesText.rectTransform, Vector2.zero, new Vector2(1260, 58));
        sideboardSeriesText.alignment = TextAnchor.MiddleRight;
        sideboardSeriesText.fontSize = 28;
        sideboardSeriesText.fontStyle = FontStyle.Bold;
        sideboardSeriesText.color = LobbyPaper;
        sideboardSeriesText.resizeTextForBestFit = false;
        sideboardStatusText.gameObject.SetActive(false);
        sideboardStartButton.gameObject.SetActive(false);
        foreach (string name in new[] { "MainDeckCard", "SideboardCard" })
        {
            var card = side.Find(name);
            Place((RectTransform)card, new Vector2(name == "MainDeckCard" ? -430 : 430, 12), new Vector2(820, 704));
            Place((RectTransform)card.Find("Heading"), new Vector2(0, 312), new Vector2(740, 40));
            Place((RectTransform)card.Find(name == "MainDeckCard" ? "MainDeckScroll" : "SideboardScroll"), new Vector2(0, -24), new Vector2(780, 610));
        }
        Place(resetSideboardButton.GetComponent<RectTransform>(), new Vector2(375, -391), new Vector2(260, 54));
        Place(sideboardReadyButton.GetComponent<RectTransform>(), new Vector2(695, -391), new Vector2(290, 54));

        var overlay = AddPanel(contentGroup.transform, "PlayDrawOverlay", Vector2.zero, new Vector2(1920, 1080), new Color(0, 0, 0, .68f));
        Stretch(overlay.rectTransform);
        overlay.color = new Color(0, 0, 0, .68f);
        overlay.GetComponent<Outline>().enabled = false;
        playDrawOverlay = overlay.gameObject;
        var dialog = AddPanel(overlay.transform, "PlayDrawDialog", Vector2.zero, new Vector2(620, 242), LobbySurface);
        playDrawTitle = AddText(dialog.transform, "ChoiceTitle", "选择先攻或后攻", new Vector2(0, 70), new Vector2(572, 48), 28, browserFont, TextAnchor.MiddleCenter, LobbyPaper);
        AddText(dialog.transform, "ChoiceHelp", "选择后发放起手牌，再决定保留或调度", new Vector2(0, 21), new Vector2(572, 32), 18, browserFont, TextAnchor.MiddleCenter, MutedTextColor);
        playFirstButton = AddButton(dialog.transform, "PlayFirst", "先攻", new Vector2(-148, -58), new Vector2(264, 54), browserFont, LobbyGold);
        drawFirstButton = AddButton(dialog.transform, "DrawFirst", "后攻", new Vector2(148, -58), new Vector2(264, 54), browserFont, LobbyGold);
        foreach (var button in new[] { playFirstButton, drawFirstButton })
        {
            button.GetComponent<Image>().color = Color.white;
            var colors = button.colors;
            colors.normalColor = colors.selectedColor = LobbyGold;
            colors.highlightedColor = new Color(.88f, .73f, .49f);
            colors.pressedColor = new Color(.61f, .47f, .28f);
            button.colors = colors;
            button.GetComponentInChildren<Text>().color = LobbyInk;
        }
        playFirstButton.onClick.AddListener(() => OnlineMatchService.Instance.ChoosePlayDraw(true));
        drawFirstButton.onClick.AddListener(() => OnlineMatchService.Instance.ChoosePlayDraw(false));
        BindBrowserText(overlay.transform);
        playDrawOverlay.SetActive(false);
    }

    private void RefreshMatchPreparation(OnlineMatchService service, bool betweenGames)
    {
        GetComponent<Image>().color = PageColor;
        foreach (string name in new[] { "Title", "Subtitle", "ConnectionBadge" })
            contentGroup.transform.Find(name).gameObject.SetActive(!betweenGames);
        int chooser = service?.LobbyState?.playDrawChooserSeat ?? 0;
        if (playDrawOverlay == null) return;
        bool choosing = chooser != 0 && service.HasJoinedLobby;
        playDrawOverlay.SetActive(choosing);
        if (!choosing) return;
        playDrawOverlay.transform.SetAsLastSibling();
        bool local = chooser == service.LocalSeat;
        string nameOfChooser = service.LobbyState.players.Find(p => p.seat == chooser)?.displayName ?? "对手";
        playDrawTitle.text = local ? "选择先攻或后攻" : $"等待 {nameOfChooser} 选择先后攻";
        playFirstButton.gameObject.SetActive(local);
        drawFirstButton.gameObject.SetActive(local);
    }
}
