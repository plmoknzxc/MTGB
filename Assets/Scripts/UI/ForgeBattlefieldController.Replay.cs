using UnityEngine;
using System;
using System.Linq;

public sealed partial class ForgeBattlefieldController
{
    public void PresentReplay(ForgeSeatView state, int perspective, string[] names, bool seek, ForgeSeatView otherView=null)
    {
        if (seek) { ResetCastingVisual(); ResetNotices(); }
        // Keep the recorded views immutable; hide interactive choices while preserving the board.
        var display = ServerSnapshotJson.Copy(state);
        // Legacy replays carry the opponent's permission list in their own
        // recorded view. Intersect with faces already public to this viewer;
        // private foretell/morph previews must never cross the perspective.
        foreach(var player in display.players??Array.Empty<ForgePlayerView>())if(player.seat!=perspective&&player.publicPlayableExile==null&&otherView?.seat==player.seat) {
            var visible=(display.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>p.exileCards??Array.Empty<ForgeCardView>()).Where(c=>!c.faceDown).ToDictionary(c=>c.id,c=>c);
            player.publicPlayableExile=(otherView.playableExile??Array.Empty<ForgeCardView>()).Where(c=>!c.faceDown&&visible.ContainsKey(c.id)).Select(c=>visible[c.id]).ToArray();
        }
        foreach(var player in display.players??Array.Empty<ForgePlayerView>())if(player.seat!=perspective&&player.publicPlayableGraveyard==null&&otherView?.seat==player.seat) {
            var visible=(display.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>p.graveyardCards??Array.Empty<ForgeCardView>()).Where(c=>c!=null&&!c.faceDown).GroupBy(c=>c.id).ToDictionary(g=>g.Key,g=>g.First());
            player.publicPlayableGraveyard=(otherView.playableGraveyard??Array.Empty<ForgeCardView>()).Where(c=>c!=null&&!c.faceDown&&visible.ContainsKey(c.id)).Select(c=>visible[c.id]).ToArray();
        }
        // Older recordings have no eligibility field. Preserve only positive
        // native payment/timing evidence from the matching recorded perspective.
        foreach(var player in display.players??Array.Empty<ForgePlayerView>())if(player.usableSourceIds==null){
            var source=player.seat==perspective?state:otherView?.seat==player.seat?otherView:null;
            var eligible=RedesignPayableSourceIds(source);
            if(player.seat==perspective){
                var ownSourceIds=(source?.playableGraveyard??Array.Empty<ForgeCardView>()).Concat(source?.playableExile??Array.Empty<ForgeCardView>()).Where(c=>c!=null).Select(c=>c.id).ToHashSet();
                eligible=eligible.Where(ownSourceIds.Contains).ToArray();
            }else{
                var publicIds=(display.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>(p.graveyardCards??Array.Empty<ForgeCardView>()).Concat(p.exileCards??Array.Empty<ForgeCardView>()))
                    .Where(c=>c!=null&&!c.faceDown).Select(c=>c.id).ToHashSet();
                eligible=eligible.Where(publicIds.Contains).ToArray();
            }
            player.usableSourceIds=eligible;
        }
        display.awaitingInput = false; display.request = null;
        Present(display, perspective, false, null, null, GamePreferences.T("观看录像"));
        if (!integrated) return;
        SetText("PlayerPlate/Name", names[perspective - 1]);
        SetText("OpponentPlate/Name", names[2 - perspective]);
        Node("PriorityPill").gameObject.SetActive(false);
        advanceButton.gameObject.SetActive(false); endTurnButton.gameObject.SetActive(false); undoManaButton.gameObject.SetActive(false);
        if (mulliganChoices != null) mulliganChoices.gameObject.SetActive(false);
        actionRoot.parent.parent.gameObject.SetActive(false);
    }
}
