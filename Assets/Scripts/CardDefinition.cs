using System;
using System.Collections.Generic;
using UnityEngine;

public enum CardFeatureRole
{
    Primary = 0,
    Generic = 1,
    Adventure = 2,
    Omen = 3,
    Prepare = 4,
    RoomDoor = 5,
    SplitPart = 6,
    ClassLevel = 7,
    CaseStage = 8
}

public enum CardRuleEntryKind
{
    Text = 0,
    Chapter = 1
}

[Serializable]
public class CardRuleEntryDefinition
{
    public int order;
    public CardRuleEntryKind kind;
    public string chapterNumbers;
    public string text;
}

[Serializable]
public class CardFeatureDefinition
{
    public int faceIndex;
    public int featureIndex;
    public CardFeatureRole role;
    public string roleValue;
    public string cardName;
    public CardType cardTypes;
    public CardSupertype supertypes;
    public string subtypes;
    public string description;
    public string manaCost;
    public int attack;
    public int defence;
    public int loyalty;
    public int battleDefense;
    public string powerExpression;
    public string toughnessExpression;
    public List<CardRuleEntryDefinition> ruleEntries = new List<CardRuleEntryDefinition>();
    public List<CardEffectDefinition> effects = new List<CardEffectDefinition>();
    public List<CardStringDefinition> strings = new List<CardStringDefinition>();
}

[Serializable]
public class CardEffectDefinition
{
    public int order;
    public string trigger;
    public string effectKey;
    public string parametersJson;
}

[Serializable]
public class CardStringDefinition
{
    public int index;
    public string text;
}

[Serializable]
public class CardDefinition
{
    public string cardName;
    public int cardID;
    public string oracleID;
    public string setCode;
    public int collectorNumber;
    public string collectorNumberText;
    public CardType cardTypes;
    public CardSupertype supertypes;
    public string subtypes;
    public string description;
    public string manaCost;
    public ManaColorFlags colorIndicator;
    public CardTargetMode targetMode;
    public int attack;
    public int defence;
    public int loyalty;
    public int battleDefense;
    public Sprite artwork;
    public string luaScript;
    public string effectKey;
    public bool customContent;
    public List<CardFeatureDefinition> features = new List<CardFeatureDefinition>();
    [NonSerialized] public bool artworkRuntime;

    public string name
    {
        get => cardName;
        set => cardName = value;
    }

    [Obsolete("Use manaCost or GetManaCost().")]
    public int cost
    {
        get => GetManaCost().Generic;
        set => manaCost = value > 0 ? $"{{{value}}}" : string.Empty;
    }

    public string CollectorNumber => !string.IsNullOrWhiteSpace(collectorNumberText)
        ? collectorNumberText.Trim()
        : collectorNumber > 0 ? collectorNumber.ToString() : string.Empty;

    public string PrintingKey => string.IsNullOrWhiteSpace(setCode) || string.IsNullOrEmpty(CollectorNumber)
        ? string.Empty
        : $"{setCode.Trim()}/{CollectorNumber}";

    public bool HasType(CardType type) => (cardTypes & type) != 0;

    public bool HasSupertype(CardSupertype type) => (supertypes & type) != 0;

    public string GetTypeLine()
    {
        string supertypeText = supertypes.ToDisplayString();
        string typeText = cardTypes.ToDisplayString();
        string subtypeText = string.IsNullOrWhiteSpace(subtypes) ? string.Empty : subtypes.Trim();

        string line = string.Join(" ", new[] { supertypeText, typeText }).Trim();
        if (!string.IsNullOrEmpty(subtypeText))
        {
            line = string.IsNullOrEmpty(line) ? subtypeText : $"{line} - {subtypeText}";
        }
        return line;
    }

    public ManaCost GetManaCost()
    {
        return ManaCost.Parse(manaCost);
    }

    public ManaColorFlags GetColors()
    {
        if (colorIndicator != ManaColorFlags.None) return colorIndicator;

        ManaCost cost = GetManaCost();
        ManaColorFlags colors = ManaColorFlags.None;
        if (cost.White > 0) colors |= ManaColorFlags.White;
        if (cost.Blue > 0) colors |= ManaColorFlags.Blue;
        if (cost.Black > 0) colors |= ManaColorFlags.Black;
        if (cost.Red > 0) colors |= ManaColorFlags.Red;
        if (cost.Green > 0) colors |= ManaColorFlags.Green;
        return colors;
    }

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(collectorNumberText) && collectorNumber > 0)
        {
            collectorNumberText = collectorNumber.ToString();
        }

        if (features == null)
        {
            features = new List<CardFeatureDefinition>();
        }

        if (features.Count == 0)
        {
            features.Add(CreatePrimaryFeature());
        }
    }

    public CardFeatureDefinition GetPrimaryFeature()
    {
        if (features != null)
        {
            for (int i = 0; i < features.Count; i++)
            {
                CardFeatureDefinition feature = features[i];
                if (feature != null && feature.faceIndex == 0 && feature.featureIndex == 0)
                {
                    return feature;
                }
            }
        }

        return CreatePrimaryFeature();
    }

    private CardFeatureDefinition CreatePrimaryFeature()
    {
        return new CardFeatureDefinition
        {
            faceIndex = 0,
            featureIndex = 0,
            role = CardFeatureRole.Primary,
            roleValue = string.Empty,
            cardName = cardName,
            cardTypes = cardTypes,
            supertypes = supertypes,
            subtypes = subtypes,
            description = description,
            manaCost = manaCost,
            attack = attack,
            defence = defence,
            loyalty = loyalty,
            battleDefense = battleDefense,
            powerExpression = attack.ToString(),
            toughnessExpression = defence.ToString()
        };
    }
}
