using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform manaPaymentActions;
    private TMPro.TextMeshProUGUI manaPaymentPrompt;
    private RectTransform choiceHintPanel;
    private bool IsManaPaymentDecision => integrated && view?.awaitingInput==true
        && view.request?.kind?.Contains("PayMana")==true;

    // Native ok performs Forge's auto payment; native cancel declines this cost.
    // Missing options remain disabled. Never estimate affordability from lands/pool.
    private void PresentManaPaymentActions()
    {
        var phaseTrack=Node("CornerCluster/ControlRow").Find("PhaseTrack");
        if(phaseTrack!=null)phaseTrack.gameObject.SetActive(true);
        if(!IsManaPaymentDecision) {
            if(manaPaymentActions!=null)manaPaymentActions.gameObject.SetActive(false);
            return;
        }
        if(manaPaymentActions==null) {
            manaPaymentActions=Rect(Node("CornerCluster/ControlRow"),"ManaPaymentActions",new Vector2(48,-20),new Vector2(184,48));
            foreach(var name in new[]{"CancelPayment","AutoPayment"}) {
                var button=Button(manaPaymentActions,name,"",new Vector2(name=="CancelPayment"?47.5f:-47.5f,0),new Vector2(89,42),()=>{},Ink);
                button.GetComponent<Image>().color=Color.clear;
                var face=Rect(button.transform,"ActionFace",Vector2.zero,new Vector2(89,42));face.SetAsFirstSibling();
                var graphic=face.gameObject.AddComponent<ForgeActionButtonGraphic>();
                graphic.Secondary=name=="CancelPayment";graphic.raycastTarget=true;button.targetGraphic=graphic;
                var outline=button.GetComponent<Outline>();if(outline!=null)outline.enabled=false;
                var text=button.GetComponentInChildren<Text>();text.rectTransform.sizeDelta=new Vector2(83,40);
                text.color=graphic.Secondary?Color.white:new Color(.13f,.11f,.075f);
                text.raycastTarget=false;text.resizeTextForBestFit=false;PreferenceText.SetSize(text,12);
                var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.08f,1.08f,1.08f);colors.pressedColor=new Color(.8f,.8f,.8f);colors.disabledColor=new Color(.42f,.42f,.42f);button.colors=colors;
            }
        }
        manaPaymentActions.gameObject.SetActive(true);
        BindManaPaymentButton("CancelPayment","cancel",TapPaymentText("取消","Cancel","キャンセル"));
        BindManaPaymentButton("AutoPayment","ok",TapPaymentText("下一步","Next","次へ"));
    }
    // Payment and direct board/hand selections share the existing mana hint
    // position, independent of the lower-right phase/action hierarchy.
    private void PresentChoiceHint()
    {
        string message=null;
        var request=view?.request;
        if(view?.awaitingInput==true&&request!=null) {
            if(IsManaPaymentDecision) {
                string cost=request.remainingManaCost;
                if(string.IsNullOrEmpty(cost))cost=Regex.Match(request.message??"",@"Pay Mana Cost:\s*([^\r\n]+)").Groups[1].Value.Trim();
                if(!string.IsNullOrEmpty(cost))message=TapPaymentText("支付 ","Pay ","支払う：")+cost+
                    (request.reservedLifePayment>0?TapPaymentText(" · 已选生命 "," · Life selected "," ・ライフ選択：")+request.reservedLifePayment:"");
            } else if(request.selectionPurpose=="costConfirmation") {
                string source=request.sourceCard?.name;
                message=(string.IsNullOrEmpty(source)?"":GamePreferences.CardFaceName(source)+"\n")+ValueChoicePrompt(out _);
            } else if(DirectHandSelection) {
                message=SelectionInstruction;
                if(!string.IsNullOrEmpty(SelectionProgress))message+="\n"+SelectionProgress;
            } else if(request.kind=="ChooseNumber"||landChoiceMenu!=null&&landChoiceMenu.gameObject.activeInHierarchy&&landChoiceMenu.Find("NumericValue")!=null) {
                if(request.message=="one: How many additional votes do you want?"||request.message=="How many additional votes do you want?")
                    message=TapPaymentText("额外投票数","Additional votes","追加の投票数");
                else if(Regex.Match(request.message??"",@"^one: Life Total: Player (\d+)$") is Match life&&life.Success&&int.TryParse(life.Groups[1].Value,out int recipient))
                    message=TapPaymentText("生命总值 · ","Life total · ","ライフ総量 · ")+PlayerDisplayName(recipient);
                else message=RandomChoiceTitle()??NativeDisplayText(request.message);
            }
        }
        if(string.IsNullOrWhiteSpace(message)) {
            if(choiceHintPanel!=null)choiceHintPanel.gameObject.SetActive(false);
            return;
        }
        if(choiceHintPanel==null) {
            choiceHintPanel=Panel(surface,"ChoiceHintPanel",new Vector2(0,65),new Vector2(644,68),new Color(.025f,.045f,.065f,.94f));
            choiceHintPanel.GetComponent<Image>().raycastTarget=false;
            var outline=choiceHintPanel.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.77f,.59f,.28f,.85f);outline.effectDistance=new Vector2(1,-1);
            manaPaymentPrompt=CardSymbolText.Create(choiceHintPanel,"ManaPaymentPrompt",20,Paper);
            manaPaymentPrompt.alignment=TMPro.TextAlignmentOptions.Center;
            manaPaymentPrompt.textWrappingMode=TMPro.TextWrappingModes.Normal;
            manaPaymentPrompt.enableAutoSizing=false;manaPaymentPrompt.raycastTarget=false;
        }
        choiceHintPanel.gameObject.SetActive(true);
        float width=Mathf.Min(644,surface.rect.width-48);
        manaPaymentPrompt.fontSize=20*GamePreferences.TextScale;
        manaPaymentPrompt.text=CardSymbolText.Format(message);
        float height=Mathf.Max(44,manaPaymentPrompt.GetPreferredValues(manaPaymentPrompt.text,width-32,Mathf.Infinity).y+6);
        choiceHintPanel.sizeDelta=new Vector2(width,height+24);
        manaPaymentPrompt.rectTransform.sizeDelta=new Vector2(width-32,height);
        // Fix the anchor explicitly: neither action-dock reparenting nor hand
        // ownership can move the explanation into the phase label again.
        choiceHintPanel.anchorMin=choiceHintPanel.anchorMax=choiceHintPanel.pivot=new Vector2(.5f,.5f);
        choiceHintPanel.anchoredPosition=new Vector2(0,65);
        choiceHintPanel.SetAsLastSibling();
    }

    private void BindManaPaymentButton(string name,string optionId,string label)
    {
        var button=manaPaymentActions.Find(name).GetComponent<Button>();
        int index=OptionIndex(optionId);long captured=requestId;
        button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>Choose(index,captured));
        button.GetComponentInChildren<Text>().text=label;
        button.interactable=canAct && !submitted && index>=0 && !view.request.options[index].disabled;
        RefreshDockActionColor(button);
    }
}
