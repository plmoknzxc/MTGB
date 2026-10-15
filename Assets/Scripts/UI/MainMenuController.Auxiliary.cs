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
    const int AuxiliaryPageSize = 6;
    List<CardDefinition> attractionCandidates,contraptionCandidates;

    static (CardDefinition[][] companions,List<CardDefinition> attractions,List<CardDefinition> contraptions) PickerCatalog(IReadOnlyList<CardDefinition> source)
    {
        var companions=source.Where(c=>!c.forgeAmbiguous && (c.description??"").IndexOf("Companion",StringComparison.OrdinalIgnoreCase)>=0).Where(DeckCompanion.IsCompanion).GroupBy(c=>c.cardName).OrderBy(g=>g.Key).Select(g=>g.ToArray()).ToArray();
        return (companions,source.Where(c=>!c.forgeAmbiguous&&DeckAuxiliary.IsAttraction(c)).ToList(),source.Where(c=>!c.forgeAmbiguous&&DeckAuxiliary.IsContraption(c)).ToList());
    }

    void CreateAuxiliaryDeckEntry()
    {
        if (sideboardCountText == null) return;
        auxiliaryEntry = CreateDialogButton(sideboardCountText.transform, "AuxiliaryDeckButton", "辅助牌组", Vector2.zero,
            MenuVisualTheme.Raised, () => ShowAuxiliaryDeck(true));
        var rect = (RectTransform)auxiliaryEntry.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
        rect.pivot = new Vector2(1, .5f);
        rect.anchoredPosition = new Vector2(-8, 0);
        rect.sizeDelta = new Vector2(148, 38);
        PreferenceText.SetSize(auxiliaryEntry.GetComponentInChildren<Text>(),17);
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
        if(auxiliaryEntry!=null)auxiliaryEntry.GetComponentInChildren<Text>().text=DeckCompanion.Text("辅助牌组","Auxiliary","補助デッキ");
        // ExchangePanel is shared with other dialogs; only redraw our active one.
        if (auxiliaryDialogOpen && exchangeDialog != null && exchangeDialog.name == "AuxiliaryDeck") RenderAuxiliaryDeck();
    }

    void RenderAuxiliaryDeck()
    {
        if (currentDeck == null) return;
        var panel = PickerPanel("AuxiliaryDeck","辅助牌组", new Vector2(1120, 1020));
        panel.Find("Heading").GetComponent<RectTransform>().sizeDelta = new Vector2(1060, 54);
        exchangeDialog.name = "AuxiliaryDeck";
        auxiliaryDialogOpen = true;
        var section = DeckAuxiliary.Entries(currentDeck, auxiliaryAttractions);
        if(attractionCandidates==null){attractionCandidates=cards.Where(c=>!c.forgeAmbiguous&&DeckAuxiliary.IsAttraction(c)).ToList();contraptionCandidates=cards.Where(c=>!c.forgeAmbiguous&&DeckAuxiliary.IsContraption(c)).ToList();}
        var choices = auxiliaryRegisteredOnly
            ? section.Select(entry => DeckRepository.ResolveCard(entry, cards)).ToList()
            : (auxiliaryAttractions ? attractionCandidates : contraptionCandidates);
        int pages = Mathf.Max(1, (choices.Count + AuxiliaryPageSize - 1) / AuxiliaryPageSize);
        auxiliaryPage = Mathf.Clamp(auxiliaryPage, 0, pages - 1);
        var attractionTab = PickerButton(panel, "Attractions", "游乐设施", new Vector2(-390, 409), new Vector2(240, 44),
            () => { auxiliaryAttractions = true; auxiliaryPage = 0; RenderAuxiliaryDeck(); });
        var contraptionTab = PickerButton(panel, "Contraptions", "装置", new Vector2(-130, 409), new Vector2(240, 44),
            () => { auxiliaryAttractions = false; auxiliaryPage = 0; RenderAuxiliaryDeck(); });
        (auxiliaryAttractions ? attractionTab : contraptionTab).GetComponent<Image>().color = MenuVisualTheme.Primary;
        PickerButton(panel, "ToggleRegistered", auxiliaryRegisteredOnly ? "浏览可加入的牌" : "查看已加入的牌",
            new Vector2(260, 409), new Vector2(440, 44),
            () => { auxiliaryRegisteredOnly = !auxiliaryRegisteredOnly; auxiliaryPage = 0; RenderAuxiliaryDeck(); });
        string label = GamePreferences.T(auxiliaryAttractions ? "游乐设施" : "装置");
        var summary = PickerText(panel, "Summary", string.Format(GamePreferences.T("{0}：{1} 张 · 至少 {2} 张，每种牌名只能一张"),
            label, section.Sum(entry => Mathf.Max(0, entry?.count ?? 0)), auxiliaryAttractions ? 10 : 15), 19,
            MenuVisualTheme.Paper, TextAnchor.MiddleLeft);
        Place(summary.rectTransform, new Vector2(0, 352), new Vector2(1020, 46));
        for (int i = 0; i < AuxiliaryPageSize && auxiliaryPage * AuxiliaryPageSize + i < choices.Count; i++)
        {
            CardDefinition card = choices[auxiliaryPage * AuxiliaryPageSize + i];
            DeckEntry registered = auxiliaryRegisteredOnly ? section[auxiliaryPage * AuxiliaryPageSize + i] : null;
            string printingKey = card?.PrintingKey ?? registered?.PrintingKey ?? "";
            float x = (i % 3 - 1) * 340;
            float y = i < 3 ? 195 : -190;
            var art = PickerImage(panel, "AuxiliaryCard_" + i, card == null ? MenuVisualTheme.Ink : Color.white);
            Place(art.rectTransform, new Vector2(x, y), new Vector2(156, 218));
            PickerArt(art,card);
            art.preserveAspect = true;
            var name = PickerText(panel, "CardName_" + i, card == null ? GamePreferences.T("无法识别的印次") : GamePreferences.CardName(card), 20, MenuVisualTheme.Paper, TextAnchor.MiddleCenter);
            Place(name.rectTransform, new Vector2(x, y - 148), new Vector2(320, 62));
            var printing = PickerText(panel, "Printing_" + i, printingKey, 15, MenuVisualTheme.Muted, TextAnchor.MiddleCenter);
            Place(printing.rectTransform, new Vector2(x, y - 192), new Vector2(300, 22));
            PickerButton(panel, "ChangeAuxiliary_" + i, auxiliaryRegisteredOnly ? "移除" : "加入",
                new Vector2(x, y - 232), new Vector2(284, 38), () => {
                    bool changed = auxiliaryRegisteredOnly ? section.Remove(registered) : DeckAuxiliary.Add(currentDeck, card, cards);
                    if (changed) MarkDeckDirty();
                    RenderAuxiliaryDeck();
                },auxiliaryRegisteredOnly || !DeckAuxiliary.ContainsName(currentDeck, card, cards));
        }
        var page = PickerText(panel, "Page", (auxiliaryPage + 1) + " / " + pages, 18, MenuVisualTheme.Paper, TextAnchor.MiddleCenter);
        Place(page.rectTransform, new Vector2(-205, -480), new Vector2(160, 34));
        PickerButton(panel, "Previous", "‹", new Vector2(-355, -480), new Vector2(86, 38),
            () => { auxiliaryPage--; RenderAuxiliaryDeck(); },auxiliaryPage > 0);
        PickerButton(panel, "Next", "›", new Vector2(-55, -480), new Vector2(86, 38),
            () => { auxiliaryPage++; RenderAuxiliaryDeck(); },auxiliaryPage + 1 < pages);
        PickerButton(panel, "Close", "完成", new Vector2(345, -480), new Vector2(330, 40),
            () => { auxiliaryDialogOpen = false; CloseDeckExchange(); RefreshDeckGrids(); RefreshSelectedCardInfo(); });
        FinishPicker(panel);
    }

    Button AuxiliaryButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Action action)
    {
        var button = CreateDialogButton(parent, name, GamePreferences.T(label), position, MenuVisualTheme.Raised, action);
        ((RectTransform)button.transform).sizeDelta = size;
        return button;
    }
}
