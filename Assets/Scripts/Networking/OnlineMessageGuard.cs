using System;
using System.Collections.Generic;

public sealed class OnlineMessageGuard
{
    public const int MaxEnvelopeCharacters = 512 * 1024;
    public const int MaxDisplayNameCharacters = 24;
    public const int MaxRoomIdCharacters = 16;
    public const int MaxReconnectTokenCharacters = 96;
    public const int MaxMessagesPerWindow = 80;
    public const int MaxDeckEntries = 256;
    public const int MaxDeckCards = 500;
    public const double RateWindowSeconds = 10d;
    public const double IdleTimeoutSeconds = 45d;

    private readonly Dictionary<ulong, ClientWindow> clients = new Dictionary<ulong, ClientWindow>();

    public bool TryAccept(ulong clientId, int envelopeCharacters, double now, out string error)
    {
        if (envelopeCharacters <= 0 || envelopeCharacters > MaxEnvelopeCharacters)
        {
            error = "消息大小非法或超过限制。";
            return false;
        }

        if (!clients.TryGetValue(clientId, out ClientWindow window))
        {
            window = new ClientWindow { windowStartedAt = now };
            clients.Add(clientId, window);
        }
        if (now - window.windowStartedAt >= RateWindowSeconds)
        {
            window.windowStartedAt = now;
            window.messages = 0;
        }
        window.messages++;
        window.lastSeenAt = now;
        if (window.messages > MaxMessagesPerWindow)
        {
            error = "消息发送过于频繁。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public void Touch(ulong clientId, double now)
    {
        if (!clients.TryGetValue(clientId, out ClientWindow window))
        {
            window = new ClientWindow { windowStartedAt = now };
            clients.Add(clientId, window);
        }
        window.lastSeenAt = now;
    }

    public bool IsIdle(ulong clientId, double now)
    {
        return clients.TryGetValue(clientId, out ClientWindow window)
            && now - window.lastSeenAt > IdleTimeoutSeconds;
    }

    /// <summary>该客户端在 withinSeconds 内有过消息（含心跳）。未跟踪的客户端视为过期。</summary>
    public bool WasSeenWithin(ulong clientId, double now, double withinSeconds)
    {
        return clients.TryGetValue(clientId, out ClientWindow window)
            && now - window.lastSeenAt <= withinSeconds;
    }

    public void Remove(ulong clientId)
    {
        clients.Remove(clientId);
    }

    public static bool ValidateRoomId(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId) || roomId.Length > MaxRoomIdCharacters) return false;
        for (int i = 0; i < roomId.Length; i++)
        {
            char character = roomId[i];
            if (!char.IsLetterOrDigit(character) && character != '-' && character != '_') return false;
        }
        return true;
    }

    public static bool ValidateHello(OnlineHelloMessage hello, out string error)
    {
        if (hello == null)
        {
            error = "握手消息为空。";
            return false;
        }
        if (string.IsNullOrWhiteSpace(hello.displayName) || hello.displayName.Length > MaxDisplayNameCharacters)
        {
            error = "玩家名称长度非法。";
            return false;
        }
        if (!ValidateRoomId(hello.roomId))
        {
            error = "房间号格式非法。";
            return false;
        }
        if ((hello.reconnectToken ?? string.Empty).Length > MaxReconnectTokenCharacters)
        {
            error = "重连凭据长度非法。";
            return false;
        }
        if (hello.manifest == null)
        {
            error = "缺少联机协议信息。";
            return false;
        }
        if (hello.requestedRoomConfig?.allowedSetCodes != null)
        {
            if (hello.requestedRoomConfig.allowedSetCodes.Count > 64)
            {
                error = "房间允许系列数量超过限制。";
                return false;
            }
            for (int i = 0; i < hello.requestedRoomConfig.allowedSetCodes.Count; i++)
            {
                if ((hello.requestedRoomConfig.allowedSetCodes[i] ?? string.Empty).Length > 16)
                {
                    error = "房间系列代码长度非法。";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    public static bool ValidateActionShape(GameAction action, out string error)
    {
        if (action == null)
        {
            error = "操作内容为空。";
            return false;
        }
        if (action.type == GameActionType.None || !Enum.IsDefined(typeof(GameActionType), action.type))
        {
            error = "操作类型非法。";
            return false;
        }
        if ((action.optionIds?.Count ?? 0) > 64 || (action.values?.Count ?? 0) > 64)
        {
            error = "操作选项数量超过限制。";
            return false;
        }
        if (action.zoneIndex < -1 || action.zoneIndex > 512
            || action.destinationIndex < 0 || action.destinationIndex > 512
            || action.featureIndex < -1 || action.featureIndex > 128
            || action.choiceRequestId < 0
            || action.sourceObjectId < 0 || action.sourceObjectId > 1000000000L
            || action.targetObjectId < 0 || action.targetObjectId > 1000000000L
            || action.targetPlayerId < 0 || action.targetPlayerId > 2
            || action.value < -1000000 || action.value > 1000000)
        {
            error = "操作字段超出允许范围。";
            return false;
        }
        if (!ValuesAreBounded(action.optionIds) || !ValuesAreBounded(action.values))
        {
            error = "操作选项值超出允许范围。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValuesAreBounded(IReadOnlyList<int> values)
    {
        if (values == null) return true;
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] < -1000000 || values[i] > 1000000) return false;
        }
        return true;
    }

    public static bool ValidateDeckShape(DeckDefinition deck, out string error)
    {
        if (deck == null)
        {
            error = "牌组内容为空。";
            return false;
        }
        if ((deck.name ?? string.Empty).Length > 64)
        {
            error = "牌组名称过长。";
            return false;
        }
        int entries = 0;
        int cards = 0;
        if (!ValidateDeckEntries(deck.commanders, ref entries, ref cards, out error)
            || !ValidateDeckEntries(deck.cards, ref entries, ref cards, out error)
            || !ValidateDeckEntries(deck.sideboard, ref entries, ref cards, out error)) return false;
        if (entries > MaxDeckEntries || cards > MaxDeckCards)
        {
            error = "牌组项目或卡牌总数超过限制。";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool ValidateDeckEntries(
        List<DeckEntry> source,
        ref int entries,
        ref int cards,
        out string error)
    {
        if (source == null)
        {
            error = string.Empty;
            return true;
        }
        for (int i = 0; i < source.Count; i++)
        {
            DeckEntry entry = source[i];
            entries++;
            if (entry == null || entry.count <= 0 || entry.count > MaxDeckCards
                || (entry.setCode ?? string.Empty).Length > 16
                || (entry.collectorNumber ?? string.Empty).Length > 24)
            {
                error = "牌组项目字段非法。";
                return false;
            }
            cards += entry.count;
            if (entries > MaxDeckEntries || cards > MaxDeckCards)
            {
                error = "牌组项目或卡牌总数超过限制。";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    private sealed class ClientWindow
    {
        public double windowStartedAt;
        public double lastSeenAt;
        public int messages;
    }
}
