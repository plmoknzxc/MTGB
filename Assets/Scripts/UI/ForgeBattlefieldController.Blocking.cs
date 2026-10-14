using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private int selectedBlocker=-1;
    private long blockRequest;
    private RectTransform blockHint;
    private ForgeTargetArrow blockArrow;
    private readonly List<ForgeTargetArrow> blockLines=new List<ForgeTargetArrow>();
    private bool IsBlockingChoice=>integrated&&canAct&&!submitted&&view?.awaitingInput==true&&view.request.kind=="InputBlock";
    private bool BlockCandidate(int id)=>IsBlockingChoice&&view.request.options.Any(o=>o.id.StartsWith("block:")&&(o.defenderId==id || selectedBlocker==o.defenderId&&o.attackerId==id));
    private bool BeginBlock(int id,long expected)
    {
        if(!IsBlockingChoice || expected!=requestId || !view.request.options.Any(o=>o.id.StartsWith("block:")&&o.defenderId==id))return false;
        selectedBlocker=id;blockRequest=requestId;PresentIntegrated(connectionText.text);return true;
    }
    private void ClickBlockCard(int id,long expected)
    {
        if(!IsBlockingChoice || expected!=requestId)return;
        int option=OptionIndex("block:"+selectedBlocker+":"+id);
        if(option>=0)Choose(option,expected);else BeginBlock(id,expected);
    }
    private bool DropBlock(Vector2 position,long expected)
    {
        if(!IsBlockingChoice||expected!=requestId)return false;
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},hits);
        var button=hits.Count>0?hits[0].gameObject.GetComponentInParent<Button>():null;
        int option=button!=null&&button.name.StartsWith("Card_")?OptionIndex("block:"+selectedBlocker+":"+button.name.Substring(5)):-1;
        if(option>=0)return TrySubmitPresentedOption(option,expected);
        selectedBlocker=-1;PresentIntegrated(connectionText.text);return false;
    }
    private void UpdateBlockArrow(Vector2 position)
    {
        if(blockArrow==null||!IsBlockingChoice||selectedBlocker<0)return;
        var source=AttackEntity("card",selectedBlocker);if(source==null)return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,position,null,out var end);
        blockArrow.SetEndpoints(surface.InverseTransformPoint(source.position),end,new Color(.3f,.72f,1));
    }
    private void PresentBlocking()
    {
        if(blockRequest!=requestId||!IsBlockingChoice)selectedBlocker=-1;
        bool selecting=IsBlockingChoice&&selectedBlocker>=0;
        if(blockHint==null&&selecting) {
            blockHint=Rect(surface,"BlockHint",Vector2.zero,new Vector2(280,72));
            Label(blockHint,"Hint","选择要阻挡的攻击生物",new Vector2(0,20),new Vector2(280,28),18,Paper);
            Button(blockHint,"Remove","取消此生物的阻挡",new Vector2(0,-20),new Vector2(190,32),()=>Choose(OptionIndex("block-remove:"+selectedBlocker),requestId),Ink);
        }
        if(blockHint!=null){blockHint.gameObject.SetActive(selecting);if(selecting){var from=AttackEntity("card",selectedBlocker);blockHint.anchoredPosition=(Vector2)surface.InverseTransformPoint(from.position)+new Vector2(0,from.rect.height/2+50);blockHint.Find("Remove").gameObject.SetActive(OptionIndex("block-remove:"+selectedBlocker)>=0);}}
        if(blockArrow==null&&selecting) {
            blockArrow=NewAttackArrow("BlockDraftArrow");
            SetOverlayLayer(blockArrow.rectTransform,DragArrowLayerOffset);
        }
        if(blockArrow!=null)blockArrow.gameObject.SetActive(selecting);
        int used=0;
        foreach(var blocker in (view?.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>()))
            foreach(int attacker in blocker.blockedAttackerIds??Array.Empty<int>()) {
                var from=AttackEntity("card",blocker.id);var to=AttackEntity("card",attacker);if(from==null||to==null)continue;
                if(used==blockLines.Count)blockLines.Add(NewAttackArrow("BlockAssignment_"+used));
                var line=blockLines[used++];line.gameObject.SetActive(true);line.transform.SetSiblingIndex(cardsRoot.GetSiblingIndex());
                line.SetEndpoints(surface.InverseTransformPoint(from.position),surface.InverseTransformPoint(to.position),new Color(.3f,.7f,1));
            }
        for(int i=used;i<blockLines.Count;i++)blockLines[i].gameObject.SetActive(false);
        if(IsBlockingChoice)SetText("PriorityPill/Text",selecting?"选择攻击生物 · 点击或拖动":"选择阻挡生物 · 完成后确认");
    }
}
