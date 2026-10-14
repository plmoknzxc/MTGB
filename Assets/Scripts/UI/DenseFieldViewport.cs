using UnityEngine;
using UnityEngine.UI;

// Keep scroll-boundary fragments out of dense rows, including the quantity and
// loyalty badges. Objects and their engine identities remain in the scroll content.
[DefaultExecutionOrder(200)]
public sealed class DenseFieldViewport : MonoBehaviour
{
    private ScrollRect scroll;
    private readonly Vector3[] corners=new Vector3[4];
    private static readonly string[] Adornments={"Visual/Count","Visual/DefenseCounter","Visual/CardShadow"};
    // Measure the final orientation, not the pointer's animated current pose.
    // Layout and clipping share the same adornments, including badge overhang.
    public static Rect LayoutFootprint(RectTransform card,float rotation)
    {
        var corners=new Vector3[4];var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=-min;
        var turn=Quaternion.Euler(0,0,rotation);
        var parts=new System.Collections.Generic.List<RectTransform>{card};
        var visual=card.Find("Visual");
        // Rebuilt faces retain inactive old children until end of frame. Find()
        // may return that old namesake; measure every active direct adornment.
        if(visual!=null)foreach(Transform child in visual) {
            if(child.gameObject.activeSelf&&System.Array.IndexOf(Adornments,"Visual/"+child.name)>=0)parts.Add((RectTransform)child);
        }
        foreach(var part in parts){part.GetWorldCorners(corners);foreach(var corner in corners){Vector2 point=turn*card.InverseTransformPoint(corner);min=Vector2.Min(min,point);max=Vector2.Max(max,point);}}
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
    private void Awake(){scroll=GetComponent<ScrollRect>();}
    private void LateUpdate()
    {
        if(scroll==null||scroll.content==null)return;
        foreach(Transform child in scroll.content) {
            if(!child.gameObject.activeSelf||!child.name.StartsWith("Card_"))continue;
            var rect=(RectTransform)child;
            float min=float.PositiveInfinity,max=float.NegativeInfinity;
            Extend(rect,ref min,ref max);
            foreach(string name in Adornments) {
                var badge=child.Find(name) as RectTransform;
                if(badge!=null&&badge.gameObject.activeSelf)Extend(badge,ref min,ref max);
            }
            bool complete=min>=scroll.viewport.rect.yMin-.5f&&max<=scroll.viewport.rect.yMax+.5f;
            if(scroll.horizontal) {
                min=float.PositiveInfinity;max=float.NegativeInfinity;
                Extend(rect,ref min,ref max,true);
                foreach(string name in Adornments) {
                    var badge=child.Find(name) as RectTransform;
                    if(badge!=null&&badge.gameObject.activeSelf)Extend(badge,ref min,ref max,true);
                }
                complete &= min>=scroll.viewport.rect.xMin-.5f&&max<=scroll.viewport.rect.xMax+.5f;
            }
            var group=child.GetComponent<CanvasGroup>();
            if(group!=null){group.alpha=complete?1:0;group.blocksRaycasts=complete;}
        }
    }
    private void Extend(RectTransform rect,ref float min,ref float max,bool horizontal=false)
    {
        rect.GetWorldCorners(corners);
        foreach(var corner in corners){var point=scroll.viewport.InverseTransformPoint(corner);float value=horizontal?point.x:point.y;min=Mathf.Min(min,value);max=Mathf.Max(max,value);}
    }
}
