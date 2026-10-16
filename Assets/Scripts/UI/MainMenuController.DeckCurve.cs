using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    private static readonly CardType[] CurveCardTypes = {
        CardType.Creature, CardType.Instant, CardType.Sorcery, CardType.Artifact,
        CardType.Enchantment, CardType.Planeswalker, CardType.Battle, CardType.Kindred, CardType.Land
    };

    private sealed class DeckCurveStatistics
    {
        public int total, nonlands, unresolved, otherTypes;
        public readonly SortedDictionary<float, int> costs = new SortedDictionary<float, int>();
        public readonly Dictionary<CardType, int> types = new Dictionary<CardType, int>();
    }

    private static DeckCurveStatistics BuildDeckCurveStatistics(DeckDefinition deck, IReadOnlyList<CardDefinition> definitions)
    {
        var result = new DeckCurveStatistics();
        foreach (var type in CurveCardTypes) result.types[type] = 0;
        if (deck?.cards == null) return result;
        foreach (var entry in deck.cards)
        {
            int count = Mathf.Max(0, entry?.count ?? 0);
            if (count == 0) continue;
            result.total += count;
            var card = DeckRepository.ResolveCard(entry, definitions);
            if (card == null) { result.unresolved += count; continue; }
            bool knownType = false;
            foreach (var type in CurveCardTypes)
                if (card.HasType(type)) { result.types[type] += count; knownType = true; }
            if (!knownType) result.otherTypes += count;
            if (card.HasType(CardType.Land)) continue;
            float value = DeckSortManaValue(card);
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value == float.MaxValue)
                continue;
            result.nonlands += count;
            result.costs.TryGetValue(value, out int previous);
            result.costs[value] = previous + count;
        }
        return result;
    }

    private void ShowDeckCurve()
    {
        if (currentDeck == null) return;
        var stats = BuildDeckCurveStatistics(currentDeck, cards);
        var panel = ExchangePanel(DeckCompanion.Text("主牌费用曲线", "Main deck mana curve", "メインデッキのマナカーブ"), new Vector2(1100, 780));
        var overlay = exchangeDialog;
        overlay.name = "DeckCurveDialog";
        var dismiss = AddImage(overlay.transform, "DismissBackdrop", Color.clear);
        dismiss.transform.SetAsFirstSibling();
        Stretch(dismiss.rectTransform, 0, 0, 0, 0);
        dismiss.raycastTarget = true;
        var backdrop = dismiss.gameObject.AddComponent<Button>();
        backdrop.targetGraphic = dismiss;
        backdrop.transition = Selectable.Transition.None;
        backdrop.onClick.AddListener(CloseDeckExchange);
        ObsidianArenaTheme.EnsureOutline(panel.GetComponent<Image>(), ObsidianArenaTheme.Line, new Vector2(1, -1));

        Text Label(Transform parent, string name, string value, int size, Color color, TextAnchor align)
        {
            var text = AddText(parent, name, value, size, color, align);
            text.GetComponent<PreferenceText>().Literal = true;
            text.GetComponent<PreferenceText>().KeepVerticalBounds = true;
            text.text = value;
            text.GetComponent<PreferenceText>().Apply();
            text.supportRichText = false;
            return text;
        }
        var heading = panel.Find("Heading").GetComponent<Text>();
        heading.GetComponent<PreferenceText>().Literal = true;
        Place(heading.rectTransform, new Vector2(0, 338), new Vector2(1000, 52));
        var summary = Label(panel, "DeckSummary", currentDeck.name + "  ·  " + DeckCompanion.Text("主牌 ", "Main ", "メイン ") + stats.total,
            19, MenuVisualTheme.Muted, TextAnchor.MiddleLeft);
        Place(summary.rectTransform, new Vector2(0, 278), new Vector2(1000, 52));

        var chartTitle = Label(panel, "CurveHeading", DeckCompanion.Text("非地牌 · ", "Nonland cards · ", "土地以外 · ") + stats.nonlands,
            21, MenuVisualTheme.Paper, TextAnchor.MiddleLeft);
        Place(chartTitle.rectTransform, new Vector2(-176, 212), new Vector2(648, 44));
        var typeTitle = Label(panel, "TypeHeading", DeckCompanion.Text("类型 / 张数", "Type / Count", "タイプ / 枚数"), 21, MenuVisualTheme.Paper, TextAnchor.MiddleLeft);
        Place(typeTitle.rectTransform, new Vector2(362, 212), new Vector2(276, 44));

        // Always show 0 through 7, retaining empty integer bins. Very large DIY
        // values remain exact extra bins rather than allocating millions of bars.
        var bins = new SortedSet<float>(stats.costs.Keys);
        float max = stats.costs.Count == 0 ? 7 : Mathf.Max(7, stats.costs.Keys.Max());
        for (int i = 0; i <= Mathf.Min(20, Mathf.CeilToInt(Mathf.Min(max, 20))); i++) bins.Add(i);
        var scrollImage = AddImage(panel, "CurveScroll", MenuVisualTheme.Ink);
        Place(scrollImage.rectTransform, new Vector2(-176, -8), new Vector2(648, 376));
        scrollImage.raycastTarget = true;
        var scroll = scrollImage.gameObject.AddComponent<ScrollRect>();
        var viewport = AddImage(scroll.transform, "Viewport", Color.white);
        Stretch(viewport.rectTransform, 8, 8, 8, 22);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        viewport.raycastTarget = true;
        float contentWidth = Mathf.Max(632, bins.Count * 58);
        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport.transform, false);
        content.anchorMin = Vector2.zero; content.anchorMax = new Vector2(0, 1); content.pivot = Vector2.zero;
        content.sizeDelta = new Vector2(contentWidth, 0);
        scroll.content = content; scroll.viewport = viewport.rectTransform;
        scroll.horizontal = true; scroll.vertical = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;
        int peak = Mathf.Max(1, stats.costs.Count == 0 ? 0 : stats.costs.Values.Max());
        float slot = contentWidth / bins.Count;
        var baseline = AddImage(content, "Baseline", MenuVisualTheme.Muted);
        baseline.rectTransform.anchorMin = baseline.rectTransform.anchorMax = baseline.rectTransform.pivot = Vector2.zero;
        baseline.rectTransform.anchoredPosition = new Vector2(0, 36);
        baseline.rectTransform.sizeDelta = new Vector2(contentWidth, 1);
        int index = 0;
        foreach (float value in bins)
        {
            stats.costs.TryGetValue(value, out int count);
            float height = 254f * count / peak;
            float x = (index++ + .5f) * slot;
            string cost = value.ToString("0.##", CultureInfo.InvariantCulture);
            var bar = AddImage(content, "CostBar_" + cost, MenuVisualTheme.Accent);
            bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = new Vector2(0, 0);
            bar.rectTransform.pivot = new Vector2(.5f, 0);
            bar.rectTransform.anchoredPosition = new Vector2(x, 37);
            bar.rectTransform.sizeDelta = new Vector2(Mathf.Min(46, slot - 18), height);
            var countText = Label(content, "CostCount_" + cost, count.ToString(), 18,
                count > 0 ? MenuVisualTheme.Paper : MenuVisualTheme.Muted, TextAnchor.MiddleCenter);
            var costText = Label(content, "CostLabel_" + cost, cost, 18, MenuVisualTheme.Paper, TextAnchor.MiddleCenter);
            foreach (var text in new[] { countText, costText })
            {
                text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0, 0);
                text.rectTransform.pivot = new Vector2(.5f, 0);
                text.rectTransform.sizeDelta = new Vector2(slot, 40);
            }
            countText.rectTransform.anchoredPosition = new Vector2(x, 40 + height);
            costText.rectTransform.anchoredPosition = new Vector2(x, -3);
        }
        var track = AddImage(scroll.transform, "Scrollbar", MenuVisualTheme.Raised);
        Stretch(track.rectTransform, 8, 8, 360, 6);
        var handle = AddImage(track.transform, "Handle", MenuVisualTheme.Muted);
        Stretch(handle.rectTransform, 0, 0, 0, 0);
        track.raycastTarget = handle.raycastTarget = true;
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.LeftToRight;
        scroll.horizontalScrollbar = scrollbar;
        scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        var axis = Label(panel, "CurveAxis", DeckCompanion.Text("法术力值", "Mana value", "マナ総量") +
            (contentWidth > 632 ? DeckCompanion.Text(" · 拖动或滚轮查看", " · Drag or scroll to view", " · ドラッグ・スクロールで表示") : ""),
            17, MenuVisualTheme.Muted, TextAnchor.MiddleCenter);
        Place(axis.rectTransform, new Vector2(-176, -226), new Vector2(648, 44));

        int row = 0;
        void TypeRow(string key, string name, int count)
        {
            float y = 158 - row++ * 36;
            var background = AddImage(panel, "TypeRow_" + key, row % 2 == 1 ? MenuVisualTheme.Raised : MenuVisualTheme.Panel);
            Place(background.rectTransform, new Vector2(362, y), new Vector2(276, 36));
            var label = Label(background.transform, "Type", name, GamePreferences.Language == "ja-JP" ? 16 : 18, MenuVisualTheme.Paper, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform, 12, 44, 0, 0);
            var number = Label(background.transform, "Count", count.ToString(), 18, MenuVisualTheme.Accent, TextAnchor.MiddleRight);
            Stretch(number.rectTransform, 234, 12, 0, 0);
        }
        foreach (var type in CurveCardTypes) TypeRow(type.ToString(), CurveTypeName(type), stats.types[type]);
        if (stats.otherTypes > 0) TypeRow("Other", DeckCompanion.Text("其他", "Other", "その他"), stats.otherTypes);
        if (stats.unresolved > 0) TypeRow("Unknown", DeckCompanion.Text("未识别", "Unresolved", "不明"), stats.unresolved);
        string noteText = DeckCompanion.Text("仅统计主牌，按实际张数计入；多类型牌分别计入对应类型。地不计入费用曲线。",
            "Main deck copies only. Multi-type cards count in each matching type. Lands are excluded from the curve.",
            "メインの枚数で集計。複数タイプは各タイプに計上します。土地はカーブに含みません。");
        var note = Label(panel, "StatisticsNote", noteText, 16, MenuVisualTheme.Muted, TextAnchor.MiddleLeft);
        Place(note.rectTransform, new Vector2(0, -280), new Vector2(1000, 64));
        var close = CreateDialogButton(panel, "CloseCurve", DeckCompanion.Text("关闭", "Close", "閉じる"), new Vector2(0, -344), MenuVisualTheme.Raised, CloseDeckExchange);
        var closeText = close.GetComponentInChildren<Text>();
        closeText.GetComponent<PreferenceText>().Literal = true;
        closeText.text = DeckCompanion.Text("关闭", "Close", "閉じる");
        closeText.GetComponent<PreferenceText>().Apply();
        FrameDialogButton(close);
        StartCoroutine(FitDeckCurveDialog(panel, overlay));
    }

    private IEnumerator FitDeckCurveDialog(RectTransform panel, GameObject overlay)
    {
        while (panel != null && exchangeDialog == overlay)
        {
            var available = ((RectTransform)overlay.transform).rect.size - new Vector2(48, 48);
            float scale = Mathf.Clamp01(Mathf.Min(available.x / 1100, available.y / 780));
            var size = Vector3.one * scale;
            if (panel.localScale != size) panel.localScale = size;
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                CloseDeckExchange();
                yield break;
            }
            yield return null;
        }
    }

    private static string CurveTypeName(CardType type)
    {
        switch (type)
        {
            case CardType.Creature: return DeckCompanion.Text("生物", "Creature", "クリーチャー");
            case CardType.Instant: return DeckCompanion.Text("瞬间", "Instant", "インスタント");
            case CardType.Sorcery: return DeckCompanion.Text("法术", "Sorcery", "ソーサリー");
            case CardType.Artifact: return DeckCompanion.Text("神器", "Artifact", "アーティファクト");
            case CardType.Enchantment: return DeckCompanion.Text("结界", "Enchantment", "エンチャント");
            case CardType.Planeswalker: return DeckCompanion.Text("鹏洛客", "Planeswalker", "プレインズウォーカー");
            case CardType.Battle: return DeckCompanion.Text("战役", "Battle", "バトル");
            case CardType.Kindred: return DeckCompanion.Text("亲缘", "Kindred", "同族");
            case CardType.Land: return DeckCompanion.Text("地", "Land", "土地");
            default: return type.ToString();
        }
    }
}
