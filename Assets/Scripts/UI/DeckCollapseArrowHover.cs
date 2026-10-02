using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public sealed class DeckCollapseArrowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CanvasGroup group;
    private bool hovered;
    private void Awake(){group=GetComponent<CanvasGroup>();group.alpha=.48f;}
    public void OnPointerEnter(PointerEventData eventData){hovered=true;}
    public void OnPointerExit(PointerEventData eventData){hovered=false;}
    private void Update(){group.alpha=Mathf.MoveTowards(group.alpha,hovered?1:.48f,Time.unscaledDeltaTime*6);}
    private void OnDisable(){hovered=false;if(group!=null)group.alpha=.48f;}
}
