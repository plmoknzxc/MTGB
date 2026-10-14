using System;
using System.Linq;
using System.Text.RegularExpressions;

public sealed partial class ForgeBattlefieldController
{
    private string VisibleCardType(ForgeCardView card)
    {
        if(card==null||card.faceDown)return GamePreferences.T("面朝下的牌");
        catalog.TryGetValue((card.copy||card.merged?card.currentArtPrintingKey:card.printingKey)??"",out var definition);
        var feature=definition?.features?.FirstOrDefault(f=>f.cardName==card.name);
        return CopyTypeText(card.currentType,definition,feature);
    }
    private string PrintedCostText(ForgeCardView source,string cost)
    {
        if(source==null||source.faceDown||string.IsNullOrWhiteSpace(cost))return GamePreferences.NativeText(cost);
        string Key(string value)=>Regex.Replace(value??"",@"\s+"," ").Trim().TrimEnd('.','。');
        foreach(string line in (source.originalRules??source.currentRules??"").Replace("\\n","\n").Split('\n')) {
            var keyword=Regex.Match(line.Trim(),@"^([A-Za-z][A-Za-z ]*)[—-](.+)$");
            if(!keyword.Success||Key(keyword.Groups[2].Value)!=Key(cost))continue;
            var entry=DeckKeywordGlossary.All.FirstOrDefault(e=>e.English==keyword.Groups[1].Value);
            if(entry==null)continue;
            string translated=PrintedRuleText(source,line);
            var body=Regex.Match(translated,"^"+Regex.Escape(entry.Name)+@"\s*[～〜—―:：-]\s*(.+)$");
            if(body.Success)return body.Groups[1].Value.TrimEnd('.','。');
        }
        return GamePreferences.NativeText(cost);
    }

    // Resolve the visible face, not just the physical printing's front face.
    // Only formatting and this source's self-name may differ from its rules.
    private readonly System.Collections.Generic.Dictionary<string,string> printedRuleCache=new();
    private int printedRuleRevision=-1;
    private string PrintedRuleText(ForgeCardView source,string paragraph)
    {
        if(source==null||source.faceDown||string.IsNullOrEmpty(paragraph))return paragraph;
        if(printedRuleRevision!=GamePreferences.Revision){printedRuleCache.Clear();printedRuleRevision=GamePreferences.Revision;}
        string key=source.printingKey+"/"+source.currentArtPrintingKey+"/"+source.name+"/"+source.originalName+"/"+source.originalRules+"/"+paragraph;
        if(printedRuleCache.TryGetValue(key,out var text))return text;
        text=PrintedRuleTextCore(source,paragraph);
        if(printedRuleCache.Count>=1024)printedRuleCache.Clear();printedRuleCache[key]=text;
        return text;
    }
    private string PrintedRuleTextCore(ForgeCardView source,string paragraph)
    {
        if(source==null||source.faceDown||string.IsNullOrEmpty(paragraph))return paragraph;
        catalog.TryGetValue((source.copy||source.merged?source.currentArtPrintingKey:source.printingKey)??"",out var definition);
        CardFeatureDefinition feature=definition?.features?.FirstOrDefault(f=>f!=null&&f.cardName==source.name);
        if(feature==null&&definition!=null&&source.name!=definition.cardName) {
            feature=definition.features?.FirstOrDefault(f=>f!=null&&f.cardName==source.originalName);
            if(feature==null&&source.originalName!=definition.cardName)definition=null;
        }
        if(IsSpecializedFace(source)&&!string.IsNullOrEmpty(source.originalRules))
            feature=new CardFeatureDefinition{cardName=source.originalName??source.name,description=source.originalRules};
        if(feature==null&&definition==null) {
            // Public native faces can precede the client printing catalog.
            // Bind their own baseline; never translate changed text wholesale.
            if(string.IsNullOrEmpty(source.originalRules)||source.originalName!=source.name)return paragraph;
            feature=new CardFeatureDefinition{cardName=source.name,description=source.originalRules};
        }
        string canonical=feature?.cardName??definition.cardName;
        string[] Lines(string value)=>(value??"").Replace("\\n","\n").Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).Where(s=>s.Length>0).ToArray();
        string Key(string value) {
            value=(value??"").Replace("CARDNAME",canonical).Replace("\\n","\n");
            value=Regex.Replace(value.Trim(),@"^-\s+","");
            if(!string.IsNullOrEmpty(source.name)&&source.name!=canonical)
                value=Regex.Replace(value,@"(?<![\p{L}\p{N}])"+Regex.Escape(source.name)+@"(?![\p{L}\p{N}])",_=>canonical);
            return Regex.Replace(value,@"\s+"," ").Trim();
        }
        var native=Lines(feature?.description??definition?.description);
        string translated=feature!=null?GamePreferences.FeatureRules(feature):GamePreferences.CardRules(definition);
        var localized=Lines(translated);
        string match=Key(paragraph);
        // Chapters can grant a quoted activation. Its live ability is the
        // quoted text, rather than the entire printed chapter sentence.
        string QuoteKey(string value)=>Key(value).Replace('"','\'').TrimEnd('.');
        for(int i=0;i<native.Length&&i<localized.Length;i++) {
            var granted=Regex.Match(native[i],@"\bgains? \""(.+)\""\.?$");
            if(!granted.Success||QuoteKey(granted.Groups[1].Value)!=QuoteKey(paragraph))continue;
            var localQuote=Regex.Match(localized[i],"[“「『\"](.+)[”」』\"]。?$");
            if(localQuote.Success)return localQuote.Groups[1].Value;
        }
        var alternative=Regex.Match(paragraph.Trim(),@"^(.*?)\s+\((If .+ rather than pay this spell's mana cost\.)\)\s*$",RegexOptions.Singleline);
        if(alternative.Success){
            string body=PrintedRuleText(source,alternative.Groups[1].Value),cost=PrintedRuleText(source,alternative.Groups[2].Value);
            if(body!=alternative.Groups[1].Value&&cost!=alternative.Groups[2].Value)return body+"（"+cost+"）";
        }
        // Whole rules do not require identical editorial line breaks.
        if(native.Length>0&&Key(string.Join("\n",native))==match)return translated;
        if(native.Length!=localized.Length){
            // Published translations may wrap a multi-sentence paragraph into
            // several lines. Align intact sentences before giving up on a rule.
            var english=Regex.Matches(string.Join(" ",native),@"[^.!?\n]+[.!?]?").Cast<Match>().Select(m=>m.Value.Trim()).Where(s=>s.Length>0).ToArray();
            var local=Regex.Matches(translated,@"[^。！？\n]+[。！？]?").Cast<Match>().Select(m=>m.Value.Trim()).Where(s=>s.Length>0).ToArray();
            if(english.Length==local.Length){
                for(int first=0;first<english.Length;first++)for(int count=1;count<=english.Length-first;count++)
                    if(Key(string.Join(" ",english.Skip(first).Take(count)))==match)return string.Join("\n",local.Skip(first).Take(count));
            }
            return paragraph.Contains('\n')?string.Join("\n",Lines(paragraph).Select(line=>PrintedRuleText(source,line))):GamePreferences.NativeText(paragraph);
        }
        var matches=Enumerable.Range(0,native.Length).Where(i=>Key(native[i])==match).Select(i=>localized[i]).Distinct().ToArray();
        if(matches.Length==1)return matches[0];
        // A native spell can omit its keyword paragraph or join several rules.
        var sequences=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        for(int first=0;first<native.Length;first++)for(int count=2;count<=native.Length-first;count++)
            if(Key(string.Join("\n",native.Skip(first).Take(count)))==match)
                sequences.Add(string.Join("\n",localized.Skip(first).Take(count)));
        if(sequences.Count==1)return sequences.Single();
        const string clashReminder=" (Each clashing player reveals the top card of their library, then puts that card on the top or bottom. A player wins if their card had a higher mana value.)";
        int at=Array.FindIndex(native,s=>Key(s)==Key(paragraph+clashReminder));
        if(at>=0)return Regex.Replace(localized[at],@"\s+[（(][^()（）]*[)）]$","");
        at=Array.FindIndex(native,s=>s.Contains(":")&&Key(s.Substring(s.IndexOf(':')+1))==match);
        if(at>=0)return Regex.Replace(localized[at],@"^.*?[:：]\s*","");
        // Keyword-generated activations use the reminder's effect, rather than
        // the whole keyword line. Keep its costs out of the stack effect body.
        for(int i=0;i<native.Length;i++){
            var reminder=Regex.Match(native[i],@"^[^()]+\((.+)\)$");
            var localReminder=Regex.Match(localized[i],@"[（(](.+)[）)]$");
            if(!reminder.Success||!localReminder.Success)continue;
            string body=reminder.Groups[1].Value;
            string costless=Regex.Replace(body,@"^.*?:\s*","");
            if(Key(body)==match)return localReminder.Groups[1].Value;
            if(Key("("+body+")")==match)return "（"+localReminder.Groups[1].Value+"）";
            if(costless!=body&&Key(costless)==match)return Regex.Replace(localReminder.Groups[1].Value,@"^.*?[:：]\s*","");
        }
        var dredge=Regex.Match(paragraph,"^"+Regex.Escape(source.name??"")+@" - Dredge (\d+)$");
        if(dredge.Success) {
            at=Array.FindIndex(native,s=>s.StartsWith("Dredge "+dredge.Groups[1].Value+" (",StringComparison.Ordinal));
            if(at>=0)return localized[at];
        }
        string chapter=(source.currentType??"").Split(' ').Contains("Saga")?ChapterRuleText(paragraph,native,localized):paragraph;
        if(chapter!=paragraph)return chapter;
        return paragraph.Contains('\n')?string.Join("\n",Lines(paragraph).Select(line=>PrintedRuleText(source,line))):GamePreferences.NativeText(paragraph);
    }

    private string AbilityRecipeText(ForgeCardView source,string effect,string fallback)
    {
        if(source==null||source.faceDown)return fallback;
        var recipes=(view?.stackItems??Array.Empty<ForgeStackItem>()).Concat(view?.request?.orderedAbilities??Array.Empty<ForgeStackItem>())
            .Where(s=>s?.source?.id==source.id&&s.description==effect).Select(s=>s.rulesText)
            .Concat((view?.request?.options??Array.Empty<ForgeOption>()).Where(o=>o?.card?.id==source.id&&o.label==effect).Select(o=>o.rulesText))
            .Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct().ToArray();
        if(recipes.Length!=1)return fallback;
        string recipe=recipes[0];string localized=PrintedRuleText(source,recipe);
        if(localized==recipe){recipe=Regex.Replace(recipe,@"^(?:\{[^{}]+\}\s*[,，]?\s*)+[:：]\s*","");localized=PrintedRuleText(source,recipe);}
        return localized!=recipe?localized:fallback;
    }
}
