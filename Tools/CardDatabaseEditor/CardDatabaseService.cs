using System.Text.Json;

namespace MTGB.CardDatabaseEditor;

internal sealed class CardDatabaseService : IDisposable
{
    public const int SupportedSchemaVersion = 1;

    private readonly NativeSqlite database;

    public CardDatabaseService(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        database = NativeSqlite.Open(Path, false);
        int version = ReadSchemaVersion();
        if (version != SupportedSchemaVersion)
        {
            database.Dispose();
            throw new InvalidDataException($"不支持的数据库版本 {version}，当前编辑器需要版本 {SupportedSchemaVersion}。");
        }
    }

    public string Path { get; }

    public static void Create(string path, string schemaPath)
    {
        if (File.Exists(path)) throw new IOException("目标数据库已经存在。");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);

        using NativeSqlite created = NativeSqlite.Open(path, true);
        created.Execute(File.ReadAllText(schemaPath));
        created.ExecuteNonQuery("INSERT INTO metadata(key, value) VALUES (?, ?);", "schema_version", SupportedSchemaVersion.ToString());
        created.ExecuteNonQuery("INSERT INTO metadata(key, value) VALUES (?, ?);", "database_id", $"custom.{Guid.NewGuid():N}");
        created.ExecuteNonQuery("INSERT INTO metadata(key, value) VALUES (?, ?);", "display_name", System.IO.Path.GetFileNameWithoutExtension(path));
    }

    public List<CardSummary> LoadSummaries()
    {
        var result = new List<CardSummary>();
        database.Query(@"
SELECT card_key, card_id, set_code, collector_number, name, type_flags
FROM cards
ORDER BY name COLLATE NOCASE, set_code, collector_number;", row =>
        {
            result.Add(new CardSummary
            {
                CardKey = row.String(0),
                CardId = row.Int32(1),
                SetCode = row.String(2),
                CollectorNumber = row.String(3),
                Name = row.String(4),
                Types = (CardTypeFlags)row.Int32(5)
            });
        });
        return result;
    }

    public CardRecord LoadCard(string cardKey)
    {
        CardRecord? card = null;
        database.Query(@"
SELECT card_key, card_id, oracle_id, set_code, collector_number, name,
       type_flags, mana_cost, rules_text, power, toughness, script_path, enabled
FROM cards WHERE card_key = ? LIMIT 1;", row =>
        {
            card = new CardRecord
            {
                OriginalCardKey = row.String(0),
                CardId = row.Int32(1),
                OracleId = row.String(2),
                SetCode = row.String(3),
                CollectorNumber = row.String(4),
                Name = row.String(5),
                Types = (CardTypeFlags)row.Int32(6),
                ManaCost = row.String(7),
                RulesText = row.String(8),
                Power = row.Int32(9),
                Toughness = row.Int32(10),
                ScriptPath = row.String(11),
                Enabled = row.Int32(12) != 0
            };
        }, cardKey);

        if (card == null) throw new KeyNotFoundException($"找不到卡牌 {cardKey}。");
        database.Query(@"
SELECT effect_order, trigger, effect_key, parameters_json
FROM card_effects WHERE card_key = ? ORDER BY effect_order;", row =>
        {
            card.Effects.Add(new CardEffectRecord
            {
                Order = row.Int32(0),
                Trigger = row.String(1),
                EffectKey = row.String(2),
                ParametersJson = row.String(3)
            });
        }, cardKey);
        return card;
    }

    public void Save(CardRecord card)
    {
        ValidateCard(card);
        string newKey = card.CardKey;

        database.Execute("BEGIN IMMEDIATE;");
        try
        {
            if (card.OriginalCardKey == null)
            {
                database.ExecuteNonQuery(@"
INSERT INTO cards(card_key, card_id, oracle_id, set_code, collector_number, name,
                  type_flags, mana_cost, rules_text, power, toughness, script_path, enabled)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);",
                    newKey, card.CardId, card.OracleId.Trim(), card.SetCode.Trim().ToUpperInvariant(),
                    card.CollectorNumber.Trim(), card.Name.Trim(), (int)card.Types, card.ManaCost.Trim(),
                    card.RulesText, card.Power, card.Toughness, card.ScriptPath.Trim(), card.Enabled);
            }
            else
            {
                database.ExecuteNonQuery("DELETE FROM card_effects WHERE card_key = ?;", card.OriginalCardKey);
                int changed = database.ExecuteNonQuery(@"
UPDATE cards SET card_key = ?, card_id = ?, oracle_id = ?, set_code = ?, collector_number = ?,
                 name = ?, type_flags = ?, mana_cost = ?, rules_text = ?, power = ?, toughness = ?,
                 script_path = ?, enabled = ?
WHERE card_key = ?;",
                    newKey, card.CardId, card.OracleId.Trim(), card.SetCode.Trim().ToUpperInvariant(),
                    card.CollectorNumber.Trim(), card.Name.Trim(), (int)card.Types, card.ManaCost.Trim(),
                    card.RulesText, card.Power, card.Toughness, card.ScriptPath.Trim(), card.Enabled,
                    card.OriginalCardKey);
                if (changed != 1) throw new InvalidOperationException("保存失败：原卡牌记录不存在。");
            }

            for (int i = 0; i < card.Effects.Count; i++)
            {
                CardEffectRecord effect = card.Effects[i];
                database.ExecuteNonQuery(@"
INSERT INTO card_effects(card_key, effect_order, trigger, effect_key, parameters_json)
VALUES (?, ?, ?, ?, ?);",
                    newKey, effect.Order, effect.Trigger.Trim(), effect.EffectKey.Trim(), effect.ParametersJson.Trim());
            }

            database.Execute("COMMIT;");
            card.OriginalCardKey = newKey;
        }
        catch
        {
            database.Execute("ROLLBACK;");
            throw;
        }
    }

    public void Delete(string cardKey)
    {
        database.ExecuteNonQuery("DELETE FROM cards WHERE card_key = ?;", cardKey);
    }

    public DatabaseValidationResult ValidateDatabase()
    {
        var result = new DatabaseValidationResult
        {
            SchemaVersion = ReadSchemaVersion(),
            CardCount = database.ScalarInt("SELECT COUNT(*) FROM cards;"),
            EffectCount = database.ScalarInt("SELECT COUNT(*) FROM card_effects;")
        };

        if (result.SchemaVersion != SupportedSchemaVersion)
            result.Errors.Add($"Schema 版本应为 {SupportedSchemaVersion}，实际为 {result.SchemaVersion}。");

        database.Query(@"
SELECT card_key FROM cards
WHERE trim(name) = '' OR trim(set_code) = '' OR trim(collector_number) = '' OR card_id <= 0;",
            row => result.Errors.Add($"卡牌 {row.String(0)} 缺少必要字段。"));

        database.Query(@"
SELECT card_key, parameters_json FROM card_effects;", row =>
        {
            try { JsonDocument.Parse(row.String(1)).Dispose(); }
            catch { result.Errors.Add($"卡牌 {row.String(0)} 的效果参数不是有效 JSON。"); }
        });

        return result;
    }

    public void Dispose() => database.Dispose();

    private int ReadSchemaVersion()
    {
        string? value = database.ScalarString("SELECT value FROM metadata WHERE key = 'schema_version' LIMIT 1;");
        return int.TryParse(value, out int version) ? version : 0;
    }

    private static void ValidateCard(CardRecord card)
    {
        if (card.CardId <= 0) throw new InvalidDataException("内部卡牌 ID 必须大于 0。");
        if (string.IsNullOrWhiteSpace(card.Name)) throw new InvalidDataException("卡名不能为空。");
        if (string.IsNullOrWhiteSpace(card.SetCode)) throw new InvalidDataException("系列代号不能为空。");
        if (card.SetCode.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidDataException("系列代号包含不能用于图片目录的字符。");
        if (string.IsNullOrWhiteSpace(card.CollectorNumber)) throw new InvalidDataException("收藏编号不能为空。");
        if (card.CollectorNumber.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidDataException("收藏编号包含不能用于图片文件名的字符。");
        if (card.Types == CardTypeFlags.None) throw new InvalidDataException("至少选择一种卡牌类型。");

        var orders = new HashSet<int>();
        foreach (CardEffectRecord effect in card.Effects)
        {
            if (!orders.Add(effect.Order)) throw new InvalidDataException($"效果顺序 {effect.Order} 重复。");
            if (string.IsNullOrWhiteSpace(effect.Trigger)) throw new InvalidDataException("效果触发时机不能为空。");
            if (string.IsNullOrWhiteSpace(effect.EffectKey)) throw new InvalidDataException("效果键不能为空。");
            try { JsonDocument.Parse(effect.ParametersJson).Dispose(); }
            catch (JsonException exception) { throw new InvalidDataException($"效果 {effect.EffectKey} 的参数 JSON 无效：{exception.Message}"); }
        }
    }
}
