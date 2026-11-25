using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ZonePileHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private Text caption;
    private Image rule;
    private Color normal, muted, accent;
    private bool interactive, playable, hovered, focused;

    public void Configure(Text label, Image underline, bool canOpen, bool hasPlay, Color text, Color disabled, Color highlight)
    {
        caption=label;rule=underline;
        interactive=canOpen;playable=hasPlay;
        normal=text;muted=disabled;accent=highlight;
        if(!interactive)hovered=focused=false;
        Paint();
    }

    public void OnPointerEnter(PointerEventData data){hovered=interactive;Paint();}
    public void OnPointerExit(PointerEventData data){hovered=false;Paint();}
    public void OnSelect(BaseEventData data){focused=interactive;Paint();}
    public void OnDeselect(BaseEventData data){focused=false;Paint();}
    private void OnDisable(){hovered=focused=false;Paint();}

    private void Paint()
    {
        if(caption==null||rule==null)return;
        bool emphasized=interactive&&(hovered||focused||playable);
        caption.color=!interactive?muted:emphasized?accent:normal;
        rule.enabled=emphasized;
        if(emphasized)rule.color=accent;
    }
}
