using System;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class OnlineClientProfile
{
    public string id;
    public string address;
    public ushort port;
    public string displayName;
    public string roomId;
    public string reconnectToken;
    public DeckDefinition deck;
}

public static class OnlineClientProfileStore
{
    private const string Prefix = "MTGB.Online.Profile.";

    [Serializable]
    private sealed class SharedProfileData
    {
        public string address = "127.0.0.1";
        public int port = 7777;
        public string displayName = "Player";
        public string roomId = "main";
        public DeckDefinition deck;
    }

    public static string ResolveCurrentProfileId()
    {
        string explicitProfile = ReadArgument("-mtgb-profile");
        if (!string.IsNullOrWhiteSpace(explicitProfile)) return NormalizeId(explicitProfile);

        string playerName = ReadArgument("-name");
        string normalizedPlayerName = (playerName ?? string.Empty).Replace(" ", string.Empty);
        if (string.Equals(normalizedPlayerName, "Player2", StringComparison.OrdinalIgnoreCase)) return "player-1";
        if (string.Equals(normalizedPlayerName, "Player3", StringComparison.OrdinalIgnoreCase)) return "player-2";
        return "default";
    }

    public static OnlineClientProfile Load(string profileId)
    {
        string id = NormalizeId(profileId);
        string key = Prefix + id + ".";
        string deckJson = PlayerPrefs.GetString(key + "Deck", string.Empty);
        SharedProfileData shared = LoadShared(id);
        string roomId = OnlineRoomRegistry.NormalizeRoomId(
            shared?.roomId ?? PlayerPrefs.GetString(key + "RoomId", "main"));
        string reconnectToken = PlayerPrefs.GetString(key + "ReconnectToken." + roomId,
            roomId == "main" ? PlayerPrefs.GetString(key + "ReconnectToken", string.Empty) : string.Empty);
        return new OnlineClientProfile
        {
            id = id,
            address = shared?.address ?? PlayerPrefs.GetString(key + "Address", "127.0.0.1"),
            port = shared != null ? ParsePort(shared.port.ToString()) : ParsePort(PlayerPrefs.GetString(key + "Port", "7777")),
            displayName = shared?.displayName ?? PlayerPrefs.GetString(key + "DisplayName", DefaultDisplayName(id)),
            roomId = roomId,
            reconnectToken = reconnectToken,
            deck = shared?.deck ?? (string.IsNullOrEmpty(deckJson) ? null : JsonUtility.FromJson<DeckDefinition>(deckJson))
        };
    }

    public static void SaveConnection(
        string profileId,
        string address,
        ushort port,
        string displayName,
        string roomId = "main")
    {
        string id = NormalizeId(profileId);
        string key = Prefix + id + ".";
        PlayerPrefs.SetString(key + "Address", string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim());
        PlayerPrefs.SetString(key + "Port", (port == 0 ? 7777 : port).ToString());
        PlayerPrefs.SetString(key + "DisplayName", NormalizeDisplayName(displayName));
        PlayerPrefs.SetString(key + "RoomId", OnlineRoomRegistry.NormalizeRoomId(roomId));
        PlayerPrefs.Save();

        SharedProfileData shared = LoadShared(id) ?? new SharedProfileData();
        shared.address = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
        shared.port = port == 0 ? 7777 : port;
        shared.displayName = NormalizeDisplayName(displayName);
        shared.roomId = OnlineRoomRegistry.NormalizeRoomId(roomId);
        SaveShared(id, shared);
    }

    public static void SaveReconnectToken(string profileId, string token, string roomId = "main")
    {
        string key = Prefix + NormalizeId(profileId) + ".ReconnectToken."
            + OnlineRoomRegistry.NormalizeRoomId(roomId);
        PlayerPrefs.SetString(key, token ?? string.Empty);
        PlayerPrefs.Save();
    }

    public static void SaveDeck(string profileId, DeckDefinition deck)
    {
        string key = Prefix + NormalizeId(profileId) + ".Deck";
        if (deck == null) PlayerPrefs.DeleteKey(key);
        else PlayerPrefs.SetString(key, JsonUtility.ToJson(deck));
        PlayerPrefs.Save();

        string id = NormalizeId(profileId);
        SharedProfileData shared = LoadShared(id) ?? new SharedProfileData
        {
            displayName = DefaultDisplayName(id)
        };
        shared.deck = deck;
        SaveShared(id, shared);
    }

    public static string NormalizeId(string value)
    {
        string source = string.IsNullOrWhiteSpace(value) ? "default" : value.Trim().ToLowerInvariant();
        char[] result = new char[Math.Min(24, source.Length)];
        int length = 0;
        for (int i = 0; i < source.Length && length < result.Length; i++)
        {
            char character = source[i];
            if (char.IsLetterOrDigit(character) || character == '-' || character == '_') result[length++] = character;
        }
        return length == 0 ? "default" : new string(result, 0, length);
    }

    public static string ReadArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        }
        return string.Empty;
    }

    private static ushort ParsePort(string value)
    {
        return ushort.TryParse(value, out ushort port) && port > 0 ? port : (ushort)7777;
    }

    private static string DefaultDisplayName(string profileId)
    {
        if (profileId == "player-1") return "Player 1";
        if (profileId == "player-2") return "Player 2";
        return "Player";
    }

    private static string NormalizeDisplayName(string value)
    {
        string result = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
        return result.Length <= 24 ? result : result.Substring(0, 24);
    }

    private static string SharedProfileRoot
    {
        get
        {
            return Application.isEditor
                ? Path.Combine(PrintingKeyUtility.ApplicationRoot, "Library", "MTGB", "OnlineProfiles")
                : Path.Combine(Application.persistentDataPath, "OnlineProfiles");
        }
    }

    private static SharedProfileData LoadShared(string profileId)
    {
        try
        {
            string path = Path.Combine(SharedProfileRoot, NormalizeId(profileId) + ".json");
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<SharedProfileData>(File.ReadAllText(path));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not load shared online profile '{profileId}': {exception.Message}");
            return null;
        }
    }

    private static void SaveShared(string profileId, SharedProfileData profile)
    {
        try
        {
            Directory.CreateDirectory(SharedProfileRoot);
            string path = Path.Combine(SharedProfileRoot, NormalizeId(profileId) + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(profile, true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not save shared online profile '{profileId}': {exception.Message}");
        }
    }
}
