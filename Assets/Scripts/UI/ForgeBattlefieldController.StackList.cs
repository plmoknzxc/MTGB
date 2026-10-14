using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private const int CrowdedStackCount=3;
    private bool stackCollapsed,stackBrowseOpen;
    private Button stackViewButton,stackCollapseButton;
    private RectTransform stackList;
    private string stackListKey,stackListContext,stackListDismissedContext;

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
                stackBrowseOpen=true;stackListKey=null;PresentStackList();RefreshBattlefieldViewControls();
            });
        }
        foreach(var button in new[]{stackCollapseButton,stackViewButton}) {
            var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,1);
            rect.anchoredPosition=new Vector2(button==stackCollapseButton?-6:-52,0);
        }
        stackCollapseButton.gameObject.SetActive(count>0);stackViewButton.gameObject.SetActive(count>=CrowdedStackCount);
        stackCollapseButton.GetComponentInChildren<StackControlIcon>().SetKind(stackCollapsed?StackControlIcon.Kind.Left:StackControlIcon.Kind.Right);
        // Move the masked viewport (cards and scroll together), keeping the
        // controls on screen. Children cannot be clicked while retracted.
        var group=viewport.GetComponent<CanvasGroup>();if(group==null)group=viewport.gameObject.AddComponent<CanvasGroup>();
        group.interactable=group.blocksRaycasts=!stackCollapsed;
        float offscreen=column.rect.width+400;
        ForgeUiMotion.On(viewport).MoveTo(new Vector2(stackCollapsed?offscreen:0,-40),viewport.sizeDelta,seconds:.28f);
        if(stackScrollbar!=null)stackScrollbar.gameObject.SetActive(!stackCollapsed&&stackContent.rect.height>height);
    }

    private bool HasCrowdedStackTargets => (view?.stackItems??Array.Empty<ForgeStackItem>()).Count(e=>OptionIndex("stack:"+e.id)>=0)>=CrowdedStackCount
        &&IsTargeting&&(view.request.options??Array.Empty<ForgeOption>()).Any(o=>o.id.StartsWith("stack:",StringComparison.Ordinal));
    private string StackTargetContext => BattlefieldDecisionContext;
    private void CloseStackList()
    {
        stackBrowseOpen=false;stackListDismissedContext=StackTargetContext;
        if(stackList!=null)stackList.gameObject.SetActive(false);
        RestoreBattlefieldWindows();PresentTargeting();
    }
    private void PresentStackList()
    {
        var entries=view?.stackItems??Array.Empty<ForgeStackItem>();
        string context=StackTargetContext;
        if(stackListContext!=context){stackBrowseOpen=false;stackListDismissedContext=null;stackListKey=null;stackListContext=context;}
        bool selecting=!stackBrowseOpen&&HasCrowdedStackTargets&&stackListDismissedContext!=context;
        if(selecting)entries=entries.Where(e=>OptionIndex("stack:"+e.id)>=0 && !view.request.options[OptionIndex("stack:"+e.id)].disabled).ToArray();
        if(entries.Length<CrowdedStackCount||(!stackBrowseOpen&&!selecting)) {
            if(stackList!=null)stackList.gameObject.SetActive(false);return;
        }
        // Center the list on the battlefield, independently of the right stack dock.
        float width=Mathf.Min(Mathf.Min(surface.rect.width-520,1200),entries.Length*264+52);
        if(stackList==null)stackList=Panel(surface,"StackList",Vector2.zero,new Vector2(width,468),new Color(.035f,.065f,.095f,.97f));
        stackList.gameObject.SetActive(true);stackList.SetAsLastSibling();stackList.sizeDelta=new Vector2(width,468);
        stackList.anchoredPosition=Vector2.zero;
        stackList.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(1200,468,14);
        string key=ServerSnapshotJson.Serialize(view.request)+"/"+string.Join("|",entries.Select(e=>ServerSnapshotJson.Serialize(e)))+"/"+selecting+"/"+GamePreferences.Revision+"/"+width;
        if(stackListKey!=key) {
            float scrollPosition=stackList.GetComponentInChildren<ScrollRect>()?.horizontalNormalizedPosition??0;
            bool sameContext=stackListKey!=null;
            foreach(var child in stackList.Cast<Transform>().ToArray()){child.gameObject.SetActive(false);child.SetParent(null,false);Destroy(child.gameObject);}
            stackListKey=key;
            Label(stackList,"Title",selecting?TapPaymentText("选择目标","Choose a target","対象を選択"):TapPaymentText("查看堆叠","View stack","スタックを見る"),new Vector2(0,210),new Vector2(width-140,32),21,Paper).raycastTarget=false;
            var close=StackIcon(stackList,"Close",StackControlIcon.Kind.Close,CloseStackList);((RectTransform)close.transform).anchoredPosition=new Vector2(width*.5f-30,210);
            var content=Scroll(stackList,"Cards",new Vector2(0,5),new Vector2(width-40,364));
            var scroll=content.parent.GetComponent<ScrollRect>();scroll.horizontal=true;scroll.vertical=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);content.sizeDelta=new Vector2(Mathf.Max(width-40,entries.Length*264+12),364);
            for(int i=0;i<entries.Length;i++) {
                var entry=entries[i];
                int option=OptionIndex("stack:"+entry.id);long expected=requestId;
                bool legal=selecting&&option>=0&&!view.request.options[option].disabled;
                bool selected=legal&&view.request.options[option].selected;
                var rim=Panel(content,"Entry_"+entry.id,new Vector2(12+i*264,-12),new Vector2(244,339),selected?Gold:legal?new Color(.1f,.9f,1):new Color(.38f,.49f,.56f));
                if(entry.kind!="spell")rim.GetComponent<Image>().sprite=AbilityShape();
                rim.anchorMin=rim.anchorMax=rim.pivot=new Vector2(0,1);rim.GetComponent<Image>().raycastTarget=false;
                var tile=Panel(rim,selecting?"StackListTarget_"+entry.id:"Card",Vector2.zero,new Vector2(240,335),Ink);
                Sprite art=null;TryBattlefieldArt(entry.source,out art);var image=tile.GetComponent<Image>();image.sprite=art;image.color=art!=null?Color.white:Ink;image.preserveAspect=true;
                if(art==null)Label(tile,"Fallback",GamePreferences.CardFaceName(entry.source?.name),Vector2.zero,new Vector2(218,80),18,Paper).raycastTarget=false;
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
            foreach(var b in stackList.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("StackListTarget_"))) {
                int option=OptionIndex("stack:"+b.name.Substring("StackListTarget_".Length));
                b.interactable=canAct&&!submitted&&option>=0&&!view.request.options[option].disabled;
            }
            BindDecisionButton(stackList,"Confirm",TapPaymentText("确认","Confirm","確認"),"ok",new Vector2(-190,-208));
            BindDecisionButton(stackList,"Reset",TapPaymentText("重新选择","Reset","選び直す"),"reset-targets",new Vector2(0,-208));
            var cancel=BindDecisionButton(stackList,"Cancel",TapPaymentText("取消","Cancel","キャンセル"),"cancel",new Vector2(190,-208));cancel.gameObject.SetActive(OptionIndex("cancel")>=0);
            if(targetOrigin!=null)targetOrigin.gameObject.SetActive(false);if(targetArrow!=null)targetArrow.SetVisible(false);}
    }
}
