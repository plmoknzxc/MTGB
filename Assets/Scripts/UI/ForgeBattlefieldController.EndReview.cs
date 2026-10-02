using System;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private Canvas endCanvas;
    private RectTransform endDialog, endDetail;
    private Text endReplayLabel;
    private Button saveEndReplay, skipEndReplay, leaveReviewButton;
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
            string winner = service.LobbyState?.players?.Find(p => p.seat == ended.winnerSeat)?.displayName ?? ("玩家 " + ended.winnerSeat);
            string title = ended.aborted ? "对局中止" : ended.winnerSeat == 0 ? "本局平局" : winner + " 获胜";
            endDialog = ReplayWidgets.Panel(endCanvas.transform, "Result", 0, 30, 760, 350, new Color(.075f,.075f,.095f,.98f));
            var heading = ReplayWidgets.Label(endDialog, title, 0, 113, 710, 65, 40, TextAnchor.MiddleCenter);
            heading.fontStyle = FontStyle.Bold; heading.color = Gold;
            ReplayWidgets.Label(endDialog, "可以留在战场查看局面，准备好后再继续", 0, 54, 700, 32, 19, TextAnchor.MiddleCenter);
            endReplayLabel = ReplayWidgets.Label(endDialog, "", 0, 4, 700, 38, 19, TextAnchor.MiddleCenter);
            if (!service.IsSpectator && !ended.aborted)
            {
                saveEndReplay = ReplayWidgets.Button(endDialog, "保存录像", -117, -46, 220, () => service.DecideEndReplay(true));
                skipEndReplay = ReplayWidgets.Button(endDialog, "不保存", 117, -46, 220, () => service.DecideEndReplay(false));
            }
            ReplayWidgets.Button(endDialog, "继续查看", -238, -119, 218, ContinueEndReview);
            ReplayWidgets.Button(endDialog, ended.series?.betweenGames == true ? "前往换备" : "返回房间", 0, -119, 218, () => service.LeaveEndReview(false), true);
            ReplayWidgets.Button(endDialog, "离开房间", 238, -119, 218, () => service.LeaveEndReview(true));
            leaveReviewButton = Button(integrated ? Node("GearBox") : surface, "LeaveReview", "离开房间",
                new Vector2(110, 0), new Vector2(152, 44), ReopenEndResult, Ink);
            leaveReviewButton.GetComponentInChildren<Text>().fontSize = 19;
            leaveReviewButton.gameObject.SetActive(false);
        }
        endReplayLabel.text = service.EndReplayStatus;
        if (saveEndReplay != null)
        {
            bool show = !GamePreferences.AutoSaveReplays || !service.EndReplayDecided;
            saveEndReplay.gameObject.SetActive(show); skipEndReplay.gameObject.SetActive(show);
            saveEndReplay.interactable = skipEndReplay.interactable = service.EndReplayStatus != "录像已保存";
        }
    }

    private void ContinueEndReview()
    {
        endDialog.gameObject.SetActive(false);
        leaveReviewButton.gameObject.SetActive(true);
    }

    private void ReopenEndResult()
    {
        CloseReviewCards();
        leaveReviewButton.gameObject.SetActive(false);
        endDialog.gameObject.SetActive(true);
        endDialog.SetAsLastSibling();
    }

    private void CloseReviewCards()
    {
        browsedZone=null;zoneBrowserSignature=null;
        if (cardSelectionOverlay != null) cardSelectionOverlay.gameObject.SetActive(false);
        if (endDetail != null) Destroy(endDetail.gameObject);
        selectionLayoutKey = null;
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
        if(sprite==null)Label(artwork,"MissingArt",(card.faceDown?"牌背":card.name)+"\n缺失卡图",Vector2.zero,new Vector2(310,120),22,Paper);
        ReplayWidgets.Button(detail, "关闭详情", 0, -280, 260, () => Destroy(detail.gameObject));
    }
}
