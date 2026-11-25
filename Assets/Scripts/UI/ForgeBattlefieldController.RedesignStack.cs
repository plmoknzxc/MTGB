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
    private float RedesignStackWidth => IsFourPlayerScene ? Mathf.Clamp(surface.rect.width*.24f,310,448) :
        HudTheme==null ? 430 : Mathf.Clamp(surface.rect.width*.29f,430,544);
    private const float RedesignStackHeader = 68, RedesignStackEntryHeight = 240;
    private float RedesignStackTop => IsFourPlayerScene
        ? Mathf.Max(view?.awaitingInput==true?68:8,16+(fourWindowPages[2].plate?.rect.height??0)) : 84+PuzzleToolbarHeight;
    private Vector2 StackDockAnchor => Vector2.one;
    private Vector2 StackDockPosition => new Vector2(IsFourPlayerScene ? -10 : -28,-RedesignStackTop);
    private float RedesignStackMaximumHeight => IsFourPlayerScene ?
        Mathf.Max(70,surface.rect.height-RedesignStackTop-RedesignStackHeader-
            (view?.request?.kind is "InputSelectTargets" or "SelectStackTarget"?422:Mathf.Max(218,redesignFooterTop+24))) :
        Mathf.Min(620, Mathf.Max(120, surface.rect.height - RedesignStackTop - RedesignStackHeader - redesignFooterTop - 14 - 24));

    private Text RedesignStackText(RectTransform row, string name, string value, int size, Color color, float left, float top, float width, MenuTypography.Face face=MenuTypography.Face.Default)
    {
        var text = row.Find(name)?.GetComponent<Text>();
        if (text == null) text = Label(row, name, "", Vector2.zero, Vector2.one, size, color);
        if(HudTheme!=null && face!=MenuTypography.Face.Default) MenuTypography.Use(text,face);
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

    private string RedesignTargetName(ForgeStackTarget target,out int stackNumber)
    {
        stackNumber=0;
        int player = RedesignTargetSeat(target);
        string Mark(string name) => service?.IsSpectator != true && (view.players?.Length ?? 0) == 2 && view.players.Any(p => p.seat == player)
            ? ColoredDetail(name, "#" + ColorUtility.ToHtmlStringRGB(player == seat ? RedesignOwn : RedesignOpponent)) : SafeDetail(name);
        if (target.kind == "player") return Mark(PlayerIdentityName(player));
        if (target.kind == "stack")
        {
            var entries = view.stackItems ?? Array.Empty<ForgeStackItem>();
            int position = target.leftZone ? -1 : Array.FindIndex(entries, s => s.id == target.stackId);
            var source=position>=0?entries[position].source??target.card:target.card;
            string name=source?.faceDown==true?TapPaymentText("面朝下的牌","Face-down card","裏向きのカード"):
                GamePreferences.CardFaceName(source?.name??TapPaymentText("堆叠对象","Stack object","スタック上の対象"));
            stackNumber=position+1;
            if(position<0)return Mark(name)+TapPaymentText("（已离开堆叠）"," (left the stack)","（スタックを離れた）");
            return HudTheme!=null?Mark(name):Mark(name)+"（"+StackPositionLabel(stackNumber).Replace(' ','\u00A0')+"）";
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
        if (IsTargeting && !UsesTargetWindow) { Choose(OptionIndex("stack:" + entry.id), requestId); return; }
        redesignSelectedStack = redesignSelectedStack == entry.id ? 0 : entry.id;
        inspectedStackTargets = redesignSelectedStack == 0 ? null : entry.targets;
        PresentIntegrated(connectionText.text);
    }

    private float LayoutClassicStackEntry(RectTransform row, ForgeStackItem entry, string title, string effectFallback,
        int i, int count, (ForgeStackItem entry,int number)[] incoming, Color owner, float rowWidth, float artWidth, string key)
    {
        float textLeft = 12 + artWidth + 16, detailsWidth = rowWidth - textLeft - 12, textWidth = detailsWidth - 12;
        var details = PrepareRedesignStackDetails(row);
        var info = details.content;
        var name = RedesignStackText(info, "CompactTitle", title, 25, HudText, 0, 0, textWidth,MenuTypography.Face.Heading);
        name.font=GamePreferences.Font;
        name.GetComponent<PreferenceText>().KeepFont=true;
        name.rectTransform.sizeDelta=new Vector2(textWidth,Mathf.Ceil(name.preferredHeight)+2);
        float titleHeight=name.rectTransform.rect.height;
        var who = RedesignStackText(info, "CompactOwner", RedesignStackOwner(entry,i,count),16,owner,0,0,textWidth,MenuTypography.Face.Control);
        who.GetComponent<PreferenceText>().Literal = true;
        name.rectTransform.anchoredPosition=new Vector2(0,-7-who.rectTransform.rect.height);
        float y = 16 + who.rectTransform.rect.height + titleHeight;
        y += LayoutRedesignStackEffect(info, entry, effectFallback, y, textWidth) + 6;
        string targets = string.Join("\n", (entry?.targets ?? Array.Empty<ForgeStackTarget>()).Select(t => TapPaymentText("↳ 指向 ", "↳ Targets ", "↳ 対象：") + RedesignTargetName(t,out _)));
        var targetText = RedesignStackText(info, "CompactTargets", targets, 18, new Color(.75f, .79f, .75f), 0, y, textWidth);
        targetText.supportRichText = true;
        targetText.GetComponent<PreferenceText>().Literal = true;
        y += targetText.rectTransform.rect.height;
        foreach (Transform child in info) if (child.name.StartsWith("CompactIncoming", StringComparison.Ordinal)) child.gameObject.SetActive(false);
        for (int n = 0; n < incoming.Length; n++)
        {
            bool fromOwn = incoming[n].entry.controllerSeat == seat;
            string sourceName = GamePreferences.CardFaceName(incoming[n].entry.source?.name ?? incoming[n].entry.description ?? "");
            string position = StackPositionLabel(incoming[n].number).Replace(' ', '\u00A0');
            string source = sourceName + "（" + position + "）";
            string hint = TapPaymentText("「" + sourceName + "」（" + position + "）的目标", "Target of " + source, "「" + source + "」の対象");
            var label = RedesignStackText(info, "CompactIncoming_" + n, hint, 18, fromOwn ? RedesignOwn : RedesignOpponent, 0, y + 8, textWidth);
            label.gameObject.SetActive(true); y += label.rectTransform.rect.height + 8;
        }
        float height = Mathf.Min(RedesignStackEntryHeight,Mathf.Max(artWidth*335/240+20,y+26));
        LayoutRedesignStackDetails(details, key, textLeft, 12, detailsWidth, y + 2,height-24);
        return height;
    }

    private void LayoutRedesignStack(ForgeStackItem[] entries, int count)
    {
        if(count==0){
            var emptyColumn=(RectTransform)Node("StackColumn");
            if(view==null){emptyColumn.gameObject.SetActive(false);if(redesignStackControls!=null)redesignStackControls.gameObject.SetActive(false);}
            else {HideRedesignElement(emptyColumn,new Vector2(0,-12));HideRedesignElement(redesignStackControls,new Vector2(0,-12));}
            return;
        }
        float width=RedesignStackWidth;
        const float header=RedesignStackHeader, inset=12;
        float artWidth = HudTheme!=null?Mathf.Round(width*.244f):116;
        float innerWidth = width - 24, rowWidth = innerWidth - 12;
        float maximumHeight = RedesignStackMaximumHeight;
        foreach (string keyToRemove in redesignStackDetailKeys.Keys.Where(k => !stackCards.ContainsKey(k)).ToArray()) redesignStackDetailKeys.Remove(keyToRemove);
        redesignStackHeightLimit = maximumHeight;
        var column = (RectTransform)Node("StackColumn");
        ForgeUiMotion.On(column).Cancel();
        RedesignPlace(column, StackDockAnchor, StackDockAnchor, StackDockPosition, new Vector2(width, header));
        var shell = column.GetComponent<Image>() ?? column.gameObject.AddComponent<Image>();
        shell.color = new Color(.10f,.115f,.12f,.94f); shell.raycastTarget = IsFourPlayerScene;
        var panelGroup = column.GetComponent<CanvasGroup>();
        if (panelGroup == null) panelGroup = column.gameObject.AddComponent<CanvasGroup>();
        panelGroup.interactable = panelGroup.blocksRaycasts = !stackCollapsed;
        var shellTint = column.GetComponent<BattlefieldStackPanelTint>() ?? column.gameObject.AddComponent<BattlefieldStackPanelTint>();
        shellTint.enabled = HudTheme == null;
        if (HudTheme != null) shell.color = HudInk;
        if (column.GetComponent<Outline>() is Outline shellOutline) shellOutline.enabled = false;
        if(HudTheme==null) RedesignStackFrame(column, 8, HudEdge).enabled = true;
        var shellShadow = column.GetComponents<Shadow>().FirstOrDefault(s => s.GetType() == typeof(Shadow)) ?? column.gameObject.AddComponent<Shadow>();
        shellShadow.enabled = false;
        var headerSurface = column.Find("StackHeaderSurface") as RectTransform;
        if(headerSurface==null)headerSurface=Panel(column,"StackHeaderSurface",Vector2.zero,new Vector2(width-16,54),new Color(.055f,.08f,.10f,.88f));
        headerSurface.SetAsFirstSibling();
        var headerImage=headerSurface.GetComponent<Image>();
        headerImage.sprite=IntegratedUiUtility.RoundedRect(64,64,6);headerImage.type=Image.Type.Sliced;headerImage.raycastTarget=false;
        headerImage.color=HudRaised;
        RedesignPlace(headerSurface,new Vector2(0,1),new Vector2(0,1),new Vector2(8,-6),new Vector2(width-16,54));
        if(HudTheme!=null){
            DuelSurface(headerSurface,BattlefieldHudGraphic.Surface.StackHeader,HudAccent);
            DuelSurface(column,BattlefieldHudGraphic.Surface.Stack,HudEdge);
        }
        var heading = SceneText("StackColumn/Header");
        heading.text = TapPaymentText("堆叠", "Stack", "スタック"); PreferenceText.SetSize(heading,HudTheme!=null?27:25);
        heading.color=HudText;heading.fontStyle=HudTheme!=null?FontStyle.Normal:FontStyle.Bold;
        if (HudTheme != null) MenuTypography.Use(heading,MenuTypography.Face.Heading);
        float headingWidth = GamePreferences.Language == "ja-JP" ? 140 : 92;
        string directionText=TapPaymentText("从上到下结算", "Top → bottom", "上から順に解決");
        if(IsFourPlayerScene)directionText=TapPaymentText("自上而下","Top first","上から解決");
        if(HudTheme!=null){
            if(stackCollapsed)directionText=TapPaymentText($"{count} 项",$"{count} {(count==1?"item":"items")}",$"{count} 件");
            float left=20,right=width-110,space=20;
            heading.alignment=TextAnchor.MiddleLeft;
            heading.horizontalOverflow=HorizontalWrapMode.Wrap;
            headingWidth=Mathf.Min(Mathf.Ceil(heading.preferredWidth)+4,(right-left-space)*.52f);
            RedesignPlace(heading.rectTransform,Vector2.up,Vector2.up,new Vector2(left,-6),new Vector2(headingWidth,52));
            float directionX=left+headingWidth+space;
            var direction=RedesignStackText(column,"CompactDirection",directionText,18,HudMuted,directionX,6,right-directionX,MenuTypography.Face.Control);
            direction.alignment=TextAnchor.MiddleLeft;
            direction.rectTransform.sizeDelta=new Vector2(right-directionX,52);
            var hairline=column.Find("HeaderTextDivider") as RectTransform;
            if(hairline==null)hairline=Panel(column,"HeaderTextDivider",Vector2.zero,Vector2.one,HudRule);
            RedesignPlace(hairline,Vector2.up,Vector2.up,new Vector2(directionX-space*.5f,-22),new Vector2(1,20));
            hairline.GetComponent<Image>().raycastTarget=false;
        }else{
            RedesignPlace(heading.rectTransform,Vector2.up,Vector2.up,new Vector2(16,-12),new Vector2(headingWidth,48));
            float directionX=headingWidth+20;
            RedesignStackText(column,"CompactDirection",directionText,15,HudMuted,directionX,20,width-108-directionX);
        }
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
        int pendingOffset=entries.FirstOrDefault()?.id==PendingCastId?1:0;
        float top = 0;
        for (int i = 0; i < count; i++)
        {
            var pair = stackCards.First(c => c.Value.row.name == "StackEntry_" + i);
            var item = pair.Value;
            var entry = item.entry;
            string effectFallback = entry == null && i < (view?.stack?.Length ?? 0) ? view.stack[i] : item.fallback.text;
            bool own = entry == null || entry.controllerSeat == seat;
            Color owner = HudTheme!=null || IsFourPlayerScene ? StackSeatTint(entry?.controllerSeat??0) : own?RedesignOwn:RedesignOpponent;
            var row = item.row;
            ForgeUiMotion.On(row).Cancel(); row.localRotation = Quaternion.identity;
            var incoming = entries.Where(e=>e!=null&&e.id!=PendingCastId).Select((e, n) => (entry:e,number:n+1)).Where(e => (e.entry.targets ?? Array.Empty<ForgeStackTarget>()).Any(t => t.kind == "stack" && !t.leftZone && t.stackId == entry?.id)).ToArray();
            // Keep ownership in the edge and label; large tinted slabs obscure
            // the artwork and make neighboring stack objects look like buttons.
            row.GetComponent<Image>().color = i==0?HudRaised:HudInk;
            row.GetComponent<Image>().raycastTarget = true;
            foreach (string n in new[] { "Shadow", "Rim", "SpentMana" }) if (row.Find(n) is Transform child) child.gameObject.SetActive(false);
            item.caption.gameObject.SetActive(false);
            bool abilityPortrait=HudTheme!=null && entry!=null && entry.kind!="spell" && entry.id!=PendingCastId;
            item.image.enabled=!abilityPortrait;
            item.glow.WarmMetal=true;
            item.glow.Theme=HudTheme;
            item.glow.CornerRadius=(HudTheme!=null?BattlefieldHudGraphic.EntryRadius:7)-item.glow.rectTransform.offsetMin.x;
            item.glow.Configure(false,entry?.id!=PendingCastId&&!IsTargeting&&i==0);
            RedesignPlace(item.tile, new Vector2(0, 1), new Vector2(0, 1), new Vector2(inset, -10), new Vector2(240, 335));
            item.tile.localScale = Vector3.one * (artWidth / 240);
            item.tile.GetComponent<ForgeBattlefieldCardPointer>().ConfigureScrollParent(scroll);
            string title = item.source?.faceDown == true ? RedesignStackEffect(entry, "") : !string.IsNullOrWhiteSpace(item.source?.name)
                ? GamePreferences.CardFaceName(item.source.name) : TapPaymentText("堆叠对象", "Stack object", "スタック上のオブジェクト");
            if (string.IsNullOrWhiteSpace(item.source?.name)) item.fallback.text = title;
            float height = HudTheme!=null
                ? LayoutThemedStackEntry(item,title,effectFallback,i+1-pendingOffset,incoming,owner,rowWidth,artWidth,abilityPortrait,pair.Key)
                : LayoutClassicStackEntry(row,entry,title,effectFallback,i,count,incoming,owner,rowWidth,artWidth,pair.Key);
            if(row.Find("SelectedGlow")!=null)
                PresentSelectionGlow(row,item.tile,entry!=null && StackTargetSelected("stack:"+entry.id));
            RedesignPlace(row, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -top), new Vector2(rowWidth, height));
            if(HudTheme==null)row.GetComponent<Image>().sprite = IntegratedUiUtility.RoundedRect(rowWidth, height, 7);
            if (row.GetComponent<Outline>() is Outline rowOutline) rowOutline.enabled = false;
            var border = RedesignStackFrame(row, 7, i==0?HudAccent:HudEdge);
            border.enabled = true;
            if (incoming.Length > 0) border.color = Color.Lerp(HudEdge,owner,.55f);
            if (entry?.id == redesignSelectedStack) border.color = HudAccent;
            var divider = row.Find("TopDivider") as RectTransform;
            if (divider == null) divider = Panel(row, "TopDivider", Vector2.zero, Vector2.one, new Color(.69f, .66f, .55f, .28f));
            RedesignPlace(divider, new Vector2(0, 1), new Vector2(0, 1), new Vector2(7,0), new Vector2(rowWidth-14, 1));
            divider.GetComponent<Image>().color=HudTheme!=null?Color.clear:HudRule;
            divider.GetComponent<Image>().raycastTarget = false;
            var edge = row.Find("OwnerEdge") as RectTransform;
            if (edge == null) edge = Panel(row, "OwnerEdge", Vector2.zero, Vector2.one, owner);
            RedesignPlace(edge, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -12), new Vector2(4, height - 24));
            edge.GetComponent<Image>().color = HudTheme!=null?Color.clear:owner; edge.GetComponent<Image>().raycastTarget = false;
            var button = row.GetComponent<Button>() ?? row.gameObject.AddComponent<Button>();
            button.targetGraphic = row.GetComponent<Image>(); button.transition = Selectable.Transition.None;
            button.interactable = item.tile.GetComponent<Button>().interactable;
            button.onClick.RemoveAllListeners(); var captured = entry; button.onClick.AddListener(() => SelectRedesignStack(captured));
            var pointer = row.GetComponent<ForgeBattlefieldCardPointer>() ?? row.gameObject.AddComponent<ForgeBattlefieldCardPointer>();
            pointer.Configure(() => { hoveredStack = item; ShowStackHover(item); }, () => { hoveredStack = null; LeaveInspectCard(); }, null, false);
            pointer.ConfigureScrollParent(scroll);
            if (entry?.id == redesignSelectedStack) row.GetComponent<Image>().color = HudSelected;
            if (HudTheme!=null) DuelSurface(row,BattlefieldHudGraphic.Surface.Entry,owner,entry!=null&&entry.id==redesignSelectedStack);
            item.glow.transform.SetAsLastSibling();
            row.SetAsLastSibling(); top += height + (i < count - 1 ? 10 : 0);
            ShowRedesignElement(row,new Vector2(24,0),interactive:!stackCollapsed);
        }
        // Small stacks occupy only their rows. The limit creates scrolling,
        // never an empty panel below the last object.
        float viewportHeight = Mathf.Min(maximumHeight, Mathf.Max(1, top));
        bool foldUp=HudTheme!=null;
        column.sizeDelta = new Vector2(width, foldUp&&stackCollapsed?header:header+viewportHeight+(foldUp?20:14));
        shell.sprite = IntegratedUiUtility.RoundedRect(width, column.rect.height, 8);
        viewport.sizeDelta = new Vector2(innerWidth, viewportHeight);
        viewport.GetComponent<Image>().sprite = IntegratedUiUtility.RoundedRect(innerWidth, viewportHeight, 3);
        viewport.gameObject.SetActive(true);
        var group = viewport.GetComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = !stackCollapsed;
        bool needsScrollbar = top > viewportHeight + .5f;
        float contentInset = foldUp && !needsScrollbar ? (innerWidth - rowWidth) * .5f : 0;
        stackContent.sizeDelta = new Vector2(innerWidth - 2 * contentInset, Mathf.Max(1, top));
        scroll.StopMovement();
        stackContent.anchoredPosition = new Vector2(contentInset, key == redesignStackKey ? Mathf.Clamp(redesignStackOffset, 0, Mathf.Max(0, top - viewportHeight)) : 0);
        redesignStackKey = key;
        var bar = (RectTransform)stackScrollbar.transform;
        RedesignPlace(bar, new Vector2(1, 1), new Vector2(1, 1), new Vector2(HudTheme != null ? -12 : -8, -header), new Vector2(6, viewportHeight));
        var barGroup = stackScrollbar.GetComponent<CanvasGroup>(); barGroup.alpha = 1; barGroup.blocksRaycasts = !stackCollapsed;
        stackScrollbar.gameObject.SetActive(needsScrollbar);
        if (redesignStackControls == null) redesignStackControls = Rect(surface, "RedesignStackControls", Vector2.zero, new Vector2(width, header));
        RedesignPlace(redesignStackControls, StackDockAnchor, StackDockAnchor, StackDockPosition, new Vector2(width, header));
        SetOverlayLayer(redesignStackControls,HudTheme!=null ? ReadingLayerOffset+1 : 4,true);
        redesignStackControls.gameObject.SetActive(count > 0);
        foreach (var button in new[] { stackCollapseButton, stackViewButton }) {
            button.transform.SetParent(redesignStackControls, false);
            var image = button.GetComponent<Image>();
            image.sprite = IntegratedUiUtility.RoundedRect(34, 34, 5);
            image.color=HudRaised;
            if (button.GetComponent<Outline>() is Outline outline) outline.enabled = false;
            var icon = button.GetComponentInChildren<StackControlIcon>(); icon.SmoothEdges=true;icon.SetVerticesDirty();
            if(foldUp) StyleBattleIcon(button);
            else {RedesignStackFrame((RectTransform)button.transform,5,HudEdge);icon.color=HudAccent;}
        }
        stackCollapseButton.gameObject.SetActive(count > 0);
        stackViewButton.gameObject.SetActive(count > 0);
        var controlSize=foldUp?new Vector2(40,38):new Vector2(34,34);
        RedesignPlace((RectTransform)stackCollapseButton.transform,Vector2.one,Vector2.one,new Vector2(-10,-12),controlSize);
        RedesignPlace((RectTransform)stackViewButton.transform,Vector2.one,Vector2.one,new Vector2(foldUp?-57:-52,-12),controlSize);
        if(foldUp){
            // Clip the rows to the animated bottom edge. Their sizes and scroll offset stay intact.
            if(!column.TryGetComponent<RectMask2D>(out _))column.gameObject.AddComponent<RectMask2D>();
        }else{
            var slide=column.GetComponent<BattlefieldStackSlide>()??column.gameObject.AddComponent<BattlefieldStackSlide>();
            slide.SetCollapsed(stackCollapsed);
        }
        ShowRedesignElement(column,new Vector2(0,-14),interactive:!stackCollapsed||IsFourPlayerScene,horizontal:foldUp);
        // On its first activation Unity registers the nested canvas with its parent.
        // Set its sorting order afterwards so the visible stack receives the first click.
        SetOverlayLayer(column,HudTheme!=null?ReadingLayerOffset:3,true);
        ShowRedesignElement(redesignStackControls,new Vector2(0,-14));
    }
}
