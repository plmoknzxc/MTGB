using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Retains the source separately so repeated locale/scale changes never accumulate.
public sealed class PreferenceText : MonoBehaviour
{
    // Icon fonts and numeric-only serif labels must survive locale refreshes.
    // Ordinary text keeps the locale font by default.
    public bool KeepFont;
    public bool Literal;
    public bool KeepVerticalBounds;
    public bool FitSingleLineHeight;
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
        var tmpInput=GetComponentInParent<TMP_InputField>();
        inputText=input!=null && input.textComponent==text || tmpInput!=null&&tmpInput.textComponent==tmp;
        userText=inputText || dropdown!=null;
        source=Current;
        initialized=true;
        return true;
    }
    string Current { get => text!=null?text.text:tmp.text; set {if(text!=null) text.text=value;else tmp.text=value;} }
    // Call at construction or an intentional layout size change, before the
    // first frame. Raw fontSize writes are overwritten by periodic BindScene.
    public static void SetSize(Text label,float size)
    {
        var binding=label.GetComponent<PreferenceText>();
        if(binding==null)binding=label.gameObject.AddComponent<PreferenceText>();
        binding.SetBaseSize(size);
    }
    public static void SetSize(TMP_Text label,float size)
    {
        var binding=label.GetComponent<PreferenceText>();
        if(binding==null)binding=label.gameObject.AddComponent<PreferenceText>();
        binding.SetBaseSize(size);
    }
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
        last=userText||Literal?source:GamePreferences.T(source ?? ""); Current=last;
        if(text!=null) {
            text.fontSize=Mathf.RoundToInt(baseSize*GamePreferences.TextScale);
            // Best-fit labels must retain their vertical bounds after localization
            // or font scaling, otherwise long ability text grows over adjacent UI.
            if(text.resizeTextForBestFit || KeepVerticalBounds)text.verticalOverflow=VerticalWrapMode.Truncate;
            else if(!userText || inputText)text.verticalOverflow=VerticalWrapMode.Overflow;
            if(!KeepFont && GamePreferences.Font!=null)text.font=GamePreferences.Font;
            if(FitSingleLineHeight){
                // Font line metrics can exceed a fixed dropdown row even when the
                // visible glyphs look small. Fit vertically without shrinking long names.
                var settings=text.GetGenerationSettings(new Vector2(10000,0));settings.resizeTextForBestFit=false;
                float line=text.cachedTextGeneratorForLayout.GetPreferredHeight("Mg",settings)/text.pixelsPerUnit;
                float height=text.rectTransform.rect.height;
                int fit=height>0&&line>0?Mathf.Max(1,Mathf.FloorToInt(text.fontSize*Mathf.Min(1,(height-.5f)/line))):text.fontSize;
                text.resizeTextForBestFit=true;text.resizeTextMinSize=text.resizeTextMaxSize=fit;
            }
        }
        else {tmp.fontSize=baseSize*GamePreferences.TextScale;if(!KeepFont && GamePreferences.TmpFont!=null) tmp.font=GamePreferences.TmpFont;}
    }
}
