using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private Button controlModeButton,pauseNextPhaseButton;
    private ForgeControlMode previewControlMode=ForgeControlMode.Auto;
    private long automaticRequest, attemptedPaymentContext;
    private float automaticReadyAt;
    private const float StackReadSeconds=1f, StackResultSeconds=.35f;
    private string readableStackKey, readableStackMatch;
    private int readableStackSeat, readableStackRestart;
    private float readableStackUntil;
    private ForgeControlMode ControlMode=>service!=null?service.ControlMode:previewControlMode;
    private bool pauseNextPhase;
    private string pausePhase,pauseMatch;
    private int pauseTurn,pauseTurnSeat,pauseSeat,pauseRestart;

    private float LayoutControlPill(Button button,string caption,string[] captions,float minimumWidth)
    {
        var text=button.GetComponentInChildren<Text>();PreferenceText.SetSize(text,18);
        text.resizeTextForBestFit=false;text.horizontalOverflow=HorizontalWrapMode.Overflow;text.alignment=TextAnchor.MiddleCenter;
        float width=minimumWidth;
        foreach(string value in captions){text.text=value;width=Mathf.Max(width,Mathf.Ceil(text.preferredWidth)+24);}
        text.text=caption;text.rectTransform.anchoredPosition=Vector2.zero;text.rectTransform.sizeDelta=new Vector2(width-18,37);
        var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(width,41);
        button.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(width,41,20);
        return width;
    }
    private void PresentControlMode()
    {
        if(!integrated)return;
        if(service?.IsSpectator==true) {
            pauseNextPhase=false;
            if(controlModeButton!=null)controlModeButton.gameObject.SetActive(false);
            else {var authoredMode=surface.Find("PriorityControlMode");if(authoredMode!=null)authoredMode.gameObject.SetActive(false);}
            if(pauseNextPhaseButton!=null)pauseNextPhaseButton.gameObject.SetActive(false);
            return;
        }
        if(controlModeButton==null) {
            var existing=surface.Find("PriorityControlMode");
            if(existing!=null){
                controlModeButton=existing.GetComponent<Button>()??existing.gameObject.AddComponent<Button>();
                controlModeButton.targetGraphic=existing.GetComponent<Image>();
                controlModeButton.onClick.RemoveAllListeners();controlModeButton.onClick.AddListener(CycleControlMode);
            } else controlModeButton=Button(surface,"PriorityControlMode","",Vector2.zero,new Vector2(80,41),CycleControlMode,Ink);
            var toggle=controlModeButton.transform.Find("Switch");
            if(toggle!=null){toggle.gameObject.SetActive(false);Destroy(toggle.gameObject);}
        }
        if(pauseNextPhaseButton==null)pauseNextPhaseButton=Button(surface,"PauseNextPhase","",Vector2.zero,new Vector2(142,41),ToggleNextPhasePause,Ink);
        controlModeButton.gameObject.SetActive(true);pauseNextPhaseButton.gameObject.SetActive(true);
        string manual=TapPaymentText("手动","Manual","手動"),auto=TapPaymentText("自动","Auto","自動"),skip=TapPaymentText("略过","Skip","スキップ");
        float modeWidth=LayoutControlPill(controlModeButton,ControlMode==ForgeControlMode.On?manual:ControlMode==ForgeControlMode.Off?skip:auto,new[]{manual,auto,skip},80);
        string pause=TapPaymentText("下阶段暂停","Pause next","次で停止");
        float pauseWidth=LayoutControlPill(pauseNextPhaseButton,pause,new[]{pause},130);
        ((RectTransform)controlModeButton.transform).anchoredPosition=new Vector2(-46-modeWidth*.5f,58.5f);
        ((RectTransform)pauseNextPhaseButton.transform).anchoredPosition=new Vector2(-46-modeWidth-10-pauseWidth*.5f,58.5f);
        controlModeButton.GetComponent<Image>().color=new Color(.09f,.153f,.196f);
        var outline=controlModeButton.GetComponent<Outline>();if(outline!=null)outline.effectColor=ControlMode==ForgeControlMode.Off?new Color(.57f,.66f,.69f,.55f):new Color(.55f,.87f,.93f,.85f);
        // Only the explicit one-shot appointment owns the selected appearance.
        // Hover/keyboard focus must not look like an armed phase stop.
        pauseNextPhaseButton.GetComponent<Image>().color=pauseNextPhase?Gold:new Color(.09f,.12f,.14f);
        pauseNextPhaseButton.GetComponentInChildren<Text>().color=pauseNextPhase?Ink:new Color(.64f,.69f,.71f);
        var pauseColors=pauseNextPhaseButton.colors;
        pauseColors.normalColor=pauseColors.selectedColor=Color.white;
        pauseColors.highlightedColor=new Color(1.12f,1.12f,1.12f);
        pauseColors.pressedColor=new Color(.8f,.8f,.8f);
        pauseNextPhaseButton.colors=pauseColors;
        var pauseOutline=pauseNextPhaseButton.GetComponent<Outline>()??pauseNextPhaseButton.gameObject.AddComponent<Outline>();
        pauseOutline.effectColor=pauseNextPhase?Gold:new Color(.4f,.48f,.52f,.5f);pauseOutline.effectDistance=new Vector2(1,-1);
        pauseNextPhaseButton.interactable=pauseNextPhase||view!=null&&view.turn>0&&!string.IsNullOrEmpty(view.phase);
        controlModeButton.interactable=true;
    }
    private void SetControlMode(ForgeControlMode mode)
    {
        if(service!=null)service.ControlMode=mode;else previewControlMode=mode;
        automaticRequest=0;renderedKey=null;
    }
    private void CycleControlMode()
    {
        SetControlMode((ForgeControlMode)(((int)ControlMode+1)%3));
        PresentControlMode();RefreshPriorityAdvanceVisibility();
        PresentRedesignLayout();
    }
    private void ToggleNextPhasePause()
    {
        if(pauseNextPhase)pauseNextPhase=false;
        else {
            if(view==null||view.turn<=0||string.IsNullOrEmpty(view.phase)||service?.IsSpectator==true)return;
            pauseNextPhase=true;pausePhase=view.phase;pauseTurn=view.turn;pauseTurnSeat=view.turnSeat;
            pauseSeat=seat;pauseRestart=view.restartRevision;pauseMatch=service?.MatchId;
        }
        renderedKey=null;PresentControlMode();
        PresentRedesignLayout();
    }
    private void ObserveNextPhasePause(ForgeSeatView state,int localSeat)
    {
        if(!pauseNextPhase)return;
        if(state==null||localSeat!=pauseSeat||state.restartRevision!=pauseRestart||service?.MatchId!=pauseMatch||service?.IsSpectator==true){pauseNextPhase=false;return;}
        if(state.inputPending||state.turn<=0||string.IsNullOrEmpty(state.phase))return;
        if(state.turn==pauseTurn&&state.turnSeat==pauseTurnSeat&&state.phase==pausePhase)return;
        // Consume the one-shot stop before presenting or automatically answering
        // any request in the newly observed phase, including opponent priority.
        pauseNextPhase=false;SetControlMode(ForgeControlMode.On);
    }
    // The button and the automatic submitter must agree before the first frame
    // of a new request. Re-evaluate the current mode, never cache a planned pass.
    private int AutomaticPriorityOption()
    {
        if(view?.request?.kind!="InputPassPriority")return -1;
        int option=ForgeControlPolicy.AutomaticOption(view,seat,ControlMode,false);
        return option>=0&&!view.request.options[option].disabled?option:-1;
    }
    private void RefreshPriorityAdvanceVisibility()
    {
        if(advanceButton==null || view?.request?.kind!="InputPassPriority")return;
        advanceButton.gameObject.SetActive(!submitted&&OptionIndex("ok")>=0&&AutomaticPriorityOption()<0);
    }
    private void ObserveStackReadWindow(ForgeSeatView state,int localSeat)
    {
        if(state==null){readableStackKey=null;readableStackUntil=0;return;}
        string match=service?.MatchId;
        if(localSeat!=readableStackSeat || state.restartRevision!=readableStackRestart || match!=readableStackMatch){
            readableStackKey=null;readableStackUntil=0;
        }
        readableStackSeat=localSeat;readableStackRestart=state.restartRevision;readableStackMatch=match;
        // Track stack incarnations/order, not request IDs: priority changes and
        // preference redraws must not keep restarting the same reading window.
        int count=Mathf.Max(state.stackCount,Mathf.Max(state.stackItems?.Length??0,state.stack?.Length??0));
        string key=count==0?"":count+":"+string.Join("|",Enumerable.Range(0,count).Select(i=>
            i<(state.stackItems?.Length??0)&&state.stackItems[i]!=null
                ?"id:"+state.stackItems[i].id
                :"text:"+(i<(state.stack?.Length??0)?state.stack[i]:i.ToString())));
        if(key==readableStackKey)return;
        bool hadStack=!string.IsNullOrEmpty(readableStackKey);
        readableStackKey=key;
        readableStackUntil=Time.unscaledTime+(count>0?StackReadSeconds:hadStack?StackResultSeconds:0);
    }
    private bool StackReadWindowOpen=>Time.unscaledTime<readableStackUntil ||
        !string.IsNullOrEmpty(readableStackKey)&&(hoveredStack!=null||stackBrowseOpen);
    private void UpdateAutomaticControl()
    {
        if(viewingBattlefield || service==null || !canAct || submitted || GameSettingsPanel.IsOpen || view?.awaitingInput!=true)return;
        int completed=CompletedSingleCardOption(view.request);
        if(completed>=0){Choose(completed,requestId);return;}
        // Resolution-time/reconnected payments need an explicit decision, even
        // when ordinary casting has automatic payment enabled.
        if(IsManaPaymentDecision)return;
        if(automaticRequest!=requestId){automaticRequest=requestId;automaticReadyAt=Time.unscaledTime+.06f;}
        if(Time.unscaledTime<automaticReadyAt || view.request.kind=="InputPassPriority"&&
            (Time.unscaledTime<phaseVisibleUntil || StackReadWindowOpen))return;
        long context=view.request.contextId>0?view.request.contextId:requestId;
        int option=view.request.kind=="InputPassPriority"?AutomaticPriorityOption():ForgeControlPolicy.AutomaticOption(view,seat,ControlMode,attemptedPaymentContext==context);
        if(option<0)return;
        if(view.request.kind.Contains("PayMana"))attemptedPaymentContext=context;
        Choose(option,requestId);
    }
}
