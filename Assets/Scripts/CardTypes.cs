using System;

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

[Flags]
public enum CardSupertype
{
    None = 0,
    Basic = 1 << 0,
    Legendary = 1 << 1,
    Ongoing = 1 << 2,
    Snow = 1 << 3,
    World = 1 << 4
}

public enum CardTargetMode
{
    None = 0,
    AnyCreature = 1,
    OpponentCreature = 2,
    FriendlyCreature = 3,
    AnyPermanent = 4,
    OpponentPermanent = 5,
    FriendlyPermanent = 6,
    AnyPlayer = 7,
    OpponentPlayer = 8,
    AnyCreatureOrPlayer = 9,
    OpponentCreatureOrPlayer = 10
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

public static class CardSupertypeUtility
{
    public static bool Has(this CardSupertype value, CardSupertype type) => (value & type) != 0;

    public static string ToDisplayString(this CardSupertype value)
    {
        if (value == CardSupertype.None) return string.Empty;
        return value.ToString().Replace(",", string.Empty);
    }
}
