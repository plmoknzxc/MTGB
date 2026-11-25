using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Persistent effects, match events, and the card inspector share one panel.
public sealed partial class ForgeBattlefieldController
{
    private RectTransform battleSidebar, sidebarStatusContent, sidebarHistoryContent, sidebarDetailContent;
    private RectTransform journalPages;
    private JournalPageScroll journalWheel;
    private RectTransform sidebarStatusView, sidebarHistoryView, sidebarDetailView;
    private Scrollbar sidebarStatusScrollbar, sidebarHistoryScrollbar,sidebarDetailScrollbar;
    private RectTransform sidebarOpponentStatus, sidebarOwnStatus;
    private Button sidebarStatusTab, sidebarHistoryTab, sidebarDetailTab;
    private RawImage journalTabStrip;
    private Button sidebarOwnStatusTab,sidebarOpponentStatusTab;
    private Button sidebarToggle;
    private StackControlIcon sidebarToggleIcon;
    private bool sidebarCollapsed;
    private bool? sidebarHoverDetailMode;
    private float sidebarSlideProgress;
    private Vector2 sidebarLayoutSize;
    private const float SidebarOpenRight=472, SidebarClosedShift=472;
    private const float JournalTopInset=258;
    private const float JournalBottomInset=292;
    private const float JournalToggleSize=40, JournalToggleInset=12;
    private const int JournalScanLayer=ReadingLayerOffset+1;
    private sealed class SidebarHistoryEntry
    {
        public long id;
        public string text;
        public ForgeCardView[] cards;
        public int turn;
        public int actorSeat;
        public int turnSeat;
        public string phase;
    }
    private readonly List<SidebarHistoryEntry> sidebarEvents=new();
    private readonly HashSet<long> sidebarNoticeIds=new();
    private readonly HashSet<int> sidebarStackIds=new();
    private readonly Dictionary<int,int> sidebarLife=new();
    private readonly HashSet<int> previousUsableSources=new();
    private string sidebarHistoryKey;
    private float sidebarHistoryMeasuredHeight;
    private long historyInspectId=long.MinValue;
    private ForgeCardView historyInspectCard;
    private bool sidebarHistoryInspection;
    private Button historyInspectClose;
    private readonly List<(RectTransform row, ForgeCardView card)> pendingHistoryArt=new();
    private Image historySelectedRow;
    private enum SidebarPage { Status, History, Detail }
    private SidebarPage sidebarPage;
    private bool sidebarShowOwn=true;
    private bool sidebarHasDetail;
    private int sidebarOpponentMarkerCount, sidebarOwnMarkerCount;
    private const float SidebarEntryWidth=412;
    private const float SidebarMarkerBodyHeight=108;
    private const float SidebarMarkerGap=8;
    private sealed class SidebarMarkerEntry
    {
        public string key,appearance;
        public RectTransform root,header,details,arrow;
        public BattlefieldHudGraphic finish;
        public float bodyHeight,detailHeight,repeatedSummaryHeight,progress;
        public float presence=1;
        public int transitionFrame;
        public bool leaving;
        public CanvasGroup fade;
        public RectMask2D clip;
        public float VisibleFraction=>Mathf.SmoothStep(0,1,presence);
    }
    private readonly List<SidebarMarkerEntry> sidebarOpponentEntries=new(),sidebarOwnEntries=new();
    private readonly HashSet<int> sidebarInitializedSeats=new();
    private readonly Dictionary<string,float> sidebarExpansionProgress=new();
    private readonly HashSet<string> sidebarExpandedMarkers=new();

    private void BuildBattleSidebar()
    {
        if(worldTable==null)return;
        foreach(var oldName in new[]{"BattleJournal","BattleJournalToggle"}){
            var old=surface.Find(oldName);
            if(old==null)continue;
            old.gameObject.SetActive(false);
            if(Application.isPlaying)Destroy(old.gameObject);
        }
        battleSidebar=Rect(surface,"BattleJournal",Vector2.zero,new Vector2(460,500));
        battleSidebar.anchorMin=new Vector2(0,0);battleSidebar.anchorMax=new Vector2(0,1);
        battleSidebar.pivot=new Vector2(0,.5f);
        battleSidebar.offsetMin=new Vector2(12,JournalBottomInset);battleSidebar.offsetMax=new Vector2(SidebarOpenRight,-JournalTopInset);
        SetOverlayLayer(battleSidebar,ReadingLayerOffset,true);
        journalPages=Panel(battleSidebar,"JournalPages",Vector2.zero,Vector2.zero,HudInk);
        journalPages.anchorMin=Vector2.zero;journalPages.anchorMax=Vector2.one;
        journalPages.offsetMin=journalPages.offsetMax=Vector2.zero;
        journalPages.gameObject.AddComponent<CanvasGroup>();
        journalWheel=journalPages.gameObject.AddComponent<JournalPageScroll>();
        var baseImage=journalPages.GetComponent<Image>();
        baseImage.sprite=IntegratedUiUtility.RoundedRect(64,64,6);
        baseImage.type=Image.Type.Sliced;
        var texture=HudTheme==null?Resources.Load<Texture2D>("UI/BattlefieldRedesign/JournalSlate"):null;
        if(texture!=null){
            var textureRect=Rect(journalPages,"SlateMaterial",Vector2.zero,Vector2.zero);
            textureRect.anchorMin=Vector2.zero;textureRect.anchorMax=Vector2.one;
            textureRect.offsetMin=textureRect.offsetMax=Vector2.zero;
            var material=textureRect.gameObject.AddComponent<RawImage>();
            material.texture=texture;material.color=new Color(.72f,.68f,.57f,.045f);material.raycastTarget=false;
            textureRect.SetAsFirstSibling();
        }
        var border=Rect(journalPages,"Edge",Vector2.zero,Vector2.zero);
        border.anchorMin=Vector2.zero;border.anchorMax=Vector2.one;
        border.offsetMin=border.offsetMax=Vector2.zero;
        var edge=border.gameObject.AddComponent<BattlefieldStackFrameGraphic>();
        edge.Radius=6;edge.Thickness=1f;edge.color=HudEdge;edge.raycastTarget=false;
        edge.enabled=HudTheme==null;
        var header=Panel(journalPages,"JournalHeader",Vector2.zero,new Vector2(460,40),HudRaised);
        header.GetComponent<Image>().raycastTarget=false;
        header.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(64,64,5);
        header.GetComponent<Image>().type=Image.Type.Sliced;
        RedesignPlace(header,Vector2.up,Vector2.up,new Vector2(4,-4),new Vector2(452,HudTheme!=null?48:36));
        var headerRule=Panel(header,"HeaderRule",Vector2.zero,new Vector2(432,1),HudRule);
        headerRule.GetComponent<Image>().raycastTarget=false;
        RedesignPlace(headerRule,Vector2.zero,Vector2.zero,new Vector2(10,0),new Vector2(432,1));
        if(HudTheme!=null){
            var rail=Rect(journalPages,"PageRail",Vector2.zero,Vector2.one);
            journalTabStrip=rail.gameObject.AddComponent<RawImage>();
            journalTabStrip.raycastTarget=false;
        }
        var title=Label(header,"StatusTitle",TapPaymentText("对局记录","Match journal","対戦記録"),Vector2.zero,new Vector2(390,28),HudTheme!=null?22:21,HudText);
        title.fontStyle=HudTheme!=null?FontStyle.Normal:FontStyle.Bold;
        MenuTypography.Use(title,MenuTypography.Face.Heading);
        title.alignment=TextAnchor.MiddleLeft;
        title.verticalOverflow=VerticalWrapMode.Overflow;
        RedesignPlace(title.rectTransform,Vector2.up,Vector2.up,HudTheme!=null?new Vector2(16,-7):new Vector2(16,-6),HudTheme!=null?new Vector2(352,34):new Vector2(390,28));
        sidebarStatusContent=Scroll(journalPages,"StatusScroll",Vector2.zero,new Vector2(446,200));
        sidebarStatusView=(RectTransform)sidebarStatusContent.parent;
        sidebarStatusScrollbar=BuildSidebarScrollbar("StatusScrollbar",sidebarStatusView);
        sidebarOpponentStatus=Rect(sidebarStatusContent,"OpponentStatus",Vector2.zero,new Vector2(416,122));
        sidebarOwnStatus=Rect(sidebarStatusContent,"OwnStatus",Vector2.zero,new Vector2(416,122));
        BuildStatusRow(sidebarOpponentStatus,false);
        BuildStatusRow(sidebarOwnStatus,true);
        sidebarStatusTab=CreateSidebarTab("StatusTab",TapPaymentText("状态","Status","状態"),()=>SelectSidebarTab(SidebarPage.Status));
        sidebarHistoryTab=CreateSidebarTab("HistoryTab",TapPaymentText("历史","History","履歴"),()=>SelectSidebarTab(SidebarPage.History));
        sidebarDetailTab=CreateSidebarTab("DetailTab",TapPaymentText("详情","Details","詳細"),()=>SelectSidebarTab(SidebarPage.Detail));
        RefreshJournalTabTheme();
        sidebarOwnStatusTab=CreateSidebarTab("OwnStatusTab",TapPaymentText("我方","Your side","自分"),()=>SelectSidebarStatusSide(true),filter:true);
        sidebarOpponentStatusTab=CreateSidebarTab("OpponentStatusTab",TapPaymentText("对方","Opponent","相手"),()=>SelectSidebarStatusSide(false),filter:true);
        sidebarHistoryContent=Scroll(journalPages,"HistoryScroll",Vector2.zero,new Vector2(434,200));
        sidebarHistoryView=(RectTransform)sidebarHistoryContent.parent;
        sidebarHistoryView.GetComponent<ScrollRect>().onValueChanged.AddListener(_=>LoadVisibleHistoryArt());
        sidebarHistoryScrollbar=BuildSidebarScrollbar("HistoryScrollbar",sidebarHistoryView);
        sidebarDetailContent=Scroll(journalPages,"DetailScroll",Vector2.zero,new Vector2(434,200));
        sidebarDetailView=(RectTransform)sidebarDetailContent.parent;
        sidebarDetailScrollbar=BuildSidebarScrollbar("DetailScrollbar",sidebarDetailView);
        BuildSidebarDetail();
        BuildFourPlayerStatusSidebar();
        LayoutBattleSidebar();
        if(IsFourPlayerScene)SelectFourPlayerStatusSeat(seat>0?seat:1);
        else SelectSidebarStatusSide(sidebarShowOwn);
        SelectSidebarTab(SidebarPage.Status);
        ApplySidebarDetailPreference();
        sidebarToggle=StackIcon(surface,"BattleJournalToggle",StackControlIcon.Kind.Left,()=>{
            sidebarCollapsed=!sidebarCollapsed;
            if(sidebarCollapsed){CloseHistoryInspector();CloseJournalScan();}
            sidebarToggleIcon.SetKind(sidebarCollapsed?StackControlIcon.Kind.Right:StackControlIcon.Kind.Left);
            if(IsDuelTable)PresentIntegrated(connectionText.text);
        });
        var toggleRect=(RectTransform)sidebarToggle.transform;
        RedesignPlace(toggleRect,Vector2.up,new Vector2(1,1),new Vector2(SidebarOpenRight-JournalToggleInset,-JournalTopInset-10),new Vector2(JournalToggleSize,JournalToggleSize));
        var toggleImage=sidebarToggle.GetComponent<Image>();
        toggleImage.raycastTarget=true;
        toggleImage.sprite=IntegratedUiUtility.RoundedRect(30,30,5);
        toggleImage.color=HudRaised;
        var toggleOutline=sidebarToggle.GetComponent<Outline>();if(toggleOutline!=null)toggleOutline.enabled=false;
        sidebarToggleIcon=sidebarToggle.GetComponentInChildren<StackControlIcon>();
        sidebarToggleIcon.color=HudAccent;sidebarToggleIcon.SmoothEdges=true;
        if(HudTheme!=null) StyleBattleIcon(sidebarToggle); else StyleHudSurface(toggleRect,HudRaised,HudEdge);
        SetOverlayLayer(toggleRect,JournalScanLayer+1,true);
    }

    private void UpdateBattleSidebarSlide()
    {
        if(battleSidebar==null||sidebarToggle==null)return;
        if(Vector2.Distance(sidebarLayoutSize,surface.rect.size)>1f){
            sidebarLayoutSize=surface.rect.size;
            LayoutBattleSidebar();
            if(journalCard!=null && sidebarHasDetail)DrawJournalCard(false);
        }
        ApplySidebarDetailPreference();
        UpdateSidebarExpansions();
        sidebarSlideProgress=Mathf.MoveTowards(sidebarSlideProgress,sidebarCollapsed?1:0,Time.unscaledDeltaTime/.25f);
        float travel=Mathf.SmoothStep(0,1,sidebarSlideProgress);
        // Translate a fixed-width page once. Changing both offsets separately also resizes every ScrollRect twice.
        var position=battleSidebar.anchoredPosition;
        position.x=12-travel*SidebarClosedShift;
        battleSidebar.anchoredPosition=position;
        float toggleRight=Mathf.Lerp(SidebarOpenRight-JournalToggleInset,JournalToggleSize+4,travel);
        ((RectTransform)sidebarToggle.transform).anchoredPosition=new Vector2(toggleRight,-JournalTopInset-10);
        sidebarToggleIcon.SetKind(sidebarCollapsed?StackControlIcon.Kind.Right:StackControlIcon.Kind.Left);
        UpdateSidebarScrollbars();
    }

    private Scrollbar BuildSidebarScrollbar(string name,RectTransform viewport,Transform trackParent=null)
    {
        var root=Panel(trackParent!=null?trackParent:journalPages,name,Vector2.zero,new Vector2(8,100),HudTheme!=null?Color.clear:HudRule);
        root.GetComponent<Image>().raycastTarget=true;
        Color thumb=HudMuted;
        thumb.a=.62f;
        var handle=Panel(root,"Handle",Vector2.zero,new Vector2(4,40),HudTheme!=null?thumb:HudAccent);
        if(HudTheme!=null){
            handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;
            handle.offsetMin=new Vector2(3,0);handle.offsetMax=new Vector2(-2,0);
        }
        handle.GetComponent<Image>().raycastTarget=true;
        var bar=root.gameObject.AddComponent<Scrollbar>();
        bar.targetGraphic=handle.GetComponent<Image>();bar.handleRect=handle;
        bar.direction=Scrollbar.Direction.BottomToTop;bar.value=1;
        var scroll=viewport.GetComponent<ScrollRect>();
        scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.verticalScrollbar=bar;
        return bar;
    }

    private Button CreateSidebarTab(string name,string caption,Action click,bool filter=false)
    {
        var root=Panel(journalPages,name,Vector2.zero,new Vector2(140,36),HudRaised);
        root.GetComponent<Image>().sprite=HudTheme!=null?null:IntegratedUiUtility.RoundedRect(64,64,4);
        root.GetComponent<Image>().type=Image.Type.Sliced;
        var button=root.gameObject.AddComponent<Button>();
        button.targetGraphic=root.GetComponent<Image>();button.transition=Selectable.Transition.None;
        button.onClick.AddListener(()=>click());
        if(HudTheme!=null&&!filter){
            root.GetComponent<Image>().color=Color.clear;
            var captionText=CardSymbolText.Create(root,"Label",17,Color.white);
            captionText.GetComponent<PreferenceText>().SetMenuFace(MenuTypography.Face.Control);
            captionText.text=caption;
            captionText.alignment=TextAlignmentOptions.Center;
            captionText.characterSpacing=16.7f;
            captionText.textWrappingMode=TextWrappingModes.NoWrap;
            captionText.raycastTarget=false;
            button.targetGraphic=captionText;
            return button;
        }
        var label=Label(root,"Label",caption,Vector2.zero,new Vector2(134,30),HudTheme!=null?(filter?13:16):18,HudText);
        if(HudTheme!=null)MenuTypography.Use(label,MenuTypography.Face.Control);
        label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
        label.verticalOverflow=VerticalWrapMode.Overflow;
        var line=Panel(root,"ActiveRule",new Vector2(0,-17),new Vector2(106,2),HudAccent);
        line.GetComponent<Image>().raycastTarget=false;
        if(HudTheme!=null&&!IsFourPlayerScene){
            PreferenceText.SetSize(label,18);
            label.alignment=TextAnchor.MiddleLeft;
            label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
            label.rectTransform.offsetMin=new Vector2(14,2);label.rectTransform.offsetMax=new Vector2(-59,-2);
            var count=Label(root,"Count","0",Vector2.zero,new Vector2(49,34),18,HudText);
            MenuTypography.Use(count,MenuTypography.Face.Reading);count.alignment=TextAnchor.MiddleRight;count.raycastTarget=false;
            RedesignPlace(count.rectTransform,new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-14,1),new Vector2(49,34));
            line.anchorMin=Vector2.zero;line.anchorMax=Vector2.right;line.pivot=new Vector2(.5f,0);
            line.offsetMin=Vector2.zero;line.offsetMax=new Vector2(0,2);
        }else if(HudTheme!=null){
            RedesignPlace(line,new Vector2(0,.5f),new Vector2(.5f,.5f),new Vector2(7,0),new Vector2(4,4));
            line.localRotation=Quaternion.Euler(0,0,45);
        }
        return button;
    }

    private void RefreshJournalTabTheme()
    {
        if(journalTabStrip==null)return;
        var theme=HudTheme;
        journalTabStrip.texture=theme.Load<Texture2D>(theme.JournalTabStrip)??Resources.Load<Texture2D>(MenuThemeCatalog.Default.JournalTabStrip);
        journalTabStrip.color=Color.white;
        PaintJournalTab(sidebarStatusTab,sidebarPage==SidebarPage.Status);
        PaintJournalTab(sidebarHistoryTab,sidebarPage==SidebarPage.History);
        PaintJournalTab(sidebarDetailTab,sidebarPage==SidebarPage.Detail);
        if(!IsFourPlayerScene&&sidebarOwnStatusTab!=null&&sidebarOpponentStatusTab!=null){
            PaintJournalTab(sidebarOwnStatusTab,sidebarShowOwn,filter:true);
            PaintJournalTab(sidebarOpponentStatusTab,!sidebarShowOwn,filter:true);
        }
    }

    private void PaintJournalTab(Button tab,bool selected,bool filter=false)
    {
        if(HudTheme!=null&&!filter){
            var ink=ColorBlock.defaultColorBlock;
            ink.normalColor=selected?HudTheme.JournalTabActiveText:HudTheme.JournalTabText;
            ink.selectedColor=Color.Lerp(ink.normalColor,HudTheme.ArchivePaper,.2f);
            ink.highlightedColor=Color.Lerp(ink.normalColor,HudTheme.ArchivePaper,.45f);
            ink.pressedColor=HudTheme.ArchivePaper;
            tab.targetGraphic.color=Color.white;
            tab.transition=Selectable.Transition.ColorTint;
            tab.colors=ink;
            return;
        }
        var label=tab.transform.Find("Label").GetComponent<Text>();
        var rule=tab.transform.Find("ActiveRule").gameObject;
        if(HudTheme!=null&&!IsFourPlayerScene){
            Color accent=Color.Lerp(tab==sidebarOwnStatusTab?RedesignOwn:RedesignOpponent,HudTheme.ArchivePaper,.25f);
            Color fill=Color.Lerp(HudInk,accent,selected?.20f:.04f);
            fill.a=1;
            var colors=ColorBlock.defaultColorBlock;
            colors.normalColor=fill;
            colors.highlightedColor=Color.Lerp(fill,accent,.15f);
            colors.selectedColor=Color.Lerp(fill,accent,.06f);
            colors.pressedColor=Color.Lerp(fill,accent,.25f);
            tab.image.color=Color.white;tab.transition=Selectable.Transition.ColorTint;tab.colors=colors;
            label.color=selected?HudText:HudMuted;
            tab.transform.Find("Count").GetComponent<Text>().color=selected?accent:HudMuted;
            rule.GetComponent<Image>().color=selected?accent:HudRule;
            rule.SetActive(true);
            return;
        }
        if(HudTheme==null){
            tab.image.color=selected?HudSelected:HudRaised;
            rule.SetActive(selected);
        }else{
            rule.SetActive(selected&&!IsFourPlayerScene);
            rule.GetComponent<Image>().color=HudAccent;
            label.fontStyle=FontStyle.Normal;
            tab.transition=Selectable.Transition.ColorTint;
            var colors=ColorBlock.defaultColorBlock;
            Color fill=Color.Lerp(HudInk,HudRaised,selected?.7f:.15f);
            fill.a=.96f;
            colors.normalColor=fill;
            colors.selectedColor=Color.Lerp(fill,HudAccent,.10f);
            colors.highlightedColor=Color.Lerp(fill,HudAccent,.16f);
            colors.pressedColor=Color.Lerp(fill,HudAccent,.28f);
            tab.image.color=Color.white;
            tab.colors=colors;
        }
        label.color=selected?HudText:Color.Lerp(HudMuted,HudRaised,.15f);
    }

    private void BuildStatusRow(RectTransform row,bool own)
    {
        var backing=row.gameObject.AddComponent<Image>();
        backing.sprite=IntegratedUiUtility.RoundedRect(64,64,5);
        backing.type=Image.Type.Sliced;
        backing.color=Color.clear;
        backing.raycastTarget=false;
        var label=Label(row,"Owner",own?TapPaymentText("你","You","あなた"):TapPaymentText("对手","Opponent","相手"),new Vector2(-147,42),new Vector2(58,25),18,own?new Color(.61f,.82f,.91f):new Color(.94f,.64f,.61f));
        label.alignment=TextAnchor.MiddleLeft;label.raycastTarget=false;
        var count=Label(row,"MarkerCount","",Vector2.zero,new Vector2(92,25),15,new Color(.62f,.72f,.75f));
        count.alignment=TextAnchor.MiddleRight;count.raycastTarget=false;
        label.gameObject.SetActive(false);
        count.gameObject.SetActive(false);
        Rect(row,"MarkerHost",new Vector2(0,-24),new Vector2(SidebarEntryWidth,(SidebarMarkerBodyHeight+SidebarMarkerGap)*2));
    }

    private static float SidebarHeaderTextHeight(Text label,float minimum)
    {
        label.horizontalOverflow=HorizontalWrapMode.Wrap;
        label.verticalOverflow=VerticalWrapMode.Overflow;
        label.alignment=TextAnchor.UpperLeft;
        return Mathf.Max(minimum,Mathf.Ceil(label.preferredHeight)+2);
    }

    private static float SidebarEntriesHeight(List<SidebarMarkerEntry> entries)
        =>entries.Sum(entry=>entry.VisibleFraction*(entry.bodyHeight+SidebarMarkerGap+(entry.detailHeight-entry.repeatedSummaryHeight)*Mathf.SmoothStep(0,1,entry.progress)));

    private static float SidebarStatusHeight(List<SidebarMarkerEntry> entries)
        =>Mathf.Max(50,16+SidebarEntriesHeight(entries));

    private void LayoutSidebarStatus(RectTransform row,float top,float height,List<SidebarMarkerEntry> entries)
    {
        RedesignPlace(row,Vector2.up,Vector2.up,new Vector2(0,-top),new Vector2(SidebarEntryWidth+16,height));
        RedesignPlace((RectTransform)row.Find("Owner"),new Vector2(0,1),new Vector2(0,1),new Vector2(16,-7),new Vector2(72,28));
        RedesignPlace((RectTransform)row.Find("MarkerCount"),new Vector2(1,1),new Vector2(1,1),new Vector2(-14,-8),new Vector2(92,26));
        var host=(RectTransform)row.Find("MarkerHost");
        RedesignPlace(host,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-8),new Vector2(SidebarEntryWidth,Mathf.Max(30,SidebarEntriesHeight(entries))));
        var empty=host.Find("Empty");
        if(empty!=null)empty.GetComponent<Text>().enabled=entries.Count==0;
        float offset=0;
        foreach(var entry in entries){
            float open=Mathf.SmoothStep(0,1,entry.progress);
            float expanded=entry.detailHeight*open,headerHeight=entry.bodyHeight-entry.repeatedSummaryHeight*open;
            float visible=entry.VisibleFraction;
            if(entry.leaving||entry.presence<1){
                if(entry.fade==null)entry.fade=entry.root.gameObject.AddComponent<CanvasGroup>();
                if(entry.clip==null)entry.clip=entry.root.gameObject.AddComponent<RectMask2D>();
            }
            if(entry.fade!=null){
                entry.fade.alpha=visible;
                entry.fade.interactable=entry.fade.blocksRaycasts=!entry.leaving&&visible>.65f;
                entry.clip.enabled=visible<1;
            }
            entry.header.sizeDelta=new Vector2(SidebarEntryWidth,headerHeight);
            RedesignPlace(entry.root,Vector2.up,Vector2.up,new Vector2(0,-offset),new Vector2(SidebarEntryWidth,(headerHeight+expanded)*visible));
            RedesignPlace(entry.details,Vector2.up,Vector2.up,new Vector2(0,-headerHeight),new Vector2(SidebarEntryWidth,expanded));
            entry.arrow.localRotation=Quaternion.Euler(0,0,-90+180*open);
            if(entry.finish!=null)entry.finish.SetRecordOpen(entry.progress);
            offset+=(headerHeight+SidebarMarkerGap+expanded)*visible;
        }
    }

    private void LayoutBattleSidebar()
    {
        if(battleSidebar==null)return;
        battleSidebar.offsetMax=new Vector2(battleSidebar.offsetMax.x,-JournalTopInset);
        var tabs=GamePreferences.HoverCardDetails?new[]{sidebarStatusTab,sidebarHistoryTab}:new[]{sidebarStatusTab,sidebarHistoryTab,sidebarDetailTab};
        float pageBottom=LayoutJournalPages(tabs);
        bool sideStrip=HudTheme!=null&&!IsFourPlayerScene;
        float filtersTop=pageBottom+(HudTheme!=null?4:0);
        float statusTop=filtersTop+(IsFourPlayerScene?76:sideStrip?49:40);
        var selectedFourEntries=fourStatusEntries.TryGetValue(fourStatusSelectedSeat,out var fourEntries)?fourEntries:null;
        float pageHeight=sidebarPage==SidebarPage.Detail?float.MaxValue:
            sidebarPage==SidebarPage.History?pageBottom+12+sidebarHistoryMeasuredHeight+12:
            (IsFourPlayerScene?statusTop+SidebarStatusHeight(selectedFourEntries??sidebarOwnEntries)
                +FourPlayerCounterHeight(fourStatusSelectedSeat)
                :statusTop+SidebarStatusHeight(sidebarShowOwn?sidebarOwnEntries:sidebarOpponentEntries))+16;
        float bottom=sidebarPage==SidebarPage.Detail?JournalBottomInset:
            Mathf.Max(JournalBottomInset,surface.rect.height-JournalTopInset-Mathf.Max(210,pageHeight));
        battleSidebar.offsetMin=new Vector2(battleSidebar.offsetMin.x,bottom);
        float opponentHeight=SidebarStatusHeight(sidebarOpponentEntries);
        float ownHeight=SidebarStatusHeight(sidebarOwnEntries);
        LayoutSidebarStatus(sidebarOpponentStatus,8,opponentHeight,sidebarOpponentEntries);
        LayoutSidebarStatus(sidebarOwnStatus,8,ownHeight,sidebarOwnEntries);
        var statusTabs=new[]{sidebarOwnStatusTab,sidebarOpponentStatusTab};
        float filterWidth=sideStrip?(battleSidebar.rect.width-38)/2:208;
        for(int i=0;i<statusTabs.Length;i++){
            var tab=(RectTransform)statusTabs[i].transform;
            RedesignPlace(tab,Vector2.up,new Vector2(0,1),new Vector2((sideStrip?18:12)+i*(filterWidth+(sideStrip?2:8)),-filtersTop),new Vector2(filterWidth,sideStrip?42:32));
            if(!sideStrip)RedesignPlace((RectTransform)tab.Find("Label"),new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(filterWidth-12,28));
            if(HudTheme==null)RedesignPlace((RectTransform)tab.Find("ActiveRule"),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,1),new Vector2(184,2));
        }
        float pageFoot=HudTheme!=null?18:12;
        foreach(var viewport in new[]{sidebarStatusView,sidebarHistoryView,sidebarDetailView}){
            viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;
            viewport.offsetMin=new Vector2(12,pageFoot);
            viewport.offsetMax=new Vector2(-12,viewport==sidebarStatusView?-statusTop:-pageBottom);
        }
        foreach(var bar in new[]{sidebarStatusScrollbar,sidebarHistoryScrollbar,sidebarDetailScrollbar}){
            var rect=(RectTransform)bar.transform;
            rect.anchorMin=new Vector2(1,0);rect.anchorMax=new Vector2(1,1);
            float rightInset=HudTheme!=null?12:6;
            rect.offsetMin=new Vector2(-rightInset-(HudTheme!=null?8:4),pageFoot);
            rect.offsetMax=new Vector2(-rightInset,bar==sidebarStatusScrollbar?-statusTop:-pageBottom);
        }
        sidebarStatusContent.sizeDelta=new Vector2(sidebarStatusView.rect.width,16+(sidebarShowOwn?ownHeight:opponentHeight));
        if(IsFourPlayerScene)LayoutFourPlayerStatusSidebar(filtersTop);
        LayoutJournalScan();
        UpdateSidebarScrollbars();
    }

    private float LayoutJournalPages(Button[] tabs)
    {
        if(HudTheme==null){
            float width=tabs.Length==2?208:140,step=tabs.Length==2?216:145;
            for(int i=0;i<tabs.Length;i++)
                RedesignPlace((RectTransform)tabs[i].transform,Vector2.up,Vector2.up,new Vector2(12+i*step,-44),new Vector2(width,36));
            return 84;
        }
        const float top=50,outerInset=4,h=49;
        float w=battleSidebar.rect.width-2*outerInset;
        RedesignPlace(journalTabStrip.rectTransform,Vector2.up,Vector2.up,new Vector2(outerInset,-top),new Vector2(w,h));
        int row=(tabs.Length==2?3:0)+Mathf.Clamp((int)sidebarPage,0,tabs.Length-1);
        journalTabStrip.uvRect=new Rect(0,(4-row)/5f,1,1/5f);
        // The artwork is authored on a 434-unit grid with centred 141-unit tabs.
        float widthPerTab=w*(423f/434)/tabs.Length;
        float x=outerInset+w*(5.5f/434);
        foreach(var tab in tabs){
            var rect=(RectTransform)tab.transform;
            RedesignPlace(rect,Vector2.up,Vector2.up,new Vector2(x,-top),new Vector2(widthPerTab,h));
            var label=(RectTransform)rect.Find("Label");
            RedesignPlace(label,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-h*.06f),new Vector2(widthPerTab-8,h*.55f));
            x+=widthPerTab;
        }
        return top+h+2;
    }

    private void UpdateSidebarExpansions()
    {
        bool changed=false;
        Advance(sidebarOpponentEntries);
        Advance(sidebarOwnEntries);
        foreach(var entries in fourStatusEntries.Values)Advance(entries);
        if(changed)LayoutBattleSidebar();

        void Advance(List<SidebarMarkerEntry> entries){
            for(int i=entries.Count-1;i>=0;i--){
                var entry=entries[i];
                // A costly snapshot may arrive just before Update. Its elapsed frame time predates this row.
                float step=entry.transitionFrame==Time.frameCount?0:Mathf.Min(.05f,Time.unscaledDeltaTime)/.24f;
                float presence=Mathf.MoveTowards(entry.presence,entry.leaving?0:1,step);
                if(entry.presence!=presence){entry.presence=presence;changed=true;}
                if(entry.leaving){
                    if(presence==0){RemoveStatusRow(entry);entries.RemoveAt(i);changed=true;}
                    continue;
                }
                float target=sidebarExpandedMarkers.Contains(entry.key)?1:0;
                float next=Mathf.MoveTowards(entry.progress,target,Time.unscaledDeltaTime/.24f);
                if(next==entry.progress)continue;
                entry.progress=next;sidebarExpansionProgress[entry.key]=next;changed=true;
            }
        }
    }

    private static void RemoveStatusRow(SidebarMarkerEntry entry)
    {
        entry.root.gameObject.SetActive(false);
        entry.root.SetParent(null,false);
        if(Application.isPlaying)Destroy(entry.root.gameObject);
        else DestroyImmediate(entry.root.gameObject);
    }

    private float BuildLokiTrickDetails(RectTransform details,ForgeCardView guide)
    {
        const float pad=14,width=SidebarEntryWidth-2*pad,stateWidth=96;
        if(guide.lokiTricks==null)
            return 24+JournalText(details,"Unavailable",TapPaymentText("这份旧记录只保存了已用数量。","This older record contains only the total used.","この古い記録には使用数のみ保存されています。"),pad,10,width,16,HudMuted);
        float top=12;
        top+=JournalText(details,"HeroPriorityRules",TapPaymentText(
            "英雄共用回合，可互看手牌。英雄出牌后，洛基先响应，再从该英雄的下家依次问回本人。全员让过只结算栈顶一项；还有堆叠就重新询问。清空后，轮到最先出牌的英雄的下家。",
            "Heroes share a turn and may see each other’s hands. For a hero’s spell or ability, Loki responds first, then the heroes starting after its controller and ending with them. All passing resolves one item. Ask again for the next item. Once clear, play passes to the hero after the opener.",
            "英雄はターンと手札の情報を共有します。英雄の呪文や能力にはまずロキ、その後は使用者の次の英雄から本人まで順に対応します。全員がパスすると一つだけ解決し、次の項目で改めて確認します。空になれば最初に行動した英雄の次の英雄の番です。"),pad,top,width,16,HudText)+12;
        top+=JournalText(details,"TrickReminder",TapPaymentText("有诡计指示物的项目视为已用，移去指示物后可再选。","A mischief counter marks a trick as used. Removing it makes the trick available again.","悪戯カウンターがある項目は使用済みです。取り除くと再び選べます。"),pad,top,width,15,HudMuted)+12;
        foreach(var trick in guide.lokiTricks){
            float title=JournalHeading(details,"TrickName",trick.name,pad,top,width-stateWidth,19);
            string state=trick.marked?TapPaymentText("已用","Used","使用済み"):TapPaymentText("未用","Unused","未使用");
            float badge=JournalText(details,"TrickState",state,pad+width-stateWidth,top+2,stateWidth,15,trick.marked?HudAccent:HudMuted,true);
            top+=Mathf.Max(title,badge)+4;
            top+=JournalText(details,"TrickEffect",trick.rules,pad,top,width,16,HudText)+10;
            JournalLine(details,pad,top,width);
            top+=12;
        }
        return top;
    }

    private RectTransform SidebarStatusRow(bool own)=>own?sidebarOwnStatus:sidebarOpponentStatus;

    private static string SidebarMarkerKey(ForgeCardView card)
        =>card.markerKind=="delayed"?"delayed:"+card.ownerSeat+":"+card.printingKey+":"+card.backFace+":"+card.name+":"+(card.markerGroupKey??"")
        :card.emblem?"emblem:"+card.ownerSeat+":"+card.printingKey+":"+card.name+":"+card.currentRules
        :"id:"+card.id;

    private static string SidebarDungeonRoom(ForgeCardView card)
    {
        string room=card.dungeonRoom??"";
        if(string.IsNullOrWhiteSpace(room))return "";
        string source=((string.IsNullOrWhiteSpace(card.originalRules)?card.currentRules:card.originalRules)??"").Replace("\\n","\n");
        string line=source.Split('\n').FirstOrDefault(value=>value.StartsWith(room+" —",StringComparison.Ordinal)
            ||card.name=="Undercity"&&room=="Archive"&&value.StartsWith("Archives —",StringComparison.Ordinal));
        if(line!=null){
            string localized=DungeonRoomText(card,line);
            int separator=localized.IndexOfAny(new[]{'—','―','～','〜'});
            if(separator>0)room=localized.Substring(0,separator).Trim();
        }
        // A projected dungeon marker can omit its printed room script. Keep the
        // UI label localized even when DungeonRoomText has no line to match.
        return room==card.dungeonRoom?SidebarDungeonRoomFallback(room):room;
    }

    private static string SidebarDungeonRoomFallback(string room)=>room switch {
        "Cave Entrance"=>TapPaymentText("洞穴入口",room,"洞窟の入口"),
        "Goblin Lair"=>TapPaymentText("鬼怪巢穴",room,"ゴブリンのねぐら"),
        "Mine Tunnels"=>TapPaymentText("矿坑隧道",room,"鉱山のトンネル"),
        "Storeroom"=>TapPaymentText("储藏室",room,"貯蔵室"),
        "Dark Pool"=>TapPaymentText("黑暗水池",room,"暗い池"),
        "Fungi Cavern"=>TapPaymentText("蕈菇洞窟",room,"菌類の洞窟"),
        "Temple of Dumathoin"=>TapPaymentText("杜马松神殿",room,"ドゥマソインの神殿"),
        "Yawning Portal"=>TapPaymentText("无底门酒馆",room,"大口亭"),
        "Dungeon Level"=>TapPaymentText("地城层",room,"ダンジョンの階層"),
        "Goblin Bazaar"=>TapPaymentText("鬼怪集市",room,"ゴブリンの市場"),
        "Twisted Caverns"=>TapPaymentText("曲折洞窟",room,"曲がりくねった洞窟"),
        "Lost Level"=>TapPaymentText("失落层",room,"失われた階層"),
        "Runestone Caverns"=>TapPaymentText("符文石洞窟",room,"ルーン石の洞窟"),
        "Muiral's Graveyard"=>TapPaymentText("缪拉尔的墓园",room,"ミューラルの墓地"),
        "Deep Mines"=>TapPaymentText("深层矿坑",room,"深い鉱山"),
        "Mad Wizard's Lair"=>TapPaymentText("疯狂法师的巢穴",room,"狂える魔道士のねぐら"),
        "Trapped Entry"=>TapPaymentText("陷阱入口",room,"罠のある入口"),
        "Veils of Fear"=>TapPaymentText("恐惧帷幕",room,"恐怖の帳"),
        "Oubliette"=>TapPaymentText("地牢",room,"地下牢"),
        "Sandfall Cell"=>TapPaymentText("流沙囚室",room,"砂の独房"),
        "Cradle of the Death God"=>TapPaymentText("死神的摇篮",room,"死神の揺りかご"),
        "Secret Entrance"=>TapPaymentText("秘密入口",room,"秘密の入口"),
        "Forge"=>TapPaymentText("锻炉",room,"鍛冶場"),
        "Lost Well"=>TapPaymentText("失落水井",room,"失われた井戸"),
        "Trap!"=>TapPaymentText("陷阱！",room,"罠！"),
        "Arena"=>TapPaymentText("竞技场",room,"闘技場"),
        "Stash"=>TapPaymentText("藏物间",room,"隠し場所"),
        "Archive" or "Archives"=>TapPaymentText("档案室",room,"書庫"),
        "Catacombs"=>TapPaymentText("地下墓穴",room,"地下墓地"),
        "Throne of the Dead Three"=>TapPaymentText("死亡三神王座",room,"死せる三者の玉座"),
        _=>room
    };

    private string SidebarMarkerSummary(ForgePlayerView player,ForgeCardView card)
    {
        if (IsCommanderEventMarker(card)) return card.emblem
            ? player.effectMarkers?.FirstOrDefault(c=>c.name=="山中之心")?.markerStatus ?? "" : card.markerStatus ?? "";
        if(IsPublicRuleHelper(card)&&card.helperArtName=="The Ring"){
            int count=Math.Max(player.ringTemptedCount,0);
            if(count==0){
                var recorded=System.Text.RegularExpressions.Regex.Match(card.markerStatus??"",@"魔戒已引诱你\s*(\d+)\s*次");
                if(recorded.Success)int.TryParse(recorded.Groups[1].Value,out count);
            }
            int level=SidebarRingLevel(player,count);
            return TapPaymentText($"等级 {level}/4 · 引诱 {count} 次",$"Level {level}/4 · tempted {count} times",$"段階 {level}/4 · 誘惑 {count} 回");
        }
        if(card.markerKind=="dungeon"){
            string room=SidebarDungeonRoom(card);
            if(!string.IsNullOrWhiteSpace(room))return TapPaymentText("当前房间：","Current room: ","現在の部屋：")+room;
        }
        string speed=SpeedMarkerValue(card);
        if(speed.Length>0)return TapPaymentText("速度 ","Speed ","速度 ")+speed+"/4";
        if(string.IsNullOrWhiteSpace(card.markerStatus)&&card.markerKind!="delayed")return "";
        string localized=SidebarMarkerDetailRules(card).Split(new[]{"\n\n"},StringSplitOptions.None)[0].Replace('\n',' ');
        localized=localized.Trim();
        return localized.Length>42?localized.Substring(0,41)+"…":localized;
    }

    private bool IsCommanderEventMarker(ForgeCardView card) =>
        view?.commanderEvent == OnlineRoomConfig.Nauctis && card.name is "广袤海域" or "雄伟大洋" ||
        view?.commanderEvent == OnlineRoomConfig.Loki && card.name == "洛基的诡计" ||
        view?.commanderEvent == OnlineRoomConfig.Arkenstone && card.name is "阿肯宝石" or "山中之心";

    private string SidebarMarkerDetailRules(ForgeCardView card)
    {
        if (IsCommanderEventMarker(card)) {
            if(view.commanderEvent==OnlineRoomConfig.Nauctis){
                string condition=TapPaymentText("落坟20——在你位于广袤海域期间，只要你坟墓场中有二十张或更多永久物牌，你便进入雄伟大洋。",
                    "Descend 20 — While you are in the preceding region, enter the Grand Marine if there are twenty or more permanent cards in your graveyard.",
                    "落魄20 ―― 直前の海域にいる間、あなたの墓地にパーマネント・カードが20枚以上あるなら、雄大なる海に入る。");
                if(card.name=="广袤海域")return condition;
                string blessing=card.originalRules??"";
                // Older recordings only contain the blessing after the region changes.
                int split=blessing.IndexOf("撒饵诱鱼",StringComparison.Ordinal);
                blessing=split>=0?blessing.Substring(split):"撒饵诱鱼——"+blessing;
                return condition+"\n\n"+TapPaymentText(blessing,
                    "At the beginning of your first main phase, create a 3/3 red Shark creature token. The next creature spell you cast this turn has devour 3 and “When this creature enters, if it devoured a creature, it dives.” It can't be blocked until it deals combat damage while attacking.",
                    "あなたの最初のメイン・フェイズの開始時に、赤の3/3のサメ・クリーチャー・トークン１体を生成する。このターンに次に唱えるクリーチャー呪文は貪食３と「このクリーチャーが戦場に出たとき、これがクリーチャーを貪食していたなら、これは潜水する。」を持つ。それが攻撃中に戦闘ダメージを与えるまで、それはブロックされない。");
            }
            if(card.emblem)return TapPaymentText(
                "你的生物得 +1/+1。结束步骤开始时抓一张牌。\n你的维持开始时得 1 点胜利点，累计 3 点获胜。\n生物对你造成战斗伤害时，其操控者夺得宝石。",
                "Your creatures get +1/+1. Draw a card at the beginning of your end step.\nAt the beginning of your upkeep, gain a victory point. Three points win the game.\nWhen a creature deals combat damage to you, its controller takes the Arkenstone.",
                "あなたのクリーチャーは+1/+1。あなたの終了ステップの開始時に1枚引く。\nあなたのアップキープの開始時に勝利点を1点得る。合計3点で勝利。\nあなたに戦闘ダメージを与えたクリーチャーのコントローラーが宝石を得る。");
            return card.originalRules;
        }
        if(card.markerKind=="delayed")return LocalizedPendingTrigger(card);
        string rules=card.emblem?EmblemLocalizedRules(card):DisplayMarkerRules(card);
        if(string.IsNullOrWhiteSpace(rules)&&card.helperArtName=="An Enduring Story")return TapPaymentText("你已获得历世书传，直到本盘游戏结束。","You have an enduring story for the rest of the game.","このゲームの終了時まで、あなたは永続する物語を持つ。");
        if(string.IsNullOrWhiteSpace(rules)&&card.helperArtName=="City's Blessing")return TapPaymentText("你已获得城市祝福，直到本盘游戏结束。","You have the city's blessing for the rest of the game.","このゲームの終了時まで、あなたは都市の承認を得ている。");
        if(card.markerKind!="dungeon")return rules;
        string original=(card.originalRules??"").Replace("\\n","\n");
        string current=(card.currentRules??"").Replace("\\n","\n");
        string Normalize(string value)=>System.Text.RegularExpressions.Regex.Replace(value??"",@"\s+"," ").Trim();
        if(!string.IsNullOrWhiteSpace(original) && (string.IsNullOrWhiteSpace(current)||Normalize(original)==Normalize(current)))
            return GamePreferences.FeatureRules(new CardFeatureDefinition{cardName=card.name,description=original});
        if(!string.IsNullOrEmpty(card.markerStatus)&&rules.StartsWith(card.markerStatus,StringComparison.Ordinal))
            return rules.Substring(card.markerStatus.Length).TrimStart('\n','\r');
        return rules;
    }

    private string LocalizedPendingTrigger(ForgeCardView card)
    {
        string native=(card.currentRules??"").Trim();
        if(native.Length==0)return "";
        var dash=System.Text.RegularExpressions.Regex.Match(native,@"^Return (.+) \((\d+)\) to your hand at the beginning of the next end step\.$");
        if(dash.Success)return TapPaymentText("在下一个结束步骤开始时，将","At the beginning of the next end step, return ","次の終了ステップの開始時に、")
            +GamePreferences.CardFaceName(dash.Groups[1].Value)+" ("+dash.Groups[2].Value+")"
            +TapPaymentText("移回其拥有者的手上。"," to its owner's hand.","をオーナーの手札に戻す。");
        if(native=="Return exiled card to the battlefield with a +1/+1 counter on it.")return TapPaymentText(
            "将被放逐的牌移回战场，且其上有一个+1/+1指示物。",native,"追放されているカードを+1/+1カウンター１個が置かれた状態で戦場に戻す。");
        var rebound=System.Text.RegularExpressions.Regex.Match(native,@"^At the beginning of your next upkeep, you may cast (.+) \(Original\) without paying its mana cost\.$");
        if(rebound.Success)return TapPaymentText("在你的下一个维持开始时，你可以不支付法术力费用施放","At the beginning of your next upkeep, you may cast ","次のあなたのアップキープの開始時に、マナ・コストを支払うことなく")
            +GamePreferences.CardFaceName(rebound.Groups[1].Value)
            +TapPaymentText("。"," without paying its mana cost.","を唱えてもよい。");
        return GamePreferences.NativeText(native);
    }

    private static int SidebarRingLevel(ForgePlayerView player,int temptedCount)
        =>Mathf.Clamp(player.ringLevel>0?player.ringLevel:Math.Min(4,temptedCount),0,4);

    // Regions are measured on the printed dungeon helper faces, from the top left.
    private static Rect DungeonRoomRect(float x,float y,float width,float height)
        =>new Rect(x/265f,y/370f,width/265f,height/370f);
    private static bool TrySidebarDungeonRoomRegion(ForgeCardView card,out Rect region)
    {
        region=(card.helperArtName+"/"+card.dungeonRoom) switch {
            "Lost Mine of Phandelver/Cave Entrance"=>DungeonRoomRect(91,56,83,52),
            "Lost Mine of Phandelver/Goblin Lair"=>DungeonRoomRect(21,110,108,73),
            "Lost Mine of Phandelver/Mine Tunnels"=>DungeonRoomRect(132,110,111,73),
            "Lost Mine of Phandelver/Storeroom"=>DungeonRoomRect(21,184,74,81),
            "Lost Mine of Phandelver/Dark Pool"=>DungeonRoomRect(96,184,73,81),
            "Lost Mine of Phandelver/Fungi Cavern"=>DungeonRoomRect(170,184,73,81),
            "Lost Mine of Phandelver/Temple of Dumathoin"=>DungeonRoomRect(21,267,222,52),
            "Dungeon of the Mad Mage/Yawning Portal"=>DungeonRoomRect(21,55,222,28),
            "Dungeon of the Mad Mage/Dungeon Level"=>DungeonRoomRect(21,85,222,25),
            "Dungeon of the Mad Mage/Goblin Bazaar"=>DungeonRoomRect(21,112,108,53),
            "Dungeon of the Mad Mage/Twisted Caverns"=>DungeonRoomRect(131,112,112,53),
            "Dungeon of the Mad Mage/Lost Level"=>DungeonRoomRect(21,168,222,24),
            "Dungeon of the Mad Mage/Runestone Caverns"=>DungeonRoomRect(21,194,108,58),
            "Dungeon of the Mad Mage/Muiral's Graveyard"=>DungeonRoomRect(131,194,112,58),
            "Dungeon of the Mad Mage/Deep Mines"=>DungeonRoomRect(21,255,222,24),
            "Dungeon of the Mad Mage/Mad Wizard's Lair"=>DungeonRoomRect(21,281,222,39),
            "Tomb of Annihilation/Trapped Entry"=>DungeonRoomRect(21,54,222,55),
            "Tomb of Annihilation/Veils of Fear"=>DungeonRoomRect(21,111,108,80),
            "Tomb of Annihilation/Oubliette"=>DungeonRoomRect(131,111,112,135),
            "Tomb of Annihilation/Sandfall Cell"=>DungeonRoomRect(21,193,108,53),
            "Tomb of Annihilation/Cradle of the Death God"=>DungeonRoomRect(21,248,222,71),
            "Undercity/Secret Entrance"=>DungeonRoomRect(21,73,222,38),
            "Undercity/Forge"=>DungeonRoomRect(21,113,108,45),
            "Undercity/Lost Well"=>DungeonRoomRect(131,113,112,45),
            "Undercity/Trap!"=>DungeonRoomRect(21,160,68,58),
            "Undercity/Arena"=>DungeonRoomRect(91,160,66,58),
            "Undercity/Stash"=>DungeonRoomRect(159,160,84,58),
            "Undercity/Archive" or "Undercity/Archives"=>DungeonRoomRect(21,220,108,49),
            "Undercity/Catacombs"=>DungeonRoomRect(131,220,112,49),
            "Undercity/Throne of the Dead Three"=>DungeonRoomRect(21,271,222,51),
            _=>default
        };
        return region.width>0;
    }

    private static Rect SidebarSpeedRegion(string speed)
        =>speed switch {
            "1"=>new Rect(.36f,.025f,.28f,.18f),
            "2"=>new Rect(.36f,.195f,.28f,.18f),
            "3"=>new Rect(.36f,.36f,.28f,.18f),
            "4"=>new Rect(.73f,.755f,.20f,.14f),
            _=>default
        };

    private static void SidebarImageHighlight(Image image,string name,Rect region,bool round,bool speed=false)
    {
        var bounds=Rect(image.rectTransform,name,Vector2.zero,Vector2.zero);
        var size=image.rectTransform.rect.size;
        RedesignPlace(bounds,new Vector2(0,1),new Vector2(0,1),
            new Vector2(region.x*size.x,-region.y*size.y),new Vector2(region.width*size.x,region.height*size.y));
        var tint=bounds.gameObject.AddComponent<Image>();
        tint.sprite=IntegratedUiUtility.RoundedRect(64,64,round?32:5);
        tint.type=Image.Type.Sliced;tint.color=speed?new Color(.15f,.72f,1f,.17f):new Color(1f,.75f,.18f,.12f);tint.raycastTarget=false;
        var edge=Rect(bounds,"CurrentOutline",Vector2.zero,Vector2.zero);
        edge.anchorMin=Vector2.zero;edge.anchorMax=Vector2.one;edge.offsetMin=edge.offsetMax=Vector2.zero;
        edge.gameObject.AddComponent<CanvasRenderer>();
        var graphic=edge.gameObject.AddComponent<BattlefieldStackFrameGraphic>();
        graphic.Radius=round?Mathf.Min(bounds.rect.width,bounds.rect.height)*.5f:5;
        graphic.Thickness=speed?3.5f:2.5f;graphic.color=speed?new Color(.37f,.91f,1f,1f):new Color(1f,.82f,.34f,.98f);graphic.raycastTarget=false;
    }

    private static void MarkSidebarHelperImage(Image image,ForgePlayerView player,ForgeCardView card,string speed)
    {
        if(card.markerKind=="dungeon"){
            if(TrySidebarDungeonRoomRegion(card,out var room))SidebarImageHighlight(image,"CurrentRoom",room,false);
            return;
        }
        if(IsPublicRuleHelper(card)&&card.helperArtName=="The Ring"){
            // The printed helper is reduced heavily in this panel. Prefer a sharper mip
            // so its original card lettering remains legible without replacing it with UI text.
            if(image.sprite?.texture!=null)image.sprite.texture.mipMapBias=-1.5f;
            int count=Math.Max(player.ringTemptedCount,0);
            if(count==0){
                var recorded=System.Text.RegularExpressions.Regex.Match(card.markerStatus??"",@"魔戒已引诱你\s*(\d+)\s*次");
                if(recorded.Success)int.TryParse(recorded.Groups[1].Value,out count);
            }
            int level=SidebarRingLevel(player,count);
            if(level==0)return;
            var bands=new[]{new Rect(.07f,.475f,.86f,.105f),new Rect(.07f,.584f,.86f,.108f),
                new Rect(.07f,.697f,.86f,.108f),new Rect(.07f,.81f,.86f,.114f)};
            for(int i=0;i<level-1;i++){
                var tick=Rect(image.rectTransform,"UnlockedLevel"+(i+1),Vector2.zero,Vector2.zero);
                var size=image.rectTransform.rect.size;
                RedesignPlace(tick,new Vector2(0,1),new Vector2(0,1),
                    new Vector2(size.x*.051f,-size.y*(bands[i].y+.02f)),new Vector2(4,size.y*(bands[i].height-.04f)));
                var mark=tick.gameObject.AddComponent<Image>();mark.color=new Color(1f,.77f,.3f,.75f);mark.raycastTarget=false;
            }
            SidebarImageHighlight(image,"CurrentRingLevel",bands[level-1],false);
            return;
        }
        if(speed.Length>0){
            var region=SidebarSpeedRegion(speed);
            if(region.width>0)SidebarImageHighlight(image,"CurrentSpeed",region,true,true);
        }
    }

    private void PresentSidebarMarkers(ForgePlayerView player,bool own,ForgeCardView[] markers)
    {
        bool four=IsFourPlayerScene;
        var row=four&&fourStatusRows.TryGetValue(player.seat,out var fourRow)?fourRow:SidebarStatusRow(own);
        if(row==null)return;
        var host=(RectTransform)row.Find("MarkerHost");
        // Rows are rebuilt on seat/review changes, but their arena cards are
        // reused (or animated out) by the arena presenter after this pass.
        foreach(var visual in arenaCards.Values.Concat(redesignLeavingCards.Values))
            if(visual.root!=null && visual.root.IsChildOf(host))visual.root.SetParent(statusMarkersRoot,true);
        var entries=four&&fourStatusEntries.TryGetValue(player.seat,out var seatEntries)?seatEntries:own?sidebarOwnEntries:sidebarOpponentEntries;
        bool initial=sidebarInitializedSeats.Add(player.seat);
        bool animate=Application.isPlaying&&!initial&&!sidebarCollapsed&&row.gameObject.activeInHierarchy&&sidebarPage==SidebarPage.Status;
        var previous=entries.ToArray();
        var unmatched=previous.ToDictionary(entry=>(entry.key,entry.appearance));
        entries.Clear();
        var groups=markers.GroupBy(SidebarMarkerKey).Select(g=>g.ToArray()).ToArray();
        if(four){
            var counters=(player.counters??Array.Empty<ForgeCardCounter>()).Where(counter=>counter.count>0).ToArray();
            fourStatusCounts[player.seat]=groups.Length+counters.Length;
            fourStatusCounterText[player.seat]=counters.Length==0?"":
                TapPaymentText("玩家指示物  ","Player counters  ","プレイヤーのカウンター  ")+
                string.Join("  ·  ",counters.Select(counter=>PermanentCounterName(counter)+" "+counter.count));
        }
        else if(own)sidebarOwnMarkerCount=groups.Length;else sidebarOpponentMarkerCount=groups.Length;
        row.Find("MarkerCount").GetComponent<Text>().text=groups.Length==0?"":TapPaymentText(groups.Length+" 项",groups.Length+" effects",groups.Length+" 件");
        for(int i=0;i<groups.Length;i++){
            var card=groups[i][0];
            string markerKey=player.seat+":"+SidebarMarkerKey(card);
            var item=Panel(host,"StatusItem",Vector2.zero,new Vector2(SidebarEntryWidth,SidebarMarkerBodyHeight),HudRaised);
            RedesignPlace(item,Vector2.up,Vector2.up,Vector2.zero,new Vector2(SidebarEntryWidth,SidebarMarkerBodyHeight));
            item.GetComponent<Image>().raycastTarget=false;
            item.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(64,64,4);
            var header=Rect(item,"ItemHeader",Vector2.zero,new Vector2(SidebarEntryWidth,SidebarMarkerBodyHeight));
            RedesignPlace(header,Vector2.up,Vector2.up,Vector2.zero,new Vector2(SidebarEntryWidth,SidebarMarkerBodyHeight));
            string kind=IsPublicRuleHelper(card)&&card.helperArtName=="The Ring"?TapPaymentText("魔戒","The Ring","指輪")
                :IsSpeedMarker(card)?TapPaymentText("速度","Speed","速度")
                :card.helperArtName is "An Enduring Story" or "City's Blessing"?TapPaymentText("资格","Status","資格")
                :card.markerKind=="game"&&card.helperArtName is "Day" or "Night"?TapPaymentText("昼夜","Day / Night","昼夜")
                :card.emblem?TapPaymentText("徽记","Emblem","紋章"):card.markerKind=="delayed"?TapPaymentText("待触发效果","Pending trigger","待機中の誘発型能力"):card.markerKind=="dungeon"?TapPaymentText("地城","Dungeon","ダンジョン"):TapPaymentText("效果","Effect","効果");
            if(view.commanderEvent==OnlineRoomConfig.Nauctis && card.name is "广袤海域" or "雄伟大洋"){
                kind=card.name=="雄伟大洋"?TapPaymentText("落坟20 · 已达成","Descend 20 · reached","落魄20 · 達成済み")
                    :TapPaymentText("进入条件 · 落坟20","Entry · Descend 20","進入条件 · 落魄20");
            }
            Color categoryColor=card.markerKind=="delayed"?(Color)HudTheme.SideAccent
                :card.emblem?(Color)HudTheme.MainAccent:(Color)HudTheme.LibraryAccent;
            DuelSurface(item,BattlefieldHudGraphic.Surface.Record,categoryColor);
            const float textLeft=12,titleWidth=SidebarEntryWidth-88,summaryWidth=SidebarEntryWidth-2*textLeft;
            float kindInset=card.markerKind=="delayed"?19:0;
            var kindLabel=Label(header,"Kind",kind,Vector2.zero,new Vector2(titleWidth-kindInset,22),13,categoryColor);
            kindLabel.raycastTarget=false;
            string title=card.emblem?EmblemLocalizedName(card):GamePreferences.CardFaceName(MarkerName(card));
            if(card.helperArtName=="An Enduring Story")title=TapPaymentText("历世书传","An Enduring Story","語り継がれる物語");
            else if(card.helperArtName=="City's Blessing")title=TapPaymentText("城市祝福","City's Blessing","都市の承認");
            string emblemPrefix=kind+" — ";
            if(card.emblem&&title?.StartsWith(emblemPrefix,StringComparison.Ordinal)==true)title=title.Substring(emblemPrefix.Length);
            if(card.markerKind=="delayed"&&!string.IsNullOrEmpty(title)){
                // Generated labels can contain both the source and the particular delayed action.
                const string separator=" — ";
                int split=title.IndexOf(separator,StringComparison.Ordinal);
                if(split>0&&split+separator.Length<title.Length&&split==title.LastIndexOf(separator,StringComparison.Ordinal)){
                    kindLabel.text=kind+" · "+title.Substring(split+separator.Length);
                    title=title.Substring(0,split);
                }
            }
            if(groups[i].Length>1)title+=" ×"+groups[i].Length;
            var name=Label(header,"Name",title,Vector2.zero,new Vector2(titleWidth,36),22,HudText);
            name.raycastTarget=false;
            if(HudTheme!=null){
                MenuTypography.Use(name,MenuTypography.Face.Heading);
                MenuTypography.Use(kindLabel,MenuTypography.Face.Control);
            }
            bool lokiGuide=card.lokiTricks!=null||(view.commanderEvent==OnlineRoomConfig.Loki&&card.name=="洛基的诡计");
            string summary=SidebarMarkerSummary(player,card);
            if(card.lokiTricks!=null)
                summary=TapPaymentText("已用诡计 ","Tricks used ","使用済み ")+card.lokiTricks.Count(t=>t.marked)+" / "+card.lokiTricks.Length;
            TextMeshProUGUI summaryLabel=null;
            if(summary.Length>0){
                summaryLabel=CardSymbolText.Create(header,"Summary",16,HudMuted);
                summaryLabel.GetComponent<PreferenceText>().SetMenuFace(MenuTypography.Face.Control);
                summaryLabel.alignment=TextAlignmentOptions.TopLeft;
                summaryLabel.text=CardSymbolText.Format(summary);
            }
            float kindHeight=SidebarHeaderTextHeight(kindLabel,17);
            float nameHeight=SidebarHeaderTextHeight(name,25);
            float summaryHeight=summaryLabel==null?0:Mathf.Max(22,Mathf.Ceil(summaryLabel.GetPreferredValues(summaryLabel.text,summaryWidth,10000).y)+3);
            float bandHeight=Mathf.Max(76,kindHeight+nameHeight+18);
            float bodyHeight=bandHeight+(summaryLabel==null?2:summaryHeight+23);
            if(lokiGuide)bodyHeight=Mathf.Max(bodyHeight,bandHeight+46);
            RedesignPlace(item,Vector2.up,Vector2.up,Vector2.zero,new Vector2(SidebarEntryWidth,bodyHeight));
            RedesignPlace(header,Vector2.up,Vector2.up,Vector2.zero,new Vector2(SidebarEntryWidth,bodyHeight));
            float textTop=(bandHeight-nameHeight-kindHeight-3)*.5f;
            float kindTop=textTop+nameHeight+3;
            RedesignPlace(name.rectTransform,Vector2.up,Vector2.up,new Vector2(textLeft,-textTop),new Vector2(titleWidth,nameHeight));
            RedesignPlace(kindLabel.rectTransform,Vector2.up,Vector2.up,new Vector2(textLeft+kindInset,-kindTop),new Vector2(titleWidth-kindInset,kindHeight));
            if(card.markerKind=="delayed"){
                float markY=-kindTop-kindHeight*.5f;
                var clock=Rect(header,"DelayedClock",Vector2.zero,Vector2.one).gameObject.AddComponent<DelayedEffectIcon>();
                RedesignPlace(clock.rectTransform,Vector2.up,new Vector2(.5f,.5f),new Vector2(textLeft+6,markY),new Vector2(15,15));
                clock.color=categoryColor;clock.raycastTarget=false;
            }
            if(summaryLabel!=null){
                RedesignPlace(summaryLabel.rectTransform,Vector2.up,Vector2.up,new Vector2(textLeft,-bandHeight-(lokiGuide?10:7)),new Vector2(summaryWidth-(lokiGuide?120:0),summaryHeight));
            }
            float artY=(bodyHeight-bandHeight)*.5f;
            string arenaKey=(four?"Seat"+player.seat:own?"Your":"Opponent")+"Emblem:"+card.id;
            if(!IsPuzzleGoal(card) && !IsCommanderEventMarker(card))
                DrawArenaCard(header,card,groups[i].Length,arenaKey,"markers:"+markerKey,new Vector2(SidebarEntryWidth*.5f-52,artY),new Vector2(48,67),false,0,i);
            if(arenaCards.TryGetValue(arenaKey,out var arenaItem)){
                arenaItem.root.SetAsFirstSibling();
                arenaItem.pointer.Configure(null,null,null,false);
                arenaItem.button.onClick.RemoveAllListeners();
                arenaItem.button.interactable=false;
            }
            if(IsCommanderEventMarker(card) && TryBattlefieldArt(card,out var eventArt)){
                var image=Rect(header,"EventArt",Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();
                float room=Mathf.Max(0,SidebarEntryWidth-Mathf.Max(name.preferredWidth,kindLabel.preferredWidth)-textLeft-52);
                float width=Mathf.Min(room,bandHeight*eventArt.rect.width/eventArt.rect.height);
                RedesignPlace(image.rectTransform,Vector2.one,Vector2.one,new Vector2(-38,-3),new Vector2(width,bandHeight-6));
                image.sprite=eventArt;image.preserveAspect=true;image.raycastTarget=false;
                image.transform.SetAsFirstSibling();
            }else if(TryBattlefieldArt(card,out var previewSprite)){
                bool dayNight=card.markerKind=="game"&&card.helperArtName is "Day" or "Night";
                if(arenaItem!=null)arenaItem.root.GetComponent<CanvasGroup>().alpha=0;
                var preview=Rect(header,"StatusArt",Vector2.zero,Vector2.one).gameObject.AddComponent<JournalIllustration>();
                float inkWidth=Mathf.Max(name.preferredWidth,kindLabel.preferredWidth+kindInset);
                float artWidth=Mathf.Clamp(SidebarEntryWidth-textLeft-inkWidth,88,202);
                RedesignPlace(preview.rectTransform,Vector2.one,Vector2.one,new Vector2(-1,0),new Vector2(artWidth,bandHeight));
                preview.transform.SetAsFirstSibling();
                preview.FeatherPortrait=true;preview.raycastTarget=false;
                // Planeswalker artwork can extend into the name bar. Keep its upper
                // portrait visible while ordinary cards stay below the printed frame.
                bool walkerArt=catalog.TryGetValue(card.effectSourcePrintingKey??"",out var sourcePrinting)
                    &&sourcePrinting.HasType(CardType.Planeswalker);
                CardArtCrop.Fill(preview,previewSprite,IsPublicRuleHelper(card)&&card.helperArtName=="The Ring"
                    ?new Rect(.28f,.57f,.44f,.31f):dayNight?new Rect(.04f,.38f,.92f,.32f)
                    :new Rect(.08f,.54f,.84f,walkerArt?.371f:.335f),1f);
                preview.color=new Color(1,1,1,.86f);
            }
            SidebarMarkerEntry markerEntry=null;
            Text trickToggle=null;
            void ToggleDetails(){
                if(!sidebarExpandedMarkers.Add(markerKey))sidebarExpandedMarkers.Remove(markerKey);
                if(trickToggle!=null){
                    bool open=sidebarExpandedMarkers.Contains(markerKey);
                    trickToggle.text=TrickButtonText(open);
                    if(open&&markerEntry.detailHeight==0)markerEntry.detailHeight=BuildLokiTrickDetails(markerEntry.details,card);
                }
            }
            string TrickButtonText(bool open)=>open?TapPaymentText("收起详情","Hide details","詳細を閉じる"):TapPaymentText("查看详情","View details","詳細を見る");
            var arrowButton=StackIcon(header,"ExpandDetails",StackControlIcon.Kind.Right,ToggleDetails);
            var arrowRect=(RectTransform)arrowButton.transform;
            arrowRect.anchorMin=Vector2.zero;arrowRect.anchorMax=Vector2.one;
            arrowRect.offsetMin=arrowRect.offsetMax=Vector2.zero;
            arrowButton.image.sprite=null;arrowButton.image.color=Color.white;
            arrowButton.GetComponent<Outline>().enabled=false;
            var colors=ColorBlock.defaultColorBlock;
            colors.normalColor=colors.selectedColor=Color.clear;
            colors.highlightedColor=new Color(1,1,1,.045f);
            colors.pressedColor=new Color(1,1,1,.09f);
            arrowButton.colors=colors;
            arrowButton.image.CrossFadeColor(Color.clear,0,true,true);
            var arrowIcon=(RectTransform)arrowRect.Find("Icon");
            RedesignPlace(arrowIcon,Vector2.one,new Vector2(.5f,.5f),new Vector2(-19,-bandHeight*.5f),new Vector2(19,19));
            var chevron=arrowIcon.GetComponent<StackControlIcon>();chevron.SmoothEdges=true;chevron.color=HudText;
            if(lokiGuide){
                arrowIcon.gameObject.SetActive(false);
                var button=JournalButton(header,"ViewLokiTricks",TrickButtonText(sidebarExpandedMarkers.Contains(markerKey)),SidebarEntryWidth-122,bandHeight+3,110,34,ToggleDetails,16);
                trickToggle=button.GetComponentInChildren<Text>();
            }
            string explanation=SidebarMarkerDetailRules(card);
            if(!string.IsNullOrWhiteSpace(explanation) && !string.IsNullOrWhiteSpace(title)){
                var link=CardSymbolText.Create(arrowRect,"StatusTitleLink",22,HudText);
                link.GetComponent<PreferenceText>().SetMenuFace(MenuTypography.Face.Heading);
                link.richText=link.raycastTarget=true;
                link.alignment=TextAlignmentOptions.MidlineLeft;
                link.text="<link=\"k0\"><u>"+title+"</u></link>";
                var entry=new DeckKeywordGlossary.Entry(title,title,title,explanation,explanation,explanation);
                link.gameObject.AddComponent<DeckKeywordLinkHover>().Configure(new List<DeckKeywordGlossary.Entry>{entry},name.font);
                RedesignPlace(link.rectTransform,Vector2.up,Vector2.up,new Vector2(textLeft,-textTop),new Vector2(titleWidth,nameHeight));
                name.enabled=false;
            }
            var details=Rect(item,"Details",Vector2.zero,new Vector2(SidebarEntryWidth,0));
            RedesignPlace(details,Vector2.up,Vector2.up,new Vector2(0,-bodyHeight),new Vector2(SidebarEntryWidth,0));
            details.gameObject.AddComponent<RectMask2D>();
            bool hasArt=TryBattlefieldArt(card,out var detailSprite) && !IsCommanderEventMarker(card);
            if(card.markerKind=="game"&&card.helperArtName is "Day" or "Night"
                ||card.helperArtName is "An Enduring Story" or "City's Blessing")hasArt=false;
            string speed=SpeedMarkerValue(card);
            if(speed.Length>0&&SpecialEffectArt.TryLoad(speed=="4"?"Max Speed":"Start Your Engines!",out var speedSprite)){
                detailSprite=speedSprite;hasArt=true;
            }
            bool largeHelper=hasArt&&(card.markerKind=="dungeon"||IsPublicRuleHelper(card)&&card.helperArtName=="The Ring"||speed.Length>0);
            float detailHeight,repeatedSummaryHeight=0;
            if(lokiGuide){
                bool visible=sidebarExpandedMarkers.Contains(markerKey)||(sidebarExpansionProgress.TryGetValue(markerKey,out var openFraction)&&openFraction>0);
                detailHeight=visible?BuildLokiTrickDetails(details,card):0;
            }else if(largeHelper){
                float imageWidth=300;
                float imageHeight=imageWidth*detailSprite.rect.height/detailSprite.rect.width;
                var cardImage=Rect(details,"FullCard",Vector2.zero,new Vector2(imageWidth,imageHeight)).gameObject.AddComponent<Image>();
                RedesignPlace(cardImage.rectTransform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-14),new Vector2(imageWidth,imageHeight));
                cardImage.sprite=detailSprite;cardImage.preserveAspect=true;cardImage.raycastTarget=false;
                MarkSidebarHelperImage(cardImage,player,card,speed);
                detailHeight=imageHeight+28;
            }else{
                string rules=explanation;
                string excerpt=summary.TrimEnd('…');
                if(excerpt.Length>0&&rules?.Replace('\n',' ').Trim().StartsWith(excerpt,StringComparison.Ordinal)==true){
                    repeatedSummaryHeight=bodyHeight-bandHeight;
                    header.gameObject.AddComponent<RectMask2D>();
                    if(arenaItem!=null){
                        var thumbnail=arenaItem.root;
                        thumbnail.anchorMin=thumbnail.anchorMax=new Vector2(.5f,1);
                        thumbnail.anchoredPosition=new Vector2(thumbnail.anchoredPosition.x,-bandHeight*.5f);
                    }
                }
                if(string.IsNullOrWhiteSpace(rules))rules=card.markerKind=="delayed"
                    ?TapPaymentText("效果说明未随对局数据提供。","Effect text was not supplied by the match.","対戦データに効果文が含まれていません。")
                    :TapPaymentText("没有额外的规则文字。","No additional rules text.","追加のルール文章はありません。");
                if(hasArt){
                    var cardImage=Rect(details,"FullCard",Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();
                    RedesignPlace(cardImage.rectTransform,Vector2.one,Vector2.one,new Vector2(-12,-14),new Vector2(86,120));
                    cardImage.sprite=detailSprite;cardImage.preserveAspect=true;cardImage.raycastTarget=false;
                }
                var rulesText=CardSymbolText.Create(details,"Rules",17,HudText);
                rulesText.alignment=TextAlignmentOptions.TopLeft;
                DeckKeywordLinkHover.BindRules(rulesText,rules);
                float textWidth=hasArt?SidebarEntryWidth-124:summaryWidth;
                float rulesHeight=Mathf.Max(48,rulesText.GetPreferredValues(rulesText.text,textWidth,10000).y+4);
                RedesignPlace(rulesText.rectTransform,Vector2.up,Vector2.up,new Vector2(textLeft,-16),new Vector2(textWidth,rulesHeight));
                detailHeight=Mathf.Max(hasArt?120:0,rulesHeight)+32;
            }
            sidebarExpansionProgress.TryGetValue(markerKey,out float progress);
            string appearance=string.IsNullOrEmpty(card.helperArtName)?card.name:card.helperArtName;
            float presence=animate?0:1;
            int transitionFrame=Time.frameCount;
            if(unmatched.Remove((markerKey,appearance),out var old)){
                if(animate)presence=old.presence;
                if(!old.leaving)transitionFrame=old.transitionFrame;
                RemoveStatusRow(old);
            }
            markerEntry=new SidebarMarkerEntry{key=markerKey,appearance=appearance,presence=presence,transitionFrame=transitionFrame,root=item,header=header,details=details,arrow=arrowIcon,finish=item.GetComponentInChildren<BattlefieldHudGraphic>(),bodyHeight=bodyHeight,detailHeight=detailHeight,repeatedSummaryHeight=repeatedSummaryHeight,progress=progress};
            entries.Add(markerEntry);
        }
        for(int i=0;i<previous.Length;i++){
            var old=previous[i];
            if(!unmatched.ContainsKey((old.key,old.appearance)))continue;
            if(!animate||old.presence<=0){RemoveStatusRow(old);continue;}
            if(!old.leaving)old.transitionFrame=Time.frameCount;
            old.leaving=true;
            entries.Insert(Mathf.Min(i,entries.Count),old);
        }
        var empty=host.Find("Empty") as RectTransform;
        if(empty==null)empty=Label(host,"Empty",TapPaymentText("没有持续状态","No ongoing effects","継続中の効果なし"),new Vector2(0,-13),new Vector2(310,28),16,new Color(.65f,.74f,.77f)).rectTransform;
        RedesignPlace(empty,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-2),new Vector2(310,25));
        empty.gameObject.SetActive(groups.Length==0&&(!four||string.IsNullOrEmpty(fourStatusCounterText[player.seat])));
        UpdateSidebarStatusTabLabels();
        LayoutBattleSidebar();
    }

    private void UpdateSidebarStatusTabLabels()
    {
        if(IsFourPlayerScene){UpdateFourPlayerStatusTabLabels();return;}
        if(sidebarOwnStatusTab==null||sidebarOpponentStatusTab==null)return;
        if(HudTheme!=null){
            sidebarOwnStatusTab.transform.Find("Count").GetComponent<Text>().text=sidebarOwnMarkerCount.ToString();
            sidebarOpponentStatusTab.transform.Find("Count").GetComponent<Text>().text=sidebarOpponentMarkerCount.ToString();
        }else{
            sidebarOwnStatusTab.transform.Find("Label").GetComponent<Text>().text=TapPaymentText("自己","You","自分")+"  "+sidebarOwnMarkerCount;
            sidebarOpponentStatusTab.transform.Find("Label").GetComponent<Text>().text=TapPaymentText("对手","Opponent","相手")+"  "+sidebarOpponentMarkerCount;
        }
    }

    private void SelectSidebarStatusSide(bool own)
    {
        if(IsFourPlayerScene)return;
        if(sidebarOwnStatus==null||sidebarOpponentStatus==null)return;
        sidebarShowOwn=own;
        sidebarOwnStatus.gameObject.SetActive(own);
        sidebarOpponentStatus.gameObject.SetActive(!own);
        UpdateSidebarStatusTabLabels();
        foreach(var tab in new[]{sidebarOwnStatusTab,sidebarOpponentStatusTab}){
            bool active=tab==sidebarOwnStatusTab?own:!own;
            PaintJournalTab(tab,active,filter:true);
        }
        LayoutBattleSidebar();
        sidebarStatusView.GetComponent<ScrollRect>().verticalNormalizedPosition=1;
    }

    private void SelectSidebarTab(SidebarPage page)
    {
        if(battleSidebar==null)return;
        if(page==SidebarPage.Detail&&GamePreferences.HoverCardDetails)page=SidebarPage.Status;
        if(page!=SidebarPage.History&&historyInspectId!=long.MinValue)CloseHistoryInspector();
        if(sidebarPage!=page)CloseJournalScan();
        sidebarPage=page;
        LayoutBattleSidebar();
        sidebarStatusView.gameObject.SetActive(page==SidebarPage.Status);
        sidebarOwnStatusTab.gameObject.SetActive(page==SidebarPage.Status&&!IsFourPlayerScene);
        sidebarOpponentStatusTab.gameObject.SetActive(page==SidebarPage.Status&&!IsFourPlayerScene);
        foreach(var tab in fourStatusTabs.Values)tab.gameObject.SetActive(page==SidebarPage.Status);
        sidebarHistoryView.gameObject.SetActive(page==SidebarPage.History);
        if(page==SidebarPage.History)LoadVisibleHistoryArt();
        sidebarDetailView.gameObject.SetActive(page==SidebarPage.Detail);
        var activeView=page==SidebarPage.Status?sidebarStatusView:page==SidebarPage.History?sidebarHistoryView:sidebarDetailView;
        journalWheel.SetPage(activeView.GetComponent<ScrollRect>());
        UpdateSidebarScrollbars();
        var tabs=new[]{sidebarStatusTab,sidebarHistoryTab,sidebarDetailTab};
        for(int i=0;i<tabs.Length;i++){
            var tab=tabs[i];
            bool active=i==(int)page;
            PaintJournalTab(tab,active);
        }
    }

    private void ApplySidebarDetailPreference()
    {
        if(sidebarDetailTab==null||sidebarHoverDetailMode==GamePreferences.HoverCardDetails)return;
        sidebarHoverDetailMode=GamePreferences.HoverCardDetails;
        sidebarDetailTab.gameObject.SetActive(!GamePreferences.HoverCardDetails);
        if(GamePreferences.HoverCardDetails&&sidebarPage==SidebarPage.Detail)SelectSidebarTab(SidebarPage.Status);
        LayoutBattleSidebar();
    }

    private void UpdateSidebarScrollbars()
    {
        if(sidebarStatusScrollbar==null||sidebarHistoryScrollbar==null||sidebarDetailScrollbar==null)return;
        sidebarStatusScrollbar.gameObject.SetActive(sidebarPage==SidebarPage.Status&&sidebarStatusContent.rect.height>sidebarStatusView.rect.height+2);
        sidebarHistoryScrollbar.gameObject.SetActive(sidebarPage==SidebarPage.History&&sidebarHistoryContent.rect.height>sidebarHistoryView.rect.height+2);
        sidebarDetailScrollbar.gameObject.SetActive(sidebarPage==SidebarPage.Detail&&sidebarDetailContent.rect.height>sidebarDetailView.rect.height+2);
    }

    private void ResetSidebarHistory()
    {
        CloseHistoryInspector();
        sidebarEvents.Clear();sidebarNoticeIds.Clear();sidebarStackIds.Clear();sidebarLife.Clear();previousUsableSources.Clear();
        sidebarHistoryKey=null;sidebarHistoryMeasuredHeight=0;
        pendingHistoryArt.Clear();
        ResetJournalCard();
        sidebarHasDetail=false;
        sidebarExpandedMarkers.Clear();sidebarExpansionProgress.Clear();
        sidebarShowOwn=true;
        if(IsFourPlayerScene)SelectFourPlayerStatusSeat(seat>0?seat:1);
        else if(sidebarOwnStatusTab!=null)SelectSidebarStatusSide(true);
    }

    private string SidebarNoticeEvent(ForgeNotice notice)
    {
        if(notice.kind is "cast" or "copy"){
            string name=notice.sourceCard!=null&&!notice.sourceCard.faceDown?GamePreferences.CardFaceName(notice.sourceCard.name??"")
                :!string.IsNullOrWhiteSpace(notice.value)?GamePreferences.CardFaceName(notice.value)
                :TapPaymentText("一张牌","a card","カード1枚");
            string action=notice.kind=="copy"?TapPaymentText("复制咒语","Copied spell","呪文をコピー")
                :string.IsNullOrWhiteSpace(notice.fromZone)?TapPaymentText("施放","Cast","唱えた")
                :TapPaymentText("从"+NoticeZone(notice.fromZone)+"施放","Cast from "+NoticeZone(notice.fromZone),NoticeZone(notice.fromZone)+"から唱えた");
            return (notice.actorSeat>0?PlayerDisplayName(notice.actorSeat)+" · ":"")+action+TapPaymentText("《"+name+"》"," "+name,"「"+name+"」");
        }
        if(notice.kind is "card-move" or "card-reveal"){
            var cards=notice.cards??Array.Empty<ForgeCardView>();
            if(cards.Length==0)return null;
            string names=string.Join("、",cards.Take(3).Select(card=>card.faceDown
                ?TapPaymentText("一张牌","a card","カード1枚")
                :GamePreferences.CardFaceName(card.name??"")));
            if(cards.Length>3)names+=TapPaymentText($"等 {cards.Length} 张牌",$" and {cards.Length-3} more",$"など{cards.Length}枚");
            if(notice.kind=="card-reveal")return TapPaymentText($"展示 {names}",$"Revealed {names}",$"{names}を公開");
            string sourceName=notice.reason=="effect"&&notice.sourceCard?.id>0&&!notice.sourceCard.faceDown
                ?GamePreferences.CardFaceName(notice.sourceCard.name):null;
            string cause=string.IsNullOrWhiteSpace(sourceName)?"":TapPaymentText("因《"+sourceName+"》：","By "+sourceName+": ","「"+sourceName+"」により：");
            if(notice.toZone=="Battlefield"){
                bool playedLand=notice.reason=="rules"&&cards.All(card=>card.land)
                    &&(notice.fromZone=="Hand"||cards.All(card=>previousUsableSources.Contains(card.id)));
                string action=playedLand
                    ?TapPaymentText("使用地牌","Played land","土地をプレイ")
                    :notice.fromZone=="Stack"?TapPaymentText("结算入场","Resolved onto battlefield","解決して戦場へ")
                    :TapPaymentText("放进战场","Put onto battlefield","戦場に出した");
                return (notice.fromZone=="Stack"?"":cause)+action+TapPaymentText("《"+names+"》"," "+names,"「"+names+"」")+" · "+NoticeZone(notice.fromZone)+" → "+NoticeZone(notice.toZone);
            }
            string movement=notice.fromZone=="Hand"&&notice.toZone=="Graveyard"&&notice.phase=="CLEANUP"
                ?TapPaymentText("弃牌","Discarded","捨てた")
                :notice.toZone=="Exile"?TapPaymentText("放逐","Exiled","追放")
                :notice.fromZone=="Graveyard"&&notice.toZone=="Hand"?TapPaymentText("返回手牌","Returned to hand","手札に戻した")
                :notice.toZone=="Graveyard"?TapPaymentText("置入坟墓场","Moved to graveyard","墓地に置いた")
                :notice.toZone=="Library"?TapPaymentText("置入牌库","Moved to library","ライブラリーに置いた")
                :TapPaymentText("移动","Moved","移動した");
            return cause+movement+TapPaymentText("《"+names+"》"," "+names,"「"+names+"」")+" · "+NoticeZone(notice.fromZone)+" → "+NoticeZone(notice.toZone);
        }
        // Generic notifications without a source are mostly prompts and status
        // messages; the live prompt already shows them at the right moment.
        if(notice.kind=="notice"&&(notice.sourceCard==null||notice.sourceCard.id<=0)&&string.IsNullOrWhiteSpace(notice.title))return null;
        if(notice.kind is not ("notice" or "cleanup" or "coin" or "dice" or "flip" or "ownership" or "restart" or "match-removal"))return null;
        string text=NoticeText(notice)?.Trim();
        return string.IsNullOrWhiteSpace(text)?null:text;
    }

    private void CollectSidebarHistory()
    {
        foreach(var notice in view.notices??Array.Empty<ForgeNotice>())
            if(sidebarNoticeIds.Add(notice.id)){
                string eventText=SidebarNoticeEvent(notice);
                if(!string.IsNullOrWhiteSpace(eventText))sidebarEvents.Add(new SidebarHistoryEntry{
                    id=notice.id,cards=(notice.cards??Array.Empty<ForgeCardView>())
                        .Concat(notice.sourceCard==null?Array.Empty<ForgeCardView>():new[]{notice.sourceCard}).ToArray(),
                    text=eventText,turn=notice.turn>0?notice.turn:view.turn,turnSeat=notice.turnSeat>0?notice.turnSeat:view.turnSeat,
                    phase=string.IsNullOrWhiteSpace(notice.phase)?view.phase:notice.phase,actorSeat=notice.actorSeat});
                if(notice.kind is "cast" or "copy"&&notice.stackId>0)sidebarStackIds.Add(notice.stackId);
            }
        var active=(view.stackItems??Array.Empty<ForgeStackItem>()).Select(item=>item.id).ToHashSet();
        foreach(var item in view.stackItems??Array.Empty<ForgeStackItem>())
            if(sidebarStackIds.Add(item.id)){
                string name=item.source?.faceDown==true?TapPaymentText("面朝下的牌","Face-down card","裏向きのカード"):GamePreferences.CardFaceName(item.source?.name??"");
                if(string.IsNullOrWhiteSpace(name))continue;
                bool ability=item.kind is "trigger" or "ability" or "activated";
                string action=ability?TapPaymentText("《"+name+"》的异能进入堆叠","Ability of "+name+" entered the stack","「"+name+"」の能力がスタックに置かれた")
                    :TapPaymentText("《"+name+"》进入堆叠",name+" entered the stack","「"+name+"」がスタックに置かれた");
                sidebarEvents.Add(new SidebarHistoryEntry{id=-item.id,cards=item.source==null?Array.Empty<ForgeCardView>():new[]{item.source},
                    text=(item.controllerSeat>0?PlayerDisplayName(item.controllerSeat)+" · ":"")+action,turn=view.turn,turnSeat=view.turnSeat,phase=view.phase,actorSeat=item.controllerSeat});
            }
        sidebarStackIds.IntersectWith(active);
        previousUsableSources.Clear();
        foreach(var player in view.players??Array.Empty<ForgePlayerView>()){
            if(player.usableSourceIds!=null)previousUsableSources.UnionWith(player.usableSourceIds);
            if(sidebarLife.TryGetValue(player.seat,out int before)&&before!=player.life){
                string name=PlayerDisplayName(player.seat);
                sidebarEvents.Add(new SidebarHistoryEntry{turn=view.turn,turnSeat=view.turnSeat,phase=view.phase,actorSeat=player.seat,
                    text=TapPaymentText($"{name}的生命：{before} → {player.life}",$"{name}'s life: {before} → {player.life}",$"{name}のライフ：{before} → {player.life}")});
            }
            sidebarLife[player.seat]=player.life;
        }
        if(sidebarEvents.Count>100)sidebarEvents.RemoveRange(0,sidebarEvents.Count-100);
    }
    private void ObserveSidebarHistory()
    {
        if(battleSidebar==null||view==null)return;
        CollectSidebarHistory();
        bool showOpening=service?.CurrentPuzzle==null && view.firstPlayerSeat>0
            && (view.players??Array.Empty<ForgePlayerView>()).Any(p=>p.seat==view.firstPlayerSeat)
            && !view.players.Any(p=>Array.Exists(p.effectMarkers??Array.Empty<ForgeCardView>(),IsPuzzleGoal));
        string openingKey=showOpening+":"+view.firstPlayerSeat+":"+string.Join(",",view.mulliganCounts??Array.Empty<int>())+":"
            +string.Join(",",view.mulliganHandSizes??Array.Empty<int>())+":"+string.Join(",",view.mulliganKept??Array.Empty<bool>());
        string key=GamePreferences.Revision+"/"+openingKey+"/"+seat+"/"+string.Join(",",(view.players??Array.Empty<ForgePlayerView>()).Select(p=>p.seat+":"+PlayerDisplayName(p.seat)))+"/"+string.Join("\u001f",sidebarEvents.Select(e=>e.turn+":"+e.turnSeat+":"+e.phase+":"+e.actorSeat+":"+e.text));
        if(key==sidebarHistoryKey)return;
        sidebarHistoryKey=key;
        pendingHistoryArt.Clear();
        Clear(sidebarHistoryContent);
        float width=sidebarHistoryView.rect.width-22,offset=10;
        if(sidebarEvents.Count==0&&!showOpening){
            var hint=Label(sidebarHistoryContent,"Empty",TapPaymentText("尚无需要记录的对局事件","No significant match events yet","記録する対戦イベントはまだありません"),Vector2.zero,new Vector2(width,42),17,new Color(.65f,.74f,.78f));
            hint.rectTransform.anchorMin=hint.rectTransform.anchorMax=hint.rectTransform.pivot=new Vector2(.5f,1);
            hint.rectTransform.anchoredPosition=new Vector2(0,-offset);
            offset+=50;
        }
        int shownTurn=-1;string shownPhase=null;
        foreach(var entry in sidebarEvents.AsEnumerable().Reverse()){
            if(entry.turn>0&&entry.turn!=shownTurn){
                shownTurn=entry.turn;shownPhase=null;
                string who=entry.turnSeat>0?PlayerDisplayName(entry.turnSeat):"";
                string headingText=TapPaymentText("第 "+shownTurn+" 回合","Turn "+shownTurn,"ターン "+shownTurn)
                    +(who.Length>0?" · "+who+TapPaymentText("的回合","'s turn","のターン"):"");
                var heading=Label(sidebarHistoryContent,"TurnHeading",headingText,Vector2.zero,new Vector2(width-20,30),20,HudText);
                MenuTypography.Use(heading,MenuTypography.Face.Heading);
                heading.alignment=TextAnchor.MiddleLeft;heading.raycastTarget=false;
                float headingHeight=Mathf.Max(30,heading.preferredHeight+4);
                heading.rectTransform.anchorMin=heading.rectTransform.anchorMax=heading.rectTransform.pivot=new Vector2(.5f,1);
                heading.rectTransform.anchoredPosition=new Vector2(0,-offset);
                heading.rectTransform.sizeDelta=new Vector2(width-20,headingHeight);offset+=headingHeight+4;
            }
            if(!string.IsNullOrWhiteSpace(entry.phase)&&entry.phase!=shownPhase){
                shownPhase=entry.phase;
                var phase=Label(sidebarHistoryContent,"PhaseHeading",Phase(shownPhase),Vector2.zero,new Vector2(width-24,25),14,HudMuted);
                MenuTypography.Use(phase,MenuTypography.Face.Control);
                phase.alignment=TextAnchor.MiddleLeft;phase.raycastTarget=false;
                phase.rectTransform.anchorMin=phase.rectTransform.anchorMax=phase.rectTransform.pivot=new Vector2(.5f,1);
                float phaseHeight=Mathf.Max(25,Mathf.Ceil(phase.preferredHeight)+4);
                phase.rectTransform.anchoredPosition=new Vector2(2,-offset);
                phase.rectTransform.sizeDelta=new Vector2(width-24,phaseHeight);
                offset+=phaseHeight+2;
            }
            var tile=Rect(sidebarHistoryContent,"Event",Vector2.zero,new Vector2(width,48));
            var linkedCard=HistoryCardForEntry(entry);
            bool hasCard=linkedCard!=null;
            float inset=hasCard?70:22;
            bool identified=IsFourPlayerScene&&entry.actorSeat>0;
            string body=hasCard?HistoryLinkedText(entry):entry.text;
            if(identified){
                string prefix=PlayerDisplayName(entry.actorSeat)+" · ";
                if(body.StartsWith(prefix,StringComparison.Ordinal))body=body.Substring(prefix.Length);
            }
            var label=Label(tile,"Text",body,Vector2.zero,new Vector2(width-inset-15,36),17,HudText);
            MenuTypography.Use(label,MenuTypography.Face.Reading);
            label.alignment=TextAnchor.UpperLeft;label.supportRichText=hasCard;label.raycastTarget=false;
            label.verticalOverflow=VerticalWrapMode.Overflow;
            float bodyTop=identified?27:5;
            float height=Mathf.Max(identified?58:hasCard?54:43,Mathf.Ceil(label.preferredHeight)+bodyTop+9);
            tile.sizeDelta=new Vector2(width,height);
            label.rectTransform.anchorMin=label.rectTransform.anchorMax=label.rectTransform.pivot=new Vector2(0,1);
            label.rectTransform.anchoredPosition=new Vector2(inset,-bodyTop);
            label.rectTransform.sizeDelta=new Vector2(width-inset-15,height-bodyTop-5);
            if(identified){
                var actor=Label(tile,"Actor",PlayerDisplayName(entry.actorSeat),Vector2.zero,new Vector2(width-50,23),16,FourSeatColor(entry.actorSeat));
                actor.fontStyle=FontStyle.Bold;actor.alignment=TextAnchor.MiddleLeft;actor.raycastTarget=false;
                actor.resizeTextForBestFit=true;actor.resizeTextMinSize=12;actor.resizeTextMaxSize=16;
                actor.rectTransform.anchorMin=actor.rectTransform.anchorMax=actor.rectTransform.pivot=new Vector2(0,1);
                actor.rectTransform.anchoredPosition=new Vector2(inset,-2);
                actor.rectTransform.sizeDelta=new Vector2(width-inset-15,23);
            }
            var rule=Panel(tile,"Rule",Vector2.zero,Vector2.one,HudRule);
            RedesignPlace(rule,Vector2.up,Vector2.up,new Vector2(8,-1),new Vector2(1,height+3));
            rule.GetComponent<Image>().raycastTarget=false;
            var accent=Panel(tile,"Accent",Vector2.zero,Vector2.one,StackSeatTint(entry.actorSeat));
            RedesignPlace(accent,Vector2.up,new Vector2(.5f,.5f),new Vector2(8.5f,-17),new Vector2(6,6));
            accent.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(16,16,8);
            accent.GetComponent<Image>().raycastTarget=false;
            tile.anchorMin=tile.anchorMax=tile.pivot=new Vector2(.5f,1);
            tile.anchoredPosition=new Vector2(0,-offset);offset+=height+3;
            if(hasCard){
                var hit=tile.gameObject.AddComponent<Image>();hit.color=HistoryRowColor(false);
                if(historyInspectId==entry.id){historySelectedRow=hit;hit.color=HistoryRowColor(true);}
                pendingHistoryArt.Add((tile,linkedCard));
                var link=tile.gameObject.AddComponent<Button>();link.targetGraphic=hit;
                link.navigation=new Navigation{mode=Navigation.Mode.None};
                link.onClick.AddListener(()=>OpenHistoryInspector(entry,tile));
                // The hit area spans the row, while only card names use link styling.
            }
        }
        if(showOpening){
            var opener=Label(sidebarHistoryContent,"OpeningHeading",TapPaymentText("开局 · 先后手与调度","Opening · play order and mulligans","開始 · 先後とマリガン"),Vector2.zero,new Vector2(width-20,30),19,HudText);
            opener.fontStyle=FontStyle.Bold;opener.alignment=TextAnchor.MiddleLeft;
            opener.rectTransform.anchorMin=opener.rectTransform.anchorMax=opener.rectTransform.pivot=new Vector2(.5f,1);
            float openerHeight=Mathf.Max(30,Mathf.Ceil(opener.preferredHeight)+4);
            opener.rectTransform.anchoredPosition=new Vector2(0,-offset);
            opener.rectTransform.sizeDelta=new Vector2(width-20,openerHeight);offset+=openerHeight+3;
            var openingSeats=view.players.Select(p=>p.seat).OrderBy(number=>number==view.firstPlayerSeat?0:1).ThenBy(number=>number).ToArray();
            foreach(int playerSeat in openingSeats){
                int index=playerSeat-1;
                int count=view.mulliganCounts!=null&&index<view.mulliganCounts.Length?view.mulliganCounts[index]:0;
                bool kept=view.mulliganKept!=null&&index<view.mulliganKept.Length&&view.mulliganKept[index];
                int handSize=view.mulliganHandSizes!=null&&index<view.mulliganHandSizes.Length?view.mulliganHandSizes[index]:0;
                string role=playerSeat==view.firstPlayerSeat?TapPaymentText("先手","First","先手"):
                    IsFourPlayerScene?TapPaymentText("其他","Other","その他"):TapPaymentText("后手","Second","後手");
                string outcome=kept?TapPaymentText($"调度 {count} 次 · 保留 {handSize} 张",$"{count} mulligans · kept {handSize}",$"マリガン {count} 回 · {handSize} 枚でキープ")
                    :TapPaymentText($"调度 {count} 次 · 尚未保留",$"{count} mulligans · deciding",$"マリガン {count} 回 · 選択中");
                var row=Label(sidebarHistoryContent,"OpeningPlayer",role+" · "+PlayerDisplayName(playerSeat)+"    "+outcome,Vector2.zero,new Vector2(width-20,36),17,
                    IsFourPlayerScene?FourSeatColor(playerSeat):playerSeat==seat?new Color(.55f,.79f,.93f):new Color(.94f,.63f,.61f));
                row.alignment=TextAnchor.MiddleLeft;
                row.rectTransform.anchorMin=row.rectTransform.anchorMax=row.rectTransform.pivot=new Vector2(.5f,1);
                float rowHeight=Mathf.Max(36,Mathf.Ceil(row.preferredHeight)+4);
                row.rectTransform.anchoredPosition=new Vector2(0,-offset);
                row.rectTransform.sizeDelta=new Vector2(width-20,rowHeight);offset+=rowHeight+2;
            }
        }
        sidebarHistoryContent.sizeDelta=new Vector2(sidebarHistoryView.rect.width,offset+8);
        sidebarHistoryMeasuredHeight=offset+8;
        if(sidebarPage==SidebarPage.History)LayoutBattleSidebar();
        LoadVisibleHistoryArt();
        UpdateSidebarScrollbars();
    }

    private Color HistoryRowColor(bool selected)
    {
        Color fill=selected?HudAccent:HudRaised;
        fill.a=selected?.26f:.32f;
        return fill;
    }

    private void LoadVisibleHistoryArt()
    {
        if(sidebarPage!=SidebarPage.History||sidebarHistoryView==null||!sidebarHistoryView.gameObject.activeInHierarchy)return;
        float top=sidebarHistoryContent.anchoredPosition.y,bottom=top+sidebarHistoryView.rect.height;
        for(int i=pendingHistoryArt.Count-1;i>=0;i--){
            var (row,card)=pendingHistoryArt[i];
            float y=-row.anchoredPosition.y;
            if(y>bottom+40||y+row.rect.height<top-40)continue;
            pendingHistoryArt.RemoveAt(i);
            if(!TryBattlefieldArt(card,out var scan))continue;
            catalog.TryGetValue(card.printingKey??"",out var def);
            JournalArt(row,scan,JournalFeature(def,card.backFace?1:0),card.stateName=="Meld",15,4,46,46);
        }
    }

    private string HistoryLinkedText(SidebarHistoryEntry entry)
    {
        string name=GamePreferences.CardFaceName(HistoryCardForEntry(entry)?.name??"");
        if(string.IsNullOrWhiteSpace(name))return entry.text;
        return System.Text.RegularExpressions.Regex.Replace(entry.text,System.Text.RegularExpressions.Regex.Escape(name),
            match=>"<color=#"+ColorUtility.ToHtmlStringRGB(HudTheme!=null?(Color)HudTheme.LibraryAccent:HudAccent)+"><b>"+match.Value+"</b></color>");
    }

    private static ForgeCardView HistoryCardForEntry(SidebarHistoryEntry entry)
    {
        var cards=(entry.cards??Array.Empty<ForgeCardView>())
            .Where(card=>card!=null&&!card.faceDown).GroupBy(card=>card.id).Select(group=>group.First()).ToArray();
        if(cards.Length==0)return null;
        // The notice source is appended last; prefer it when the history line
        // names it, instead of paging through unrelated cards in the notice.
        return cards.LastOrDefault(card=>{
            string name=GamePreferences.CardFaceName(card.name??"");
            return !string.IsNullOrWhiteSpace(name)&&entry.text.Contains(name,StringComparison.Ordinal);
        })??cards[cards.Length-1];
    }

    private void OpenHistoryInspector(SidebarHistoryEntry entry,RectTransform source)
    {
        var selected=HistoryCardForEntry(entry);
        if(selected==null)return;
        if(historyInspectId==entry.id){CloseHistoryInspector();return;}
        CloseHistoryInspector();
        historyInspectId=entry.id;
        historyInspectCard=selected;
        historySelectedRow=source!=null?source.GetComponent<Image>():null;
        if(historySelectedRow!=null)historySelectedRow.color=HistoryRowColor(true);
        if(TryBattlefieldArt(selected,out var scan)){
            ShowJournalScan(scan,selected.room||selected.battle,true);
            return;
        }
        ShowHistoryInspector(source);
    }

    private void ShowHistoryInspector(RectTransform source)
    {
        if(historyInspectCard==null)return;
        sidebarHistoryInspection=true;
        InspectIntegratedCore(historyInspectCard,true);
        var panel=(RectTransform)Node("InspectPanel");
        if(source!=null){
            Vector2 point=surface.InverseTransformPoint(source.TransformPoint(source.rect.center));
            panel.anchorMin=panel.anchorMax=new Vector2(.5f,.5f);
            float x=surface.rect.xMin+SidebarOpenRight+panel.rect.width*.5f+18;
            panel.anchoredPosition=new Vector2(
                Mathf.Clamp(x,surface.rect.xMin+panel.rect.width*.5f+12,surface.rect.xMax-panel.rect.width*.5f-12),
                Mathf.Clamp(point.y,surface.rect.yMin+panel.rect.height*.5f+46,surface.rect.yMax-panel.rect.height*.5f-86));
        }
        if(historyInspectClose==null){
            historyInspectClose=Button(panel,"HistoryInspectClose","×",Vector2.zero,new Vector2(30,30),CloseHistoryInspector,new Color(.12f,.19f,.22f,.97f));
        }
        var close=(RectTransform)historyInspectClose.transform;
        close.anchorMin=close.anchorMax=close.pivot=Vector2.one;
        close.anchoredPosition=new Vector2(-8,-48);
        historyInspectClose.gameObject.SetActive(true);
    }

    private void CloseHistoryInspector()
    {
        if(scanFromHistory)CloseJournalScan();
        if(historySelectedRow!=null)historySelectedRow.color=HistoryRowColor(false);
        historySelectedRow=null;
        if(sidebarHistoryInspection){
            var panel=surface!=null?surface.Find("InspectPanel"):null;
            if(panel!=null)panel.gameObject.SetActive(false);
        }
        sidebarHistoryInspection=false;
        historyInspectId=long.MinValue;
        historyInspectCard=null;
        if(historyInspectClose!=null)historyInspectClose.gameObject.SetActive(false);
    }

    private void BuildSidebarDetail()
    {
        ResetJournalCard();
    }
}
