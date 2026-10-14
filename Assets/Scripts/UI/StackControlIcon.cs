using UnityEngine;
using UnityEngine.UI;

// Resolution-independent controls: magnifier, directional chevron and close.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class StackControlIcon : MaskableGraphic
{
    public enum Kind { View,Right,Left,Close }
    [field: SerializeField] public Kind Current {get;private set;}
    public void SetKind(Kind kind){if(Current==kind)return;Current=kind;SetVerticesDirty();}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();float scale=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/22;
        if(Current==Kind.View) {
            for(int i=0;i<32;i++){float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;Line(vh,new Vector2(-2,2)+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*6,new Vector2(-2,2)+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*6,scale);}
            Line(vh,new Vector2(2,-2),new Vector2(8,-8),scale);
        }else if(Current==Kind.Close){Line(vh,new Vector2(-6,6),new Vector2(6,-6),scale);Line(vh,new Vector2(-6,-6),new Vector2(6,6),scale);}
        else {float sign=Current==Kind.Right?1:-1;Line(vh,new Vector2(-3*sign,7),new Vector2(4*sign,0),scale);Line(vh,new Vector2(4*sign,0),new Vector2(-3*sign,-7),scale);}
    }
    private void Line(VertexHelper vh,Vector2 a,Vector2 b,float scale)
    {
        Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*1.2f;int start=vh.currentVertCount;
        vh.AddVert((a+n)*scale,color,Vector2.zero);vh.AddVert((b+n)*scale,color,Vector2.zero);vh.AddVert((b-n)*scale,color,Vector2.zero);vh.AddVert((a-n)*scale,color,Vector2.zero);
        vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
    }
}
