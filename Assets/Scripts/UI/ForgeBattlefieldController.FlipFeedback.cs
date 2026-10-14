using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private string FlipCardLabel(ForgeCardView card)
    {
        if(card==null||card.id<=0)return "";
        string name=card.faceDown?TapPaymentText("牌面朝下的牌","Face-down card","裏向きのカード"):
            string.IsNullOrEmpty(card.originalName)||card.originalName==card.name
            ?GamePreferences.CardFaceName(card.name):card.name;
        return name+" ("+card.id+")";
    }

    private string FlipNoticeText(ForgeNotice notice)
    {
        string source=notice.sourceCard?.id>0?FlipCardLabel(notice.sourceCard):GamePreferences.NativeText(notice.title??"");
        string title=(notice.actorSeat>0?PlayerDisplayName(notice.actorSeat)+" · ":"")+source+"\n";
        string result=notice.flipOutcome=="empty"
            ?TapPaymentText("战场上没有可选的落点，未命中任何牌。","No landing location on the battlefield; no cards hit.","戦場に選択できる位置がありません。命中したカードはありません。")
            :notice.flipOutcome=="no-turn"
            ?TapPaymentText("未完成翻转，未命中任何牌。","The card did not turn over; no cards hit.","カードは回転しませんでした。命中したカードはありません。")
            :TapPaymentText($"翻转了 {notice.flipCount} 次。",$"Turned over {notice.flipCount} time(s).",$"{notice.flipCount}回回転しました。");
        if(notice.flipLocation?.id>0)result+="\n"+TapPaymentText("选定位置：","Selected location: ","選択した位置：")+FlipCardLabel(notice.flipLocation);
        if(notice.flipOutcome=="hit")result+="\n"+TapPaymentText("命中：","Hit: ","命中：")+
            string.Join(TapPaymentText("、",", ","、"),(notice.flipHits??Array.Empty<ForgeCardView>()).Select(FlipCardLabel));
        else if(notice.flipOutcome=="miss")result+="\n"+TapPaymentText("未命中任何牌。","No cards hit.","命中したカードはありません。");
        return title+result;
    }

    private void PresentFlipHighlights(ForgeNotice notice)
    {
        var hits=notice?.kind=="flip"&&notice.flipOutcome=="hit"
            ?(notice.flipHits??Array.Empty<ForgeCardView>()).Where(c=>c!=null&&c.id>0).Select(c=>c.id).ToArray():Array.Empty<int>();
        foreach(var card in arenaCards.Values)
        {
            if(card.root==null)continue;
            var frame=card.root.Find("FlipHitFrame") as RectTransform;
            var liveHits=(view?.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>()).Where(c=>hits.Contains(c.id));
            bool hit=hits.Any(id=>card.root.name=="Card_"+id)||card.representedCount>1&&
                liveHits.Any(c=>card.groupKey?.EndsWith(":"+ArenaGroupKey(c),StringComparison.Ordinal)==true);
            if(hit&&frame==null)
            {
                frame=Rect(card.root,"FlipHitFrame",Vector2.zero,card.root.sizeDelta+new Vector2(4,4));
                foreach(string edge in new[]{"Top","Bottom","Left","Right"})
                {
                    var line=Rect(frame,edge,Vector2.zero,Vector2.zero);
                    var image=line.gameObject.AddComponent<Image>();
                    image.color=new Color(.3f,.85f,1,.95f);image.raycastTarget=false;
                }
            }
            if(frame!=null)
            {
                frame.sizeDelta=card.root.sizeDelta+new Vector2(4,4);
                foreach(RectTransform line in frame)
                {
                    bool horizontal=line.name=="Top"||line.name=="Bottom";
                    line.sizeDelta=horizontal?new Vector2(frame.sizeDelta.x,3):new Vector2(3,frame.sizeDelta.y);
                    line.anchoredPosition=line.name=="Top"?new Vector2(0,frame.sizeDelta.y*.5f):line.name=="Bottom"?new Vector2(0,-frame.sizeDelta.y*.5f):
                        line.name=="Left"?new Vector2(-frame.sizeDelta.x*.5f,0):new Vector2(frame.sizeDelta.x*.5f,0);
                }
                frame.gameObject.SetActive(hit);
            }
        }
    }
}
