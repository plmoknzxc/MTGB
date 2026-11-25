using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private int browsedSeat;
    private int browsedComponentId;
    private string browsedZone, zoneBrowserSignature;
    private void StyleZoneSheet(RectTransform panel,float extraFooter=0)
    {
        Color paper=HudInk;paper.a=1f;
        StyleHudSurface(panel,paper,HudEdge);
        panel.GetComponent<Image>().sprite=IntegratedUiUtility.SlicedRoundedRect(6);
        var head=Rect(panel,"ZoneHeader",Vector2.zero,Vector2.zero);
        head.anchorMin=Vector2.up;head.anchorMax=Vector2.one;head.pivot=new Vector2(.5f,1);
        head.sizeDelta=new Vector2(-6,55);head.anchoredPosition=new Vector2(0,-3);
        BattlefieldHudGraphic.Apply(head,HudTheme,BattlefieldHudGraphic.Surface.JournalHeader,HudAccent);
        var rule=Panel(head,"Rule",Vector2.zero,Vector2.zero,HudRule);
        rule.anchorMin=Vector2.zero;rule.anchorMax=Vector2.right;rule.sizeDelta=new Vector2(-36,1);
        rule.GetComponent<Image>().raycastTarget=false;
        var footerRule=Panel(panel,"FooterRule",Vector2.zero,Vector2.zero,HudRule);
        footerRule.anchorMin=Vector2.zero;footerRule.anchorMax=Vector2.right;
        footerRule.sizeDelta=new Vector2(-40,1);footerRule.anchoredPosition=new Vector2(0,66+extraFooter);
        footerRule.GetComponent<Image>().raycastTarget=false;
    }
    private void StyleZoneButton(Button button,bool primary=false)
    {
        MenuVisualTheme.StyleArchiveControl(button,primary);
        var label=button.GetComponentInChildren<Text>(true);
        MenuTypography.Use(label,MenuTypography.Face.Control);
        label.color=primary?HudTheme.ActionText:HudText;
    }
    private void StylePickerTile(RectTransform tile,bool chosen,bool targeted,bool selectable)
    {
        var image=tile.GetComponent<Image>();
        if(HudTheme==null){
            image.color=chosen?new Color(.16f,.18f,.20f):selectable?new Color(.18f,.45f,.58f):new Color(.12f,.16f,.21f);
            return;
        }
        image.sprite=IntegratedUiUtility.SlicedRoundedRect(8);image.type=Image.Type.Sliced;
        image.color=Color.Lerp(HudRaised,chosen?HudAccent:HudEdge,chosen?.13f:selectable||targeted?.075f:.018f);
        var edge=RedesignStackFrame(tile,8,targeted?Color.Lerp(HudInk,HudTheme.MainAccent,.65f):Color.Lerp(HudInk,HudTheme.LibraryAccent,.45f));
        edge.Thickness=targeted?1.45f:1f;
        edge.enabled=!chosen&&(targeted||selectable);
    }
    private void PresentPickerCost(RectTransform tile,string cost,float width,float hintHeight,float height)
    {
        var line=CardSymbolText.Create(tile,"PickerManaCost",16,HudText);
        line.text=CardSymbolText.Format(TapPaymentText("费用  ","Cost  ","コスト  ")+cost);
        line.alignment=TMPro.TextAlignmentOptions.MidlineRight;line.enableAutoSizing=true;
        line.fontSizeMax=line.fontSize;line.fontSizeMin=11;
        RedesignPlace(line.rectTransform,Vector2.up,Vector2.up,new Vector2((tile.rect.width-width)/2,-8-hintHeight),new Vector2(width,height-4));
    }
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
        shell.sizeDelta=new Vector2(342,140);
        var portraitArt=Node(plate+"/Avatar").GetComponentInChildren<PlayerAvatarGraphic>();
        if(portraitArt!=null){
            var member=service?.LobbyState?.players?.FirstOrDefault(p=>p.seat==player.seat);
            bool local=own && service?.IsSpectator!=true && replayPlayerNames==null;
            string avatar=local?GamePreferences.AvatarId:member?.avatarId;
            if(!PlayerAvatarGraphic.Valid(avatar))avatar=member?.isAi==true||player.seat!=1?PlayerAvatarGraphic.RivalId:PlayerAvatarGraphic.DefaultId;
            portraitArt.SetAvatar(IsFourPlayerScene ? FourPlayerPortrait(player.seat) : avatar);
        }
        if (HudTheme != null)
        {
            var identity = own ? HudTheme.MainAccent : HudTheme.SideAccent;
            DuelSurface(shell,BattlefieldHudGraphic.Surface.Seat,identity);
            Node(plate).GetComponent<Image>().color = Color.clear;
            SceneText(plate+"/Life").color = HudText;
            SceneText(plate+"/Sub").color = HudMuted;
            var portrait = Node(plate+"/Avatar").GetComponent<Image>();
            portraitArt.rectTransform.anchorMin=Vector2.zero;
            portraitArt.rectTransform.anchorMax=Vector2.one;
            portraitArt.rectTransform.offsetMin=portraitArt.rectTransform.offsetMax=Vector2.zero;
            var targetButton=Node(plate).GetComponent<Button>();
            DuelSurface(portrait.rectTransform,BattlefieldHudGraphic.Surface.Portrait,targetButton.interactable?HudAccent:identity,targetButton.interactable);
            portrait.rectTransform.anchoredPosition=new Vector2(-132,0);
            portrait.rectTransform.sizeDelta=new Vector2(82,82);
            Node(plate+"/FactionBar").gameObject.SetActive(false);
            Node(plate+"/Edge").gameObject.SetActive(false);
            var lifeRule=(RectTransform)Node(plate+"/Divider");
            lifeRule.gameObject.SetActive(true);
            lifeRule.anchoredPosition=new Vector2(66,0);
            lifeRule.sizeDelta=new Vector2(1,53);
            lifeRule.GetComponent<Image>().color=Color.Lerp(HudInk,HudMuted,.40f);
        }
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
        ((RectTransform)node.Find("BarDivider")).anchoredPosition=new Vector2(0,own?-23:23);
        if(HudTheme!=null){
            var rule=(RectTransform)node.Find("BarDivider");
            rule.sizeDelta=new Vector2(326,1);
            rule.GetComponent<Image>().color=Color.Lerp(HudInk,HudMuted,.44f);
        }
        // Known hand browsing is read-only. Targeting still owns clicks on
        // the player plate while an authoritative player choice is active.
        var handLabel=SceneText(plate+"/Sub");
        var handBrowser=handLabel.GetComponent<Button>()??handLabel.gameObject.AddComponent<Button>();
        handBrowser.targetGraphic=handLabel;handBrowser.transition=Selectable.Transition.None;handBrowser.onClick.RemoveAllListeners();
        handBrowser.interactable=player.handCount>0&&!IsTargeting&&!IsDeclaringAttack&&SelectionHand==null;
        handLabel.raycastTarget=handBrowser.interactable;handBrowser.onClick.AddListener(()=>OpenZoneBrowser(player.seat,"手牌"));
        if(IsFourPlayerScene && own){
            handLabel.text=FourCommanderNames(player);
            bool pair=player.commanders?.Length>1;
            var rect=handLabel.rectTransform;
            rect.anchoredPosition=new Vector2(-10,pair?-12:-18);
            rect.sizeDelta=new Vector2(156,pair?53:30);
            handLabel.horizontalOverflow=HorizontalWrapMode.Wrap;
            handLabel.color=HudText;
            handLabel.resizeTextForBestFit=true;
            handLabel.resizeTextMinSize=16;
            handLabel.resizeTextMaxSize=20;
            handBrowser.targetGraphic=null;
            handLabel.CrossFadeColor(Color.white,0,true,true);
            handBrowser.onClick.RemoveAllListeners();
            handBrowser.interactable=CanBrowseZone(player,"指挥官")&&!IsTargeting&&!IsDeclaringAttack;
            handLabel.raycastTarget=handBrowser.interactable;
            handBrowser.onClick.AddListener(()=>OpenZoneBrowser(player.seat,"指挥官"));
            var tax=Node(plate).Find("CommanderTax");
            var destination=FourLocalPager().Find("Tax").GetComponent<Text>();
            bool relocate=pair && tax!=null && tax.gameObject.activeSelf;
            destination.gameObject.SetActive(relocate);
            if(relocate){
                destination.text=tax.GetComponent<Text>().text;
                tax.gameObject.SetActive(false);
            }
        }
        PresentZonePile(player,path,"牌库",0,player.libraryCount);
        var grave=ZoneCards(player,"坟场");var exile=ZoneCards(player,"放逐");
        PresentZonePile(player,path,"坟场",1,grave.Length);
        PresentZonePile(player,path,"放逐",2,Math.Max(player.exileCount,exile.Length));
        bool ante=view?.anteEnabled==true;
        bool command=view?.formatId is "duelcommander" or "brawl";
        var anteEntry=node.Find("Pile_赌注");
        if(ante&&anteEntry==null)anteEntry=Panel(node,"Pile_赌注",Vector2.zero,new Vector2(80,40),Color.clear);
        if(anteEntry!=null)anteEntry.gameObject.SetActive(ante);
        if(ante)PresentZonePile(player,path,"赌注",3,ZoneCards(player,"赌注").Length);
        var commandEntry=node.Find("Pile_统帅区");
        if(command&&commandEntry==null)commandEntry=Panel(node,"Pile_统帅区",Vector2.zero,new Vector2(80,40),Color.clear);
        if(commandEntry!=null)commandEntry.gameObject.SetActive(command);
        if(command)PresentZonePile(player,path,"统帅区",3,ZoneCards(player,"统帅区").Length);
        PresentAuxiliaryEntry(player,node);
        var zones=(command?new[]{"牌库","坟场","放逐","统帅区"}:ante?new[]{"牌库","坟场","放逐","赌注"}:new[]{"牌库","坟场","放逐"}).ToList();
        bool sideboard=IsRedesignLayout&&own&&!command&&service?.IsSpectator!=true&&player.sideboardCards?.Length>0;
        var sideboardEntry=node.Find("Pile_备牌");
        if(sideboard&&sideboardEntry==null)sideboardEntry=Panel(node,"Pile_备牌",Vector2.zero,new Vector2(80,40),Color.clear);
        if(sideboardEntry!=null)sideboardEntry.gameObject.SetActive(sideboard);
        if(sideboard){PresentZonePile(player,path,"备牌",4,player.sideboardCards?.Length??0);zones.Add("备牌");}
        var entries=zones.Select(z=>(RectTransform)Node(path+"/Pile_"+z)).ToArray();
        if(IsRedesignLayout){
            var labels=entries.Select(e=>e.Find("ZoneEntry/ZoneCaption").GetComponent<Text>()).ToArray();
            foreach(var label in labels){label.fontStyle=FontStyle.Normal;label.horizontalOverflow=HorizontalWrapMode.Overflow;}
            // Share a single row by actual caption width. Long locales may reduce
            // the enlarged text together, without wrapping individual entries.
            float textWidth=labels.Sum(l=>l.preferredWidth),padding=entries.Length*10;
            if(textWidth+padding>342){
                // Floor the final pixel size; rounding it up can push the last
                // entry outside the row after localization or a three-digit count.
                float baseSize=Mathf.Max(1,Mathf.Floor(labels[0].fontSize*(342-padding)/textWidth))/GamePreferences.TextScale;
                foreach(var label in labels)PreferenceText.SetSize(label,baseSize);
            }
            var sizes=labels.Select(l=>l.preferredWidth+10).ToArray();
            float total=sizes.Sum(),compression=Mathf.Min(1,342/total);
            float spare=Mathf.Max(0,(342-total)/entries.Length),left=-171;
            for(int i=0;i<entries.Length;i++){
                float width=sizes[i]*compression+spare;
                var root=entries[i];root.sizeDelta=new Vector2(width,40);root.anchoredPosition=new Vector2(left+width*.5f,0);left+=width;
                var face=(RectTransform)root.Find("ZoneEntry");face.sizeDelta=new Vector2(width-2,40);
                face.Find("Icon").gameObject.SetActive(false);
                var label=labels[i];label.alignment=TextAnchor.MiddleCenter;
                label.rectTransform.pivot=new Vector2(.5f,.5f);label.rectTransform.anchoredPosition=Vector2.zero;label.rectTransform.sizeDelta=new Vector2(width-6,40);
                if(label.preferredWidth>width-6)PreferenceText.SetSize(label,Mathf.Max(1,Mathf.Floor(label.fontSize*(width-6)/label.preferredWidth))/GamePreferences.TextScale);
                var separator=root.Find("Separator") as RectTransform;
                if(separator!=null){
                    separator.gameObject.SetActive(HudTheme!=null&&i>0);
                    separator.anchoredPosition=new Vector2(-width*.5f,0);separator.sizeDelta=new Vector2(1,14);
                    separator.GetComponent<Image>().color=HudRule;
                }
            }
            return;
        }
        // Ante uses the same fixed row. Omit the decorative icons to keep four captions legible.
        var widths=entries.Select(e=>e.Find("ZoneEntry/ZoneCaption").GetComponent<Text>().preferredWidth+(ante||command?12:32)).ToArray();
        float extra=Mathf.Max(0,(342-widths.Sum())/entries.Length),cursor=-widths.Sum()*.5f-extra*entries.Length*.5f;
        for(int i=0;i<entries.Length;i++){
            var root=entries[i];float width=widths[i]+extra;root.sizeDelta=new Vector2(width,40);root.anchoredPosition=new Vector2(cursor+width*.5f,0);cursor+=width;
            var face=(RectTransform)root.Find("ZoneEntry");face.sizeDelta=new Vector2(width-2,40);
            var icon=face.Find("Icon");icon.gameObject.SetActive(!ante&&!command);((RectTransform)icon).anchoredPosition=new Vector2(-width*.5f+12,0);
            var label=face.Find("ZoneCaption").GetComponent<Text>();label.rectTransform.pivot=new Vector2(0,.5f);label.rectTransform.anchoredPosition=new Vector2(-width*.5f+(ante||command?6:25),0);label.rectTransform.sizeDelta=new Vector2(width-(ante||command?12:28),40);
            var separator=root.Find("Separator") as RectTransform;if(separator!=null)separator.anchoredPosition=new Vector2(-width*.5f,0);
        }
    }
    private void PresentZonePile(ForgePlayerView player,string path,string zone,int index,int count)
    {
        string pilePath=path+"/Pile_"+zone;
        var root=(RectTransform)Node(pilePath);
        ArenaPlace(pilePath,new Vector2(.5f,.5f),new Vector2((index-1)*114,0),new Vector2(110,40));
        var button=SceneButton(pilePath);
        button.interactable=count>0;button.transition=Selectable.Transition.None;
        if(button.interactable)button.onClick.AddListener(()=>OpenZoneBrowser(player.seat,zone));
        var image=root.GetComponent<Image>();image.color=Color.clear;image.raycastTarget=button.interactable;
        var outline=root.GetComponent<Outline>();if(outline!=null)outline.enabled=false;
        var face=root.Find("ZoneEntry") as RectTransform;
        if(face==null)
        {
            foreach(Transform child in root)child.gameObject.SetActive(false);
            face=Rect(root,"ZoneEntry",Vector2.zero,new Vector2(108,40));
            var icon=Rect(face,"Icon",new Vector2(-44,0),new Vector2(18,18)).gameObject.AddComponent<ZoneRegionIcon>();
            icon.Kind=index;icon.color=new Color(.73f,.8f,.85f);
            var label=Label(face,"ZoneCaption","",new Vector2(10,0),new Vector2(86,40),12,Paper);label.alignment=TextAnchor.MiddleLeft;label.resizeTextForBestFit=false;
            if(index>0)Panel(root,"Separator",new Vector2(-44,0),new Vector2(1,20),new Color(.5f,.62f,.7f,.18f));
        }
        var underline=face.Find("FocusRule") as RectTransform;
        if(underline==null){
            underline=Panel(face,"FocusRule",Vector2.zero,Vector2.zero,Color.clear);
            underline.anchorMin=new Vector2(.16f,0);underline.anchorMax=new Vector2(.84f,0);
            underline.pivot=new Vector2(.5f,0);underline.anchoredPosition=new Vector2(0,4);
            underline.sizeDelta=new Vector2(0,2);underline.GetComponent<Image>().raycastTarget=false;
            face.localScale=Vector3.one;
            root.Find("HoverCount")?.gameObject.SetActive(false);
        }
        string caption=zone=="备牌"?TapPaymentText("备牌","Sideboard","サイドボード"):zone=="统帅区"?TapPaymentText("统帅区","Command","統率領域"):index==0?TapPaymentText("牌库","Library","ライブラリー"):index==1?TapPaymentText("坟墓场","Graveyard","墓地"):index==2?TapPaymentText("放逐","Exile","追放"):TapPaymentText("赌注","Ante","アンティ");
        var zoneLabel=face.Find("ZoneCaption").GetComponent<Text>();zoneLabel.text=caption+" "+count;PreferenceText.SetSize(zoneLabel,IsRedesignLayout?16:12);
        if(HudTheme!=null){zoneLabel.color=HudText;MenuTypography.Use(zoneLabel,MenuTypography.Face.Control);}
        var payable=zone=="备牌"&&player.seat==seat&&service?.IsSpectator!=true&&canAct&&!submitted
            ?RedesignPayableSourceIds(view):Array.Empty<int>();
        bool available=(player.sideboardCards??Array.Empty<ForgeCardView>()).Any(c=>payable.Contains(c.id));
        foreach(var graphic in root.GetComponentsInChildren<Graphic>(true))if(graphic.transform!=root)graphic.raycastTarget=false;
        var hover=root.GetComponent<ZonePileHover>()??root.gameObject.AddComponent<ZonePileHover>();
        Color accent=HudTheme!=null?(Color)HudTheme.LibraryAccent:new Color(.58f,.84f,.9f);
        hover.Configure(zoneLabel,underline.GetComponent<Image>(),button.interactable,available,HudText,HudMuted,accent);
    }
    private static ForgeCardView[] ZoneCards(ForgePlayerView player,string zone)
    {
        if(zone=="手牌")return player.hand??Array.Empty<ForgeCardView>();
        if(zone=="备牌")return player.sideboardCards??Array.Empty<ForgeCardView>();
        if(zone=="牌库")return player.reviewLibrary??Array.Empty<ForgeCardView>();
        if(zone=="放逐")return player.exileCards??player.reviewExile??Array.Empty<ForgeCardView>();
        if(zone=="赌注")return player.anteCards??Array.Empty<ForgeCardView>();
        if(zone=="统帅区")return player.commandCards??player.commanders??Array.Empty<ForgeCardView>();
        if(zone=="垃圾场")return player.junkyardCards??Array.Empty<ForgeCardView>();
        if(player.graveyardCards!=null)return player.graveyardCards;
        if(player.reviewGraveyard!=null)return player.reviewGraveyard;
        // Older replays carried names only. Do not merge identical copies or invent live object IDs.
        return (player.graveyard??Array.Empty<string>()).Select((name,i)=>new ForgeCardView{id=-i-1,name=name,zone="Graveyard",ownerSeat=player.seat}).ToArray();
    }
    private void OpenZoneBrowser(int playerSeat,string zone)
    {
        if(IsCardPickerRequest || endDialog!=null&&endDialog.gameObject.activeSelf)return;
        var player=view?.players?.FirstOrDefault(p=>p.seat==playerSeat);
        if(!CanBrowseZone(player,zone))return;
        CloseReviewCards();browsedSeat=playerSeat;browsedZone=zone;selectionPage=0;
        RefreshZoneBrowser();
    }
    private ForgeCardView[] ZoneBrowserContents(ForgePlayerView player,string zone)
        =>IsFourPlayerScene && (zone!="手牌"||player.seat!=seat) && Array.Exists(FourPlateSources,s=>s.key==zone)
            ?FourSourceCards(player,zone):ZoneCards(player,zone);

    private bool CanBrowseZone(ForgePlayerView player,string zone)
    {
        if(player==null)return false;
        switch(zone){
            case "牌库":return player.libraryCount>0;
            case "手牌":return player.handCount>0;
            case "备牌":return player.seat==seat&&service?.IsSpectator!=true&&player.sideboardCards?.Length>0;
            case "放逐":return player.exileCount>0||ZoneCards(player,zone).Length>0;
            // The auxiliary viewer also reports the two decks and the active sprocket.
            case "垃圾场":return player.attractionDeckCount>0||player.contraptionDeckCount>0||
                ZoneCards(player,zone).Length>0||(player.battlefield??Array.Empty<ForgeCardView>()).Any(c=>c.attraction||c.contraption);
            default:return ZoneBrowserContents(player,zone).Length>0;
        }
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
        string zone=card.zone;
        if(string.IsNullOrEmpty(zone)||card.id<=0)return "";
        return ConfirmedTargetHint(target=>target.kind=="card"&&!target.leftZone&&target.id==card.id&&target.ownerSeat==card.ownerSeat&&target.zone==zone,out _);
    }
    private (string heading,string body) StackIncomingTargetHint(ForgeStackItem entry)
    {
        if(entry==null||entry.id<=0)return ("","");
        string body=ConfirmedTargetHint(target=>target.kind=="stack"&&!target.leftZone&&target.stackId==entry.id,out int count,groupStackSources:true);
        return (count>1?StackTargetSourceCount(count):"",body);
    }
    private string StackTargetSourceCount(int count)=>TapPaymentText($"被 {count} 个来源指定",$"Targeted by {count} sources",$"{count}件の発生源が対象に指定");
    private string ConfirmedTargetHint(Func<ForgeStackTarget,bool> matches,out int count,bool groupStackSources=false)
    {
        var sources=(view?.stackItems??Array.Empty<ForgeStackItem>())
            .Select((entry,index)=>new {entry,order=index+1})
            .Where(item=>(item.entry?.targets??Array.Empty<ForgeStackTarget>()).Any(matches)).ToArray();
        count=sources.Length;
        bool grouped=groupStackSources&&count>1;
        var lines=sources.Select(item=>{
            var entry=item.entry;
            string name=entry.source?.faceDown==true?TapPaymentText("面朝下的咒语","Face-down spell","裏向きの呪文"):GamePreferences.CardFaceName(entry.source?.name);
            if(string.IsNullOrEmpty(name))name=TapPaymentText("堆叠中的对象","Stack object","スタック上のオブジェクト");
            string text=grouped?$"{item.order:00}   {name}":TapPaymentText($"成为「{name}」的目标（堆叠{item.order}）",$"Targeted by {name} (Stack {item.order})",$"「{name}」の対象（スタック{item.order}）");
            int times=entry.targets.Count(matches);
            if(times>1)text+=" ×"+times;
            return ColoredDetail(text,"#"+ColorUtility.ToHtmlStringRGB(StackSeatTint(entry.controllerSeat)));
        });
        return string.Join("\n",lines);
    }
    private float ZoneTargetHintHeight(string[] hints,float width)
    {
        if(!hints.Any(h=>!string.IsNullOrEmpty(h)))return 0;
        var measure=Label(surface,"MeasureZoneTargetHint","",Vector2.zero,new Vector2(width-12,100),14,new Color(.65f,.85f,1));
        measure.supportRichText=true;measure.raycastTarget=false;measure.resizeTextForBestFit=false;
        (measure.GetComponent<PreferenceText>()??measure.gameObject.AddComponent<PreferenceText>()).Literal=true;
        float height=0;
        foreach(string hint in hints){measure.text=hint;height=Mathf.Max(height,Mathf.Ceil(measure.preferredHeight)+12);}
        measure.gameObject.SetActive(false);Destroy(measure.gameObject);
        return Mathf.Min(160,height)+6;
    }
    private void PresentZoneTargetHint(RectTransform tile,string text,float width,float height,string heading=null,float scrollOffset=0)
    {
        if(string.IsNullOrEmpty(text)||height<=0)return;
        var band=Panel(tile,"TargetedBy",new Vector2(0,-8-(height-6)/2),new Vector2(width,height-6),HudTheme!=null?Color.clear:new Color(.06f,.14f,.21f));
        band.anchorMin=band.anchorMax=new Vector2(.5f,1);band.GetComponent<Image>().raycastTarget=false;
        float headingHeight=0;
        if(!string.IsNullOrEmpty(heading)){
            var title=Label(band,"SourceCount",heading,Vector2.zero,new Vector2(width-12,height),14,HudText);
            title.raycastTarget=false;title.resizeTextForBestFit=false;title.alignment=TextAnchor.UpperLeft;
            (title.GetComponent<PreferenceText>()??title.gameObject.AddComponent<PreferenceText>()).Literal=true;
            headingHeight=Mathf.Ceil(title.preferredHeight);
            var rect=title.rectTransform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);
            rect.anchoredPosition=new Vector2(0,-3);rect.sizeDelta=new Vector2(width-12,headingHeight);
        }
        float bodyHeight=height-12-headingHeight;
        var content=Scroll(band,"HintScroll",new Vector2(0,-headingHeight*.5f),new Vector2(width-12,bodyHeight));
        if(HudTheme!=null)content.parent.GetComponent<Image>().color=Color.clear;
        AddStackScrollIndicator(content, 10);
        var label=Label(content,"Text",text,Vector2.zero,new Vector2(width-12,bodyHeight),14,new Color(.65f,.85f,1));
        label.supportRichText=true;label.raycastTarget=false;label.resizeTextForBestFit=false;label.alignment=TextAnchor.UpperLeft;
        (label.GetComponent<PreferenceText>()??label.gameObject.AddComponent<PreferenceText>()).Literal=true;
        float textHeight=Mathf.Max(bodyHeight,Mathf.Ceil(label.preferredHeight));
        content.sizeDelta=new Vector2(width-12,textHeight);label.rectTransform.sizeDelta=content.sizeDelta;
        label.rectTransform.anchorMin=label.rectTransform.anchorMax=label.rectTransform.pivot=new Vector2(.5f,1);label.rectTransform.anchoredPosition=Vector2.zero;
        content.anchoredPosition=Vector2.up*Mathf.Clamp(scrollOffset,0,textHeight-bodyHeight);
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
        if(!CanBrowseZone(player,browsedZone)){CloseReviewCards();return;}
        bool library=browsedZone=="牌库";
        var cards=library?LibraryBrowserCards(player):ZoneBrowserContents(player,browsedZone);
        cards=cards.Select(c=>{
            if(!c.faceDown||string.IsNullOrEmpty(c.knownFaceName)||service?.IsSpectator==true)return c;
            var visible=ServerSnapshotJson.Copy(c);
            visible.faceDown=false;visible.name=c.knownFaceName;visible.printingKey=c.knownFacePrintingKey;
            visible.currentRules=c.knownFaceRules;return visible;
        }).ToArray();
        bool fourHand=IsFourPlayerScene && browsedZone=="手牌" && player.seat!=seat;
        string signature=browsedSeat+"/"+browsedZone+"/"+surface.rect.size+"/"+GamePreferences.Revision+"/"+player.libraryCount+"/"+player.handCount+"/"+player.exileCount+"/"+string.Join("|",cards.Select(ServerSnapshotJson.Serialize))+"/"+string.Join("|",cards.Select(ZoneTargetHint));
        signature+="/"+view?.request?.requestId+"/"+canAct+"/"+submitted+"/"+PickerAuthoritySignature();
        if(signature==zoneBrowserSignature && cardSelectionOverlay!=null && cardSelectionOverlay.gameObject.activeSelf)return;
        zoneBrowserSignature=signature;
        if(endDetail!=null)Destroy(endDetail.gameObject);
        string title=(player.seat==seat?"你的":"对手的")+(browsedZone=="手牌"?"已知手牌":browsedZone=="坟场"?"坟墓场":browsedZone=="放逐"?"放逐区":browsedZone=="赌注"?"赌注区":"剩余牌库");
        if(browsedZone=="赌注")title=TapPaymentText((player.seat==seat?"你的":"对手的")+"赌注区",(player.seat==seat?"Your":"Opponent's")+" ante",(player.seat==seat?"あなた":"対戦相手")+"のアンティ");
        if(browsedZone=="统帅区")title=TapPaymentText((player.seat==seat?"你的":"对手的")+"统帅区",(player.seat==seat?"Your":"Opponent's")+" command zone",(player.seat==seat?"あなた":"対戦相手")+"の統率領域");
        if(IsFourPlayerScene) {
            string name=PlayerDisplayName(player.seat);
            var zone=System.Array.Find(FourPlateZones,z=>z.key==browsedZone);
            if(zone.key==null)zone=Array.Find(FourPlateSources,z=>z.key==browsedZone);
            if(zone.key!=null)title=TapPaymentText(name+" · "+(zone.key=="放逐"?"放逐区":zone.zh),name+" · "+zone.en,name+" · "+zone.ja);
        }
        if(browsedZone=="备牌")title=TapPaymentText("你的备牌","Your sideboard","あなたのサイドボード");
        RenderCardSelection(new ForgeRequest{kind=library?"BrowseLibrary":browsedZone=="备牌"?"BrowseSideboard":"BrowseZone",options=Array.Empty<ForgeOption>(),previewCards=cards},title);
        if(browsedZone=="放逐"&&player.exileCount>cards.Length){
            int total=player.exileCount;
            selectionPanel.Find("Count").GetComponent<Text>().text=TapPaymentText($"{total} 张",$"{total} cards",$"{total} 枚");
            selectionPanel.Find("Instruction").GetComponent<Text>().text=TapPaymentText("仅展示当前可查看的放逐牌。","Only visible exiled cards are shown.","閲覧可能な追放カードのみを表示します。");
            var empty=selectionPanel.Find("Empty");
            if(empty!=null)empty.GetComponent<Text>().text=TapPaymentText("没有可查看的牌。","No visible cards.","閲覧可能なカードはありません。");
        }
        if(browsedZone=="垃圾场"){
            selectionPanel.Find("Title").GetComponent<Text>().text=TapPaymentText((player.seat==seat?"你的":"对手的")+"垃圾场",(player.seat==seat?"Your":"Opponent's")+" scrapyard",(player.seat==seat?"あなた":"対戦相手")+"の廃品置き場");
            selectionPanel.Find("Instruction").GetComponent<Text>().text=AuxiliarySummary(player);
        }
        if(browsedZone=="手牌") {
            selectionPanel.Find("Count").GetComponent<Text>().text=TapPaymentText(player.handCount+" 张",player.handCount+" cards",player.handCount+" 枚");
            selectionPanel.Find("Instruction").GetComponent<Text>().text=TapPaymentText("仅显示已知手牌；未看过的牌保持隐藏。","Only known hand cards are shown; unseen cards remain hidden.","既知の手札のみ表示します。見ていないカードは非公開です。");
            if(fourHand)selectionPanel.Find("Instruction").GetComponent<Text>().text=TapPaymentText("已知手牌显示正面，其余显示牌背。","Known cards are face up. Other cards show their backs.","既知の手札は表向き、それ以外は裏向きで表示します。");
            var empty=selectionPanel.Find("Empty");if(empty!=null)empty.GetComponent<Text>().text=TapPaymentText("没有已知手牌。","No known hand cards.","既知の手札はありません。");
        }
        if(library) {
            selectionPanel.Find("Instruction").GetComponent<Text>().text=LibraryBrowserInstruction;
            var empty=selectionPanel.Find("Empty");if(empty!=null)empty.GetComponent<Text>().text=TapPaymentText("牌库为空。","Library is empty.","ライブラリーは空です。");
        }
    }
    private string LibraryBrowserInstruction=>HasOwnLibraryReview(view?.players?.FirstOrDefault(p=>p.seat==browsedSeat))
        ?TapPaymentText("你的剩余牌库全部显示，仅你可见；按牌库顶至底排列。","Your full remaining library is visible only to you, ordered top to bottom.","あなたの残りのライブラリーを、上から下の順であなたにだけ表示します。")
        :TapPaymentText("按牌库顶至底排列；未公开的位置显示牌背。","Top to bottom; hidden positions show card backs.","上から下の順。非公開の位置はカード裏面です。");
    private bool HasOwnLibraryReview(ForgePlayerView player)=>player!=null&&player.seat==seat&&service?.IsSpectator!=true
        &&player.reviewLibrary!=null&&player.reviewLibrary.Length==Math.Max(0,player.libraryCount);
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
        if(HasOwnLibraryReview(player))
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
