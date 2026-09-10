using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class OnlineMatchService : MonoBehaviour
{
    private const string BattleSceneName = "MTGAPrototype";
    private const string MainSceneName = "Main";
    private const int DefaultPort = 7777;
    private const double ActionTimeoutSeconds = 10d;
    private const double ReconnectGraceSeconds = 30d;
    private const double ReconnectRetrySeconds = 2d;
    private const double LobbyLeaseSeconds = 15d; // 客户端心跳 10s/次，15s 无消息视为已消失
    private const double HeartbeatIntervalSeconds = 10d;
    private const string DevelopmentServerFlag = "MTGB.Online.DevelopmentServerPending";

    private sealed class ClientLocation
    {
        public string roomId;
        public int seat;
    }

    public static OnlineMatchService Instance { get; private set; }

    public event Action<string> StatusChanged;
    public event Action<OnlineLobbyState> LobbyChanged;
    public event Action<bool> ConnectionChanged;
    public event Action<OnlineMatchEndedMessage> MatchEnded;
    public event Action<OnlineClientInteractionState> InteractionStateChanged;

    public bool IsOnline => networkManager != null && networkManager.IsListening;
    public bool IsServer => networkManager != null && networkManager.IsServer;
    public bool IsClient => networkManager != null && networkManager.IsClient;
    public bool IsHost => networkManager != null && networkManager.IsHost;
    public bool IsConnected => networkManager != null && networkManager.IsConnectedClient;
    public bool IsDisconnecting => IsManagerShutdownInProgress(shuttingDownManager);
    public bool HasJoinedLobby => HasLobbyIdentity(IsConnected, LocalSeat, LobbyState);
    public bool IsInMatch { get; private set; }
    public bool HasPendingAction => actionGate.State == OnlineClientInteractionState.Submitting;
    public bool CanSubmitRuleAction => (IsServer && !IsHost) || !IsInMatch || (IsConnected && actionGate.CanSubmit);
    public OnlineClientInteractionState InteractionState => actionGate.State;
    public string CurrentProfileId => currentProfileId;
    public string CurrentRoomId => currentRoomId;
    public string MatchId => matchId;
    public bool IsReconnecting => IsInMatch && reconnectingMatch;
    public double ReconnectGraceRemainingSeconds => clientReconnectWindow.RemainingSeconds(Time.realtimeSinceStartupAsDouble);
    public int LocalSeat { get; private set; }
    public OnlineLobbyState LobbyState { get; private set; }

    private static bool HasLobbyIdentity(bool transportConnected, int localSeat, OnlineLobbyState lobby)
    {
        return transportConnected
            && localSeat > 0
            && lobby?.players != null
            && lobby.players.Exists(player =>
                player != null && player.connected && player.seat == localSeat);
    }

    private readonly OnlineRoomRegistry roomRegistry = new OnlineRoomRegistry();
    private readonly Dictionary<ulong, ClientLocation> clientLocations = new Dictionary<ulong, ClientLocation>();
    private readonly OnlineMessageGuard messageGuard = new OnlineMessageGuard();
    private readonly OnlineClientActionGate actionGate = new OnlineClientActionGate();
    private readonly OnlineReconnectWindow clientReconnectWindow = new OnlineReconnectWindow(ReconnectGraceSeconds);
    private NetworkManager networkManager;
    private UnityTransport transport;
    private GameRules gameRules;
    private OnlineContentManifest localManifest;
    private OnlineRoomConfig requestedRoomConfig = new OnlineRoomConfig();
    private OnlinePlayerViewState pendingPlayerView;
    private bool handlersRegistered;
    private long nextRequestId = 1;
    private string currentProfileId = "default";
    private string currentRoomId = "main";
    private string matchId = string.Empty;
    private bool reconnectingMatch;
    private bool automaticReconnectEnabled;
    private bool connectedToLanHost;
    private double nextReconnectAttemptAt;
    private double nextHeartbeatAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartNetworkingFromCommandLine()
    {
        string playerName = OnlineClientProfileStore.ReadArgument("-name");
        string normalizedPlayerName = (playerName ?? string.Empty).Replace(" ", string.Empty);
        bool preparedMainEditorServer = string.IsNullOrWhiteSpace(playerName)
            && PlayerPrefs.GetInt(DevelopmentServerFlag, 0) == 1;
        bool developmentServer = string.Equals(normalizedPlayerName, "Player1", StringComparison.OrdinalIgnoreCase)
            || preparedMainEditorServer;
        bool developmentClient = ShouldAutoStartNetworkClient(playerName, HasArgument("-mtgb-client"));
        if (HasArgument("-mtgb-server") || developmentServer)
        {
            if (preparedMainEditorServer)
            {
                PlayerPrefs.DeleteKey(DevelopmentServerFlag);
                PlayerPrefs.Save();
            }
            EnsureInstance().StartDedicatedServer(ReadPortArgument());
            return;
        }
        if (!developmentClient) return;

        OnlineClientProfile profile = OnlineClientProfileStore.Load(OnlineClientProfileStore.ResolveCurrentProfileId());
        string address = OnlineClientProfileStore.ReadArgument("-address");
        if (string.IsNullOrWhiteSpace(address)) address = profile.address;
        string displayName = OnlineClientProfileStore.ReadArgument("-displayName");
        if (string.IsNullOrWhiteSpace(displayName)) displayName = profile.displayName;
        string room = OnlineClientProfileStore.ReadArgument("-room");
        if (string.IsNullOrWhiteSpace(room)) room = profile.roomId;

        OnlineMatchService service = EnsureInstance();
        service.Connect(address, ReadPortArgument(profile.port), displayName, profile.id, room);
    }

    private static bool ShouldAutoStartNetworkClient(string playerName, bool explicitClientRequested)
    {
        // Additional Editor 的名称只用于选择隔离配置，不能等同于玩家已经点击“加入”。
        // 自动客户端只允许由明确的启动参数开启，避免 Player 2 在房主刚建房时抢先入座。
        return explicitClientRequested;
    }

    public static OnlineMatchService EnsureInstance()
    {
        if (Instance != null) return Instance;
        OnlineMatchService existing = FindFirstObjectByType<OnlineMatchService>();
        if (existing != null) return existing;
        return new GameObject("OnlineMatchService").AddComponent<OnlineMatchService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Shutdown();
        if (shuttingDownManager != null)
        {
            if (shuttingDownManager.gameObject != null) Destroy(shuttingDownManager.gameObject);
            shuttingDownManager = null;
        }
        roomRegistry.Dispose();
        if (Instance == this) Instance = null;
    }

    private bool pendingLobbyDisconnect;
    private string pendingLobbyDisconnectReason;
    private NetworkManager shuttingDownManager;

    private void Update()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if (shuttingDownManager != null)
        {
            bool shutdownFinished;
            try { shutdownFinished = !shuttingDownManager.ShutdownInProgress; }
            catch { shutdownFinished = true; }
            if (shutdownFinished)
            {
                if (shuttingDownManager.gameObject != null) Destroy(shuttingDownManager.gameObject);
                shuttingDownManager = null;
                ConnectionChanged?.Invoke(false);
            }
        }
        if (pendingLobbyDisconnect && !IsInMatch)
        {
            // 延迟到 Update 执行断开，避免在网络消息回调内直接销毁网络对象
            string rejectionReason = pendingLobbyDisconnectReason;
            pendingLobbyDisconnect = false;
            pendingLobbyDisconnectReason = null;
            Disconnect();
            if (!string.IsNullOrWhiteSpace(rejectionReason)) SetStatus(rejectionReason);
            return;
        }
        if (IsServer)
        {
            UpdateServerConnections(now);
        }
        if (IsConnected && networkManager is { IsListening: true } && now >= nextHeartbeatAt)
        {
            nextHeartbeatAt = now + HeartbeatIntervalSeconds;
            SendToServer(OnlineProtocol.Heartbeat, new OnlineHeartbeatMessage { nonce = DateTime.UtcNow.Ticks });
        }
        if (!IsInMatch) return;
        if (IsClient && actionGate.Tick(now))
        {
            NotifyInteractionStateChanged();
            SetStatus("服务器确认超时，正在请求权威状态。");
            SendToServer(OnlineProtocol.RequestResync, new OnlineHashReportMessage
            {
                appliedSequence = gameRules?.GetAppliedActionSequence() ?? 0,
                stateHash = gameRules?.ComputeDeterministicStateHash() ?? string.Empty
            });
        }
        UpdateAutomaticReconnect(now);
    }

    public bool StartDedicatedServer(ushort port = DefaultPort)
    {
        connectedToLanHost = false;
        localManifest = OnlineContentManifest.Build();
        if (!CreateNetworkManager("0.0.0.0", port, true)) return false;
        RegisterNetworkCallbacks();
        bool started = networkManager.StartServer();
        if (!started) Shutdown();
        SetStatus(started ? $"独立服务器正在监听 UDP {port}。" : "无法启动独立服务器。");
        return started;
    }

    public bool StartLanHost(
        ushort port,
        string displayName,
        string profileId = null,
        string roomId = null,
        OnlineRoomConfig roomConfig = null)
    {
        pendingLobbyDisconnect = false;
        pendingLobbyDisconnectReason = null;
        connectedToLanHost = true;
        reconnectingMatch = false;
        automaticReconnectEnabled = false;
        ClearLobbyState();
        currentProfileId = OnlineClientProfileStore.NormalizeId(
            string.IsNullOrWhiteSpace(profileId) ? OnlineClientProfileStore.ResolveCurrentProfileId() : profileId);
        OnlineClientProfile stored = OnlineClientProfileStore.Load(currentProfileId);
        currentRoomId = OnlineRoomRegistry.NormalizeRoomId(string.IsNullOrWhiteSpace(roomId) ? stored.roomId : roomId);
        requestedRoomConfig = CloneRoomConfig(roomConfig);
        localManifest = OnlineContentManifest.Build();
        ushort listenPort = port == 0 ? (ushort)DefaultPort : port;
        if (!CreateNetworkManager("127.0.0.1", listenPort, true))
        {
            connectedToLanHost = false;
            return false;
        }
        RegisterNetworkCallbacks();
        OnlineClientProfileStore.SaveConnection(
            currentProfileId, "127.0.0.1", listenPort, displayName, currentRoomId);
        bool started = networkManager.StartHost();
        if (!started)
        {
            connectedToLanHost = false;
            Shutdown();
        }
        SetStatus(started
            ? $"局域网主机正在监听 UDP {listenPort}，房间 {currentRoomId}。"
            : "无法启动局域网主机。");
        return started;
    }

    public bool Connect(
        string address,
        ushort port,
        string displayName,
        string profileId = null,
        string roomId = null,
        OnlineRoomConfig roomConfig = null)
    {
        pendingLobbyDisconnect = false;
        pendingLobbyDisconnectReason = null;
        connectedToLanHost = false;
        if (!reconnectingMatch) ClearLobbyState();
        if (string.IsNullOrWhiteSpace(address)) address = "127.0.0.1";
        currentProfileId = OnlineClientProfileStore.NormalizeId(
            string.IsNullOrWhiteSpace(profileId) ? OnlineClientProfileStore.ResolveCurrentProfileId() : profileId);
        OnlineClientProfile stored = OnlineClientProfileStore.Load(currentProfileId);
        currentRoomId = OnlineRoomRegistry.NormalizeRoomId(string.IsNullOrWhiteSpace(roomId) ? stored.roomId : roomId);
        requestedRoomConfig = CloneRoomConfig(roomConfig);
        localManifest = OnlineContentManifest.Build();
        if (!CreateNetworkManager(address.Trim(), port == 0 ? (ushort)DefaultPort : port, false)) return false;
        RegisterNetworkCallbacks();
        OnlineClientProfileStore.SaveConnection(currentProfileId, address, port, displayName, currentRoomId);
        bool started = networkManager.StartClient();
        if (started) nextReconnectAttemptAt = Time.realtimeSinceStartupAsDouble + ReconnectRetrySeconds;
        else Shutdown();
        SetStatus(started ? $"正在连接 {address}:{port}，房间 {currentRoomId}…" : "无法启动网络客户端。");
        return started;
    }

    public void Disconnect()
    {
        pendingLobbyDisconnect = false;
        pendingLobbyDisconnectReason = null;
        Shutdown();
        OnlinePlayerContext.Reset();
        LocalSeat = 0;
        IsInMatch = false;
        reconnectingMatch = false;
        automaticReconnectEnabled = false;
        connectedToLanHost = false;
        clientReconnectWindow.Reset();
        ClearLobbyState();
        SetInteractionConnected(false);
        ConnectionChanged?.Invoke(false);
        SetStatus("已断开连接。");
    }

    public void SubmitDeck(DeckDefinition deck)
    {
        if (HasJoinedLobby && deck != null)
            SendToServer(OnlineProtocol.SubmitDeck, new OnlineDeckMessage { deck = CloneDeck(deck) });
    }

    public void SetReady(bool ready)
    {
        if (HasJoinedLobby) SendToServer(OnlineProtocol.SetReady, new OnlineReadyMessage { ready = ready });
    }

    public void RequestStartMatch()
    {
        if (HasJoinedLobby && !IsInMatch)
            SendToServer(OnlineProtocol.RequestStartMatch, new OnlineReadyMessage { ready = true });
    }

    public void Concede()
    {
        if (IsConnected && IsInMatch) SendToServer(OnlineProtocol.Concede, new OnlineReadyMessage { ready = true });
    }

    public static bool TryRouteAction(GameRules rules, GameAction action, out GameActionResult result)
    {
        result = null;
        OnlineMatchService service = Instance;
        if (service == null || (service.IsServer && !service.IsHost) || !service.IsInMatch || service.LocalSeat == 0)
            return false;
        if (!service.IsConnected)
        {
            result = GameActionResult.Rejected("已与权威服务器断开连接。", action?.sequence ?? 0);
            return true;
        }
        if (!service.actionGate.CanSubmit)
        {
            result = GameActionResult.Rejected("正在等待服务器确认或同步。", action?.sequence ?? 0);
            return true;
        }
        if (action == null)
        {
            result = GameActionResult.Rejected("Action is null.", 0);
            return true;
        }
        action.playerId = service.LocalSeat;
        action.sequence = 0;
        action.expectedStateSequence = rules?.GetAppliedActionSequence() ?? -1;
        long requestId = service.nextRequestId++;
        if (!service.actionGate.TryBeginSubmit(requestId, Time.realtimeSinceStartupAsDouble, ActionTimeoutSeconds))
        {
            result = GameActionResult.Rejected("操作输入当前已锁定。", 0);
            return true;
        }
        service.NotifyInteractionStateChanged();
        service.SendToServer(OnlineProtocol.SubmitAction, new OnlineActionMessage
        {
            requestId = requestId,
            action = action.Clone()
        });
        result = GameActionResult.Accepted(rules?.GetAppliedActionSequence() ?? 0, rules?.ComputeDeterministicStateHash());
        return true;
    }

    private bool CreateNetworkManager(string address, ushort port, bool server)
    {
        bool currentManagerListening = networkManager != null && networkManager.IsListening;
        bool shutdownInProgress = IsManagerShutdownInProgress(shuttingDownManager);
        if (!CanCreateNetworkManager(currentManagerListening, shutdownInProgress))
        {
            SetStatus(shutdownInProgress ? "正在退出上一个房间，请稍候。" : "网络已经在运行。");
            return false;
        }
        if (networkManager != null)
        {
            // 上一次会话的 NetworkManager 已 Shutdown（甚至关闭中）：复用会对已释放的
            // transport 调 StartClient 并抛 ObjectDisposedException，必须销毁重建
            Destroy(networkManager.gameObject);
            networkManager = null;
            transport = null;
        }
        if (shuttingDownManager != null)
        {
            if (shuttingDownManager.gameObject != null) Destroy(shuttingDownManager.gameObject);
            shuttingDownManager = null;
        }
        GameObject managerObject = new GameObject("MTGBNetworkManager");
        transport = managerObject.AddComponent<UnityTransport>();
        networkManager = managerObject.AddComponent<NetworkManager>();
        if (Application.isPlaying) DontDestroyOnLoad(managerObject);
        networkManager.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            EnableSceneManagement = false,
            ConnectionApproval = false
        };
        transport.SetConnectionData(address, port, server ? "0.0.0.0" : null);
        return true;
    }

    private static bool CanCreateNetworkManager(bool currentManagerListening, bool shutdownInProgress)
    {
        return !currentManagerListening && !shutdownInProgress;
    }

    private static bool IsManagerShutdownInProgress(NetworkManager manager)
    {
        if (manager == null) return false;
        try { return manager.ShutdownInProgress; }
        catch { return false; }
    }

    private void RegisterNetworkCallbacks()
    {
        if (handlersRegistered || networkManager == null) return;
        handlersRegistered = true;
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void RegisterMessageHandler()
    {
        networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(OnlineProtocol.MessageName);
        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(OnlineProtocol.MessageName, HandleMessage);
    }

    private void OnClientConnected(ulong clientId)
    {
        RegisterMessageHandler();
        if (IsServer)
        {
            messageGuard.Touch(clientId, Time.realtimeSinceStartupAsDouble);
            SetStatus($"客户端 {clientId} 已连接，等待身份验证。");
        }
        if (!IsClient || clientId != networkManager.LocalClientId) return;
        automaticReconnectEnabled = false;
        SetInteractionConnected(true);
        ConnectionChanged?.Invoke(true);
        SendHello();
    }

    private void SendHello()
    {
        SendToServer(OnlineProtocol.Hello, BuildHelloMessage());
    }

    private OnlineHelloMessage BuildHelloMessage()
    {
        OnlineClientProfile profile = OnlineClientProfileStore.Load(currentProfileId);
        return new OnlineHelloMessage
        {
            displayName = NormalizeDisplayName(profile.displayName),
            reconnectToken = reconnectingMatch ? profile.reconnectToken : string.Empty,
            roomId = currentRoomId,
            manifest = localManifest,
            requestedRoomConfig = CloneRoomConfig(requestedRoomConfig)
        };
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsServer)
        {
            messageGuard.Remove(clientId);
            if (!clientLocations.TryGetValue(clientId, out ClientLocation location)) return;
            clientLocations.Remove(clientId);
            if (!roomRegistry.TryGet(location.roomId, out OnlineRoomState room)
                || !room.members.TryGetValue(location.seat, out OnlineRoomMember member)) return;
            member.connected = false;
            member.sceneReady = false;
            member.disconnectedAt = Time.realtimeSinceStartupAsDouble;
            if (room.match != null)
            {
                member.reconnectWindow.Begin(member.disconnectedAt);
            SetRoomStatus(room, $"玩家 {member.seat} 断线，保留席位 {ReconnectGraceSeconds:0} 秒。");
            }
            else
            {
                RemoveLobbyMember(room, member.seat);
                roomRegistry.RemoveIfEmpty(room.roomId);
            }
            BroadcastLobby(room);
            RefreshServerMatchFlag();
            return;
        }
        if (networkManager == null || clientId != networkManager.LocalClientId) return;
        SetInteractionConnected(false);
        ConnectionChanged?.Invoke(false);
        if (IsInMatch)
        {
            reconnectingMatch = true;
            automaticReconnectEnabled = true;
            if (!clientReconnectWindow.IsActive) clientReconnectWindow.Begin(Time.realtimeSinceStartupAsDouble);
            nextReconnectAttemptAt = Time.realtimeSinceStartupAsDouble + 0.5d;
            SetStatus($"连接中断，将在 {ReconnectGraceSeconds:0} 秒内自动重连。");
        }
        else
        {
            OnlineClientProfileStore.SaveReconnectToken(currentProfileId, string.Empty, currentRoomId);
            HandleLobbyConnectionLost();
        }
    }

    private void HandleLobbyConnectionLost()
    {
        bool lanRoomClosed = connectedToLanHost;
        string rejectionReason = pendingLobbyDisconnectReason;
        string disconnectReason = !string.IsNullOrWhiteSpace(rejectionReason)
            ? rejectionReason
            : lanRoomClosed ? "局域网房主已退出，房间已解散。" : "连接已中断。";
        connectedToLanHost = false;
        LocalSeat = 0;
        OnlinePlayerContext.Reset();
        ClearLobbyState();
        SetStatus(disconnectReason);
        if (ShouldFinalizeLobbyTransport(networkManager != null, IsInMatch))
        {
            // NGO 的远端断开回调不会替客户端销毁本地 NetworkManager。
            // 下一帧完成本地 Shutdown，否则 UI 会永久停在 IsOnline/“正在连接”状态。
            pendingLobbyDisconnect = true;
            pendingLobbyDisconnectReason = disconnectReason;
        }
    }

    private static bool ShouldFinalizeLobbyTransport(bool hasNetworkManager, bool isInMatch)
    {
        return hasNetworkManager && !isInMatch;
    }

    private void HandleMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (IsServer && !IsHost && reader.Length > OnlineMessageGuard.MaxEnvelopeCharacters * 4)
        {
            RejectClient(senderClientId, "message-too-large", "消息大小超过限制。");
            return;
        }
        string json;
        try
        {
            reader.ReadValueSafe(out json);
        }
        catch (Exception)
        {
            if (IsServer) RejectClient(senderClientId, "malformed-message", "消息编码非法。");
            else SetStatus("收到无法解析的网络消息。");
            return;
        }
        OnlineEnvelope envelope;
        try { envelope = JsonUtility.FromJson<OnlineEnvelope>(json); }
        catch { envelope = null; }
        if (envelope == null || envelope.version != OnlineProtocol.Version)
        {
            if (IsServer) RejectClient(senderClientId, "protocol-mismatch", "协议版本不一致。");
            else SetStatus("服务器协议版本不兼容。");
            return;
        }
        bool serverBound = IsServerBoundMessage(envelope.type);
        if (IsServer && serverBound)
        {
            if (!messageGuard.TryAccept(senderClientId, json?.Length ?? 0,
                    Time.realtimeSinceStartupAsDouble, out string guardError))
            {
                RejectClient(senderClientId, "message-rejected", guardError);
                return;
            }
            HandleServerMessage(senderClientId, envelope);
            return;
        }
        if (IsClient && !serverBound && senderClientId == NetworkManager.ServerClientId)
        {
            HandleClientMessage(envelope);
            return;
        }
        if (IsServer) RejectClient(senderClientId, "unknown-message", "未知的协议消息。");
    }

    private static bool IsServerBoundMessage(string type)
    {
        switch (type)
        {
            case OnlineProtocol.Hello:
            case OnlineProtocol.SubmitDeck:
            case OnlineProtocol.SetReady:
            case OnlineProtocol.RequestStartMatch:
            case OnlineProtocol.SceneReady:
            case OnlineProtocol.SubmitAction:
            case OnlineProtocol.HashReport:
            case OnlineProtocol.RequestResync:
            case OnlineProtocol.Concede:
            case OnlineProtocol.Heartbeat:
                return true;
            default:
                return false;
        }
    }

    private void HandleServerMessage(ulong clientId, OnlineEnvelope envelope)
    {
        switch (envelope.type)
        {
            case OnlineProtocol.Hello: HandleHello(clientId, Deserialize<OnlineHelloMessage>(envelope)); break;
            case OnlineProtocol.SubmitDeck: HandleDeck(clientId, Deserialize<OnlineDeckMessage>(envelope)); break;
            case OnlineProtocol.SetReady: HandleReady(clientId, Deserialize<OnlineReadyMessage>(envelope)); break;
            case OnlineProtocol.RequestStartMatch: HandleStartMatchRequest(clientId); break;
            case OnlineProtocol.SceneReady: HandleSceneReady(clientId, Deserialize<OnlineSceneReadyMessage>(envelope)); break;
            case OnlineProtocol.SubmitAction: HandleSubmittedAction(clientId, Deserialize<OnlineActionMessage>(envelope)); break;
            case OnlineProtocol.HashReport: HandleHashReport(clientId, Deserialize<OnlineHashReportMessage>(envelope)); break;
            case OnlineProtocol.RequestResync: HandleResyncRequest(clientId); break;
            case OnlineProtocol.Concede: HandleConcede(clientId); break;
            case OnlineProtocol.Heartbeat:
                Send(clientId, OnlineProtocol.HeartbeatAck, Deserialize<OnlineHeartbeatMessage>(envelope));
                break;
            default: RejectClient(clientId, "unknown-message", "未知的协议消息。"); break;
        }
    }

    private void HandleClientMessage(OnlineEnvelope envelope)
    {
        switch (envelope.type)
        {
            case OnlineProtocol.Welcome: HandleWelcome(Deserialize<OnlineWelcomeMessage>(envelope)); break;
            case OnlineProtocol.Lobby:
                LobbyState = Deserialize<OnlineLobbyState>(envelope);
                LobbyChanged?.Invoke(LobbyState);
                break;
            case OnlineProtocol.StartMatch: BeginClientMatch(Deserialize<OnlineStartMatchMessage>(envelope)); break;
            case OnlineProtocol.StateUpdate: ApplyStateUpdate(Deserialize<OnlineStateUpdateMessage>(envelope)); break;
            case OnlineProtocol.ActionRejected:
                OnlineErrorMessage rejected = Deserialize<OnlineErrorMessage>(envelope);
                actionGate.CompleteSubmit(rejected?.requestId ?? 0);
                NotifyInteractionStateChanged();
                SetStatus(rejected?.message ?? "服务器拒绝了操作。");
                break;
            case OnlineProtocol.ResyncState: ApplyResyncView(Deserialize<OnlineResyncMessage>(envelope)); break;
            case OnlineProtocol.SyncConfirmed:
                reconnectingMatch = false;
                clientReconnectWindow.Reset();
                actionGate.CompleteResync();
                NotifyInteractionStateChanged();
                SetStatus("权威状态同步完成。");
                break;
            case OnlineProtocol.MatchEnded:
                OnlineMatchEndedMessage ended = Deserialize<OnlineMatchEndedMessage>(envelope);
                IsInMatch = false;
                reconnectingMatch = false;
                matchId = string.Empty;
                gameRules = null;
                pendingPlayerView = null;
                if (ended?.series != null)
                {
                    if (LobbyState == null) LobbyState = new OnlineLobbyState { roomId = currentRoomId };
                    LobbyState.series = CloneSeriesState(ended.series);
                }
                OnlineClientProfileStore.SaveReconnectToken(currentProfileId, string.Empty, currentRoomId);
                actionGate.SetConnected(IsConnected);
                NotifyInteractionStateChanged();
                MatchEnded?.Invoke(ended);
                SetStatus(ended?.reason ?? "对局结束。");
                if (SceneManager.GetActiveScene().name != MainSceneName) SceneManager.LoadScene(MainSceneName);
                break;
            case OnlineProtocol.Error:
                OnlineErrorMessage protocolError = Deserialize<OnlineErrorMessage>(envelope);
                if (protocolError?.code == OnlineProtocol.ReconnectRejected
                    || protocolError?.code == "reconnect-rejected")
                {
                    OnlineClientProfileStore.SaveReconnectToken(currentProfileId, string.Empty, currentRoomId);
                    reconnectingMatch = false;
                    automaticReconnectEnabled = false;
                    IsInMatch = false;
                }
                SetStatus(protocolError?.message ?? "网络协议错误。");
                if (ShouldResetLobbyConnectionOnError(IsServer, IsInMatch, HasJoinedLobby, protocolError))
                {
                    // 加入被拒（房间已满等）时立即复位，否则 UI 卡在连接态且无法重试
                    pendingLobbyDisconnect = true;
                    pendingLobbyDisconnectReason = protocolError.message;
                }
                break;
            case OnlineProtocol.HeartbeatAck: break;
        }
    }

    private static bool ShouldResetLobbyConnectionOnError(
        bool isServer,
        bool isInMatch,
        bool hasJoinedLobby,
        OnlineErrorMessage error)
    {
        return !isInMatch && !isServer && !hasJoinedLobby && error != null;
    }

    private void HandleWelcome(OnlineWelcomeMessage welcome)
    {
        if (welcome == null) return;
        if (localManifest != null && !localManifest.IsCompatibleWith(welcome.manifest, out string error))
        {
            SetStatus(error);
            Disconnect();
            return;
        }
        LocalSeat = welcome.seat;
        connectedToLanHost = welcome.lanHosted;
        currentRoomId = OnlineRoomRegistry.NormalizeRoomId(welcome.roomId);
        OnlinePlayerContext.SetLocalPlayer(LocalSeat);
        if (!string.IsNullOrEmpty(welcome.reconnectToken))
            OnlineClientProfileStore.SaveReconnectToken(currentProfileId, welcome.reconnectToken, currentRoomId);
        if (welcome.resumingMatch)
        {
            reconnectingMatch = true;
            actionGate.BeginResync();
            NotifyInteractionStateChanged();
        }
        SetStatus($"已作为玩家 {LocalSeat} 加入房间 {currentRoomId}。");
    }

    private void HandleHello(ulong clientId, OnlineHelloMessage hello)
    {
        if (clientLocations.ContainsKey(clientId))
        {
            RejectClient(clientId, "duplicate-hello", "连接已经完成身份验证。");
            return;
        }
        if (!OnlineMessageGuard.ValidateHello(hello, out string validationError))
        {
            RejectClient(clientId, "invalid-hello", validationError);
            return;
        }
        if (!localManifest.IsCompatibleWith(hello.manifest, out string manifestError))
        {
            RejectClient(clientId, "content-mismatch", manifestError);
            return;
        }
        bool roomExisted = roomRegistry.TryGet(hello.roomId, out _);
        if (!roomExisted && !string.IsNullOrWhiteSpace(hello.reconnectToken))
        {
            RejectClient(clientId, "reconnect-rejected", "重连房间不存在或令牌已失效。");
            return;
        }
        if (!roomRegistry.TryGetOrCreate(hello.roomId, out OnlineRoomState room, out string roomError))
        {
            RejectClient(clientId, "room-error", roomError);
            return;
        }
        if (!roomExisted)
        {
            room.config = SanitizeRoomConfig(hello.requestedRoomConfig);
            room.series.Reset(room.config.bestOf);
        }

        double now = Time.realtimeSinceStartupAsDouble;
        OnlineRoomMember member = null;
        if (!string.IsNullOrWhiteSpace(hello.reconnectToken))
        {
            member = room.members.Values.FirstOrDefault(item =>
                string.Equals(item.reconnectToken, hello.reconnectToken, StringComparison.Ordinal));
            if (member == null)
            {
                RejectClient(clientId, "reconnect-rejected", "重连令牌无效。");
                return;
            }
            if (member != null && (member.connected || !member.reconnectWindow.CanResume(now)))
            {
                RejectClient(clientId, "reconnect-rejected", "重连席位已占用或已过期。");
                return;
            }
        }
        bool resuming = member != null && room.match != null;
        if (member == null)
        {
            if (room.match != null)
            {
                RejectClient(clientId, "match-in-progress", "房间对局已经开始。");
                return;
            }
            // 新加入时回收已消失的占座成员（连接不在列表或心跳租约过期），
            // 否则离开过的玩家会一直被“房间已满”拒绝
            var liveIds = new HashSet<ulong>(networkManager.ConnectedClientsIds);
            if (PruneGoneLobbyMembers(room, liveIds, now))
            {
                roomRegistry.RemoveIfEmpty(room.roomId);
                BroadcastLobby(room);
                if (!roomRegistry.TryGet(room.roomId, out room)) return;
            }
            int seat = !room.members.ContainsKey(1) ? 1 : !room.members.ContainsKey(2) ? 2 : 0;
            if (seat == 0)
            {
                RejectClient(clientId, "room-full", "房间已满。");
                return;
            }
            member = new OnlineRoomMember { seat = seat, reconnectToken = Guid.NewGuid().ToString("N") };
            room.members.Add(seat, member);
        }
        member.clientId = clientId;
        member.displayName = NormalizeDisplayName(hello.displayName);
        member.connected = true;
        member.restoring = resuming;
        member.sceneReady = false;
        member.reconnectWindow.Reset();
        if (room.hostSeat == 0) room.hostSeat = member.seat;
        clientLocations[clientId] = new ClientLocation { roomId = room.roomId, seat = member.seat };
        Send(clientId, OnlineProtocol.Welcome, new OnlineWelcomeMessage
        {
            seat = member.seat,
            reconnectToken = member.reconnectToken,
            serverName = Application.productName,
            resumingMatch = resuming,
            lanHosted = IsHost,
            roomId = room.roomId,
            manifest = localManifest
        });
        BroadcastLobby(room);
        if (resuming) SendStartMatch(room, member);
    }

    private void HandleDeck(ulong clientId, OnlineDeckMessage message)
    {
        if (!TryGetMember(clientId, out OnlineRoomState room, out OnlineRoomMember member) || room.match != null) return;
        if (!ValidateDeck(message?.deck, room.config, out string error))
        {
            Send(clientId, OnlineProtocol.Error, new OnlineErrorMessage { code = "invalid-deck", message = error });
            return;
        }
        if (room.series?.betweenGames == true
            && member.registeredDeck != null
            && !ValidateSideboardChange(member.registeredDeck, message.deck, room.config, out error))
        {
            Send(clientId, OnlineProtocol.Error, new OnlineErrorMessage { code = "invalid-sideboard", message = error });
            return;
        }
        member.deck = CloneDeck(message.deck);
        if (room.series?.finished == true) member.registeredDeck = null;
        member.ready = false;
        BroadcastLobby(room);
    }

    private void HandleReady(ulong clientId, OnlineReadyMessage message)
    {
        if (!TryGetMember(clientId, out OnlineRoomState room, out OnlineRoomMember member) || room.match != null) return;
        member.ready = message?.ready == true && member.deck != null;
        BroadcastLobby(room);
    }

    private void HandleStartMatchRequest(ulong clientId)
    {
        if (!TryGetMember(clientId, out OnlineRoomState room, out OnlineRoomMember member)) return;
        if (!CanStartRoomMatch(room, member.seat, out string error))
        {
            Send(clientId, OnlineProtocol.Error, new OnlineErrorMessage
            {
                code = "start-not-allowed",
                message = error
            });
            return;
        }
        StartRoomMatch(room);
    }

    private static bool CanStartRoomMatch(OnlineRoomState room, int requesterSeat, out string error)
    {
        EnsureRoomHost(room);
        if (room == null || room.match != null)
        {
            error = "房间当前不能开始新对局。";
            return false;
        }
        if (requesterSeat != room.hostSeat)
        {
            error = "只有房主可以开始比赛。";
            return false;
        }
        if (room.members.Count != 2
            || !room.members.Values.All(item => item.connected && item.ready && item.deck != null))
        {
            error = "需要两名玩家都提交牌组并准备。";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private void StartRoomMatch(OnlineRoomState room)
    {
        try
        {
            if (room.series == null) room.series = new OnlineSeriesState();
            if (room.series.finished)
            {
                room.series.Reset(room.config.bestOf);
                foreach (OnlineRoomMember member in room.members.Values) member.registeredDeck = null;
            }
            foreach (OnlineRoomMember member in room.members.Values)
            {
                if (member.registeredDeck == null) member.registeredDeck = CloneDeck(member.deck);
            }
            room.series.BeginGame();
            int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            OnlineRoomConfig matchConfig = CloneRoomConfig(room.config);
            matchConfig.firstPlayerSeat = UnityEngine.Random.Range(1, 3);
            room.match = new OnlineMatchSession(Guid.NewGuid().ToString("N"),
                room.members[1].deck, room.members[2].deck, seed, matchConfig);
            foreach (OnlineRoomMember member in room.members.Values)
            {
                member.sceneReady = false;
                member.restoring = false;
                SendStartMatch(room, member);
            }
            RefreshServerMatchFlag();
            SetRoomStatus(room, "对局已开始。");
        }
        catch (Exception exception)
        {
            SetRoomStatus(room, $"无法开始对局：{exception.Message}");
        }
    }

    private void SendStartMatch(OnlineRoomState room, OnlineRoomMember member)
    {
        Send(member.clientId, OnlineProtocol.StartMatch, new OnlineStartMatchMessage
        {
            matchId = room.match.MatchId,
            localSeat = member.seat,
            series = CloneSeriesState(room.series),
            view = room.match.GetView(member.seat)
        });
    }

    private void BeginClientMatch(OnlineStartMatchMessage message)
    {
        if (message?.view == null) return;
        LocalSeat = message.localSeat;
        matchId = message.matchId ?? string.Empty;
        OnlinePlayerContext.SetLocalPlayer(LocalSeat);
        pendingPlayerView = message.view;
        IsInMatch = true;
        actionGate.SetConnected(true);
        if (reconnectingMatch) actionGate.BeginResync();
        NotifyInteractionStateChanged();
        SetStatus(reconnectingMatch ? "正在恢复对局视图。" : "正在载入权威对局视图。");
        SceneManager.LoadScene(BattleSceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsInMatch && scene.name == BattleSceneName) StartCoroutine(BindRulesAfterSceneLoad());
    }

    private IEnumerator BindRulesAfterSceneLoad()
    {
        gameRules = null;
        for (int i = 0; i < 120 && gameRules == null; i++)
        {
            gameRules = FindFirstObjectByType<GameRules>();
            if (gameRules == null) yield return null;
        }
        if (gameRules == null)
        {
            SetStatus("战斗场景中没有 GameRules。");
            yield break;
        }
        yield return null;
        if (!ApplyPlayerView(pendingPlayerView, out string error))
        {
            SetStatus($"无法应用玩家视图：{error}");
            yield break;
        }
        pendingPlayerView = null;
        SendToServer(OnlineProtocol.SceneReady, new OnlineSceneReadyMessage
        {
            initialStateHash = gameRules.ComputeDeterministicStateHash()
        });
    }

    private void HandleSceneReady(ulong clientId, OnlineSceneReadyMessage message)
    {
        if (!TryGetMember(clientId, out OnlineRoomState room, out OnlineRoomMember member) || room.match == null) return;
        OnlinePlayerViewState view = room.match.GetView(member.seat);
        if (view == null || !string.Equals(view.viewHash, message?.initialStateHash, StringComparison.Ordinal))
        {
            SendResync(room, member);
            return;
        }
        member.sceneReady = true;
        member.restoring = false;
        Send(clientId, OnlineProtocol.SyncConfirmed, new OnlineHashReportMessage
        {
            appliedSequence = view.appliedSequence,
            stateHash = view.viewHash
        });
    }

    private void HandleSubmittedAction(ulong clientId, OnlineActionMessage message)
    {
        if (!TryGetMember(clientId, out OnlineRoomState room, out OnlineRoomMember member) || room.match == null) return;
        GameAction action = message?.action?.Clone();
        bool validShape = OnlineMessageGuard.ValidateActionShape(action, out string shapeError);
        string requestError = string.Empty;
        bool validRequest = validShape
            && member.requestGate.TryPrepare(message?.requestId ?? 0, member.seat, action, out requestError);
        if (!validShape || !validRequest)
        {
            SetRoomStatus(room, $"拒绝玩家 {member.seat} 请求 {message?.requestId ?? 0}：{(validShape ? requestError : shapeError)}");
            RejectAction(clientId, message?.requestId ?? 0, validShape ? requestError : shapeError);
            return;
        }
        if (!member.sceneReady || room.members.Values.Any(item => item.connected && !item.sceneReady))
        {
            SetRoomStatus(room, $"拒绝玩家 {member.seat} 请求 {message.requestId}：玩家视图尚未同步。");
            RejectAction(clientId, message.requestId, "正在等待房间内玩家完成同步。");
            return;
        }
        GameActionResult result = room.match.ApplyAction(action);
        if (!result.accepted)
        {
            SetRoomStatus(room, $"拒绝玩家 {member.seat} 请求 {message.requestId}：{result.error}");
            RejectAction(clientId, message.requestId, result.error);
            return;
        }
        foreach (OnlineRoomMember recipient in room.members.Values.Where(item => item.connected))
        {
            OnlinePlayerViewState view = room.match.GetView(recipient.seat);
            Send(recipient.clientId, OnlineProtocol.StateUpdate, new OnlineStateUpdateMessage
            {
                requestId = recipient.clientId == clientId ? message.requestId : 0,
                result = GameActionResult.Accepted(view.appliedSequence, view.viewHash),
                view = view
            });
        }
        string viewHash = room.match.GetView(member.seat)?.viewHash ?? string.Empty;
        SetRoomStatus(room,
            $"玩家 {member.seat} 已应用动作 seq={room.match.AppliedSequence} type={action.type} hash={ShortHash(viewHash)}。");
        MatchOutcome outcome = room.match.GetView(1).outcome;
        if (outcome.isFinished) EndRoomMatch(room, outcome.winnerPlayerId, outcome.message);
    }

    private void ApplyStateUpdate(OnlineStateUpdateMessage message)
    {
        if (message?.view == null || gameRules == null) return;
        if (!ApplyPlayerView(message.view, out string error))
        {
            actionGate.BeginResync();
            NotifyInteractionStateChanged();
            SetStatus($"状态更新校验失败：{error}");
            SendToServer<OnlineHashReportMessage>(OnlineProtocol.RequestResync, null);
            return;
        }
        if (message.requestId > 0) actionGate.CompleteSubmit(message.requestId);
        NotifyInteractionStateChanged();
    }

    private void HandleHashReport(ulong clientId, OnlineHashReportMessage message)
    {
        if (!TryGetMember(clientId, out OnlineRoomState room, out OnlineRoomMember member) || room.match == null) return;
        OnlinePlayerViewState view = room.match.GetView(member.seat);
        if (message != null && message.appliedSequence == view.appliedSequence && message.stateHash == view.viewHash)
        {
            member.sceneReady = true;
            member.restoring = false;
            Send(clientId, OnlineProtocol.SyncConfirmed, new OnlineHashReportMessage
            {
                appliedSequence = view.appliedSequence,
                stateHash = view.viewHash
            });
            return;
        }
        SendResync(room, member);
    }

    private void HandleResyncRequest(ulong clientId)
    {
        if (TryGetMember(clientId, out OnlineRoomState room, out OnlineRoomMember member) && room.match != null)
            SendResync(room, member);
    }

    private void SendResync(OnlineRoomState room, OnlineRoomMember member)
    {
        member.sceneReady = false;
        Send(member.clientId, OnlineProtocol.ResyncState, new OnlineResyncMessage
        {
            matchId = room.match.MatchId,
            view = room.match.GetView(member.seat)
        });
    }

    private void ApplyResyncView(OnlineResyncMessage message)
    {
        if (gameRules == null || message?.view == null) return;
        actionGate.BeginResync();
        NotifyInteractionStateChanged();
        if (!string.IsNullOrEmpty(message.matchId)) matchId = message.matchId;
        if (!ApplyPlayerView(message.view, out string error))
        {
            SetStatus($"权威状态恢复失败：{error}");
            return;
        }
        SendToServer(OnlineProtocol.HashReport, new OnlineHashReportMessage
        {
            appliedSequence = gameRules.GetAppliedActionSequence(),
            stateHash = gameRules.ComputeDeterministicStateHash()
        });
    }

    private bool ApplyPlayerView(OnlinePlayerViewState view, out string error)
    {
        if (gameRules == null)
        {
            error = "GameRules is not bound.";
            return false;
        }
        return gameRules.ApplyOnlinePlayerView(view, out error);
    }

    private void HandleConcede(ulong clientId)
    {
        if (!TryGetMember(clientId, out OnlineRoomState room, out OnlineRoomMember member) || room.match == null) return;
        HandleSubmittedAction(clientId, new OnlineActionMessage
        {
            requestId = Math.Max(1, member.requestGate.LastRequestId + 1),
            action = new GameAction { type = GameActionType.Concede, playerId = member.seat }
        });
    }

    private void EndRoomMatch(OnlineRoomState room, int winnerSeat, string reason)
    {
        if (room?.match == null) return;
        room.series ??= new OnlineSeriesState();
        room.series.RecordGameResult(winnerSeat);
        var ended = new OnlineMatchEndedMessage
        {
            matchId = room.match.MatchId,
            winnerSeat = winnerSeat,
            reason = string.IsNullOrWhiteSpace(reason) ? "对局结束。" : reason,
            series = CloneSeriesState(room.series)
        };
        SendToRoom(room, OnlineProtocol.MatchEnded, ended);
        SetRoomStatus(room, $"对局结束，胜者席位 {winnerSeat}：{ended.reason}");
        room.match.Dispose();
        room.match = null;
        foreach (OnlineRoomMember member in room.members.Values)
        {
            member.ready = false;
            member.sceneReady = false;
            member.restoring = false;
            member.reconnectWindow.Reset();
        }
        foreach (int seat in room.members.Where(pair => !pair.Value.connected).Select(pair => pair.Key).ToList())
            room.members.Remove(seat);
        EnsureRoomHost(room);
        BroadcastLobby(room);
        roomRegistry.RemoveIfEmpty(room.roomId);
        RefreshServerMatchFlag();
    }

    private void UpdateServerConnections(double now)
    {
        var connectedIds = new HashSet<ulong>(networkManager.ConnectedClientsIds);
        var idleClients = networkManager.ConnectedClientsIds
            .Where(clientId => clientId != NetworkManager.ServerClientId && messageGuard.IsIdle(clientId, now))
            .ToList();
        for (int i = 0; i < idleClients.Count; i++) networkManager.DisconnectClient(idleClients[i], "Idle timeout");
        foreach (OnlineRoomState room in roomRegistry.Rooms.ToList())
        {
            if (room.match != null)
            {
                OnlineRoomMember expired = room.members.Values.FirstOrDefault(item =>
                    !item.connected && item.reconnectWindow.IsExpired(now));
                if (expired == null) continue;
                OnlineRoomMember opponent = room.members.Values.FirstOrDefault(item => item.connected);
                EndRoomMatch(room, opponent?.seat ?? 0, $"玩家 {expired.seat} 未在 {ReconnectGraceSeconds:0} 秒内重连。");
                continue;
            }

            // 大厅（无对局）：按席位租约回收。消失的 UDP 连接在 NGO 在线列表里会残留很久，
            // 因此以“连接不在列表”或“15 秒无任何消息（含心跳）”为准，命中即回收并广播
            if (PruneGoneLobbyMembers(room, connectedIds, now))
            {
                roomRegistry.RemoveIfEmpty(room.roomId);
                BroadcastLobby(room);
            }
        }
    }

    /// <summary>大厅席位租约清理：连接已消失或心跳租约过期的成员视为已离开。
    /// 返回是否有变更。</summary>
    private bool PruneGoneLobbyMembers(OnlineRoomState room, HashSet<ulong> connectedIds, double now)
    {
        bool changed = false;
        foreach (OnlineRoomMember member in room.members.Values.ToList())
        {
            if (member.clientId == NetworkManager.ServerClientId) continue; // 主机本地成员
            bool connectionGone = !connectedIds.Contains(member.clientId);
            bool leaseExpired = !messageGuard.WasSeenWithin(member.clientId, now, LobbyLeaseSeconds);
            if (!connectionGone && !leaseExpired) continue;
            room.members.Remove(member.seat);
            clientLocations.Remove(member.clientId);
            messageGuard.Remove(member.clientId);
            if (!connectionGone) networkManager.DisconnectClient(member.clientId, "Lobby lease expired");
            changed = true;
        }
        if (changed) EnsureRoomHost(room);
        return changed;
    }

    private void UpdateAutomaticReconnect(double now)
    {
        if (!reconnectingMatch || !automaticReconnectEnabled || IsConnected) return;
        if (clientReconnectWindow.IsExpired(now))
        {
            automaticReconnectEnabled = false;
            reconnectingMatch = false;
            IsInMatch = false;
            SetInteractionConnected(false);
            MatchEnded?.Invoke(new OnlineMatchEndedMessage { matchId = matchId, reason = "重连等待时间已过。" });
            SetStatus("重连等待时间已过。");
            return;
        }
        if ((networkManager != null && networkManager.IsListening) || now < nextReconnectAttemptAt) return;
        nextReconnectAttemptAt = now + ReconnectRetrySeconds;
        OnlineClientProfile profile = OnlineClientProfileStore.Load(currentProfileId);
        Connect(profile.address, profile.port, profile.displayName, profile.id, currentRoomId);
    }

    private bool TryGetMember(ulong clientId, out OnlineRoomState room, out OnlineRoomMember member)
    {
        room = null;
        member = null;
        return clientLocations.TryGetValue(clientId, out ClientLocation location)
            && roomRegistry.TryGet(location.roomId, out room)
            && room.members.TryGetValue(location.seat, out member)
            && member.clientId == clientId
            && member.connected;
    }

    private void BroadcastLobby(OnlineRoomState room)
    {
        if (room == null) return;
        SendToRoom(room, OnlineProtocol.Lobby, BuildLobbyState(room));
    }

    private static OnlineLobbyState BuildLobbyState(OnlineRoomState room)
    {
        EnsureRoomHost(room);
        var state = new OnlineLobbyState
        {
            roomId = room.roomId,
            hostSeat = room.hostSeat,
            config = room.config,
            series = CloneSeriesState(room.series)
        };
        foreach (OnlineRoomMember member in room.members.Values
                     .Where(item => item != null && item.connected)
                     .OrderBy(item => item.seat))
        {
            state.players.Add(new OnlineLobbyPlayer
            {
                seat = member.seat,
                displayName = member.displayName,
                connected = true,
                hasDeck = member.deck != null,
                ready = member.ready
            });
        }
        return state;
    }

    private static void EnsureRoomHost(OnlineRoomState room)
    {
        if (room == null) return;
        if (room.hostSeat > 0
            && room.members.TryGetValue(room.hostSeat, out OnlineRoomMember current)
            && current != null
            && current.connected)
        {
            return;
        }
        OnlineRoomMember next = room.members.Values
            .Where(item => item != null && item.connected)
            .OrderBy(item => item.seat)
            .FirstOrDefault();
        room.hostSeat = next?.seat ?? 0;
    }

    private static bool RemoveLobbyMember(OnlineRoomState room, int seat)
    {
        if (room == null || !room.members.Remove(seat)) return false;
        EnsureRoomHost(room);
        return true;
    }

    private void SendToRoom<T>(OnlineRoomState room, string type, T payload)
    {
        if (room == null) return;
        foreach (OnlineRoomMember member in room.members.Values)
            if (member.connected) Send(member.clientId, type, payload);
    }

    private void SendToServer<T>(string type, T payload)
    {
        if (networkManager != null && networkManager.IsListening && networkManager.IsClient)
            Send(NetworkManager.ServerClientId, type, payload);
    }

    private void Send<T>(ulong clientId, string type, T payload)
    {
        if (networkManager?.CustomMessagingManager == null) return;
        string payloadJson = payload == null ? string.Empty : JsonUtility.ToJson(payload);
        string json = JsonUtility.ToJson(new OnlineEnvelope { type = type, payload = payloadJson });
        if (json.Length > OnlineMessageGuard.MaxEnvelopeCharacters)
        {
            if (IsServer && clientLocations.TryGetValue(clientId, out ClientLocation location)
                && roomRegistry.TryGet(location.roomId, out OnlineRoomState room))
                SetRoomStatus(room, $"拒绝向玩家 {location.seat} 发送过大的 {type} 消息。");
            else SetStatus($"拒绝发送过大的 {type} 消息。");
            return;
        }
        using var writer = new FastBufferWriter(Mathf.Max(256, json.Length * 2 + 16), Allocator.Temp);
        writer.WriteValueSafe(json);
        networkManager.CustomMessagingManager.SendNamedMessage(
            OnlineProtocol.MessageName, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    private void RejectClient(ulong clientId, string code, string message)
    {
        Send(clientId, OnlineProtocol.Error, new OnlineErrorMessage { code = code, message = message });
        networkManager.DisconnectClient(clientId, message);
    }

    private void RejectAction(ulong clientId, long requestId, string message)
    {
        Send(clientId, OnlineProtocol.ActionRejected, new OnlineErrorMessage
        {
            requestId = requestId,
            code = "action-rejected",
            message = message
        });
    }

    private static T Deserialize<T>(OnlineEnvelope envelope) where T : class
    {
        if (envelope == null || string.IsNullOrEmpty(envelope.payload)) return null;
        try { return JsonUtility.FromJson<T>(envelope.payload); }
        catch { return null; }
    }

    private bool ValidateDeck(DeckDefinition deck, OnlineRoomConfig config, out string error)
    {
        if (!OnlineMessageGuard.ValidateDeckShape(deck, out error)) return false;
        IReadOnlyList<CardDefinition> cards = CardDatabaseRepository.LoadAll();
        DeckDefinition copy = CloneDeck(deck);
        DeckRepository.EnforceCopyLimits(copy, cards);
        List<CardDefinition> runtime = DeckRepository.BuildRuntimeDeck(copy, cards, out string warning);
        if (runtime.Count == 0)
        {
            error = string.IsNullOrEmpty(warning) ? "牌组没有有效卡牌。" : warning;
            return false;
        }
        int mainCount = DeckRepository.GetTotalCount(deck, false);
        int sideboardCount = DeckRepository.GetTotalCount(deck, true);
        int minimumMain = Math.Max(1, config?.minimumMainDeckSize ?? 60);
        int maximumSideboard = Math.Max(0, config?.maximumSideboardSize ?? 15);
        if (mainCount < minimumMain)
        {
            error = $"主牌至少需要 {minimumMain} 张，当前为 {mainCount} 张。";
            return false;
        }
        if (sideboardCount > maximumSideboard)
        {
            error = $"备牌至多允许 {maximumSideboard} 张，当前为 {sideboardCount} 张。";
            return false;
        }
        if (JsonUtility.ToJson(deck) != JsonUtility.ToJson(copy))
        {
            error = "牌组包含无效项目或违反复制数量限制。";
            return false;
        }
        foreach (DeckEntry entry in EnumerateDeckEntries(deck))
        {
            CardDefinition card = DeckRepository.ResolveCard(entry, CardDatabaseRepository.LoadAll());
            if (card == null)
            {
                error = $"服务器未安装卡牌 {entry.PrintingKey}。";
                return false;
            }
            if (card.customContent && config?.allowCustomContent != true)
            {
                error = $"房间未允许自定义内容：{card.PrintingKey}。";
                return false;
            }
            if (config?.allowedSetCodes != null && config.allowedSetCodes.Count > 0
                && !config.allowedSetCodes.Exists(code =>
                    string.Equals(code, card.setCode, StringComparison.OrdinalIgnoreCase)))
            {
                error = $"房间未允许系列 {card.setCode}。";
                return false;
            }
        }
        return true;
    }

    private bool ValidateSideboardChange(
        DeckDefinition registeredDeck,
        DeckDefinition submittedDeck,
        OnlineRoomConfig config,
        out string error)
    {
        if (!ValidateDeck(submittedDeck, config, out error)) return false;
        if (!HaveSameEntries(registeredDeck.commanders, submittedDeck.commanders)
            || !HaveSameCardPool(registeredDeck, submittedDeck))
        {
            error = "局间换备只能在本轮注册的主牌与备牌之间交换卡牌。";
            return false;
        }
        return true;
    }

    private static bool HaveSameCardPool(DeckDefinition first, DeckDefinition second)
    {
        Dictionary<string, int> firstPool = BuildEntryCounts(first?.cards, first?.sideboard);
        Dictionary<string, int> secondPool = BuildEntryCounts(second?.cards, second?.sideboard);
        return CountsEqual(firstPool, secondPool);
    }

    private static bool HaveSameEntries(List<DeckEntry> first, List<DeckEntry> second)
    {
        return CountsEqual(BuildEntryCounts(first), BuildEntryCounts(second));
    }

    private static Dictionary<string, int> BuildEntryCounts(params List<DeckEntry>[] groups)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (groups == null) return counts;
        for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            List<DeckEntry> entries = groups[groupIndex];
            if (entries == null) continue;
            for (int i = 0; i < entries.Count; i++)
            {
                DeckEntry entry = entries[i];
                if (entry == null || entry.count <= 0) continue;
                string key = !string.IsNullOrEmpty(entry.PrintingKey)
                    ? entry.PrintingKey.ToUpperInvariant()
                    : $"ID:{entry.cardId}";
                counts.TryGetValue(key, out int count);
                counts[key] = count + entry.count;
            }
        }
        return counts;
    }

    private static bool CountsEqual(Dictionary<string, int> first, Dictionary<string, int> second)
    {
        if (first.Count != second.Count) return false;
        foreach (KeyValuePair<string, int> pair in first)
            if (!second.TryGetValue(pair.Key, out int count) || count != pair.Value) return false;
        return true;
    }

    private static IEnumerable<DeckEntry> EnumerateDeckEntries(DeckDefinition deck)
    {
        if (deck?.commanders != null)
            for (int i = 0; i < deck.commanders.Count; i++) if (deck.commanders[i] != null) yield return deck.commanders[i];
        if (deck?.cards != null)
            for (int i = 0; i < deck.cards.Count; i++) if (deck.cards[i] != null) yield return deck.cards[i];
        if (deck?.sideboard != null)
            for (int i = 0; i < deck.sideboard.Count; i++) if (deck.sideboard[i] != null) yield return deck.sideboard[i];
    }

    private static OnlineRoomConfig SanitizeRoomConfig(OnlineRoomConfig source)
    {
        var result = new OnlineRoomConfig
        {
            startingLife = Math.Max(1, Math.Min(200, source?.startingLife ?? 20)),
            firstPlayerSeat = 0,
            bestOf = source?.bestOf == 3 ? 3 : 1,
            minimumMainDeckSize = Math.Max(1, Math.Min(250, source?.minimumMainDeckSize ?? 60)),
            maximumSideboardSize = Math.Max(0, Math.Min(30, source?.maximumSideboardSize ?? 15)),
            allowCustomContent = source?.allowCustomContent == true
        };
        if (source?.allowedSetCodes == null) return result;
        for (int i = 0; i < source.allowedSetCodes.Count && result.allowedSetCodes.Count < 64; i++)
        {
            string code = (source.allowedSetCodes[i] ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length > 0 && code.Length <= 16 && !result.allowedSetCodes.Contains(code))
                result.allowedSetCodes.Add(code);
        }
        return result;
    }

    private static OnlineRoomConfig CloneRoomConfig(OnlineRoomConfig source)
    {
        return JsonUtility.FromJson<OnlineRoomConfig>(JsonUtility.ToJson(source ?? new OnlineRoomConfig()));
    }

    private static OnlineSeriesState CloneSeriesState(OnlineSeriesState source)
    {
        return source == null
            ? new OnlineSeriesState()
            : JsonUtility.FromJson<OnlineSeriesState>(JsonUtility.ToJson(source));
    }

    private void RefreshServerMatchFlag()
    {
        IsInMatch = roomRegistry.Rooms.Any(room => room.match != null);
    }

    private void Shutdown()
    {
        if (networkManager != null)
        {
            if (handlersRegistered)
            {
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                handlersRegistered = false;
            }
            if (networkManager.IsListening)
            {
                // NGO 的 Shutdown 是异步的：立即销毁对象会在断开包发出前拆掉传输层，
                // 对端收不到断开通知（座位残留、再进被拒、ObjectDisposedException 的根源）。
                // 保留对象，由 Update 确认关闭完成后再销毁。
                networkManager.Shutdown();
                shuttingDownManager = networkManager;
            }
            else if (networkManager.gameObject != null)
            {
                Destroy(networkManager.gameObject);
            }
        }
        networkManager = null;
        transport = null;
        gameRules = null;
        pendingPlayerView = null;
        matchId = string.Empty;
        reconnectingMatch = false;
        automaticReconnectEnabled = false;
        connectedToLanHost = false;
        clientReconnectWindow.Reset();
        clientLocations.Clear();
        roomRegistry.Dispose();
    }

    private void SetStatus(string status)
    {
        Debug.Log($"[Online][room={currentRoomId}][match={(string.IsNullOrEmpty(matchId) ? "lobby" : matchId)}] {status}");
        StatusChanged?.Invoke(status);
    }

    private void ClearLobbyState()
    {
        LobbyState = null;
        LobbyChanged?.Invoke(null);
    }

    private void SetRoomStatus(OnlineRoomState room, string status)
    {
        string roomId = room?.roomId ?? "unknown";
        string roomMatchId = room?.match?.MatchId;
        Debug.Log($"[Online][room={roomId}][match={(string.IsNullOrEmpty(roomMatchId) ? "lobby" : roomMatchId)}] {status}");
        StatusChanged?.Invoke(status);
    }

    private static string ShortHash(string value)
    {
        if (string.IsNullOrEmpty(value)) return "none";
        return value.Length <= 12 ? value : value.Substring(0, 12);
    }

    private void SetInteractionConnected(bool connected)
    {
        OnlineClientInteractionState previous = actionGate.State;
        actionGate.SetConnected(connected);
        if (previous != actionGate.State) NotifyInteractionStateChanged();
    }

    private void NotifyInteractionStateChanged()
    {
        InteractionStateChanged?.Invoke(actionGate.State);
    }

    private static string NormalizeDisplayName(string value)
    {
        string result = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
        return result.Length <= OnlineMessageGuard.MaxDisplayNameCharacters
            ? result
            : result.Substring(0, OnlineMessageGuard.MaxDisplayNameCharacters);
    }

    private static bool HasArgument(string value)
    {
        return Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, value, StringComparison.OrdinalIgnoreCase));
    }

    private static ushort ReadPortArgument(ushort fallback = DefaultPort)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "-port", StringComparison.OrdinalIgnoreCase)
                && ushort.TryParse(args[i + 1], out ushort port)) return port;
        }
        return fallback == 0 ? (ushort)DefaultPort : fallback;
    }

    private static DeckDefinition CloneDeck(DeckDefinition source)
    {
        return source == null ? null : JsonUtility.FromJson<DeckDefinition>(JsonUtility.ToJson(source));
    }
}
