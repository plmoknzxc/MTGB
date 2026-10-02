using System;
using UnityEngine.SceneManagement;

public sealed partial class OnlineMatchService
{
    public OnlineMatchEndedMessage EndReview { get; private set; }
    public string EndReplayStatus { get; private set; }
    public bool EndReplayDecided => endReplayChoice.HasValue || IsSpectator || EndReview?.aborted == true;
    private bool? endReplayChoice;
    private MatchReplay endReplay;
    private bool endReplaySaved;
    private double endReplayDeadline;
    private readonly System.Collections.Generic.HashSet<string> declinedReviewReplays = new System.Collections.Generic.HashSet<string>();

    private void BeginEndReview(OnlineMatchEndedMessage ended)
    {
        EndReview = ended;
        endReplay = null;
        endReplaySaved = false;
        endReplayDeadline = UnityEngine.Time.realtimeSinceStartupAsDouble + 60;
        endReplayChoice = GamePreferences.AutoSaveReplays ? true : (bool?)null;
        EndReplayStatus = IsSpectator || ended?.aborted == true ? "" : GamePreferences.AutoSaveReplays ? "录像接收后自动保存" : "是否保存本局录像？";
    }

    private void ClearEndReview()
    {
        EndReview = null; endReplay = null; endReplayChoice = null; EndReplayStatus = "";
    }

    private bool ReceiveEndReviewReplay(MatchReplay replay)
    {
        if (declinedReviewReplays.Remove(replay.matchId)) return true;
        if (EndReview == null || EndReview.matchId != replay.matchId) return false;
        endReplay = replay;
        if (endReplayChoice.HasValue) DecideEndReplay(endReplayChoice.Value);
        return true;
    }

    public void DecideEndReplay(bool save)
    {
        if (endReplaySaved) return;
        if (save && EndReview != null) declinedReviewReplays.Remove(EndReview.matchId);
        if (save && endReplay == null && !expectedReplays.Contains(EndReview?.matchId ?? ""))
        { EndReplayStatus = "录像不可用，可以继续查看或离开"; endReplayChoice = false; return; }
        endReplayChoice = save;
        if (!save && EndReview != null && expectedReplays.Contains(EndReview.matchId)) declinedReviewReplays.Add(EndReview.matchId);
        EndReplayStatus = save ? "正在接收录像，完成后保存…" : "本局录像不保存";
        if (!save) { endReplay = null; return; }
        if (endReplay == null) return;
        try { ReplayRepository.Save(endReplay); EndReplayStatus = "录像已保存"; endReplaySaved = true; endReplay = null; }
        catch (Exception e) { endReplayChoice = null; EndReplayStatus = "保存失败：" + e.Message; }
    }

    private void TickEndReplay(double now)
    {
        if (EndReview == null || !expectedReplays.Contains(EndReview.matchId) || now < endReplayDeadline) return;
        expectedReplays.Remove(EndReview.matchId);
        if (receivingReplays.TryGetValue(EndReview.matchId, out var stream)) { stream.Dispose(); receivingReplays.Remove(EndReview.matchId); }
        endReplayChoice = false;
        EndReplayStatus = "录像接收超时，可以继续查看或离开";
    }

    public void LeaveEndReview(bool leaveRoom)
    {
        if (!EndReplayDecided) { EndReplayStatus = "请先选择保存录像或不保存"; return; }
        if (endReplayChoice == true && expectedReplays.Contains(EndReview.matchId))
        { EndReplayStatus = "正在接收录像，请稍候再离开"; return; }
        ClearEndReview();
        ForgeView = null;
        if (leaveRoom) Disconnect();
        SceneManager.LoadScene(MainSceneName);
    }
}
