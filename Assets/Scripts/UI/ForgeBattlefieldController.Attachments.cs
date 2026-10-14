using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private readonly List<ForgeTargetArrow> attachmentArrows=new List<ForgeTargetArrow>();

    private ForgeCardView[] PublicBattlefieldCards() => (view?.players??Array.Empty<ForgePlayerView>())
        .SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>()).ToArray();

    private string AttachmentEntityName(string kind,int id)
    {
        if(kind=="player") {
            var player=view?.players?.FirstOrDefault(p=>p.targetId==id);
            return player==null?"":PlayerDisplayName(player.seat);
        }
        var card=PublicBattlefieldCards().FirstOrDefault(c=>c.id==id);
        if(card==null)return "";
        return card.faceDown?TapPaymentText("面朝下卡牌","Face-down card","裏向きのカード"):AbilitySourceName(card);
    }

    private string AttachmentDescription(ForgeCardView card)
    {
        var lines=new List<string>();
        if(card.attachedToKind is "card" or "player") {
            string name=AttachmentEntityName(card.attachedToKind,card.attachedToId);
            if(name.Length>0)lines.Add(TapPaymentText("附着于：","Attached to: ","エンチャント／装備先：")+SafeDetail(name));
        }
        var names=(card.attachedByIds??Array.Empty<int>()).Select(id=>AttachmentEntityName("card",id)).Where(n=>n.Length>0);
        if(names.Any())lines.Add(TapPaymentText("附着物：","Attachments: ","ついているカード：")+string.Join(", ",names.Select(SafeDetail)));
        return string.Join("\n",lines);
    }

    // Reuse the existing animated target arrow. These links are read-only and
    // only visible while inspecting an attached object or its host.
    private void RefreshAttachmentLinks()
    {
        var links=new List<(int from,string kind,int to)>();
        if(integrated&&surface!=null&&view!=null&&inspectedCardId!=0&&Node("InspectPanel").gameObject.activeSelf) {
            var cards=PublicBattlefieldCards();var inspected=cards.FirstOrDefault(c=>c.id==inspectedCardId);
            if(inspected!=null) {
                if(inspected.attachedToKind is "card" or "player")links.Add((inspected.id,inspected.attachedToKind,inspected.attachedToId));
                foreach(int id in inspected.attachedByIds??Array.Empty<int>()) {
                    var source=cards.FirstOrDefault(c=>c.id==id);
                    if(source?.attachedToKind=="card"&&source.attachedToId==inspected.id)links.Add((id,"card",inspected.id));
                }
            }
        }
        int used=0;
        foreach(var link in links) {
            var from=AttackEntity("card",link.from);var to=AttackEntity(link.kind,link.to);
            if(from==null||to==null||!from.gameObject.activeInHierarchy||!to.gameObject.activeInHierarchy)continue;
            if(used==attachmentArrows.Count)attachmentArrows.Add(NewAttackArrow("AttachmentLink_"+used));
            var arrow=attachmentArrows[used++];arrow.SetVisible(true);
            arrow.SetEndpoints(surface.InverseTransformPoint(from.TransformPoint(from.rect.center)),surface.InverseTransformPoint(to.TransformPoint(to.rect.center)),Blue);
            // Keep links behind dialogs and the inspected card, with no raycasts.
            arrow.transform.SetSiblingIndex(Mathf.Max(0,Node("InspectPanel").GetSiblingIndex()-1));
        }
        for(int i=used;i<attachmentArrows.Count;i++)attachmentArrows[i].SetVisible(false);
    }
}
