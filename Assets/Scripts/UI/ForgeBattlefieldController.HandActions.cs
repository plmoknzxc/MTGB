using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform handChoiceActions;
    private string stagedHandChoice;
    private long stagedHandRequest;
    private bool committingHandChoice;
    // The hand draft also serves one-card payments from public zones.
    private bool IsSingleCardCostDraft => view?.awaitingInput==true && view.request.paymentMode=="manual"
        && view.request.kind is "InputSelectCardsFromList" or "InputSelectEntitiesFromList"
        && view.request.maxCount==1 && view.request.selectedCount==0;
    private bool IsOptionalHandSelection=>view?.request?.maxCount==1 && view.request.options.Any(o=>o.id=="cancel") &&
        (IsSingleCardCostDraft || SelectionHand!=null && !view.request.options.Any(o=>o.id=="ok"||o.id=="confirm-cards"));
    private bool CanSkipSingleCostDraft => IsSingleCardCostDraft && OptionIndex("ok") is int index
        && index>=0 && !view.request.options[index].disabled;
    private bool IsOptionalDirectHandSelection=>DirectHandSelection&&IsOptionalHandSelection;
    private bool IsStagedHandChoice(string id)=>!string.IsNullOrEmpty(id)&&IsOptionalHandSelection&&stagedHandRequest==requestId&&stagedHandChoice==id;
    private bool TryStageHandChoice(int index,long captured) {
        if(committingHandChoice||!IsOptionalHandSelection||!HasChoiceCard(view.request.options[index]))return false;
        string id=view.request.options[index].id;
        stagedHandChoice=IsStagedHandChoice(id)?null:id;stagedHandRequest=captured;
        // Keep the authoritative options untouched. Only confirming the draft
        // sends the original card option; the engine still performs selection.
        PresentIntegrated(connectionText.text);return true;
    }
    private void CommitHandChoice(long captured) {
        if(stagedHandRequest!=captured||string.IsNullOrEmpty(stagedHandChoice)) {
            if(CanSkipSingleCostDraft)Choose(OptionIndex("ok"),captured);
            return;
        }
        int index=OptionIndex(stagedHandChoice);
        committingHandChoice=true;try{Choose(index,captured);}finally{committingHandChoice=false;}
    }
    private Button DockAction(Transform parent,string name,string label,Vector2 position,float width,Action action,bool secondary) {
        var button=Button(parent,name,label,position,new Vector2(width,42),action,Color.clear);
        var face=Rect(button.transform,"Face",Vector2.zero,new Vector2(width,42));face.SetAsFirstSibling();
        var graphic=face.gameObject.AddComponent<ForgeActionButtonGraphic>();graphic.Secondary=secondary;button.targetGraphic=graphic;
        var outline=button.GetComponent<Outline>();if(outline!=null)outline.enabled=false;
        var text=button.GetComponentInChildren<Text>();PreferenceText.SetSize(text,12);text.rectTransform.sizeDelta=new Vector2(width-6,40);text.raycastTarget=false;text.resizeTextForBestFit=false;text.color=secondary?Color.white:new Color(.13f,.11f,.075f);
        return button;
    }
    private static void RefreshDockActionColor(Button button) {
        var face=button.GetComponentInChildren<ForgeActionButtonGraphic>();
        button.GetComponentInChildren<Text>().color=!button.interactable?new Color(.66f,.68f,.7f)
            :face!=null&&face.Secondary?Color.white:new Color(.13f,.11f,.075f);
    }
    private bool PresentHandChoiceActions()
    {
        if(handChoiceActions!=null){handChoiceActions.gameObject.SetActive(false);Clear(handChoiceActions);}
        if(stagedHandRequest!=requestId||!IsOptionalHandSelection){stagedHandChoice=null;stagedHandRequest=0;}
        if(!DirectHandSelection)return false;
        var actions=view.request.options.Where(o=>!HasChoiceCard(o)).OrderBy(ChoiceActionOrder).ToArray();
        if(actions.Length==0||actions.Length>3)return false;
        if(handChoiceActions==null)handChoiceActions=Rect(Node("CornerCluster/ControlRow"),"HandChoiceActions",new Vector2(48,-20),new Vector2(184,48));
        handChoiceActions.sizeDelta=new Vector2(actions.Length==3?279:184,48);
        handChoiceActions.anchoredPosition=new Vector2(actions.Length==3?.5f:48,-20);
        handChoiceActions.gameObject.SetActive(true);advanceButton.gameObject.SetActive(false);
        SetOverlayLayer(handChoiceActions,DecisionLayerOffset+1,true);
        if(IsOptionalDirectHandSelection) {
            long captured=requestId;int cancel=OptionIndex("cancel");
            var decline=DockAction(handChoiceActions,"HandAction_"+cancel,TapPaymentText("取消","Cancel","キャンセル"),new Vector2(47.5f,0),89,()=>Choose(cancel,captured),true);
            decline.interactable=canAct&&!submitted&&cancel>=0&&!view.request.options[cancel].disabled;
            var confirm=DockAction(handChoiceActions,"ConfirmHandChoice",TapPaymentText("下一步","Next","次へ"),new Vector2(-47.5f,0),89,()=>CommitHandChoice(captured),false);
            int selected=OptionIndex(stagedHandChoice);
            confirm.interactable=canAct&&!submitted&&(selected>=0&&!view.request.options[selected].disabled||CanSkipSingleCostDraft);
            RefreshDockActionColor(confirm);RefreshDockActionColor(decline);
            return true;
        }
        float step=actions.Length==1?184:95,width=actions.Length==1?184:89;
        for(int i=0;i<actions.Length;i++) {
            int index=OptionIndex(actions[i].id);long captured=requestId;
            var button=DockAction(handChoiceActions,"HandAction_"+index,ChoiceActionLabel(actions[i]),new Vector2((i-(actions.Length-1)*.5f)*step,0),width,()=>Choose(index,captured),actions[i].id=="cancel"||actions[i].id=="reset-cards");
            button.interactable=canAct&&!submitted&&!actions[i].disabled;RefreshDockActionColor(button);
        }
        return true;
    }
}
