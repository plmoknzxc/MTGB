using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private Color StackSeatTint(int player)
    {
        if(player<=0)return HudMuted;
        if(IsFourPlayerScene)return FourSeatColor(player);
        if(service?.IsSpectator==true)return HudMuted;
        return player==seat?RedesignOwn:RedesignOpponent;
    }

    private static readonly char[] StackSubtypeSeparators = { '-', '—', '–' };
    private static readonly char[] StackTypeSeparators = { ' ', ',' };

    private string StackSpellCategory(ForgeCardView card)
    {
        string line = string.IsNullOrWhiteSpace(card?.currentType) ? card?.originalType : card.currentType;
        if (string.IsNullOrWhiteSpace(line) && card != null) {
            string printing = card.copy || card.merged ? card.currentArtPrintingKey : card.printingKey;
            if (catalog.TryGetValue(printing ?? "", out var definition)) {
                var face = definition.features?.FirstOrDefault(f => f?.cardName == card.name);
                if (face != null) line = face.cardTypes.ToDisplayString();
                else if (definition.cardName == card.name) line = definition.cardTypes.ToDisplayString();
            }
        }

        // The stack face determines the category, including for Adventures and back faces.
        // Supertypes and everything after the subtype separator are not spell categories.
        line ??= "";
        int end = line.IndexOfAny(StackSubtypeSeparators);
        if (end >= 0) line = line.Substring(0, end);
        string category = "";
        foreach (string word in line.Split(StackTypeSeparators, StringSplitOptions.RemoveEmptyEntries)) {
            if (!Enum.TryParse(word == "Tribal" ? "Kindred" : word, true, out CardType type)
                || type is CardType.None or CardType.Land) continue;
            string term = CardTypeText.Term(type.ToString());
            if (category.Length > 0) category += TapPaymentText("", " ", "・");
            category += term;
        }
        return category.Length > 0 ? category : TapPaymentText("咒语", "spell", "呪文");
    }

    private string StackActionLabel(ForgeStackItem entry)
    {
        switch (entry?.kind) {
            case "ability":
            case "activated":
                return entry.copied ? TapPaymentText("复制启动式异能", "Copy activated ability", "起動型能力をコピー")
                    : TapPaymentText("启动异能", "Activate ability", "能力を起動");
            case "trigger":
                return entry.copied ? TapPaymentText("复制触发式异能", "Copy triggered ability", "誘発型能力をコピー")
                    : TapPaymentText("触发异能", "Trigger ability", "能力が誘発");
            case "spell":
                if (entry.source?.faceDown == true)
                    return entry.copied ? TapPaymentText("复制面朝下的咒语", "Copy face-down spell", "裏向きの呪文をコピー")
                        : TapPaymentText("面朝下施放", "Cast face down", "裏向きで唱える");
                string category = StackSpellCategory(entry.source);
                return entry.copied ? TapPaymentText("复制" + category, "Copy " + category.ToLowerInvariant(), category + "をコピー")
                    : TapPaymentText("施放" + category, "Cast " + category.ToLowerInvariant(), category + "を唱える");
            default:
                return "";
        }
    }

    private float LayoutThemedStackEntry(StackCardVisual item,string title,string fallback,int position,
        (ForgeStackItem entry,int number)[] incoming,Color owner,float width,float artWidth,bool ability,string key)
    {
        var row=item.row;
        var entry=item.entry;
        var top=row.Find("ObjectHeader") as RectTransform;
        if(top==null){
            top=Rect(row,"ObjectHeader",Vector2.zero,Vector2.one);
            top.SetAsFirstSibling();
        }
        bool pending=entry?.id==PendingCastId;
        float left=artWidth+(ability?26:30),available=width-left-16;
        string rank=pending?"":position.ToString("00");
        var order=RedesignStackText(top,"Position",rank,21,owner,left,11,available,MenuTypography.Face.Heading);
        float rankWidth=pending?0:Mathf.Ceil(order.preferredWidth)+14;
        string action=pending?TapPaymentText("正在宣告","Being announced","宣言中"):StackActionLabel(entry);
        string player=entry?.controllerSeat>0?SafeDetail(IsFourPlayerScene?PlayerDisplayName(entry.controllerSeat):PlayerIdentityName(entry.controllerSeat)):"";
        if(IsFourPlayerScene && player.Length>0)
            player=ColoredDetail(player,"#"+ColorUtility.ToHtmlStringRGB(FourSeatColor(entry.controllerSeat)));
        string origin=player.Length>0&&action.Length>0?player+"  ·  "+action:player+action;
        var who=RedesignStackText(top,"CompactOwner",origin,18,IsFourPlayerScene?Color.white:Color.Lerp(owner,HudText,.42f),left+rankWidth,12,available-rankWidth,MenuTypography.Face.Control);
        who.GetComponent<PreferenceText>().Literal=true;
        float nameTop=16+Mathf.Max(order.rectTransform.rect.height,who.rectTransform.rect.height);
        var name=RedesignStackText(top,"CompactTitle",title,29,HudText,left,nameTop,available,MenuTypography.Face.Heading);
        float titleBottom=nameTop+name.rectTransform.rect.height;
        Text sourceCount=null;
        if(incoming.Length>1){
            sourceCount=RedesignStackText(top,"TargetSourceCount",StackTargetSourceCount(incoming.Length),16,HudText,left,titleBottom+2,available,MenuTypography.Face.Control);
            sourceCount.gameObject.SetActive(true);
            titleBottom+=sourceCount.rectTransform.rect.height+2;
        }else if(top.Find("TargetSourceCount") is Transform previousCount)previousCount.gameObject.SetActive(false);
        var thumb=item.tile.Find("AbilityIllustration")?.GetComponent<RawImage>();
        if (ability && thumb==null) {
            thumb=Rect(item.tile,"AbilityIllustration",Vector2.zero,Vector2.one).gameObject.AddComponent<StackIllustration>();
            thumb.raycastTarget=true;
            thumb.rectTransform.anchorMin=Vector2.zero;
            thumb.rectTransform.anchorMax=Vector2.one;
            thumb.rectTransform.offsetMin=thumb.rectTransform.offsetMax=Vector2.zero;
        }
        float artHeight=ability?Mathf.Round(artWidth*1.20f):artWidth*335/240;
        if(thumb!=null)thumb.gameObject.SetActive(ability);
        if(ability){
            item.tile.sizeDelta=new Vector2(240,240*artHeight/artWidth);
            float centerOffset=Mathf.Max(0,10+artHeight-titleBottom)*.5f;
            var offset=Vector2.down*centerOffset;
            order.rectTransform.anchoredPosition+=offset;
            who.rectTransform.anchoredPosition+=offset;
            name.rectTransform.anchoredPosition+=offset;
            if(sourceCount!=null)sourceCount.rectTransform.anchoredPosition+=offset;
            Sprite illustration=null;
            if(item.source!=null && !item.source.faceDown)TryBattlefieldArt(item.source,out illustration);
            // The type bar sits higher on printed planeswalker frames.
            string printedType=item.source?.originalType;
            bool walkerFrame=string.IsNullOrWhiteSpace(printedType)
                ? item.source?.planeswalker==true : printedType.Contains("Planeswalker");
            var artRegion=walkerFrame ? new Rect(.07f,.525f,.86f,.355f) : new Rect(.07f,.45f,.86f,.43f);
            CardArtCrop.Fill(thumb,illustration,artRegion,1f);
            thumb.color=illustration!=null?Color.white:HudInk;
        }

        float textTop=ability?Mathf.Max(titleBottom,10+artHeight)+7:titleBottom+7;
        float bodyLeft=ability?12:left;
        float bodyWidth=ability?width-bodyLeft-12:available;
        var details=PrepareRedesignStackDetails(row);
        details.gameObject.SetActive(true);
        var info=details.content;
        float wrap=bodyWidth-12;
        float y=LayoutRedesignStackEffect(info,entry,fallback,0,wrap,ability);
        if(y>0)y+=5;
        int links=0;
        foreach(var target in entry?.targets??Array.Empty<ForgeStackTarget>()){
            if(target==null)continue;
            string targetName=RedesignTargetName(target,out int number);
            y+=LayoutStackLink(info,links++,y,wrap,false,targetName,number,StackSeatTint(RedesignTargetSeat(target)));
        }
        foreach(var source in incoming){
            var card=source.entry.source;
            string sourceName=card?.faceDown==true?RedesignStackEffect(source.entry,""):
                GamePreferences.CardFaceName(card?.name??source.entry.description??"");
            int times=source.entry.targets.Count(t=>t!=null&&t.kind=="stack"&&!t.leftZone&&t.stackId==entry.id);
            if(times>1)sourceName+=" ×"+times;
            y+=LayoutStackLink(info,links++,y,wrap,true,SafeDetail(sourceName),source.number,StackSeatTint(source.entry.controllerSeat));
        }
        for(int i=links;info.Find("Link"+i) is Transform extra;i++)extra.gameObject.SetActive(false);
        bool hasBody=y>0;
        details.gameObject.SetActive(hasBody);
        float visible=Mathf.Min(y,Mathf.Max(72,RedesignStackEntryHeight+24-textTop));
        float height=Mathf.Max(artHeight+20,textTop+visible+9);
        RedesignPlace(top,Vector2.up,Vector2.up,Vector2.zero,new Vector2(width,height));
        var vignette=top.Find("Illustration")?.GetComponent<JournalIllustration>();
        var publicArt=ability || item.source?.faceDown!=false ? null : item.image.sprite;
        if(publicArt!=null && vignette==null){
            vignette=Rect(top,"Illustration",Vector2.zero,Vector2.one).gameObject.AddComponent<JournalIllustration>();
            vignette.raycastTarget=false;vignette.FeatherPortrait=true;
            vignette.transform.SetAsFirstSibling();
        }
        if(vignette!=null){
            vignette.gameObject.SetActive(publicArt!=null);
            if(publicArt!=null){
                RedesignPlace(vignette.rectTransform,Vector2.one,Vector2.one,new Vector2(-6,-6),new Vector2(width*.50f,height-12));
                CardArtCrop.Illustration(vignette,publicArt);
                vignette.color=new Color(1,1,1,position==1?.18f:.10f);
            }
        }
        if(hasBody)LayoutRedesignStackDetails(details,key,bodyLeft,textTop,bodyWidth,y,visible);
        return height;
    }

    private float LayoutStackLink(RectTransform info,int index,float top,float width,bool incoming,string name,int position,Color identity)
    {
        var link=info.Find("Link"+index) as RectTransform;
        if(link==null){
            link=Panel(info,"Link"+index,Vector2.zero,Vector2.one,Color.clear);
            var back=link.GetComponent<Image>();
            back.sprite=IntegratedUiUtility.SlicedRoundedRect(6);
            back.type=Image.Type.Sliced;
            back.raycastTarget=false;
        }
        link.gameObject.SetActive(true);
        Color wash=identity;wash.a=.075f;
        link.GetComponent<Image>().color=wash;
        string caption=incoming?TapPaymentText("来自","From","発生源"):TapPaymentText("指向","Targets","対象");
        var label=RedesignStackText(link,"Direction",caption,16,HudMuted,26,0,width*.30f,MenuTypography.Face.Control);
        float labelWidth=Mathf.Min(width*.30f,Mathf.Ceil(label.preferredWidth)+2);
        var number=RedesignStackText(link,"Reference",position>0?position.ToString("00"):"",17,identity,0,0,width,MenuTypography.Face.Heading);
        float badgeWidth=position>0?Mathf.Ceil(number.preferredWidth)+12:0;
        float nameX=26+labelWidth+12,nameWidth=width-nameX-badgeWidth-10;
        var target=RedesignStackText(link,"Object",name,19,HudText,nameX,0,nameWidth,MenuTypography.Face.Control);
        target.supportRichText=true;target.GetComponent<PreferenceText>().Literal=true;
        float height=Mathf.Max(34,Mathf.Max(target.rectTransform.rect.height,label.rectTransform.rect.height)+8);
        RedesignPlace(link,Vector2.up,Vector2.up,new Vector2(0,-top),new Vector2(width,height));
        void Center(Text text,float x,float w){
            text.alignment=TextAnchor.MiddleLeft;
            RedesignPlace(text.rectTransform,Vector2.up,Vector2.up,new Vector2(x,0),new Vector2(w,height));
        }
        Center(label,26,labelWidth);Center(target,nameX,nameWidth);Center(number,width-badgeWidth,badgeWidth);

        var arrow=link.Find("Arrow")?.GetComponent<StackControlIcon>();
        if(arrow==null){
            arrow=Rect(link,"Arrow",Vector2.zero,Vector2.one).gameObject.AddComponent<StackControlIcon>();
            arrow.SmoothEdges=true;arrow.raycastTarget=false;
        }
        RedesignPlace(arrow.rectTransform,new Vector2(0,.5f),new Vector2(.5f,.5f),new Vector2(13,0),new Vector2(15,15));
        arrow.SetKind(incoming?StackControlIcon.Kind.Left:StackControlIcon.Kind.Right);
        arrow.color=identity;
        return height+3;
    }
}
