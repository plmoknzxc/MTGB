using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private Button controlModeButton;
    private ForgeControlMode previewControlMode=ForgeControlMode.Auto;
    private long automaticRequest, attemptedPaymentContext;
    private float automaticReadyAt;
    private ForgeControlMode ControlMode=>service!=null?service.ControlMode:previewControlMode;

    private void PresentControlMode()
    {
        if(!integrated)return;
        if(service?.IsSpectator==true) { if(controlModeButton!=null)controlModeButton.gameObject.SetActive(false); return; }
        if(controlModeButton==null) {
            controlModeButton=Button(surface,"PriorityControlMode","AUTO",Vector2.zero,new Vector2(64,64),CycleControlMode,Ink);
            var rect=(RectTransform)controlModeButton.transform;
            rect.anchorMin=rect.anchorMax=new Vector2(1,0);rect.anchoredPosition=new Vector2(-380,130);
            controlModeButton.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(64,64,32);
        }
        var text=controlModeButton.GetComponentInChildren<Text>();text.text=ControlMode==ForgeControlMode.On?"ON":ControlMode==ForgeControlMode.Off?"OFF":"AUTO";text.fontSize=16;
        text.horizontalOverflow=HorizontalWrapMode.Overflow;
        text.resizeTextForBestFit=true;text.resizeTextMinSize=12;text.resizeTextMaxSize=16;
        controlModeButton.GetComponent<Image>().color=ControlMode==ForgeControlMode.On?new Color(.55f,.31f,.08f):ControlMode==ForgeControlMode.Off?new Color(.22f,.25f,.29f):new Color(.17f,.37f,.62f);
        controlModeButton.interactable=true;
    }
    private void CycleControlMode()
    {
        var next=(ForgeControlMode)(((int)ControlMode+1)%3);
        if(service!=null)service.ControlMode=next;else previewControlMode=next;
        renderedKey=null;PresentControlMode();
    }
    private void UpdateAutomaticControl()
    {
        if(service==null || !canAct || submitted || GameSettingsPanel.IsOpen || view?.awaitingInput!=true)return;
        if(automaticRequest!=requestId){automaticRequest=requestId;automaticReadyAt=Time.unscaledTime+.15f;}
        if(Time.unscaledTime<automaticReadyAt)return;
        long context=view.request.contextId>0?view.request.contextId:requestId;
        int option=ForgeControlPolicy.AutomaticOption(view,seat,ControlMode,attemptedPaymentContext==context);
        if(option<0)return;
        if(view.request.kind.Contains("PayMana"))attemptedPaymentContext=context;
        Choose(option,requestId);
    }
}
