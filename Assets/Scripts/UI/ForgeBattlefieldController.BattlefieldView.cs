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
    private MenuThemeStyle fieldShortcutTheme;

    private void StyleFieldShortcut(Button button, bool returning)
    {
        if(button==null || HudTheme==null)return;
        MenuVisualTheme.StyleArchiveControl(button,returning);
        var label=button.GetComponentInChildren<Text>(true);
        MenuTypography.Use(label,MenuTypography.Face.Control);
        PreferenceText.SetSize(label,19);
        label.color=returning?HudTheme.ActionText:HudText;
        label.alignment=TextAnchor.MiddleCenter;
        label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
        label.rectTransform.offsetMin=new Vector2(30,1);label.rectTransform.offsetMax=new Vector2(-6,-1);
        var glyph=button.transform.Find("FieldShortcutMark")?.GetComponent<StackControlIcon>();
        if(glyph==null)glyph=Rect(button.transform,"FieldShortcutMark",Vector2.zero,new Vector2(16,16)).gameObject.AddComponent<StackControlIcon>();
        glyph.rectTransform.anchorMin=glyph.rectTransform.anchorMax=new Vector2(0,.5f);
        glyph.rectTransform.anchoredPosition=new Vector2(19,0);
        glyph.SetKind(returning?StackControlIcon.Kind.Left:StackControlIcon.Kind.View);
        glyph.color=label.color;glyph.SmoothEdges=true;glyph.raycastTarget=false;
    }
    private readonly Dictionary<CanvasGroup,(float alpha,bool interactable,bool raycasts)> hiddenBattlefieldWindows
        =new Dictionary<CanvasGroup,(float,bool,bool)>();
    private RectTransform fourTargetBackdrop;
    private readonly Dictionary<GameObject,bool> hiddenSelectionHandRoots=new();

    private void RefreshSelectionHandVisibility()
    {
        if (FourSeatInputBlocked) {
            draggingFourSeat = -1;
            draggingBattlefield = false;
            foreach (var handle in fourHandles) if (handle != null) handle.CancelDrag();
        }
        bool targets = IsFourPlayerScene && UsesTargetWindow && stackList!=null && stackList.gameObject.activeSelf && !viewingBattlefield;
        if (targets && fourTargetBackdrop==null) {
            fourTargetBackdrop=Panel(surface,"TargetSelectionBackdrop",Vector2.zero,Vector2.zero,new Color(.025f,.025f,.05f,.57f));
            fourTargetBackdrop.anchorMin=Vector2.zero;fourTargetBackdrop.anchorMax=Vector2.one;
            fourTargetBackdrop.offsetMin=fourTargetBackdrop.offsetMax=Vector2.zero;
            SetOverlayLayer(fourTargetBackdrop,DecisionLayerOffset-1,true);
        }
        if (fourTargetBackdrop!=null) fourTargetBackdrop.gameObject.SetActive(targets);
        bool modal=IsRedesignLayout&&!viewingBattlefield
            &&(cardSelectionOverlay!=null&&cardSelectionOverlay.gameObject.activeInHierarchy
                ||fourCombatRoot!=null&&fourCombatRoot.gameObject.activeInHierarchy);
        if(modal){
            // Hand meshes, separate cost labels, captions and backplates live
            // in different render layers. Disable their roots together while
            // the picker is up; canvas alpha alone leaves pieces on screen.
            foreach(var root in new[]{ownHand,otherHand,redesignHandBackplate,redesignOtherBackplate,handSourceTabs,otherSourceTabs,battleSidebar,sidebarToggle==null?null:(RectTransform)sidebarToggle.transform}){
                if(root==null)continue;
                var go=root.gameObject;
                if(!hiddenSelectionHandRoots.ContainsKey(go))hiddenSelectionHandRoots.Add(go,go.activeSelf);
                go.SetActive(false);
            }
        }else if(hiddenSelectionHandRoots.Count>0){
            foreach(var pair in hiddenSelectionHandRoots)if(pair.Key!=null)pair.Key.SetActive(pair.Value);
            hiddenSelectionHandRoots.Clear();
        }
    }

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
        yield return fourCombatRoot;
        if(fourPlayerScenePrepared)yield return fourTargetList;
        yield return targetChoiceActions;
        yield return choiceHintPanel;
        yield return handChoiceActions;
        if(handSelectionPrompt!=null)yield return handSelectionPrompt.rectTransform;
        foreach(var window in cardNoticeWindows.Values)yield return window;
        if(targetArrow!=null)yield return targetArrow.transform as RectTransform;
        if(integrated && actionRoot!=null)yield return actionRoot.parent.parent as RectTransform;
    }
    private string BattlefieldDecisionContext => seat+"/"+(service!=null?service.MatchId:"")+"/"+
        (view?.awaitingInput==true&&view.request!=null
            ? view.request.kind+"/"+(view.request.contextId>0?view.request.contextId:view.request.requestId).ToString()
            : "idle");

    private void LateUpdate(){RefreshBattlefieldViewControls();RefreshAttachmentLinks();RefreshInspectPosition();}

    private void RefreshBattlefieldViewControls()
    {
        if(surface==null)return;
        bool endReview=service?.EndReview!=null&&endDialog!=null;
        if(endReview&&viewingBattlefield)RestoreBattlefieldWindows();
        RefreshSelectionHandVisibility();
        var windows=BattlefieldWindows().Where(w=>w!=null&&w.gameObject.activeInHierarchy).Distinct().ToArray();
        if(viewingBattlefield && windows.Length==0)RestoreBattlefieldWindows();
        if(viewBattlefieldButton==null) {
            viewBattlefieldButton=Button(surface,"ViewBattlefield",TapPaymentText("查看战场","View battlefield","戦場を見る"),Vector2.zero,new Vector2(220,44),ViewBattlefield,Ink);
            returnSelectionButton=Button(surface,"ReturnSelection",TapPaymentText("返回选择","Return to choice","選択に戻る"),Vector2.zero,new Vector2(220,44),RestoreBattlefieldWindows,Ink);
            foreach(var b in new[]{viewBattlefieldButton,returnSelectionButton}) {
                SetOverlayLayer((RectTransform)b.transform,(HudTheme!=null?InspectLayerOffset:DecisionLayerOffset)+2,true);
                b.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(220,44,8);
                PreferenceText.SetSize(b.GetComponentInChildren<Text>(),18);
                var rect=(RectTransform)b.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            }
        }
        if(HudTheme!=fieldShortcutTheme){
            fieldShortcutTheme=HudTheme;
            StyleFieldShortcut(viewBattlefieldButton,false);
            StyleFieldShortcut(returnSelectionButton,true);
            StyleFieldShortcut(leaveReviewButton,true);
            StyleFieldShortcut(endViewFieldButton,false);
        }
        viewBattlefieldButton.GetComponentInChildren<Text>().text=TapPaymentText("查看战场","View battlefield","戦場を見る");
        returnSelectionButton.GetComponentInChildren<Text>().text=TapPaymentText("返回选择","Return to choice","選択に戻る");
        var viewRect=(RectTransform)viewBattlefieldButton.transform;
        var returnRect=(RectTransform)returnSelectionButton.transform;
        if(IsRedesignLayout) {
            // The stack reserves a dedicated row above its header for this
            // control, so it never competes with a selection window's title.
            const float buttonWidth=174;
            viewRect.sizeDelta=returnRect.sizeDelta=new Vector2(buttonWidth,46);
            returnRect.anchoredPosition=IsFourPlayerScene ? new Vector2(surface.rect.width-RedesignStackWidth-10,StackDockPosition.y+54)
                : new Vector2(surface.rect.width-RedesignStackWidth-28,StackDockPosition.y+60);
        }else if(integrated) {
            viewRect.sizeDelta=returnRect.sizeDelta=new Vector2(220,44);
            var gear=(RectTransform)Node("GearBox/SettingsButton");
            var corners=new Vector3[4];gear.GetWorldCorners(corners);
            var right=surface.InverseTransformPoint(corners[2]);
            returnRect.anchoredPosition=new Vector2(right.x-surface.rect.xMin+12,right.y-surface.rect.yMax-2);
        }else returnRect.anchoredPosition=new Vector2(24,-78);
        // One stable corner slot for viewing, returning, and leaving end review.
        viewRect.anchoredPosition=returnRect.anchoredPosition;
        if(leaveReviewButton!=null) {
            var caption=leaveReviewButton.GetComponentInChildren<Text>();
            caption.text=TapPaymentText("返回结算","Results menu","結果に戻る");
            var leaveRect=(RectTransform)leaveReviewButton.transform;
            leaveRect.SetParent(surface,false);
            leaveRect.anchorMin=leaveRect.anchorMax=leaveRect.pivot=new Vector2(0,1);
            leaveRect.anchoredPosition=returnRect.anchoredPosition;leaveRect.sizeDelta=returnRect.sizeDelta;
            if(HudTheme==null)caption.rectTransform.sizeDelta=new Vector2(leaveRect.rect.width-16,caption.rectTransform.sizeDelta.y);
            leaveReviewButton.gameObject.SetActive(endReview&&!endDialog.gameObject.activeSelf);
        }
        // End review has one entry in its result menu and a persistent corner
        // return action, including while browsing the read-only zones.
        var passiveActionRoot=integrated&&actionRoot!=null?actionRoot.parent.parent as RectTransform:null;
        bool hasWindow=windows.Any(w=>w!=passiveActionRoot&&w!=noticeHistory&&w!=noticeToast&&w!=randomResultRoot);
        viewBattlefieldButton.gameObject.SetActive(!endReview&&!viewingBattlefield&&(!IsRedesignLayout||hasWindow)&&windows.Length>0);
        returnSelectionButton.gameObject.SetActive(!endReview&&viewingBattlefield);
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
        if(service?.EndReview!=null&&endDialog!=null){ContinueEndReview();return;}
        if(viewingBattlefield||!BattlefieldWindows().Any(w=>w!=null&&w.gameObject.activeInHierarchy))return;
        viewingBattlefield=true;battlefieldViewContext=StackTargetContext;
        if(IsFourPlayerScene)PresentFourTargetList();
        if(IsRedesignLayout){ApplyRedesignHandOffset(opponentFan,0,true);FitFieldBetweenHands();}
        RefreshBattlefieldViewControls();
    }
    public void RestoreBattlefieldWindows()
    {
        foreach(var pair in hiddenBattlefieldWindows)if(pair.Key!=null) {
            pair.Key.alpha=pair.Value.alpha;pair.Key.interactable=pair.Value.interactable;pair.Key.blocksRaycasts=pair.Value.raycasts;
        }
        bool wasViewing=viewingBattlefield;
        hiddenBattlefieldWindows.Clear();viewingBattlefield=false;battlefieldViewContext=null;
        if(wasViewing&&FourWindowSelection){
            stackBrowseOpen=false;
            if(stackOverview!=null)stackOverview.gameObject.SetActive(false);
            if(browsedZone!=null){CloseReviewCards();PresentCardSelection();}
        }
        if(wasViewing&&IsFourPlayerScene)PresentFourTargetList();
        if(wasViewing&&IsRedesignLayout){ApplyRedesignHandOffset(opponentFan,0,true);FitFieldBetweenHands();}
        if(returnSelectionButton!=null)returnSelectionButton.gameObject.SetActive(false);
    }
}
