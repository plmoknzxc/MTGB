using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FourSeatResizeHandle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ForgeBattlefieldController controller;
    private RectTransform coordinateSpace;
    private Image grip,rule;
    private Color ruleColor;
    private Color normal;
    private int handle,dragPointer;
    private bool dragging,over;
    private Vector2 previous;
    private static readonly Texture2D[] cursors=new Texture2D[4];
    private static FourSeatResizeHandle cursorOwner;

    internal void Bind(ForgeBattlefieldController owner,int index,RectTransform line,RectTransform thumb)
    {
        controller=owner;handle=index;coordinateSpace=(RectTransform)transform.parent;
        grip=thumb.GetComponent<Image>();normal=grip.color;
        rule=line.GetComponent<Image>();ruleColor=rule.color;
        if(index<3){
            Vector2 stretch=index==2?Vector2.right:Vector2.up;
            line.anchorMin=(Vector2.one-stretch)*.5f;line.anchorMax=line.anchorMin+stretch;
            line.sizeDelta=(Vector2.one-stretch)*2.5f;
            thumb.sizeDelta=index==2?new Vector2(36,4):new Vector2(4,36);
            grip.sprite=IntegratedUiUtility.RoundedRect(thumb.sizeDelta.x,thumb.sizeDelta.y,1.5f);
        }else{
            // A short L on each pane's own side of the junction makes the chosen row unambiguous.
            bool left=index==3||index==5,top=index<=4;
            float x=left?4:-4,y=top?-4:4;
            line.anchoredPosition=new Vector2(x,y+(top?4:-4));line.sizeDelta=new Vector2(2,10);
            thumb.anchoredPosition=new Vector2(x+(left?-4:4),y);thumb.sizeDelta=new Vector2(10,2);
        }
        RefreshCursor();
    }

    internal void SetPresentation(bool resizable,Color edge,Color thumb)
    {
        if(enabled==resizable && ruleColor==edge && normal==thumb)return;
        ruleColor=edge;normal=thumb;
        enabled=resizable;
        GetComponent<Image>().raycastTarget=resizable;
        RefreshCursor();
    }

    public void OnPointerEnter(PointerEventData e){over=true;RefreshCursor();}
    public void OnPointerExit(PointerEventData e){over=false;RefreshCursor();}
    public void OnInitializePotentialDrag(PointerEventData e){e.useDragThreshold=false;}
    public void OnBeginDrag(PointerEventData e){
        if(dragging || controller.FourSeatInputBlocked || e.button!=PointerEventData.InputButton.Left)return;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(coordinateSpace,e.pressPosition,e.pressEventCamera,out previous))return;
        dragging=true;dragPointer=e.pointerId;RefreshCursor();
    }
    public void OnDrag(PointerEventData e){
        if(!dragging || dragPointer!=e.pointerId)return;
        if(controller.FourSeatInputBlocked){CancelDrag();return;}
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(coordinateSpace,e.position,e.pressEventCamera,out var point)){
            controller.DragFourSeatEdge(handle,point-previous);previous=point;
        }
    }
    public void OnEndDrag(PointerEventData e){
        if(!dragging || dragPointer!=e.pointerId)return;
        dragging=false;RefreshCursor();
    }
    internal void CancelDrag()
    {
        if(!dragging && !over)return;
        dragging=over=false;
        RefreshCursor();
    }
    private void OnDisable(){over=dragging=false;RefreshCursor();}
    private void RefreshCursor()
    {
        bool active=over||dragging;
        if(grip!=null){
            grip.color=active?Color.Lerp(normal,Color.white,.75f):normal;
            grip.enabled=active||(enabled&&handle<3);
            rule.color=active?Color.Lerp(ruleColor,normal,.7f):ruleColor;
            rule.enabled=active||handle<3;
        }
        if(!over && !dragging){
            if(cursorOwner==this){Cursor.SetCursor(null,Vector2.zero,CursorMode.Auto);cursorOwner=null;}
            return;
        }
        if(cursorOwner!=null && cursorOwner!=this && cursorOwner.dragging)return;
        int axis=handle<2?0:handle==2?1:handle==3||handle==6?2:3;
        if(cursors[axis]==null)cursors[axis]=CreateCursor(axis);
        Cursor.SetCursor(cursors[axis],new Vector2(16,16),CursorMode.Auto);cursorOwner=this;
    }

    private static Texture2D CreateCursor(int axis)
    {
        Vector2 direction=axis==0?Vector2.right:axis==1?Vector2.up:new Vector2(1,axis==2?-1:1).normalized;
        Vector2 across=new Vector2(-direction.y,direction.x);
        var texture=new Texture2D(32,32,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};
        var pixels=new Color32[32*32];
        // Two open chevrons around a short grip. Distance fields preserve the
        // same rounded stroke and soft keyline at all four native cursor angles.
        Vector2 start=new Vector2(5,3.5f),end=new Vector2(9,0),segment=end-start;
        for(int y=0;y<32;y++)for(int x=0;x<32;x++){
            Vector2 p=new Vector2(x-15.5f,y-15.5f);
            var uv=new Vector2(Mathf.Abs(Vector2.Dot(p,direction)),Mathf.Abs(Vector2.Dot(p,across)));
            float t=Mathf.Clamp01(Vector2.Dot(uv-start,segment)/segment.sqrMagnitude);
            float arrow=Vector2.Distance(uv,start+t*segment);
            float gripDistance=new Vector2(uv.x,Mathf.Max(0,uv.y-2.5f)).magnitude;
            float distance=Mathf.Min(gripDistance,arrow);
            Color color=Color.Lerp(new Color(.09f,.10f,.14f),new Color(.92f,.94f,.98f),Mathf.Clamp01(1.55f-distance));
            color.a=Mathf.Clamp01(2.7f-distance);
            pixels[y*32+x]=color;
        }
        texture.SetPixels32(pixels);texture.Apply();return texture;
    }
}
