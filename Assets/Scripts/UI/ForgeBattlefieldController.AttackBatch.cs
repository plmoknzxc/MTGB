using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform attackBatchActions;
    private long attackConfirmRequest,attackConfirmContext;
    private int attackConfirmSeat,attackConfirmTurn,attackConfirmRestart,attackConfirmDefender;
    private string attackConfirmDefenderKind;
    private int[] attackConfirmMembers;
    private bool HasAttackDecision => integrated&&view?.awaitingInput==true&&view.request.seat==seat&&view.request.kind=="InputAttack";
    private int[] AssignedAttackers()=>view.request.options.Where(o=>o.id.StartsWith("attack-remove:",StringComparison.Ordinal)).Select(o=>o.attackerId).Distinct().ToArray();
    private int DefaultAttackDestination()
    {
        // Use Forge's common legal player, including a controlled opponent's
        // turn, rather than assuming the recipient owns the attackers.
        if(view?.players?.Length==2) {
            int destination=-1;
            foreach(var player in view.players) {
                int option=AttackOption("player",player.targetId);if(option<0)continue;
                if(destination>=0)return -1;
                destination=option;
            }
            return destination;
        }
        var opponent=view?.players?.FirstOrDefault(p=>p.seat!=seat);
        return opponent!=null?AttackOption("player",opponent.targetId):-1;
    }
    private void ConfirmSelectedAttackers(long expected)
    {
        if(!IsDeclaringAttack||requestId!=expected)return;
        if(selectedAttackers.Count==0){Choose(OptionIndex("ok"),expected);return;}
        int destination=DefaultAttackDestination();if(destination<0)return;
        var option=view.request.options[destination];
        attackConfirmMembers=selectedAttackers.ToArray();attackConfirmRequest=expected;
        attackConfirmContext=view.request.contextId;attackConfirmSeat=seat;attackConfirmTurn=view.turn;
        attackConfirmRestart=view.restartRevision;attackConfirmDefender=option.defenderId;attackConfirmDefenderKind=option.defenderKind;
        ChooseAttackDestination(destination,expected);
        if(!submitted)attackConfirmMembers=null;
    }
    private void CompleteSelectedAttackDeclaration()
    {
        if(attackConfirmMembers==null)return;
        if(!HasAttackDecision||seat!=attackConfirmSeat||view.turn!=attackConfirmTurn||view.restartRevision!=attackConfirmRestart
            ||view.request.contextId!=attackConfirmContext){attackConfirmMembers=null;return;}
        if(requestId==attackConfirmRequest||!IsDeclaringAttack||viewingBattlefield)return;
        var assigned=AssignedAttackers();
        var field=view.players.SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>());
        bool confirmed=attackConfirmMembers.All(id=>assigned.Contains(id)&&field.Any(c=>c.id==id&&c.attacking
            &&c.defenderId==attackConfirmDefender&&c.defenderKind==attackConfirmDefenderKind));
        attackConfirmMembers=null;
        int accept=OptionIndex("ok");
        // Confirm only after Forge has assigned the exact requested group. Taxes,
        // mandatory attacks and new decisions retain their native input contract.
        if(confirmed&&accept>=0&&!view.request.options[accept].disabled)Choose(accept,requestId);
    }
    private void PresentAttackBatchActions()
    {
        if(attackBatchActions!=null)attackBatchActions.gameObject.SetActive(HasAttackDecision);
        if(!HasAttackDecision)return;
        advanceButton.gameObject.SetActive(false);endTurnButton.gameObject.SetActive(false);
        if(attackBatchActions==null){
            attackBatchActions=Rect(Node("CornerCluster/ControlRow"),"AttackBatchActions",new Vector2(0,-20),new Vector2(284,48));
            DockAction(attackBatchActions,"Primary","",new Vector2(-73,0),140,()=>{},false);
            DockAction(attackBatchActions,"EndPhase","",new Vector2(73,0),140,()=>{},true);
            foreach(var label in attackBatchActions.GetComponentsInChildren<Text>())PreferenceText.SetSize(label,16);
        }
        var eligible=view.request.options.Where(o=>o.id.StartsWith("attack:",StringComparison.Ordinal)||o.id.StartsWith("attack-remove:",StringComparison.Ordinal)).Select(o=>o.attackerId).Distinct().ToArray();
        var assigned=AssignedAttackers();
        bool all=eligible.Length>0&&eligible.All(assigned.Contains);
        bool partial=selectedAttackers.Count>0||assigned.Length>0&&!all;
        int primary=OptionIndex(partial?"ok":all?"reset-attacks":"attack-all");
        long captured=requestId;
        var left=attackBatchActions.Find("Primary").GetComponent<Button>();
        left.GetComponentInChildren<Text>().text=partial?TapPaymentText("确定攻击","Confirm attacks","攻撃を確定")
            :all?TapPaymentText("全部取消","Recall all","全て取消"):TapPaymentText("全部攻击","Attack all","全て攻撃");
        left.interactable=IsDeclaringAttack&&(selectedAttackers.Count>0
            ?DefaultAttackDestination()>=0&&(selectedAttackers.Count==1||submitOrder!=null)
            :primary>=0&&!view.request.options[primary].disabled);
        left.onClick.RemoveAllListeners();left.onClick.AddListener(()=>{
            if(!IsDeclaringAttack||captured!=requestId)return;
            if(partial)ConfirmSelectedAttackers(captured);
            else {ClearAttackSelection();Choose(primary,captured);}
        });
        var right=attackBatchActions.Find("EndPhase").GetComponent<Button>();int finish=OptionIndex("ok");
        right.GetComponentInChildren<Text>().text=TapPaymentText("结束阶段","End phase","フェイズ終了");
        right.interactable=IsDeclaringAttack&&finish>=0&&!view.request.options[finish].disabled;
        right.onClick.RemoveAllListeners();right.onClick.AddListener(()=>{if(IsDeclaringAttack&&captured==requestId){ClearAttackSelection();Choose(finish,captured);}});
        RefreshDockActionColor(left);RefreshDockActionColor(right);attackBatchActions.SetAsLastSibling();
    }
}
