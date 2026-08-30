using System;

public enum ContinuousEffectLayer
{
    Copy = 1,
    Control = 2,
    Text = 3,
    Type = 4,
    Color = 5,
    Ability = 6,
    PowerToughness = 7
}

public enum PowerToughnessSublayer
{
    CharacteristicDefining = 1,
    Set = 2,
    Modify = 3,
    Switch = 4
}

public enum CopyEffectSublayer
{
    Copy = 1,
    FaceDown = 2
}

public enum ContinuousEffectDuration
{
    Permanent = 0,
    UntilEndOfTurn = 1,
    WhileSourceRemains = 2
}

public enum ContinuousEffectOperation
{
    AddCardType = 0,
    RemoveCardType = 1,
    SetColors = 2,
    AddKeyword = 3,
    RemoveKeyword = 4,
    SetPowerToughness = 5,
    ModifyPowerToughness = 6,
    SwitchPowerToughness = 7,
    SetController = 8,
    ReplaceRulesText = 9,
    SetCardTypes = 10,
    RemoveAllKeywords = 11,
    CopyCharacteristics = 12,
    SetSupertypes = 13,
    AddSupertype = 14,
    RemoveSupertype = 15,
    SetSubtypes = 16,
    SetName = 17,
    SetFaceDownCharacteristics = 18
}

[Flags]
public enum ContinuousEffectCharacteristic
{
    None = 0,
    Copy = 1 << 0,
    Control = 1 << 1,
    Text = 1 << 2,
    Type = 1 << 3,
    Color = 1 << 4,
    Ability = 1 << 5,
    PowerToughness = 1 << 6,
    All = Copy | Control | Text | Type | Color | Ability | PowerToughness
}

[Serializable]
public sealed class ContinuousEffectRuntime
{
    public long effectId;
    public long timestamp;
    public ContinuousEffectLayer layer;
    public CopyEffectSublayer copyEffectSublayer;
    public PowerToughnessSublayer powerToughnessSublayer;
    public ContinuousEffectDuration duration;
    public ContinuousEffectOperation operation;
    public RuntimeCard source;
    public int sourceZoneChangeVersion;
    public string sourceKey;
    public string linkId;
    public string dependencyKeys;
    public bool characteristicDefining;
    public ContinuousEffectCharacteristic reads;
    public ContinuousEffectCharacteristic writes;
    public RuntimeCard copySource;
    public int copySourceZoneChangeVersion = -1;
    public CardType cardTypes;
    public CardSupertype supertypes;
    public ManaColorFlags colors;
    public string keyword;
    public string rulesText;
    public string subtypes;
    public string cardName;
    public int controllerPlayerId;
    public int power;
    public int toughness;

    public bool DependsOn(ContinuousEffectRuntime other)
    {
        if (other == null
            || string.IsNullOrWhiteSpace(other.linkId)
            || string.IsNullOrWhiteSpace(dependencyKeys))
        {
            return false;
        }

        string[] values = dependencyKeys.Split(
            new[] { ',', ';', '|' },
            StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < values.Length; i++)
        {
            if (string.Equals(
                    values[i].Trim(),
                    other.linkId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    public bool StructurallyDependsOn(ContinuousEffectRuntime other)
    {
        if (other == null || ReferenceEquals(this, other)) return false;
        if ((reads & other.GetWrittenCharacteristics()) == 0) return false;

        // Effects from characteristic-defining abilities do not depend on
        // non-CDA effects. CR 613.8a also excludes a CDA/non-CDA dependency.
        return characteristicDefining == other.characteristicDefining;
    }

    public ContinuousEffectCharacteristic GetWrittenCharacteristics()
    {
        if (writes != ContinuousEffectCharacteristic.None) return writes;

        switch (operation)
        {
            case ContinuousEffectOperation.CopyCharacteristics:
            case ContinuousEffectOperation.SetFaceDownCharacteristics:
                return ContinuousEffectCharacteristic.Copy;
            case ContinuousEffectOperation.SetController:
                return ContinuousEffectCharacteristic.Control;
            case ContinuousEffectOperation.ReplaceRulesText:
            case ContinuousEffectOperation.SetName:
                return ContinuousEffectCharacteristic.Text;
            case ContinuousEffectOperation.SetCardTypes:
            case ContinuousEffectOperation.AddCardType:
            case ContinuousEffectOperation.RemoveCardType:
            case ContinuousEffectOperation.SetSupertypes:
            case ContinuousEffectOperation.AddSupertype:
            case ContinuousEffectOperation.RemoveSupertype:
            case ContinuousEffectOperation.SetSubtypes:
                return ContinuousEffectCharacteristic.Type;
            case ContinuousEffectOperation.SetColors:
                return ContinuousEffectCharacteristic.Color;
            case ContinuousEffectOperation.AddKeyword:
            case ContinuousEffectOperation.RemoveKeyword:
            case ContinuousEffectOperation.RemoveAllKeywords:
                return ContinuousEffectCharacteristic.Ability;
            case ContinuousEffectOperation.SetPowerToughness:
            case ContinuousEffectOperation.ModifyPowerToughness:
            case ContinuousEffectOperation.SwitchPowerToughness:
                return ContinuousEffectCharacteristic.PowerToughness;
            default:
                return ContinuousEffectCharacteristic.None;
        }
    }

    public bool IsActive
    {
        get
        {
            if (duration == ContinuousEffectDuration.WhileSourceRemains
                && (source == null
                    || source.ZoneChangeVersion != sourceZoneChangeVersion
                    || source.CurrentZone != GameZone.Battlefield))
            {
                return false;
            }

            return operation != ContinuousEffectOperation.CopyCharacteristics
                || copySource == null
                || copySourceZoneChangeVersion < 0
                || copySource.ZoneChangeVersion == copySourceZoneChangeVersion;
        }
    }
}
