using System;
using System.Collections.Generic;

public static class OnlineProtocol
{
    public const int Version = 17;
    public const string MessageName = "mtgb.protocol.v15";

    public const string Hello = "hello";
    public const string Welcome = "welcome";
    public const string Lobby = "lobby";
    public const string SubmitDeck = "submit-deck";
    public const string SetSpectator = "set-spectator";
    public const string SetReady = "set-ready";
    public const string RequestStartMatch = "request-start-match";
    public const string ChoosePlayDraw = "choose-play-draw";
    public const string StartMatch = "start-match";
    public const string SceneReady = "scene-ready";
    public const string SubmitAction = "submit-action";
    public const string StateUpdate = "state-update";
    public const string ActionRejected = "action-rejected";
    public const string HashReport = "hash-report";
    public const string RequestResync = "request-resync";
    public const string ResyncState = "resync-state";
    public const string SyncConfirmed = "sync-confirmed";
    public const string Concede = "concede";
    public const string MatchEnded = "match-ended";
    public const string ReplayChunk = "replay-chunk";
    public const string Error = "error";
    public const string ReconnectRejected = "reconnect-rejected";
    public const string Heartbeat = "heartbeat";
    public const string HeartbeatAck = "heartbeat-ack";
}

[Serializable]
public sealed class OnlineEnvelope
{
    public int version = OnlineProtocol.Version;
    public string type;
    public string payload;
}

[Serializable]
public sealed class OnlineHelloMessage
{
    public string roomAction;
    public string displayName;
    public string reconnectToken;
    public string roomId;
    public OnlineContentManifest manifest;
    public OnlineRoomConfig requestedRoomConfig;
}

[Serializable]
public sealed class OnlineWelcomeMessage
{
    public int seat;
    public string reconnectToken;
    public string serverName;
    public bool resumingMatch;
    public bool lanHosted;
    public string roomId;
    public OnlineContentManifest manifest;
}

[Serializable]
public sealed class OnlineLobbyPlayer
{
    public int seat;
    public string displayName;
    public bool connected;
    public bool hasDeck;
    public bool ready;
}

[Serializable]
public sealed class OnlineLobbyState
{
    public int playDrawChooserSeat;
    public string roomId;
    public int hostSeat;
    public OnlineRoomConfig config;
    public OnlineSeriesState series;
    public List<OnlineLobbyPlayer> players = new List<OnlineLobbyPlayer>();
}

[Serializable]
public sealed class OnlineDeckMessage
{
    public DeckDefinition deck;
}

[Serializable]
public sealed class OnlineReadyMessage
{
    public bool ready;
}

[Serializable]
public sealed class OnlinePlayDrawMessage
{
    public int firstPlayerSeat;
}

[Serializable]
public sealed class OnlineStartMatchMessage
{
    public string matchId;
    public int localSeat;
    public OnlineSeriesState series;
    public OnlinePlayerViewState view;
}

[Serializable]
public sealed class OnlineSceneReadyMessage
{
    public string initialStateHash;
}

[Serializable]
public sealed class OnlineActionMessage
{
    public string matchId;
    public long requestId;
    public GameAction action;
}

[Serializable]
public sealed class OnlineStateUpdateMessage
{
    public long requestId;
    public GameActionResult result;
    public OnlinePlayerViewState view;
}

[Serializable]
public sealed class OnlineHashReportMessage
{
    public long appliedSequence;
    public string stateHash;
}

[Serializable]
public sealed class OnlineResyncMessage
{
    public string matchId;
    public OnlinePlayerViewState view;
}

[Serializable]
public sealed class OnlineHeartbeatMessage
{
    public long nonce;
}

[Serializable]
public sealed class OnlineErrorMessage
{
    public long requestId;
    public string code;
    public string message;
}

[Serializable]
public sealed class OnlineMatchEndedMessage
{
    public OnlinePlayerViewState finalView;
    public DeckDefinition ownDeck;
    public bool aborted;
    public string matchId;
    public int winnerSeat;
    public string reason;
    public OnlineSeriesState series;
}
