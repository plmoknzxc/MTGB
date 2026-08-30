using System;
using System.Collections.Generic;

[Serializable]
public sealed class OnlineRoomConfig
{
    public int startingLife = 20;
    public int firstPlayerSeat;
    public int bestOf = 1;
    public int minimumMainDeckSize = 60;
    public int maximumSideboardSize = 15;
    public bool allowCustomContent;
    public List<string> allowedSetCodes = new List<string>();
}

[Serializable]
public sealed class OnlineSeriesState
{
    public int bestOf = 1;
    public int gameNumber = 1;
    public int player1Wins;
    public int player2Wins;
    public int draws;
    public int winnerSeat;
    public bool betweenGames;
    public bool finished;

    public int WinsRequired => bestOf == 3 ? 2 : 1;

    public void Reset(int requestedBestOf)
    {
        bestOf = requestedBestOf == 3 ? 3 : 1;
        gameNumber = 1;
        player1Wins = 0;
        player2Wins = 0;
        draws = 0;
        winnerSeat = 0;
        betweenGames = false;
        finished = false;
    }

    public void BeginGame()
    {
        betweenGames = false;
    }

    public void RecordGameResult(int gameWinnerSeat)
    {
        if (finished) return;
        if (gameWinnerSeat == 1) player1Wins++;
        else if (gameWinnerSeat == 2) player2Wins++;
        else draws++;

        if (player1Wins >= WinsRequired || player2Wins >= WinsRequired)
        {
            winnerSeat = player1Wins >= WinsRequired ? 1 : 2;
            finished = true;
            betweenGames = false;
            return;
        }

        gameNumber++;
        betweenGames = true;
    }
}

public sealed class OnlineRoomMember
{
    public int seat;
    public ulong clientId;
    public string displayName;
    public string reconnectToken;
    public DeckDefinition deck;
    public DeckDefinition registeredDeck;
    public bool connected;
    public bool ready;
    public bool sceneReady;
    public bool restoring;
    public double disconnectedAt;
    public OnlineReconnectWindow reconnectWindow = new OnlineReconnectWindow(30d);
    public OnlineServerRequestGate requestGate = new OnlineServerRequestGate();
}

public sealed class OnlineRoomState : IDisposable
{
    public string roomId;
    public OnlineRoomConfig config = new OnlineRoomConfig();
    public OnlineSeriesState series = new OnlineSeriesState();
    public readonly Dictionary<int, OnlineRoomMember> members = new Dictionary<int, OnlineRoomMember>();
    public OnlineMatchSession match;

    public bool IsEmpty => members.Count == 0;

    public void Dispose()
    {
        match?.Dispose();
        match = null;
        members.Clear();
    }
}

public sealed class OnlineRoomRegistry : IDisposable
{
    public const int MaxRooms = 128;

    private readonly Dictionary<string, OnlineRoomState> rooms =
        new Dictionary<string, OnlineRoomState>(StringComparer.OrdinalIgnoreCase);

    public int Count => rooms.Count;
    public IEnumerable<OnlineRoomState> Rooms => rooms.Values;

    public bool TryGetOrCreate(string requestedRoomId, out OnlineRoomState room, out string error)
    {
        room = null;
        string roomId = NormalizeRoomId(requestedRoomId);
        if (!OnlineMessageGuard.ValidateRoomId(roomId))
        {
            error = "房间号格式非法。";
            return false;
        }
        if (rooms.TryGetValue(roomId, out room))
        {
            error = string.Empty;
            return true;
        }
        if (rooms.Count >= MaxRooms)
        {
            error = "服务器房间数量已达上限。";
            return false;
        }

        room = new OnlineRoomState { roomId = roomId };
        rooms.Add(roomId, room);
        error = string.Empty;
        return true;
    }

    public bool TryGet(string roomId, out OnlineRoomState room)
    {
        return rooms.TryGetValue(NormalizeRoomId(roomId), out room);
    }

    public bool RemoveIfEmpty(string roomId)
    {
        string normalized = NormalizeRoomId(roomId);
        if (!rooms.TryGetValue(normalized, out OnlineRoomState room) || !room.IsEmpty) return false;
        rooms.Remove(normalized);
        room.Dispose();
        return true;
    }

    public static string NormalizeRoomId(string value)
    {
        string source = string.IsNullOrWhiteSpace(value) ? "main" : value.Trim().ToLowerInvariant();
        char[] result = new char[Math.Min(OnlineMessageGuard.MaxRoomIdCharacters, source.Length)];
        int length = 0;
        for (int i = 0; i < source.Length && length < result.Length; i++)
        {
            char character = source[i];
            if (char.IsLetterOrDigit(character) || character == '-' || character == '_')
            {
                result[length++] = character;
            }
        }
        return length == 0 ? "main" : new string(result, 0, length);
    }

    public void Dispose()
    {
        foreach (OnlineRoomState room in rooms.Values) room.Dispose();
        rooms.Clear();
    }
}
