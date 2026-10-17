using System;
using System.Linq;
using System.Text.RegularExpressions;

// Small grammars for engine result messages, with typed numbers and identities.
// Printed effects continue to use their exact source paragraph.
internal static class NativeMessageText
{
    static string L(string zh, string ja) => GamePreferences.Language == "ja-JP" ? ja : zh;
    internal static bool UnsafeArguments(Match match)
    {
        // Typed result grammars own these fields; unknown identities, amounts
        // and clauses must not be swallowed by the generic damage format.
        if (Regex.IsMatch(match.Value, @"^.+ deals .+ damage to .+\.$")) return true;
        foreach (string name in new[] { "p0", "p1", "p2", "p3", "p4", "p5", "p6", "p7" })
        {
            var group = match.Groups[name];
            if (!group.Success || GamePreferences.IsCardIdentity(group.Value)) continue;
            if (Regex.IsMatch(group.Value, @"[.!?]\s+[A-Z]|[\r\n]")) return true;
        }
        return false;
    }
    static string Entity(string source)
    {
        source = source.Trim();
        var player = Regex.Match(source, @"^Player ([0-9]+)$");
        if (player.Success) return L("玩家 " + player.Groups[1].Value, "プレイヤー" + player.Groups[1].Value);
        string bare=Regex.Replace(source,@" \([0-9]+\)$","");
        if (GamePreferences.IsCardIdentity(source)) return GamePreferences.CardFaceName(source);
        var members=Regex.Split(source,@", | and ");
        if(members.Length>1) {
            var names=members.Select(Entity).ToArray();
            if(names.All(n=>n!=null))return string.Join(L("、","、"),names);
        }
        string tokenName=DeckTokenLinks.GeneratedTokenName(bare);
        if (tokenName!=null) return tokenName;
        return source switch
        {
            "any target" => L("任意一个目标", "対象１つ"),
            "target creature" => L("目标生物", "対象のクリーチャー"),
            "target opponent or planeswalker" => L("目标对手或鹏洛客", "対象の対戦相手やプレインズウォーカー"),
            "each creature" => L("每个生物", "各クリーチャー"),
            "each creature you don't control" => L("每个不由你操控的生物", "あなたがコントロールしていない各クリーチャー"),
            "this creature" or "This creature" => L("此生物", "このクリーチャー"),
            "this permanent" or "This permanent" => L("此永久物", "このパーマネント"),
            _ => null
        };
    }
    internal static bool TryTranslate(string source, out string translated)
    {
        translated = source;
        if (GamePreferences.Language == "en-US" || string.IsNullOrEmpty(source)) return false;
        bool latin=false;
        foreach(char c in source)if(c>='A'&&c<='Z'||c>='a'&&c<='z'){latin=true;break;}
        if(!latin)return false;
        // Only split before a new explicit engine clause. Card names containing
        // periods are validated as identities by the single-sentence grammars.
        var clauses = Regex.Split(source, @"(?<=\.)\s+(?=Player [0-9]+ (?:draws|scries|gains|loses|perpetually)\b)");
        if (clauses.Length > 1)
        {
            var parts = clauses.Select(c => TrySingle(c, out var text) ? text : GamePreferences.NativeText(c)).ToArray();
            if (parts.Where((p, i) => p != clauses[i]).Any()) { translated = string.Join("\n", parts); return true; }
            return false;
        }
        return TrySingle(source, out translated);
    }
    static bool TrySingle(string source, out string translated)
    {
        translated = source;
        if(NativeGeneratedText.TryTranslate(source,out translated))return true;
        var perPermanent=Regex.Match(source,@"^This creature gets ([+-][0-9]+)/([+-][0-9]+) for each (artifact|creature|enchantment|land|permanent) you control\.$");
        if(perPermanent.Success) {
            string kind=perPermanent.Groups[3].Value;
            string type=GamePreferences.T(char.ToUpperInvariant(kind[0])+kind.Substring(1));
            string stats=perPermanent.Groups[1].Value+"/"+perPermanent.Groups[2].Value;
            translated=L("你每操控一个"+type+"，此生物便得"+stats+"。","あなたがコントロールしている"+type+"１つにつき、このクリーチャーは"+stats+"の修整を受ける。");return true;
        }
        const string symbols=@"(?:\{[0-9WUBRGCSPXYZ/]+\})+";
        var remaining=Regex.Match(source,@"^Remaining mana cost is ("+symbols+@"|0)\.?$");
        if(remaining.Success){translated=L("剩余法术力费用：","残りのマナ・コスト：")+remaining.Groups[1].Value;return true;}
        if(source=="Choose artifact to tap for Improvise.") {
            translated=L("选择一个神器横置以支付拼造费用。","即席でタップするアーティファクト１つを選択。");return true;
        }
        if(source=="Choose creature to tap for Convoke.") {
            translated=L("选择一个生物横置以支付召集费用。","召集でタップするクリーチャー１体を選択。");return true;
        }
        var rejectedMana=Regex.Match(source,@"^The colors provided by (.+) you've chosen cannot be used to pay the manacost of ("+symbols+@")\.?$");
        if(rejectedMana.Success) {
            string name=Entity(rejectedMana.Groups[1].Value);
            if(name!=null){translated=L("所选的"+name+"产生的颜色不能支付法术力费用 ","選択した"+name+"が生み出す色では、マナ・コスト ")+rejectedMana.Groups[2].Value+L("。","を支払えません。");return true;}
        }
        var objectLine=Regex.Match(source,@"^(.+) - ((?:Artifact|Creature|Enchantment|Planeswalker|Battle|Land|Instant|Sorcery)(?: (?:Artifact|Creature|Enchantment|Planeswalker|Battle|Land|Instant|Sorcery))*) (-?[0-9]+|\*) / (-?[0-9]+|\*)$");
        if(objectLine.Success) {
            string name=Entity(objectLine.Groups[1].Value);
            if(name!=null){translated=name+" - "+string.Join(" ",objectLine.Groups[2].Value.Split(' ').Select(GamePreferences.T))+" "+objectLine.Groups[3].Value+" / "+objectLine.Groups[4].Value;return true;}
        }
        var targetPrompt=Regex.Match(source,@"^Select target (artifact|creature|enchantment|instant|land|planeswalker|sorcery) card in your (graveyard|hand|library|exile)\.?$");
        if(targetPrompt.Success) {
            string type=GamePreferences.T(char.ToUpperInvariant(targetPrompt.Groups[1].Value[0])+targetPrompt.Groups[1].Value.Substring(1));
            string zone=GamePreferences.T(char.ToUpperInvariant(targetPrompt.Groups[2].Value[0])+targetPrompt.Groups[2].Value.Substring(1));
            translated=L("选择目标在你的"+zone+"中的"+type+"牌。","あなたの"+zone+"にある"+type+"・カード１枚を対象として選択。");return true;
        }
        var damage = Regex.Match(source, @"^(?<actor>.+?) deals (?<amount>[0-9]+) damage to (?<target>.+)\.$");
        if (damage.Success)
        {
            string actor = Entity(damage.Groups["actor"].Value), target = Entity(damage.Groups["target"].Value), amount = damage.Groups["amount"].Value;
            if (actor != null && target != null)
            {
                translated = L(actor + "对" + target + "造成 " + amount + " 点伤害。", actor + "は" + target + "に" + amount + "点のダメージを与える。");
                return true;
            }
        }
        var intensity = Regex.Match(source, @"^Player ([0-9]+) perpetually increases the intensity of all cards they own named (.+) by ([0-9]+)\.$");
        if (intensity.Success && GamePreferences.IsCardIdentity(intensity.Groups[2].Value))
        {
            string actor = Entity("Player " + intensity.Groups[1].Value), name = GamePreferences.CardFaceName(intensity.Groups[2].Value), amount = intensity.Groups[3].Value;
            translated = L(actor + "使所有由其拥有且名称为“" + name + "”的牌永久增加 " + amount + " 点强度。", actor + "は自分がオーナーであり「" + name + "」という名前のすべてのカードの強度を永久に" + amount + "増やす。");
            return true;
        }
        var resource = Regex.Match(source, @"^Player ([0-9]+) (draws (a|one|two|three|four|five|[0-9]+) cards?|scries ([0-9]+)|(gains|loses) ([0-9]+) life)\.$");
        if (resource.Success)
        {
            string actor = Entity("Player " + resource.Groups[1].Value);
            if (resource.Groups[3].Success)
            {
                string number = resource.Groups[3].Value switch { "a" or "one" => "1", "two" => "2", "three" => "3", "four" => "4", "five" => "5", _ => resource.Groups[3].Value };
                translated = L(actor + "抓 " + number + " 张牌。", actor + "はカードを" + number + "枚引く。");
            }
            else if (resource.Groups[4].Success) translated = L(actor + "占卜 " + resource.Groups[4].Value + "。", actor + "は占術" + resource.Groups[4].Value + "を行う。");
            else
            {
                string amount = resource.Groups[6].Value; bool gain = resource.Groups[5].Value == "gains";
                translated = L(actor + (gain ? "获得 " : "失去 ") + amount + " 点生命。", actor + "は" + amount + "点のライフを" + (gain ? "得る。" : "失う。"));
            }
            return true;
        }
        var departure = Regex.Match(source, @"^(Sacrifice|Exile) (.+) at the beginning of the next end step\.$");
        var end = Regex.Match(source, @"^At the beginning of the (next )?end step, (sacrifice|exile) (.+)\.$");
        if (departure.Success || end.Success)
        {
            string name = Entity(departure.Success ? departure.Groups[2].Value : end.Groups[3].Value);
            if (name == null) return false;
            bool sacrifice = (departure.Success ? departure.Groups[1].Value : end.Groups[2].Value).Equals("sacrifice", StringComparison.OrdinalIgnoreCase);
            bool next = departure.Success || end.Groups[1].Success;
            translated = L("在" + (next ? "下一个" : "") + "结束步骤开始时，" + (sacrifice ? "牺牲" : "放逐") + name + "。",
                (next ? "次の" : "") + "終了ステップの開始時に、" + name + (sacrifice ? "を生け贄に捧げる。" : "を追放する。"));
            return true;
        }
        const string echo = "At the beginning of your upkeep, if this permanent came under your control since the beginning of your last upkeep, sacrifice it unless you pay its echo cost.";
        if (source == echo)
        {
            translated = L("在你的维持开始时，若此永久物是在你上个维持开始后才受你操控，则除非你支付其返响费用，否则牺牲它。", "あなたのアップキープの開始時に、このパーマネントがあなたの前のアップキープの開始時以降にあなたのコントロール下になった場合、エコー・コストを支払わないかぎり、これを生け贄に捧げる。");
            return true;
        }
        return false;
    }
}
