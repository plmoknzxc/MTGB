using UnityEngine;

public sealed partial class ForgeBattlefieldController
{
    private void ResetRestartPresentation()
    {
        ResetCastingVisual();ResetZoneMotion();RestoreBattlefieldWindows();ResetNotices();
        expandedGroups.Clear();pages.Clear();groupRequest=0;groupExpandedFrame=-1;
        nameRequest=0;nameLanguageRevision=-1;pileRequest=0;selectedPileIndex=-1;
        CloseReviewCards();selectionPage=0;selectionKind=null;selectionLayoutKey=null;
        hoveredStack=null;detailCard=null;detailCardId=inspectedCardId=0;inspectHoverRevision++;
        if(integrated)Node("InspectPanel").gameObject.SetActive(false);
        if(cardSelectionOverlay!=null){Clear(cardSelectionOverlay);cardSelectionOverlay.gameObject.SetActive(false);}
        submitted=false;requestId=0;
    }
}
