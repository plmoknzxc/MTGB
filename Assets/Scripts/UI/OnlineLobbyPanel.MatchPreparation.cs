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
        var header=(RectTransform)side.Find("SideboardHeader");
        header.Find("Kicker").gameObject.SetActive(false);
        header.GetComponent<Image>().enabled=false;
        header.GetComponent<Outline>().enabled=false;
        Place(header,new Vector2(76,426),new Vector2(1280,68));
        Place((RectTransform)header.Find("Heading"),new Vector2(-300,0),new Vector2(588,64));
        Place(sideboardSeriesText.rectTransform,new Vector2(336,0),new Vector2(648,60));
        sideboardSeriesText.alignment=TextAnchor.MiddleRight;
        sideboardStartButton.gameObject.SetActive(false);
        foreach(bool main in new[]{true,false}){
            var card=(RectTransform)side.Find(main?"MainDeckCard":"SideboardCard");
            float width=main?944:572;
            Place(card,new Vector2(main?-296:482,26),new Vector2(width,676));
            Place((RectTransform)card.Find("Heading"),new Vector2(0,302),new Vector2(width-48,56));
            var scroll=card.GetComponentInChildren<ScrollRect>(true);
            Place(scroll.GetComponent<RectTransform>(),new Vector2(0,-24),new Vector2(width-32,598));
            var grid=scroll.content.GetComponent<GridLayoutGroup>();
            grid.constraintCount=main?5:3;
            grid.cellSize=new Vector2(164,229);
            grid.childAlignment=TextAnchor.UpperCenter;
        }
        var footer=MenuVisualTheme.Detail(side,"SideboardFooter",MenuVisualTheme.ArchivePanel);
        Place(footer.rectTransform,new Vector2(0,-398),new Vector2(1536,146));
        footer.transform.SetAsFirstSibling();
        Place(sideboardDeckText.rectTransform,new Vector2(-396,-362),new Vector2(694,50));
        sideboardDeckText.alignment=TextAnchor.MiddleLeft;
        sideboardStatusText.alignment=TextAnchor.MiddleLeft;
        Place(resetSideboardButton.GetComponent<RectTransform>(),new Vector2(300,-363),new Vector2(260,54));
        Place(sideboardReadyButton.GetComponent<RectTransform>(),new Vector2(607,-363),new Vector2(322,54));

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

    private MenuThemeStyle sideboardTheme;
    private bool sideboardBackdropVisible;
    private bool? sideboardStyledReady;

    private void StyleSideboardPage()
    {
        var theme=MenuThemeCatalog.Current;
        if(sideboardTheme!=theme){
            sideboardTheme=theme;
            sideboardStyledReady=null;
            var side=sideboardPageGroup.transform;
            MenuTypography.Scope(side);
            var title=side.Find("SideboardHeader/Heading").GetComponent<Text>();
            title.color=theme.ArchivePaper;
            MenuTypography.Use(title,MenuTypography.Face.Heading);
            PreferenceText.SetSize(title,34);
            MenuTypography.Use(sideboardSeriesText,MenuTypography.Face.Control);
            sideboardSeriesText.color=theme.Silver;
            PreferenceText.SetSize(sideboardSeriesText,22);
            foreach(bool main in new[]{true,false}){
                var card=side.Find(main?"MainDeckCard":"SideboardCard");
                Color accent=main?theme.MainAccent:theme.SideAccent;
                StyleRoomSection(card,card.Find("Heading").GetComponent<Text>(),accent);
                var scroll=card.GetComponentInChildren<ScrollRect>(true);
                StyleRoomInset(scroll.GetComponent<Image>(),theme.Recess);
                MenuVisualTheme.StyleDeckScrollbar(scroll);
            }
            var footer=side.Find("SideboardFooter").GetComponent<Image>();
            footer.color=theme.SectionFill;
            MenuVisualTheme.SectionTop(footer.transform,theme.Etching);
            MenuTypography.Use(sideboardDeckText,MenuTypography.Face.Heading);
            PreferenceText.SetSize(sideboardDeckText,24);
            sideboardDeckText.color=theme.ArchivePaper;
            sideboardStatusText.color=theme.Stone;
            MenuVisualTheme.StyleDeckTool(resetSideboardButton);
            DisabledButtonVisual.Bind(resetSideboardButton);
            foreach(var tile in side.GetComponentsInChildren<OnlineSideboardCardTile>(true)){
                var controls=tile.transform.Find("QuantityControls");
                if(controls!=null)foreach(var button in controls.GetComponentsInChildren<Button>(true))
                    MenuVisualTheme.StyleDeckTool(button);
                var badge=tile.transform.Find("CountBadge")?.GetComponent<Image>();
                if(badge!=null)badge.color=theme.Recess;
            }
            sideboardGridSignature=null;
        }
        if(sideboardStyledReady!=ready){
            MenuVisualTheme.StyleDeckTool(sideboardReadyButton,!ready);
            sideboardStyledReady=ready;
        }
    }

    private void RefreshMatchPreparation(OnlineMatchService service, bool betweenGames)
    {
        GetComponent<Image>().color = PageColor;
        if(sideboardBackdropVisible!=betweenGames){
            var artwork=transform.Find("SanctumArtwork").GetComponent<RawImage>();
            artwork.material=null;
            MenuThemeArtwork.Bind(artwork,betweenGames?MenuThemeArtwork.Role.Deck:MenuThemeArtwork.Role.Hall);
            artwork.color=new Color(1,1,1,betweenGames?.48f:.62f);
            sideboardBackdropVisible=betweenGames;
        }
        if(betweenGames)StyleSideboardPage();
        foreach (string name in new[] { "Title", "ConnectionBadge", "ConnectionBadgePlate", "HeaderBand" })
            contentGroup.transform.Find(name).gameObject.SetActive(!betweenGames);
        int chooser = service?.LobbyState?.playDrawChooserSeat ?? 0;
        if (playDrawOverlay == null) return;
        // The authoritative choice now lives over the battlefield scene.
        playDrawOverlay.SetActive(false);
    }
}
