using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public sealed partial class ForgeBattlefieldController
{
    private static Sprite abilityShape;
    private static Sprite AbilityShape()
    {
        if(abilityShape!=null)return abilityShape;
        const int width=240,height=335;
        var pixels=new Color[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++) {
            // Straight sides and a shallow elliptical base, as on an ability card.
            float half=y<54?120*Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow((54-y-.5f)/54,2))):120;
            pixels[y*width+x]=new Color(1,1,1,Mathf.Clamp01(half-Mathf.Abs(x+.5f-120)));
        }
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        texture.SetPixels(pixels);texture.Apply(false,true);
        abilityShape=Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),100);
        abilityShape.hideFlags=HideFlags.HideAndDontSave;return abilityShape;
    }
    private string AbilityEffectText(ForgeCardView source,string effect)
    {
        string prefix=source?.name+" ("+source?.id+") - ";
        string effectText=effect!=null&&effect.StartsWith(prefix,System.StringComparison.Ordinal)?effect.Substring(prefix.Length):effect??"";
        effectText=System.Text.RegularExpressions.Regex.Replace(effectText,@"\s*\[Zone Changer: [^\r\n]*\]\s*$","");
        string eventContext="";
        var eventSuffix=System.Text.RegularExpressions.Regex.Match(effectText,@"\s+\[Card: (.+?) \((\d+)\), Activator: Player (\d+), SpellAbility: [^\r\n]*\]\s*$");
        if(eventSuffix.Success) {
            effectText=effectText.Substring(0,eventSuffix.Index);
            eventContext="\n"+TapPaymentText("触发牌：","Triggering card: ","誘発したカード：")+GamePreferences.CardFaceName(eventSuffix.Groups[1].Value)+" ("+eventSuffix.Groups[2].Value+")";
            if(int.TryParse(eventSuffix.Groups[3].Value,out int actor))eventContext+=" · "+PlayerDisplayName(actor);
        }
        string specializedContext="";
        string specializedSuffix=" [Specialized: "+source?.name+" ("+source?.id+")]";
        if(IsSpecializedFace(source)&&effectText.EndsWith(specializedSuffix,System.StringComparison.Ordinal)) {
            effectText=effectText.Substring(0,effectText.Length-specializedSuffix.Length);
            specializedContext="\n["+TapPaymentText("精进形态：","Specialized: ","専門化：")+GamePreferences.CardFaceName(source.name)+" ("+source.id+")]";
        }
        string addedContext="";
        string addedSuffix=" [Added once: "+source?.name+" ("+source?.id+")]";
        if(source!=null&&effectText.EndsWith(addedSuffix,System.StringComparison.Ordinal)) {
            effectText=effectText.Substring(0,effectText.Length-addedSuffix.Length);
            addedContext="\n["+TapPaymentText("添加来源：","Added once: ","追加元：")+GamePreferences.CardFaceName(source.name)+" ("+source.id+")]";
        }
        // WrappedAbility and SpellAbilityEffect append targets after the printed
        // paragraph. Match the rule separately, then retain the actual targets.
        var targets=System.Text.RegularExpressions.Regex.Match(effectText,@"\s+\(Targeting: ([^\r\n]*)\)$");
        string paragraph=targets.Success?effectText.Substring(0,targets.Index):effectText;
        // Native mana triggers append the actual produced symbols after the
        // printed rule. Match that rule separately and keep its result visible.
        var produced=System.Text.RegularExpressions.Regex.Match(paragraph,@"\s+\[Produced: ((?:\{(?:[WUBRGC]|[0-9]+)\})+)\]\s*$");
        string producedContext="";
        if(source!=null&&!source.faceDown&&produced.Success) {
            paragraph=paragraph.Substring(0,produced.Index);
            producedContext="\n["+TapPaymentText("产生：","Produced: ","生成：")+produced.Groups[1].Value+"]";
        }
        var attacking=System.Text.RegularExpressions.Regex.Match(paragraph,@"\s+\[Number Attackers: \[([^\r\n]*)\]\]$");
        string attackerContext="";
        if(attacking.Success && source!=null && !source.faceDown) {
            string attackerNames=attacking.Groups[1].Value;
            var objects=System.Text.RegularExpressions.Regex.Matches(attackerNames,@"(.+?) \(([0-9]+)\)(?:,\s*|$)");
            var visible=(view?.players??System.Array.Empty<ForgePlayerView>()).SelectMany(p=>p.battlefield??System.Array.Empty<ForgeCardView>()).ToArray();
            bool exact=objects.Count>0 && string.Concat(objects.Cast<System.Text.RegularExpressions.Match>().Select(m=>m.Value))==attackerNames
                && objects.Cast<System.Text.RegularExpressions.Match>().All(m=>visible.Any(c=>!c.faceDown&&c.id.ToString()==m.Groups[2].Value&&c.name==m.Groups[1].Value));
            if(exact) {
                attackerContext="\n"+TapPaymentText("攻击者：","Attackers: ","攻撃クリーチャー：")
                    +string.Join(", ",objects.Cast<System.Text.RegularExpressions.Match>().Select(m=>GamePreferences.CardFaceName(m.Groups[1].Value)+" ("+m.Groups[2].Value+")"));
                paragraph=paragraph.Substring(0,attacking.Index);
            }
        }
        paragraph=DungeonAbilityText(source,paragraph);
        // BlightEffect writes the actual recipient and amount instead of the
        // printed ability paragraph. Leave extended/custom sentences intact.
        var blight=System.Text.RegularExpressions.Regex.Match(paragraph,@"^Player (\d+) blight (\d+)\.\s*$");
        if(blight.Success)paragraph=TapPaymentText(
            "玩家 "+blight.Groups[1].Value+" 衰萎 "+blight.Groups[2].Value+"。",
            paragraph,
            "プレイヤー"+blight.Groups[1].Value+"は、自分がコントロールするクリーチャー１体の上に-1/-1カウンターを"+blight.Groups[2].Value+"個置く。");
        string spentContext="";
        var spentEvent=System.Text.RegularExpressions.Regex.Match(paragraph,@"\s+(\(\[[^\r\n]*\]\)\s+\[Card: [^\r\n]*\])$");
        string spentRule=spentEvent.Success?paragraph.Substring(0,spentEvent.Index):paragraph;
        if(spentEvent.Success&&(spentRule=="When you spend this mana to cast an instant or sorcery spell with mana value 3 or less, you may exile that spell instead of putting it into its owner's graveyard as it resolves."||spentRule=="When you spend this mana to cast a Dragon creature spell, it enters with an additional +1/+1 counter on it and gains hexproof until your next turn. (It can't be the target of spells or abilities your opponents control.)")) {
            spentContext=spentEvent.Groups[1].Value;paragraph=spentRule;
        }
        // Exact spent-mana paragraphs are shorter than the source's full mana
        // ability. Keep unknown effects unchanged rather than guessing a rule.
        switch(paragraph) {
            case "Regeneration (if creature would be destroyed, regenerate it instead)":
                paragraph=GamePreferences.T(paragraph);break;
            case "You may exile that spell instead of putting it into its owner's graveyard as it resolves.":
                paragraph=TapPaymentText("于该咒语结算时，你可以将它放逐，而非置入其拥有者的坟墓场。",paragraph,"その呪文の解決に際し、それをオーナーの墓地に置く代わりに追放してもよい。");break;
            case "When you spend this mana to cast an instant or sorcery spell with mana value 3 or less, you may exile that spell instead of putting it into its owner's graveyard as it resolves.":
                paragraph=TapPaymentText("当你花费此法术力来施放法术力值等于或小于3的瞬间或法术咒语时，于该咒语结算时，你可以将它放逐，而非置入其拥有者的坟墓场。",paragraph,"このマナを使ってマナ総量が３以下のインスタントやソーサリーである呪文を唱えたとき、その呪文の解決に際し、それをオーナーの墓地に置く代わりに追放してもよい。");break;
            case "When you spend this mana to cast a Dragon creature spell, it enters with an additional +1/+1 counter on it and gains hexproof until your next turn. (It can't be the target of spells or abilities your opponents control.)":
                paragraph=TapPaymentText("当你花费此法术力来施放龙生物咒语时，该生物进战场时上面额外有一个+1/+1指示物，且获得辟邪异能直到你的下个回合。",paragraph,"このマナを使ってドラゴン・クリーチャー呪文を唱えたとき、それは追加の+1/+1カウンター１個が置かれた状態で戦場に出て、あなたの次のターンまで呪禁を得る。");break;
            case "If you tap a land you control for mana, it produces U instead of any other type.":
                paragraph=TapPaymentText("如果你将由你操控的地横置以产生法术力，则它改为产生{U}，而非任何其他类别的法术力。",paragraph,"あなたがコントロールする土地をマナを引き出す目的でタップするなら、それは他のタイプの代わりに{U}を生み出す。");break;
            case "If you tap a land for mana, it produces one mana of a color of your choice instead of any other type and amount.":
                paragraph=TapPaymentText("如果将地横置以产生法术力，则它改为产生一点由你选择颜色的法术力，而非任何其他类别与数量的法术力。",paragraph,"土地をマナを引き出す目的でタップするなら、それは他のタイプと点数の代わりにあなたが選んだ色のマナ１点を生み出す。");break;
            case "Lands tapped for mana produce mana of the chosen color instead of any other color.":
                paragraph=TapPaymentText("横置以产生法术力的地改为产生所选颜色的法术力，而非任何其他颜色。",paragraph,"マナを引き出す目的でタップされた土地は、他の色の代わりに選ばれた色のマナを生み出す。");break;
        }
        if(!string.IsNullOrEmpty(spentContext))paragraph+="\n"+spentContext;
        // A printed upkeep trigger appends its phase player after the rule.
        // Keep that event context without preventing exact translation lookup.
        var phase=System.Text.RegularExpressions.Regex.Match(paragraph,@"\s+\[Phase: Player (\d+)\]$");
        bool printedPhase=phase.Success&&!IsPublicRuleHelper(source);
        if(printedPhase)paragraph=paragraph.Substring(0,phase.Index);
        // Generated keyword abilities use a short rule and may append the
        // original remembered-object list. Translate only the exact rule;
        // preserve that list and the actual phase player separately.
        var departureContext=System.Text.RegularExpressions.Regex.Match(paragraph,@"\s+(\(\[[^\r\n]*\]\))$");
        string departureRule=departureContext.Success?paragraph.Substring(0,departureContext.Index):paragraph;
        string generatedDeparture=GeneratedDepartureRules(source,departureRule);
        if(generatedDeparture!=null)paragraph=generatedDeparture+(departureContext.Success?"\n"+departureContext.Groups[1].Value:"");
        string decayedAttacker=" [Attacker: "+source?.name+" ("+source?.id+")]";
        if(source!=null&&(source.keywords??System.Array.Empty<string>()).Contains("DECAYED")
            &&paragraph.EndsWith(decayedAttacker,System.StringComparison.Ordinal)) {
            string decayedRule=GeneratedDepartureRules(source,paragraph.Substring(0,paragraph.Length-decayedAttacker.Length));
            if(decayedRule!=null)paragraph=decayedRule+"\n["+TapPaymentText("攻击者：","Attacker: ","攻撃クリーチャー：")+GamePreferences.CardFaceName(source.name)+" ("+source.id+")]";
        }
        if(IsPublicRuleHelper(source)) {
            // Keep the native damage event separate from the exact rules paragraph.
            // It distinguishes multiple triggers that share the same helper card.
            var damage=System.Text.RegularExpressions.Regex.Match(paragraph,@"\s+(\[(?:Damage Source|Damaged|Phase|Player|Attacker|Blocker): [^\r\n]*\])$");
            string rule=damage.Success?paragraph.Substring(0,damage.Index):paragraph;
            var remembered=System.Text.RegularExpressions.Regex.Match(rule,@"\s+(\(\[[^\r\n]*\]\))$");
            if(remembered.Success)rule=rule.Substring(0,remembered.Index);
            paragraph=MarkerRules(new ForgeCardView{effectMarker=true,markerKind="effect",helperArtName=source.helperArtName,currentRules=rule});
            if(remembered.Success)paragraph+="\n"+remembered.Groups[1].Value;
            if(damage.Success)paragraph+="\n"+damage.Groups[1].Value
                .Replace("Damage Source: ",TapPaymentText("伤害来源：","Damage Source: ","ダメージ源："))
                .Replace("Damaged: ",TapPaymentText("受伤害者：","Damaged: ","ダメージを受けた対象："))
                .Replace("Amount: ",TapPaymentText("数量：","Amount: ","点数："))
                .Replace("Phase: ",TapPaymentText("阶段牌手：","Phase: ","フェイズのプレイヤー："))
                .Replace("Player: ",TapPaymentText("牌手：","Player: ","プレイヤー："))
                .Replace("Attacker: ",TapPaymentText("攻击者：","Attacker: ","攻撃クリーチャー："))
                .Replace("Blocker: ",TapPaymentText("阻挡者：","Blocker: ","ブロック・クリーチャー："));
        }
        var prevention=System.Text.RegularExpressions.Regex.Match(paragraph,@"^Prevent the next (\d+) damage\.\s*\nShields remain: (\d+)$");
        if(prevention.Success)paragraph=TapPaymentText("防止接下来将造成的","Prevent the next ","次の")+prevention.Groups[1].Value+TapPaymentText("点伤害。\n剩余预防量："," damage.\nShields remain: ","点のダメージを軽減する。\n残りの軽減量：")+prevention.Groups[2].Value;
        // Cast permissions/cost summaries are not part of the printed rule.
        // Keep them visible without making an existing translation miss.
        var castSuffix=System.Text.RegularExpressions.Regex.Match(paragraph,@"\s+\(without paying its mana cost\)(?<sacrifice>\s+Sacrifice permanents\.)?\s*$");
        string castContext="";
        if(castSuffix.Success) {
            paragraph=paragraph.Substring(0,castSuffix.Index);
            castContext="\n"+TapPaymentText("无需支付法术力费用。","Without paying its mana cost.","マナ・コストを支払わずに。")
                +(castSuffix.Groups["sacrifice"].Success?"\n"+TapPaymentText("牺牲永久物。","Sacrifice permanents.","パーマネントを生け贄に捧げる。"):"");
        }
        paragraph=NativeCostEffectText(PrintedRuleText(source,paragraph));
        paragraph=AbilityRecipeText(source,effect,paragraph);
        paragraph+=castContext;
        var libraryLook=System.Text.RegularExpressions.Regex.Match(paragraph,@"^Player (\d+) looks at the top card of their library\. (.+)$");
        if(libraryLook.Success)paragraph=PlayerDisplayName(int.Parse(libraryLook.Groups[1].Value))
            +TapPaymentText("检视自己的牌库顶牌。"," looks at the top card of their library.","は自分のライブラリーの一番上のカードを見る。")
            +"\n"+GeneratedPublicRuleText(libraryLook.Groups[2].Value);
        paragraph=string.Join("\n",paragraph.Replace("\\n","\n").Split('\n').Select(line=>GeneratedPublicRuleText(GeneratedSearchRuleText(line))));
        paragraph=System.Text.RegularExpressions.Regex.Replace(paragraph,@"Player (\d+) looks at the top card of Player (\d+)'s library\.",m=>TapPaymentText(
            PlayerDisplayName(int.Parse(m.Groups[1].Value))+"检视"+PlayerDisplayName(int.Parse(m.Groups[2].Value))+"的牌库顶牌。",m.Value,
            PlayerDisplayName(int.Parse(m.Groups[1].Value))+"は"+PlayerDisplayName(int.Parse(m.Groups[2].Value))+"のライブラリーの一番上のカードを見る。"));
        foreach(string generated in new[]{"Draw a card at the beginning of the next turn's upkeep.","Draw a card at the beginning of the next upkeep."})
            paragraph=paragraph.Replace(generated,GeneratedPublicRuleText(generated));
        if(printedPhase)paragraph+="\n"+TapPaymentText("阶段牌手：","Phase: ","フェイズのプレイヤー：")+PlayerDisplayName(int.Parse(phase.Groups[1].Value));
        paragraph+=eventContext;
        paragraph+=specializedContext;
        paragraph+=addedContext;
        paragraph+=attackerContext;
        paragraph+=producedContext;
        if(!targets.Success)return paragraph;
        string names=System.Text.RegularExpressions.Regex.Replace(targets.Groups[1].Value,@"([^\[\],]+?) \((\d+)\)",m=>GamePreferences.CardFaceName(m.Groups[1].Value.Trim())+" ("+m.Groups[2].Value+")");
        // Card.toString also emits "name - type stats" without an object ID.
        // Match only public objects present in this seat's snapshot.
        var publicTargets=(view?.players??System.Array.Empty<ForgePlayerView>()).SelectMany(p=>(p.battlefield??System.Array.Empty<ForgeCardView>()).Concat(p.graveyardCards??System.Array.Empty<ForgeCardView>()).Concat(p.exileCards??System.Array.Empty<ForgeCardView>()))
            .Concat((view?.stackItems??System.Array.Empty<ForgeStackItem>()).Select(s=>s.source)).Where(c=>c!=null&&!c.faceDown&&!string.IsNullOrEmpty(c.name));
        foreach(var card in publicTargets.OrderByDescending(c=>c.name.Length))names=names.Replace(card.name+" - ",GamePreferences.CardFaceName(card.name)+" - ");
        names=System.Text.RegularExpressions.Regex.Replace(names,@"(?<= - )(?:Artifact|Creature|Enchantment|Planeswalker|Battle|Land|Instant|Sorcery)(?: (?:Artifact|Creature|Enchantment|Planeswalker|Battle|Land|Instant|Sorcery))*",m=>string.Join(" ",m.Value.Split(' ').Select(GamePreferences.T)));
        names=System.Text.RegularExpressions.Regex.Replace(names,@"Face-down card (?=\([A-Z]?[0-9]+\))",TapPaymentText("盖牌 ","Face-down card ","裏向きのカード "));
        names=System.Text.RegularExpressions.Regex.Replace(names,@"(?<=\[)Player (\d+)(?=\])",m=>TapPaymentText("玩家 ","Player ","プレイヤー ")+m.Groups[1].Value);
        return paragraph+"\n"+TapPaymentText("目标：","Targets: ","対象：")+names;
    }
    private static string RegenerationSourceName(ForgeCardView source)
    {
        if(source?.zone=="Command"&&!string.IsNullOrEmpty(source.effectSourceName)) {
            var regeneration=System.Text.RegularExpressions.Regex.Match(source.name??"","^"+System.Text.RegularExpressions.Regex.Escape(source.effectSourceName)+@" \(([0-9]+)\)'s Regeneration$");
            // The body identifies the shield. Keep the title short enough for
            // the fixed user font while preserving the actual producer ID.
            if(regeneration.Success)return GamePreferences.CardFaceName(source.effectSourceName)+" ("+regeneration.Groups[1].Value+")";
        }
        return null;
    }
    private static string AbilitySourceName(ForgeCardView source)
    {
        if(IsPublicRuleHelper(source))return MarkerName(source);
        string regeneration=RegenerationSourceName(source);
        if(regeneration!=null)return regeneration;
        if(source?.genericEffectName==true&&!string.IsNullOrEmpty(source.effectSourcePrintingKey)&&!string.IsNullOrEmpty(source.effectSourceName))
            return GamePreferences.CardFaceName(source.effectSourceName)+TapPaymentText("的效果","'s Effect","の効果");
        return GamePreferences.CardFaceName(source?.name)??TapPaymentText("异能","Ability","能力");
    }
    // Ability objects use their source illustration and their own effect text.
    // They are not a second copy of the permanent's full rules card.
    private void PresentAbilityFace(RectTransform parent, ForgeCardView source, string effect, string kind, bool visible)
    {
        var face=parent.Find("AbilityFace") as RectTransform;
        if(!visible){if(face!=null)face.gameObject.SetActive(false);return;}
        parent.GetComponent<Image>().sprite=null;parent.GetComponent<Image>().color=Color.clear;
        if(face==null) {
            // Place uses centre anchors. A top-pivot stack tile already offsets that
            // anchor; adding parent.rect.center here would apply the offset twice.
            face=Panel(parent,"AbilityFace",Vector2.zero,new Vector2(240,335),Ink);
            face.GetComponent<Image>().sprite=AbilityShape();
            face.GetComponent<Image>().raycastTarget=false;
            var name=Label(face,"Name","",new Vector2(0,146),new Vector2(220,32),18,Paper);
            name.raycastTarget=false;name.resizeTextForBestFit=true;name.resizeTextMinSize=12;name.resizeTextMaxSize=18;
            var art=Rect(face,"Art",new Vector2(0,69),new Vector2(218,118)).gameObject.AddComponent<RawImage>();art.raycastTarget=false;
            var text=Label(face,"Effect","",new Vector2(0,-65),new Vector2(218,138),18,Paper);
            text.alignment=TextAnchor.UpperLeft;text.supportRichText=false;text.raycastTarget=false;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=12;text.resizeTextMaxSize=18;
        }
        face.gameObject.SetActive(true);
        face.localScale=new Vector3(parent.rect.width/240f,parent.rect.height/335f,1);
        face.Find("Name").GetComponent<Text>().text=AbilitySourceName(source);
        var effectLabel=(face.Find("Effect")??face.Find("EffectClip/Effect")).GetComponent<Text>();
        string rendered=GamePreferences.T(AbilityEffectText(source,effect));
        bool changed=effectLabel.text!=rendered;
        effectLabel.text=rendered;
        {
            var name=face.Find("Name").GetComponent<Text>();var text=effectLabel;
            name.resizeTextForBestFit=false;text.resizeTextForBestFit=false;
            (name.GetComponent<PreferenceText>()??name.gameObject.AddComponent<PreferenceText>()).SetBaseSize(15);(text.GetComponent<PreferenceText>()??text.gameObject.AddComponent<PreferenceText>()).SetBaseSize(13);
            // Keep the chosen font size and make room for the complete source name.
            // The illustration gives up height instead of covering wrapped name lines.
            float nameHeight=Mathf.Max(32,Mathf.Ceil(name.preferredHeight)+4);
            name.rectTransform.sizeDelta=new Vector2(220,nameHeight);
            name.rectTransform.anchoredPosition=new Vector2(0,162-nameHeight*.5f);
            var artRect=(RectTransform)face.Find("Art");
            float artHeight=Mathf.Max(0,148-nameHeight);
            artRect.sizeDelta=new Vector2(218,artHeight);
            artRect.anchoredPosition=new Vector2(0,10+artHeight*.5f);
            var clip=face.Find("EffectClip") as RectTransform;
            if(clip==null){
                clip=Panel(face,"EffectClip",new Vector2(0,-65),new Vector2(218,138),Color.clear);
                clip.GetComponent<Image>().raycastTarget=true;clip.gameObject.AddComponent<RectMask2D>();
                var scroll=clip.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.viewport=clip;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=25;
                text.rectTransform.SetParent(clip,false);text.rectTransform.anchorMin=text.rectTransform.anchorMax=text.rectTransform.pivot=new Vector2(.5f,1);text.rectTransform.anchoredPosition=Vector2.zero;
                scroll.content=text.rectTransform;
            }
            text.rectTransform.sizeDelta=new Vector2(218,Mathf.Max(138,text.preferredHeight));
        }
        var symbolEffect=SymbolizeStackLabel(effectLabel);
        FitAbilityBodyText(effectLabel,symbolEffect,208,138,13,18);
        symbolEffect.overflowMode=TMPro.TextOverflowModes.Masking;
        var viewport=(RectTransform)face.Find("EffectClip");
        var effectScroll=viewport.GetComponent<ScrollRect>();
        var track=face.Find("EffectScrollbar") as RectTransform;
        if(track==null) {
            track=Panel(face,"EffectScrollbar",new Vector2(107,-65),new Vector2(4,130),new Color(.18f,.25f,.29f));
            var handle=Panel(track,"Handle",Vector2.zero,new Vector2(4,32),new Color(.6f,.74f,.79f));
            handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=handle.offsetMax=Vector2.zero;
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=handle.GetComponent<Image>();bar.direction=Scrollbar.Direction.BottomToTop;
            effectScroll.verticalScrollbar=bar;
            effectScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        }
        symbolEffect.ForceMeshUpdate();
        float height=Mathf.Max(138,Mathf.Ceil(symbolEffect.GetPreferredValues(symbolEffect.text,208,10000).y)+8);
        effectLabel.rectTransform.sizeDelta=new Vector2(208,height);
        effectLabel.rectTransform.anchoredPosition=new Vector2(-4,effectLabel.rectTransform.anchoredPosition.y);
        track.gameObject.SetActive(height>138);
        if(changed){effectScroll.StopMovement();effectLabel.rectTransform.anchoredPosition=new Vector2(-4,0);}
        Sprite sprite=null;
        TryBattlefieldArt(source, out sprite);
        var image=face.Find("Art").GetComponent<RawImage>();image.texture=sprite?.texture;
        image.color=sprite==null?new Color(.12f,.19f,.24f):Color.white;
        CardArtCrop.Illustration(image,sprite);
    }
    // Grow short rules modestly, retaining a readable floor for long rules.
    // Measure against the viewport, not the expanded scrollable content.
    private static void FitAbilityBodyText(Text label,TMPro.TMP_Text symbols,float width,float height,int minimum,int maximum)
    {
        int size=minimum;
        for(int candidate=maximum;candidate>=minimum;candidate--) {
            symbols.fontSize=candidate*GamePreferences.TextScale;
            if(symbols.GetPreferredValues(symbols.text,width,10000).y+8<=height){size=candidate;break;}
        }
        PreferenceText.SetSize(label,size);PreferenceText.SetSize(symbols,size);
        symbols.enableAutoSizing=false;
    }
}
