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
        if(string.IsNullOrWhiteSpace(card.currentType))return feature!=null?GamePreferences.FeatureType(feature,card.originalType):definition!=null?GamePreferences.CardType(definition):CardTypeText.Line(card.originalType);
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
    // Compare a native omission only against its own source paragraph. Never
    // remove arbitrary parentheses or costs from the text being displayed.
    private static string WithoutPrintedReminder(string text)
    {
        text=text.TrimEnd();
        while(text.EndsWith(")",StringComparison.Ordinal)||text.EndsWith("）",StringComparison.Ordinal)) {
            var brackets=new System.Collections.Generic.Stack<char>();int start=-1;
            for(int i=text.Length-1;i>=0;i--) {
                char c=text[i];
                if(c==')'||c=='）')brackets.Push(c);
                else if(c=='('||c=='（') {
                    if(brackets.Count==0||brackets.Pop()!=(c=='('?')':'）'))return text;
                    if(brackets.Count==0){start=i;break;}
                }
            }
            if(start<0)break;
            text=text.Substring(0,start).TrimEnd();
        }
        return text;
    }
    private static string WithoutPrintedCost(string text)=>Regex.Replace(text,@"^.*?[:：]\s*","");
    private string PrintedRuleText(ForgeCardView source,string paragraph)
    {
        if(source==null||source.faceDown||string.IsNullOrEmpty(paragraph))return paragraph;
        if(printedRuleRevision!=GamePreferences.Revision){printedRuleCache.Clear();printedRuleRevision=GamePreferences.Revision;}
        string key=source.printingKey+"/"+source.currentArtPrintingKey+"/"+source.name+"/"+source.originalName+"/"+source.originalRules+"/"+paragraph;
        if(printedRuleCache.TryGetValue(key,out var text))return text;
        text=PrintedRuleTextCore(source,paragraph);
        // Generated tokens need not have an independent printing row. Apply
        // only complete known public templates, including keyword reminders.
        text=string.Join("\n",(text??paragraph).Replace("\\n","\n").Split('\n').Select(GeneratedPublicRuleText));
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
            // The pinned Forge Oracle line misspells this card's own name.
            // Its live trigger and localized faces use Boggart Trawler.
            if(canonical=="Boggart Trawler")value=Regex.Replace(value,@"\bGoblin Trawler\b",canonical);
            value=Regex.Replace(value.Trim(),@"^(?:-\s+|[•●]\s*)","");
            if(!string.IsNullOrEmpty(source.name)&&source.name!=canonical)
                value=Regex.Replace(value,@"(?<![\p{L}\p{N}])"+Regex.Escape(source.name)+@"(?![\p{L}\p{N}])",_=>canonical);
            // Forge's generated trigger text uses "enters" while older Oracle
            // rows use "enters the battlefield" (or "enters battlefield").
            // Normalize only this equivalent phrase; keep conditions intact.
            value=Regex.Replace(value,@"\benters (?:the )?battlefield\b","enters");
            // Older native search prompts use "a Island" where the printed
            // paragraph uses "an Island". This is an article spelling change,
            // not a different card type or search restriction.
            value=Regex.Replace(value,@"\bfor an (?=[A-Za-z])","for a ");
            value=Regex.Replace(value,@"\bPermanent\b","permanent");
            return Regex.Replace(value,@"\s+"," ").Trim();
        }
        var native=Lines(feature?.description??definition?.description);
        string translated=feature!=null?GamePreferences.FeatureRules(feature):GamePreferences.CardRules(definition);
        if(canonical=="Boggart Trawler")translated=Regex.Replace(translated??"",@"\bGoblin Trawler\b",canonical);
        // Some translation rows contain editorial wraps inside a Japanese
        // sentence. A following grammatical particle continues that sentence;
        // it is not a new ability paragraph. Keep keyword-only lines separate.
        string aligned=GamePreferences.Language=="ja-JP"?Regex.Replace((translated??"").Replace("\\n","\n"),@"(?<=[^。！？）)\n])\n(?=[をにがはへともでやの])",""):translated;
        var localized=Lines(aligned);
        string match=Key(paragraph);
        // Chapters can grant a quoted activation. Its live ability is the
        // quoted text, rather than the entire printed chapter sentence.
        string QuoteKey(string value)=>Key(value).Replace('"','\'').TrimEnd('.');
        for(int i=0;i<native.Length&&i<localized.Length;i++) {
            var granted=Regex.Match(native[i],@"\b(?:gains?|has|have) \""(.+)\""\.?$");
            if(!granted.Success)continue;
            string CostlessQuote(string text)=>Regex.Replace(text,@"^(?:(?:\{[^{}]+\}\s*[,，]?\s*)+|[+−-]?\d+)\s*[:：]\s*","");
            string quoted=granted.Groups[1].Value;
            bool costless=CostlessQuote(quoted)!=quoted&&QuoteKey(CostlessQuote(quoted))==QuoteKey(paragraph);
            if(QuoteKey(quoted)!=QuoteKey(paragraph)&&!costless)continue;
            var localQuote=Regex.Match(localized[i],"[“「『\"](.+)[”」』\"](?:[。.]|を(?:持つ|得る)[。.]?)?$");
            if(localQuote.Success)return costless?CostlessQuote(localQuote.Groups[1].Value):localQuote.Groups[1].Value;
        }
        var alternative=Regex.Match(paragraph.Trim(),@"^(.*?)\s+\((If .+ rather than pay this spell's mana cost\.)\)\s*$",RegexOptions.Singleline);
        if(alternative.Success){
            string body=PrintedRuleText(source,alternative.Groups[1].Value),cost=PrintedRuleText(source,alternative.Groups[2].Value);
            if(body!=alternative.Groups[1].Value&&cost!=alternative.Groups[2].Value)return body+"（"+cost+"）";
        }
        // Whole rules do not require identical editorial line breaks.
        if(native.Length>0&&Key(string.Join("\n",native))==match)return translated;
        var variants=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        void MatchVariants(string original,string local) {
            if(Key(original)==match)variants.Add(local);
            // Spree options omit the additional-cost heading. Strip it only
            // from paired printed rows, never from a live payment description.
            const string modePrefix=@"^\+\s*(?:\{[^{}]+\}\s*)+[—～〜-]\s*";
            string mode=Regex.Replace(original,modePrefix,"");
            string localMode=Regex.Replace(local,modePrefix,"");
            if(mode!=original&&localMode!=local&&Key(mode)==match)variants.Add(localMode);
            // Pawprint callbacks omit the printed budget prefix as well.
            // Match the complete effect against its paired source row.
            const string pawPrefix=@"^(?:\{(?:P|oH+)\}\s*)+[—―～〜-]\s*";
            string paw=Regex.Replace(original,pawPrefix,"");
            string localPaw=Regex.Replace(local,pawPrefix,"");
            if(paw!=original&&localPaw!=local&&Key(paw)==match)variants.Add(localPaw);
            string noReminder=WithoutPrintedReminder(original);
            if(noReminder!=original&&Key(noReminder)==match)variants.Add(WithoutPrintedReminder(local));
            string body=WithoutPrintedCost(original);
            if(body!=original) {
                if(Key(body)==match)variants.Add(WithoutPrintedCost(local));
                if(Key(WithoutPrintedReminder(body))==match)variants.Add(WithoutPrintedReminder(WithoutPrintedCost(local)));
            }
        }
        if(native.Length==localized.Length) {
            for(int i=0;i<native.Length;i++)MatchVariants(native[i],localized[i]);
            if(variants.Count==1)return variants.Single();
        }
        if(native.Length!=localized.Length){
            // Reminders can themselves contain sentences, and translations
            // can wrap inside an activation. Align complete rule sentences,
            // retaining paragraph boundaries for unpunctuated keyword lines.
            foreach(bool omitReminder in new[]{true,false}) {
                var english=native.Select(s=>omitReminder?WithoutPrintedReminder(s):s).SelectMany(line=>Regex.Matches(line,@"[^.!?\n]+[.!?]?").Cast<Match>().Select(m=>m.Value.Trim())).Where(s=>s.Length>0).ToArray();
                var local=localized.Select(s=>omitReminder?WithoutPrintedReminder(s):s).SelectMany(line=>Regex.Matches(line,@"[^。！？\n]+[。！？]?").Cast<Match>().Select(m=>m.Value.Trim())).Where(s=>s.Length>0).ToArray();
                if(english.Length!=local.Length||english.Length>128)continue;
                variants.Clear();
                for(int first=0;first<english.Length;first++)for(int count=1;count<=english.Length-first;count++)
                    MatchVariants(string.Join("\n",english.Skip(first).Take(count)),string.Join("\n",local.Skip(first).Take(count)));
                if(variants.Count==1)return variants.Single();
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
            // Keyword reminders can contain a real delayed/phase trigger.
            // Match that sentence against this source's own reminder rather
            // than expecting it to be a separate printed paragraph.
            string[] Sentences(string value,bool english)=>Regex.Matches(value,english?@"[^.!?]+[.!?]?":@"[^。！？]+[。！？]?")
                .Cast<Match>().Select(m=>m.Value.Trim()).Where(s=>s.Length>0).ToArray();
            var reminderNative=Sentences(body,true);
            var reminderLocal=Sentences(localReminder.Groups[1].Value,GamePreferences.Language=="en-US");
            if(reminderNative.Length==reminderLocal.Length) {
                var sentence=Enumerable.Range(0,reminderNative.Length).Where(j=>Key(reminderNative[j])==match).Select(j=>reminderLocal[j]).Distinct().ToArray();
                if(sentence.Length==1)return sentence[0];
            }
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
        var recipes=AbilityRecipes(source,effect);
        if(recipes.Length!=1)return fallback;
        string recipe=recipes[0].text;var origin=recipes[0].origin;
        string localized=PrintedRuleText(origin,recipe);
        if(localized==recipe){recipe=Regex.Replace(recipe,@"^(?:\{[^{}]+\}\s*[,，]?\s*)+[:：]\s*","");localized=PrintedRuleText(origin,recipe);}
        return localized!=recipe?localized:fallback;
    }
    private (string text,ForgeCardView origin)[] AbilityRecipes(ForgeCardView source,string effect)
    {
        // JsonUtility materializes absent nested DTOs as empty objects. Legacy
        // snapshots must still bind to their real physical source, not that DTO.
        ForgeCardView Origin(ForgeCardView candidate)=>candidate!=null&&(candidate.faceDown||!string.IsNullOrEmpty(candidate.name)&&!string.IsNullOrEmpty(candidate.printingKey))?candidate:source;
        var recipes=(view?.stackItems??Array.Empty<ForgeStackItem>()).Concat(view?.request?.orderedAbilities??Array.Empty<ForgeStackItem>())
            .Concat((view?.request?.options??Array.Empty<ForgeOption>()).Where(o=>o?.ability!=null).Select(o=>o.ability))
            .Where(s=>s?.source?.id==source.id&&s.description==effect).Select(s=>(text:s.rulesText,origin:Origin(s.rulesSource)))
            .Concat((view?.request?.options??Array.Empty<ForgeOption>()).Where(o=>o?.card?.id==source.id&&o.label==effect).Select(o=>(text:o.rulesText,origin:Origin(o.rulesSource))))
            .Concat(view?.request?.sourceCard?.id==source.id&&view.request.abilityText==effect
                ?new[]{(text:view.request.rulesText,origin:Origin(view.request.rulesSource))}:Array.Empty<(string text,ForgeCardView origin)>())
            .Where(s=>!string.IsNullOrWhiteSpace(s.text)&&s.origin!=null&&!s.origin.faceDown)
            .GroupBy(s=>(s.text,s.origin.printingKey,s.origin.name,s.origin.originalRules)).Select(g=>g.First()).ToArray();
        return recipes;
    }
}
