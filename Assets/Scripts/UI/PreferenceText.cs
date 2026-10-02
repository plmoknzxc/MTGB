using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Retains the source separately so repeated locale/scale changes never accumulate.
public sealed class PreferenceText : MonoBehaviour
{
    Text text;
    TMP_Text tmp;
    string source,last;
    [SerializeField] float baseSize;
    bool userText;
    bool inputText;
    bool initialized;
    int revision=-1;
    void Awake() { Apply(); }
    bool EnsureInitialized()
    {
        if(initialized && (text!=null || tmp!=null)) return true;
        text=GetComponent<Text>(); tmp=GetComponent<TMP_Text>();
        if(text==null && tmp==null) return false;
        if(baseSize<=0) baseSize=text!=null?text.fontSize:tmp.fontSize;
        var input=GetComponentInParent<InputField>();
        var dropdown=GetComponentInParent<Dropdown>();
        inputText=input!=null && input.textComponent==text;
        userText=inputText || dropdown!=null;
        source=Current;
        initialized=true;
        return true;
    }
    string Current { get => text!=null?text.text:tmp.text; set {if(text!=null) text.text=value;else tmp.text=value;} }
    public void SetBaseSize(float size)
    {
        baseSize=size;
        Apply();
    }
    void LateUpdate()
    {
        if(!EnsureInitialized()) return;
        if(Current!=last) {source=Current;Apply();}
        else if(revision!=GamePreferences.Revision) Apply();
    }
    public void Apply()
    {
        // Hidden labels can receive layout updates before Unity calls Awake.
        // Bind once here too, retaining the original text/size when later activated.
        if(!EnsureInitialized()) return;
        if(Current!=last) source=Current;
        revision=GamePreferences.Revision;
        last=userText?source:GamePreferences.T(source ?? ""); Current=last;
        if(text!=null) {
            text.fontSize=Mathf.RoundToInt(baseSize*GamePreferences.TextScale);
            // Best-fit labels must retain their vertical bounds after localization
            // or font scaling, otherwise long ability text grows over adjacent UI.
            if(text.resizeTextForBestFit)text.verticalOverflow=VerticalWrapMode.Truncate;
            else if(!userText || inputText)text.verticalOverflow=VerticalWrapMode.Overflow;
            if(GamePreferences.Font!=null)text.font=GamePreferences.Font;
        }
        else {tmp.fontSize=baseSize*GamePreferences.TextScale;if(GamePreferences.TmpFont!=null) tmp.font=GamePreferences.TmpFont;}
    }
}
