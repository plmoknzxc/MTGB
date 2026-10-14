using System;
using System.Text.RegularExpressions;

public sealed partial class ForgeBattlefieldController
{
    private CardDefinition EmblemDefinition(ForgeCardView card)
    {
        if(card==null||!card.emblem||card.faceDown||string.IsNullOrEmpty(card.tokenSourcePrintingKey))return null;
        if(!catalog.TryGetValue(card.tokenSourcePrintingKey,out var source))return null;
        return card.name=="Emblem — "+source.cardName?source:null;
    }
    private string EmblemLocalizedName(ForgeCardView card)
    {
        var source=EmblemDefinition(card);
        return source==null?MarkerName(card):TapPaymentText("徽记 — ","Emblem — ","紋章 — ")+GamePreferences.CardName(source);
    }
    private string EmblemLocalizedRules(ForgeCardView card)
    {
        string fallback=MarkerRules(card);var source=EmblemDefinition(card);
        if(source==null)return fallback;
        var native=(source.description??"").Replace("\\n","\n").Split('\n',StringSplitOptions.RemoveEmptyEntries);
        var localized=GamePreferences.CardRules(source).Split('\n',StringSplitOptions.RemoveEmptyEntries);
        if(native.Length!=localized.Length)return fallback;
        for(int i=0;i<native.Length;i++) {
            // Match the full native emblem recipe, not an arbitrary quoted sentence.
            var original=Regex.Match(native[i],@"You get an emblem with ""([^""]+)""");
            if(!original.Success||original.Groups[1].Value!=card.currentRules)continue;
            var translated=Regex.Match(localized[i],@"[""「“]([^""」”]+)[""」”]");
            if(translated.Success)return translated.Groups[1].Value;
        }
        return fallback;
    }
}
