using System.Linq;
using UnityEngine;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform redesignHandBackplate;
    // Expanded top meets the player counter row's top (72 + 170 + 15).
    private const float RedesignHandTrayHeight=321,RedesignHandTrayLower=64,RedesignHandRestY=-59;
    private const float RedesignOpponentHandStrip=90;
    private const float RedesignSourceGap=4,RedesignSourceGutter=8;
    private const float RedesignMinFaceWidth=144,LocalHandMinFaceWidth=112,HiddenHandMinFaceWidth=96,RedesignCardGap=8;
    private float redesignOwnWidth=300,redesignOtherWidth=300,redesignOwnLane,redesignOtherLane;
    // Both fans can extend into the journal's vertical span, including the lifted brow of a hovered card.
    private float HandLaneStart => battleSidebar!=null ? SidebarOpenRight+16 : 380;
    private float RedesignHandMaximumWidth => Mathf.Max(1,(redesignFooterLeft>0?redesignFooterLeft:surface.rect.width-336)-HandLaneStart-12);
    private float RedesignOtherHandMaximumWidth => Mathf.Max(1,surface.rect.width-HandLaneStart-RedesignStackWidth-44);
    private float RedesignHandWidth => Mathf.Min(redesignOwnWidth,RedesignHandMaximumWidth);
    private float RedesignOtherHandWidth => Mathf.Min(redesignOtherWidth,RedesignOtherHandMaximumWidth);
    // Bring the whole card above the screen edge when browsing a dense source.
    private float ExpandedSourceLift => redesignHandExpanded && (IsMulliganChoice||ownFan.browseSources||redesignOwnSections.Values.Any(s=>s.slots.Count>s.pageSize)) ? RedesignHandTrayLower : 0;
    private float RedesignHandLeft(bool other)=>HandLaneStart+((other?RedesignOtherHandMaximumWidth:RedesignHandMaximumWidth)-(other?RedesignOtherHandWidth:RedesignHandWidth))*.5f;

    private static int RedesignPublicPageSize(float sectionWidth) =>
        Mathf.Max(1,Mathf.FloorToInt((Mathf.Max(1,sectionWidth-RedesignSourceGutter)+8)/128));

    private static float RedesignPageStep(float captionWidth) => Mathf.Clamp(captionWidth-168,24,34);

    private static float HandGalleryMinimum(string zone,bool hiddenHand) =>
        zone!="Hand"?RedesignMinFaceWidth:hiddenHand?HiddenHandMinFaceWidth:LocalHandMinFaceWidth;

    private static int RedesignGalleryCapacity(float width,float minFace)
    {
        float space=width-RedesignSourceGutter+RedesignCardGap;
        int capacity=Mathf.Max(1,Mathf.FloorToInt(space/(RedesignMinFaceWidth+RedesignCardGap)));
        if(capacity>=7)return capacity;
        return Mathf.Max(capacity,Mathf.Min(7,Mathf.FloorToInt(space/(minFace+RedesignCardGap))));
    }

    private void TurnSourcePage(HandFanState fan, RedesignHandSection section, int direction)
    {
        int page=Mathf.Clamp(section.page+direction,0,section.LastPage);
        if(page==section.page)return;
        section.page=page;
        if(fan.opponent)SetRedesignOtherHandExpanded(true);else SetRedesignHandExpanded(true);
        ApplyRedesignHandOffset(fan,0,true);
    }

    private void PrepareSourcePager(HandFanState fan,RectTransform tabs,float width,float height,float rightInset)
    {
        if(fan.sourcePager==null){
            fan.sourcePager=Rect(tabs,"SourcePages",Vector2.zero,Vector2.one);
            void Turn(int direction)=>TurnSourcePage(fan,RedesignSections(fan)[fan.browsedSource],direction);
            fan.sourcePrevious=StackIcon(fan.sourcePager,"Previous",StackControlIcon.Kind.Left,()=>Turn(-1));
            fan.sourceNext=StackIcon(fan.sourcePager,"Next",StackControlIcon.Kind.Right,()=>Turn(1));
            StyleRedesignSourceButton(fan.sourcePrevious);
            StyleRedesignSourceButton(fan.sourceNext);
            fan.sourceRange=Label(fan.sourcePager,"Range","",Vector2.zero,Vector2.one,15,HudText);
            fan.sourceRange.alignment=TextAnchor.MiddleCenter;
            fan.sourceRange.raycastTarget=false;
            FitPhaseLabel(fan.sourceRange);
        }
        var right=new Vector2(1,.5f);
        RedesignPlace(fan.sourcePager,right,right,Vector2.left*rightInset,new Vector2(width,height));
        float arrow=Mathf.Min(32,width*.24f);
        foreach(var button in new[]{fan.sourcePrevious,fan.sourceNext}){
            var rect=(RectTransform)button.transform;
            rect.sizeDelta=new Vector2(arrow,30);
            rect.anchoredPosition=new Vector2((button==fan.sourceNext?1:-1)*(width*.5f-arrow*.5f-2),0);
        }
        fan.sourceRange.rectTransform.sizeDelta=new Vector2(width-2*arrow-6,height);
        fan.sourcePager.SetAsLastSibling();
    }

    private void LayoutRedesignHand(RectTransform parent, ForgeCardView[] cards, HandFanState fan, bool choosingHand)
    {
        var zones=RedesignOrderedSources(cards).ToArray();
        foreach(var section in RedesignSections(fan).Values){
            section.slots.Clear();
            if(section.region!=null && !zones.Contains(section.zone)){
                section.page=0;
                HideRedesignElement(section.region,Vector2.zero);
            }
            if(section.divider!=null)section.divider.gameObject.SetActive(false);
        }
        fan.hits.Clear();fan.slots.Clear();
        fan.navigation=null;fan.previous=null;fan.next=null;
        fan.available=fan.opponent?RedesignOtherHandWidth:RedesignHandWidth;
        bool besideCommander=zones.Contains("Command")&&RedesignSections(fan)["Command"].pinnedCommander;
        int order=0,sectionOrder=0;
        foreach(string zone in zones){
            var section=RedesignSections(fan)[zone];var members=cards.Where(c=>RedesignSource(c)==zone).ToArray();
            if(section.region==null){
                section.region=Panel(parent,zone=="Hand"?"HandFanNavigation":"SourceNavigation_"+zone,Vector2.zero,Vector2.one,Color.clear);
                section.region.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
                section.navigation=section.region.gameObject.AddComponent<ForgeHandFanNavigation>();
                var reveal=section.region.gameObject.AddComponent<UnityEngine.UI.Button>();
                reveal.targetGraphic=section.region.GetComponent<UnityEngine.UI.Image>();reveal.transition=UnityEngine.UI.Selectable.Transition.None;
                section.region.gameObject.AddComponent<BattlefieldRedesignHandInput>().Configure(fan.opponent?ExpandRedesignOtherHand:ExpandRedesignHand);
                UnityEngine.UI.Button Arrow(string name,int direction){
                    var caption=(fan.opponent?otherSourceButtons:handSourceButtons)[zone].transform;
                    var b=Button(caption,name,"",Vector2.zero,new Vector2(36,32),()=>TurnSourcePage(fan,section,direction),Ink);
                    StyleRedesignSourceButton(b);
                    var glyph=Rect(b.transform,"Chevron",Vector2.zero,new Vector2(12,24)).gameObject.AddComponent<BattlefieldScrollChevron>();
                    glyph.direction=direction;glyph.color=Paper;glyph.raycastTarget=false;return b;
                }
                section.previous=Arrow("Previous",-1);section.next=Arrow("Next",1);
                fan.regions["Redesign_"+zone]=section.region;
                section.divider=Panel(parent,"SourceDivider_"+zone,Vector2.zero,Vector2.one,new Color(.65f,.64f,.5f,.45f));
                section.divider.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            }
            section.region.gameObject.SetActive(true);section.region.SetAsFirstSibling();
            if(HudTheme!=null){
                Color seam=HudTheme.ControlEdge;
                seam.a=.26f;
                section.divider.GetComponent<UnityEngine.UI.Image>().color=seam;
            }
            section.divider.gameObject.SetActive(sectionOrder++>0&&!fan.browseSources||section.pinnedCommander);
            float width=Mathf.Min(166,Mathf.Max(32,section.width-RedesignSourceGutter-Mathf.Max(0,members.Length-1)*12)),height=width*232/166;
            float step=Mathf.Clamp((section.width-RedesignSourceGutter-width)/Mathf.Max(1,members.Length-1),0,182);
            section.pageSize=Mathf.Max(1,members.Length);
            if(HudTheme!=null){
                int capacity=section.pinnedCommander?2:RedesignGalleryCapacity(section.width,section.minimumFaceWidth);
                section.pageSize=Mathf.Min(members.Length,capacity);
                float space=Mathf.Max(1,section.width-RedesignSourceGutter);
                width=Mathf.Min(166,(space-(section.pageSize-1)*RedesignCardGap)/section.pageSize);
                height=width*232/166;step=width+RedesignCardGap;
            }else if(zone is "Graveyard" or "Exile"){
                const float gap=8;
                float space=Mathf.Max(1,section.width-RedesignSourceGutter);
                section.pageSize=Mathf.Min(RedesignPublicPageSize(section.width),section.pageSize);
                width=Mathf.Min(166,(space-(section.pageSize-1)*gap)/section.pageSize);
                height=width*232/166;
                step=width+gap;
            }
            float total=width+(section.pageSize-1)*step;
            if(section.LastPage>0)
                section.navigation.ConfigurePaging(parent,direction=>TurnSourcePage(fan,section,direction));
            else section.navigation.Configure(parent,0,null);
            if(fan.browseSources?zone==fan.browsedSource:zone=="Hand"){fan.navigation=section.navigation;fan.previous=section.previous;fan.next=section.next;fan.travel=0;}
            int start=0,following=section.PageStart(1),page=0;
            for(int i=0;i<members.Length;i++){
                if(i==following){start=i;following=section.PageStart(++page+1);}
                var card=members[i];string key=(fan.opponent?"OtherFan:":"Hand:")+card.id;
                float left=section.center-total*.5f;
                if(fan.browseSources&&!section.pinnedCommander){
                    float pageWidth=width+(following-start-1)*step;
                    // Keep short pages centered unless they share an edge with the pinned commanders.
                    left=besideCommander
                        ?section.center+(section.width-RedesignSourceGutter)*.5f-pageWidth
                        :section.center-pageWidth*.5f;
                }
                float x=left+width*.5f+(i-start)*step;
                DrawArenaCard(parent,card,1,key,key,new Vector2(x,fan.opponent?11:-11),new Vector2(width,height),true,fan.opponent?180:0,order++);
                var item=arenaCards[key];
                if(zone=="Hand"||zone=="Command"||zone=="Companion")item.root.GetComponent<CanvasGroup>().alpha=1;
                if(fan.opponent){
                    item.root.GetComponent<CanvasGroup>().alpha=1;
                    if(!choosingHand||zone!="Hand"){
                        item.button.onClick.RemoveAllListeners();
                        if(!card.faceDown)item.button.onClick.AddListener(()=>{
                            if(item.root.GetComponent<BattlefieldRedesignHandInput>()?.ConsumeRevealClick()!=true)InspectIntegrated(card);
                        });
                    }
                    item.pointer.DisableActions();
                }
                if(worldTable==null){
                    var layer=item.face.GetComponent<BattlefieldRedesignHandLayer>()??item.face.gameObject.AddComponent<BattlefieldRedesignHandLayer>();
                    layer.Configure(item.pointer,canvas.sortingLayerID,canvas.sortingOrder+HandHoverLayerOffset);
                }
                item.pointer.ConfigurePlayRegion(section.region);item.pointer.ConfigureHandNavigation(section.navigation);
                item.pointer.ConfigureHandJournal(battleSidebar);
                var reveal=item.root.GetComponent<BattlefieldRedesignHandInput>()??item.root.gameObject.AddComponent<BattlefieldRedesignHandInput>();
                reveal.Configure(fan.opponent?ExpandRedesignOtherHand:ExpandRedesignHand);
                fan.hits.Add(item.pointer);
                var slot=new HandFanSlot{item=item,region=section.region,x=x,opacity=item.root.GetComponent<CanvasGroup>().alpha};
                fan.slots.Add(slot);section.slots.Add(slot);
            }
        }
        ApplyRedesignHandOffset(fan,0,true);
        ForgeBattlefieldCardPointer.RestoreHandOrder(parent);
    }

    private void ApplyRedesignHandOffset(float offset,bool snap)=>ApplyRedesignHandOffset(ownFan,offset,snap);

    private void ApplyRedesignHandOffset(HandFanState fan,float offset,bool snap)
    {
        if(!IsRedesignLayout||ownHand==null)return;
        var parent=fan.opponent?otherHand:ownHand;
        fan.slots.RemoveAll(s=>s.item.root==null||s.item.pointer==null||s.region==null);
        fan.available=fan.opponent?RedesignOtherHandWidth:RedesignHandWidth;
        if(fan.opponent)RedesignPlace(parent,Vector2.up,Vector2.up,new Vector2(RedesignHandLeft(true),0),new Vector2(fan.available,270));
        else{
            RedesignPlace(parent,Vector2.zero,Vector2.zero,new Vector2(RedesignHandLeft(false),RedesignHandRestY+ExpandedSourceLift-(redesignHandExpanded?0:154)),new Vector2(fan.available,270));
            ShowRedesignElement(parent,Vector2.zero);
        }
        var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=-min;
        var sections=RedesignSections(fan);
        fan.pageTrim=0;
        bool pinned=sections.TryGetValue("Command",out var commander) && commander.pinnedCommander && commander.slots.Count>0;
        if(fan.browseSources && fan.browsedSource!=null && sections.TryGetValue(fan.browsedSource,out var browsed)
            && browsed.LastPage>0 && browsed.page>=browsed.LastPage && browsed.slots.Count>0
            && browsed.slots[0].item.root!=null){
            int missing=browsed.pageSize-(browsed.count-browsed.PageStart(browsed.LastPage));
            float pitch=browsed.slots[0].item.root.rect.width+RedesignCardGap;
            // Page capacity uses the full lane. Only the frame and its hit regions contract.
            fan.pageTrim=Mathf.Max(0,Mathf.Min((fan.available-32)*.24f,Mathf.Min(fan.headerTrimBudget,missing*pitch*.45f)));
        }
        foreach(var section in sections.Values){
            if(section.region==null||section.slots.Count==0)continue;
            section.slots.RemoveAll(s=>!fan.slots.Contains(s));
            int lastPage=section.LastPage;
            section.page=Mathf.Clamp(section.page,0,lastPage);
            int first=section.PageStart(section.page),end=section.PageStart(section.page+1);
            bool visibleSource=section.pinnedCommander||!fan.browseSources||fan.browsedSource==section.zone;
            float sectionCenter=section.center-(section.pinnedCommander?fan.pageTrim*.5f:0);
            float sectionWidth=section.width-(section.pinnedCommander?0:fan.pageTrim);
            section.region.anchoredPosition=new Vector2(sectionCenter,fan.opponent?11:-11);
            section.region.sizeDelta=new Vector2(sectionWidth,232);
            section.divider.anchoredPosition=new Vector2(sectionCenter-sectionWidth*.5f-RedesignSourceGap*.5f,fan.opponent?11:-11);
            section.divider.sizeDelta=new Vector2(1,232);
            for(int i=0;i<section.slots.Count;i++){
                var slot=section.slots[i];
                var position=new Vector2(slot.x-(pinned?fan.pageTrim*.5f:0),fan.opponent?11:-11);
                slot.item.pointer.SetHandFanPose(position,fan.opponent?180:0,snap);
                bool onPage=visibleSource && i>=first && i<end;
                float exposure=onPage&&(fan.opponent||redesignHandExpanded)?1:0;
                if(slot.item.pointer.IsCastingDrag)exposure=1;
                var visibility=slot.item.root.GetComponent<CanvasGroup>();if(visibility==castingHandGroup)exposure=0;
                visibility.alpha=slot.opacity*exposure;visibility.blocksRaycasts=exposure>.02f;
                ShowRedesignElement(slot.item.root,new Vector2(0,fan.opponent?28:-28),slot.opacity*exposure,exposure>.02f);
                slot.item.pointer.SetHandFanExposure(exposure>.02f);
                slot.item.pointer.AllowHandHover=exposure>.02f;
                if(!onPage&&slot.item.pointer.IsHovered)slot.item.pointer.ResetHover();
                if(exposure>.02f){
                    var center=parent.rect.center+position;var half=slot.item.root.rect.size*.5f;
                    min=Vector2.Min(min,center-half);max=Vector2.Max(max,center+half);
                    if(fan.opponent){
                        if(slot.item.world!=null)
                            min.y=Mathf.Min(min.y,center.y-slot.item.world.HandCostExtent*slot.item.root.rect.width);
                        var turn=Quaternion.Euler(0,0,180);var corners=new Vector3[4];
                        foreach(var cost in slot.item.face.GetComponentsInChildren<LegacyCardCost>())
                            if(cost.Root!=null&&cost.Root.gameObject.activeInHierarchy)
                                foreach(var graphic in cost.Root.GetComponentsInChildren<UnityEngine.UI.Graphic>()){
                                    if(!graphic.enabled)continue;
                                    graphic.rectTransform.GetWorldCorners(corners);
                                    foreach(var corner in corners){Vector2 point=center+(Vector2)(turn*slot.item.face.InverseTransformPoint(corner));min=Vector2.Min(min,point);max=Vector2.Max(max,point);}
                                }
                    }
                }
            }
            bool sectionPager=!fan.browseSources||section.pinnedCommander;
            section.previous.gameObject.SetActive(sectionPager&&section.page>0);
            section.next.gameObject.SetActive(sectionPager&&section.page<lastPage);
            var caption=(RectTransform)section.next.transform.parent;
            var previous=(RectTransform)section.previous.transform;
            var next=(RectTransform)section.next.transform;
            if(HudTheme!=null){
                float step=RedesignPageStep(section.captionWidth);
                previous.anchorMin=previous.anchorMax=next.anchorMin=next.anchorMax=new Vector2(1,.5f);
                previous.sizeDelta=next.sizeDelta=new Vector2(step-2,30);
                next.anchoredPosition=new Vector2(-step*.5f-2,0);
                previous.anchoredPosition=next.anchoredPosition-Vector2.right*(lastPage>1?step:0);
            }else{
                float arrowInset=Mathf.Min(26,caption.rect.width*.16f);
                previous.anchoredPosition=Vector2.left*(caption.rect.width*.5f-arrowInset);
                next.anchoredPosition=Vector2.right*(caption.rect.width*.5f-arrowInset);
            }
            if(visibleSource)ShowRedesignElement(section.region,Vector2.zero);
            else section.region.gameObject.SetActive(false);
            if(fan.browseSources && section.zone==fan.browsedSource && fan.sourcePager!=null){
                fan.sourcePager.gameObject.SetActive(lastPage>0);
                if(lastPage>0){
                    fan.sourceRange.text=$"{section.page+1} / {lastPage+1}";
                    fan.sourcePrevious.interactable=section.page>0;
                    fan.sourceNext.interactable=section.page<lastPage;
                    fan.sourcePrevious.transform.Find("Icon").GetComponent<StackControlIcon>().color=fan.sourcePrevious.interactable?HudText:HudEdge;
                    fan.sourceNext.transform.Find("Icon").GetComponent<StackControlIcon>().color=fan.sourceNext.interactable?HudText:HudEdge;
                }
            }
            if(section.divider.gameObject.activeSelf)ShowRedesignElement(section.divider,Vector2.zero,interactive:false);
        }
        if(fan.opponent&&!float.IsInfinity(min.x)){
            fan.restingBounds=UnityEngine.Rect.MinMaxRect(min.x,min.y,max.x,max.y);
            var reserved=fan.restingBounds;reserved.yMin-=(otherSourceTabs!=null?otherSourceTabs.rect.height:24)+8;
            fan.restingBounds=reserved;
            // Keep a stable, reachable strip even on an empty opening board.
            // Explicit browsing and native choices show the entire hand; field
            // density never changes this pose or persists a stale clearance.
            bool expanded=redesignOtherHandExpanded||RedesignOtherHandForced;
            float shift=expanded?Mathf.Min(0,-max.y-8):Mathf.Max(0,-min.y-RedesignOpponentHandStrip);
            parent.anchoredPosition=new Vector2(parent.anchoredPosition.x,shift);
            redesignOtherCardsBottom=Mathf.Max(0,-shift-min.y);
        }
        fan.hits.Invalidate();
        if(fan.opponent)PresentRedesignOtherBackplate();
        else PresentRedesignHandBackplate();
        PositionRedesignSourceTabs();
    }

    private void PresentRedesignHandBackplate()
    {
        if (redesignHandBackplate == null) {
            redesignHandBackplate = Panel(surface, "RedesignHandBackplate", Vector2.zero, Vector2.one, Color.white);
            redesignHandBackplate.gameObject.AddComponent<BattlefieldHandPanelTint>();
            var button = redesignHandBackplate.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = redesignHandBackplate.GetComponent<UnityEngine.UI.Image>();
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            // Match battlefield dismissal on press, not release. The tray can
            // move between down/up, so a completed Button click is not reliable.
            redesignHandBackplate.gameObject.AddComponent<BattlefieldRedesignHandInput>().Configure(ExpandRedesignHand);
            // The old transparent navigation rectangle must not swallow clicks
            // intended for this visible tray; retain blank-space panning here.
            var drag = redesignHandBackplate.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            void Forward(UnityEngine.EventSystems.EventTriggerType type, System.Action<UnityEngine.EventSystems.PointerEventData> action) {
                var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
                entry.callback.AddListener(data => action((UnityEngine.EventSystems.PointerEventData)data));
                drag.triggers.Add(entry);
            }
            Forward(UnityEngine.EventSystems.EventTriggerType.BeginDrag, data => ownFan.navigation?.OnBeginDrag(data));
            Forward(UnityEngine.EventSystems.EventTriggerType.Drag, data => ownFan.navigation?.OnDrag(data));
            Forward(UnityEngine.EventSystems.EventTriggerType.EndDrag, data => ownFan.navigation?.OnEndDrag(data));
            var edge = Panel(redesignHandBackplate, "TopEdge", Vector2.zero, Vector2.one, new Color(142 / 255f, 162 / 255f, 185 / 255f, .18f));
            edge.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        }
        if(ownFan.slots.Count==0){HideRedesignElement(redesignHandBackplate,new Vector2(0,-16));return;}
        // Leave the player-side mana arrow's outer edge (x=392) clear.
        // The hand can still be animating from the previous seat's width.
        // Size its clickable surface from the destination lane as well.
        float trim=ownFan.pageTrim;
        float width = Mathf.Max(1,RedesignHandWidth-32-trim);
        RedesignPlace(redesignHandBackplate,Vector2.zero,Vector2.zero,new Vector2(RedesignHandLeft(false)+16+trim*.5f,redesignHandExpanded?ExpandedSourceLift-RedesignHandTrayLower:0),new Vector2(width,redesignHandExpanded?RedesignHandTrayHeight:48));
        var image = redesignHandBackplate.GetComponent<UnityEngine.UI.Image>();
        if(HudTheme!=null) {
            image.GetComponent<BattlefieldHandPanelTint>().enabled=false;
            DuelSurface(redesignHandBackplate,BattlefieldHudGraphic.Surface.Tray,HudTheme.MainAccent);
        } else BattlefieldHandPanelTint.ApplyModernFrame(image);
        image.raycastTarget = true;
        var line = (RectTransform)redesignHandBackplate.Find("TopEdge");
        line.gameObject.SetActive(false);
        RedesignPlace(line, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(width, 1));
        // The card tray stops before the native footer, which keeps its own visual space.
        var footer=Node("CornerCluster");int footerIndex=footer.GetSiblingIndex();
        redesignHandBackplate.SetSiblingIndex(footerIndex-(redesignHandBackplate.GetSiblingIndex()<footerIndex?1:0));
        ShowRedesignElement(redesignHandBackplate,new Vector2(0,-16));
        SetOverlayLayer(redesignHandBackplate,HandTrayLayer,true);
    }
}
