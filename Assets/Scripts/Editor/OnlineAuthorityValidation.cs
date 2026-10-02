#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class OnlineAuthorityValidation
{
    private static OnlineMatchSession session;
    private static double deadline;
    private static bool answered;
    public static bool IsRunning => session != null;
    public static string LastResult { get; private set; }

    [MenuItem("MTGB/验证/联机/权威与权限")]
    public static void Run()
    {
        if (IsRunning) throw new InvalidOperationException("Authority validation is running.");
        LastResult = "RUNNING";
        try
        {
            ForgeCardCatalog.InvalidateVerification();
            var watch = Stopwatch.StartNew();
            var local = OnlineContentManifest.Build();
            watch.Stop();
            // A client may use a different engine/catalog or only have display data.
            // Older diagnostic fields are deliberately different and must not gate admission.
            var remote = JsonUtility.FromJson<OnlineContentManifest>(
                "{\"protocolVersion\":" + OnlineProtocol.Version + ",\"gameVersion\":\"different-release\","
                + "\"forgeProtocol\":999,\"forgeRevision\":\"other-engine\",\"forgeRuntimeHash\":\"other-resources\","
                + "\"cardDataHash\":\"other-catalog\",\"contentHash\":\"other-content\"}");
            Require(local.IsCompatibleWith(remote, out _) && remote.IsCompatibleWith(local, out _),
                "Compatible protocol rejected because of local resources or release label.");
            remote.protocolVersion++;
            Require(!local.IsCompatibleWith(remote, out _), "Incompatible protocol accepted.");
            remote.protocolVersion = 0;
            Require(!local.IsCompatibleWith(remote, out _), "Missing protocol accepted.");
            Require(!local.IsCompatibleWith(null, out _), "Missing handshake accepted.");
            RequireNoFullScan();
            ValidateLaunchConfiguration();

            var deck = DeckRepository.CreateDefaultDeck(CardDatabaseRepository.LoadAll());
            ValidateServerDecks(deck);
            RequireNoFullScan();
            session = new OnlineMatchSession(Guid.NewGuid().ToString("N"), deck, deck, 42);
            RequireNoFullScan();
            answered = false;
            deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update += Tick;
            UnityEngine.Debug.Log($"Online authority: handshake {watch.Elapsed.TotalMilliseconds:F2} ms; compatibility, launch and server deck checks passed.");
        }
        catch (Exception error) { Finish(error); throw; }
    }

    private static void ValidateLaunchConfiguration()
    {
        string root = Path.GetFullPath(Path.Combine(".tools", "online-authority-validation", Guid.NewGuid().ToString("N")));
        foreach (string directory in new[] { "jre/bin", "assets", "classes", "lib" })
            Directory.CreateDirectory(Path.Combine(root, directory));
        string java = Path.Combine(root, "jre/bin/java.exe");
        File.WriteAllText(java, "fixture only; never executed");
        var config = new ForgeCardCatalog.RuntimeConfig {
            java = "jre/bin/java.exe", assets = "assets", revision = ForgeCardCatalog.Revision, protocol = ForgeCardCatalog.Protocol
        };
        string path = Path.Combine(root, "runtime.json");
        File.WriteAllText(path, JsonUtility.ToJson(config));
        // No files.json or content hashes: launch checks must not require a resource scan.
        ForgeCardCatalog.ReadConfigForMatch(root);
        File.Delete(java);
        Reject(() => ForgeCardCatalog.ReadConfigForMatch(root), "Missing authority Java accepted.");
        File.WriteAllText(java, "fixture only; never executed");
        config.java = "../java.exe";
        File.WriteAllText(path, JsonUtility.ToJson(config));
        Reject(() => ForgeCardCatalog.ReadConfigForMatch(root), "Invalid authority Java path accepted.");
        config.java = "jre/bin/java.exe";
        config.protocol++;
        File.WriteAllText(path, JsonUtility.ToJson(config));
        Reject(() => ForgeCardCatalog.ReadConfigForMatch(root), "Incompatible local bridge protocol accepted.");
    }

    private static void ValidateServerDecks(DeckDefinition deck)
    {
        var owner = new GameObject("OnlineAuthorityValidation");
        owner.SetActive(false); // Do not register a singleton or open a network connection.
        var service = owner.AddComponent<OnlineMatchService>();
        var validate = typeof(OnlineMatchService).GetMethod("ValidateDeck", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            bool Accepted(DeckDefinition candidate)
            {
                var args = new object[] { candidate, new OnlineRoomConfig(), null };
                return (bool)validate.Invoke(service, args);
            }
            Require(Accepted(deck), "Server rejected the valid fixture deck.");
            var unknown = JsonUtility.FromJson<DeckDefinition>(JsonUtility.ToJson(deck));
            unknown.cards[0].setCode = "UNKNOWN";
            unknown.cards[0].collectorNumber = "999999";
            Require(!Accepted(unknown), "Server accepted a printing absent from its own catalog.");
            var excessive = JsonUtility.FromJson<DeckDefinition>(JsonUtility.ToJson(deck));
            var spell = excessive.cards.Find(entry => entry.count == 4);
            Require(spell != null, "Fixture needs a four-copy spell.");
            spell.count = 5;
            Require(!Accepted(excessive), "Server accepted an illegal fifth copy.");
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    private static void Tick()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup < deadline, "Authority session timed out.");
            session.Poll();
            Require(session.Failure == null, session.Failure);
            if (session.GetView(1).outcome.isFinished)
            {
                Require(answered && session.GetView(1).outcome.winnerPlayerId == 2, "Incorrect authority result.");
                Finish(null);
                return;
            }
            if (answered) return;
            for (int seat = 1; seat <= 2; seat++)
            {
                var view = session.GetView(seat);
                if (!view.forge.awaitingInput) continue;
                var opponent = session.GetView(3 - seat);
                Require(!opponent.forge.awaitingInput && opponent.forge.request.requestId == 0, "Private request leaked.");
                foreach (var player in view.forge.players)
                    if (player.seat != seat) Require(player.hand.Length == 0, "Opponent hand leaked.");
                var request = view.forge.request;
                var action = new GameAction { type = GameActionType.ConfirmChoice, playerId = 3 - seat,
                    sourceObjectId = request.requestId, optionId = 0 };
                Require(!session.ApplyAction(action).accepted, "Wrong seat answered authority input.");
                action.playerId = seat;
                action.optionId = request.options.Length;
                Require(!session.ApplyAction(action).accepted, "Out-of-range response accepted.");
                action.optionId = 0;
                Require(session.ApplyAction(action).accepted, "Valid response rejected.");
                Require(!session.ApplyAction(action).accepted, "Duplicate response accepted.");
                Require(session.ApplyAction(new GameAction { type = GameActionType.Concede, playerId = 1 }).accepted,
                    "Concession rejected.");
                answered = true;
                return;
            }
        }
        catch (Exception error) { Finish(error); UnityEngine.Debug.LogException(error); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        session?.Dispose();
        session = null;
        LastResult = error?.ToString() ?? "PASS: protocol-only admission; authority launch, deck validation, private views, action rejection and concession; no full runtime scan.";
        if (error == null) UnityEngine.Debug.Log(LastResult);
    }

    private static void RequireNoFullScan()
    {
        var field = typeof(ForgeCardCatalog).GetField("verifiedRuntimeHash", BindingFlags.Static | BindingFlags.NonPublic);
        Require(field.GetValue(null) == null, "Normal online flow ran a full runtime scan.");
    }

    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}
#endif
