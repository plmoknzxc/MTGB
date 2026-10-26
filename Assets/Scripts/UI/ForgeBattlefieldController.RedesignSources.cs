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
        public float center, width, travel;
        public RectTransform region, divider;
        public ForgeHandFanNavigation navigation;
        public Button previous, next;
        public readonly List<HandFanSlot> slots = new();
    }
    private readonly Dictionary<string,RedesignHandSection> redesignOwnSections=new(), redesignOtherSections=new();
    private Dictionary<string,RedesignHandSection> RedesignSections(HandFanState fan)=>fan.opponent?redesignOtherSections:redesignOwnSections;
    private static readonly string[] RedesignSourceOrder={"Hand","Graveyard","Exile","Command"};
    private static string RedesignSource(ForgeCardView card)=>HandSource(card)=="Command"&&!card.commander&&(card.keywords??Array.Empty<string>()).Any(k=>string.Equals(k,"COMPANION",StringComparison.OrdinalIgnoreCase))?"Companion":HandSource(card);
    private static IEnumerable<string> RedesignOrderedSources(ForgeCardView[] cards)=>RedesignSourceOrder.Where(z=>cards.Any(c=>RedesignSource(c)==z)).Concat(cards.Select(RedesignSource).Where(z=>!RedesignSourceOrder.Contains(z)).Distinct());
    private string RedesignSourceLabel(string zone)=>zone=="Companion"?TapPaymentText("行侣","Companion","相棒"):zone=="Command"?TapPaymentText("指挥官","Commander","統率者"):zone=="Exile"?TapPaymentText("放逐区","Exile","追放領域"):HandSourceLabel(zone);

    private static int[] RedesignPayableSourceIds(ForgeSeatView snapshot)
        =>snapshot?.awaitingInput==true&&snapshot.request?.kind=="InputPassPriority"
            ?(snapshot.request.options??Array.Empty<ForgeOption>()).Where(o=>!o.disabled&&!o.requiresPaymentCheck&&o.id?.StartsWith("card:",StringComparison.Ordinal)==true)
                .Select(o=>int.TryParse(o.id.Substring(5),out int id)?id:0).Where(id=>id>0).Distinct().ToArray()
            :Array.Empty<int>();

    private static void StyleRedesignSourceButton(Button button,bool passive=false)
    {
        var image=button.targetGraphic as Image;
        if(image==null)return;
        BattlefieldHandPanelTint.ApplyModernFrame(image,true);
        button.transition=passive?Selectable.Transition.None:Selectable.Transition.ColorTint;
        var colors=button.colors;
        colors.normalColor=Color.white;colors.selectedColor=Color.white;
        colors.highlightedColor=new Color(1.12f,1.1f,1.06f);
        colors.pressedColor=new Color(.76f,.79f,.82f);
        colors.disabledColor=new Color(.5f,.5f,.5f);
        colors.fadeDuration=.12f;button.colors=colors;
    }

    private ForgeCardView[] PresentRedesignSources(ForgeCardView[] cards,HandFanState fan,bool choosingHand)
    {
        bool other=fan.opponent;
        var player=(view?.players??Array.Empty<ForgePlayerView>()).FirstOrDefault(p=>other?p.seat!=DisplayedHandSeat:p.seat==DisplayedHandSeat);
        var usable=(!other&&view?.awaitingInput==true?RedesignPayableSourceIds(view):player?.usableSourceIds??Array.Empty<int>()).ToHashSet();
        // Shared public faces also cover permissions to use another player's card.
        var publicSources=(view?.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>(p.graveyardCards??Array.Empty<ForgeCardView>()).Concat(p.exileCards??Array.Empty<ForgeCardView>()))
            .Where(c=>c!=null&&!c.faceDown&&usable.Contains(c.id));
        cards=cards.Concat(publicSources).GroupBy(c=>c.id).Select(g=>g.First())
            .Where(c=>HandSource(c) is not ("Graveyard" or "Exile")||usable.Contains(c.id)).ToArray();
        // Empty or currently unavailable outside-hand sources occupy no tray space.
        var commandCards=(player?.commandCards??Array.Empty<ForgeCardView>())
            .Where(c=>c!=null&&!c.faceDown&&c.zone=="Command"&&!c.emblem&&!c.effectMarker);
        cards=cards.Concat(commandCards).GroupBy(c=>c.id).Select(g=>g.First())
            .Where(c=>HandSource(c)!="Command"||usable.Contains(c.id)).ToArray();
        var zones=RedesignOrderedSources(cards).ToArray();
        var tabs=other?otherSourceTabs:handSourceTabs;
        if(tabs==null){
            tabs=Rect(surface,other?"OtherHandSourceTabs":"HandSourceTabs",Vector2.zero,new Vector2(600,36));
            if(other)otherSourceTabs=tabs;else handSourceTabs=tabs;
            SetOverlayLayer(tabs,3,true);
        }
        tabs.gameObject.SetActive(true);
        var buttons=other?otherSourceButtons:handSourceButtons;
        foreach(var pair in buttons)if(!zones.Contains(pair.Key))HideRedesignElement((RectTransform)pair.Value.transform,new Vector2(0,other?-12:12));
        if(zones.Length==0)return cards;
        var sections=RedesignSections(fan);
        float lane=other?RedesignOtherHandMaximumWidth:RedesignHandMaximumWidth;
        if(other)redesignOtherLane=lane;else redesignOwnLane=lane;
        const float gap=RedesignSourceGap;
        var minima=new List<float>();var headerMinima=new List<float>();var weights=new List<float>();
        var accent=other?new Color(.92f,.66f,.65f):new Color(.66f,.82f,.92f);
        foreach(string zone in zones){
            if(!sections.ContainsKey(zone))sections.Add(zone,new RedesignHandSection{zone=zone});
            if(!buttons.TryGetValue(zone,out var button)){
                button=Button(tabs,"Source_"+zone,"",Vector2.zero,new Vector2(120,36),()=>{},new Color(.035f,.065f,.08f,.92f));
                buttons.Add(zone,button);
                var line=Panel(button.transform,"SectionLine",Vector2.zero,Vector2.one,accent);
                line.GetComponent<Image>().raycastTarget=false;
            }
            int count=cards.Count(c=>RedesignSource(c)==zone);
            var label=button.GetComponentInChildren<Text>();label.GetComponent<PreferenceText>().Literal=true;
            label.text=RedesignSourceLabel(zone)+"  "+count;
            PreferenceText.SetSize(label,18);label.resizeTextForBestFit=false;
            // Measure a single title line before assigning this frame's final width.
            label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Overflow;
            float outerInset=zone==zones[0]||zone==zones[zones.Length-1]?8:0;
            headerMinima.Add(Mathf.Ceil(label.preferredWidth)+40+outerInset);
            minima.Add(Mathf.Max(headerMinima.Last(),RedesignSourceGutter+166));
            // Begin with complete card faces and a small gap. Only compress
            // their overlap when all occupied sources exceed the available lane.
            float desired=RedesignSourceGutter+166+Mathf.Max(0,count-1)*174;
            weights.Add(Mathf.Max(0,desired-minima.Last()));
            label.color=accent;button.gameObject.SetActive(true);button.interactable=true;
            StyleRedesignSourceButton(button,true);
            // Keep the framed caption, without a button hit target or dimmed
            // disabled tint. Revealing the hand still belongs to its tray/cards.
            button.onClick.RemoveAllListeners();button.enabled=false;
            button.transition=Selectable.Transition.None;
            var disabledVisual=button.GetComponent<DisabledButtonVisual>();if(disabledVisual!=null)disabledVisual.enabled=false;
            // Cancel any tint tween started before this caption became passive.
            button.targetGraphic.CrossFadeColor(Color.white,0,true,true);label.canvasRenderer.SetAlpha(1);
            button.targetGraphic.raycastTarget=false;label.raycastTarget=false;
        }
        // Size to the occupied cards first; the lane is only a maximum. Large
        // sources keep every physical card and compress their overlap at that limit.
        float occupied=minima.Sum()+weights.Sum()+(zones.Length-1)*gap;
        float width=Mathf.Min(lane,occupied+48),available=width-48;
        if(other)redesignOtherWidth=width;else redesignOwnWidth=width;
        float budget=Mathf.Max(1,available-(zones.Length-1)*gap),minimum=minima.Sum(),remaining=Mathf.Max(0,budget-minimum),weight=weights.Sum(),distributed=Mathf.Min(remaining,weight),cursor=-available*.5f;
        float headers=headerMinima.Sum();
        float headerHeight=zones.Max(z=>Mathf.Max(44,buttons[z].GetComponentInChildren<Text>().preferredHeight+12));
        for(int i=0;i<zones.Length;i++){
            string zone=zones[i];var section=sections[zone];
            section.width=minimum>budget
                ?headers>=budget?headerMinima[i]*budget/headers:headerMinima[i]+(budget-headers)*(minima[i]-headerMinima[i])/(minimum-headers)
                :minima[i]+(weight>0?distributed*weights[i]/weight:0)+(i==0?remaining-distributed:0);
            section.center=cursor+section.width*.5f;
            // Small rounded corners leave the top band available to the captions.
            float outerInset=i==0||i==zones.Length-1?8:0;
            float buttonWidth=Mathf.Max(1,section.width-8-outerInset);
            var button=buttons[zone];var rect=(RectTransform)button.transform;rect.sizeDelta=new Vector2(buttonWidth,headerHeight);rect.anchoredPosition=new Vector2(section.center+(i==0?4:i==zones.Length-1?-4:0),0);
            var label=button.GetComponentInChildren<Text>();label.rectTransform.sizeDelta=new Vector2(Mathf.Max(1,buttonWidth-12),headerHeight-4);
            var line=(RectTransform)rect.Find("SectionLine");
            line.gameObject.SetActive(false);
            RedesignPlace(line,new Vector2(0,other?0:1),new Vector2(0,other?0:1),new Vector2(8,0),new Vector2(Mathf.Max(1,buttonWidth-16),2));
            cursor+=section.width+gap;
            ShowRedesignElement(rect,new Vector2(0,other?-12:12),interactive:false);
        }
        tabs.sizeDelta=new Vector2(available,headerHeight);
        PositionRedesignSourceTabs();
        return cards;
    }
    private readonly Dictionary<string,Button> otherSourceButtons=new();

    private void PositionRedesignSourceTabs()
    {
        // Expanded labels sit above all four sources; tucked labels stay reachable at the edge.
        if(handSourceTabs!=null){handSourceTabs.anchorMin=handSourceTabs.anchorMax=Vector2.zero;handSourceTabs.pivot=new Vector2(.5f,.5f);handSourceTabs.anchoredPosition=new Vector2(RedesignHandLeft(false)+RedesignHandWidth*.5f,redesignHandExpanded?RedesignHandTrayHeight-RedesignHandTrayLower-6-handSourceTabs.rect.height*.5f:24);}
        if(otherSourceTabs!=null){otherSourceTabs.anchorMin=otherSourceTabs.anchorMax=Vector2.up;otherSourceTabs.pivot=new Vector2(.5f,.5f);otherSourceTabs.anchoredPosition=new Vector2(RedesignHandLeft(true)+RedesignOtherHandWidth*.5f,-redesignOtherCardsBottom-4-otherSourceTabs.rect.height*.5f);}
    }

    private void PresentRedesignOtherBackplate()
    {
        if(opponentFan.slots.Count==0)redesignOtherCardsBottom=0;
        if(redesignOtherBackplate==null){
            redesignOtherBackplate=Panel(surface,"RedesignOtherHandBackplate",Vector2.zero,Vector2.one,Color.white);
            redesignOtherBackplate.gameObject.AddComponent<BattlefieldHandPanelTint>().reversed=true;
            redesignOtherBackplate.GetComponent<Image>().raycastTarget=false;
        }
        bool visible=view!=null&&opponentFan.slots.Count>0;
        if(!visible){HideRedesignElement(redesignOtherBackplate,new Vector2(0,16));return;}
        float minimumHeight=(otherSourceTabs!=null?otherSourceTabs.rect.height:24)+8;
        float visibleHeight=minimumHeight+redesignOtherCardsBottom;
        RedesignPlace(redesignOtherBackplate,Vector2.up,Vector2.up,new Vector2(RedesignHandLeft(true)+16,0),new Vector2(RedesignOtherHandWidth-32,visibleHeight));
        BattlefieldHandPanelTint.ApplyModernFrame(redesignOtherBackplate.GetComponent<Image>());
        int handIndex=otherHand.GetSiblingIndex();
        redesignOtherBackplate.SetSiblingIndex(handIndex-(redesignOtherBackplate.GetSiblingIndex()<handIndex?1:0));
        PositionRedesignSourceTabs();
        ShowRedesignElement(redesignOtherBackplate,new Vector2(0,16),interactive:false);
    }
}
