using System;
using UnityEngine.SceneManagement;

public sealed partial class OnlineMatchService
{
    public OnlineMatchEndedMessage EndReview { get; private set; }
    public string EndReplayStatus { get; private set; }
    public bool EndReplayDecided => endReplayChoice.HasValue || IsSpectator || EndReview?.aborted == true;
    private bool? endReplayChoice;
    private MatchReplay endReplay;
    private byte[] endReplayBytes;
    private readonly System.Collections.Generic.Dictionary<string,bool> replaySaveChoices=new();
    private readonly System.Collections.Generic.HashSet<string> savingReplays=new();
    private bool endReplaySaved;
    private readonly System.Collections.Generic.HashSet<string> declinedReviewReplays = new System.Collections.Generic.HashSet<string>();

    private void BeginEndReview(OnlineMatchEndedMessage ended)
    {
        EndReview = ended;
        endReplay = null;endReplayBytes=null;
        endReplaySaved = false;
        endReplayChoice = GamePreferences.AutoSaveReplays ? true : (bool?)null;
        EndReplayStatus = IsSpectator || ended?.aborted == true ? "" : GamePreferences.AutoSaveReplays ? "录像接收后自动保存" : "是否保存本局录像？";
    }

    private void ClearEndReview()
    {
        EndReview = null; endReplay = null;endReplayBytes=null; endReplayChoice = null; EndReplayStatus = "";
    }

    private bool ReceiveEndReviewReplay(MatchReplay replay,byte[] bytes=null)
    {
        if(replaySaveChoices.TryGetValue(replay.matchId,out bool save)) {
            replaySaveChoices.Remove(replay.matchId);
            declinedReviewReplays.Remove(replay.matchId);
            if(save)StartCoroutine(SaveReceivedReplay(replay,bytes));
            return true;
        }
        if (declinedReviewReplays.Remove(replay.matchId)) return true;
        if (EndReview == null || EndReview.matchId != replay.matchId) return false;
        endReplay = replay;endReplayBytes=bytes;
        if (endReplayChoice.HasValue) DecideEndReplay(endReplayChoice.Value);
        return true;
    }

    public void DecideEndReplay(bool save)
    {
        if (endReplaySaved) return;
        if (save && EndReview != null) declinedReviewReplays.Remove(EndReview.matchId);
        if (save && endReplay == null && !expectedReplays.Contains(EndReview?.matchId ?? "")&& !decodingReplays.Contains(EndReview?.matchId??"")&&!savingReplays.Contains(EndReview?.matchId??""))
        { EndReplayStatus = "录像不可用，可以继续查看或离开"; endReplayChoice = false; return; }
        endReplayChoice = save;
        if(EndReview!=null)replaySaveChoices[EndReview.matchId]=save;
        if (!save && EndReview != null && expectedReplays.Contains(EndReview.matchId)) declinedReviewReplays.Add(EndReview.matchId);
        EndReplayStatus = save ? "正在接收录像，完成后保存…" : "本局录像不保存";
        if (!save) { endReplay = null;endReplayBytes=null; return; }
        if (endReplay == null) return;
        var ready=endReplay;var bytes=endReplayBytes;endReplay=null;endReplayBytes=null;
        replaySaveChoices.Remove(ready.matchId);StartCoroutine(SaveReceivedReplay(ready,bytes));
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
        if (!EndReplayDecided) { EndReplayStatus = "请先选择保存录像或不保存"; return; }
        if(EndReview!=null&&endReplayChoice.HasValue)replaySaveChoices[EndReview.matchId]=endReplayChoice.Value;
        if(!leaveRoom)SendToServer<object>(OnlineProtocol.LeaveReview,null);
        ClearEndReview();
        ForgeView = null;
        if (leaveRoom) Disconnect();
        if(!leaveRoom && !IsSpectator && LobbyState?.series?.betweenGames==true && SceneManager.GetActiveScene().name==ForgeBattlefieldController.SceneName)
        { OnlineLobbyPanel.OpenSideboarding();return; }
        SceneManager.LoadScene(MainSceneName);
    }
    private System.Collections.IEnumerator SaveReceivedReplay(MatchReplay replay,byte[] bytes)
    {
        if(!savingReplays.Add(replay.matchId))yield break;
        string directory=ReplayRepository.Root;
        var task=System.Threading.Tasks.Task.Run(()=>ReplayRepository.SaveReceived(bytes??ReplayRepository.Encode(replay),replay,directory));
        while(!task.IsCompleted)yield return null;
        savingReplays.Remove(replay.matchId);
        replaySaveChoices.Remove(replay.matchId);
        if(task.IsFaulted){string reason="录像保存失败："+task.Exception.GetBaseException().Message;UnityEngine.Debug.LogWarning(reason);if(EndReview?.matchId==replay.matchId){endReplayChoice=null;EndReplayStatus=reason;}yield break;}
        ReplayRepository.NotifyChanged();
        if(EndReview?.matchId==replay.matchId){EndReplayStatus="录像已保存";endReplaySaved=true;}
    }
}
