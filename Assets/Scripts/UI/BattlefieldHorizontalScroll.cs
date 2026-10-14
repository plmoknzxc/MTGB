using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Pixel-based dragging, with a short settle to whole columns. Never scale a
// small pointer delta by the length of the content (large boards can be huge).
public sealed class BattlefieldHorizontalScroll : ScrollRect
{
    public float columnWidth=96;
    public bool hidePartialChildren;
    private float target;
    private bool settling;
    private RectTransform navigation;
    private Button previous,next;
    public void Configure(float stride)
    {
        columnWidth=Mathf.Max(1,stride);horizontal=true;vertical=false;
        inertia=false;movementType=MovementType.Clamped;
        velocity=Vector2.zero;
        if(content!=null) {
            float offset=Mathf.Clamp(-content.anchoredPosition.x,0,Travel);
            content.anchoredPosition=new Vector2(-offset,0);
            if(settling)target=Mathf.Clamp(target,0,Travel);
        }
    }
    private float Travel=>content==null||viewport==null?0:Mathf.Max(0,content.rect.width-viewport.rect.width);
    public override void OnBeginDrag(PointerEventData data){settling=false;base.OnBeginDrag(data);}
    public override void OnEndDrag(PointerEventData data)
    {
        base.OnEndDrag(data);velocity=Vector2.zero;
        SetTarget(Mathf.Round(-content.anchoredPosition.x/columnWidth)*columnWidth);
    }
    public override void OnScroll(PointerEventData data)
    {
        if(!IsActive()||Travel<=0)return;
        float delta=Mathf.Abs(data.scrollDelta.x)>Mathf.Abs(data.scrollDelta.y)?data.scrollDelta.x:-data.scrollDelta.y;
        if(Mathf.Abs(delta)<.001f)return;
        SetTarget((settling?target:-content.anchoredPosition.x)+Mathf.Clamp(delta,-1,1)*columnWidth);
    }
    private void SetTarget(float value){target=Mathf.Clamp(value,0,Travel);settling=true;}
    private Button NavigationButton(string name,float anchor,string glyph,int direction)
    {
        var root=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));root.transform.SetParent(navigation,false);
        var rect=(RectTransform)root.transform;rect.anchorMin=rect.anchorMax=new Vector2(anchor,.5f);rect.sizeDelta=new Vector2(32,40);rect.anchoredPosition=Vector2.zero;
        var image=root.GetComponent<Image>();image.sprite=IntegratedUiUtility.RoundedRect(32,40,10);image.color=new Color(.07f,.15f,.19f,.96f);
        var outline=root.AddComponent<Outline>();outline.effectDistance=new Vector2(1,-1);outline.effectColor=new Color(.4f,.69f,.8f,.8f);
        var button=root.GetComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.ColorTint;
        var colors=button.colors;colors.highlightedColor=new Color(1.4f,1.4f,1.4f);colors.disabledColor=new Color(.5f,.5f,.5f,.65f);colors.fadeDuration=.1f;button.colors=colors;
        button.onClick.AddListener(()=>SetTarget((settling?target:-content.anchoredPosition.x)+direction*columnWidth));
        var label=new GameObject("Chevron",typeof(RectTransform),typeof(BattlefieldScrollChevron));label.transform.SetParent(root.transform,false);
        var text=label.GetComponent<BattlefieldScrollChevron>();text.direction=direction;text.color=new Color(.8f,.93f,.98f);text.raycastTarget=false;
        var labelRect=(RectTransform)label.transform;labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
        return button;
    }
    private void UpdateNavigation()
    {
        if(Travel<=.1f){if(navigation!=null)navigation.gameObject.SetActive(false);return;}
        if(navigation==null) {
            var go=new GameObject(name+"_Navigation",typeof(RectTransform));go.transform.SetParent(transform.parent,false);navigation=(RectTransform)go.transform;
            previous=NavigationButton("Previous",0,"‹",-1);next=NavigationButton("Next",1,"›",1);
        }
        navigation.gameObject.SetActive(true);navigation.SetSiblingIndex(transform.GetSiblingIndex()+1);
        var rect=(RectTransform)transform;navigation.anchorMin=rect.anchorMin;navigation.anchorMax=rect.anchorMax;navigation.pivot=rect.pivot;
        navigation.anchoredPosition=rect.anchoredPosition;navigation.sizeDelta=rect.rect.size+new Vector2(32,0);
        float offset=settling?target:-content.anchoredPosition.x;previous.interactable=offset>.1f;next.interactable=offset<Travel-.1f;
        previous.GetComponentInChildren<BattlefieldScrollChevron>().color=new Color(.8f,.93f,.98f,previous.interactable?1:.35f);
        next.GetComponentInChildren<BattlefieldScrollChevron>().color=new Color(.8f,.93f,.98f,next.interactable?1:.35f);
    }
    protected override void OnDisable(){base.OnDisable();settling=false;if(navigation!=null)navigation.gameObject.SetActive(false);}
    protected override void OnDestroy(){if(navigation!=null)Destroy(navigation.gameObject);base.OnDestroy();}
    protected override void LateUpdate()
    {
        base.LateUpdate();
        if(content==null)return;
        UpdateNavigation();
        if(settling) {
            float x=Mathf.Lerp(-content.anchoredPosition.x,target,1-Mathf.Exp(-22*Time.unscaledDeltaTime));
            if(Mathf.Abs(x-target)<.1f){x=target;settling=false;}
            content.anchoredPosition=new Vector2(-x,0);velocity=Vector2.zero;
        }
        if(hidePartialChildren)foreach(RectTransform child in content) {
            if(!child.gameObject.activeSelf)continue;
            var corners=new Vector3[4];child.GetWorldCorners(corners);bool fits=true;
            foreach(var corner in corners){var p=viewport.InverseTransformPoint(corner);fits &= p.x>=viewport.rect.xMin-.5f&&p.x<=viewport.rect.xMax+.5f&&p.y>=viewport.rect.yMin-.5f&&p.y<=viewport.rect.yMax+.5f;}
            var group=child.GetComponent<CanvasGroup>();if(group==null)group=child.gameObject.AddComponent<CanvasGroup>();group.alpha=fits?1:0;group.blocksRaycasts=fits;
        }
    }
}
