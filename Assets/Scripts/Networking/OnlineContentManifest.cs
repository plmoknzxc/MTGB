using System;
using UnityEngine;
[Serializable]
public sealed class OnlineContentManifest
{
    // The authority owns card data and executes Forge. Clients need the same wire
    // protocol, not the same local engine, card database, artwork or release label.
    public string gameVersion;
    public int protocolVersion;
    public static OnlineContentManifest Build()
    {
        return new OnlineContentManifest
        {
            gameVersion = string.IsNullOrWhiteSpace(Application.version) ? "development" : Application.version.Trim(),
            protocolVersion = OnlineProtocol.Version
        };
    }

    public bool IsCompatibleWith(OnlineContentManifest other, out string reason)
    {
        if (other == null) { reason = "缺少联机协议信息。"; return false; }
        if (protocolVersion != OnlineProtocol.Version || other.protocolVersion != OnlineProtocol.Version)
        { reason = "客户端与服务器通信协议版本不兼容。"; return false; }
        reason = string.Empty; return true;
    }
}
