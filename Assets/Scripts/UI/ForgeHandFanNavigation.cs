using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Browsing is latched for the whole gesture, including its release.
public sealed class ForgeHandFanNavigation : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public float Offset { get; private set; }
    public bool IsDragging { get; private set; }
    public bool CanPan=>isActiveAndEnabled&&limit>.1f;
    private float limit,startOffset;
    private Vector2 start;
    private RectTransform space;
    private Action<float> moved;
    public void Configure(RectTransform inputSpace,float travel,Action<float> onMoved)
    {space=inputSpace;limit=Mathf.Max(0,travel)*.5f;moved=onMoved;Offset=Mathf.Clamp(Offset,-limit,limit);}
    public void MoveBy(float distance){if(CanPan)SetOffset(Offset+distance);}
    public void CancelDrag(){IsDragging=false;}
    private void SetOffset(float value){Offset=Mathf.Clamp(value,-limit,limit);moved?.Invoke(Offset);}
    public void OnBeginDrag(PointerEventData data)
    {
        if(!CanPan||data.button is not (PointerEventData.InputButton.Left or PointerEventData.InputButton.Middle))return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(space,data.pressPosition,data.pressEventCamera,out start);
        startOffset=Offset;IsDragging=true;data.eligibleForClick=false;
    }
    public void OnDrag(PointerEventData data)
    {
        if(!IsDragging)return;
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(space,data.position,data.pressEventCamera,out var point))SetOffset(startOffset+point.x-start.x);
    }
    public void OnEndDrag(PointerEventData data)
    {
        if(!IsDragging)return;OnDrag(data);IsDragging=false;data.eligibleForClick=false;
        data.pointerEnter?.GetComponentInParent<ForgeBattlefieldCardPointer>()?.OnPointerEnter(data);
    }
    private void OnDisable(){CancelDrag();}
}
