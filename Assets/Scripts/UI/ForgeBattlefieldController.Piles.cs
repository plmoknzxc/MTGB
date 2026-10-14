using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private long pileRequest;
    private int selectedPileIndex=-1;
    private Button pileConfirm;

    private void RenderPileSelection(ForgeRequest request)
    {
        string key="pile/"+request.requestId+"/"+surface.rect.size+"/"+GamePreferences.Revision;
        if(pileRequest!=request.requestId){pileRequest=request.requestId;selectedPileIndex=-1;}
        if(selectionLayoutKey==key && selectionPanel!=null) {
            cardSelectionOverlay.gameObject.SetActive(true);cardSelectionOverlay.SetAsLastSibling();
            UpdatePileSelection();return;
        }
        selectionLayoutKey=key;
        if(cardSelectionOverlay==null) {
            cardSelectionOverlay=Panel(surface,"CardSelectionOverlay",Vector2.zero,Vector2.zero,new Color(0,0,0,.72f));
            cardSelectionOverlay.anchorMin=Vector2.zero;cardSelectionOverlay.anchorMax=Vector2.one;
            cardSelectionOverlay.offsetMin=cardSelectionOverlay.offsetMax=Vector2.zero;
        }
        Clear(cardSelectionOverlay);cardSelectionOverlay.gameObject.SetActive(true);cardSelectionOverlay.SetAsLastSibling();
        float width=Mathf.Min(1000,surface.rect.width-40),height=Mathf.Min(690,surface.rect.height-40);
        selectionPanel=Panel(cardSelectionOverlay,"PileSelection",Vector2.zero,new Vector2(width,height),Ink);
        Label(selectionPanel,"Title",TapPaymentText("选择一堆牌","Choose a pile","束を選択"),new Vector2(0,height/2-29),new Vector2(width-40,52),24,Paper);
        Label(selectionPanel,"Instruction",TapPaymentText("选择整堆，再确认。","Select a whole pile, then confirm.","束全体を選んでから確定。"),new Vector2(0,height/2-74),new Vector2(width-40,40),18,Paper);
        long captured=request.requestId;float columnWidth=(width-56)/2,columnHeight=height-160;
        foreach(var pile in request.piles??Array.Empty<ForgePileView>()) {
            int index=Array.FindIndex(request.options,o=>o.id=="pile:"+pile.pileId);
            if(index<0)continue;
            float x=(pile.pileId==1?-1:1)*(columnWidth+12)/2;
            var column=Panel(selectionPanel,"Pile_"+pile.pileId,new Vector2(x,-9),new Vector2(columnWidth,columnHeight),new Color(.09f,.13f,.18f));
            Label(column,"Count",TapPaymentText($"牌堆 {pile.pileId} · {pile.cardCount} 张",$"Pile {pile.pileId} · {pile.cardCount} cards",$"束 {pile.pileId}・{pile.cardCount} 枚"),new Vector2(0,columnHeight/2-24),new Vector2(columnWidth-20,44),20,Paper);
            var select=Button(column,"SelectPile_"+index,TapPaymentText("选择这堆","Select pile","この束を選択"),new Vector2(0,-columnHeight/2+30),new Vector2(columnWidth-28,44),()=>{if(canAct&&!submitted&&requestId==captured){selectedPileIndex=index;UpdatePileSelection();}},new Color(.18f,.32f,.49f));
            select.interactable=canAct&&!submitted;
            float scrollHeight=columnHeight-112;
            var content=Scroll(column,"PileScroll",new Vector2(0,5),new Vector2(columnWidth-16,scrollHeight));
            if(pile.concealed) {
                // No art lookup, ID or face name exists for a concealed pile.
                var back=Panel(content,"HiddenPile",new Vector2(0,-146),new Vector2(172,236),new Color(.15f,.20f,.26f));
                back.anchorMin=back.anchorMax=new Vector2(.5f,1);
                Label(back,"HiddenLabel",TapPaymentText("面朝下","Face down","裏向き"),Vector2.zero,new Vector2(152,72),20,Paper);
                content.sizeDelta=new Vector2(columnWidth-16,Mathf.Max(scrollHeight,300));
            }else if(pile.cardCount==0) {
                TopLabel(content,"EmptyPile",TapPaymentText("空牌堆","Empty pile","空の束"),64,columnWidth-30,52);
            }else {
                var cards=pile.cards??Array.Empty<ForgeCardView>();int columns=columnWidth<360?1:2;
                float cell=(columnWidth-28)/columns,artWidth=Mathf.Min(178,cell-12),artHeight=artWidth*1.4f,row=artHeight+58;
                for(int i=0;i<cards.Length;i++) {
                    var card=cards[i];
                    var art=Panel(content,"PileCard_"+card.id,new Vector2((i%columns-(columns-1)/2f)*cell,-12-(i/columns)*row-artHeight/2),new Vector2(artWidth,artHeight),Ink);
                    art.anchorMin=art.anchorMax=new Vector2(.5f,1);
                    // Inspectable exile stays face down in the engine. Only this
                    // recipient's supplied known face is used for its pile preview.
                    var shown=card;
                    if(card.faceDown&&!string.IsNullOrEmpty(card.knownFacePrintingKey)) {
                        shown=ServerSnapshotJson.Copy(card);
                        shown.faceDown=false;shown.name=card.knownFaceName;shown.printingKey=card.knownFacePrintingKey;
                        shown.currentRules=card.knownFaceRules;
                    }
                    TryBattlefieldArt(shown,out var sprite);var image=art.GetComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.color=sprite==null?Ink:Color.white;
                    if(sprite==null)Label(art,"MissingArt",TapPaymentText("缺失卡图","Missing card image","カード画像なし"),Vector2.zero,new Vector2(artWidth-12,92),16,Paper);
                    Label(art,"CardName",GamePreferences.CardFaceName(shown.name),new Vector2(0,-artHeight/2-23),new Vector2(cell-12,42),16,Paper).supportRichText=false;
                }
                content.sizeDelta=new Vector2(columnWidth-16,Mathf.Max(scrollHeight,12+Mathf.CeilToInt(cards.Length/(float)columns)*row));
            }
        }
        pileConfirm=Button(selectionPanel,"PileConfirm",TapPaymentText("确认这堆","Confirm pile","束を確定"),new Vector2(0,-height/2+29),new Vector2(260,44),()=>Choose(selectedPileIndex,captured),new Color(.18f,.38f,.55f));
        UpdatePileSelection();
    }

    private void UpdatePileSelection()
    {
        foreach(var button in selectionPanel.GetComponentsInChildren<Button>()) {
            if(!button.name.StartsWith("SelectPile_"))continue;
            int index=int.Parse(button.name.Substring(11));button.interactable=canAct&&!submitted;
            button.GetComponent<Image>().color=index==selectedPileIndex?new Color(.30f,.48f,.27f):new Color(.18f,.32f,.49f);
            button.GetComponentInChildren<Text>().text=index==selectedPileIndex?TapPaymentText("已选这堆","Selected pile","選択済み"):TapPaymentText("选择这堆","Select pile","この束を選択");
        }
        if(pileConfirm!=null)pileConfirm.interactable=canAct&&!submitted&&selectedPileIndex>=0;
    }
}
