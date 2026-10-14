using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    bool auxiliaryAttractions = true;
    bool auxiliaryRegisteredOnly;
    int auxiliaryPage;
    bool auxiliaryDialogOpen;
    Button auxiliaryEntry;
    const int AuxiliaryPageSize = 10;

    void CreateAuxiliaryDeckEntry()
    {
        if (mainDeckCountText == null) return;
        auxiliaryEntry = CreateDialogButton(mainDeckCountText.transform, "AuxiliaryDeckButton", "辅助牌组", Vector2.zero,
            MenuVisualTheme.Raised, () => ShowAuxiliaryDeck(true));
        var rect = (RectTransform)auxiliaryEntry.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
        rect.pivot = new Vector2(1, .5f);
        rect.anchoredPosition = new Vector2(-8, 0);
        rect.sizeDelta = new Vector2(190, 38);
    }

    void AddSelectedAuxiliary()
    {
        if (DeckAuxiliary.Add(currentDeck, selectedCard, cards)) MarkDeckDirty();
        ShowAuxiliaryDeck(DeckAuxiliary.IsAttraction(selectedCard));
    }

    void ShowAuxiliaryDeck(bool attractions)
    {
        auxiliaryAttractions = attractions;
        auxiliaryRegisteredOnly = true;
        auxiliaryPage = 0;
        RenderAuxiliaryDeck();
    }

    void RefreshAuxiliaryDeckLocale()
    {
        // ExchangePanel is shared with other dialogs; only redraw our active one.
        if (auxiliaryDialogOpen && exchangeDialog != null && exchangeDialog.name == "AuxiliaryDeck") RenderAuxiliaryDeck();
    }

    void RenderAuxiliaryDeck()
    {
        if (currentDeck == null) return;
        var panel = ExchangePanel("辅助牌组", new Vector2(1120, 950));
        panel.Find("Heading").GetComponent<RectTransform>().sizeDelta = new Vector2(1060, 54);
        exchangeDialog.name = "AuxiliaryDeck";
        auxiliaryDialogOpen = true;
        var section = DeckAuxiliary.Entries(currentDeck, auxiliaryAttractions);
        var choices = auxiliaryRegisteredOnly
            ? section.Select(entry => DeckRepository.ResolveCard(entry, cards)).ToList()
            : cards.Where(card => !card.forgeAmbiguous && (auxiliaryAttractions ? DeckAuxiliary.IsAttraction(card) : DeckAuxiliary.IsContraption(card))).ToList();
        int pages = Mathf.Max(1, (choices.Count + AuxiliaryPageSize - 1) / AuxiliaryPageSize);
        auxiliaryPage = Mathf.Clamp(auxiliaryPage, 0, pages - 1);
        var attractionTab = AuxiliaryButton(panel, "Attractions", "游乐设施", new Vector2(-390, 362), new Vector2(240, 44),
            () => { auxiliaryAttractions = true; auxiliaryPage = 0; RenderAuxiliaryDeck(); });
        var contraptionTab = AuxiliaryButton(panel, "Contraptions", "装置", new Vector2(-130, 362), new Vector2(240, 44),
            () => { auxiliaryAttractions = false; auxiliaryPage = 0; RenderAuxiliaryDeck(); });
        (auxiliaryAttractions ? attractionTab : contraptionTab).GetComponent<Image>().color = MenuVisualTheme.Primary;
        AuxiliaryButton(panel, "ToggleRegistered", auxiliaryRegisteredOnly ? "浏览可加入的牌" : "查看已加入的牌",
            new Vector2(260, 362), new Vector2(440, 44),
            () => { auxiliaryRegisteredOnly = !auxiliaryRegisteredOnly; auxiliaryPage = 0; RenderAuxiliaryDeck(); });
        string label = GamePreferences.T(auxiliaryAttractions ? "游乐设施" : "装置");
        var summary = AddText(panel, "Summary", string.Format(GamePreferences.T("{0}：{1} 张 · 至少 {2} 张，每种牌名只能一张"),
            label, section.Sum(entry => Mathf.Max(0, entry?.count ?? 0)), auxiliaryAttractions ? 10 : 15), 19,
            MenuVisualTheme.Paper, TextAnchor.MiddleLeft);
        Place(summary.rectTransform, new Vector2(0, 307), new Vector2(1020, 46));
        for (int i = 0; i < AuxiliaryPageSize && auxiliaryPage * AuxiliaryPageSize + i < choices.Count; i++)
        {
            CardDefinition card = choices[auxiliaryPage * AuxiliaryPageSize + i];
            DeckEntry registered = auxiliaryRegisteredOnly ? section[auxiliaryPage * AuxiliaryPageSize + i] : null;
            string printingKey = card?.PrintingKey ?? registered?.PrintingKey ?? "";
            float x = (i % 5 - 2) * 204;
            float y = i < 5 ? 130 : -190;
            var art = AddImage(panel, "AuxiliaryCard_" + printingKey, card == null ? MenuVisualTheme.Ink : Color.white);
            Place(art.rectTransform, new Vector2(x, y + 25), new Vector2(126, 176));
            art.sprite = card == null ? null : LoadCardSprite(card);
            art.preserveAspect = true;
            var name = AddText(panel, "CardName_" + i, card == null ? GamePreferences.T("无法识别的印次") : GamePreferences.CardName(card), 17, MenuVisualTheme.Paper, TextAnchor.MiddleCenter);
            Place(name.rectTransform, new Vector2(x, y - 98), new Vector2(190, 64));
            var printing = AddText(panel, "Printing_" + i, printingKey, 15, MenuVisualTheme.Muted, TextAnchor.MiddleCenter);
            Place(printing.rectTransform, new Vector2(x, y - 147), new Vector2(190, 30));
            var change = AuxiliaryButton(panel, "ChangeAuxiliary_" + i, auxiliaryRegisteredOnly ? "移除" : "加入",
                new Vector2(x, y - 183), new Vector2(180, 38), () => {
                    bool changed = auxiliaryRegisteredOnly ? section.Remove(registered) : DeckAuxiliary.Add(currentDeck, card, cards);
                    if (changed) MarkDeckDirty();
                    RenderAuxiliaryDeck();
                });
            change.interactable = auxiliaryRegisteredOnly || !DeckAuxiliary.ContainsName(currentDeck, card, cards);
        }
        var page = AddText(panel, "Page", (auxiliaryPage + 1) + " / " + pages, 18, MenuVisualTheme.Paper, TextAnchor.MiddleCenter);
        Place(page.rectTransform, new Vector2(-205, -442), new Vector2(160, 34));
        AuxiliaryButton(panel, "Previous", "‹", new Vector2(-355, -442), new Vector2(86, 38),
            () => { auxiliaryPage--; RenderAuxiliaryDeck(); }).interactable = auxiliaryPage > 0;
        AuxiliaryButton(panel, "Next", "›", new Vector2(-55, -442), new Vector2(86, 38),
            () => { auxiliaryPage++; RenderAuxiliaryDeck(); }).interactable = auxiliaryPage + 1 < pages;
        AuxiliaryButton(panel, "Close", "完成", new Vector2(345, -442), new Vector2(330, 40),
            () => { auxiliaryDialogOpen = false; CloseDeckExchange(); RefreshDeckGrids(); RefreshSelectedCardInfo(); });
    }

    Button AuxiliaryButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Action action)
    {
        var button = CreateDialogButton(parent, name, GamePreferences.T(label), position, MenuVisualTheme.Raised, action);
        ((RectTransform)button.transform).sizeDelta = size;
        return button;
    }
}
