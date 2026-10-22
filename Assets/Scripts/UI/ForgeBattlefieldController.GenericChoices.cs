using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private string VoteChoiceTitle()
    {
        var request=view?.request;
        if(request==null)return null;
        if(request.votingPlayerSeat<=0)return IsNamedVoteRequest(request)?TapPaymentText("投票","Vote","投票"):null;
        int voter=request.votingPlayerSeat;
        string name=PlayerDisplayName(voter);
        bool controlled=voter!=request.playerSeat;
        if(request.secretVote)return controlled?TapPaymentText($"为{name}秘密投票",$"Secret vote for {name}",$"{name}の秘密投票を選択"):
            TapPaymentText($"秘密投票 · {name}",$"Secret vote · {name}",$"秘密投票 · {name}");
        return controlled?TapPaymentText($"为{name}投票",$"Vote for {name}",$"{name}の投票を選択"):
            TapPaymentText($"投票 · {name}",$"Vote · {name}",$"投票 · {name}");
    }
    // One shared bounded, scrollable selector for colors, modes, dice and other
    // non-card objects. Forge owns the count gate and returns the original objects.
    private void PresentGenericChoices(long captured)
    {
        var request=view.request;
        float width=Mathf.Min(640,surface.rect.width-48),height=Mathf.Min(490,surface.rect.height-48);
        landChoiceMenu.sizeDelta=new Vector2(width,height);
        landChoiceMenu.anchoredPosition=Vector2.zero;
        landChoiceMenu.GetComponent<Image>().color=Ink;
        bool playerVote=request.votingPlayerSeat>0&&request.options.Any(o=>o.id.StartsWith("player:",StringComparison.Ordinal));
        var selections=request.options.Where(o=>o.id.StartsWith("select:",StringComparison.Ordinal)||(playerVote&&o.id.StartsWith("player:",StringComparison.Ordinal))).ToArray();
        bool colors=selections.Length>0&&selections.All(o=>ChoiceManaSymbols(o.label)!=null);
        string heading=VoteChoiceTitle()??(colors?TapPaymentText("选择颜色","Choose colors","色を選択"):
            request.sourceCard!=null&&!string.IsNullOrEmpty(request.sourceCard.name)?TapPaymentText("选择效果","Choose effects","効果を選択"):
            AuxiliaryPrompt(request.message)??System.Text.RegularExpressions.Regex.Replace(request.message??"",@"\s*\(click, then CTRL/CMD \+ click\)",""));
        if(request.message?.StartsWith("Choose rolls to reroll?")==true)heading=TapPaymentText("选择要重掷的骰子；也可以直接确认保留。","Select dice to reroll, or confirm to keep them.","振り直すサイコロを選択。そのまま確定して維持することもできます。");
        var title=Label(landChoiceMenu,"GenericTitle",heading,new Vector2(0,height/2-38),new Vector2(width-44,54),20,Paper);
        title.supportRichText=false;title.resizeTextForBestFit=false;
        if(request.votingPlayerSeat>0)SetNamedPlayerText(title,heading);
        var progress=Label(landChoiceMenu,"GenericProgress",TapPaymentText(
            $"已选 {request.selectedCount} · 需要 {request.minCount}–{request.maxCount}",
            $"Selected {request.selectedCount} · Required {request.minCount}–{request.maxCount}",
            $"選択済み {request.selectedCount} · 必要 {request.minCount}–{request.maxCount}"),
            new Vector2(0,height/2-82),new Vector2(width-44,30),18,Gold);
        progress.resizeTextForBestFit=false;
        float titleHeight=Mathf.Max(54,title.preferredHeight+4);
        title.rectTransform.sizeDelta=new Vector2(width-44,titleHeight);
        title.rectTransform.anchoredPosition=new Vector2(0,height/2-11-titleHeight/2);
        float progressHeight=Mathf.Max(30,progress.preferredHeight+4);
        progress.rectTransform.sizeDelta=new Vector2(width-44,progressHeight);
        progress.rectTransform.anchoredPosition=new Vector2(0,height/2-19-titleHeight-progressHeight/2);
        float optionsTop=progress.rectTransform.anchoredPosition.y-progressHeight/2-12;
        float optionsBottom=-height/2+77;
        float viewportWidth=width-30,viewportHeight=Mathf.Max(60,optionsTop-optionsBottom);
        var clip=Panel(landChoiceMenu,"GenericOptions",new Vector2(0,(optionsTop+optionsBottom)/2),new Vector2(viewportWidth,viewportHeight),Color.clear);
        clip.gameObject.AddComponent<RectMask2D>();var scroll=clip.gameObject.AddComponent<BattlefieldHorizontalScroll>();scroll.hidePartialChildren=true;
        var content=Rect(clip,"Content",Vector2.zero,new Vector2(viewportWidth,viewportHeight));
        content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);scroll.viewport=clip;scroll.content=content;scroll.Configure(viewportWidth);
        var options=selections;
        float tallest=0;var tiles=new System.Collections.Generic.List<RectTransform>();
        foreach(var option in options)
        {
            int index=OptionIndex(option.id);
            string symbols=ChoiceManaSymbols(option.label);
            string source=request.sourceCard?.originalName??request.sourceCard?.name;
            string ownership=request.kind=="ChooseOptions"&&(source=="Bronze Tablet"||source=="Tempest Efreet")?RequestOptionLabel(option):AuxiliaryOptionLabel(option);
            string label=playerVote&&int.TryParse(option.id.Substring(7),out int playerId)?PlayerDisplayName(playerId+1):symbols==null?ownership??VoteOptionLabel(option.label):TapPaymentText(
                option.label switch {"WHITE"=>"白色","BLUE"=>"蓝色","BLACK"=>"黑色","RED"=>"红色","GREEN"=>"绿色",_=>option.label},
                option.label,
                option.label switch {"WHITE"=>"白","BLUE"=>"青","BLACK"=>"黒","RED"=>"赤","GREEN"=>"緑",_=>option.label});
            var textWidth=width-92;
            var measure=new TextGenerationSettings{font=GamePreferences.Font,fontSize=Mathf.RoundToInt(18*GamePreferences.TextScale),
                generationExtents=new Vector2(textWidth,0),scaleFactor=1,richText=false,lineSpacing=1,
                verticalOverflow=VerticalWrapMode.Overflow,horizontalOverflow=HorizontalWrapMode.Wrap};
            if(symbols==null&&!playerVote) {
                string prefix=request.sourceCard?.name+" — ";
                label=!string.IsNullOrEmpty(request.sourceCard?.name)&&label.StartsWith(prefix,StringComparison.Ordinal)
                    ?GamePreferences.CardFaceName(request.sourceCard.name)+" — "+NamedAbilityEffectText(request.sourceCard,label.Substring(prefix.Length))
                    :GamePreferences.NativeText(label);
            }
            string buttonLabel=(option.selected?"●  ":"○  ")+label;
            float requiredHeight=Mathf.Max(48,new TextGenerator().GetPreferredHeight(buttonLabel,measure)+18);
            float rowHeight=Mathf.Min(viewportHeight,requiredHeight);
            var button=Button(content,"GenericOption_"+index,buttonLabel,
                Vector2.zero,new Vector2(width-58,rowHeight),()=>Choose(index,captured),option.selected?new Color(.18f,.38f,.55f):new Color(.13f,.19f,.25f));
            var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);tiles.Add(rect);tallest=Mathf.Max(tallest,rowHeight);
            var text=button.GetComponentInChildren<Text>();PreferenceText.SetSize(text,18);text.resizeTextForBestFit=false;text.supportRichText=false;
            if(playerVote)SetNamedPlayerText(text,buttonLabel);
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;
            if(requiredHeight>rowHeight) {
                // Keep the action and panel bounds fixed; only the long label scrolls.
                var labelContent=Scroll(rect,"LongOption",Vector2.zero,new Vector2(textWidth,rowHeight-16));
                text.rectTransform.SetParent(labelContent,false);
                text.rectTransform.anchorMin=text.rectTransform.anchorMax=text.rectTransform.pivot=new Vector2(.5f,1);
                text.rectTransform.anchoredPosition=Vector2.zero;
                text.rectTransform.sizeDelta=new Vector2(textWidth,requiredHeight-18);
                text.alignment=TextAnchor.UpperLeft;
                labelContent.sizeDelta=new Vector2(textWidth,requiredHeight-18);
                AddStackScrollIndicator(labelContent);
            }
            button.interactable=canAct&&!submitted&&!option.disabled&&(option.selected||request.selectedCount<request.maxCount);
        }
        int visibleRows=tallest+8<=viewportHeight/2?2:1;
        content.sizeDelta=new Vector2(Mathf.Max(viewportWidth,Mathf.CeilToInt(tiles.Count/(float)visibleRows)*viewportWidth),Mathf.Max(viewportHeight,tallest));
        clip.sizeDelta=new Vector2(viewportWidth,content.sizeDelta.y);
        for(int i=0;i<tiles.Count;i++)tiles[i].anchoredPosition=new Vector2(i/visibleRows*viewportWidth+14,-i%visibleRows*(tallest+8));
        content.anchoredPosition=new Vector2(-choiceScrollOffset,0);scroll.Configure(viewportWidth);
        if(request.paymentMode=="manual") {
            var actions=request.options.Where(o=>o.id is "ok" or "confirm-cards" or "reset-cards" or "cancel").OrderBy(ChoiceActionOrder).ToArray();
            for(int i=0;i<actions.Length;i++) {
                var option=actions[i];int index=OptionIndex(option.id);
                var button=Button(landChoiceMenu,"CostChoiceAction_"+index,ChoiceActionLabel(option),new Vector2((i-(actions.Length-1)*.5f)*166,-height/2+35),new Vector2(156,44),()=>Choose(index,captured),new Color(.18f,.32f,.49f));
                button.interactable=canAct&&!submitted&&!option.disabled;
            }
            landChoiceMenu.gameObject.SetActive(true);landChoiceMenu.SetAsLastSibling();return;
        }
        foreach(var item in new[]{(playerVote?"ok":"confirm-cards","GenericConfirm",TapPaymentText("确认","Confirm","確定"),80f),
                                   ("reset-cards","GenericReset",TapPaymentText("重新选择","Reset","選び直す"),-80f)})
        {
            int index=OptionIndex(item.Item1);
            var button=Button(landChoiceMenu,item.Item2,item.Item3,new Vector2(item.Item4,-height/2+35),new Vector2(145,44),()=>Choose(index,captured),new Color(.18f,.32f,.49f));
            button.interactable=index>=0&&canAct&&!submitted&&!request.options[index].disabled;
        }
        landChoiceMenu.gameObject.SetActive(true);landChoiceMenu.SetAsLastSibling();
    }
}
