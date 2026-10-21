#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed partial class EndReviewValidation
{
    public static bool ReconnectCase;
    public static bool PauseAtFirstResult;
    static readonly List<string> lifecycleChecks=new();
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Field(object value,string name)=>value.GetType().GetField(name,Private).GetValue(value);
    static object Invoke(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Private).Invoke(value,args);
    static DeckDefinition Copy(DeckDefinition deck)=>JsonUtility.FromJson<DeckDefinition>(JsonUtility.ToJson(deck));
    static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;
        var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var input=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();
        EventSystem.current.RaycastAll(input,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Actual pointer reaches "+button.name);
        ExecuteEvents.Execute(button.gameObject,input,ExecuteEvents.pointerClickHandler);
    }
    IEnumerator ValidateSeriesReconnect(Probe second,Probe observer,DeckDefinition own,DeckDefinition other)
    {
        var registry=(OnlineRoomRegistry)Field(service,"roomRegistry");registry.TryGet("spectator-test",out var room);
        Check(room.series.betweenGames&&!room.series.finished&&room.series.gameNumber==2,"Actual first Forge game enters unfinished BO3 sideboarding");
        Check(!string.IsNullOrEmpty(OnlineClientProfileStore.Load(service.CurrentProfileId).reconnectToken),"Client retains room credential after real game result");
        while(PauseAtFirstResult)yield return null;
        service.DecideEndReplay(false);service.LeaveEndReview(true);
        Check(service.IsOnline&&service.EndReview!=null,"Result-screen room exit is rejected while BO3 is unfinished");
        service.LeaveEndReview(false);yield return Wait(()=>OnlineLobbyPanel.HasBattlefieldLobby);yield return null;yield return null;
        var panel=UnityEngine.Object.FindFirstObjectByType<OnlineLobbyPanel>();
        Check(panel!=null&&service.IsCompeting&&!service.CanLeaveRoom,"Sideboard page keeps participant competition identity");
        Check(!((Button)Field(panel,"leaveRoomButton")).interactable,"Leave-room button is disabled between games");
        Click(panel.GetComponentsInChildren<Button>(true).First(b=>b.name=="BackButton"));yield return null;
        Check(panel.gameObject.activeInHierarchy&&service.IsOnline,"Normal Back button cannot leave or disconnect an unfinished BO3");
        Invoke(panel,"LeaveRoom");
        Check(service.IsOnline&&service.LobbyState.series.betweenGames,"Direct/stale leave callback is also guarded");

        var changed=Copy(other);changed.cards[0].count=59;
        changed.cards.Add(new DeckEntry{setCode=other.sideboard[0].setCode,collectorNumber=other.sideboard[0].collectorNumber,count=1});
        changed.sideboard[0].count=3;changed.sideboard.Add(new DeckEntry{setCode=other.cards[0].setCode,collectorNumber=other.cards[0].collectorNumber,count=1});
        second.Send(OnlineProtocol.SubmitDeck,new OnlineDeckMessage{deck=changed});
        yield return Wait(()=>room.members[2].deck.cards.Count==2);
        second.Send(OnlineProtocol.SetReady,new OnlineReadyMessage{ready=true});yield return Wait(()=>room.members[2].ready);
        var reserved=room.members[2];string token=second.welcome.reconnectToken;
        reserved.requestGate.TryPrepare(41,2,new GameAction{type=GameActionType.Concede,playerId=2},out _); // nonzero sequence fixture; no native action is executed
        second.net.Shutdown();yield return Wait(()=>!reserved.connected);
        Check(room.members[2]==reserved&&reserved.reconnectWindow.IsActive,"Actual transport disconnect reserves the same player object with a reconnect deadline");
        Check(reserved.deck.cards.Count==2&&reserved.registeredDeck.sideboard.Sum(e=>e.count)==4,"Submitted sideboard changes and original pool survive disconnect");
        var stranger=Connect("新观众","join");yield return Wait(()=>stranger.welcome!=null);
        Check(stranger.welcome.seat>2&&room.members[2]==reserved,"Fresh join cannot steal the reserved player slot");
        var wrong=Connect("错误身份","join",Guid.NewGuid().ToString("N"));yield return Wait(()=>wrong.error!=null);
        Check(wrong.welcome==null,"Invalid recovery credential is rejected");
        var resumed=Connect("原选手","join",token);yield return Wait(()=>resumed.welcome!=null);
        Check(resumed.welcome.seat==2&&room.members[2]==reserved&&reserved.connected,"Authenticated rejoin restores original player 2 identity");
        Check(resumed.welcome.lastRequestId==41,"Rejoin welcome carries the member's existing nonzero action sequence");
        Check(!resumed.welcome.resumingMatch&&resumed.welcome.ownDeck?.cards.Count==2,"Lobby rejoin restores its private submitted deck without pretending a native game is running");
        Check((observer.welcome.ownDeck?.cards?.Count??0)==0&&(stranger.welcome.ownDeck?.cards?.Count??0)==0
            &&(observer.welcome.ownDeck?.sideboard?.Count??0)==0&&(stranger.welcome.ownDeck?.sideboard?.Count??0)==0,"Spectator welcomes contain no private deck entries (Unity may instantiate empty null objects)");
        Check(room.series.gameNumber==2&&room.series.player2Wins==1&&room.series.player1Wins==0,"Reconnect does not reset BO3 game index or score");
        service.SubmitDeck(own);service.SetReady(true);
        yield return Wait(()=>service.LobbyState.playDrawChooserSeat>0||service.IsInMatch,20);
        if(!service.IsInMatch){if(service.LobbyState.playDrawChooserSeat==1)service.ChoosePlayDraw(true);else resumed.Send(OnlineProtocol.ChoosePlayDraw,new OnlinePlayDrawMessage{firstPlayerSeat=1});}
        yield return Wait(()=>service.IsInMatch&&service.ForgeView?.forge?.players?.Length==2&&resumed.view!=null,65);
        Check(service.EndReview==null&&service.ForgeView.matchId==resumed.view.matchId&&room.series.gameNumber==2,"Both players enter the actual next Forge game after reconnect");
        yield return Wait(()=>service.InteractionState==OnlineClientInteractionState.Idle,15);
        resumed.Send(OnlineProtocol.SubmitAction,new OnlineActionMessage{matchId=resumed.view.matchId,requestId=resumed.welcome.lastRequestId+1,action=new GameAction{type=GameActionType.Concede,playerId=2}});
        yield return Wait(()=>service.EndReview!=null,20);
        Check(room.series.betweenGames&&room.series.gameNumber==3&&room.series.player1Wins==1&&room.series.player2Wins==1,"Real second concession advances to deciding game with correct 1-1 score");
        resumed.net.Shutdown();yield return Wait(()=>!reserved.connected);
        reserved.reconnectWindow.Begin(Time.realtimeSinceStartupAsDouble-31); // deterministic deadline boundary, not a 30-second wall-clock claim
        yield return Wait(()=>room.series.finished);
        Check(!room.series.betweenGames&&room.series.winnerSeat==1&&!room.members.ContainsKey(2),"Expired between-game recovery finishes the series and releases the absent player");
        Check(service.CanLeaveRoom&&service.EndReview.series.finished,"Remaining player's result and exit policy follow authoritative series completion");
        service.DecideEndReplay(false);service.LeaveEndReview(true);yield return null;
        Check(!service.IsOnline,"Completed BO3 permits normal room exit");
        File.WriteAllText(Output,"PASS: actual Forge games, sideboard submission, real transport disconnect/rejoin, next game, deterministic expiry and actual Back-button raycast; "+lifecycleChecks.Count+" checks");
    }
}
#endif
