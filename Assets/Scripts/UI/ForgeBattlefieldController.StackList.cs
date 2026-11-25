using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private bool stackCollapsed,stackBrowseOpen;
    private Button stackViewButton,stackCollapseButton;
    private RectTransform stackList,stackOverview;
    private string stackListKey,stackListContext,stackOverviewKey;
    private long stackPreviewRequest;
    private string stackPreviewOption;
    private bool stackPreviewSelected,stackPreviewReset;
    private string StackPositionLabel(int position)=>TapPaymentText("堆叠 "+position,"Stack "+position,"スタック "+position);
    private bool StackTargetSelected(string id)
    {
        if(!UsesTargetWindow)return false;
        int index=OptionIndex(id);if(index<0)return false;
        if(submitted&&stackPreviewRequest==requestId) {
            if(stackPreviewReset)return false;
            if(stackPreviewOption==id)return stackPreviewSelected;
            if(view.request.kind=="SelectStackTarget" || view.request.maxCount==1)return false;
        }
        return view.request.options[index].selected;
    }
    private void PreviewStackTargetChoice(ForgeOption option)
    {
        if(!UsesTargetWindow || option.id!="reset-targets" && option.id!="reset-cards" && !IsStackPickerCandidate(option.id))return;
        // Feedback while Forge replies, without changing its authoritative options.
        stackPreviewRequest=requestId;stackPreviewOption=option.id;
        stackPreviewReset=option.id is "reset-targets" or "reset-cards";stackPreviewSelected=!option.selected;
        foreach(var pair in stackCards) {
            if(!pair.Key.StartsWith("id:",StringComparison.Ordinal))continue;
            string id="stack:"+pair.Key.Substring(3);bool selected=StackTargetSelected(id);
            pair.Value.rim.color=selected?Gold:Color.white;
            PresentSelectionGlow(pair.Value.row,pair.Value.tile,selected);
        }
        foreach(var window in new[]{stackList,stackOverview}) {
            var content=window?.GetComponentInChildren<ScrollRect>()?.content;if(content==null)continue;
            foreach(var candidate in view.request.options.Where(o=>IsStackPickerCandidate(o.id))) {
                string id=candidate.id;
                var entry=content.Find("Entry_"+id.Replace(':','_')) as RectTransform;
                if(entry==null)continue;
                bool selected=StackTargetSelected(id);
                if(window==stackList){
                    var button=entry.GetComponentInChildren<ForgeBattlefieldCardPointer>();
                    if(button!=null)PaintStackPickerTab(entry,(RectTransform)button.transform,id,false);
                    continue;
                }
                if(!id.StartsWith("stack:",StringComparison.Ordinal))continue;
                entry.GetComponent<Image>().color=selected?Gold:new Color(.38f,.49f,.56f);
                var art=entry.Find("Card") as RectTransform;
                if(art!=null)PresentSelectionGlow(entry,art,selected);
            }
        }
    }

    // Retain the plain source for inspection/accessibility; only the symbol-aware
    // child renders it. Fixed-size text and sprites share one CJK-capable font.
    private TextMeshProUGUI SymbolizeStackLabel(Text source)
    {
        var sourcePreference=source.GetComponent<PreferenceText>();if(sourcePreference==null)sourcePreference=source.gameObject.AddComponent<PreferenceText>();sourcePreference.Apply();
        source.enabled=false;
        var text=source.transform.Find("Symbols")?.GetComponent<TextMeshProUGUI>();
        if(text==null) {
            text=CardSymbolText.Create(source.transform,"Symbols",source.fontSize,source.color);
            text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;
            text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;
        }
        text.font=GamePreferences.TmpFont??TMP_Settings.defaultFontAsset;
        text.fontSize=source.fontSize;text.color=source.color;text.enableAutoSizing=false;
        text.richText=sourcePreference.Literal&&source.supportRichText;
        var preference=text.GetComponent<PreferenceText>();if(preference==null)preference=text.gameObject.AddComponent<PreferenceText>();
        preference.SetBaseSize(source.fontSize/GamePreferences.TextScale);
        text.alignment=source.alignment==TextAnchor.UpperLeft?TextAlignmentOptions.TopLeft:TextAlignmentOptions.Center;
        text.text=CardSymbolText.Format(source.text);
        return text;
    }

    private Button StackIcon(Transform parent,string name,StackControlIcon.Kind kind,Action action)
    {
        var rect=Panel(parent,name,Vector2.zero,new Vector2(38,38),new Color(.08f,.13f,.18f,.96f));
        rect.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(38,38,19);
        var outline=rect.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.6f,.75f,.81f,.8f);outline.effectDistance=new Vector2(1,-1);
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
        var colors=button.colors;colors.highlightedColor=new Color(.6f,.82f,1);colors.pressedColor=new Color(.35f,.7f,.9f);button.colors=colors;
        var icon=Rect(rect,"Icon",Vector2.zero,new Vector2(22,22)).gameObject.AddComponent<StackControlIcon>();icon.SetKind(kind);icon.color=Paper;icon.raycastTarget=false;
        button.onClick.AddListener(()=>action());return button;
    }
    private void PresentStackControls(RectTransform column,int count,RectTransform viewport,float height)
    {
        if(count==0){stackCollapsed=false;stackBrowseOpen=false;}
        if(stackCollapseButton==null) {
            stackCollapseButton=StackIcon(column,"CollapseStack",StackControlIcon.Kind.Right,()=>{
                stackCollapsed=!stackCollapsed;hoveredStack=null;LeaveInspectCard();
                PresentStackCards();
            });
            stackViewButton=StackIcon(column,"ViewStack",StackControlIcon.Kind.View,()=>{
                if (FourWindowSelection && !viewingBattlefield) return;
                stackBrowseOpen=!stackBrowseOpen;PresentStackList();RefreshBattlefieldViewControls();
            });
        }
        foreach(var button in new[]{stackCollapseButton,stackViewButton}) {
            var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,1);
            rect.anchoredPosition=new Vector2(button==stackCollapseButton?-6:-52,0);
        }
        if(!IsRedesignLayout || count>0){stackCollapseButton.gameObject.SetActive(count>0);stackViewButton.gameObject.SetActive(count>0);}
        var foldIcon=HudTheme!=null
            ?(stackCollapsed?StackControlIcon.Kind.Down:StackControlIcon.Kind.Up)
            :(stackCollapsed?StackControlIcon.Kind.Left:StackControlIcon.Kind.Right);
        stackCollapseButton.GetComponentInChildren<StackControlIcon>().SetKind(foldIcon);
        // Move the masked viewport (cards and scroll together), keeping the
        // controls on screen. Children cannot be clicked while retracted.
        var group=viewport.GetComponent<CanvasGroup>();if(group==null)group=viewport.gameObject.AddComponent<CanvasGroup>();
        group.interactable=group.blocksRaycasts=!stackCollapsed;
        if(IsRedesignLayout)return; // The complete copied-scene panel has its own sliding presentation.
        float offscreen=column.rect.width+400;
        ForgeUiMotion.On(viewport).MoveTo(new Vector2(stackCollapsed?offscreen:0,-40),viewport.sizeDelta,seconds:.28f);
        if(stackScrollbar!=null)stackScrollbar.gameObject.SetActive(!stackCollapsed&&stackContent.rect.height>height);
    }

    private bool FourWindowSelection => IsFourPlayerScene && (UsesTargetWindow || IsCardPickerRequest || FourCombatDecision);

    private bool UsesTargetWindow {
        get {
            var request=view?.request;
            if(!integrated||view?.awaitingInput!=true||request?.seat!=seat||request.options==null)return false;
            if(request.kind is "InputSelectTargets" or "SelectStackTarget")
                return IsFourPlayerScene || request.options.Any(o=>o.id.StartsWith("stack:",StringComparison.Ordinal));
            return IsFourPlayerScene && request.kind=="InputSelectEntitiesFromList"
                && request.options.Any(o=>IsStackPickerCandidate(o.id) && !o.id.StartsWith("card:",StringComparison.Ordinal))
                && request.options.All(o=>IsStackPickerCandidate(o.id) || o.id is "ok" or "cancel" or "reset-targets" or "reset-cards");
        }
    }
    private static bool IsStackPickerCandidate(string id) => id.StartsWith("stack:",StringComparison.Ordinal)
        || id.StartsWith("card:",StringComparison.Ordinal) || id.StartsWith("player:",StringComparison.Ordinal);

    private string StackTargetContext
    {
        get {
            if(!UsesTargetWindow || view.request.contextId!=0 || view.request.kind!="SelectStackTarget")return BattlefieldDecisionContext;
            // Native selectStackTarget republishes a fresh request ID for every
            // tentative pick. Only visual continuity uses this identity.
            return $"{service?.MatchId}/{seat}/{view.restartRevision}/{view.turn}/{view.phase}/{view.request.sourceCardId}/"
                +string.Join("|",view.request.options.Where(o=>IsStackPickerCandidate(o.id)).Select(o=>o.id));
        }
    }

    private void PaintStackPickerTab(RectTransform rim,RectTransform face,string id,bool hovered)
    {
        var rule=rim.Find("FocusRule") as RectTransform;
        if(rule==null)return;
        int index=OptionIndex(id);
        bool available=index>=0 && !view.request.options[index].disabled;
        bool selected=StackTargetSelected(id);
        bool focus=hovered && available && canAct && !submitted;
        Color ink=selected?ForgePlayableGlow.ChosenLight:focus?ForgePlayableGlow.ReadyLight:HudMuted;
        rim.GetComponent<Image>().color=selected||focus?ink:available?Color.Lerp(HudEdge,HudMuted,.4f):HudRule;
        rim.Find("StackPosition").GetComponent<Text>().color=ink;
        rim.Find("Picked").gameObject.SetActive(selected);
        PresentSelectionGlow(rim,face,selected);
        rule.GetComponent<Image>().color=ink;
        float width=selected?rim.rect.width:focus?rim.rect.width*.40f:0;
        ForgeUiMotion.On(rule).MoveTo(rule.anchoredPosition,new Vector2(width,2),seconds:.14f);
    }
    private void PresentStackList()
    {
        string context=StackTargetContext;
        if(stackListContext!=context){stackListKey=null;stackListContext=context;}
        if((view?.stackItems?.Length??0)==0 || FourWindowSelection && !viewingBattlefield)stackBrowseOpen=false;
        PresentStackWindow(false);
        PresentStackWindow(true);
    }
    // A stack object's controller remains independent of the current controller of its source.
    private (int position, int owner, ForgePlayerView player) PickerIdentity(ForgeStackItem entry, string optionId, ForgeStackItem[] entries)
    {
        if (optionId.StartsWith("stack:", StringComparison.Ordinal))
            return (Array.FindIndex(entries, s => optionId == "stack:" + s.id) + 1, entry.controllerSeat, null);
        var players = view.players ?? Array.Empty<ForgePlayerView>();
        if (optionId.StartsWith("player:", StringComparison.Ordinal))
            return (0, 0, players.FirstOrDefault(p => optionId == "player:" + p.targetId));
        var card = entry.source;
        var holder = card == null ? null : players.FirstOrDefault(p => p.battlefield?.Any(c => c.id == card.id) == true);
        return (0, holder?.seat ?? card?.ownerSeat ?? 0, null);
    }

    private void DrawTargetPlayer(RectTransform tile, ForgePlayerView player)
    {
        var accent = FourSeatColor(player.seat);
        tile.GetComponent<Image>().color = Color.Lerp(HudInk, accent, .10f);
        var portrait = PlayerAvatarGraphic.Resolve(FourPlayerPortrait(player.seat));
        var art = Rect(tile, "PlayerAvatar", new Vector2(0,64), new Vector2(240,207)).gameObject.AddComponent<RawImage>();
        art.texture = portrait.Load();
        var uv = portrait.UV;
        float crop = uv.height * (1 - 207f / 240f) * .5f;
        art.uvRect = UnityEngine.Rect.MinMaxRect(uv.xMin, uv.yMin + crop, uv.xMax, uv.yMax - crop);
        art.raycastTarget = false;
        var edge = Panel(tile, "PortraitEdge", new Vector2(0,-40), new Vector2(240,3), accent);
        edge.GetComponent<Image>().raycastTarget = false;

        var name = Label(tile, "PlayerName", "", new Vector2(0,-77), new Vector2(208,68), 25, accent);
        MenuTypography.Use(name, MenuTypography.Face.Heading);
        name.GetComponent<PreferenceText>().Literal = true;
        name.text = PlayerDisplayName(player.seat);
        name.supportRichText = false;
        name.alignment = TextAnchor.MiddleLeft;
        name.resizeTextForBestFit = true;
        name.resizeTextMinSize = 14;
        name.resizeTextMaxSize = name.fontSize;
        name.raycastTarget = false;
        var rule = Panel(tile, "LifeRule", new Vector2(0,-114), new Vector2(208,1), HudRule);
        rule.GetComponent<Image>().raycastTarget = false;
        var life = Label(tile, "LifeLabel", TapPaymentText("生命","Life","ライフ"), new Vector2(-50,-140), new Vector2(108,40), 17, HudMuted);
        life.alignment = TextAnchor.MiddleLeft;
        life.raycastTarget = false;
        var amount = Label(tile, "Life", player.life.ToString(), new Vector2(47,-138), new Vector2(114,48), 28, accent);
        MenuTypography.Use(amount, MenuTypography.Face.Heading);
        amount.alignment = TextAnchor.MiddleRight;
        amount.raycastTarget = false;
    }

    private void PresentStackWindow(bool overview)
    {
        var entries=view?.stackItems??Array.Empty<ForgeStackItem>();
        bool selecting=!overview&&UsesTargetWindow;
        var window=overview?stackOverview:stackList;
        var choices=entries.Select(e=>(entry:e,id:"stack:"+e.id)).ToList();
        if(selecting) {
            choices.RemoveAll(e=>OptionIndex(e.id)<0);
            foreach(var option in view.request.options.Where(o=>o.id.StartsWith("stack:",StringComparison.Ordinal)&&!choices.Any(e=>e.id==o.id))) {
                int.TryParse(option.id.Substring(6),out int id);
                choices.Add((option.ability??new ForgeStackItem{id=id,source=HasChoiceCard(option)?option.card:null,kind="spell",description=option.label},option.id));
            }
            // A request can allow a spell OR a permanent/player. Keep those
            // native alternatives in the same window with their original IDs.
            var visible=(view.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>
                (p.battlefield??Array.Empty<ForgeCardView>()).Concat(p.hand??Array.Empty<ForgeCardView>())
                .Concat(p.graveyardCards??Array.Empty<ForgeCardView>()).Concat(p.exileCards??Array.Empty<ForgeCardView>())
                .Concat(p.commandCards??Array.Empty<ForgeCardView>()));
            foreach(var option in view.request.options.Where(o=>o.id.StartsWith("card:",StringComparison.Ordinal)||o.id.StartsWith("player:",StringComparison.Ordinal))) {
                var card=HasChoiceCard(option)?option.card:visible.FirstOrDefault(c=>option.id=="card:"+c.id);
                choices.Add((new ForgeStackItem{source=card,kind="spell",description=option.label},option.id));
            }
        }
        if(choices.Count==0&&!(selecting&&IsFourPlayerScene)||overview&&!stackBrowseOpen||!overview&&!selecting) {
            if(window!=null)window.gameObject.SetActive(false);return;
        }
        float width=Mathf.Min(Mathf.Min(surface.rect.width-80,1200),Mathf.Max(640,choices.Count*264+96));
        var targetHints=choices.Select(e=>e.id.StartsWith("stack:",StringComparison.Ordinal)
            ?StackIncomingTargetHint(e.entry):(heading:"",body:e.entry.source==null?"":ZoneTargetHint(e.entry.source))).ToArray();
        float targetHintHeight=ZoneTargetHintHeight(targetHints.Select(h=>h.heading.Length>0?h.heading+"\n"+h.body:h.body).ToArray(),244);
        float ordinalHeight=Mathf.Ceil(28*GamePreferences.TextScale),height=502+ordinalHeight+targetHintHeight,viewportHeight=height-104;
        if(window==null) {
            window=Panel(surface,overview?"StackOverview":"StackList",Vector2.zero,new Vector2(width,height),new Color(.035f,.065f,.095f,.97f));
            if(overview)stackOverview=window;else stackList=window;
        }
        window.gameObject.SetActive(true);window.sizeDelta=new Vector2(width,height);
        SetOverlayLayer(window,DecisionLayerOffset+(overview?1:0),true);
        if(!overview){window.SetAsLastSibling();window.anchoredPosition=Vector2.zero;}
        else {
            var limit=Vector2.Max(Vector2.zero,(surface.rect.size-window.rect.size)*.5f);
            window.anchoredPosition=new Vector2(Mathf.Clamp(window.anchoredPosition.x,-limit.x,limit.x),Mathf.Clamp(window.anchoredPosition.y,-limit.y,limit.y));
        }
        if(HudTheme==null)window.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(1200,Mathf.CeilToInt(height),14);
        string key=ServerSnapshotJson.Serialize(view.request)+"/"+string.Join("|",choices.Select(e=>e.id+ServerSnapshotJson.Serialize(e.entry)))+"/"+string.Join("|",targetHints)+"/"+submitted+"/"+GamePreferences.Revision+"/"+width;
        var identities = choices.Select(c => PickerIdentity(c.entry, c.id, entries)).ToArray();
        if (IsFourPlayerScene) {
            key += "/" + seat + "/" + string.Join("|", identities.Select(i => i.player != null
                ? $"{i.player.seat}:{i.player.life}:{FourPlayerPortrait(i.player.seat)}:{PlayerDisplayName(i.player.seat)}"
                : $"{i.position}:{i.owner}:{PlayerDisplayName(i.owner)}"));
        }
        string previousKey=overview?stackOverviewKey:stackListKey;
        if(previousKey!=key) {
            var previousCards=window.GetComponentInChildren<ScrollRect>();
            float scrollPosition=previousCards?.horizontalNormalizedPosition??0;
            bool sameContext=previousKey!=null;
            var priorWidths=selecting && sameContext ? previousCards?.content.Cast<Transform>()
                .Where(t=>t.Find("FocusRule")!=null).ToDictionary(t=>t.name,t=>((RectTransform)t.Find("FocusRule")).rect.width) : null;
            var sourceOffsets=sameContext?previousCards?.content.Cast<Transform>()
                .Select(t=>(t.name,list:t.Find("TargetHint")?.GetComponentInChildren<ScrollRect>()))
                .Where(row=>row.list!=null&&row.list.content.anchoredPosition.y>0)
                .ToDictionary(row=>row.name,row=>row.list.content.anchoredPosition.y):null;
            foreach(var child in window.Cast<Transform>().ToArray()){child.gameObject.SetActive(false);child.SetParent(null,false);Destroy(child.gameObject);}
            if(overview)stackOverviewKey=key;else stackListKey=key;
            if (HudTheme!=null) StyleHudSurface(window,HudInk,HudEdge);
            if(overview) {
                var header=Panel(window,"DragHeader",new Vector2(0,height*.5f-26),new Vector2(width-88,52),Color.clear);
                header.gameObject.AddComponent<ForgeCardNoticeDrag>().Window=window;
            }
            var title=Label(window,"Title",selecting?TapPaymentText("选择目标","Choose a target","対象を選択"):TapPaymentText("查看堆叠","View stack","スタックを見る"),Vector2.zero,new Vector2(width-140,32),21,Paper);title.raycastTarget=false;
            if(selecting && view.request.sourceCard is {faceDown:false} source && !string.IsNullOrWhiteSpace(source.name)){
                string action=title.text;
                title.text=GamePreferences.CardFaceName(source.name)+" · "+action;
                if(title.preferredWidth>width-140)title.text=action;
            }
            float titleHeight=Mathf.Max(32,Mathf.Ceil(title.preferredHeight)+4);
            title.rectTransform.sizeDelta=new Vector2(width-140,titleHeight);title.rectTransform.anchoredPosition=new Vector2(0,height*.5f-8-titleHeight*.5f);
            var close=StackIcon(window,"Close",StackControlIcon.Kind.Close,()=>{if(overview){stackBrowseOpen=false;stackOverview.gameObject.SetActive(false);}else ViewBattlefield();});((RectTransform)close.transform).anchoredPosition=new Vector2(width*.5f-30,height*.5f-24);
            var content=Scroll(window,"Cards",new Vector2(0,5),new Vector2(width-40,viewportHeight));
            var scroll=content.parent.GetComponent<ScrollRect>();scroll.horizontal=true;scroll.vertical=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);content.sizeDelta=new Vector2(Mathf.Max(width-40,choices.Count*264+52),viewportHeight);
            if(choices.Count==0)Label(window,"EmptyTargets",TapPaymentText("没有可选目标","No available targets","選択できる対象がありません"),Vector2.zero,new Vector2(width-80,48),21,HudMuted);
            for(int i=0;i<choices.Count;i++) {
                var entry=choices[i].entry;string optionId=choices[i].id;
                int option=OptionIndex(optionId);long expected=requestId;
                bool legal=selecting&&option>=0&&!view.request.options[option].disabled;
                bool selected=StackTargetSelected(optionId);
                float left=Mathf.Max(12,(content.rect.width-choices.Count*264+20)*.5f);
                var rim=Panel(content,"Entry_"+optionId.Replace(":","_"),new Vector2(left+i*264,-34-ordinalHeight),new Vector2(244,339),selected?Gold:legal?new Color(.1f,.9f,1):new Color(.38f,.49f,.56f));
                if(entry.kind!="spell")rim.GetComponent<Image>().sprite=AbilityShape();
                rim.anchorMin=rim.anchorMax=rim.pivot=new Vector2(0,1);rim.GetComponent<Image>().raycastTarget=false;
                var (position, owner, player) = identities[i];
                if(position>0 || selecting) {
                    string caption=position>0?StackPositionLabel(position):optionId.StartsWith("stack:",StringComparison.Ordinal)
                        ?TapPaymentText("堆叠","Stack","スタック"):player!=null && IsFourPlayerScene
                        ?TapPaymentText($"{player.seat} 号席",$"Seat {player.seat}",$"席 {player.seat}"):optionId.StartsWith("player:",StringComparison.Ordinal)
                        ?TapPaymentText("玩家","Player","プレイヤー"):TapPaymentText("卡牌","Card","カード");
                    if(IsFourPlayerScene&&owner>0){
                        caption+=" · "+PlayerNamesRichText(PlayerDisplayName(owner),owner);
                    }
                    var label=Label(rim,"StackPosition",caption,new Vector2(0,ordinalHeight*.5f+26),new Vector2(244,ordinalHeight),17,Paper);label.raycastTarget=false;
                    label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f,1);
                    if(selecting){
                        label.alignment=TextAnchor.MiddleLeft;
                        var picked=Panel(rim,"Picked",Vector2.zero,Vector2.zero,ForgePlayableGlow.ChosenLight);
                        picked.GetComponent<Image>().raycastTarget=false;
                        int repetitions=option>=0?view.request.options[option].chosenCount:0;
                        var word=Label(picked,"Label",TapPaymentText("已选","Selected","選択中")+(repetitions>1?" ×"+repetitions:""),Vector2.zero,
                            new Vector2(136,ordinalHeight),15,HudInk);
                        word.raycastTarget=false;
                        float tagWidth=Mathf.Min(136,Mathf.Ceil(word.preferredWidth)+16);
                        picked.anchorMin=picked.anchorMax=picked.pivot=new Vector2(0,1);
                        picked.sizeDelta=new Vector2(tagWidth,ordinalHeight);
                        picked.anchoredPosition=new Vector2(Mathf.Min(Mathf.Ceil(label.preferredWidth)+10,rim.rect.width-tagWidth),ordinalHeight+26);
                        word.rectTransform.sizeDelta=picked.sizeDelta;
                        float widthBefore=priorWidths!=null && priorWidths.TryGetValue(rim.name,out float w)?w:selected?rim.rect.width:0;
                        var rule=Panel(rim,"FocusRule",new Vector2(0,20),new Vector2(widthBefore,2),HudAccent);
                        rule.anchorMin=rule.anchorMax=rule.pivot=new Vector2(0,1);rule.GetComponent<Image>().raycastTarget=false;
                    }
                }
                var tile=Panel(rim,selecting?(optionId.StartsWith("stack:",StringComparison.Ordinal)?"StackListTarget_"+optionId.Substring("stack:".Length):"StackListChoice_"+option):"Card",Vector2.zero,new Vector2(240,335),Ink);
                if(!selecting)PresentSelectionGlow(rim,tile,selected);
                if(!string.IsNullOrEmpty(targetHints[i].body)) {
                    var hint=Rect(rim,"TargetHint",new Vector2(0,-339),new Vector2(244,targetHintHeight));
                    hint.anchorMin=hint.anchorMax=hint.pivot=new Vector2(.5f,1);
                    float sourceOffset=sourceOffsets!=null&&sourceOffsets.TryGetValue(rim.name,out float saved)?saved:0;
                    PresentZoneTargetHint(hint,targetHints[i].body,244,targetHintHeight,targetHints[i].heading,sourceOffset);
                }
                Sprite art=null;if(entry.source!=null)TryBattlefieldArt(entry.source,out art);var image=tile.GetComponent<Image>();image.sprite=art;image.color=art!=null?Color.white:Ink;image.preserveAspect=true;
                if (player != null && IsFourPlayerScene) DrawTargetPlayer(tile, player);
                else if(art==null) {
                    string name=entry.source?.faceDown==true?TapPaymentText("面朝下的牌","Face-down card","裏向きのカード"):entry.source!=null?GamePreferences.CardFaceName(entry.source.name):option>=0?RequestOptionLabel(view.request.options[option]):"";
                    var fallback=Label(tile,"Fallback",name,Vector2.zero,new Vector2(218,120),18,Paper);fallback.raycastTarget=false;
                    if(player!=null)SetNamedPlayerText(fallback,PlayerDisplayName(player.seat),player.seat);
                }
                PresentAbilityFace(tile,entry.source,entry.description,entry.kind,entry.kind!="spell");
                PresentPreparedSpellFace(tile,entry.source,tile.sizeDelta);
                PresentChosenSpellFace(tile,entry,art);
                if(entry.kind=="spell")PresentComponentFace(tile,entry.source);
                var button=tile.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.None;button.interactable=legal;
                var colors=button.colors;colors.disabledColor=selecting&&!legal?new Color(.55f,.55f,.55f,1):Color.white;button.colors=colors;
                // Browsing has no submit listener, including during targeting.
                if(selecting)button.onClick.AddListener(()=>Choose(option,expected));
                var hover=new StackCardVisual{tile=tile,source=entry.source,entry=entry};
                var pointer=tile.gameObject.AddComponent<ForgeBattlefieldCardPointer>();
                pointer.Configure(()=>ShowStackHover(hover),LeaveInspectCard,null,false);
                pointer.ConfigureScrollParent(scroll);
                if(selecting){
                    PaintStackPickerTab(rim,tile,optionId,false);
                    pointer.VisualStateChanged+=()=>{if(rim!=null && rim.gameObject.activeInHierarchy)PaintStackPickerTab(rim,tile,optionId,pointer.IsHovered);};
                }
            }
            Canvas.ForceUpdateCanvases();scroll.horizontalNormalizedPosition=sameContext?scrollPosition:0;
        }
        if(selecting){
            foreach(var b in window.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("StackListTarget_")||b.name.StartsWith("StackListChoice_"))) {
                int option=b.name.StartsWith("StackListChoice_")?int.Parse(b.name.Substring("StackListChoice_".Length)):OptionIndex("stack:"+b.name.Substring("StackListTarget_".Length));
                b.interactable=canAct&&!submitted&&option>=0&&!view.request.options[option].disabled;
            }
            int confirm=OptionIndex("ok"),skip=OptionIndex("cancel");
            // Only the native finish-targeting sentinel permits an empty choice.
            // A generic cancellation must never become confirmation.
            if(confirm<0&&view.request.selectedCount==0&&view.request.minCount<=0&&skip>=0
                &&view.request.options[skip].label=="[FINISH TARGETING]")confirm=skip;
            string confirmId=confirm>=0?view.request.options[confirm].id:"ok";
            var accept=BindDecisionButton(window,"Confirm",TapPaymentText("确认","Confirm","確定"),confirmId,new Vector2(-94,-height*.5f+26));
            accept.interactable=canAct&&!submitted&&confirm>=0&&!view.request.options[confirm].disabled
                &&view.request.selectedCount>=view.request.minCount;
            string resetId=OptionIndex("reset-cards")>=0?"reset-cards":"reset-targets";
            int reset=OptionIndex(resetId);
            var restart=BindDecisionButton(window,"Reset",TapPaymentText("重新选择","Reset","選び直す"),resetId,new Vector2(94,-height*.5f+26));
            restart.interactable=canAct&&!submitted&&reset>=0&&!view.request.options[reset].disabled;
            if(IsFourPlayerScene&&skip>=0&&skip!=confirm){
                var cancel=BindDecisionButton(window,"Cancel",TapPaymentText("取消","Cancel","キャンセル"),"cancel",new Vector2(184,-height*.5f+26));
                ((RectTransform)accept.transform).anchoredPosition=new Vector2(-184,-height*.5f+26);
                ((RectTransform)restart.transform).anchoredPosition=new Vector2(0,-height*.5f+26);
                cancel.interactable=canAct&&!submitted&&!view.request.options[skip].disabled;
            }
            if(targetOrigin!=null)targetOrigin.gameObject.SetActive(false);if(targetArrow!=null)targetArrow.SetVisible(false);}
    }
}
