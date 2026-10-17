using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    // Card ordering and other string lists retain their own consumers. These
    // are native CostPart descriptions, never client-side payment decisions.
    private bool IsCostOrderRequest => view?.awaitingInput==true && view.request.kind=="OrderCards" &&
        view.request.message=="Order costs\nPay first" &&
        !(view.request.options??Array.Empty<ForgeOption>()).Any(HasChoiceCard);

    private string CostOrderLabel(string label)
    {
        var exile=System.Text.RegularExpressions.Regex.Match(label??"",@"^Exile (a|one|two|three|four|\d+) (?:(?:Card\.)?(white|blue|black|red|green|colorless) )?cards? from your hand$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if(exile.Success) {
            string count=exile.Groups[1].Value.ToLowerInvariant() switch {"a" or "one"=>"1","two"=>"2","three"=>"3","four"=>"4",_=>exile.Groups[1].Value};
            string color=exile.Groups[2].Value.ToLowerInvariant();
            string zh=color switch {"white"=>"白色","blue"=>"蓝色","black"=>"黑色","red"=>"红色","green"=>"绿色","colorless"=>"无色",_=>""};
            string ja=color switch {"white"=>"白の","blue"=>"青の","black"=>"黒の","red"=>"赤の","green"=>"緑の","colorless"=>"無色の",_=>""};
            return TapPaymentText("从手牌中放逐 "+count+" 张"+zh+"牌",label,"手札から"+ja+"カード"+count+"枚を追放する");
        }
        return label switch {
        "Choose a color" => TapPaymentText("选择一种颜色","Choose a color","色を１色選ぶ"),
        "Discard a card of the chosen color or its associated basic land type card" => TapPaymentText("弃掉一张所选颜色的牌，或一张对应基本地类别的牌","Discard a card of the chosen color or associated basic land type","選んだ色のカードか対応する基本土地タイプのカード１枚を捨てる"),
        "Exile two Card.Green cards from your hand" => TapPaymentText("从手牌中放逐两张绿色牌","Exile two green cards from your hand","手札から緑のカード２枚を追放する"),
        "Sacrifice two Mountains" => TapPaymentText("牺牲两座山脉","Sacrifice two Mountains","山２つを生け贄に捧げる"),
        "Return two Islands you control to their owner's hand" => TapPaymentText("将你操控的两座海岛移回其拥有者手上","Return two Islands you control to their owner's hand","あなたがコントロールする島２つをオーナーの手札に戻す"),
        _ => label // Unknown/custom CostParts preserve their full native text.
        };
    }

    private void PresentCostOrderChoices()
    {
        if(landChoiceMenu==null)landChoiceMenu=Panel(surface,"CardAbilityChoices",Vector2.zero,Vector2.zero,Ink);
        landChoiceMenu.SetParent(surface,false);
        SetOverlayLayer(landChoiceMenu,DecisionLayerOffset,true);
        landChoiceMenu.anchorMin=landChoiceMenu.anchorMax=landChoiceMenu.pivot=new Vector2(.5f,.5f);
        float width=Mathf.Min(620,surface.rect.width-48),height=Mathf.Min(430,surface.rect.height-48);
        landChoiceMenu.sizeDelta=new Vector2(width,height);landChoiceMenu.anchoredPosition=Vector2.zero;
        landChoiceMenu.GetComponent<Image>().color=Ink;
        landChoiceMenu.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(620,430,14);
        var request=view.request;long captured=requestId;
        Label(landChoiceMenu,"CostOrderTitle",TapPaymentText("排列费用支付顺序","Order cost payments","コストの支払い順を選択"),new Vector2(0,height/2-34),new Vector2(width-40,42),20,Paper);
        Label(landChoiceMenu,"CostOrderProgress",TapPaymentText(
            $"先支付所选的第 1 项 · 已排列 {request.selectedCount}/{request.maxCount}",
            $"Pay item 1 first · Ordered {request.selectedCount}/{request.maxCount}",
            $"１番から支払う・選択済み {request.selectedCount}/{request.maxCount}"),
            new Vector2(0,height/2-76),new Vector2(width-40,36),16,Gold);
        var content=Scroll(landChoiceMenu,"CostOrderOptions",new Vector2(0,-14),new Vector2(width-30,height-174));
        float y=0;
        foreach(var option in request.options.Where(o=>o.id.StartsWith("select:",StringComparison.Ordinal))) {
            int index=OptionIndex(option.id);
            var button=Button(content,"CostOrderOption_"+index,"",Vector2.zero,new Vector2(width-54,48),()=>Choose(index,captured),option.selected?new Color(.18f,.38f,.55f):new Color(.13f,.19f,.25f));
            var text=CardSymbolText.Create(button.transform,"CostText",18,Paper);
            text.alignment=TMPro.TextAlignmentOptions.MidlineLeft;text.richText=false;text.enableAutoSizing=false;
            text.text=CardSymbolText.Format(CostOrderLabel(option.label));
            float rowHeight=Mathf.Max(52,Mathf.Ceil(text.GetPreferredValues(text.text,width-126,10000).y)+24);
            var rect=(RectTransform)button.transform;rect.sizeDelta=new Vector2(width-54,rowHeight);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-y);
            IntegratedUiUtility.Place(text.rectTransform,new Vector2(24,0),new Vector2(width-126,rowHeight-16));
            Label(rect,"PaymentOrder",option.selected?option.order.ToString():"—",new Vector2(-(width-54)/2+26,0),new Vector2(42,rowHeight-8),18,Gold);
            button.interactable=canAct&&!submitted&&!option.disabled;y+=rowHeight+8;
        }
        content.sizeDelta=new Vector2(width-40,Mathf.Max(height-174,y));
        foreach(var item in new[]{("reset-cards","CostOrderReset",TapPaymentText("重新排列","Reset order","選び直す"),90f),
                                   ("confirm-cards","CostOrderConfirm",TapPaymentText("下一步","Next","次へ"),-90f)}) {
            int index=OptionIndex(item.Item1);
            var button=Button(landChoiceMenu,item.Item2,item.Item3,new Vector2(item.Item4,-height/2+35),new Vector2(166,46),()=>Choose(index,captured),new Color(.18f,.32f,.49f));
            button.interactable=index>=0&&canAct&&!submitted&&!request.options[index].disabled;
        }
        landChoiceMenu.gameObject.SetActive(true);landChoiceMenu.SetAsLastSibling();
    }
}
