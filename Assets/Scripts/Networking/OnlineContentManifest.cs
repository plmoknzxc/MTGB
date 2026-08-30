using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class OnlineContentManifest
{
    public string gameVersion;
    public int protocolVersion;
    public int databaseSchemaVersion;
    public string cardDataHash;
    public string luaScriptsHash;
    public string contentHash;

    public static OnlineContentManifest Build()
    {
        return Build(CardDatabaseRepository.LoadAll());
    }

    public static OnlineContentManifest Build(IReadOnlyList<CardDefinition> cards)
    {
        string cardDataHash = ComputeCategoryHash(cards, includeCardData: true, includeLua: false);
        string luaScriptsHash = ComputeCategoryHash(cards, includeCardData: false, includeLua: true);
        return new OnlineContentManifest
        {
            gameVersion = string.IsNullOrWhiteSpace(Application.version) ? "development" : Application.version.Trim(),
            protocolVersion = OnlineProtocol.Version,
            databaseSchemaVersion = CardDatabaseRepository.SupportedSchemaVersion,
            cardDataHash = cardDataHash,
            luaScriptsHash = luaScriptsHash,
            contentHash = HashText(cardDataHash + "|" + luaScriptsHash)
        };
    }

    public bool IsCompatibleWith(OnlineContentManifest other, out string reason)
    {
        if (other == null)
        {
            reason = "缺少联机内容清单。";
            return false;
        }
        if (protocolVersion != other.protocolVersion)
        {
            reason = $"协议版本不一致（服务端 {protocolVersion}，客户端 {other.protocolVersion}）。";
            return false;
        }
        if (databaseSchemaVersion != other.databaseSchemaVersion)
        {
            reason = $"卡牌数据库结构版本不一致（服务端 {databaseSchemaVersion}，客户端 {other.databaseSchemaVersion}）。";
            return false;
        }
        if (!string.Equals(gameVersion ?? string.Empty, other.gameVersion ?? string.Empty, StringComparison.Ordinal))
        {
            reason = $"游戏版本不一致（服务端 {gameVersion}，客户端 {other.gameVersion}）。";
            return false;
        }
        if (!string.Equals(contentHash ?? string.Empty, other.contentHash ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        {
            var mismatches = new List<string>();
            if (!string.Equals(cardDataHash ?? string.Empty, other.cardDataHash ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                mismatches.Add("卡牌数据库");
            if (!string.Equals(luaScriptsHash ?? string.Empty, other.luaScriptsHash ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                mismatches.Add("Lua 脚本");
            reason = mismatches.Count == 0
                ? "联机规则内容清单不一致。"
                : string.Join("、", mismatches) + "内容不一致。";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public static string ComputeContentHash(IReadOnlyList<CardDefinition> cards)
    {
        string cardDataHash = ComputeCategoryHash(cards, includeCardData: true, includeLua: false);
        string luaScriptsHash = ComputeCategoryHash(cards, includeCardData: false, includeLua: true);
        return HashText(cardDataHash + "|" + luaScriptsHash);
    }

    private static string ComputeCategoryHash(
        IReadOnlyList<CardDefinition> cards,
        bool includeCardData,
        bool includeLua)
    {
        var ordered = new List<CardDefinition>();
        if (cards != null)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null) ordered.Add(cards[i]);
            }
        }
        ordered.Sort((left, right) =>
        {
            int printing = string.Compare(left.PrintingKey, right.PrintingKey, StringComparison.Ordinal);
            return printing != 0 ? printing : left.cardID.CompareTo(right.cardID);
        });

        var canonical = new StringBuilder(ordered.Count * 256);
        Append(canonical, CardDatabaseRepository.SupportedSchemaVersion.ToString());
        for (int i = 0; i < ordered.Count; i++)
        {
            CardDefinition card = ordered[i];
            Append(canonical, card.PrintingKey);
            if (includeCardData)
            {
            Append(canonical, card.cardID.ToString());
            Append(canonical, card.oracleID);
            Append(canonical, card.cardName);
            Append(canonical, ((int)card.cardTypes).ToString());
            Append(canonical, ((int)card.supertypes).ToString());
            Append(canonical, card.subtypes);
            Append(canonical, card.manaCost);
            Append(canonical, card.description);
            Append(canonical, ((int)card.colorIndicator).ToString());
            Append(canonical, ((int)card.targetMode).ToString());
            Append(canonical, card.attack.ToString());
            Append(canonical, card.defence.ToString());
            Append(canonical, card.loyalty.ToString());
            Append(canonical, card.battleDefense.ToString());
            Append(canonical, card.effectKey);
            Append(canonical, card.customContent ? "custom" : "core");
            Append(canonical, JsonUtility.ToJson(new FeatureList { values = card.features }));
            }
            if (includeLua) Append(canonical, card.luaScript);
        }

        return HashText(canonical.ToString());
    }

    private static string HashText(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            var result = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) result.Append(bytes[i].ToString("x2"));
            return result.ToString();
        }
    }

    private static void Append(StringBuilder builder, string value)
    {
        string normalized = (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        builder.Append(normalized.Length).Append(':').Append(normalized).Append('|');
    }

    [Serializable]
    private sealed class FeatureList
    {
        public List<CardFeatureDefinition> values;
    }
}
