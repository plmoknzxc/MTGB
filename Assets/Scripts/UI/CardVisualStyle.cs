using UnityEngine;

public static class CardVisualStyle
{
    public static Sprite LoadFrame(CardData card)
    {
        if (card == null) return null;

        string kind = card.HasType(CardType.Land) ? "Land" : "Creature";
        string color = GetColorCode(GetPrimaryColor(card));
        return Resources.Load<Sprite>($"UI/CardFrames/{kind}_{color}");
    }

    public static ManaColor GetPrimaryColor(CardData card)
    {
        if (card == null) return ManaColor.Colorless;
        if (card.HasType(CardType.Land)) return card.GetProducedManaColor();

        ManaCost cost = card.GetManaCost();
        int highest = 0;
        ManaColor selected = ManaColor.Colorless;
        Select(cost.White, ManaColor.White, ref highest, ref selected);
        Select(cost.Blue, ManaColor.Blue, ref highest, ref selected);
        Select(cost.Black, ManaColor.Black, ref highest, ref selected);
        Select(cost.Red, ManaColor.Red, ref highest, ref selected);
        Select(cost.Green, ManaColor.Green, ref highest, ref selected);
        Select(cost.Colorless, ManaColor.Colorless, ref highest, ref selected);
        if (highest > 0) return selected;

        if (card.cardTypes.IsInstantOrSorcery()) return ManaColor.Red;
        if (card.HasType(CardType.Creature)) return ManaColor.Blue;
        return ManaColor.Colorless;
    }

    private static void Select(int amount, ManaColor color, ref int highest, ref ManaColor selected)
    {
        if (amount <= highest) return;
        highest = amount;
        selected = color;
    }

    private static string GetColorCode(ManaColor color)
    {
        switch (color)
        {
            case ManaColor.White: return "W";
            case ManaColor.Blue: return "U";
            case ManaColor.Black: return "B";
            case ManaColor.Red: return "R";
            case ManaColor.Green: return "G";
            default: return "C";
        }
    }
}
