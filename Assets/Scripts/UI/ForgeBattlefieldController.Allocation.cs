using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform allocationPanel;
    private long allocationContext;
    private string allocationLayoutKey;
    private Button BindDecisionButton(RectTransform parent,string name,string label,string id,Vector2 position)
    {
        var existing=parent.Find(name);
        var button=existing!=null?existing.GetComponent<Button>():Button(parent,name,label,position,new Vector2(164,38),()=>{},new Color(.16f,.27f,.36f));
        int option=OptionIndex(id);long captured=requestId;
        button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>Choose(option,captured));button.interactable=canAct&&!submitted&&option>=0&&!view.request.options[option].disabled;
        button.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(164,38,8);
        var text=button.GetComponentInChildren<Text>();text.text=label;text.resizeTextForBestFit=false;
        (text.GetComponent<PreferenceText>()??text.gameObject.AddComponent<PreferenceText>()).SetBaseSize(16);
        return button;
    }
    private static void AllocationFont(Text text,int size)
    {
        text.resizeTextForBestFit=false;
        (text.GetComponent<PreferenceText>()??text.gameObject.AddComponent<PreferenceText>()).SetBaseSize(size);
    }
    private string AllocationName(ForgeAllocation value)
    {
        if(value.card?.id>0)return GamePreferences.CardFaceName(value.card.name);
        if(value.targetKind=="player"){
            return PlayerDisplayName(value.playerSeat);
        }
        return GamePreferences.T(value.label);
    }
    private void PresentAllocation()
    {
        if(view?.awaitingInput!=true || view.request.kind!="AssignAmount") {
            if(allocationPanel!=null)allocationPanel.gameObject.SetActive(false);
            // A later session can reuse the same native context number.
            allocationContext=-1;return;
        }
        var values=view.request.allocations??Array.Empty<ForgeAllocation>();
        bool mana=view.request.selectionPurpose=="genericAmount"&&view.request.message=="分配 · mana";
        float width=Mathf.Min(surface.rect.width-96,Mathf.Max(520,values.Length*198+40));
        if(allocationPanel==null)allocationPanel=Panel(surface,"DamageAllocation",Vector2.zero,new Vector2(width,446),Ink);
        string layoutKey=width+"/"+GamePreferences.Revision+"/"+string.Join(",",values.Select(v=>v.id));
        bool sameContext=allocationContext==view.request.contextId;
        var oldScroll=allocationPanel.GetComponentInChildren<ScrollRect>();
        float offset=sameContext&&oldScroll!=null?-oldScroll.content.anchoredPosition.x:0;
        bool rebuild=!sameContext || allocationLayoutKey!=layoutKey || allocationPanel.childCount==0;
        allocationLayoutKey=layoutKey;
        allocationContext=view.request.contextId;allocationPanel.gameObject.SetActive(true);allocationPanel.SetAsLastSibling();
        if(rebuild) {
            // Destroy is deferred; detach before Find/binding creates new controls.
            foreach(var child in allocationPanel.Cast<Transform>().ToArray()){child.gameObject.SetActive(false);child.SetParent(null,false);Destroy(child.gameObject);}
            allocationPanel.sizeDelta=new Vector2(width,446);
            allocationPanel.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(600,446,14);
            AllocationFont(Label(allocationPanel,"Title","",new Vector2(0,198),new Vector2(width-30,48),21,Paper),21);
            AllocationFont(Label(allocationPanel,"Source","",new Vector2(0,157),new Vector2(width-30,38),16,Paper),16);
            AllocationFont(Label(allocationPanel,"Remaining","",new Vector2(0,119),new Vector2(width-30,38),16,Gold),16);
            var content=Scroll(allocationPanel,"Recipients",new Vector2(0,-13),new Vector2(width-28,226));
            var scroll=content.parent.GetComponent<ScrollRect>();scroll.horizontal=true;scroll.vertical=false;
            content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);content.sizeDelta=new Vector2(Mathf.Max(width-28,values.Length*198),226);
            for(int i=0;i<values.Length;i++) {
                var value=values[i];
                var tile=Panel(content,"Recipient_"+value.id,Vector2.zero,new Vector2(182,214),new Color(.07f,.1f,.13f));
                tile.anchorMin=tile.anchorMax=tile.pivot=new Vector2(0,1);tile.anchoredPosition=new Vector2(8+i*198,-3);
                var name=Label(tile,"Name","",new Vector2(0,76),new Vector2(166,54),13,Paper);name.raycastTarget=false;AllocationFont(name,13);
                var image=Rect(tile,"Art",new Vector2(0,7),new Vector2(166,66)).gameObject.AddComponent<RawImage>();
                Sprite sprite=null;
                if(IsSpecializedFace(value.card))TryBattlefieldArt(value.card,out sprite);
                else if(value.card!=null && catalog.TryGetValue(value.card.printingKey??"",out var definition))CardImageLibrary.TryLoadFullCard(definition,value.card.backFace?1:0,out sprite);
                image.texture=sprite?.texture;image.color=sprite!=null?Color.white:Color.clear;image.raycastTarget=false;
                CardArtCrop.Illustration(image,sprite);
                if(mana&&ChoiceManaSymbols(value.label)!=null) {
                    var symbol=CardSymbolText.Create(tile,"ManaSymbol",44,Color.white);
                    IntegratedUiUtility.Place(symbol.rectTransform,new Vector2(0,7),new Vector2(166,66));
                    symbol.alignment=TMPro.TextAlignmentOptions.Center;symbol.text=CardSymbolText.Format(ChoiceManaSymbols(value.label));symbol.raycastTarget=false;
                }
                AllocationFont(Label(tile,"Lethal","",new Vector2(0,-41),new Vector2(166,24),12,Gold),12);
                var amount=Label(tile,"Amount","",new Vector2(0,-79),new Vector2(88,48),24,Paper);amount.fontStyle=FontStyle.Bold;AllocationFont(amount,24);
                AllocationFont(Button(tile,"Minus","−",new Vector2(-63,-79),new Vector2(36,36),()=>{},Ink).GetComponentInChildren<Text>(),18);
                AllocationFont(Button(tile,"Plus","+",new Vector2(63,-79),new Vector2(36,36),()=>{},Ink).GetComponentInChildren<Text>(),18);
            }
            content.anchoredPosition=new Vector2(-Mathf.Clamp(offset,0,Mathf.Max(0,content.rect.width-(width-28))),0);
        }
        allocationPanel.Find("Title").GetComponent<Text>().text=view.request.selectionPurpose=="combatDamage"?TapPaymentText("分配战斗伤害","Assign combat damage","戦闘ダメージの割り振り"):
            view.request.selectionPurpose=="genericAmount"&&view.request.message=="分配 · damage"?TapPaymentText("分配伤害","Assign damage","ダメージを割り振る"):GamePreferences.T(view.request.message);
        if(view.request.selectionPurpose=="genericAmount")switch(view.request.message) {
            case "分配 · mana":allocationPanel.Find("Title").GetComponent<Text>().text=TapPaymentText("分配法术力","Assign mana","マナを割り振る");break;
            case "分配 · counters":allocationPanel.Find("Title").GetComponent<Text>().text=TapPaymentText("分配指示物","Assign counters","カウンターを割り振る");break;
            case "分配 · shield":allocationPanel.Find("Title").GetComponent<Text>().text=TapPaymentText("分配预防伤害","Assign damage prevention","ダメージ軽減を割り振る");break;
        }
        allocationPanel.Find("Source").GetComponent<Text>().text=view.request.sourceCard?.id>0?TapPaymentText("来源：","Source: ","発生源：")+GamePreferences.CardFaceName(view.request.sourceCard.name):"";
        allocationPanel.Find("Remaining").GetComponent<Text>().text=TapPaymentText($"已分配 {view.request.selectedCount} / {view.request.maxCount} · 剩余 {view.request.remainingCount}",$"Assigned {view.request.selectedCount} / {view.request.maxCount} · Remaining {view.request.remainingCount}",$"割り振り {view.request.selectedCount} / {view.request.maxCount} · 残り {view.request.remainingCount}");
        foreach(var value in values) {
            var tile=allocationPanel.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Recipient_"+value.id);
            tile.Find("Name").GetComponent<Text>().text=mana&&ChoiceManaSymbols(value.label)!=null?"":AllocationName(value);
            if(value.targetKind=="player")SetNamedPlayerText(tile.Find("Name").GetComponent<Text>(),AllocationName(value));
            tile.Find("Lethal").GetComponent<Text>().text=value.hasLethal?TapPaymentText($"致命伤害 {value.lethal}",$"Lethal damage {value.lethal}",$"致死ダメージ {value.lethal}"):"";
            tile.Find("Amount").GetComponent<Text>().text=value.amount.ToString();
            foreach(var direction in new[]{"Minus","Plus"}) {
                var button=tile.Find(direction).GetComponent<Button>();int index=OptionIndex((direction=="Plus"?"amount-add:":"amount-sub:")+value.id);long captured=requestId;
                button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>Choose(index,captured));button.interactable=canAct&&!submitted&&index>=0&&!view.request.options[index].disabled;
            }
        }
        bool maySkip=OptionIndex("skip-amount")>=0;
        var confirm=BindDecisionButton(allocationPanel,"Confirm",TapPaymentText("确认分配","Confirm","確定"),"confirm-amount",new Vector2(maySkip?-172:-92,-187));
        var reset=BindDecisionButton(allocationPanel,"Reset",TapPaymentText("重置","Reset","リセット"),"reset-amount",new Vector2(maySkip?0:92,-187));
        var skip=BindDecisionButton(allocationPanel,"Skip",TapPaymentText("稍后分配","Assign later","後で割り振る"),"skip-amount",new Vector2(172,-187));skip.gameObject.SetActive(maySkip);
        // These buttons persist within a draft; update positions when skip becomes unavailable.
        ((RectTransform)confirm.transform).anchoredPosition=new Vector2(maySkip?-172:-92,-187);
        ((RectTransform)reset.transform).anchoredPosition=new Vector2(maySkip?0:92,-187);
    }
}
