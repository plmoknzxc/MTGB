using System;
using System.Linq;
using UnityEngine;

/// <summary>Server-side public projection. Never forward a player's requests or private zones.</summary>
public static class OnlineSpectatorView
{
    public static OnlinePlayerViewState Create(OnlinePlayerViewState source, int spectatorSeat)
    {
        if(source?.forge==null)return null;
        var view = source.forge;
        var result = new OnlinePlayerViewState {
            matchId=source.matchId, viewerSeat=spectatorSeat, appliedSequence=source.appliedSequence,
            outcome=source.outcome,
            forge=new ForgeSeatView {
                seat=1, phase=view.phase, dayTime=view.dayTime,turn=view.turn, turnSeat=view.turnSeat,
                prioritySeat=view.prioritySeat, stackCount=view.stackCount,
                notices=(view.notices??Array.Empty<ForgeNotice>()).Where(n=>n.publicNotice).Select(n=>new ForgeNotice {
                    id=n.id,actorSeat=n.actorSeat,kind=n.kind,value=n.value,message=n.message,title=n.title,publicNotice=true,sourceCard=PublicCard(n.sourceCard),
                    reason=n.reason,fromZone=n.fromZone,toZone=n.toZone,cards=(n.cards??Array.Empty<ForgeCardView>()).Select(PublicCard).ToArray()
                }).ToArray(),
                globalEffects=(view.globalEffects??Array.Empty<ForgeCardView>()).Select(PublicCard).ToArray(),
                players=(view.players??Array.Empty<ForgePlayerView>()).Select(p=>new ForgePlayerView {
                    seat=p.seat,controllingSeat=p.controllingSeat,targetId=p.targetId,life=p.life,handCount=p.handCount,libraryCount=p.libraryCount,
                    libraryTop=p.libraryTopPublic?PublicCard(p.libraryTop):null,libraryTopPublic=p.libraryTopPublic,
                    redMana=p.redMana,manaPool=p.manaPool?.ToArray(),snowManaPool=p.snowManaPool?.ToArray(),
                    manaPoolEntries=p.manaPoolEntries?.Select(e=>new ForgeManaPoolEntry {
                        id=e.id,symbol=e.symbol,count=e.count,snow=e.snow,restricted=e.restricted,
                        // A player's visible source name can come from their
                        // private hand. Observers need buckets, not that identity.
                        sourceName=""
                    }).ToArray(),graveyard=p.graveyard,exileCount=p.exileCount,
                    counters=p.counters,speed=p.speed,ringTemptedCount=p.ringTemptedCount,ringBearerId=p.ringBearerId,completedDungeons=p.completedDungeons,
                    graveyardCards=(p.graveyardCards??Array.Empty<ForgeCardView>()).Select(PublicCard).ToArray(),
                    exileCards=(p.exileCards??Array.Empty<ForgeCardView>()).Select(PublicCard).ToArray(),
                    reviewGraveyard=(p.reviewGraveyard??Array.Empty<ForgeCardView>()).Select(PublicCard).ToArray(),
                    reviewExile=(p.reviewExile??Array.Empty<ForgeCardView>()).Select(PublicCard).ToArray(),
                    effectMarkers=(p.effectMarkers??Array.Empty<ForgeCardView>()).Select(PublicCard).ToArray(),
                    emblems=(p.emblems??Array.Empty<ForgeCardView>()).Select(PublicCard).ToArray(),
                    hand=Array.Empty<ForgeCardView>(),battlefield=(p.battlefield??Array.Empty<ForgeCardView>()).Select(PublicCard).ToArray()
                }).ToArray(),
                stackItems=(view.stackItems??Array.Empty<ForgeStackItem>()).Select(s=>new ForgeStackItem {
                    id=s.id,controllerSeat=s.controllerSeat,kind=s.kind,copied=s.copied,
                    description=s.source?.faceDown==true?"Face-down spell":s.description,
                    hasXValue=s.source?.faceDown!=true&&s.hasXValue,xValue=s.source?.faceDown==true?0:s.xValue,
                    source=PublicCard(s.source)
                }).ToArray(),
                playableExile=Array.Empty<ForgeCardView>(),playableGraveyard=Array.Empty<ForgeCardView>(),playableCommand=Array.Empty<ForgeCardView>()
            }
        };
        result.forge.stack=result.forge.stackItems.Select(s=>s.description).ToArray();
        result.SealHash();
        return result;
    }

    private static ForgeCardView PublicCard(ForgeCardView card)
    {
        if(card==null)return null;
        if(!card.faceDown)return ServerSnapshotJson.Copy(card);
        return new ForgeCardView { id=card.id,ownerSeat=card.ownerSeat,faceDown=true,name="Face-down card",
            zone=card.zone,tapped=card.tapped,power=card.power,toughness=card.toughness,damage=card.damage,
            detained=card.detained,exertedBySeats=card.exertedBySeats,exertedThisTurn=card.exertedThisTurn,
            creature=card.creature,attacking=card.attacking,blocking=card.blocking,
            defenderId=card.defenderId,defenderKind=card.defenderKind,blockedAttackerIds=card.blockedAttackerIds,
            powerBaseChanged=card.powerBaseChanged,toughnessBaseChanged=card.toughnessBaseChanged,effectiveBasePower=card.effectiveBasePower,effectiveBaseToughness=card.effectiveBaseToughness,hasStatBaseline=card.hasStatBaseline,basePower=card.basePower,baseToughness=card.baseToughness,grantedKeywords=card.grantedKeywords,
            counters=card.counters,faceDownKind=card.faceDownKind,currentType=card.currentType,currentRules=card.currentRules,keywords=card.keywords };
    }
}
