using System;
using System.Linq;
using System.Text.RegularExpressions;

public sealed partial class ForgeBattlefieldController
{
    private static string CopyTypeText(string type,CardDefinition definition,CardFeatureDefinition feature)
    {
        string Key(string text)=>string.Join(" ",Regex.Split((text??"").ToLowerInvariant(),@"[\s~～—-]+").Where(w=>w.Length>0).OrderBy(w=>w,StringComparer.Ordinal));
        string printed=feature!=null?string.Join(" ",feature.supertypes.ToDisplayString(),feature.cardTypes.ToDisplayString(),feature.subtypes):definition?.GetTypeLine();
        if(printed!=null&&Key(printed)==Key(type))return feature!=null?GamePreferences.FeatureType(feature,type):GamePreferences.CardType(definition);
        if(printed!=null&&Key(Regex.Replace(printed,@"\bLegendary\b",""))==Key(type))return Regex.Replace(feature!=null?GamePreferences.FeatureType(feature,type):GamePreferences.CardType(definition),@"\bLegendary\s*|传奇\s*|伝説の\s*","");
        return CardTypeText.Line(type);
    }
    private string CopyRulesText(ForgeCardView card,CardDefinition definition,CardFeatureDefinition feature)
    {
        string[] Lines(string text)=>(text??"").Replace("\\n","\n").Replace("\r","").Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).Where(s=>s.Length>0).ToArray();
        string Key(string text)=>Regex.Replace((text??"").Replace(card.name??"","CARDNAME").Replace(feature?.cardName??definition?.cardName??card.name??"","CARDNAME"),@"[\s().,:\[\]]","").ToLowerInvariant();
        var native=Lines(feature?.description??definition?.description);
        var localized=Lines(feature!=null?GamePreferences.FeatureRules(feature):GamePreferences.CardRules(definition));
        string Translate(string text){
            int at=Array.FindIndex(native,p=>Key(p)==Key(text));
            if(at>=0&&native.Length==localized.Length)return localized[at];
            var destroy=Regex.Match(text,@"^(.*?:\s*)?Destroy target creature with the same name as this creature\.$");
            if(destroy.Success)return destroy.Groups[1].Value+TapPaymentText("消灭目标与此生物同名的生物。","Destroy target creature with the same name as this creature.","このクリーチャーと同じ名前を持つクリーチャー１体を対象とする。それを破壊する。");
            if(text=="This creature gets +1/+1 for each Construct you control.")return TapPaymentText("你每操控一个组构体，此生物便得+1/+1。",text,"このクリーチャーは、あなたがコントロールしている構築物１体につき+1/+1の修整を受ける。");
            if(text=="At the beginning of your upkeep, you may have this creature become a copy of target creature, except it doesn't copy that creature's color and it has this ability.")return TapPaymentText("在你的维持开始时，你可以使此生物成为目标生物的复制品，但不复制该生物的颜色，且具有此异能。",text,"あなたのアップキープの開始時に、あなたはこのクリーチャーをクリーチャー１体を対象とし、そのコピーにしてもよい。ただし、そのクリーチャーの色はコピーせず、この能力を持つ。");
            if(text=="At the beginning of your next end step, sacrifice "+card.name+".")return TapPaymentText("在你的下一个结束步骤开始时，牺牲"+GamePreferences.CardFaceName(card.name)+"。",text,"あなたの次の終了ステップの開始時に、"+GamePreferences.CardFaceName(card.name)+"を生け贄に捧げる。");
            return text;
        }
        // Compare traits, not the stale copied oracle string. Keyword changes
        // remain in the shared keyword section and are not duplicated here.
        string before=string.Join("\n",Lines(card.copyOriginalAbilityText).Select(Translate));
        string after=string.Join("\n",Lines(card.copyCurrentAbilityText).Select(Translate));
        return LocalizedChangedRules(card,before,after);
    }
}
