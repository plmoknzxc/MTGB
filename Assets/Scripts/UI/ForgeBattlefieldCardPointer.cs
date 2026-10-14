using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

// Keeps a stable hit surface while the card face lifts. Layout and input use Canvas coordinates.
public sealed class ForgeBattlefieldCardPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
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
    private BattlefieldHorizontalScroll scrollDrag;
    public void ResetHover()
    {
        hovering=false;detailShown=false;
        if(hoverSurface!=null)hoverSurface.raycastTarget=false;
        if(face!=null){face.localPosition=Vector3.zero;face.localScale=Vector3.one;face.localRotation=Quaternion.identity;}
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
    private void LateUpdate()
    {
        if(hovering&&!dragging&&!combatDragging&&!detailShown&&Time.unscaledTime-hoverStartedAt>=.75f){detailShown=true;enter?.Invoke();}
        if(!animated || dragging || root==null || face==null)return;
        float speed=1-Mathf.Exp(-18*Time.unscaledDeltaTime);
        root.anchoredPosition=Vector2.Lerp(root.anchoredPosition,destination,speed);
        // Overlay Canvas has no perspective camera. X/Y rotation shears/compresses its
        // artwork rather than placing it on a real table; use only in-plane rotation.
        var tableRotation=Quaternion.Euler(0,0,angle);
        root.localRotation=Quaternion.Slerp(root.localRotation,tableRotation,speed);
        // Lift in table coordinates, keeping the end cards from sliding sideways.
        bool topHand=root.parent.name=="OpponentHandRoot";
        var lift=Quaternion.Inverse(tableRotation)*new Vector3(0,topHand?-130:130,0);
        face.localPosition=Vector3.Lerp(face.localPosition,hovering && hand?lift:Vector3.zero,speed);
        face.localScale=Vector3.Lerp(face.localScale,Vector3.one*(hovering && hand?1.32f:1),speed);
        // Hover straightens a fanned hand card, but must preserve a permanent's tapped orientation.
        face.localRotation=Quaternion.Slerp(face.localRotation,Quaternion.Euler(0,0,hovering && hand?(topHand?180:0)-angle:0),speed);
        // The lifted face stays clickable while crossing from the hand's original hit area.
        if(hoverSurface!=null)hoverSurface.raycastTarget=hovering;
    }
    private void OnDisable() { bool open=detailShown; ResetHover(); if(open)exit?.Invoke(); }
    public void OnPointerEnter(PointerEventData eventData)
    {if(dragging || hovering)return; hovering=true;detailShown=false;hoverStartedAt=Time.unscaledTime; sibling=transform.GetSiblingIndex(); transform.SetAsLastSibling();}
    public void OnPointerExit(PointerEventData eventData)
    {if(dragging || !hovering)return; hovering=false;detailShown=false; RestoreOrder(); exit?.Invoke();}
    public static void RestoreHandOrder(Transform parent)
    {
        var cards=parent.GetComponentsInChildren<ForgeBattlefieldCardPointer>(false)
            .Where(p=>p.hand && p.transform.parent==parent).OrderBy(p=>p.handOrder).ToArray();
        foreach(var card in cards)card.transform.SetAsLastSibling();
        foreach(var card in cards.Where(p=>p.hovering || p.dragging))card.transform.SetAsLastSibling();
    }
    private void RestoreOrder()
    {if(hand)RestoreHandOrder(transform.parent);else transform.SetSiblingIndex(sibling);}
    public void OnBeginDrag(PointerEventData eventData)
    {
        if(eventData.button==PointerEventData.InputButton.Left && combatBegin!=null)
        {
            combatDragging=true;activeCombatEnd=combatEnd;ResetHover();exit?.Invoke();
            combatBegin(eventData.position);return;
        }
        if(!draggable || eventData.button!=PointerEventData.InputButton.Left) {
            scrollDrag=GetComponentInParent<BattlefieldHorizontalScroll>();
            if(scrollDrag!=null){ResetHover();exit?.Invoke();scrollDrag.OnBeginDrag(eventData);}return;
        }
        if(!hovering)sibling=transform.GetSiblingIndex();
        dragging=true; hovering=false; initial=transform.position; transform.SetAsLastSibling(); exit?.Invoke();
        if(face!=null){face.localPosition=Vector3.zero;face.localRotation=Quaternion.identity;face.localScale=Vector3.one*1.12f;}
        transform.localRotation=Quaternion.identity;
    }
    public void OnDrag(PointerEventData eventData)
    {
        if(combatDragging){combatMove?.Invoke(eventData.position);return;}
        if(scrollDrag!=null){scrollDrag.OnDrag(eventData);return;}
        if(!dragging)return;
        if(RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)transform.parent,eventData.position,eventData.pressEventCamera,out var point))transform.position=point;
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        if(scrollDrag!=null){scrollDrag.OnEndDrag(eventData);scrollDrag=null;eventData.eligibleForClick=false;return;}
        if(combatDragging)
        {
            combatDragging=false;eventData.eligibleForClick=false;
            var finish=activeCombatEnd;activeCombatEnd=null;finish?.Invoke(eventData.position);return;
        }
        if(!dragging)return;
        dragging=false;eventData.eligibleForClick=false;
        bool releasedOnBoard=eventData.position.y>Screen.height*.28f && eventData.position.y<Screen.height*.88f
            && eventData.position.x>Screen.width*.12f && eventData.position.x<Screen.width*.83f;
        if(!animated)transform.position=initial;
        RestoreOrder();
        if(releasedOnBoard)play?.Invoke();
    }
}
