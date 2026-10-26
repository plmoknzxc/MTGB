using System.Linq;
using UnityEngine;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform redesignHandBackplate;
    // Expanded top meets the player counter row's top (72 + 170 + 15).
    private const float RedesignHandTrayHeight=321,RedesignHandTrayLower=64,RedesignHandRestY=-59;
    private const float RedesignSourceGap=4,RedesignSourceGutter=8;
    private float redesignOwnWidth=300,redesignOtherWidth=300,redesignOwnLane,redesignOtherLane;
    private float RedesignHandMaximumWidth => Mathf.Max(300,Mathf.Min(surface.rect.width-404,(redesignFooterLeft>0?redesignFooterLeft:surface.rect.width-336)-376));
    private float RedesignOtherHandMaximumWidth => Mathf.Max(300, Mathf.Min(RedesignHandMaximumWidth, surface.rect.width - 380 - RedesignStackWidth - 40));
    private float RedesignHandWidth => Mathf.Min(redesignOwnWidth,RedesignHandMaximumWidth);
    private float RedesignOtherHandWidth => Mathf.Min(redesignOtherWidth,RedesignOtherHandMaximumWidth);
    private float RedesignHandLeft(bool other)=>380+((other?RedesignOtherHandMaximumWidth:RedesignHandMaximumWidth)-(other?RedesignOtherHandWidth:RedesignHandWidth))*.5f;

    private void LayoutRedesignHand(RectTransform parent, ForgeCardView[] cards, HandFanState fan, bool choosingHand)
    {
        var zones=RedesignOrderedSources(cards).ToArray();
        foreach(var section in RedesignSections(fan).Values){
            section.slots.Clear();
            if(section.region!=null && !zones.Contains(section.zone))HideRedesignElement(section.region,Vector2.zero);
            if(section.divider!=null)section.divider.gameObject.SetActive(false);
        }
        fan.hits.Clear();fan.slots.Clear();
        fan.navigation=null;fan.previous=null;fan.next=null;
        fan.available=fan.opponent?RedesignOtherHandWidth:RedesignHandWidth;
        int order=0,sectionOrder=0;
        foreach(string zone in zones){
            var section=RedesignSections(fan)[zone];var members=cards.Where(c=>RedesignSource(c)==zone).ToArray();
            if(section.region==null){
                section.region=Panel(parent,zone=="Hand"?"HandFanNavigation":"SourceNavigation_"+zone,Vector2.zero,Vector2.one,Color.clear);
                section.region.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
                section.navigation=section.region.gameObject.AddComponent<ForgeHandFanNavigation>();
                var reveal=section.region.gameObject.AddComponent<UnityEngine.UI.Button>();
                reveal.targetGraphic=section.region.GetComponent<UnityEngine.UI.Image>();reveal.transition=UnityEngine.UI.Selectable.Transition.None;
                if(!fan.opponent)section.region.gameObject.AddComponent<BattlefieldRedesignHandInput>().Configure(ExpandRedesignHand);
                UnityEngine.UI.Button Arrow(string name,int direction){
                    var b=Button(section.region,name,"",Vector2.zero,new Vector2(16,30),()=>section.navigation.MoveBy(direction*section.width*.65f),Ink);
                    StyleRedesignSourceButton(b);
                    var glyph=Rect(b.transform,"Chevron",Vector2.zero,new Vector2(12,24)).gameObject.AddComponent<BattlefieldScrollChevron>();
                    glyph.direction=-direction;glyph.color=Paper;glyph.raycastTarget=false;return b;
                }
                section.previous=Arrow("Previous",1);section.next=Arrow("Next",-1);
                fan.regions["Redesign_"+zone]=section.region;
                section.divider=Panel(parent,"SourceDivider_"+zone,Vector2.zero,Vector2.one,new Color(.65f,.64f,.5f,.45f));
                section.divider.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            }
            section.region.gameObject.SetActive(true);section.region.SetAsFirstSibling();
            section.divider.gameObject.SetActive(sectionOrder++>0);
            float width=Mathf.Min(166,Mathf.Max(32,section.width-RedesignSourceGutter-Mathf.Max(0,members.Length-1)*12)),height=width*232/166;
            // Keep every source card exposed; compress overlap instead of paging.
            float step=Mathf.Clamp((section.width-RedesignSourceGutter-width)/Mathf.Max(1,members.Length-1),0,182);
            float total=members.Length==0?0:width+(members.Length-1)*step;
            section.travel=0;
            section.navigation.Configure(parent,section.travel,o=>ApplyRedesignHandOffset(fan,0,true));
            if(zone=="Hand"){fan.navigation=section.navigation;fan.previous=section.previous;fan.next=section.next;fan.travel=section.travel;}
            for(int i=0;i<members.Length;i++){
                var card=members[i];string key=(fan.opponent?"OtherFan:":"Hand:")+card.id;
                float x=section.center-total*.5f+width*.5f+i*step;
                DrawArenaCard(parent,card,1,key,key,new Vector2(x,fan.opponent?11:-11),new Vector2(width,height),true,fan.opponent?180:0,order++);
                var item=arenaCards[key];
                if(zone=="Hand"||zone=="Command"||zone=="Companion")item.root.GetComponent<CanvasGroup>().alpha=1;
                if(fan.opponent){
                    item.root.GetComponent<CanvasGroup>().alpha=1;
                    if(!choosingHand||zone!="Hand"){
                        item.button.onClick.RemoveAllListeners();
                        if(!card.faceDown)item.button.onClick.AddListener(()=>InspectIntegrated(card));
                    }
                    item.pointer.DisableActions();
                }
                var layer=item.face.GetComponent<BattlefieldRedesignHandLayer>()??item.face.gameObject.AddComponent<BattlefieldRedesignHandLayer>();
                layer.Configure(item.pointer,canvas.sortingLayerID,canvas.sortingOrder+DecisionLayerOffset-1);
                item.pointer.ConfigurePlayRegion(section.region);item.pointer.ConfigureHandNavigation(section.navigation);
                if(!fan.opponent){
                    var reveal=item.root.GetComponent<BattlefieldRedesignHandInput>()??item.root.gameObject.AddComponent<BattlefieldRedesignHandInput>();
                    reveal.Configure(ExpandRedesignHand);
                }
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
            RedesignPlace(parent,Vector2.zero,Vector2.zero,new Vector2(RedesignHandLeft(false),RedesignHandRestY-(redesignHandExpanded?0:154)),new Vector2(fan.available,270));
            PresentRedesignHandBackplate();
            ShowRedesignElement(parent,Vector2.zero);
        }
        var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=-min;
        foreach(var section in RedesignSections(fan).Values){
            if(section.region==null||section.slots.Count==0)continue;
            section.slots.RemoveAll(s=>!fan.slots.Contains(s));
            float scroll=section.navigation.Offset;
            section.region.anchoredPosition=new Vector2(section.center,fan.opponent?11:-11);section.region.sizeDelta=new Vector2(section.width,232);
            section.divider.anchoredPosition=new Vector2(section.center-section.width*.5f-RedesignSourceGap*.5f,fan.opponent?11:-11);
            section.divider.sizeDelta=new Vector2(1,232);
            foreach(var slot in section.slots){
                float x=slot.x+scroll;
                var position=new Vector2(x,fan.opponent?11:-11);
                slot.item.pointer.SetHandFanPose(position,fan.opponent?180:0,snap);
                float exposure=!fan.opponent&&!redesignHandExpanded?0:1;
                if(slot.item.pointer.IsCastingDrag)exposure=1;
                var visibility=slot.item.root.GetComponent<CanvasGroup>();if(visibility==castingHandGroup)exposure=0;
                visibility.alpha=slot.opacity*exposure;visibility.blocksRaycasts=exposure>.02f;
                ShowRedesignElement(slot.item.root,new Vector2(0,fan.opponent?28:-28),slot.opacity*exposure,exposure>.02f);
                slot.item.pointer.SetHandFanExposure(exposure>.02f);
                slot.item.pointer.AllowHandHover=fan.opponent||redesignHandExpanded;
                if(exposure>.02f){
                    var center=parent.rect.center+position;var half=slot.item.root.rect.size*.5f;
                    min=Vector2.Min(min,center-half);max=Vector2.Max(max,center+half);
                    if(fan.opponent){
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
            bool pan=section.travel>.1f;
            section.previous.gameObject.SetActive(pan);section.next.gameObject.SetActive(pan);
            section.previous.interactable=scroll<section.travel*.5f-.1f;
            section.next.interactable=scroll>-section.travel*.5f+.1f;
            ShowRedesignElement(section.region,Vector2.zero);
            if(section.divider.gameObject.activeSelf)ShowRedesignElement(section.divider,Vector2.zero,interactive:false);
        }
        if(fan.opponent&&!float.IsInfinity(min.x)){
            fan.restingBounds=UnityEngine.Rect.MinMaxRect(min.x,min.y,max.x,max.y);
            var reserved=fan.restingBounds;reserved.yMin-=(otherSourceTabs!=null?otherSourceTabs.rect.height:24)+8;
            fan.restingBounds=reserved;
            ReserveRestingHandSpace(fan);
            redesignOtherCardsBottom=Mathf.Max(0,-parent.anchoredPosition.y-min.y);
        }
        foreach(var section in RedesignSections(fan).Values){
            if(section.region==null||!section.region.gameObject.activeSelf)continue;
            float arrowY=fan.opponent?124-parent.anchoredPosition.y-64:0;
            section.previous.GetComponent<RectTransform>().anchoredPosition=new Vector2(-section.width*.5f+4,arrowY);
            section.next.GetComponent<RectTransform>().anchoredPosition=new Vector2(section.width*.5f-4,arrowY);
        }
        fan.hits.Invalidate();
        if(fan.opponent)PresentRedesignOtherBackplate();
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
        float width = Mathf.Max(1,ownHand.rect.width-32);
        RedesignPlace(redesignHandBackplate,Vector2.zero,Vector2.zero,new Vector2(RedesignHandLeft(false)+16,redesignHandExpanded?-RedesignHandTrayLower:0),new Vector2(width,redesignHandExpanded?RedesignHandTrayHeight:48));
        var image = redesignHandBackplate.GetComponent<UnityEngine.UI.Image>();
        BattlefieldHandPanelTint.ApplyModernFrame(image);
        image.raycastTarget = true;
        var line = (RectTransform)redesignHandBackplate.Find("TopEdge");
        line.gameObject.SetActive(false);
        RedesignPlace(line, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(width, 1));
        // The card tray stops before the native footer, which keeps its own visual space.
        var footer=Node("CornerCluster");int footerIndex=footer.GetSiblingIndex();
        redesignHandBackplate.SetSiblingIndex(footerIndex-(redesignHandBackplate.GetSiblingIndex()<footerIndex?1:0));
        ShowRedesignElement(redesignHandBackplate,new Vector2(0,-16));
    }
}
