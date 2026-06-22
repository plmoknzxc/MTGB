namespace MTGB.CardDatabaseEditor;

[Flags]
internal enum CardTypeFlags
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

internal sealed class CardSummary
{
    public required string CardKey { get; init; }
    public required int CardId { get; init; }
    public required string SetCode { get; init; }
    public required string CollectorNumber { get; init; }
    public required string Name { get; init; }
    public required CardTypeFlags Types { get; init; }
}

internal sealed class CardRecord
{
    public string? OriginalCardKey { get; set; }
    public int CardId { get; set; }
    public string OracleId { get; set; } = string.Empty;
    public string SetCode { get; set; } = string.Empty;
    public string CollectorNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CardTypeFlags Types { get; set; }
    public string ManaCost { get; set; } = string.Empty;
    public string RulesText { get; set; } = string.Empty;
    public int Power { get; set; }
    public int Toughness { get; set; }
    public string ScriptPath { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<CardEffectRecord> Effects { get; } = new();

    public string CardKey => $"{SetCode.Trim().ToUpperInvariant()}/{CollectorNumber.Trim()}";
}

internal sealed class CardEffectRecord
{
    public int Order { get; set; }
    public string Trigger { get; set; } = "on_play";
    public string EffectKey { get; set; } = string.Empty;
    public string ParametersJson { get; set; } = "{}";
}

internal sealed class DatabaseValidationResult
{
    public int SchemaVersion { get; init; }
    public int CardCount { get; init; }
    public int EffectCount { get; init; }
    public List<string> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;
}
