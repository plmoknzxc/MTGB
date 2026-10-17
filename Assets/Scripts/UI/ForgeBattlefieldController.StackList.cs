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
        if(!HasStackTargetChoices)return false;
        int index=OptionIndex(id);if(index<0)return false;
        if(submitted&&stackPreviewRequest==requestId) {
            if(stackPreviewReset)return false;
            if(stackPreviewOption==id)return stackPreviewSelected;
        }
        return view.request.options[index].selected;
    }
    private void PreviewStackTargetChoice(ForgeOption option)
    {
        if(!HasStackTargetChoices || option.id!="reset-targets"&&!option.id.StartsWith("stack:",StringComparison.Ordinal))return;
        // Feedback while Forge replies, without changing its authoritative options.
        stackPreviewRequest=requestId;stackPreviewOption=option.id;
        stackPreviewReset=option.id=="reset-targets";stackPreviewSelected=!option.selected;
        foreach(var pair in stackCards) {
            if(!pair.Key.StartsWith("id:",StringComparison.Ordinal))continue;
            string id="stack:"+pair.Key.Substring(3);bool selected=StackTargetSelected(id);
            pair.Value.rim.color=selected?Gold:Color.white;
            PresentSelectionGlow(pair.Value.row,pair.Value.tile,selected);
        }
        foreach(var window in new[]{stackList,stackOverview}) {
            var content=window?.GetComponentInChildren<ScrollRect>()?.content;if(content==null)continue;
            foreach(Transform entry in content) {
                if(!entry.name.StartsWith("Entry_stack_",StringComparison.Ordinal))continue;
                string id="stack:"+entry.name.Substring("Entry_stack_".Length);bool selected=StackTargetSelected(id);
                entry.GetComponent<Image>().color=selected?Gold:window==stackList?new Color(.1f,.9f,1):new Color(.38f,.49f,.56f);
                var art=entry.Find(window==stackList?"StackListTarget_"+id.Substring(6):"Card") as RectTransform;
                if(art!=null)PresentSelectionGlow((RectTransform)entry,art,selected);
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
                stackBrowseOpen=!stackBrowseOpen;PresentStackList();RefreshBattlefieldViewControls();
            });
        }
        foreach(var button in new[]{stackCollapseButton,stackViewButton}) {
            var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,1);
            rect.anchoredPosition=new Vector2(button==stackCollapseButton?-6:-52,0);
        }
        stackCollapseButton.gameObject.SetActive(count>0);stackViewButton.gameObject.SetActive(count>0);
        stackCollapseButton.GetComponentInChildren<StackControlIcon>().SetKind(stackCollapsed?StackControlIcon.Kind.Left:StackControlIcon.Kind.Right);
        // Move the masked viewport (cards and scroll together), keeping the
        // controls on screen. Children cannot be clicked while retracted.
        var group=viewport.GetComponent<CanvasGroup>();if(group==null)group=viewport.gameObject.AddComponent<CanvasGroup>();
        group.interactable=group.blocksRaycasts=!stackCollapsed;
        float offscreen=column.rect.width+400;
        ForgeUiMotion.On(viewport).MoveTo(new Vector2(stackCollapsed?offscreen:0,-40),viewport.sizeDelta,seconds:.28f);
        if(stackScrollbar!=null)stackScrollbar.gameObject.SetActive(!stackCollapsed&&stackContent.rect.height>height);
    }

    private bool HasStackTargetChoices => integrated && view?.awaitingInput==true && view.request!=null && view.request.seat==seat
        && (view.request.kind is "InputSelectTargets" or "SelectStackTarget")
        && (view.request.options??Array.Empty<ForgeOption>()).Any(o=>o.id.StartsWith("stack:",StringComparison.Ordinal));
    private string StackTargetContext => BattlefieldDecisionContext;
    private void PresentStackList()
    {
        string context=StackTargetContext;
        if(stackListContext!=context){stackListKey=null;stackListContext=context;}
        if((view?.stackItems?.Length??0)==0)stackBrowseOpen=false;
        PresentStackWindow(false);
        PresentStackWindow(true);
    }
    private void PresentStackWindow(bool overview)
    {
        var entries=view?.stackItems??Array.Empty<ForgeStackItem>();
        bool selecting=!overview&&HasStackTargetChoices;
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
                .Concat(p.graveyardCards??Array.Empty<ForgeCardView>()).Concat(p.exileCards??Array.Empty<ForgeCardView>()));
            foreach(var option in view.request.options.Where(o=>o.id.StartsWith("card:",StringComparison.Ordinal)||o.id.StartsWith("player:",StringComparison.Ordinal))) {
                var card=HasChoiceCard(option)?option.card:visible.FirstOrDefault(c=>option.id=="card:"+c.id);
                choices.Add((new ForgeStackItem{source=card,kind="spell",description=option.label},option.id));
            }
        }
        if(choices.Count==0||overview&&!stackBrowseOpen||!overview&&!selecting) {
            if(window!=null)window.gameObject.SetActive(false);return;
        }
        float width=Mathf.Min(Mathf.Min(surface.rect.width-80,1200),Mathf.Max(640,choices.Count*264+96));
        float ordinalHeight=Mathf.Ceil(28*GamePreferences.TextScale),height=502+ordinalHeight,viewportHeight=height-104;
        if(window==null) {
            window=Panel(surface,overview?"StackOverview":"StackList",Vector2.zero,new Vector2(width,height),new Color(.035f,.065f,.095f,.97f));
            if(overview)stackOverview=window;else stackList=window;
        }
        SetOverlayLayer(window,DecisionLayerOffset+(overview?1:0),true);
        window.gameObject.SetActive(true);window.sizeDelta=new Vector2(width,height);
        if(!overview){window.SetAsLastSibling();window.anchoredPosition=Vector2.zero;}
        else {
            var limit=Vector2.Max(Vector2.zero,(surface.rect.size-window.rect.size)*.5f);
            window.anchoredPosition=new Vector2(Mathf.Clamp(window.anchoredPosition.x,-limit.x,limit.x),Mathf.Clamp(window.anchoredPosition.y,-limit.y,limit.y));
        }
        window.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(1200,Mathf.CeilToInt(height),14);
        string key=ServerSnapshotJson.Serialize(view.request)+"/"+string.Join("|",choices.Select(e=>e.id+ServerSnapshotJson.Serialize(e.entry)))+"/"+submitted+"/"+GamePreferences.Revision+"/"+width;
        string previousKey=overview?stackOverviewKey:stackListKey;
        if(previousKey!=key) {
            float scrollPosition=window.GetComponentInChildren<ScrollRect>()?.horizontalNormalizedPosition??0;
            bool sameContext=previousKey!=null;
            foreach(var child in window.Cast<Transform>().ToArray()){child.gameObject.SetActive(false);child.SetParent(null,false);Destroy(child.gameObject);}
            if(overview)stackOverviewKey=key;else stackListKey=key;
            if(overview) {
                var header=Panel(window,"DragHeader",new Vector2(0,height*.5f-26),new Vector2(width-88,52),Color.clear);
                header.gameObject.AddComponent<ForgeCardNoticeDrag>().Window=window;
            }
            var title=Label(window,"Title",selecting?TapPaymentText("选择目标","Choose a target","対象を選択"):TapPaymentText("查看堆叠","View stack","スタックを見る"),Vector2.zero,new Vector2(width-140,32),21,Paper);title.raycastTarget=false;
            float titleHeight=Mathf.Max(32,Mathf.Ceil(title.preferredHeight)+4);
            title.rectTransform.sizeDelta=new Vector2(width-140,titleHeight);title.rectTransform.anchoredPosition=new Vector2(0,height*.5f-8-titleHeight*.5f);
            var close=StackIcon(window,"Close",StackControlIcon.Kind.Close,()=>{if(overview){stackBrowseOpen=false;stackOverview.gameObject.SetActive(false);}else ViewBattlefield();});((RectTransform)close.transform).anchoredPosition=new Vector2(width*.5f-30,height*.5f-24);
            var content=Scroll(window,"Cards",new Vector2(0,5),new Vector2(width-40,viewportHeight));
            var scroll=content.parent.GetComponent<ScrollRect>();scroll.horizontal=true;scroll.vertical=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);content.sizeDelta=new Vector2(Mathf.Max(width-40,choices.Count*264+52),viewportHeight);
            for(int i=0;i<choices.Count;i++) {
                var entry=choices[i].entry;string optionId=choices[i].id;
                int option=OptionIndex(optionId);long expected=requestId;
                bool legal=selecting&&option>=0&&!view.request.options[option].disabled;
                bool selected=StackTargetSelected(optionId);
                float left=Mathf.Max(12,(content.rect.width-choices.Count*264+20)*.5f);
                var rim=Panel(content,"Entry_"+optionId.Replace(":","_"),new Vector2(left+i*264,-34-ordinalHeight),new Vector2(244,339),selected?Gold:legal?new Color(.1f,.9f,1):new Color(.38f,.49f,.56f));
                if(entry.kind!="spell")rim.GetComponent<Image>().sprite=AbilityShape();
                rim.anchorMin=rim.anchorMax=rim.pivot=new Vector2(0,1);rim.GetComponent<Image>().raycastTarget=false;
                int position=optionId.StartsWith("stack:",StringComparison.Ordinal)?Array.FindIndex(entries,e=>e.id==entry.id)+1:0;
                if(position>0) {
                    var label=Label(rim,"StackPosition",StackPositionLabel(position),new Vector2(0,ordinalHeight*.5f+26),new Vector2(244,ordinalHeight),17,Paper);label.raycastTarget=false;
                    label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f,1);
                }
                var tile=Panel(rim,selecting?(optionId.StartsWith("stack:",StringComparison.Ordinal)?"StackListTarget_"+entry.id:"StackListChoice_"+option):"Card",Vector2.zero,new Vector2(240,335),Ink);
                PresentSelectionGlow(rim,tile,selected);
                Sprite art=null;if(entry.source!=null)TryBattlefieldArt(entry.source,out art);var image=tile.GetComponent<Image>();image.sprite=art;image.color=art!=null?Color.white:Ink;image.preserveAspect=true;
                if(art==null) {
                    string name=entry.source?.faceDown==true?TapPaymentText("面朝下的牌","Face-down card","裏向きのカード"):entry.source!=null?GamePreferences.CardFaceName(entry.source.name):option>=0?RequestOptionLabel(view.request.options[option]):"";
                    var player=(view.players??Array.Empty<ForgePlayerView>()).FirstOrDefault(p=>optionId=="player:"+p.targetId);
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
                tile.gameObject.AddComponent<ForgeBattlefieldCardPointer>().Configure(()=>ShowStackHover(hover),LeaveInspectCard,null,false);
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
            int reset=OptionIndex("reset-targets");
            var restart=BindDecisionButton(window,"Reset",TapPaymentText("重新选择","Reset","選び直す"),"reset-targets",new Vector2(94,-height*.5f+26));
            restart.interactable=canAct&&!submitted&&reset>=0&&!view.request.options[reset].disabled;
            if(targetOrigin!=null)targetOrigin.gameObject.SetActive(false);if(targetArrow!=null)targetArrow.SetVisible(false);}
    }
}
