using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private float battlefieldZoom = 1;
    private Vector2 battlefieldPan;
    private Vector2 battlefieldDragOrigin;
    private Vector2 battlefieldPanOrigin;
    private bool draggingBattlefield;
    private float handFitScale=1, handFitOffset;

    private void FitFieldBetweenHands()
    {
        if(worldTable==null||cardsRoot==null)return;
        // Each quadrant already reserves the hand and seat banners in its cell geometry.
        if(IsFourPlayerScene)return;
        float lowest=0,highest=0,lowestNearFooter=float.PositiveInfinity;
        float phaseLeft=surface.rect.xMin+DuelFieldFooterLeft-16;
        bool reservePhase=IsDuelTable;
        void Include(float bottomEdge,float topEdge,float rightEdge){
            lowest=Mathf.Min(lowest,bottomEdge);
            highest=Mathf.Max(highest,topEdge);
            if(reservePhase&&rightEdge>=phaseLeft)lowestNearFooter=Mathf.Min(lowestNearFooter,bottomEdge);
        }
        foreach(var pair in arenaCards){
            var item=pair.Value;
            if(item.root.parent!=cardsRoot || item.card?.zone!="Battlefield" || !retainedCards.Contains(pair.Key))continue;
            var footprint=FieldFootprint(item,pair.Key.StartsWith("Your",System.StringComparison.Ordinal));
            Include(item.restingPosition.y+footprint.yMin,item.restingPosition.y+footprint.yMax,item.restingPosition.x+footprint.xMax);
        }
        foreach(var lane in denseFieldRows.Values){
            if(lane==null||!lane.gameObject.activeInHierarchy)continue;
            var viewport=lane.viewport;
            var min=cardsRoot.InverseTransformPoint(viewport.TransformPoint(viewport.rect.min));
            var max=cardsRoot.InverseTransformPoint(viewport.TransformPoint(viewport.rect.max));
            Include(min.y,max.y,max.x);
        }
        // Clicking a permanent tucks the hand. The HUD changes pose over the field
        // without changing the camera or moving a permanent away from the pointer.
        float ownHeight=RedesignHandTrayHeight-RedesignHandTrayLower;
        float otherHeight=RedesignOpponentHandStrip+(otherSourceTabs!=null?otherSourceTabs.rect.height:36)+8;
        float bottom=surface.rect.yMin+ownHeight+16,top=surface.rect.yMax-otherHeight-16;
        float scale=Mathf.Clamp((top-bottom)/Mathf.Max(1,highest-lowest),.25f,1);
        float phaseTop=surface.rect.yMin+DuelFieldFooterTop+16;
        if(lowestNearFooter<highest)
            scale=Mathf.Min(scale,Mathf.Clamp((top-phaseTop)/(highest-lowestNearFooter),.25f,1));
        float minimum=bottom-lowest*scale;
        if(!float.IsPositiveInfinity(lowestNearFooter))minimum=Mathf.Max(minimum,phaseTop-lowestNearFooter*scale);
        float displacement=Mathf.Clamp(0,minimum,Mathf.Max(minimum,top-highest*scale));
        if(Mathf.Approximately(scale,handFitScale)&&Mathf.Abs(displacement-handFitOffset)<.1f)return;
        handFitScale=scale;handFitOffset=displacement;
        ApplyBattlefieldView();
    }

    private void UpdateBattlefieldViewNavigation()
    {
        if (worldTable == null || cardsRoot == null || EventSystem.current == null) return;
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;
        Vector2 pointer = mouse.position.ReadValue();
        float wheel = mouse.scroll.ReadValue().y;
        bool pressed = mouse.leftButton.wasPressedThisFrame;
        bool held = mouse.leftButton.isPressed;
#else
        Vector2 pointer = Input.mousePosition;
        float wheel = Input.mouseScrollDelta.y;
        bool pressed = Input.GetMouseButtonDown(0);
        bool held = Input.GetMouseButton(0);
#endif
        NavigateBattlefieldView(pointer, wheel, pressed, held);
    }

    internal bool FourSeatInputBlocked => IsFourPlayerScene && (GameSettingsPanel.IsOpen ||
        !viewingBattlefield && (canAct && FourCombatDecision ||
            stackList != null && stackList.gameObject.activeInHierarchy ||
            cardSelectionOverlay != null && cardSelectionOverlay.gameObject.activeInHierarchy ||
            fourCombatRoot != null && fourCombatRoot.gameObject.activeInHierarchy));

    private void NavigateBattlefieldView(Vector2 pointer, float wheel, bool pressed, bool held)
    {
        if (FourSeatInputBlocked) {
            draggingFourSeat = -1;
            draggingBattlefield = false;
            return;
        }
        if (fourPlayerScenePrepared && fourWindowMode) {
            draggingBattlefield=false;
            if(battlefieldPan!=Vector2.zero || !Mathf.Approximately(battlefieldZoom,1)){
                battlefieldPan=Vector2.zero;battlefieldZoom=1;ApplyBattlefieldView();
            }
            NavigateFourSeat(pointer,pressed,held);
            return;
        }
        draggingFourSeat=-1;
        if (GameSettingsPanel.IsOpen) {
            draggingBattlefield = false;
            return;
        }
        if (!held) draggingBattlefield = false;
        if (!RectTransformUtility.RectangleContainsScreenPoint(surface, pointer, canvas.worldCamera)) return;
        redesignPointerHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, redesignPointerHits);
        bool overHud = false, overCard = false;
        foreach (var result in redesignPointerHits) {
            var target = result.gameObject.transform;
            if (target.IsChildOf(cardsRoot)) {
                if (target.GetComponentInParent<ForgeBattlefieldCardPointer>() != null) overCard = true;
                continue;
            }
            if (IsBattlefieldNavigationHud(target)) { overHud = true; break; }
        }
        if (wheel != 0 && !overHud) {
            float next = Mathf.Clamp(battlefieldZoom * (wheel > 0 ? 1.12f : 1 / 1.12f), .75f, 1.8f);
            if (!Mathf.Approximately(next, battlefieldZoom) && RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, pointer, canvas.worldCamera, out var cursor)) {
                battlefieldPan = cursor - (cursor - battlefieldPan) * (next / battlefieldZoom);
                battlefieldZoom = next;
                ApplyBattlefieldView();
            }
        }
        // A drag beginning on a card belongs to that card, and one beginning
        // on a HUD element belongs to the HUD. Only empty stone starts a pan.
        if (pressed && !overHud && !overCard) {
            draggingBattlefield = true;
            battlefieldDragOrigin = pointer;
            battlefieldPanOrigin = battlefieldPan;
        }
        if (draggingBattlefield && held) {
            Vector2 delta = (pointer - battlefieldDragOrigin) / Mathf.Max(.01f, canvas.scaleFactor);
            battlefieldPan = battlefieldPanOrigin + delta;
            ApplyBattlefieldView();
        }
    }

    private void NavigateFourSeat(Vector2 pointer,bool pressed,bool held)
    {
        if(!held||GameSettingsPanel.IsOpen){draggingFourSeat=-1;return;}
        if(!pressed&&draggingFourSeat<0)return;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,pointer,canvas.worldCamera,out var local))return;
        if(pressed){
            draggingFourSeat=-1;
            redesignPointerHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=pointer},redesignPointerHits);
            if(redesignPointerHits.Count>0){
                var top=redesignPointerHits[0].gameObject.transform;
                if(IsBattlefieldNavigationHud(top)||top.IsChildOf(cardsRoot)||
                    fourWindowEdges!=null&&top.IsChildOf(fourWindowEdges))return;
            }
            for(int slot=0;slot<fourWindowPages.Length;slot++){
                var page=fourWindowPages[slot];
                if(page.plate==null||page.count==0)continue;
                Rect bounds=FourSeatBounds(slot);
                bounds.yMax-=page.plate.rect.height;
                if(!bounds.Contains(local))continue;
                draggingFourSeat=slot;battlefieldDragOrigin=local;battlefieldPanOrigin=page.pan;
                break;
            }
        }
        if(draggingFourSeat<0)return;
        var active=fourWindowPages[draggingFourSeat];
        var next=ClampFourPan(draggingFourSeat,battlefieldPanOrigin+local-battlefieldDragOrigin);
        if(next==active.pan)return;
        active.pan=next;
        PositionFourPage(draggingFourSeat);
        if(assignedAttackArrows.Count>0||IsDeclaringAttack)PresentAttackSelection();
        if(blockLines.Count>0||IsBlockingChoice)PresentBlocking();
    }

    private bool IsBattlefieldNavigationHud(Transform target)
    {
        if (target == null) return false;
        if (target.GetComponentInParent<Selectable>() != null || target.GetComponentInParent<ScrollRect>() != null) return true;
        for (var node = target; node != null && node != authored; node = node.parent)
            if (node == battleSidebar || node == ownHand || node == otherHand || node == stackContent
                || node == actionRoot || node == previewRoot || node.name is "CornerCluster" or "GearBox"
                    or "InspectPanel" or "ForgeChoices" or "StackColumn") return true;
        return false;
    }

    private float HorizontalPanLimit(float zoom)=>surface.rect.width*(.18f+.42f*Mathf.Max(0,zoom-1));

    private void ApplyBattlefieldView()
    {
        float x = HorizontalPanLimit(battlefieldZoom);
        float extension=Mathf.Max(ownFieldLanes.rightExtension,otherFieldLanes.rightExtension);
        float extraAtZoom=extension*Mathf.Max(0,battlefieldZoom-1);
        float y = surface.rect.height * (.18f + .42f * Mathf.Max(0, battlefieldZoom - 1));
        battlefieldPan.x = Mathf.Clamp(battlefieldPan.x, -x-extraAtZoom, x);
        battlefieldPan.y = Mathf.Clamp(battlefieldPan.y, -y, y);
        cardsRoot.localScale = Vector3.one * (battlefieldZoom*handFitScale);
        cardsRoot.anchoredPosition = battlefieldPan+Vector2.up*(handFitOffset*battlefieldZoom);
        worldTable.SetView(new Vector2(battlefieldPan.x / surface.rect.width, battlefieldPan.y / surface.rect.height), battlefieldZoom);
        if(assignedAttackArrows.Count>0||IsDeclaringAttack)PresentAttackSelection();
        if(blockLines.Count>0||IsBlockingChoice)PresentBlocking();
    }
}
