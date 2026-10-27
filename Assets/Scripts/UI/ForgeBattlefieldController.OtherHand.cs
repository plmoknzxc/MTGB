using System;
using System.Linq;
using UnityEngine;

public sealed partial class ForgeBattlefieldController
{
    private static ForgeCardView[] AnonymousHand(ForgePlayerView player)=>Enumerable.Range(0,Math.Max(0,player.handCount))
        .Select(i=>new ForgeCardView{id=-100000-player.seat*1000-i,ownerSeat=player.seat,faceDown=true,zone="Hand",name="Face-down card"}).ToArray();
    private void PresentOtherHand(ForgePlayerView player)
    {
        var known=VisibleHand(player).Where(c=>c!=null&&!c.faceDown&&!string.IsNullOrEmpty(c.name))
            .GroupBy(c=>c.id).Select(g=>g.First()).Take(Math.Max(0,player.handCount)).ToArray();
        // Anonymous backs contain no opponent hand identities or submit listeners.
        var hidden=Enumerable.Range(known.Length,Math.Max(0,player.handCount-known.Length))
            .Select(i=>new ForgeCardView{id=-1-i,faceDown=true,zone="Hand",name="Face-down card"});
        var exile=(player.publicPlayableExile??Array.Empty<ForgeCardView>())
            .Where(c=>c!=null&&!c.faceDown&&!string.IsNullOrEmpty(c.name)).GroupBy(c=>c.id).Select(g=>g.First());
        var grave=(player.publicPlayableGraveyard??Array.Empty<ForgeCardView>())
            .Where(c=>c!=null&&!c.faceDown&&!string.IsNullOrEmpty(c.name)).GroupBy(c=>c.id).Select(g=>g.First());
        var command=(player.commandCards??Array.Empty<ForgeCardView>()).Where(c=>c!=null&&!c.faceDown&&c.commander&&c.zone=="Command");
        LayoutPlayableHand(otherHand,known.Concat(hidden).Concat(exile).Concat(IsRedesignLayout?grave.Concat(command):Array.Empty<ForgeCardView>()).ToArray(),opponentFan);
    }
}
