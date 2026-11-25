using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed partial class ForgeBattlefieldController
{
    private BattlefieldRedesignLayout redesignProfile;
    private bool IsRedesignLayout => redesignProfile != null;
    private bool redesignHandExpanded = true;
    private bool redesignOtherHandExpanded;
    // A native reveal/choice owns visibility until it completes. Never store
    // that forced expansion as the viewer's ordinary hand preference.
    private bool RedesignOtherHandForced => !viewingBattlefield && DirectHandSelection && SelectionHand?.seat != DisplayedHandSeat;
    private float redesignFooterTop = 262;
    private float redesignFooterLeft;
    private static readonly Vector2 DuelClusterPosition=new Vector2(-186,101);
    private const float PhaseDockTop=104;
    // Permanent lanes reserve the full phase dock even when its actions briefly collapse.
    private float DuelFieldFooterTop=>DuelClusterPosition.y+PhaseDockTop;
    private float DuelFieldFooterLeft {
        get {
            var pause=pauseNextPhaseButton?.transform as RectTransform;
            float width=pause!=null?-pause.anchoredPosition.x-pause.rect.xMin:0;
            return surface.rect.width-Mathf.Max(340,width);
        }
    }
    private const float RedesignPlayerManaReserve = 56;
    private const float RedesignOpponentHudLift = 54;
    private Vector2 redesignSurfaceSize;
    private Vector2Int redesignViewportSize;
    private int redesignPreferenceRevision = -1;
    private readonly List<RaycastResult> redesignPointerHits = new();

    public static bool IsBattlefieldSceneActive => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == SceneName
        || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == FourPlayerSceneName;

    private static void RedesignPlace(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void PrepareRedesignLayout()
    {
        // CanvasScaler settles at the render boundary. A snapshot can arrive in
        // the resize frame; measure every lane against the new canvas, not the old size.
        if (IsRedesignLayout && redesignViewportSize != new Vector2Int(Screen.width, Screen.height))
            Canvas.ForceUpdateCanvases();
    }

    private void PresentRedesignLayout()
    {
        if (!IsRedesignLayout) return;
        // Reuse the shipping footer completely; only move the whole group down.
        // These are the native anchors from ApplyDuelLayout/PresentControlMode.
        var cluster = (RectTransform)Node("CornerCluster");
        cluster.anchoredPosition = DuelClusterPosition;
        if(worldTable!=null){
            var opponent=(RectTransform)Node("OpponentPlate");
            opponent.anchoredPosition=new Vector2(opponent.anchoredPosition.x,-120+RedesignOpponentHudLift);
            var opponentShell=(RectTransform)Node("OpponentPlateWithZones");
            opponentShell.anchoredPosition=new Vector2(opponentShell.anchoredPosition.x,-144+RedesignOpponentHudLift);
            var opponentZones=(RectTransform)Node("ZonePiles_OpponentTopRight");
            opponentZones.anchoredPosition=new Vector2(opponentZones.anchoredPosition.x,-192+RedesignOpponentHudLift);
        }
        foreach (var button in new[] { controlModeButton, pauseNextPhaseButton })
            if (button != null)
            {
                var rect = (RectTransform)button.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 26.5f);
            }
        if(worldTable!=null){
            var gear=(RectTransform)Node("GearBox/SettingsButton");
            if(controlModeButton!=null&&controlModeButton.gameObject.activeInHierarchy){
                var corners=new Vector3[4];((RectTransform)controlModeButton.transform).GetWorldCorners(corners);
                gear.position=new Vector3(corners[2].x+27,(corners[0].y+corners[2].y)*.5f,corners[2].z);
            }else gear.position=surface.TransformPoint(new Vector3(surface.rect.xMax-23,surface.rect.yMin+26.5f,0));
            gear.sizeDelta=new Vector2(40,40);
        }

        var row = Node("CornerCluster/ControlRow");
        float footerTop = 0,footerLeft=surface.rect.width-32;
        void Reserve(RectTransform rect,bool fitText=false)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            // Right-aligned phase labels have a wide transparent layout box.
            // Reserve their text width rather than turning that padding into a gutter.
            var label=fitText?rect.GetComponent<UnityEngine.UI.Text>():null;
            if(label!=null){
                var bounds=rect.rect;float width=Mathf.Min(bounds.width,label.preferredWidth+4);
                int alignment=(int)label.alignment%3;
                float left=alignment==2?bounds.xMax-width:alignment==1?bounds.center.x-width*.5f:bounds.xMin;
                corners[0]=rect.TransformPoint(new Vector2(left,bounds.yMin));
                corners[1]=rect.TransformPoint(new Vector2(left,bounds.yMax));
                corners[2]=rect.TransformPoint(new Vector2(left+width,bounds.yMax));
                corners[3]=rect.TransformPoint(new Vector2(left+width,bounds.yMin));
            }
            foreach (var corner in corners){var point=surface.InverseTransformPoint(corner);footerTop=Mathf.Max(footerTop,point.y-surface.rect.yMin);footerLeft=Mathf.Min(footerLeft,point.x-surface.rect.xMin);}
        }
        Reserve(row.Find("PhaseDockSurface") as RectTransform);
        Reserve((RectTransform)row.Find("PhaseText"),true);
        Reserve((RectTransform)row.Find("TurnChip/TurnLabel"),true);
        foreach (var button in row.GetComponentsInChildren<UnityEngine.UI.Button>(false)) Reserve((RectTransform)button.transform);
        if(controlModeButton!=null)Reserve((RectTransform)controlModeButton.transform);
        if(pauseNextPhaseButton!=null)Reserve((RectTransform)pauseNextPhaseButton.transform);
        redesignFooterTop = footerTop;
        redesignFooterLeft = footerLeft;
        ReflowRedesignSourceWidth(ownFan);
        ReflowRedesignSourceWidth(opponentFan);
        if (Mathf.Abs(redesignStackHeightLimit - RedesignStackMaximumHeight) > .1f)
        {
            redesignStackOffset = stackContent.anchoredPosition.y;
            var entries=DisplayedStackEntries();
            LayoutRedesignStack(entries,Math.Max(entries.Length,view?.stack?.Length??0));
        }
        ApplyRedesignHandOffset(ownFan.navigation?.Offset ?? 0, true);
        ApplyRedesignHandOffset(opponentFan, opponentFan.navigation?.Offset ?? 0, true);
        FitFieldBetweenHands();
        PositionRedesignSourceTabs();
        redesignSurfaceSize = surface.rect.size;
        redesignViewportSize = new Vector2Int(Screen.width, Screen.height);
        redesignPreferenceRevision = GamePreferences.Revision;
        LayoutBattleSidebar();
    }

    private void ReflowRedesignSourceWidth(HandFanState fan)
    {
        var tabs=fan.opponent?otherSourceTabs:handSourceTabs;
        float width=fan.opponent?RedesignOtherHandMaximumWidth:RedesignHandMaximumWidth;
        if(tabs==null||Mathf.Abs((fan.opponent?redesignOtherLane:redesignOwnLane)-width)<.1f)return;
        var cards=fan.slots.Select(s=>s.item.card).Where(c=>c!=null).ToArray();
        bool choosingHand=DirectHandSelection&&SelectionHand is ForgePlayerView selection&&cards.Any(c=>c.zone=="Hand"&&c.ownerSeat==selection.seat);
        cards=PresentRedesignSources(cards,fan,choosingHand);
        LayoutRedesignHand(fan.opponent?otherHand:ownHand,cards,fan,choosingHand);
    }

    private void UpdateRedesignLayout()
    {
        if (!IsRedesignLayout || !integrated || view == null) return;
        UpdateBattleSidebarSlide();
        if (surface.rect.size != redesignSurfaceSize || GamePreferences.Ready && redesignPreferenceRevision != GamePreferences.Revision) { PresentIntegrated(connectionText.text); return; }
        UpdateBattlefieldViewNavigation();
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) HandleRedesignBattlefieldPointer(mouse.position.ReadValue());
#else
        if (Input.GetMouseButtonDown(0)) HandleRedesignBattlefieldPointer(Input.mousePosition);
#endif
    }

    private void HandleRedesignBattlefieldPointer(Vector2 position)
    {
        // Picking a target, including the response-in-flight frame, is part of casting.
        if (EventSystem.current == null || HasCastingPrompt || UsesTargetWindow) return;
        redesignPointerHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, redesignPointerHits);
        var hit = redesignPointerHits.Count > 0 ? redesignPointerHits[0].gameObject.transform : null;
        if (hit != null && (hit.IsChildOf(ownHand) || hit == ownHand || handSourceTabs != null && hit.IsChildOf(handSourceTabs))) return;
        if (hit != null && (hit.IsChildOf(otherHand) || hit == otherHand || otherSourceTabs != null && hit.IsChildOf(otherSourceTabs))) return;
        bool field = hit == null || hit.IsChildOf(cardsRoot) || hit.IsChildOf(Node("PlayerPlate")) || hit.IsChildOf(Node("OpponentPlate"));
        if (field) { SetRedesignHandExpanded(false); SetRedesignOtherHandExpanded(false); }
    }

    private bool ExpandRedesignHand()
    {
        bool changed = !redesignHandExpanded;
        SetRedesignHandExpanded(true);
        return changed;
    }

    private void SetRedesignHandExpanded(bool expanded)
    {
        if(service?.IsSpectator==true)expanded=true;
        if (!IsRedesignLayout || redesignHandExpanded == expanded) return;
        redesignHandExpanded = expanded;
        foreach (var slot in ownFan.slots) slot.item.pointer.ResetHover();
        ForgeBattlefieldCardPointer.RestoreHandOrder(ownHand);
        LeaveInspectCard();
        ApplyRedesignHandOffset(ownFan.navigation?.Offset ?? 0, true);
        FitFieldBetweenHands();
    }

    private bool ExpandRedesignOtherHand()
    {
        if (RedesignOtherHandForced) return false;
        bool changed = !redesignOtherHandExpanded;
        SetRedesignOtherHandExpanded(true);
        return changed;
    }

    private void SetRedesignOtherHandExpanded(bool expanded)
    {
        if (!IsRedesignLayout || RedesignOtherHandForced || redesignOtherHandExpanded == expanded) return;
        redesignOtherHandExpanded = expanded;
        foreach (var slot in opponentFan.slots) slot.item.pointer.ResetHover();
        ForgeBattlefieldCardPointer.RestoreHandOrder(otherHand);
        LeaveInspectCard();
        ApplyRedesignHandOffset(opponentFan, 0, true);
        FitFieldBetweenHands();
    }
}
