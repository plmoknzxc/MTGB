using System;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Opt-in player-build acceptance driver. Never enabled in normal player sessions.</summary>
public sealed class ForgeNetworkSmoke : MonoBehaviour
{
    private OnlineMatchService service;
    private string role, report;
    private double deadline, nextAction;
    private int games, actions;
    private int bestOf = 3;
    private bool submitted, starting;
    private bool reconnectStarted, reconnectObserved, reconnectVerified, sideboardChanged;
    private long reconnectChoice;
    private string reconnectViewHash;
    private DeckDefinition deck;
    private int mulligans;
    private bool openingVerified;
    private int packetQueueCapacity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!string.IsNullOrEmpty(OnlineClientProfileStore.ReadArgument("-forge-smoke")))
            new GameObject("ForgeNetworkSmoke").AddComponent<ForgeNetworkSmoke>();
    }
    private void Start()
    {
        DontDestroyOnLoad(gameObject);
        role = OnlineClientProfileStore.ReadArgument("-forge-smoke");
        report = OnlineClientProfileStore.ReadArgument("-forge-report");
        deadline = Time.realtimeSinceStartupAsDouble + 180;
        service = OnlineMatchService.EnsureInstance();
        service.StatusChanged += text => {
            Debug.Log("Forge smoke: " + text);
            if (role == "incompatible" && text.Contains("通信协议版本不兼容"))
                Complete(true, "Server rejected an incompatible wire protocol during handshake.");
        };
        service.MatchEnded += Ended;
        service.ConnectionChanged += connected => { if (reconnectStarted && !connected) reconnectObserved = true; };
        string room = OnlineClientProfileStore.ReadArgument("-forge-room") ?? "forge-smoke";
        string profile = "forge-smoke-" + role;
        OnlineClientProfileStore.SaveReconnectToken(profile, string.Empty, room);
        ushort port = ushort.Parse(OnlineClientProfileStore.ReadArgument("-forge-port") ?? "17877");
        bestOf = int.Parse(OnlineClientProfileStore.ReadArgument("-forge-bestof") ?? "3");
        var config = new OnlineRoomConfig { bestOf = bestOf };
        if (role == "server") service.StartDedicatedServer(port);
        else if (role == "host") service.StartLanHost(port, role, profile, room, config);
        else service.Connect("127.0.0.1", port, role, profile, room, config);
        if (role == "incompatible")
        {
            var manifest = (OnlineContentManifest)typeof(OnlineMatchService)
                .GetField("localManifest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(service);
            manifest.protocolVersion = OnlineProtocol.Version + 1;
        }
        if (role != "server")
        {
            deck = DeckRepository.Load(Path.Combine(DeckRepository.DeckRoot, "Forge-Validation.mdk"));
            var land = deck.cards.Find(entry => entry.count == 36);
            if (land == null) throw new InvalidDataException("Validation deck needs its 36-land entry.");
            deck.sideboard.Add(new DeckEntry { setCode = land.setCode, collectorNumber = land.collectorNumber, count = 1 });
        }
    }
    private void Ended(OnlineMatchEndedMessage message)
    {
        if (message.winnerSeat != 2) { Complete(false, message.reason); return; }
        if (!openingVerified) { Complete(false, "Game ended before mulligan verification."); return; }
        games++; actions = 0; submitted = false; starting = false; mulligans = 0; openingVerified = false;
        if (games == 1 && bestOf > 1)
        {
            var land = deck.cards.Find(entry => entry.count == 36);
            var spell = deck.cards.Find(entry => entry.count == 4);
            land.count++; spell.count--;
            deck.sideboard.Clear();
            deck.sideboard.Add(new DeckEntry { setCode = spell.setCode, collectorNumber = spell.collectorNumber, count = 1 });
            sideboardChanged = true;
        }
        if (message.series.finished)
        {
            int requiredWins = bestOf / 2 + 1;
            Complete(games == requiredWins && message.series.player2Wins == requiredWins && (bestOf == 1 || sideboardChanged)
                && (role == "host" || reconnectVerified), "BO" + bestOf + ", registered-pool sideboarding when applicable and pending-choice reconnect.");
        }
    }
    private void Update()
    {
        if (service == null) return;
        var activeTransport = Unity.Netcode.NetworkManager.Singleton?.NetworkConfig?.NetworkTransport
            as Unity.Netcode.Transports.UTP.UnityTransport;
        if (activeTransport != null) packetQueueCapacity = activeTransport.MaxPacketQueueSize;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now > deadline) { Complete(false, "Network smoke timed out."); return; }
        if (role == "server" || now < nextAction) return;
        nextAction = now + 0.15;
        if (!service.HasJoinedLobby) return;
        if (!service.IsInMatch)
        {
            if (service.LobbyState.playDrawChooserSeat == service.LocalSeat)
            { service.ChoosePlayDraw(true); return; }
            if (!submitted) { service.SubmitDeck(deck); submitted = true; return; }
            var own = service.LobbyState.players.Find(p => p.seat == service.LocalSeat);
            if (own?.hasDeck == true && !own.ready) { service.SetReady(true); return; }
            if (!starting && service.LobbyState.hostSeat == service.LocalSeat && service.LobbyState.players.Count == 2
                && service.LobbyState.players.All(p => p.ready)) { service.RequestStartMatch(); starting = true; }
            return;
        }
        var view = service.ForgeView?.forge;
        if (view?.players != null)
            foreach (var player in view.players)
                if (player.seat != service.LocalSeat && player.hand?.Length > 0) { Complete(false, "Hidden hand leaked."); return; }
        if (!openingVerified && view != null && view.turn > 0)
        {
            if (mulligans != 1 || view.players.Any(p => p.handCount != 6))
            { Complete(false, "Expected both players to start with six cards after mulligan."); return; }
            openingVerified = true;
            Debug.Log("Forge smoke: both seats completed London mulligan and started with six cards.");
        }
        if (service.LocalSeat == 1 && openingVerified && actions >= 5 && service.InteractionState == OnlineClientInteractionState.Idle)
        { service.Concede(); actions = -100; return; }
        if (view?.awaitingInput != true || service.InteractionState != OnlineClientInteractionState.Idle) return;
        var request = view.request;
        if (role != "host" && !reconnectStarted)
        {
            reconnectStarted = true; reconnectChoice = request.requestId;
            reconnectViewHash = service.ForgeView.viewHash;
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
            return;
        }
        if (reconnectStarted && !reconnectVerified)
        {
            if (!reconnectObserved) return;
            if (request.requestId != reconnectChoice || service.ForgeView.viewHash != reconnectViewHash)
            { Complete(false, "Pending choice or seat snapshot changed across reconnect."); return; }
            reconnectVerified = true;
            Debug.Log("Forge smoke: pending-choice reconnect verified.");
        }
        int index = Array.FindIndex(request.options, option => option.id == "ok");
        bool takeMulligan = request.kind == "InputConfirmMulligan" && mulligans == 0;
        if (takeMulligan) index = Array.FindIndex(request.options, option => option.id == "cancel");
        if (request.kind == "InputLondonMulligan" && index < 0)
            index = Array.FindIndex(request.options, option => option.id.StartsWith("card:") && !option.label.StartsWith("[已选回牌库底]"));
        var battlefield = FindFirstObjectByType<ForgeBattlefieldController>();
        if (battlefield == null || !battlefield.TrySubmitPresentedOption(index < 0 ? 0 : index, request.requestId)) return;
        if (takeMulligan) mulligans++;
        actions++;
    }
    private void Complete(bool success, string detail)
    {
        if (!string.IsNullOrWhiteSpace(report))
            File.WriteAllText(report, JsonUtility.ToJson(new Result { success = success, games = games,
                detail = detail, packetQueueCapacity = packetQueueCapacity }, true));
        enabled = false;
        StartCoroutine(QuitAfterDelivery(success));
    }
    private System.Collections.IEnumerator QuitAfterDelivery(bool success)
    {
        // A LAN host must flush the match-ended message before shutting down transport.
        yield return new WaitForSecondsRealtime(2);
        service?.Disconnect(); Application.Quit(success ? 0 : 1);
    }
    [Serializable] private sealed class Result { public bool success; public int games; public string detail; public int packetQueueCapacity; }
}
