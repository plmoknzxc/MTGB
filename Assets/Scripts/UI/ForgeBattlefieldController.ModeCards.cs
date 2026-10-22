using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private void PresentModeCards(ForgeCardView source,long captured,bool modes,bool castingCosts=false)
    {
        var request=view.request;
        bool pawprint=request.selectionPurpose=="pawprint";
        var options=request.options.Where(o=>o.id.StartsWith(castingCosts?"choice:":modes?"mode:":"select:",StringComparison.Ordinal)).ToArray();
        if(!modes && !castingCosts && request.minCount==0) {
            // Zero optional costs is a native choice too: confirm zero, or reset the
            // existing selection before confirming. Never synthesize a response ID.
            string id=request.selectedCount==0?"confirm-cards":"reset-cards";
            options=new[]{new ForgeOption{id=id,label=TapPaymentText("正常施放","Cast normally","通常の唱え方"),selected=request.selectedCount==0}}.Concat(options).ToArray();
        }
        bool spree=request.selectionPurpose=="spree" || modes&&options.Any(o=>!string.IsNullOrEmpty(o.modeCost));
        // A filtered mode list can contain a single card, but still needs room
        // for confirmation, reset and cancel at the user's chosen text size.
        float width=Mathf.Min(surface.rect.width-100,Mathf.Max(536,options.Length*254+36)),height=Mathf.Min(540,surface.rect.height-80);
        landChoiceMenu.sizeDelta=new Vector2(width,height);landChoiceMenu.anchoredPosition=Vector2.zero;
        landChoiceMenu.GetComponent<Image>().color=Ink;
        string title=spree?TapPaymentText("追欢","Spree","放題"):modes?TapPaymentText("选择效果","Choose modes","モードを選択"):TapPaymentText("选择施放方式","Choose casting method","唱え方を選択");
        if(pawprint&&source!=null)title=GamePreferences.CardFaceName(source.name);
        var heading=Label(landChoiceMenu,"Title",title,new Vector2(0,height*.5f-30),new Vector2(width-32,54),24,Paper);
        heading.fontStyle=FontStyle.Bold;
        var rule=Panel(landChoiceMenu,"TitleLine",new Vector2(0,height*.5f-60),new Vector2(width-40,1),new Color(1,1,1,.45f));rule.GetComponent<Image>().raycastTarget=false;
        string instruction=pawprint?TapPaymentText($"爪印 {request.selectedValue}/{request.maxValue} · 可重复选择",$"Pawprints {request.selectedValue}/{request.maxValue} · Modes may repeat",$"足跡 {request.selectedValue}/{request.maxValue} · 同じモードを選択可"):!modes?TapPaymentText("选择施放方式","Choose a casting method","唱え方を選択"):
            TapPaymentText($"已选 {request.selectedCount} · 选择 {request.minCount}–{request.maxCount} 项",$"Selected {request.selectedCount} · Choose {request.minCount}–{request.maxCount}",$"選択済み {request.selectedCount} · {request.minCount}–{request.maxCount} 項目を選択");
        Label(landChoiceMenu,"Count",instruction,new Vector2(0,height*.5f-84),new Vector2(width-32,40),16,Paper);
        float cardWidth=Mathf.Min(234,(width-36)/Math.Max(1,Math.Min(4,options.Length))-16);
        string CostText(ForgeOption option) {
            if(pawprint)return TapPaymentText($"{option.pawprintCost} 爪印",$"{option.pawprintCost} pawprint"+(option.pawprintCost==1?"":"s"),$"足跡 {option.pawprintCost}");
            bool normal=castingCosts?option.faceKind=="normal":option.id=="confirm-cards"||option.id=="reset-cards";
            // Promising a gift has no additional mana cost. The native cost's
            // generic "Gift something" text is represented by the method heading.
            bool gift=!modes&&!castingCosts&&option.label.StartsWith("Promise Gift",StringComparison.Ordinal);
            string raw=castingCosts?option.modeCost:modes?(string.IsNullOrEmpty(option.modeCost)?"":"+ "+option.modeCost):normal||gift?source?.manaCost:string.IsNullOrEmpty(option.modeCost)?"":"+ "+option.modeCost;
            string prefix=raw?.StartsWith("+ ",StringComparison.Ordinal)==true?"+ ":"";
            if(prefix.Length>0)raw=raw.Substring(prefix.Length);
            string nativeCost=raw;raw=PrintedCostText(option.castingFace??source,raw);
            // Retain the existing fallback for old views without a rule baseline.
            if(raw==nativeCost&&raw=="Exile a green card from your hand")raw=TapPaymentText("从手牌中放逐一张绿色牌",raw,"あなたの手札から緑のカード１枚を追放する");
            return CardSymbolText.Format(prefix+(raw??""));
        }
        var costTexts=options.Select(CostText).ToArray();
        var costSizes=costTexts.Select(t=>t.Any(char.IsLetter)?17f:23f).ToArray();
        // Reserve measured space for nonmana costs as well as mana symbols.
        // Text costs such as Endurance's green-card exile can occupy two lines.
        var measure=CardSymbolText.Create(landChoiceMenu,"CostMeasure",23,Color.white);
        float costHeight=32;
        for(int i=0;i<costTexts.Length;i++) {
            measure.fontSize=costSizes[i]*GamePreferences.TextScale;
            costHeight=Mathf.Max(costHeight,measure.GetPreferredValues(costTexts[i],cardWidth,Mathf.Infinity).y+4);
        }
        UnityEngine.Object.Destroy(measure.gameObject);
        float methodHeight=modes?0:Mathf.Max(28,28*GamePreferences.TextScale);
        float headerHeight=costHeight+methodHeight+12;
        float cardHeight=Mathf.Min(cardWidth*1.4f,height-180-headerHeight-12);
        var content=Scroll(landChoiceMenu,"ModeOptions",new Vector2(0,-14),new Vector2(width-20,height-180));
        var scroll=content.parent.GetComponent<ScrollRect>();scroll.horizontal=true;scroll.vertical=false;
        content.anchorMin=content.anchorMax=new Vector2(0,1);content.pivot=new Vector2(0,1);
        content.sizeDelta=new Vector2(Mathf.Max(width-20,options.Length*(cardWidth+20)),height-180);
        content.anchoredPosition=new Vector2(-Mathf.Clamp(choiceScrollOffset,0,Mathf.Max(0,content.rect.width-scroll.viewport.rect.width)),0);
        float firstCardX=Mathf.Max(10,(width-20-(options.Length*(cardWidth+20)-20))*.5f);
        Sprite art=null;if(source!=null)TryBattlefieldArt(source,out art);
        for(int i=0;i<options.Length;i++) {
            var option=options[i];int index=OptionIndex(option.id);
            var button=Button(content,"Mode_"+option.id,"",Vector2.zero,new Vector2(cardWidth,cardHeight),()=>Choose(index,captured),option.selected?Gold:option.disabled?new Color(.2f,.24f,.28f):new Color(.28f,.83f,.95f));
            var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(firstCardX+i*(cardWidth+20),-headerHeight);
            button.transition=Selectable.Transition.None;button.interactable=canAct&&!submitted&&!option.disabled;
            var border=button.GetComponent<Image>();border.sprite=IntegratedUiUtility.RoundedRect(240,336,10);
            var selection=Label(button.transform,"Selection",option.selected?(pawprint?"×"+option.chosenCount:"✓"):"",new Vector2(cardWidth*.5f-28,cardHeight*.5f-18),new Vector2(46,30),18,Gold);selection.gameObject.SetActive(option.selected);
            // Clip away the entire printed rules area instead of placing a smaller
            // opaque rectangle over it. No costs, other modes or flavour can peek out.
            float faceHeight=cardHeight-8,faceWidth=cardWidth-8,headHeight=Mathf.Min(92,faceHeight*.35f);
            var head=Rect(rect,"CardHeader",new Vector2(0,(faceHeight-headHeight)*.5f),new Vector2(faceWidth,headHeight));
            var face=Rect(head,"Artwork",Vector2.zero,new Vector2(faceWidth,headHeight));
            var image=face.gameObject.AddComponent<RawImage>();image.texture=art?.texture;image.color=art==null?Ink:Color.white;image.raycastTarget=false;
            CardArtCrop.Header(image,art);
            float bodyHeight=faceHeight-headHeight;
            var frame=Panel(rect,"RulesFrame",new Vector2(0,-headHeight*.5f),new Vector2(faceWidth,bodyHeight),new Color(.13f,.15f,.16f));frame.GetComponent<Image>().raycastTarget=false;
            var bodyPanel=Panel(frame,"ModeRules",new Vector2(0,-2),new Vector2(faceWidth-14,bodyHeight-18),new Color(.85f,.87f,.81f));
            bodyPanel.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(210,148,9);bodyPanel.GetComponent<Image>().raycastTarget=false;
            var inset=Panel(bodyPanel,"PaperInset",Vector2.zero,bodyPanel.sizeDelta-new Vector2(4,4),new Color(.92f,.93f,.88f));
            inset.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(206,144,7);inset.GetComponent<Image>().raycastTarget=false;
            float visibleRulesHeight=bodyHeight-34;
            var rules=Scroll(bodyPanel,"RulesScroll",Vector2.zero,new Vector2(cardWidth-40,visibleRulesHeight));
            var text=Label(rules,"Effect",ModeRules(source,option,modes),Vector2.zero,new Vector2(cardWidth-46,visibleRulesHeight),17,new Color(.07f,.09f,.09f));
            text.alignment=TextAnchor.UpperLeft;text.resizeTextForBestFit=false;text.supportRichText=false;
            text.rectTransform.anchorMin=text.rectTransform.anchorMax=text.rectTransform.pivot=new Vector2(.5f,1);
            float textHeight=Mathf.Max(visibleRulesHeight,text.preferredHeight+6);text.rectTransform.sizeDelta=new Vector2(cardWidth-46,textHeight);rules.sizeDelta=new Vector2(cardWidth-40,textHeight);
            if(textHeight>visibleRulesHeight)AddStackScrollIndicator(rules);
            if(art==null)Label(head,"MissingArt",TapPaymentText("缺失卡图","Missing card art","カード画像なし"),Vector2.zero,new Vector2(cardWidth-20,28),16,Paper);
            if(!modes||!string.IsNullOrWhiteSpace(costTexts[i])) {
                var promptFrame=Panel(rect,"CastingPromptFrame",new Vector2(0,cardHeight*.5f+headerHeight*.5f),new Vector2(cardWidth,headerHeight-4),new Color(.3f,.43f,.49f));
                promptFrame.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(240,96,8);promptFrame.GetComponent<Image>().raycastTarget=false;
                var promptBackground=Panel(promptFrame,"Background",Vector2.zero,promptFrame.sizeDelta-new Vector2(2,2),Ink);
                promptBackground.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(238,94,7);promptBackground.GetComponent<Image>().raycastTarget=false;
            }
            var cost=CardSymbolText.Create(rect,"ExtraCost",costSizes[i],Color.white);IntegratedUiUtility.Place(cost.rectTransform,new Vector2(0,cardHeight*.5f+4+costHeight*.5f),new Vector2(cardWidth,costHeight));cost.alignment=TMPro.TextAlignmentOptions.Center;cost.text=costTexts[i];
            if(!modes) {
                bool normal=castingCosts?option.faceKind=="normal":option.id=="confirm-cards"||option.id=="reset-cards";
                string name=normal?TapPaymentText("正常施放","Cast normally","通常の唱え方"):castingCosts?TapPaymentText("呼魂","Evoke","想起"):
                    option.label.StartsWith("Kicker",StringComparison.OrdinalIgnoreCase)?TapPaymentText("增幅","Kicker","キッカー"):
                    option.label.StartsWith("Promise Gift",StringComparison.Ordinal)?TapPaymentText("承诺赠礼","Promise gift","贈り物を約束"):
                    DeckKeywordGlossary.All.FirstOrDefault(e=>Regex.IsMatch(option.label??"","^"+Regex.Escape(e.English)+@"(?:\s|[—–-]|$)"))?.Name??GamePreferences.NativeText(option.label);
                Label(rect,"CastingMethod",name,new Vector2(0,cardHeight*.5f+costHeight+6+methodHeight*.5f),new Vector2(cardWidth,methodHeight),19,Paper);
            }
            // Dim colours, not opacity: a disabled card must still hide anything
            // behind its rules box (including an active battlefield hover preview).
            if(option.disabled) {
                image.color=new Color(.38f,.38f,.38f,1);
                bodyPanel.GetComponent<Image>().color=new Color(.24f,.26f,.25f,1);
                inset.GetComponent<Image>().color=new Color(.27f,.29f,.27f,1);
                text.color=new Color(.62f,.65f,.63f,1);
            }
            selection.transform.SetAsLastSibling();
        }
        string confirmId=modes?"confirm-modes":"confirm-cards",resetId=modes?"reset-modes":"reset-cards";
        var actions=new[]{(confirmId,TapPaymentText("下一步","Next","次へ")),(resetId,TapPaymentText("重新选择","Reset","選び直す")),("cancel",TapPaymentText("取消","Cancel","キャンセル"))}
            .Where(a=>OptionIndex(a.Item1)>=0||!castingCosts&&a.Item1!="cancel").ToArray();
        float actionWidth=Mathf.Min(156,(width-40)/Math.Max(1,actions.Length)-12);
        for(int i=0;i<actions.Length;i++) {
            int index=OptionIndex(actions[i].Item1);
            var button=Button(landChoiceMenu,actions[i].Item1,actions[i].Item2,new Vector2((i-(actions.Length-1)*.5f)*(actionWidth+12),-height*.5f+28),new Vector2(actionWidth,42),()=>Choose(index,captured),i==0?new Color(.22f,.37f,.55f):Ink);
            button.interactable=canAct&&!submitted&&index>=0&&!request.options[index].disabled;
        }
        landChoiceMenu.gameObject.SetActive(true);landChoiceMenu.SetAsLastSibling();
    }

    private string ModeRules(ForgeCardView source,ForgeOption option,bool modes)
    {
        if(source==null || !catalog.TryGetValue(source.printingKey??"",out var definition))return GamePreferences.NativeText(option.label);
        string localized=GamePreferences.CardRules(definition);
        if(!modes) {
            var printed=Regex.Split(localized.Replace("\\n","\n"),"[\r\n]+");
            // Cost/reminder lines are represented by the casting-method heading.
            // Keep the printed effects (including conditional kicker text) intact.
            string effect=string.Join("\n",printed.Where(l=>!Regex.IsMatch(l.Trim(),@"^(?:Kicker\b|Evoke\b|Bargain\b|Offspring\b|增幅|呼魂|加码|繁生|キッカー|想起|協約|新生)")));
            if(option.faceKind=="evoke")effect+="\n"+TapPaymentText("以呼魂施放：进战场时牺牲它。","Cast with evoke: sacrifice it when it enters.","想起で唱えた場合、戦場に出たときにこれを生け贄に捧げる。");
            return effect;
        }
        // Use the original rule-line identity; native filtering can remove earlier modes.
        string[] Lines(string rules)=>Regex.Split((rules??"").Replace("\\n","\n"),"[\r\n]+").Where(l=>Regex.IsMatch(l.Trim(),@"^(?:[+＋•・]|—|\{(?:P|oH+)\})")).ToArray();
        var english=Lines(definition.description);var translated=Lines(localized);
        if(option.order>0 && english.Length==translated.Length && option.order<=translated.Length) {
            string line=translated[option.order-1].Trim();
            line=Regex.Replace(line,@"^(?:\{(?:P|oH+)\}\s*)+[—―－〜～~-]\s*","");
            line=Regex.Replace(line,@"^[+＋]\s*(?:\{[^}]+\}\s*)+\s*[—－〜～~-]\s*","");
            return Regex.Replace(line,@"^[•・—]\s*","");
        }
        return GamePreferences.NativeText(option.label);
    }
}
