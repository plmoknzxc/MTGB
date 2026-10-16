using System;
using System.Linq;
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
    private float PlayerTargetRowHeight(ForgeStackTarget target)
    {
        var text=SceneText("InspectPanel/Name");var settings=text.GetGenerationSettings(new Vector2(268,0));
        settings.fontSize=Mathf.RoundToInt(16*GamePreferences.TextScale);settings.richText=false;settings.resizeTextForBestFit=false;
        string value=TapPaymentText("目标：","Target: ","対象：")+StackTargetPlayer(target.playerSeat);
        return Mathf.Max(32,Mathf.Ceil(text.cachedTextGeneratorForLayout.GetPreferredHeight(value,settings)/text.pixelsPerUnit))+8;
    }
    private float StackTargetDetailsHeight()
    {
        var targets=inspectedStackTargets??Array.Empty<ForgeStackTarget>();
        int cards=targets.Count(t=>t.kind!="player");
        return targets.Where(t=>t.kind=="player").Sum(PlayerTargetRowHeight)+(cards>0?182+StackTargetCaptionHeight()+(cards>2?12:0):0);
    }
    private string StackTargetLocation(ForgeStackTarget target)
    {
        string value=StackTargetPlayer(target.ownerSeat)+" · "+StackTargetZone(target.zone);
        return target.leftZone?value+"\n"+TapPaymentText("已离开原区域","Left its original zone","元の領域を離れた"):value;
    }
    private float StackTargetCaptionHeight()
    {
        var text=SceneText("InspectPanel/Name");var settings=text.GetGenerationSettings(new Vector2(128,0));
        settings.fontSize=Mathf.RoundToInt(13*GamePreferences.TextScale);settings.richText=false;settings.resizeTextForBestFit=false;
        return (inspectedStackTargets??Array.Empty<ForgeStackTarget>()).Where(t=>t.kind!="player")
            .Select(t=>Mathf.Ceil(text.cachedTextGeneratorForLayout.GetPreferredHeight(StackTargetLocation(t),settings)/text.pixelsPerUnit)+6).DefaultIfEmpty(44).Append(44).Max();
    }
    private void PresentStackTargetDetails(RectTransform panel,float top,float height)
    {
        if(stackTargetDetails!=null)stackTargetDetails.gameObject.SetActive(height>0);
        if(height<=0)return;
        if(stackTargetDetails==null)stackTargetDetails=Rect(panel,"StackTargets",Vector2.zero,new Vector2(308,height));
        stackTargetDetails.gameObject.SetActive(true);
        stackTargetDetails.anchorMin=stackTargetDetails.anchorMax=stackTargetDetails.pivot=new Vector2(.5f,1);
        stackTargetDetails.anchoredPosition=new Vector2(0,-top);stackTargetDetails.sizeDelta=new Vector2(268,height);
        string key=seat+":"+GamePreferences.Revision+":"+PlayerNamesKey()+":"+string.Join("|",Array.ConvertAll(inspectedStackTargets,t=>ServerSnapshotJson.Serialize(t)));
        if(stackTargetDetailsKey==key)return;
        stackTargetDetailsKey=key;Clear(stackTargetDetails);
        float playerHeight=0;
        foreach(var player in inspectedStackTargets.Where(t=>t.kind=="player")) {
            float rowHeight=PlayerTargetRowHeight(player);
            string value=TapPaymentText("目标：","Target: ","対象：")+StackTargetPlayer(player.playerSeat);
            var name=Label(stackTargetDetails,"PlayerTarget_"+player.playerSeat,value,new Vector2(0,-playerHeight-rowHeight*.5f),new Vector2(268,rowHeight-8),16,Paper);
            name.rectTransform.anchorMin=name.rectTransform.anchorMax=new Vector2(.5f,1);name.alignment=TextAnchor.MiddleLeft;name.raycastTarget=false;
            SetNamedPlayerText(name,value,player.playerSeat);playerHeight+=rowHeight;
        }
        var cardTargets=inspectedStackTargets.Where(t=>t.kind!="player").ToArray();
        if(cardTargets.Length==0)return;
        float captionHeight=StackTargetCaptionHeight(),cardHeight=152+captionHeight;
        var title=Label(stackTargetDetails,"Title",TapPaymentText("目标牌","Target cards","対象のカード"),new Vector2(0,-playerHeight-13),new Vector2(268,26),16,Paper);
        title.rectTransform.anchorMin=title.rectTransform.anchorMax=new Vector2(.5f,1);title.raycastTarget=false;
        var content=Scroll(stackTargetDetails,"Cards",new Vector2(0,-playerHeight*.5f-14+(cardTargets.Length>2?6:0)),new Vector2(268,cardHeight));
        var scroll=content.parent.GetComponent<ScrollRect>();scroll.horizontal=true;scroll.vertical=false;scroll.movementType=ScrollRect.MovementType.Clamped;
        const float step=134;
        content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);content.sizeDelta=new Vector2(Mathf.Max(268,cardTargets.Length*step),cardHeight);
        if(cardTargets.Length>2) {
            var track=Panel(stackTargetDetails,"HorizontalBar",new Vector2(0,6),new Vector2(268,8),new Color(.13f,.18f,.22f));
            track.anchorMin=track.anchorMax=new Vector2(.5f,0);
            var handle=Panel(track,"Handle",Vector2.zero,Vector2.zero,new Color(.43f,.57f,.66f));
            handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=handle.offsetMax=Vector2.zero;
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=handle.GetComponent<Image>();bar.direction=Scrollbar.Direction.LeftToRight;bar.transition=Selectable.Transition.None;
            scroll.horizontalScrollbar=bar;
        }
        for(int i=0;i<cardTargets.Length;i++) {
            var target=cardTargets[i];var card=target.card;
            var tile=Rect(content,"Target_"+target.kind+"_"+target.id,new Vector2(step*.5f+i*step,-cardHeight*.5f),new Vector2(128,cardHeight));
            tile.anchorMin=tile.anchorMax=new Vector2(0,1);
            var art=Rect(tile,"Art",new Vector2(0,cardHeight*.5f-77),new Vector2(106,148)).gameObject.AddComponent<Image>();
            art.preserveAspect=true;art.raycastTarget=false;Sprite sprite=null;if(!string.IsNullOrEmpty(card?.name))TryBattlefieldArt(card,out sprite);
            art.sprite=sprite;art.color=sprite!=null?Color.white:Ink;
            string name=card?.faceDown==true?TapPaymentText("面朝下的牌","Face-down card","裏向きのカード"):GamePreferences.CardFaceName(card?.name);
            if(sprite==null){var nameLabel=Label(tile,"Name",name,new Vector2(0,cardHeight*.5f-77),new Vector2(124,120),16,Paper);nameLabel.raycastTarget=false;}
            string location=StackTargetLocation(target);
            var caption=Label(tile,"Location",location,new Vector2(0,-cardHeight*.5f+captionHeight*.5f),new Vector2(128,captionHeight),13,Paper);
            SetNamedPlayerText(caption,location);
            caption.raycastTarget=false;caption.resizeTextForBestFit=false;
        }
    }
}
