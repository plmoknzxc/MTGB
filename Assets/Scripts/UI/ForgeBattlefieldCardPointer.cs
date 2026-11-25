using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

// Keeps a stable hit surface while the card face lifts. Layout and input use Canvas coordinates.
public sealed class ForgeBattlefieldCardPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, UnityEngine.ICanvasRaycastFilter
{
    // Raising artwork must not move its original hit area above neighbouring
    // cards. Resolve the resting fan order once per raycast point instead.
    public sealed class HandHitLayout
    {
        public bool LeftOnTop {get;}
        public HandHitLayout(bool leftOnTop=false){LeftOnTop=leftOnTop;}
        private readonly System.Collections.Generic.List<ForgeBattlefieldCardPointer> cards=new();
        private int hitFrame=-1;
        private Vector2 hitPoint;
        private Camera hitCamera;
        private ForgeBattlefieldCardPointer hitCard;
        public void Clear(){cards.Clear();hitFrame=-1;}
        public void Invalidate(){hitFrame=-1;}
        public void Add(ForgeBattlefieldCardPointer card){cards.Add(card);card.handHitLayout=this;hitFrame=-1;}
        public bool Accept(ForgeBattlefieldCardPointer card,Vector2 point,Camera camera)
        {
            if(hitFrame!=Time.frameCount||hitPoint!=point||hitCamera!=camera) {
                hitFrame=Time.frameCount;hitPoint=point;hitCamera=camera;hitCard=null;
                for(int i=0;i<cards.Count;i++) {
                    var candidate=cards[LeftOnTop?i:cards.Count-1-i];if(candidate==null||!candidate.isActiveAndEnabled||!candidate.handFanVisible||candidate.root==null)continue;
                    if(RectTransformUtility.ScreenPointToLocalPointInRectangle(candidate.root,point,camera,out var local)&&candidate.root.rect.Contains(local)) {
                        hitCard=candidate;break;
                    }
                }
            }
            // The lifted face is interactive above the fan, while the resting
            // fan itself always selects the card visibly exposed at that point.
            return hitCard==card||hitCard==null&&card.hovering;
        }
    }
    private HandHitLayout handHitLayout;
    private ForgeHandFanNavigation handNavigation;
    private bool browsingHand,handFanVisible=true;
    public bool IsCastingDrag=>dragging;
    public bool IsHovered=>hovering&&!dragging;
    public bool LeftHandCardOnTop=>handHitLayout?.LeftOnTop==true;
    public event Action VisualStateChanged;
    public void ConfigureHandNavigation(ForgeHandFanNavigation navigation)=>handNavigation=navigation;
    public void SetHandFanExposure(bool visible){if(handFanVisible!=visible){handFanVisible=visible;handHitLayout?.Invalidate();}}
    public void SetHandFanPose(Vector2 position,float rotation,bool snap)
    {destination=position;angle=rotation;if(snap&&!dragging){root.anchoredPosition=position;root.localRotation=Quaternion.Euler(0,0,rotation);}}
    public bool IsRaycastLocationValid(Vector2 screenPoint,Camera eventCamera)=>handHitLayout==null||handHitLayout.Accept(this,screenPoint,eventCamera);
    private Action enter, exit, play;
    private bool draggable, dragging, hovering, animated, initialized, hand;
    private RectTransform root, face;
    private UnityEngine.UI.Graphic hoverSurface;
    private Vector2 destination;
    private float angle;
    private int sibling;
    private int handOrder;
    private Vector3 initial;
    private Action<Vector2> combatBegin, combatMove, combatEnd, activeCombatEnd;
    private bool combatDragging;
    private float hoverStartedAt;
    private bool detailShown;
    private UnityEngine.UI.ScrollRect scrollDrag, scrollParent;
    public bool AllowHandHover { get; set; } = true;
    public void ConfigureScrollParent(UnityEngine.UI.ScrollRect scroll) => scrollParent = scroll;
    private RectTransform playRegion,dragSpace,handJournal;
    private Rect dragSourceBounds;
    private Vector2 dragGrip;
    private const float DragScale=1.12f;
    private Action activePlay;
    public void ConfigurePlayRegion(RectTransform region)=>playRegion=region;
    public void ConfigureHandJournal(RectTransform journal)=>handJournal=journal;
    public void DisableActions(){draggable=false;play=null;ConfigureCombat(null,null,null);}
    public void InspectOnClick()
    {
        detailShown=true;
        enter?.Invoke();
    }
    public void ResetHover()
    {
        hovering=false;detailShown=false;
        if(hoverSurface!=null)hoverSurface.raycastTarget=false;
        if(face!=null){face.localPosition=Vector3.zero;face.localScale=Vector3.one;face.localRotation=Quaternion.identity;}
        VisualStateChanged?.Invoke();
    }
    public void ConfigureCombat(Action<Vector2> begin,Action<Vector2> move,Action<Vector2> end)
    { combatBegin=begin;combatMove=move;combatEnd=end; }
    public void Configure(Action onEnter, Action onExit, Action onPlay, bool canDrag)
    { enter=onEnter; exit=onExit; play=onPlay; draggable=canDrag; }
    public void SetLayout(Vector2 position, float rotation, RectTransform visual, bool isHand, int order=0)
    {
        root=(RectTransform)transform; face=visual; hand=isHand; handOrder=order; destination=position; angle=rotation; animated=true;
        foreach(var graphic in face.GetComponentsInChildren<UnityEngine.UI.Graphic>())
            if(graphic.name=="Border")hoverSurface=graphic;
        if(!initialized) {root.anchoredPosition=position+new Vector2(0,isHand?-32:12); initialized=true;}
    }
    // Existing field card moved with its window. Keep the input anchor under its mesh.
    public void ResizeFieldSlot(Vector2 position, Vector2 size)
    {
        if(hand || root==null || face==null)return;
        destination=root.anchoredPosition=position;
        root.sizeDelta=face.sizeDelta=size;
    }

    private void LateUpdate()
    {
        if(hovering&&!dragging&&!combatDragging&&!detailShown&&Time.unscaledTime-hoverStartedAt>=(animated&&!hand ? .35f : .75f)){detailShown=true;enter?.Invoke();}
        if(!animated || root==null || face==null)return;
        if(dragging){
            float pickup=1-Mathf.Exp(-20*Mathf.Min(Time.unscaledDeltaTime,.05f));
            face.localPosition=Vector3.Lerp(face.localPosition,Vector3.zero,pickup);
            face.localScale=Vector3.Lerp(face.localScale,Vector3.one*DragScale,pickup);
            face.localRotation=Quaternion.Slerp(face.localRotation,Quaternion.identity,pickup);
            return;
        }
        float step=hand?Time.unscaledDeltaTime:Mathf.Min(Time.unscaledDeltaTime,.05f);
        float speed=1-Mathf.Exp(-(hand?18:12)*step);
        root.anchoredPosition=Vector2.Lerp(root.anchoredPosition,destination,speed);
        // Overlay Canvas has no perspective camera. X/Y rotation shears/compresses its
        // artwork rather than placing it on a real table; use only in-plane rotation.
        var tableRotation=Quaternion.Euler(0,0,angle);
        root.localRotation=Quaternion.Slerp(root.localRotation,tableRotation,speed);
        // Lift in table coordinates, keeping the end cards from sliding sideways.
        bool topHand=root.parent.name=="OpponentHandRoot";
        const float hoverScale=1.8f;
        // Grow inward, preserving the visible outer edge of either hand.
        float extraLift=face.rect.height*(hoverScale-1.25f)*.5f;
        // Resting fans may tuck outward to clear the battlefield; hover still lifts fully into view.
        float restingInset=hand?((RectTransform)root.parent).anchoredPosition.y:0;
        var lift=Quaternion.Inverse(tableRotation)*new Vector3(0,(topHand?-184-extraLift:154+extraLift)-restingInset,0);
        if(hovering && hand && handJournal!=null && handJournal.gameObject.activeInHierarchy){
            var center=root.TransformPoint(lift);
            var half=Vector2.Scale(face.rect.size,root.lossyScale)*(.5f*hoverScale);
            var min=handJournal.TransformPoint(handJournal.rect.min);
            var max=handJournal.TransformPoint(handJournal.rect.max);
            if(center.y+half.y>min.y && center.y-half.y<max.y){
                float gap=10*handJournal.lossyScale.x;
                float offset=Mathf.Max(0,max.x+gap+half.x-center.x);
                lift+=root.InverseTransformVector(Vector3.right*offset);
            }
        }
        face.localPosition=Vector3.Lerp(face.localPosition,hovering && hand?lift:Vector3.zero,speed);
        face.localScale=Vector3.Lerp(face.localScale,Vector3.one*(hovering && hand?hoverScale:1),speed);
        // Hover straightens a fanned hand card, but must preserve a permanent's tapped orientation.
        face.localRotation=Quaternion.Slerp(face.localRotation,Quaternion.Euler(0,0,hovering && hand?-angle:0),speed);
        // The lifted face stays clickable while crossing from the hand's original hit area.
        if(hoverSurface!=null)hoverSurface.raycastTarget=hovering;
    }
    private void OnDisable() { bool open=detailShown;if(browsingHand&&handNavigation!=null)handNavigation.CancelDrag();dragging=false;browsingHand=false;activePlay=null;combatDragging=false;activeCombatEnd=null;scrollDrag=null;ResetHover(); if(open)exit?.Invoke(); }
    public void OnPointerEnter(PointerEventData eventData)
    {if(hand && !AllowHandHover || dragging || hovering || handNavigation!=null&&handNavigation.IsDragging)return; hovering=true;detailShown=hand;hoverStartedAt=Time.unscaledTime; sibling=transform.GetSiblingIndex(); transform.SetAsLastSibling();VisualStateChanged?.Invoke();if(hand)enter?.Invoke();}
    public void OnPointerExit(PointerEventData eventData)
    {if(dragging || !hovering)return; hovering=false;detailShown=false;VisualStateChanged?.Invoke();RestoreOrder(); exit?.Invoke();}
    public static void RestoreHandOrder(Transform parent)
    {
        var cards=parent.GetComponentsInChildren<ForgeBattlefieldCardPointer>(false)
            .Where(p=>p.hand && p.transform.parent==parent).OrderBy(p=>p.handOrder).ToArray();
        // Expose the right edges of the playable fan, including its current fees.
        var resting=cards.Any(p=>p.handHitLayout?.LeftOnTop==true)?cards.Reverse():cards.AsEnumerable();
        foreach(var card in resting)card.transform.SetAsLastSibling();
        foreach(var card in cards.Where(p=>p.hovering || p.dragging))card.transform.SetAsLastSibling();
    }
    private void RestoreOrder()
    {if(hand)RestoreHandOrder(transform.parent);else transform.SetSiblingIndex(sibling);}
    public void OnBeginDrag(PointerEventData eventData)
    {
        // Unity dispatches the click before end-drag when the pressed button
        // also owns the drag. A gesture beyond the threshold has claimed it.
        if(eventData.button==PointerEventData.InputButton.Left)eventData.eligibleForClick=false;
        if(eventData.button==PointerEventData.InputButton.Left && combatBegin!=null)
        {
            combatDragging=true;activeCombatEnd=combatEnd;ResetHover();exit?.Invoke();
            combatBegin(eventData.position);return;
        }
        var delta=eventData.position-eventData.pressPosition;
        if(handNavigation!=null&&handNavigation.CanBrowse&&(eventData.button==PointerEventData.InputButton.Middle
            ||eventData.button==PointerEventData.InputButton.Left&&Mathf.Abs(delta.x)>=Mathf.Abs(delta.y))) {
            browsingHand=true;ResetHover();exit?.Invoke();handNavigation.OnBeginDrag(eventData);return;
        }
        if(!draggable || eventData.button!=PointerEventData.InputButton.Left) {
            scrollDrag=(UnityEngine.UI.ScrollRect)GetComponentInParent<BattlefieldHorizontalScroll>() ?? scrollParent;
            if(scrollDrag!=null){ResetHover();exit?.Invoke();scrollDrag.OnBeginDrag(eventData);}return;
        }
        if(!hovering)sibling=transform.GetSiblingIndex();
        root=(RectTransform)transform;
        dragSpace=playRegion!=null?(RectTransform)playRegion.parent:(RectTransform)root.parent;
        var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(dragSpace,playRegion!=null?playRegion:root);
        dragSourceBounds=new Rect(bounds.min,bounds.size);
        activePlay=play;
        dragging=true; hovering=false; initial=transform.position; transform.SetAsLastSibling();
        dragGrip=Vector2.zero;
        if(face!=null){
            // The resting slot and its raised illustration are both valid hit surfaces.
            // Keep the visible pose on pickup, then settle into the same grip on the smaller card.
            var center=face.position;
            var rotation=face.rotation;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(face,eventData.pressPosition,eventData.pressEventCamera,out dragGrip)
                ||!face.rect.Contains(dragGrip))
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root,eventData.pressPosition,eventData.pressEventCamera,out dragGrip);
            dragGrip=Vector2.Min(face.rect.max,Vector2.Max(face.rect.min,dragGrip));
            transform.localRotation=Quaternion.identity;
            MoveDraggedCard(eventData.pressPosition,eventData.pressEventCamera);
            face.SetPositionAndRotation(center,rotation);
        }
        exit?.Invoke();
        VisualStateChanged?.Invoke();
    }
    private void MoveDraggedCard(Vector2 screen,Camera camera)
    {
        if(RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)transform.parent,screen,camera,out var point))
            transform.position=point-transform.TransformVector((Vector3)dragGrip*DragScale);
    }
    public void OnDrag(PointerEventData eventData)
    {
        if(browsingHand){handNavigation.OnDrag(eventData);return;}
        if(combatDragging){combatMove?.Invoke(eventData.position);return;}
        if(scrollDrag!=null){scrollDrag.OnDrag(eventData);return;}
        if(!dragging)return;
        MoveDraggedCard(eventData.position,eventData.pressEventCamera);
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        if(browsingHand){browsingHand=false;handNavigation.OnEndDrag(eventData);return;}
        if(scrollDrag!=null){scrollDrag.OnEndDrag(eventData);scrollDrag=null;eventData.eligibleForClick=false;return;}
        if(combatDragging)
        {
            combatDragging=false;eventData.eligibleForClick=false;
            var finish=activeCombatEnd;activeCombatEnd=null;finish?.Invoke(eventData.position);return;
        }
        if(!dragging)return;
        MoveDraggedCard(eventData.position,eventData.pressEventCamera);
        dragging=false;eventData.eligibleForClick=false;
        VisualStateChanged?.Invoke();
        bool releasedOnBoard=eventData.position.y>Screen.height*.28f && eventData.position.y<Screen.height*.88f
            && eventData.position.x>Screen.width*.12f && eventData.position.x<Screen.width*.83f;
        bool clearOfSource=false;
        if(dragSpace!=null&&RectTransformUtility.ScreenPointToLocalPointInRectangle(dragSpace,eventData.position,eventData.pressEventCamera,out var point)) {
            var half=root.rect.size*.56f;const float clearance=24;
            clearOfSource=point.x-half.x>dragSourceBounds.xMax+clearance||point.x+half.x<dragSourceBounds.xMin-clearance
                ||point.y-half.y>dragSourceBounds.yMax+clearance||point.y+half.y<dragSourceBounds.yMin-clearance;
        }
        var playAction=activePlay;activePlay=null;
        if(!animated)transform.position=initial;
        RestoreOrder();
        if(draggable&&releasedOnBoard&&clearOfSource)playAction?.Invoke();
        else if(IsOverRestingHandSlot(eventData))OnPointerEnter(eventData);
    }
    private bool IsOverRestingHandSlot(PointerEventData data)
    {
        if(!hand||!animated||data.pointerCurrentRaycast.gameObject?.GetComponentInParent<ForgeBattlefieldCardPointer>()!=this)return false;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)root.parent,data.position,data.pressEventCamera,out var point))return false;
        // The dragged root is still under the cursor. Hit-test its resting pose
        // so releasing above a neighbouring card won't reopen this one's hover.
        Vector2 restingPivot=(Vector2)root.localPosition+(destination-root.anchoredPosition);
        Vector3 local=Quaternion.Euler(0,0,-angle)*(point-restingPivot);
        return root.rect.Contains(new Vector2(local.x/root.localScale.x,local.y/root.localScale.y));
    }
}
