using System;
using System.Linq;
using System.Text.RegularExpressions;

// Exact native grammars for generated text without an Oracle translation row.
internal static class NativeGeneratedText
{
    static string L(string zh,string ja)=>GamePreferences.Language=="ja-JP"?ja:zh;
    internal static string ColorName(string color)
    {
        string term=GamePreferences.T(System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(color));
        return GamePreferences.Language=="zh-CN"&&!term.EndsWith("色",StringComparison.Ordinal)?term+"色":term;
    }
    internal static bool TryTranslate(string source,out string text)
    {
        text=source switch {
            "How many counters?" => L("选择指示物数量。","カウンターの個数を選ぶ。"),
            "Transformed cards in" => L("已变化的牌","変化したカード"),
            "Behold a Dragon" => L("请援龙","ドラゴンの後見を受ける"),
            "Commander Effect" => L("统帅区替代效应","統率領域への置換効果"),
            "If a commander would be put into its owner's hand or library from anywhere, its owner may put it into the command zone instead." =>
                L("如果统帅将从任何地方置入其拥有者的手牌或牌库，其拥有者可以改为将它置于统帅区。","統率者がいずれかの領域からオーナーの手札やライブラリーに置かれるなら、そのオーナーは代わりにそれを統率領域に置いてもよい。"),
            "Time travel. (For each suspended card you own and each permanent you control with a time counter on it, you may add or remove a time counter.)" =>
                L("时间旅行。（对每张由你拥有且已延缓的牌，以及每个由你操控且其上有计时指示物的永久物，你可以在其上增加或移去一个计时指示物。）","タイムトラベルを行う。（あなたがオーナーであり待機状態の各カードと、あなたがコントロールしていて時間カウンターが置かれている各パーマネントについて、それぞれ時間カウンター１個を増やすか取り除いてもよい。）"),
            "It enters with an additional +1/+1 counter on it if it's a creature, it enters with an additional loyalty counter on it if it's a planeswalker." =>
                L("若它是生物，则额外带着一个 +1/+1 指示物进战场。若它是鹏洛客，则额外带着一个忠诚指示物进战场。","それがクリーチャーなら追加の＋1/＋1カウンター１個が、プレインズウォーカーなら追加の忠誠カウンター１個が置かれた状態で戦場に出る。"),
            "Visit — Draw a card." => L("造访～抓一张牌。","観覧 ― カード１枚を引く。"),
            "Visit — Create a Food token. (It's an artifact with \"{2}, {T}, Sacrifice this token: You gain 3 life.\")" =>
                L("造访～派出一个食品衍生物。（它是具有「{2}，{T}，牺牲此衍生物：你获得3点生命」的神器。）","観覧 ― 食物トークン１つを生成する。（それは「{2}, {T}, このトークンを生け贄に捧げる：３点のライフを得る。」を持つアーティファクトである。）"),
            _ => source
        };
        if(text!=source)return true;
        if(source.Contains("perpetually specializes into ")) {
            var special=Regex.Match(source,@"^(?:(?<prefix>.+?) - )?(?<who>.+?) perpetually specializes into (?<color>white|blue|black|red|green)\.\z");
            if(special.Success&&(!special.Groups["prefix"].Success||special.Groups["prefix"].Value==special.Groups["who"].Value)
                &&GamePreferences.IsCardIdentity(special.Groups["who"].Value)) {
                string name=GamePreferences.CardFaceName(special.Groups["who"].Value);
                string color=ColorName(special.Groups["color"].Value);
                text=L($"{name}永久精进为{color}。",$"{name}は永久に{color}に専門化する。");
                return true;
            }
        }
        if(source.StartsWith("At the beginning of your next main phase, you may add an amount of {C}",StringComparison.Ordinal)) {
            var mana=Regex.Match(source,@"^At the beginning of your next main phase, you may add an amount of \{C\} equal to that spell's mana value\. \(\[(\d+)\]\)\z");
            if(mana.Success) {
                string n=mana.Groups[1].Value;
                text=L($"在你的下一个行动阶段开始时，你可以加等同于该咒语法术力值的无色法术力（本次为 {n} 点{{C}}）。",$"あなたの次のメイン・フェイズの開始時に、その呪文のマナ総量に等しい点数の{{C}}を加えてもよい（今回は{n}点）。");
                return true;
            }
        }
        if(source.Contains("enters with ")&&source.EndsWith(" loyalty counters on it.",StringComparison.Ordinal)) {
            var loyalty=Regex.Match(source,@"^(.+) enters with (one|two|three|four|five|six|seven|eight|nine|ten|\d+) loyalty counters on it\.\z");
            if(loyalty.Success&&GamePreferences.IsCardIdentity(loyalty.Groups[1].Value)) {
                string name=GamePreferences.CardFaceName(loyalty.Groups[1].Value),n=loyalty.Groups[2].Value;
                string[] numbers={"zero","one","two","three","four","five","six","seven","eight","nine","ten"};
                int number=Array.IndexOf(numbers,n);
                if(number>0)n=number.ToString();
                text=L($"{name}带着 {n} 个忠诚指示物进战场。",$"{name}は忠誠カウンター{n}個が置かれた状態で戦場に出る。");
                return true;
            }
        }
        // A granted activation can reach a payment or inspection window with
        // only its new host as the source, without the granting card's Oracle text.
        const string exileCost="Exile a card from your hand";
        const string castCopy="Create a copy of this card. You may cast the copy. Activate only during your turn and only if this card is exiled.";
        if(source==castCopy||source==exileCost+": "+castCopy) {
            string effect=L("派出一个此牌的复制品。你可以施放该复制品。只能于你的回合中且此牌在放逐区时起动。","このカードのコピーを生成する。そのコピーを唱えてもよい。あなたのターンの間で、このカードが追放領域にあるときにのみ起動できる。");
            text=source==castCopy?effect:L("从你手上放逐一张牌：","あなたの手札にあるカード１枚を追放する：")+effect;
            return true;
        }
        if(source==exileCost){text=L("从你手上放逐一张牌","あなたの手札にあるカード１枚を追放する");return true;}
        var payment=Regex.Match(source,@"^Select (?:a\(n\) )?(.+) to (sacrifice|discard|tap|exile) \((\d+) left\)$");
        if(payment.Success) {
            string types=TypeList(payment.Groups[1].Value);
            if(types==null) {
                var nativeType=payment.Groups[1].Value;
                var localized=CardTypeText.Term(nativeType);
                if(localized!=nativeType)types=localized;
            }
            string action=payment.Groups[2].Value switch {"sacrifice"=>L("牺牲","生け贄に捧げる"),"discard"=>L("弃掉","捨てる"),"tap"=>L("横置","タップする"),_=>L("放逐","追放する")};
            if(types!=null){text=L("选择要"+action+"的"+types+"（还剩"+payment.Groups[3].Value+"）",action+types+"を選択（残り"+payment.Groups[3].Value+"）");return true;}
        }
        var mobilize=Regex.Match(source,@"^Mobilize (\d+) \(When this creature attacks, create (a|one|two|three|four|five|six|seven|eight|nine|ten|\d+) tapped and attacking 1/1 red Warrior creature tokens?\. Sacrifice them at the beginning of the next end step\.\)$");
        if(mobilize.Success) {
            string n=mobilize.Groups[1].Value,word=mobilize.Groups[2].Value;
            string[] numbers={"zero","one","two","three","four","five","six","seven","eight","nine","ten"};
            int amount=word=="a"?1:Array.IndexOf(numbers,word);if(amount<0&&!int.TryParse(word,out amount))return false;
            if(!int.TryParse(n,out int count)||count!=amount)return false;
            string name=DeckKeywordGlossary.All.First(e=>e.English=="Mobilize").Name;
            text=L(name+" "+n+"（当此生物攻击时，派出"+n+"个已横置且正进行攻击的1/1红色战士衍生生物。在下一个结束步骤开始时，牺牲它们。）",
                name+" "+n+"（このクリーチャーが攻撃するたび、赤の1/1の戦士・クリーチャー・トークン"+n+"体をタップ状態かつ攻撃している状態で生成する。次の終了ステップの開始時に、それらを生け贄に捧げる。）");return true;
        }
        var unless=Regex.Match(source,@"^You lose (\d+) life unless you (sacrifice a nonland permanent|discard a card)\.$");
        if(unless.Success) {
            bool sacrifice=unless.Groups[2].Value.StartsWith("sacrifice",StringComparison.Ordinal);string n=unless.Groups[1].Value;
            text=L("除非你"+(sacrifice?"牺牲一个非地永久物":"弃一张牌")+"，否则失去"+n+"点生命。",
                (sacrifice?"土地でないパーマネント１つを生け贄に捧げる":"カード１枚を捨てる")+"のでないかぎり、あなたは"+n+"点のライフを失う。");return true;
        }
        if(source=="Sacrifice an artifact, enchantment or token") {
            text=L("牺牲一个神器、结界或衍生物","アーティファクトやエンチャントやトークン１つを生け贄に捧げる");return true;
        }
        var token=Regex.Match(source,@"^(?<cost>(?:\{[0-9WUBRGCSTQPXYZ/]+\},?\s*)*)(?<discard>Discard a card, )?Sacrifice this token: (?<effect>.+)$");
        if(token.Success) {
            string effect=token.Groups["effect"].Value;
            string body=effect switch {
                "Add one mana of any color."=>L("加一点任意颜色的法术力。","好きな色１色のマナ１点を加える。"),
                "Draw a card."=>L("抓一张牌。","カード１枚を引く。"),
                "You gain 3 life."=>L("你获得3点生命。","あなたは３点のライフを得る。"),
                "Search your library for a basic land card, put it onto the battlefield tapped, then shuffle."=>L("从你的牌库中搜寻一张基本地牌，将它横置放进战场，然后洗牌。","あなたのライブラリーから基本土地カード１枚を探し、タップ状態で戦場に出す。その後ライブラリーを切り直す。"),
                _=>GamePreferences.NativeText(effect)
            };
            if(body==effect)return false;
            text=token.Groups["cost"].Value+(token.Groups["discard"].Success?L("弃一张牌，","カード１枚を捨てる、"):"")+L("牺牲此衍生物：","このトークンを生け贄に捧げる：")+body;return true;
        }
        if(source=="This creature can't block.") {text=L("此生物不能进行阻挡。","このクリーチャーではブロックできない。");return true;}
        return false;
    }
    private static string TypeList(string source)
    {
        var pieces=Regex.Split(source,@"( and/or | or |, )");
        var parts=pieces.Select(p=>p switch {
            " and/or "=>L("和／或","や")," or "=>L("或","や"),", "=>"、",
            "nonland permanent"=>L("非地永久物","土地でないパーマネント"),
            "artifact"=>L("神器","アーティファクト"),"enchantment"=>L("结界","エンチャント"),
            "creature"=>L("生物","クリーチャー"),"land"=>L("地","土地"),
            "permanent"=>L("永久物","パーマネント"),"token"=>L("衍生物","トークン"),
            "card"=>L("牌","カード"),_=>null
        }).ToArray();
        return parts.Any(p=>p==null)?null:string.Concat(parts);
    }
}
