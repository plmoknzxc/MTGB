using UnityEngine;
using UnityEngine.UI;

// Retarget continuously from the currently rendered color. Snapshot refreshes
// do not restart a transition or resize the bar back to its initial frame.
[DisallowMultipleComponent]
public sealed class ForgePhaseBarFade:MonoBehaviour
{
    private Image image;
    private Color start,target;
    private float elapsed;
    private bool initialized,moving;
    public void SetTarget(Color value)
    {
        image??=GetComponent<Image>();
        if(!initialized){initialized=true;target=value;image.color=value;return;}
        if(target==value)return;
        start=image.color;target=value;elapsed=0;moving=true;
    }
    private void LateUpdate()
    {
        if(!moving)return;
        elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(elapsed/.24f);
        image.color=Color.Lerp(start,target,t*t*(3-2*t));moving=t<1;
    }
}
