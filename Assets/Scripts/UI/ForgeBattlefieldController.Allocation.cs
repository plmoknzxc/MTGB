using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform allocationPanel;
    private long allocationContext;
    private Button BindDecisionButton(RectTransform parent,string name,string label,string id,Vector2 position)
    {
        var existing=parent.Find(name);
        var button=existing!=null?existing.GetComponent<Button>():Button(parent,name,label,position,new Vector2(164,38),()=>{},new Color(.16f,.27f,.36f));
        int option=OptionIndex(id);long captured=requestId;
        button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>Choose(option,captured));button.interactable=canAct&&!submitted&&option>=0;
        button.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(164,38,8);
        return button;
    }
    private void PresentAllocation()
    {
        if(view?.awaitingInput!=true || view.request.kind!="AssignAmount") {if(allocationPanel!=null)allocationPanel.gameObject.SetActive(false);return;}
        var values=view.request.allocations??Array.Empty<ForgeAllocation>();
        float width=Mathf.Min(surface.rect.width-96,Mathf.Max(520,values.Length*198+40));
        if(allocationPanel==null)allocationPanel=Panel(surface,"DamageAllocation",Vector2.zero,new Vector2(width,406),Ink);
        bool rebuild=allocationContext!=view.request.contextId || allocationPanel.childCount==0;
        allocationContext=view.request.contextId;allocationPanel.gameObject.SetActive(true);allocationPanel.SetAsLastSibling();
        if(rebuild) {
            Clear(allocationPanel);allocationPanel.sizeDelta=new Vector2(width,406);
            allocationPanel.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(600,406,14);
            Label(allocationPanel,"Title",view.request.message,new Vector2(0,170),new Vector2(width-30,32),24,Paper);
            Label(allocationPanel,"Remaining","",new Vector2(0,135),new Vector2(width-30,28),20,Gold);
            var content=Scroll(allocationPanel,"Recipients",new Vector2(0,-3),new Vector2(width-28,226));
            var scroll=content.parent.GetComponent<ScrollRect>();scroll.horizontal=true;scroll.vertical=false;
            content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);content.sizeDelta=new Vector2(Mathf.Max(width-28,values.Length*198),226);
            for(int i=0;i<values.Length;i++) {
                var value=values[i];
                var tile=Panel(content,"Recipient_"+value.id,Vector2.zero,new Vector2(182,214),new Color(.07f,.1f,.13f));
                tile.anchorMin=tile.anchorMax=tile.pivot=new Vector2(0,1);tile.anchoredPosition=new Vector2(8+i*198,-3);
                Label(tile,"Name",value.label,new Vector2(0,86),new Vector2(166,28),17,Paper).raycastTarget=false;
                var image=Rect(tile,"Art",new Vector2(0,17),new Vector2(166,98)).gameObject.AddComponent<RawImage>();
                Sprite sprite=null;
                if(value.card!=null && catalog.TryGetValue(value.card.printingKey??"",out var definition))CardImageLibrary.TryLoadFullCard(definition,value.card.backFace?1:0,out sprite);
                image.texture=sprite?.texture;image.color=sprite!=null?Color.white:Color.clear;image.raycastTarget=false;
                if(sprite!=null){var r=sprite.rect;image.uvRect=new Rect((r.x+r.width*.07f)/sprite.texture.width,(r.y+r.height*.43f)/sprite.texture.height,r.width*.86f/sprite.texture.width,r.height*.38f/sprite.texture.height);}
                Label(tile,"Amount","",new Vector2(0,-65),new Vector2(70,44),30,Paper).fontStyle=FontStyle.Bold;
                Button(tile,"Minus","−",new Vector2(-63,-65),new Vector2(36,36),()=>{},Ink);
                Button(tile,"Plus","+",new Vector2(63,-65),new Vector2(36,36),()=>{},Ink);
            }
        }
        allocationPanel.Find("Remaining").GetComponent<Text>().text=$"已分配 {view.request.selectedCount} / {view.request.maxCount} · 剩余 {view.request.remainingCount}";
        foreach(var value in values) {
            var tile=allocationPanel.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Recipient_"+value.id);
            tile.Find("Amount").GetComponent<Text>().text=value.amount.ToString();
            foreach(var direction in new[]{"Minus","Plus"}) {
                var button=tile.Find(direction).GetComponent<Button>();int index=OptionIndex((direction=="Plus"?"amount-add:":"amount-sub:")+value.id);long captured=requestId;
                button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>Choose(index,captured));button.interactable=canAct&&!submitted&&index>=0;
            }
        }
        BindDecisionButton(allocationPanel,"Confirm","确认分配","confirm-amount",new Vector2(-92,-167));
        BindDecisionButton(allocationPanel,"Reset","重置","reset-amount",new Vector2(92,-167));
        var skip=BindDecisionButton(allocationPanel,"Skip","稍后分配","skip-amount",new Vector2(width/2-98,135));skip.gameObject.SetActive(OptionIndex("skip-amount")>=0);
    }
}
