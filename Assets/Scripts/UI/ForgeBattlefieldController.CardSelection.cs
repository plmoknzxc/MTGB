using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform cardSelectionOverlay;
    private bool cardSelectionReadOnly;
    private int selectionPage;
    private float selectionPageFooter;
    private string selectionLayoutKey;
    private RectTransform selectionPanel, selectionFooter;
    private Text handSelectionPrompt;
    private Text selectionInstructionText;
    private RectTransform selectionInstructionContent;
    private string selectionKind;
    private static string TapPaymentText(string zh,string en,string ja) => GamePreferences.Language=="zh-CN"?zh:GamePreferences.Language=="ja-JP"?ja:en;
    private static bool IsTapPayment(ForgeRequest request) => request.selectionPurpose=="convoke" || request.selectionPurpose=="improvise" || request.selectionPurpose=="waterbend";
    private static bool IsExertAttack(ForgeRequest request)=>request.selectionPurpose=="exertAttack";
    private static string TapPaymentTitle(ForgeRequest request) => request.selectionPurpose=="convoke" ? TapPaymentText("召集", "Convoke", "召集")
        : request.selectionPurpose=="improvise" ? TapPaymentText("拼造", "Improvise", "即席") : TapPaymentText("截水", "Waterbend", "水の技");
    private bool IsRingBearerSelection=>view?.request?.kind=="InputSelectEntitiesFromList"&&System.Text.RegularExpressions.Regex.IsMatch(view.request.message??"",@"(?:^|\n)Choose your Ring-bearer$");
    private bool IsProliferateSelection=>view?.request?.kind=="InputSelectEntitiesFromList"&&(view.request.message??"").EndsWith("Choose any number of permanents and/or players for proliferate",StringComparison.Ordinal);
    private bool IsOptionalCardVote=>view?.awaitingInput==true&&view.request.votingPlayerSeat>0&&view.request.options.Any(HasChoiceCard)&&OptionIndex("cancel")>=0;
    private string SelectionProgress => IsOptionalCardVote?TapPaymentText($"已选 {view.request.selectedCount} · 至多 {view.request.maxCount} 张",$"Selected {view.request.selectedCount} · Up to {view.request.maxCount}",$"選択済み {view.request.selectedCount} · 最大 {view.request.maxCount} 枚") : IsProliferateSelection?TapPaymentText($"已选 {view.request.selectedCount} · 最多 {view.request.maxCount} 个对象",$"Selected {view.request.selectedCount} · Up to {view.request.maxCount} entities",$"選択済み {view.request.selectedCount} · 最大 {view.request.maxCount} 個") : IsRingBearerSelection?TapPaymentText($"已选 {view.request.selectedCount} · 需要 1 张",$"Selected {view.request.selectedCount} · Choose 1",$"選択済み {view.request.selectedCount} · １枚選択") : IsOptionalHandSelection
        ? TapPaymentText($"已选 {(IsStagedHandChoice(stagedHandChoice)?1:0)} · 需要 1 张",$"Selected {(IsStagedHandChoice(stagedHandChoice)?1:0)} · Choose 1",$"選択済み {(IsStagedHandChoice(stagedHandChoice)?1:0)} · １枚選択")
        : IsExertAttack(view.request) ? TapPaymentText($"已选 {view.request.selectedCount} / {view.request.maxCount}",$"Selected {view.request.selectedCount} / {view.request.maxCount}",$"選択 {view.request.selectedCount} / {view.request.maxCount}")
        : view.request.selectionPurpose=="removeCounters" ? $"{view.request.selectedCount} / {view.request.maxCount}"
        : view.request.selectionPurpose=="collectEvidence"
        ? TapPaymentText($"法术力值 {view.request.selectedValue} / {view.request.requiredValue}",$"Mana value {view.request.selectedValue} / {view.request.requiredValue}",$"マナ総量 {view.request.selectedValue} / {view.request.requiredValue}")
        : view.request.selectionPurpose=="putLibraryCost"||view.request.selectionPurpose=="chooseSource"||view.request.kind=="OrderCards" ? TapPaymentText($"已选 {view.request.selectedCount} / {view.request.minCount}", $"Selected {view.request.selectedCount} / {view.request.minCount}", $"選択済み {view.request.selectedCount} / {view.request.minCount}")
        : IsTapPayment(view.request) ? TapPaymentText($"已选 {view.request.selectedCount}", $"Selected {view.request.selectedCount}", $"選択済み {view.request.selectedCount}")
        : view.request.maxCount>0&&view.request.minCount==view.request.maxCount?TapPaymentText($"已选 {view.request.selectedCount} / {view.request.maxCount}",$"Selected {view.request.selectedCount} / {view.request.maxCount}",$"選択 {view.request.selectedCount} / {view.request.maxCount}")
        : view.request.maxCount>0?TapPaymentText($"已选 {view.request.selectedCount} · 需要 {view.request.minCount}–{view.request.maxCount} 张",$"Selected {view.request.selectedCount} · Choose {view.request.minCount}–{view.request.maxCount}",$"選択済み {view.request.selectedCount} · {view.request.minCount}–{view.request.maxCount}枚選択"):"";
    private bool IsHandDiscardSelection => view?.request?.kind=="InputSelectCardsFromList" &&
        ((view.request.message??"").IndexOf("to discard",StringComparison.OrdinalIgnoreCase)>=0 ||
         System.Text.RegularExpressions.Regex.IsMatch(view.request.message??"",@"(?:^|\n)Discard \d+ card\(s\)$"));
    private string SelectionInstruction => IsOptionalCardVote?TapPaymentText($"选择至多 {view.request.maxCount} 张牌投票，或弃权。",$"Choose up to {view.request.maxCount} card(s) to vote for, or abstain.",$"最大 {view.request.maxCount} 枚に投票するか、棄権。") : IsProliferateSelection?TapPaymentText("选择任意数量的永久物和玩家，也可以不选。每个选中的对象获得其已有的每种指示物各一个。","Choose any number of permanents and players, including none. Each chosen entity gets another counter of each kind already there.","パーマネントやプレイヤーを望む数選択（０でも可）。それぞれに既にある各種類のカウンターを１個ずつ置く。") : IsRingBearerSelection?TapPaymentText("选择你的持戒者（可以保持不变）。","Choose your Ring-bearer (you may keep the same creature).","指輪所持者を選択。同じクリーチャーでもよい。") : IsHandDiscardSelection ? DiscardSelectionInstruction : view.request.selectionPurpose=="dungeon" ? TapPaymentText("选择要进入的地城。","Choose a dungeon to enter.","探索するダンジョンを選択。") : view.request.selectionPurpose=="collectEvidence"
        ? TapPaymentText($"从坟墓场选择要放逐的牌，法术力值总和至少为 {view.request.requiredValue}。",$"Choose cards to exile from your graveyard with total mana value at least {view.request.requiredValue}.",$"マナ総量の合計が{view.request.requiredValue}以上になるように、墓地から追放するカードを選択。")
        : view.request.selectionPurpose=="removeCounters" ? TapPaymentText("点击卡牌选择一个指示物，选足后继续。","Click a card to choose one counter to remove; continue when all are chosen.","カードをクリックして取り除くカウンターを１個選択。必要な数を選ぶと続行。")
        : view.request.selectionPurpose=="putLibraryCost" ? TapPaymentText($"选择 {view.request.minCount} 张牌支付；取消则不支付。",$"Choose {view.request.minCount} cards to pay, or cancel.",$"{view.request.minCount} 枚を選んで支払うか、キャンセル。")
        : view.request.selectionPurpose=="chooseSource" ? TapPaymentText("选择一个来源，然后确认。","Choose a source, then confirm.","発生源を選んで確認。")
        : view.request.selectionPurpose=="beholdExile" ? TapPaymentText($"从手牌或战场选择 {view.request.minCount} 张牌来放逐并支付费用。",$"Choose {view.request.minCount} cards from your hand or battlefield to exile as payment.",$"支払いとして手札か戦場から {view.request.minCount} 枚を選び、追放。")
        : view.request.selectionPurpose=="behold" ? TapPaymentText($"从手牌展示或从战场选择 {view.request.minCount} 张符合条件的牌。",$"Reveal {view.request.minCount} matching cards from your hand or choose them on your battlefield.",$"条件を満たす {view.request.minCount} 枚を手札から公開するか、戦場から選択。")
        : IsExertAttack(view.request) ? TapPaymentText("选择要耗竭的攻击者；不选择则不耗竭。","Choose attackers to exert, or confirm without selecting to decline.","督励する攻撃クリーチャーを選択。選ばずに確認すれば督励しません。")
        : view.request.kind=="OrderCards" && view.request.message?.Replace("\r","")=="Choose order of cards to put into the library\nClosest to bottom"
            ? TapPaymentText("依次选择置底牌的顺序，然后确认；1 号牌最靠近牌库底。","Choose the order, then confirm; card 1 is closest to the bottom of your library.","一番下に置く順番を選んで確認。１番がライブラリーの一番下です。")
        : view.request.kind=="OrderCards" && view.request.message?.Replace("\r","")=="Choose order of cards to put into the library\nClosest to top"
            ? TapPaymentText("依次选择置顶牌的顺序，然后确认；1 号牌最靠近牌库顶。","Choose the order, then confirm; card 1 is closest to the top of your library.","一番上に置く順番を選んで確認。１番がライブラリーの一番上です。")
        : view.request.kind=="OrderCards" && view.request.message?.Replace("\r","")=="Choose order of cards to put into the graveyard\nClosest to bottom"
            ? TapPaymentText("依次选择原件顺序，然后确认；1 号牌最靠近坟墓场底。","Choose the order, then confirm; card 1 is closest to the bottom of the graveyard.","順番を選んで確認。１番が墓地の一番下です。")
        : view.request.kind=="OrderCards" ? TapPaymentText("依次选择顺序，然后确认。","Choose the order, then confirm.","順番を選んで確認。")
        : IsTapPayment(view.request) ? TapPaymentText($"选择要横置来支付费用的永久物；剩余费用 {view.request.remainingManaCost}。", $"Choose permanents to tap for payment; remaining cost {view.request.remainingManaCost}.", $"支払いのためにタップするパーマネントを選択。残りのコスト {view.request.remainingManaCost}。")
        : view.request.kind=="Surveil"?view.request.maxCount==1?TapPaymentText("选择置顶或入墓。","Choose top or graveyard.","上か墓地を選択。") : TapPaymentText("选择置顶或入墓；同一区域按选择顺序排列。","Choose top or graveyard; order cards in each destination by selection.","上か墓地を選択。同じ領域では選択した順に並びます。")
        : view.request.kind=="Scry"?view.request.maxCount==1?TapPaymentText("选择置顶或置底。","Choose top or bottom.","上か下を選択。") : TapPaymentText("选择置顶或置底；同一区域先选的牌在上方。","Choose top or bottom; the first card selected in each destination goes on top.","上か下を選択。同じ領域では先に選んだカードが上になります。"):ResolutionChoiceInstruction(SelectionMessage(view.request));
    private static string SelectionMessage(ForgeRequest request)
    {
        string message=request.message??"";
        if(request.kind!="InputSelectTargets"&&request.kind!="SelectStackTarget")return message;
        int split=message.LastIndexOf("\n\n",StringComparison.Ordinal);
        if(split<0)return message;
        string task=message.Substring(split+2).Trim();
        if(!task.StartsWith("Select ",StringComparison.Ordinal))return message;
        string clause="Choose "+task.Substring(7).TrimEnd('.');
        string rule=request.rulesText;
        if(string.IsNullOrEmpty(rule)&&request.sourceCard!=null) {
            string prefix=request.sourceCard.name+" ("+request.sourceCard.id+") - ";
            string context=message.Substring(0,split);
            if(context.StartsWith(prefix,StringComparison.Ordinal))
                rule=System.Text.RegularExpressions.Regex.Replace(context.Substring(prefix.Length),@"^(?:\{[^{}]+\}\s*,?\s*)+:\s*","");
        }
        // Drop only the repeated native selector, before localization. Keep the
        // complete effect and any different, narrower or unknown restriction.
        return rule!=null&&(rule==clause||rule.StartsWith(clause+".",StringComparison.Ordinal))
            ?message.Substring(0,split):message;
    }
    private string DiscardSelectionInstruction {
        get {
            int min=view.request.minCount,max=view.request.maxCount;
            if(min==0&&max>0)return TapPaymentText($"可弃至多 {max} 张牌；确认以继续。",$"You may discard up to {max} cards; confirm to continue.",$"最大 {max} 枚まで捨ててもよい。選択後に確認。");
            if(max>min)return TapPaymentText($"选择 {min}–{max} 张牌让其拥有者弃掉。",$"Choose {min}–{max} cards for their owner to discard.",$"オーナーが捨てるカードを {min}–{max} 枚選択。");
            return TapPaymentText($"选择 {min} 张牌让其拥有者弃掉。",$"Choose {min} card(s) for their owner to discard.",$"オーナーが捨てるカードを {min} 枚選択。");
        }
    }
    private string ResolutionChoiceInstruction(string message,bool localizeFallback=true)
    {
        // The pinned sacrifice producer omits the article used by the existing
        // localized format. Normalize only that equivalent selector phrase.
        message=System.Text.RegularExpressions.Regex.Replace(message??"",@"(?m)^Select another creature or artifact to sacrifice \((\d+) left\)$",
            m=>GamePreferences.NativeText("Select another creature or an artifact to sacrifice ("+m.Groups[1].Value+" left)"));
        string auxiliary=AuxiliaryPrompt(message);if(auxiliary!=null)return auxiliary;
        // Native prompts count the remaining cards (2, 1, then 0). Keep the
        // total requirement and progress separate so a full draft says 2/2.
        var handExile=System.Text.RegularExpressions.Regex.Match(message??"",@"(?:^|\n)Exile (\d+) card\(s\) from your hand$");
        if(handExile.Success&&view?.request?.kind=="InputSelectCardsFromList"&&view.request.maxCount>0) {
            string amount=view.request.minCount==view.request.maxCount?view.request.maxCount.ToString():view.request.minCount+"–"+view.request.maxCount;
            string instruction=TapPaymentText($"从手牌中选择 {amount} 张牌来放逐。",$"Choose {amount} cards from your hand to exile.",$"手札から追放するカードを {amount} 枚選択。");
            string prefix=(message??"").Substring(0,handExile.Index).TrimEnd();
            return string.IsNullOrWhiteSpace(prefix)?instruction:NativeDisplayText(prefix)+"\n"+instruction;
        }
        var balanceSacrifice=System.Text.RegularExpressions.Regex.Match(message??"",@"(?:^|\n)Select (\d+) (Land|Creature|Permanent)\(s\) to sacrifice\.$");
        if(balanceSacrifice.Success&&view?.request?.kind=="InputSelectCardsFromList"
            &&view.request.minCount==view.request.maxCount&&view.request.minCount.ToString()==balanceSacrifice.Groups[1].Value) {
            string amount=balanceSacrifice.Groups[1].Value,type=balanceSacrifice.Groups[2].Value;
            string zh=type=="Land"?"地":type=="Creature"?"生物":"永久物";
            string ja=type=="Land"?"土地":type=="Creature"?"クリーチャー":"パーマネント";
            string instruction=TapPaymentText($"选择 {amount} 个{zh}来牺牲。",$"Choose {amount} {type.ToLowerInvariant()}(s) to sacrifice.",$"生け贄に捧げる{ja}を {amount} 個選択。");
            string prefix=(message??"").Substring(0,balanceSacrifice.Index);
            return string.IsNullOrWhiteSpace(prefix)?instruction:NativeDisplayText(prefix)+"\n"+instruction;
        }
        if(view?.request?.kind=="InputSelectEntitiesFromList" && (message??"").EndsWith("Select a face-down creature you control",StringComparison.Ordinal))
            return TapPaymentText("选择一个由你操控的盖牌生物。","Select a face-down creature you control.","あなたがコントロールする裏向きのクリーチャー１体を選択。");
        if(view?.request?.kind=="InputConfirm") {
            if(view.request.options.Any(o=>o.label=="Top")&&view.request.options.Any(o=>o.label=="Bottom")
                &&view.request.previewCards?.Length==1) {
                string name=GamePreferences.CardFaceName(view.request.previewCards[0].name);
                return TapPaymentText("将“"+name+"”放在牌库顶还是牌库底？","Put "+name+" on the top or bottom of its owner's library?","「"+name+"」をオーナーのライブラリーの一番上か一番下に置く？");
            }
            if(message=="Do you want to investigate?")
                return TapPaymentText("要探查吗？","Do you want to investigate?","調査を行いますか？");
            var endure=System.Text.RegularExpressions.Regex.Match(message??"",@"(?:^|\n)You may put (\d+) \+1/\+1 counters on (.+)\. If you don't, create a (\d+)/(\d+) white Spirit creature token\.$");
            if(endure.Success&&endure.Groups[1].Value==endure.Groups[3].Value&&endure.Groups[1].Value==endure.Groups[4].Value) {
                string amount=endure.Groups[1].Value,name=GamePreferences.CardFaceName(endure.Groups[2].Value);
                return TapPaymentText($"在{name}上放置 {amount} 个 +1/+1 指示物？若不如此作，则派出一个 {amount}/{amount} 白色精怪衍生生物。",
                    $"Put {amount} +1/+1 counter(s) on {name}? If you don't, create a {amount}/{amount} white Spirit creature token.",
                    $"{name}の上に+1/+1カウンターを{amount}個置きますか？そうしないなら、白の{amount}/{amount}のスピリット・クリーチャー・トークン１体を生成します。");
            }
        }
        // The pinned mass-tap producer leaves {0} in its native question.
        // Keep the binary's original IDs and translate only this exact contract.
        if(view?.request?.kind=="InputConfirm"&&view.request.options.Any(o=>o.id=="ok"&&o.label=="Tap")&&view.request.options.Any(o=>o.id=="cancel"&&o.label=="Untap")) {
            if(message=="Tap or untap {0}? permanents of the chosen type target player controls?")
                return TapPaymentText("横置或重置目标牌手操控的所选类别永久物？","Tap or untap permanents of the chosen type target player controls?","対象のプレイヤーがコントロールする、選ばれたタイプのパーマネントをタップまたはアンタップしますか？");
            var tapTarget=System.Text.RegularExpressions.Regex.Match(message??"",@"^Tap or untap (.+)\?$");
            if(tapTarget.Success) {
                string name=GamePreferences.CardFaceName(tapTarget.Groups[1].Value);
                return TapPaymentText("横置或重置“"+name+"”？","Tap or untap "+name+"?","「"+name+"」をタップまたはアンタップしますか？");
            }
        }
        if(System.Text.RegularExpressions.Regex.IsMatch(message??"",@"(?:^|\n)Select a card from your graveyard and exile$"))
            return TapPaymentText("从你的坟墓场或放逐区选择一张牌。","Choose a card from your graveyard or exile.","あなたの墓地や追放領域からカード１枚を選択。");
        var sourceChoice=System.Text.RegularExpressions.Regex.Match(message??"",@"^(.+) \((\d+)\)'s Effect \((\d+)\)\n\nChoose a source $");
        if(sourceChoice.Success&&view?.request?.kind=="InputSelectEntitiesFromList") {
            var marker=(view.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>p.effectMarkers??Array.Empty<ForgeCardView>())
                .FirstOrDefault(c=>c.genericEffectName&&!c.faceDown&&c.id.ToString()==sourceChoice.Groups[3].Value
                    &&c.effectSourceName==sourceChoice.Groups[1].Value&&c.name==sourceChoice.Groups[1].Value+" ("+sourceChoice.Groups[2].Value+")'s Effect");
            if(marker!=null) return GamePreferences.CardFaceName(marker.effectSourceName)
                +TapPaymentText("的效果","'s Effect","の効果")+"\n\n"
                +TapPaymentText("选择一个来源。","Choose a source.","発生源を選択。");
        }
        if(view?.request?.kind=="dialog"&&message=="one: Select up to another target creature or spell")
            return TapPaymentText("可再选择至多一个目标生物或咒语。","Select up to another target creature or spell.","追加でクリーチャー１体か呪文１つを対象に選んでもよい。");
        if(view?.request?.kind=="InputConfirm"&&message=="Unattach Sunforger?") {
            string name=GamePreferences.CardFaceName("Sunforger");
            return TapPaymentText("将“"+name+"”卸装以支付费用？","Unattach "+name+" to pay this cost?","コストを支払うために「"+name+"」をはずしますか？");
        }
        var foundry=System.Text.RegularExpressions.Regex.Match(message??"",@"^Forger's Foundry \(\d+\)[\s\S]*\n\nDo you want to play (.+)\?$");
        if(foundry.Success){string name=ConfirmationCardName(foundry.Groups[1].Value);return TapPaymentText("免付法术力费用施放“"+name+"”？","Cast "+name+" without paying its mana cost?","「"+name+"」をマナ・コストを支払わずに唱えますか？");}
        var play=System.Text.RegularExpressions.Regex.Match(message??"",@"(?:^|\n)Do you want to play (.+)\?$",System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if(view?.request?.kind=="InputConfirm"&&play.Success) {
            string name=ConfirmationCardName(play.Groups[1].Value);
            bool land=view.request.sourceCard?.land==true;
            return land?TapPaymentText("打出“"+name+"”？","Play "+name+"?","「"+name+"」をプレイしますか？")
                :TapPaymentText("施放“"+name+"”？","Cast "+name+"?","「"+name+"」を唱えますか？");
        }
        if(message=="Sacrifice Food")return TapPaymentText("牺牲一个食品？","Sacrifice a Food?","食物１つを生け贄に捧げますか？");
        var blightCost=System.Text.RegularExpressions.Regex.Match(message??"",@"(?:^|\n)Put (\d+) -1/-1 counter on a creature you control$");
        if(blightCost.Success&&view?.request?.kind=="InputSelectCardsFromList"&&view.request.minCount==1&&view.request.maxCount==1) {
            string amount=blightCost.Groups[1].Value;
            return TapPaymentText($"选择一个由你操控的生物，在其上放置 {amount} 个 -1/-1 指示物以支付费用。",$"Choose a creature you control to put {amount} -1/-1 counter(s) on as a cost.",$"コストとして、あなたがコントロールするクリーチャー１体を選び、その上に-1/-1カウンターを{amount}個置く。");
        }
        var exileCost=System.Text.RegularExpressions.Regex.Match(message??"",@"(?:^|\n)Select (\d+) or more to exile$");
        if(exileCost.Success&&view?.request?.kind=="InputSelectCardsFromList"&&view.request.minCount==view.request.maxCount&&view.request.minCount.ToString()==exileCost.Groups[1].Value)
            return TapPaymentText($"选择 {view.request.minCount} 张牌来放逐。",$"Choose {view.request.minCount} cards to exile.",$"追放するカードを {view.request.minCount} 枚選択。");
        if(System.Text.RegularExpressions.Regex.IsMatch(message??"",@"(?:^|\n)Select Food to sacrifice \(\d+ left\)$")&&view?.request?.kind=="InputSelectCardsFromList"&&view.request.minCount==1&&view.request.maxCount==1)
            return TapPaymentText("选择一个食品来牺牲。","Choose a Food to sacrifice.","生け贄に捧げる食物１つを選択。");
        if(System.Text.RegularExpressions.Regex.IsMatch(message??"",@"(?:^|\n)Choose an exiled card to put into graveyard$"))return TapPaymentText("选择一张放逐中的牌，将其置入其拥有者的坟墓场。","Choose an exiled card to put into its owner's graveyard.","追放領域のカード１枚を選び、オーナーの墓地に置く。");
        if((message??"").EndsWith("Select Mana to Produce",System.StringComparison.Ordinal))return TapPaymentText("选择要产生的法术力颜色。","Choose a mana color to produce.","生み出すマナの色を選択。");
        if(message=="Choose a card to heist")return TapPaymentText("选择一张牌来窃取。","Choose a card to heist.","強奪するカード１枚を選択。");
        if(System.Text.RegularExpressions.Regex.IsMatch(message??"",@"(?:^|\n)Choose a card$"))return TapPaymentText("选择一张牌。","Choose a card.","カード１枚を選択。");
        if((message??"").StartsWith("Growth Spiral ("))return TapPaymentText("成长涡旋：可从手牌放一张地进战场，也可以取消。","Growth Spiral: you may put a land from your hand onto the battlefield, or decline.","成長のらせん：手札の土地１枚を戦場に出してもよい。");
        if((message??"").StartsWith("Planar Genesis ("))return TapPaymentText("时空创生：选择一张加入手牌；如果是地，可改为横置放进战场。","Planar Genesis: choose a card for your hand; a land may enter tapped instead.",GamePreferences.CardFaceName("Planar Genesis")+"：１枚を手札に加える。土地ならタップ状態で戦場に出してもよい。");
        if((message??"").StartsWith("Brainsurge ("))return TapPaymentText("脑力涌动：从手牌选择两张，再按指定顺序放回牌库顶。","Brainsurge: choose two hand cards, then order them on top of your library.",GamePreferences.CardFaceName("Brainsurge")+"：手札２枚を選び、好きな順番でライブラリーの一番上に置く。");
        var discover=System.Text.RegularExpressions.Regex.Match(message??"",@"(?:^|\n)Do you want to cast (.+) without paying its mana cost or put it into your hand\?$",System.Text.RegularExpressions.RegexOptions.Multiline);
        if(discover.Success) {
            string name=GamePreferences.CardFaceName(discover.Groups[1].Value);
            return TapPaymentText("发现：不支付法术力费用施放“"+name+"”，或将其放入手牌。","Discover: cast "+name+" without paying its mana cost, or put it in your hand.","発見：「"+name+"」をマナ・コストを支払わずに唱えるか、手札に加える。");
        }
        var explore=System.Text.RegularExpressions.Regex.Match(message??"",@"^\[Explore\] Put (.+) in your graveyard\?$",System.Text.RegularExpressions.RegexOptions.Multiline);
        if(explore.Success){string name=GamePreferences.CardFaceName(explore.Groups[1].Value);return TapPaymentText("勘察：将“"+name+"”放进坟墓场？","Explore: put "+name+" into your graveyard?","探検：「"+name+"」を墓地に置きますか？");}
        return localizeFallback?NativeDisplayText(message):message;
    }
    private string ConfirmationCardName(string fallback)
    {
        var shown=view?.request?.previewCards;
        string canonical=shown?.Length==1&&!shown[0].faceDown?shown[0].name:view?.request?.sourceCard?.name;
        return GamePreferences.CardFaceName(string.IsNullOrEmpty(canonical)?fallback:canonical);
    }
    // Unity's JsonUtility may materialize an empty nested object for an omitted card.
    private static bool HasChoiceCard(ForgeOption option)=>option.card!=null && !string.IsNullOrEmpty(option.card.name);
    private ForgeCardView[] VisibleHand(ForgePlayerView player)
    {
        var visible=player.hand??Array.Empty<ForgeCardView>();
        if(visible.Length==player.handCount)return visible;
        if(view?.awaitingInput==true && view.request?.kind=="RevealCards") {
            var revealed=(view.request.previewCards??Array.Empty<ForgeCardView>()).Where(c=>c.zone=="Hand"&&c.ownerSeat==player.seat).GroupBy(c=>c.id).Select(g=>g.First()).ToArray();
            if(revealed.Length==player.handCount)return revealed;
        }
        return visible;
    }
    // Use the recipient's visible hand only. Candidate subsets must not reveal hidden cards.
    private ForgePlayerView SelectionHand
    {
        get {
            if(!integrated || view?.awaitingInput!=true || view.request==null ||
                view.request.kind is "InputPassPriority" or "InputConfirm" or "ChoosePile" or "Scry" or "Surveil" or "OrderCards" || view.request.kind.Contains("Mulligan") ||
                view.request.selectionPurpose=="optionalReplacement")return null;
            var cards=(view.request.options??Array.Empty<ForgeOption>()).Where(HasChoiceCard).Select(o=>o.card)
                .Concat(view.request.previewCards??Array.Empty<ForgeCardView>()).ToArray();
            if(cards.Length==0 || cards.Any(c=>c.zone!="Hand"))return null;
            return (view.players??Array.Empty<ForgePlayerView>()).FirstOrDefault(p=>p.handCount>0 &&
                VisibleHand(p).Length==p.handCount && cards.All(c=>VisibleHand(p).Any(h=>h.id==c.id)));
        }
    }
    private bool DirectHandSelection => SelectionHand is ForgePlayerView player && player.handCount<=10;
    private bool IsCardPickerRequest => !HasStackTargetChoices && view?.awaitingInput==true && view.request!=null && view.request.selectionPurpose!="optionalReplacement" && view.request.selectionPurpose!="roomDoor" &&
        !DirectHandSelection && !IsManaProductionChoice(view.request) &&
        (view.request.kind=="ChoosePile" || (view.request.previewCards?.Length??0)>0 ||
         (view.request.options??Array.Empty<ForgeOption>()).Any(o=>HasChoiceCard(o) &&
            (view.request.kind!="InputSelectTargets" || !RepresentedOnBoard(o.id))));

    private void PresentCardSelection()
    {
        if(CompletedSingleCardOption(view?.request)>=0){if(cardSelectionOverlay!=null)cardSelectionOverlay.gameObject.SetActive(false);return;}
        if(handSelectionPrompt!=null)handSelectionPrompt.gameObject.SetActive(false);
        if((IsCardPickerRequest || DirectHandSelection) && browsedZone!=null)CloseReviewCards();
        if(!IsCardPickerRequest && browsedZone!=null){RefreshZoneBrowser();return;}
        if(!IsCardPickerRequest){if(cardSelectionOverlay!=null)cardSelectionOverlay.gameObject.SetActive(false);selectionLayoutKey=null;return;}
        RenderCardSelection(view.request);
    }

    // Shared presentation for engine choices and read-only post-game zone browsing.
    // The latter never changes the authoritative view/request or submits a choice.
    private void RenderCardSelection(ForgeRequest request, string reviewTitle = null)
    {
        bool readOnly = reviewTitle != null;
        cardSelectionReadOnly = readOnly;
        if(!readOnly && request.kind=="ChoosePile"){RenderPileSelection(request);return;}
        var selectedHand=SelectionHand;
        var previews=(request.previewCards??Array.Empty<ForgeCardView>()).Concat(readOnly||selectedHand==null?Array.Empty<ForgeCardView>():VisibleHand(selectedHand)).ToArray();
        var allCards=request.options.Where(HasChoiceCard).Select(o=>o.card)
            .Concat(previews).GroupBy(c=>c.id).Select(g=>g.First()).ToArray();
        bool mixedOwners=allCards.Where(c=>c.ownerSeat>0).Select(c=>c.ownerSeat).Distinct().Count()>1;
        bool mixedZones=allCards.Where(c=>!string.IsNullOrEmpty(c.zone)).Select(c=>c.zone).Distinct().Count()>1;
        string key=request.kind+"/"+selectionPage+"/"+surface.rect.size+"/"+request.sourceCardId+"/"+GamePreferences.Revision+"/"+
            request.votingPlayerSeat+"/"+request.secretVote+"/"+request.message+"/"+
            string.Join("|",allCards.OrderBy(c=>c.id).Select(ZoneTargetHint))+"/"+
            string.Join("|",allCards.OrderBy(c=>c.id).Select(c=>c.id+":"+c.printingKey+":"+c.backFace+":"+c.ownerSeat+":"+c.zone));
        if(!readOnly && key==selectionLayoutKey && selectionPanel!=null) {
            cardSelectionOverlay.gameObject.SetActive(true);
            cardSelectionOverlay.SetAsLastSibling();SetOverlayLayer(cardSelectionOverlay,DecisionLayerOffset,true);
            selectionPanel.Find("Count").GetComponent<Text>().text=SelectionProgress;
            selectionInstructionText.text=SelectionInstruction;
            RefreshLongSelectionInstruction();
            foreach(var card in allCards) {
                var options=request.options.Where(o=>HasChoiceCard(o)&&o.card.id==card.id).ToArray();
                var tile=selectionPanel.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t=>t.name=="ChoiceCard_"+card.id);
                if(tile==null)continue;
                PresentComponentFace((RectTransform)tile.Find("Artwork"),card);
                bool chosen=options.Any(o=>o.selected||IsStagedHandChoice(o.id))||!string.IsNullOrEmpty(card.selection)&&card.selection!="不可选";
                PresentSelectionGlow(tile,(RectTransform)tile.Find("Artwork"),chosen||!string.IsNullOrEmpty(ZoneTargetHint(card)));
                tile.GetComponent<Image>().color=chosen?new Color(.16f,.18f,.20f):options.Length>0?new Color(.18f,.45f,.58f):new Color(.12f,.16f,.21f);
                var destination=tile.Find("Destination")?.GetComponent<Text>();if(destination!=null)destination.text=card.selection??"";
                var order=tile.Find("Artwork/Order")?.GetComponent<Text>();if(order!=null)order.text=chosen&&options.Length>0?options[0].order.ToString():"";
                var counterCount=tile.Find("CounterCount")?.GetComponent<Text>();if(counterCount!=null)counterCount.text=CounterSelectionCount(options);
                if((request.kind=="Scry"||request.kind=="Surveil")&&allCards.Length>1) {
                    float cellWidth=tile.rect.width+8,existingArtHeight=tile.Find("Artwork").GetComponent<RectTransform>().rect.height;
                    for(int j=0;j<options.Length;j++)if(tile.Find("Select_"+options[j].id)==null) {
                        var option=options[j];float buttonWidth=(cellWidth-24)/2;
                        string label=option.id.StartsWith("scry-top:")?TapPaymentText("置顶","Top","上"):request.kind=="Surveil"?TapPaymentText("入墓","Graveyard","墓地"):TapPaymentText("置底","Bottom","下");
                        var added=Button(tile,"Select_"+option.id,label,new Vector2((j-.5f)*buttonWidth,-existingArtHeight-72),new Vector2(buttonWidth-4,48),()=>{},new Color(.18f,.32f,.49f));
                        ((RectTransform)added.transform).anchorMin=((RectTransform)added.transform).anchorMax=new Vector2(.5f,1);PreferenceText.SetSize(added.GetComponentInChildren<Text>(),16);
                        FitSelectionDestinationButton(added);
                    }
                }
                foreach(var control in tile.GetComponentsInChildren<Button>(true).Where(b=>b.name.StartsWith("Select_"))) {
                    var option=options.FirstOrDefault(o=>"Select_"+o.id==control.name);
                    control.gameObject.SetActive(option!=null);control.onClick.RemoveAllListeners();
                    var artwork=(RectTransform)tile.Find("Artwork");var buttonRect=(RectTransform)control.transform;
                    buttonRect.anchoredPosition=new Vector2(buttonRect.anchoredPosition.x,
                        artwork.anchoredPosition.y-artwork.rect.height*.5f-12-buttonRect.rect.height*.5f
                        -(tile.Find("OriginZone")!=null?26:0)-(tile.Find("Destination")!=null?36:0));
                    if(option!=null){int index=OptionIndex(option.id);long captured=requestId;control.onClick.AddListener(()=>Choose(index,captured));control.interactable=canAct&&!submitted;}
                }
                var face=tile.Find("Artwork").GetComponent<Button>();
                if(face==null)continue;
                face.onClick.RemoveAllListeners();face.interactable=options.Length==1&&canAct&&!submitted&&!options[0].disabled;
                if(options.Length==1){int index=OptionIndex(options[0].id);long captured=requestId;face.onClick.AddListener(()=>Choose(index,captured));}
            }
            var previousPage=selectionPanel.Find("PreviousPage")?.GetComponent<Button>();
            var nextPage=selectionPanel.Find("NextPage")?.GetComponent<Button>();
            int count=allCards.Length;
            if(previousPage!=null)previousPage.interactable=canAct&&!submitted&&selectionPage>0;
            if(nextPage!=null)nextPage.interactable=canAct&&!submitted&&(selectionPage+1)*10<count;
            PresentSelectionFooter(selectionPanel.rect.height,request.kind);return;
        }
        selectionLayoutKey=readOnly?null:key;
        if(cardSelectionOverlay!=null)Clear(cardSelectionOverlay);
        if(selectionKind!=request.kind){selectionPage=0;selectionKind=request.kind;}
        if(cardSelectionOverlay==null)
        {
            cardSelectionOverlay=Panel(surface,"CardSelectionOverlay",Vector2.zero,Vector2.zero,new Color(0,0,0,.7f));
            cardSelectionOverlay.anchorMin=Vector2.zero;cardSelectionOverlay.anchorMax=Vector2.one;
            cardSelectionOverlay.offsetMin=cardSelectionOverlay.offsetMax=Vector2.zero;
        }
        cardSelectionOverlay.gameObject.SetActive(true);cardSelectionOverlay.SetAsLastSibling();SetOverlayLayer(cardSelectionOverlay,DecisionLayerOffset,true);
        var groups=(request.options??Array.Empty<ForgeOption>()).Where(HasChoiceCard).GroupBy(o=>o.card.id)
            .Select(g=>new {card=g.First().card,options=g.ToArray()}).ToList();
        groups.AddRange(previews.Where(c=>!groups.Any(g=>g.card.id==c.id)).GroupBy(c=>c.id).Select(g=>new {card=g.First(),options=Array.Empty<ForgeOption>()}));
        if(groups.Any(g=>g.card.selection=="不可选"))groups=groups.OrderBy(g=>g.card.selection=="不可选")
            .ThenBy(g=>g.card.name,StringComparer.OrdinalIgnoreCase).ThenBy(g=>g.card.id).ToList();
        const int pageSize=10;
        int pageCount=Math.Max(1,(groups.Count+pageSize-1)/pageSize);selectionPage=Mathf.Clamp(selectionPage,0,pageCount-1);
        selectionPageFooter=pageCount>1?44:0;
        var visible=groups.Skip(selectionPage*pageSize).Take(pageSize).ToArray();
        string kind=request.kind;
        bool orderedLibrary=readOnly&&kind=="BrowseLibrary";
        bool destinationChoices=groups.Any(g=>g.options.Length>1);
        bool showDestination=kind=="Scry" || kind=="Surveil";
        bool singleDestination=showDestination&&groups.Count==1;
        float width=Mathf.Min(Mathf.Max(360,Math.Min(5,visible.Length)*200+56),surface.rect.width-36);
        // A one-card native picker can gain confirm/cancel/reset after selection.
        // Reserve all three footer slots before that cached update happens.
        if(!readOnly&&visible.Length==1&&kind=="InputSelectCardsFromList")width=Mathf.Min(Mathf.Max(width,438),surface.rect.width-36);
        if(!readOnly&&kind=="InputSelectEntitiesFromList"&&request.options.Any(o=>o.id.StartsWith("player:",StringComparison.Ordinal))) {
            int footerSlots=request.options.Count(o=>!HasChoiceCard(o))+ (request.options.Any(o=>o.id=="reset-cards")?0:1);
            float slotWidth=(GamePreferences.Language=="ja-JP"?148:130)*Mathf.Max(1,GamePreferences.TextScale);
            width=Mathf.Min(Mathf.Max(width,footerSlots*slotWidth+48),surface.rect.width-36);
        }
        if(IsCommanderPerpetualReset)width=Mathf.Min(Mathf.Max(width,560),surface.rect.width-36);
        int columns=Mathf.Clamp(Mathf.FloorToInt((width-48)/200),1,Math.Max(1,Math.Min(5,visible.Length)));
        float cell=Mathf.Min(200,(width-56)/columns),artWidth=cell-20,artHeight=artWidth*1.4f;
        var targetHints=visible.Select(item=>ZoneTargetHint(item.card)).ToArray();
        float targetHintHeight=ZoneTargetHintHeight(targetHints,artWidth);
        float rowHeight=artHeight+(singleDestination?28:destinationChoices||showDestination?116:request.selectionPurpose=="removeCounters"?64:28)+targetHintHeight;
        float originHeight=mixedZones?26:0;
        rowHeight+=originHeight;
        float positionHeight=orderedLibrary?Mathf.Max(28,Mathf.Ceil(24*GamePreferences.TextScale)):0;
        rowHeight+=positionHeight;
        bool castRow=(kind=="BrowseZone"||kind=="BrowseSideboard"||orderedLibrary)&&view?.request?.kind=="InputPassPriority"&&canAct&&!submitted
            &&visible.Any(item=>!item.card.faceDown&&OptionIndex("card:"+item.card.id) is int index&&index>=0&&!view.request.options[index].disabled);
        if(castRow)rowHeight+=40;
        bool legacyCosts=visible.Any(item=>HasLegacyCost(item.card));
        float legacyCostHeadroom=legacyCosts?LegacyCardCost.IconSize(artWidth)+9:0;
        rowHeight+=legacyCostHeadroom;
        int rows=Mathf.Max(1,Mathf.CeilToInt(visible.Length/(float)columns));
        float height=Mathf.Min(186+selectionPageFooter+16+rows*rowHeight,surface.rect.height-36);
        var panel=Panel(cardSelectionOverlay,"CardSelection",Vector2.zero,new Vector2(width,height),Ink);
        selectionPanel=panel;
        selectionFooter=Rect(panel,"SelectionFooter",Vector2.zero,Vector2.zero);
        string title=request.selectionPurpose=="collectEvidence"?TapPaymentText("搜证","Collect evidence","証拠収集"):IsTapPayment(request)?TapPaymentTitle(request):kind=="Scry"?TapPaymentText("占卜","Scry","占術"):kind=="Surveil"?TapPaymentText("刺探","Surveil","諜報"):kind=="OrderCards"?TapPaymentText("排列卡牌顺序","Order cards","カードの順番"):kind=="RevealCards"?TapPaymentText("查看卡牌","View cards","カードを見る"):kind=="InputConfirm"?TapPaymentText("确认效果","Confirm effect","効果を確認"):TapPaymentText("选择卡牌","Choose cards","カードを選択");
        if(IsCommanderPerpetualReset)title=TapPaymentText("主将的永久改动","Commander modifications","統率者の永久的な変更");
        if(request.selectionPurpose=="dungeon")title=TapPaymentText("选择地城","Choose a dungeon","ダンジョンを選択");
        if(request.selectionPurpose=="removeCounters")title=TapPaymentText("移去指示物","Remove counters","カウンターを取り除く");
        if(request.selectionPurpose=="putLibraryCost")title=TapPaymentText("支付费用","Payment","支払い");
        if(request.selectionPurpose=="beholdExile"||request.selectionPurpose=="behold")title=TapPaymentText("支付费用","Payment","支払い");
        if(request.selectionPurpose=="chooseSource")title=TapPaymentText("选择来源","Choose a source","発生源を選択");
        if(IsRingBearerSelection)title=TapPaymentText("选择持戒者","Ring-bearer","指輪所持者");
        if(IsProliferateSelection)title=TapPaymentText("增殖","Proliferate","増殖");
        if(IsHandDiscardSelection)title=TapPaymentText("选择弃牌","Choose a discard","捨てるカードを選択");
        if(VoteChoiceTitle()!=null)title=VoteChoiceTitle();
        if(kind=="OrderCards")title=IsExertAttack(request)?TapPaymentText("耗竭","Exert","督励"):TapPaymentText("排序","Order","順番");
        var titleLabel=Label(panel,"Title",reviewTitle??title,new Vector2(0,height/2-28),new Vector2(width-40,38),25,Paper);titleLabel.alignment=TextAnchor.MiddleLeft;
        // Apply the same binding used by the periodic preference refresh now:
        // a scaled CJK title must not disappear in the initial truncated frame.
        PreferenceText.SetSize(titleLabel,25);
        if(readOnly&&visible.Any(item=>UsesComponentFace(item.card))) {
            titleLabel.resizeTextForBestFit=false;titleLabel.verticalOverflow=VerticalWrapMode.Overflow;
            (titleLabel.GetComponent<PreferenceText>()??titleLabel.gameObject.AddComponent<PreferenceText>()).SetBaseSize(21);
        }
        float countWidth=Mathf.Max(130,width-210);
        var countLabel=Label(panel,"Count",readOnly?TapPaymentText($"{allCards.Length} 张",$"{allCards.Length} "+(allCards.Length==1?"card":"cards"),$"{allCards.Length} 枚"):SelectionProgress,new Vector2(width/2-countWidth/2-20,height/2-28),new Vector2(countWidth,38),16,Paper);countLabel.alignment=TextAnchor.MiddleRight;
        if(singleDestination)countLabel.gameObject.SetActive(false);
        titleLabel.rectTransform.sizeDelta=new Vector2(width-40,Mathf.Max(38,Mathf.Ceil(titleLabel.preferredHeight)+4));
        countLabel.rectTransform.sizeDelta=new Vector2(countWidth,Mathf.Max(38,Mathf.Ceil(countLabel.preferredHeight)+4));
        if(!readOnly&&(IsProliferateSelection||IsRingBearerSelection||request.selectionPurpose=="putLibraryCost"||request.selectionPurpose=="removeCounters"||kind=="OrderCards")) {
            (titleLabel.GetComponent<PreferenceText>()??titleLabel.gameObject.AddComponent<PreferenceText>()).SetBaseSize(IsRingBearerSelection?21:25);
            titleLabel.resizeTextForBestFit=false;countLabel.resizeTextForBestFit=false;
            (countLabel.GetComponent<PreferenceText>()??countLabel.gameObject.AddComponent<PreferenceText>()).SetBaseSize(16);
            if(kind=="OrderCards") {
                titleLabel.rectTransform.sizeDelta=new Vector2(width-40,Mathf.Max(38,Mathf.Ceil(titleLabel.preferredHeight)+4));
                countLabel.rectTransform.sizeDelta=new Vector2(countWidth,Mathf.Max(38,Mathf.Ceil(countLabel.preferredHeight)+4));
            }
        }
        var prompt=Label(panel,"Instruction",orderedLibrary?LibraryBrowserInstruction:readOnly?TapPaymentText("点击卡牌查看详情","Click a card for details","カードをクリックして詳細を見る"):SelectionInstruction,new Vector2(0,height/2-79),new Vector2(width-48,58),17,Paper);
        selectionInstructionText=prompt;selectionInstructionContent=null;
        if(request.selectionPurpose=="chooseSource"){
            (titleLabel.GetComponent<PreferenceText>()??titleLabel.gameObject.AddComponent<PreferenceText>()).SetBaseSize(21);titleLabel.rectTransform.sizeDelta=new Vector2(width-40,44);
            (countLabel.GetComponent<PreferenceText>()??countLabel.gameObject.AddComponent<PreferenceText>()).SetBaseSize(16);
            (prompt.GetComponent<PreferenceText>()??prompt.gameObject.AddComponent<PreferenceText>()).SetBaseSize(17);
            titleLabel.resizeTextForBestFit=false;prompt.resizeTextForBestFit=false;
        }
        prompt.supportRichText=false;prompt.alignment=TextAnchor.UpperLeft;
        prompt.verticalOverflow=VerticalWrapMode.Truncate;
        if(prompt.text.Contains("{"))SymbolizeStackLabel(prompt);
        prompt.resizeTextForBestFit=true;prompt.resizeTextMinSize=12;prompt.resizeTextMaxSize=17;
        if(IsOptionalCardVote||IsProliferateSelection||IsRingBearerSelection||request.selectionPurpose=="putLibraryCost"||request.selectionPurpose=="beholdExile"||request.selectionPurpose=="behold"||request.selectionPurpose=="removeCounters"||request.selectionPurpose=="chooseSource"||kind=="OrderCards"||kind=="Scry"||kind=="Surveil"||ResolutionChoiceInstruction(request.message)!=request.message) {
            prompt.resizeTextForBestFit=false;
            // New dynamic labels otherwise wait for the periodic preference
            // scan, showing a smaller first frame before the user's text scale.
            (prompt.GetComponent<PreferenceText>()??prompt.gameObject.AddComponent<PreferenceText>()).SetBaseSize(17);
        }
        float instructionExtra=0;
        // Unknown native descriptions still need a readable, complete fallback.
        bool nativeInstruction=!readOnly&&(SelectionInstruction==request.message||kind=="RevealCards"
            ||kind=="InputSelectTargets"||kind=="SelectStackTarget");
        if(nativeInstruction){prompt.resizeTextForBestFit=false;PreferenceText.SetSize(prompt,17);}
        if(kind=="RevealCards")prompt.rectTransform.sizeDelta=new Vector2(width-62,prompt.rectTransform.sizeDelta.y);
        bool longNativeInstruction=nativeInstruction&&prompt.preferredHeight>58;
        bool localizedEntityInstruction=!readOnly&&kind=="InputSelectEntitiesFromList"&&ResolutionChoiceInstruction(request.message)!=request.message;
        float headingExtra=longNativeInstruction||localizedEntityInstruction?Mathf.Max(0,Mathf.Max(titleLabel.rectTransform.rect.height,countLabel.rectTransform.rect.height)*.5f-16):0;
        // Known localized choices may have a multi-line question. Reserve
        // its measured height at the user's font scale instead of shrinking it.
        if(!readOnly&&(longNativeInstruction||IsProliferateSelection||IsRingBearerSelection||request.selectionPurpose=="beholdExile"||request.selectionPurpose=="behold"||request.selectionPurpose=="removeCounters"||kind=="OrderCards"||kind=="Scry"||kind=="Surveil"||(kind=="InputSelectCardsFromList"||kind=="InputSelectEntitiesFromList"||kind=="dialog")&&ResolutionChoiceInstruction(request.message)!=request.message||System.Text.RegularExpressions.Regex.IsMatch(request.message??"",@"(?:^|\n)Choose an exiled card to put into graveyard$")||kind=="InputConfirm"&&ResolutionChoiceInstruction(request.message)!=request.message&&prompt.preferredHeight>58||kind=="InputConfirm"&&((request.message??"").EndsWith("Select Mana to Produce",System.StringComparison.Ordinal)||request.options.Any(o=>o.label=="Cast")&&request.options.Any(o=>o.label=="Hand")||(request.message??"").StartsWith("Forger's Foundry (",System.StringComparison.Ordinal)&&ResolutionChoiceInstruction(request.message)!=request.message))) {
            instructionExtra=Mathf.Min(Mathf.Max(0,Mathf.Ceil(prompt.preferredHeight)+4-58),Mathf.Max(0,surface.rect.height-36-height));
            if(localizedEntityInstruction)instructionExtra+=headingExtra;
            if(longNativeInstruction) {
                // Reveals include one result line per owner plus the outcome.
                // Use available screen height before making that result scroll.
                float instructionRoom=kind=="RevealCards"?Mathf.Max(0,surface.rect.height-36-height-headingExtra)
                    :Mathf.Max(0,surface.rect.height-36-186-selectionPageFooter-144-headingExtra);
                float instructionLimit=kind=="RevealCards"?160:Mathf.Max(92,surface.rect.height*.3f-58);
                instructionExtra=Mathf.Min(Mathf.Max(0,Mathf.Ceil(prompt.preferredHeight)+4-58),instructionLimit,instructionRoom)+headingExtra;
            }
            float panelGrowth=Mathf.Min(instructionExtra,Mathf.Max(0,surface.rect.height-36-height));
            height+=panelGrowth;panel.sizeDelta=new Vector2(width,height);
            titleLabel.rectTransform.anchoredPosition+=new Vector2(0,panelGrowth/2);
            countLabel.rectTransform.anchoredPosition+=new Vector2(0,panelGrowth/2);
            prompt.rectTransform.anchoredPosition+=new Vector2(0,(panelGrowth-instructionExtra-headingExtra)/2);
            prompt.rectTransform.sizeDelta=new Vector2(width-48,58+instructionExtra-headingExtra);
        }
        if(longNativeInstruction&&prompt.preferredHeight+4>prompt.rectTransform.rect.height) {
            selectionInstructionContent=Scroll(panel,"InstructionScroll",prompt.rectTransform.anchoredPosition,prompt.rectTransform.sizeDelta);
            AddStackScrollIndicator(selectionInstructionContent);
            prompt.rectTransform.SetParent(selectionInstructionContent,false);
            prompt.rectTransform.anchorMin=prompt.rectTransform.anchorMax=prompt.rectTransform.pivot=new Vector2(.5f,1);
            prompt.rectTransform.anchoredPosition=new Vector2(-4,0);
            RefreshLongSelectionInstruction();
        }
        var content=Scroll(panel,"CardSelectionScroll",new Vector2(0,-20-instructionExtra/2+selectionPageFooter/2),new Vector2(width-36,height-186-selectionPageFooter-instructionExtra));
        AddStackScrollIndicator(content);
        float gridLeft=Mathf.Max(8,(width-44-columns*cell)/2);
        for(int i=0;i<visible.Length;i++)
        {
            var item=visible[i];var card=item.card;
            var tile=Panel(content,"ChoiceCard_"+card.id,Vector2.zero,new Vector2(cell-8,singleDestination?artHeight+16+targetHintHeight+legacyCostHeadroom:rowHeight-10),new Color(.12f,.16f,.21f));
            tile.anchorMin=tile.anchorMax=tile.pivot=new Vector2(0,1);
            tile.anchoredPosition=new Vector2(gridLeft+(i%columns)*cell,-8-(i/columns)*rowHeight);
            bool unavailable=card.selection=="不可选";
            bool chosen=item.options.Any(o=>o.selected||IsStagedHandChoice(o.id))||!string.IsNullOrEmpty(card.selection)&&!unavailable;
            bool selectable=item.options.Length>0;
            tile.GetComponent<Image>().color=chosen?new Color(.16f,.18f,.20f):selectable?new Color(.18f,.45f,.58f):new Color(.12f,.16f,.21f);
            if(targetHints.Length>i)PresentZoneTargetHint(tile,targetHints[i],artWidth,targetHintHeight);
            var art=Panel(tile,"Artwork",new Vector2(0,-artHeight/2-8-targetHintHeight-legacyCostHeadroom),new Vector2(artWidth,artHeight),Ink);
            art.anchorMin=art.anchorMax=new Vector2(.5f,1);
            PresentSelectionGlow(tile,art,chosen||!string.IsNullOrEmpty(targetHints[i]));
            Sprite sprite=null;
            TryBattlefieldArt(card,out sprite);
            var image=art.GetComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.color=sprite==null?Ink:Color.white;
            if(unavailable)image.color=new Color(.38f,.38f,.38f);
            PresentComponentFace(art,card);
            PresentLegacyCost(art,card);
            var name=Label(art,"CardName",card.faceDown?"牌背":card.name,Vector2.zero,new Vector2(artWidth-12,artHeight-12),18,Paper);
            name.raycastTarget=false;name.supportRichText=false;name.gameObject.SetActive(sprite==null);
            if(!card.faceDown && card.planeswalker && card.zone=="Battlefield") {
                var badge=Panel(art,"CurrentLoyalty",new Vector2(0,-artHeight/2+16),new Vector2(artWidth,32),Ink);
                badge.GetComponent<Image>().raycastTarget=false;
                var value=Label(badge,"Value",TapPaymentText("忠诚","Loyalty","忠誠度")+" "+card.loyalty,Vector2.zero,new Vector2(artWidth-8,30),20,Paper);
                value.fontStyle=FontStyle.Bold;value.raycastTarget=false;
                PreferenceText.SetSize(value,20);value.resizeTextForBestFit=false;
                float badgeHeight=Mathf.Max(32,Mathf.Ceil(value.preferredHeight)+4);
                badge.sizeDelta=new Vector2(artWidth,badgeHeight);badge.anchoredPosition=new Vector2(0,-artHeight/2+badgeHeight/2);
                value.rectTransform.sizeDelta=new Vector2(artWidth-8,badgeHeight);
            }
            if(mixedOwners) {
                var owner=Panel(art,"Owner",new Vector2(0,artHeight/2-11),new Vector2(artWidth,22),new Color(.035f,.055f,.075f,.94f));
                var ownerText=Label(owner,"OwnerLabel",card.ownerSeat==seat?TapPaymentText("你的牌","Your card","あなたのカード"):TapPaymentText("对手的牌","Opponent’s card","対戦相手のカード"),Vector2.zero,new Vector2(artWidth-8,22),13,Paper);ownerText.raycastTarget=false;owner.GetComponent<Image>().raycastTarget=false;
            }
            if(readOnly) {
                var face=art.gameObject.AddComponent<Button>();face.targetGraphic=image;face.transition=Selectable.Transition.None;
                face.onClick.AddListener(()=>ShowReviewDetail(card));
            }
            float y=-artHeight-28-legacyCostHeadroom;
            if(orderedLibrary) {
                var position=Label(tile,"LibraryPosition",LibraryPositionLabel(card.libraryPosition,allCards.Length),new Vector2(0,y),new Vector2(artWidth,positionHeight),14,Paper);
                position.rectTransform.anchorMin=position.rectTransform.anchorMax=new Vector2(.5f,1);
                position.raycastTarget=false;position.resizeTextForBestFit=false;PreferenceText.SetSize(position,14);
                y-=positionHeight;
            }
            if(mixedZones) {
                var origin=Label(tile,"OriginZone",StackTargetZone(card.zone),new Vector2(0,y),new Vector2(artWidth,24),14,Paper);
                origin.rectTransform.anchorMin=origin.rectTransform.anchorMax=new Vector2(.5f,1);
                origin.raycastTarget=false;origin.resizeTextForBestFit=false;
                PreferenceText.SetSize(origin,14);y-=originHeight;
            }
            // Browsing is read-only except an explicit server-offered cast action.
            // Its availability must not depend on the chosen card renderer.
            int castOption=OptionIndex("card:"+card.id);
            if((kind=="BrowseZone"||kind=="BrowseSideboard"||orderedLibrary)&&!card.faceDown&&view?.request?.kind=="InputPassPriority"
                &&castOption>=0&&!view.request.options[castOption].disabled&&canAct&&!submitted) {
                long expected=requestId;
                var cast=Button(tile,"CastComponentCard",card.land?TapPaymentText("使用","Play","プレイ"):TapPaymentText("施放","Cast","唱える"),new Vector2(0,y-6),new Vector2(artWidth,32),()=>{CloseReviewCards();Choose(castOption,expected);},new Color(.18f,.32f,.49f));
                ((RectTransform)cast.transform).anchorMin=((RectTransform)cast.transform).anchorMax=new Vector2(.5f,1);
                PreferenceText.SetSize(cast.GetComponentInChildren<Text>(),15);
            }
            if(showDestination&&!singleDestination)
            {
                var caption=Label(tile,"Destination",card.selection??"",new Vector2(0,y),new Vector2(cell-20,32),16,Paper);
                caption.rectTransform.anchorMin=caption.rectTransform.anchorMax=new Vector2(.5f,1);caption.supportRichText=false;
            }
            if(request.selectionPurpose=="removeCounters") {
                var count=Label(tile,"CounterCount",CounterSelectionCount(item.options),new Vector2(0,y),new Vector2(cell-20,32),16,Paper);
                count.rectTransform.anchorMin=count.rectTransform.anchorMax=new Vector2(.5f,1);count.raycastTarget=false;
                (count.GetComponent<PreferenceText>()??count.gameObject.AddComponent<PreferenceText>()).SetBaseSize(16);
            }
            if(kind=="OrderCards"&&!IsExertAttack(request))
            {
                var order=Label(art,"Order",chosen&&item.options.Length>0?item.options[0].order.ToString():"",new Vector2(artWidth/2-20,artHeight/2-20),new Vector2(36,36),24,Color.yellow);
                order.raycastTarget=false;
                order.resizeTextForBestFit=false;
                order.rectTransform.anchorMin=order.rectTransform.anchorMax=order.rectTransform.pivot=Vector2.one;
                order.rectTransform.anchoredPosition=new Vector2(-6,-6);
                order.rectTransform.sizeDelta=new Vector2(36,Mathf.Max(36,Mathf.Ceil(order.preferredHeight)+4));
            }
            for(int j=0;j<item.options.Length;j++)
            {
                var option=item.options[j];int index=OptionIndex(option.id);long captured=requestId;
                // One-card destination choices belong to the window footer,
                // outside the artwork frame and scroll viewport.
                if(singleDestination)continue;
                if(item.options.Length==1)
                {
                    var face=art.gameObject.AddComponent<Button>();face.targetGraphic=image;face.transition=Selectable.Transition.None;
                    face.onClick.AddListener(()=>Choose(index,captured));face.interactable=canAct&&!submitted&&!option.disabled;
                    continue;
                }
                string text=option.id.StartsWith("card:")?(option.selected?TapPaymentText("取消选择","Deselect","選択解除"):TapPaymentText("选择","Select","選択")):option.label;
                if(option.id.StartsWith("scry-top:"))text=TapPaymentText("置顶","Top","上");
                if(option.id.StartsWith("scry-bottom:"))text=kind=="Surveil"?TapPaymentText("入墓","Graveyard","墓地"):TapPaymentText("置底","Bottom","下");
                float buttonWidth=(cell-24)/item.options.Length;
                var button=Button(tile,"Select_"+option.id,text,new Vector2((j-(item.options.Length-1)*.5f)*buttonWidth,y-(singleDestination?0:44)),new Vector2(buttonWidth-4,48),()=>Choose(index,captured),new Color(.18f,.32f,.49f));
                ((RectTransform)button.transform).anchorMin=((RectTransform)button.transform).anchorMax=new Vector2(.5f,1);
                PreferenceText.SetSize(button.GetComponentInChildren<Text>(),16);button.interactable=canAct&&!submitted;
                FitSelectionDestinationButton(button);
                // Anchor from the artwork's measured bottom edge, including
                // cost/header headroom and the localized button's full height.
                var buttonRect=(RectTransform)button.transform;
                buttonRect.anchoredPosition=new Vector2(buttonRect.anchoredPosition.x,
                    art.anchoredPosition.y-art.rect.height*.5f-12-buttonRect.rect.height*.5f
                    -(mixedZones?originHeight:0)-(showDestination&&!singleDestination?36:0));
            }
        }
        var cardScroll=content.parent.GetComponent<ScrollRect>();
        float cardViewportHeight=cardScroll.viewport.rect.height;
        float cardContentHeight=Mathf.Max(cardViewportHeight,16+rows*rowHeight);
        content.sizeDelta=new Vector2(width-44,cardContentHeight);
        cardScroll.vertical=cardContentHeight>cardViewportHeight+1;
        if(readOnly) {
            Button(selectionFooter,"CloseReviewCards","关闭",new Vector2(0,-height/2+34+selectionPageFooter),new Vector2(150,44),CloseReviewCards,new Color(.18f,.32f,.49f));
            if(allCards.Length==0)Label(panel,"Empty","暂无卡牌",Vector2.zero,new Vector2(width-48,40),20,Paper);
        } else PresentSelectionFooter(height,kind);
        float footerY=-height/2+34;
        if(pageCount>1)
        {
            var previous=Button(panel,"PreviousPage","‹",new Vector2(-width/2+34,footerY),new Vector2(42,42),()=>{selectionPage--;RenderCardSelection(request,reviewTitle);},Ink);previous.interactable=selectionPage>0;
            Label(panel,"Page",(selectionPage+1)+" / "+pageCount,new Vector2(-width/2+110,footerY),new Vector2(95,36),16,Paper);
            var next=Button(panel,"NextPage","›",new Vector2(width/2-34,footerY),new Vector2(42,42),()=>{selectionPage++;RenderCardSelection(request,reviewTitle);},Ink);next.interactable=selectionPage+1<pageCount;
        }
    }
    private void DisableCardSelectionChoices()
    {
        if(cardSelectionOverlay==null)return;
        foreach(var button in cardSelectionOverlay.GetComponentsInChildren<Button>())
        {
            // Closing, paging and inspecting a browser never sends a rule action.
            // Casting an offered card does, so it must still be locked in flight.
            if(!cardSelectionReadOnly || button.name=="CastComponentCard")button.interactable=false;
        }
    }
    private void RefreshLongSelectionInstruction()
    {
        if(selectionInstructionContent==null)return;
        var scroll=selectionInstructionContent.parent.GetComponent<ScrollRect>();
        float width=scroll.viewport.rect.width,viewportHeight=scroll.viewport.rect.height;
        selectionInstructionText.rectTransform.sizeDelta=new Vector2(width-8,viewportHeight);
        float height=Mathf.Max(viewportHeight,Mathf.Ceil(selectionInstructionText.preferredHeight)+4);
        selectionInstructionText.rectTransform.sizeDelta=new Vector2(width-8,height);
        selectionInstructionContent.sizeDelta=new Vector2(width,height);
        scroll.vertical=height>viewportHeight;
        scroll.viewport.GetComponent<Image>().raycastTarget=scroll.vertical;
        var track=(RectTransform)scroll.viewport.Find("Scrollbar");track.sizeDelta=new Vector2(5,viewportHeight);
        foreach(var graphic in track.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=scroll.vertical;
        selectionInstructionText.raycastTarget=false;
    }
    private static string CounterSelectionCount(ForgeOption[] options) {
        int count=options.Select(o=>o.order).DefaultIfEmpty(0).Max();
        return TapPaymentText($"已选 {count} 个",$"Selected {count}",$"選択済み {count} 個");
    }
    private void PresentSelectionFooter(float height,string kind)
    {
        // Modal card pickers block the battlefield controls. Their target
        // confirmation/reset must stay inside the same modal canvas.
        if(HasTargetChoice&&!IsCardPickerRequest) { Clear(selectionFooter); return; }
        if(IsOptionalHandSelection) {
            Clear(selectionFooter);
            long captured=requestId;int cancel=OptionIndex("cancel");float y=-height*.5f+31+selectionPageFooter;
            var decline=Button(selectionFooter,"SelectionAction_"+cancel,TapPaymentText("取消","Cancel","キャンセル"),new Vector2(78,y),new Vector2(144,38),()=>Choose(cancel,captured),new Color(.18f,.32f,.49f));
            var confirm=Button(selectionFooter,"ConfirmHandChoice",TapPaymentText("下一步","Next","次へ"),new Vector2(-78,y),new Vector2(144,38),()=>CommitHandChoice(captured),new Color(.18f,.32f,.49f));
            decline.interactable=canAct&&!submitted&&cancel>=0&&!view.request.options[cancel].disabled;
            int selected=OptionIndex(stagedHandChoice);confirm.interactable=canAct&&!submitted&&(selected>=0&&!view.request.options[selected].disabled||CanSkipSingleCostDraft);
            foreach(var button in new[]{decline,confirm})PreferenceText.SetSize(button.GetComponentInChildren<Text>(),16);
            return;
        }
        Clear(selectionFooter);
        bool singleDestination=(kind=="Scry"||kind=="Surveil")
            &&(view.request.options??Array.Empty<ForgeOption>()).Where(HasChoiceCard).Select(o=>o.card.id).Distinct().Count()==1;
        var actions=(view.request.options??Array.Empty<ForgeOption>()).Where(o=>
            singleDestination?HasChoiceCard(o):!HasChoiceCard(o)&&!IsCrossZoneTargetHeader(o)).OrderBy(ChoiceActionOrder).ToArray();
        float footerY=-height/2+34+selectionPageFooter;
        float step=Mathf.Min(180,(selectionPanel.rect.width-48)/Math.Max(1,actions.Length));
        for(int i=0;i<actions.Length;i++)
        {
            var option=actions[i];int index=OptionIndex(option.id);long captured=requestId;
            string actionLabel=option.id=="ok" && (kind!="InputConfirm" || view.request.paymentMode=="manual" && view.request.selectionPurpose=="costConfirmation")?TapPaymentText("下一步","Next","次へ"):RequestOptionLabel(option);
            if(IsOptionalCardVote&&option.id=="cancel")actionLabel=TapPaymentText("弃权","Abstain","棄権");
            if(kind=="OrderCards"&&option.id=="confirm-cards")actionLabel=TapPaymentText("确认顺序","Confirm order","順番を確定");
            if(kind=="RevealCards"&&option.id=="confirm-cards")actionLabel=TapPaymentText("确认","Confirm","確認");
            if(option.id=="reset-cards")actionLabel=TapPaymentText("重新选择","Reset selection","選択をリセット");
            if(singleDestination)actionLabel=option.id.StartsWith("scry-top:")?TapPaymentText("置顶","Top","上"):
                kind=="Surveil"?TapPaymentText("入墓","Graveyard","墓地"):TapPaymentText("置底","Bottom","下");
            if(option.id.StartsWith("player:",StringComparison.Ordinal)) {
                var player=view.players.FirstOrDefault(p=>option.id=="player:"+p.targetId);
                if(player!=null)actionLabel=PlayerDisplayName(player.seat);
            }
            if(IsProliferateSelection) {
                if(option.id=="cancel")actionLabel=option.label.ToUpperInvariant() switch {"MINE"=>TapPaymentText("我方永久物","My permanents","自分の永久物"),"ALL"=>TapPaymentText("全选","Select all","すべて選択"),"CLEAR ALL"=>TapPaymentText("清空选择","Clear selection","選択解除"),_=>actionLabel};
            }
            bool selectedPlayer=kind=="InputSelectEntitiesFromList"&&option.id.StartsWith("player:",StringComparison.Ordinal)&&option.selected;
            var button=Button(selectionFooter,"SelectionAction_"+index,actionLabel,new Vector2((i-(actions.Length-1)*.5f)*step,footerY),new Vector2(step-10,44),()=>Choose(index,captured),selectedPlayer?Gold:new Color(.18f,.32f,.49f));
            var actionText=button.GetComponentInChildren<Text>();
            if(option.id.StartsWith("player:",StringComparison.Ordinal))SetNamedPlayerText(actionText,actionLabel);
            PreferenceText.SetSize(actionText,16);actionText.resizeTextForBestFit=false;actionText.horizontalOverflow=HorizontalWrapMode.Overflow;
            button.interactable=canAct&&!submitted&&!option.disabled;
        }
    }
    private static void FitSelectionDestinationButton(Button button)
    {
        var label=button.GetComponentInChildren<Text>();
        label.resizeTextForBestFit=false;
        float height=Mathf.Max(48,Mathf.Ceil(label.preferredHeight)+4);
        var rect=(RectTransform)button.transform;rect.sizeDelta=new Vector2(rect.sizeDelta.x,height);
        label.rectTransform.sizeDelta=new Vector2(label.rectTransform.sizeDelta.x,height-4);
    }
    private void PresentSelectionGlow(RectTransform tile,RectTransform art,bool chosen)
    {
        var existing=tile.Find("SelectedGlow") as RectTransform;
        if(existing==null&&chosen) {
            var glow=Rect(tile,"SelectedGlow",Vector2.zero,art.rect.size).gameObject.AddComponent<ForgePlayableGlow>();
            glow.selected=true;glow.raycastTarget=false;
            existing=glow.rectTransform;
        }
        if(existing!=null) {
            existing.gameObject.SetActive(chosen);
            if(!chosen)return;
            existing.anchorMin=existing.anchorMax=art.anchorMin;
            existing.sizeDelta=art.rect.size;
            // The shared mesh is centred. Stack cards have a top pivot, while
            // picker artwork has a centre pivot; align to the actual face centre.
            existing.anchoredPosition=art.anchoredPosition+art.rect.center;
            existing.SetAsFirstSibling();
        }
    }
    private bool IsCrossZoneTargetHeader(ForgeOption option)
    {
        // Forge's cross-zone target chooser includes informational strings in its
        // option array. Their original indices stay intact, but they are not actions.
        if(view?.request?.kind!="dialog" ||
           !(view.request.options??Array.Empty<ForgeOption>()).Any(HasChoiceCard))return false;
        switch(option.label)
        {
            case "--CARDS ON BATTLEFIELD:--":
            case "--CARDS IN EXILE:--":
            case "--CARDS IN GRAVEYARD:--":
            case "--CARDS IN LIBRARY:--":
            case "--CARDS IN STACK:--":
            case "--CARDS IN ANTE:--":return true;
            default:return false;
        }
    }
}
