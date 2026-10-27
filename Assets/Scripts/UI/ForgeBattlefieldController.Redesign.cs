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
    private float redesignFooterTop = 262;
    private float redesignFooterLeft;
    private const float RedesignPlayerManaReserve = 56;
    private Vector2 redesignSurfaceSize;
    private Vector2Int redesignViewportSize;
    private int redesignPreferenceRevision = -1;
    private readonly List<RaycastResult> redesignPointerHits = new();

    public static bool IsBattlefieldSceneActive => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == SceneName;

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
        cluster.anchoredPosition = new Vector2(-186, 101);
        foreach (var button in new[] { controlModeButton, pauseNextPhaseButton })
            if (button != null)
            {
                var rect = (RectTransform)button.transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 26.5f);
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
            LayoutRedesignStack(view?.stackItems ?? Array.Empty<ForgeStackItem>(), Math.Max(view?.stackItems?.Length ?? 0, view?.stack?.Length ?? 0));
        }
        ApplyRedesignHandOffset(ownFan.navigation?.Offset ?? 0, true);
        ApplyRedesignHandOffset(opponentFan, opponentFan.navigation?.Offset ?? 0, true);
        PositionRedesignSourceTabs();
        redesignSurfaceSize = surface.rect.size;
        redesignViewportSize = new Vector2Int(Screen.width, Screen.height);
        redesignPreferenceRevision = GamePreferences.Revision;
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
        if (surface.rect.size != redesignSurfaceSize || GamePreferences.Ready && redesignPreferenceRevision != GamePreferences.Revision) { PresentIntegrated(connectionText.text); return; }
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) HandleRedesignBattlefieldPointer(mouse.position.ReadValue());
#else
        if (Input.GetMouseButtonDown(0)) HandleRedesignBattlefieldPointer(Input.mousePosition);
#endif
    }

    private void HandleRedesignBattlefieldPointer(Vector2 position)
    {
        if (EventSystem.current == null) return;
        redesignPointerHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, redesignPointerHits);
        var hit = redesignPointerHits.Count > 0 ? redesignPointerHits[0].gameObject.transform : null;
        if (hit != null && (hit.IsChildOf(ownHand) || hit == ownHand || handSourceTabs != null && hit.IsChildOf(handSourceTabs))) return;
        bool field = hit == null || hit.IsChildOf(cardsRoot) || hit.IsChildOf(Node("PlayerPlate")) || hit.IsChildOf(Node("OpponentPlate"));
        if (field) SetRedesignHandExpanded(false);
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
    }
}
