using System.Linq;

public sealed partial class OnlineMatchService
{
    public bool IsCompeting => (LocalSeat == 1 || LocalSeat == 2)
        && (IsInMatch || LobbyState?.series?.betweenGames == true && !LobbyState.series.finished);
    public bool CanLeaveRoom => !IsCompeting;

    public bool TryLeaveRoom()
    {
        if(!CanLeaveRoom) {
            SetStatus("本场比赛尚未结束，请完成比赛；连接中断时会保留身份并尝试重连。");
            return false;
        }
        Disconnect();return true;
    }

    private void EndDisconnectedSeries(OnlineRoomState room,int winner,string reason)
    {
        room.series.Forfeit(winner,reason);
        room.playDrawChooserSeat=0;
        foreach(var member in room.members.Values)member.ready=member.isAi;
        foreach(var member in room.members.Values.Where(m=>!m.connected).ToArray())room.members.Remove(member.seat);
        RemoveOrphanedAi(room);
        SetRoomStatus(room,reason);
        BroadcastLobby(room);
        roomRegistry.RemoveIfEmpty(room.roomId);
    }
}
