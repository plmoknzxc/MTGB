using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class OnlineMatchService
{
    private OnlineMatchSession localAiSession;
    private OnlineMatchSession preparedPuzzle;
    private string preparedOpening;
    private Coroutine preparingPuzzle;
    public bool IsLocalAiMatch { get; private set; }
    public LocalPuzzle CurrentPuzzle { get; private set; }
    internal bool? EditingCurrentPuzzle { get; private set; }
    private LocalPuzzle revisedPuzzle;
    internal LocalPuzzle PuzzleEditorSource => revisedPuzzle ?? CurrentPuzzle;
    internal static LocalPuzzle ReturnPuzzle { get; private set; }
    internal static bool RetryPuzzleOnReturn { get; private set; }

    public void StartLocalPuzzle(LocalPuzzle puzzle)
    {
        if (IsOnline || IsDisconnecting || IsInMatch || EndReview != null)
            throw new InvalidOperationException("请先退出当前对局或房间。");
        var setup = puzzle.Copy();
        string state = setup.EngineSetup();
        LaunchPuzzle(setup,state);
    }

    private static OnlineRoomConfig PuzzleConfig(LocalPuzzle puzzle) => new OnlineRoomConfig {
        formatId="none", bestOf=1, startingLife=20, firstPlayerSeat=puzzle.firstSide+1
    };

    internal void PreparePuzzleOpening(LocalPuzzle puzzle, GameObject page)
    {
        DiscardPreparedPuzzle();
        preparingPuzzle=StartCoroutine(PrepareWhileVisible());

        IEnumerator PrepareWhileVisible()
        {
            yield return new WaitForSecondsRealtime(.3f);
            if(page!=null && page.activeInHierarchy && !IsInMatch && !IsOnline){
                try{
                    preparedOpening=puzzle.EngineSetup();
                    preparedPuzzle=new OnlineMatchSession(null,new DeckDefinition(),new DeckDefinition(),puzzle.seed,
                        PuzzleConfig(puzzle),"local-puzzle",1,2,true,preparedOpening);
                }
                catch(Exception error){ Debug.LogWarning("残局预载未完成："+error.Message); }
                while(page!=null && page.activeInHierarchy && preparedPuzzle!=null)yield return null;
            }
            preparedPuzzle?.Dispose();preparedPuzzle=null;preparedOpening=null;preparingPuzzle=null;
        }
    }

    private void DiscardPreparedPuzzle()
    {
        if(preparingPuzzle!=null){StopCoroutine(preparingPuzzle);preparingPuzzle=null;}
        preparedPuzzle?.Dispose();preparedPuzzle=null;preparedOpening=null;
    }

    private void LaunchPuzzle(LocalPuzzle setup,string state,bool reuseField=false)
    {
        OnlineMatchSession opening=null;
        if(preparedPuzzle!=null && preparedOpening==state && preparedPuzzle.RandomSeed==setup.seed){
            opening=preparedPuzzle;preparedPuzzle=null;
            opening.BeginPreparedPuzzle();
        }
        DiscardPreparedPuzzle();
        LaunchLocalGame(new DeckDefinition(), new DeckDefinition(), PuzzleConfig(setup), setup.seed, setup, state,opening,reuseField);
    }

    public void StartLocalAiMatch(DeckDefinition own,DeckDefinition opponent,bool playFirst=true,OnlineRoomConfig settings=null)
    {
        if(IsOnline||IsDisconnecting||IsInMatch||EndReview!=null)
            throw new InvalidOperationException("请先结束当前对局或退出联机房间。");
        if(own==null||opponent==null||own.cards==null||opponent.cards==null||own.cards.Count==0||opponent.cards.Count==0)
            throw new ArgumentException("双方都需要选择已保存的非空卡组。");
        if(!File.Exists(Path.Combine(ForgeCardCatalog.Root,"classes/mtgb/bridge/ForgeBridge$BridgeAiPlayer.class")))
            throw new InvalidOperationException("本地 AI 运行资源尚未更新。");
        ClearEndReview();
        string format=settings?.formatId??own.format;
        int life=settings?.startingLife??(format=="brawl"?25:20);
        if(Array.IndexOf(DeckFormatLegality.OnlineKeys,format)<0)throw new ArgumentException("请选择支持的本地赛制。");
        if(life<1||life>200)throw new ArgumentException("初始生命必须在1至200之间。");
        int first=settings?.firstPlayerSeat??(playFirst?1:2);
        if(first<0||first>2)throw new ArgumentException("无效的先手设置。");
        var config=new OnlineRoomConfig{formatId=format,startingLife=life,firstPlayerSeat=first==0?UnityEngine.Random.Range(1,3):first,
            bestOf=1,noShuffle=settings?.noShuffle==true};
        if(!ValidateDeck(own,config,out string ownError))throw new ArgumentException("你的卡组："+ownError);
        if(!ValidateDeck(opponent,config,out string aiError))throw new ArgumentException("AI 卡组："+aiError);
        LaunchLocalGame(own, opponent, config, UnityEngine.Random.Range(1,int.MaxValue), null, null);
    }

    private void LaunchLocalGame(DeckDefinition own, DeckDefinition opponent, OnlineRoomConfig config, int seed, LocalPuzzle puzzle, string puzzleState,
        OnlineMatchSession opening=null,bool reuseField=false)
    {
        string playerName=OnlineClientProfileStore.Load(currentProfileId).displayName;
        if(string.IsNullOrWhiteSpace(playerName))playerName=OnlineClientProfileStore.DefaultDisplayName(currentProfileId);
        var session=opening??new OnlineMatchSession(null,CloneDeck(own),CloneDeck(opponent),seed,config,puzzle == null ? "local-ai" : "local-puzzle",1,2,true,puzzleState);
        string id=session.MatchId;
        CurrentPuzzle=puzzle;
        puzzleOpening=puzzleState;
        localAiSession=session;IsLocalAiMatch=true;IsInMatch=true;IsPreparingMatch=false;
        LocalSeat=1;matchId=id;currentRoomId="本地 AI";clientRegisteredDeck=CloneDeck(own);
        LobbyState=new OnlineLobbyState{roomId="local-ai",config=config,players=new List<OnlineLobbyPlayer>{
            new(){seat=1,displayName=playerName,connected=true,hasDeck=true,ready=true},
            new(){seat=2,displayName="Forge AI",connected=true,hasDeck=true,ready=true}}};
        OnlinePlayerContext.SetLocalPlayer(1);actionGate.SetConnected(true);ForgeView=session.GetView(1);NotifyInteractionStateChanged();
        if(puzzle!=null)PuzzleSceneTransition.Show(PuzzleSceneTransition.Destination.Battlefield,"正在载入残局…");
        if(reuseField)PuzzleTimelineRevision++;
        ForgeBattlefieldController.Open(this,reload:puzzle!=null && !reuseField && ForgeBattlefieldController.IsBattlefieldSceneActive);
    }

    private void SubmitLocalAiChoice(int option,int[] order,bool concede)
    {
        if(localAiSession==null||!IsInMatch||EditingCurrentPuzzle.HasValue||PuzzleUndoPending)return;
        var board=ForgeView?.forge;
        var request=board?.awaitingInput==true?board.request:null;
        var result=localAiSession.ApplyAction(new GameAction{type=concede?GameActionType.Concede:GameActionType.ConfirmChoice,
            playerId=1,sourceObjectId=request?.requestId??0,optionId=option,
            optionIds=order==null?new List<int>():new List<int>(order)});
        if(!result.accepted)SetStatus("当前操作不可用，请等待最新对局状态。");
        else if(CurrentPuzzle!=null && !concede && request!=null)RecordPuzzleInput(board,option,order);
        ForgeView=localAiSession.GetView(1);NotifyInteractionStateChanged();
    }

    private void UpdateLocalAiMatch()
    {
        if(localAiSession==null||!IsInMatch||EditingCurrentPuzzle.HasValue)return;
        bool changed=localAiSession.Poll();
        if(changed){
            ForgeView=localAiSession.GetView(1);
            if(CurrentPuzzle!=null)CompletePuzzleInput(ForgeView.forge);
        }
        bool failed=!string.IsNullOrEmpty(localAiSession.Failure);
        if(!failed&&!localAiSession.Outcome.isFinished){
            if(CurrentPuzzle!=null)UpdatePuzzleUndo();
            return;
        }
        ReleasePuzzleCheckpoint();
        var ended=new OnlineMatchEndedMessage{matchId=localAiSession.MatchId,aborted=failed,
            winnerSeat=failed?0:localAiSession.Outcome.winnerPlayerId,
            reason=failed?localAiSession.Failure:localAiSession.Outcome.message,finalView=ForgeView,ownDeck=CloneDeck(clientRegisteredDeck)};
        IsInMatch=false;BeginEndReview(ended);actionGate.SetConnected(false);NotifyInteractionStateChanged();
        if(OffersEndReplay){
            try{var recording=localAiSession.CompleteReplay(new[]{LobbyState.players[0].displayName,"Forge AI"},1,1,ended.winnerSeat,ended.reason);
                StartCoroutine(ReceiveLocalAiRecording(recording));}
            catch(Exception error){MarkEndReplayUnavailable("录像生成失败："+error.Message);}
        }
        localAiSession.Dispose();localAiSession=null;
        MatchEnded?.Invoke(ended);
    }

    private IEnumerator ReceiveLocalAiRecording(RecordedMatchReplay recording)
    {
        string id=recording.Metadata.matchId;expectedReplays.Add(id);
        var task=Task.Run(()=>{using(recording)return recording.Encode();});
        while(!task.IsCompleted)yield return null;
        expectedReplays.Remove(id);
        if(task.IsFaulted){replaySaveChoices.Remove(id);if(EndReview?.matchId==id)MarkEndReplayUnavailable("录像生成失败："+task.Exception.GetBaseException().Message);yield break;}
        try{ReceiveEndReviewReplay(ReplayRepository.ReadReceivedSummary(task.Result),task.Result);}
        catch(Exception error){if(EndReview?.matchId==id)MarkEndReplayUnavailable("录像读取失败："+error.Message);}
    }

    private void LeaveLocalAiReview()
    {
        if (CurrentPuzzle != null) { LeavePuzzle(false); return; }
        StopLocalAiMatch();ClearEndReview();ForgeView=null;LobbyState=null;LocalSeat=0;matchId="";
        actionGate.SetConnected(false);SceneManager.LoadScene(MainSceneName);
    }

    private void StopLocalAiMatch()
    {
        ReleasePuzzleCheckpoint();puzzleHistory.Clear();lastPuzzleDecision=pendingPuzzleInput=targetDraftStart=-1;
        targetPaymentContext=-1;puzzleOpening=null;PuzzleUndoMessage=null;
        localAiSession?.Dispose();localAiSession=null;IsLocalAiMatch=false;CurrentPuzzle=null;
        revisedPuzzle=null;EditingCurrentPuzzle=null;
    }

    internal void OpenPuzzleEditorFromMatch(bool modify)
    {
        if (CurrentPuzzle == null || EditingCurrentPuzzle.HasValue || (!IsInMatch && EndReview == null)) return;
        // Editing replaces a pending navigation choice, but the replay save itself continues.
        leaveAfterReplaySave = null;
        EditingCurrentPuzzle = modify;
        ReleasePuzzleCheckpoint();
        localAiSession?.SetPuzzleEditorPaused(true);
        PuzzleSceneTransition.Show(PuzzleSceneTransition.Destination.Menu,"正在打开残局编辑器…");
        SceneManager.LoadScene(MainSceneName);
    }

    internal void ReturnFromPuzzleEditor(LocalPuzzle saved = null, bool replayPuzzle = false)
    {
        if (EditingCurrentPuzzle is not bool modify) return;
        if (saved != null && (modify || replayPuzzle)) revisedPuzzle = saved.Copy();
        EditingCurrentPuzzle = null;
        PuzzleSceneTransition.Show(PuzzleSceneTransition.Destination.Battlefield,"正在返回残局…");
        if (replayPuzzle)
        {
            // Keep the result visible if a previously requested replay is still saving.
            if (WaitForChosenSave(() => LeavePuzzle(true))) ForgeBattlefieldController.Open(this);
            else LeavePuzzle(true);
            return;
        }
        localAiSession?.SetPuzzleEditorPaused(false);
        ForgeBattlefieldController.Open(this);
    }

    public void LeavePuzzle(bool retry)
    {
        if (CurrentPuzzle == null) return;
        if(WaitForChosenSave(()=>LeavePuzzle(retry)))return;
        if(retry)
        {
            var setup=PuzzleEditorSource.Copy();
            try
            {
                bool reuseField=revisedPuzzle==null && ForgeBattlefieldController.IsBattlefieldSceneActive;
                string state=revisedPuzzle==null ? puzzleOpening : setup.EngineSetup();
                ClearEndReview();StopLocalAiMatch();
                LaunchPuzzle(setup,state,reuseField);
                return;
            }
            catch(Exception error)
            {
                // Reopen the library if the edited setup or the worker cannot start.
                ReturnPuzzle=setup;
                Debug.LogWarning("残局重开失败："+error.Message);
            }
        }
        else ReturnPuzzle = PuzzleEditorSource;
        RetryPuzzleOnReturn = retry;
        PuzzleSceneTransition.Show(PuzzleSceneTransition.Destination.Menu,retry?"正在重新开始…":"正在返回残局列表…");
        DiscardPreparedPuzzle();ClearEndReview(); StopLocalAiMatch();
        IsInMatch = false; IsPreparingMatch = false; ForgeView = null; LobbyState = null; LocalSeat = 0; matchId = "";
        actionGate.SetConnected(false);
        SceneManager.LoadScene(MainSceneName);
    }

    internal static LocalPuzzle TakeReturningPuzzle(out bool retry)
    {
        var puzzle = ReturnPuzzle; retry = RetryPuzzleOnReturn;
        ReturnPuzzle = null; RetryPuzzleOnReturn = false;
        return puzzle;
    }
}
