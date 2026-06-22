using System;
using UnityEngine;
using UnityEngine.Serialization;
using XLua;

[CreateAssetMenu(fileName = "NewCard", menuName = "TCG/Card")]
[LuaCallCSharp]
public class CardData : ScriptableObject
{
    [Header("Identity")]
    public string cardName;
    [Tooltip("Internal unique ID used by gameplay and networking.")]
    public int cardID;
    [Tooltip("Rules identity shared by all printings of the same MTG card.")]
    public string oracleID;
    [Tooltip("MTG set code, used as the card image folder name, for example WOE.")]
    public string setCode;
    [Min(0), Tooltip("Collector number inside the set, used as the card image file name.")]
    public int collectorNumber;
    [Tooltip("Text collector number used by databases, including values such as 45a. Numeric legacy assets may leave this empty.")]
    public string collectorNumberText;

    [Header("Card")]
    public CardType cardTypes;
    [TextArea]
    public string description;

    [Header("Mana Cost")]
    [Tooltip("Mana symbols such as {2}{G}{G/U/P}{U}.")]
    public string manaCost;

    [Header("Stats")]
    public int attack;
    public int defence;

    [Header("Artwork")]
    public Sprite artwork;

    [Header("Scripting")]
    [TextArea(5, 20)] public string luaScript;
    public string effectKey;

    [FormerlySerializedAs("cardType"), SerializeField, HideInInspector]
    private int legacyCardType = -1;
    [FormerlySerializedAs("cost"), SerializeField, HideInInspector]
    private int legacyGenericCost;
    [FormerlySerializedAs("costWhite"), SerializeField, HideInInspector]
    private int legacyWhiteCost;
    [FormerlySerializedAs("costBlue"), SerializeField, HideInInspector]
    private int legacyBlueCost;
    [FormerlySerializedAs("costBlack"), SerializeField, HideInInspector]
    private int legacyBlackCost;
    [FormerlySerializedAs("costRed"), SerializeField, HideInInspector]
    private int legacyRedCost;
    [FormerlySerializedAs("costGreen"), SerializeField, HideInInspector]
    private int legacyGreenCost;
    [FormerlySerializedAs("costColorless"), SerializeField, HideInInspector]
    private int legacyColorlessCost;

    public string CollectorNumber => !string.IsNullOrWhiteSpace(collectorNumberText)
        ? collectorNumberText.Trim()
        : collectorNumber > 0 ? collectorNumber.ToString() : string.Empty;

    public string PrintingKey => string.IsNullOrWhiteSpace(setCode) || string.IsNullOrEmpty(CollectorNumber)
        ? string.Empty
        : $"{setCode.Trim()}/{CollectorNumber}";

    [NonSerialized] public bool artworkRuntime;

    public bool HasType(CardType type) => (cardTypes & type) != 0;

    [Obsolete("Use manaCost or GetManaCost().")]
    public int cost
    {
        get => GetManaCost().Generic;
        set => manaCost = value > 0 ? $"{{{value}}}" : string.Empty;
    }

    public ManaCost GetManaCost()
    {
        MigrateLegacyData();
        return ManaCost.Parse(manaCost);
    }

    public ManaColor GetProducedManaColor()
    {
        return ManaSymbolUtility.FindFirstProducedColor(description);
    }

    public void MigrateLegacyData()
    {
        if (string.IsNullOrWhiteSpace(collectorNumberText) && collectorNumber > 0)
        {
            collectorNumberText = collectorNumber.ToString();
        }

        if (cardTypes == CardType.None && legacyCardType >= 0)
        {
            cardTypes = LegacyCardTypeToFlags(legacyCardType);
            legacyCardType = -1;
        }

        if (string.IsNullOrWhiteSpace(manaCost))
        {
            manaCost = ManaSymbolUtility.BuildLegacyCost(
                legacyGenericCost,
                legacyWhiteCost,
                legacyBlueCost,
                legacyBlackCost,
                legacyRedCost,
                legacyGreenCost,
                legacyColorlessCost);
        }
    }

    private static CardType LegacyCardTypeToFlags(int value)
    {
        switch (value)
        {
            case 0: return CardType.Creature;
            case 1: return CardType.Sorcery;
            case 2: return CardType.Land;
            case 3: return CardType.Artifact;
            case 4: return CardType.Enchantment;
            default: return CardType.None;
        }
    }

    private void OnEnable()
    {
        MigrateLegacyData();
    }

    private void OnDestroy()
    {
        if (!artworkRuntime || artwork == null) return;

        Texture2D texture = artwork.texture;
        if (texture != null) Destroy(texture);
        Destroy(artwork);
        artwork = null;
    }
}

[Flags]
public enum CardType
{
    None = 0,
    Artifact = 1 << 0,
    Battle = 1 << 1,
    Creature = 1 << 2,
    Enchantment = 1 << 3,
    Instant = 1 << 4,
    Kindred = 1 << 5,
    Land = 1 << 6,
    Planeswalker = 1 << 7,
    Sorcery = 1 << 8
}

public static class CardTypeUtility
{
    public static bool Has(this CardType value, CardType type) => (value & type) != 0;

    public static bool IsInstantOrSorcery(this CardType value)
    {
        return value.Has(CardType.Instant) || value.Has(CardType.Sorcery);
    }

    public static string ToDisplayString(this CardType value)
    {
        if (value == CardType.None) return string.Empty;
        return value.ToString().Replace(",", string.Empty);
    }
}
