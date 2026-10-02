using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Keep the authored Text as the data/localization source; its child renders inside a fixed viewport.
[DefaultExecutionOrder(100)]
public sealed class PlayerNameMarquee : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Text source, display;
    private RectTransform viewport, content;
    private string lastText, idleText;
    private Font lastFont;
    private int lastSize;
    private float lastWidth, overflow, elapsed;
    private bool hovered;
    private const float Speed = 45f, HoverDelay = .35f, EndPause = 1f;

    public void Initialize(Text text)
    {
        source=text; viewport=(RectTransform)transform;
        source.enabled=false;
        if(GetComponent<RectMask2D>()==null)gameObject.AddComponent<RectMask2D>();
        if(display==null)
        {
            var child=new GameObject("ScrollingName",typeof(RectTransform),typeof(Text));
            child.transform.SetParent(transform,false);
            display=child.GetComponent<Text>(); content=display.rectTransform;
            content.anchorMin=new Vector2(0,0); content.anchorMax=new Vector2(0,1);
            content.pivot=new Vector2(0,.5f);
            display.alignment=TextAnchor.MiddleLeft;
            display.horizontalOverflow=HorizontalWrapMode.Overflow;
            display.verticalOverflow=VerticalWrapMode.Overflow;
            display.supportRichText=false; display.resizeTextForBestFit=false;
        }
        display.raycastTarget=true;
        Refresh();
    }

    private void Refresh()
    {
        if(source==null||display==null)return;
        float width=viewport.rect.width;
        display.color=source.color;
        if(lastText==source.text&&lastFont==source.font&&lastSize==source.fontSize&&Mathf.Approximately(lastWidth,width))return;
        lastText=source.text; lastFont=source.font; lastSize=source.fontSize; lastWidth=width;
        display.text=lastText; display.font=lastFont; display.fontSize=lastSize;
        display.fontStyle=source.fontStyle; display.lineSpacing=source.lineSpacing;
        float textWidth=display.preferredWidth;
        overflow=Mathf.Max(0,textWidth-width);
        content.sizeDelta=new Vector2(Mathf.Max(width,textWidth),0);
        idleText=lastText;
        if(overflow>0)
        {
            // Cut at text-element boundaries so emoji and combining characters stay intact.
            var elements=new System.Globalization.StringInfo(lastText);
            int low=0, high=elements.LengthInTextElements;
            while(low<high)
            {
                int middle=(low+high+1)/2;
                display.text=elements.SubstringByTextElements(0,middle)+"…";
                if(display.preferredWidth<=width)low=middle;else high=middle-1;
            }
            idleText=(low>0?elements.SubstringByTextElements(0,low):"")+"…";
        }
        display.text=hovered?lastText:idleText;
        elapsed=0; content.anchoredPosition=Vector2.zero;
    }

    private void LateUpdate()
    {
        Refresh();
        if(!hovered||overflow<=0)return;
        elapsed+=Time.unscaledDeltaTime;
        float duration=overflow/Speed;
        float time=elapsed-HoverDelay;
        float offset=time<=0?0:time<duration?time*Speed:
            time<duration+EndPause?overflow:
            Mathf.Max(0,overflow-(time-duration-EndPause)*Speed);
        content.anchoredPosition=new Vector2(-offset,0);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered=true; elapsed=0;
        if(display!=null)display.text=lastText;
    }
    public void OnPointerExit(PointerEventData eventData) { ResetScroll(); }
    private void OnDisable() { ResetScroll(); }
    private void ResetScroll()
    {
        hovered=false; elapsed=0;
        if(content!=null)content.anchoredPosition=Vector2.zero;
        if(display!=null)display.text=idleText;
    }
}
