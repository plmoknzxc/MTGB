using UnityEngine;
using UnityEngine.UI;

public sealed partial class OnlineLobbyPanel
{
    private string quantityPrintingKey;
    private bool quantityInSideboard;

    internal void SelectSideboardQuantity(OnlineSideboardCardTile tile)
    {
        if (ready || OnlineMatchService.Instance?.LobbyState?.series?.betweenGames != true) return;
        quantityPrintingKey = tile.Entry.PrintingKey;
        quantityInSideboard = tile.IsSideboard;
        RefreshSideboardQuantityControls();
    }

    private void BuildSideboardQuantityControls(OnlineSideboardCardTile tile, GameObject badge, Vector2 size)
    {
        var controls = new GameObject("QuantityControls", typeof(RectTransform));
        controls.transform.SetParent(tile.transform, false);
        Place((RectTransform)controls.transform, new Vector2(0, size.y * .5f - 54), new Vector2(size.x - 10, 58));
        var minus = MainMenuController.CreateQuantityButton(controls.transform, "Decrease", "−", -size.x * .30f);
        var plus = MainMenuController.CreateQuantityButton(controls.transform, "Increase", "+", size.x * .30f);
        var count = AddPanel(controls.transform, "QuantityCountBackground", Vector2.zero, new Vector2(52, 42), Color.clear);
        count.sprite = IntegratedUiUtility.RoundedRect(54, 42, 9);
        count.color = new Color(.015f, .045f, .065f, .83f);
        count.raycastTarget = false;
        var label = AddText(count.transform, "QuantityCount", "×" + tile.Entry.count, Vector2.zero,
            new Vector2(52, 42), 20, browserFont, TextAnchor.MiddleCenter, Color.white);
        label.fontStyle = FontStyle.Bold;
        tile.ConfigureQuantityControls(controls, badge, minus, plus);
        minus.onClick.AddListener(() => MoveOneCard(tile.Entry, !tile.IsSideboard));
        plus.onClick.AddListener(() =>
        {
            var other = tile.IsSideboard ? selectedDeck?.cards : SideboardDisplayEntries;
            var entry = other?.Find(item => SamePrinting(item, tile.Entry));
            if (entry != null) MoveOneCard(entry, tile.IsSideboard);
        });
        tile.SetQuantityControls(false, false);
    }

    private void RefreshSideboardQuantityControls()
    {
        if (sideboardPageGroup == null) return;
        bool editable = !ready && OnlineMatchService.Instance?.IsSpectator != true
            && OnlineMatchService.Instance?.LobbyState?.series?.betweenGames == true;
        foreach (var tile in sideboardPageGroup.GetComponentsInChildren<OnlineSideboardCardTile>())
        {
            var other = tile.IsSideboard ? selectedDeck?.cards : SideboardDisplayEntries;
            bool canIncrease = other?.Exists(item => item.count > 0 && SamePrinting(item, tile.Entry)) == true;
            tile.SetQuantityControls(editable && tile.Entry.PrintingKey == quantityPrintingKey
                && tile.IsSideboard == quantityInSideboard, canIncrease);
        }
    }
}
