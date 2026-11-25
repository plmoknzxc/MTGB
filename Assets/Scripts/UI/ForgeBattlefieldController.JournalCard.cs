using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    // Keep the native inspector's public, localized state. Only explicitly browsed
    // faces use printed characteristics; browsing never changes the match object.
    private struct JournalSnapshot
    {
        public int id, revision, liveFace;
        public string key, state, name, cost, type, rules, stats;
        public Sprite art;
        public bool original, originalAvailable, browse, dual, roomStates, leftOpen, rightOpen, faceDown;
    }
    private ForgeCardView journalCard;
    private JournalSnapshot journalLive;
    private CardDefinition journalSource, journalShown, journalPrimary, journalPartner;
    private int journalFace;
    private bool journalRoomReminder;
    private RectTransform journalScan;
    private bool scanSideways,scanFromHistory;
    private const float JournalActionHeight=38, JournalActionWidth=144, JournalFlipWidth=42;
    private const float JournalViewerHeader=64, JournalBackWidth=184;
    private static readonly Regex JournalChapter = new Regex(@"^([IVXL]+(?:\s*[,、，・]\s*[IVXL]+)*)\s*[—―－–]\s*(.+)$");
    private static readonly Regex JournalLoyalty = new Regex(@"^\[?([+\-−]?\d+)\]?\s*[:：]\s*(.+)$");

    private void ResetJournalCard()
    {
        CloseJournalScan();
        journalCard=null;
        journalSource=journalShown=journalPrimary=journalPartner=null;
        journalFace=0;
        journalRoomReminder=false;
        if(sidebarDetailContent==null)return;
        Clear(sidebarDetailContent);
        JournalText(sidebarDetailContent,"Empty",TapPaymentText("悬停卡牌查看详情","Hover over a card for details","カードにカーソルを合わせて詳細を表示"),14,12,385,18,HudMuted);
        sidebarDetailContent.sizeDelta=new Vector2(sidebarDetailView.rect.width,64);
    }

    private void CaptureSidebarDetail()
    {
        if(battleSidebar==null||sidebarHistoryInspection||GamePreferences.HoverCardDetails||detailCard==null)return;
        if(scanFromHistory){Node("InspectPanel").gameObject.SetActive(false);return;}
        var c=detailCard;
        CardDefinition definition=null;
        if(!c.faceDown)catalog.TryGetValue(c.printingKey??"",out definition);
        var live=new JournalSnapshot {
            id=c.id,key=c.printingKey,state=c.stateName,revision=GamePreferences.Revision,faceDown=c.faceDown,
            name=SceneText("InspectPanel/Name").text,type=SceneText("InspectPanel/TypeLine").text,
            rules=SceneText("InspectPanel/Rules").text,stats=SceneText("InspectPanel/Footer").text,
            cost=c.faceDown?"":Node("InspectPanel/CostRow/Mana").GetComponent<TextMeshProUGUI>()?.text,
            original=inspectOriginalState,originalAvailable=Node("InspectPanel/OriginalState").gameObject.activeSelf,
            liveFace=!c.faceDown&&(c.backFace||c.stateName=="Meld")?1:0,dual=inspectDualFace,
            browse=!c.faceDown&&!c.copy&&!c.merged&&!c.token&&!c.effectMarker&&!c.emblem&&!IsPreparedSpell(c),
            roomStates=c.zone=="Battlefield"&&c.room,leftOpen=c.leftRoomUnlocked,rightOpen=c.rightRoomUnlocked,
            art=Node("InspectPanel/Art").GetComponent<Image>().sprite
        };
        bool changed=journalCard==null||!live.Equals(journalLive);
        bool newCard=journalCard==null||live.id!=journalLive.id||live.key!=journalLive.key||
            live.liveFace!=journalLive.liveFace||live.faceDown!=journalLive.faceDown||live.browse!=journalLive.browse||live.dual!=journalLive.dual;
        journalCard=c;
        if(newCard){
            CloseJournalScan();
            journalPrimary=journalPartner=null;
            if(live.browse && MeldCardArt.TryPair(definition,catalog,out journalPrimary,out journalPartner)
                &&HasMeldComponents(c)){
                foreach(var part in c.componentCards){
                    if(!catalog.TryGetValue(part.printingKey??"",out var exact))continue;
                    if(exact.cardName==journalPrimary.cardName)journalPrimary=exact;
                    else if(exact.cardName==journalPartner.cardName)journalPartner=exact;
                }
            }
            journalSource=live.liveFace==1&&journalPrimary!=null?journalPrimary:definition;
            journalShown=journalSource;
            journalFace=live.liveFace;
            journalRoomReminder=false;
        }
        journalLive=live;
        sidebarHasDetail=true;
        if(sidebarPage!=SidebarPage.Detail)SelectSidebarTab(SidebarPage.Detail);
        if(changed)DrawJournalCard(newCard);
        Node("InspectPanel").gameObject.SetActive(false);
    }

    private static CardFeatureDefinition JournalFeature(CardDefinition card,int face)
        =>card?.features?.FirstOrDefault(f=>f!=null&&f.faceIndex==face&&f.featureIndex==0)
            ??(face==0?card?.GetPrimaryFeature():null);

    private static bool JournalSubtype(CardFeatureDefinition f,string type)
        =>(f?.subtypes??"").Split(' ').Contains(type,StringComparer.OrdinalIgnoreCase);

    private static string JournalType(CardFeatureDefinition f)
    {
        if(f==null)return "";
        var type=(f.supertypes.ToDisplayString()+" "+f.cardTypes.ToDisplayString()).Trim();
        return GamePreferences.FeatureType(f,type+(string.IsNullOrEmpty(f.subtypes)?"":" - "+f.subtypes));
    }

    private static string JournalPrintedStats(CardFeatureDefinition f)
    {
        if(f==null)return "";
        if(f.cardTypes.Has(CardType.Creature))return (string.IsNullOrEmpty(f.powerExpression)?f.attack.ToString():f.powerExpression)+"/"+
            (string.IsNullOrEmpty(f.toughnessExpression)?f.defence.ToString():f.toughnessExpression);
        if(f.cardTypes.Has(CardType.Planeswalker))return TapPaymentText("起始忠诚","Starting loyalty","初期忠誠度")+"  "+f.loyalty;
        if(f.cardTypes.Has(CardType.Battle))return TapPaymentText("初始布防值","Starting defense","初期守備値")+"  "+f.battleDefense;
        return "";
    }

    private bool OnJournalLiveFace => journalShown==journalSource&&journalFace==journalLive.liveFace;
    private void BrowseJournal(CardDefinition card,int face)
    {
        journalShown=card;journalFace=face;journalRoomReminder=false;
        DrawJournalCard(true);
    }

    private void DrawJournalCard(bool resetScroll)
    {
        if(journalCard==null||sidebarDetailView==null)return;
        var root=sidebarDetailContent;
        var scroll=sidebarDetailView.GetComponent<ScrollRect>();
        float previous=root.anchoredPosition.y;
        Clear(root);
        float w=Mathf.Max(100,sidebarDetailView.rect.width-12);
        root.sizeDelta=new Vector2(sidebarDetailView.rect.width,0);
        var f=JournalFeature(journalShown,journalFace);
        bool live=OnJournalLiveFace, printed=journalLive.browse&&f!=null;
        string title=live?journalLive.name:GamePreferences.FeatureName(f);
        if(printed && live && string.Equals(title,f.cardName,StringComparison.Ordinal))title=GamePreferences.FeatureName(f);
        string type=live?journalLive.type:JournalType(f);
        string cost=live?journalLive.cost:f?.manaCost;
        if(printed&&journalFace==1&&live&&journalCard.zone!="Stack"&&journalCard.manaCost==journalCard.originalManaCost)cost=f.manaCost;
        string rules=live?journalLive.rules:GamePreferences.FeatureRules(f);
        if(live&&journalFace==1&&printed){
            if(string.IsNullOrWhiteSpace(type)||type==journalCard.currentType)type=JournalType(f);
            if(string.IsNullOrWhiteSpace(rules)||rules==f.description)rules=GamePreferences.FeatureRules(f);
        }
        string stats=live?journalLive.stats:JournalPrintedStats(f);
        if(live && journalCard.creature && journalCard.damage==0 && stats!=null)
            stats=stats.Replace(" · "+TapPaymentText("伤害","Damage","ダメージ")+" 0","");
        if(printed && string.IsNullOrWhiteSpace(stats))stats=JournalPrintedStats(f);
        Sprite scan=live?journalLive.art:null;
        if(!live || (printed && journalLive.dual && journalFace==1))
            CardImageLibrary.TryLoadFullCard(journalShown,journalFace,out scan);
        bool dual=printed && journalPrimary==null && journalLive.dual && JournalFeature(journalShown,1)!=null;
        var doors=printed?journalShown.features.Where(x=>x!=null&&JournalSubtype(x,"Room")).ToArray():Array.Empty<CardFeatureDefinition>();
        float top;
        if(doors.Length>=2 && journalCard.zone!="Stack" && !journalLive.original && !journalCard.hasAbilityChanges){
            top=JournalRoom(root,w,scan,doors,live);
        }else{
            bool saga=printed && JournalSubtype(f,"Saga");
            const float pad=12;
            float bodyWidth=w-2*pad,artWidth=Mathf.Min(140,w*.31f);
            float portraitBottom=0;
            if(saga){
                float artHeight=artWidth*(f.cardTypes.Has(CardType.Creature)?1.80f:2.13f);
                JournalArt(root,scan,f,false,w-pad-artWidth,10,artWidth,artHeight);
                float toolX=w-pad-artWidth;
                float toolsTop=14+artHeight;
                JournalViewButton(root,scan,toolX,toolsTop,artWidth);
                portraitBottom=toolsTop+JournalActionHeight+6;
                if(dual){
                    JournalFlip(root,toolX+(artWidth-JournalFlipWidth)*.5f,portraitBottom);
                    portraitBottom+=JournalActionHeight+6;
                }
                float left=bodyWidth-artWidth-14;
                top=12+JournalText(root,"Type",type,pad,12,left,15,HudMuted)+3;
                top+=JournalHeading(root,"Name",title,pad,top,left,26)+5;
                if(!string.IsNullOrEmpty(cost))top+=JournalText(root,"Cost",cost,pad,top,left,16,HudText)+2;
                if(!string.IsNullOrEmpty(stats))top+=JournalText(root,"Stats",stats,pad,top,left,16,HudAccent)+2;
                JournalLine(root,pad,top+3,left);top+=12;
            }else{
                const float hero=145;
                JournalArt(root,scan,printed?f:null,journalFace==1&&journalPrimary!=null,w*.22f,0,w*.78f,hero,true);
                JournalViewButton(root,scan,pad,8,JournalActionWidth);
                if(live&&journalLive.originalAvailable)JournalOriginal(root,w-pad-JournalActionWidth,8);
                float nameHeight=JournalHeading(root,"Name",title,pad,hero-45,bodyWidth,27,overArtwork:scan!=null);
                if(!string.IsNullOrEmpty(cost))JournalText(root,"Cost",cost,pad,hero-70,bodyWidth,17,HudText);
                top=Mathf.Max(hero,hero-45+nameHeight+4);
                JournalLine(root,0,top,w);
                float flipW=dual?JournalFlipWidth+8:0;
                // A colour tag changes the ink, never the space reserved beside the type line.
                int statChars=string.IsNullOrEmpty(stats)?0:Regex.Replace(stats,@"<[^>]+>","").Length;
                float statW=statChars==0?0:Mathf.Min(bodyWidth*.49f,Mathf.Max(96,statChars*14+26));
                float bandY=top+7;
                float th=JournalText(root,"Type",type,pad,bandY,bodyWidth-flipW-statW,15,HudMuted);
                float sh=statW==0?0:JournalText(root,"Stats",stats,w-pad-flipW-statW,bandY,statW,16,HudAccent,right:true);
                if(dual)JournalFlip(root,w-pad-JournalFlipWidth,top+3);
                top+=Mathf.Max(dual?JournalActionHeight+6:32,Mathf.Max(th,sh)+12);
                JournalLine(root,pad,top,bodyWidth);top+=8;
            }
            if(journalPrimary!=null)top=JournalRelations(root,pad,top,bodyWidth);
            if(saga && live && journalLive.originalAvailable){
                JournalOriginal(root,pad,top);top+=JournalActionHeight+6;
            }
            top=JournalRules(root,pad,top,bodyWidth,rules,saga,portraitBottom,artWidth+14);
            top=Mathf.Max(top,portraitBottom);
            if(dual){
                int other=1-journalFace;
                var alt=JournalFeature(journalShown,other);
                top+=7;JournalLine(root,pad,top,bodyWidth);top+=9;
                top+=JournalText(root,"OtherFace",TapPaymentText("另一面","Other face","もう一方の面")+"   "+GamePreferences.FeatureName(alt)+"   "+alt.manaCost,pad,top,bodyWidth,18,HudAccent)+2;
                top+=JournalText(root,"OtherType",JournalType(alt)+"   "+JournalPrintedStats(alt),pad,top,bodyWidth,14,HudMuted)+4;
                var altRules=other==journalLive.liveFace && !string.IsNullOrWhiteSpace(journalLive.rules)?journalLive.rules:GamePreferences.FeatureRules(alt);
                top=JournalRules(root,pad,top,bodyWidth,altRules,JournalSubtype(alt,"Saga"));
            }
        }
        root.sizeDelta=new Vector2(sidebarDetailView.rect.width,top+12);
        scroll.StopMovement();
        root.anchoredPosition=new Vector2(0,resetScroll?0:Mathf.Clamp(previous,0,Mathf.Max(0,root.rect.height-sidebarDetailView.rect.height)));
        UpdateSidebarScrollbars();
    }

    private static void JournalPlace(RectTransform r,float x,float y,float w,float h)
    {
        r.anchorMin=r.anchorMax=r.pivot=Vector2.up;
        r.anchoredPosition=new Vector2(x,-y);
        r.sizeDelta=new Vector2(w,h);
    }

    private float JournalHeading(Transform root,string key,string value,float x,float y,float w,int size,bool overArtwork=false)
    {
        if(string.IsNullOrWhiteSpace(value))return 0;
        w=Mathf.Max(20,w);
        var label=Label(root,key,value,Vector2.zero,new Vector2(w,38),size,HudText);
        label.supportRichText=true;label.raycastTarget=false;MenuTypography.Use(label,MenuTypography.Face.Heading);
        label.alignment=TextAnchor.UpperLeft;
        if(overArtwork){
            var outline=label.gameObject.AddComponent<Outline>();
            outline.effectColor=new Color32(20,17,25,244);
            outline.effectDistance=Vector2.one*(1.35f*GamePreferences.TextScale);
        }
        float h=Mathf.Ceil(label.preferredHeight)+3;
        JournalPlace(label.rectTransform,x,y,w,h);
        return h;
    }

    private float JournalText(Transform root,string key,string value,float x,float y,float w,float size,Color color,bool right=false)
    {
        if(string.IsNullOrWhiteSpace(value))return 0;
        w=Mathf.Max(20,w);
        var t=CardSymbolText.Create(root,key,size,color);
        t.GetComponent<PreferenceText>().SetMenuFace(MenuTypography.Face.Reading);
        if(key is "Rules" or "Ability" or "Reminder")DeckKeywordLinkHover.BindRules(t,value,journalShown);
        else { t.richText=true;t.text=CardSymbolText.Format(value); }
        t.alignment=right?TextAlignmentOptions.TopRight:TextAlignmentOptions.TopLeft;
        t.lineSpacing=4;
        float height=Mathf.Ceil(t.GetPreferredValues(t.text,w,10000).y)+3;
        JournalPlace(t.rectTransform,x,y,w,height);
        return height;
    }

    private void JournalLine(RectTransform root,float x,float y,float w)
    {
        var line=Panel(root,"Rule",Vector2.zero,Vector2.one,Color.Lerp(HudRule,HudMuted,.32f));
        line.GetComponent<Image>().raycastTarget=false;JournalPlace(line,x,y,w,1);
    }

    private Button JournalButton(RectTransform root,string key,string text,float x,float y,float w,float h,Action click,int fontSize=18)
    {
        var b=Button(root,key,text,Vector2.zero,new Vector2(w,h),click,HudRaised);
        JournalPlace((RectTransform)b.transform,x,y,w,h);
        var label=b.GetComponentInChildren<Text>();
        MenuTypography.Use(label,MenuTypography.Face.Control);
        PreferenceText.SetSize(label,fontSize);
        label.color=HudText;
        label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
        label.rectTransform.offsetMin=new Vector2(6,1);label.rectTransform.offsetMax=new Vector2(-6,-1);
        StyleHudSurface((RectTransform)b.transform,HudRaised,HudEdge);
        b.image.sprite=IntegratedUiUtility.SlicedRoundedRect(6);
        return b;
    }

    private void JournalFlip(RectTransform root,float x,float y)
    {
        var b=JournalButton(root,"FlipFace","",x,y,JournalFlipWidth,JournalActionHeight,()=>BrowseJournal(journalShown,1-journalFace));
        var icon=Rect(b.transform,"FlipIcon",Vector2.zero,new Vector2(26,26)).gameObject.AddComponent<Image>();
        icon.sprite=Resources.Load<Sprite>("Menu/FlipCard");icon.preserveAspect=true;icon.color=HudText;icon.raycastTarget=false;
        icon.rectTransform.localScale=new Vector3(journalFace==0?1:-1,1,1);
    }

    private void JournalOriginal(RectTransform root,float x,float y)
    {
        var original=Node("InspectPanel/OriginalState").GetComponent<Button>();
        JournalButton(root,"Original",original.GetComponentInChildren<Text>().text,x,y,JournalActionWidth,JournalActionHeight,()=>original.onClick.Invoke());
    }

    private void JournalViewButton(RectTransform root,Sprite scan,float x,float y,float w)
    {
        var feature=JournalFeature(journalShown,journalFace);
        bool sideways=journalLive.browse&&(JournalSubtype(feature,"Room")||feature?.cardTypes.Has(CardType.Battle)==true);
        var b=JournalButton(root,"ViewCard",TapPaymentText("查看卡面","View card","カード画像"),x,y,w,JournalActionHeight,()=>ShowJournalScan(scan,sideways));
        b.interactable=scan!=null;
    }

    private void JournalArt(RectTransform root,Sprite scan,CardFeatureDefinition f,bool meld,float x,float y,float w,float h,bool fade=false)
    {
        if(scan==null)return;
        var r=Rect(root,"Illustration",Vector2.zero,Vector2.zero);
        JournalPlace(r,x,y,w,h);
        var raw=r.gameObject.AddComponent<JournalIllustration>();raw.FadeLeft=fade;raw.raycastTarget=false;
        // Use the printed illustration window, including narrow Saga and sideways Siege frames.
        bool saga=JournalSubtype(f,"Saga");
        Rect uv=saga?(f.cardTypes.Has(CardType.Creature)?new Rect(.51f,.26f,.409f,.525f):new Rect(.51f,.17f,.41f,.71f)):
            meld?new Rect(.036f,.493f,.927f,.411f):
            f!=null&&f.cardTypes.Has(CardType.Planeswalker)?new Rect(.08f,.53f,.84f,.35f):new Rect(.08f,.446f,.84f,.44f);
        if(f!=null&&f.cardTypes.Has(CardType.Battle)){
            r.pivot=Vector2.one*.5f;r.anchoredPosition=new Vector2(x+w/2,-y-h/2);
            r.sizeDelta=new Vector2(h,w);r.localRotation=Quaternion.Euler(0,0,-90);
            uv=new Rect(.153f,.103f,.33f,.834f);raw.FadeLeft=false;
        }
        CardArtCrop.Fill(raw,scan,uv,.85f);
        // Portraits and thumbnails keep both edges. JournalIllustration normally fades one.
        if(!fade||f!=null&&f.cardTypes.Has(CardType.Battle))raw.Solid=true;
    }

    private float JournalRules(RectTransform root,float x,float y,float width,string text,bool saga,float portraitBottom=0,float reserved=0)
    {
        bool previousChapter=false;
        // Changed text can contain rich-text spans across several lines. Keep those spans intact.
        if(!saga && (text??"").Contains("<color="))return y+JournalText(root,"Rules",text,x,y,width,18,HudText);
        foreach(string raw in (text??"").Replace("\\n","\n").Split('\n')){
            var line=raw.Trim();
            if(line.Length==0){y+=4;continue;}
            float w=y<portraitBottom?width-reserved:width;
            var chapter=saga?JournalChapter.Match(line):Match.Empty;
            var loyalty=JournalLoyalty.Match(line);
            if(chapter.Success||loyalty.Success){
                if(previousChapter){JournalLine(root,x,y+2,w);y+=9;}
                var m=chapter.Success?chapter:loyalty;
                float gutter=Mathf.Min(w*.31f,Mathf.Max(34,m.Groups[1].Value.Length*8+7));
                float marker=JournalText(root,chapter.Success?"Chapter":"Loyalty",m.Groups[1].Value,x,y,gutter-3,15,HudAccent);
                float body=JournalText(root,"Ability",m.Groups[2].Value,x+gutter,y,w-gutter,18,HudText);
                y+=Mathf.Max(marker,body)+5;previousChapter=true;
            }else{
                if(previousChapter){JournalLine(root,x,y+2,w);y+=9;}
                bool reminder=line.StartsWith("(")||line.StartsWith("（");
                y+=JournalText(root,"Rules",line,x,y,w,reminder?15:18,reminder?HudMuted:HudText)+5;previousChapter=false;
            }
        }
        return y;
    }

    private float JournalRelations(RectTransform root,float x,float y,float w)
    {
        float part=(w-8)*.5f;
        bool result=journalFace==1;
        var a=result?journalPrimary:journalShown==journalPrimary?journalPartner:journalPrimary;
        JournalRelation(root,a,0,result?TapPaymentText("融合来源","Meld component","合体元"):TapPaymentText("融合搭档","Meld partner","合体相手"),x,y,part);
        JournalRelation(root,result?journalPartner:journalPrimary,result?0:1,result?TapPaymentText("融合来源","Meld component","合体元"):TapPaymentText("融合结果","Meld result","合体先"),x+part+8,y,part);
        return y+64;
    }

    private void JournalRelation(RectTransform root,CardDefinition card,int face,string relation,float x,float y,float w)
    {
        var f=JournalFeature(card,face);
        if(f==null)return;
        var b=JournalButton(root,"MeldLink_"+card.PrintingKey.Replace('/','_')+"_"+face,"",x,y,w,56,()=>BrowseJournal(card,face));
        var r=(RectTransform)b.transform;
        CardImageLibrary.TryLoadFullCard(card,face,out var art);
        JournalArt(r,art,f,face==1,5,6,41,44);
        JournalText(r,"Relation",relation,52,3,w-58,13,HudMuted);
        var n=Label(r,"CardName",GamePreferences.FeatureName(f),Vector2.zero,Vector2.one,18,HudText);
        n.alignment=TextAnchor.MiddleLeft;n.raycastTarget=false;n.resizeTextForBestFit=true;n.resizeTextMinSize=13;n.resizeTextMaxSize=n.fontSize;
        JournalPlace(n.rectTransform,52,24,w-58,29);
    }

    private float JournalRoom(RectTransform root,float w,Sprite scan,CardFeatureDefinition[] doors,bool live)
    {
        const float pad=12;
        float width=w-2*pad;
        bool original=live&&journalLive.originalAvailable;
        float typeWidth=width-(JournalActionWidth+8)*(original?2:1);
        float typeHeight=JournalText(root,"Type",JournalType(doors[0]),pad,12,typeWidth,16,HudMuted);
        JournalViewButton(root,scan,w-pad-JournalActionWidth,7,JournalActionWidth);
        if(original)JournalOriginal(root,w-pad-2*JournalActionWidth-8,7);
        float artTop=Mathf.Max(7+JournalActionHeight,12+typeHeight)+7;
        float h=width/2.98f;
        if(scan!=null){
            var r=Rect(root,"RoomPanorama",Vector2.zero,new Vector2(h,width));
            r.anchorMin=r.anchorMax=Vector2.up;r.pivot=Vector2.one*.5f;r.anchoredPosition=new Vector2(pad+width/2,-artTop-h/2);
            r.localRotation=Quaternion.Euler(0,0,-90);
            var a=r.gameObject.AddComponent<RawImage>();a.raycastTarget=false;
            CardArtCrop.Fill(a,scan,new Rect(.138f,.105f,.393f,.846f));
        }
        bool pair=width>=350;
        float col=pair?(width-22)*.5f:width,y=artTop+8+h,bottom=y;
        string reminder="";
        for(int i=0;i<2;i++){
            var f=doors[i];float x=pad+(pair?i*(col+22):0),at=pair?y:bottom;
            if(i==1 && !pair){JournalLine(root,x,at,col);at+=8;}
            at+=JournalHeading(root,"DoorName",GamePreferences.FeatureName(f),x,at,col,21);
            at+=JournalText(root,"DoorCost",f.manaCost,x,at,col,16,HudMuted);
            if(live&&journalLive.roomStates){
                bool unlocked=i==0?journalLive.leftOpen:journalLive.rightOpen;
                at+=JournalText(root,"DoorState",unlocked?TapPaymentText("已解锁","Unlocked","解放済み"):TapPaymentText("未解锁","Locked","施錠"),x,at,col,14,unlocked?HudAccent:HudMuted);
            }
            var lines=GamePreferences.FeatureRules(f).Replace("\\n","\n").Split('\n').ToList();
            if(lines.Count>1 && (lines.Last().TrimStart().StartsWith("(")||lines.Last().TrimStart().StartsWith("（"))){
                reminder=lines.Last();lines.RemoveAt(lines.Count-1);
            }
            at=JournalRules(root,x,at+4,col,string.Join("\n",lines),false)+5;
            bottom=Mathf.Max(bottom,at);
        }
        if(pair){
            var line=Panel(root,"DoorDivider",Vector2.zero,Vector2.zero,HudRule);line.GetComponent<Image>().raycastTarget=false;
            JournalPlace(line,pad+col+10,y,1,bottom-y);
        }
        if(reminder.Length>0){
            JournalButton(root,"RoomRules",TapPaymentText("隔间规则","Room rules","部屋のルール"),pad,bottom,JournalActionWidth,JournalActionHeight,()=>{
                journalRoomReminder=!journalRoomReminder;DrawJournalCard(false);});
            bottom+=JournalActionHeight+6;
            if(journalRoomReminder)bottom+=JournalText(root,"Reminder",reminder,pad,bottom,width,15,HudMuted);
        }
        return bottom;
    }

    private void ShowJournalScan(Sprite scan,bool sideways,bool history=false)
    {
        if(scan==null)return;
        CloseJournalScan();
        scanSideways=sideways&&scan.rect.height>scan.rect.width;
        scanFromHistory=history;
        Color paper=HudInk;
        paper.a=1;
        journalScan=Panel(battleSidebar,"JournalCardViewer",Vector2.zero,Vector2.zero,paper);
        journalScan.GetComponent<Image>().raycastTarget=true;
        StyleHudSurface(journalScan,paper,HudEdge);
        journalScan.GetComponent<Image>().sprite=IntegratedUiUtility.SlicedRoundedRect(6);
        SetOverlayLayer(journalScan,JournalScanLayer,true);
        var art=Rect(journalScan,"FullCard",Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>();
        art.sprite=scan;art.preserveAspect=true;art.raycastTarget=false;
        string back=history?TapPaymentText("返回记录","Back to history","履歴に戻る"):TapPaymentText("返回详情","Back to details","詳細に戻る");
        JournalButton(journalScan,"Close",back,12,9,JournalBackWidth,44,history?(Action)CloseHistoryInspector:CloseJournalScan,20);
        if(history&&historyInspectCard!=null)
            JournalText(journalScan,"CardName",GamePreferences.CardFaceName(historyInspectCard.name),0,0,190,16,HudText,right:true);
        LayoutJournalScan();
    }

    private void LayoutJournalScan()
    {
        if(journalScan==null)return;
        float gap=12,available=surface.rect.width-SidebarOpenRight-gap;
        bool beside=scanFromHistory && available>=350;
        SetJournalPagesVisible(beside);
        journalScan.anchorMin=beside?Vector2.right:Vector2.zero;
        journalScan.anchorMax=Vector2.one;
        float width=Mathf.Min(368,available-gap);
        // Short histories shrink their list. A full scan still gets the same reading
        // height as the detail page so a one-event log can show a legible card.
        float extraHeight=scanFromHistory?Mathf.Max(0,surface.rect.height-JournalTopInset-JournalBottomInset-battleSidebar.rect.height):0;
        journalScan.offsetMin=new Vector2(beside?gap:0,-extraHeight);
        journalScan.offsetMax=new Vector2(beside?gap+width:0,0);
        var art=(RectTransform)journalScan.Find("FullCard");
        if(journalScan.Find("CardName") is RectTransform title){
            float titleX=JournalBackWidth+22;
            float rightInset=beside?12:JournalToggleInset+JournalToggleSize+10;
            JournalPlace(title,titleX,5,journalScan.rect.width-titleX-rightInset,JournalViewerHeader-10);
            var text=title.GetComponent<TMP_Text>();
            text.alignment=TextAlignmentOptions.MidlineRight;text.overflowMode=TextOverflowModes.Ellipsis;
        }
        const float bottomMargin=10;
        Vector2 dims=journalScan.rect.size-new Vector2(20,JournalViewerHeader+bottomMargin);
        art.anchorMin=art.anchorMax=art.pivot=Vector2.one*.5f;
        art.anchoredPosition=new Vector2(0,(bottomMargin-JournalViewerHeader)*.5f);
        art.sizeDelta=scanSideways?new Vector2(dims.y,dims.x):dims;
        art.localRotation=Quaternion.Euler(0,0,scanSideways?-90:0);
    }

    private void CloseJournalScan()
    {
        if(journalScan==null)return;
        journalScan.gameObject.SetActive(false);Destroy(journalScan.gameObject);journalScan=null;scanFromHistory=false;
        SetJournalPagesVisible(true);
    }

    private void SetJournalPagesVisible(bool visible)
    {
        // Live match updates can still measure the pages while the scan occupies their slot.
        var pages=journalPages.GetComponent<CanvasGroup>();
        pages.alpha=visible?1:0;
        pages.interactable=pages.blocksRaycasts=visible;
    }
}
