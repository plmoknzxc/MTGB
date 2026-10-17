using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private int browsedSeat;
    private int browsedComponentId;
    private string browsedZone, zoneBrowserSignature;
    private void PresentZonePiles(ForgePlayerView player,string path)
    {
        bool own=player.seat==seat;
        string plate=own?"PlayerPlate":"OpponentPlate";
        var anchor=own?Vector2.zero:new Vector2(0,1);
        ArenaPlace(plate,anchor,own?new Vector2(205,72):new Vector2(205,-120),new Vector2(342,92));
        ArenaPlace(path,anchor,own?new Vector2(205,144):new Vector2(205,-192),new Vector2(342,44));
        var node=(RectTransform)Node(path);
        var shell=Node(plate).parent.Find(plate+"WithZones") as RectTransform;
        if(shell==null) {
            shell=Panel(Node(plate).parent,plate+"WithZones",Vector2.zero,new Vector2(342,140),new Color(.045f,.065f,.085f,.97f));
            shell.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(342,140,8);
            shell.GetComponent<Image>().raycastTarget=false;
            shell.gameObject.AddComponent<Outline>().effectColor=new Color(.3f,.4f,.48f,.5f);
        }
        shell.anchorMin=shell.anchorMax=anchor;
        shell.anchoredPosition=own?new Vector2(205,96):new Vector2(205,-144);
        int behind=Math.Min(Node(plate).GetSiblingIndex(),node.GetSiblingIndex());
        // Removing a previous sibling shifts the target index down by one.
        // Repeated snapshots/paging must not bring the shell over the zone text.
        if(shell.GetSiblingIndex()<behind)behind--;
        shell.SetSiblingIndex(Math.Max(0,behind));
        var outline=Node(plate).GetComponent<Outline>();if(outline!=null)outline.enabled=false;
        var backdrop=node.GetComponent<Image>();if(backdrop!=null)backdrop.color=Color.clear;
        if(node.Find("BarDivider")==null) {
            var divider=Panel(node,"BarDivider",new Vector2(0,own?-23:23),new Vector2(310,1),new Color(.5f,.62f,.7f,.25f));
            divider.GetComponent<Image>().raycastTarget=false;
        }
        // Known hand browsing is read-only. Targeting still owns clicks on
        // the player plate while an authoritative player choice is active.
        var handLabel=SceneText(plate+"/Sub");
        var handBrowser=handLabel.GetComponent<Button>()??handLabel.gameObject.AddComponent<Button>();
        handBrowser.targetGraphic=handLabel;handBrowser.transition=Selectable.Transition.None;handBrowser.onClick.RemoveAllListeners();
        handBrowser.interactable=!IsTargeting&&!IsDeclaringAttack&&SelectionHand==null;
        handLabel.raycastTarget=handBrowser.interactable;handBrowser.onClick.AddListener(()=>OpenZoneBrowser(player.seat,"手牌"));
        PresentZonePile(player,path,"牌库",0,player.libraryCount);
        var grave=ZoneCards(player,"坟场");var exile=ZoneCards(player,"放逐");
        PresentZonePile(player,path,"坟场",1,grave.Length);
        PresentZonePile(player,path,"放逐",2,Math.Max(player.exileCount,exile.Length));
        bool ante=view?.anteEnabled==true;
        var anteEntry=node.Find("Pile_赌注");
        if(ante&&anteEntry==null)anteEntry=Panel(node,"Pile_赌注",Vector2.zero,new Vector2(80,40),Color.clear);
        if(anteEntry!=null)anteEntry.gameObject.SetActive(ante);
        if(ante)PresentZonePile(player,path,"赌注",3,ZoneCards(player,"赌注").Length);
        PresentAuxiliaryEntry(player,node);
        var entries=(ante?new[]{"牌库","坟场","放逐","赌注"}:new[]{"牌库","坟场","放逐"}).Select(z=>(RectTransform)Node(path+"/Pile_"+z)).ToArray();
        // Ante uses the same fixed row. Omit the decorative icons to keep four captions legible.
        var widths=entries.Select(e=>e.Find("ZoneEntry/ZoneCaption").GetComponent<Text>().preferredWidth+(ante?12:32)).ToArray();
        float extra=Mathf.Max(0,(342-widths.Sum())/entries.Length),cursor=-widths.Sum()*.5f-extra*entries.Length*.5f;
        for(int i=0;i<entries.Length;i++){
            var root=entries[i];float width=widths[i]+extra;root.sizeDelta=new Vector2(width,40);root.anchoredPosition=new Vector2(cursor+width*.5f,0);cursor+=width;
            var face=(RectTransform)root.Find("ZoneEntry");face.sizeDelta=new Vector2(width-2,40);
            var icon=face.Find("Icon");icon.gameObject.SetActive(!ante);((RectTransform)icon).anchoredPosition=new Vector2(-width*.5f+12,0);
            var label=face.Find("ZoneCaption").GetComponent<Text>();label.rectTransform.pivot=new Vector2(0,.5f);label.rectTransform.anchoredPosition=new Vector2(-width*.5f+(ante?6:25),0);label.rectTransform.sizeDelta=new Vector2(width-(ante?12:28),40);
            var separator=root.Find("Separator") as RectTransform;if(separator!=null)separator.anchoredPosition=new Vector2(-width*.5f,0);
        }
    }
    private void PresentZonePile(ForgePlayerView player,string path,string zone,int index,int count)
    {
        string pilePath=path+"/Pile_"+zone;
        var root=(RectTransform)Node(pilePath);
        ArenaPlace(pilePath,new Vector2(.5f,.5f),new Vector2((index-1)*114,0),new Vector2(110,40));
        var button=SceneButton(pilePath);
        button.interactable=true;button.transition=Selectable.Transition.None;
        button.onClick.AddListener(()=>OpenZoneBrowser(player.seat,zone));
        var image=root.GetComponent<Image>();image.sprite=IntegratedUiUtility.RoundedRect(84,40,4);image.color=Color.clear;
        var outline=root.GetComponent<Outline>();if(outline!=null)outline.enabled=false;
        var face=root.Find("ZoneEntry") as RectTransform;
        if(face==null)
        {
            foreach(Transform child in root)child.gameObject.SetActive(false);
            face=Rect(root,"ZoneEntry",Vector2.zero,new Vector2(108,40));
            var icon=Rect(face,"Icon",new Vector2(-44,0),new Vector2(18,18)).gameObject.AddComponent<ZoneRegionIcon>();
            icon.Kind=index;icon.color=new Color(.73f,.8f,.85f);
            var label=Label(face,"ZoneCaption","",new Vector2(10,0),new Vector2(86,40),12,Paper);label.alignment=TextAnchor.MiddleLeft;label.resizeTextForBestFit=false;
            Label(root,"HoverCount","",new Vector2(0,-34),new Vector2(68,26),18,Paper);
            if(index>0)Panel(root,"Separator",new Vector2(-44,0),new Vector2(1,20),new Color(.5f,.62f,.7f,.18f));
        }
        string caption=index==0?TapPaymentText("牌库","Library","ライブラリー"):index==1?TapPaymentText("坟墓场","Graveyard","墓地"):index==2?TapPaymentText("放逐","Exile","追放"):TapPaymentText("赌注","Ante","アンティ");
        var zoneLabel=face.Find("ZoneCaption").GetComponent<Text>();zoneLabel.text=caption+" "+count;PreferenceText.SetSize(zoneLabel,12);
        foreach(var graphic in root.GetComponentsInChildren<Graphic>(true))if(graphic.transform!=root)graphic.raycastTarget=false;
        var hover=root.GetComponent<ZonePileHover>()??root.gameObject.AddComponent<ZonePileHover>();
        hover.Configure(root.Find("HoverCount").GetComponent<Text>(),face,count,false);
    }
    private static ForgeCardView[] ZoneCards(ForgePlayerView player,string zone)
    {
        if(zone=="手牌")return player.hand??Array.Empty<ForgeCardView>();
        if(zone=="牌库")return player.reviewLibrary??Array.Empty<ForgeCardView>();
        if(zone=="放逐")return player.exileCards??player.reviewExile??Array.Empty<ForgeCardView>();
        if(zone=="赌注")return player.anteCards??Array.Empty<ForgeCardView>();
        if(zone=="垃圾场")return player.junkyardCards??Array.Empty<ForgeCardView>();
        if(player.graveyardCards!=null)return player.graveyardCards;
        if(player.reviewGraveyard!=null)return player.reviewGraveyard;
        // Older replays carried names only. Do not merge identical copies or invent live object IDs.
        return (player.graveyard??Array.Empty<string>()).Select((name,i)=>new ForgeCardView{id=-i-1,name=name,zone="Graveyard",ownerSeat=player.seat}).ToArray();
    }
    private void OpenZoneBrowser(int playerSeat,string zone)
    {
        if(IsCardPickerRequest || endDialog!=null&&endDialog.gameObject.activeSelf)return;
        CloseReviewCards();browsedSeat=playerSeat;browsedZone=zone;selectionPage=0;
        RefreshZoneBrowser();
    }
    private void OpenMeldComponents(int cardId)
    {
        if(IsCardPickerRequest || endDialog!=null&&endDialog.gameObject.activeSelf)return;
        CloseReviewCards();browsedComponentId=cardId;browsedZone="MeldComponent";selectionPage=0;
        RefreshZoneBrowser();
    }
    private static bool HasMeldComponents(ForgeCardView card)=>card!=null&&!card.faceDown&&card.zone=="Battlefield"
        &&card.stateName=="Meld"&&card.componentCards?.Length==2&&card.componentCards.All(c=>c!=null&&c.componentPreview&&!c.faceDown&&c.id>0)
        &&card.componentCards[0].id!=card.componentCards[1].id;
    private string ZoneTargetHint(ForgeCardView card)
    {
        string zone=browsedZone=="坟场"?"Graveyard":browsedZone=="放逐"?"Exile":browsedZone=="赌注"?"Ante":null;
        if(zone==null||card.id<0)return "";
        return string.Join("\n",(view?.stackItems??Array.Empty<ForgeStackItem>())
            .Select((entry,index)=>new {entry,order=index+1})
            .Where(item=>(item.entry.targets??Array.Empty<ForgeStackTarget>()).Any(target=>
                target.kind=="card"&&!target.leftZone&&target.id==card.id&&target.ownerSeat==browsedSeat&&target.zone==zone))
            .Select(item=>{
                var entry=item.entry;
                string name=entry.source?.faceDown==true?TapPaymentText("面朝下的咒语","Face-down spell","裏向きの呪文"):GamePreferences.CardFaceName(entry.source?.name);
                if(string.IsNullOrEmpty(name))name=TapPaymentText("堆叠中的对象","Stack object","スタック上のオブジェクト");
                return TapPaymentText($"成为「{name}」的目标（堆叠{item.order}）",$"Targeted by {name} (Stack {item.order})",$"「{name}」の対象（スタック{item.order}）");
            }));
    }
    private float ZoneTargetHintHeight(string[] hints,float width)
    {
        if(!hints.Any(h=>!string.IsNullOrEmpty(h)))return 0;
        var measure=Label(surface,"MeasureZoneTargetHint","",Vector2.zero,new Vector2(width-12,100),14,new Color(.65f,.85f,1));
        measure.supportRichText=false;measure.raycastTarget=false;measure.resizeTextForBestFit=false;
        float height=0;
        foreach(string hint in hints){measure.text=hint;height=Mathf.Max(height,Mathf.Ceil(measure.preferredHeight)+12);}
        measure.gameObject.SetActive(false);Destroy(measure.gameObject);
        return Mathf.Min(160,height)+6;
    }
    private void PresentZoneTargetHint(RectTransform tile,string text,float width,float height)
    {
        if(string.IsNullOrEmpty(text)||height<=0)return;
        var band=Panel(tile,"TargetedBy",new Vector2(0,-8-(height-6)/2),new Vector2(width,height-6),new Color(.06f,.14f,.21f));
        band.anchorMin=band.anchorMax=new Vector2(.5f,1);band.GetComponent<Image>().raycastTarget=false;
        var content=Scroll(band,"HintScroll",Vector2.zero,new Vector2(width-12,height-12));
        AddStackScrollIndicator(content);
        var label=Label(content,"Text",text,Vector2.zero,new Vector2(width-12,height-12),14,new Color(.65f,.85f,1));
        label.supportRichText=false;label.raycastTarget=false;label.resizeTextForBestFit=false;label.alignment=TextAnchor.UpperLeft;
        float textHeight=Mathf.Max(height-12,Mathf.Ceil(label.preferredHeight));
        content.sizeDelta=new Vector2(width-12,textHeight);label.rectTransform.sizeDelta=content.sizeDelta;
        label.rectTransform.anchorMin=label.rectTransform.anchorMax=label.rectTransform.pivot=new Vector2(.5f,1);label.rectTransform.anchoredPosition=Vector2.zero;
    }
    private void RefreshZoneBrowser()
    {
        if(browsedComponentId>0) {
            var source=(view?.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>()).FirstOrDefault(c=>c.id==browsedComponentId);
            if(!HasMeldComponents(source)){CloseReviewCards();return;}
            string componentSignature=browsedComponentId+"/"+surface.rect.size+"/"+GamePreferences.Revision+"/"+ServerSnapshotJson.Serialize(source);
            if(componentSignature==zoneBrowserSignature&&cardSelectionOverlay!=null&&cardSelectionOverlay.gameObject.activeSelf)return;
            zoneBrowserSignature=componentSignature;
            if(endDetail!=null)Destroy(endDetail.gameObject);
            RenderCardSelection(new ForgeRequest{kind="BrowseComponents",options=Array.Empty<ForgeOption>(),previewCards=source.componentCards},
                TapPaymentText("融合原件","Meld components","合体の構成カード"));
            selectionPanel.Find("Instruction").GetComponent<Text>().text=TapPaymentText("仅查看两张原件正面，不可单独选择。","Printed fronts only; not separate targets.","元の2枚の表面です。個別には選べません。");
            return;
        }
        var player=view?.players?.FirstOrDefault(p=>p.seat==browsedSeat);
        if(player==null){CloseReviewCards();return;}
        bool library=browsedZone=="牌库";
        var cards=library?LibraryBrowserCards(player):ZoneCards(player,browsedZone);
        cards=cards.Select(c=>{
            if(!c.faceDown||string.IsNullOrEmpty(c.knownFaceName)||service?.IsSpectator==true)return c;
            var visible=ServerSnapshotJson.Copy(c);
            visible.faceDown=false;visible.name=c.knownFaceName;visible.printingKey=c.knownFacePrintingKey;
            visible.currentRules=c.knownFaceRules;return visible;
        }).ToArray();
        string signature=browsedSeat+"/"+browsedZone+"/"+surface.rect.size+"/"+GamePreferences.Revision+"/"+player.libraryCount+"/"+string.Join("|",cards.Select(ServerSnapshotJson.Serialize))+"/"+string.Join("|",cards.Select(ZoneTargetHint));
        if(library)signature+="/"+view?.request?.requestId+"/"+canAct+"/"+submitted;
        if(signature==zoneBrowserSignature && cardSelectionOverlay!=null && cardSelectionOverlay.gameObject.activeSelf)return;
        zoneBrowserSignature=signature;
        if(endDetail!=null)Destroy(endDetail.gameObject);
        string title=(player.seat==seat?"你的":"对手的")+(browsedZone=="手牌"?"已知手牌":browsedZone=="坟场"?"坟墓场":browsedZone=="放逐"?"放逐区":browsedZone=="赌注"?"赌注区":"剩余牌库");
        if(browsedZone=="赌注")title=TapPaymentText((player.seat==seat?"你的":"对手的")+"赌注区",(player.seat==seat?"Your":"Opponent's")+" ante",(player.seat==seat?"あなた":"対戦相手")+"のアンティ");
        RenderCardSelection(new ForgeRequest{kind=library?"BrowseLibrary":"BrowseZone",options=Array.Empty<ForgeOption>(),previewCards=cards},title);
        if(browsedZone=="垃圾场"){
            selectionPanel.Find("Title").GetComponent<Text>().text=TapPaymentText((player.seat==seat?"你的":"对手的")+"垃圾场",(player.seat==seat?"Your":"Opponent's")+" scrapyard",(player.seat==seat?"あなた":"対戦相手")+"の廃品置き場");
            selectionPanel.Find("Instruction").GetComponent<Text>().text=AuxiliarySummary(player);
        }
        if(browsedZone=="手牌") {
            selectionPanel.Find("Count").GetComponent<Text>().text=player.handCount+" 张";
            selectionPanel.Find("Instruction").GetComponent<Text>().text=TapPaymentText("仅显示已知手牌；未看过的牌保持隐藏。","Only known hand cards are shown; unseen cards remain hidden.","既知の手札のみ表示します。見ていないカードは非公開です。");
            var empty=selectionPanel.Find("Empty");if(empty!=null)empty.GetComponent<Text>().text=TapPaymentText("没有已知手牌。","No known hand cards.","既知の手札はありません。");
        }
        if(library) {
            selectionPanel.Find("Instruction").GetComponent<Text>().text=LibraryBrowserInstruction;
            var empty=selectionPanel.Find("Empty");if(empty!=null)empty.GetComponent<Text>().text=TapPaymentText("牌库为空。","Library is empty.","ライブラリーは空です。");
        }
    }
    private static string LibraryBrowserInstruction=>TapPaymentText("按牌库顶至底排列；未公开的位置显示牌背。","Top to bottom; hidden positions show card backs.","上から下の順。非公開の位置はカード裏面です。");
    private ForgeCardView[] LibraryBrowserCards(ForgePlayerView player)
    {
        int count=Math.Max(0,player.libraryCount);
        var cards=Enumerable.Range(1,count).Select(position=>new ForgeCardView{
            id=-position,ownerSeat=player.seat,zone="Library",faceDown=true,name="Face-down card",libraryPosition=position
        }).ToArray();
        void Show(int position,ForgeCardView card){
            if(position<1||position>count||card==null||card.faceDown||string.IsNullOrEmpty(card.name))return;
            var visible=ServerSnapshotJson.Copy(card);visible.libraryPosition=position;cards[position-1]=visible;
        }
        if(player.seat==seat&&service?.IsSpectator!=true&&player.reviewLibrary?.Length==count)
            for(int i=0;i<count;i++)Show(i+1,player.reviewLibrary[i]);
        else {
            if(service?.IsSpectator!=true||player.libraryTopPublic)Show(1,player.libraryTop);
            foreach(var known in player.libraryCards??Array.Empty<ForgeLibraryPosition>())
                if(known!=null&&(service?.IsSpectator!=true||known.publicInfo))Show(known.position,known.card);
        }
        return cards;
    }
    private static string LibraryPositionLabel(int position,int count)=>TapPaymentText(
        "第 "+position+" 张","#"+position,position+"枚目")+(position==1&&position==count?TapPaymentText(" · 牌库顶／底"," · Top / bottom"," · 一番上／下"):
        position==1?TapPaymentText(" · 牌库顶"," · Top"," · 一番上"):position==count?TapPaymentText(" · 牌库底"," · Bottom"," · 一番下"):"");
}
