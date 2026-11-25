using UnityEngine;
using UnityEngine.UI;

public sealed partial class OnlineLobbyPanel
{
    // Distances in the shared sheet are measured from its top. The two existing
    // control groups stay in place so dropdowns, scroll state and focus survive seat changes.
    private const float RoomInset = 48f;
    private const float RoomSheetWidth = 1608f;
    private const float RoomSeatsWidth = 732f;
    private const float RoomDeckWidth = RoomSheetWidth - 3 * RoomInset - RoomSeatsWidth;
    private const float RoomDeckCenter = (RoomSheetWidth - RoomDeckWidth) * .5f - RoomInset;
    private const float RoomSeatsCenter = (RoomSeatsWidth - RoomSheetWidth) * .5f + RoomInset;
    private RectTransform playerRoomSurface, playerFooter, playerDivider;
    private bool? showingPlayerRoom;
    private int showingSeatCapacity;

    private void BuildPlayerRoomSurface(RectTransform preparation)
    {
        var sheet = AddPanel(roomPageGroup.transform, "PlayerRoomSheet", Vector2.zero,
            new Vector2(RoomSheetWidth, 540), MenuThemeCatalog.Current.DialogTint);
        sheet.raycastTarget = false;
        sheet.sprite = MenuVisualTheme.StoneWash();
        ObsidianArenaTheme.EnsureOutline(sheet, MenuVisualTheme.Etching, new Vector2(1, -1));
        playerRoomSurface = sheet.rectTransform;
        playerRoomSurface.pivot = new Vector2(.5f, 1);
        playerRoomSurface.anchoredPosition = new Vector2(0, preparation.anchoredPosition.y - RoomInset);
        playerRoomSurface.SetAsFirstSibling();
        MenuVisualTheme.StyleArchiveLintel(playerRoomSurface, 132, .28f);
        playerDivider = MenuVisualTheme.Detail(playerRoomSurface, "ColumnRule", MenuVisualTheme.Etching).rectTransform;
        playerFooter = MenuVisualTheme.Detail(playerRoomSurface, "ActionBand", MenuVisualTheme.Recess).rectTransform;
        var rule = MenuVisualTheme.Detail(playerFooter, "Rule", MenuVisualTheme.Etching).rectTransform;
        PlaceFromTop(rule, 0, 0, RoomSheetWidth, 1);
        playerRoomSurface.gameObject.SetActive(false);
    }

    private void SetRoomRoleLayout(bool playerView, RectTransform summary, RectTransform preparation)
    {
        int capacity=OnlineMatchService.Instance?.LobbyState?.config?.PlayerCapacity ?? 2;
        if (showingPlayerRoom == playerView && showingSeatCapacity == capacity) return;
        showingSeatCapacity = capacity;
        showingPlayerRoom = playerView;
        playerRoomSurface.gameObject.SetActive(playerView);
        transform.Find("SanctumArtwork").GetComponent<RawImage>().canvasRenderer.SetAlpha(playerView ? .45f : 1f);
        foreach (var column in new[] { summary, preparation })
        {
            column.GetComponent<Image>().enabled = !playerView;
            column.Find("SectionTop").gameObject.SetActive(!playerView);
        }

        summary.anchoredPosition = new Vector2(playerView ? RoomSeatsCenter : -400, summary.anchoredPosition.y);
        preparation.anchoredPosition = new Vector2(playerView ? RoomDeckCenter : 400, preparation.anchoredPosition.y);
        preparation.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, playerView ? RoomDeckWidth + 2 * RoomInset : 760);

        // Header: room and round, enforced rules, spectator count.
        float headerLeft = (488 - RoomSeatsWidth) * .5f;
        PlaceFromTop(roomTitleText.rectTransform, playerView ? headerLeft : 0, playerView ? RoomInset + 22 : 31, playerView ? 488 : 648, 54);
        roomTitleText.color = playerView ? MenuVisualTheme.ArchivePaper : MenuVisualTheme.MainAccent;
        PlaceFromTop(seriesText.rectTransform, playerView ? headerLeft : 0, playerView ? RoomInset + 81 : 222, playerView ? 488 : 648, 36);
        PlaceFromTop(roomRulesText.rectTransform, playerView ? headerLeft + 690 : 0, playerView ? RoomInset + 10 : 97, playerView ? 652 : 648, 112);
        roomRulesText.color = playerView ? MenuVisualTheme.Silver : MenuVisualTheme.Muted;
        roomRulesText.resizeTextForBestFit = playerView;
        roomRulesText.resizeTextMinSize = 14;
        roomRulesText.resizeTextMaxSize = roomRulesText.fontSize;
        var counter = (RectTransform)spectatorCount.transform.parent;
        float playerCounterX = RoomSheetWidth * .5f - RoomInset - counter.rect.width * .5f - RoomSeatsCenter;
        PlaceFromTop(counter, playerView ? playerCounterX : -210, playerView ? RoomInset + 43 : 534, counter.rect.width, 46);

        // The seat rows and deck selector start on the same baseline.
        var seatsHeading = summary.Find("PlayersLabel").GetComponent<Text>();
        var deckHeading = preparation.Find("Heading").GetComponent<Text>();
        float seatsWidth = playerView ? RoomSeatsWidth : 648;
        PlaceFromTop(seatsHeading.rectTransform, 0, playerView ? RoomInset + 164 : 284, seatsWidth, playerView ? 48 : 36);
        MenuTypography.Use(seatsHeading, playerView ? MenuTypography.Face.Heading : MenuTypography.Face.Reading);
        PreferenceText.SetSize(seatsHeading, playerView ? 26 : 20);
        seatsHeading.color = playerView ? MenuVisualTheme.MainAccent : MenuVisualTheme.ArchivePaper;
        PlaceFromTop((RectTransform)summary.Find("PlayersPanel"), 0, playerView ? RoomInset + 228 : 332, seatsWidth, playerView ? 184 : 168);
        for (int i = 0; i < seatNames.Length; i++)
        {
            var seat = (RectTransform)seatNames[i].transform.parent;
            bool four=capacity==4;
            float y=((four?i/2:i)==0?1:-1)*(playerView?50:46);
            const float gap = 12;
            float cellWidth = four ? (seatsWidth - gap) * .5f : seatsWidth;
            float cellX = four ? (i % 2 == 0 ? -1 : 1) * (cellWidth + gap) * .5f : 0;
            float badgeWidth = four ? 96 : 164;
            float nameLeft = -cellWidth * .5f + 58;
            float nameRight = cellWidth * .5f - badgeWidth - 20;
            float nameX = (nameLeft + nameRight) * .5f;
            Place(seat,new Vector2(cellX,y),new Vector2(cellWidth,playerView?84:72));
            Place((RectTransform)seat.Find("SeatNumber"),new Vector2(-cellWidth*.5f+26,0),new Vector2(44,48));
            Place(seatNames[i].rectTransform,new Vector2(nameX,14),new Vector2(nameRight-nameLeft,36));
            Place(seatRoles[i].rectTransform,new Vector2(nameX,-17),new Vector2(nameRight-nameLeft,27));
            Place(seatBadges[i].rectTransform,new Vector2((cellWidth-badgeWidth)*.5f-10,0),new Vector2(badgeWidth,38));
            Place(seatStates[i].rectTransform,Vector2.zero,new Vector2(badgeWidth-8,34));
        }
        float deckWidth = playerView ? RoomDeckWidth : 648;
        PlaceFromTop(deckHeading.rectTransform, 0, playerView ? RoomInset + 164 : 31, deckWidth, playerView ? 48 : 54);
        PreferenceText.SetSize(deckHeading, playerView ? 26 : 30);
        PlaceFromTop((RectTransform)deckDropdown.transform, 0, playerView ? RoomInset + 228 : 123, deckWidth, playerView ? 66 : 56);

        if (!playerView)
        {
            PlaceFromTop((RectTransform)statusText.transform.parent, 0, 645, 648, 34);
            PlaceFromTop((RectTransform)leaveRoomButton.transform, -170, 693, 308, 50);
            PlaceFromTop((RectTransform)spectatorButton.transform, 170, 693, 308, 50);
        }
    }

    private float LayoutPlayerRoomFooter()
    {
        var notice = deckNotice.rectTransform;
        float endOfRows = Mathf.Max(412, -notice.anchoredPosition.y - RoomInset + notice.rect.height);
        float dividerBottom = endOfRows;
        if (aiControls.gameObject.activeSelf)
            endOfRows = LayoutRoomAiRow(endOfRows + 20);
        bool hasStatus = !string.IsNullOrWhiteSpace(statusText.text);
        float bandTop = endOfRows + (hasStatus ? 58 : 30);
        const float bandHeight = 98;
        playerRoomSurface.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bandTop + bandHeight);
        float dividerX = RoomSeatsCenter + (RoomSeatsWidth + RoomInset) * .5f;
        PlaceFromTop(playerDivider, dividerX, 164, 1, dividerBottom - 164);
        PlaceFromTop(playerFooter, 0, bandTop, RoomSheetWidth, bandHeight);
        PlaceFromTop((RectTransform)statusText.transform.parent, 0, RoomInset + endOfRows + 4, 648, 34);
        float top = RoomInset + bandTop + 20;
        PlaceFromTop((RectTransform)leaveRoomButton.transform, (228-RoomSeatsWidth)*.5f, top, 228, 58);
        PlaceFromTop((RectTransform)spectatorButton.transform, (236-RoomSeatsWidth)*.5f+248, top, 236, 58);
        return top;
    }
}
