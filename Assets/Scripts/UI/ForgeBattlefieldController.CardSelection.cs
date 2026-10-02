using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform cardSelectionOverlay;
    private int selectionPage;
    private float selectionPageFooter;
    private string selectionLayoutKey;
    private RectTransform selectionPanel, selectionFooter;
    private string selectionKind;
    private string SelectionProgress => view.request.selectionPurpose=="collectEvidence"
        ? $"法术力值 {view.request.selectedValue} / {view.request.requiredValue}"
        : view.request.maxCount>0?$"已选 {view.request.selectedCount} · 需要 {view.request.minCount}–{view.request.maxCount} 张":"";
    private string SelectionInstruction => view.request.selectionPurpose=="collectEvidence"
        ? $"从墓地选择要放逐的牌，法术力值总和至少为 {view.request.requiredValue}。"
        : view.request.kind=="Surveil"?"选择置顶或入墓；同一区域按选择顺序排列。"
        : view.request.kind=="Scry"?"选择置顶或置底；同一区域先选的牌在上方。":view.request.message;
    // Unity's JsonUtility may materialize an empty nested object for an omitted card.
    private static bool HasChoiceCard(ForgeOption option)=>option.card!=null && !string.IsNullOrEmpty(option.card.name);
    private bool IsCardPickerRequest => view?.awaitingInput==true && view.request!=null &&
        ((view.request.previewCards?.Length??0)>0 ||
         (view.request.options??Array.Empty<ForgeOption>()).Any(o=>HasChoiceCard(o) &&
            (view.request.kind!="InputSelectTargets" || !RepresentedOnBoard(o.id))));

    private void PresentCardSelection()
    {
        if(IsCardPickerRequest && browsedZone!=null)CloseReviewCards();
        if(!IsCardPickerRequest && browsedZone!=null){RefreshZoneBrowser();return;}
        if(!IsCardPickerRequest){if(cardSelectionOverlay!=null)cardSelectionOverlay.gameObject.SetActive(false);selectionLayoutKey=null;return;}
        RenderCardSelection(view.request);
    }

    // Shared presentation for engine choices and read-only post-game zone browsing.
    // The latter never changes the authoritative view/request or submits a choice.
    private void RenderCardSelection(ForgeRequest request, string reviewTitle = null)
    {
        bool readOnly = reviewTitle != null;
        var allCards=request.options.Where(HasChoiceCard).Select(o=>o.card)
            .Concat(request.previewCards??Array.Empty<ForgeCardView>()).GroupBy(c=>c.id).Select(g=>g.First()).ToArray();
        bool mixedOwners=allCards.Where(c=>c.ownerSeat>0).Select(c=>c.ownerSeat).Distinct().Count()>1;
        string key=request.kind+"/"+selectionPage+"/"+surface.rect.size+"/"+request.sourceCardId+"/"+
            string.Join("|",allCards.OrderBy(c=>c.id).Select(c=>c.id+":"+c.printingKey+":"+c.backFace+":"+c.ownerSeat+":"+c.zone));
        if(!readOnly && key==selectionLayoutKey && selectionPanel!=null) {
            cardSelectionOverlay.gameObject.SetActive(true);
            selectionPanel.Find("Count").GetComponent<Text>().text=SelectionProgress;
            selectionPanel.Find("Instruction").GetComponent<Text>().text=SelectionInstruction;
            foreach(var card in allCards) {
                var options=request.options.Where(o=>HasChoiceCard(o)&&o.card.id==card.id).ToArray();
                var tile=selectionPanel.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t=>t.name=="ChoiceCard_"+card.id);
                if(tile==null)continue;
                bool chosen=options.Any(o=>o.selected)||!string.IsNullOrEmpty(card.selection)&&card.selection!="不可选";
                tile.GetComponent<Image>().color=chosen?new Color(.7f,.51f,.16f):options.Length>0?new Color(.18f,.45f,.58f):new Color(.12f,.16f,.21f);
                var destination=tile.Find("Destination")?.GetComponent<Text>();if(destination!=null)destination.text=card.selection??"";
                var order=tile.Find("Artwork/Order")?.GetComponent<Text>();if(order!=null)order.text=chosen&&options.Length>0?options[0].order.ToString():"";
                if(request.kind=="Scry"||request.kind=="Surveil") {
                    float cellWidth=tile.rect.width+8,existingArtHeight=tile.Find("Artwork").GetComponent<RectTransform>().rect.height;
                    for(int j=0;j<options.Length;j++)if(tile.Find("Select_"+options[j].id)==null) {
                        var option=options[j];float buttonWidth=(cellWidth-24)/2;
                        string label=option.id.StartsWith("scry-top:")?"置顶":request.kind=="Surveil"?"入墓":"置底";
                        var added=Button(tile,"Select_"+option.id,label,new Vector2((j-.5f)*buttonWidth,-existingArtHeight-72),new Vector2(buttonWidth-4,48),()=>{},new Color(.18f,.32f,.49f));
                        ((RectTransform)added.transform).anchorMin=((RectTransform)added.transform).anchorMax=new Vector2(.5f,1);added.GetComponentInChildren<Text>().fontSize=16;
                    }
                }
                foreach(var control in tile.GetComponentsInChildren<Button>(true).Where(b=>b.name.StartsWith("Select_"))) {
                    var option=options.FirstOrDefault(o=>"Select_"+o.id==control.name);
                    control.gameObject.SetActive(option!=null);control.onClick.RemoveAllListeners();
                    if(option!=null){int index=OptionIndex(option.id);long captured=requestId;control.onClick.AddListener(()=>Choose(index,captured));control.interactable=canAct&&!submitted;}
                }
                var face=tile.Find("Artwork").GetComponent<Button>();
                if(face==null)continue;
                face.onClick.RemoveAllListeners();face.interactable=options.Length==1&&canAct&&!submitted;
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
        cardSelectionOverlay.gameObject.SetActive(true);cardSelectionOverlay.SetAsLastSibling();
        var groups=(request.options??Array.Empty<ForgeOption>()).Where(HasChoiceCard).GroupBy(o=>o.card.id)
            .Select(g=>new {card=g.First().card,options=g.ToArray()}).ToList();
        groups.AddRange((request.previewCards??Array.Empty<ForgeCardView>()).Where(c=>!groups.Any(g=>g.card.id==c.id)).Select(c=>new {card=c,options=Array.Empty<ForgeOption>()}));
        if(groups.Any(g=>g.card.selection=="不可选"))groups=groups.OrderBy(g=>g.card.selection=="不可选")
            .ThenBy(g=>g.card.name,StringComparer.OrdinalIgnoreCase).ThenBy(g=>g.card.id).ToList();
        const int pageSize=10;
        int pageCount=Math.Max(1,(groups.Count+pageSize-1)/pageSize);selectionPage=Mathf.Clamp(selectionPage,0,pageCount-1);
        selectionPageFooter=pageCount>1?44:0;
        var visible=groups.Skip(selectionPage*pageSize).Take(pageSize).ToArray();
        string kind=request.kind;
        bool destinationChoices=groups.Any(g=>g.options.Length>1);
        bool showDestination=kind=="Scry" || kind=="Surveil";
        float width=Mathf.Min(Mathf.Max(360,Math.Min(5,visible.Length)*200+56),surface.rect.width-36);
        int columns=Mathf.Clamp(Mathf.FloorToInt((width-48)/200),1,Math.Max(1,Math.Min(5,visible.Length)));
        float cell=Mathf.Min(200,(width-56)/columns),artWidth=cell-20,artHeight=artWidth*1.4f;
        float rowHeight=artHeight+(destinationChoices||showDestination?116:28);
        int rows=Mathf.Max(1,Mathf.CeilToInt(visible.Length/(float)columns));
        float height=Mathf.Min(186+selectionPageFooter+16+rows*rowHeight,surface.rect.height-36);
        var panel=Panel(cardSelectionOverlay,"CardSelection",Vector2.zero,new Vector2(width,height),Ink);
        selectionPanel=panel;
        selectionFooter=Rect(panel,"SelectionFooter",Vector2.zero,Vector2.zero);
        string title=request.selectionPurpose=="collectEvidence"?"搜证":kind=="Scry"?"占卜":kind=="Surveil"?"刺探":kind=="OrderCards"?"排列卡牌顺序":kind=="RevealCards"?"查看卡牌":kind=="InputConfirm"?"确认效果":"选择卡牌";
        Label(panel,"Title",reviewTitle??title,new Vector2(0,height/2-28),new Vector2(width-40,38),25,Paper).alignment=TextAnchor.MiddleLeft;
        float countWidth=Mathf.Max(130,width-210);
        Label(panel,"Count",readOnly?$"{allCards.Length} 张":SelectionProgress,new Vector2(width/2-countWidth/2-20,height/2-28),new Vector2(countWidth,38),16,Paper).alignment=TextAnchor.MiddleRight;
        var prompt=Label(panel,"Instruction",readOnly?"点击卡牌查看详情":SelectionInstruction,new Vector2(0,height/2-79),new Vector2(width-48,58),17,Paper);
        prompt.supportRichText=false;prompt.alignment=TextAnchor.UpperLeft;
        prompt.verticalOverflow=VerticalWrapMode.Truncate;
        prompt.resizeTextForBestFit=true;prompt.resizeTextMinSize=12;prompt.resizeTextMaxSize=17;
        var content=Scroll(panel,"CardSelectionScroll",new Vector2(0,-20+selectionPageFooter/2),new Vector2(width-36,height-186-selectionPageFooter));
        AddStackScrollIndicator(content);
        float gridLeft=Mathf.Max(8,(width-44-columns*cell)/2);
        for(int i=0;i<visible.Length;i++)
        {
            var item=visible[i];var card=item.card;
            var tile=Panel(content,"ChoiceCard_"+card.id,Vector2.zero,new Vector2(cell-8,rowHeight-10),new Color(.12f,.16f,.21f));
            tile.anchorMin=tile.anchorMax=tile.pivot=new Vector2(0,1);
            tile.anchoredPosition=new Vector2(gridLeft+(i%columns)*cell,-8-(i/columns)*rowHeight);
            bool unavailable=card.selection=="不可选";
            bool chosen=item.options.Any(o=>o.selected)||!string.IsNullOrEmpty(card.selection)&&!unavailable;
            bool selectable=item.options.Length>0;
            tile.GetComponent<Image>().color=chosen?new Color(.7f,.51f,.16f):selectable?new Color(.18f,.45f,.58f):new Color(.12f,.16f,.21f);
            var art=Panel(tile,"Artwork",new Vector2(0,-artHeight/2-8),new Vector2(artWidth,artHeight),Ink);
            art.anchorMin=art.anchorMax=new Vector2(.5f,1);
            Sprite sprite=null;
            TryBattlefieldArt(card,out sprite);
            var image=art.GetComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.color=sprite==null?Ink:Color.white;
            if(unavailable)image.color=new Color(.38f,.38f,.38f);
            var name=Label(art,"CardName",card.faceDown?"牌背":card.name,Vector2.zero,new Vector2(artWidth-12,artHeight-12),18,Paper);
            name.raycastTarget=false;name.supportRichText=false;name.gameObject.SetActive(sprite==null);
            if(!card.faceDown && card.planeswalker && card.zone=="Battlefield") {
                var badge=Panel(art,"CurrentLoyalty",new Vector2(0,-artHeight/2+16),new Vector2(artWidth,32),Ink);
                badge.GetComponent<Image>().raycastTarget=false;
                var value=Label(badge,"Value","忠诚 "+card.loyalty,Vector2.zero,new Vector2(artWidth-8,30),20,Paper);
                value.fontStyle=FontStyle.Bold;value.raycastTarget=false;
            }
            if(mixedOwners) {
                var owner=Panel(art,"Owner",new Vector2(0,artHeight/2-11),new Vector2(artWidth,22),new Color(.035f,.055f,.075f,.94f));
                var ownerText=Label(owner,"OwnerLabel",card.ownerSeat==seat?"你的牌":"对手的牌",Vector2.zero,new Vector2(artWidth-8,22),13,Paper);ownerText.raycastTarget=false;owner.GetComponent<Image>().raycastTarget=false;
            }
            if(readOnly) {
                var face=art.gameObject.AddComponent<Button>();face.targetGraphic=image;face.transition=Selectable.Transition.None;
                face.onClick.AddListener(()=>ShowReviewDetail(card));
            }
            float y=-artHeight-28;
            if(showDestination)
            {
                var caption=Label(tile,"Destination",card.selection??"",new Vector2(0,y),new Vector2(cell-20,32),16,Paper);
                caption.rectTransform.anchorMin=caption.rectTransform.anchorMax=new Vector2(.5f,1);caption.supportRichText=false;
            }
            if(kind=="OrderCards")
            {
                var order=Label(art,"Order",chosen&&item.options.Length>0?item.options[0].order.ToString():"",new Vector2(artWidth/2-20,artHeight/2-20),new Vector2(36,36),24,Color.yellow);
                order.raycastTarget=false;
            }
            for(int j=0;j<item.options.Length;j++)
            {
                var option=item.options[j];int index=OptionIndex(option.id);long captured=requestId;
                if(item.options.Length==1)
                {
                    var face=art.gameObject.AddComponent<Button>();face.targetGraphic=image;face.transition=Selectable.Transition.None;
                    face.onClick.AddListener(()=>Choose(index,captured));face.interactable=canAct&&!submitted;
                    continue;
                }
                string text=option.id.StartsWith("card:")?(option.selected?"取消选择":"选择"):option.label;
                if(option.id.StartsWith("scry-top:"))text="置顶";
                if(option.id.StartsWith("scry-bottom:"))text=kind=="Surveil"?"入墓":"置底";
                float buttonWidth=(cell-24)/item.options.Length;
                var button=Button(tile,"Select_"+option.id,text,new Vector2((j-(item.options.Length-1)*.5f)*buttonWidth,y-44),new Vector2(buttonWidth-4,48),()=>Choose(index,captured),new Color(.18f,.32f,.49f));
                ((RectTransform)button.transform).anchorMin=((RectTransform)button.transform).anchorMax=new Vector2(.5f,1);
                button.GetComponentInChildren<Text>().fontSize=16;button.interactable=canAct&&!submitted;
            }
        }
        content.sizeDelta=new Vector2(width-44,Math.Max(height-186-selectionPageFooter,16+Mathf.CeilToInt(visible.Length/(float)columns)*rowHeight));
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
    private void PresentSelectionFooter(float height,string kind)
    {
        Clear(selectionFooter);
        var actions=(view.request.options??Array.Empty<ForgeOption>()).Where(o=>!HasChoiceCard(o)).ToArray();
        float footerY=-height/2+34+selectionPageFooter;
        float step=Mathf.Min(180,(selectionPanel.rect.width-48)/Math.Max(1,actions.Length));
        for(int i=0;i<actions.Length;i++)
        {
            var option=actions[i];int index=OptionIndex(option.id);long captured=requestId;
            string actionLabel=option.id=="ok" && kind!="InputConfirm"?"确认":RequestOptionLabel(option);
            var button=Button(selectionFooter,"SelectionAction_"+index,actionLabel,new Vector2((i-(actions.Length-1)*.5f)*step,footerY),new Vector2(step-10,44),()=>Choose(index,captured),new Color(.18f,.32f,.49f));
            var actionText=button.GetComponentInChildren<Text>();
            actionText.fontSize=16;actionText.resizeTextForBestFit=false;actionText.horizontalOverflow=HorizontalWrapMode.Overflow;
            button.interactable=canAct&&!submitted;
        }
    }
}
