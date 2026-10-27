using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private float redesignStackOffset;
    private string redesignStackKey;
    private int redesignSelectedStack;
    private float redesignStackHeightLimit;
    private RectTransform redesignStackControls;
    private static readonly Color RedesignOpponent = new(.84f, .47f, .48f), RedesignOwn = new(.48f, .70f, .84f);
    private const float RedesignStackWidth = 430, RedesignStackTop = 24, RedesignStackHeader = 68, RedesignStackEntryHeight = 240;
    private float RedesignStackMaximumHeight => Mathf.Min(620, Mathf.Max(120, surface.rect.height - RedesignStackTop - RedesignStackHeader - redesignFooterTop - 14 - 24));

    private Text RedesignStackText(RectTransform row, string name, string value, int size, Color color, float left, float top, float width)
    {
        var text = row.Find(name)?.GetComponent<Text>();
        if (text == null) text = Label(row, name, "", Vector2.zero, Vector2.one, size, color);
        text.text = value; text.color = color; text.raycastTarget = false;
        PreferenceText.SetSize(text, size); text.resizeTextForBestFit = false;
        text.alignment = TextAnchor.UpperLeft; text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        RedesignPlace(text.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(left, -top), new Vector2(width, 600));
        text.rectTransform.sizeDelta = new Vector2(width, string.IsNullOrEmpty(value) ? 0 : Mathf.Ceil(text.preferredHeight) + 2);
        return text;
    }

    private int RedesignTargetSeat(ForgeStackTarget target)
    {
        if (target.kind == "player") return target.playerSeat > 0 ? target.playerSeat : target.ownerSeat;
        if (!target.leftZone && target.kind == "stack") {
            var current = view.stackItems?.FirstOrDefault(s => s.id == target.stackId);
            if (current?.controllerSeat > 0) return current.controllerSeat;
        }
        if (!target.leftZone && target.kind == "card" && target.zone == "Battlefield") {
            // Public battlefield membership carries current control; ownerSeat
            // alone would miscolor a stolen permanent. Do not bind a left-zone
            // target to a new incarnation with the same card ID.
            var current = view.players?.FirstOrDefault(p => p.battlefield?.Any(c => c.id == target.id) == true);
            if (current != null) return current.seat;
        }
        return target.ownerSeat > 0 ? target.ownerSeat : target.card?.ownerSeat ?? 0;
    }

    private string RedesignTargetName(ForgeStackTarget target)
    {
        int player = RedesignTargetSeat(target);
        string Mark(string name) => service?.IsSpectator != true && (view.players?.Length ?? 0) == 2 && view.players.Any(p => p.seat == player)
            ? ColoredDetail(name, "#" + ColorUtility.ToHtmlStringRGB(player == seat ? RedesignOwn : RedesignOpponent)) : SafeDetail(name);
        if (target.kind == "player") return Mark(PlayerIdentityName(player));
        if (target.kind == "stack")
        {
            var entries = view.stackItems ?? Array.Empty<ForgeStackItem>();
            int position = target.leftZone ? -1 : Array.FindIndex(entries, s => s.id == target.stackId);
            string name = GamePreferences.CardFaceName((position >= 0 ? entries[position].source?.name : null) ?? target.card?.name ?? TapPaymentText("堆叠对象", "Stack object", "スタック上の対象"));
            return Mark(name) + (position >= 0 ? "（" + StackPositionLabel(position + 1) + "）" : TapPaymentText("（已离开堆叠）", " (left the stack)", "（スタックを離れた）"));
        }
        return Mark(target.card?.faceDown == true ? TapPaymentText("面朝下的牌", "Face-down card", "裏向きのカード")
            : GamePreferences.CardFaceName(target.card?.name ?? TapPaymentText("目标", "Target", "対象")));
    }

    private string RedesignStackOwner(ForgeStackItem entry, int index, int count)
    {
        if(entry?.id==PendingCastId)return "";
        if(PendingCastEntry()!=null)index--;
        // This is an identity label, not rules prose. Keep an actual nickname
        // even when it is "You"; missing names use seats instead of role words.
        string who = SafeDetail(PlayerIdentityName(entry?.controllerSeat ?? 0));
        // Keep "Stack n" together when a long nickname wraps after it.
        string position = StackPositionLabel(index + 1).Replace(' ', '\u00A0');
        return position + (GamePreferences.Language == "en-US" ? " (" + who + ")" : "（" + who + "）");
    }

    private BattlefieldStackFrameGraphic RedesignStackFrame(RectTransform parent, float radius, Color color)
    {
        var rect = parent.Find("CompactFrame") as RectTransform;
        if (rect == null) rect = Rect(parent, "CompactFrame", Vector2.zero, parent.rect.size);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var frame = rect.GetComponent<BattlefieldStackFrameGraphic>() ?? rect.gameObject.AddComponent<BattlefieldStackFrameGraphic>();
        frame.Radius = radius; frame.color = color; frame.raycastTarget = false; frame.SetVerticesDirty();
        return frame;
    }

    private void SelectRedesignStack(ForgeStackItem entry)
    {
        if (entry == null) return;
        if (IsTargeting && !HasStackTargetChoices) { Choose(OptionIndex("stack:" + entry.id), requestId); return; }
        redesignSelectedStack = redesignSelectedStack == entry.id ? 0 : entry.id;
        inspectedStackTargets = redesignSelectedStack == 0 ? null : entry.targets;
        PresentIntegrated(connectionText.text);
    }

    private void LayoutRedesignStack(ForgeStackItem[] entries, int count)
    {
        if(count==0){
            var emptyColumn=(RectTransform)Node("StackColumn");
            if(view==null){emptyColumn.gameObject.SetActive(false);if(redesignStackControls!=null)redesignStackControls.gameObject.SetActive(false);}
            else {HideRedesignElement(emptyColumn,new Vector2(0,-12));HideRedesignElement(redesignStackControls,new Vector2(0,-12));}
            return;
        }
        const float width = RedesignStackWidth, header = RedesignStackHeader, inset = 12, artWidth = 116;
        const float innerWidth = width - 24, rowWidth = innerWidth - 12;
        float maximumHeight = RedesignStackMaximumHeight;
        foreach (string keyToRemove in redesignStackDetailKeys.Keys.Where(k => !stackCards.ContainsKey(k)).ToArray()) redesignStackDetailKeys.Remove(keyToRemove);
        redesignStackHeightLimit = maximumHeight;
        var column = (RectTransform)Node("StackColumn");
        ForgeUiMotion.On(column).Cancel();
        RedesignPlace(column, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -RedesignStackTop), new Vector2(width, header));
        SetOverlayLayer(column, 3, true);
        var shell = column.GetComponent<Image>() ?? column.gameObject.AddComponent<Image>();
        shell.color = Color.white; shell.raycastTarget = true;
        var panelGroup = column.GetComponent<CanvasGroup>();
        if (panelGroup == null) panelGroup = column.gameObject.AddComponent<CanvasGroup>();
        panelGroup.interactable = panelGroup.blocksRaycasts = !stackCollapsed;
        if (column.GetComponent<BattlefieldStackPanelTint>() == null) column.gameObject.AddComponent<BattlefieldStackPanelTint>();
        if (column.GetComponent<Outline>() is Outline shellOutline) shellOutline.enabled = false;
        RedesignStackFrame(column, 12, new Color(.43f, .55f, .59f, .38f));
        var shellShadow = column.GetComponents<Shadow>().FirstOrDefault(s => s.GetType() == typeof(Shadow)) ?? column.gameObject.AddComponent<Shadow>();
        shellShadow.effectColor = new Color(0, 0, 0, .35f); shellShadow.effectDistance = new Vector2(0, -4);
        var heading = SceneText("StackColumn/Header");
        heading.text = TapPaymentText("堆叠", "Stack", "スタック"); PreferenceText.SetSize(heading, 24);
        float headingWidth = GamePreferences.Language == "ja-JP" ? 140 : 92;
        RedesignPlace(heading.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -12), new Vector2(headingWidth, 48));
        float directionLeft = headingWidth + 20;
        RedesignStackText(column, "CompactDirection", TapPaymentText("从上到下结算", "Top → bottom", "上から順に解決"), 16, new Color(.61f, .67f, .65f), directionLeft, 18, width - 108 - directionLeft);
        var viewport = (RectTransform)Node("StackColumn/Viewport");
        ForgeUiMotion.On(viewport).Cancel();
        RedesignPlace(viewport, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -header), new Vector2(innerWidth, maximumHeight));
        viewport.GetComponent<Image>().raycastTarget = true;
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        var scroll = viewport.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 38;
        RedesignPlace(stackContent, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(innerWidth, 1));
        string key = string.Join("|", entries.Select(e => e.id)) + "/" + count + "/" + GamePreferences.Revision;
        float top = 0;
        for (int i = 0; i < count; i++)
        {
            var pair = stackCards.First(c => c.Value.row.name == "StackEntry_" + i);
            var item = pair.Value;
            var entry = item.entry;
            string effectFallback = entry == null && i < (view?.stack?.Length ?? 0) ? view.stack[i] : item.fallback.text;
            bool own = entry == null || entry.controllerSeat == seat;
            Color owner = own ? RedesignOwn : RedesignOpponent;
            var row = item.row;
            ForgeUiMotion.On(row).Cancel(); row.localRotation = Quaternion.identity;
            var incoming = entries.Where(e=>e.id!=PendingCastId).Select((e, n) => new { entry = e, number = n + 1 }).Where(e => (e.entry.targets ?? Array.Empty<ForgeStackTarget>()).Any(t => t.kind == "stack" && !t.leftZone && t.stackId == entry?.id)).ToArray();
            row.GetComponent<Image>().color = own ? new Color(.12f, .21f, .28f, .94f) : new Color(.27f, .16f, .19f, .94f);
            row.GetComponent<Image>().raycastTarget = true;
            foreach (string n in new[] { "Shadow", "Rim", "SpentMana" }) if (row.Find(n) is Transform child) child.gameObject.SetActive(false);
            item.caption.gameObject.SetActive(false);
            // Reuse the original travelling highlight, fitted to this list row.
            // Only the current top item flashes; target selection suppresses it.
            item.glow.transform.SetParent(row,false);
            item.glow.gameObject.SetActive(true);item.glow.Configure(false,entry?.id!=PendingCastId&&!IsTargeting&&i==0);
            item.glow.rectTransform.anchorMin=Vector2.zero;item.glow.rectTransform.anchorMax=Vector2.one;
            item.glow.rectTransform.offsetMin=new Vector2(6,6);item.glow.rectTransform.offsetMax=new Vector2(-6,-6);
            RedesignPlace(item.tile, new Vector2(0, 1), new Vector2(0, 1), new Vector2(inset, -10), new Vector2(240, 335));
            item.tile.localScale = Vector3.one * (artWidth / 240);
            item.tile.GetComponent<ForgeBattlefieldCardPointer>().ConfigureScrollParent(scroll);
            float textLeft = inset + artWidth + 16, detailsWidth = rowWidth - textLeft - 12, textWidth = detailsWidth - 12;
            var details = PrepareRedesignStackDetails(row);
            var info = details.content;
            var who = RedesignStackText(info, "CompactOwner", RedesignStackOwner(entry, i, count), 16, owner, 0, 0, textWidth);
            who.GetComponent<PreferenceText>().Literal = true;
            string title = item.source?.faceDown == true ? RedesignStackEffect(entry, "") : !string.IsNullOrWhiteSpace(item.source?.name)
                ? GamePreferences.CardFaceName(item.source.name) : TapPaymentText("堆叠对象", "Stack object", "スタック上のオブジェクト");
            if (string.IsNullOrWhiteSpace(item.source?.name)) item.fallback.text = title;
            var name = RedesignStackText(info, "CompactTitle", title, 28, new Color(.91f, .90f, .85f), 0, 4 + who.rectTransform.rect.height, textWidth);
            string fontName = GamePreferences.Language == "zh-CN" ? "NameCN" : GamePreferences.Language == "ja-JP" ? "NameJP" : "NameEN";
            name.font = Resources.Load<Font>("UI/CardTemplates/Fonts/" + fontName) ?? GamePreferences.Font;
            name.GetComponent<PreferenceText>().KeepFont = true;
            name.rectTransform.sizeDelta = new Vector2(textWidth, Mathf.Ceil(name.preferredHeight) + 2);
            float y = 10 + who.rectTransform.rect.height + name.rectTransform.rect.height;
            y += LayoutRedesignStackEffect(info, entry, effectFallback, y, textWidth) + 6;
            string targets = string.Join("\n", (entry?.targets ?? Array.Empty<ForgeStackTarget>()).Select(t => TapPaymentText("↳ 指向 ", "↳ Targets ", "↳ 対象：") + RedesignTargetName(t)));
            var targetText = RedesignStackText(info, "CompactTargets", targets, 18, new Color(.75f, .79f, .75f), 0, y, textWidth);
            targetText.supportRichText = true;
            targetText.GetComponent<PreferenceText>().Literal = true;
            y += targetText.rectTransform.rect.height;
            foreach (Transform child in info) if (child.name.StartsWith("CompactIncoming", StringComparison.Ordinal)) child.gameObject.SetActive(false);
            for (int n = 0; n < incoming.Length; n++)
            {
                bool fromOwn = incoming[n].entry.controllerSeat == seat;
                string sourceName = GamePreferences.CardFaceName(incoming[n].entry.source?.name ?? incoming[n].entry.description ?? "");
                string source = sourceName + "（" + StackPositionLabel(incoming[n].number) + "）";
                string hint = TapPaymentText("「" + sourceName + "」（" + StackPositionLabel(incoming[n].number) + "）的目标", "Target of " + source, "「" + source + "」の対象");
                var label = RedesignStackText(info, "CompactIncoming_" + n, hint, 18, fromOwn ? RedesignOwn : RedesignOpponent, 0, y + 8, textWidth);
                label.gameObject.SetActive(true); y += label.rectTransform.rect.height + 8;
            }
            float height = Mathf.Min(RedesignStackEntryHeight,Mathf.Max(artWidth*335/240+20,y+26));
            LayoutRedesignStackDetails(details, pair.Key, textLeft, 12, detailsWidth, y + 2,height-24);
            RedesignPlace(row, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -top), new Vector2(rowWidth, height));
            row.GetComponent<Image>().sprite = IntegratedUiUtility.RoundedRect(rowWidth, height, 3);
            if (row.GetComponent<Outline>() is Outline rowOutline) rowOutline.enabled = false;
            var border = RedesignStackFrame(row, 3, incoming.Length > 0 ? (incoming[0].entry.controllerSeat == seat ? RedesignOwn : RedesignOpponent) * new Color(1, 1, 1, .5f) : owner * new Color(1, 1, 1, .6f));
            border.enabled = true;
            if (incoming.Length == 0 && entry?.id != redesignSelectedStack) border.color = owner * new Color(1, 1, 1, .22f);
            var divider = row.Find("TopDivider") as RectTransform;
            if (divider == null) divider = Panel(row, "TopDivider", Vector2.zero, Vector2.one, new Color(.52f, .59f, .56f, .3f));
            RedesignPlace(divider, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(rowWidth, 1));
            divider.GetComponent<Image>().raycastTarget = false;
            var edge = row.Find("OwnerEdge") as RectTransform;
            if (edge == null) edge = Panel(row, "OwnerEdge", Vector2.zero, Vector2.one, owner);
            RedesignPlace(edge, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -16), new Vector2(2, height - 32));
            edge.GetComponent<Image>().color = owner; edge.GetComponent<Image>().raycastTarget = false;
            var button = row.GetComponent<Button>() ?? row.gameObject.AddComponent<Button>();
            button.targetGraphic = row.GetComponent<Image>(); button.transition = Selectable.Transition.None;
            button.interactable = item.tile.GetComponent<Button>().interactable;
            button.onClick.RemoveAllListeners(); var captured = entry; button.onClick.AddListener(() => SelectRedesignStack(captured));
            var pointer = row.GetComponent<ForgeBattlefieldCardPointer>() ?? row.gameObject.AddComponent<ForgeBattlefieldCardPointer>();
            pointer.Configure(() => { hoveredStack = item; ShowStackHover(item); }, () => { hoveredStack = null; LeaveInspectCard(); }, null, false);
            pointer.ConfigureScrollParent(scroll);
            if (entry?.id == redesignSelectedStack) row.GetComponent<Image>().color = new Color(owner.r * .3f, owner.g * .3f, owner.b * .3f, .98f);
            item.glow.transform.SetAsLastSibling();
            row.SetAsLastSibling(); top += height + (i < count - 1 ? 10 : 0);
            ShowRedesignElement(row,new Vector2(24,0),interactive:!stackCollapsed);
        }
        // Small stacks occupy only their rows. The limit creates scrolling,
        // never an empty panel below the last object.
        float viewportHeight = Mathf.Min(maximumHeight, Mathf.Max(1, top));
        column.sizeDelta = new Vector2(width, header + viewportHeight + 14);
        shell.sprite = IntegratedUiUtility.RoundedRect(width, column.rect.height, 12);
        viewport.sizeDelta = new Vector2(innerWidth, viewportHeight);
        viewport.GetComponent<Image>().sprite = IntegratedUiUtility.RoundedRect(innerWidth, viewportHeight, 3);
        viewport.gameObject.SetActive(true);
        var group = viewport.GetComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = !stackCollapsed;
        stackContent.sizeDelta = new Vector2(innerWidth, Mathf.Max(1, top));
        scroll.StopMovement();
        stackContent.anchoredPosition = new Vector2(0, key == redesignStackKey ? Mathf.Clamp(redesignStackOffset, 0, Mathf.Max(0, top - viewportHeight)) : 0);
        redesignStackKey = key;
        var bar = (RectTransform)stackScrollbar.transform;
        RedesignPlace(bar, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -header), new Vector2(6, viewportHeight));
        var barGroup = stackScrollbar.GetComponent<CanvasGroup>(); barGroup.alpha = 1; barGroup.blocksRaycasts = true;
        stackScrollbar.gameObject.SetActive(top > viewportHeight + .5f);
        if (redesignStackControls == null) redesignStackControls = Rect(surface, "RedesignStackControls", Vector2.zero, new Vector2(width, header));
        RedesignPlace(redesignStackControls, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -RedesignStackTop), new Vector2(width, header));
        SetOverlayLayer(redesignStackControls, 4, true);
        redesignStackControls.gameObject.SetActive(count > 0);
        foreach (var button in new[] { stackCollapseButton, stackViewButton }) {
            button.transform.SetParent(redesignStackControls, false);
            var image = button.GetComponent<Image>();
            image.sprite = IntegratedUiUtility.RoundedRect(34, 34, 17);
            if (button.GetComponent<Outline>() is Outline outline) outline.enabled = false;
            RedesignStackFrame((RectTransform)button.transform, 17, new Color(.6f,.75f,.81f,.8f));
            var icon = button.GetComponentInChildren<StackControlIcon>(); icon.SmoothEdges = true; icon.SetVerticesDirty();
        }
        stackCollapseButton.gameObject.SetActive(count > 0);
        stackViewButton.gameObject.SetActive(count > 0);
        RedesignPlace((RectTransform)stackCollapseButton.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -12), new Vector2(34, 34));
        RedesignPlace((RectTransform)stackViewButton.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-52, -12), new Vector2(34, 34));
        var slide = column.GetComponent<BattlefieldStackSlide>() ?? column.gameObject.AddComponent<BattlefieldStackSlide>();
        slide.SetCollapsed(stackCollapsed);
        ShowRedesignElement(column,new Vector2(0,-14),interactive:!stackCollapsed,horizontal:false);
        ShowRedesignElement(redesignStackControls,new Vector2(0,-14));
    }
}
