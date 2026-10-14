using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private bool viewingBattlefield;
    private string battlefieldViewContext;
    private Button viewBattlefieldButton, returnSelectionButton;
    private readonly Dictionary<CanvasGroup,(float alpha,bool interactable,bool raycasts)> hiddenBattlefieldWindows
        =new Dictionary<CanvasGroup,(float,bool,bool)>();

    // Keep the windows active: hiding must not discard page, scroll, input text,
    // native selections, or the next snapshot's update to the same decision.
    private IEnumerable<RectTransform> BattlefieldWindows()
    {
        if(landChoiceMenu!=null&&landChoiceMenu.parent==surface)yield return landChoiceMenu;
        yield return choiceDismiss;yield return cardSelectionOverlay;
        yield return abilityOrder;yield return allocationPanel;yield return mulliganChoices;
        yield return noticeHistory;yield return noticeToast;yield return randomResultRoot;
        yield return endDialog;yield return stackList;
        yield return targetOrigin;
        yield return targetChoiceActions;
        yield return choiceHintPanel;
        foreach(var window in cardNoticeWindows.Values)yield return window;
        if(targetArrow!=null)yield return targetArrow.transform as RectTransform;
        if(integrated && actionRoot!=null)yield return actionRoot.parent.parent as RectTransform;
    }
    private string BattlefieldDecisionContext => seat+"/"+(service!=null?service.MatchId:"")+"/"+
        (view?.awaitingInput==true&&view.request!=null
            ? view.request.kind+"/"+(view.request.contextId>0?view.request.contextId:view.request.requestId).ToString()
            : "idle");

    private void LateUpdate(){RefreshBattlefieldViewControls();RefreshAttachmentLinks();}

    private void RefreshBattlefieldViewControls()
    {
        if(surface==null)return;
        if(viewingBattlefield && battlefieldViewContext!=BattlefieldDecisionContext && view?.inputPending!=true)
            RestoreBattlefieldWindows();
        var windows=BattlefieldWindows().Where(w=>w!=null&&w.gameObject.activeInHierarchy).Distinct().ToArray();
        if(viewingBattlefield && windows.Length==0)RestoreBattlefieldWindows();
        if(viewBattlefieldButton==null) {
            viewBattlefieldButton=Button(surface,"ViewBattlefield",TapPaymentText("查看战场","View battlefield","戦場を見る"),Vector2.zero,new Vector2(220,44),ViewBattlefield,Ink);
            returnSelectionButton=Button(surface,"ReturnSelection",TapPaymentText("返回选择","Return to choice","選択に戻る"),Vector2.zero,new Vector2(220,44),RestoreBattlefieldWindows,Ink);
            foreach(var b in new[]{viewBattlefieldButton,returnSelectionButton}) {
                b.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(220,44,8);
                PreferenceText.SetSize(b.GetComponentInChildren<Text>(),18);
                var rect=(RectTransform)b.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            }
        }
        viewBattlefieldButton.GetComponentInChildren<Text>().text=TapPaymentText("查看战场","View battlefield","戦場を見る");
        returnSelectionButton.GetComponentInChildren<Text>().text=TapPaymentText("返回选择","Return to choice","選択に戻る");
        var viewRect=(RectTransform)viewBattlefieldButton.transform;
        var returnRect=(RectTransform)returnSelectionButton.transform;
        if(integrated) {
            var gear=(RectTransform)Node("GearBox/SettingsButton");
            var corners=new Vector3[4];gear.GetWorldCorners(corners);
            var right=surface.InverseTransformPoint(corners[2]);
            returnRect.anchoredPosition=new Vector2(right.x-surface.rect.xMin+12,right.y-surface.rect.yMax-2);
        }else returnRect.anchoredPosition=new Vector2(24,-78);
        // One stable corner slot for viewing, returning, and leaving end review.
        viewRect.anchoredPosition=returnRect.anchoredPosition;
        if(leaveReviewButton!=null) {
            var leaveRect=(RectTransform)leaveReviewButton.transform;
            leaveRect.SetParent(surface,false);
            leaveRect.anchorMin=leaveRect.anchorMax=leaveRect.pivot=new Vector2(0,1);
            leaveRect.anchoredPosition=returnRect.anchoredPosition;leaveRect.sizeDelta=returnRect.sizeDelta;
            leaveReviewButton.gameObject.SetActive(!viewingBattlefield&&windows.Length==0&&endDialog!=null&&!endDialog.gameObject.activeSelf);
        }
        viewBattlefieldButton.gameObject.SetActive(!viewingBattlefield&&windows.Length>0);
        returnSelectionButton.gameObject.SetActive(viewingBattlefield);
        if(noticeHistoryButton!=null)((RectTransform)noticeHistoryButton.transform).anchoredPosition=new Vector2(windows.Length>0?360:128,-48);
        if(viewingBattlefield)foreach(var window in windows) {
            var group=window.GetComponent<CanvasGroup>();
            if(group==null)group=window.gameObject.AddComponent<CanvasGroup>();
            if(!hiddenBattlefieldWindows.ContainsKey(group))hiddenBattlefieldWindows.Add(group,(group.alpha,group.interactable,group.blocksRaycasts));
            group.alpha=0;group.interactable=false;group.blocksRaycasts=false;
        }
        viewBattlefieldButton.transform.SetAsLastSibling();returnSelectionButton.transform.SetAsLastSibling();
    }
    public void ViewBattlefield()
    {
        if(viewingBattlefield||!BattlefieldWindows().Any(w=>w!=null&&w.gameObject.activeInHierarchy))return;
        viewingBattlefield=true;battlefieldViewContext=BattlefieldDecisionContext;
        RefreshBattlefieldViewControls();
    }
    public void RestoreBattlefieldWindows()
    {
        foreach(var pair in hiddenBattlefieldWindows)if(pair.Key!=null) {
            pair.Key.alpha=pair.Value.alpha;pair.Key.interactable=pair.Value.interactable;pair.Key.blocksRaycasts=pair.Value.raycasts;
        }
        hiddenBattlefieldWindows.Clear();viewingBattlefield=false;battlefieldViewContext=null;
        if(returnSelectionButton!=null)returnSelectionButton.gameObject.SetActive(false);
    }
}
