using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Presentation only. A drop is submitted by the controller against the current Forge candidates.
[RequireComponent(typeof(CanvasRenderer))]
[DefaultExecutionOrder(510)] // BattlefieldWorldTable updates the rendered card poses at 500.
public sealed class ForgeTargetArrow : MaskableGraphic
{
    private Vector2 from, to;
    private bool valid;
    private Vector2 desiredFrom, desiredTo;
    private Color desiredColor;
    private bool initialized, shown=true, direct;
    private Transform focus;
    private float opacity, reveal, ring;
    private RectTransform combatSource, combatTarget;
    private BattlefieldWorldCard sourceFace, targetFace;
    private Color combatTint;
    private bool combatOccluded, combatCrossesRows;

    internal void FollowCombat(RectTransform source,BattlefieldWorldCard sourceCard,RectTransform target,BattlefieldWorldCard targetCard,Color tint)
    {
        combatSource=source; sourceFace=sourceCard;
        combatTarget=target; targetFace=targetCard; combatTint=tint;
    }

    void UpdateCombatEndpoints()
    {
        bool hidden=!combatSource.gameObject.activeInHierarchy || combatTarget==null || !combatTarget.gameObject.activeInHierarchy
            || sourceFace!=null&&!sourceFace.HasVisibleFace || targetFace!=null&&!targetFace.HasVisibleFace;
        if(combatOccluded!=hidden){combatOccluded=hidden;SetVerticesDirty();}
        if(hidden)return;
        Camera camera=canvas.rootCanvas.worldCamera;
        Vector2 Center(RectTransform rect,BattlefieldWorldCard card)=>card!=null?card.ProjectedCenter:
            RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
        Vector2 Edge(RectTransform rect,BattlefieldWorldCard card,Vector2 toward){
            if(card!=null)return card.ProjectedEdgeToward(toward);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,toward,camera,out var local);
            Vector2 direction=local-rect.rect.center;
            Vector2 half=rect.rect.size*.5f;
            float ratio=Mathf.Max(Mathf.Abs(direction.x)/Mathf.Max(1,half.x),Mathf.Abs(direction.y)/Mathf.Max(1,half.y));
            return RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center+direction/Mathf.Max(1,ratio)));
        }
        var sourceCenter=Center(combatSource,sourceFace);
        var targetCenter=Center(combatTarget,targetFace);
        var delta=targetCenter-sourceCenter;
        combatCrossesRows=Mathf.Abs(delta.y)>Mathf.Max(12,Mathf.Abs(delta.x)*.2f);
        // Leave the row through the face nearest the opponent, clear of adjacent cards.
        var vertical=Vector2.up*(Mathf.Sign(delta.y)*Screen.height*.3f);
        var start=Edge(combatSource,sourceFace,combatCrossesRows&&sourceFace!=null?sourceCenter+vertical:targetCenter);
        var end=Edge(combatTarget,targetFace,combatCrossesRows&&targetFace!=null?targetCenter-vertical:sourceCenter);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,start,camera,out var localStart);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,end,camera,out var localEnd);
        SetEndpoints(localStart,localEnd,combatTint);
    }
    public void SetVisible(bool visible)
    {
        shown=visible;
        if(visible && !gameObject.activeSelf)gameObject.SetActive(true);
    }
    protected override void OnEnable()
    {base.OnEnable();initialized=false;focus=null;opacity=0;reveal=0;ring=0;}
    public void SetEndpoints(Vector2 start, Vector2 end, Color tint, bool isValid=false, bool followPointer=false, Transform focusTarget=null)
    {
        if(focusTarget!=null && focusTarget!=focus)ring=0;
        focus=focusTarget;
        if(initialized && start==desiredFrom && end==desiredTo && tint==desiredColor
            && isValid==valid && followPointer==direct && (!direct || reveal==1))return;
        desiredFrom=start;desiredTo=end;desiredColor=tint;valid=isValid;direct=followPointer;
        if(!initialized){from=start;to=end;color=tint;initialized=true;}
        // A drop uses the current pointer. Its visible tip must reach that same
        // point in this frame, including immediately after the arrow opens.
        if(direct){from=start;to=end;color=tint;reveal=1;}
        SetVerticesDirty();
    }
    private void LateUpdate()
    {
        if(shown && combatSource!=null)UpdateCombatEndpoints();
        if(initialized && shown && opacity==1 && reveal==1 && ring==(valid?1:0)
            && from==desiredFrom && to==desiredTo && color==desiredColor)return;
        float dt=Time.unscaledDeltaTime;
        from=desiredFrom;
        to=direct||combatSource!=null?desiredTo:Vector2.Lerp(to,desiredTo,1-Mathf.Exp(-32*dt));
        color=Color.Lerp(color,desiredColor,1-Mathf.Exp(-20*dt));
        opacity=Mathf.MoveTowards(opacity,shown?1:0,dt/ .14f);
        reveal=Mathf.MoveTowards(reveal,shown?1:0,dt/ .18f);
        ring=Mathf.MoveTowards(ring,valid?1:0,dt/ .1f);
        SetVerticesDirty();
        if(!shown && opacity<=0)gameObject.SetActive(false);
    }
    private Vector2 Point(float t)
    {
        if(combatSource!=null){
            var bend=combatCrossesRows?new Vector2(from.x,to.y):(from+to)*.5f+Vector2.up*24f;
            float u=1-t;
            return u*u*from+2*u*t*bend+t*t*to;
        }
        float direction=from.y>0&&to.y>0?-1:1;
        var control=(from+to)*.5f+Vector2.up*(direction*Mathf.Min(Vector2.Distance(from,to)*.29f,200));
        return (1-t)*(1-t)*from+2*(1-t)*t*control+t*t*to;
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();if(combatOccluded || (to-from).sqrMagnitude<25 || reveal<=.001f || opacity<=0)return;
        var ink=new Color(.045f,.055f,.075f,.92f*opacity);
        var enamel=Color.Lerp(color,Color.white,.28f);enamel.a=opacity;
        Ribbon(mesh,3.4f,ink);
        Ribbon(mesh,1.85f,enamel);
        var tip=Point(reveal);
        var direction=(tip-Point(Mathf.Max(0,reveal-.03f))).normalized;
        float head=Mathf.Min(18,Vector2.Distance(from,tip)*.55f);
        Arrowhead(mesh,tip,direction,head,ink);
        Arrowhead(mesh,tip-direction*1.8f,direction,head*.76f,enamel);
        if(ring<=.001f)return;
        float lockRadius=Mathf.Lerp(26,12,Mathf.SmoothStep(0,1,ring));
        var mark=Color.Lerp(enamel,Color.white,.45f);mark.a=opacity*ring;
        for(int i=0;i<4;i++){
            var axis=Quaternion.Euler(0,0,45+i*90)*Vector2.right;
            Vector2 radial=axis,across=Vector2.Perpendicular(radial);
            int n=mesh.currentVertCount;
            mesh.AddVert(tip+radial*lockRadius,mark,Vector2.zero);
            mesh.AddVert(tip+radial*(lockRadius+6)+across*1.6f,mark,Vector2.zero);
            mesh.AddVert(tip+radial*(lockRadius+6)-across*1.6f,mark,Vector2.zero);
            mesh.AddTriangle(n,n+1,n+2);
        }
    }
    private static void Arrowhead(VertexHelper mesh,Vector2 tip,Vector2 direction,float length,Color tint)
    {
        Vector2 wing=Vector2.Perpendicular(direction)*length*.43f;
        var heel=tip-direction*length;
        int i=mesh.currentVertCount;
        mesh.AddVert(tip,tint,Vector2.zero);
        mesh.AddVert(heel+wing,tint,Vector2.zero);
        mesh.AddVert(tip-direction*length*.64f,tint,Vector2.zero);
        mesh.AddVert(heel-wing,tint,Vector2.zero);
        mesh.AddTriangle(i,i+1,i+2);mesh.AddTriangle(i,i+2,i+3);
    }
    private void Ribbon(VertexHelper mesh,float halfWidth,Color tint)
    {
        int first=mesh.currentVertCount;
        const int steps=48;
        for(int i=0;i<=steps;i++)
        {
            float t=i/(float)steps*reveal;var point=Point(t);
            var tangent=(Point(Mathf.Min(1,t+.01f))-Point(Mathf.Max(0,t-.01f))).normalized;
            var offset=new Vector2(-tangent.y,tangent.x)*(halfWidth*Mathf.Lerp(.47f,1,Mathf.Min(1,t*5)));
            mesh.AddVert(point-offset,tint,Vector2.zero);mesh.AddVert(point+offset,tint,Vector2.zero);
            if(i==0)continue;
            int n=first+i*2;mesh.AddTriangle(n-2,n-1,n);mesh.AddTriangle(n,n-1,n+1);
        }
    }
}

public sealed class ForgeTargetDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Action<Vector2> Drop;
    public void OnBeginDrag(PointerEventData data) { }
    public void OnDrag(PointerEventData data) { }
    public void OnEndDrag(PointerEventData data)
    { if(data.button==PointerEventData.InputButton.Left){data.eligibleForClick=false;Drop?.Invoke(data.position);} }
}
