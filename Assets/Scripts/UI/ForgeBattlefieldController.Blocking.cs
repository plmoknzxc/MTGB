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
    private readonly List<CombatLink> blockLines=new();
    private bool IsBlockingChoice=>integrated&&!IsFourPlayerScene&&canAct&&!submitted&&view?.awaitingInput==true&&view.request.kind=="InputBlock" && view.request.seat==seat && !GameSettingsPanel.IsOpen && !viewingBattlefield;
    private bool BlockCandidate(int id)=>IsBlockingChoice&&view.request.options.Any(o=>!o.disabled && o.id.StartsWith("block:")&&(o.defenderId==id || selectedBlocker==o.defenderId&&o.attackerId==id));
    private bool BeginBlock(int id,long expected)
    {
        if(!IsBlockingChoice || expected!=requestId || !view.request.options.Any(o=>!o.disabled && o.id.StartsWith("block:")&&o.defenderId==id))return false;
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
        int option=BlockOptionAt(position);
        if(option>=0)return TrySubmitPresentedOption(option,expected);
        selectedBlocker=-1;PresentIntegrated(connectionText.text);return false;
    }
    private int BlockOptionAt(Vector2 position)
    {
        var button=ChoiceButtonAt(position);
        if(button==null || !button.name.StartsWith("Card_"))return -1;
        int option=OptionIndex("block:"+selectedBlocker+":"+button.name.Substring(5));
        return option>=0 && !view.request.options[option].disabled?option:-1;
    }
    private void UpdateBlockArrow(Vector2 position)
    {
        if(blockArrow==null)return;
        if(!IsBlockingChoice || selectedBlocker<0 || !canvas.pixelRect.Contains(position)){blockArrow.SetVisible(false);return;}
        var source=AttackEntity("card",selectedBlocker);
        if(source==null){blockArrow.SetVisible(false);return;}
        int option=BlockOptionAt(position);
        var target=option>=0?AttackEntity("card",view.request.options[option].attackerId):null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,position,canvas.worldCamera,out var end);
        if(target!=null)end=surface.InverseTransformPoint(target.TransformPoint(target.rect.center));
        blockArrow.SetVisible(true);
        blockArrow.SetEndpoints(surface.InverseTransformPoint(source.position),end,target!=null?BlockInk:new Color(.28f,.56f,.76f),target!=null,true,target);
    }
    private void PresentBlocking()
    {
        if(blockRequest!=requestId||!IsBlockingChoice)selectedBlocker=-1;
        bool selecting=IsBlockingChoice&&selectedBlocker>=0;
        if(blockHint==null&&selecting) {
            blockHint=Rect(surface,"BlockHint",Vector2.zero,new Vector2(280,72));
            Label(blockHint,"Hint","选择要阻挡的攻击生物",new Vector2(0,20),new Vector2(280,28),18,Paper).raycastTarget=false;
            Button(blockHint,"Remove","取消此生物的阻挡",new Vector2(0,-20),new Vector2(190,32),()=>Choose(OptionIndex("block-remove:"+selectedBlocker),requestId),Ink);
        }
        if(blockHint!=null){
            var from=selecting?AttackEntity("card",selectedBlocker):null;
            blockHint.gameObject.SetActive(from!=null);
            if(from!=null){
                blockHint.anchoredPosition=(Vector2)surface.InverseTransformPoint(from.position)+new Vector2(0,from.rect.height/2+50);
                blockHint.Find("Remove").gameObject.SetActive(OptionIndex("block-remove:"+selectedBlocker)>=0);
            }
        }
        if(blockArrow==null&&selecting) {
            blockArrow=NewAttackArrow("BlockDraftArrow");
        }
        if(selecting)UpdateBlockArrow(Input.mousePosition);
        else if(blockArrow!=null)blockArrow.SetVisible(false);
        foreach(var blocker in (view?.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>()))
            foreach(int attacker in blocker.blockedAttackerIds??Array.Empty<int>())
                ShowCombatLink(blockLines,"BlockAssignment_",blocker.id,"card",attacker,BlockInk);
        FinishCombatLinks(blockLines);
        if(IsBlockingChoice)SetText("PriorityPill/Text",selecting?"选择攻击生物 · 点击或拖动":"选择阻挡生物 · 完成后确认");
    }
}
