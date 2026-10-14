using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A face has independently bound regions; changing a cost never repaints a scan
// or recreates the card's input target. The controller currently opts in one card.
public sealed class ComponentCardFace : MonoBehaviour
{
    public TextMeshProUGUI Title { get; private set; }
    public TextMeshProUGUI Cost { get; private set; }
    public TextMeshProUGUI TypeLine { get; private set; }
    public TextMeshProUGUI Rules { get; private set; }
    public TextMeshProUGUI Stats { get; private set; }
    public TextMeshProUGUI PrintedCost { get; private set; }
    public RawImage Illustration { get; private set; }
    RectTransform design;
    bool compact;
    Sprite source;
    string signature;
    static readonly Color Ink=new Color(.055f,.075f,.06f);

    RectTransform Region(string name,float x,float y,float w,float h)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(design,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
        r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
    }
    TextMeshProUGUI TextRegion(string name,float x,float y,float w,float h,float size)
    {
        var r=Region(name,x,y,w,h);var t=CardSymbolText.Create(r,name+"Text",size,Ink);
        t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;
        t.rectTransform.offsetMin=t.rectTransform.offsetMax=Vector2.zero;
        t.richText=true;t.enableAutoSizing=false;t.textWrappingMode=TextWrappingModes.NoWrap;
        t.alignment=TextAlignmentOptions.MidlineLeft;t.overflowMode=TextOverflowModes.Overflow;return t;
    }
    void Build(bool small)
    {
        compact=small;
        design=new GameObject("Regions",typeof(RectTransform)).GetComponent<RectTransform>();
        design.SetParent(transform,false);design.sizeDelta=new Vector2(300,small?300:420);
        Illustration=Region("Illustration",small?0:20,small?34:49,small?300:260,small?266:195).gameObject.AddComponent<RawImage>();
        Illustration.raycastTarget=false;
        if(!small) {
            var frame=Region("Frame",0,0,300,420).gameObject.AddComponent<Image>();
            frame.sprite=Resources.Load<Sprite>("UI/CardFrames/Creature_G");frame.raycastTarget=false;
            Title=TextRegion("Title",25,17,175,30,17);
            Cost=TextRegion("Cost",201,14,74,36,22);Cost.alignment=TextAlignmentOptions.MidlineRight;
            TypeLine=TextRegion("TypeLine",25,237,250,30,14);
            Rules=TextRegion("Rules",25,280,250,76,16);
            Rules.alignment=TextAlignmentOptions.TopLeft;Rules.textWrappingMode=TextWrappingModes.Normal;Rules.overflowMode=TextOverflowModes.Truncate;
            PrintedCost=TextRegion("PrintedCost",25,360,190,28,11);
            PrintedCost.gameObject.SetActive(false);
            var plate=Region("StatsPlate",226,360,54,27).gameObject.AddComponent<Image>();
            plate.color=new Color(.86f,.91f,.83f);plate.raycastTarget=false;
            Stats=TextRegion("Stats",226,360,54,27,17);Stats.alignment=TextAlignmentOptions.Center;
        } else {
            // Battlefield tiles keep their compact illustration and existing
            // counters/P/T; they must not squeeze a portrait into a square.
            var header=Region("Header",0,0,300,34).gameObject.AddComponent<Image>();
            header.color=new Color(.83f,.9f,.82f);header.raycastTarget=false;
            Title=TextRegion("Title",8,2,185,30,17);
            Cost=TextRegion("Cost",200,2,94,30,23);Cost.alignment=TextAlignmentOptions.MidlineRight;
        }
        Fit();
    }
    void Fit()
    {
        if(design==null)return;
        var available=((RectTransform)transform).rect.size;
        float scale=Mathf.Min(available.x/300,available.y/(compact?300:420));
        design.localScale=Vector3.one*scale;
        if(source!=null)CardArtCrop.Illustration(Illustration,source);
    }
    void OnRectTransformDimensionsChange()=>Fit();
    public void SetCost(string cost,string printed,bool changed)
    {
        Cost.text=CardSymbolText.Format(cost??"");
        Cost.color=changed?new Color(.06f,.38f,.68f):Ink;
        if(PrintedCost!=null) {
            PrintedCost.text="";
        }
    }
    public void Bind(Sprite art,string title,string cost,string printed,string type,string rules,string stats,bool small,bool changed)
    {
        if(design==null)Build(small);
        string key=title+"/"+cost+"/"+printed+"/"+type+"/"+rules+"/"+stats+"/"+GamePreferences.Language+"/"+GamePreferences.Revision+"/"+changed;
        if(signature!=key) {
            signature=key;
            SetCost(cost,printed,changed);
            Title.text=title;
            if(TypeLine!=null) {
                TypeLine.text=type;Rules.text=CardSymbolText.Format(rules??"");Stats.text=stats;
            }
        }
        source=art;Illustration.texture=art?.texture;Illustration.color=art==null?new Color(.15f,.2f,.16f):Color.white;
        Fit();
    }
}
