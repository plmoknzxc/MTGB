using System;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform targetChoiceActions;
    // Preserve the original option indices; only the visual order changes.
    private static int ChoiceActionOrder(ForgeOption option) => option.id is "ok" or "confirm-cards" or "confirm-modes" or "confirm-number" ? 0
        : option.id == "cancel" ? 2 : 1;
    private string ChoiceActionLabel(ForgeOption option) => option.id is "ok" or "confirm-cards" or "confirm-modes" or "confirm-number"
        ? TapPaymentText("下一步","Next","次へ")
        : option.id=="reset-cards" ? TapPaymentText("重新选择","Reselect","選び直す") : RequestOptionLabel(option);

    private bool HasTargetChoice => integrated && view?.awaitingInput == true &&
        (view.request?.kind is "InputSelectTargets" or "SelectStackTarget" || view.request?.selectionPurpose is "costConfirmation" or "costScalar");

    // Some native callbacks use cancel as an ordinal (Blue, Bottom, No...).
    // Only an explicitly labelled cancellation is safe for the global shortcut.
    private int PresentedCancelOption
    {
        get {
            if (!integrated || view?.awaitingInput != true || view.request == null) return -1;
            int index = OptionIndex("cancel");
            if (index < 0 || view.request.options[index].disabled) return -1;
            string label = (view.request.options[index].label ?? "").Trim();
            return label.Equals("Cancel", StringComparison.OrdinalIgnoreCase) || label == "取消" || label == "取消施放" || label == "取消支付" || label == "キャンセル"
                ? index : -1;
        }
    }

    public bool TryCancelPresentedChoice()
    {
        if (!canAct || submitted) return false;
        int index = PresentedCancelOption;
        if (index < 0) return false;
        if (viewingBattlefield) RestoreBattlefieldWindows();
        return TrySubmitPresentedOption(index, requestId);
    }
    private void UpdateChoiceCancelShortcut()
    {
#if ENABLE_INPUT_SYSTEM
        bool right = UnityEngine.InputSystem.Mouse.current?.rightButton.wasPressedThisFrame == true;
#else
        bool right = Input.GetMouseButtonDown(1);
#endif
        if (right) TryCancelPresentedChoice();
    }

    private void PresentTargetChoiceActions()
    {
        if (!HasTargetChoice || HasStackTargetChoices || IsCardPickerRequest) {
            if (targetChoiceActions != null) targetChoiceActions.gameObject.SetActive(false);
            return;
        }
        if (targetChoiceActions == null) {
            targetChoiceActions = Rect(Node("CornerCluster/ControlRow"), "TargetChoiceActions", new Vector2(48,-20), new Vector2(184,48));
            SetOverlayLayer(targetChoiceActions, DecisionLayerOffset+1, true);
            DockAction(targetChoiceActions,"CancelTargetChoice","",new Vector2(47.5f,0),89,()=>{},true);
            DockAction(targetChoiceActions,"NextTargetChoice","",new Vector2(-47.5f,0),89,()=>{},false);
        }
        targetChoiceActions.gameObject.SetActive(true);
        advanceButton.gameObject.SetActive(false);
        int cancel = PresentedCancelOption, next = OptionIndex("ok");
        long captured = requestId;
        var decline = targetChoiceActions.Find("CancelTargetChoice").GetComponent<Button>();
        decline.gameObject.SetActive(cancel >= 0);
        decline.GetComponentInChildren<Text>().text = TapPaymentText("取消","Cancel","キャンセル");
        decline.onClick.RemoveAllListeners();decline.onClick.AddListener(()=>Choose(cancel,captured));
        decline.interactable = canAct && !submitted && cancel >= 0;
        var advance = targetChoiceActions.Find("NextTargetChoice").GetComponent<Button>();
        advance.GetComponentInChildren<Text>().text = TapPaymentText("下一步","Next","次へ");
        advance.onClick.RemoveAllListeners();advance.onClick.AddListener(()=>Choose(next,captured));
        advance.interactable = canAct && !submitted && next >= 0 && !view.request.options[next].disabled;
        // Preserve the selected font size: localized captions determine the
        // paired control widths, anchored to the same right edge as before.
        float Fit(Button button,float minimum){
            var label=button.GetComponentInChildren<Text>();label.horizontalOverflow=HorizontalWrapMode.Overflow;
            float width=Mathf.Max(minimum,Mathf.Ceil(label.preferredWidth)+16);
            var rect=(RectTransform)button.transform;rect.sizeDelta=new Vector2(width,54);
            ((RectTransform)rect.Find("Face")).sizeDelta=new Vector2(width,54);
            label.rectTransform.sizeDelta=new Vector2(width-8,50);label.horizontalOverflow=HorizontalWrapMode.Wrap;
            return width;
        }
        float advanceWidth=Fit(advance,cancel>=0?89:184),declineWidth=cancel>=0?Fit(decline,89):0;
        float total=advanceWidth+(cancel>=0?declineWidth+6:0);
        targetChoiceActions.sizeDelta=new Vector2(total,54);
        targetChoiceActions.anchoredPosition=new Vector2(140-total*.5f,-20);
        ((RectTransform)advance.transform).anchoredPosition=new Vector2(-total*.5f+advanceWidth*.5f,0);
        ((RectTransform)decline.transform).anchoredPosition=new Vector2(total*.5f-declineWidth*.5f,0);
        RefreshDockActionColor(advance);RefreshDockActionColor(decline);
    }
}
