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
public sealed partial class OnlineMatchService : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Null in normal sessions; a validation driver can discard a received acknowledgement.
    internal static Func<OnlineMatchService,OnlineEnvelope,bool> ValidationDropClientEnvelope;
#endif
    private const string MainSceneName = "Main";
    private const int DefaultPort = 7777;
    // Two seat snapshots can each approach the envelope limit and fragment into
    // hundreds of UTF-16 packets in one update. UTP's default 128 packet slots
    // exhaust during ordinary action/resync bursts; leave room for both views.
    private const int TransportPacketQueueCapacity = 2048;
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
    public bool IsConnected => localAiSession != null || networkManager != null && networkManager.IsConnectedClient;
    public bool IsDisconnecting => IsManagerShutdownInProgress(shuttingDownManager);
    public bool HasJoinedLobby => HasLobbyIdentity(IsConnected, LocalSeat, LobbyState);
    public bool IsSpectator => LocalSeat > 2;
    public bool IsInMatch { get; private set; }
    public bool IsPreparingMatch { get; private set; }
    public bool HasPendingAction => actionGate.State == OnlineClientInteractionState.Submitting;
    public bool CanSubmitRuleAction => EndReview == null && !IsSpectator && ((IsServer && !IsHost) || !IsInMatch || (IsConnected && actionGate.CanSubmit));
    public OnlineClientInteractionState InteractionState => actionGate.State;
    public string CurrentProfileId => currentProfileId;
    public string CurrentRoomId => currentRoomId;
    public string MatchId => matchId;
    public bool IsReconnecting => reconnectingMatch;
    public double ReconnectGraceRemainingSeconds => clientReconnectWindow.RemainingSeconds(Time.realtimeSinceStartupAsDouble);
    public int LocalSeat { get; private set; }
    public OnlineLobbyState LobbyState { get; private set; }
    private DeckDefinition clientRegisteredDeck;
    public DeckDefinition LocalRegisteredDeck => CloneDeck(clientRegisteredDeck);

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
    private OnlineContentManifest localManifest;
    private OnlineRoomConfig requestedRoomConfig = new OnlineRoomConfig();
    public OnlinePlayerViewState ForgeView { get; private set; }
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
        if(ReplayExportWorker.IsWorker)return;
        string playerName = OnlineClientProfileStore.ReadArgument("-name");
        string normalizedPlayerName = (playerName ?? string.Empty).Replace(" ", string.Empty);
        bool preparedMainEditorServer = string.IsNullOrWhiteSpace(playerName)
            && PlayerPrefs.GetInt(DevelopmentServerFlag, 0) == 1;
        bool developmentServer = string.Equals(normalizedPlayerName, "Player1", StringComparison.OrdinalIgnoreCase)
            || preparedMainEditorServer;
        bool developmentClient = ShouldAutoStartNetworkClient(playerName, HasArgument("-mtgb-client"));
        if (HasArgument("-mtgb-server") || developmentServer)
        {
            if (HasArgument("-mtgb-server")) Application.targetFrameRate = 30;
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
        // Archive open recordings before OnlineMatchSession's quit handler
        // disposes its workers and temporary frame streams.
        Application.quitting+=Shutdown;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.quitting+=Shutdown;
#endif
    }

    private void OnDestroy()
    {
        Application.quitting-=Shutdown;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.quitting-=Shutdown;
#endif
        ClearReplayReceivers();
        Shutdown();
        if (shuttingDownManager != null)
        {
            if (shuttingDownManager.gameObject != null) Destroy(shuttingDownManager.gameObject);
            shuttingDownManager = null;
        }
        roomRegistry.Dispose();
        if (Instance == this) Instance = null;
    }

    private OnlineRoomDirectoryServer roomDirectory;
    private string requestedRoomAction;
    private string directorySnapshot;
    private double nextDirectorySnapshot;
    private bool pendingLobbyDisconnect;
    private string pendingLobbyDisconnectReason;
    private NetworkManager shuttingDownManager;
    private double lobbyConnectDeadline;

    private void Update()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        TickEndReplay(now);
        if(IsLocalAiMatch){UpdateLocalAiMatch();return;}
        TickServerReplayArchives();
        if (lobbyConnectDeadline > 0 && !IsInMatch)
        {
            if (HasJoinedLobby) lobbyConnectDeadline = 0;
            else if (now >= lobbyConnectDeadline)
            {
                lobbyConnectDeadline = 0;
                pendingLobbyDisconnect = true;
                pendingLobbyDisconnectReason = "连接超时，房间可能已关闭或服务器不可达。请刷新房间列表后重试。";
            }
        }
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
            if(roomDirectory!=null) {
                if(now>=nextDirectorySnapshot) {
                    directorySnapshot=JsonUtility.ToJson(new OnlineRoomDirectory { instanceId=roomDirectory.InstanceId,rooms=roomRegistry.Rooms.Where(r=>!r.IsEmpty).Select(r=>OnlineRoomListing.FromRoom(r,IsHost)).ToArray() });nextDirectorySnapshot=now+.2;
                }
                roomDirectory.Pump(directorySnapshot);
            }
            UpdateServerConnections(now);
            UpdateForgeSessions();
        }
        if (IsConnected && networkManager is { IsListening: true } && now >= nextHeartbeatAt)
        {
            nextHeartbeatAt = now + HeartbeatIntervalSeconds;
            SendToServer(OnlineProtocol.Heartbeat, new OnlineHeartbeatMessage { nonce = DateTime.UtcNow.Ticks });
        }
        // BO3 sideboarding has no live game, but still owns its reconnect window.
        UpdateAutomaticReconnect(now);
        if (!IsInMatch) return;
        if (IsClient && actionGate.Tick(now))
        {
            NotifyInteractionStateChanged();
            SetStatus("服务器确认超时，正在请求权威状态。");
            SendToServer(OnlineProtocol.RequestResync, new OnlineHashReportMessage
            {
                appliedSequence = ForgeView?.appliedSequence ?? 0,
                stateHash = ForgeView?.viewHash ?? string.Empty
            });
        }
    }

    public bool StartDedicatedServer(ushort port = DefaultPort)
    {
        connectedToLanHost = false;
        localManifest = OnlineContentManifest.Build();
        if (!CreateNetworkManager("0.0.0.0", port, true)) return false;
        RegisterNetworkCallbacks();
        bool started = networkManager.StartServer();
        if (!started) Shutdown();
        if(started&&!StartRoomDirectory(port)){Shutdown();return false;}
        if(started)InitializeServerReplayArchive();
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
        if (!OnlineMessageGuard.ValidateDisplayName(displayName, out string nameError)) { SetStatus(nameError); return false; }
        pendingLobbyDisconnect = false;
        pendingLobbyDisconnectReason = null;
        connectedToLanHost = true;
        requestedRoomAction="create";
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
        if(started&&!StartRoomDirectory(listenPort)){Shutdown();return false;}
        if(started)InitializeServerReplayArchive();
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
        OnlineRoomConfig roomConfig = null,
        string roomAction = null)
    {
        if (!OnlineMessageGuard.ValidateDisplayName(displayName, out string nameError)) { SetStatus(nameError); return false; }
        requestedRoomAction=roomAction;
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
        if (started)
        {
            nextReconnectAttemptAt = Time.realtimeSinceStartupAsDouble + ReconnectRetrySeconds;
            if (!reconnectingMatch) lobbyConnectDeadline = Time.realtimeSinceStartupAsDouble + 6d;
        }
        else Shutdown();
        SetStatus(started ? $"正在连接 {address}:{port}，房间 {currentRoomId}…" : "无法启动网络客户端。");
        return started;
    }

    public void Disconnect()
    {
        if(LocalSeat>0)OnlineClientProfileStore.SaveReconnectToken(currentProfileId,string.Empty,currentRoomId);
        lobbyConnectDeadline = 0;
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
        if (HasJoinedLobby && deck != null) {
            clientRegisteredDeck=CloneDeck(deck);
            SendToServer(OnlineProtocol.SubmitDeck, new OnlineDeckMessage { deck = CloneDeck(deck) });
        }
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
        if(localAiSession!=null){SubmitLocalAiChoice(-1,null,true);return;}
        if (IsSpectator || !IsConnected || !IsInMatch || !actionGate.CanSubmit) return;
        long requestId = nextRequestId++;
        if (!actionGate.TryBeginSubmit(requestId, Time.realtimeSinceStartupAsDouble, ActionTimeoutSeconds)) return;
        SendToServer(OnlineProtocol.SubmitAction, new OnlineActionMessage {
            matchId = matchId,
            requestId = requestId,
            action = new GameAction { type = GameActionType.Concede, playerId = LocalSeat }
        });
        NotifyInteractionStateChanged();
    }

    public void ChoosePlayDraw(bool playFirst)
    {
        if (HasJoinedLobby && LobbyState.playDrawChooserSeat == LocalSeat)
            SendToServer(OnlineProtocol.ChoosePlayDraw, new OnlinePlayDrawMessage { firstPlayerSeat = playFirst ? LocalSeat : 3 - LocalSeat });
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
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            Destroy(networkManager.gameObject);
            networkManager = null;
            transport = null;
        }
        if (shuttingDownManager != null)
        {
            if (shuttingDownManager.gameObject != null) Destroy(shuttingDownManager.gameObject);
            shuttingDownManager = null;
        }
        // Registration belongs to the manager instance, including automatic reconnects.
        handlersRegistered = false;
        GameObject managerObject = new GameObject("MTGBNetworkManager");
        transport = managerObject.AddComponent<UnityTransport>();
        transport.MaxPacketQueueSize = TransportPacketQueueCapacity;
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
            roomAction=reconnectingMatch?"join":requestedRoomAction,
            reconnectToken = reconnectingMatch || requestedRoomAction!="create"&&requestedRoomAction!="spectate"
                ? OnlineClientProfileStore.GetReconnectToken(currentProfileId,profile.address,profile.port,currentRoomId) : string.Empty,
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
            if (room.RetainsPlayers)
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
        if (IsInMatch || IsCompeting)
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
            try { OnlineEnvelopeCodec.Decode(envelope); }
            catch (Exception) { RejectClient(senderClientId, "malformed-message", "消息编码非法。"); return; }
            HandleServerMessage(senderClientId, envelope);
            return;
        }
        if (IsClient && !serverBound && senderClientId == NetworkManager.ServerClientId)
        {
            try { OnlineEnvelopeCodec.Decode(envelope); }
            catch (Exception) { SetStatus("收到无法解析的网络消息。"); return; }
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
            case OnlineProtocol.ConfigureAi:
            case OnlineProtocol.SetSpectator:
            case OnlineProtocol.SetReady:
            case OnlineProtocol.RequestStartMatch:
            case OnlineProtocol.ChoosePlayDraw:
            case OnlineProtocol.SceneReady:
            case OnlineProtocol.SubmitAction:
            case OnlineProtocol.HashReport:
            case OnlineProtocol.RequestResync:
            case OnlineProtocol.Concede:
            case OnlineProtocol.LeaveReview:
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
            case OnlineProtocol.ConfigureAi: HandleConfigureAi(clientId, Deserialize<OnlineAiMessage>(envelope)); break;
            case OnlineProtocol.SetSpectator: HandleSpectator(clientId, Deserialize<OnlineReadyMessage>(envelope)); break;
            case OnlineProtocol.SetReady: HandleReady(clientId, Deserialize<OnlineReadyMessage>(envelope)); break;
            case OnlineProtocol.RequestStartMatch: HandleStartMatchRequest(clientId); break;
            case OnlineProtocol.ChoosePlayDraw: HandlePlayDraw(clientId, Deserialize<OnlinePlayDrawMessage>(envelope)); break;
            case OnlineProtocol.SceneReady: HandleSceneReady(clientId, Deserialize<OnlineSceneReadyMessage>(envelope)); break;
            case OnlineProtocol.SubmitAction: HandleSubmittedAction(clientId, Deserialize<OnlineActionMessage>(envelope)); break;
            case OnlineProtocol.HashReport: HandleHashReport(clientId, Deserialize<OnlineHashReportMessage>(envelope)); break;
            case OnlineProtocol.RequestResync: HandleResyncRequest(clientId); break;
            case OnlineProtocol.Concede: HandleConcede(clientId); break;
            case OnlineProtocol.LeaveReview:
                if(TryGetMember(clientId,out var reviewRoom,out var reviewMember)){reviewMember.reviewing=false;BroadcastLobby(reviewRoom);}
                break;
            case OnlineProtocol.Heartbeat:
                Send(clientId, OnlineProtocol.HeartbeatAck, Deserialize<OnlineHeartbeatMessage>(envelope));
                break;
            default: RejectClient(clientId, "unknown-message", "未知的协议消息。"); break;
        }
    }

    private void HandleClientMessage(OnlineEnvelope envelope)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(ValidationDropClientEnvelope?.Invoke(this,envelope)==true)return;
#endif
        switch (envelope.type)
        {
            case OnlineProtocol.Welcome: HandleWelcome(Deserialize<OnlineWelcomeMessage>(envelope)); break;
            case OnlineProtocol.ReplayChunk: ReceiveReplay(Deserialize<OnlineReplayChunk>(envelope)); break;
            case OnlineProtocol.Lobby:
                LobbyState = Deserialize<OnlineLobbyState>(envelope);
                if(EndReview!=null&&LobbyState?.series!=null)EndReview.series=CloneSeriesState(LobbyState.series);
                if(!string.IsNullOrEmpty(LobbyState?.series?.completionReason))SetStatus(LobbyState.series.completionReason);
                LobbyChanged?.Invoke(LobbyState);
                if(HasJoinedLobby && LobbyState.playDrawChooserSeat>0){IsPreparingMatch=true;ForgeBattlefieldController.Open(this);}
                else if(IsPreparingMatch && (LobbyState?.players?.Count(p=>p.seat<=2&&p.connected&&p.ready)??0)<2)IsPreparingMatch=false;
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
                if(ended?.ownDeck!=null)clientRegisteredDeck=CloneDeck(ended.ownDeck);
                if (!IsSpectator && ended != null && !ended.aborted) ExpectReplay(ended.matchId);
                IsInMatch = false;
                reconnectingMatch = false;
                automaticReconnectEnabled = false;
                clientReconnectWindow.Reset();
                matchId = string.Empty;
                ForgeView = ended?.finalView ?? ForgeView;
                BeginEndReview(ended);
                if (ended?.series != null)
                {
                    if (LobbyState == null) LobbyState = new OnlineLobbyState { roomId = currentRoomId };
                    LobbyState.series = CloneSeriesState(ended.series);
                }
                // This ends one game, not the authenticated room membership.
                // The same credential also covers sideboarding and a later series.
                actionGate.SetConnected(false);
                NotifyInteractionStateChanged();
                MatchEnded?.Invoke(ended);
                SetStatus(ended?.reason ?? "对局结束。");
                if(IsSpectator&&ended?.finalView!=null&&!ForgeBattlefieldController.IsBattlefieldSceneActive)ForgeBattlefieldController.Open(this);
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
                    ClearLobbyState();
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
        // A restarted client must continue this authenticated member's sequence.
        // The server keeps its replay/duplicate guard across connections and games.
        if(welcome.lastRequestId<long.MaxValue)nextRequestId=Math.Max(nextRequestId,welcome.lastRequestId+1);
        if(welcome.ownDeck!=null&&!IsSpectator)clientRegisteredDeck=CloneDeck(welcome.ownDeck);
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
        else {
            reconnectingMatch=false;automaticReconnectEnabled=false;clientReconnectWindow.Reset();
        }
        SetStatus(IsSpectator ? $"已加入房间 {currentRoomId} 的观战席。" : $"已作为玩家 {LocalSeat} 加入房间 {currentRoomId}。");
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
            RejectClient(clientId, "protocol-mismatch", manifestError);
            return;
        }
        bool roomExisted = roomRegistry.TryGet(hello.roomId, out _);
        if(string.IsNullOrEmpty(hello.reconnectToken) && ((hello.roomAction=="create"&&roomExisted)||((hello.roomAction=="join"||hello.roomAction=="spectate")&&!roomExisted))) {
            RejectClient(clientId,"room-unavailable",roomExisted?"此房间名称已存在，请换一个名称。":"房间已关闭，请刷新列表。");return;
        }
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
            if (room.members.Count >= 34) { RejectClient(clientId,"room-full","观战席已满。"); return; }
            // 新加入时回收已消失的占座成员（连接不在列表或心跳租约过期），
            // 否则离开过的玩家会一直被“房间已满”拒绝
            var liveIds = new HashSet<ulong>(networkManager.ConnectedClientsIds);
            if (room.match == null && PruneGoneLobbyMembers(room, liveIds, now))
            {
                roomRegistry.RemoveIfEmpty(room.roomId);
                BroadcastLobby(room);
                if (!roomRegistry.TryGet(room.roomId, out room)) return;
            }
            int seat = !room.SpectatorOnly && hello.roomAction != "spectate" ? (!room.members.ContainsKey(1) ? 1 : !room.members.ContainsKey(2) ? 2 : 0) : 0;
            if (seat == 0) seat = Enumerable.Range(3, 32).First(s => !room.members.ContainsKey(s));
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
            lastRequestId = member.requestGate.LastRequestId,
            ownDeck = member.IsSpectator ? null : CloneDeck(member.deck),
            lanHosted = IsHost,
            roomId = room.roomId,
            manifest = localManifest
        });
        BroadcastLobby(room);
        if (resuming || (member.IsSpectator && room.match != null)) SendStartMatch(room, member);
        else if(member.IsSpectator&&room.publicReview!=null&&room.SpectatorOnly) {
            var review=room.SpectatorReview(member.seat);
            if(review!=null){member.reviewing=true;Send(member.clientId,OnlineProtocol.MatchEnded,review);}
        }
    }

    private readonly Dictionary<ulong,long> companionDeckChecks=new();
    private readonly Dictionary<ulong,OnlineReadyMessage> companionPendingReady=new();
    private long companionDeckCheckSequence;
    private async void HandleDeck(ulong clientId, OnlineDeckMessage message)
    {
        if (!TryGetMember(clientId, out OnlineRoomState room, out OnlineRoomMember member) || room.match != null || member.IsSpectator) return;
        long check=++companionDeckCheckSequence;companionDeckChecks[clientId]=check;companionPendingReady.Remove(clientId);
        if(message?.deck!=null)message.deck.format=room.config.formatId;
        if (!ValidateDeck(message?.deck, SeriesValidationConfig(room,member), out string error))
        {
            companionDeckChecks.Remove(clientId);
            Send(clientId, OnlineProtocol.Error, new OnlineErrorMessage { code = "invalid-deck", message = error });
            return;
        }
        if ((room.series?.betweenGames == true || member.seriesDeckChanged && room.series?.finished != true)
            && member.registeredDeck != null
            && !ValidateSideboardChange(member.registeredDeck, message.deck, SeriesValidationConfig(room,member), out error))
        {
            companionDeckChecks.Remove(clientId);
            Send(clientId, OnlineProtocol.Error, new OnlineErrorMessage { code = "invalid-sideboard", message = error });
            return;
        }
        member.ready=false;
        if(DeckCommander.IsFormat(room.config.formatId)){
            BroadcastLobby(room);
            var inspected=CloneDeck(message.deck);inspected.format=room.config.formatId;
            DeckCommanderInspector.Result result;
            try{result=await DeckCommanderInspector.Inspect(inspected);}catch(Exception){result=new DeckCommanderInspector.Result{error="engine"};}
            if(this==null||!companionDeckChecks.TryGetValue(clientId,out long latest)||latest!=check)return;
            if(!TryGetMember(clientId,out var currentRoom,out var currentMember)||currentRoom!=room||currentMember!=member||room.match!=null){companionDeckChecks.Remove(clientId);companionPendingReady.Remove(clientId);return;}
            if(result.error!=null||!string.IsNullOrEmpty(result.problem)){
                companionDeckChecks.Remove(clientId);companionPendingReady.Remove(clientId);
                Send(clientId,OnlineProtocol.Error,new OnlineErrorMessage{code="invalid-commander",message=DeckCommanderInspector.Status(result)});BroadcastLobby(room);return;
            }
            if(room.config.formatId=="duelcommander"&&room.series?.betweenGames==true&&member.registeredDeck!=null){
                DeckCommanderInspector.Result original;
                try{original=await DeckCommanderInspector.Inspect(member.registeredDeck);}catch(Exception){original=new DeckCommanderInspector.Result{error="engine"};}
                if(this==null||!companionDeckChecks.TryGetValue(clientId,out latest)||latest!=check)return;
                if(!TryGetMember(clientId,out currentRoom,out currentMember)||currentRoom!=room||currentMember!=member||room.match!=null){companionDeckChecks.Remove(clientId);companionPendingReady.Remove(clientId);return;}
                if(original.error!=null||original.identity!=result.identity){
                    companionDeckChecks.Remove(clientId);companionPendingReady.Remove(clientId);
                    Send(clientId,OnlineProtocol.Error,new OnlineErrorMessage{code="invalid-commander-swap",message="局间更换主将不能改变原注册牌组的标识色。"});BroadcastLobby(room);return;
                }
            }
        }
        if(DeckCompanion.Has(message.deck)){
            BroadcastLobby(room);
            DeckCompanionInspector.Result result;
            try{result=await DeckCompanionInspector.Inspect(message.deck);}catch(Exception){result=new DeckCompanionInspector.Result{error="engine"};}
            if(this==null||!companionDeckChecks.TryGetValue(clientId,out long latest)||latest!=check)return;
            if(!TryGetMember(clientId,out var currentRoom,out var currentMember)||currentRoom!=room||currentMember!=member||room.match!=null){companionDeckChecks.Remove(clientId);companionPendingReady.Remove(clientId);return;}
            if(result.error!=null||!result.Allows(message.deck)){
                companionDeckChecks.Remove(clientId);companionPendingReady.Remove(clientId);
                Send(clientId,OnlineProtocol.Error,new OnlineErrorMessage{code="invalid-companion",message=DeckCompanionInspector.Status(result,false)});BroadcastLobby(room);return;
            }
        }
        companionDeckChecks.Remove(clientId);
        member.deck = CloneDeck(message.deck);
        member.deck.format=room.config.formatId;
        if (room.series?.finished == true) { member.registeredDeck = null; member.seriesOriginalDeck = null; member.seriesDeckChanged = false; }
        member.ready = false;
        BroadcastLobby(room);
        if(companionPendingReady.TryGetValue(clientId,out var queued)){companionPendingReady.Remove(clientId);HandleReady(clientId,queued);}
    }

    public void SetSpectator(bool spectator)
    {
        if (HasJoinedLobby && !IsInMatch) SendToServer(OnlineProtocol.SetSpectator, new OnlineReadyMessage { ready = spectator });
    }

    private void HandleSpectator(ulong clientId, OnlineReadyMessage message)
    {
        if (!TryGetMember(clientId, out var room, out var member) || room.match != null || room.series.betweenGames) return;
        bool spectator = message?.ready == true;
        if (spectator == member.IsSpectator) return;
        int newSeat = spectator ? Enumerable.Range(3,32).FirstOrDefault(s=>!room.members.ContainsKey(s))
            : !room.members.ContainsKey(1) ? 1 : !room.members.ContainsKey(2) ? 2 : 0;
        if (newSeat == 0) { Send(clientId, OnlineProtocol.Error, new OnlineErrorMessage { message="席位已满。" }); return; }
        bool host = room.hostSeat == member.seat;
        room.members.Remove(member.seat);
        member.seat=newSeat; member.ready=false; member.deck=null; member.registeredDeck=null;
        room.members.Add(newSeat,member); clientLocations[clientId].seat=newSeat;
        if(host)room.hostSeat=newSeat;
        Send(clientId,OnlineProtocol.Welcome,new OnlineWelcomeMessage { seat=newSeat, roomId=room.roomId,
            reconnectToken=member.reconnectToken,lanHosted=IsHost,manifest=localManifest });
        BroadcastLobby(room);
    }

    private void HandleReady(ulong clientId, OnlineReadyMessage message)
    {
        if (!TryGetMember(clientId, out OnlineRoomState room, out OnlineRoomMember member) || room.match != null || member.IsSpectator) return;
        if(companionDeckChecks.ContainsKey(clientId)){companionPendingReady[clientId]=message;return;}
        member.ready = message?.ready == true && member.deck != null;
        if (!member.ready) room.playDrawChooserSeat = 0;
        BroadcastLobby(room);
        if (room.series.betweenGames && CanStartRoomMatch(room, room.hostSeat, out _)) StartRoomMatch(room);
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
        if (room == null || room.match != null || room.aiPending)
        {
            error = "房间当前不能开始新对局。";
            return false;
        }
        if (requesterSeat != room.hostSeat)
        {
            error = "只有房主可以开始比赛。";
            return false;
        }
        if (!room.members.ContainsKey(1) || !room.members.ContainsKey(2)
            || !room.members.Values.Where(item=>!item.IsSpectator).All(item => item.connected && item.ready && item.deck != null))
        {
            error = "需要两名玩家都提交牌组并准备。";
            return false;
        }
        // Dedicated hosts can cap concurrent games without limiting ordinary LAN hosts.
        if (HasArgument("-mtgb-server") && int.TryParse(OnlineClientProfileStore.ReadArgument("-max-matches"), out int maxMatches)
            && maxMatches > 0 && Instance != null
            && Instance.roomRegistry.Rooms.Count(other => other != room && (other.match != null || other.playDrawChooserSeat != 0)) >= maxMatches)
        {
            error = "服务器当前对局数量已达上限，请稍后再试。";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private void StartRoomMatch(OnlineRoomState room)
    {
        if (room.playDrawChooserSeat != 0) return;
        if (room.series.finished)
        {
            room.series.Reset(room.config.bestOf);
            RestoreSeriesDecks(room);
        }
        if (room.config.bestOf == 3)
        {
            room.playDrawChooserSeat = room.series.nextChooserSeat == 0 ? UnityEngine.Random.Range(1, 3) : room.series.nextChooserSeat;
            room.series.nextChooserSeat = room.playDrawChooserSeat;
            if (room.members[room.playDrawChooserSeat].isAi)
            {
                int first = room.playDrawChooserSeat;
                room.playDrawChooserSeat = 0;
                BeginRoomGame(room, first);
                return;
            }
            BroadcastLobby(room);
            return;
        }
        BeginRoomGame(room, UnityEngine.Random.Range(1, 3));
    }

    private void HandlePlayDraw(ulong clientId, OnlinePlayDrawMessage message)
    {
        if (!TryGetMember(clientId, out var room, out var member) || room.match != null
            || member.seat != room.playDrawChooserSeat || member.IsSpectator
            || message == null || (message.firstPlayerSeat != 1 && message.firstPlayerSeat != 2)) return;
        if (!CanStartRoomMatch(room, room.hostSeat, out _)) { room.playDrawChooserSeat = 0; BroadcastLobby(room); return; }
        room.playDrawChooserSeat = 0;
        BeginRoomGame(room, message.firstPlayerSeat);
    }

    private void BeginRoomGame(OnlineRoomState room, int firstPlayerSeat)
    {
        try
        {
            if (room.series == null) room.series = new OnlineSeriesState();
            if (room.series.finished)
            {
                room.series.Reset(room.config.bestOf);
                RestoreSeriesDecks(room);
            }
            foreach (OnlineRoomMember member in room.members.Values)
            {
                if (member.registeredDeck == null) { member.registeredDeck = CloneDeck(member.deck); member.seriesOriginalDeck = CloneDeck(member.deck); }
            }
            room.series.BeginGame();
            int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            OnlineRoomConfig matchConfig = CloneRoomConfig(room.config);
            matchConfig.firstPlayerSeat = firstPlayerSeat;
            matchConfig.authorizedAuxiliarySeriesPools = new[] {
                room.members[1].seriesDeckChanged ? room.members[1].registeredDeck : null,
                room.members[2].seriesDeckChanged ? room.members[2].registeredDeck : null
            };
            room.match = new OnlineMatchSession(Guid.NewGuid().ToString("N"),
                room.members[1].deck, room.members[2].deck, seed, matchConfig, room.roomId, room.series.gameNumber,
                room.members.Values.FirstOrDefault(m => m.isAi)?.seat ?? 0);
            room.publicReview=null;
            foreach (OnlineRoomMember member in room.members.Values)
            {
                member.reviewing=false;
                member.sceneReady = member.isAi;
                member.restoring = false;
                SendStartMatch(room, member);
            }
            RefreshServerMatchFlag();
            SetRoomStatus(room, "对局已开始。");
        }
        catch (Exception exception)
        {
            // Return both clients from the preparation board if native startup
            // fails; a cleared chooser alone is indistinguishable from loading.
            foreach(var member in room.members.Values)member.ready=member.isAi;
            BroadcastLobby(room);
            SetRoomStatus(room, $"无法开始对局：{exception.Message}");
        }
    }

    private void SendStartMatch(OnlineRoomState room, OnlineRoomMember member)
    {
        if (member.isAi) return;
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
        IsPreparingMatch=false;
        if (message?.view == null) return;
        ClearEndReview();
        LocalSeat = message.localSeat;
        matchId = message.matchId ?? string.Empty;
        OnlinePlayerContext.SetLocalPlayer(LocalSeat);
        IsInMatch = true;
        ForgeView=null;
        if(!ApplyPlayerView(message.view,out string openingError)) {
            actionGate.BeginResync();NotifyInteractionStateChanged();
            SetStatus("起手状态校验失败，正在重新同步："+openingError);
            ForgeBattlefieldController.Open(this);
            SendToServer<OnlineHashReportMessage>(OnlineProtocol.RequestResync,null);return;
        }
        actionGate.SetConnected(true);
        if (reconnectingMatch) actionGate.BeginResync();
        NotifyInteractionStateChanged();
        SetStatus(reconnectingMatch ? "正在恢复对局视图。" : "正在载入权威对局视图。");
        ForgeBattlefieldController.Open(this);
        SendToServer(OnlineProtocol.SceneReady, new OnlineSceneReadyMessage { initialStateHash = ForgeView.viewHash });
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
        if (message?.matchId != room.match.MatchId)
        {
            RejectAction(clientId, message?.requestId ?? 0, "Action belongs to a different match.");
            return;
        }
        if (member.IsSpectator) { RejectAction(clientId, message?.requestId ?? 0, "观战者不能提交对局操作。"); return; }
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
        if (!member.sceneReady || room.members.Values.Any(item => !item.IsSpectator && item.connected && !item.sceneReady))
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
        foreach (OnlineRoomMember recipient in room.members.Values.Where(item => item.connected && !item.isAi))
        {
            OnlinePlayerViewState view = room.match.GetViewForTransport(recipient.seat);
            Send(recipient.clientId, OnlineProtocol.StateUpdate, new OnlineStateUpdateMessage
            {
                requestId = recipient.clientId == clientId ? message.requestId : 0,
                result = GameActionResult.Accepted(view.appliedSequence, view.viewHash),
                view = view
            });
        }
        string viewHash = room.match.GetViewForTransport(member.seat)?.viewHash ?? string.Empty;
        SetRoomStatus(room,
            $"玩家 {member.seat} 已应用动作 seq={room.match.AppliedSequence} type={action.type} hash={ShortHash(viewHash)}。");
        MatchOutcome outcome = room.match.Outcome;
        if (outcome.isFinished) EndRoomMatch(room, outcome.winnerPlayerId, outcome.message);
    }

    private void ApplyStateUpdate(OnlineStateUpdateMessage message)
    {
        if (message?.view == null) return;
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
        if (message?.view == null) return;
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
            appliedSequence = ForgeView.appliedSequence,
            stateHash = ForgeView.viewHash
        });
    }

    private bool ApplyPlayerView(OnlinePlayerViewState view, out string error)
    {
        error = string.Empty;
        if (view?.forge == null || view.viewerSeat != LocalSeat || view.matchId != matchId)
        { error = "Invalid Forge match or seat projection."; return false; }
        if (!view.HasValidHash()) { error = "Forge view hash mismatch."; return false; }
        if (ForgeView != null && view.appliedSequence < ForgeView.appliedSequence) { error = "Stale Forge view."; return false; }
        ForgeView = view;
        return true;
    }

    private void HandleConcede(ulong clientId)
    {
        RejectAction(clientId, 0, "Concession requires a numbered action request.");
    }

    private void EndRoomMatch(OnlineRoomState room, int winnerSeat, string reason, bool aborted = false,bool finishSeries = false)
    {
        if (room?.match == null) return;
        room.series ??= new OnlineSeriesState();
        int replayGame = room.series.gameNumber;
        if (!aborted) {
            try { ApplySeriesRemovals(room); }
            catch (Exception error) { aborted = true; reason = "比赛内卡池变更无效：" + error.Message; }
        }
        if (!aborted) room.series.RecordGameResult(winnerSeat);
        // An engine failure does not complete a game or open a sideboarding window.
        else room.series.betweenGames = false;
        if(finishSeries)room.series.Forfeit(winnerSeat,reason);
        var ended = new OnlineMatchEndedMessage
        {
            aborted = aborted,
            matchId = room.match.MatchId,
            winnerSeat = winnerSeat,
            reason = string.IsNullOrWhiteSpace(reason) ? "对局结束。" : reason,
            series = CloneSeriesState(room.series)
        };
        foreach (var member in room.members.Values.Where(m => m.connected && !m.isAi))
        {
            var personal = ServerSnapshotJson.Copy(ended);
            member.reviewing=true;
            personal.finalView = room.match.GetView(member.seat);
            personal.ownDeck = member.IsSpectator ? null : CloneDeck(member.deck);
            Send(member.clientId, OnlineProtocol.MatchEnded, personal);
        }
        try
        {
            var players = Enumerable.Range(1, 2).Select(seat => room.members.TryGetValue(seat, out var member)
                ? member.displayName : "Player " + seat).ToArray();
            var replay = room.match.CompleteReplay(players, replayGame, room.series.bestOf, winnerSeat, ended.reason);
            if(replay.FrameCount>0)
                StartCoroutine(SendCompletedReplay(replay, aborted ? Array.Empty<ulong>() : room.members.Values.Where(m => m.connected && !m.IsSpectator && !m.isAi).Select(m => m.clientId).ToArray()));
            else {replay.Dispose();Debug.LogWarning("对局尚无可回放画面，未生成录像："+replay.Metadata.matchId);}
        }
        catch (Exception error) { Debug.LogWarning("录像生成失败：" + error.Message); }
        try
        {
            System.IO.File.AppendAllText(System.IO.Path.Combine(Application.persistentDataPath, "ForgeMatchHistory.jsonl"),
                ServerSnapshotJson.Serialize(ended) + Environment.NewLine);
        }
        catch (Exception exception) { Debug.LogError("无法保存 Forge 对局结束记录：" + exception.Message); }
        SetRoomStatus(room, $"对局结束，胜者席位 {winnerSeat}：{ended.reason}");
        room.publicReview=ServerSnapshotJson.Copy(ended);
        room.publicReview.finalView=room.match.GetView(3);
        room.match.Dispose();
        room.match = null;
        foreach (OnlineRoomMember member in room.members.Values)
        {
            member.ready = member.isAi;
            member.sceneReady = member.isAi;
            member.restoring = false;
            if(member.connected || !room.series.betweenGames)member.reconnectWindow.Reset();
        }
        foreach (int seat in room.members.Where(pair => !pair.Value.connected && (!room.series.betweenGames || pair.Value.IsSpectator)).Select(pair => pair.Key).ToList())
            room.members.Remove(seat);
        RemoveOrphanedAi(room);
        EnsureRoomHost(room);
        BroadcastLobby(room);
        roomRegistry.RemoveIfEmpty(room.roomId);
        RefreshServerMatchFlag();
    }

    private void UpdateForgeSessions()
    {
        foreach (OnlineRoomState room in roomRegistry.Rooms.ToList())
        {
            if (room.match == null || !room.match.Poll()) continue;
            if (room.match.Failure != null) { EndRoomMatch(room, 0, room.match.Failure, true); continue; }
            foreach (OnlineRoomMember recipient in room.members.Values.Where(item => item.connected && !item.isAi))
            {
                var view = room.match.GetViewForTransport(recipient.seat);
                Send(recipient.clientId, OnlineProtocol.StateUpdate, new OnlineStateUpdateMessage {
                    view = view, result = GameActionResult.Accepted(view.appliedSequence, view.viewHash)
                });
            }
            MatchOutcome outcome = room.match.Outcome;
            if (outcome.isFinished) EndRoomMatch(room, outcome.winnerPlayerId, outcome.message);
        }
    }

    public ForgeControlMode ControlMode { get; set; } = ForgeControlMode.Auto;

    public void SubmitForgeOption(int optionIndex)
        => SubmitForgeChoice(optionIndex, null);

    public void SubmitForgeOrder(int optionIndex, int[] order)
        => SubmitForgeChoice(optionIndex, order);

    private void SubmitForgeChoice(int optionIndex, int[] order)
    {
        if(localAiSession!=null){SubmitLocalAiChoice(optionIndex,order,false);return;}
        ForgeRequest request = ForgeView?.forge?.awaitingInput == true ? ForgeView.forge.request : null;
        if (request == null || !IsConnected || !actionGate.CanSubmit) return;
        long requestId = nextRequestId++;
        if (!actionGate.TryBeginSubmit(requestId, Time.realtimeSinceStartupAsDouble, ActionTimeoutSeconds)) return;
        SendToServer(OnlineProtocol.SubmitAction, new OnlineActionMessage {
            matchId = matchId,
            requestId = requestId,
            action = new GameAction { type = GameActionType.ConfirmChoice, playerId = LocalSeat,
                sourceObjectId = request.requestId, optionId = optionIndex,
                optionIds = order == null ? new List<int>() : new List<int>(order) }
        });
        NotifyInteractionStateChanged();
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
            if (room.RetainsPlayers)
            {
                foreach(var gone in room.members.Values.Where(m=>!m.isAi && m.connected && m.clientId!=NetworkManager.ServerClientId && !connectedIds.Contains(m.clientId)).ToArray())OnClientDisconnected(gone.clientId);
                foreach(var observer in room.members.Values.Where(m=>m.IsSpectator&&!m.connected&&m.reconnectWindow.IsExpired(now)).ToArray())room.members.Remove(observer.seat);
                OnlineRoomMember expired = room.members.Values.FirstOrDefault(item =>
                    !item.IsSpectator && !item.connected && item.reconnectWindow.IsExpired(now));
                if (expired == null) continue;
                OnlineRoomMember opponent = room.members.Values.FirstOrDefault(item => !item.IsSpectator && item.connected);
                string reason=$"选手未在 {ReconnectGraceSeconds:0} 秒内重连，本场比赛按弃权结束。";
                if(room.match!=null)EndRoomMatch(room,opponent?.seat??0,reason,opponent==null,true);
                else EndDisconnectedSeries(room,opponent?.seat??0,reason);
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
        if(room.RetainsPlayers)return false;
        bool changed = false;
        foreach (OnlineRoomMember member in room.members.Values.ToList())
        {
            if (member.isAi || member.clientId == NetworkManager.ServerClientId) continue; // AI 或主机本地成员
            bool connectionGone = !connectedIds.Contains(member.clientId);
            bool leaseExpired = !messageGuard.WasSeenWithin(member.clientId, now, LobbyLeaseSeconds);
            if (!connectionGone && !leaseExpired) continue;
            room.members.Remove(member.seat);
            clientLocations.Remove(member.clientId);
            messageGuard.Remove(member.clientId);
            if (!connectionGone) networkManager.DisconnectClient(member.clientId, "Lobby lease expired");
            changed = true;
        }
        if (changed) { RemoveOrphanedAi(room); EnsureRoomHost(room); }
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
            OnlineClientProfileStore.SaveReconnectToken(currentProfileId,string.Empty,currentRoomId);
            ClearLobbyState();
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
        // Membership/review/series changes invalidate the directory before
        // its next TCP request; a cached listing must not lag a lobby event.
        nextDirectorySnapshot=0;
        if (room.playDrawChooserSeat != 0 && !CanStartRoomMatch(room, room.hostSeat, out _)) room.playDrawChooserSeat = 0;
        SendToRoom(room, OnlineProtocol.Lobby, BuildLobbyState(room));
    }

    private static OnlineLobbyState BuildLobbyState(OnlineRoomState room)
    {
        EnsureRoomHost(room);
        var state = new OnlineLobbyState
        {
            roomId = room.roomId,
            hostSeat = room.hostSeat,
            playDrawChooserSeat = room.playDrawChooserSeat,
            aiPending = room.aiPending,
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
                isAi = member.isAi,
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
            && !current.isAi && current.connected && (!room.RetainsPlayers || !current.IsSpectator))
        {
            return;
        }
        OnlineRoomMember next = room.members.Values
            .Where(item => item != null && !item.isAi && item.connected && (!room.RetainsPlayers || !item.IsSpectator))
            .OrderBy(item => item.seat)
            .FirstOrDefault();
        room.hostSeat = next?.seat ?? 0;
    }

    private static bool RemoveLobbyMember(OnlineRoomState room, int seat)
    {
        if (room == null || !room.members.Remove(seat)) return false;
        room.aiRevision++; room.aiPending = false;
        RemoveOrphanedAi(room);
        EnsureRoomHost(room);
        return true;
    }

    private void SendToRoom<T>(OnlineRoomState room, string type, T payload)
    {
        if (room == null) return;
        foreach (OnlineRoomMember member in room.members.Values)
            if (member.connected && !member.isAi) Send(member.clientId, type, payload);
    }

    private void SendToServer<T>(string type, T payload)
    {
        if (networkManager != null && networkManager.IsListening && networkManager.IsClient)
            Send(NetworkManager.ServerClientId, type, payload);
    }

    private void Send<T>(ulong clientId, string type, T payload)
    {
        if (clientId == AiClientId) return;
        if (networkManager?.CustomMessagingManager == null) return;
        string payloadJson = payload == null ? string.Empty : payload is OnlineStateUpdateMessage or OnlineStartMatchMessage or OnlineResyncMessage or OnlineMatchEndedMessage ? ServerSnapshotJson.Serialize(payload) : JsonUtility.ToJson(payload);
        string json = JsonUtility.ToJson(OnlineEnvelopeCodec.Encode(type, payloadJson));
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
        try { return typeof(T)==typeof(OnlineStateUpdateMessage)||typeof(T)==typeof(OnlineStartMatchMessage)||typeof(T)==typeof(OnlineResyncMessage)||typeof(T)==typeof(OnlineMatchEndedMessage) ? ServerSnapshotJson.Deserialize<T>(envelope.payload) : JsonUtility.FromJson<T>(envelope.payload); }
        catch { return null; }
    }

    private readonly DeckFormatLegality roomLegality = new DeckFormatLegality();
    private bool ValidateDeck(DeckDefinition deck, OnlineRoomConfig config, out string error)
    {
        if (!OnlineMessageGuard.ValidateDeckShape(deck, out error)) return false;
        IReadOnlyList<CardDefinition> cards = CardDatabaseRepository.LoadAll();
        string format = string.IsNullOrEmpty(config?.formatId) ? "none" : config.formatId;
        bool commandFormat=format=="duelcommander"||format=="brawl";
        if(!commandFormat&&DeckCommander.Has(deck)){error="当前房间赛制不使用主将。";return false;}
        if(commandFormat){
            if(deck.commanders==null||deck.cards==null||deck.sideboard==null){error="主将牌组分区缺失。";return false;}
            int commanders=deck.commanders.Sum(e=>e.count);
            if(commanders<1||commanders>(format=="brawl"?1:2)||deck.commanders.Any(e=>e.count!=1)){
                error=format=="brawl"?"争锋必须指定一位主将。":"法禁必须指定一位主将或合法的共同主将。";return false;
            }
            if(deck.cards.Sum(e=>e.count)+commanders!=100){error="主牌与主将合计必须为100张。";return false;}
            if(deck.sideboard.Sum(e=>e.count)!=(DeckCompanion.Has(deck)?1:0)){
                error="此赛制没有备牌；统帅区外只可保留指定的行侣。";return false;
            }
            if((deck.attractions?.Count??0)>0||(deck.contraptions?.Count??0)>0){error="此赛制不使用游乐设施或装置辅助牌组。";return false;}
        }
        if(!DeckCompanion.ValidateReference(deck,cards,out error))return false;
        bool authorizedPool=config?.authorizedSeriesPool!=null&&HaveSameCardPool(config.authorizedSeriesPool,deck)
            &&HaveSameEntries(config.authorizedSeriesPool.commanders,deck.commanders);
        if (!DeckAuxiliary.Validate(deck, cards, out error,authorizedPool ? config.authorizedSeriesPool : null)) return false;
        if (Array.IndexOf(DeckFormatLegality.OnlineKeys, format) < 0) { error = "不支持的房间赛制。"; return false; }
        if (format != "none")
        {
            try { roomLegality.PrepareForValidation(format, cards);if(format=="duelcommander")roomLegality.PrepareForValidation("vintage",cards); }
            catch (Exception e) { error = "无法读取赛制规则：" + e.Message; return false; }
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in EnumerateDeckEntries(deck))
            {
                var card = DeckRepository.ResolveCard(entry, cards);
                if (card == null || card.customContent) continue; // Custom content has its separate room permission below.
                counts.TryGetValue(card.cardName, out int count); counts[card.cardName] = count + entry.count;
                string legality = roomLegality.Get(card, format, DeckFormatLegality.Current, deck.commanders.Contains(entry), DeckCompanion.IsSelected(deck,entry));
                if(format=="duelcommander"&&roomLegality.Get(card,"vintage",DeckFormatLegality.Current) is "not_legal" or "banned" or "unknown"){
                    error=$"卡牌 {card.cardName} 不属于法禁的纸牌卡池。";return false;
                }
                if (legality == "banned" || legality == "not_legal" || legality == "unknown" || legality == "restricted" && counts[card.cardName] > 1 && !authorizedPool)
                { error = $"卡牌 {card.cardName} 不符合房间赛制。"; return false; }
            }
        }
        foreach (DeckEntry entry in EnumerateDeckEntries(deck))
        {
            if (DeckRepository.ResolveCard(entry, cards) == null)
            { error = $"无法唯一映射牌组条目 {(string.IsNullOrWhiteSpace(entry.PrintingKey) ? "旧本地 ID " + entry.cardId : entry.PrintingKey)}；请修正缺失或歧义条目。"; return false; }
        }
        DeckDefinition copy = CloneDeck(deck);
        copy.format=format;
        if(!authorizedPool)DeckRepository.EnforceCopyLimits(copy, cards);
        copy.format=deck.format;
        List<CardDefinition> runtime = DeckRepository.BuildRuntimeDeck(copy, cards, out string warning);
        // Only the host's series-removal config can lower the minimum to zero.
        // An emptied registered pool must reach Forge's normal empty-library loss.
        if (runtime.Count == 0 && config?.minimumMainDeckSize != 0)
        {
            error = string.IsNullOrEmpty(warning) ? "牌组没有有效卡牌。" : warning;
            return false;
        }
        int mainCount = DeckRepository.GetTotalCount(deck, false);
        int sideboardCount = DeckRepository.GetTotalCount(deck, true);
        int minimumMain = commandFormat?100-deck.commanders.Sum(e=>e.count):Math.Max(0, config?.minimumMainDeckSize ?? 60);
        int maximumSideboard = commandFormat?(DeckCompanion.Has(deck)?1:0):Math.Max(0, config?.maximumSideboardSize ?? 15);
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
        if(config?.formatId=="duelcommander"){
            if(!CountsEqual(BuildEntryCounts(registeredDeck.cards,registeredDeck.commanders),BuildEntryCounts(submittedDeck.cards,submittedDeck.commanders))
                ||!HaveSameEntries(registeredDeck.sideboard,submittedDeck.sideboard)
                ||!HaveSameEntries(registeredDeck.attractions,submittedDeck.attractions)
                ||!HaveSameEntries(registeredDeck.contraptions,submittedDeck.contraptions)
                ||!HaveSameEntries(DeckCompanion.Has(registeredDeck)?new List<DeckEntry>{registeredDeck.companion}:null,DeckCompanion.Has(submittedDeck)?new List<DeckEntry>{submittedDeck.companion}:null)){
                error="法禁局间只可从原注册的100张牌中更换主将，其他卡池和行侣必须保持不变。";return false;
            }
            return true;
        }
        if (!HaveSameEntries(registeredDeck.commanders, submittedDeck.commanders)
            || !HaveSameEntries(registeredDeck.attractions, submittedDeck.attractions)
            || !HaveSameEntries(registeredDeck.contraptions, submittedDeck.contraptions)
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
        return CountsEqual(firstPool, secondPool)
            && HaveSameEntries(first?.attractions, second?.attractions)
            && HaveSameEntries(first?.contraptions, second?.contraptions);
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
        if (deck?.attractions != null)
            for (int i = 0; i < deck.attractions.Count; i++) if (deck.attractions[i] != null) yield return deck.attractions[i];
        if (deck?.contraptions != null)
            for (int i = 0; i < deck.contraptions.Count; i++) if (deck.contraptions[i] != null) yield return deck.contraptions[i];
    }

    private static OnlineRoomConfig SanitizeRoomConfig(OnlineRoomConfig source)
    {
        var result = new OnlineRoomConfig
        {
            startingLife = Math.Max(1, Math.Min(200, source?.startingLife ?? 20)),
            firstPlayerSeat = 0,
            bestOf = source?.bestOf == 3 ? 3 : 1,
            formatId = string.IsNullOrEmpty(source?.formatId) ? "none" : source.formatId,
            minimumMainDeckSize = Math.Max(1, Math.Min(250, source?.minimumMainDeckSize ?? 60)),
            maximumSideboardSize = Math.Max(0, Math.Min(30, source?.maximumSideboardSize ?? 15)),
            allowCustomContent = source?.allowCustomContent == true,
            useAnte = source?.useAnte == true,
            noShuffle = source?.noShuffle == true
        };
        result.useAnte &= OnlineRoomConfig.AllowsAnte(result.formatId, result.allowCustomContent);
        if(result.formatId=="duelcommander"||result.formatId=="brawl"){
            result.startingLife=result.formatId=="brawl"?25:20;
            result.minimumMainDeckSize=99;result.maximumSideboardSize=1;
            result.allowCustomContent=false;result.useAnte=false;
            if(result.formatId=="brawl")result.bestOf=1;
        }
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

    private bool StartRoomDirectory(ushort port)
    {
        roomDirectory?.Dispose();roomDirectory=null;
        try {roomDirectory=new OnlineRoomDirectoryServer(port);nextDirectorySnapshot=0;return true;}
        catch(System.Net.Sockets.SocketException){SetStatus("房间查询端口已被占用，请更换端口后建立房间。");return false;}
    }
    private void Shutdown()
    {
        StopLocalAiMatch();
        ArchiveOpenServerMatches();
        FlushServerReplayArchives();
        declinedReviewReplays.Clear();
        roomDirectory?.Dispose();roomDirectory=null;
        ClearReplayReceivers();
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
        IsPreparingMatch=false;
        clientRegisteredDeck=null;
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
