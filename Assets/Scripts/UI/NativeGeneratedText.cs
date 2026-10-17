using System;
using System.Linq;
using System.Text.RegularExpressions;

// Exact native grammars for generated text without an Oracle translation row.
internal static class NativeGeneratedText
{
    static string L(string zh,string ja)=>GamePreferences.Language=="ja-JP"?ja:zh;
    internal static bool TryTranslate(string source,out string text)
    {
        text=source;
        var payment=Regex.Match(source,@"^Select (.+) to (sacrifice|discard|tap|exile) \((\d+) left\)$");
        if(payment.Success) {
            string types=TypeList(payment.Groups[1].Value);
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
