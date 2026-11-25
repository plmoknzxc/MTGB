using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Browsing is latched for the whole gesture, including its release.
public sealed class ForgeHandFanNavigation : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public float Offset { get; private set; }
    public bool IsDragging { get; private set; }
    public bool CanPan=>isActiveAndEnabled&&limit>.1f;
    public bool CanBrowse=>isActiveAndEnabled&&(limit>.1f||turnPage!=null);
    private float limit,startOffset;
    private Vector2 start;
    private RectTransform space;
    private Action<float> moved;
    private Action<int> turnPage;
    public void Configure(RectTransform inputSpace,float travel,Action<float> onMoved)
    {space=inputSpace;limit=Mathf.Max(0,travel)*.5f;moved=onMoved;turnPage=null;Offset=Mathf.Clamp(Offset,-limit,limit);}
    public void ConfigurePaging(RectTransform inputSpace,Action<int> onTurn)
    {Configure(inputSpace,0,null);turnPage=onTurn;}
    public void MoveBy(float distance){if(CanPan)SetOffset(Offset+distance);}
    public void CancelDrag(){IsDragging=false;}
    private void SetOffset(float value){Offset=Mathf.Clamp(value,-limit,limit);moved?.Invoke(Offset);}
    public void OnBeginDrag(PointerEventData data)
    {
        if(!CanBrowse||data.button is not (PointerEventData.InputButton.Left or PointerEventData.InputButton.Middle))return;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(space,data.pressPosition,data.pressEventCamera,out start))return;
        startOffset=Offset;IsDragging=true;data.eligibleForClick=false;
    }
    public void OnDrag(PointerEventData data)
    {
        if(!IsDragging||turnPage!=null)return;
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(space,data.position,data.pressEventCamera,out var point))SetOffset(startOffset+point.x-start.x);
    }
    public void OnEndDrag(PointerEventData data)
    {
        if(!IsDragging)return;
        if(turnPage!=null){
            IsDragging=false;data.eligibleForClick=false;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(space,data.position,data.pressEventCamera,out var end)){
                Vector2 travel=end-start;
                if(Mathf.Abs(travel.x)>=42 && Mathf.Abs(travel.x)>Mathf.Abs(travel.y))
                    turnPage(travel.x<0?1:-1);
            }
            return;
        }
        OnDrag(data);IsDragging=false;data.eligibleForClick=false;
        data.pointerEnter?.GetComponentInParent<ForgeBattlefieldCardPointer>()?.OnPointerEnter(data);
    }
    private void OnDisable(){CancelDrag();}
}
