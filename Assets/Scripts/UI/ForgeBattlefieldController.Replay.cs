using UnityEngine;

public sealed partial class ForgeBattlefieldController
{
    public void PresentReplay(ForgeSeatView state, int perspective, string[] names, bool seek)
    {
        if (seek) ResetCastingVisual();
        // Keep the recorded views immutable; hide interactive choices while preserving the board.
        var display = ServerSnapshotJson.Copy(state);
        display.awaitingInput = false; display.request = null;
        Present(display, perspective, false, null, null, GamePreferences.T("观看录像"));
        if (!integrated) return;
        SetText("PlayerPlate/Name", names[perspective - 1]);
        SetText("OpponentPlate/Name", names[2 - perspective]);
        Node("PriorityPill").gameObject.SetActive(false);
        advanceButton.gameObject.SetActive(false); endTurnButton.gameObject.SetActive(false); undoManaButton.gameObject.SetActive(false);
        if (mulliganChoices != null) mulliganChoices.gameObject.SetActive(false);
        actionRoot.parent.parent.gameObject.SetActive(false);
    }
}
