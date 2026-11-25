using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class OnlineMatchService
{
    private sealed class PuzzleStep
    {
        public string kind, option, position, decisionPosition;
        public int source, selected;
        public int[] order;
        public long context;
        public bool automatic,decision,resolvingTarget,verified;
        public bool IsTarget => kind is "InputSelectTargets" or "SelectStackTarget";

        // Forge's wire boardKey includes Java map iteration order, which can
        // differ between workers. Hash the typed state and ordered choices.
        public static string Position(ForgeSeatView board, ForgeOption[] options) => ServerSnapshotJson.HashFields(new object[] {
            board.turn, board.turnSeat, board.prioritySeat, board.phase, board.dayTime,
            board.players, board.globalEffects, board.stackItems, options
        });

        // Forge may expose its own mana undo after a cancelled cast even when
        // the board is identical. That hidden priority shortcut is not a puzzle decision.
        public static string DecisionPosition(ForgeSeatView board,string exact)
        {
            var options=board.request.options;
            if(board.request.kind!="InputPassPriority" || !Array.Exists(options,IsManaUndo))return exact;
            return Position(board,options.Where(o=>!IsManaUndo(o)).ToArray());
        }

        internal static bool IsManaUndo(ForgeOption option) =>
            option?.id=="cancel" && !option.disabled && option.label?.StartsWith("Undo",StringComparison.OrdinalIgnoreCase)==true;

        public bool Matches(ForgeSeatView board) => board.request.kind==kind &&
            board.request.sourceCardId==source && board.request.selectedCount==selected &&
            Position(board,board.request.options)==position;
    }

    private readonly List<PuzzleStep> puzzleHistory=new();
    private OnlineMatchSession puzzleRestore;
    private int restoreIndex;
    private int verifiedDecision=-1;
    private int lastPuzzleDecision=-1;
    private int pendingPuzzleInput=-1, targetDraftStart=-1;
    private long targetPaymentContext=-1;
    internal bool AutomaticPuzzleChoice { get; set; }
    private float restoreDeadline;
    private float nextCheckpointHealthCheck;
    private string puzzleOpening;
    internal int PuzzleManaUndoIndex
    {
        get {
            if(lastPuzzleDecision<0)return -1;
            var board=ForgeView?.forge;
            if(board?.awaitingInput!=true || board.request?.kind!="InputPassPriority" || board.request.seat!=LocalSeat)return -1;
            var player=Array.Find(board.players,p=>p.seat==LocalSeat);
            if(player?.manaPool==null || !Array.Exists(player.manaPool,n=>n>0))return -1;
            return Array.FindIndex(board.request.options,PuzzleStep.IsManaUndo);
        }
    }
    internal bool CanUndoPuzzle => CurrentPuzzle!=null && IsInMatch && !EditingCurrentPuzzle.HasValue &&
        !PuzzleUndoPending && PuzzleUndoMessage==null && pendingPuzzleInput<0 &&
        lastPuzzleDecision>=0 && puzzleHistory[lastPuzzleDecision].verified && ForgeView?.forge?.awaitingInput==true;
    internal bool PuzzleUndoPending { get; private set; }
    internal int PuzzleTimelineRevision { get; private set; }
    internal string PuzzleUndoMessage { get; private set; }

    internal void UndoPuzzleStep()
    {
        if(!CanUndoPuzzle)return;
        if(verifiedDecision!=lastPuzzleDecision){
            // This exact step was checked on the same branch. Accept the click
            // while the replacement worker catches up, then verify it again.
            PuzzleUndoPending=true;
            NotifyInteractionStateChanged();
            return;
        }
        RestorePuzzleStep();
    }

    private void RestorePuzzleStep()
    {
        if(!CheckParkedPuzzle())return;
        PuzzleUndoPending=false;
        var previous=localAiSession;
        localAiSession=puzzleRestore;
        puzzleRestore=null;
        ForgeView=localAiSession.GetView(1);
        puzzleHistory.RemoveRange(verifiedDecision,puzzleHistory.Count-verifiedDecision);
        verifiedDecision=-1;
        lastPuzzleDecision=puzzleHistory.FindLastIndex(s=>s.decision);
        pendingPuzzleInput=targetDraftStart=-1;
        targetPaymentContext=-1;
        PuzzleTimelineRevision++;
        NotifyInteractionStateChanged();
        previous.Dispose();
    }

    private void ReleasePuzzleCheckpoint()
    {
        puzzleRestore?.Dispose();puzzleRestore=null;
        verifiedDecision=-1;
        if(PuzzleUndoPending){
            PuzzleUndoPending=false;
            NotifyInteractionStateChanged();
        }
    }

    private void RecordPuzzleInput(ForgeSeatView board,int index,int[] order)
    {
        var request=board.request;
        int target=lastPuzzleDecision>=0 && puzzleHistory[lastPuzzleDecision].IsTarget
            ? puzzleHistory[lastPuzzleDecision].source : -1;
        string option=request.options[index].id;
        string exact=PuzzleStep.Position(board,request.options);
        var step=new PuzzleStep {
            kind=request.kind, option=option, context=request.contextId, source=request.sourceCardId,
            selected=request.selectedCount, position=exact, decisionPosition=PuzzleStep.DecisionPosition(board,exact),
            automatic=AutomaticPuzzleChoice,
            order=order==null?null:(int[])order.Clone(),
            resolvingTarget=target>0 && ((request.contextId==targetPaymentContext && request.kind.Contains("PayMana") && option=="ok") ||
                (request.kind=="InputPassPriority" && option=="ok" && board.stackItems is {Length:>0} &&
                 board.stackItems[0].source?.id==target))
        };
        pendingPuzzleInput=puzzleHistory.Count;
        puzzleHistory.Add(step);
    }

    // Keep every raw input to reproduce Forge's IDs. Only completed player
    // decisions are offered as undo stops; an abandoned draft has none.
    private void CompletePuzzleInput(ForgeSeatView next)
    {
        if(pendingPuzzleInput<0 || next?.awaitingInput!=true || next.request==null)return;
        int index=pendingPuzzleInput;
        pendingPuzzleInput=-1;
        var step=puzzleHistory[index];
        targetPaymentContext=-1;
        if(step.option=="cancel"){
            string position=PuzzleStep.DecisionPosition(next,PuzzleStep.Position(next,next.request.options));
            for(int i=lastPuzzleDecision;i>=0;i--){
                var earlier=puzzleHistory[i];
                if(!earlier.decision)continue;
                if(earlier.kind==next.request.kind && earlier.source==next.request.sourceCardId &&
                    earlier.selected==next.request.selectedCount && earlier.decisionPosition==position){
                    for(int j=i;j<index;j++)puzzleHistory[j].decision=false;
                    lastPuzzleDecision=puzzleHistory.FindLastIndex(s=>s.decision);
                    if(lastPuzzleDecision<0 || restoreIndex>lastPuzzleDecision)ReleasePuzzleCheckpoint();
                    PuzzleUndoMessage=null;
                    targetDraftStart=-1;
                    return;
                }
                if(earlier.kind=="InputPassPriority")break;
            }
        }
        if(step.IsTarget){
            if(targetDraftStart<0)targetDraftStart=index;
            if(next.request.kind==step.kind && next.request.contextId==step.context &&
                next.request.sourceCardId==step.source)return;
            int start=targetDraftStart;
            targetDraftStart=-1;
            bool manual=false;
            for(int i=start;i<=index;i++)manual|=!puzzleHistory[i].automatic;
            if(!manual)return;
            index=start;
            if(next.request.kind.Contains("PayMana"))targetPaymentContext=next.request.contextId;
        }
        else if(step.automatic || step.resolvingTarget){
            if(step.resolvingTarget && step.kind.Contains("PayMana") && next.request.kind.Contains("PayMana"))
                targetPaymentContext=next.request.contextId;
            return;
        }
        puzzleHistory[index].decision=true;
        lastPuzzleDecision=index;
    }

    private void UpdatePuzzleUndo()
    {
        if(lastPuzzleDecision<0 || PuzzleUndoMessage!=null || ForgeView?.forge?.awaitingInput!=true)return;
        // The live engine can reverse this tap directly. Starting a second Forge
        // process on Unity's update thread would delay painting the ready controls.
        if(!PuzzleUndoPending && PuzzleManaUndoIndex>=0)return;
        if(verifiedDecision==lastPuzzleDecision)
        {
            if(Time.realtimeSinceStartup>=nextCheckpointHealthCheck)CheckParkedPuzzle();
            return;
        }
        try
        {
            if(puzzleRestore==null)
            {
                puzzleRestore=new OnlineMatchSession(matchId,new DeckDefinition(),new DeckDefinition(),CurrentPuzzle.seed,
                    LobbyState.config,"local-puzzle",1,2,true,puzzleOpening);
                restoreIndex=0;
                restoreDeadline=Time.realtimeSinceStartup+60;
            }
            AdvancePuzzleCheckpoint();
        }
        catch(Exception error)
        {
            ReleasePuzzleCheckpoint();
            PuzzleUndoMessage="这一段操作无法还原。";
            Debug.LogWarning("残局上一步不可用："+error.Message);
        }
    }

    private bool CheckParkedPuzzle()
    {
        nextCheckpointHealthCheck=Time.realtimeSinceStartup+2;
        puzzleRestore.Poll();
        if(puzzleRestore.Failure==null && !puzzleRestore.Outcome.isFinished)return true;
        ReleasePuzzleCheckpoint();
        PuzzleUndoMessage="这一段操作无法还原。";
        return false;
    }

    private void AdvancePuzzleCheckpoint()
    {
        // A parked worker is already at a verified human decision. It only
        // advances when the player makes another decision on the live board.
        if(verifiedDecision>=0)restoreDeadline=Time.realtimeSinceStartup+60;
        verifiedDecision=-1;
        puzzleRestore.Poll();
        if(puzzleRestore.Failure!=null || puzzleRestore.Outcome.isFinished || Time.realtimeSinceStartup>restoreDeadline)
            throw new InvalidOperationException(puzzleRestore.Failure??"还原未完成。");
        var snapshot=puzzleRestore.GetViewForTransport(1);
        var board=snapshot.forge;
        if(!board.awaitingInput || board.request==null)return;
        var step=puzzleHistory[restoreIndex];
        int index=Array.FindIndex(board.request.options,o=>o.id==step.option && !o.disabled);
        if(!step.Matches(board) || index<0)throw new InvalidOperationException("还原的可选操作与原局不一致。");
        step.verified=true;
        if(restoreIndex==lastPuzzleDecision)
        {
            verifiedDecision=restoreIndex;
            if(PuzzleUndoPending)RestorePuzzleStep();
            return;
        }
        var result=puzzleRestore.ApplyAction(new GameAction{type=GameActionType.ConfirmChoice,playerId=1,
            sourceObjectId=board.request.requestId,optionId=index,
            optionIds=step.order==null?new List<int>():new List<int>(step.order)});
        if(!result.accepted)throw new InvalidOperationException("无法重放先前操作。");
        restoreIndex++;restoreDeadline=Time.realtimeSinceStartup+60;
    }
}
