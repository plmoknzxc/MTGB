using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform otherSourceTabs, redesignOtherBackplate;
    private float redesignOtherCardsBottom;
    private sealed class RedesignHandSection {
        public string zone;
        public bool pinnedCommander;
        public float center, width, captionWidth, minimumFaceWidth;
        public int count, page, pageSize;
        public int LastPage => Mathf.Max(0,(count-1)/Mathf.Max(1,pageSize));
        public int PageStart(int index) => Mathf.Min(count,index*pageSize);
        public RectTransform region, divider;
        public ForgeHandFanNavigation navigation;
        public Button previous, next;
        public readonly List<HandFanSlot> slots = new();
    }
    private readonly Dictionary<string,RedesignHandSection> redesignOwnSections=new(), redesignOtherSections=new();
    private Dictionary<string,RedesignHandSection> RedesignSections(HandFanState fan)=>fan.opponent?redesignOtherSections:redesignOwnSections;
    private static readonly string[] RedesignSourceOrder={"Hand","Graveyard","Exile","Command","Companion"};
    private static string RedesignSource(ForgeCardView card)
    {
        if(card.stateName=="PreparedSpell")return "Exile";
        string source=HandSource(card);
        if(source=="Command"&&!card.commander)
            return (card.keywords??Array.Empty<string>()).Any(k=>string.Equals(k,"COMPANION",StringComparison.OrdinalIgnoreCase))?"Companion":"CommandZone";
        return source;
    }
    private static IEnumerable<string> RedesignOrderedSources(ForgeCardView[] cards)=>RedesignSourceOrder.Where(z=>cards.Any(c=>RedesignSource(c)==z)).Concat(cards.Select(RedesignSource).Where(z=>!RedesignSourceOrder.Contains(z)).Distinct());
    private string RedesignSourceLabel(string zone)=>zone switch {
        "Companion"=>TapPaymentText("行侣","Companion","相棒"),
        "Command"=>TapPaymentText("指挥官","Commander","統率者"),
        "CommandZone"=>HandSourceLabel("Command"),
        "Exile"=>TapPaymentText("放逐区","Exile","追放領域"),
        _=>HandSourceLabel(zone)
    };

    private static int[] RedesignPayableSourceIds(ForgeSeatView snapshot)
        =>snapshot?.awaitingInput==true&&snapshot.request?.kind=="InputPassPriority"
            ?(snapshot.request.options??Array.Empty<ForgeOption>()).Where(o=>!o.disabled&&!o.requiresPaymentCheck&&o.id?.StartsWith("card:",StringComparison.Ordinal)==true)
                .Select(o=>int.TryParse(o.id.Substring(5),out int id)?id:0).Where(id=>id>0).Distinct().ToArray()
            :Array.Empty<int>();

    private void StyleRedesignSourceButton(Button button,bool passive=false,Color accent=default,bool opponent=false)
    {
        var image=button.targetGraphic as Image;
        if(image==null)return;
        if(passive){
            image.color=Color.clear;
            var surface=button.transform.Find("CaptionSurface") as RectTransform;
            if(surface==null){
                var go=new GameObject("CaptionSurface",typeof(RectTransform),typeof(CanvasRenderer),typeof(BattlefieldSourceCaptionGraphic));
                surface=(RectTransform)go.transform;surface.SetParent(button.transform,false);
            }
            surface.anchorMin=Vector2.zero;surface.anchorMax=Vector2.one;
            surface.offsetMin=surface.offsetMax=Vector2.zero;surface.SetSiblingIndex(0);
            var graphic=surface.GetComponent<BattlefieldSourceCaptionGraphic>();
            graphic.Theme=HudTheme;
            graphic.accent=accent;graphic.opponent=opponent;graphic.raycastTarget=false;graphic.SetVerticesDirty();
        }else{
            image.sprite=IntegratedUiUtility.RoundedRect(64,36,4);
            image.type=Image.Type.Sliced;
            image.color=HudTheme!=null?HudRaised:new Color(.055f,.075f,.085f,.94f);
        }
        if(image.transform.Find("ModernOutline") is Transform oldFrame)oldFrame.gameObject.SetActive(false);
        button.transition=passive?Selectable.Transition.None:Selectable.Transition.ColorTint;
        var colors=button.colors;
        colors.normalColor=Color.white;colors.selectedColor=Color.white;
        colors.highlightedColor=new Color(1.12f,1.1f,1.06f);
        colors.pressedColor=new Color(.76f,.79f,.82f);
        colors.disabledColor=new Color(.5f,.5f,.5f);
        colors.fadeDuration=.12f;button.colors=colors;
    }

    private IEnumerable<ForgeCardView> PublicHandSources(ForgePlayerView player,HashSet<int> usable)
    {
        // Permission determines tray membership. Priority and payment only
        // determine what the current native request lets the player do.
        var permitted=(player?.publicPlayableGraveyard??Array.Empty<ForgeCardView>())
            .Concat(player?.publicPlayableExile??Array.Empty<ForgeCardView>())
            .Where(c=>c!=null&&!c.faceDown);
        var commandCards=(player?.commandCards??player?.commanders??Array.Empty<ForgeCardView>())
            .Where(c=>c!=null&&!c.faceDown&&c.zone=="Command"&&!c.emblem&&!c.effectMarker
                &&(c.commander||RedesignSource(c)=="Companion"||usable.Contains(c.id)));
        // A current action can also authorize an individual public card that
        // has no continuing permission (including a card owned by another seat).
        var offered=(view?.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>(p.graveyardCards??Array.Empty<ForgeCardView>()).Concat(p.exileCards??Array.Empty<ForgeCardView>()))
            .Where(c=>c!=null&&!c.faceDown&&usable.Contains(c.id));
        // Forge exposes the public prepared spell on its battlefield source. The
        // spell itself has its own exile identity, including when also offered by ID.
        var prepared=(player?.battlefield??Array.Empty<ForgeCardView>())
            .Where(c=>c!=null&&!c.faceDown&&HasPreparedSpell(c)).Select(c=>c.preparedSpell.ToCard());
        return prepared.Concat(permitted).Concat(commandCards).Concat(offered);
    }

    private ForgeCardView[] PresentRedesignSources(ForgeCardView[] cards,HandFanState fan,bool choosingHand)
    {
        bool other=fan.opponent;
        var player=(view?.players??Array.Empty<ForgePlayerView>()).FirstOrDefault(p=>other?p.seat!=DisplayedHandSeat:p.seat==DisplayedHandSeat);
        var usable=(!other&&view?.awaitingInput==true?RedesignPayableSourceIds(view):player?.usableSourceIds??Array.Empty<int>()).ToHashSet();
        cards=cards.Concat(PublicHandSources(player,usable)).GroupBy(c=>c.id).Select(g=>g.FirstOrDefault(IsPreparedSpell)??g.First()).ToArray();
        var zones=(service?.IsSpectator==true?new[]{"Hand"}.Concat(RedesignOrderedSources(cards)).Distinct():RedesignOrderedSources(cards)).ToArray();
        var tabs=other?otherSourceTabs:handSourceTabs;
        if(tabs==null){
            tabs=Rect(surface,other?"OtherHandSourceTabs":"HandSourceTabs",Vector2.zero,new Vector2(600,36));
            if(other)otherSourceTabs=tabs;else handSourceTabs=tabs;
            SetOverlayLayer(tabs,HandTabLayer,true);
        }
        tabs.gameObject.SetActive(true);
        var buttons=other?otherSourceButtons:handSourceButtons;
        foreach(var pair in buttons)if(!zones.Contains(pair.Key))HideRedesignElement((RectTransform)pair.Value.transform,new Vector2(0,other?-12:12));
        if(zones.Length==0){
            fan.browseSources=false;fan.browsedSource=null;
            if(fan.sourcePager!=null)fan.sourcePager.gameObject.SetActive(false);
            if(HudTheme!=null && tabs.Find("DuelFinish") is Transform emptyRail)emptyRail.gameObject.SetActive(false);
            return cards;
        }
        var sections=RedesignSections(fan);
        float lane=other?RedesignOtherHandMaximumWidth:RedesignHandMaximumWidth;
        bool tide=HudTheme?.IsTidelights==true;
        if(other)redesignOtherLane=lane;else redesignOwnLane=lane;
        SetOverlayLayer(tabs,choosingHand?DecisionLayerOffset+1:HandTabLayer,true);
        if(!choosingHand && tabs.anchoredPosition3D.z!=0){
            var position=tabs.anchoredPosition3D;position.z=0;tabs.anchoredPosition3D=position;
        }
        const float gap=RedesignSourceGap;
        var minima=new List<float>();var headerMinima=new List<float>();var weights=new List<float>();
        var accent=other?new Color(.92f,.66f,.65f):new Color(.66f,.82f,.92f);
        if(HudTheme!=null)accent=other?HudTheme.SideAccent:HudTheme.MainAccent;
        if(HudTheme!=null){
            DuelSurface(tabs,BattlefieldHudGraphic.Surface.Rail,accent);
            tabs.Find("DuelFinish").gameObject.SetActive(true);
        }
        foreach(string zone in zones){
            if(!sections.ContainsKey(zone))sections.Add(zone,new RedesignHandSection{zone=zone});
            if(!buttons.TryGetValue(zone,out var button)){
                button=Button(tabs,"Source_"+zone,"",Vector2.zero,new Vector2(120,36),()=>{},new Color(.035f,.065f,.08f,.92f));
                buttons.Add(zone,button);
            }
            var sourceAccent=accent;
            if(HudTheme!=null)sourceAccent=zone switch {
                "Hand"=>HudTheme.MainAccent,
                "Command"=>HudTheme.SideAccent,
                "Companion"=>Color.Lerp(HudTheme.SideAccent,HudTheme.MainAccent,.45f),
                "Graveyard"=>HudTheme.Silver,
                "Battlefield"=>HudTheme.ActionEdge,
                _=>HudTheme.LibraryAccent
            };
            int count=cards.Count(c=>RedesignSource(c)==zone);
            sections[zone].count=count;
            var label=button.GetComponentInChildren<Text>();label.GetComponent<PreferenceText>().Literal=true;
            label.text=RedesignSourceLabel(zone);
            if(HudTheme!=null)MenuTypography.Use(label,tide?MenuTypography.Face.Control:MenuTypography.Face.Heading);
            PreferenceText.SetSize(label,HudTheme!=null?20:19);label.resizeTextForBestFit=false;
            label.fontStyle=FontStyle.Normal;
            label.alignment=TextAnchor.MiddleLeft;
            var number=button.transform.Find("CaptionNumber")?.GetComponent<Text>();
            if(number==null)number=Label(button.transform,"CaptionNumber","",Vector2.zero,Vector2.one,20,new Color(.91f,.86f,.72f));
            MenuTypography.Use(number,tide?MenuTypography.Face.Control:MenuTypography.Face.Default);
            number.text=count.ToString();number.color=new Color(.91f,.86f,.72f);
            if(HudTheme!=null)number.color=sourceAccent;
            number.fontStyle=FontStyle.Normal;number.alignment=TextAnchor.MiddleLeft;
            number.raycastTarget=false;PreferenceText.SetSize(number,tide?15:HudTheme!=null?18:20);
            // Measure a single title line before assigning this frame's final width.
            label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Overflow;
            float outerInset=zone==zones[0]||zone==zones[zones.Length-1]?8:0;
            headerMinima.Add(Mathf.Ceil(label.preferredWidth+number.preferredWidth)+(HudTheme!=null?64:42)+outerInset);
            sections[zone].minimumFaceWidth=HandGalleryMinimum(zone,other && zone=="Hand" && cards.Where(c=>RedesignSource(c)=="Hand").All(c=>c.faceDown));
            float minFace=count<=7?sections[zone].minimumFaceWidth:RedesignMinFaceWidth;
            float minimumCards=HudTheme!=null?Mathf.Max(1,count)*(minFace+RedesignCardGap):RedesignSourceGutter+166;
            minima.Add(Mathf.Max(headerMinima.Last(),minimumCards));
            float desired=RedesignSourceGutter+166+Mathf.Max(0,count-1)*174;
            weights.Add(Mathf.Max(0,desired-minima.Last()));
            label.color=new Color(.89f,.90f,.87f);button.gameObject.SetActive(true);button.interactable=true;
            if(HudTheme!=null)label.color=HudText;
            StyleRedesignSourceButton(button,true,sourceAccent,other);
            // Keep the framed caption, without a button hit target or dimmed
            // disabled tint. Revealing the hand still belongs to its tray/cards.
            button.onClick.RemoveAllListeners();button.enabled=false;
            button.transition=Selectable.Transition.None;
            var disabledVisual=button.GetComponent<DisabledButtonVisual>();if(disabledVisual!=null)disabledVisual.enabled=false;
            // Cancel any tint tween started before this caption became passive.
            button.targetGraphic.CrossFadeColor(Color.white,0,true,true);label.canvasRenderer.SetAlpha(1);
            button.targetGraphic.raycastTarget=false;label.raycastTarget=false;
        }
        // Move the cards into a shared gallery before any source becomes too narrow to read.
        fan.browseSources=HudTheme!=null && minima.Sum()+(zones.Length-1)*gap>lane-48;
        int commanderIndex=Array.IndexOf(zones,"Command");
        bool pinCommander=fan.browseSources && commanderIndex>=0 && zones.Any(z=>z!="Command"&&sections[z].count>0);
        if(!zones.Contains(fan.browsedSource)||sections[fan.browsedSource].count==0||pinCommander&&fan.browsedSource=="Command")
            fan.browsedSource=zones.FirstOrDefault(z=>sections[z].count>0&&(!pinCommander||z!="Command"))??zones[0];
        if(choosingHand&&zones.Contains("Hand"))fan.browsedSource="Hand";
        float occupied=minima.Sum()+weights.Sum()+(zones.Length-1)*gap;
        float width=Mathf.Min(lane,occupied+48),available=width-48;
        if(other)redesignOtherWidth=width;else redesignOwnWidth=width;
        float budget=Mathf.Max(1,available-(zones.Length-1)*gap),minimum=minima.Sum(),remaining=Mathf.Max(0,budget-minimum),weight=weights.Sum(),distributed=Mathf.Min(remaining,weight),cursor=-available*.5f;
        float headers=headerMinima.Sum();
        // Keep space for both partners. Other sources share the gallery to their left.
        float commandWidth=pinCommander?Mathf.Min(Mathf.Min(2,sections["Command"].count)*174,available*.48f):0;
        float reserved=pinCommander?commandWidth+gap:0;
        float galleryWidth=available-reserved;
        bool needsPager=fan.browseSources && zones.Any(z=>(!pinCommander||z!="Command")&&sections[z].count>RedesignGalleryCapacity(galleryWidth,sections[z].minimumFaceWidth));
        float pagerWidth=needsPager?Mathf.Min(128,galleryWidth*.26f):0;
        float tabBudget=Mathf.Max(1,budget-commandWidth-pagerWidth);
        float galleryHeaders=headers-(pinCommander?headerMinima[commanderIndex]:0);
        fan.headerTrimBudget=Mathf.Max(0,tabBudget-galleryHeaders);
        float headerHeight=zones.Max(z=>HudTheme!=null
            ?Mathf.Max(36,buttons[z].GetComponentInChildren<Text>().preferredHeight+8)
            :Mathf.Max(44,buttons[z].GetComponentInChildren<Text>().preferredHeight+12));
        for(int i=0;i<zones.Length;i++){
            string zone=zones[i];var section=sections[zone];
            section.pinnedCommander=pinCommander&&zone=="Command";
            float sectionWidth=minimum>budget
                ?headers>=budget?headerMinima[i]*budget/headers:headerMinima[i]+(budget-headers)*(minima[i]-headerMinima[i])/(minimum-headers)
                :minima[i]+(weight>0?distributed*weights[i]/weight:0)+(i==0?remaining-distributed:0);
            float tabWidth=fan.browseSources?headerMinima[i]*Mathf.Min(1,tabBudget/galleryHeaders):sectionWidth;
            float tabLeft=cursor;
            section.width=fan.browseSources?galleryWidth:sectionWidth;
            section.center=fan.browseSources?-reserved*.5f:cursor+sectionWidth*.5f;
            if(section.pinnedCommander){
                tabWidth=section.width=commandWidth;
                tabLeft=available*.5f-commandWidth;
                section.center=tabLeft+commandWidth*.5f;
            }
            // Small rounded corners leave the top band available to the captions.
            float outerInset=i==0||i==zones.Length-1?8:0;
            float buttonWidth=Mathf.Max(1,tabWidth-(fan.browseSources?2:8+outerInset));
            section.captionWidth=buttonWidth;
            var button=buttons[zone];var rect=(RectTransform)button.transform;
            float anchor=fan.browseSources?(section.pinnedCommander?1:0):.5f;
            rect.anchorMin=rect.anchorMax=new Vector2(anchor,.5f);
            rect.sizeDelta=new Vector2(buttonWidth,headerHeight);
            float tabCenter=tabLeft+tabWidth*.5f+(fan.browseSources?0:i==0?4:i==zones.Length-1?-4:0);
            rect.anchoredPosition=new Vector2(tabCenter+(.5f-anchor)*available,0);
            var label=button.GetComponentInChildren<Text>();
            var number=button.transform.Find("CaptionNumber").GetComponent<Text>();
            float titleWidth=Mathf.Ceil(label.preferredWidth)+2;
            float numberWidth=Mathf.Ceil(number.preferredWidth)+2;
            const float titleGap=10;
            if(HudTheme!=null){
                float pager=0;
                if(section.pinnedCommander&&section.count>2)
                    pager=Mathf.Min(2,(section.count-1)/2)*RedesignPageStep(buttonWidth);
                else if(!fan.browseSources && zone is ("Graveyard" or "Exile")){
                    int lastPage=(section.count-1)/RedesignPublicPageSize(section.width);
                    pager=Mathf.Min(lastPage,2)*RedesignPageStep(buttonWidth);
                }
                const float start=8;
                bool drawGlyph=buttonWidth-pager>=titleWidth+numberWidth+titleGap+41;
                float symbol=drawGlyph?(tide?21:25):0;
                float groupWidth=symbol+titleWidth+titleGap+numberWidth;
                float scale=Mathf.Min(1,Mathf.Max(1,buttonWidth-start-8-pager)/groupWidth);
                float inset=start+symbol*scale;
                var left=new Vector2(0,.5f);
                float numberX=inset+(titleWidth+titleGap)*scale;
                RedesignPlace(label.rectTransform,left,left,new Vector2(inset,0),new Vector2(titleWidth,headerHeight));
                RedesignPlace(number.rectTransform,left,left,new Vector2(numberX,0),new Vector2(numberWidth,headerHeight));
                label.rectTransform.localScale=number.rectTransform.localScale=Vector3.one*scale;
                var caption=rect.Find("CaptionSurface").GetComponent<BattlefieldSourceCaptionGraphic>();
                bool active=!fan.browseSources||section.pinnedCommander||zone==fan.browsedSource;
                caption.SetCaption(zone,start,drawGlyph?scale:0,numberX+numberWidth*scale+12,numberX,active);
                if(tide)number.color=active?(Color)HudTheme.Recess:HudText;
                if(fan.browseSources&&!section.pinnedCommander){
                    bool current=zone==fan.browsedSource;
                    label.color=current?HudText:HudMuted;
                    if(!current)caption.accent=Color.Lerp(HudRaised,caption.accent,.52f);
                    var face=button.GetComponent<Image>();
                    face.color=Color.clear;
                    // During a native hand choice all pages of that hand remain accessible.
                    bool canBrowse=section.count>0&&(!choosingHand||zone=="Hand");
                    button.enabled=button.interactable=face.raycastTarget=canBrowse;
                    if(canBrowse)button.onClick.AddListener(()=>{
                        fan.browsedSource=zone;
                        if(other)SetRedesignOtherHandExpanded(true);else SetRedesignHandExpanded(true);
                        PresentIntegrated(connectionText.text);
                    });
                }
            }else{
                RedesignPlace(label.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-(numberWidth+titleGap)*.5f,0),new Vector2(titleWidth,headerHeight-4));
                RedesignPlace(number.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2((titleWidth+titleGap)*.5f,0),new Vector2(numberWidth,headerHeight-4));
            }
            if(IsFourPlayerScene&&!other&&!choosingHand&&!IsCardPickerRequest&&section.count>0&&zone is ("Command" or "Companion")){
                button.enabled=button.interactable=button.targetGraphic.raycastTarget=true;
                // Keep the gallery's page switch too, so closing a companion's
                // face returns to the card and its native actions.
                button.onClick.AddListener(()=>OpenZoneBrowser(player.seat,zone=="Command"?"指挥官":"行侣"));
            }
            if(!section.pinnedCommander)cursor+=tabWidth+gap;
            ShowRedesignElement(rect,new Vector2(0,other?-12:12));
        }
        tabs.sizeDelta=new Vector2(available,headerHeight);
        if(needsPager)PrepareSourcePager(fan,tabs,pagerWidth,headerHeight,reserved);
        else if(fan.sourcePager!=null)fan.sourcePager.gameObject.SetActive(false);
        PositionRedesignSourceTabs();
        return cards;
    }
    private readonly Dictionary<string,Button> otherSourceButtons=new();

    private void PositionRedesignSourceTabs()
    {
        // Expanded labels sit above all four sources; tucked labels stay reachable at the edge.
        void Position(RectTransform tabs,HandFanState fan,float width,float y){
            tabs.anchorMin=tabs.anchorMax=fan.opponent?Vector2.up:Vector2.zero;
            tabs.pivot=new Vector2(.5f,.5f);
            tabs.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Max(1,width-48-fan.pageTrim));
            tabs.anchoredPosition=new Vector2(RedesignHandLeft(fan.opponent)+width*.5f,y);
        }
        if(handSourceTabs!=null)Position(handSourceTabs,ownFan,RedesignHandWidth,redesignHandExpanded?RedesignHandTrayHeight-RedesignHandTrayLower+ExpandedSourceLift-6-handSourceTabs.rect.height*.5f:24);
        if(otherSourceTabs!=null)Position(otherSourceTabs,opponentFan,RedesignOtherHandWidth,-redesignOtherCardsBottom-4-otherSourceTabs.rect.height*.5f);
    }

    private void PresentRedesignOtherBackplate()
    {
        if(opponentFan.slots.Count==0)redesignOtherCardsBottom=0;
        if(redesignOtherBackplate==null){
            redesignOtherBackplate=Panel(surface,"RedesignOtherHandBackplate",Vector2.zero,Vector2.one,Color.white);
            redesignOtherBackplate.gameObject.AddComponent<BattlefieldHandPanelTint>().reversed=true;
            redesignOtherBackplate.GetComponent<Image>().raycastTarget=true;
            redesignOtherBackplate.gameObject.AddComponent<BattlefieldRedesignHandInput>().Configure(ExpandRedesignOtherHand);
        }
        bool visible=view!=null&&opponentFan.slots.Count>0;
        if(!visible){HideRedesignElement(redesignOtherBackplate,new Vector2(0,16));return;}
        float minimumHeight=(otherSourceTabs!=null?otherSourceTabs.rect.height:24)+8;
        float visibleHeight=minimumHeight+redesignOtherCardsBottom;
        float trim=opponentFan.pageTrim;
        RedesignPlace(redesignOtherBackplate,Vector2.up,Vector2.up,new Vector2(RedesignHandLeft(true)+16+trim*.5f,0),new Vector2(RedesignOtherHandWidth-32-trim,visibleHeight));
        var image=redesignOtherBackplate.GetComponent<Image>();
        if(HudTheme!=null){
            image.GetComponent<BattlefieldHandPanelTint>().enabled=false;
            DuelSurface(redesignOtherBackplate,BattlefieldHudGraphic.Surface.OpponentTray,HudTheme.SideAccent);
        } else BattlefieldHandPanelTint.ApplyModernFrame(image);
        int handIndex=otherHand.GetSiblingIndex();
        redesignOtherBackplate.SetSiblingIndex(handIndex-(redesignOtherBackplate.GetSiblingIndex()<handIndex?1:0));
        PositionRedesignSourceTabs();
        ShowRedesignElement(redesignOtherBackplate,new Vector2(0,16));
        SetOverlayLayer(redesignOtherBackplate,HandTrayLayer,true);
    }
}
