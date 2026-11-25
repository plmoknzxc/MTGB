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
        MenuVisualTheme.StyleDeckTool(auxiliaryEntry);
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
        var section = DeckAuxiliary.Entries(currentDeck, auxiliaryAttractions);
        if(attractionCandidates==null){attractionCandidates=cards.Where(c=>!c.forgeAmbiguous&&DeckAuxiliary.IsAttraction(c)).ToList();contraptionCandidates=cards.Where(c=>!c.forgeAmbiguous&&DeckAuxiliary.IsContraption(c)).ToList();}
        var choices = auxiliaryRegisteredOnly
            ? section.Select(entry => DeckRepository.ResolveCard(entry, cards)).ToList()
            : (auxiliaryAttractions ? attractionCandidates : contraptionCandidates);
        int pages = Mathf.Max(1, (choices.Count + AuxiliaryPageSize - 1) / AuxiliaryPageSize);
        auxiliaryPage = Mathf.Clamp(auxiliaryPage, 0, pages - 1);
        bool empty = choices.Count == 0;
        var size = empty ? new Vector2(900,482) : new Vector2(1240,694);
        var panel = PickerPanel("AuxiliaryDeck",GamePreferences.T("辅助牌组"),size);
        auxiliaryDialogOpen = true;
        float left = -size.x*.5f+36, top = size.y*.5f, footer = -top+54;
        var attractionTab = PickerButton(panel, "Attractions", "游乐设施", new Vector2(left+96,top-123), new Vector2(192, 44),
            () => { auxiliaryAttractions = true; auxiliaryPage = 0; RenderAuxiliaryDeck(); });
        var contraptionTab = PickerButton(panel, "Contraptions", "装置", new Vector2(left+294,top-123), new Vector2(192, 44),
            () => { auxiliaryAttractions = false; auxiliaryPage = 0; RenderAuxiliaryDeck(); });
        MenuVisualTheme.StyleDeckTool(auxiliaryAttractions ? attractionTab : contraptionTab,true);
        PickerButton(panel, "ToggleRegistered", auxiliaryRegisteredOnly ? "浏览可加入的牌" : "查看已加入的牌",
            empty ? new Vector2(0,-66) : new Vector2(-left-139,top-123), new Vector2(278, 44),
            () => { auxiliaryRegisteredOnly = !auxiliaryRegisteredOnly; auxiliaryPage = 0; RenderAuxiliaryDeck(); });
        string label = GamePreferences.T(auxiliaryAttractions ? "游乐设施" : "装置");
        var summary = PickerText(panel, "Summary", string.Format(GamePreferences.T("{0}：{1} 张 · 至少 {2} 张，每种牌名只能一张"),
            label, section.Sum(entry => Mathf.Max(0, entry?.count ?? 0)), auxiliaryAttractions ? 10 : 15), 19,
            MenuVisualTheme.ArchivePaper, TextAnchor.MiddleLeft);
        Place(summary.rectTransform, new Vector2(0,top-177), new Vector2(-2*left,46));
        if(empty)
        {
            string message=auxiliaryRegisteredOnly ? DeckCompanion.Text("尚未加入任何牌","No cards registered","登録されているカードはありません") :
                DeckCompanion.Text("没有可加入的牌","No cards available","追加できるカードはありません");
            var emptyLabel=PickerText(panel,"EmptySection",message,23,MenuVisualTheme.Stone,TextAnchor.MiddleCenter);
            Place(emptyLabel.rectTransform,new Vector2(0,-5),new Vector2(-2*left,56));
        }
        for (int i = 0; i < AuxiliaryPageSize && auxiliaryPage * AuxiliaryPageSize + i < choices.Count; i++)
        {
            CardDefinition card = choices[auxiliaryPage * AuxiliaryPageSize + i];
            DeckEntry registered = auxiliaryRegisteredOnly ? section[auxiliaryPage * AuxiliaryPageSize + i] : null;
            string printingKey = card?.PrintingKey ?? registered?.PrintingKey ?? "";
            var slot=PickerCardSlot(panel,i);
            var art = PickerImage(panel, "AuxiliaryCard_" + i, card == null ? MenuVisualTheme.Ink : Color.white);
            Place(art.rectTransform, slot+new Vector2(-121,0), new Vector2(118,165));
            PickerArt(art,card);
            art.preserveAspect = true;
            var name = PickerText(panel, "CardName_" + i, card == null ? GamePreferences.T("无法识别的印次") : GamePreferences.CardName(card), 20, MenuVisualTheme.ArchivePaper, TextAnchor.MiddleLeft);
            Place(name.rectTransform, slot+new Vector2(65,40), new Vector2(216,82));
            var printing = PickerText(panel, "Printing_" + i, printingKey, 16, MenuVisualTheme.Stone, TextAnchor.MiddleLeft);
            Place(printing.rectTransform, slot+new Vector2(65,-14), new Vector2(216,28));
            PickerButton(panel, "ChangeAuxiliary_" + i, auxiliaryRegisteredOnly ? "移除" : "加入",
                slot+new Vector2(62,-56), new Vector2(206,42), () => {
                    bool changed = auxiliaryRegisteredOnly ? section.Remove(registered) : DeckAuxiliary.Add(currentDeck, card, cards);
                    if (changed) MarkDeckDirty();
                    RenderAuxiliaryDeck();
                },auxiliaryRegisteredOnly || !DeckAuxiliary.ContainsName(currentDeck, card, cards));
        }
        if (pages > 1)
        {
            var page = PickerText(panel, "Page", (auxiliaryPage + 1) + " / " + pages, 17, MenuVisualTheme.Stone, TextAnchor.MiddleCenter);
            Place(page.rectTransform, new Vector2(0,footer), new Vector2(86,42));
            PickerButton(panel, "Previous", "‹", new Vector2(-76,footer), new Vector2(50,42),
                () => { auxiliaryPage--; RenderAuxiliaryDeck(); },auxiliaryPage > 0);
            PickerButton(panel, "Next", "›", new Vector2(76,footer), new Vector2(50,42),
                () => { auxiliaryPage++; RenderAuxiliaryDeck(); },auxiliaryPage + 1 < pages);
        }
        PickerButton(panel, "Close", "完成", new Vector2(-left-72,footer), new Vector2(144,44),
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
