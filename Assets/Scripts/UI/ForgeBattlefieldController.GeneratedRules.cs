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
        int reminder=rule?.IndexOf(" (",System.StringComparison.Ordinal)??-1;
        string keywordName=reminder>=0?rule.Substring(0,reminder):rule;
        if(!string.IsNullOrEmpty(keywordName)&&keywordName.Length<=40)foreach(var entry in DeckKeywordGlossary.All) {
            if(keywordName!=entry.English)continue;
            if(reminder<0)return entry.Name;
            if(rule==entry.English+" ("+entry.EnglishHelp+")")return entry.Name+"（"+entry.Help+"）";
            break;
        }
        var explore=System.Text.RegularExpressions.Regex.Match(rule??"",@"^(?<name>.+?)(?: \(\d+\))? explores\.\s*$");
        if(explore.Success) {
            string name=GamePreferences.CardFaceName(explore.Groups["name"].Value);
            return TapPaymentText(name+"勘察。",rule,name+"は探検を行う。");
        }
        var unless=System.Text.RegularExpressions.Regex.Match(rule??"",@"^You lose (?<life>\d+) life unless you (?<cost>sacrifice a nonland permanent|discard a card)\.$");
        if(unless.Success) {
            string life=unless.Groups["life"].Value;bool sacrifice=unless.Groups["cost"].Value.StartsWith("sacrifice",System.StringComparison.Ordinal);
            return TapPaymentText("你失去 "+life+" 点生命，除非你"+(sacrifice?"牺牲一个非地永久物。":"弃一张牌。"),rule,
                (sacrifice?"土地でないパーマネント１つを生け贄に捧げないかぎり、":"カード１枚を捨てないかぎり、")+"あなたは"+life+"点のライフを失う。");
        }
        var munitions=System.Text.RegularExpressions.Regex.Match(rule??"",@"^When this token leaves the battlefield, it deals (?<damage>\d+) damage to any target\.$");
        if(munitions.Success)return TapPaymentText("当此衍生物离开战场时，它对任意一个目标造成 "+munitions.Groups["damage"].Value+" 点伤害。",rule,
            "このトークンが戦場を離れたとき、１つを対象とする。これはそれに"+munitions.Groups["damage"].Value+"点のダメージを与える。");
        var keyword=System.Text.RegularExpressions.Regex.Match(rule??"",@"^(Cascade|Storm|Suspend|Rebound|Replicate|Conspire|Demonstrate) - (.+)$");
        if(keyword.Success) {
            string name=keyword.Groups[1].Value;
            string translated=name;
            foreach(var entry in DeckKeywordGlossary.All)if(entry.English==name){translated=entry.Name;break;}
            return translated+" — "+GamePreferences.CardFaceName(keyword.Groups[2].Value);
        }
        return rule switch {
        "This token can block only creatures with flying." =>
            TapPaymentText("此衍生物只能阻挡具有飞行异能的生物。",rule,"このトークンは飛行を持つクリーチャーしかブロックできない。"),
        "{1}, {T}, Sacrifice this token: Target creature you control explores. Activate only as a sorcery. (Reveal the top card of your library. Put that card into your hand if it's a land. Otherwise, put a +1/+1 counter on that creature, then put the card back or put it into your graveyard.)" =>
            TapPaymentText("{1}，{T}，牺牲此衍生物：目标由你操控的生物勘察。只能于法术时机起动。（展示你的牌库顶牌。若是地，将它置于手上。否则，在该生物上放置一个+1/+1指示物，然后将该牌放回牌库顶或置入你的坟墓场。）",rule,
                "{1}、{T}、このトークンを生け贄に捧げる：あなたがコントロールするクリーチャー１体を対象とする。それは探検を行う。起動はソーサリーとしてのみ行う。（あなたのライブラリーの一番上のカードを公開する。それが土地なら手札に加える。そうでないなら、そのクリーチャーに+1/+1カウンター１個を置き、そのカードを戻すかあなたの墓地に置く。）"),
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
