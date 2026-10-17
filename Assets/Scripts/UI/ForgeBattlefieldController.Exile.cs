using System;
using System.Linq;
using UnityEngine;

public sealed partial class ForgeBattlefieldController
{
    private sealed class HandFanState {
        public readonly bool opponent;
        public HandFanState(bool opponent){this.opponent=opponent;hits=new ForgeBattlefieldCardPointer.HandHitLayout(!opponent);}
        public readonly System.Collections.Generic.Dictionary<string,RectTransform> regions=new();
        public readonly System.Collections.Generic.List<HandFanSlot> slots=new();
        public readonly ForgeBattlefieldCardPointer.HandHitLayout hits;
        public ForgeHandFanNavigation navigation;
        public UnityEngine.UI.Button previous,next;
        public float available,radius,radians,travel;
    }
    private sealed class HandFanSlot {public ArenaCard item;public RectTransform region;public float x,opacity;}
    private readonly HandFanState ownFan=new(false),opponentFan=new(true);
    private ForgeHandFanNavigation handNavigation=>ownFan.navigation;
    private System.Collections.Generic.List<HandFanSlot> handFanSlots=>ownFan.slots;
    private void ApplyHandFanOffset(float offset,bool snap)=>ApplyHandFanOffset(ownFan,offset,snap);
    private void ApplyHandFanOffset(HandFanState fan,float offset,bool snap)
    {
        foreach(var group in fan.slots.GroupBy(s=>s.region)) {
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=-min;
            foreach(var slot in group) {
                float x=slot.x+(fan.opponent?-offset:offset);
                float theta=fan.radius>0?Mathf.Asin(Mathf.Clamp(x/fan.radius,-Mathf.Sin(18*Mathf.Deg2Rad),Mathf.Sin(18*Mathf.Deg2Rad))):0;
                float rotation=-theta*Mathf.Rad2Deg;var position=new Vector2(64+x,46+1.2f*fan.radius*(Mathf.Cos(theta)-Mathf.Cos(fan.radians)));
                // Rotate the opponent fan around the same reserved centre. Mirroring
                // the +64px inset itself shifts that fan into the player plate.
                if(fan.opponent){position=new Vector2(128-position.x,-position.y);rotation+=180;}
                slot.item.pointer.SetHandFanPose(position,rotation,snap);
                var size=slot.item.root.rect.size;float c=Mathf.Abs(Mathf.Cos(theta)),s=Mathf.Abs(Mathf.Sin(theta));
                var half=new Vector2(size.x*c+size.y*s,size.x*s+size.y*c)*.5f;
                min=Vector2.Min(min,position-half);max=Vector2.Max(max,position+half);
                float localX=fan.opponent?128-position.x:position.x;
                float edge=Mathf.Min(localX-half.x-(64-fan.available*.5f),64+fan.available*.5f-localX-half.x);
                float exposure=fan.navigation.CanPan?Mathf.Clamp01(edge/24):1;
                if(slot.item.pointer.IsCastingDrag)exposure=1;
                var visibility=slot.item.root.GetComponent<CanvasGroup>();
                if(visibility==castingHandGroup)exposure=0;
                visibility.alpha=slot.opacity*exposure;visibility.blocksRaycasts=exposure>.02f;
                slot.item.pointer.SetHandFanExposure(exposure>.02f);
            }
            group.Key.anchoredPosition=(min+max)*.5f;group.Key.sizeDelta=max-min;
        }
        fan.hits.Invalidate();
        if(fan.navigation.CanPan){fan.previous.interactable=offset<fan.travel*.5f-.1f;fan.next.interactable=offset>-fan.travel*.5f+.1f;}
    }
    private void LayoutPlayableHand(RectTransform parent,ForgeCardView[] cards)=>LayoutPlayableHand(parent,cards,ownFan);
    private void LayoutPlayableHand(RectTransform parent,ForgeCardView[] cards,HandFanState fan)
    {
        foreach(var region in fan.regions.Values)region.gameObject.SetActive(false);
        fan.hits.Clear();
        fan.slots.Clear();
        string Region(ForgeCardView c)=>string.IsNullOrEmpty(c.zone)?"Hand":c.zone;
        int Order(string zone)=>zone switch {"Hand"=>0,"Graveyard"=>1,"Exile"=>2,"Command"=>3,_=>4};
        var groups=cards.GroupBy(Region).OrderBy(g=>Order(g.Key)).ThenBy(g=>g.Key).ToArray();
        if(groups.Length==0){if(fan.navigation!=null){fan.navigation.Configure(parent,0,null);fan.navigation.gameObject.SetActive(false);}return;}
        // Leave the player plate and the phase/action dock clear at both ends.
        float available=Mathf.Max(320,Mathf.Min(1240,surface.rect.width-680))-32;
        const float width=182,height=254,gap=88;
        float angle=Mathf.Min(15,(cards.Length-1)*1.8f);
        // Steeper wings need enough room for their rotated corners and edge fade.
        float inset=Mathf.Lerp(96,120,Mathf.InverseLerp(8,15,angle));
        // A few cards should show most of each face. Overflow browses horizontally
        // instead of squeezing even a small source group into a narrow strip.
        float step=Mathf.Clamp((available-inset-groups.Length*width-(groups.Length-1)*gap)/Mathf.Max(1,cards.Length-groups.Length),112,152);
        float total=groups.Sum(g=>width+(g.Count()-1)*step)+(groups.Length-1)*gap;
        float cornerOverhang=width*Mathf.Cos(angle*Mathf.Deg2Rad)+height*Mathf.Sin(angle*Mathf.Deg2Rad)-width;
        bool overflow=total+cornerOverhang>available-16;
        // The edge-fade reserve is needed while browsing, not for a small fan
        // whose full rotated footprint already fits the viewport.
        fan.available=available;fan.travel=overflow?Mathf.Max(0,total-(available-inset)):0;
        if(fan.navigation==null){
            var track=Panel(parent,"HandFanNavigation",new Vector2(64,fan.opponent?-70:70),new Vector2(available,140),Color.clear);track.SetAsFirstSibling();
            fan.navigation=track.gameObject.AddComponent<ForgeHandFanNavigation>();
            UnityEngine.UI.Button Arrow(string name,float side,int direction){
                var b=Button(track,name,"",new Vector2(side*(available*.5f-12),48),new Vector2(24,36),()=>fan.navigation.MoveBy(direction*fan.available*.65f),Ink);
                var glyph=Rect(b.transform,"Chevron",Vector2.zero,new Vector2(20,30)).gameObject.AddComponent<BattlefieldScrollChevron>();glyph.direction=-direction;glyph.color=Paper;glyph.raycastTarget=false;return b;
            }
            fan.previous=Arrow("Previous",-1,1);fan.next=Arrow("Next",1,-1);
        }
        fan.navigation.gameObject.SetActive(fan.travel>.1f);fan.navigation.Configure(parent,fan.travel,o=>ApplyHandFanOffset(fan,o,true));
        ((RectTransform)fan.navigation.transform).sizeDelta=new Vector2(available,140);
        fan.previous.GetComponent<RectTransform>().anchoredPosition=new Vector2(-available*.5f+12,fan.opponent?-48:48);fan.next.GetComponent<RectTransform>().anchoredPosition=new Vector2(available*.5f-12,fan.opponent?-48:48);
        float x=-total*.5f;int order=0;
        float spread=Mathf.Max(1,((overflow?Mathf.Min(total,available-inset):total)-width)*.5f);
        fan.radians=angle*Mathf.Deg2Rad;fan.radius=angle>0?spread/Mathf.Sin(fan.radians):0;
        foreach(var group in groups) {
            var members=group.ToArray();float span=width+(members.Length-1)*step,center=x+span*.5f;
            if(!fan.regions.TryGetValue(group.Key,out var region)) {
                region=Rect(parent,"PlayableRegion_"+group.Key,Vector2.zero,Vector2.one);fan.regions.Add(group.Key,region);
            }
            for(int i=0;i<members.Length;i++) {
                // Source gaps occupy space on the same arc; they do not restart the fan.
                float offset=i-(members.Length-1)*.5f,cardX=center+offset*step;
                var card=members[i];string key=(fan.opponent?"OtherFan:":"Hand:")+card.id;
                var initial=new Vector2(64+cardX,46);
                DrawArenaCard(parent,card,1,key,key,fan.opponent?new Vector2(128-initial.x,-initial.y):initial,new Vector2(width,height),true,fan.opponent?180:0,order++);
                if(fan.opponent){
                    var item=arenaCards[key];item.root.GetComponent<CanvasGroup>().alpha=1;
                    item.button.onClick.RemoveAllListeners();
                    if(!card.faceDown)item.button.onClick.AddListener(()=>InspectIntegrated(card));
                    item.pointer.DisableActions();
                }
                arenaCards[key].pointer.ConfigurePlayRegion(region);
                arenaCards[key].pointer.ConfigureHandNavigation(fan.navigation);
                fan.hits.Add(arenaCards[key].pointer);
                fan.slots.Add(new HandFanSlot{item=arenaCards[key],region=region,x=cardX,opacity=arenaCards[key].root.GetComponent<CanvasGroup>().alpha});
            }
            // Drag cancellation uses the complete resting footprint of this source group.
            region.gameObject.SetActive(true);
            x+=span+gap;
        }
        ApplyHandFanOffset(fan,fan.navigation.Offset,false);
    }
    private ForgeCardView[] AvailableExileCards() => (view?.playableGraveyard??Array.Empty<ForgeCardView>()).Concat(view?.playableExile??Array.Empty<ForgeCardView>())
        .Concat((view?.playableCommand??Array.Empty<ForgeCardView>()).Where(c=>c.ownerSeat==ActionPlayerSeat))
        .Concat((view?.players??Array.Empty<ForgePlayerView>()).Where(p=>p.seat==ActionPlayerSeat)
            .SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>()).Where(HasPreparedSpell).Select(c=>c.preparedSpell.ToCard()))
        .GroupBy(c=>c.id).Select(g=>KnownExileCastingFace(g.First())).ToArray();

    private ForgeCardView KnownExileCastingFace(ForgeCardView card)
    {
        // The zone object remains face down. This recipient-only casting preview
        // uses its supplied known print, never the face-down creature's 2/2 stats.
        if(!card.faceDown || string.IsNullOrEmpty(card.knownFacePrintingKey))return card;
        catalog.TryGetValue(card.knownFacePrintingKey,out var definition);
        return new ForgeCardView {
            id=card.id,ownerSeat=card.ownerSeat,zone=card.zone,stateName="ExileCastingFace",
            name=card.knownFaceName,printingKey=card.knownFacePrintingKey,
            currentRules=card.knownFaceRules,currentType=definition?.GetTypeLine(),
            manaCost=definition?.manaCost,power=definition?.attack??0,toughness=definition?.defence??0,
            creature=definition?.HasType(CardType.Creature)??false,
            land=definition?.HasType(CardType.Land)??false,
            planeswalker=definition?.HasType(CardType.Planeswalker)??false,
            battle=definition?.HasType(CardType.Battle)??false
        };
    }

}
