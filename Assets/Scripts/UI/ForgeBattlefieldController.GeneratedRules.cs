public sealed partial class ForgeBattlefieldController
{
    // Native generated paragraphs have no independent printed translation row.
    // Exact templates only: unknown rules stay visible rather than losing clauses.
    private string GeneratedSearchRuleText(string rule) {
        var search=System.Text.RegularExpressions.Regex.Match(rule??"",@"^Player (\d+) searches their library for a (Plains|Island|Swamp|Mountain|Forest|basic land|land|creature|artifact|enchantment|instant|sorcery) card, reveals it, puts it into their hand, then shuffles\.$");
        if(search.Success){string actor=PlayerDisplayName(int.Parse(search.Groups[1].Value));string type=search.Groups[2].Value;
            string localized=type switch{"Plains"=>TapPaymentText("平原","Plains","平地"),"Island"=>TapPaymentText("海岛","Island","島"),"Swamp"=>TapPaymentText("沼泽","Swamp","沼"),"Mountain"=>TapPaymentText("山脉","Mountain","山"),"Forest"=>TapPaymentText("树林","Forest","森"),"basic land"=>TapPaymentText("基本地","basic land","基本土地"),_=>GamePreferences.T(char.ToUpperInvariant(type[0])+type.Substring(1))};
            return TapPaymentText(actor+"从自己的牌库中搜寻一张"+localized+"牌，展示该牌，将它置于手上，然后洗牌。",rule,actor+"は自分のライブラリーから"+localized+"カード１枚を探し、公開して手札に加え、その後ライブラリーを切り直す。");}
        return rule;
    }
    private static string GeneratedPublicRuleText(string rule) {
        var keyword=System.Text.RegularExpressions.Regex.Match(rule??"",@"^(Cascade|Storm|Suspend|Rebound|Replicate|Conspire|Demonstrate) - (.+)$");
        if(keyword.Success) {
            string name=keyword.Groups[1].Value;
            string translated=name;
            foreach(var entry in DeckKeywordGlossary.All)if(entry.English==name){translated=entry.Name;break;}
            return translated+" — "+GamePreferences.CardFaceName(keyword.Groups[2].Value);
        }
        return rule switch {
        "{T}, Sacrifice CARDNAME: Look at the top card of target player's library." =>
            TapPaymentText("{T}，牺牲此牌：检视目标牌手的牌库顶牌。",rule,"{T}、このカードを生け贄に捧げる：プレイヤー１人を対象とする。そのプレイヤーのライブラリーの一番上のカードを見る。"),
        "Evoke (You may cast this spell for its evoke cost. If you do, it's sacrificed when it enters.)" =>
            TapPaymentText("呼魂（你可以支付此咒语的呼魂费用来施放它。若如此，当它进战场时，牺牲它。）",rule,"想起（あなたはこの呪文を想起コストで唱えてもよい。そうしたなら、戦場に出たときそれを生け贄に捧げる。）"),
        "Draw a card at the beginning of the next turn's upkeep." =>
            TapPaymentText("在下个回合的维持开始时，抓一张牌。",rule,"次のターンのアップキープの開始時に、カード１枚を引く。"),
        "Draw a card at the beginning of the next upkeep." =>
            TapPaymentText("在下个维持开始时，抓一张牌。",rule,"次のアップキープの開始時に、カード１枚を引く。"),
        _ => rule
        };
    }
}
