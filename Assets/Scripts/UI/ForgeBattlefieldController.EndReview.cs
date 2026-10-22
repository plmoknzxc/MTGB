using System;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private Canvas endCanvas;
    private RectTransform endDialog, endDetail, endReplaySection;
    private Text endReplayLabel;
    private Button saveEndReplay, skipEndReplay, leaveReviewButton, returnEndReviewButton;
    private string reviewedMatch;
    private void PresentEndReview()
    {
        var ended = service.EndReview;
        if (reviewedMatch != ended.matchId)
        {
            reviewedMatch = ended.matchId;
            var snapshot = service.ForgeView?.forge;
            if (snapshot != null) { snapshot.awaitingInput = false; snapshot.inputPending = false; snapshot.request = null; }
            submitted = false;
            Present(snapshot, service.IsSpectator ? 1 : service.LocalSeat, false, null, null, "本局已结束 · 可继续查看局面");
            if (controlModeButton != null) controlModeButton.gameObject.SetActive(false);
            if (integrated) { SetText("PriorityPill/Text", "本局已结束"); Node("CornerCluster/ControlRow").gameObject.SetActive(false); }
            endCanvas = ReplayWidgets.Canvas("MatchResult", 600);
            string winner = PlayerDisplayName(ended.winnerSeat);
            string title = ended.aborted ? "对局中止" : ended.winnerSeat == 0 ? "本局平局" : TapPaymentText(winner+" 获胜",winner+" wins",winner+"の勝利");
            bool hasReplayChoice=!service.IsSpectator&&!ended.aborted;
            endDialog = ReplayWidgets.Panel(endCanvas.transform, "Result", 0, 30, 720, hasReplayChoice?350:260, new Color(.075f,.075f,.095f,.98f));
            var heading = ReplayWidgets.Label(endDialog, title, 0, hasReplayChoice?125:80, 650, 82, 40, TextAnchor.MiddleCenter);
            if(!ended.aborted&&ended.winnerSeat>0)SetNamedPlayerText(heading,title);
            heading.fontStyle = FontStyle.Bold; heading.color = Gold;
            var subtitle=ReplayWidgets.Label(endDialog, "可以留在战场查看局面，准备好后再继续", 0, hasReplayChoice?62:18, 650, 40, 19, TextAnchor.MiddleCenter);
            subtitle.color=new Color(.68f,.71f,.76f);
            endReplaySection=ReplayWidgets.Panel(endDialog,"ReplayChoice",0,-12,624,86,new Color(.11f,.13f,.16f));
            endReplaySection.gameObject.SetActive(hasReplayChoice);
            endReplayLabel = ReplayWidgets.Label(endReplaySection, "", -166, 0, 252, 74, 17, TextAnchor.MiddleLeft);
            if (hasReplayChoice)
            {
                saveEndReplay = ReplayWidgets.Button(endReplaySection, "保存录像", 58, 0, 180, () => service.DecideEndReplay(true));
                skipEndReplay = ReplayWidgets.Button(endReplaySection, "不保存", 236, 0, 136, () => service.DecideEndReplay(false));
                PreferenceText.SetSize(saveEndReplay.GetComponentInChildren<Text>(),18);
                PreferenceText.SetSize(skipEndReplay.GetComponentInChildren<Text>(),18);
            }
            float actionsY=hasReplayChoice?-116:-72;
            ReplayWidgets.Button(endDialog, "查看战场", -204, actionsY, 216, ViewBattlefield);
            returnEndReviewButton=ReplayWidgets.Button(endDialog, ended.series?.betweenGames == true ? "前往换备" : "返回房间", 140, actionsY, 344, () => service.LeaveEndReview(false), true);
            // Result buttons retain the user's font size while the dialog is
            // hidden and reopened; ReplayWidgets' generic best-fit is unsuitable here.
            foreach (var label in endDialog.GetComponentsInChildren<Text>(true))
            {
                label.resizeTextForBestFit = false;
                label.GetComponent<PreferenceText>()?.Apply();
            }
            leaveReviewButton = Button(integrated ? Node("GearBox") : surface, "LeaveReview", TapPaymentText("返回结算","Results menu","結果に戻る"),
                new Vector2(110, 0), new Vector2(152, 44), ReopenEndResult, Ink);
            PreferenceText.SetSize(leaveReviewButton.GetComponentInChildren<Text>(),19);
            // Keep the existing settings-adjacent position clickable over a
            // readonly card browser, which is raised within the battlefield canvas.
            var reviewShortcutCanvas = leaveReviewButton.gameObject.AddComponent<Canvas>();
            reviewShortcutCanvas.overrideSorting = true;
            reviewShortcutCanvas.sortingOrder = canvas.sortingOrder + DecisionLayerOffset + 2;
            leaveReviewButton.gameObject.AddComponent<GraphicRaycaster>();
            leaveReviewButton.gameObject.SetActive(false);
        }
        endReplayLabel.text = service.EndReplayStatus;
        returnEndReviewButton.GetComponentInChildren<Text>().text=GamePreferences.T(ended.series?.betweenGames==true?"前往换备":"返回房间");
        if (saveEndReplay != null)
        {
            bool show = !GamePreferences.AutoSaveReplays || !service.EndReplayDecided;
            saveEndReplay.gameObject.SetActive(show); skipEndReplay.gameObject.SetActive(show);
            endReplayLabel.rectTransform.anchoredPosition=new Vector2(show?-166:0,0);
            endReplayLabel.rectTransform.sizeDelta=new Vector2(show?252:580,74);
            endReplayLabel.alignment=show?TextAnchor.MiddleLeft:TextAnchor.MiddleCenter;
            saveEndReplay.interactable = skipEndReplay.interactable = service.EndReplayStatus != "录像已保存";
        }
    }

    private void ContinueEndReview()
    {
        endDialog.gameObject.SetActive(false);
        leaveReviewButton.gameObject.SetActive(true);
        RefreshBattlefieldViewControls();
    }

    private void ReopenEndResult()
    {
        CloseReviewCards();
        leaveReviewButton.gameObject.SetActive(false);
        endDialog.gameObject.SetActive(true);
        endDialog.SetAsLastSibling();
        RefreshBattlefieldViewControls();
    }

    private void CloseReviewCards()
    {
        browsedZone=null;browsedComponentId=0;zoneBrowserSignature=null;
        if (cardSelectionOverlay != null) cardSelectionOverlay.gameObject.SetActive(false);
        if (endDetail != null) Destroy(endDetail.gameObject);
        selectionLayoutKey = null;
        cardSelectionReadOnly=false;
    }

    private void ShowReviewCards(string title, ForgeCardView[] cards)
    {
        if (endDialog!=null && endDialog.gameObject.activeSelf) return;
        CloseReviewCards();
        selectionPage = 0;
        RenderCardSelection(new ForgeRequest {
            kind = "ReviewCards", options = Array.Empty<ForgeOption>(),
            previewCards = cards ?? Array.Empty<ForgeCardView>()
        }, title);
    }

    private void ShowReviewDetail(ForgeCardView card)
    {
        if (endDetail != null) Destroy(endDetail.gameObject);
        var detail = endDetail = ReplayWidgets.Panel(cardSelectionOverlay, "ReviewDetail", 0, 0, 380, 660, Ink);
        var artwork=Panel(detail,"Artwork",new Vector2(0,30),new Vector2(330,470),Ink);
        TryBattlefieldArt(card,out var sprite);
        var image=artwork.GetComponent<Image>();image.sprite=sprite;image.preserveAspect=true;
        image.color=sprite==null?Ink:Color.white;
        PresentComponentFace(artwork,card);
        PresentLegacyCost(artwork,card);
        if(sprite==null)Label(artwork,"MissingArt",(card.faceDown?"牌背":card.name)+"\n缺失卡图",Vector2.zero,new Vector2(310,120),22,Paper);
        ReplayWidgets.Button(detail, "关闭详情", 0, -280, 260, () => Destroy(detail.gameObject));
    }
}
