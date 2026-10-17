using UnityEngine;
using UnityEngine.UI;

public sealed class GameSettingsPanel : MonoBehaviour
{
    static GameSettingsPanel current;
    Text preview;
    System.Action concede;
    Button concedeButton;
    GameObject concedeConfirmation;
    public static bool IsOpen => current!=null && current.gameObject.activeInHierarchy;
    public static void Show(Transform parent, System.Action onConcede=null)
    {
        if(current!=null) {current.Configure(onConcede);current.gameObject.SetActive(true);current.transform.SetAsLastSibling();return;}
        var canvas=parent.GetComponentInParent<Canvas>().rootCanvas;
        var go=new GameObject("GameSettings",typeof(RectTransform),typeof(Image),typeof(GameSettingsPanel));
        go.transform.SetParent(canvas.transform,false);
        var r=(RectTransform)go.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
        go.GetComponent<Image>().color=new Color(0,0,0,.75f);
        current=go.GetComponent<GameSettingsPanel>();current.Build();current.Configure(onConcede);
    }
    void Configure(System.Action onConcede)
    {
        concede=onConcede;concedeButton.gameObject.SetActive(concede!=null);
        if(concedeConfirmation!=null)concedeConfirmation.SetActive(false);
    }
    void Build()
    {
        var box=new GameObject("SettingsPanel",typeof(RectTransform),typeof(Image));box.transform.SetParent(transform,false);
        float compactOffset=GamePreferences.LanguageSelectionEnabled ? 0 : 68;
        var r=(RectTransform)box.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(820,600-compactOffset*2);
        box.GetComponent<Image>().color=MenuVisualTheme.Panel;
        Label(box.transform,"游戏设置",28,new Vector2(0,244-compactOffset),new Vector2(730,64));
        if(GamePreferences.LanguageSelectionEnabled)
        {
            Label(box.transform,"语言",21,new Vector2(0,175),new Vector2(730,40));
            Button(box.transform,"简体中文",-245,112,()=>GamePreferences.SetLanguage("zh-CN"));
            Button(box.transform,"日本語",0,112,()=>GamePreferences.SetLanguage("ja-JP"));
            Button(box.transform,"English",245,112,()=>GamePreferences.SetLanguage("en-US"));
        }
        Label(box.transform,"文字大小",21,new Vector2(0,36+compactOffset),new Vector2(730,40));
        Button(box.transform,"小",-245,-24+compactOffset,()=>GamePreferences.SetScale(.85f));
        Button(box.transform,"标准",0,-24+compactOffset,()=>GamePreferences.SetScale(1));
        Button(box.transform,"大",245,-24+compactOffset,()=>GamePreferences.SetScale(1.3f));
        preview=Label(box.transform,"",20,new Vector2(0,-96+compactOffset),new Vector2(730,48));
        var autoSave = ReplayWidgets.AutoSaveToggle(box.transform);
        ((RectTransform)autoSave.transform).anchoredPosition = new Vector2(0,-160+compactOffset);
        Button(box.transform,"完成",245,-242+compactOffset,()=>gameObject.SetActive(false));
        concedeButton=Button(box.transform,"认输",-245,-242+compactOffset,ConfirmConcede);
    }
    void ConfirmConcede()
    {
        if(concede==null)return;
        if(concedeConfirmation==null)
        {
            concedeConfirmation=new GameObject("ConcedeConfirmation",typeof(RectTransform),typeof(Image));
            concedeConfirmation.transform.SetParent(transform,false);
            var rect=(RectTransform)concedeConfirmation.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            concedeConfirmation.GetComponent<Image>().color=new Color(.035f,.05f,.07f,.98f);
            Label(rect,"确认认输？",26,new Vector2(0,70),new Vector2(580,50)).alignment=TextAnchor.MiddleCenter;
            Button(rect,"继续对局",-145,-30,()=>concedeConfirmation.SetActive(false));
            Button(rect,"确认认输",145,-30,()=> {
                if(!gameObject.activeInHierarchy || !concedeConfirmation.activeSelf)return;
                var action=concede;concede=null;gameObject.SetActive(false);action?.Invoke();
            });
        }
        concedeConfirmation.SetActive(true);concedeConfirmation.transform.SetAsLastSibling();
    }
    void Update()
    {
        if(preview!=null) preview.text=GamePreferences.T("文字预览")+" · "+Mathf.RoundToInt(GamePreferences.TextScale*100)+"%";
        foreach(var button in GetComponentsInChildren<Button>())
        {
            bool selected=button.name=="简体中文" && GamePreferences.Language=="zh-CN" || button.name=="日本語" && GamePreferences.Language=="ja-JP" || button.name=="English" && GamePreferences.Language=="en-US"
                || button.name=="小" && GamePreferences.TextScale<.9f || button.name=="标准" && Mathf.Approximately(GamePreferences.TextScale,1) || button.name=="大" && GamePreferences.TextScale>1.2f;
            button.GetComponent<Image>().color=selected?MenuVisualTheme.Primary:MenuVisualTheme.Raised;
        }
    }
    void LateUpdate()
    {
        // A fresh match snapshot can reorder the hand; the modal must continue to block it.
        if(transform.GetSiblingIndex()!=transform.parent.childCount-1)transform.SetAsLastSibling();
    }
    static Text Label(Transform parent,string value,int size,Vector2 position,Vector2 dimensions)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
        var t=go.GetComponent<Text>();t.text=value;t.font=GamePreferences.Font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.color=MenuVisualTheme.Paper;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;
        t.rectTransform.sizeDelta=dimensions;t.rectTransform.anchoredPosition=position;PreferenceText.SetSize(t,size);return t;
    }
    static Button Button(Transform parent,string value,float x,float y,UnityEngine.Events.UnityAction action)
    {
        var go=new GameObject(value,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
        var r=(RectTransform)go.transform;r.sizeDelta=new Vector2(218,50);r.anchoredPosition=new Vector2(x,y);
        go.GetComponent<Image>().color=MenuVisualTheme.Raised;go.GetComponent<Button>().onClick.AddListener(action);
        var text=Label(go.transform,value,20,Vector2.zero,new Vector2(208,48));text.alignment=TextAnchor.MiddleCenter;
        return go.GetComponent<Button>();
    }
}
