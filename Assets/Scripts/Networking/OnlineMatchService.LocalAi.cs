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
    public bool IsLocalAiMatch { get; private set; }

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
        string playerName=OnlineClientProfileStore.Load(currentProfileId).displayName;
        if(string.IsNullOrWhiteSpace(playerName))playerName="Player 1";
        string id=Guid.NewGuid().ToString("N");
        var session=new OnlineMatchSession(id,CloneDeck(own),CloneDeck(opponent),UnityEngine.Random.Range(1,int.MaxValue),config,"local-ai",1,2,true);
        localAiSession=session;IsLocalAiMatch=true;IsInMatch=true;IsPreparingMatch=false;
        LocalSeat=1;matchId=id;currentRoomId="本地 AI";clientRegisteredDeck=CloneDeck(own);
        LobbyState=new OnlineLobbyState{roomId="local-ai",config=config,players=new List<OnlineLobbyPlayer>{
            new(){seat=1,displayName=playerName,connected=true,hasDeck=true,ready=true},
            new(){seat=2,displayName="Forge AI",connected=true,hasDeck=true,ready=true}}};
        OnlinePlayerContext.SetLocalPlayer(1);actionGate.SetConnected(true);ForgeView=session.GetView(1);NotifyInteractionStateChanged();
        ForgeBattlefieldController.Open(this);
    }

    private void SubmitLocalAiChoice(int option,int[] order,bool concede)
    {
        if(localAiSession==null||!IsInMatch)return;
        var request=ForgeView?.forge?.awaitingInput==true?ForgeView.forge.request:null;
        var result=localAiSession.ApplyAction(new GameAction{type=concede?GameActionType.Concede:GameActionType.ConfirmChoice,
            playerId=1,sourceObjectId=request?.requestId??0,optionId=option,
            optionIds=order==null?new List<int>():new List<int>(order)});
        if(!result.accepted)SetStatus("当前操作不可用，请等待最新对局状态。");
        ForgeView=localAiSession.GetView(1);NotifyInteractionStateChanged();
    }

    private void UpdateLocalAiMatch()
    {
        if(localAiSession==null||!IsInMatch)return;
        bool changed=localAiSession.Poll();
        if(changed)ForgeView=localAiSession.GetView(1);
        bool failed=!string.IsNullOrEmpty(localAiSession.Failure);
        if(!failed&&!localAiSession.Outcome.isFinished)return;
        var ended=new OnlineMatchEndedMessage{matchId=localAiSession.MatchId,aborted=failed,
            winnerSeat=failed?0:localAiSession.Outcome.winnerPlayerId,
            reason=failed?localAiSession.Failure:localAiSession.Outcome.message,finalView=ForgeView,ownDeck=CloneDeck(clientRegisteredDeck)};
        IsInMatch=false;BeginEndReview(ended);actionGate.SetConnected(false);NotifyInteractionStateChanged();
        if(!failed){
            try{var recording=localAiSession.CompleteReplay(new[]{LobbyState.players[0].displayName,"Forge AI"},1,1,ended.winnerSeat,ended.reason);
                StartCoroutine(ReceiveLocalAiRecording(recording));}
            catch(Exception error){EndReplayStatus="录像生成失败："+error.Message;endReplayChoice=false;}
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
        if(task.IsFaulted){if(EndReview?.matchId==id){EndReplayStatus="录像生成失败："+task.Exception.GetBaseException().Message;endReplayChoice=false;}yield break;}
        try{ReceiveEndReviewReplay(ReplayRepository.ReadReceivedSummary(task.Result),task.Result);}
        catch(Exception error){if(EndReview?.matchId==id){EndReplayStatus="录像读取失败："+error.Message;endReplayChoice=false;}}
    }

    private void LeaveLocalAiReview()
    {
        if(!EndReplayDecided){EndReplayStatus="请先选择保存录像或不保存";return;}
        if(EndReview!=null&&endReplayChoice.HasValue)replaySaveChoices[EndReview.matchId]=endReplayChoice.Value;
        StopLocalAiMatch();ClearEndReview();ForgeView=null;LobbyState=null;LocalSeat=0;matchId="";
        actionGate.SetConnected(false);SceneManager.LoadScene(MainSceneName);
    }

    private void StopLocalAiMatch()
    {
        localAiSession?.Dispose();localAiSession=null;IsLocalAiMatch=false;
    }
}
