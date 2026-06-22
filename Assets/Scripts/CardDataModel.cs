// 2025/11/24 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public class CardDataModel:INetworkSerializable
{
    public string cardName;
    public int cardID;
    public string setCode;
    public int collectorNumber;
    public CardType cardTypes;
    public int cardType = -1;
    public string description;
    public string manaCost;
    public int cost;
    public int costWhite;
    public int costBlue;
    public int costBlack;
    public int costRed;
    public int costGreen;
    public int costColorless;
    public int attack;
    public int defence;
    public string artworkPath;
    public string luaScript;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref cardName);
        serializer.SerializeValue(ref cardID);
        serializer.SerializeValue(ref setCode);
        serializer.SerializeValue(ref collectorNumber);
        serializer.SerializeValue(ref cardTypes);
        serializer.SerializeValue(ref cardType);
        serializer.SerializeValue(ref description);
        serializer.SerializeValue(ref manaCost);
        serializer.SerializeValue(ref cost);
        serializer.SerializeValue(ref costWhite);
        serializer.SerializeValue(ref costBlue);
        serializer.SerializeValue(ref costBlack);
        serializer.SerializeValue(ref costRed);
        serializer.SerializeValue(ref costGreen);
        serializer.SerializeValue(ref costColorless);
        serializer.SerializeValue(ref attack);
        serializer.SerializeValue(ref defence);
        serializer.SerializeValue(ref artworkPath);
        serializer.SerializeValue(ref luaScript);
    }

    public CardType GetCardTypes()
    {
        if (cardTypes != CardType.None) return cardTypes;
        switch (cardType)
        {
            case 0: return CardType.Creature;
            case 1: return CardType.Sorcery;
            case 2: return CardType.Land;
            case 3: return CardType.Artifact;
            case 4: return CardType.Enchantment;
            default: return CardType.None;
        }
    }

    public string GetManaCostString()
    {
        return !string.IsNullOrWhiteSpace(manaCost)
            ? manaCost
            : ManaSymbolUtility.BuildLegacyCost(cost, costWhite, costBlue, costBlack, costRed, costGreen, costColorless);
    }
}

[Serializable]
public class CardDataModelList
{
    public CardDataModel[] items;
}

public static class CardDataModelJson
{
    public static List<CardDataModel> ParseList(string json)
    {
        var result = new List<CardDataModel>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        json = json.Trim();
        if (json.StartsWith("["))
        {
            string wrapped = "{\"items\":" + json + "}";
            var wrapper = JsonUtility.FromJson<CardDataModelList>(wrapped);
            if (wrapper?.items != null && wrapper.items.Length > 0)
            {
                result.AddRange(wrapper.items);
            }
            return result;
        }

        var single = JsonUtility.FromJson<CardDataModel>(json);
        if (single != null && !string.IsNullOrEmpty(single.cardName))
        {
            result.Add(single);
        }
        return result;
    }
}
