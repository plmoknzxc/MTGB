using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private sealed class FourWindowPage
    {
        public RectTransform plate;
        public readonly ArenaCard[] items=new ArenaCard[MaxFourSeatColumns*MaxFourSeatRows];
        public int count,frontCount,capacity;
        public bool own;
        public Vector2 pan;
        public int seat,pageIndex;
    }

    private readonly FourWindowPage[] fourWindowPages={new(),new(),new(),new()};
    private const int MaxFourSeatColumns=8, MaxFourSeatRows=4;
    private readonly System.Collections.Generic.Dictionary<int,int> fourSeatCapacities=new();
    private readonly float[] fourWindowColumns={.5f,.5f};
    private float fourWindowTop=.5f;
    private float fourTopMinimum=225,fourBottomMinimum=225;
    private bool fourWindowMode;
    private int draggingFourSeat=-1;
    private RectTransform fourWindowEdges;
    private readonly FourSeatResizeHandle[] fourHandles=new FourSeatResizeHandle[7];
    private readonly Image[] fourSeatJoints=new Image[2];

    private Rect FourTableBounds()
    {
        var viewport=surface.rect;
        // The life plate and phase controls stay on screen. The stack, hand and
        // journal float above the table and do not participate in its geometry.
        return UnityEngine.Rect.MinMaxRect(viewport.xMin+16,viewport.yMin+158,viewport.xMax-16,viewport.yMax-8);
    }

    private Vector2 ClampFourPan(int slot,Vector2 pan)
    {
        var frame=FourSeatBounds(slot);
        float horizontal=frame.width*.48f,vertical=(frame.height-fourWindowPages[slot].plate.rect.height)*.44f;
        return new Vector2(Mathf.Clamp(pan.x,-horizontal,horizontal),Mathf.Clamp(pan.y,-vertical,vertical));
    }

    private Rect? FourPanViewport(Rect bounds,FourWindowPage page)
    {
        if(!fourWindowMode||page.pan==Vector2.zero)return null;
        var min=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,surface.TransformPoint(bounds.min+new Vector2(5,3)));
        var max=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,surface.TransformPoint(bounds.max-new Vector2(5,page.plate.rect.height+4)));
        return UnityEngine.Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }

    private void PositionFourPage(int slot)
    {
        var page=fourWindowPages[slot];
        if(page.plate==null||!page.plate.gameObject.activeInHierarchy)return;
        var frame=FourSeatBounds(slot);
        page.pan=ClampFourPan(slot,page.pan);
        var grid=new FourSeatGrid(frame,slot,page.frontCount,page.count,true,page.plate.rect.height);
        var clip=FourPanViewport(frame,page);
        for(int i=0;i<page.count;i++){
            var item=page.items[i];
            item.restingPosition=grid.Position(i)+page.pan;
            item.pointer.ResizeFieldSlot(item.restingPosition,grid.cell);
            if(item.world!=null)item.world.SeatViewport=clip;
        }
    }

    private static float ClampSplit(float ratio,float length,float minimum) {
        float floor=Mathf.Min(.5f,minimum/Mathf.Max(1,length));
        return Mathf.Clamp(ratio,floor,1-floor);
    }

    private Rect FourSeatBounds(int slot)
    {
        Rect table=FourTableBounds();
        bool upper=slot==1 || slot==2, left=slot==0 || slot==1;
        float column=fourWindowMode?ClampSplit(fourWindowColumns[upper?0:1],table.width,345):.5f;
        float fit=Mathf.Min(1,table.height/(fourTopMinimum+fourBottomMinimum));
        float topHeight=fourWindowMode
            ?Mathf.Clamp(fourWindowTop*table.height,fourTopMinimum*fit,table.height-fourBottomMinimum*fit)
            :table.height*.5f;
        float x=table.xMin+table.width*column,y=table.yMax-topHeight;
        return UnityEngine.Rect.MinMaxRect(left?table.xMin:x,upper?y:table.yMin,left?x:table.xMax,upper?table.yMax:y);
    }

    private void FitFourSeatHeaders()
    {
        fourTopMinimum=fourBottomMinimum=225;
        for(int slot=0;slot<fourWindowPages.Length;slot++){
            var page=fourWindowPages[slot];
            if(page.plate==null)continue;
            float minimum=LayoutFourSeatHeader(page,FourSeatBounds(slot))+112;
            if(slot==1 || slot==2)fourTopMinimum=Mathf.Max(fourTopMinimum,minimum);
            else fourBottomMinimum=Mathf.Max(fourBottomMinimum,minimum);
        }
        // Both columns share the same row edge. Keep the tallest seat header
        // and a visible rank of cards inside either row, even after permissions change.
        for(int slot=0;slot<fourWindowPages.Length;slot++){
            var plate=fourWindowPages[slot].plate;
            if(plate==null)continue;
            Rect frame=FourSeatBounds(slot);
            plate.anchoredPosition=new Vector2(frame.center.x,frame.yMax);
        }
    }

    private int FourSeatCapacity(Rect frame,float header)
    {
        if(!fourWindowMode)return FourSeatPageSize;
        Rect table=FourTableBounds();
        // An equal pane starts with two ranks of four groups.
        float pitchX=Mathf.Max(128,(table.width*.5f-24)/FourSeatColumns);
        float pitchY=Mathf.Max(116,(table.height*.5f-header-12)/2);
        int columns=Mathf.Clamp(Mathf.RoundToInt((frame.width-24)/pitchX),1,MaxFourSeatColumns);
        int rows=Mathf.Clamp(Mathf.RoundToInt((frame.height-header-12)/pitchY),1,MaxFourSeatRows);
        return columns*rows;
    }

    private float LayoutFourSeatHeader(FourWindowPage page,Rect frame)
    {
        var plate=page.plate;
        float width=fourWindowMode?frame.width-18:Mathf.Min(frame.width-18,940);
        const float inset=10, gap=5, chipHeight=26;
        void Place(string node,float x,float mid,float w,float h){
            RedesignPlace((RectTransform)plate.Find(node),Vector2.up,new Vector2(0,.5f),new Vector2(x,-mid),new Vector2(w,h));
        }
        void Header(float height){
            RedesignPlace(plate,new Vector2(.5f,.5f),new Vector2(.5f,1),
                new Vector2(frame.center.x,frame.yMax),new Vector2(width,height));
            Place("SeatAccent",0,height*.5f,3,height-8);
        }
        float pageY=page.own?28:21;
        Place("Page",width-90,pageY,54,28);
        Place("Previous",width-125,pageY,32,30);
        Place("Next",width-34,pageY,32,30);
        if(page.own){
            Header(56);
            var tax=(RectTransform)plate.Find("Tax");
            bool compact=width<475 && tax.gameObject.activeSelf;
            FourSeatPlace((RectTransform)plate.Find("Name"),18,compact?11:0,130,32);
            FourSeatPlace((RectTransform)plate.Find("Count"),148,compact?11:0,46,32);
            FourSeatPlace(tax,compact?18:184,compact?-13:0,140,24);
            return 56;
        }

        var name=plate.Find("Name").GetComponent<Text>();
        var commander=plate.Find("CommanderName").GetComponent<Text>();
        bool named=commander.gameObject.activeSelf,wide=width>=580;
        float identityWidth=Mathf.Clamp(Mathf.Max(91+name.preferredWidth,named?commander.preferredWidth+3:0),
            138,wide?Mathf.Min(280,width*.37f):Mathf.Clamp(width*.42f,138,210));
        float sourceStart=inset+identityWidth+10;
        float Needed(string node){
            var label=plate.Find(node+"/Label").GetComponent<Text>();
            return Mathf.Max(56,Mathf.Ceil(label.preferredWidth)+18);
        }
        float right=width-132;
        float x=wide?sourceStart:inset,top=8,rowHeight=chipHeight,identityEnd=0;
        void Chip(string node){
            var t=plate.Find(node);
            var label=t.Find("Label").GetComponent<Text>();
            float natural=Needed(node);
            if(x>inset && x+natural>right+.01f){
                top+=rowHeight+gap;rowHeight=chipHeight;
                right=width-inset;
                x=wide||top<identityEnd?sourceStart:inset;
                if(x>inset && x+natural>right){x=inset;top=identityEnd+gap;}
            }
            float w=Mathf.Min(natural,right-x);
            label.horizontalOverflow=HorizontalWrapMode.Wrap;
            label.alignment=TextAnchor.MiddleCenter;
            float h=chipHeight;
            if(w<natural){
                var settings=label.GetGenerationSettings(new Vector2(w-12,0));
                float textHeight=label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text,settings)/label.pixelsPerUnit;
                h=Mathf.Max(h,Mathf.Ceil(textHeight)+6);
            }
            Place(node,x,top+h*.5f,w,h);
            RedesignPlace(label.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(w-10,h-2));
            rowHeight=Mathf.Max(rowHeight,h);
            x+=w+gap;
        }
        foreach(var zone in FourPlateZones)Chip(zone.node);
        if(plate.Find("Status").gameObject.activeSelf)Chip("Status");

        // Source membership can change with a permission or a companion moving to hand.
        // Reserve only the rows actually occupied, including at the narrowest split.
        float totalsBottom=top+rowHeight;
        float identityTop=wide?4:totalsBottom+5;
        Place("Avatar",inset,identityTop+16,32,32);
        Place("Life",46,identityTop+16,44,32);
        Place("Name",91,identityTop+16,inset+identityWidth-91,30);
        identityEnd=identityTop+32;
        if(named){
            var settings=commander.GetGenerationSettings(new Vector2(identityWidth,0));
            float lines=Mathf.Ceil(commander.cachedTextGeneratorForLayout.GetPreferredHeight(commander.text,settings)/commander.pixelsPerUnit)+2;
            Place("CommanderName",inset,identityEnd+lines*.5f,identityWidth,lines);
            identityEnd+=lines;
        }
        top=totalsBottom+6;x=sourceStart;right=width-inset;rowHeight=chipHeight;
        float bottom=Mathf.Max(identityEnd,totalsBottom);
        foreach(var source in FourPlateSources){
            if(!plate.Find(source.node).gameObject.activeSelf)continue;
            Chip(source.node);
            bottom=Mathf.Max(bottom,top+rowHeight);
        }
        float height=bottom+5;
        Header(height);
        return height;
    }

    private readonly struct FourSeatGrid
    {
        private readonly Rect bounds;
        private readonly int ranks,front,count,columns;
        private readonly bool upper;
        private readonly float pitch,rowHeight,headerHeight;
        public readonly Vector2 cell;

        public FourSeatGrid(Rect bounds,int slot,int front,int count,bool allowReflow,float header) {
            this.bounds=bounds;
            this.count=count;
            this.front=front;
            headerHeight=header;
            upper=slot==1 || slot==2;
            int back=count-front;
            columns=Mathf.Max(1,Mathf.Min(FourSeatColumns,Mathf.Max(front,back)));
            ranks=RankCount(front,back,columns);
            float bodyHeight=Mathf.Max(1,bounds.height-header-12);
            float bodyWidth=Mathf.Max(1,bounds.width-24);
            if(allowReflow && count>1){
                // Wider panes may fit more columns. Each row belongs to one rank.
                float best=FittedWidth(bodyWidth,bodyHeight,columns,ranks)*1.15f;
                for(int n=1;n<=Mathf.Max(front,back);n++){
                    int rows=RankCount(front,back,n);
                    float width=FittedWidth(bodyWidth,bodyHeight,n,rows);
                    if(width<=best)continue;
                    best=width;
                    columns=n;
                    ranks=rows;
                }
            }
            rowHeight=bodyHeight/ranks;
            float lane=bodyWidth/columns;
            pitch=Mathf.Min(260,Mathf.Min(lane,rowHeight*1.16f));
            cell=new Vector2(Mathf.Max(1,pitch-12),Mathf.Clamp(rowHeight-12,1,285));
        }

        private static int RankCount(int front,int back,int columns) =>
            Mathf.Max(2,(front+columns-1)/columns+(back+columns-1)/columns);

        private static float FittedWidth(float width,float height,int columns,int rows) {
            return Mathf.Min(Mathf.Min(width/columns-12,248),Mathf.Min(height/rows-12,285)/1.08f);
        }

        public Vector2 Position(int i) {
            bool creature=i<front;
            int index=creature?i:i-front;
            int members=creature?front:count-front;
            int rank=index/columns, column=index%columns;
            int width=Mathf.Min(columns,members-rank*columns);
            // Creatures face the centre; lands and other supports start at the rear.
            int row=creature?rank:ranks-1-rank;
            if(upper)row=ranks-1-row;
            return new Vector2(bounds.center.x+(column-(width-1)*.5f)*pitch,bounds.yMax-headerHeight-6-(row+.5f)*rowHeight);
        }
    }

    private void LayoutFourWindowEdges()
    {
        bool visible=view?.players?.Length>1;
        if(fourWindowEdges!=null)fourWindowEdges.gameObject.SetActive(visible);
        if(!visible)return;
        if(fourWindowEdges==null){
            fourWindowEdges=Rect(authored,"FourSeatWindowEdges",Vector2.zero,Vector2.zero);
            fourWindowEdges.anchorMin=Vector2.zero;fourWindowEdges.anchorMax=Vector2.one;
            fourWindowEdges.offsetMin=fourWindowEdges.offsetMax=Vector2.zero;
            // Only the narrow edges intercept input. Dialogs, choices and the journal retain their layers.
            string[] names={"TopColumn","BottomColumn","Row","TopLeftCorner","TopRightCorner","BottomLeftCorner","BottomRightCorner"};
            for(int i=0;i<fourHandles.Length;i++){
                var hit=Panel(fourWindowEdges,names[i],Vector2.zero,Vector2.one,Color.clear);
                if(i<3){
                    var shadow=Panel(hit,"Shadow",Vector2.zero,Vector2.one,new Color(.027f,.030f,.048f,.88f));
                    shadow.GetComponent<Image>().raycastTarget=false;
                    var span=i==2?Vector2.right:Vector2.up;
                    shadow.anchorMin=(Vector2.one-span)*.5f;shadow.anchorMax=shadow.anchorMin+span;
                    shadow.sizeDelta=(Vector2.one-span)*6;
                }
                var ink=Panel(hit,"Edge",Vector2.zero,Vector2.one,HudRule);
                ink.GetComponent<Image>().raycastTarget=false;
                var grip=Panel(hit,"Grip",Vector2.zero,Vector2.one,HudMuted);
                grip.GetComponent<Image>().raycastTarget=false;
                fourHandles[i]=hit.gameObject.AddComponent<FourSeatResizeHandle>();
                fourHandles[i].Bind(this,i,ink,grip);
                if(i<fourSeatJoints.Length){
                    bool upper=i==0;
                    var joint=Rect(hit,"SeatJoint",Vector2.zero,new Vector2(32,11));
                    joint.anchorMin=joint.anchorMax=new Vector2(.5f,upper?0:1);
                    joint.pivot=new Vector2(.5f,0);
                    joint.anchoredPosition=new Vector2(0,upper?1:-1);
                    joint.localScale=new Vector3(1,upper?1:-1,1);
                    fourSeatJoints[i]=joint.gameObject.AddComponent<Image>();
                    fourSeatJoints[i].raycastTarget=false;
                }
            }
        }
        Rect table=FourTableBounds(),top=FourSeatBounds(1),bottom=FourSeatBounds(0);
        Color edge=Color.Lerp(HudEdge,HudText,.26f);edge.a=.82f;
        Color thumb=Color.Lerp(HudEdge,HudText,.72f);
        foreach(var joint in fourSeatJoints){
            joint.sprite=HudTheme?.SeatJoint;
            joint.enabled=joint.sprite!=null;
        }
        for(int i=0;i<fourHandles.Length;i++){
            fourHandles[i].gameObject.SetActive(i<3||fourWindowMode);
            fourHandles[i].SetPresentation(fourWindowMode,edge,thumb);
        }
        float y=top.yMin;
        void Place(int i,float x,float cy,float w,float h){
            var t=(RectTransform)fourHandles[i].transform;
            t.anchoredPosition=new Vector2(x,cy);t.sizeDelta=new Vector2(w,h);
        }
        Place(0,top.xMax,top.center.y,14,top.height);
        Place(1,bottom.xMax,bottom.center.y,14,bottom.height);
        Place(2,table.center.x,y,table.width,12);
        for(int i=0;i<4;i++){
            bool high=i<2,left=(i%2)==0;
            Place(i+3,(high?top.xMax:bottom.xMax)+(left?-10:10),y+(high?10:-10),20,20);
        }
    }

    internal void DragFourSeatEdge(int handle,Vector2 localDelta)
    {
        if(!fourWindowMode || FourSeatInputBlocked || view==null)return;
        var table=FourTableBounds();
        if(handle!=2){
            int row=handle==0 || handle==3 || handle==4?0:1;
            float x=ClampSplit(fourWindowColumns[row],table.width,345)+localDelta.x/table.width;
            fourWindowColumns[row]=ClampSplit(x,table.width,345);
        }
        if(handle>=2){
            float y=FourSeatBounds(1).height/table.height-localDelta.y/table.height;
            fourWindowTop=ClampSplit(y,table.height,225);
        }
        // Rebind cards only when a divider crosses a capacity threshold. Between thresholds
        // the same meshes and input targets follow the pointer without rebuilding the HUD.
        float stackTop=RedesignStackTop;
        FitFourSeatHeaders();
        if(view.stackCount>0 && !Mathf.Approximately(stackTop,RedesignStackTop)){
            PresentIntegrated(connectionText.text);
            return;
        }
        for(int slot=0;slot<fourWindowPages.Length;slot++){
            var page=fourWindowPages[slot];
            if(page.plate==null || !page.plate.gameObject.activeInHierarchy)continue;
            Rect frame=FourSeatBounds(slot);
            float header=page.plate.rect.height;
            if(page.capacity!=FourSeatCapacity(frame,header)){
                PresentIntegrated(connectionText.text);
                return;
            }
        }
        for(int slot=0;slot<fourWindowPages.Length;slot++)PositionFourPage(slot);
        LayoutFourWindowEdges();
        if(assignedAttackArrows.Count>0||IsDeclaringAttack)PresentAttackSelection();
        if(blockLines.Count>0||IsBlockingChoice)PresentBlocking();
    }
}
