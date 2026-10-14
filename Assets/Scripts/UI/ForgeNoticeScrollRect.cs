using UnityEngine;
using UnityEngine.EventSystems;

// Read-only long result text: ordinary short notifications pass input through.
public sealed class ForgeNoticeScrollRect : UnityEngine.UI.ScrollRect,IPointerEnterHandler,IPointerExitHandler
{
    private bool scrollable,hovered,dragging;
    private float interactedUntil;
    public bool Reading=>scrollable&&(hovered||dragging||Time.unscaledTime<interactedUntil);
    public void SetScrollable(bool value)
    {
        scrollable=value;vertical=value;
        if(!value){hovered=dragging=false;interactedUntil=0;}
    }
    public void OnPointerEnter(PointerEventData data){if(scrollable)hovered=true;}
    public void OnPointerExit(PointerEventData data){hovered=false;}
    public override void OnScroll(PointerEventData data)
    {
        if(!scrollable)return;
        interactedUntil=Time.unscaledTime+.8f;base.OnScroll(data);
    }
    public override void OnBeginDrag(PointerEventData data)
    {
        if(!scrollable||data.button!=PointerEventData.InputButton.Left)return;
        dragging=true;base.OnBeginDrag(data);
    }
    public override void OnEndDrag(PointerEventData data)
    {
        base.OnEndDrag(data);dragging=false;interactedUntil=Time.unscaledTime+.8f;
    }
    protected override void OnDisable()
    {
        base.OnDisable();hovered=dragging=false;interactedUntil=0;
    }
}
