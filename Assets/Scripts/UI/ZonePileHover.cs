using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ZonePileHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Text count;
    private RectTransform face;
    private bool hovering;
    public void Configure(Text countLabel, RectTransform cardFace, int value)
    {
        count=countLabel;face=cardFace;count.text=value.ToString();
        count.gameObject.SetActive(hovering);
    }
    public void OnPointerEnter(PointerEventData data)
    {
        hovering=true;if(count!=null)count.gameObject.SetActive(true);
    }
    public void OnPointerExit(PointerEventData data)
    {
        hovering=false;if(count!=null)count.gameObject.SetActive(false);
    }
    private void Update()
    {
        float amount=1-Mathf.Exp(-16*Time.unscaledDeltaTime);
        if(face!=null)face.localScale=Vector3.Lerp(face.localScale,Vector3.one*(hovering?1.03f:1),amount);
        var image=GetComponent<Image>();
        if(image!=null)image.color=Color.Lerp(image.color,hovering?new Color(.18f,.25f,.3f,.7f):Color.clear,amount);
    }
    private void OnDisable(){hovering=false;if(count!=null)count.gameObject.SetActive(false);}
}
