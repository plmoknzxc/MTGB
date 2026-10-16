using System;
using System.Linq;
using UnityEngine;

public sealed partial class ForgeBattlefieldController
{
    private readonly System.Collections.Generic.Dictionary<string,RectTransform> playableHandRegions=new();
    private readonly ForgeBattlefieldCardPointer.HandHitLayout playableHandHits=new(true);
    private sealed class HandFanSlot {public ArenaCard item;public RectTransform region;public float x,opacity;}
    private readonly System.Collections.Generic.List<HandFanSlot> handFanSlots=new();
    private ForgeHandFanNavigation handNavigation;
    private UnityEngine.UI.Button handPrevious,handNext;
    private float handFanAvailable,handFanRadius,handFanRadians;
    private void ApplyHandFanOffset(float offset,bool snap)
    {
        foreach(var group in handFanSlots.GroupBy(s=>s.region)) {
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=-min;
            foreach(var slot in group) {
                float x=slot.x+offset;
                float theta=handFanRadius>0?Mathf.Asin(Mathf.Clamp(x/handFanRadius,-Mathf.Sin(18*Mathf.Deg2Rad),Mathf.Sin(18*Mathf.Deg2Rad))):0;
                float rotation=-theta*Mathf.Rad2Deg;var position=new Vector2(64+x,46+1.2f*handFanRadius*(Mathf.Cos(theta)-Mathf.Cos(handFanRadians)));
                slot.item.pointer.SetHandFanPose(position,rotation,snap);
                var size=slot.item.root.rect.size;float c=Mathf.Abs(Mathf.Cos(theta)),s=Mathf.Abs(Mathf.Sin(theta));
                var half=new Vector2(size.x*c+size.y*s,size.x*s+size.y*c)*.5f;
                min=Vector2.Min(min,position-half);max=Vector2.Max(max,position+half);
                float edge=Mathf.Min(position.x-half.x-(64-handFanAvailable*.5f),64+handFanAvailable*.5f-position.x-half.x);
                float exposure=handNavigation.CanPan?Mathf.Clamp01(edge/24):1;
                if(slot.item.pointer.IsCastingDrag)exposure=1;
                var visibility=slot.item.root.GetComponent<CanvasGroup>();
                if(visibility==castingHandGroup)exposure=0;
                visibility.alpha=slot.opacity*exposure;visibility.blocksRaycasts=exposure>.02f;
                slot.item.pointer.SetHandFanExposure(exposure>.02f);
            }
            group.Key.anchoredPosition=(min+max)*.5f;group.Key.sizeDelta=max-min;
        }
        playableHandHits.Invalidate();
        if(handNavigation.CanPan){handPrevious.interactable=offset<handFanTravel*.5f-.1f;handNext.interactable=offset>-handFanTravel*.5f+.1f;}
    }
    private float handFanTravel;
    private void LayoutPlayableHand(RectTransform parent,ForgeCardView[] cards)
    {
        foreach(var region in playableHandRegions.Values)region.gameObject.SetActive(false);
        playableHandHits.Clear();
        handFanSlots.Clear();
        string Region(ForgeCardView c)=>string.IsNullOrEmpty(c.zone)?"Hand":c.zone;
        int Order(string zone)=>zone switch {"Hand"=>0,"Graveyard"=>1,"Exile"=>2,"Command"=>3,_=>4};
        var groups=cards.GroupBy(Region).OrderBy(g=>Order(g.Key)).ThenBy(g=>g.Key).ToArray();
        if(groups.Length==0){if(handNavigation!=null){handNavigation.Configure(parent,0,null);handNavigation.gameObject.SetActive(false);}return;}
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
        handFanAvailable=available;handFanTravel=overflow?Mathf.Max(0,total-(available-inset)):0;
        if(handNavigation==null){
            var track=Panel(parent,"HandFanNavigation",new Vector2(64,70),new Vector2(available,140),Color.clear);track.SetAsFirstSibling();
            handNavigation=track.gameObject.AddComponent<ForgeHandFanNavigation>();
            UnityEngine.UI.Button Arrow(string name,float side,int direction){
                var b=Button(track,name,"",new Vector2(side*(available*.5f-12),48),new Vector2(24,36),()=>handNavigation.MoveBy(direction*handFanAvailable*.65f),Ink);
                var glyph=Rect(b.transform,"Chevron",Vector2.zero,new Vector2(20,30)).gameObject.AddComponent<BattlefieldScrollChevron>();glyph.direction=-direction;glyph.color=Paper;glyph.raycastTarget=false;return b;
            }
            handPrevious=Arrow("Previous",-1,1);handNext=Arrow("Next",1,-1);
        }
        handNavigation.gameObject.SetActive(handFanTravel>.1f);handNavigation.Configure(parent,handFanTravel,o=>ApplyHandFanOffset(o,true));
        ((RectTransform)handNavigation.transform).sizeDelta=new Vector2(available,140);
        handPrevious.GetComponent<RectTransform>().anchoredPosition=new Vector2(-available*.5f+12,48);handNext.GetComponent<RectTransform>().anchoredPosition=new Vector2(available*.5f-12,48);
        float x=-total*.5f;int order=0;
        float spread=Mathf.Max(1,((overflow?Mathf.Min(total,available-inset):total)-width)*.5f);
        handFanRadians=angle*Mathf.Deg2Rad;handFanRadius=angle>0?spread/Mathf.Sin(handFanRadians):0;
        foreach(var group in groups) {
            var members=group.ToArray();float span=width+(members.Length-1)*step,center=x+span*.5f;
            if(!playableHandRegions.TryGetValue(group.Key,out var region)) {
                region=Rect(parent,"PlayableRegion_"+group.Key,Vector2.zero,Vector2.one);playableHandRegions.Add(group.Key,region);
            }
            for(int i=0;i<members.Length;i++) {
                // Source gaps occupy space on the same arc; they do not restart the fan.
                float offset=i-(members.Length-1)*.5f,cardX=center+offset*step;
                var card=members[i];string key="Hand:"+card.id;
                DrawArenaCard(parent,card,1,key,key,new Vector2(64+cardX,46),new Vector2(width,height),true,0,order++);
                arenaCards[key].pointer.ConfigurePlayRegion(region);
                arenaCards[key].pointer.ConfigureHandNavigation(handNavigation);
                playableHandHits.Add(arenaCards[key].pointer);
                handFanSlots.Add(new HandFanSlot{item=arenaCards[key],region=region,x=cardX,opacity=arenaCards[key].root.GetComponent<CanvasGroup>().alpha});
            }
            // Drag cancellation uses the complete resting footprint of this source group.
            region.gameObject.SetActive(true);
            x+=span+gap;
        }
        ApplyHandFanOffset(handNavigation.Offset,false);
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
