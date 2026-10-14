using System;
using System.Linq;
using System.Text.RegularExpressions;

public sealed partial class ForgeBattlefieldController
{
    // These are complete paragraphs captured from native face-down views.
    // Unknown granted abilities stay visible verbatim, including extra conditions.
    private static string FaceDownRules(ForgeCardView card) => string.Join("\n",(card.currentRules??"").Replace("\r","").Split('\n').Select(line=>line switch {
        "Manifested"=>TapPaymentText("已显化",line,"予示された"),
        "Cloaked"=>TapPaymentText("已伪装",line,"偽装された"),
        "Ward {2} (Whenever this permanent becomes the target of a spell or ability an opponent controls, counter it unless that player pays {2}.)"=>TapPaymentText("守护{2}（每当此永久物成为由对手操控之咒语或异能的目标时，除非该牌手支付{2}，否则反击该咒语或异能。）",line,"護法{2}（このパーマネントが対戦相手がコントロールする呪文や能力の対象になるたび、そのプレイヤーが{2}を支払わないかぎり、それを打ち消す。）"),
        "Equipped creature gets +1/+0 and can't be blocked."=>TapPaymentText("佩带此武具的生物得+1/+0且不能被阻挡。",line,"装備しているクリーチャーは+1/+0の修整を受け、ブロックされない。"),
        "Your Ring-bearer can't be blocked by creatures with greater power."=>TapPaymentText("你的持戒者不能被力量大于它的生物阻挡。",line,"あなたの指輪所持者は、それより大きいパワーを持つクリーチャーにはブロックされない。"),
        _=>line
    }));
    private static string KnownFaceRules(ForgeCardView card) => GamePreferences.FeatureRules(new CardFeatureDefinition {
        cardName=card.knownFaceName,description=(card.knownFaceRules??"").Replace("\\n","\n")
    });
    private static string ChapterBody(string line) => Regex.Replace(line,@"^[IVX]+\s+[—―]\s+","");
    private static string ChapterRuleText(string paragraph,string[] native,string[] localized)
    {
        // Reflexive targets carry the chapter's body without its numeral.
        // Translate only an unambiguous, exact body in this source's own rules.
        if(native.Length!=localized.Length)return paragraph;
        var matches=Enumerable.Range(0,native.Length).Where(i=>ChapterBody(native[i])!=native[i]&&ChapterBody(native[i])==paragraph).ToArray();
        return matches.Length==1?ChapterBody(localized[matches[0]]):paragraph;
    }
}
