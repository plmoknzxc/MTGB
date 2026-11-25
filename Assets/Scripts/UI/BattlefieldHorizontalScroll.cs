using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Pixel-based dragging, with a short settle to whole columns. Never scale a
// small pointer delta by the length of the content (large boards can be huge).
public sealed class BattlefieldHorizontalScroll : ScrollRect
{
    public float columnWidth=96;
    public bool hidePartialChildren;
    public bool smoothIcons;
    internal MenuThemeStyle DecisionTheme;
    internal MenuThemeStyle LaneTheme;
    public bool wheelControlsBoard;
    public bool alignNavigationToVisibleCards;
    public float navigationLeftCardGap=4;
    public float navigationRightCardGap=4;
    public float navigationLeftOutsideAllowance;
    private float[] columnStarts;
    private float target;
    private bool settling;
    private RectTransform navigation;
    private Button previous,next;
    private BattlefieldScrollChevron previousGlyph,nextGlyph;
    public void Configure(float stride)
    {
        columnWidth=Mathf.Max(1,stride);horizontal=true;vertical=false;
        columnStarts=null;
        inertia=false;movementType=MovementType.Clamped;
        velocity=Vector2.zero;
        if(content!=null) {
            float offset=Mathf.Clamp(-content.anchoredPosition.x,0,Travel);
            content.anchoredPosition=new Vector2(-offset,0);
            if(settling)target=Mathf.Clamp(target,0,Travel);
        }
    }
    public void ConfigureColumns(float[] starts) => columnStarts=starts;
    private float NextColumn(float offset,int direction)
    {
        if(columnStarts==null)return offset+direction*columnWidth;
        if(direction>0){foreach(float x in columnStarts)if(x>offset+.5f)return x;return Travel;}
        for(int i=columnStarts.Length-1;i>=0;i--)if(columnStarts[i]<offset-.5f)return columnStarts[i];
        return 0;
    }
    private float Travel=>content==null||viewport==null?0:Mathf.Max(0,content.rect.width-viewport.rect.width);
    public override void OnBeginDrag(PointerEventData data){settling=false;base.OnBeginDrag(data);}
    public override void OnEndDrag(PointerEventData data)
    {
        base.OnEndDrag(data);velocity=Vector2.zero;
        float offset=-content.anchoredPosition.x;
        float previous=NextColumn(offset,-1),next=NextColumn(offset,1);
        if(columnStarts!=null)foreach(float x in columnStarts)if(Mathf.Abs(x-offset)<.5f){SetTarget(x);return;}
        SetTarget(columnStarts==null?Mathf.Round(offset/columnWidth)*columnWidth:offset-previous<next-offset?previous:next);
    }
    public override void OnScroll(PointerEventData data)
    {
        if(wheelControlsBoard)return;
        if(!IsActive()||Travel<=0)return;
        float delta=Mathf.Abs(data.scrollDelta.x)>Mathf.Abs(data.scrollDelta.y)?data.scrollDelta.x:-data.scrollDelta.y;
        if(Mathf.Abs(delta)<.001f)return;
        float offset=settling?target:-content.anchoredPosition.x;
        SetTarget(columnStarts==null?offset+Mathf.Clamp(delta,-1,1)*columnWidth:NextColumn(offset,delta>0?1:-1));
    }
    private void SetTarget(float value){target=Mathf.Clamp(value,0,Travel);settling=true;}
    private Button NavigationButton(string name,float anchor,int direction,out BattlefieldScrollChevron glyph)
    {
        var root=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));root.transform.SetParent(navigation,false);
        var rect=(RectTransform)root.transform;rect.anchorMin=rect.anchorMax=new Vector2(anchor,.5f);rect.sizeDelta=new Vector2(32,40);rect.anchoredPosition=Vector2.zero;
        var image=root.GetComponent<Image>();
        var button=root.GetComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.ColorTint;
        button.onClick.AddListener(()=>SetTarget(NextColumn(settling?target:-content.anchoredPosition.x,direction)));
        var label=new GameObject("Chevron",typeof(RectTransform),typeof(BattlefieldScrollChevron));label.transform.SetParent(root.transform,false);
        glyph=label.GetComponent<BattlefieldScrollChevron>();glyph.direction=direction;glyph.SmoothEdges=smoothIcons||DecisionTheme!=null;glyph.raycastTarget=false;
        var labelRect=(RectTransform)label.transform;labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
        var colors=button.colors;
        if(LaneTheme!=null){
            // A full rectangular hit target, independent of the drawn tab.
            image.color=Color.clear;
            button.targetGraphic=glyph;
            colors.normalColor=new Color(.89f,.89f,.89f);
            colors.highlightedColor=colors.selectedColor=Color.white;
            colors.pressedColor=new Color(.73f,.73f,.73f);
            colors.disabledColor=Color.white;
        }else{
            image.sprite=IntegratedUiUtility.RoundedRect(32,40,10);image.color=new Color(.07f,.15f,.19f,.96f);
            var outline=root.AddComponent<Outline>();outline.effectDistance=new Vector2(1,-1);outline.effectColor=new Color(.4f,.69f,.8f,.8f);
            colors.highlightedColor=new Color(1.4f,1.4f,1.4f);colors.disabledColor=new Color(.5f,.5f,.5f,.65f);
        }
        colors.fadeDuration=.10f;button.colors=colors;
        if(DecisionTheme!=null)MenuVisualTheme.StyleArchiveControl(button);
        return button;
    }
    private void UpdateNavigation()
    {
        if(Travel<=.1f){if(navigation!=null)navigation.gameObject.SetActive(false);return;}
        if(navigation==null) {
            var go=new GameObject(name+"_Navigation",typeof(RectTransform));go.transform.SetParent(transform.parent,false);navigation=(RectTransform)go.transform;
            previous=NavigationButton("Previous",0,-1,out previousGlyph);next=NavigationButton("Next",1,1,out nextGlyph);
        }
        navigation.gameObject.SetActive(true);navigation.SetSiblingIndex(transform.GetSiblingIndex()+1);
        var rect=(RectTransform)transform;navigation.anchorMin=rect.anchorMin;navigation.anchorMax=rect.anchorMax;navigation.pivot=rect.pivot;
        navigation.anchoredPosition=rect.anchoredPosition;navigation.sizeDelta=rect.rect.size+new Vector2(32,0);
        if(alignNavigationToVisibleCards)AlignNavigationToVisibleCards();
        float offset=settling?target:-content.anchoredPosition.x;previous.interactable=offset>.1f;next.interactable=offset<Travel-.1f;
        if(LaneTheme!=null){
            previousGlyph.SetLaneState(LaneTheme,previous.interactable);
            nextGlyph.SetLaneState(LaneTheme,next.interactable);
            return;
        }
        Color arrow=DecisionTheme!=null?(Color)DecisionTheme.ArchivePaper:new Color(.8f,.93f,.98f);
        arrow.a=previous.interactable?1:.35f;previousGlyph.color=arrow;
        arrow.a=next.interactable?1:.35f;nextGlyph.color=arrow;
    }
    private void AlignNavigationToVisibleCards()
    {
        float left=float.PositiveInfinity,right=float.NegativeInfinity;
        var corners=new Vector3[4];
        foreach(RectTransform card in content)
        {
            if(!card.gameObject.activeInHierarchy||!card.name.StartsWith("Card_",System.StringComparison.Ordinal))continue;
            var visibility=card.GetComponent<CanvasGroup>();
            if(visibility!=null&&visibility.alpha<.5f)continue;
            card.GetWorldCorners(corners);
            float cardLeft=float.PositiveInfinity,cardRight=float.NegativeInfinity;
            foreach(var corner in corners)
            {
                float x=navigation.InverseTransformPoint(corner).x;
                cardLeft=Mathf.Min(cardLeft,x);cardRight=Mathf.Max(cardRight,x);
            }
            // A partially clipped card is hidden by DenseFieldViewport. Do not
            // let it pull the arrow away from the last fully visible card.
            float viewportLeft=navigation.rect.xMin+16,viewportRight=navigation.rect.xMax-16;
            if(cardLeft<viewportLeft-.5f||cardRight>viewportRight+.5f)continue;
            left=Mathf.Min(left,cardLeft);right=Mathf.Max(right,cardRight);
        }
        if(float.IsPositiveInfinity(left))return;
        var previousRect=(RectTransform)previous.transform;
        var nextRect=(RectTransform)next.transform;
        float half=previousRect.rect.width*.5f;
        float previousCenter=Mathf.Max(navigation.rect.xMin-navigationLeftOutsideAllowance,left-navigationLeftCardGap-half);
        float nextCenter=Mathf.Min(navigation.rect.xMax,right+navigationRightCardGap+half);
        previousRect.anchoredPosition=new Vector2(previousCenter-navigation.rect.xMin,0);
        nextRect.anchoredPosition=new Vector2(nextCenter-navigation.rect.xMax,0);
    }
    protected override void OnDisable(){base.OnDisable();settling=false;if(navigation!=null)navigation.gameObject.SetActive(false);}
    protected override void OnDestroy(){
        if(navigation!=null){
#if UNITY_EDITOR
            if(!Application.isPlaying)DestroyImmediate(navigation.gameObject);else
#endif
            Destroy(navigation.gameObject);
        }
        base.OnDestroy();
    }
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
