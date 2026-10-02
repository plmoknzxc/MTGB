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
            SetText("InspectPanel/Rules",item.entry.description);
            LayoutInspectPanel(item.source,item.tile);
        }
    }
    private void PresentAbilityOrder()
    {
        if(view?.awaitingInput!=true || view.request.kind!="OrderAbilities") {if(abilityOrder!=null)abilityOrder.gameObject.SetActive(false);return;}
        var ordered=view.request.orderedAbilities??Array.Empty<ForgeStackItem>();
        var options=view.request.options.Where(o=>o.id.StartsWith("order:")).ToArray();
        var entries=ordered.Concat(options.Select(o=>o.ability??new ForgeStackItem{description=o.label})).ToArray();
        float width=Mathf.Min(surface.rect.width-100,Mathf.Max(540,entries.Length*238+36));
        if(abilityOrder==null)abilityOrder=Panel(surface,"AbilityOrder",Vector2.zero,new Vector2(width,548),Ink);
        abilityOrder.gameObject.SetActive(true);abilityOrder.SetAsLastSibling();
        string key=string.Join("|",entries.OrderBy(e=>e.id).Select(e=>e.id+":"+e.description))+"/"+width;
        bool rebuild=abilityOrderKey!=key;
        if(rebuild){Clear(abilityOrder);abilityOrderKey=key;}
        abilityOrder.sizeDelta=new Vector2(width,548);
        abilityOrder.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(600,548,14);
        if(rebuild){
            Label(abilityOrder,"Title","排列结算顺序",new Vector2(0,235),new Vector2(width-24,34),25,Paper);
            Label(abilityOrder,"Direction","先结算 → 后结算 · 点击异能依次排列",new Vector2(0,198),new Vector2(width-24,28),18,Paper);
            var added=Scroll(abilityOrder,"OrderScroll",new Vector2(0,-5),new Vector2(width-28,360));
            var scroll=added.parent.GetComponent<ScrollRect>();scroll.horizontal=true;scroll.vertical=false;
            added.anchorMin=added.anchorMax=added.pivot=new Vector2(0,1);added.sizeDelta=new Vector2(Mathf.Max(width-28,entries.Length*238),360);
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
            tile.anchoredPosition=new Vector2(10+i*238,-32);
            tile.GetComponent<Image>().color=chosen?Gold:new Color(.25f,.48f,.57f);
            tile.Find("Order").GetComponent<Text>().text=chosen?(i+1).ToString():"";
            int index=chosen?-1:OptionIndex(options[i-ordered.Length].id);long captured=requestId;
            var button=tile.GetComponent<Button>();button.interactable=!chosen && canAct && !submitted;button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>Choose(index,captured));
        }
        BindDecisionButton(abilityOrder,"ConfirmOrder","确认","confirm-order",new Vector2(-94,-234));
        BindDecisionButton(abilityOrder,"ResetOrder","重新排序","reset-order",new Vector2(94,-234));
    }
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
        const float cardWidth=240,cardHeight=335,rowHeight=389,spreadX=76,spreadY=48;
        var entries=view?.stackItems ?? Array.Empty<ForgeStackItem>();
        var text=view?.stack ?? Array.Empty<string>();
        int count=Math.Max(entries.Length,text.Length);
        float spread=Math.Min(Math.Max(0,count-1),4)*spreadX;
        float columnWidth=288+spread;
        float contentHeight=cardHeight+Math.Max(0,count-1)*spreadY+54;
        var opponent=(view?.players??Array.Empty<ForgePlayerView>()).FirstOrDefault(p=>p.seat!=seat);
        int emblemCount=(opponent?.emblems?.Length??0)+(opponent?.effectMarkers?.Length??0);
        int emblemRows=(emblemCount+1)/2;
        float stackTop=250+emblemRows*96;
        float viewportHeight=Mathf.Min(Mathf.Min(620,Mathf.Max(408,contentHeight)),Mathf.Max(220,surface.rect.height-stackTop-230));
        var column=(RectTransform)Node("StackColumn");
        column.sizeDelta=new Vector2(columnWidth,viewportHeight+40);
        // During a response, keep the casting preview and stack side by side.
        ForgeUiMotion.On(column).MoveTo(new Vector2(HasCastingPrompt?-340:-28,-stackTop),column.sizeDelta);
        var viewport=(RectTransform)Node("StackColumn/Viewport");
        viewport.anchorMin=viewport.anchorMax=viewport.pivot=new Vector2(.5f,1);
        viewport.anchoredPosition=new Vector2(0,-40);viewport.sizeDelta=new Vector2(columnWidth,viewportHeight);
        viewport.GetComponent<Image>().color=new Color(.035f,.05f,.07f,.76f);
        viewport.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(296,viewportHeight,10);
        viewport.GetComponent<Mask>().showMaskGraphic=false;
        var scroll=viewport.GetComponent<ScrollRect>();
        scroll.viewport=viewport;scroll.content=stackContent;scroll.horizontal=false;scroll.vertical=true;
        scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=38;
        ArenaTextSize("StackColumn/Header",20);
        SceneText("StackColumn/Header").alignment=TextAnchor.MiddleLeft;
        ((RectTransform)Node("StackColumn/Header")).sizeDelta=new Vector2(280,28);
        SetText("StackColumn/Header","堆叠  "+(view?.stackCount ?? 0));
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
        var retained = new HashSet<string>();
        for(int i=0;i<count;i++)
        {
            var source=i<entries.Length?entries[i].source:null;
            string id=i<entries.Length?"id:"+entries[i].id:"text:"+i+":"+text[i];
            retained.Add(id);
            bool created=!stackCards.TryGetValue(id,out var item);
            // Reference 04:17 / 04:27: top object in front, earlier objects peeking
            // out behind it, rather than a list of full cards separated vertically.
            Vector2 position=new Vector2(spread*.5f-Math.Min(i,4)*spreadX,-i*spreadY-8);
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
            ForgeUiMotion.On(item.row).MoveTo(position,item.row.sizeDelta,seconds:.26f);
            int targetOption=item.entry!=null?OptionIndex("stack:"+item.entry.id):-1;
            bool targetSelected=targetOption>=0 && view.request.options[targetOption].selected;
            item.rim.color=targetSelected?Gold:IsTargeting && targetOption>=0?new Color(.1f,.9f,1):i==0?new Color(.67f,.76f,.8f,.85f):new Color(.28f,.36f,.42f,.8f);
            string printing=source!=null && !source.faceDown?source.printingKey:"";
            if(created || item.printing!=printing+"/"+source?.backFace)
            {
                item.printing=printing+"/"+source?.backFace;Sprite art=null;
                if(!string.IsNullOrEmpty(printing) && catalog.TryGetValue(printing,out var definition))CardImageLibrary.TryLoadFullCard(definition,source.backFace?1:0,out art);
                item.image.sprite=art;item.image.color=art!=null?Color.white:Ink;
            }
            item.fallback.text=source?.name ?? (i<text.Length?text[i]:"堆叠");
            item.fallback.gameObject.SetActive(item.image.sprite==null && !ability);
            PresentAbilityFace(item.tile,source,item.entry?.description,item.entry?.kind,ability);
            PresentPreparedSpellFace(item.tile,source,new Vector2(cardWidth,cardHeight),item.entry?.hasXValue==true);
            if(item.tile.Find("PreparedSpellFace")?.gameObject.activeSelf==true)item.fallback.gameObject.SetActive(false);
            item.glow.Configure(ability,i==0);
            item.glow.transform.SetAsLastSibling();
            string owner=i<entries.Length?(entries[i].controllerSeat==seat?"你":"对手"):"";
            item.caption.text=(i+1)+" · "+owner+(item.entry?.copied==true?" · 复制":"")+(targetSelected?" · ✓":i==0?" · 先结算":"");
            item.caption.rectTransform.anchoredPosition=new Vector2(0,-cardHeight-Math.Max(0,count-1)*spreadY-20);
            item.caption.gameObject.SetActive(false);
            item.caption.resizeTextForBestFit=true;item.caption.resizeTextMinSize=10;item.caption.resizeTextMaxSize=14;
            item.caption.horizontalOverflow=HorizontalWrapMode.Overflow;
            item.caption.color=i==0?new Color(.7f,.85f,.92f):Paper;
            if(created && source?.id!=castingCardId)AnimateStackArrival(item,i<entries.Length?entries[i].controllerSeat:seat);
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
        if(previousFace!=null && catalog.TryGetValue(item.source.printingKey??"",out var definition) && CardImageLibrary.TryLoadFullCard(definition,0,out var front))
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
