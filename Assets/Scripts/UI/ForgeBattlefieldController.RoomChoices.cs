using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    // Both native states belong to the same card. Keep their option ordinals
    // separate and show the actual printed half rather than grouping by card ID.
    private void PresentRoomDoorChoices(ForgeOption[] options,long captured)
    {
        float width=Mathf.Min(surface.rect.width-64,options.Length*224+28);
        float height=Mathf.Min(surface.rect.height-80,410);
        landChoiceMenu.sizeDelta=new Vector2(width,height);
        landChoiceMenu.anchoredPosition=Vector2.zero;
        landChoiceMenu.GetComponent<Image>().color=Ink;
        string prompt=view.request.message switch {
            "one: Choose Room to lock"=>TapPaymentText("选择要锁定的门","Choose a door to lock","閉じるドアを選択"),
            "one: Choose Room to unlock"=>TapPaymentText("选择要解锁的门","Choose a door to unlock","開放するドアを選択"),
            _=>TapPaymentText("选择要锁定或解锁的门","Choose a door to lock or unlock","閉じるか開放するドアを選択")};
        var title=Label(landChoiceMenu,"RoomDoorTitle",prompt,new Vector2(0,height/2-24),new Vector2(width-28,36),20,Paper);
        title.resizeTextForBestFit=false;title.raycastTarget=false;
        float header=Mathf.Max(42,title.preferredHeight+12);
        title.rectTransform.sizeDelta=new Vector2(width-28,header-8);
        title.rectTransform.anchoredPosition=new Vector2(0,height/2-header/2);
        float tileHeight=height-header-22,cellWidth=(width-28)/options.Length;
        for(int i=0;i<options.Length;i++) {
            var option=options[i];var card=option.card;int index=OptionIndex(option.id);
            var tile=Panel(landChoiceMenu,"RoomDoorOption_"+index,
                new Vector2((i-(options.Length-1)*.5f)*cellWidth,-header/2),new Vector2(cellWidth-12,tileHeight),new Color(.12f,.19f,.24f));
            tile.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(216,340,9);
            var button=tile.gameObject.AddComponent<Button>();button.targetGraphic=tile.GetComponent<Image>();
            button.onClick.AddListener(()=>Choose(index,captured));button.interactable=canAct&&!submitted&&!option.disabled;
            bool left=card.stateName=="LeftSplit",unlocked=left?card.leftRoomUnlocked:card.rightRoomUnlocked;
            string name=GamePreferences.CardFaceName(card.name);
            var caption=Label(tile,"DoorName",name,new Vector2(0,tileHeight/2-22),new Vector2(cellWidth-28,36),16,Paper);
            caption.resizeTextForBestFit=false;caption.raycastTarget=false;
            float nameHeight=Mathf.Max(36,caption.preferredHeight+8);
            caption.rectTransform.sizeDelta=new Vector2(cellWidth-28,nameHeight);
            caption.rectTransform.anchoredPosition=new Vector2(0,tileHeight/2-nameHeight/2-4);
            float artHeight=tileHeight-nameHeight-42,artWidth=Mathf.Min(cellWidth-28,artHeight*.71f);
            var art=Rect(tile,"DoorArt",new Vector2(0,14-nameHeight/2),new Vector2(artHeight,artWidth)).gameObject.AddComponent<RawImage>();
            art.rectTransform.localRotation=Quaternion.Euler(0,0,-90);art.raycastTarget=false;
            if(catalog.TryGetValue(card.printingKey??"",out var definition) && CardImageLibrary.TryLoadFullCard(definition,out var sprite)) {
                var r=sprite.rect;art.texture=sprite.texture;
                art.uvRect=new Rect((r.x+r.width*.04f)/sprite.texture.width,(r.y+r.height*(left?.035f:.5f))/sprite.texture.height,
                    r.width*.92f/sprite.texture.width,r.height*.465f/sprite.texture.height);
            } else {
                art.color=Color.clear;
                var fallback=Label(tile,"MissingDoorArt",card.currentRules,new Vector2(0,14-nameHeight/2),new Vector2(cellWidth-32,artHeight),15,Paper);
                fallback.raycastTarget=false;fallback.resizeTextForBestFit=false;
            }
            string action=unlocked?TapPaymentText("锁定","Lock","閉じる"):TapPaymentText("解锁","Unlock","開放");
            string side=left?TapPaymentText("左门","Left door","左のドア"):TapPaymentText("右门","Right door","右のドア");
            var footer=Label(tile,"DoorAction",action+" · "+side,new Vector2(0,-tileHeight/2+20),new Vector2(cellWidth-28,30),16,unlocked?Gold:Paper);
            footer.resizeTextForBestFit=false;footer.raycastTarget=false;
        }
        landChoiceMenu.gameObject.SetActive(true);landChoiceMenu.SetAsLastSibling();
    }
}
