using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform noticeToast,noticeHistory;
    private ForgeNoticeScrollRect noticeScroll;
    private Text noticeBody;
    private long noticeLayoutId=long.MinValue;
    private Button noticeHistoryButton;
    private long lastNoticeId;
    private float noticeExpires;
    private readonly Queue<ForgeNotice> pendingNotices=new Queue<ForgeNotice>();
    private ForgeNotice activeNotice;
    private static bool IsRejectedBlockNotice(ForgeNotice notice)=>notice.kind=="notice"&&Regex.IsMatch(notice.value??notice.message??"",@"^(.+) \((\d+)\) cannot be blocked with (\d+) creatures you've assigned$");
    private bool NoticeHistoryAllowed=>view!=null&&!view.inputPending&&(!view.awaitingInput||view.request?.kind=="InputPassPriority");

    private void ResetNotices()
    {
        ResetCardNotices();
        lastNoticeId=0;noticeExpires=0;
        noticeLayoutId=long.MinValue;
        pendingNotices.Clear();activeNotice=null;
        PresentFlipHighlights(null);
        randomResultAwaitingFrame=false;
        if(noticeToast!=null)noticeToast.gameObject.SetActive(false);
        if(noticeHistory!=null)noticeHistory.gameObject.SetActive(false);
        if(noticeHistoryButton!=null)noticeHistoryButton.gameObject.SetActive(false);
        if(randomResultRoot!=null)randomResultRoot.gameObject.SetActive(false);
    }
    private void UpdateNoticeExpiry()
    {
        if(viewingBattlefield){noticeExpires+=Time.unscaledDeltaTime;randomResultStarted+=Time.unscaledDeltaTime;return;}
        if(noticeScroll!=null&&noticeScroll.gameObject.activeInHierarchy&&noticeScroll.Reading){noticeExpires+=Time.unscaledDeltaTime;return;}
        // Begin motion on the first displayed frame, after synchronous card/UI
        // loading. Presenting a snapshot must not spend the animation lifetime.
        if(randomResultAwaitingFrame&&IsRandomResult(activeNotice)) {
            randomResultAwaitingFrame=false;randomResultStarted=Time.unscaledTime;
            noticeExpires=Time.unscaledTime+(activeNotice.kind=="coin"?1.65f:1.85f);
        }
        UpdateRandomResultAnimation();
        if(activeNotice==null||Time.unscaledTime<noticeExpires)return;
        if(pendingNotices.Count>0)ShowResultNotice(pendingNotices.Dequeue());
        else {if(noticeToast!=null)noticeToast.gameObject.SetActive(false);if(randomResultRoot!=null)randomResultRoot.gameObject.SetActive(false);activeNotice=null;PresentFlipHighlights(null);}
    }
    private string RandomChoiceLabel(string label)
    {
        switch((label??"").ToLowerInvariant()) {
            case "heads":return TapPaymentText("正面","Heads","表");
            case "tails":return TapPaymentText("反面","Tails","裏");
            case "win":return TapPaymentText("赢","Win","勝ち");
            case "lose":return TapPaymentText("输","Lose","負け");
            case "increase":return TapPaymentText("增加","Increase","増加");
            case "decrease":return TapPaymentText("减少","Decrease","減少");
            case "power":return TapPaymentText("力量","Power","パワー");
            case "toughness":return TapPaymentText("防御力","Toughness","タフネス");
            case "yes":return TapPaymentText("是","Yes","はい");
            case "no":return TapPaymentText("否","No","いいえ");
            case "white":return TapPaymentText("白色","White","白");
            case "blue":return TapPaymentText("蓝色","Blue","青");
            case "black":return TapPaymentText("黑色","Black","黒");
            case "red":return TapPaymentText("红色","Red","赤");
            case "green":return TapPaymentText("绿色","Green","緑");
            case "colorless":return TapPaymentText("无色","Colorless","無色");
            case "left":return TapPaymentText("左","Left","左");
            case "right":return TapPaymentText("右","Right","右");
            case "land":return TapPaymentText("地","Land","土地");
            case "nonland":return TapPaymentText("非地","Nonland","土地でない");
            default:return NativeDisplayText(label);
        }
    }
    private string RandomChoiceTitle()
    {
        string prompt=view?.request?.message??"";
        if(view?.request?.kind=="InputConfirm" && prompt=="Transform  ?" && (view.stackItems??System.Array.Empty<ForgeStackItem>()).Any(s=>s.source?.name=="Hauntwoods Shrieker"
            && s.description?.StartsWith(s.source.name+" ("+s.source.id+") - Reveal target face-down permanent. If it's a creature card, you may turn it face up.",StringComparison.Ordinal)==true))
            return TapPaymentText("要将这张牌翻正吗？","Turn this card face up?","このカードを表向きにしますか？");
        if(prompt=="one: Choose a basic land type")return TapPaymentText("选择基本地类别","Choose a basic land type","基本土地タイプを選択");
        if(prompt=="one: Choose a land type")return TapPaymentText("选择地类别","Choose a land type","土地タイプを選択");
        if(prompt=="one: Choose a card type")return TapPaymentText("选择牌类别","Choose a card type","カード・タイプを選択");
        if(prompt=="one: Choose a shared type")return TapPaymentText("选择共有的牌类别","Choose a shared card type","共通のカード・タイプを選択");
        if(view?.request?.kind=="InputConfirm"&&prompt=="Choose a direction")return TapPaymentText("选择方向","Choose a direction","方向を選択");
        if(prompt=="oneOrNone: Exile from Stack")return TapPaymentText("选择要放逐的堆叠咒语","Choose a spell on the stack to exile","追放するスタック上の呪文を選択");
        if(prompt=="one: Select type of counters to remove"||prompt=="one: Select type counters to remove")return TapPaymentText("选择要移去的指示物类型","Choose a counter type to remove","取り除くカウンターの種類を選択");
        if(prompt=="one: Select type of counters to add or remove")return TapPaymentText("选择要增加或移除的指示物类型","Choose a counter type to add or remove","置くか取り除くカウンターの種類を選択");
        var counterNumber=Regex.Match(prompt,@"^one: Select the number of (.+) counters to remove$");
        if(counterNumber.Success) {
            string type=CounterChoiceName(counterNumber.Groups[1].Value);
            return TapPaymentText($"选择要移除的{type}指示物数量",$"Choose the number of {type} counters to remove",$"取り除く{type}カウンターの数を選択");
        }
        var movedNumber=Regex.Match(prompt,@"^one: Take how many (.+) counters from (.+)\?$");
        if(movedNumber.Success) {
            string type=CounterChoiceName(movedNumber.Groups[1].Value),source=GamePreferences.CardFaceName(movedNumber.Groups[2].Value);
            return TapPaymentText($"要从{source}移动多少个{type}指示物？",$"Move how many {type} counters from {source}?",$"{source}から{type}カウンターを何個移動しますか？");
        }
        var putNumber=Regex.Match(prompt,@"^one: Put how many (.+) counters on (.+)\?$");
        if(putNumber.Success) {
            string type=CounterChoiceName(putNumber.Groups[1].Value),target=GamePreferences.CardFaceName(putNumber.Groups[2].Value);
            return TapPaymentText($"要在{target}上放置多少个{type}指示物？",$"Put how many {type} counters on {target}?",$"{target}に{type}カウンターを何個置きますか？");
        }
        if(prompt=="one: Choose a result")return TapPaymentText("选择一个骰子结果","Choose a die result","サイコロの出目を選択");
        if(prompt=="one: Choose Phyrexian symbols to pay with life before nonmana payment")
            return TapPaymentText("选择用生命支付的非瑞费用数量（每个支付 2 点生命）","Choose Phyrexian symbols to pay with life (2 life each)","ライフで支払うファイレクシア・シンボルの数（１個につき２点）");
        if(prompt=="oneOrNone: Choose a roll to modify?")return TapPaymentText("选择要修改的骰子","Choose a die to modify","変更するサイコロを選択");
        if(prompt=="oneOrNone: Choose a roll to swap?")return TapPaymentText("选择要交换的骰子","Choose a die to swap","交換するサイコロを選択");
        if(prompt.EndsWith(" - Call coin flip"))return TapPaymentText("猜测硬币的正面或反面","Call heads or tails","表か裏かを選択");
        if(prompt.StartsWith("one: ")&&prompt.EndsWith(" - Choose a result"))return TapPaymentText("选择保留的硬币结果","Choose the coin result to keep","維持するコインの結果を選択");
        var increment=Regex.Match(prompt,@"^Increase or decrease result ([0-9]+) by 1$");
        if(increment.Success)return TapPaymentText($"将骰值 {increment.Groups[1].Value} 增加或减少 1",$"Increase or decrease {increment.Groups[1].Value} by 1",$"出目 {increment.Groups[1].Value} を１増減");
        var swap=Regex.Match(prompt,@"^oneOrNone: Swap result ([0-9]+) with ([0-9]+)/([0-9]+) creature's Power or Toughness\?$");
        if(swap.Success)return TapPaymentText($"将骰值 {swap.Groups[1].Value} 与该生物的力量／防御力 {swap.Groups[2].Value}/{swap.Groups[3].Value} 交换？",$"Swap die {swap.Groups[1].Value} with this creature's power or toughness ({swap.Groups[2].Value}/{swap.Groups[3].Value})?",$"出目 {swap.Groups[1].Value} をクリーチャーのパワーかタフネス（{swap.Groups[2].Value}/{swap.Groups[3].Value}）と交換？");
        return null;
    }
    // One prompt formatter for the value panel and the shared payment dock.
    private string ValueChoicePrompt(out bool namedBid)
    {
        if(view?.request?.selectionPurpose=="forageMethod") {namedBid=false;return TapPaymentText("选择觅食的支付方式","Choose forage payment","給餌の支払い方法を選択");}
        if(view?.request?.message=="Choose forage payment: Food only") {namedBid=false;return TapPaymentText("牺牲一个食品以支付觅食费用。","Sacrifice a Food to pay forage.","給餌のコストとして食物１つを生け贄に捧げます。");}
        string message=RandomChoiceTitle()??ResolutionChoiceInstruction(view.request.message??"",false);
        var searchZone=Regex.Match(message,@"^Search your (library|graveyard|hand)\?$");
        if(searchZone.Success) {
            string zone=searchZone.Groups[1].Value;
            message=TapPaymentText("要搜索你的"+(zone=="library"?"牌库":zone=="graveyard"?"坟墓场":"手牌")+"吗？",message,(zone=="library"?"ライブラリー":zone=="graveyard"?"墓地":"手札")+"を探しますか？");
        }
        if(message=="Search your graveyard and exile?")
            message=TapPaymentText("要从你的坟墓场或放逐区选择一张牌吗？",message,"あなたの墓地や追放領域からカードを選びますか？");
        var alternateSearch=Regex.Match(message,@"^Would you like to search your library with this ability\? If you do, your library will be shuffled\. (\d+) cards match your searching type in Alternate Zones\.$");
        if(alternateSearch.Success) {
            string count=alternateSearch.Groups[1].Value;
            message=TapPaymentText($"要搜索你的牌库吗？若如此作，之后将洗牌。其他可搜索区域有 {count} 张符合条件的牌。",message,$"ライブラリーを探しますか？探したなら、その後切り直します。他の領域には条件に合うカードが{count}枚あります。");
        }
        var cancelSearch=Regex.Match(message,@"^Cancel Search\? Up to (\d+) more card\(s\) can be selected\.$");
        if(cancelSearch.Success)message=TapPaymentText($"结束搜索？还可以选择至多 {cancelSearch.Groups[1].Value} 张牌。",message,$"探すのを終了しますか？あと最大{cancelSearch.Groups[1].Value}枚選べます。");
        var removeCounter=Regex.Match(message,@"^Remove (\d+) \+1/\+1 counter\(s\) from (.+)\?$");
        var exileTop=Regex.Match(message,@"^Exile (\d+) card(?:s)? from the top of your library\?$");
        if(exileTop.Success) {
            string count=exileTop.Groups[1].Value;
            message=TapPaymentText($"从你的牌库顶放逐 {count} 张牌以支付费用？",$"Exile {count} cards from the top of your library to pay this cost?",$"コストを支払うために、あなたのライブラリーの一番上から{count}枚を追放しますか？");
        }
        var millCost=Regex.Match(message,@"^Do you want to mill (\d+) card\(s\)\?(?:\s+\(or:.*\))?$",RegexOptions.Singleline);
        if(millCost.Success) {
            string count=millCost.Groups[1].Value;
            message=TapPaymentText($"磨 {count} 张牌以支付费用？",$"Mill {count} cards to pay this cost?",$"コストを支払うために、カードを{count}枚切削しますか？");
        }
        if(removeCounter.Success) {
            string amount=removeCounter.Groups[1].Value,name=ConfirmationSourceName(removeCounter.Groups[2].Value);
            message=TapPaymentText($"从 {name} 上移去 {amount} 个 +1/+1 指示物？",$"Remove {amount} +1/+1 counter(s) from {name}?",$"{name}から+1/+1カウンターを{amount}個取り除きますか？");
        }
        var entryCounter=Regex.Match(message,@"^Put (\d+) \+1/\+1 counter on (.+)$");
        if(entryCounter.Success) {
            string name=ConfirmationSourceName(entryCounter.Groups[2].Value),amount=entryCounter.Groups[1].Value;
            message=TapPaymentText($"在 {name} 上放置 {amount} 个 +1/+1 指示物？",$"Put {amount} +1/+1 counter(s) on {name}?",$"{name}の上に+1/+1カウンターを{amount}個置きますか？");
        }
        var sacrifice=Regex.Match(message,@"^Sacrifice (.+)\?$");
        if(sacrifice.Success) {
            string name=ConfirmationSourceName(sacrifice.Groups[1].Value);
            message=TapPaymentText($"要牺牲 {name} 吗？",$"Sacrifice {name}?",$"{name}を生け贄に捧げますか？");
        }
        var optionalTrigger=Regex.Match(message,@"^Use triggered ability of (.+) \((\d+)\)\?(?:\r?\nTriggered by: (.+) \((\d+)\))?$");
        if(optionalTrigger.Success&&int.TryParse(optionalTrigger.Groups[2].Value,out int sourceId)) {
            string name=ConfirmationSourceName(optionalTrigger.Groups[1].Value,sourceId);
            message=TapPaymentText($"要使用 {name} 的触发式异能吗？",$"Use triggered ability of {name}?",$"{name}の誘発型能力を使いますか？");
            if(optionalTrigger.Groups[3].Success&&int.TryParse(optionalTrigger.Groups[4].Value,out int triggeringId)) {
                string triggeringName=ConfirmationSourceName(optionalTrigger.Groups[3].Value,triggeringId);
                message+=TapPaymentText($"\n触发来源：{triggeringName}",$"\nTriggered by: {triggeringName}",$"\n誘発元：{triggeringName}");
            }
        }
        var exert=Regex.Match(message,@"^Exert (.+)\?$");
        if(exert.Success) {
            string name=exert.Groups[1].Value;
            var card=(view.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>()).FirstOrDefault(c=>c.name==name);
            if(card!=null&&catalog.TryGetValue(card.printingKey??"",out var definition))name=GamePreferences.CardName(definition);
            message=TapPaymentText($"要耗竭 {name} 吗？",$"Exert {name}?",$"{name}を督励しますか？");
        }
        if(message=="Search your hand?"&&(view.stackItems??Array.Empty<ForgeStackItem>()).Any(s=>s.source?.name=="Growth Spiral"))message=TapPaymentText("要从手牌中放一张地进战场吗？","Put a land from your hand onto the battlefield?","手札の土地を戦場に出しますか？");
        if(message=="Put that card from your library to battlefield?"&&(view.stackItems??Array.Empty<ForgeStackItem>()).Any(s=>s.source?.name=="Planar Genesis"))message=TapPaymentText("将这张地横置放进战场？","Put this land onto the battlefield tapped?","この土地をタップ状態で戦場に出しますか？");
        var resource=Regex.Match(message,@"^Pay ((?:\{E\})+|(?:\{M\})+)\?\s*\(You have (\d+) \{([EM])\}\)$");
        if(resource.Success) {
            int amount=Regex.Matches(resource.Groups[1].Value,@"\{[EM]\}").Count;
            string held=resource.Groups[2].Value;bool shards=resource.Groups[3].Value=="M";
            message=shards?TapPaymentText($"支付 {amount} 个魔法碎片？（现有 {held}）",$"Pay {amount} mana shards? (You have {held})",$"マナの破片を{amount}個支払いますか？（現在{held}個）"):
                TapPaymentText($"支付 {amount} 个能量？（现有 {held}）",$"Pay {amount} energy? (You have {held})",$"エネルギーを{amount}個支払いますか？（現在{held}個）");
        }
        var energy=Regex.Match(message,@"^(?:Pay (\d+) energy|Do you want to spend (\d+) Energy counter)\?$");
        if(energy.Success){string amount=energy.Groups[1].Success?energy.Groups[1].Value:energy.Groups[2].Value;message=TapPaymentText("支付 "+amount+" 个能量？","Pay "+amount+" energy?","エネルギー"+amount+"個を支払いますか？");}
        var life=Regex.Match(message,@"^Do you want to pay (\d+) life\?$");
        if(life.Success)message=TapPaymentText("支付 "+life.Groups[1].Value+" 点生命？","Pay "+life.Groups[1].Value+" life?",life.Groups[1].Value+"点のライフを支払いますか？");
        var bid=Regex.Match(message,@"^Do you want to top bid\? Current Bid =\s*(\d+) Highest Bidder Player (\d+)$");
        if(bid.Success){string bidder=PlayerDisplayName(int.Parse(bid.Groups[2].Value));message=TapPaymentText($"提高生命竞价？当前出价 {bid.Groups[1].Value}，最高出价为{bidder}。",$"Raise the life bid? Current bid: {bid.Groups[1].Value}; highest bidder: {bidder}.",$"ライフの入札額を増やしますか？現在の入札額：{bid.Groups[1].Value}、最高入札者：{bidder}。");}
        if(!bid.Success)message=NativeDisplayText(message);
        namedBid=bid.Success;return message;
    }
    private void PresentValueChoices(ForgeOption[] options,long captured)
    {
        if(options.Length==2 && (view.request.kind=="InputConfirm" || view.request.message=="one: Choose a card type" || view.request.message=="one: Choose a shared type")) {
            bool sharedType=view.request.message=="one: Choose a shared type";
            float compactWidth=Mathf.Min(sharedType?480:400,surface.rect.width-48);
            string message=ValueChoicePrompt(out bool namedBid);
            var measure=new TextGenerator();var settings=new TextGenerationSettings{font=GamePreferences.Font,fontSize=Mathf.RoundToInt(16*GamePreferences.TextScale),fontStyle=FontStyle.Normal,lineSpacing=1,generationExtents=new Vector2(compactWidth-36,0),scaleFactor=1,richText=false,verticalOverflow=VerticalWrapMode.Overflow,horizontalOverflow=HorizontalWrapMode.Wrap};
            float titleHeight=Mathf.Max(32,measure.GetPreferredHeight(message,settings)+4),compactHeight=Mathf.Min(surface.rect.height-48,titleHeight+90);
            landChoiceMenu.sizeDelta=new Vector2(compactWidth,compactHeight);landChoiceMenu.anchoredPosition=Vector2.zero;landChoiceMenu.GetComponent<Image>().color=Ink;
            var heading=Label(landChoiceMenu,"ValueTitle",message,new Vector2(0,compactHeight*.5f-18-titleHeight*.5f),new Vector2(compactWidth-36,titleHeight),16,Paper);heading.supportRichText=false;heading.raycastTarget=false;
            if(namedBid)SetNamedPlayerText(heading,message);
            for(int i=0;i<options.Length;i++) {
                float buttonWidth=sharedType?(compactWidth-48)/2:144,buttonOffset=sharedType?(buttonWidth+12)/2:78;
                int index=OptionIndex(options[i].id);var control=Button(landChoiceMenu,"ValueOption_"+index,RequestOptionLabel(options[i]),new Vector2((i==0?-1:1)*buttonOffset,-compactHeight*.5f+31),new Vector2(buttonWidth,38),()=>Choose(index,captured),new Color(.16f,.29f,.43f));
                PreferenceText.SetSize(control.GetComponentInChildren<Text>(),16);control.interactable=canAct&&!submitted&&!options[i].disabled;
            }
            landChoiceMenu.gameObject.SetActive(true);landChoiceMenu.SetAsLastSibling();return;
        }
        float width=Mathf.Min(640,surface.rect.width-48),height=Mathf.Min(view.request.message=="one: Choose a basic land type"?464:400,surface.rect.height-48);
        landChoiceMenu.sizeDelta=new Vector2(width,height);landChoiceMenu.anchoredPosition=Vector2.zero;landChoiceMenu.GetComponent<Image>().color=Ink;
        var title=Label(landChoiceMenu,"ValueTitle",RandomChoiceTitle()??NativeDisplayText(view.request.message),new Vector2(0,height/2-60),new Vector2(width-40,100),22,Paper);title.supportRichText=false;
        var content=Scroll(landChoiceMenu,"ValueOptions",new Vector2(0,-54),new Vector2(width-30,height-132));
        float offset=0;
        foreach(var option in options) {
            int index=OptionIndex(option.id);string label=RequestOptionLabel(option);
            bool stackExile=view.request.kind=="dialog"&&view.request.message=="oneOrNone: Exile from Stack";
            if(stackExile) {
                if(option.id=="cancel")label=TapPaymentText("取消支付","Cancel payment","支払いをキャンセル");
                else {
                    // Match the native description without deriving choice IDs from
                    // stack order: the server owns the option-to-spell mapping.
                    var matches=(view.stackItems??Array.Empty<ForgeStackItem>()).Where(s=>s.source!=null&&(s.description==option.label||s.copied&&option.label==s.description+" (Copied Spell)")).ToArray();
                    if(matches.Length==1)label=GamePreferences.CardFaceName(matches[0].source.name)+(matches[0].copied?TapPaymentText("（复制品 · 堆叠 "," (Copy · Stack ","（コピー · スタック "):TapPaymentText("（堆叠 "," (Stack ","（スタック "))+matches[0].id+(GamePreferences.Language=="en-US"?")":"）");
                }
            }
            else if(option.id=="cancel"&&view.request.kind=="dialog")label=TapPaymentText("保留原值","Keep unchanged","変更しない");
            var button=Button(content,"ValueOption_"+index,label,Vector2.zero,new Vector2(width-56,58),()=>Choose(index,captured),new Color(.16f,.29f,.43f));
            var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-offset);offset+=66;
            PreferenceText.SetSize(button.GetComponentInChildren<Text>(),22);button.GetComponentInChildren<Text>().supportRichText=false;button.interactable=canAct&&!submitted&&!option.disabled;
        }
        content.sizeDelta=new Vector2(width-30,Mathf.Max(height-132,offset));landChoiceMenu.gameObject.SetActive(true);landChoiceMenu.SetAsLastSibling();
    }
    private string ConfirmationSourceName(string nativeName,int sourceId=0)
    {
        var card=(view.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>(p.battlefield??Array.Empty<ForgeCardView>()).Concat(p.hand??Array.Empty<ForgeCardView>()))
            .Concat((view.stackItems??Array.Empty<ForgeStackItem>()).Select(s=>s.source).Where(c=>c!=null))
            .Concat(view.request?.previewCards??Array.Empty<ForgeCardView>())
            .FirstOrDefault(c=>c.name==nativeName&&(sourceId==0||c.id==sourceId));
        // Keep a changed native name; a printing's translated original name would be wrong.
        if(card!=null&&(string.IsNullOrEmpty(card.originalName)||card.originalName==nativeName)&&catalog.TryGetValue(card.printingKey??"",out var definition))return GamePreferences.CardName(definition);
        return nativeName;
    }
    private string NoticeText(ForgeNotice notice)
    {
        if(notice.kind=="restart") {
            string restartingPlayer=PlayerDisplayName(notice.actorSeat);
            return TapPaymentText($"{restartingPlayer}重新开始了本局，由其先手。",$"{restartingPlayer} restarted this game and takes the first turn.",$"{restartingPlayer}がゲームを再開始し、先手になります。");
        }
        if(notice.kind=="flip"&&!string.IsNullOrEmpty(notice.flipOutcome))return FlipNoticeText(notice);
        if(notice.kind=="cleanup")return CleanupNoticeText(notice);
        if(notice.kind=="match-removal")return TapPaymentText("本场比赛内移除：","Removed for this match: ","このマッチから取り除く：")+string.Join("、",(notice.removedCards??Array.Empty<ForgeCardView>()).Select(c=>GamePreferences.CardFaceName(c.originalName??c.name)));
        if(notice.kind=="ownership") {
            string player=PlayerDisplayName(notice.actorSeat),card=GamePreferences.CardFaceName(notice.sourceCard?.originalName??notice.sourceCard?.name??notice.value);
            return TapPaymentText($"{player}取得「{card}」的拥有权。",$"{player} now owns {card}.",$"{player}が「{card}」の所有権を得ました。");
        }
        string actor=PlayerDisplayName(notice.actorSeat);
        // JsonUtility can construct an empty DTO for an omitted nested field.
        string title=notice.sourceCard?.id>0?GamePreferences.CardFaceName(notice.sourceCard.name):notice.title;
        string value=notice.value??notice.message??"";
        if(notice.kind=="notice") {
            // CostRevealChosen uses the registered native player names in its
            // public result. Resolve those identities only after the reveal.
            var revealed=Regex.Match(value,@"^Player ([1-9][0-9]*) reveals (.+)$");
            if(revealed.Success&&int.TryParse(revealed.Groups[1].Value,out int revealingSeat)&&
               (view?.players??Array.Empty<ForgePlayerView>()).Any(p=>p.seat==revealingSeat)) {
                string who=PlayerDisplayName(revealingSeat),chosen=GamePreferences.NativeText(revealed.Groups[2].Value);
                var chosenPlayer=Regex.Match(revealed.Groups[2].Value,@"^Player ([1-9][0-9]*)$");
                if(chosenPlayer.Success&&int.TryParse(chosenPlayer.Groups[1].Value,out int chosenSeat)&&
                   view.players.Any(p=>p.seat==chosenSeat))chosen=PlayerDisplayName(chosenSeat);
                string result=TapPaymentText($"{who}公开了{chosen}。",$"{who} reveals {chosen}.",$"{who}は{chosen}を公開しました。");
                return (string.IsNullOrEmpty(title)?"":GamePreferences.NativeText(title)+"\n")+result;
            }
            var direction=Regex.Match(value,@"^Left \(clockwise\): (\[[^\r\n]*\])\r?\nRight \(anticlockwise\):(\[[^\r\n]*\])$");
            if(direction.Success)value=TapPaymentText("左（顺时针）：","Left (clockwise): ","左（時計回り）：")+direction.Groups[1].Value+"\n"+
                TapPaymentText("右（逆时针）：","Right (anticlockwise):","右（反時計回り）：")+direction.Groups[2].Value;
            else value=VoteNoticeText(notice,value)??value;
            var bid=Regex.Match(value,@"^topped bid with (\d+) life$");
            if(bid.Success)value=TapPaymentText($"将生命竞价提高至 {bid.Groups[1].Value}。",$"Raised the life bid to {bid.Groups[1].Value}.",$"ライフの入札額を{bid.Groups[1].Value}に増やしました。");
            // Exact pinned CombatUtil formatting plus a currently visible card
            // identity. Unknown/dynamic errors remain native rather than guessed.
            var invalid=Regex.Match(value,@"^(.+) \((\d+)\) cannot be blocked with (\d+) creatures you've assigned$");
            if(invalid.Success&&int.TryParse(invalid.Groups[2].Value,out int cardId)) {
                var card=view?.players?.SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>()).FirstOrDefault(c=>c.id==cardId&&!c.faceDown&&c.name==invalid.Groups[1].Value);
                if(card!=null) {
                    string name=GamePreferences.CardFaceName(card.name),count=invalid.Groups[3].Value;
                    return TapPaymentText($"当前选择的 {count} 个生物不能阻挡{name}。",$"The {count} selected creature(s) cannot block {name}.",$"現在選択している{count}体では{name}をブロックできません。");
                }
            }
        }
        if(notice.kind=="coin")value=TapPaymentText("硬币：","Coin: ","コイン：")+RandomChoiceLabel(value);
        else if(notice.kind=="dice") {
            // Translate only pinned native number formatting. Never compute a result here.
            var lines=value.Replace("\r","").Split('\n');
            for(int i=0;i<lines.Length;i++) {
                var rolled=Regex.Match(lines[i],@"^.+ rolled (\[?[0-9, ]+\]?)$");
                var visit=Regex.Match(lines[i],@"^.+ rolled to visit their Attractions\. Result: ([0-9, ]+)\.$");
                if(visit.Success)lines[i]=TapPaymentText("游乐设施掷骰：","Attraction roll: ","アトラクションの出目：")+visit.Groups[1].Value;
                else if(rolled.Success)lines[i]=TapPaymentText("骰子：","Dice: ","サイコロ：")+rolled.Groups[1].Value;
                else if(lines[i].StartsWith("Ignored rolls: "))lines[i]=TapPaymentText("忽略：","Ignored: ","無視：")+lines[i].Substring(15);
                else if(lines[i].StartsWith("Natural rolls: "))lines[i]=TapPaymentText("原始骰值：","Natural rolls: ","出目：")+lines[i].Substring(15);
            }
            value=string.Join("\n",lines);
        }
        return (notice.actorSeat>0?actor+" · ":"")+(string.IsNullOrEmpty(title)?"":GamePreferences.NativeText(title)+"\n")+GamePreferences.NativeText(value);
    }
    private string CleanupNoticeText(ForgeNotice notice)
    {
        var lines=(notice.value??notice.message??"").Replace("\r","").Split('\n');
        for(int i=0;i<lines.Length;i++) {
            var choice=Regex.Match(lines[i],@"^(.+)'s chosen (card|cards|player): (.+)$");
            var empty=Regex.Match(lines[i],@"^(.+) found no valid choices\.$");
            if(choice.Success) {
                string source=GamePreferences.CardFaceName(choice.Groups[1].Value);
                string value=choice.Groups[3].Value;
                if(choice.Groups[2].Value=="player") {
                    var player=Regex.Match(value,@"^Player ([1-9][0-9]*)$");
                    if(player.Success)value=PlayerDisplayName(int.Parse(player.Groups[1].Value));
                }else value=Regex.Replace(value,@"(.+?) \(([0-9]+)\)(, |$)",m=>
                    GamePreferences.CardFaceName(m.Groups[1].Value)+" ("+m.Groups[2].Value+")"+m.Groups[3].Value);
                lines[i]=string.Format(GamePreferences.T("{0}'s chosen "+choice.Groups[2].Value+": {1}"),source,value);
            }else if(empty.Success)lines[i]=string.Format(GamePreferences.T("{0} found no valid choices."),GamePreferences.CardFaceName(empty.Groups[1].Value));
            else lines[i]=GamePreferences.NativeText(lines[i]);
        }
        // The native public result supplies its identities; user names remain
        // literal, including nicknames that are also interface translation keys.
        return string.Join("\n",lines);
    }
    private void PresentNotices()
    {
        var notes=view?.notices??Array.Empty<ForgeNotice>();
        PresentCardNotices(notes);
        if(noticeHistoryButton==null) {
            noticeHistoryButton=Button(surface,"ResultHistoryButton","≡",Vector2.zero,new Vector2(34,34),ShowNoticeHistory,Ink);
            var rect=(RectTransform)noticeHistoryButton.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.anchoredPosition=new Vector2(128,-48);
        }
        // Keep the results entry hidden until the battlefield history UI is ready.
        noticeHistoryButton.gameObject.SetActive(false);
        noticeHistoryButton.interactable=false;
        if(noticeHistory!=null)noticeHistory.gameObject.SetActive(false);
        if(notes.Length==0)return;
        if(noticeToast==null&&notes.Any(n=>!IsRandomResult(n)&&!IsCardNotice(n))) {
            noticeToast=Panel(surface,"ResultToast",Vector2.zero,new Vector2(660,150),new Color(.035f,.055f,.085f,.98f));
            SetOverlayLayer(noticeToast,NoticeLayerOffset,true);
            noticeToast.anchorMin=noticeToast.anchorMax=noticeToast.pivot=new Vector2(.5f,1);
            noticeToast.anchoredPosition=new Vector2(0,-88);
            noticeToast.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(660,150,14);
            var badge=Panel(noticeToast,"ResultBadge",Vector2.zero,new Vector2(96,96),new Color(.14f,.2f,.28f));
            badge.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(96,96,16);
            var symbol=Label(badge,"ResultValue","",Vector2.zero,new Vector2(88,84),30,Gold);symbol.supportRichText=false;symbol.fontStyle=FontStyle.Bold;
            var viewport=Rect(noticeToast,"ResultBody",Vector2.zero,new Vector2(520,120));
            viewport.gameObject.AddComponent<Image>().color=Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            noticeScroll=viewport.gameObject.AddComponent<ForgeNoticeScrollRect>();
            var content=Rect(viewport,"Content",Vector2.zero,new Vector2(520,120));
            content.anchorMin=content.anchorMax=content.pivot=new Vector2(.5f,1);
            noticeScroll.viewport=viewport;noticeScroll.content=content;noticeScroll.horizontal=false;
            noticeScroll.movementType=ScrollRect.MovementType.Clamped;noticeScroll.inertia=false;
            noticeBody=Label(content,"ResultText","",Vector2.zero,new Vector2(512,120),20,Paper);
            noticeBody.supportRichText=false;
            noticeBody.rectTransform.anchorMin=noticeBody.rectTransform.anchorMax=noticeBody.rectTransform.pivot=new Vector2(.5f,1);
            noticeBody.rectTransform.anchoredPosition=new Vector2(-4,0);
            AddStackScrollIndicator(content);
            var pending=Label(noticeToast,"ResultPending","",Vector2.zero,new Vector2(96,22),13,new Color(.67f,.73f,.8f));pending.supportRichText=false;
            foreach(var graphic in noticeToast.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
            noticeToast.gameObject.SetActive(false);
        }
        // A snapshot may contain several completed flips/rolls. Consume each new
        // native ID once, in order; a later snapshot must not overwrite a result.
        foreach(var notice in notes.Where(n=>n.id>lastNoticeId).OrderBy(n=>n.id)) {
            if(IsCardNotice(notice))continue;
            if(IsRejectedBlockNotice(notice)) {
                // A rejected action must explain itself immediately, even while
                // earlier rolls are being displayed. Resume those results after it.
                if(activeNotice!=null) {
                    var waiting=pendingNotices.ToArray();pendingNotices.Clear();
                    pendingNotices.Enqueue(activeNotice);
                    foreach(var queued in waiting)pendingNotices.Enqueue(queued);
                }
                ShowResultNotice(notice);
            }else pendingNotices.Enqueue(notice);
        }
        lastNoticeId=Math.Max(lastNoticeId,notes.Max(n=>n.id));
        if(activeNotice==null&&pendingNotices.Count>0)ShowResultNotice(pendingNotices.Dequeue());
        else if(activeNotice!=null)LayoutResultNotice(activeNotice);
    }
    private string ResultBadgeText(ForgeNotice notice)
    {
        if(notice.kind=="restart")return TapPaymentText("重开","Restart","再開始");
        if(notice.kind=="flip")return TapPaymentText("翻牌","Flip","フリップ");
        if(notice.kind=="coin")return RandomChoiceLabel(notice.value??notice.message??"");
        if(notice.kind=="dice") {
            var values=DiceResultValues(notice);
            if(values.Length>0) {
                string number=string.Join(", ",values);
                if(number.Length<=5)return number;
            }
            return TapPaymentText("骰子","Dice","サイコロ");
        }
        return IsRejectedBlockNotice(notice)?"!":TapPaymentText("提示","Info","通知");
    }
    private void ShowResultNotice(ForgeNotice notice)
    {
        randomResultAwaitingFrame=false;
        activeNotice=notice;noticeExpires=Time.unscaledTime+(notice.kind=="coin"?1.65f:notice.kind=="dice"?1.85f:4);
        PresentFlipHighlights(notice);
        if(IsRandomResult(notice)) {
            if(noticeToast!=null)noticeToast.gameObject.SetActive(false);
            ShowRandomResult(notice);
        }else {
            if(randomResultRoot!=null)randomResultRoot.gameObject.SetActive(false);
            LayoutResultNotice(notice);noticeToast.gameObject.SetActive(true);
        }
    }
    private void LayoutResultNotice(ForgeNotice notice)
    {
        if(IsRandomResult(notice))return;
        PresentFlipHighlights(notice);
        var body=noticeBody;SetNamedPlayerText(body,NoticeText(notice));
        body.GetComponent<PreferenceText>().Apply();
        float width=Mathf.Min(660,Mathf.Max(180,surface.rect.width-48));
        body.rectTransform.sizeDelta=new Vector2(width-148,120);
        float preferred=Mathf.Ceil(body.preferredHeight)+2,maxHeight=Mathf.Max(60,Mathf.Min(300,surface.rect.height-140));
        float height=Mathf.Clamp(preferred+24,Mathf.Min(150,maxHeight),maxHeight),viewportHeight=height-24;
        // Restart feedback stays below the opponent's hand during the new opening.
        noticeToast.anchoredPosition=new Vector2(0,notice.kind=="restart"?-220:-88);
        noticeToast.sizeDelta=new Vector2(width,height);
        noticeScroll.viewport.sizeDelta=new Vector2(width-140,viewportHeight);
        noticeScroll.viewport.anchoredPosition=new Vector2(54,0);
        float contentHeight=Mathf.Max(preferred,viewportHeight);
        noticeScroll.content.sizeDelta=new Vector2(width-140,contentHeight);
        body.rectTransform.sizeDelta=new Vector2(width-148,contentHeight);
        bool overflow=preferred>viewportHeight;
        body.alignment=overflow?TextAnchor.UpperLeft:TextAnchor.MiddleLeft;
        noticeScroll.SetScrollable(overflow);
        noticeScroll.viewport.GetComponent<Image>().raycastTarget=overflow;
        var track=(RectTransform)noticeScroll.viewport.Find("Scrollbar");track.sizeDelta=new Vector2(5,viewportHeight);
        track.GetComponent<Image>().raycastTarget=overflow;
        track.Find("Handle").GetComponent<Image>().raycastTarget=overflow;
        if(noticeLayoutId!=notice.id) {
            noticeLayoutId=notice.id;noticeScroll.StopMovement();noticeScroll.content.anchoredPosition=Vector2.zero;
        }
        var badge=(RectTransform)noticeToast.Find("ResultBadge");
        badge.anchoredPosition=new Vector2(-width*.5f+62,12);
        var coin=notice.kind=="coin";
        badge.GetComponent<Image>().sprite=coin?IntegratedUiUtility.Circle(96):IntegratedUiUtility.RoundedRect(96,96,16);
        badge.GetComponent<Image>().color=coin?new Color(.27f,.2f,.07f):new Color(.12f,.22f,.34f);
        badge.Find("ResultValue").GetComponent<Text>().text=ResultBadgeText(notice);
        var remaining=noticeToast.Find("ResultPending").GetComponent<Text>();
        remaining.rectTransform.anchoredPosition=new Vector2(-width*.5f+62,-50);
        remaining.text=pendingNotices.Count>0?TapPaymentText("还有 ","Next: ","残り ")+pendingNotices.Count:"";
    }
    private void ShowNoticeHistory()
    {
        if(!NoticeHistoryAllowed)return;
        if(noticeHistory==null) {
            noticeHistory=Panel(surface,"ResultHistory",Vector2.zero,new Vector2(370,surface.rect.height-150),new Color(.035f,.055f,.085f,.98f));
            noticeHistory.anchorMin=noticeHistory.anchorMax=noticeHistory.pivot=new Vector2(0,1);
            noticeHistory.anchoredPosition=new Vector2(20,-90);
        }
        Clear(noticeHistory);noticeHistory.gameObject.SetActive(true);noticeHistory.SetAsLastSibling();
        float height=noticeHistory.rect.height;
        var panel=Panel(noticeHistory,"HistoryPanel",Vector2.zero,new Vector2(370,height),Ink);
        Label(panel,"Title",TapPaymentText("结果记录","Results","結果の履歴"),new Vector2(-35,height*.5f-30),new Vector2(250,38),21,Gold);
        Button(panel,"CloseHistory",TapPaymentText("关闭","Close","閉じる"),new Vector2(133,height*.5f-30),new Vector2(80,36),()=>noticeHistory.gameObject.SetActive(false),Ink);
        var content=Scroll(panel,"ResultsScroll",new Vector2(0,-28),new Vector2(342,height-86));float offset=0;
        foreach(var notice in (view?.notices??Array.Empty<ForgeNotice>()).Reverse()) {
            var text=Label(content,"Notice_"+notice.id,NoticeText(notice),Vector2.zero,new Vector2(320,100),17,Paper);text.supportRichText=false;
            SetNamedPlayerText(text,NoticeText(notice));
            float itemHeight=Mathf.Max(70,text.preferredHeight+24);text.rectTransform.sizeDelta=new Vector2(320,itemHeight);
            text.rectTransform.anchorMin=text.rectTransform.anchorMax=text.rectTransform.pivot=new Vector2(.5f,1);text.rectTransform.anchoredPosition=new Vector2(0,-offset);offset+=itemHeight+8;
        }
        content.sizeDelta=new Vector2(342,Mathf.Max(height-86,offset));
    }
}
