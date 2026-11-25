using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Long rules and target lists scroll inside a stack entry. At either end,
// the next wheel movement in that direction belongs to the enclosing stack.
public sealed class ForgeStackDetailScrollRect : ScrollRect
{
    public override void OnScroll(PointerEventData data)
    {
        float wheel=data.scrollDelta.y;
        if(wheel!=0 && Mathf.Abs(wheel)>=Mathf.Abs(data.scrollDelta.x)){
            float travel=content.rect.height-viewport.rect.height;
            float remaining=travel*(wheel>0?1-verticalNormalizedPosition:verticalNormalizedPosition);
            if(remaining<=.5f && transform.parent.GetComponentInParent<ScrollRect>() is {vertical:true} stack && stack.IsActive()){
                stack.OnScroll(data);
                return;
            }
        }
        base.OnScroll(data);
    }
}
