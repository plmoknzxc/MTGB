public sealed partial class ForgeBattlefieldController
{
    // Native generated paragraphs have no independent printed translation row.
    // Exact templates only: unknown rules stay visible rather than losing clauses.
    private string GeneratedSearchRuleText(string rule,bool playerReferences=false) {
        var dividedSearch=System.Text.RegularExpressions.Regex.Match(rule??"",@"^Player (?<owner>\d+) searches their library for up to four cards with different names and reveals them\. Player (?<chooser>\d+) chooses two of those cards\. Player \k<owner> puts the chosen cards into their graveyard and the rest into their hand\. Then Player \k<owner> shuffles\.\s*$");
        if(dividedSearch.Success) {
            string Name(string group)=>playerReferences?PlayerTextReference(int.Parse(dividedSearch.Groups[group].Value)):PlayerDisplayName(int.Parse(dividedSearch.Groups[group].Value));
            string owner=Name("owner"),chooser=Name("chooser");
            return TapPaymentText(owner+"从自己的牌库中搜寻至多四张不同名的牌并展示。\n"+chooser+"选择其中两张。\n"+owner+"将所选牌置入自己的坟墓场，其余牌置于手上，然后洗牌。",rule,
                owner+"は自分のライブラリーから名前の異なるカードを最大４枚探し、公開する。\n"+chooser+"はそのうち２枚を選ぶ。\n"+owner+"は選ばれたカードを自分の墓地に、残りを手札に加える。その後ライブラリーを切り直す。");
        }
        const string typePattern="Plains|Island|Swamp|Mountain|Forest|basic land|land|creature|artifact|enchantment|instant|sorcery";
        var search=System.Text.RegularExpressions.Regex.Match(rule??"",@"^Player \d+ searches their library for an? (?<type>"+typePattern+@")(?: or (?<other>"+typePattern+@"))? card, (?<reveal>reveals it, )?puts it (?<destination>into their hand|onto the battlefield(?<tapped> tapped)?), then shuffles\.$");
        if(search.Success){
            string Type(string type)=>type switch{"Plains"=>TapPaymentText("平原","Plains","平地"),"Island"=>TapPaymentText("海岛","Island","島"),"Swamp"=>TapPaymentText("沼泽","Swamp","沼"),"Mountain"=>TapPaymentText("山脉","Mountain","山"),"Forest"=>TapPaymentText("树林","Forest","森"),"basic land"=>TapPaymentText("基本地","basic land","基本土地"),_=>GamePreferences.T(char.ToUpperInvariant(type[0])+type.Substring(1))};
            string type=search.Groups["type"].Value,other=search.Groups["other"].Value;
            string local=Type(type)+(other.Length>0?TapPaymentText("或"," or ","か")+Type(other):"");
            bool hand=search.Groups["destination"].Value=="into their hand",reveal=search.Groups["reveal"].Success,tapped=search.Groups["tapped"].Success;
            return TapPaymentText("从你的牌库中搜寻一张"+local+"牌，"+(reveal?"展示该牌，":"")+(hand?"将它置于手上":tapped?"将它横置放进战场":"将它放进战场")+"，然后洗牌。",
                "Search your library for a "+type+(other.Length>0?" or "+other:"")+" card, "+(reveal?"reveal it, ":"")+(hand?"put it into your hand":"put it onto the battlefield"+(tapped?" tapped":""))+", then shuffle.",
                "あなたのライブラリーから"+local+"カード１枚を探し、"+(reveal?"公開して":"")+(hand?"手札に加える":(tapped?"タップ状態で":"")+"戦場に出す")+"。その後ライブラリーを切り直す。");
        }
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
