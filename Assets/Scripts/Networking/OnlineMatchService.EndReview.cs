using System;
using UnityEngine.SceneManagement;

public sealed partial class OnlineMatchService
{
    public OnlineMatchEndedMessage EndReview { get; private set; }
    public string EndReplayStatus { get; private set; }
    public bool EndReplayDecided => endReplayChoice.HasValue || IsSpectator || EndReview?.aborted == true;
    public enum ReplayChoiceState { Undecided, Waiting, Saving, Saved, Skipped, Unavailable, Failed }
    public ReplayChoiceState ReplayChoice { get; private set; }
    internal bool OffersEndReplay => EndReview!=null && !IsSpectator && !EndReview.aborted &&
        (CurrentPuzzle==null || EndReview.winnerSeat==1);
    internal bool CanRequestEndReplay => EndReview!=null && (endReplay!=null || expectedReplays.Contains(EndReview.matchId) || decodingReplays.Contains(EndReview.matchId));
    private bool? endReplayChoice;
    private MatchReplay endReplay;
    private byte[] endReplayBytes;
    private readonly System.Collections.Generic.Dictionary<string,bool> replaySaveChoices=new();
    private readonly System.Collections.Generic.HashSet<string> savingReplays=new();
    private Action leaveAfterReplaySave;

    private void BeginEndReview(OnlineMatchEndedMessage ended)
    {
        ClearEndReview();
        EndReview = ended;
        endReplayChoice = OffersEndReplay && GamePreferences.AutoSaveReplays ? true : (bool?)null;
        ReplayChoice = endReplayChoice==true ? ReplayChoiceState.Waiting : ReplayChoiceState.Undecided;
        EndReplayStatus = !OffersEndReplay ? "" : endReplayChoice==true ? "录像接收后自动保存" : "是否保存本局录像？";
    }

    private void ClearEndReview()
    {
        // Chunks can arrive after returning to the lobby or starting the next game.
        // Keep only decisions that still have a transfer in flight.
        if(OffersEndReplay && (expectedReplays.Contains(EndReview.matchId)||decodingReplays.Contains(EndReview.matchId)))
            replaySaveChoices[EndReview.matchId]=endReplayChoice==true;
        leaveAfterReplaySave=null;
        EndReview = null; endReplay = null;endReplayBytes=null; endReplayChoice = null; EndReplayStatus = "";
        ReplayChoice = ReplayChoiceState.Undecided;
    }

    private bool ReceiveEndReviewReplay(MatchReplay replay,byte[] bytes=null)
    {
        if(replaySaveChoices.TryGetValue(replay.matchId,out bool save)) {
            replaySaveChoices.Remove(replay.matchId);
            if(save)StartCoroutine(SaveReceivedReplay(replay,bytes));
            return true;
        }
        if (EndReview == null || EndReview.matchId != replay.matchId) return false;
        endReplay = replay;endReplayBytes=bytes;
        if (endReplayChoice==true) DecideEndReplay(true);
        return true;
    }

    private void MarkEndReplayUnavailable(string message)
    {
        ReplayChoice=ReplayChoiceState.Unavailable;
        leaveAfterReplaySave=null;endReplayChoice=false;EndReplayStatus=message;
    }

    public void DecideEndReplay(bool save)
    {
        if (!OffersEndReplay || ReplayChoice==ReplayChoiceState.Saving || ReplayChoice==ReplayChoiceState.Saved) return;
        if (save && !CanRequestEndReplay)
        { MarkEndReplayUnavailable("录像不可用，可以继续查看或离开"); return; }
        endReplayChoice = save;
        EndReplayStatus = save ? "正在接收录像，完成后保存…" : "本局录像不保存";
        ReplayChoice=save ? ReplayChoiceState.Waiting : ReplayChoiceState.Skipped;
        if (!save) { FinishReplayDeparture();return; }
        if (endReplay == null) return;
        var ready=endReplay;var bytes=endReplayBytes;endReplay=null;endReplayBytes=null;
        replaySaveChoices.Remove(ready.matchId);StartCoroutine(SaveReceivedReplay(ready,bytes));
    }

    private bool WaitForChosenSave(Action leave)
    {
        if(ReplayChoice is not (ReplayChoiceState.Waiting or ReplayChoiceState.Saving))return false;
        leaveAfterReplaySave=leave;
        return true;
    }

    private void FinishReplayDeparture()
    {
        var leave=leaveAfterReplaySave;
        leaveAfterReplaySave=null;
        leave?.Invoke();
    }

    private void TickEndReplay(double now)
    {
        // A player may already be in sideboarding when a stalled transfer expires.
        var expired=new System.Collections.Generic.List<string>();
        foreach(var pair in replayReceiveDeadlines)if(now>=pair.Value)expired.Add(pair.Key);
        foreach(string id in expired)FailReplay(id,"录像接收超时");
    }

    public void LeaveEndReview(bool leaveRoom)
    {
        if(EndReview==null)return;
        if(!IsLocalAiMatch&&leaveRoom&&!CanLeaveRoom){EndReplayStatus="本场比赛尚未结束，请前往换备继续比赛。";return;}
        if(WaitForChosenSave(()=>LeaveEndReview(leaveRoom)))return;
        if(IsLocalAiMatch){LeaveLocalAiReview();return;}
        if(!leaveRoom)SendToServer<object>(OnlineProtocol.LeaveReview,null);
        ClearEndReview();
        ForgeView = null;
        if (leaveRoom) Disconnect();
        if(!leaveRoom && !IsSpectator && LobbyState?.series?.betweenGames==true && ForgeBattlefieldController.IsBattlefieldSceneActive)
        { OnlineLobbyPanel.OpenSideboarding();return; }
        SceneManager.LoadScene(MainSceneName);
    }
    private System.Collections.IEnumerator SaveReceivedReplay(MatchReplay replay,byte[] bytes)
    {
        if(!savingReplays.Add(replay.matchId))yield break;
        if(EndReview?.matchId==replay.matchId)ReplayChoice=ReplayChoiceState.Saving;
        string directory=ReplayRepository.Root;
        var task=System.Threading.Tasks.Task.Run(()=>ReplayRepository.SaveReceived(bytes??ReplayRepository.Encode(replay),replay,directory));
        while(!task.IsCompleted)yield return null;
        savingReplays.Remove(replay.matchId);
        replaySaveChoices.Remove(replay.matchId);
        if(task.IsFaulted){string reason="录像保存失败："+task.Exception.GetBaseException().Message;UnityEngine.Debug.LogWarning(reason);if(EndReview?.matchId==replay.matchId){leaveAfterReplaySave=null;endReplay=replay;endReplayBytes=bytes;ReplayChoice=ReplayChoiceState.Failed;endReplayChoice=null;EndReplayStatus=reason;}yield break;}
        ReplayRepository.NotifyChanged();
        if(EndReview?.matchId==replay.matchId){EndReplayStatus="录像已保存";ReplayChoice=ReplayChoiceState.Saved;FinishReplayDeparture();}
    }
}
