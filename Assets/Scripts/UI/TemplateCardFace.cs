using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

// PSD coordinates are paper coordinates. UI preference scaling must not change
// the typography inside a printed face; the complete face scales uniformly.
[DefaultExecutionOrder(2000)]
public sealed class TemplateCardFace : MonoBehaviour
{
    public TextMeshProUGUI Title,Cost,Rules,AdventureTitle,AdventureCost,AdventureRules;
    public UnityEngine.UI.RawImage Illustration;
    readonly Dictionary<string,RectTransform> layouts=new();
    readonly Dictionary<TextMeshProUGUI,float> sizes=new();
    static readonly Dictionary<string,TMP_FontAsset> fonts=new();
    RectTransform design;
    bool compact;
    bool sagaLayout;
    public bool Fits { get; private set; }
    Sprite source;
    static Color Ink=>Color.black;
    static TMP_FontAsset Font(bool heading,bool symbols=false,bool stats=false)
    {
        if(!heading&&!symbols&&GamePreferences.Language.StartsWith("ja"))return GamePreferences.TmpFont;
        string locale=symbols||GamePreferences.Language.StartsWith("en")?"EN":GamePreferences.Language.StartsWith("zh")?"CN":"";
        if(locale=="")return GamePreferences.TmpFont;
        string key=stats?"StatsEN":(heading?"Name":"Rules")+locale;
        if(!fonts.TryGetValue(key,out var font)) {
            var native=Resources.Load<Font>("UI/CardTemplates/Fonts/"+key);
            int sampling=locale=="EN"?72:48;
            font=TMP_FontAsset.CreateFontAsset(native,sampling,sampling/8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic);
            // Match the printed stroke at the small sizes used by hand/stack cards.
            // No outline, shadow, or synthetic bold is added to the printed face.
            font.material.SetFloat(ShaderUtilities.ID_FaceDilate,key=="RulesCN"?.10f:stats?0:.04f);
            font.isMultiAtlasTexturesEnabled=true;font.fallbackFontAssetTable=new List<TMP_FontAsset>();
            if(GamePreferences.TmpFont!=null)font.fallbackFontAssetTable.Add(GamePreferences.TmpFont);fonts[key]=font;
        }
        return font;
    }
    RectTransform Region(string name,float x,float y,float w,float h)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(design,false);
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
    }
    TextMeshProUGUI Text(string name,float x,float y,float w,float h,float size,bool heading=false)
    {
        var r=Region(name,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font=Font(heading);t.spriteAsset=CardSymbolText.Atlas;t.fontSize=size;t.color=Ink;
        t.richText=true;t.raycastTarget=false;t.enableAutoSizing=false;t.textWrappingMode=TextWrappingModes.NoWrap;
        t.overflowMode=TextOverflowModes.Overflow;t.alignment=TextAlignmentOptions.MidlineLeft;sizes[t]=size;
        // Global preference scans retain the template font. LateUpdate restores
        // paper-sized typography after the shared UI preference binding.
        var binding=r.gameObject.AddComponent<PreferenceText>();binding.KeepFont=true;
        return t;
    }
    void Picture(string name,string resource,float x,float y,float w,float h)
    {
        var image=Region(name,x,y,w,h).gameObject.AddComponent<UnityEngine.UI.RawImage>();
        image.texture=Resources.Load<Texture2D>("UI/CardTemplates/"+resource);image.raycastTarget=false;
    }
    public void Bind(Sprite art,string title,string cost,string type,string rules,string stats,string printed,
        string adventureTitle,string adventureCost,string adventureType,string adventureRules,string frame,bool small)
    {
        bool adventure=frame.StartsWith("Adventure"),walker=frame.StartsWith("Planeswalker"),saga=frame.StartsWith("Saga"),fourRows=frame.StartsWith("Planeswalker4");
        string key=frame+"/"+small+"/"+!string.IsNullOrEmpty(stats);
        if(!layouts.TryGetValue(key,out design)) {
            design=new GameObject(key,typeof(RectTransform)).GetComponent<RectTransform>();design.SetParent(transform,false);
            design.sizeDelta=small?new Vector2(300,300):new Vector2(595,831);layouts[key]=design;
            if(small) {
                var header=Region("Header",0,0,300,34).gameObject.AddComponent<UnityEngine.UI.Image>();header.color=frame=="BasicG"?new Color(.82f,.87f,.78f):new Color(.9f,.78f,.7f);header.raycastTarget=false;
                Region("Illustration",0,34,300,266).gameObject.AddComponent<UnityEngine.UI.RawImage>().raycastTarget=false;
                Text("Title",8,0,185,34,17,true);Text("Cost",195,0,98,34,23).alignment=TextAlignmentOptions.MidlineRight;
            } else {
                Picture("Frame",frame,0,0,595,831);
                Region("Illustration",saga?298:47,94,saga?253:504,saga?604:fourRows?314:370).gameObject.AddComponent<UnityEngine.UI.RawImage>().raycastTarget=false;
                Text("Title",54,walker?27:37,340,51,36,true);Text("Cost",391,walker?30:40,162,43,32).alignment=TextAlignmentOptions.MidlineRight;
                Text("Type",54,saga?704:fourRows?412:468,492,45,32,true);
                var body=Text("Rules",adventure?310:walker?74:53,saga?110:adventure?548:fourRows?466:walker?522:540,saga?233:adventure?232:walker?466:489,saga?573:adventure?189:fourRows?260:walker?204:190,adventure?25:32);
                body.alignment=TextAlignmentOptions.TopLeft;body.textWrappingMode=TextWrappingModes.Normal;body.overflowMode=TextOverflowModes.Truncate;
                if(adventure) {
                    Text("AdventureTitle",58,521,146,38,28,true);
                    Text("AdventureCost",201,521,77,34,26).alignment=TextAlignmentOptions.MidlineRight;
                    Text("AdventureType",58,560,220,32,26,true);
                    var ab=Text("AdventureRules",58,609,220,154,27);ab.alignment=TextAlignmentOptions.TopLeft;ab.textWrappingMode=TextWrappingModes.Normal;ab.overflowMode=TextOverflowModes.Truncate;
                }
                if(!string.IsNullOrEmpty(stats)) {
                    string plate=walker?"Loyalty":(adventure?"Basic"+frame.Substring(9):frame)+"-pt";
                    if(Resources.Load<Texture2D>("UI/CardTemplates/"+plate)==null)plate="BasicC-pt";
                    Picture("StatsPlate",plate,461,737,102,51);
                    // Use the same bold serif family as the existing battlefield badge.
                    Text("Stats",461,737,102,51,42,true).alignment=TextAlignmentOptions.Midline;
                }
            }
        }
        foreach(var pair in layouts)pair.Value.gameObject.SetActive(pair.Key==key);
        compact=small;sagaLayout=saga;source=art;
        Title=design.Find("Title").GetComponent<TextMeshProUGUI>();Cost=design.Find("Cost").GetComponent<TextMeshProUGUI>();
        Illustration=design.Find("Illustration").GetComponent<UnityEngine.UI.RawImage>();
        Title.text=title;Title.font=Font(true);Cost.text=CardSymbolText.Format(cost);Cost.color=cost!=printed?new Color(.08f,.36f,.63f):Ink;
        int symbolCount=System.Text.RegularExpressions.Regex.Matches(cost??"",@"\{[^{}]+\}").Count;
        float symbolSize=small?28:Mathf.Min(38,Mathf.Max(22,300f/Mathf.Max(1,symbolCount)-3));
        if(!small){
            float symbolsWidth=Mathf.Max(0,symbolCount*(symbolSize+3)-3);
            var costRect=Cost.rectTransform;costRect.sizeDelta=new Vector2(Mathf.Max(162,symbolsWidth),43);
            costRect.anchoredPosition=new Vector2(553-costRect.sizeDelta.x,walker?-30:-40);
            Title.rectTransform.sizeDelta=new Vector2(Mathf.Max(1,Mathf.Min(340,553-symbolsWidth-62)),51);
        }
        BindCost(Cost,cost,symbolSize);
        Rules=design.Find("Rules")?.GetComponent<TextMeshProUGUI>();AdventureTitle=design.Find("AdventureTitle")?.GetComponent<TextMeshProUGUI>();
        AdventureCost=design.Find("AdventureCost")?.GetComponent<TextMeshProUGUI>();AdventureRules=design.Find("AdventureRules")?.GetComponent<TextMeshProUGUI>();
        if(!small) {
            design.Find("Type").GetComponent<TextMeshProUGUI>().text=type;
            Rules.text=CardSymbolText.Format((rules??"").Replace("\\n","\n"));Rules.font=Font(false);
            var stat=design.Find("Stats")?.GetComponent<TextMeshProUGUI>();if(stat!=null)stat.text=stats;
            if(AdventureTitle!=null) {
                AdventureTitle.text=adventureTitle;AdventureCost.text=CardSymbolText.Format(adventureCost);
                BindCost(AdventureCost,adventureCost,30);
                design.Find("AdventureType").GetComponent<TextMeshProUGUI>().text=adventureType;
                AdventureRules.text=CardSymbolText.Format((adventureRules??"").Replace("\\n","\n"));AdventureRules.font=Font(false);
            }
        }
        Illustration.texture=art?.texture;Fit();
        bool japanese=GamePreferences.Language.StartsWith("ja");
        foreach(var text in design.GetComponentsInChildren<TextMeshProUGUI>()) {
            text.font=Font(text.name is "Title" or "Type" or "Stats" or "AdventureTitle" or "AdventureType",text.name is "Cost" or "AdventureCost" or "Stats");
            // Printed Japanese uses denser typesetting in the Adventure book.
            // These fixed paper sizes never depend on window or preference scale.
            bool cjk=!GamePreferences.Language.StartsWith("en");
            if(text.name=="Title"&&!small)sizes[text]=japanese?34:36;
            if(text.name=="Type")sizes[text]=japanese?30:32;
            if(text.name=="AdventureTitle")sizes[text]=japanese?26:28;
            if(text.name=="Rules")sizes[text]=japanese?(adventure?22:28):adventure?(cjk?32:24):cjk?36:32;
            if(text.name=="AdventureRules")sizes[text]=japanese?16:cjk?27:24;
            // DFHei has tighter native line metrics than the UI's Noto font.
            // Use printed leading instead of carrying over Noto's negative spacing.
            if(text.name is "Rules" or "AdventureRules")text.lineSpacing=GamePreferences.Language.StartsWith("zh")?15:japanese?-9:text.name=="Rules"&&adventure?-8:0;
            if(text.name=="Stats"){text.font=Font(true,true,true);text.fontStyle=FontStyles.Normal;}
            if(text.name is "Title" or "Type")text.fontStyle=FontStyles.Bold;
            if(text.name is "AdventureTitle" or "AdventureType"){text.color=Color.white;text.fontStyle=FontStyles.Normal;}
            else if(text.name is not "Cost" and not "AdventureCost")text.color=Ink;
            if(text.name=="Stats"&&walker)text.color=Color.white;
            if(text.name=="AdventureType")sizes[text]=japanese?18:26;
        }
        // Fit only in paper coordinates when content changes, never in response
        // to a window's scale. Unreadable/unsupported faces fall back to scans.
        Fits=true;
        if(!small&&(walker||saga))Fits=BindStructuredRules(rules,walker,fourRows,japanese);
        foreach(var text in design.GetComponentsInChildren<TextMeshProUGUI>()) {
            if(!text.gameObject.activeInHierarchy)continue;
            if(text.name is "Cost" or "AdventureCost" or "Stats")continue;
            float minimum=text.name is "Rules" or "AdventureRules"?japanese?16:22:22;
            float size=sizes[text];text.fontSize=size;
            var area=text.rectTransform.rect;
            for(int step=0;step<24;step++){
                var preferred=text.GetPreferredValues(text.text,area.width,10000);
                bool body=text.textWrappingMode==TextWrappingModes.Normal;
                bool fits=body?preferred.y<=area.height:preferred.x<=area.width;
                if(fits)break;
                if(size<=minimum){Fits=false;break;}
                size=Mathf.Max(minimum,size-1);text.fontSize=size;
            }
            sizes[text]=size;
        }
    }
    // Loyalty costs and Saga chapters are separate semantic rows, not a single
    // paragraph with a numerical prefix. Their badges come from the same PSD.
    bool BindStructuredRules(string rules,bool walker,bool fourRows,bool japanese)
    {
        var old=design.Find("StructuredRules");
        if(old!=null){foreach(var t in old.GetComponentsInChildren<TextMeshProUGUI>(true))sizes.Remove(t);old.gameObject.SetActive(false);Destroy(old.gameObject);}
        string text=(rules??"").Replace("\\n","\n");
        string pattern=walker?@"(?m)^[ \t]*\[?([+＋\-−－]?[0-9０-９XYZＸＹＺ]+)\]?[ \t]*[:：][ \t]*":@"(?m)^[ \t]*([IVX]+(?:[ ,、，]+[IVX]+)*)[ \t]*[—–―－-][ \t]*";
        var matches=Regex.Matches(text,pattern);
        Rules.gameObject.SetActive(false);
        if(matches.Count==0)return false;
        var root=Region("StructuredRules",0,0,595,831);
        var labels=new List<TextMeshProUGUI>();var tokens=new List<string>();
        string prefix=text.Substring(0,matches[0].Index).Trim();
        if(prefix.Length>0){labels.Add(Text("RulePrefix",walker?83:51,0,walker?461:235,100,24));tokens.Add("");}
        for(int i=0;i<matches.Count;i++){
            int start=matches[i].Index+matches[i].Length,end=i+1<matches.Count?matches[i+1].Index:text.Length;
            labels.Add(Text("RuleRow"+i,walker?110:79,0,walker?430:207,100,28));tokens.Add(matches[i].Groups[1].Value);
            labels[labels.Count-1].text=CardSymbolText.Format(text.Substring(start,end-start).Trim());
        }
        if(prefix.Length>0)labels[0].text=CardSymbolText.Format(prefix);
        foreach(var label in labels){label.transform.SetParent(root,false);label.font=Font(false);label.fontStyle=FontStyles.Normal;label.alignment=TextAlignmentOptions.TopLeft;label.textWrappingMode=TextWrappingModes.Normal;label.overflowMode=TextOverflowModes.Overflow;label.lineSpacing=walker&&fourRows&&GamePreferences.Language.StartsWith("en")?-20:0;}
        float top=walker?fourRows?466:522:112,bottom=walker?fourRows?737:728:681,gap=walker?fourRows?4:7:18;
        float size=japanese?25:GamePreferences.Language.StartsWith("zh")?29:28,minimum=japanese?16:22;
        var heights=new float[labels.Count];
        for(int step=0;step<20;step++){
            for(int i=0;i<labels.Count;i++){
                labels[i].fontSize=size;
                // Populate dynamic glyphs/kerning before measuring wrapping;
                // their first rendered width can differ from an unbuilt mesh.
                labels[i].ForceMeshUpdate(true,true);
                heights[i]=Mathf.Max(tokens[i]==""?0:walker?44:58,labels[i].GetPreferredValues(labels[i].text,labels[i].rectTransform.rect.width,10000).y+2);
            }
            if(heights.Sum()+gap*(labels.Count-1)<=bottom-top)break;
            if(size<=minimum)return false;size=Mathf.Max(minimum,size-1);
        }
        float extra=Mathf.Max(0,bottom-top-heights.Sum()-gap*(labels.Count-1))/(labels.Count+1),y=top+extra;
        for(int i=0;i<labels.Count;i++){
            var label=labels[i];label.rectTransform.anchoredPosition=new Vector2(walker?tokens[i]==""?83:110:tokens[i]==""?51:79,-y);label.rectTransform.sizeDelta=new Vector2(label.rectTransform.sizeDelta.x,heights[i]);sizes[label]=size;
            if(tokens[i]!=""){
                if(walker){
                    string token=tokens[i].Normalize(System.Text.NormalizationForm.FormKC).Replace('−','-').Replace('－','-');
                    string shape=token.StartsWith("+")?"Plus":token.StartsWith("-")?"Minus":"Zero";
                    Picture("LoyaltyCost"+i,"Loyalty"+shape,fourRows?30:24,y+(heights[i]-(fourRows?48:60))/2,fourRows?60:75,fourRows?48:60);var badge=design.GetChild(design.childCount-1);badge.SetParent(root,false);
                    var value=Text("LoyaltyValue"+i,24,y+(heights[i]-42)/2,75,42,30,true);value.transform.SetParent(root,false);value.font=Font(true,true,true);value.color=Color.white;value.alignment=TextAlignmentOptions.Midline;value.text=token;value.fontStyle=FontStyles.Normal;sizes[value]=30;
                }else{
                    var chapters=Regex.Matches(tokens[i],@"[IVX]+");
                    for(int c=0;c<chapters.Count;c++){
                        string resource="Chapter"+chapters[c].Value;if(Resources.Load<Texture2D>("UI/CardTemplates/"+resource)==null)return false;
                        float height=Mathf.Min(54,(heights[i]-4)/chapters.Count);
                        Picture("ChapterCost"+i+"_"+c,resource,23,y+(heights[i]-height*chapters.Count)/2+c*height,47,height);design.GetChild(design.childCount-1).SetParent(root,false);
                    }
                }
            }
            y+=heights[i]+gap+extra;
        }
        return true;
    }
    static void BindCost(TextMeshProUGUI label,string cost,float diameter)
    {
        var strip=label.transform.GetComponent<TemplateManaCost>()??label.gameObject.AddComponent<TemplateManaCost>();
        strip.Bind(cost,diameter);
        // Retain the semantic text for the display model and diagnostics.
        // Images avoid the font's sprite baseline and character-size scaling.
        label.enabled=!strip.Complete;
    }
    void Fit()
    {
        if(design==null)return;var available=((RectTransform)transform).rect.size;
        design.localScale=Vector3.one*Mathf.Min(available.x/(compact?300:595),available.y/(compact?300:831));
        if(source!=null)CardArtCrop.Fill(Illustration,source,sagaLayout?new Rect(.5f,.16f,.43f,.73f):new Rect(.078f,.442f,.844f,.445f),1);
    }
    void OnRectTransformDimensionsChange()=>Fit();
    void LateUpdate(){foreach(var pair in sizes)if(pair.Key!=null)pair.Key.fontSize=pair.Value;}
}
