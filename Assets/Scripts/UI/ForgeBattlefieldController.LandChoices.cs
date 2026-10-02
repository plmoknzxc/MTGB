using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform landChoiceMenu, choiceDismiss;
    private int clickedLandId, choiceSeat;
    private Vector2 clickedChoiceAnchor;

    private void RememberLandChoice(ForgeOption option)
    {
        if(!integrated || !option.id.StartsWith("card:") || !int.TryParse(option.id.Substring(5),out int id))return;
        id=PreparedSource(option.id)?.id??id;
        clickedLandId=id;choiceSeat=seat;
        var visual=arenaCards.Values.FirstOrDefault(c=>c.root.name=="Card_"+id);
        if(visual==null)return;
        var corners=new Vector3[4];visual.face.GetWorldCorners(corners);
        clickedChoiceAnchor=new Vector2(surface.InverseTransformPoint(visual.root.position).x,corners.Max(c=>surface.InverseTransformPoint(c).y));
    }

    // Only unambiguous production/color options become mana buttons. A spell's
    // mana cost or an ability with additional consequences must retain its text.
    public static string ChoiceManaSymbols(string label)
    {
        switch((label??"").Trim().ToUpperInvariant()) {
            case "WHITE":return "{W}";case "BLUE":return "{U}";case "BLACK":return "{B}";
            case "RED":return "{R}";case "GREEN":return "{G}";case "COLORLESS":return "{C}";
        }
        var match=Regex.Match(label??"",@"^\s*(?:Add\s+)?((?:\{[WUBRGC0-9/]+\}\s*)+)\.?\s*$",RegexOptions.IgnoreCase);
        return match.Success?match.Groups[1].Value.Trim():null;
    }

    private ForgeCardView AbilityChoiceSource()
    {
        if(view.request.sourceCard!=null && !string.IsNullOrEmpty(view.request.sourceCard.name))return view.request.sourceCard;
        int id=view.request.sourceCardId>0?view.request.sourceCardId:choiceSeat==seat?clickedLandId:0;
        return (view.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>(p.hand??Array.Empty<ForgeCardView>())
            .Concat(p.battlefield??Array.Empty<ForgeCardView>())).FirstOrDefault(c=>c.id==id);
    }

    private bool PrepareLandChoices()
    {
        if(landChoiceMenu!=null){landChoiceMenu.gameObject.SetActive(false);Clear(landChoiceMenu);}
        if(choiceDismiss!=null)choiceDismiss.gameObject.SetActive(false);
        if(view?.awaitingInput!=true || (view.request.kind!="dialog"&&view.request.kind!="ChooseNumber"&&view.request.kind!="ChooseModes") || IsCardPickerRequest)return false;
        var options=view.request.options.Where(o=>o.id!="cancel" && o.id!="ok" && !RepresentedOnBoard(o.id)).ToArray();
        if(options.Length==0)return false;
        var source=AbilityChoiceSource();
        Vector2 anchor=clickedChoiceAnchor;
        var sourceVisual=arenaCards.Values.FirstOrDefault(c=>c.root.name=="Card_"+source?.id);
        if(sourceVisual!=null) {
            var corners=new Vector3[4];sourceVisual.face.GetWorldCorners(corners);
            anchor=new Vector2(surface.InverseTransformPoint(sourceVisual.root.position).x,corners.Max(c=>surface.InverseTransformPoint(c).y));
        }
        bool mana=options.All(o=>ChoiceManaSymbols(o.label)!=null);
        bool confirmation=options.All(o=>o.label=="Yes" || o.label=="No" || o.label=="是" || o.label=="否");
        bool cards=!mana && !confirmation;
        if(choiceDismiss==null) {
            choiceDismiss=Panel(surface,"DismissAbilityChoices",Vector2.zero,Vector2.zero,Color.clear);
            choiceDismiss.anchorMin=Vector2.zero;choiceDismiss.anchorMax=Vector2.one;
            choiceDismiss.offsetMin=choiceDismiss.offsetMax=Vector2.zero;
            choiceDismiss.gameObject.AddComponent<ForgeChoiceDismiss>().ListenForRightClick=true;
        }
        long captured=requestId;
        choiceDismiss.GetComponent<Image>().color=new Color(.015f,.025f,.04f,.32f);
        choiceDismiss.GetComponent<ForgeChoiceDismiss>().Dismiss=()=>{if(canAct && !submitted && OptionIndex("cancel")>=0)Choose(OptionIndex("cancel"),captured);};
        choiceDismiss.gameObject.SetActive(true);choiceDismiss.SetAsLastSibling();
        if(landChoiceMenu==null)landChoiceMenu=Panel(surface,"CardAbilityChoices",Vector2.zero,Vector2.zero,Color.clear);
        var menuDismiss=landChoiceMenu.GetComponent<ForgeChoiceDismiss>();
        if(menuDismiss==null)menuDismiss=landChoiceMenu.gameObject.AddComponent<ForgeChoiceDismiss>();
        menuDismiss.Dismiss=choiceDismiss.GetComponent<ForgeChoiceDismiss>().Dismiss;
        landChoiceMenu.anchorMin=landChoiceMenu.anchorMax=landChoiceMenu.pivot=new Vector2(.5f,.5f);
        landChoiceMenu.GetComponent<Image>().color=cards?Color.clear:Ink;
        landChoiceMenu.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(500,240,16);
        if(view.request.kind=="ChooseModes") {PresentSpreeChoices(source,anchor,captured);return true;}
        bool numeric=options.Length>0&&options.All(o=>int.TryParse(o.label,out _));
        if(numeric || view.request.kind=="ChooseNumber") {
            PresentNumericChoices(options,source,anchor,captured);return true;
        }
        int columns=Math.Min(options.Length,cards?3:6);
        bool hasFooter=view.request.options.Any(o=>o.id=="ok");
        float width=Mathf.Min(surface.rect.width-64,cards?columns*232+24:mana?columns*44+24:228);
        float height=cards?Mathf.Min(432,surface.rect.height-80):(mana?96:104)+(hasFooter?32:0);
        landChoiceMenu.sizeDelta=new Vector2(width,height);
        landChoiceMenu.anchoredPosition=new Vector2(
            Mathf.Clamp(anchor.x,surface.rect.xMin+width/2+20,surface.rect.xMax-width/2-20),
            Mathf.Clamp(anchor.y+height/2+18,surface.rect.yMin+height/2+20,surface.rect.yMax-height/2-20));
        var title=Label(landChoiceMenu,"Title",mana?"选择法术力":confirmation?"是否执行？":"选择异能或施放方式",new Vector2(0,height/2-(cards?20:16)),new Vector2(width,26),cards?21:17,Color.white);
        title.raycastTarget=false;
        string name=source?.name??"";
        if(source!=null && catalog.TryGetValue(source.printingKey??"",out var definition))name=GamePreferences.CardName(definition);
        var subtitle=Label(landChoiceMenu,"Source",name,new Vector2(0,height/2-(cards?48:36)),new Vector2(width-20,20),cards?15:13,new Color(.7f,.78f,.84f));subtitle.raycastTarget=false;
        float viewportHeight=cards?height-(hasFooter?128:88):46,cellWidth=(width-24)/columns;
        var content=Scroll(landChoiceMenu,"Choices",new Vector2(0,cards?(hasFooter?-12:-32):(hasFooter?-4:-22)),new Vector2(width,viewportHeight));
        var scroll=content.parent.GetComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=35;
        float itemHeight=cards?viewportHeight-12:mana?36:32;
        int rows=(options.Length+columns-1)/columns;
        content.anchorMin=content.anchorMax=content.pivot=new Vector2(.5f,1);
        content.sizeDelta=new Vector2(width,rows*(itemHeight+18));
        if(rows>1)AddStackScrollIndicator(content);
        for(int i=0;i<options.Length;i++) {
            var option=options[i];int index=OptionIndex(option.id);
            var size=new Vector2(cards?cellWidth-16:mana?36:cellWidth-10,itemHeight);
            var tile=Panel(content,"Option_"+index,Vector2.zero,size,new Color(.25f,.48f,.57f));
            tile.anchorMin=tile.anchorMax=tile.pivot=new Vector2(.5f,1);
            int rowCount=Math.Min(columns,options.Length-i/columns*columns);
            tile.anchoredPosition=new Vector2((i%columns-(rowCount-1)*.5f)*cellWidth,-4-(i/columns)*(itemHeight+18));
            tile.GetComponent<Image>().sprite=cards?AbilityShape():mana?IntegratedUiUtility.Circle(96):IntegratedUiUtility.RoundedRect(112,36,7);
            var button=tile.gameObject.AddComponent<Button>();button.targetGraphic=tile.GetComponent<Image>();
            button.onClick.AddListener(()=>Choose(index,captured));button.interactable=canAct&&!submitted;
            tile.gameObject.AddComponent<CanvasGroup>().alpha=button.interactable?1f:.45f;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.3f,1.8f,2);colors.pressedColor=Gold;colors.disabledColor=new Color(.35f,.35f,.35f);button.colors=colors;
            var inner=Panel(tile,"Face",Vector2.zero,size-Vector2.one*5,new Color(.055f,.085f,.11f));
            inner.GetComponent<Image>().sprite=tile.GetComponent<Image>().sprite;inner.GetComponent<Image>().raycastTarget=false;
            if(mana) {
                var symbol=CardSymbolText.Create(inner,"Mana",26,Color.white);IntegratedUiUtility.Place(symbol.rectTransform,Vector2.zero,new Vector2(size.x-6,size.y-6));
                symbol.alignment=TMPro.TextAlignmentOptions.Center;symbol.text=CardSymbolText.Format(ChoiceManaSymbols(option.label));
            }
            else if(cards)DrawAbilityChoice(inner,source,option,size-Vector2.one*5);
            else {var text=Label(inner,"Answer",RequestOptionLabel(option),Vector2.zero,size-Vector2.one*4,19,Paper);text.raycastTarget=false;}
        }
        // Dismissible choices use outside-click/right-click; keep only an actual engine confirmation.
        var footer=view.request.options.Where(o=>o.id=="ok").ToArray();
        for(int i=0;i<footer.Length;i++) {
            int action=OptionIndex(footer[i].id);
            var close=Button(landChoiceMenu,footer[i].id=="cancel"?"Cancel":"Confirm",footer[i].id=="cancel"?"取消":"确认",new Vector2((i-(footer.Length-1)*.5f)*(cards?182:120),-height/2+(cards?24:19)),new Vector2(cards?166:106,cards?42:28),()=>Choose(action,captured),new Color(.14f,.2f,.26f));
            close.interactable=canAct&&!submitted;close.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(166,42,10);
        }
        landChoiceMenu.gameObject.SetActive(true);landChoiceMenu.SetAsLastSibling();return true;
    }
    private void PresentNumericChoices(ForgeOption[] options,ForgeCardView source,Vector2 anchor,long captured)
    {
        const float width=264,height=154;
        landChoiceMenu.sizeDelta=new Vector2(width,height);landChoiceMenu.GetComponent<Image>().color=Ink;
        landChoiceMenu.anchoredPosition=new Vector2(Mathf.Clamp(anchor.x,surface.rect.xMin+width/2+20,surface.rect.xMax-width/2-20),
            Mathf.Clamp(anchor.y+height/2+18,surface.rect.yMin+height/2+20,surface.rect.yMax-height/2-20));
        bool serverRange=view.request.kind=="ChooseNumber";
        var choices=serverRange?options:options.OrderBy(o=>int.Parse(o.label)).ToArray();int selected=0;
        Label(landChoiceMenu,"Title","选择数值",new Vector2(0,55),new Vector2(width-20,26),19,Paper);
        var context=Label(landChoiceMenu,"Context",view.request.message.StartsWith("one: ")?view.request.message.Substring(5):view.request.message,
            new Vector2(0,29),new Vector2(width-22,26),13,new Color(.7f,.78f,.84f));context.supportRichText=false;
        context.resizeTextForBestFit=true;context.resizeTextMinSize=10;context.resizeTextMaxSize=13;
        var value=Label(landChoiceMenu,"NumericValue",serverRange?view.request.selectedValue.ToString():choices[0].label,new Vector2(0,-5),new Vector2(100,36),28,Paper);value.fontStyle=FontStyle.Bold;
        if(serverRange) {
            foreach(var item in new[]{("Decrease","−","number-minus",-68f),("Increase","+","number-plus",68f),("DecreaseFive","−5","number-minus-five",-110f),("IncreaseFive","+5","number-plus-five",110f)}) {
                int index=OptionIndex(item.Item3);
                var control=Button(landChoiceMenu,item.Item1,item.Item2,new Vector2(item.Item4,-5),new Vector2(36,34),()=>Choose(index,captured),new Color(.14f,.2f,.26f));
                control.interactable=index>=0&&canAct&&!submitted;var label=control.GetComponentInChildren<Text>();label.fontSize=16;
                label.rectTransform.sizeDelta=new Vector2(36,34);label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Truncate;
            }
            var commit=Button(landChoiceMenu,"ConfirmValue","确认",new Vector2(0,-52),new Vector2(140,32),()=>Choose(OptionIndex("confirm-number"),captured),new Color(.18f,.32f,.49f));
            commit.interactable=canAct&&!submitted;landChoiceMenu.gameObject.SetActive(true);landChoiceMenu.SetAsLastSibling();return;
        }
        Button minus=null,plus=null;
        Action refresh=()=>{value.text=choices[selected].label;minus.interactable=canAct&&!submitted&&selected>0;plus.interactable=canAct&&!submitted&&selected+1<choices.Length;};
        minus=Button(landChoiceMenu,"Decrease","−",new Vector2(-78,-5),new Vector2(40,34),()=>{if(selected>0)selected--;refresh();},new Color(.14f,.2f,.26f));
        plus=Button(landChoiceMenu,"Increase","+",new Vector2(78,-5),new Vector2(40,34),()=>{if(selected+1<choices.Length)selected++;refresh();},new Color(.14f,.2f,.26f));
        var confirm=Button(landChoiceMenu,"ConfirmValue","确认",new Vector2(0,-52),new Vector2(140,32),()=>Choose(OptionIndex(choices[selected].id),captured),new Color(.18f,.32f,.49f));
        confirm.interactable=canAct&&!submitted;refresh();landChoiceMenu.gameObject.SetActive(true);landChoiceMenu.SetAsLastSibling();
    }

    private void DrawAbilityChoice(RectTransform face,ForgeCardView source,ForgeOption option,Vector2 size)
    {
        string description=option.label??"";
        string title="发动能力",cost="";
        var keyword=Regex.Match(description,@"^(Evoke|Cycling|Ninjutsu|Basic landcycling|Plainscycling|Islandcycling|Swampcycling|Mountaincycling|Forestcycling|Flashback|Unearth|Equip|Crew|Surge|Kicker|Cast|Play)\b",RegexOptions.IgnoreCase);
        if(keyword.Success) {
            switch(keyword.Value.ToLowerInvariant()) {
                case "evoke":title="呼魂";break;case "cycling":title="循环";break;case "ninjutsu":title="忍术";break;case "equip":title="佩带";break;case "crew":title="搭载";break;
                case "flashback":title="返照";break;case "unearth":title="破坟";break;case "kicker":title="增幅";break;case "surge":title="怒涛";break;
                case "cast":case "play":title="施放";break;default:title="地循环";break;
            }
            description=description.Substring(keyword.Length).TrimStart(' ',':','—','-');
        }
        else if(source!=null && description.StartsWith(source.name,StringComparison.Ordinal))title="施放";
        var prefix=Regex.Match(description,keyword.Success?@"^(?<cost>(?:\{[^{}]+\}\s*)+)\s*[:—-]?\s*":@"^(?<cost>(?:\{[^{}]+\}\s*)+|[+−-]?\d+)\s*[:—-]\s*");
        if(prefix.Success){cost=prefix.Groups["cost"].Value;description=description.Substring(prefix.Length);}
        bool casting=option.castingFace!=null&&!string.IsNullOrEmpty(option.castingFace.name);
        if(casting) {
            source=option.castingFace;description=source.currentRules?.Replace("\\n","\n")??"";cost=source.manaCost;
            title=option.faceKind=="prepare"?"备法":option.faceKind=="adventure"?"历险":option.faceKind=="omen"?"前兆":option.faceKind=="mdfc-front"?"正面":option.faceKind=="mdfc-back"?"背面":"施放";
        }
        float top=size.y/2;
        var heading=Label(face,"Mode",title,new Vector2(0,top-19),new Vector2(size.x-18,28),20,Color.white);heading.raycastTarget=false;
        Sprite sprite=null;
        if(source!=null && !source.faceDown && catalog.TryGetValue(source.printingKey??"",out var definition)) {
            CardImageLibrary.TryLoadFullCard(definition,source.backFace?1:0,out sprite);
            if(description==source.name)description=GamePreferences.CardRules(definition);
        }
        if(sprite==null)TryBattlefieldArt(source,out sprite);
        if(casting && option.faceKind.StartsWith("mdfc-")) {
            var full=Rect(face,"CastingFace",new Vector2(0,-10),new Vector2(size.x-14,size.y-64)).gameObject.AddComponent<Image>();
            full.sprite=sprite;full.preserveAspect=true;full.color=sprite==null?Ink:Color.white;full.raycastTarget=false;
            if(sprite==null)Label(face,"MissingFace",source.name+"\n"+source.currentType+"\n"+description,Vector2.zero,size-Vector2.one*18,16,Paper);
            var name=Label(face,"FaceName",source.name,new Vector2(0,-size.y*.5f+12),new Vector2(size.x-12,24),13,Paper);
            name.resizeTextForBestFit=true;name.resizeTextMinSize=10;name.resizeTextMaxSize=13;return;
        }
        float artHeight=size.y*.27f;
        var art=Rect(face,"Art",new Vector2(0,top-38-artHeight/2),new Vector2(size.x-16,artHeight)).gameObject.AddComponent<RawImage>();
        art.texture=sprite?.texture;art.color=sprite==null?new Color(.12f,.18f,.22f):Color.white;art.raycastTarget=false;
        if(sprite!=null){var r=sprite.rect;art.uvRect=new Rect((r.x+r.width*.07f)/sprite.texture.width,(r.y+r.height*.43f)/sprite.texture.height,r.width*.86f/sprite.texture.width,r.height*.38f/sprite.texture.height);}
        if(casting) {
            var strip=Panel(face,"FaceNameStrip",new Vector2(0,top-38-artHeight+10),new Vector2(size.x-16,24),new Color(.03f,.04f,.05f,.92f));strip.GetComponent<Image>().raycastTarget=false;
            var name=Label(strip,"FaceName",source.name,Vector2.zero,new Vector2(size.x-24,22),14,Paper);name.resizeTextForBestFit=true;name.resizeTextMinSize=10;name.resizeTextMaxSize=14;
        }
        float effectHeight=size.y-artHeight-94;
        var effect=Panel(face,"EffectPanel",new Vector2(0,top-44-artHeight-effectHeight/2),new Vector2(size.x-16,effectHeight),new Color(.86f,.88f,.84f));effect.GetComponent<Image>().raycastTarget=false;
        float typeHeight=casting?20:0;
        if(casting){var type=Label(effect,"Type",source.currentType,new Vector2(0,effectHeight*.5f-12),new Vector2(size.x-30,20),11,new Color(.16f,.2f,.21f));type.resizeTextForBestFit=true;type.resizeTextMinSize=9;type.resizeTextMaxSize=11;}
        var effectContent=Scroll(effect,"TextScroll",new Vector2(0,-typeHeight*.5f),new Vector2(size.x-28,effectHeight-12-typeHeight));
        var body=Label(effectContent,"Effect",description,Vector2.zero,new Vector2(size.x-38,effectHeight-14-typeHeight),casting?12:15,new Color(.055f,.075f,.085f));
        body.alignment=TextAnchor.UpperLeft;body.supportRichText=false;body.raycastTarget=false;
        float textHeight=Mathf.Max(effectHeight-14-typeHeight,body.preferredHeight+4);
        body.rectTransform.anchorMin=body.rectTransform.anchorMax=body.rectTransform.pivot=new Vector2(.5f,1);
        body.rectTransform.anchoredPosition=Vector2.zero;body.rectTransform.sizeDelta=new Vector2(size.x-38,textHeight);
        effectContent.sizeDelta=new Vector2(size.x-28,textHeight);
        if(textHeight>effectHeight-12-typeHeight)AddStackScrollIndicator(effectContent);
        if(!string.IsNullOrEmpty(cost)) {
            if(cost.Contains("{")){var symbols=CardSymbolText.Create(face,"Cost",25,Paper);IntegratedUiUtility.Place(symbols.rectTransform,new Vector2(0,-top+29),new Vector2(size.x-22,34));symbols.alignment=TMPro.TextAlignmentOptions.Center;symbols.text=CardSymbolText.Format(cost);}
            else {var value=Label(face,"Cost",cost,new Vector2(0,-top+29),new Vector2(size.x-22,34),26,Gold);value.raycastTarget=false;}
        }
        if(casting&&source.creature){var stats=Label(face,"CreatureStats",source.power+"/"+source.toughness,new Vector2(size.x*.5f-25,-top+29),new Vector2(42,28),18,Paper);stats.fontStyle=FontStyle.Bold;}
    }
    private void PresentSpreeChoices(ForgeCardView source,Vector2 anchor,long captured)
    {
        var modes=view.request.options.Where(o=>o.id.StartsWith("mode:")).ToArray();
        float width=430,height=96+modes.Length*82;
        landChoiceMenu.sizeDelta=new Vector2(width,height);landChoiceMenu.GetComponent<Image>().color=Ink;
        landChoiceMenu.anchoredPosition=new Vector2(Mathf.Clamp(anchor.x,surface.rect.xMin+width/2+16,surface.rect.xMax-width/2-16),Mathf.Clamp(anchor.y+height/2+18,surface.rect.yMin+height/2+16,surface.rect.yMax-height/2-16));
        Label(landChoiceMenu,"Title","追欢 · 选择一项或多项",new Vector2(0,height/2-20),new Vector2(width-24,28),19,Paper);
        for(int i=0;i<modes.Length;i++) {
            var option=modes[i];int index=OptionIndex(option.id);
            var button=Button(landChoiceMenu,"Mode_"+option.id,"",new Vector2(0,height/2-78-i*82),new Vector2(width-24,74),()=>Choose(index,captured),option.selected?new Color(.38f,.3f,.13f):new Color(.09f,.14f,.18f));
            button.interactable=canAct&&!submitted;button.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(400,74,7);
            Label(button.transform,"Selection",option.selected?"✓":"○",new Vector2(-182,0),new Vector2(26,28),22,option.selected?Gold:Paper);
            var body=Label(button.transform,"Effect",option.label,new Vector2(18,-8),new Vector2(334,49),15,Paper);body.alignment=TextAnchor.UpperLeft;body.supportRichText=false;
            body.resizeTextForBestFit=true;body.resizeTextMinSize=11;body.resizeTextMaxSize=15;
            var cost=CardSymbolText.Create(button.transform,"ExtraCost",17,Gold);IntegratedUiUtility.Place(cost.rectTransform,new Vector2(18,24),new Vector2(334,22));cost.text=CardSymbolText.Format("+ "+option.modeCost);cost.alignment=TMPro.TextAlignmentOptions.Left;
        }
        foreach(var item in new[]{("confirm-modes","确认",-76f),("reset-modes","重新选择",76f)}) {
            int index=OptionIndex(item.Item1);var button=Button(landChoiceMenu,item.Item1,item.Item2,new Vector2(item.Item3,-height/2+25),new Vector2(140,34),()=>Choose(index,captured),new Color(.18f,.3f,.44f));button.interactable=index>=0&&canAct&&!submitted;
        }
        landChoiceMenu.gameObject.SetActive(true);landChoiceMenu.SetAsLastSibling();
    }
}
