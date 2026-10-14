using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private readonly List<RectTransform> otherHandCards=new List<RectTransform>();
    private void PresentOtherHand(ForgePlayerView player)
    {
        int count=Math.Min(Math.Max(0,player.handCount),24);
        var visibleHand=VisibleHand(player);
        bool revealed=visibleHand.Length==player.handCount && player.handCount>0;
        if(revealed) {
            foreach(var oldBack in otherHandCards)oldBack.gameObject.SetActive(false);
            const float width=182,height=254;
            float span=Mathf.Max(320,Mathf.Min(1120,surface.rect.width-760));
            float spacing=Mathf.Min(112,(span-width-32)/Mathf.Max(1,visibleHand.Length-1));
            for(int i=0;i<visibleHand.Length;i++) {
                float offset=i-(visibleHand.Length-1)*.5f,fan=offset/Mathf.Max(1,(visibleHand.Length-1)*.5f);
                var card=visibleHand[i];
                DrawArenaCard(otherHand,card,1,"OtherHand:"+card.id,"OtherHand:"+card.id,
                    new Vector2(offset*spacing,-46+fan*fan*28),new Vector2(width,height),true,
                    180+fan*Mathf.Min(14,(visibleHand.Length-1)*1.5f),i);
            }
            ForgeBattlefieldCardPointer.RestoreHandOrder(otherHand);return;
        }
        float available=Mathf.Max(320,Mathf.Min(1120,surface.rect.width-760));
        float step=Mathf.Min(112,(available-182-32)/Math.Max(1,count-1));
        CardImageLibrary.TryLoadCardBack(out var back);
        for(int i=0;i<Math.Max(count,otherHandCards.Count);i++) {
            if(i>=otherHandCards.Count) {
                var card=Panel(otherHand,"OtherHand_"+i,Vector2.zero,new Vector2(182,254),Color.white);
                card.GetComponent<Image>().preserveAspect=true;card.GetComponent<Image>().raycastTarget=false;
                otherHandCards.Add(card);
            }
            var tile=otherHandCards[i];tile.gameObject.SetActive(i<count);if(i>=count)continue;
            float offset=i-(count-1)*.5f;
            float normalized=offset/Mathf.Max(1,(count-1)*.5f);
            tile.anchoredPosition=new Vector2(offset*step,-46+normalized*normalized*28);
            tile.localRotation=Quaternion.Euler(0,0,180+normalized*Mathf.Min(14,(count-1)*1.5f));
            var image=tile.GetComponent<Image>();image.sprite=back;image.color=back!=null?Color.white:Ink;
            // Only display identities already present in this recipient's snapshot.
            if(player.hand!=null&&i<player.hand.Length&&TryBattlefieldArt(player.hand[i],out var art)&&art!=null)image.sprite=art;
            tile.SetAsLastSibling();
        }
    }
}
