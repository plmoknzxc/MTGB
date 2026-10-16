using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private readonly List<RectTransform> otherHandCards=new List<RectTransform>();
    private readonly ForgeBattlefieldCardPointer.HandHitLayout otherHandHits=new();
    private void PresentOtherHand(ForgePlayerView player)
    {
        otherHandHits.Clear();
        int count=Math.Min(Math.Max(0,player.handCount),24);
        // A partial reveal offers previews for those known identities only.
        // Hidden slots remain non-interactive backs, with no preview callback.
        var visibleHand=VisibleHand(player).Where(c=>c!=null&&!c.faceDown&&!string.IsNullOrEmpty(c.name))
            .GroupBy(c=>c.id).Select(g=>g.First()).Take(Math.Max(0,player.handCount)).ToArray();
        count=Math.Max(count,visibleHand.Length);
        float available=Mathf.Max(320,Mathf.Min(1120,surface.rect.width-760));
        float step=Mathf.Min(112,(available-182-32)/Math.Max(1,count-1));
        CardImageLibrary.TryLoadCardBack(out var back);
        foreach(var oldBack in otherHandCards)oldBack.gameObject.SetActive(false);
        for(int i=0;i<Math.Max(count,otherHandCards.Count);i++) {
            if(i>=count)continue;
            float offset=i-(count-1)*.5f;
            float normalized=offset/Mathf.Max(1,(count-1)*.5f);
            var position=new Vector2(offset*step,-46+normalized*normalized*28);
            float rotation=180+normalized*Mathf.Min(14,(count-1)*1.5f);
            if(i<visibleHand.Length) {
                var known=visibleHand[i];
                DrawArenaCard(otherHand,known,1,"OtherHand:"+known.id,"OtherHand:"+known.id,
                    position,new Vector2(182,254),true,rotation,i);
                otherHandHits.Add(arenaCards["OtherHand:"+known.id].pointer);
                continue;
            }
            while(i>=otherHandCards.Count) {
                var card=Panel(otherHand,"OtherHand_"+otherHandCards.Count,Vector2.zero,new Vector2(182,254),Color.white);
                card.GetComponent<Image>().preserveAspect=true;card.GetComponent<Image>().raycastTarget=false;
                card.gameObject.SetActive(false);
                otherHandCards.Add(card);
            }
            var tile=otherHandCards[i];tile.gameObject.SetActive(true);
            tile.anchoredPosition=position;
            tile.localRotation=Quaternion.Euler(0,0,rotation);
            var image=tile.GetComponent<Image>();image.sprite=back;image.color=back!=null?Color.white:Ink;
            tile.SetAsLastSibling();
        }
        ForgeBattlefieldCardPointer.RestoreHandOrder(otherHand);
    }

}
