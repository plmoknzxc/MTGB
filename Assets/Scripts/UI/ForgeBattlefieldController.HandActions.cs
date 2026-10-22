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
    // Native list selection already stages cards and publishes its own OK.
    // A second local draft would require confirming the same card twice.
    private bool HasNativeHandDraft=>view?.request?.paymentMode!="manual" && view?.request?.kind is "InputSelectCardsFromList" or "InputSelectEntitiesFromList";
    private bool IsOptionalHandSelection=>!HasNativeHandDraft && view?.request?.maxCount==1 && view.request.options.Any(o=>o.id=="cancel") &&
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
        var button=Button(parent,name,label,position,new Vector2(width,54),action,Color.clear);
        var face=Rect(button.transform,"Face",Vector2.zero,new Vector2(width,54));face.SetAsFirstSibling();
        var graphic=face.gameObject.AddComponent<ForgeActionButtonGraphic>();graphic.Secondary=secondary;button.targetGraphic=graphic;
        var outline=button.GetComponent<Outline>();if(outline!=null)outline.enabled=false;
        var text=button.GetComponentInChildren<Text>();PreferenceText.SetSize(text,16);text.rectTransform.sizeDelta=new Vector2(width-8,50);text.raycastTarget=false;text.resizeTextForBestFit=false;text.color=secondary?Color.white:new Color(.13f,.11f,.075f);
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
        int actionCount=IsOptionalDirectHandSelection?2:actions.Length;
        float rowWidth=actionCount==1?184:actionCount*100+(actionCount-1)*6;
        handChoiceActions.sizeDelta=new Vector2(rowWidth,54);
        handChoiceActions.anchoredPosition=new Vector2(140-rowWidth*.5f,-20);
        handChoiceActions.gameObject.SetActive(true);advanceButton.gameObject.SetActive(false);
        SetOverlayLayer(handChoiceActions,DecisionLayerOffset+1,true);
        if(IsOptionalDirectHandSelection) {
            long captured=requestId;int cancel=OptionIndex("cancel");
            var decline=DockAction(handChoiceActions,"HandAction_"+cancel,TapPaymentText("取消","Cancel","キャンセル"),new Vector2(53,0),100,()=>Choose(cancel,captured),true);
            decline.interactable=canAct&&!submitted&&cancel>=0&&!view.request.options[cancel].disabled;
            var confirm=DockAction(handChoiceActions,"ConfirmHandChoice",TapPaymentText("下一步","Next","次へ"),new Vector2(-53,0),100,()=>CommitHandChoice(captured),false);
            int selected=OptionIndex(stagedHandChoice);
            confirm.interactable=canAct&&!submitted&&(selected>=0&&!view.request.options[selected].disabled||CanSkipSingleCostDraft);
            RefreshDockActionColor(confirm);RefreshDockActionColor(decline);
            return true;
        }
        float width=actions.Length==1?184:100;
        var actionButtons=new Button[actions.Length];
        var actionWidths=new float[actions.Length];
        for(int i=0;i<actions.Length;i++) {
            int index=OptionIndex(actions[i].id);long captured=requestId;
            var button=DockAction(handChoiceActions,"HandAction_"+index,ChoiceActionLabel(actions[i]),Vector2.zero,width,()=>Choose(index,captured),actions[i].id=="cancel"||actions[i].id=="reset-cards");
            var text=button.GetComponentInChildren<Text>();
            float fittedWidth=Mathf.Max(width,Mathf.Ceil(text.preferredWidth)+12);
            ((RectTransform)button.transform).sizeDelta=new Vector2(fittedWidth,54);
            ((RectTransform)button.transform.Find("Face")).sizeDelta=new Vector2(fittedWidth,54);
            text.rectTransform.sizeDelta=new Vector2(fittedWidth-8,50);
            actionButtons[i]=button;actionWidths[i]=fittedWidth;
            button.interactable=canAct&&!submitted&&!actions[i].disabled;RefreshDockActionColor(button);
        }
        rowWidth=actionWidths.Sum()+(actions.Length-1)*6;
        handChoiceActions.sizeDelta=new Vector2(rowWidth,54);
        handChoiceActions.anchoredPosition=new Vector2(140-rowWidth*.5f,-20);
        float left=-rowWidth*.5f;
        for(int i=0;i<actionButtons.Length;i++) {
            ((RectTransform)actionButtons[i].transform).anchoredPosition=new Vector2(left+actionWidths[i]*.5f,0);
            left+=actionWidths[i]+6;
        }
        return true;
    }
}
