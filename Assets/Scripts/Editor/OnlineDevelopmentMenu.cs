#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

public static class OnlineDevelopmentMenu
{
    private const string LocalScenarioPath = "Assets/Settings/PlayMode/NewPlayModeScenario.asset";
    private const string DevelopmentServerFlag = "MTGB.Online.DevelopmentServerPending";

    [CliCommand("mtgb_preview_online_lobby", "Open the online lobby page in Play Mode for UI verification")]
    public static void PreviewOnlineLobby()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("Enter Play Mode before previewing the online lobby.");

        MainMenuController controller = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        if (controller == null) throw new InvalidOperationException("The active scene has no MainMenuController.");
        IReadOnlyList<DeckDefinition> decks = DeckRepository.LoadAll();
        if (decks.Count == 0) throw new InvalidOperationException("No saved deck is available for the lobby preview.");
        OnlineLobbyPanel.Show(controller, decks[0]);
    }

    [CliCommand("mtgb_preview_deck_builder", "Open the deck builder page in Play Mode for UI verification")]
    public static void PreviewDeckBuilder()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("Enter Play Mode before previewing the deck builder.");

        MainMenuController controller = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        if (controller == null) throw new InvalidOperationException("The active scene has no MainMenuController.");
        OnlineLobbyPanel lobby = UnityEngine.Object.FindFirstObjectByType<OnlineLobbyPanel>();
        if (lobby != null) lobby.gameObject.SetActive(false);
        controller.SetOnlineLobbyVisible(false);
        controller.SendMessage("ShowDeckBuilder", SendMessageOptions.RequireReceiver);
    }

    [MenuItem("MTGB/Networking/Prepare Local 2-Player Session")]
    [CliCommand("mtgb_prepare_local_session", "Prepare isolated profiles for a local three-Editor network session")]
    public static void PrepareLocalSession()
    {
        IReadOnlyList<DeckDefinition> decks = DeckRepository.LoadAll();
        if (decks.Count == 0)
        {
            throw new InvalidOperationException("Create and save at least one deck before preparing a local online session.");
        }

        SaveProfile("player-1", "Player 1", decks[0]);
        SaveProfile("player-2", "Player 2", decks.Count > 1 ? decks[1] : decks[0]);
        PlayerPrefs.SetInt(DevelopmentServerFlag, 1);
        PlayerPrefs.Save();

        bool opened = EditorApplication.ExecuteMenuItem("Window/Multiplayer/Play Mode Scenarios")
            || EditorApplication.ExecuteMenuItem("Window/Play Mode/Scenarios");
        Debug.Log(
            "Local online profiles are ready. Configure a Multiplayer Play Mode scenario with " +
            "the Main Editor and two Additional Editor instances, leave streamed logs disabled, then press Play. " +
            "Player1 starts the server; Player2 and Player3 auto-connect and auto-ready with isolated profiles." +
            (opened ? string.Empty : " Open Window > Multiplayer > Play Mode Scenarios manually."));
    }

    [CliCommand("mtgb_inspect_local_scenario", "Report the configured Multiplayer Play Mode editor instances")]
    public static string InspectLocalScenario()
    {
        ScriptableObject config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(LocalScenarioPath);
        if (config == null) return "missing";
        object instances = config.GetType().GetProperty("VirtualEditorInstances")?.GetValue(config);
        int count = 0;
        var names = new List<string>();
        if (instances is IEnumerable enumerable)
        {
            foreach (object instance in enumerable)
            {
                count++;
                string name = instance?.GetType().BaseType?.BaseType?.GetField("Name")?.GetValue(instance) as string;
                object index = instance?.GetType().BaseType?.GetField("PlayerInstanceIndex")?.GetValue(instance);
                names.Add($"{(string.IsNullOrWhiteSpace(name) ? "unnamed" : name)}[{index ?? "?"}]");
            }
        }
        return $"additional-editors={count}: {string.Join(", ", names)}";
    }

    private static void SaveProfile(string id, string displayName, DeckDefinition deck)
    {
        OnlineClientProfileStore.SaveConnection(id, "127.0.0.1", 7777, displayName);
        OnlineClientProfileStore.SaveDeck(id, deck);
    }
}
#endif
