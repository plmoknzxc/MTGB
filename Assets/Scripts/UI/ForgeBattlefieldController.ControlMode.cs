using System.Linq;
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
            var existing=surface.Find("PriorityControlMode");
            if(existing!=null){
                controlModeButton=existing.GetComponent<Button>()??existing.gameObject.AddComponent<Button>();
                controlModeButton.targetGraphic=existing.GetComponent<Image>();
                controlModeButton.onClick.RemoveAllListeners();controlModeButton.onClick.AddListener(CycleControlMode);
            }
        }
        if(controlModeButton==null) {
            controlModeButton=Button(surface,"PriorityControlMode","AUTO",Vector2.zero,new Vector2(148,41),CycleControlMode,Ink);
            var rect=(RectTransform)controlModeButton.transform;
            rect.anchorMin=rect.anchorMax=new Vector2(1,0);rect.anchoredPosition=new Vector2(-120,58.5f);
            controlModeButton.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(148,41,20);
            var track=Panel(rect,"Switch",new Vector2(-50,0),new Vector2(28,16),Ink);track.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(28,16,8);track.GetComponent<Image>().raycastTarget=false;
            var knob=Panel(track,"Knob",new Vector2(-6,0),new Vector2(12,12),new Color(.88f,.91f,.90f));knob.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(12,12,6);knob.GetComponent<Image>().raycastTarget=false;
        }
        var text=controlModeButton.GetComponentInChildren<Text>();text.text=ControlMode==ForgeControlMode.On?TapPaymentText("手动","Manual","手動"):ControlMode==ForgeControlMode.Off?TapPaymentText("略过","Skip","スキップ"):TapPaymentText("自动","Auto","自動");PreferenceText.SetSize(text,14);
        text.rectTransform.anchoredPosition=new Vector2(16,0);text.rectTransform.sizeDelta=new Vector2(100,35);
        text.horizontalOverflow=HorizontalWrapMode.Overflow;
        text.resizeTextForBestFit=false;
        controlModeButton.GetComponent<Image>().color=new Color(.09f,.153f,.196f);
        var toggle=controlModeButton.transform.Find("Switch");toggle.GetComponent<Image>().color=ControlMode==ForgeControlMode.Off?new Color(.416f,.475f,.49f):new Color(.16f,.5f,.58f);
        ForgeUiMotion.On((RectTransform)toggle.Find("Knob")).MoveTo(new Vector2(ControlMode==ForgeControlMode.Off?-6:6,0),new Vector2(12,12),seconds:.15f);
        var outline=controlModeButton.GetComponent<Outline>();if(outline!=null)outline.effectColor=ControlMode==ForgeControlMode.Off?new Color(.57f,.66f,.69f,.55f):new Color(.55f,.87f,.93f,.85f);
        controlModeButton.interactable=true;
    }
    private void CycleControlMode()
    {
        var next=(ForgeControlMode)(((int)ControlMode+1)%3);
        if(service!=null)service.ControlMode=next;else previewControlMode=next;
        automaticRequest=0;
        renderedKey=null;PresentControlMode();
    }
    private void UpdateAutomaticControl()
    {
        if(viewingBattlefield || service==null || !canAct || submitted || GameSettingsPanel.IsOpen || view?.awaitingInput!=true)return;
        int completed=CompletedSingleCardOption(view.request);
        if(completed>=0){Choose(completed,requestId);return;}
        // Resolution-time/reconnected payments need an explicit decision, even
        // when ordinary casting has automatic payment enabled.
        if(IsManaPaymentDecision)return;
        if(automaticRequest!=requestId){automaticRequest=requestId;automaticReadyAt=Time.unscaledTime+.06f;}
        if(Time.unscaledTime<automaticReadyAt || view.request.kind=="InputPassPriority"&&Time.unscaledTime<phaseVisibleUntil)return;
        long context=view.request.contextId>0?view.request.contextId:requestId;
        int option=ForgeControlPolicy.AutomaticOption(view,seat,ControlMode,attemptedPaymentContext==context);
        if(option<0)return;
        if(view.request.kind.Contains("PayMana"))attemptedPaymentContext=context;
        Choose(option,requestId);
    }
}
