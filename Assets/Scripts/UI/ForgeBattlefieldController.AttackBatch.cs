using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform attackBatchActions;
    private void PresentAttackBatchActions()
    {
        if(attackBatchActions!=null)attackBatchActions.gameObject.SetActive(IsDeclaringAttack);
        if(!IsDeclaringAttack)return;
        if(attackBatchActions==null){
            attackBatchActions=Rect(surface,"AttackBatchActions",Vector2.zero,new Vector2(300,96));
            attackBatchActions.anchorMin=attackBatchActions.anchorMax=new Vector2(1,0);attackBatchActions.anchoredPosition=new Vector2(-186,300);
            Button(attackBatchActions,"All","全部攻击",new Vector2(0,23),new Vector2(184,40),()=>{},Ink);
            Button(attackBatchActions,"Recall","取消攻击",new Vector2(0,-23),new Vector2(184,40),()=>{},Ink);
        }
        long captured=requestId;
        foreach(string name in new[]{"All","Recall"}){
            var button=attackBatchActions.Find(name).GetComponent<Button>();
            int option=OptionIndex(name=="All"?"attack-all":"reset-attacks");
            button.interactable=canAct&&!submitted&&option>=0;
            button.GetComponentInChildren<Text>().text=name=="All"?TapPaymentText("全部攻击","Attack all","全て攻撃"):TapPaymentText("取消攻击","Recall all","全て取消");
            button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{if(IsDeclaringAttack){ClearAttackSelection();Choose(option,captured);}});
        }
        attackBatchActions.SetAsLastSibling();
    }
}
