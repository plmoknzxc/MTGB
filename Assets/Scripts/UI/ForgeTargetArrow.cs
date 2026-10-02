using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Presentation only. A drop is submitted by the controller against the current Forge candidates.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ForgeTargetArrow : MaskableGraphic
{
    private Vector2 from, to;
    private bool valid;
    private Vector2 desiredFrom, desiredTo;
    private Color desiredColor;
    private bool initialized, shown=true;
    private float opacity, reveal, ring;
    public void SetVisible(bool visible)
    {
        shown=visible;
        if(visible && !gameObject.activeSelf)gameObject.SetActive(true);
    }
    protected override void OnEnable()
    {base.OnEnable();initialized=false;opacity=0;reveal=0;ring=0;}
    public void SetEndpoints(Vector2 start, Vector2 end, Color tint, bool isValid=false)
    {
        desiredFrom=start;desiredTo=end;desiredColor=tint;valid=isValid;
        if(!initialized){from=start;to=end;color=tint;initialized=true;}
        SetVerticesDirty();
    }
    private void LateUpdate()
    {
        float dt=Time.unscaledDeltaTime;
        from=desiredFrom;
        to=Vector2.Lerp(to,desiredTo,1-Mathf.Exp(-32*dt));
        color=Color.Lerp(color,desiredColor,1-Mathf.Exp(-20*dt));
        opacity=Mathf.MoveTowards(opacity,shown?1:0,dt/ .14f);
        reveal=Mathf.MoveTowards(reveal,shown?1:0,dt/ .18f);
        ring=Mathf.MoveTowards(ring,valid?1:0,dt/ .1f);
        SetVerticesDirty();
        if(!shown && opacity<=0)gameObject.SetActive(false);
    }
    private Vector2 Point(float t)
    {
        var control=(from+to)*.5f+Vector2.up*Mathf.Clamp(Vector2.Distance(from,to)*.38f,65,250);
        return (1-t)*(1-t)*from+2*(1-t)*t*control+t*t*to;
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();if((to-from).sqrMagnitude<25)return;
        Ribbon(mesh,13,new Color(color.r,color.g,color.b,.07f*opacity));
        Ribbon(mesh,8,new Color(color.r,color.g,color.b,.16f*opacity));
        Ribbon(mesh,3,new Color(color.r,color.g,color.b,.95f*opacity));
        Ribbon(mesh,1,new Color(.88f,.98f,1,.8f*opacity));
        var tip=Point(reveal);
        var direction=(tip-Point(Mathf.Max(0,reveal-.03f))).normalized;
        var normal=new Vector2(-direction.y,direction.x);
        int n=mesh.currentVertCount;
        var tint=new Color(color.r,color.g,color.b,opacity);
        mesh.AddVert(tip,tint,Vector2.zero);
        mesh.AddVert(tip-direction*20+normal*9,tint,Vector2.zero);
        mesh.AddVert(tip-direction*20-normal*9,tint,Vector2.zero);
        mesh.AddTriangle(n,n+1,n+2);
        if(valid || ring>0)
        {
            tint.a*=ring*reveal;
            for(int i=0;i<40;i++)
            {
                float a=i*Mathf.PI*2/40,b=(i+1)*Mathf.PI*2/40;
                var u=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var v=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                n=mesh.currentVertCount;
                mesh.AddVert(tip+u*22,tint,Vector2.zero);mesh.AddVert(tip+u*25,tint,Vector2.zero);
                mesh.AddVert(tip+v*22,tint,Vector2.zero);mesh.AddVert(tip+v*25,tint,Vector2.zero);
                mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n+2,n+1,n+3);
            }
        }
    }
    private void Ribbon(VertexHelper mesh,float halfWidth,Color tint)
    {
        int first=mesh.currentVertCount;
        const int steps=48;
        for(int i=0;i<=steps;i++)
        {
            float t=i/(float)steps*reveal;var point=Point(t);
            var tangent=(Point(Mathf.Min(1,t+.01f))-Point(Mathf.Max(0,t-.01f))).normalized;
            var offset=new Vector2(-tangent.y,tangent.x)*halfWidth;
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
