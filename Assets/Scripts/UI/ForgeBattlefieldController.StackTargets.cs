using System;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private ForgeStackTarget[] inspectedStackTargets;
    private RectTransform stackTargetDetails;
    private string stackTargetDetailsKey;
    private string StackTargetZone(string zone)=>zone switch {
        "Graveyard"=>TapPaymentText("坟墓场","Graveyard","墓地"),
        "Exile"=>TapPaymentText("放逐区","Exile","追放領域"),
        "Ante"=>TapPaymentText("赌注区","Ante","アンティ領域"),
        "Battlefield"=>TapPaymentText("战场","Battlefield","戦場"),
        "Stack"=>TapPaymentText("堆叠","Stack","スタック"),
        "Hand"=>TapPaymentText("手牌","Hand","手札"),
        "Library"=>TapPaymentText("牌库","Library","ライブラリー"),
        "Command"=>TapPaymentText("统帅区","Command","統率領域"),_=>zone??""
    };
    private string StackTargetPlayer(int player)=>PlayerDisplayName(player);
    private void PresentStackTargetDetails(RectTransform panel,float top,float height)
    {
        if(stackTargetDetails!=null)stackTargetDetails.gameObject.SetActive(height>0);
        if(height<=0)return;
        if(stackTargetDetails==null)stackTargetDetails=Rect(panel,"StackTargets",Vector2.zero,new Vector2(308,height));
        stackTargetDetails.gameObject.SetActive(true);
        stackTargetDetails.anchorMin=stackTargetDetails.anchorMax=stackTargetDetails.pivot=new Vector2(.5f,1);
        stackTargetDetails.anchoredPosition=new Vector2(0,-top);stackTargetDetails.sizeDelta=new Vector2(308,height);
        string key=seat+":"+GamePreferences.Revision+":"+PlayerNamesKey()+":"+string.Join("|",Array.ConvertAll(inspectedStackTargets,t=>ServerSnapshotJson.Serialize(t)));
        if(stackTargetDetailsKey==key)return;
        stackTargetDetailsKey=key;Clear(stackTargetDetails);
        var title=Label(stackTargetDetails,"Title",TapPaymentText("目标","Targets","対象")+" · "+inspectedStackTargets.Length,new Vector2(0,-13),new Vector2(308,26),16,Paper);
        title.rectTransform.anchorMin=title.rectTransform.anchorMax=new Vector2(.5f,1);title.raycastTarget=false;
        var content=Scroll(stackTargetDetails,"Cards",new Vector2(0,-14),new Vector2(308,194));
        var scroll=content.parent.GetComponent<ScrollRect>();scroll.horizontal=true;scroll.vertical=false;scroll.movementType=ScrollRect.MovementType.Clamped;
        content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);content.sizeDelta=new Vector2(Mathf.Max(308,inspectedStackTargets.Length*148),194);
        for(int i=0;i<inspectedStackTargets.Length;i++) {
            var target=inspectedStackTargets[i];var card=target.card;
            var tile=Rect(content,"Target_"+target.kind+"_"+target.id,new Vector2(74+i*148,-97),new Vector2(140,194));
            tile.anchorMin=tile.anchorMax=new Vector2(0,1);
            var art=Rect(tile,"Art",new Vector2(0,20),new Vector2(106,148)).gameObject.AddComponent<Image>();
            art.preserveAspect=true;art.raycastTarget=false;Sprite sprite=null;if(!string.IsNullOrEmpty(card?.name))TryBattlefieldArt(card,out sprite);
            art.sprite=sprite;art.color=sprite!=null?Color.white:Ink;
            string name=target.kind=="player"?StackTargetPlayer(target.playerSeat):card?.faceDown==true?TapPaymentText("面朝下的牌","Face-down card","裏向きのカード"):GamePreferences.CardFaceName(card?.name);
            if(sprite==null){var nameLabel=Label(tile,"Name",name,new Vector2(0,20),new Vector2(130,120),16,Paper);nameLabel.raycastTarget=false;if(target.kind=="player")SetNamedPlayerText(nameLabel,name);}
            string location=target.kind=="player"?name:StackTargetPlayer(target.ownerSeat)+" · "+StackTargetZone(target.zone);
            if(target.leftZone)location+="\n"+TapPaymentText("已离开原区域","Left its original zone","元の領域を離れた");
            var caption=Label(tile,"Location",(i+1)+" · "+location,new Vector2(0,-75),new Vector2(140,44),13,Paper);
            SetNamedPlayerText(caption,(i+1)+" · "+location);
            caption.raycastTarget=false;caption.resizeTextForBestFit=false;
        }
    }
}
