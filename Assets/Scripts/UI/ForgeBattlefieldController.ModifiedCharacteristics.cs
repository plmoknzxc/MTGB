using UnityEngine;

public sealed partial class ForgeBattlefieldController
{
    private static readonly Color GrantedKeywordColor=new Color(.44f,.75f,1f);
    public static string ModifiedStatsText(ForgeCardView card,bool darkBackground)
    {
        string Value(int value,int baseline,bool baseChanged,int effectiveBase) {
            if(!card.hasStatBaseline || value==baseline&&!baseChanged)return value.ToString();
            bool rewriteOnly=baseChanged&&value==effectiveBase;
            int boostBaseline=baseChanged?effectiveBase:baseline;
            string color=rewriteOnly?(darkBackground?"70BFFF":"216D9E"):value>boostBaseline?(darkBackground?"63E69B":"16894C"):(darkBackground?"FF7184":"D12D46");
            return "<color=#"+color+">"+value+"</color>";
        }
        return Value(card.power,card.basePower,card.powerBaseChanged,card.effectiveBasePower)+"/"+Value(card.toughness,card.baseToughness,card.toughnessBaseChanged,card.effectiveBaseToughness);
    }
    private static string KeywordDetailLabel(string detail,string name)
    {
        // Chapter stores SVar recipe names after its chapter count. They are
        // engine identifiers; only the chapter count is player-facing state.
        var chapter=System.Text.RegularExpressions.Regex.Match(detail??"",@"^Chapter:(\d+):.+$");
        if(chapter.Success)return TapPaymentText("章节数量：","Chapters: ","章の数：")+chapter.Groups[1].Value;
        string localizedName=GamePreferences.CardFaceName(name)??name??"";
        if(detail=="CARDNAME can't attack.")return localizedName+TapPaymentText("不能进行攻击。"," can't attack.","は攻撃できない。");
        if(detail=="CARDNAME can't block.")return localizedName+TapPaymentText("不能进行阻挡。"," can't block.","はブロックできない。");
        string raw=(detail??"").Replace("CARDNAME",name??"");
        int colon=raw.IndexOf(':');string key=(colon<0?raw:raw.Substring(0,colon)).Replace(" ","_").ToUpperInvariant();
        string label=StatusName(key);
        return label==key?raw:label+(colon<0?"":" "+raw.Substring(colon+1).Replace(':',' '));
    }
    private static System.Collections.Generic.IEnumerable<string> KeywordDetailChanges(ForgeCardView card,bool includeUnchanged)
    {
        var before=card.originalKeywordDetails??System.Array.Empty<string>();
        var after=card.currentKeywordDetails??System.Array.Empty<string>();
        foreach(var value in after) {
            if(card.hasIntensity && value.StartsWith("Starting intensity:",System.StringComparison.OrdinalIgnoreCase))continue;
            string label=KeywordDetailLabel(value,card.name);
            if(!System.Linq.Enumerable.Contains(before,value))yield return ColoredDetail(label,ChangedBlue);
            else if(includeUnchanged || label!=value)yield return SafeDetail(label);
        }
        foreach(var value in before)if(!System.Linq.Enumerable.Contains(after,value))yield return LostDetail(KeywordDetailLabel(value,card.name));
    }
    private static string IntensityDetail(ForgeCardView card,bool original)
    {
        if(card.faceDown || !card.hasIntensity)return "";
        int value=original?card.originalIntensity:card.intensity;
        string number=!original&&value!=card.originalIntensity?ColoredDetail(value.ToString(),ChangedBlue):value.ToString();
        return "<nobr>"+TapPaymentText("强度：","Intensity: ","強度：")+number+"</nobr>";
    }
    private static string AdditionalNamesDetail(ForgeCardView card)
    {
        if(card.faceDown || !card.hasNonLegendaryCreatureNames)return "";
        return ColoredDetail(TapPaymentText("额外拥有所有非传奇生物牌的名称。","Also has the names of all nonlegendary creature cards.","伝説でないすべてのクリーチャー・カードの名前も持つ。"),ChangedBlue);
    }
    private static string ColorDetail(int mask)
    {
        var labels=new System.Collections.Generic.List<string>();
        string[] zh={"白色","蓝色","黑色","红色","绿色"},en={"White","Blue","Black","Red","Green"},ja={"白","青","黒","赤","緑"};
        for(int i=0;i<5;i++)if((mask&(1<<i))!=0)labels.Add(TapPaymentText(zh[i],en[i],ja[i]));
        return labels.Count==0?TapPaymentText("无色","Colorless","無色"):string.Join(" / ",labels);
    }
}
