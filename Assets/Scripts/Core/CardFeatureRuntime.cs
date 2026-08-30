using System;
using System.Collections.Generic;

public sealed class CardFeatureRuntime
{
    public CardDefinition Card { get; }
    public CardFeatureDefinition Definition { get; }

    public int FaceIndex => Definition.faceIndex;
    public int FeatureIndex => Definition.featureIndex;
    public CardFeatureRole Role => Definition.role;
    public string RoleValue => Definition.roleValue ?? string.Empty;
    public string CardName => string.IsNullOrWhiteSpace(Definition.cardName)
        ? Card?.cardName ?? string.Empty
        : Definition.cardName.Trim();
    public CardType CardTypes => Definition.cardTypes;
    public CardSupertype Supertypes => Definition.supertypes;
    public string Subtypes => Definition.subtypes ?? string.Empty;
    public string RulesText => Definition.description ?? string.Empty;
    public ManaCost ManaCost => ManaCost.Parse(Definition.manaCost);
    public IReadOnlyList<CardRuleEntryDefinition> RuleEntries =>
        Definition.ruleEntries ?? (IReadOnlyList<CardRuleEntryDefinition>)Array.Empty<CardRuleEntryDefinition>();
    public IReadOnlyList<CardEffectDefinition> Effects =>
        Definition.effects ?? (IReadOnlyList<CardEffectDefinition>)Array.Empty<CardEffectDefinition>();
    public CardTargetMode TargetMode { get; }
    public int Power => Definition.attack;
    public int Toughness => Definition.defence;
    public int Loyalty => Definition.loyalty;
    public int BattleDefense => Definition.battleDefense;
    public bool IsPrimary => Role == CardFeatureRole.Primary;
    public bool IsSaga => HasSubtype("Saga", "\u4f20\u7eaa", "\u4f20\u8bb0");

    public bool IsPermanent =>
        CardTypes.Has(CardType.Artifact)
        || CardTypes.Has(CardType.Battle)
        || CardTypes.Has(CardType.Creature)
        || CardTypes.Has(CardType.Enchantment)
        || CardTypes.Has(CardType.Land)
        || CardTypes.Has(CardType.Planeswalker);

    public bool IsInstantOrSorcery => CardTypes.IsInstantOrSorcery();
    public bool IsClassLevel => Role == CardFeatureRole.ClassLevel;
    public bool IsCaseStage => Role == CardFeatureRole.CaseStage;

    public int ClassLevel
    {
        get
        {
            return Role == CardFeatureRole.ClassLevel
                && int.TryParse(RoleValue, out int level)
                ? Math.Max(1, level)
                : 0;
        }
    }

    public CardFeatureRuntime(CardDefinition card, CardFeatureDefinition definition)
    {
        Card = card;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        TargetMode = CardTargetModeInference.Infer(
            Definition.description,
            Definition.effects);
    }

    public bool HasType(CardType type)
    {
        return CardTypes.Has(type);
    }

    public bool HasSupertype(CardSupertype type)
    {
        return Supertypes.Has(type);
    }

    public bool HasSubtype(params string[] aliases)
    {
        if (aliases == null || aliases.Length == 0 || string.IsNullOrWhiteSpace(Subtypes))
        {
            return false;
        }

        string[] values = Subtypes.Split(
            new[] { ',', '\uff0c', ';', '\uff1b' },
            StringSplitOptions.RemoveEmptyEntries);
        for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
        {
            string value = values[valueIndex].Trim();
            for (int aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
            {
                if (value.Equals(aliases[aliasIndex], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public ManaColorFlags GetColors()
    {
        ManaCost cost = ManaCost;
        ManaColorFlags colors = ManaColorFlags.None;
        if (cost.White > 0) colors |= ManaColorFlags.White;
        if (cost.Blue > 0) colors |= ManaColorFlags.Blue;
        if (cost.Black > 0) colors |= ManaColorFlags.Black;
        if (cost.Red > 0) colors |= ManaColorFlags.Red;
        if (cost.Green > 0) colors |= ManaColorFlags.Green;
        return colors;
    }

    public bool IsCaseStageNamed(params string[] aliases)
    {
        if (!IsCaseStage || aliases == null) return false;
        for (int i = 0; i < aliases.Length; i++)
        {
            if (RoleValue.Equals(aliases[i], StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    public IEnumerable<int> EnumerateChapterNumbers()
    {
        for (int i = 0; i < RuleEntries.Count; i++)
        {
            CardRuleEntryDefinition entry = RuleEntries[i];
            if (entry == null || entry.kind != CardRuleEntryKind.Chapter) continue;

            IReadOnlyList<int> chapters = GetChapterNumbers(entry);
            for (int valueIndex = 0; valueIndex < chapters.Count; valueIndex++)
            {
                yield return chapters[valueIndex];
            }
        }
    }

    public static IReadOnlyList<int> GetChapterNumbers(CardRuleEntryDefinition entry)
    {
        var result = new List<int>();
        if (entry == null || entry.kind != CardRuleEntryKind.Chapter) return result;

        string[] values = (entry.chapterNumbers ?? string.Empty).Split(
            new[] { ',', '\uff0c', ';', '\uff1b', ' ' },
            StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < values.Length; i++)
        {
            if (int.TryParse(values[i], out int chapter)
                && chapter > 0
                && !result.Contains(chapter))
            {
                result.Add(chapter);
            }
        }

        result.Sort();
        return result;
    }

    public int GetFinalChapterNumber()
    {
        int finalChapter = 0;
        foreach (int chapter in EnumerateChapterNumbers())
        {
            finalChapter = Math.Max(finalChapter, chapter);
        }

        return finalChapter;
    }

    public bool CanCastFromHand(bool cardHasSplitParts, bool cardHasRoomDoors)
    {
        if (Role == CardFeatureRole.SplitPart || Role == CardFeatureRole.RoomDoor)
        {
            return true;
        }

        if (Role == CardFeatureRole.Adventure || Role == CardFeatureRole.Omen)
        {
            return true;
        }

        if (Role == CardFeatureRole.Prepare
            || Role == CardFeatureRole.ClassLevel
            || Role == CardFeatureRole.CaseStage)
        {
            return false;
        }

        // V7 stores the first half/door as Primary and additional regions with
        // SplitPart/RoomDoor. The primary region remains a castable choice.
        return IsPrimary;
    }

    public bool CanCastFromAdventureExile()
    {
        return IsPrimary;
    }

    public bool CanCastAsPreparedCopy()
    {
        return Role == CardFeatureRole.Prepare;
    }

    public override string ToString()
    {
        return $"{CardName} [{Role} {FaceIndex}:{FeatureIndex}]";
    }
}

public static class CardFeatureRuntimeFactory
{
    public static List<CardFeatureRuntime> Create(CardDefinition card)
    {
        var result = new List<CardFeatureRuntime>();
        if (card == null) return result;

        card.Normalize();
        for (int i = 0; i < card.features.Count; i++)
        {
            CardFeatureDefinition feature = card.features[i];
            if (feature != null)
            {
                result.Add(new CardFeatureRuntime(card, feature));
            }
        }

        result.Sort(CompareFeatures);
        return result;
    }

    private static int CompareFeatures(CardFeatureRuntime left, CardFeatureRuntime right)
    {
        int face = left.FaceIndex.CompareTo(right.FaceIndex);
        return face != 0 ? face : left.FeatureIndex.CompareTo(right.FeatureIndex);
    }
}

public static class CardTargetModeInference
{
    public static CardTargetMode Infer(
        string rulesText,
        IReadOnlyList<CardEffectDefinition> effects = null)
    {
        string text = rulesText ?? string.Empty;
        if (effects != null)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                CardEffectDefinition effect = effects[i];
                if (effect == null) continue;
                text += $" {effect.trigger} {effect.effectKey} {effect.parametersJson}";
            }
        }

        text = text.ToUpperInvariant();
        if (!ContainsTargetWord(text)) return CardTargetMode.None;

        bool opponent = text.Contains("OPPONENT") || text.Contains("\u5bf9\u624b");
        bool friendly = text.Contains("YOU CONTROL") || text.Contains("\u4f60\u63a7\u5236");
        bool creatureOrPlayer = text.Contains("CREATURE OR PLAYER")
            || text.Contains("ANY TARGET")
            || text.Contains("\u751f\u7269\u6216\u73a9\u5bb6")
            || text.Contains("\u4efb\u610f\u76ee\u6807");
        if (creatureOrPlayer)
        {
            return opponent
                ? CardTargetMode.OpponentCreatureOrPlayer
                : CardTargetMode.AnyCreatureOrPlayer;
        }

        bool creature = text.Contains("CREATURE") || text.Contains("\u751f\u7269");
        if (creature)
        {
            if (friendly) return CardTargetMode.FriendlyCreature;
            return opponent ? CardTargetMode.OpponentCreature : CardTargetMode.AnyCreature;
        }

        bool permanent = text.Contains("PERMANENT") || text.Contains("\u6c38\u4e45\u7269");
        if (permanent)
        {
            if (friendly) return CardTargetMode.FriendlyPermanent;
            return opponent ? CardTargetMode.OpponentPermanent : CardTargetMode.AnyPermanent;
        }

        bool player = text.Contains("PLAYER") || text.Contains("\u73a9\u5bb6");
        if (player)
        {
            return opponent ? CardTargetMode.OpponentPlayer : CardTargetMode.AnyPlayer;
        }

        return CardTargetMode.None;
    }

    private static bool ContainsTargetWord(string text)
    {
        return text.Contains("TARGET")
            || text.Contains("\u76ee\u6807")
            || text.Contains("\u4efb\u610f\u4e00\u4e2a");
    }
}
