// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public struct CardIdHash : INetworkSerializable, IEquatable<CardIdHash>
{
    public int cardID;
    public FixedString128Bytes hash;

    public bool Equals(CardIdHash other)
    {
        return cardID == other.cardID && hash.Equals(other.hash);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref cardID);
        serializer.SerializeValue(ref hash);
    }
}

public class CardSyncManager : NetworkBehaviour
{
    public NetworkList<CardIdHash> approvedCards = new NetworkList<CardIdHash>();

    [Header("Custom Card Sync")]
    public string customCardsFolderName = "CustomCards";

    private readonly Dictionary<int, FixedString128Bytes> approvedMap = new Dictionary<int, FixedString128Bytes>();

    public override void OnNetworkSpawn()
    {
        approvedCards.OnListChanged += OnApprovedCardsChanged;

        if (IsServer)
        {
            // 初始化卡牌数据
            LoadAndSyncCards();
        }
        else
        {
            RebuildApprovedMap();
        }
    }

    private void LoadAndSyncCards()
    {
        // 加载卡牌数据并同步到客户端
        string folderPath = Path.Combine(Application.persistentDataPath, customCardsFolderName);
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"Custom card folder not found: {folderPath}");
            return;
        }

        approvedCards.Clear();
        approvedMap.Clear();

        var files = Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly);
        var seenIds = new HashSet<int>();

        foreach (var file in files)
        {
            string json = File.ReadAllText(file);
            var cardModels = CardDataModelJson.ParseList(json);
            foreach (var model in cardModels)
            {
                if (!seenIds.Add(model.cardID))
                {
                    Debug.LogWarning($"Duplicate CardID ignored: {model.cardID} ({model.cardName})");
                    continue;
                }

                var hash = ComputeCardHash(model);
                approvedCards.Add(new CardIdHash { cardID = model.cardID, hash = hash });
                approvedMap[model.cardID] = hash;
            }
        }
    }

    private void OnApprovedCardsChanged(NetworkListEvent<CardIdHash> changeEvent)
    {
        if (!IsServer)
        {
            RebuildApprovedMap();
        }
    }

    private void RebuildApprovedMap()
    {
        approvedMap.Clear();
        foreach (var entry in approvedCards)
        {
            approvedMap[entry.cardID] = entry.hash;
        }
    }

    public bool IsCardApproved(CardDataModel model)
    {
        if (model.cardID == 0) return false;
        var hash = ComputeCardHash(model);
        return approvedMap.TryGetValue(model.cardID, out var approvedHash) && approvedHash.Equals(hash);
    }

    private static FixedString128Bytes ComputeCardHash(CardDataModel model)
    {
        var canonical = new CardHashModel
        {
            cardName = model.cardName,
            cardID = model.cardID,
            setCode = model.setCode,
            collectorNumber = model.collectorNumber,
            cardTypes = model.GetCardTypes(),
            description = model.description,
            manaCost = model.GetManaCostString(),
            attack = model.attack,
            defence = model.defence,
            luaScript = model.luaScript
        };

        string json = JsonUtility.ToJson(canonical);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        using var sha = SHA256.Create();
        byte[] hashBytes = sha.ComputeHash(bytes);

        var sb = new StringBuilder(hashBytes.Length * 2);
        for (int i = 0; i < hashBytes.Length; i++)
        {
            sb.Append(hashBytes[i].ToString("x2"));
        }

        return new FixedString128Bytes(sb.ToString());
    }

    [Serializable]
    private struct CardHashModel
    {
        public string cardName;
        public int cardID;
        public string setCode;
        public int collectorNumber;
        public CardType cardTypes;
        public string description;
        public string manaCost;
        public int attack;
        public int defence;
        public string luaScript;
    }

    private void OnDestroy()
    {
        if (approvedCards != null)
        {
            approvedCards.OnListChanged -= OnApprovedCardsChanged;
        }
    }
}
