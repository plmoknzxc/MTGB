using UnityEngine;
using UnityEngine.EventSystems;

// Only the window header drags. Card scrolling and the close button keep
// their own pointer actions; no game choice is sent by moving a notification.
public sealed class ForgeCardNoticeDrag:MonoBehaviour,IPointerDownHandler,IDragHandler
{
    public RectTransform Window;
    private Vector2 offset;
    public void OnPointerDown(PointerEventData e)
    {
        if(Window==null||e.button!=PointerEventData.InputButton.Left)return;
        Window.SetAsLastSibling();
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(Window.parent as RectTransform,e.position,e.pressEventCamera,out var p))offset=Window.anchoredPosition-p;
    }
    public void OnDrag(PointerEventData e)
    {
        if(Window==null||e.button!=PointerEventData.InputButton.Left)return;
        var parent=Window.parent as RectTransform;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,e.position,e.pressEventCamera,out var p))return;
        p+=offset;var half=Window.rect.size*.5f;
        Window.anchoredPosition=new Vector2(Mathf.Clamp(p.x,parent.rect.xMin+half.x,parent.rect.xMax-half.x),Mathf.Clamp(p.y,parent.rect.yMin+half.y,parent.rect.yMax-half.y));
    }
}
