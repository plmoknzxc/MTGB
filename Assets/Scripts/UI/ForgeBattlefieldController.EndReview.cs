using System;
using UnityEngine;
using UnityEngine.UI;
using ReplayState = OnlineMatchService.ReplayChoiceState;

public sealed partial class ForgeBattlefieldController
{
    private Canvas endCanvas;
    private RectTransform endShade;
    private Text resultHeading,resultSummary;
    private MenuThemeStyle resultTheme;
    private string EndDestination(OnlineMatchEndedMessage ended) => service.IsLocalAiMatch ? "返回主菜单"
        : !service.IsSpectator && ended.series?.betweenGames==true ? "前往换备" : "返回房间";

    private string EndSummary(OnlineMatchEndedMessage ended)
    {
        var board=ended.finalView?.forge;
        if (!ended.aborted && ended.winnerSeat>0 && board!=null) {
            if (board.commanderEvent==OnlineRoomConfig.Arkenstone) {
                var winner=Array.Find(board.players,p=>p.seat==ended.winnerSeat);
                if (Array.Exists(winner?.effectMarkers??Array.Empty<ForgeCardView>(),c=>c.name=="山中之心" && c.markerStatus=="胜利点 3/3"))
                    return TapPaymentText("山中之心 · 阿肯宝石持有者累计获得 3 点胜利点。","Heart of the Mountain · The Arkenstone’s bearer reached 3 victory points.","山の心 · アーケン石の所持者が勝利点を3点獲得しました。");
            } else if (board.commanderEvent==OnlineRoomConfig.Loki && board.lokiSeat>0)
                return TapPaymentText($"诡计之神 · 第 {board.lokiSeat} 席担任洛基，其余三席组成英雄阵营。",$"God of Mischief · Seat {board.lokiSeat} was Loki. The other three seats were the Heroes.",$"いたずらの神 · 席{board.lokiSeat}がロキ、残る3席が英雄チーム。");
        }
        return GamePreferences.T("可以留在战场查看局面，准备好后再继续");
    }

    private void StyleEndResult(OnlineMatchEndedMessage ended)
    {
        var theme=HudTheme;
        if(theme==null || resultTheme==theme)return;
        resultTheme=theme;
        paintedReplayChoice=null;
        bool replay=endReplaySection.gameObject.activeSelf;
        if(endShade==null){
            endShade=ReplayWidgets.Panel(endCanvas.transform,"BattlefieldShade",0,0,0,0,theme.DialogShade);
            endShade.anchorMin=Vector2.zero;endShade.anchorMax=Vector2.one;
            endShade.offsetMin=endShade.offsetMax=Vector2.zero;
            endShade.SetAsFirstSibling();
            endShade.GetComponent<Image>().raycastTarget=true;
        }
        Color shade=theme.DialogShade;shade.a=.54f;
        endShade.GetComponent<Image>().color=shade;
        endShade.gameObject.SetActive(endDialog.gameObject.activeSelf);
        endDialog.sizeDelta=new Vector2(800,replay?388:250);
        MenuVisualTheme.StyleDeckDialogSurface(endDialog,resultHeading,98);
        var panorama=endDialog.Find("DialogLintel/Panorama").GetComponent<Image>();
        panorama.color=new Color(1,1,1,.50f);
        MenuTypography.Use(resultSummary,MenuTypography.Face.Control);
        resultSummary.color=theme.Silver;
        resultSummary.alignment=TextAnchor.MiddleLeft;
        resultSummary.rectTransform.sizeDelta=new Vector2(728,56);
        resultSummary.rectTransform.anchoredPosition=new Vector2(0,replay?63:-7);
        if(ended.series?.bestOf==3){
            var series=ended.series;
            int game=Mathf.Max(1,series.gameNumber-(series.betweenGames?1:0));
            string gameLabel=TapPaymentText($"第 {game} 局",$"Game {game}",$"第{game}ゲーム");
            resultSummary.text=$"BO3   ·   {gameLabel}     {PlayerDisplayName(1)}  {series.player1Wins} — {series.player2Wins}  {PlayerDisplayName(2)}";
            resultSummary.GetComponent<PreferenceText>().Literal=true;
        }
        endReplaySection.anchoredPosition=new Vector2(0,-28);
        StyleEndReplaySection(theme);
        foreach(var button in endDialog.GetComponentsInChildren<Button>(true)){
            MenuVisualTheme.StyleDeckTool(button,button==returnEndReviewButton);
            if(button!=saveEndReplay && button!=skipEndReplay)DisabledButtonVisual.Bind(button);
        }
        var secondary=(RectTransform)endViewFieldButton.transform;
        var primary=(RectTransform)returnEndReviewButton.transform;
        float footerY=replay?-141:-78;
        secondary.anchoredPosition=new Vector2(-220,footerY);secondary.sizeDelta=new Vector2(288,54);
        primary.anchoredPosition=new Vector2(151,footerY);primary.sizeDelta=new Vector2(426,54);
    }
    private RectTransform endDialog, endDetail, endReplaySection;
    private Text endReplayTitle, endReplayLabel;
    private Button saveEndReplay, skipEndReplay, leaveReviewButton, returnEndReviewButton, endViewFieldButton;
    private string reviewedMatch;
    private string resultBoardMatch;
    private ReplayState? paintedReplayChoice;
    private bool paintedSaveAvailable;
    private int replayChoiceRevision;
    private void PresentResultBoard(OnlineMatchEndedMessage ended)
    {
        var snapshot=ended.finalView?.forge;
        if((snapshot?.players?.Length??0)<2)snapshot=service.ForgeView?.forge;
        if((snapshot?.players?.Length??0)<2)snapshot=view;
        if(snapshot!=null){snapshot.awaitingInput=false;snapshot.inputPending=false;snapshot.request=null;}
        submitted=false;
        Present(snapshot,service.IsSpectator?1:service.LocalSeat,false,null,null,"本局已结束 · 可继续查看局面");
        if(controlModeButton!=null)controlModeButton.gameObject.SetActive(false);
        if(integrated){SetText("PriorityPill/Text","本局已结束");Node("CornerCluster/ControlRow").gameObject.SetActive(false);}
    }
    private void PresentEndReview()
    {
        var ended = service.EndReview;
        if (resultBoardMatch != ended.matchId) {
            PresentResultBoard(ended);
            resultBoardMatch = ended.matchId;
        }
        if (!ended.aborted && combatStrikeAge < BattlefieldWorldCard.AttackLungeDuration)
            return;
        if (reviewedMatch != ended.matchId && !ended.aborted && ended.winnerSeat > 0 && service.LocalSeat > 0 && !service.IsSpectator)
            GameAudio.Play(OnlineRoomConfig.SeatWon(ended.finalView?.forge ?? view,ended.winnerSeat,service.LocalSeat) ? TableSound.Victory : TableSound.Defeat);
        if (service.CurrentPuzzle is LocalPuzzle puzzle)
        {
            if (reviewedMatch == ended.matchId) { if(service.OffersEndReplay)RefreshEndReplayChoice(); return; }
            reviewedMatch = ended.matchId;
            endCanvas = ReplayWidgets.Canvas("PuzzleResult", 600);
            var shade = ReplayWidgets.Panel(endCanvas.transform, "Shade", 0, 0, 0, 0, new Color(.03f,.03f,.05f,.68f));
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero;
            bool replay=service.OffersEndReplay;
            endDialog = ReplayWidgets.Panel(shade, "Result", 0, 0, 800, replay?390:268, MenuVisualTheme.ArchivePanel);
            string outcome = ended.aborted ? "残局中止" : ended.winnerSeat == 1 ? "残局破解" : "挑战未完成";
            var heading = ReplayWidgets.Label(endDialog, outcome, 0, replay?180:119, 720, 64, 38);
            MenuVisualTheme.StyleDeckDialogSurface(endDialog, heading, 94);
            var summary=ReplayWidgets.Label(endDialog, ended.aborted ? ended.reason : puzzle.title + "   ·   " + puzzle.Objective, 0, replay?60:0, 720, 72, 24);
            if(!ended.aborted && summary.preferredWidth>summary.rectTransform.rect.width)
                summary.text=puzzle.title+"\n"+puzzle.Objective;
            if(replay){
                BuildEndReplayChoice(true);
                endReplaySection.anchoredPosition=new Vector2(0,-29);
                StyleEndReplaySection(HudTheme);
                RefreshEndReplayChoice();
            }
            float footer=replay?-142:-83;
            MenuVisualTheme.StyleDeckTool(ReplayWidgets.Button(endDialog, "返回残局列表", -180, footer, 352, () => service.LeavePuzzle(false)));
            MenuVisualTheme.StyleDeckTool(ReplayWidgets.Button(endDialog, "再次挑战", 180, footer, 352, () => service.LeavePuzzle(true)), true);
            if(!ended.aborted)service.PreparePuzzleOpening(service.PuzzleEditorSource,endDialog.gameObject);
            return;
        }
        if (reviewedMatch != ended.matchId)
        {
            reviewedMatch = ended.matchId;
            paintedReplayChoice=null;
            endCanvas = ReplayWidgets.Canvas("MatchResult", 600);
            string winner = OnlineRoomConfig.WinnerLabel(ended.finalView?.forge,ended.winnerSeat,PlayerDisplayName(ended.winnerSeat));
            string title = ended.aborted ? "对局中止" : ended.winnerSeat == 0 ? "本局平局" : TapPaymentText(winner+" 获胜",winner+" wins",winner+"の勝利");
            bool hasReplayChoice=service.OffersEndReplay;
            endDialog = ReplayWidgets.Panel(endCanvas.transform, "Result", 0, 30, 720, hasReplayChoice?350:260, new Color(.075f,.075f,.095f,.98f));
            var heading = ReplayWidgets.Label(endDialog, title, 0, hasReplayChoice?125:80, 650, 82, 40, TextAnchor.MiddleCenter);
            if(!ended.aborted&&ended.winnerSeat>0)SetNamedPlayerText(heading,title);
            heading.fontStyle = FontStyle.Bold; heading.color = Gold;
            var subtitle=ReplayWidgets.Label(endDialog, EndSummary(ended), 0, hasReplayChoice?62:18, 650, 40, 19, TextAnchor.MiddleCenter);
            subtitle.color=new Color(.68f,.71f,.76f);
            BuildEndReplayChoice(hasReplayChoice);
            float actionsY=hasReplayChoice?-116:-72;
            endViewFieldButton=ReplayWidgets.Button(endDialog, "查看战场", -204, actionsY, 216, ViewBattlefield);
            StyleFieldShortcut(endViewFieldButton,false);
            endViewFieldButton.interactable=view?.players?.Length>=2;
            returnEndReviewButton=ReplayWidgets.Button(endDialog, EndDestination(ended), 140, actionsY, 344, () => service.LeaveEndReview(false), true);
            // Result buttons retain the user's font size while the dialog is
            // hidden and reopened; ReplayWidgets' generic best-fit is unsuitable here.
            foreach (var label in endDialog.GetComponentsInChildren<Text>(true))
            {
                label.resizeTextForBestFit = false;
                label.GetComponent<PreferenceText>()?.Apply();
            }
            resultHeading=heading;resultSummary=subtitle;resultTheme=null;
            leaveReviewButton = Button(integrated ? Node("GearBox") : surface, "LeaveReview", TapPaymentText("返回结算","Results menu","結果に戻る"),
                new Vector2(110, 0), new Vector2(152, 44), ReopenEndResult, Ink);
            PreferenceText.SetSize(leaveReviewButton.GetComponentInChildren<Text>(),19);
            StyleFieldShortcut(leaveReviewButton,true);
            // Keep the existing settings-adjacent position clickable over a
            // readonly card browser, which is raised within the battlefield canvas.
            SetOverlayLayer((RectTransform)leaveReviewButton.transform,(HudTheme!=null?InspectLayerOffset:DecisionLayerOffset)+2,true);
            leaveReviewButton.gameObject.SetActive(false);
        }
        StyleEndResult(ended);
        // The result canvas remains hidden while the player reviews the field.
        // Its label still needs updating without querying only active children.
        returnEndReviewButton.GetComponentInChildren<Text>(true).text=GamePreferences.T(EndDestination(ended));
        RefreshEndReplayChoice();
    }

    private void BuildEndReplayChoice(bool visible)
    {
        paintedReplayChoice=null;
        endReplaySection=ReplayWidgets.Panel(endDialog,"ReplayChoice",0,-20,-72,100,new Color(.11f,.13f,.16f));
        endReplaySection.anchorMin=new Vector2(0,.5f);
        endReplaySection.anchorMax=new Vector2(1,.5f);
        endReplaySection.gameObject.SetActive(visible);
        saveEndReplay=skipEndReplay=null;
        if(!visible)return;
        const float inset=16, gap=12, choiceWidth=142, columnGap=18, verticalInset=8;
        var viewport=ReplayWidgets.Panel(endReplaySection,"ReplayMessage",0,0,0,0,Color.clear);
        viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;
        viewport.offsetMin=new Vector2(inset,verticalInset);
        viewport.offsetMax=new Vector2(-(inset+2*choiceWidth+gap+columnGap),-verticalInset);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content=ReplayWidgets.Rect(viewport,"Text",0,0,0,0);
        content.anchorMin=Vector2.up;content.anchorMax=Vector2.one;content.pivot=Vector2.up;
        var group=content.gameObject.AddComponent<VerticalLayoutGroup>();
        group.childAlignment=TextAnchor.MiddleLeft;
        group.spacing=4;
        group.childControlHeight=group.childControlWidth=group.childForceExpandWidth=true;
        group.childForceExpandHeight=false;
        content.gameObject.AddComponent<LayoutElement>().minHeight=endReplaySection.rect.height-2*verticalInset;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        endReplayTitle=ReplayWidgets.Label(content,TapPaymentText("本局录像","Match replay","対戦リプレイ"),0,0,0,0,23);
        endReplayLabel=ReplayWidgets.Label(content,"",0,0,0,0,18);
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;
        scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=28;
        Button Choice(string caption,float right,float width,Action click)
        {
            var b=ReplayWidgets.Button(endReplaySection,caption,right,0,width,click);
            var rect=(RectTransform)b.transform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,.5f);
            rect.sizeDelta=new Vector2(width,48);
            var label=b.GetComponentInChildren<Text>();
            label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
            label.rectTransform.offsetMin=new Vector2(12,0);label.rectTransform.offsetMax=new Vector2(-12,0);
            label.horizontalOverflow=HorizontalWrapMode.Wrap;
            label.resizeTextMinSize=12;label.resizeTextMaxSize=24;
            label.alignment=TextAnchor.MiddleCenter;label.alignByGeometry=true;
            PreferenceText.SetSize(label,18);
            return b;
        }
        saveEndReplay=Choice("保存录像",-(inset+choiceWidth+gap),choiceWidth,()=>service.DecideEndReplay(true));
        skipEndReplay=Choice("不保存",-inset,choiceWidth,()=>service.DecideEndReplay(false));
    }

    private void StyleEndReplaySection(MenuThemeStyle theme)
    {
        if(theme==null || !endReplaySection.gameObject.activeSelf)return;
        endReplaySection.GetComponent<Image>().color=theme.Recess;
        MenuVisualTheme.SectionTop(endReplaySection,theme.Etching);
        MenuTypography.Use(endReplayTitle,MenuTypography.Face.Control);
        MenuTypography.Use(endReplayLabel,MenuTypography.Face.Control);
        endReplayTitle.color=theme.ArchivePaper;
        endReplayLabel.color=theme.Silver;
    }

    private void RefreshEndReplayChoice()
    {
        if (saveEndReplay != null)
        {
            var state=service.ReplayChoice;
            endReplayLabel.text=state==ReplayState.Undecided && service.EndReplayStatus=="是否保存本局录像？" ? (service.CurrentPuzzle!=null
                ? TapPaymentText("直接返回或重试将不保存录像。","No autosave on exit or retry.","戻る・再挑戦では自動保存なし。")
                : TapPaymentText("直接离开结算将不保存录像。","No autosave on exit.","終了時は自動保存されません。"))
                : GamePreferences.T(service.EndReplayStatus);
            bool available=state!=ReplayState.Skipped || service.CanRequestEndReplay;
            if(paintedReplayChoice==state && paintedSaveAvailable==available && replayChoiceRevision==GamePreferences.Revision)return;
            paintedSaveAvailable=available;
            paintedReplayChoice=state;replayChoiceRevision=GamePreferences.Revision;
            bool selected=state is ReplayState.Waiting or ReplayState.Saving or ReplayState.Saved;
            bool locked=state is ReplayState.Saving or ReplayState.Saved or ReplayState.Unavailable;
            string saveLabel=state switch{
                ReplayState.Waiting=>TapPaymentText("已选保存","Save selected","保存を選択済み"),
                ReplayState.Saving=>TapPaymentText("正在保存…","Saving…","保存中…"),
                ReplayState.Saved=>TapPaymentText("已保存","Saved","保存済み"),
                ReplayState.Failed=>TapPaymentText("重试保存","Retry save","保存を再試行"),
                ReplayState.Unavailable=>TapPaymentText("录像不可用","Unavailable","保存不可"),
                _=>GamePreferences.T("保存录像")
            };
            PaintReplayButton(saveEndReplay,saveLabel,selected,available&&!locked&&!selected);
            bool skipped=state==ReplayState.Skipped;
            PaintReplayButton(skipEndReplay,skipped?TapPaymentText("已跳过","Skipped","スキップ済み"):GamePreferences.T("不保存"),skipped,!locked&&!skipped);
        }
    }

    private static void PaintReplayButton(Button button,string caption,bool selected,bool interactive)
    {
        MenuVisualTheme.StyleDeckTool(button,selected);
        var label=button.GetComponentInChildren<Text>(true);
        label.GetComponent<PreferenceText>().Literal=true;
        label.text=caption;
        label.GetComponent<PreferenceText>().Apply();
        var colors=button.colors;
        if(selected)colors.disabledColor=colors.normalColor;
        button.colors=colors;
        button.interactable=interactive;
        button.targetGraphic.CrossFadeColor(interactive||selected?colors.normalColor:colors.disabledColor,0,true,true);
    }

    private void ContinueEndReview()
    {
        endDialog.gameObject.SetActive(false);
        if(endShade!=null)endShade.gameObject.SetActive(false);
        leaveReviewButton.gameObject.SetActive(true);
        RefreshBattlefieldViewControls();
    }

    private void ReopenEndResult()
    {
        CloseReviewCards();
        leaveReviewButton.gameObject.SetActive(false);
        endDialog.gameObject.SetActive(true);
        if(endShade!=null)endShade.gameObject.SetActive(true);
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
        if(endDetail!=null){endDetail.gameObject.SetActive(false);Destroy(endDetail.gameObject);}
        bool themed=HudTheme!=null;
        float height=Mathf.Min(themed?760:660,surface.rect.height-32);
        float width=Mathf.Min(themed?460:380,surface.rect.width-32);
        var detail=endDetail=ReplayWidgets.Panel(cardSelectionOverlay,"ReviewDetail",0,0,width,height,Ink);
        // Keep the grid and its scroll position alive underneath this full-card page.
        selectionPanel.gameObject.SetActive(false);
        if(themed){
            StyleZoneSheet(detail);
            string title=card.faceDown?TapPaymentText("牌背","Card back","カード裏面"):GamePreferences.CardFaceName(card.name);
            var name=Label(detail,"Title",title,new Vector2(0,height*.5f-30),new Vector2(width-48,42),24,HudText);
            MenuTypography.Use(name,MenuTypography.Face.Heading);name.alignment=TextAnchor.MiddleLeft;
            name.resizeTextForBestFit=true;name.resizeTextMinSize=14;name.resizeTextMaxSize=24;
        }
        string cost=themed?PickerCostText(card):"";
        float costHeight=cost.Length>0?Mathf.Ceil(26*GamePreferences.TextScale)+6:0;
        if(costHeight>0)PresentPickerCost(detail,cost,width-44,54,costHeight);
        float artWidth=Mathf.Min(width-44,(height-158-costHeight)/1.4f);
        var artwork=Panel(detail,"Artwork",new Vector2(0,4-costHeight/2),new Vector2(artWidth,artWidth*1.4f),Ink);
        TryBattlefieldArt(card,out var sprite);
        var image=artwork.GetComponent<Image>();image.sprite=sprite;image.preserveAspect=true;
        image.color=sprite==null?Ink:Color.white;
        PresentComponentFace(artwork,card);
        if(IsPreparedSpell(card))PresentPreparedSpellFace(artwork,card,artwork.sizeDelta);
        if(!themed)PresentLegacyCost(artwork,card);
        if(sprite==null&&!IsPreparedSpell(card))Label(artwork,"MissingArt",(card.faceDown?"牌背":card.name)+"\n缺失卡图",Vector2.zero,new Vector2(310,120),22,Paper);
        var back=ReplayWidgets.Button(detail,TapPaymentText("返回列表","Back to list","一覧に戻る"),themed?width*.5f-99:0,-height*.5f+34,158,()=>{
            detail.gameObject.SetActive(false);Destroy(detail.gameObject);endDetail=null;
            if(selectionPanel!=null)selectionPanel.gameObject.SetActive(true);
        });
        if(themed)StyleZoneButton(back);
    }
}
