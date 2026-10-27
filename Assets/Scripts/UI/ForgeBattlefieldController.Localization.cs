using System;
using System.Linq;
using System.Text.RegularExpressions;

public sealed partial class ForgeBattlefieldController
{
    private const string CommanderPerpetualResetPrompt="Remove all perpetual modifications from your commander?";
    private bool IsCommanderPerpetualReset => view?.request?.kind=="InputConfirm"
        && view.request.sourceCard?.commander==true && !view.request.sourceCard.faceDown
        && (view.request.message==CommanderPerpetualResetPrompt
            ||(view.request.message??"").Replace("\r","").EndsWith("\n\n"+CommanderPerpetualResetPrompt,StringComparison.Ordinal));
    private const string CommanderHandLibraryRule="If a commander would be put into its owner's hand or library from anywhere, its owner may put it into the command zone instead.";
    private string CommanderReturnText(ForgeCardView commander,string rule)
    {
        if(commander?.commander!=true||commander.faceDown)return null;
        string text=Regex.Replace(rule?.Trim()??"",@"^Commander Effect \(\d+\) - ","");
        if(text!=CommanderHandLibraryRule)return null;
        return TapPaymentText("你可以将这张主将放回统帅区，代替将其移入手牌或牌库。",CommanderHandLibraryRule,"この統率者が手札かライブラリーに置かれるなら、代わりに統率領域に置いてもよい。");
    }
    // Translate the public source's rules through the existing printed-rule
    // consumer, then translate the choice instruction. Do not translate wire
    // fields in place: those fields also select native interaction contracts.
    private string NativeDisplayText(string message)
    {
        if(CompletedSingleCardOption(view?.request)>=0)return "";
        if(string.IsNullOrEmpty(message))return message;
        if(view?.request?.kind=="InputSelectTargets")
            message=string.Join("\n",message.Replace("\r","").Split('\n').Select(TargetInstructionText));
        if(IsCommanderPerpetualReset)return TapPaymentText(
            "主将回统帅区时，如何处理永久改动？",
            "How should this commander's perpetual changes be handled?",
            "統率者の永久的な変更をどうしますか？");
        var commander=view?.request?.sourceCard;
        if(view?.request?.kind=="InputConfirm"&&view.request.selectionPurpose=="optionalReplacement"
            &&message.Replace("\r","")=="Apply replacement effect of Commander Effect?\n"+CommanderHandLibraryRule
            &&CommanderReturnText(commander,CommanderHandLibraryRule) is string replacementRule)
            return TapPaymentText("是否将主将放回统帅区？","Return your commander to the command zone?","統率者を統率領域に戻しますか？")+"\n\n"+replacementRule;
        const string commanderReturn="If a commander is in a graveyard or in exile and that card was put into that zone since the last time state-based actions were checked, its owner may put it into the command zone.";
        bool commanderReturnPrompt=commander!=null&&(message==commander.name+": "+commanderReturn
            ||message.StartsWith(commander.name+" ("+commander.id+") - ",StringComparison.Ordinal)
                &&message.Replace("\r","").EndsWith("\n\n"+commander.name+": "+commanderReturn,StringComparison.Ordinal));
        if(view?.request?.kind=="InputConfirm"&&commander?.commander==true&&!commander.faceDown&&commanderReturnPrompt){
            string name=GamePreferences.CardFaceName(commander.name);
            return TapPaymentText($"是否将{name}放回统帅区？\n选择“否”会将它留在当前区域。",message,$"{name}を統率領域に戻しますか？\n「いいえ」を選ぶと現在の領域に残ります。");
        }
        message=NormalizePrintedSelfNames(view?.request?.sourceCard,message);
        message=string.Join("\n",message.Replace("\r","").Split('\n').Select(TargetInstructionText));
        var replacementSource=view?.request?.sourceCard;
        if(view?.request?.kind=="InputConfirm"&&replacementSource!=null&&!replacementSource.faceDown
            &&!string.IsNullOrEmpty(replacementSource.name)) {
            var replacement=Regex.Match(message,"^Apply replacement effect of "+Regex.Escape(replacementSource.name)+@"\?\n+(.+)$",RegexOptions.Singleline);
            if(replacement.Success)return TapPaymentText("是否应用“"+GamePreferences.CardFaceName(replacementSource.name)+"”的替代效果？",
                "Apply replacement effect of "+replacementSource.name+"?",GamePreferences.CardFaceName(replacementSource.name)+"の置換効果を適用しますか？")
                +"\n\n"+AbilityEffectText(replacementSource,replacement.Groups[1].Value);
        }
        string auxiliary=AuxiliaryPrompt(message);if(auxiliary!=null)return auxiliary;
        if(view?.request?.kind=="InputSelectEntitiesFromList") {
            message=Regex.Replace(message,@"(?m)^(?:Choose a creature(?: with a different power| with -?[0-9]+ power)?|Secretly choose a creature you (?:control|don't control)|Choose (?:a|an) (?:Cleric|Rogue|Warrior|Wizard|Plains|Island|Swamp|Mountain|Forest))\r?$",match=> {
                string prompt=match.Value.TrimEnd('\r');
                if(prompt=="Choose a creature")return TapPaymentText("选择一个生物。","Choose a creature.","クリーチャー１体を選択。");
                if(prompt=="Choose a creature with a different power")return TapPaymentText("选择一个力量与已选生物不同的生物。",prompt,"選択済みと異なるパワーを持つクリーチャー１体を選択。");
                if(Regex.IsMatch(prompt,@"^Choose a creature with -?[0-9]+ power$")) {
                    string number=Regex.Match(prompt,@"-?[0-9]+").Value;
                    return TapPaymentText($"选择一个力量为 {number} 的生物。",prompt,$"パワーが {number} のクリーチャー１体を選択。");
                }
                if(prompt.StartsWith("Secretly choose",StringComparison.Ordinal)) {
                    bool own=!prompt.Contains("don't control");
                    return own?TapPaymentText("秘密选择一个由你操控的生物。",prompt,"あなたがコントロールするクリーチャー１体を秘密裏に選択。")
                        :TapPaymentText("秘密选择一个不由你操控的生物。",prompt,"あなたがコントロールしていないクリーチャー１体を秘密裏に選択。");
                }
                string type=prompt.Substring(prompt.LastIndexOf(' ')+1);
                string localized=type switch {
                    "Cleric"=>TapPaymentText("僧侣",type,"クレリック"),"Rogue"=>TapPaymentText("浪客",type,"ならず者"),
                    "Warrior"=>TapPaymentText("战士",type,"戦士"),"Wizard"=>TapPaymentText("法术师",type,"ウィザード"),
                    "Plains"=>TapPaymentText("平原",type,"平地"),"Island"=>TapPaymentText("海岛",type,"島"),
                    "Swamp"=>TapPaymentText("沼泽",type,"沼"),"Mountain"=>TapPaymentText("山脉",type,"山"),
                    "Forest"=>TapPaymentText("树林",type,"森"),_=>type
                };
                return TapPaymentText("选择一个"+localized+"。",prompt,localized+"１つを選択。");
            });
            var power=Regex.Match(message.Replace("\r",""),@"(?:^|\n)Select creature\(s\) with total power less than or equal to (-?[0-9]+)\n\(Selected:(\[[^\n]*\])\)\n\(Total Power: (-?[0-9]+)\)$");
            if(power.Success) {
                string selected=power.Groups[2].Value;
                // Translate only identities already visible to this chooser.
                foreach(var card in (view.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>()).Where(c=>c!=null&&!c.faceDown&&!string.IsNullOrEmpty(c.name)))
                    selected=selected.Replace(card.name+" ("+card.id+")",GamePreferences.CardFaceName(card.name)+" ("+card.id+")");
                if(selected=="[]")selected=TapPaymentText("无","None","なし");
                string limit=power.Groups[1].Value,total=power.Groups[3].Value;
                string prefix=message.Replace("\r","").Substring(0,power.Index);
                string instruction=TapPaymentText($"选择力量总和不超过 {limit} 的生物。\n已选：{selected}\n力量总和：{total}",
                    $"Select creatures with total power at most {limit}.\nSelected: {selected}\nTotal power: {total}",
                    $"パワーの合計が {limit} 以下のクリーチャーを選択。\n選択済み：{selected}\nパワーの合計：{total}");
                return string.IsNullOrWhiteSpace(prefix)?instruction:NativeDisplayText(prefix)+"\n"+instruction;
            }
        }
        if(view?.request?.kind=="RevealCards" && Regex.IsMatch(message,@"(?:^|\n)(?:Player [0-9]+ wins clash\.|No Winner!)(?:$|\n)")) {
            var resultLines=message.Replace("\r","").Split('\n');
            for(int i=0;i<resultLines.Length;i++) {
                var reveal=Regex.Match(resultLines[i],@"^Player ([0-9]+) reveals: (.+)\. Mana Value= ([0-9]+)$");
                if(reveal.Success&&int.TryParse(reveal.Groups[1].Value,out int playerSeat)
                    &&(view.request.previewCards??Array.Empty<ForgeCardView>()).Any(c=>c!=null&&!c.faceDown&&c.ownerSeat==playerSeat&&c.name==reveal.Groups[2].Value)) {
                    string player=PlayerDisplayName(playerSeat),card=GamePreferences.CardFaceName(reveal.Groups[2].Value),value=reveal.Groups[3].Value;
                    resultLines[i]=TapPaymentText($"{player}展示：{card}，法术力值 {value}。",$"{player} revealed {card} (mana value {value}).",$"{player}が公開：{card}。マナ総量：{value}。");
                    continue;
                }
                var winner=Regex.Match(resultLines[i],@"^Player ([0-9]+) wins clash\.$");
                if(winner.Success&&int.TryParse(winner.Groups[1].Value,out int winnerSeat)
                    &&(view.players??Array.Empty<ForgePlayerView>()).Any(p=>p.seat==winnerSeat)) {
                    string player=PlayerDisplayName(winnerSeat);
                    resultLines[i]=TapPaymentText($"{player}赢得比点。",$"{player} won the clash.",$"{player}が激突に勝利。");
                } else if(resultLines[i]=="No Winner!")resultLines[i]=TapPaymentText("没有牌手赢得比点。","No winner!","激突の勝者なし。");
                else resultLines[i]=GamePreferences.NativeText(resultLines[i]);
            }
            return string.Join("\n",resultLines);
        }
        if(view?.request?.kind=="InputConfirm"&&view.request.options.Any(o=>o.id=="ok"&&o.label=="Yes")&&view.request.options.Any(o=>o.id=="cancel"&&o.label=="No")) {
            var optionalCounter=Regex.Match(message,@"^Would you like to add '(.+)' counters to or remove '\1' counters from (.+)\?$");
            if(optionalCounter.Success) {
                string type=CounterChoiceName(optionalCounter.Groups[1].Value);
                string target=GamePreferences.CardFaceName(optionalCounter.Groups[2].Value);
                return TapPaymentText($"是否在{target}上增加或移除{type}指示物？",$"Add or remove {type} counters on {target}?",$"{target}の{type}カウンターを置くか取り除きますか？");
            }
        }
        if(view?.request?.kind=="InputConfirm"&&view.request.options.Any(o=>o.id=="ok"&&o.label=="Add Counter")&&view.request.options.Any(o=>o.id=="cancel"&&o.label=="Remove Counter")) {
            var counter=Regex.Match(message,@"^Choose what to do with '(.+)' counter on (.+?)\s*$");
            if(counter.Success) {
                string type=CounterChoiceName(counter.Groups[1].Value);
                string target=GamePreferences.CardFaceName(counter.Groups[2].Value);
                return TapPaymentText($"要在{target}上增加还是移除一个{type}指示物？",$"Add or remove a {type} counter on {target}?",$"{target}の{type}カウンターを１個置きますか、取り除きますか？");
            }
        }
        if(view?.request?.kind=="InputConfirm"&&view.request.options.Any(o=>o.id=="ok"&&o.label=="Tap")&&view.request.options.Any(o=>o.id=="cancel"&&o.label=="Untap")) {
            string instruction=ResolutionChoiceInstruction(message,false);
            if(instruction!=message)return instruction;
        }
        if(!Regex.IsMatch(message,@"(?:^|\n).+ \([0-9]+\)(?: - .*|\r?)$",RegexOptions.Multiline)) {
            var publicSource=view?.request?.sourceCard;
            string text=publicSource!=null&&!publicSource.faceDown&&!string.IsNullOrEmpty(publicSource.name)
                ?AbilityEffectText(publicSource,message):message;
            return GamePreferences.NativeText(text);
        }
        var visible=(view?.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>
            (p.battlefield??Array.Empty<ForgeCardView>()).Concat(p.hand??Array.Empty<ForgeCardView>())
            .Concat(p.graveyardCards??Array.Empty<ForgeCardView>()).Concat(p.exileCards??Array.Empty<ForgeCardView>()))
            .Concat((view?.stackItems??Array.Empty<ForgeStackItem>()).Select(s=>s.source))
            .Concat(new[]{view?.request?.sourceCard})
            .Concat(view?.request?.previewCards??Array.Empty<ForgeCardView>())
            .Concat((view?.request?.options??Array.Empty<ForgeOption>()).Select(o=>o.card))
            .Where(card=>card!=null&&!card.faceDown&&!string.IsNullOrEmpty(card.name)).ToArray();
        ForgeCardView source=null;
        var lines=message.Replace("\r","").Split('\n');
        for(int i=0;i<lines.Length;i++) {
            var header=Regex.Match(lines[i],@"^(.+) \(([0-9]+)\)(?: - (.*))?$");
            if(header.Success&&int.TryParse(header.Groups[2].Value,out int id)) {
                source=visible.FirstOrDefault(card=>card.id==id
                    &&card.name==NormalizePrintedSelfNames(card,header.Groups[1].Value));
                if(source!=null) {
                    lines[i]=GamePreferences.CardFaceName(source.name)+" ("+id+")";
                    if(header.Groups[3].Success)lines[i]+=" — "+AbilityEffectText(source,
                        view.request.kind=="InputSelectTargets"&&!string.IsNullOrEmpty(view.request.abilityText)?view.request.abilityText:header.Groups[3].Value);
                    continue;
                }
            }
            if(source!=null&&lines[i].StartsWith(" - ",StringComparison.Ordinal))
                lines[i]=AbilityEffectText(source,lines[i].Substring(3).Trim());
            if(view?.request?.kind=="InputSelectTargets") {
                if(lines[i]=="Select up to one target creature card exiled with this creature")
                    lines[i]=TapPaymentText("从以此生物放逐的牌中选择至多一张目标生物牌。",lines[i],"このクリーチャーによって追放されているクリーチャー・カード最大１枚を対象に選択。");
                else if(lines[i]=="Select up to one target instant or sorcery card exiled with this creature")
                    lines[i]=TapPaymentText("从以此生物放逐的牌中选择至多一张目标瞬间或法术牌。",lines[i],"このクリーチャーによって追放されているインスタントやソーサリーであるカード最大１枚を対象に選択。");
                else if(lines[i].StartsWith("Parent Targeted:[",StringComparison.Ordinal)&&lines[i].EndsWith("]",StringComparison.Ordinal)) {
                    string identities=lines[i].Substring(17,lines[i].Length-18);
                    var objects=Regex.Matches(identities,@"(.+?) \(([0-9]+)\)(?:,\s*|$)");
                    if(objects.Count>0&&string.Concat(objects.Cast<Match>().Select(m=>m.Value))==identities
                        &&objects.Cast<Match>().All(m=>visible.Any(c=>c.id.ToString()==m.Groups[2].Value&&c.name==m.Groups[1].Value)))
                        lines[i]=TapPaymentText("前一段已选目标：","Previous targets: ","前の段階の対象：")
                            +string.Join(", ",objects.Cast<Match>().Select(m=>GamePreferences.CardFaceName(m.Groups[1].Value)+" ("+m.Groups[2].Value+")"));
                }
            }
        }
        return GamePreferences.NativeText(string.Join("\n",lines));
    }
    // TargetRestrictions also accepts capitalized subtype lists and an
    // explicit graveyard qualifier. Unknown restrictions remain intact.
    private string TargetInstructionText(string text)
    {
        string original=text;
        if(text=="Select a commander")return TapPaymentText("选择一位主将。",text,"統率者１枚を選択。");
        var ninjutsuReturn=Regex.Match(text??"",@"^Return (\d+) unblocked attacker card\(s\) to Hand$");
        if(ninjutsuReturn.Success)return TapPaymentText("选择"+ninjutsuReturn.Groups[1].Value+"个由你操控且未受阻挡的攻击生物，移回其拥有者手上。",text,"あなたがコントロールするブロックされていない攻撃クリーチャーを"+ninjutsuReturn.Groups[1].Value+"体選び、オーナーの手札に戻す。");
        var permanentValue=Regex.Match(text??"",@"^Select target permanent card with mana value (\d+) or less in your graveyard$");
        if(permanentValue.Success)return TapPaymentText("从你的坟墓场选择一张法术力值不大于"+permanentValue.Groups[1].Value+"的目标永久物牌。",text,"あなたの墓地にあるマナ総量が"+permanentValue.Groups[1].Value+"以下のパーマネント・カード１枚を対象に選ぶ。");
        if(text=="Select target Permanent.nonLand")return TapPaymentText("选择目标非地永久物。",text,"土地でないパーマネント１つを対象に選ぶ。");
        var graveyard=Regex.Match(text??"",@"^Select a card from your graveyard( \(\d+ / \d+\))?$");
        if(graveyard.Success)return TapPaymentText("从你的坟墓场选择一张牌", "Select a card from your graveyard","あなたの墓地からカード１枚を選択")+graveyard.Groups[1].Value;
        var tapPermanent=Regex.Match(text??"",@"^Select a\(n\) Permanent to tap \((\d+) left\)$");
        if(tapPermanent.Success)return TapPaymentText("选择要横置的永久物（还剩"+tapPermanent.Groups[1].Value+"）",text,"タップするパーマネントを選択（残り"+tapPermanent.Groups[1].Value+"）");
        bool optionalPrefix=text?.StartsWith("oneOrNone: ",StringComparison.Ordinal)==true;
        if(optionalPrefix)text=text.Substring(11);
        else if(text?.StartsWith("one: ",StringComparison.Ordinal)==true)text=text.Substring(5);
        var library=Regex.Match(text??"",@"^Select up to (\d+) cards from your library$");
        if(library.Success)return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            GamePreferences.T("Select up to {0} cards from your library"),library.Groups[1].Value);
        if(text=="Select any target")return TapPaymentText("选择任意一个目标。",text,"対象１つを選ぶ。");
        if(text=="Select target non-outlaw creature. (Assassins, Mercenaries, Pirates, Rogues, and Warlocks are outlaws. Everyone else is fair game.)")
            return TapPaymentText("选择目标非狂徒生物。（杀手、佣兵、海盗、浪客和邪术师是狂徒。除此之外概不设限。）",text,"無法者でないクリーチャー１体を対象に選ぶ。（暗殺者、傭兵、海賊、ならず者、邪術師は無法者である。それ以外はすべて対象にできる。）");
        var target=Regex.Match(text??"",@"^Select (?<optional>up to one )?(?<other>other )?target (?<types>[A-Za-z ,/]+?)(?<zone> (?:card )?(?:in|from) (?<owner>your|an opponent's|a) graveyard)?$");
        if(!target.Success)return original;
        var words=Regex.Split(target.Groups["types"].Value.ToLowerInvariant().Replace(", or "," or ").Replace(", and/or "," and/or "),@"( and/or | or |, )");
        var translated=new System.Collections.Generic.List<string>();
        foreach(string word in words) {
            string local=word switch {
                "artifact"=>TapPaymentText("神器",word,"アーティファクト"),"battle"=>TapPaymentText("战役",word,"バトル"),
                "card"=>TapPaymentText("牌",word,"カード"),"creature"=>TapPaymentText("生物",word,"クリーチャー"),
                "enchantment"=>TapPaymentText("结界",word,"エンチャント"),"instant"=>TapPaymentText("瞬间",word,"インスタント"),
                "equipment"=>TapPaymentText("武具",word,"装備品"),
                "kindred"=>TapPaymentText("亲缘",word,"同族"),"land"=>TapPaymentText("地",word,"土地"),
                "opponent"=>TapPaymentText("对手",word,"対戦相手"),"permanent"=>TapPaymentText("永久物",word,"パーマネント"),
                "nonland permanent"=>TapPaymentText("非地永久物",word,"土地でないパーマネント"),
                "planeswalker"=>TapPaymentText("鹏洛客",word,"プレインズウォーカー"),"player"=>TapPaymentText("牌手",word,"プレイヤー"),
                "sorcery"=>TapPaymentText("法术",word,"ソーサリー"),"spell"=>TapPaymentText("咒语",word,"呪文"),
                " or "=>TapPaymentText("或",word,"または")," and/or "=>TapPaymentText("和／或",word,"や"),", "=>TapPaymentText("、",word,"、"),_=>null
            };
            if(local==null)return original;
            translated.Add(local);
        }
        string types=string.Concat(translated);
        bool other=target.Groups["other"].Success;
        bool upTo=optionalPrefix||target.Groups["optional"].Success;
        string optional=upTo?TapPaymentText("至多一个", "up to one ","最大１つ"):"";
        if(target.Groups["zone"].Success) {
            string owner=target.Groups["owner"].Value;
            string zone=owner=="your"?TapPaymentText("你的坟墓场", "your graveyard","あなたの墓地")
                :owner=="an opponent's"?TapPaymentText("对手的坟墓场","an opponent's graveyard","対戦相手の墓地")
                :TapPaymentText("坟墓场","a graveyard","墓地");
            bool plainCard=target.Groups["types"].Value.Equals("card",StringComparison.OrdinalIgnoreCase);
            return TapPaymentText("从"+zone+"中选择"+(upTo?"至多一张":"一张")+(other?"其他":"")+"目标"+(plainCard?"":types)+"牌。",original,zone+"にある"+(other?"他の":"")+(plainCard?"":types+"である")+"カード"+(upTo?"最大１枚":"１枚")+"を対象に選ぶ。");
        }
        if(upTo)return TapPaymentText("选择至多"+(other?"另一个":"一个")+"目标"+types+"。",original,(other?"他の":"")+types+"最大１つを対象に選ぶ。");
        return TapPaymentText("选择"+(other?"另一个":"")+"目标"+types+"。",original,(other?"他の":"")+types+"を対象に選ぶ。");
    }
    // Forge generates payment prose from CostParts, rather than the printed
    // rule (including alternate costs). Translate that exact clause through
    // the existing cost vocabulary; never substitute an unrelated card rule.
    private string NativeCostEffectText(string paragraph)
    {
        const string prefix="As an additional cost to cast this spell, ";
        if(!paragraph.StartsWith(prefix,StringComparison.Ordinal)||!paragraph.EndsWith(".",StringComparison.Ordinal))return paragraph;
        string clause=paragraph.Substring(prefix.Length,paragraph.Length-prefix.Length-1);
        if(string.IsNullOrEmpty(clause))return paragraph;
        if(clause=="you may sacrifice any number of permanent.nonlands")
            return TapPaymentText("你可以牺牲任意数量的非地永久物，以作为施放此咒语的额外费用。",paragraph,"この呪文を唱えるための追加コストとして、土地でないパーマネントを望む数だけ生け贄に捧げてもよい。");
        clause=char.ToUpperInvariant(clause[0])+clause.Substring(1);
        string translated=GamePreferences.NativeText(CostOrderLabel(clause));
        if(translated==clause)return paragraph;
        return TapPaymentText("作为施放此咒语的额外费用，"+translated.TrimEnd('。','？')+"。",paragraph,
            "この呪文を唱えるための追加コストとして、"+translated.TrimEnd('。','？')+"。");
    }
    private static string CounterChoiceName(string native)
    {
        if(native=="P1P1"||native=="+1/+1")return "+1/+1";
        if(native=="M1M1"||native=="-1/-1")return "−1/−1";
        if(string.Equals(native,"Doom",StringComparison.OrdinalIgnoreCase))return TapPaymentText("末日","Doom","破滅");
        return PermanentCounterName(new ForgeCardCounter {kind=native.ToUpperInvariant(),label=native});
    }
}
