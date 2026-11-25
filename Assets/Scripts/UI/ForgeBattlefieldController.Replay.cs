using UnityEngine;
using System;
using System.Linq;

public sealed partial class ForgeBattlefieldController
{
    private MatchReplay historyReplay;
    private bool replayNoticesPrepared;
    private int historyReplayFrame=-1,historyReplaySeat,historyReplayLanguage;

    public void PresentReplayFrame(MatchReplay replay,int frame,int perspective,bool seek)
    {
        var frames=replay.frames;
        var state=frames[frame].views[perspective-1];
        bool rebuild=historyReplay!=replay||historyReplaySeat!=perspective||frame<historyReplayFrame
            ||historyReplayLanguage!=GamePreferences.Revision||view?.restartRevision!=state.restartRevision;
        if(view!=null&&view.restartRevision!=state.restartRevision){ResetRestartPresentation();view=null;}
        replayPlayerNames=replay.players;
        if(rebuild||seek){
            ResetCastingVisual();ResetZoneMotion();ResetTransientNotices();
            // The immediate predecessor distinguishes a new reveal at this
            // frame from a private reveal still retained in the rolling log.
            if(frame>0&&frames[frame-1].views[perspective-1].restartRevision==state.restartRevision)
                lastNoticeId=(frames[frame-1].views[perspective-1].notices??Array.Empty<ForgeNotice>()).Select(n=>n.id).DefaultIfEmpty(0).Max();
        }
        int start=historyReplayFrame+1;
        if(rebuild){
            ResetSidebarHistory();
            start=frame;
            while(start>0&&frames[start-1].views[perspective-1].restartRevision==state.restartRevision)start--;
        }
        // Collect skipped frames without creating UI or transient notifications.
        var shown=view;int shownSeat=seat;
        try {
            seat=perspective;
            for(int i=start;i<frame;i++){
                view=frames[i].views[perspective-1];
                CollectSidebarHistory();
            }
        } finally {view=shown;seat=shownSeat;}
        replayNoticesPrepared=true;
        try {PresentReplay(state,perspective,replay.players,false,recordedViews:frames[frame].views);}
        finally {replayNoticesPrepared=false;}
        historyReplay=replay;historyReplaySeat=perspective;historyReplayFrame=frame;historyReplayLanguage=GamePreferences.Revision;
    }

    public void PresentReplay(ForgeSeatView state, int perspective, string[] names, bool seek, ForgeSeatView otherView=null, ForgeSeatView[] recordedViews=null)
    {
        replayPlayerNames=names;
        if (seek) { ResetCastingVisual(); ResetZoneMotion(); ResetNotices(); lastNoticeId=(state.notices??Array.Empty<ForgeNotice>()).Select(n=>n.id).DefaultIfEmpty(0).Max(); }
        // Keep the recorded views immutable; hide interactive choices while preserving the board.
        var display = ServerSnapshotJson.Copy(state);
        // Legacy replays carry the opponent's permission list in their own
        // recorded view. Intersect with faces already public to this viewer;
        // private foretell/morph previews must never cross the perspective.
        foreach(var player in display.players??Array.Empty<ForgePlayerView>()){
            var source=player.seat==perspective?state:otherView?.seat==player.seat?otherView:recordedViews?.FirstOrDefault(v=>v?.seat==player.seat);
            if(player.seat!=perspective && source!=null){
                if(player.publicPlayableExile==null)
                    player.publicPlayableExile=VisibleReplaySources(display.players.SelectMany(p=>p.exileCards??Array.Empty<ForgeCardView>()),source.playableExile);
                if(player.publicPlayableGraveyard==null)
                    player.publicPlayableGraveyard=VisibleReplaySources(display.players.SelectMany(p=>p.graveyardCards??Array.Empty<ForgeCardView>()),source.playableGraveyard);
            }
            if(player.usableSourceIds!=null)continue;
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
        Node("PriorityPill").gameObject.SetActive(false);
        advanceButton.gameObject.SetActive(false); endTurnButton.gameObject.SetActive(false); undoManaButton.gameObject.SetActive(false);
        if (mulliganChoices != null) mulliganChoices.gameObject.SetActive(false);
        actionRoot.parent.parent.gameObject.SetActive(false);
    }
    private static ForgeCardView[] VisibleReplaySources(System.Collections.Generic.IEnumerable<ForgeCardView> visible,ForgeCardView[] permissions)
    {
        var allowed=(permissions??Array.Empty<ForgeCardView>()).Where(c=>c!=null&&!c.faceDown).Select(c=>c.id).ToHashSet();
        return visible.Where(c=>c!=null&&!c.faceDown&&allowed.Contains(c.id)).GroupBy(c=>c.id).Select(group=>group.First()).ToArray();
    }

}
