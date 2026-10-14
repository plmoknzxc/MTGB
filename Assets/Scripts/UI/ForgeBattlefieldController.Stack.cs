using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private Scrollbar stackScrollbar;
    private string stackLayoutKey;
    private sealed class StackCardVisual
    {
        public RectTransform row, tile;
        public Image image, rim;
        public Text fallback, caption;
        public ForgeCardView source;
        public ForgeStackItem entry;
        public string printing;
        public ForgeStackGlow glow;
    }
    private readonly Dictionary<string, StackCardVisual> stackCards = new Dictionary<string, StackCardVisual>();
    private RectTransform abilityOrder;
    private string abilityOrderKey;
    private StackCardVisual hoveredStack;
    private void ShowStackHover(StackCardVisual item)
    {
        if(item.source==null)return;
        InspectIntegrated(item.source);
        inspectedCardId=0; // Stack ability details must not be replaced by its source land.
        if(item.entry!=null && item.entry.kind!="spell") {
            SetText("InspectPanel/Rules",AbilityEffectText(item.source,item.entry.description));
            LayoutInspectPanel(item.source,item.tile);
        }
        if(HasSpellChoices(item.entry)) {
            SetText("InspectPanel/Rules",ChosenSpellHeading(item.entry)+"\n"+ChosenSpellRules(item.entry));
            LayoutInspectPanel(item.source,item.tile);
            var art=Node("InspectPanel/Art").GetComponent<Image>();
            PresentChosenSpellFace((RectTransform)art.transform,item.entry,art.sprite);
        }
        else {
            var art=Node("InspectPanel/Art").GetComponent<Image>();
            PresentChosenSpellFace((RectTransform)art.transform,item.entry,art.sprite);
        }
        inspectedStackTargets=item.entry?.targets;
        LayoutInspectPanel(item.source,item.tile);
    }
    private void PresentAbilityOrder()
    {
        bool optional=view?.awaitingInput==true&&view.request.selectionPurpose=="optionalReplacement";
        if(view?.awaitingInput!=true || view.request.kind!="OrderAbilities"&&!optional) {if(abilityOrder!=null)abilityOrder.gameObject.SetActive(false);return;}
        var ordered=view.request.orderedAbilities??Array.Empty<ForgeStackItem>();
        bool replacement=view.request.selectionPurpose=="replacementOrder"||optional;
        var options=view.request.options.Where(o=>o.id.StartsWith("order:")).ToArray();
        var entries=ordered.Concat(options.Select(o=>o.ability??new ForgeStackItem{description=o.label})).ToArray();
        bool localOrder=view.request.options.Any(o=>o.id=="submit-order");
        if(localOrder) {
            if(localAbilityOrderRequest!=requestId) {localAbilityOrderRequest=requestId;localAbilityOrder=entries.Select(e=>e.id).ToList();}
            var byId=entries.ToDictionary(e=>e.id);entries=localAbilityOrder.Where(byId.ContainsKey).Select(id=>byId[id]).ToArray();
        }
        float width=Mathf.Min(surface.rect.width-100,Mathf.Max(540,entries.Length*238+36));
        if(abilityOrder==null)abilityOrder=Panel(surface,"AbilityOrder",Vector2.zero,new Vector2(width,548),Ink);
        abilityOrder.gameObject.SetActive(true);abilityOrder.SetAsLastSibling();
        string key=string.Join("|",entries.OrderBy(e=>e.id).Select(e=>e.id+":"+e.description+":"+ServerSnapshotJson.Serialize(e.source)))+"/"+width+"/"+replacement+"/"+optional+"/"+GamePreferences.Revision;
        bool rebuild=abilityOrderKey!=key;
        if(rebuild){
            // Destroy is deferred. Detach old controls so Find cannot reuse a
            // button that will disappear at the end of this frame.
            foreach(var child in abilityOrder.Cast<Transform>().ToArray()){child.gameObject.SetActive(false);child.SetParent(null,false);Destroy(child.gameObject);}
            abilityOrderKey=key;
        }
        abilityOrder.sizeDelta=new Vector2(width,548);
        abilityOrder.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(600,548,14);
        if(rebuild){
            var title=Label(abilityOrder,"Title",optional?TapPaymentText("使用替代效果？","Apply replacement effect?","置換効果を適用しますか？"):replacement?TapPaymentText("排列替代效果","Order replacement effects","置換効果の順序"):TapPaymentText("排列结算顺序","Order ability resolution","能力の解決順序"),new Vector2(0,235),new Vector2(width-24,34),25,Paper);
            var direction=Label(abilityOrder,"Direction",optional?"":replacement?TapPaymentText("先应用 → 后应用 · 选择后确认","Apply first → last · Choose, then confirm","適用順に選択 · 選択後に確認"):TapPaymentText("先结算 → 后结算 · 拖曳调整顺序","Resolve first → last · Drag to reorder","先に解決 → 後に解決 · ドラッグで並べ替え"),new Vector2(0,198),new Vector2(width-24,28),18,Paper);
            (title.GetComponent<PreferenceText>()??title.gameObject.AddComponent<PreferenceText>()).SetBaseSize(21);(direction.GetComponent<PreferenceText>()??direction.gameObject.AddComponent<PreferenceText>()).SetBaseSize(16);title.resizeTextForBestFit=false;direction.resizeTextForBestFit=false;title.rectTransform.sizeDelta=new Vector2(width-24,48);title.rectTransform.anchoredPosition=new Vector2(0,244);direction.rectTransform.sizeDelta=new Vector2(width-24,40);direction.rectTransform.anchoredPosition=new Vector2(0,197);
            var added=Scroll(abilityOrder,"OrderScroll",new Vector2(0,-5),new Vector2(width-28,360));
            var scroll=added.parent.GetComponent<ScrollRect>();scroll.horizontal=true;scroll.vertical=false;
            added.anchorMin=added.anchorMax=added.pivot=new Vector2(0,1);added.sizeDelta=new Vector2(Mathf.Max(width-28,entries.Length*238),360);
        }
        if(optional){
            string affected=view.request.replacementAffectedPlayerSeat>0?PlayerDisplayName(view.request.replacementAffectedPlayerSeat):view.request.replacementAffectedCard!=null?GamePreferences.CardFaceName(view.request.replacementAffectedCard.name):"";
            SetNamedPlayerText(abilityOrder.Find("Direction").GetComponent<Text>(),TapPaymentText("受影响对象：","Affected: ","影響を受ける対象：")+affected);
        }
        var content=abilityOrder.GetComponentInChildren<ScrollRect>().content;
        for(int i=0;i<entries.Length;i++) {
            var entry=entries[i];bool chosen=i<ordered.Length;
            var tile=content.Find("OrderedAbility_"+entry.id) as RectTransform;
            if(tile==null){
                tile=Panel(content,"OrderedAbility_"+entry.id,Vector2.zero,new Vector2(216,306),Gold);
                tile.anchorMin=tile.anchorMax=tile.pivot=new Vector2(0,1);
                tile.GetComponent<Image>().sprite=AbilityShape();
                var face=Panel(tile,"Face",Vector2.zero,new Vector2(211,301),Ink);face.GetComponent<Image>().raycastTarget=false;
                PresentAbilityFace(face,entry.source,entry.description,entry.kind,true);
                Label(tile,"Order","",new Vector2(0,171),new Vector2(40,28),23,Gold).raycastTarget=false;
                tile.gameObject.AddComponent<Button>().targetGraphic=tile.GetComponent<Image>();
            }
            tile.anchoredPosition=new Vector2(optional?(content.rect.width-216)/2:10+i*238,-32);
            tile.GetComponent<Image>().color=chosen?Gold:new Color(.25f,.48f,.57f);
            tile.Find("Order").gameObject.SetActive(false);
            int index=chosen?-1:OptionIndex(options[i-ordered.Length].id);long captured=requestId;
            var button=tile.GetComponent<Button>();button.interactable=!chosen && canAct && !submitted;button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>Choose(index,captured));
            if(localOrder) {
                button.onClick.RemoveAllListeners();button.interactable=canAct&&!submitted;
                var drag=tile.GetComponent<AbilityOrderDrag>()??tile.gameObject.AddComponent<AbilityOrderDrag>();
                drag.Id=entry.id;drag.CanDrag=canAct&&!submitted;
                drag.Move=(id,to)=>{if(!canAct||submitted||requestId!=captured)return;int at=localAbilityOrder.IndexOf(id);to=Mathf.Min(to,localAbilityOrder.Count-1);if(at==to||at<0)return;localAbilityOrder.RemoveAt(at);localAbilityOrder.Insert(to,id);PresentAbilityOrder();};
                drag.Snap=PresentAbilityOrder;
                // The rule text's vertical scroller must not consume the card
                // drag intended to move this ability in the queue.
                foreach(var ruleScroll in tile.GetComponentsInChildren<ScrollRect>())ruleScroll.enabled=false;
            }
        }
        var confirm=BindDecisionButton(abilityOrder,"ConfirmOrder",TapPaymentText("确认","Confirm","確認"),optional?"ok":"confirm-order",new Vector2(-94,-234));
        var reset=BindDecisionButton(abilityOrder,"ResetOrder",TapPaymentText("重新排序","Reset order","順序をリセット"),optional?"cancel":"reset-order",new Vector2(94,-234));
        if(localOrder) {
            confirm.interactable=reset.interactable=canAct&&!submitted;
            long orderRequest=requestId;
            confirm.onClick.RemoveAllListeners();confirm.onClick.AddListener(()=>{
                if(viewingBattlefield||!canAct||submitted||submitOrder==null||requestId!=orderRequest)return;
                if(service!=null&&(service.ForgeView?.forge?.request?.requestId!=orderRequest||!service.CanSubmitRuleAction))return;
                submitted=true;DisableIntegratedChoices();submitOrder(OptionIndex("submit-order"),localAbilityOrder.ToArray());
            });
            reset.onClick.RemoveAllListeners();reset.onClick.AddListener(()=>{localAbilityOrderRequest=0;PresentAbilityOrder();});
        }
        if(replacement){confirm.GetComponentInChildren<Text>().text=optional?TapPaymentText("使用","Apply","適用"):TapPaymentText("确认","Confirm","確認");reset.GetComponentInChildren<Text>().text=optional?TapPaymentText("不使用","Decline","適用しない"):TapPaymentText("重新排序","Reset order","順序をリセット");}
    }
    private long localAbilityOrderRequest;
    private List<int> localAbilityOrder=new List<int>();
    private void AddStackScrollIndicator(RectTransform content)
    {
        var scroll=content.parent.GetComponent<ScrollRect>();
        var track=Panel(content.parent,"Scrollbar",Vector2.zero,new Vector2(5,scroll.viewport.rect.height),new Color(.12f,.18f,.21f));
        track.anchorMin=track.anchorMax=track.pivot=new Vector2(1,1);
        var handle=Panel(track,"Handle",Vector2.zero,new Vector2(5,40),new Color(.48f,.67f,.73f));
        handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=handle.offsetMax=Vector2.zero;
        var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=handle.GetComponent<Image>();bar.direction=Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;scroll.scrollSensitivity=35;
    }
    private void PresentStackCards()
    {
        const float cardWidth=240,cardHeight=335,rowHeight=389;
        var entries=view?.stackItems ?? Array.Empty<ForgeStackItem>();
        var text=view?.stack ?? Array.Empty<string>();
        int count=Math.Max(entries.Length,text.Length);
        // Open a small fan; a long stack uses the compact fan and overview.
        bool small=count<=3;
        float spread=small?Mathf.Max(0,count-1)*52:Mathf.Min((count-1)*22,140);
        float depth=small?Mathf.Max(0,count-1)*18:Mathf.Min((count-1)*5,36);
        float angle=small?Mathf.Max(0,count-1)*6:Mathf.Min((count-1)*2.5f,15);
        float columnWidth=288+spread;
        float contentHeight=cardHeight+depth+(small?24:54);
        // Requests change the controls, never the stack's battlefield dock.
        const float stackTop=300;
        float viewportHeight=Mathf.Min(Mathf.Min(620,Mathf.Max(408,contentHeight)),Mathf.Max(220,surface.rect.height-stackTop-230));
        var column=(RectTransform)Node("StackColumn");
        column.sizeDelta=new Vector2(columnWidth,viewportHeight+40);
        ForgeUiMotion.On(column).Cancel();
        column.anchoredPosition=new Vector2(-28,-stackTop);
        var viewport=(RectTransform)Node("StackColumn/Viewport");
        viewport.anchorMin=viewport.anchorMax=viewport.pivot=new Vector2(.5f,1);
        viewport.sizeDelta=new Vector2(columnWidth,viewportHeight);
        // The fan includes empty space beside and below its cards. Only the
        // visible cards and scrollbar should intercept battlefield input there.
        viewport.GetComponent<Image>().raycastTarget=false;
        viewport.GetComponent<Image>().color=new Color(.035f,.05f,.07f,.76f);
        viewport.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(296,viewportHeight,10);
        viewport.GetComponent<Mask>().showMaskGraphic=false;
        var scroll=viewport.GetComponent<ScrollRect>();
        scroll.viewport=viewport;scroll.content=stackContent;scroll.horizontal=false;scroll.vertical=true;
        scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=38;
        ArenaTextSize("StackColumn/Header",20);
        SceneText("StackColumn/Header").alignment=TextAnchor.MiddleLeft;
        ((RectTransform)Node("StackColumn/Header")).sizeDelta=new Vector2(280,28);
        SetText("StackColumn/Header","");
        PresentStackControls(column,count,viewport,viewportHeight);
        string key=string.Join("|",entries.Select(e=>e.id+":"+e.description))+"/"+string.Join("|",text);
        stackContent.anchorMin=stackContent.anchorMax=stackContent.pivot=new Vector2(.5f,1);
        stackContent.sizeDelta=new Vector2(columnWidth-16,Math.Max(viewportHeight,contentHeight));
        if(key!=stackLayoutKey){scroll.StopMovement();stackContent.anchoredPosition=Vector2.zero;stackLayoutKey=key;}
        if(stackScrollbar==null)
        {
            var track=Panel(column,"StackScrollbar",Vector2.zero,new Vector2(6,viewportHeight-16),new Color(.15f,.2f,.24f,.8f));
            track.anchorMin=track.anchorMax=track.pivot=new Vector2(1,1);track.anchoredPosition=new Vector2(-6,-48);
            var handle=Panel(track,"Handle",Vector2.zero,new Vector2(6,50),new Color(.52f,.64f,.7f,.9f));
            handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=handle.offsetMax=Vector2.zero;
            stackScrollbar=track.gameObject.AddComponent<Scrollbar>();stackScrollbar.handleRect=handle;stackScrollbar.targetGraphic=handle.GetComponent<Image>();
            stackScrollbar.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=stackScrollbar;
            scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        }
        ((RectTransform)stackScrollbar.transform).sizeDelta=new Vector2(6,viewportHeight-16);
        var barGroup=stackScrollbar.GetComponent<CanvasGroup>();if(barGroup==null)barGroup=stackScrollbar.gameObject.AddComponent<CanvasGroup>();
        barGroup.alpha=stackCollapsed?0:1;barGroup.blocksRaycasts=!stackCollapsed;
        var retained = new HashSet<string>();
        for(int i=0;i<count;i++)
        {
            var source=i<entries.Length?entries[i].source:null;
            string id=i<entries.Length?"id:"+entries[i].id:"text:"+i+":"+text[i];
            retained.Add(id);
            bool created=!stackCards.TryGetValue(id,out var item);
            float fraction=count>1?i/(float)(count-1):0;
            Vector2 position=new Vector2(spread*.5f-fraction*spread,-(small?1-fraction:fraction)*depth-8);
            if(created)
            {
                item=new StackCardVisual();
                item.row=Panel(stackContent,"StackEntry_"+i,Vector2.zero,new Vector2(272,rowHeight-8),Color.clear);
                item.row.GetComponent<Image>().raycastTarget=false;
                item.row.anchorMin=item.row.anchorMax=item.row.pivot=new Vector2(.5f,1);
                item.row.anchoredPosition=position;
                var shadow=Panel(item.row,"Shadow",new Vector2(3,-cardHeight*.5f-4),new Vector2(cardWidth+6,cardHeight+6),new Color(0,0,0,.48f));
                shadow.anchorMin=shadow.anchorMax=new Vector2(.5f,1);shadow.GetComponent<Image>().raycastTarget=false;
                var rim=Panel(item.row,"Rim",new Vector2(0,-cardHeight*.5f),new Vector2(cardWidth+3,cardHeight+3),Color.white);
                rim.anchorMin=rim.anchorMax=new Vector2(.5f,1);item.rim=rim.GetComponent<Image>();item.rim.raycastTarget=false;
                item.tile=Panel(item.row,"StackCard_"+i,Vector2.zero,new Vector2(cardWidth,cardHeight),Ink);
                item.tile.anchorMin=item.tile.anchorMax=item.tile.pivot=new Vector2(.5f,1);item.tile.anchoredPosition=Vector2.zero;
                item.image=item.tile.GetComponent<Image>();item.image.preserveAspect=true;
                item.fallback=Label(item.tile,"Fallback","",new Vector2(0,-cardHeight*.5f),new Vector2(cardWidth-20,cardHeight-20),18,Paper);
                item.fallback.rectTransform.anchorMin=item.fallback.rectTransform.anchorMax=new Vector2(.5f,1);
                item.fallback.raycastTarget=false;
                var button=item.tile.gameObject.AddComponent<Button>();button.targetGraphic=item.image;button.transition=Selectable.Transition.None;
                var captured=item;button.onClick.AddListener(()=>{
                    if(IsTargeting && captured.entry!=null){Choose(OptionIndex("stack:"+captured.entry.id),requestId);return;}
                });
                item.tile.gameObject.AddComponent<ForgeBattlefieldCardPointer>().Configure(()=>{
                    hoveredStack=captured;ShowStackHover(captured);
                },()=>{hoveredStack=null;LeaveInspectCard();},null,false);
                item.glow=Rect(item.tile,"ActiveStackGlow",Vector2.zero,new Vector2(cardWidth,cardHeight)).gameObject.AddComponent<ForgeStackGlow>();
                item.caption=Label(item.tile,"Owner","",new Vector2(0,-cardHeight-20),new Vector2(252,26),16,Paper);
                item.caption.rectTransform.anchorMin=item.caption.rectTransform.anchorMax=new Vector2(.5f,1);item.caption.raycastTarget=false;
                stackCards.Add(id,item);
            }
            item.row.name="StackEntry_"+i;item.tile.name="StackCard_"+i;
            item.source=source;item.entry=i<entries.Length?entries[i]:null;
            bool ability=item.entry!=null && item.entry.kind!="spell";
            item.rim.sprite=ability?AbilityShape():null;
            item.row.Find("Shadow").GetComponent<Image>().sprite=ability?AbilityShape():null;
            ForgeUiMotion.On(item.row).MoveTo(position,item.row.sizeDelta,angle:fraction*angle,seconds:.26f);
            int targetOption=item.entry!=null?OptionIndex("stack:"+item.entry.id):-1;
            bool targetSelected=targetOption>=0 && view.request.options[targetOption].selected;
            item.rim.color=targetSelected?Gold:IsTargeting && targetOption>=0?new Color(.1f,.9f,1):i==0?new Color(.67f,.76f,.8f,.85f):new Color(.28f,.36f,.42f,.8f);
            string printing=source!=null && !source.faceDown?source.printingKey:"";
            string artKey=printing+"/"+source?.backFace+"/"+GamePreferences.Language;
            if(IsSpecializedFace(source))artKey+="/"+source.stateName+"/"+source.name;
            if(created || item.printing!=artKey)
            {
                item.printing=artKey;Sprite art=null;
                if(IsSpecializedFace(source))TryBattlefieldArt(source,out art);
                else if(!string.IsNullOrEmpty(printing) && catalog.TryGetValue(printing,out var definition))CardImageLibrary.TryLoadFullCard(definition,source.backFace?1:0,out art);
                item.image.sprite=art;item.image.color=art!=null?Color.white:Ink;
            }
            item.fallback.text=source?.name ?? (i<text.Length?text[i]:"堆叠");
            item.fallback.gameObject.SetActive(item.image.sprite==null && !ability);
            PresentAbilityFace(item.tile,source,item.entry?.description,item.entry?.kind,ability);
            Sprite chosenArt=item.image.sprite;
            if(chosenArt==null&&HasSpellChoices(item.entry))TryBattlefieldArt(source,out chosenArt);
            PresentPreparedSpellFace(item.tile,source,new Vector2(cardWidth,cardHeight));
            PresentChosenSpellFace(item.tile,item.entry,chosenArt);
            if(!ability)PresentComponentFace(item.tile,source);
            if(HasSpellChoices(item.entry))item.fallback.gameObject.SetActive(false);
            if(item.tile.Find("PreparedSpellFace")?.gameObject.activeSelf==true)item.fallback.gameObject.SetActive(false);
            item.glow.Configure(ability,i==0);
            // Follow the actual card rectangle, including its top pivot. The
            // highlight belongs to this face, never to a fan/row-sized overlay.
            item.glow.rectTransform.anchorMin=Vector2.zero;
            item.glow.rectTransform.anchorMax=Vector2.one;
            item.glow.rectTransform.offsetMin=item.glow.rectTransform.offsetMax=Vector2.zero;
            item.glow.transform.SetAsLastSibling();
            string owner=i<entries.Length?(entries[i].controllerSeat==seat?"你":"对手"):"";
            item.caption.text=(i+1)+" · "+owner+(item.entry?.copied==true?" · 复制":"")+(targetSelected?" · ✓":i==0?" · 先结算":"");
            item.caption.rectTransform.anchoredPosition=new Vector2(0,-cardHeight-20);
            item.caption.gameObject.SetActive(false);
            item.caption.resizeTextForBestFit=true;item.caption.resizeTextMinSize=10;item.caption.resizeTextMaxSize=14;
            item.caption.horizontalOverflow=HorizontalWrapMode.Overflow;
            item.caption.color=i==0?new Color(.7f,.85f,.92f):Paper;
            if(created && !stackCollapsed && source?.id!=castingCardId)AnimateStackArrival(item,i<entries.Length?entries[i].controllerSeat:seat);
        }
        foreach(var id in stackCards.Keys.Where(id=>!retained.Contains(id)).ToArray())
        {
            var item=stackCards[id];
            if(hoveredStack==item){hoveredStack=null;Node("InspectPanel").gameObject.SetActive(false);}
            item.row.gameObject.SetActive(false);Destroy(item.row.gameObject);stackCards.Remove(id);
        }
        if(hoveredStack!=null)ShowStackHover(hoveredStack);
        // Drawing order is independent of engine order and card identity.
        foreach(var item in stackCards.Values.OrderByDescending(c=>int.Parse(c.row.name.Substring("StackEntry_".Length))))item.row.SetAsLastSibling();
    }

    private void AnimateStackArrival(StackCardVisual item,int controllerSeat)
    {
        if(item.image.sprite==null)return;
        var origin=arenaCards.Values.FirstOrDefault(c=>c.root.name=="Card_"+item.source?.id);
        var previousFace=item.source?.backFace==true?stackCards.Values.FirstOrDefault(c=>c!=item&&c.source?.id==item.source.id&&c.source.backFace==false):null;
        var from=previousFace?.tile ?? origin?.face ?? (controllerSeat==seat?ownHand:otherHand);
        var ghost=Panel(surface,"StackArrival",Vector2.zero,new Vector2(182,254),Color.white);
        var image=ghost.GetComponent<Image>();image.sprite=item.image.sprite;image.preserveAspect=true;image.raycastTarget=false;
        ForgeUiMotion.Pose(from,surface,out var position,out var size,out var angle);
        ghost.anchoredPosition=position;ghost.localRotation=Quaternion.Euler(0,0,angle);
        if(origin!=null || previousFace!=null)ghost.sizeDelta=size;
        // Hide rim/shadow/caption too, otherwise an empty solid card flashes under the flying image.
        var group=item.row.GetComponent<CanvasGroup>();
        if(group==null)group=item.row.gameObject.AddComponent<CanvasGroup>();
        group.alpha=0;group.blocksRaycasts=false;
        Action arrive=()=>ForgeUiMotion.On(ghost).Follow(item.tile,.38f,()=>{if(group!=null){group.alpha=1;group.blocksRaycasts=true;}Destroy(ghost.gameObject);});
        if(previousFace!=null && !IsSpecializedFace(item.source) && catalog.TryGetValue(item.source.printingKey??"",out var definition) && CardImageLibrary.TryLoadFullCard(definition,0,out var front))
            StartCoroutine(RevealStackBackFace(ghost,front,item.image.sprite,arrive));
        else arrive();
    }

    private System.Collections.IEnumerator RevealStackBackFace(RectTransform ghost,Sprite front,Sprite back,Action arrive)
    {
        var image=ghost.GetComponent<Image>();
        for(float elapsed=0;elapsed<.32f;elapsed+=Time.unscaledDeltaTime){
            if(ghost==null)yield break;
            float t=elapsed/.32f;image.sprite=t<.5f?front:back;
            // The face turns edge-on and opens to the back; text is never mirrored.
            ghost.localScale=new Vector3(Mathf.Max(.015f,Mathf.Abs(Mathf.Cos(t*Mathf.PI))),1,1);
            yield return null;
        }
        if(ghost!=null){ghost.localScale=Vector3.one;image.sprite=back;arrive();}
    }
}
