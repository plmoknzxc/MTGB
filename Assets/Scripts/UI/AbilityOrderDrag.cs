using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Dragging changes only the local queue. One authoritative action submits it.
public sealed class AbilityOrderDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public bool CanDrag;
    public int Id;
    public Action<int,int> Move;
    public Action Snap;
    private ScrollRect scroll;
    private RectTransform rect, content;
    private bool dragging;
    public void OnBeginDrag(PointerEventData e) {
        if(!CanDrag)return;
        rect=(RectTransform)transform;content=(RectTransform)rect.parent;scroll=GetComponentInParent<ScrollRect>();
        dragging=true;scroll.StopMovement();rect.SetAsLastSibling();
    }
    public void OnDrag(PointerEventData e) {
        if(!dragging||!CanDrag)return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content,e.position,e.pressEventCamera,out var p);
        int target=Mathf.Max(0,Mathf.FloorToInt(p.x/238));Move?.Invoke(Id,target);
        rect.SetAsLastSibling();rect.anchoredPosition=new Vector2(p.x-108,-32);
    }
    public void OnEndDrag(PointerEventData e) {if(!dragging)return;dragging=false;Snap?.Invoke();}
    private void OnDisable(){dragging=false;}
}
