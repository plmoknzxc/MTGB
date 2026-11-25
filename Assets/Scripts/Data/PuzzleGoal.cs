using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class PuzzleGoal
{
    public string mode = "all";
    public string checkAt = "state";
    public List<PuzzleCondition> conditions = new();

    public void Validate()
    {
        if (mode != "all" && mode != "any") throw new ArgumentException("goal.mode 需要填写 all 或 any。");
        if (checkAt != "state" && checkAt != "win" && checkAt != "survive")
            throw new ArgumentException("goal.checkAt 应为 state、win 或 survive。");
        if (conditions == null || conditions.Count < 1 || conditions.Count > 32)
            throw new ArgumentException("组合目标需要 1～32 项条件。");
        for (int i = 0; i < conditions.Count; i++)
        {
            try { (conditions[i] ?? throw new ArgumentException("条件为空。")).Validate(); }
            catch (ArgumentException e) { throw new ArgumentException($"goal.conditions[{i}]：{e.Message}"); }
        }
    }
}

[Serializable]
public sealed class PuzzleCondition
{
    public string metric, player, op, zone, name, cardType;
    public int value = -1;
    public static readonly string[] Metrics = { "life", "damageTakenThisTurn", "cardCount" };
    public static readonly string[] Players = { "you", "opponent" };
    public static readonly string[] Comparisons = { "atLeast", "atMost", "exactly" };
    public static readonly string[] CardTypes = { "Creature", "Land", "Artifact", "Enchantment", "Planeswalker", "Battle", "Instant", "Sorcery", "Kindred" };
    public static readonly string[] TypeNames = { "生物", "地", "神器", "结界", "鹏洛客", "战役", "瞬间", "法术", "亲缘" };
    public static readonly string[] ZoneNames = { "战场", "手牌", "牌库", "坟墓场", "放逐区" };

    public void Validate()
    {
        if (!Metrics.Contains(metric)) throw new ArgumentException("未知的 metric：" + metric);
        if (!Players.Contains(player)) throw new ArgumentException("player 应为 you 或 opponent。");
        if (!Comparisons.Contains(op)) throw new ArgumentException("op 应为 atLeast、atMost 或 exactly。");
        if (value < 0) throw new ArgumentException("value 需要填写非负整数。");
        if (metric == "cardCount") {
            if (!LocalPuzzle.Zones.Contains(zone)) throw new ArgumentException("cardCount 需要有效的 zone。");
            if (!string.IsNullOrEmpty(cardType) && !CardTypes.Contains(cardType)) throw new ArgumentException("未知的 cardType：" + cardType);
        } else if (!string.IsNullOrEmpty(zone) || !string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(cardType))
            throw new ArgumentException("只有 cardCount 可以指定 zone、name 和 cardType。");
    }

    public string Describe()
    {
        string subject = player == "you" ? "我方" : "对方";
        string comparison = op == "atLeast" ? "至少" : op == "atMost" ? "至多" : "恰好";
        if (metric == "life") return $"{subject}生命{comparison} {value} 点";
        if (metric == "damageTakenThisTurn") return $"{subject}在当前回合受到的伤害{comparison} {value} 点";
        string type = string.IsNullOrEmpty(cardType) ? "牌" : TypeNames[Array.IndexOf(CardTypes,cardType)] + "牌";
        string card = string.IsNullOrEmpty(name) ? type : $"名为《{GamePreferences.CardFaceName(name)}》的{type}";
        return $"{subject}{ZoneNames[Array.IndexOf(LocalPuzzle.Zones,zone)]}中{card}{comparison} {value} 张";
    }
}
