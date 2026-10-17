using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform landChoiceMenu, choiceDismiss;
    private int clickedLandId, choiceSeat;
    private long choiceScrollContext;
    private float choiceScrollOffset;
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

    public static string ChoiceManaSymbols(string label,ForgeRequest request)
    {
        // The native convoke color dialog uses COLORLESS for a generic payment,
        // not for producing {C}. Keep ordinary production/allocation unchanged.
        bool convoke=request?.kind=="dialog" && Regex.IsMatch(request.message??"",
            @"^one: Convoke .+ \(\d+\)  for which color\?$");
        if(convoke && string.Equals(label?.Trim(),"COLORLESS",StringComparison.OrdinalIgnoreCase))return "{1}";
        // Legacy activation dialogs include the shared tap cost. Accept only
        // this exact cost and a pure output; extra costs/effects retain their text.
        if(request?.kind=="dialog"&&request.message=="getAbilityToPlay") {
            var tap=Regex.Match(label??"",@"^\s*\{T\}\s*:\s*(Add\s+.+)$",RegexOptions.IgnoreCase);
            if(tap.Success)return ChoiceManaSymbols(tap.Groups[1].Value);
        }
        return ChoiceManaSymbols(label);
    }
    private static bool IsManaProductionChoice(ForgeRequest request)=>request!=null
        && (request.message??"").EndsWith("Select Mana to Produce",StringComparison.Ordinal)
        && (request.options?.Length??0)>0 && request.options.All(o=>ChoiceManaSymbols(o.label,request)!=null);

    private ForgeCardView AbilityChoiceSource()
    {
        if(view.request.sourceCard!=null && !string.IsNullOrEmpty(view.request.sourceCard.name))return view.request.sourceCard;
        int id=view.request.sourceCardId>0?view.request.sourceCardId:choiceSeat==seat?clickedLandId:0;
        return (view.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>(p.hand??Array.Empty<ForgeCardView>())
            .Concat(p.battlefield??Array.Empty<ForgeCardView>()))
            .Concat((view.stackItems??Array.Empty<ForgeStackItem>()).Select(s=>s.source))
            .FirstOrDefault(c=>c!=null&&c.id==id);
    }

    private int GroupAssignmentSeat(ForgeCardView source)
    {
        var request=view?.request;
        if(request?.kind!="dialog"||source==null||!(source.originalRules??"").StartsWith("For each player, choose friend or foe.",StringComparison.Ordinal)
            ||request.options.Length!=2||request.options[0].id!="choice:0"||request.options[1].id!="choice:1")return 0;
        var match=Regex.Match(request.message??"",@"^one: Choose ability for Player (\d+)$");
        return match.Success&&int.TryParse(match.Groups[1].Value,out int player)&&view.players.Any(p=>p.seat==player)?player:0;
    }

    private string GroupChoiceText(ForgeCardView source,ForgeOption option)
    {
        // These are the exact two native child descriptions, not the entire
        // printed paragraph. Unknown child text keeps its existing display path.
        if(source?.name=="Pir's Whim") {
            if(option.id=="choice:0"&&option.label=="Friend searches their library for a land card, puts it onto the battlefield tapped, then shuffles their library.")
                return TapPaymentText("每位友方各从其牌库中搜寻一张地牌，将之横置放进战场，然后洗牌。",option.label,"各味方はそれぞれ、自分のライブラリーから土地カード１枚を探し、それをタップ状態で戦場に出し、その後ライブラリーを切り直す。");
            if(option.id=="choice:1"&&option.label=="Foe sacrifices an artifact or enchantment they control.")
                return TapPaymentText("每位敌方各牺牲一个依其选择的神器或结界。",option.label,"各敵はそれぞれ、自分がコントロールしているアーティファクト１つかエンチャント１つを生け贄に捧げる。");
        }
        return AbilityEffectText(source,option.label);
    }

    private bool PrepareLandChoices()
    {
        long context=view?.request?.contextId>0?view.request.contextId:view?.request?.requestId??0;
        var previousScroll=landChoiceMenu!=null?landChoiceMenu.GetComponentInChildren<BattlefieldHorizontalScroll>(true):null;
        choiceScrollOffset=context==choiceScrollContext&&previousScroll!=null?-previousScroll.content.anchoredPosition.x:0;
        choiceScrollContext=context;
        if(landChoiceMenu!=null){landChoiceMenu.gameObject.SetActive(false);Clear(landChoiceMenu);}
        if(choiceDismiss!=null)choiceDismiss.gameObject.SetActive(false);
        if(IsCostOrderRequest) { PresentCostOrderChoices(); return true; }
        bool playerVote=view?.awaitingInput==true && view.request.votingPlayerSeat>0
            && view.request.options.Any(o=>o.id.StartsWith("player:",StringComparison.Ordinal));
        if(view?.awaitingInput!=true || view.request.selectionPurpose=="optionalReplacement" || view.request.selectionPurpose=="costConfirmation" || (!playerVote&&view.request.kind!="dialog"&&view.request.kind!="ChooseNumber"&&view.request.kind!="ChooseModes"&&view.request.kind!="ChooseOptions"&&view.request.kind!="InputConfirm") || IsCardPickerRequest)return false;
        bool manaProduction=IsManaProductionChoice(view.request);
        bool valueChoice=!manaProduction&&(view.request.kind=="InputConfirm"||RandomChoiceTitle()!=null);
        var options=view.request.options.Where(o=>playerVote?o.id.StartsWith("player:",StringComparison.Ordinal):
            (view.request.selectionPurpose!="costScalar" || o.id!="ok"&&o.id!="cancel") && (manaProduction||valueChoice||o.id!="cancel" && o.id!="ok" && !RepresentedOnBoard(o.id))).ToArray();
        if(options.Length==0)return false;
        bool sectorChoice=IsSectorChoice(view.request);
        var source=sectorChoice?SectorChoiceSource(view.request):AbilityChoiceSource();
        Vector2 anchor=clickedChoiceAnchor;
        var sourceVisual=arenaCards.Values.FirstOrDefault(c=>c.root.name=="Card_"+source?.id);
        if(sourceVisual!=null) {
            var corners=new Vector3[4];sourceVisual.face.GetWorldCorners(corners);
            anchor=new Vector2(surface.InverseTransformPoint(sourceVisual.root.position).x,corners.Max(c=>surface.InverseTransformPoint(c).y));
        }
        bool mana=options.All(o=>ChoiceManaSymbols(o.label,view.request)!=null);
        bool confirmation=options.All(o=>o.label=="Yes" || o.label=="No" || o.label=="是" || o.label=="否");
        bool cards=!mana && !confirmation && !sectorChoice;
        if(choiceDismiss==null) {
            choiceDismiss=Panel(surface,"DismissAbilityChoices",Vector2.zero,Vector2.zero,Color.clear);
            choiceDismiss.anchorMin=Vector2.zero;choiceDismiss.anchorMax=Vector2.one;
            choiceDismiss.offsetMin=choiceDismiss.offsetMax=Vector2.zero;
            choiceDismiss.gameObject.AddComponent<ForgeChoiceDismiss>().ListenForRightClick=true;
        }
        long captured=requestId;
        choiceDismiss.GetComponent<Image>().color=new Color(.015f,.025f,.04f,.32f);
        choiceDismiss.GetComponent<ForgeChoiceDismiss>().Dismiss=()=>{if(!manaProduction&&!valueChoice&&canAct && !submitted && OptionIndex("cancel")>=0)Choose(OptionIndex("cancel"),captured);};
        choiceDismiss.gameObject.SetActive(true);choiceDismiss.SetAsLastSibling();
        if(landChoiceMenu==null)landChoiceMenu=Panel(surface,"CardAbilityChoices",Vector2.zero,Vector2.zero,Color.clear);
        landChoiceMenu.SetParent(surface,false);
        SetOverlayLayer(landChoiceMenu,DecisionLayerOffset,true);
        SetOverlayLayer(choiceDismiss,DecisionLayerOffset-1,true);
        var menuDismiss=landChoiceMenu.GetComponent<ForgeChoiceDismiss>();
        if(menuDismiss==null)menuDismiss=landChoiceMenu.gameObject.AddComponent<ForgeChoiceDismiss>();
        menuDismiss.ListenForRightClick=false;
        menuDismiss.Dismiss=choiceDismiss.GetComponent<ForgeChoiceDismiss>().Dismiss;
        landChoiceMenu.anchorMin=landChoiceMenu.anchorMax=landChoiceMenu.pivot=new Vector2(.5f,.5f);
        landChoiceMenu.GetComponent<Image>().color=Ink;
        landChoiceMenu.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(500,240,16);
        if(playerVote){PresentGenericChoices(captured);return true;}
        if(view.request.selectionPurpose=="manaAbility" && (!mana||options.Any(o=>Regex.Matches(ChoiceManaSymbols(o.label,view.request)??"",@"\{[^{}]+\}").Count>1))) {
            PresentManaAbilityChoices(options,source,anchor,captured);return true;
        }
        if(view.request.paymentMode=="manual" && (view.request.kind=="ChooseNumber"||options.All(o=>int.TryParse(o.label,out _)))) {
            PresentNumericChoices(options,source,anchor,captured);return true;
        }
        if(valueChoice){PresentValueChoices(options,captured);return true;}
        if(view.request.message=="one: Choose a creature type" || view.request.message=="oneOrNone: Choose a creature type") {PresentCreatureTypeChoices(captured);return true;}
        if(view.request.selectionPurpose=="cardName") {PresentCardNameChoices(captured);return true;}
        if(view.request.selectionPurpose=="dungeonRoom") {PresentDungeonRoomChoices(source,captured);return true;}
        if(view.request.selectionPurpose=="roomDoor") {PresentRoomDoorChoices(options,captured);return true;}
        if(view.request.kind=="ChooseOptions") {
            if(view.request.selectionPurpose=="optionalCosts")PresentModeCards(source,captured,false);
            else PresentGenericChoices(captured);return true;
        }
        if(view.request.kind=="ChooseModes") {PresentSpreeChoices(source,anchor,captured);return true;}
        if(view.request.selectionPurpose=="castingCosts") {PresentModeCards(source,captured,false,true);return true;}
        bool numeric=options.Length>0&&options.All(o=>int.TryParse(o.label,out _));
        if(numeric || view.request.kind=="ChooseNumber") {
            PresentNumericChoices(options,source,anchor,captured);return true;
        }
        int columns=Math.Min(options.Length,cards?3:6);
        bool hasFooter=!manaProduction&&view.request.options.Any(o=>o.id=="ok");
        float width=Mathf.Min(surface.rect.width-64,sectorChoice?420:cards?columns*232+24:mana?columns*44+24:228);
        float height=cards?Mathf.Min(432,surface.rect.height-80):(mana?96:104)+(hasFooter?32:0);
        landChoiceMenu.sizeDelta=new Vector2(width,height);
        landChoiceMenu.anchoredPosition=cards||sectorChoice?Vector2.zero:new Vector2(
            Mathf.Clamp(anchor.x,surface.rect.xMin+width/2+20,surface.rect.xMax-width/2-20),
            Mathf.Clamp(anchor.y+height/2+18,surface.rect.yMin+height/2+20,surface.rect.yMax-height/2-20));
        bool assigningSector=sectorChoice&&view.request.message!="one: Choose a sector";
        int groupPlayer=GroupAssignmentSeat(source);
        string sectorTitle=assigningSector?TapPaymentText("分配分区","Assign a sector","セクターを割り当て"):TapPaymentText("选择分区","Choose a sector","セクターを選択");
        string groupTitle=groupPlayer>0?TapPaymentText($"为{PlayerDisplayName(groupPlayer)}选择友方或敌方",$"Choose friend or foe for {PlayerDisplayName(groupPlayer)}",$"{PlayerDisplayName(groupPlayer)}を味方か敵かに選ぶ"):null;
        bool tapMana=mana&&view.request.message=="getAbilityToPlay"&&options.All(o=>Regex.IsMatch(o.label??"",@"^\s*\{T\}\s*:"));
        string manaTitle=tapMana?TapPaymentText("横置产费","Tap for mana","タップしてマナを出す"):TapPaymentText("选择法术力","Choose mana","マナを選択");
        var title=Label(landChoiceMenu,"Title",groupTitle??(sectorChoice?sectorTitle:mana?manaTitle:confirmation?"是否执行？":TapPaymentText("选择异能或施放方式","Choose ability or spell","能力や唱え方を選択")),new Vector2(0,height/2-(cards?20:16)),new Vector2(width,26),cards?21:17,Color.white);
        title.raycastTarget=false;
        string name=source?.name??"";
        if(source!=null && (!sectorChoice||source.name==source.originalName) && catalog.TryGetValue(source.printingKey??"",out var definition))name=GamePreferences.CardName(definition);
        if(sectorChoice)name=source!=null?$"{name} ({source.id})":assigningSector?Regex.Match(view.request.message,@"^one: Assign (.+) to a sector$").Groups[1].Value:"";
        var subtitle=Label(landChoiceMenu,"Source",name,new Vector2(0,height/2-(cards?48:36)),new Vector2(width-20,20),cards||sectorChoice?15:13,new Color(.7f,.78f,.84f));subtitle.raycastTarget=false;
        float groupHeader=0;
        if(groupPlayer>0){
            title.supportRichText=subtitle.supportRichText=false;PreferenceText.SetSize(title,17);PreferenceText.SetSize(subtitle,13);title.resizeTextForBestFit=subtitle.resizeTextForBestFit=false;
            float titleHeight=Mathf.Max(26,title.preferredHeight+4),sourceHeight=Mathf.Max(20,subtitle.preferredHeight+4);
            title.rectTransform.sizeDelta=new Vector2(width,titleHeight);title.rectTransform.anchoredPosition=new Vector2(0,height/2-6-titleHeight/2);
            subtitle.rectTransform.sizeDelta=new Vector2(width-20,sourceHeight);subtitle.rectTransform.anchoredPosition=new Vector2(0,height/2-10-titleHeight-sourceHeight/2);
            groupHeader=titleHeight+sourceHeight+22;
        }
        if(sectorChoice){title.resizeTextForBestFit=subtitle.resizeTextForBestFit=false;subtitle.supportRichText=false;PreferenceText.SetSize(title,17);PreferenceText.SetSize(subtitle,15);}
        float sectorItemHeight=Mathf.Max(36,24*GamePreferences.TextScale+12);
        if(mana||sectorChoice) {
            // Locale fonts and the accessibility scale can make even a short
            // heading taller than one fixed row. Reserve its measured height
            // before the first frame, keeping the mana buttons below it.
            float titleHeight=Mathf.Max(26,title.preferredHeight+4);
            float sourceHeight=string.IsNullOrEmpty(name)?0:Mathf.Max(20,subtitle.preferredHeight+4);
            float headerHeight=titleHeight+12+(sourceHeight>0?sourceHeight+4:0);
            height=headerHeight+(sectorChoice?sectorItemHeight+10:46)+12+(hasFooter?32:0);
            landChoiceMenu.sizeDelta=new Vector2(width,height);
            var menuPosition=landChoiceMenu.anchoredPosition;
            menuPosition.y=sectorChoice?0:Mathf.Clamp(anchor.y+height/2+18,surface.rect.yMin+height/2+20,surface.rect.yMax-height/2-20);
            landChoiceMenu.anchoredPosition=menuPosition;
            title.rectTransform.sizeDelta=new Vector2(width,titleHeight);
            title.rectTransform.anchoredPosition=new Vector2(0,height/2-6-titleHeight/2);
            subtitle.gameObject.SetActive(sourceHeight>0);
            subtitle.rectTransform.sizeDelta=new Vector2(width-20,sourceHeight);
            subtitle.rectTransform.anchoredPosition=new Vector2(0,height/2-10-titleHeight-sourceHeight/2);
        }
        float viewportHeight=sectorChoice?sectorItemHeight+10:cards?height-(hasFooter?128:88):46,cellWidth=(width-24)/columns;
        float choicesY=mana||sectorChoice?-height/2+12+(hasFooter?32:0)+viewportHeight/2:cards?(hasFooter?-12:-32):(hasFooter?-4:-22);
        if(groupPlayer>0){viewportHeight=height-groupHeader-16-(hasFooter?32:0);choicesY=-height/2+12+(hasFooter?32:0)+viewportHeight/2;}
        var clip=Panel(landChoiceMenu,"Choices",new Vector2(0,choicesY),new Vector2(width-24,viewportHeight),Color.clear);
        clip.gameObject.AddComponent<RectMask2D>();var scroll=clip.gameObject.AddComponent<BattlefieldHorizontalScroll>();scroll.hidePartialChildren=true;
        var content=Rect(clip,"Content",Vector2.zero,new Vector2(width,viewportHeight));scroll.viewport=clip;scroll.content=content;scroll.Configure(cellWidth);
        float itemHeight=sectorChoice?sectorItemHeight:cards?viewportHeight-12:mana?36:32;

        content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);
        content.sizeDelta=new Vector2(Mathf.Max(width-24,options.Length*cellWidth),viewportHeight);
        for(int i=0;i<options.Length;i++) {
            var option=options[i];int index=OptionIndex(option.id);
            var size=new Vector2(cards?cellWidth-16:mana?36:cellWidth-10,itemHeight);
            var tile=Panel(content,"Option_"+index,Vector2.zero,size,option.selected?Gold:new Color(.25f,.48f,.57f));
            tile.anchorMin=tile.anchorMax=tile.pivot=new Vector2(0,1);
            tile.anchoredPosition=new Vector2(i*cellWidth+(cellWidth-size.x)*.5f,-4);
            tile.GetComponent<Image>().sprite=cards?AbilityShape():mana?IntegratedUiUtility.Circle(96):IntegratedUiUtility.RoundedRect(112,36,7);
            var button=tile.gameObject.AddComponent<Button>();button.targetGraphic=tile.GetComponent<Image>();
            button.onClick.AddListener(()=>Choose(index,captured));button.interactable=canAct&&!submitted&&!option.disabled;
            tile.gameObject.AddComponent<CanvasGroup>().alpha=button.interactable?1f:.45f;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.3f,1.8f,2);colors.pressedColor=Gold;colors.disabledColor=new Color(.35f,.35f,.35f);button.colors=colors;
            var inner=Panel(tile,"Face",Vector2.zero,size-Vector2.one*5,new Color(.055f,.085f,.11f));
            inner.GetComponent<Image>().sprite=tile.GetComponent<Image>().sprite;inner.GetComponent<Image>().raycastTarget=false;
            if(mana) {
                var symbol=CardSymbolText.Create(inner,"Mana",26,Color.white);IntegratedUiUtility.Place(symbol.rectTransform,Vector2.zero,new Vector2(size.x-6,size.y-6));
                symbol.alignment=TMPro.TextAlignmentOptions.Center;symbol.text=CardSymbolText.Format(ChoiceManaSymbols(option.label,view.request));
            }
            else if(cards)DrawAbilityChoice(inner,source,option,size-Vector2.one*5);
            else {var text=Label(inner,"Answer",RequestOptionLabel(option),Vector2.zero,size-Vector2.one*4,19,Paper);text.raycastTarget=false;if(sectorChoice){PreferenceText.SetSize(text,19);text.resizeTextForBestFit=false;}}
        }
        content.anchoredPosition=new Vector2(-choiceScrollOffset,0);scroll.Configure(cellWidth);
        // Dismissible choices use outside-click/right-click; keep only an actual engine confirmation.
        var footer=view.request.options.Where(o=>!manaProduction&&o.id=="ok"&&!HasTargetChoice).ToArray();
        for(int i=0;i<footer.Length;i++) {
            int action=OptionIndex(footer[i].id);
            var close=Button(landChoiceMenu,footer[i].id=="cancel"?"Cancel":"Confirm",footer[i].id=="cancel"?"取消":"确认",new Vector2((i-(footer.Length-1)*.5f)*(cards?182:120),-height/2+(cards?24:19)),new Vector2(cards?166:106,cards?42:28),()=>Choose(action,captured),new Color(.14f,.2f,.26f));
            close.interactable=canAct&&!submitted;close.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(166,42,10);
        }
        landChoiceMenu.gameObject.SetActive(true);SetOverlayLayer(landChoiceMenu,DecisionLayerOffset,true);landChoiceMenu.SetAsLastSibling();return true;
    }
    private void PresentManaAbilityChoices(ForgeOption[] options,ForgeCardView source,Vector2 anchor,long captured)
    {
        // A text row preserves the complete native cost/effect of pain, filter,
        // restricted or variable mana abilities without repeating large card art.
        float width=Mathf.Min(400,surface.rect.width-48);
        var title=Label(landChoiceMenu,"Title",TapPaymentText("产法术力","Produce mana","マナを出す"),Vector2.zero,new Vector2(width-24,32),17,Paper);
        title.raycastTarget=false;title.resizeTextForBestFit=false;PreferenceText.SetSize(title,17);
        var name=Label(landChoiceMenu,"Source",GamePreferences.CardFaceName(source?.name??""),Vector2.zero,new Vector2(width-24,26),13,new Color(.7f,.78f,.84f));
        name.raycastTarget=false;name.resizeTextForBestFit=false;PreferenceText.SetSize(name,13);
        float titleHeight=Mathf.Max(28,title.preferredHeight+4),nameHeight=string.IsNullOrEmpty(name.text)?0:Mathf.Max(22,name.preferredHeight+4);
        float header=titleHeight+nameHeight+16;
        var content=Scroll(landChoiceMenu,"ManaAbilities",Vector2.zero,new Vector2(width-24,100));
        float total=0;
        foreach(var option in options) {
            int index=OptionIndex(option.id);
            var row=Panel(content,"ManaAbility_"+index,Vector2.zero,new Vector2(width-24,44),new Color(.12f,.23f,.3f));
            row.anchorMin=row.anchorMax=row.pivot=new Vector2(.5f,1);
            row.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(370,50,7);
            var button=row.gameObject.AddComponent<Button>();button.targetGraphic=row.GetComponent<Image>();
            button.onClick.AddListener(()=>Choose(index,captured));button.interactable=canAct&&!submitted&&!option.disabled;
            row.gameObject.AddComponent<CanvasGroup>().alpha=button.interactable?1:.45f;
            var body=CardSymbolText.Create(row,"Description",16,Paper);body.enableAutoSizing=false;body.alignment=TMPro.TextAlignmentOptions.MidlineLeft;
            var origin=!string.IsNullOrEmpty(option.rulesSource?.name)?option.rulesSource:source;
            string text=PrintedRuleText(origin,(option.label??"").Trim());
            body.text=CardSymbolText.Format(text);
            float rowHeight=Mathf.Max(44,Mathf.Ceil(body.GetPreferredValues(body.text,width-48,10000).y)+20);
            row.sizeDelta=new Vector2(width-24,rowHeight);row.anchoredPosition=new Vector2(0,-total);
            IntegratedUiUtility.Place(body.rectTransform,Vector2.zero,new Vector2(width-48,rowHeight-12));
            total+=rowHeight+6;
        }
        total=Mathf.Max(0,total-6);
        float height=Mathf.Min(Mathf.Min(320,surface.rect.height-64),header+total+16);
        landChoiceMenu.sizeDelta=new Vector2(width,height);landChoiceMenu.GetComponent<Image>().color=Ink;
        landChoiceMenu.anchoredPosition=new Vector2(Mathf.Clamp(anchor.x,surface.rect.xMin+width/2+20,surface.rect.xMax-width/2-20),
            Mathf.Clamp(anchor.y+height/2+18,surface.rect.yMin+height/2+20,surface.rect.yMax-height/2-20));
        title.rectTransform.sizeDelta=new Vector2(width-24,titleHeight);title.rectTransform.anchoredPosition=new Vector2(0,height/2-8-titleHeight/2);
        name.gameObject.SetActive(nameHeight>0);name.rectTransform.sizeDelta=new Vector2(width-24,nameHeight);name.rectTransform.anchoredPosition=new Vector2(0,height/2-8-titleHeight-nameHeight/2);
        float available=Mathf.Max(40,height-header-16);
        var viewport=(RectTransform)content.parent;viewport.sizeDelta=new Vector2(width-24,available);viewport.anchoredPosition=new Vector2(0,-height/2+8+available/2);
        content.sizeDelta=new Vector2(width-24,Mathf.Max(available,total));
        viewport.GetComponent<ScrollRect>().vertical=total>available+1;
        if(total>available+1)AddStackScrollIndicator(content);
        landChoiceMenu.gameObject.SetActive(true);SetOverlayLayer(landChoiceMenu,DecisionLayerOffset,true);landChoiceMenu.SetAsLastSibling();
    }
    private static bool IsSectorChoice(ForgeRequest request)
    {
        if(request?.kind!="dialog" || request.options?.Length!=3)return false;
        string[] sectors={"Alpha","Beta","Gamma"};
        for(int i=0;i<sectors.Length;i++)if(request.options[i].id!="choice:"+i || request.options[i].label!=sectors[i])return false;
        return request.message=="one: Choose a sector" || Regex.IsMatch(request.message??"",@"^one: Assign .+ \(\d+\) to a sector$");
    }

    private ForgeCardView SectorChoiceSource(ForgeRequest request)
    {
        var match=Regex.Match(request.message??"",@"^one: Assign .+ \((\d+)\) to a sector$");
        if(match.Success&&int.TryParse(match.Groups[1].Value,out int id))
            return (view.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>()).FirstOrDefault(c=>c.id==id);
        return view.stackItems?.FirstOrDefault()?.source;
    }

    private void PresentNumericChoices(ForgeOption[] options,ForgeCardView source,Vector2 anchor,long captured)
    {
        // Numeric decisions occupy exactly the same dock slot as advance,
        // payment and hand actions. They do not cover or dim the battlefield.
        landChoiceMenu.SetParent(Node("CornerCluster/ControlRow"),false);
        landChoiceMenu.anchorMin=landChoiceMenu.anchorMax=landChoiceMenu.pivot=new Vector2(.5f,.5f);
        landChoiceMenu.anchoredPosition=new Vector2(48,-20);landChoiceMenu.sizeDelta=new Vector2(184,48);
        landChoiceMenu.GetComponent<Image>().color=Color.clear;
        if(choiceDismiss!=null)choiceDismiss.gameObject.SetActive(false);
        var dismiss=landChoiceMenu.GetComponent<ForgeChoiceDismiss>();
        dismiss.ListenForRightClick=true;
        dismiss.Dismiss=()=>{if(UnityEngine.InputSystem.Mouse.current?.rightButton.wasPressedThisFrame==true && canAct&&!submitted&&OptionIndex("cancel")>=0)Choose(OptionIndex("cancel"),captured);};
        bool serverRange=view.request.kind=="ChooseNumber";
        var choices=serverRange?options:options.OrderBy(o=>int.Parse(o.label)).ToArray();int selected=0;
        var value=Label(landChoiceMenu,"NumericValue",serverRange?view.request.selectedValue.ToString():choices[0].label,new Vector2(-47.5f,0),new Vector2(35,40),18,Paper);value.fontStyle=FontStyle.Bold;value.raycastTarget=false;
        value.horizontalOverflow=HorizontalWrapMode.Overflow;value.resizeTextForBestFit=false;
        // Keep a fixed font and the right edge of the dock; grow left only when
        // the native number needs more space than the ordinary numeric slot.
        string widest=serverRange?value.text:choices.OrderByDescending(o=>o.label.Length).First().label;
        string shown=value.text;value.text=widest;
        float extra=Mathf.Max(0,value.preferredWidth+8-35);value.text=shown;
        landChoiceMenu.sizeDelta=new Vector2(184+extra,48);
        landChoiceMenu.anchoredPosition=new Vector2(48-extra/2,-20);
        value.rectTransform.sizeDelta=new Vector2(35+extra,40);
        bool costNumber=view.request.paymentMode=="manual";
        float left=-79-extra/2,right=-16+extra/2,commitX=47.5f+extra/2;
        if(costNumber) {
            landChoiceMenu.sizeDelta=new Vector2(279+extra,48);landChoiceMenu.anchoredPosition=new Vector2(.5f-extra/2,-20);
            value.rectTransform.anchoredPosition+=new Vector2(-47.5f,0);left-=47.5f;right-=47.5f;commitX=-47.5f+extra/2;
            int decline=PresentedCancelOption;
            if(decline>=0) {
                var cancel=DockAction(landChoiceMenu,"CancelValue",TapPaymentText("取消","Cancel","キャンセル"),new Vector2(47.5f+extra/2,0),89,()=>Choose(decline,captured),true);
                cancel.interactable=canAct&&!submitted;
            }
        }
        if(serverRange) {
            foreach(var item in new[]{("Decrease","−","number-minus",left),("Increase","+","number-plus",right)}) {
                int index=OptionIndex(item.Item3);
                var control=Button(landChoiceMenu,item.Item1,item.Item2,new Vector2(item.Item4,0),new Vector2(26,36),()=>Choose(index,captured),new Color(.14f,.2f,.26f));
                control.interactable=index>=0&&canAct&&!submitted;var label=control.GetComponentInChildren<Text>();PreferenceText.SetSize(label,16);
                label.rectTransform.sizeDelta=new Vector2(26,34);label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Truncate;
            }
            int confirmation=OptionIndex("confirm-number");
            var commit=DockAction(landChoiceMenu,"ConfirmValue",costNumber?TapPaymentText("下一步","Next","次へ"):TapPaymentText("确定","Confirm","確認"),new Vector2(commitX,0),89,()=>Choose(confirmation,captured),false);
            commit.interactable=canAct&&!submitted&&confirmation>=0&&!view.request.options[confirmation].disabled;landChoiceMenu.gameObject.SetActive(true);SetOverlayLayer(landChoiceMenu,DecisionLayerOffset,true);landChoiceMenu.SetAsLastSibling();return;
        }
        Button minus=null,plus=null;
        Action refresh=()=>{value.text=choices[selected].label;minus.interactable=canAct&&!submitted&&selected>0;plus.interactable=canAct&&!submitted&&selected+1<choices.Length;};
        minus=Button(landChoiceMenu,"Decrease","−",new Vector2(left,0),new Vector2(26,36),()=>{if(selected>0)selected--;refresh();},new Color(.14f,.2f,.26f));
        plus=Button(landChoiceMenu,"Increase","+",new Vector2(right,0),new Vector2(26,36),()=>{if(selected+1<choices.Length)selected++;refresh();},new Color(.14f,.2f,.26f));
        foreach(var control in new[]{minus,plus}){var label=control.GetComponentInChildren<Text>();PreferenceText.SetSize(label,16);label.rectTransform.sizeDelta=new Vector2(26,34);label.resizeTextForBestFit=false;}
        var confirm=DockAction(landChoiceMenu,"ConfirmValue",costNumber?TapPaymentText("下一步","Next","次へ"):TapPaymentText("确定","Confirm","確認"),new Vector2(commitX,0),89,()=>Choose(OptionIndex(choices[selected].id),captured),false);
        confirm.interactable=canAct&&!submitted&&!choices[selected].disabled;refresh();landChoiceMenu.gameObject.SetActive(true);SetOverlayLayer(landChoiceMenu,DecisionLayerOffset,true);landChoiceMenu.SetAsLastSibling();
    }

    private ForgeCardView AbilityChoiceFace(ForgeCardView source,ForgeOption option)
    {
        if(!string.IsNullOrEmpty(option.castingFace?.name))return option.castingFace;
        // Older snapshots did not carry LandAbility's actual face. Recover
        // only an exact public printed face; unknown action text stays intact.
        var land=Regex.Match(option.label??"",@"^Play land \((.+)\)$");
        if(!land.Success||source==null||source.faceDown||!catalog.TryGetValue(source.printingKey??"",out var definition))return null;
        var features=(definition.features??new System.Collections.Generic.List<CardFeatureDefinition>())
            .Where(f=>f!=null&&f.cardName==land.Groups[1].Value&&f.cardTypes.Has(global::CardType.Land)).ToArray();
        if(features.Length!=1)return null;
        var feature=features[0];var result=ServerSnapshotJson.Copy(source);
        result.name=result.originalName=feature.cardName;result.backFace=feature.faceIndex==1;
        result.stateName=result.backFace?"Backside":"Original";
        result.originalRules=result.currentRules=feature.description;
        result.originalType=result.currentType=feature.cardTypes.ToString();
        result.originalManaCost=result.manaCost=feature.manaCost;
        result.land=true;result.creature=result.planeswalker=result.battle=false;
        return result;
    }
    private void DrawAbilityChoice(RectTransform face,ForgeCardView source,ForgeOption option,Vector2 size)
    {
        string description=option.label??"";
        string title="发动能力",cost="";
        string keywordPattern=string.Join("|",DeckKeywordGlossary.All.Select(e=>e.English).Concat(new[]{"Basic landcycling","Plainscycling","Islandcycling","Swampcycling","Mountaincycling","Forestcycling","Cast","Play"}).Distinct().OrderByDescending(s=>s.Length).Select(Regex.Escape));
        var keyword=Regex.Match(description,"^("+keywordPattern+")\\b",RegexOptions.IgnoreCase);
        if(keyword.Success) {
            switch(keyword.Value.ToLowerInvariant()) {
                case "evoke":title="呼魂";break;case "cycling":title="循环";break;case "ninjutsu":title="忍术";break;case "equip":title="佩带";break;case "crew":title="搭载";break;
                case "flashback":title="返照";break;case "unearth":title="破坟";break;case "kicker":title="增幅";break;case "surge":title="怒涛";break;
                case "cast":case "play":title="施放";break;default:title=DeckKeywordGlossary.All.FirstOrDefault(e=>string.Equals(e.English,keyword.Value,StringComparison.OrdinalIgnoreCase))?.Name??"地循环";break;
            }
            description=description.Substring(keyword.Length).TrimStart(' ',':','—','-');
        }
        else if(source!=null && description.StartsWith(source.name,StringComparison.Ordinal))title="施放";
        var prefix=Regex.Match(description,keyword.Success?@"^(?<cost>(?:\{[^{}]+\}\s*)+)\s*[:—-]?\s*":@"^(?<cost>(?:\{[^{}]+\}\s*)+|[+−-]?\d+)\s*[:—-]\s*");
        if(prefix.Success){cost=prefix.Groups["cost"].Value;description=description.Substring(prefix.Length);}
        var castingFace=AbilityChoiceFace(source,option);
        bool casting=castingFace!=null;
        bool grouping=GroupAssignmentSeat(source)>0;
        if(grouping){title=option.id=="choice:0"?TapPaymentText("友方","Friend","味方"):TapPaymentText("敌方","Foe","敵");description=GroupChoiceText(source,option);}
        if(casting) {
            source=castingFace;description=PrintedRuleText(source,source.currentRules?.Replace("\\n","\n")??"");cost=source.manaCost;
            title=source.land?TapPaymentText("使用地","Play land","土地をプレイ"):option.faceKind=="prepare"?"备法":option.faceKind=="adventure"?"历险":option.faceKind=="omen"?"前兆":option.faceKind=="mdfc-front"?"正面":option.faceKind=="mdfc-back"?"背面":"施放";
        }
        else if(view.request.kind=="dialog" && view.request.message=="getAbilityToPlay" && source!=null && !source.faceDown) {
            // Native alternate permissions may have no castingFace metadata.
            // Keep their exact option identity; localize only the public display.
            // A rules sentence can begin with a glossary word (e.g. Counter).
            // Without a keyword cost prefix, it is still part of the effect:
            // stripping it breaks the exact printed-rule translation match.
            if(keyword.Success&&!prefix.Success)description=option.label??"";
            if(!keyword.Success||!prefix.Success)title=source.zone=="Battlefield"&&prefix.Success?TapPaymentText("激活异能","Activate ability","能力起動"):"施放";
            string permission="";
            description=Regex.Replace(description,@" by (.+?) \(([0-9]+)\) \(without paying its mana cost\)",m=> {
                var producer=(view.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>())
                    .FirstOrDefault(c=>c!=null&&!c.faceDown&&c.id.ToString()==m.Groups[2].Value);
                bool printedName=producer!=null&&catalog.TryGetValue(producer.printingKey??"",out var producerDefinition)
                    &&producerDefinition.cardName==m.Groups[1].Value;
                if(producer==null||!(producer.name==m.Groups[1].Value||printedName))return m.Value;
                permission=TapPaymentText("无需支付法术力费用（来源：","Without paying its mana cost (source: ","マナ・コストを支払わずに（発生源：")
                    +GamePreferences.CardFaceName(producer.name)+" ("+producer.id+")"+TapPaymentText("）", ")", "）");
                return "";
            });
            // The permission must be visible before the scrollable rules text.
            var summary=Regex.Match(description,"^"+Regex.Escape(source.name??"")+@" - (?:Artifact|Creature|Enchantment|Planeswalker|Battle|Land|Instant|Sorcery)(?: (?:Artifact|Creature|Enchantment|Planeswalker|Battle|Land|Instant|Sorcery))*(?: -?\d+\s*/\s*-?\d+)?$");
            if(summary.Success){description=PrintedRuleText(source,(source.currentRules??source.originalRules??"").Replace("\\n","\n"));cost=source.manaCost;}
            description=(permission.Length>0?permission+"\n":"")+AbilityRecipeText(source,option.label,AbilityEffectText(source,description));
        }
        float top=size.y/2;
        var heading=Label(face,"Mode",title,new Vector2(0,top-19),new Vector2(size.x-18,28),20,Color.white);heading.raycastTarget=false;
        if(!casting&&source?.zone=="Battlefield"&&prefix.Success)
            heading.rectTransform.sizeDelta=new Vector2(size.x-18,Mathf.Max(28,heading.preferredHeight+4));
        if(grouping){PreferenceText.SetSize(heading,17);heading.resizeTextForBestFit=false;heading.rectTransform.sizeDelta=new Vector2(size.x-18,Mathf.Max(28,heading.preferredHeight+4));}
        if(casting&&UsesComponentFace(source)) {
            var full=Rect(face,"CastingFace",new Vector2(0,-14),new Vector2(size.x-14,size.y-58));
            full.gameObject.AddComponent<Image>().raycastTarget=false;
            if(PresentComponentFace(full,source))return;
            full.gameObject.SetActive(false);Destroy(full.gameObject);
        }
        Sprite sprite=null;
        if(source?.copy==true || IsSpecializedFace(source))TryBattlefieldArt(source,out sprite);
        if(IsSpecializedFace(source)&&description==source.name)
            description=GamePreferences.FeatureRules(new CardFeatureDefinition {cardName=source.name,description=(source.currentRules??"").Replace("\\n","\n")});
        if(sprite==null && source!=null && !source.faceDown && catalog.TryGetValue(source.printingKey??"",out var definition)) {
            CardImageLibrary.TryLoadFullCard(definition,source.backFace?1:0,out sprite);
            if(description==source.name)description=GamePreferences.CardRules(definition);
        }
        if(sprite==null)TryBattlefieldArt(source,out sprite);
        if(casting && (option.faceKind?.StartsWith("mdfc-")==true || source.backFace)) {
            var full=Rect(face,"CastingFace",new Vector2(0,-10),new Vector2(size.x-14,size.y-64)).gameObject.AddComponent<Image>();
            full.sprite=sprite;full.preserveAspect=true;full.color=sprite==null?Ink:Color.white;full.raycastTarget=false;
            if(sprite==null)Label(face,"MissingFace",GamePreferences.CardFaceName(source.name)+"\n"+VisibleCardType(source)+"\n"+description,Vector2.zero,size-Vector2.one*18,16,Paper);
            var name=Label(face,"FaceName",GamePreferences.CardFaceName(source.name),new Vector2(0,-size.y*.5f+12),new Vector2(size.x-12,24),13,Paper);
            name.resizeTextForBestFit=true;name.resizeTextMinSize=10;name.resizeTextMaxSize=13;return;
        }
        float artHeight=size.y*.27f;
        var art=Rect(face,"Art",new Vector2(0,top-38-artHeight/2),new Vector2(size.x-16,artHeight)).gameObject.AddComponent<RawImage>();
        art.texture=sprite?.texture;art.color=sprite==null?new Color(.12f,.18f,.22f):Color.white;art.raycastTarget=false;
        CardArtCrop.Illustration(art,sprite);
        if(casting) {
            var strip=Panel(face,"FaceNameStrip",new Vector2(0,top-38-artHeight+10),new Vector2(size.x-16,24),new Color(.03f,.04f,.05f,.92f));strip.GetComponent<Image>().raycastTarget=false;
            var name=Label(strip,"FaceName",GamePreferences.CardFaceName(source.name),Vector2.zero,new Vector2(size.x-24,22),14,Paper);name.resizeTextForBestFit=true;name.resizeTextMinSize=10;name.resizeTextMaxSize=14;
        }
        float effectHeight=size.y-artHeight-94;
        var effect=Panel(face,"EffectPanel",new Vector2(0,top-44-artHeight-effectHeight/2),new Vector2(size.x-16,effectHeight),new Color(.86f,.88f,.84f));effect.GetComponent<Image>().raycastTarget=false;
        float typeHeight=casting?20:0;
        if(casting){var type=Label(effect,"Type",VisibleCardType(source),new Vector2(0,effectHeight*.5f-12),new Vector2(size.x-30,20),11,new Color(.16f,.2f,.21f));type.resizeTextForBestFit=true;type.resizeTextMinSize=9;type.resizeTextMaxSize=11;}
        var effectContent=Scroll(effect,"TextScroll",new Vector2(0,-typeHeight*.5f),new Vector2(size.x-28,effectHeight-12-typeHeight));
        var body=Label(effectContent,"Effect",PrintedRuleText(source,description),Vector2.zero,new Vector2(size.x-38,effectHeight-14-typeHeight),casting?12:15,new Color(.055f,.075f,.085f));
        body.alignment=TextAnchor.UpperLeft;SetNamedPlayerText(body,PrintedRuleText(source,description));body.raycastTarget=false;
        if(grouping){PreferenceText.SetSize(body,15);body.resizeTextForBestFit=false;}
        float textHeight=Mathf.Max(effectHeight-14-typeHeight,body.preferredHeight+4);
        body.rectTransform.anchorMin=body.rectTransform.anchorMax=body.rectTransform.pivot=new Vector2(.5f,1);
        body.rectTransform.anchoredPosition=Vector2.zero;body.rectTransform.sizeDelta=new Vector2(size.x-38,textHeight);
        effectContent.sizeDelta=new Vector2(size.x-28,textHeight);
        var bodySymbols=SymbolizeStackLabel(body);
        FitAbilityBodyText(body,bodySymbols,size.x-38,effectHeight-14-typeHeight,casting?13:15,18);
        textHeight=Mathf.Max(effectHeight-14-typeHeight,bodySymbols.GetPreferredValues(bodySymbols.text,size.x-38,10000).y+4);
        body.rectTransform.sizeDelta=new Vector2(size.x-38,textHeight);effectContent.sizeDelta=new Vector2(size.x-28,textHeight);
        if(textHeight>effectHeight-12-typeHeight)AddStackScrollIndicator(effectContent);
        if(!string.IsNullOrEmpty(cost)) {
            if(cost.Contains("{")){var symbols=CardSymbolText.Create(face,"Cost",25,Paper);IntegratedUiUtility.Place(symbols.rectTransform,new Vector2(0,-top+29),new Vector2(size.x-22,34));symbols.alignment=TMPro.TextAlignmentOptions.Center;symbols.text=CardSymbolText.Format(cost);}
            else {var value=Label(face,"Cost",PrintedCostText(source,cost),new Vector2(0,-top+29),new Vector2(size.x-22,34),26,Gold);value.raycastTarget=false;}
        }
        if(casting&&source.creature){var stats=Label(face,"CreatureStats",source.power+"/"+source.toughness,new Vector2(size.x*.5f-25,-top+29),new Vector2(42,28),18,Paper);stats.fontStyle=FontStyle.Bold;}
    }
    private void PresentSpreeChoices(ForgeCardView source,Vector2 anchor,long captured) => PresentModeCards(source,captured,true);
}
