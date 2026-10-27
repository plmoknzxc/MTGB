using System;
using System.Linq;

public sealed partial class OnlineMatchService
{
    private const ulong AiClientId = ulong.MaxValue;

    public void ConfigureRoomAi(DeckDefinition deck, bool remove = false)
    {
        if (HasJoinedLobby && !IsInMatch)
            SendToServer(OnlineProtocol.ConfigureAi, new OnlineAiMessage { deck = deck, remove = remove });
    }

    private static bool CanConfigureAi(OnlineRoomState room, OnlineRoomMember actor)
        => room != null && actor != null && !actor.isAi && !actor.IsSpectator && actor.connected
            && room.hostSeat == actor.seat && !room.RetainsPlayers && !actor.reviewing
            && room.playDrawChooserSeat == 0;

    private async void HandleConfigureAi(ulong clientId, OnlineAiMessage message)
    {
        if (!TryGetMember(clientId, out var room, out var actor)) return;
        void Reject(string value) => Send(clientId, OnlineProtocol.Error,
            new OnlineErrorMessage { code = "invalid-ai", message = value });
        if (message == null || !CanConfigureAi(room, actor)) { Reject("只有参赛房主可以在比赛开始前配置 AI。"); return; }
        int targetSeat = 3 - actor.seat;
        room.members.TryGetValue(targetSeat, out var previous);
        if (previous != null && !previous.isAi) { Reject("对面席位已有玩家，不能替换为 AI。"); return; }
        if (message.remove)
        {
            room.aiRevision++; room.aiPending = false;
            if (previous != null) room.members.Remove(targetSeat);
            BroadcastLobby(room); return;
        }
        if (room.aiPending) { Reject("正在检查 AI 卡组，请稍候。"); return; }
        long revision = ++room.aiRevision;
        room.aiPending = true;
        BroadcastLobby(room);
        bool Current() => this != null && roomRegistry.TryGet(room.roomId, out var current) && current == room
            && room.aiRevision == revision && TryGetMember(clientId, out var currentRoom, out var currentActor)
            && currentRoom == room && currentActor == actor && CanConfigureAi(room, actor)
            && (!room.members.TryGetValue(targetSeat, out var occupant) || occupant == previous);
        try
        {
            var deck = CloneDeck(message.deck);
            if (deck != null) deck.format = room.config.formatId;
            if (!ValidateDeck(deck, room.config, out string error)) { Reject(error); return; }
            if (DeckCommander.IsFormat(room.config.formatId))
            {
                var result = await DeckCommanderInspector.Inspect(deck);
                if (!Current()) return;
                if (result.error != null || !string.IsNullOrEmpty(result.problem)) { Reject(DeckCommanderInspector.Status(result)); return; }
            }
            if (DeckCompanion.Has(deck))
            {
                var result = await DeckCompanionInspector.Inspect(deck);
                if (!Current()) return;
                if (result.error != null || !result.Allows(deck)) { Reject(DeckCompanionInspector.Status(result, false)); return; }
            }
            if (!Current()) return;
            room.members[targetSeat] = new OnlineRoomMember {
                seat = targetSeat, isAi = true, clientId = AiClientId, displayName = "Forge AI",
                connected = true, ready = true, sceneReady = true, deck = deck
            };
            // Changing the opponent requires the human to confirm readiness again.
            actor.ready = false;
        }
        catch (Exception error)
        {
            if (Current()) Reject("AI 卡组检查失败：" + error.Message);
        }
        finally
        {
            if (this != null && room.aiRevision == revision)
            {
                room.aiPending = false;
                if (roomRegistry.TryGet(room.roomId, out var current) && current == room) BroadcastLobby(room);
            }
        }
    }

    // AI is a participant, never a network client or an elected room host.
    private static void RemoveOrphanedAi(OnlineRoomState room)
    {
        if (room == null || room.RetainsPlayers || room.members.Values.Any(m => !m.isAi)) return;
        room.aiRevision++; room.aiPending = false;
        foreach (var bot in room.members.Values.Where(m => m.isAi).ToArray()) room.members.Remove(bot.seat);
    }
}
