using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class PuzzleFileJson
{
    static readonly string[] Sides = PuzzleCondition.Players;
    static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings {
        MissingMemberHandling = MissingMemberHandling.Error, NullValueHandling = NullValueHandling.Ignore
    });

    public static LocalPuzzle Read(string json)
    {
        var data = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        // Local editor hint only. Loading a puzzle never needs the schema or a network request.
        data.Remove("$schema");
        if (data["author"] is JToken author && author.Type != JTokenType.String && author.Type != JTokenType.Null)
            throw new InvalidDataException("author 应填写玩家名称。");
        int version = (int?)data["version"] ?? (data["goal"] != null ? 3 : 1);
        if (version < 1 || version > LocalPuzzle.Version) throw new InvalidDataException("未知的残局版本：" + version);
        if (version >= 3)
        {
            if (data["survive"] != null) throw new InvalidDataException("版本 3 请用 goal.mode 指定目标。");
            IndexField(data, "firstSide", Sides);
            IndexField(data, "phase", LocalPuzzle.Phases);
            if (data["cards"] is not JArray cards) throw new InvalidDataException("cards 需要是数组。");
            foreach (var token in cards)
            {
                if (token is not JObject row) throw new InvalidDataException(token.Path + " 需要是卡牌对象。");
                IndexField(row,"side",Sides);
                IndexField(row,"zone",LocalPuzzle.Zones);
            }
            if (data["goal"] is not JObject goal) throw new InvalidDataException("请填写 goal.mode。");
            string mode = (string)goal["mode"];
            if (mode == "win" || mode == "survive")
            {
                if (goal.Properties().Any(p=>p.Name!="mode")) throw new InvalidDataException("win 和 survive 不接受其他目标字段。");
                data["survive"] = mode == "survive";
                data.Remove("goal");
            }
            else if (mode != "all" && mode != "any") throw new InvalidDataException("未知的 goal.mode：" + mode);
            else if (goal["conditions"] is JArray rules)
            {
                foreach (var rule in rules)
                    if (rule is not JObject condition || condition["value"]?.Type != JTokenType.Integer)
                        throw new InvalidDataException(rule.Path + ".value 需要填写非负整数。");
            }
        }
        var puzzle = data.ToObject<LocalPuzzle>(Serializer);
        puzzle.version = version;
        puzzle.Validate();
        return puzzle;
    }

    static void IndexField(JObject data, string key, string[] choices)
    {
        var token = data[key];
        if (token == null) return;
        int i = token.Type == JTokenType.String ? Array.IndexOf(choices, (string)token) : -1;
        if (i < 0) throw new InvalidDataException($"{token.Path} 应为 {string.Join(" / ", choices)}。");
        data[key] = i;
    }

    public static string Write(LocalPuzzle puzzle)
    {
        puzzle.Validate();
        var data = new JObject { ["version"] = LocalPuzzle.Version, ["title"] = puzzle.title };
        if (!string.IsNullOrWhiteSpace(puzzle.author)) data["author"] = puzzle.author;
        if (!string.IsNullOrEmpty(puzzle.id)) data["id"] = puzzle.id;
        if (!string.IsNullOrEmpty(puzzle.description)) data["description"] = puzzle.description;
        var goal = new JObject { ["mode"] = puzzle.goal?.mode ?? (puzzle.survive ? "survive" : "win") };
        if (puzzle.goal != null)
        {
            if (puzzle.goal.checkAt != "state") goal["checkAt"] = puzzle.goal.checkAt;
            var conditions = new JArray();
            foreach (var rule in puzzle.goal.conditions)
            {
                var condition = new JObject {
                    ["metric"] = rule.metric, ["player"] = rule.player, ["op"] = rule.op, ["value"] = rule.value
                };
                if (rule.metric == "cardCount")
                {
                    condition["zone"] = rule.zone;
                    if (!string.IsNullOrEmpty(rule.name)) condition["name"] = rule.name;
                    if (!string.IsNullOrEmpty(rule.cardType)) condition["cardType"] = rule.cardType;
                }
                conditions.Add(condition);
            }
            goal["conditions"] = conditions;
        }
        data["goal"] = goal;
        data["turns"] = puzzle.turns;
        data["humanLife"] = puzzle.humanLife;
        data["aiLife"] = puzzle.aiLife;
        data["firstSide"] = Sides[puzzle.firstSide];
        data["phase"] = LocalPuzzle.Phases[puzzle.phase];
        data["seed"] = puzzle.seed;
        if (puzzle.orderedBattlefield) data["orderedBattlefield"] = true;
        if (puzzle.hasTableRows) data["hasTableRows"] = true;
        var cards = new JArray();
        var defaults = JObject.Parse(JsonUtility.ToJson(new PuzzleCard()));
        foreach (var row in puzzle.cards)
        {
            var card = new JObject { ["name"] = row.name, ["side"] = Sides[row.side], ["zone"] = LocalPuzzle.Zones[row.zone] };
            foreach (var field in JObject.Parse(JsonUtility.ToJson(row)).Properties())
                if (card[field.Name] == null && !JToken.DeepEquals(field.Value, defaults[field.Name]))
                    card[field.Name] = field.Value;
            cards.Add(card);
        }
        data["cards"] = cards;
        return data.ToString(Formatting.Indented) + "\n";
    }
}
