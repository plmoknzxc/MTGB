using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ZonePileHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Text count;
    private RectTransform face;
    private bool hovering;
    private bool showCount=true;
    private bool available;
    private Color Tint=>hovering?new Color(.18f,.25f,.3f,.7f):available?new Color(.04f,.29f,.37f,.6f):Color.clear;
    public void Configure(Text countLabel, RectTransform cardFace, int value,bool showCountOnHover=true,bool hasPlayableCard=false)
    {
        count=countLabel;face=cardFace;count.text=value.ToString();showCount=showCountOnHover;available=hasPlayableCard;
        count.gameObject.SetActive(hovering&&showCount);
        var image=GetComponent<Image>();if(image!=null)image.color=Tint;
    }
    public void OnPointerEnter(PointerEventData data)
    {
        hovering=true;if(count!=null)count.gameObject.SetActive(showCount);
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
        if(image!=null)image.color=Color.Lerp(image.color,Tint,amount);
    }
    private void OnDisable(){hovering=false;if(count!=null)count.gameObject.SetActive(false);}
}
