using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private static bool HasSpellChoices(ForgeStackItem entry)=>entry?.kind=="spell"&&entry.source?.faceDown!=true&&
        ((entry.chosenModes?.Length??0)>0||(entry.castingChoices?.Length??0)>0);

    private static string StackManaCost(ForgeCardView card)=>string.IsNullOrEmpty(card?.castingManaCost)?card?.manaCost??"":card.castingManaCost;

    private void PresentPrintedXCost(RectTransform parent,ForgeStackItem entry,Sprite art)
    {
        var overlay=parent.Find("ResolvedManaCost") as RectTransform;
        bool visible=entry?.kind=="spell"&&entry.hasXValue&&entry.source?.faceDown!=true&&
            entry.source?.stateName!="Secondary"&&entry.source?.stateName!="PreparedSpell"&&!HasSpellChoices(entry)&&art!=null;
        if(!visible){if(overlay!=null)overlay.gameObject.SetActive(false);return;}
        var size=parent.rect.size;float ratio=art.rect.width/art.rect.height;
        float height=Mathf.Min(size.y,size.x/ratio),width=height*ratio;
        string key=GamePreferences.Language+"/"+GamePreferences.TextScale+"/"+parent.rect.size+"/"+art.GetInstanceID()+"/"+ServerSnapshotJson.Serialize(entry);
        if(overlay!=null&&overlay.GetComponent<ChosenSpellVisualKey>().value==key){overlay.gameObject.SetActive(true);return;}
        if(overlay==null){overlay=Rect(parent,"ResolvedManaCost",Vector2.zero,Vector2.zero);overlay.gameObject.AddComponent<ChosenSpellVisualKey>();}
        else Clear(overlay);
        overlay.GetComponent<ChosenSpellVisualKey>().value=key;overlay.gameObject.SetActive(true);overlay.SetAsLastSibling();
        overlay.sizeDelta=new Vector2(width,height);overlay.anchoredPosition=Vector2.zero;
        // Keep the scan's printed mana symbols (including X/XX). Only reflow
        // the rules interior, starting below the complete printed type bar.
        const float rulesTop=.369f,rulesBottom=.111f;
        var paper=Panel(overlay,"Rules",new Vector2(0,height*((rulesTop+rulesBottom)*.5f-.5f)),new Vector2(width*.85f,height*(rulesTop-rulesBottom)),new Color(.87f,.89f,.85f));
        paper.GetComponent<Image>().raycastTarget=false;
        var content=Scroll(paper,"Text",Vector2.zero,paper.sizeDelta-new Vector2(10,8));
        content.parent.GetComponent<Image>().color=Color.clear;
        string rules=catalog.TryGetValue(entry.source.printingKey??"",out var definition)?GamePreferences.CardRules(definition):entry.source.currentRules??"";
        var body=Label(content,"Body",rules.Replace("\\n","\n"),Vector2.zero,new Vector2(content.rect.width-6,content.rect.height),14,new Color(.06f,.08f,.10f));
        body.alignment=TextAnchor.UpperLeft;body.raycastTarget=false;body.resizeTextForBestFit=false;
        var symbols=SymbolizeStackLabel(body);symbols.raycastTarget=true;
        PresentSecondaryXValue(parent,entry);
        var scroll=content.parent.GetComponent<ScrollRect>();
        if(content.rect.height>scroll.viewport.rect.height+1)AddStackScrollIndicator(content);
    }

    private void PresentSecondaryXValue(RectTransform parent,ForgeStackItem entry)
    {
        var face=parent.Find("PreparedSpellFace");
        if(face==null||!face.gameObject.activeSelf)face=parent.Find("ChosenSpellFace");
        if(face==null||!face.gameObject.activeSelf)face=parent.Find("ResolvedManaCost");
        if(face==null||!face.gameObject.activeSelf)return;
        var body=face.GetComponentsInChildren<Text>().FirstOrDefault(t=>t.name=="Body"||t.name=="Effects");
        if(body==null)return;
        var symbols=body.transform.Find("Symbols")?.GetComponent<TMPro.TextMeshProUGUI>();
        if(symbols==null)return;
        var value=body.transform.Find("XValue")?.GetComponent<TMPro.TextMeshProUGUI>();
        if(value==null)value=CardSymbolText.Create(body.transform,"XValue",symbols.fontSize,new Color(.16f,.36f,.76f));
        value.gameObject.SetActive(entry.hasXValue);
        float noteHeight=entry.hasXValue?symbols.fontSize*1.25f:0;
        value.rectTransform.anchorMin=value.rectTransform.anchorMax=value.rectTransform.pivot=new Vector2(0,1);
        value.rectTransform.anchoredPosition=Vector2.zero;value.rectTransform.sizeDelta=new Vector2(body.rectTransform.rect.width,noteHeight);
        value.alignment=TMPro.TextAlignmentOptions.TopLeft;value.text="X="+entry.xValue;
        PreferenceText.SetSize(value,symbols.fontSize/GamePreferences.TextScale);
        symbols.text=CardSymbolText.Format(body.text);
        symbols.rectTransform.offsetMin=Vector2.zero;symbols.rectTransform.offsetMax=new Vector2(0,-noteHeight);
        float height=Mathf.Max(body.rectTransform.rect.height,symbols.GetPreferredValues(symbols.text,body.rectTransform.rect.width,10000).y+noteHeight+6);
        body.rectTransform.anchorMin=body.rectTransform.anchorMax=body.rectTransform.pivot=new Vector2(.5f,1);
        body.rectTransform.anchoredPosition=Vector2.zero;
        body.rectTransform.sizeDelta=new Vector2(body.rectTransform.sizeDelta.x,height);
        var content=body.transform.parent as RectTransform;
        content.sizeDelta=new Vector2(content.sizeDelta.x,height);
    }

    private string CastingChoiceName(string kind)
    {
        if(kind=="Generic")return TapPaymentText("额外费用","Additional cost","追加コスト");
        if(kind=="AltCost")return TapPaymentText("替代费用","Alternative cost","代替コスト");
        if(Regex.IsMatch(kind??"",@"^Reduce[WUBRG]$"))return TapPaymentText("减费","Cost reduction","コスト軽減")+" {"+kind.Substring(6)+"}";
        string name=kind switch { "Kicker1" or "Kicker2"=>"Kicker", "PromiseGift"=>"Gift", "Jumpstart"=>"Jump-start", "Foretold"=>"Foretell", "Plotted"=>"Plot", "MTMtE"=>"More Than Meets the Eye", "WebSlinging"=>"Web-slinging", _=>Regex.Replace(kind??"",@"(?<=[a-z])(?=[A-Z])"," ").Replace('_',' ') };
        var entry=DeckKeywordGlossary.Find(name,"en-US").FirstOrDefault(e=>string.Equals(e.English,name,StringComparison.OrdinalIgnoreCase));
        return entry?.Name??name;
    }

    private string ChosenSpellHeading(ForgeStackItem entry)
    {
        var costs=(entry.castingChoices??Array.Empty<ForgeCastingChoice>()).GroupBy(c=>CastingChoiceName(c.kind)).Select(g=>{
            var threshold=g.FirstOrDefault(c=>c.kind=="CASUALTY");
            if(threshold!=null)return g.Key+" "+threshold.count;
            var tappedPower=g.FirstOrDefault(c=>c.kind=="HARMONIZE");
            if(tappedPower!=null)return g.Key+" ("+TapPaymentText("力量","power","パワー")+" "+tappedPower.count+")";
            int repeats=g.Where(c=>c.kind=="MULTIKICKER"||c.kind=="REPLICATE"||c.kind=="SQUAD").Sum(c=>c.count);
            return g.Key+(repeats>1?" ×"+repeats:"");
        });
        return string.Join(" · ",costs);
    }

    private string ChosenSpellRules(ForgeStackItem entry)
    {
        if((entry.chosenModes?.Length??0)>0)return string.Join("\n\n",entry.chosenModes.Select(m=>ModeRules(entry.source,new ForgeOption{order=m.order,label=m.description},true).TrimStart(' ','\t','\r','\n','•','・').TrimEnd()));
        string rules=ModeRules(entry.source,new ForgeOption{faceKind=entry.castingChoices?.Any(c=>c.kind=="Evoke")==true?"evoke":"",label=entry.source.currentRules??""},false);
        return entry.castingChoices?.Any(c=>c.kind=="Overload")==true?OverloadedSpellRules(rules,entry.source.name):rules;
    }

    private string OverloadedSpellRules(string rules,string name)
    {
        var lines=Regex.Split((rules??"").Replace("\\n","\n"),"[\r\n]+");
        string effect=string.Join("\n",lines.Where(l=>!Regex.IsMatch(l.Trim(),@"^(?:Overload\b|超载|超載|超過)")));
        if(GamePreferences.Language=="zh-CN")return effect.Replace("目标","每个");
        if(GamePreferences.Language=="ja-JP") {
            // Japanese printings can include the exact overloaded effect in the
            // reminder; prefer that sentence to reconstructing its grammar.
            var reminder=Regex.Match(rules,@"そうしたなら[、,](?<effect>[^。]+。)");
            if(reminder.Success&&reminder.Groups["effect"].Value.Contains(GamePreferences.CardFaceName(name)))return reminder.Groups["effect"].Value;
            var target=Regex.Match(effect,@"^(?<objects>.+?)[１1](?:体|つ|枚)を対象とする。(?<effect>.+)$",RegexOptions.Singleline);
            if(target.Success)return target.Groups["effect"].Value.Replace("それら",target.Groups["objects"].Value+"すべて").Replace("それ",target.Groups["objects"].Value+"すべて");
            return effect.Replace("対象の","各");
        }
        return Regex.Replace(effect,@"\btarget\b","each",RegexOptions.IgnoreCase);
    }

    // A spell keeps its title/art and replaces printed alternatives with the
    // actual chosen effects. The bounded rules scroll never shrinks the font.
    private void PresentChosenSpellFace(RectTransform parent,ForgeStackItem entry,Sprite art)
    {
        // Choosing only X does not replace the printed card's frame or rules.
        PresentPrintedXCost(parent,entry,art);
        var face=parent.Find("ChosenSpellFace") as RectTransform;
        if(!HasSpellChoices(entry)){if(face!=null)face.gameObject.SetActive(false);PresentSecondaryXValue(parent,entry);return;}
        string key=GamePreferences.Language+"/"+GamePreferences.TextScale+"/"+parent.rect.size+"/"+entry.source.printingKey+"/"+ServerSnapshotJson.Serialize(entry);
        if(face!=null&&face.GetComponent<ChosenSpellVisualKey>().value==key){face.gameObject.SetActive(true);PresentSecondaryXValue(parent,entry);return;}
        if(face==null){face=Panel(parent,"ChosenSpellFace",Vector2.zero,parent.rect.size,Color.clear);face.GetComponent<Image>().raycastTarget=false;face.gameObject.AddComponent<ChosenSpellVisualKey>();}
        else {foreach(var child in face.Cast<Transform>().ToArray()){child.gameObject.SetActive(false);child.SetParent(null,false);Destroy(child.gameObject);}}
        face.GetComponent<ChosenSpellVisualKey>().value=key;face.gameObject.SetActive(true);face.sizeDelta=parent.rect.size;
        float width=face.rect.width,height=face.rect.height,topHeight=height*.55f,bodyHeight=height-topHeight;
        var head=Rect(face,"CardHeader",new Vector2(0,(height-topHeight)*.5f),new Vector2(width,topHeight));
        var image=head.gameObject.AddComponent<RawImage>();image.raycastTarget=false;image.texture=art?.texture;image.color=art!=null?Color.white:Ink;
        CardArtCrop.Header(image,art);
        if(entry.hasXValue) {
            var title=Panel(head,"SpellTitle",new Vector2(0,topHeight*.5f-20),new Vector2(width,40),Ink);
            title.GetComponent<Image>().raycastTarget=false;
            PresentSpellTitle(title,entry.source,title.sizeDelta);
        }
        else if(art==null)Label(head,"Name",GamePreferences.CardFaceName(entry.source.name),Vector2.zero,new Vector2(width-16,topHeight),16,Paper);
        var body=Panel(face,"RulesFrame",new Vector2(0,-topHeight*.5f),new Vector2(width,bodyHeight),new Color(.12f,.15f,.17f));body.GetComponent<Image>().raycastTarget=false;
        string heading=ChosenSpellHeading(entry);float headingHeight=string.IsNullOrEmpty(heading)?0:28;
        if(headingHeight>0){var title=Label(body,"CastingChoices",heading,new Vector2(0,bodyHeight*.5f-15),new Vector2(width-18,26),13,Gold);title.raycastTarget=false;SymbolizeStackLabel(title);}
        var paper=Panel(body,"ChosenRules",new Vector2(0,-headingHeight*.5f),new Vector2(width-12,bodyHeight-headingHeight-12),new Color(.91f,.93f,.87f));
        paper.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(228,140,7);paper.GetComponent<Image>().raycastTarget=false;
        var content=Scroll(paper,"RulesScroll",Vector2.zero,paper.sizeDelta-new Vector2(12,10));
        var text=Label(content,"Effects",ChosenSpellRules(entry),Vector2.zero,new Vector2(width-34,100),14,new Color(.055f,.075f,.085f));
        text.alignment=TextAnchor.UpperLeft;text.supportRichText=false;text.raycastTarget=false;text.resizeTextForBestFit=false;
        (text.GetComponent<PreferenceText>()??text.gameObject.AddComponent<PreferenceText>()).SetBaseSize(14);
        var symbols=SymbolizeStackLabel(text);
        float textHeight=Mathf.Max(paper.rect.height-10,symbols.GetPreferredValues(symbols.text,width-34,10000).y+6);
        content.sizeDelta=new Vector2(width-24,textHeight);text.rectTransform.anchorMin=text.rectTransform.anchorMax=text.rectTransform.pivot=new Vector2(.5f,1);
        text.rectTransform.sizeDelta=new Vector2(width-34,textHeight);
        if(textHeight>paper.rect.height-9)AddStackScrollIndicator(content);
        PresentSecondaryXValue(parent,entry);
    }
}

public sealed class ChosenSpellVisualKey : MonoBehaviour { public string value; }
