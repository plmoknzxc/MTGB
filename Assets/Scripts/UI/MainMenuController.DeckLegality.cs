using System;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    private readonly DeckFormatLegality deckLegality = new DeckFormatLegality();
    private bool legalitySubscribed;

    private void ApplyDeckLegalitySelection()
    {
        if (currentDeck == null) return;
        string format = Array.IndexOf(DeckFormatLegality.Keys, currentDeck.format) >= 0
            ? currentDeck.format : "standard";
        GamePreferences.SetDeckFormat(format);
        GamePreferences.SetDeckBanlist(string.IsNullOrWhiteSpace(currentDeck.banlist)
            ? DeckFormatLegality.Current : currentDeck.banlist);
    }

    private void OnDeckLegalitySelectionChanged()
    {
        if (currentDeck == null) return;
        string format = GamePreferences.DeckFormat;
        string banlist = GamePreferences.DeckBanlist;
        if (currentDeck.format != format || currentDeck.banlist != banlist)
        {
            currentDeck.format = format;
            currentDeck.banlist = banlist;
            if (loadedDeckIndex >= 0 && loadedDeckIndex < decks.Count)
            {
                try
                {
                    // Persist only legality metadata, leaving unsaved card edits untouched.
                    var saved = CloneDeck(decks[loadedDeckIndex]);
                    saved.format = format;
                    saved.banlist = banlist;
                    DeckRepository.SaveReplacing(decks[loadedDeckIndex].name, saved);
                    decks[loadedDeckIndex] = saved;
                }
                catch (Exception exception)
                {
                    MarkDeckDirty();
                    SetStatus("赛制设置未能自动保存：" + exception.Message);
                }
            }
            else MarkDeckDirty();
        }
        RefreshDeckLegality();
    }

    private void RefreshDeckLegality()
    {
        if (!legalitySubscribed)
        {
            deckLegality.Changed += OnDeckLegalityChanged;
            legalitySubscribed = true;
        }
        OnDeckLegalityChanged();
        if (currentDeck != null && cards.Count > 0)
            StartCoroutine(deckLegality.Refresh(GamePreferences.DeckFormat, GamePreferences.DeckBanlist, cards));
    }

    private void OnDeckLegalityChanged()
    {
        if (deckBuilderGroup != null)
            foreach (var tile in deckBuilderGroup.GetComponentsInChildren<MainMenuCardTile>(true))
                if (tile.Zone != MainMenuCardTileZone.Collection) ApplyLegality(tile);
    }

    private void ApplyLegality(MainMenuCardTile tile)
    {
        if (tile == null || tile.Card == null) return;
        string state = deckLegality.Get(tile.Card, GamePreferences.DeckFormat, GamePreferences.DeckBanlist);
        bool restrictedOverflow = state == "restricted" && TotalCopiesByName(tile.Card.cardName) > 1;
        bool invalid = state == "not_legal" || state == "banned" || restrictedOverflow;
        var wash = tile.transform.Find("LegalityWash")?.GetComponent<Image>();
        if (!invalid && state != "restricted" && tile.transform.Find("LegalityBadge") == null && wash == null) return;
        if (wash == null)
        {
            var go = new GameObject("LegalityWash", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(tile.transform, false);
            wash = go.GetComponent<Image>();
            wash.raycastTarget = false;
            wash.rectTransform.anchorMin = Vector2.zero;
            wash.rectTransform.anchorMax = Vector2.one;
            wash.rectTransform.offsetMin = wash.rectTransform.offsetMax = Vector2.zero;
        }
        wash.color = state == "banned"
            ? new Color(.33f, .02f, .03f, .38f)
            : new Color(.83f, .07f, .09f, .47f);
        wash.gameObject.SetActive(invalid);
        var badge = tile.transform.Find("LegalityBadge/Label")?.GetComponent<Text>();
        if (badge == null)
        {
            var background = new GameObject("LegalityBadge", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(tile.transform, false);
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(background.transform, false);
            badge = go.GetComponent<Text>();
            badge.font = GamePreferences.Font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            badge.fontSize = 20;
            badge.fontStyle = FontStyle.Bold;
            badge.alignment = TextAnchor.MiddleCenter;
            badge.color = Color.white;
            badge.raycastTarget = false;
            var rect = (RectTransform)background.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-5, -5);
            rect.sizeDelta = new Vector2(64, 34);
            background.GetComponent<Image>().color = new Color(.58f, .01f, .03f, 1f);
            background.GetComponent<Image>().raycastTarget = false;
            var border = background.AddComponent<Outline>();
            border.effectColor = new Color(1f, .82f, .63f, 1f);
            border.effectDistance = new Vector2(2, 2);
            badge.rectTransform.anchorMin = Vector2.zero;
            badge.rectTransform.anchorMax = Vector2.one;
            badge.rectTransform.offsetMin = badge.rectTransform.offsetMax = Vector2.zero;
        }
        badge.text = state == "banned" ? GamePreferences.T("禁用")
            : state == "restricted" ? GamePreferences.T("限1") : string.Empty;
        badge.transform.parent.gameObject.SetActive(badge.text.Length > 0);
        // Quantity controls and the status mark remain readable over the red tint.
        var controls = tile.transform.Find("QuantityControls");
        if (controls != null) controls.SetAsLastSibling();
        badge.transform.parent.SetAsLastSibling();
    }

    private int TotalCopiesByName(string name)
    {
        int count = 0;
        if (currentDeck == null) return count;
        foreach (var parent in new[] { mainDeckContent, sideboardContent })
            if (parent != null) foreach (var tile in parent.GetComponentsInChildren<MainMenuCardTile>(true))
                if (tile.Card != null && string.Equals(tile.Card.cardName, name, StringComparison.OrdinalIgnoreCase))
                    count += CountCardCopies(tile.Card, tile.Zone == MainMenuCardTileZone.Sideboard);
        return count;
    }

}
