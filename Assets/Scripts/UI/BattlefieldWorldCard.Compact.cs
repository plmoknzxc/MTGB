using System;
using TMPro;
using UnityEngine;

// Optional scene-local visual preview. Native object identity and input anchors
// remain unchanged; hands and unknown/special print layouts retain full scans.
public sealed partial class BattlefieldWorldCard
{
    bool compactFace,compactLowerTitle,compactResourceLand;
    bool missingHandArt;
    float landCaptionCountWidth,compactHeaderHeight;
    MeshRenderer compactBody, compactHeader, compactFoot, compactNameRule;
    TextMeshPro compactKind;

    void BuildCompactFace()
    {
        compactBody=Surface("Compact inset",new Vector3(0,0,-.002f),Vector2.one,new Color(.055f,.065f,.075f));
        compactHeader=Surface("Name vignette",new Vector3(0,0,-.012f),Vector2.one,new Color(.018f,.027f,.031f,.95f));
        compactHeader.GetPropertyBlock(properties);properties.SetFloat("_TopFade",1);compactHeader.SetPropertyBlock(properties);
        compactNameRule=Surface("Name edge inlay",new Vector3(0,0,-.014f),Vector2.one,Color.white);
        compactFoot=Surface("Compact footer",new Vector3(0,0,-.012f),Vector2.one,new Color(.045f,.06f,.07f,.96f));
        compactKind=Label("Permanent type",Vector3.zero,new Vector2(.48f,.13f),.86f,TextAlignmentOptions.Left);
    }

    void BindCompactFace(ForgeCardView value,Sprite art,bool inHand,Color stateAccent,bool activeOutline)
    {
        string type=value.currentType??value.originalType??"";
        var artLayout=table.artLayouts!=null?table.artLayouts.Find(art):null;
        // The preview uses standard illustration bounds. Keep full images for
        // non-standard layouts rather than accidentally cropping their rules.
        compactFace=table.compactBattlefieldCards&&!inHand&&!value.faceDown
            &&artLayout?.keepFullScan!=true;
        compactKind.text="";
        compactLowerTitle=false;
        compactResourceLand=false;
        missingHandArt=inHand && art==null && !value.faceDown;
        if(missingHandArt){
            FramePalette(FrameColorBits(value),out var left,out var right,table.darkMetalCardFrames);
            compactBody.transform.localPosition=new Vector3(0,0,-.012f);
            compactBody.transform.localScale=new Vector3(1,aspect,1);
            colors[surfaces.IndexOf(compactBody)]=Color.white;
            ApplyPrintedFrame(compactBody,0,aspect,left,right);
            title.enableAutoSizing=true;
            title.fontSizeMin=.82f;title.fontSizeMax=2.1f;
            title.textWrappingMode=TextWrappingModes.Normal;
        }
        if(!compactFace)return;
        // Ordinary permanents share the creature footprint. Lands still use
        // their mana-color frame and type/count footer; Rooms keep two doors.
        bool resourceLand=value.land&&!value.creature&&!value.battle&&!value.planeswalker;
        compactResourceLand=resourceLand;
        compactHeaderHeight=value.room?.14f:value.battle?.16f:.18f;
        // The name compartment is part of the silhouette's height budget.
        aspect=value.room?.64f:value.battle?.62f:value.planeswalker?1.08f:1.08f-compactHeaderHeight;
        float top=aspect*.5f, bottom=-top;
        float battleCorner=value.battle?.08f:0;
        FramePalette(FrameColorBits(value),out var frameLeft,out var frameRight,table.darkMetalCardFrames);
        colors[surfaces.IndexOf(rim)]=value.battle
            ?Color.Lerp(new Color(.055f,.070f,.077f), (frameLeft+frameRight)*.5f, .55f)
            :new Color(.025f,.032f,.039f);
        colors[surfaces.IndexOf(compactBody)]=Color.white;
        // Keep existing actionable/selected/attack outlines distinguishable.
        if(activeOutline)colors[surfaces.IndexOf(rim)]=stateAccent;
        colors[surfaces.IndexOf(compactNameRule)]=Color.white;
        compactNameRule.GetPropertyBlock(properties);
        properties.SetFloat("_FrameTint",2);
        properties.SetColor("_FrameColorA",frameLeft);properties.SetColor("_FrameColorB",frameRight);
        compactNameRule.SetPropertyBlock(properties);
        float outline=activeOutline?.095f:.045f;
        rim.transform.localPosition=new Vector3(0,compactHeaderHeight*.5f,0);
        rim.transform.localScale=new Vector3(1+outline,aspect+outline+compactHeaderHeight,1);
        rim.GetPropertyBlock(properties);properties.SetFloat("_Bevel",0);properties.SetFloat("_CutCorner",battleCorner);properties.SetFloat("_Radius",.025f);rim.SetPropertyBlock(properties);
        // Thin printed stock rather than a raised ornamental frame. The open
        // center still reveals the original illustration beneath this mesh.
        compactBody.transform.localPosition=new Vector3(0,compactHeaderHeight*.5f,-.018f);
        compactBody.transform.localScale=new Vector3(1.04f,aspect+.04f+compactHeaderHeight,1);
        ApplyPrintedFrame(compactBody,compactHeaderHeight,(aspect+.10f)/1.1f,frameLeft,frameRight);
        surfaces[1].transform.localScale=new Vector3(1.028f,aspect+.028f,.018f);
        shadow.transform.localScale=new Vector3(1.09f,aspect+.09f+compactHeaderHeight,1);
        shadow.transform.localPosition=new Vector3(.012f,compactHeaderHeight*.5f-.018f,.022f);
        shadow.GetPropertyBlock(properties);properties.SetFloat("_Contact",2);shadow.SetPropertyBlock(properties);
        colliderShape.center=new Vector3(0,compactHeaderHeight*.5f,0);
        colliderShape.size=new Vector3(1+outline,aspect+outline+compactHeaderHeight,.045f);
        for(int i=0;i<groupLayers.Length;i++){
            groupLayers[i].transform.localScale=new Vector3(1.04f,aspect+.04f+compactHeaderHeight,1);
            groupLayers[i].transform.localPosition=new Vector3(.035f*(i+1),.035f*(i+1)+compactHeaderHeight*.5f,.055f+.03f*i);
        }
        float footer=value.creature||value.battle?.25f:.31f;
        float artHeight=aspect-.022f;
        compactNameRule.transform.localPosition=new Vector3(-.32f,bottom+.026f,-.022f);
        compactNameRule.transform.localScale=new Vector3(.20f,.012f,1);
        compactFoot.transform.localPosition=new Vector3(0,bottom+footer*.5f+.008f,-.012f);
        compactFoot.transform.localScale=new Vector3(.984f,footer,1);
        colors[surfaces.IndexOf(compactFoot)]=value.battle
            ?new Color(.065f,.082f,.090f,.98f)
            :new Color(.006f,.012f,.019f,.99f);
        compactFoot.GetPropertyBlock(properties);
        properties.SetFloat("_BottomFade",1);properties.SetFloat("_CutCorner",0);properties.SetFloat("_FooterTrim",1);
        properties.SetColor("_FrameColorA",frameLeft);properties.SetColor("_FrameColorB",frameRight);
        compactFoot.SetPropertyBlock(properties);
        face.transform.localRotation=Quaternion.identity;
        face.transform.localPosition=new Vector3(0,0,-.006f);
        face.transform.localScale=new Vector3(.988f,artHeight,1);
        bool rotatedBattle=false,sideIllustration=false;
        var crop=art!=null?BattlefieldCardArtLayouts.IllustrationPixels(value,art,type,artLayout,.988f/artHeight,out rotatedBattle,out sideIllustration):default;
        if(rotatedBattle){
            face.transform.localRotation=Quaternion.Euler(0,0,-90);
            face.transform.localScale=new Vector3(artHeight,.988f,1);
        }
        face.GetPropertyBlock(properties);
        properties.SetFloat("_CutCorner",value.battle?0:.035f);
        // Local scans are only 488 px wide. A restrained screen-space unsharp
        // pass restores edge contrast after the art crop is minified.
        properties.SetFloat("_ImageSharpness",sideIllustration||value.battle?0:.24f);
        if(art!=null)properties.SetVector("_UVRect",new Vector4(crop.x/art.texture.width,crop.y/art.texture.height,crop.width/art.texture.width,crop.height/art.texture.height));
        face.SetPropertyBlock(properties);
        title.font=table.CardNameFont;
        title.text=GamePreferences.CardFaceName(value.name??"");
        title.transform.localPosition=new Vector3(0,top+compactHeaderHeight*.5f,-.029f);
        title.rectTransform.sizeDelta=new Vector2(.86f,table.darkMetalCardFrames?.17f:.13f);
        title.fontSize=table.darkMetalCardFrames?1.02f:1.08f;title.enableAutoSizing=true;
        title.fontSizeMin=.76f;title.fontSizeMax=table.darkMetalCardFrames?1.02f:1.08f;
        // Retain the sharp name-only SDF, but fit its visible glyphs inside the
        // header after mesh generation instead of letting them cross the trim.
        title.fontStyle=table.darkMetalCardFrames||GamePreferences.Language.StartsWith("zh")?FontStyles.Normal:FontStyles.Bold;
        title.alignment=TextAlignmentOptions.Left;title.overflowMode=TextOverflowModes.Ellipsis;
        title.color=table.darkMetalCardFrames?new Color(.95f,.93f,.86f):new Color(.024f,.030f,.035f);
        stats.font=table.numberFont!=null?table.numberFont:table.font;
        stats.fontStyle=FontStyles.Bold;
        if(value.creature)stats.text=ForgeBattlefieldController.WorldStatsText(value,table.darkMetalCardFrames);
        else stats.text=value.planeswalker?value.loyalty.ToString():value.battle?value.defense.ToString():"";
        bool insetStats=value.creature&&table.darkMetalCardFrames;
        float statsMaxFont=insetStats?1.50f:value.creature?1.38f:1.85f;
        stats.enableAutoSizing=false;stats.fontSize=statsMaxFont;
        stats.margin=Vector4.zero;
        // Measure the actual glyph run: proportional digits, signs and large
        // loyalty/defense values cannot share a fixed character-count estimate.
        int statBadge=value.creature?3:!value.room?(value.planeswalker?1:value.battle?2:0):0;
        float statPadding=insetStats?.11f:value.creature?.095f:statBadge>0?.08f:.045f;
        float statsWidth=Mathf.Clamp(stats.GetPreferredValues(stats.text).x+statPadding,insetStats?.33f:value.creature?.31f:statBadge>0?.28f:.25f,insetStats?.56f:value.creature?.48f:.66f);
        float statsX=(insetStats?.495f:value.creature?.55f:.43f)-statsWidth*.5f;
        stats.transform.localPosition=new Vector3(statsX,bottom+(insetStats?.122f:statBadge==1?.125f:statBadge==2?.139f:.102f),-.029f);
        stats.rectTransform.sizeDelta=new Vector2(statsWidth-statPadding,insetStats?.175f:statBadge==1?.17f:value.creature?.16f:.20f);
        stats.enableAutoSizing=true;stats.fontSizeMin=insetStats?.35f:.65f;stats.fontSizeMax=statsMaxFont;
        stats.alignment=TextAlignmentOptions.Center;stats.margin=Vector4.zero;
        stats.color=insetStats?new Color(.97f,.94f,.83f):value.creature?new Color(.11f,.12f,.12f):new Color(.94f,.95f,.92f);
        statsPlate.transform.localPosition=new Vector3(statsX,bottom+(insetStats?.122f:statBadge==1?.125f:value.creature?.105f:.127f),-.024f);
        float statHeight=insetStats?.21f:statBadge==1?.26f:statBadge==2?.22f:value.creature?.19f:.18f;
        statsPlate.transform.localScale=new Vector3(statsWidth,statHeight,1);
        colors[surfaces.IndexOf(statsPlate)]=statBadge>0?Color.white:new Color(.012f,.021f,.028f,.82f);
        statsPlate.GetPropertyBlock(properties);
        properties.SetFloat("_Radius",.12f);properties.SetFloat("_StatBadge",statBadge);
        properties.SetFloat("_FrameFinish",table.darkMetalCardFrames?1:0);
        properties.SetVector("_StatBadgeSize",new Vector4(statsWidth,statHeight,0,0));
        statsPlate.SetPropertyBlock(properties);
        countLabel.font=stats.font;countLabel.fontStyle=FontStyles.Bold;
        float countY=top-(value.room?.29f:.14f);
        countLabel.transform.localPosition=new Vector3(-.32f,countY,-.025f);countLabel.fontSize=1.55f;
        countPlate.transform.localPosition=new Vector3(-.32f,countY,-.02f);
        countPlate.transform.localScale=new Vector3(.30f,.18f,1);
        countPlate.GetPropertyBlock(properties);properties.SetFloat("_Radius",.16f);countPlate.SetPropertyBlock(properties);
        compactKind.font=table.CardNameFont;
        compactKind.text=value.creature?"":CompactType(value,type);
        compactKind.fontSize=1.08f;compactKind.fontStyle=FontStyles.Normal;compactKind.enableAutoSizing=false;
        compactKind.textWrappingMode=TextWrappingModes.NoWrap;
        float typeWidth=string.IsNullOrEmpty(stats.text)?.86f:.86f-statsWidth-.025f;
        float typeHeight=.17f;
        compactKind.transform.localPosition=new Vector3(-.43f+typeWidth*.5f,bottom+.125f,-.025f);
        compactKind.rectTransform.sizeDelta=new Vector2(typeWidth,typeHeight);
        compactKind.overflowMode=TextOverflowModes.Ellipsis;
        compactKind.alignment=TextAlignmentOptions.Left;compactKind.margin=Vector4.zero;
        compactKind.color=new Color(.95f,.96f,.93f);
        compactKind.outlineWidth=0;
        if(resourceLand){
            // Match creature information hierarchy: name above, type and
            // quantities below. Terrain itself is a shorter landscape tile.
            // Share the quiet footer treatment used by other permanents. A
            // single-digit quantity fits on this same line with a basic type.
            compactKind.fontSize=1.08f;compactKind.fontStyle=FontStyles.Normal;
            compactKind.textWrappingMode=TextWrappingModes.NoWrap;
            compactKind.enableAutoSizing=false;
            compactKind.overflowMode=TextOverflowModes.Ellipsis;
            compactKind.alignment=TextAlignmentOptions.Left;
            landCaptionCountWidth=0;
            colors[surfaces.IndexOf(compactFoot)]=new Color(.006f,.012f,.019f,.96f);
            compactFoot.GetPropertyBlock(properties);properties.SetFloat("_BottomFade",1);compactFoot.SetPropertyBlock(properties);
            LayoutLandCaption();
        }
        status.transform.localPosition=new Vector3(0,top+compactHeaderHeight+.14f,-.023f);
        status.rectTransform.sizeDelta=new Vector2(1.08f,.15f);status.fontSize=1.12f;
        status.overflowMode=TextOverflowModes.Overflow;
        statePlate.transform.localPosition=new Vector3(0,top+compactHeaderHeight+.14f,-.018f);
        statePlate.transform.localScale=new Vector3(1.08f,.15f,1);
        BindRoomFace(value,art);
    }

    void ApplyPrintedFrame(MeshRenderer part,float header,float frameAspect,Color left,Color right)
    {
        part.GetPropertyBlock(properties);
        properties.SetFloat("_FrameTint",2);
        properties.SetColor("_FrameColorA",left);properties.SetColor("_FrameColorB",right);
        var size=part.transform.localScale;
        properties.SetVector("_PaperSize",new Vector4(size.x,size.y,0,0));
        properties.SetFloat("_PaperHeader",header);
        properties.SetFloat("_FrameFinish",table.darkMetalCardFrames?1:0);
        var stock=table.CardNameplate;
        properties.SetTexture("_NameTex",stock!=null?stock.texture:Texture2D.whiteTexture);
        var rect=stock!=null?stock.textureRect:new Rect(0,0,1,1);
        properties.SetVector("_NameUVRect",stock!=null?new Vector4(rect.x/stock.texture.width,rect.y/stock.texture.height,rect.width/stock.texture.width,rect.height/stock.texture.height):new Vector4(0,0,1,1));
        properties.SetTexture("_MainTex",table.cardFrameTexture!=null?table.cardFrameTexture:Texture2D.whiteTexture);
        properties.SetFloat("_FrameWidth",.10f);properties.SetFloat("_FrameAspect",frameAspect);
        properties.SetFloat("_CutCorner",0);properties.SetFloat("_Radius",0);
        part.SetPropertyBlock(properties);
    }

    void AlignCompactNames()
    {
        if(!compactFace||!table.darkMetalCardFrames)return;
        float lower=aspect*.5f+.028f;
        float upper=aspect*.5f+compactHeaderHeight-.008f;
        void Fit(TextMeshPro text){
            if(text==null||string.IsNullOrEmpty(text.text))return;
            float available=upper-lower;
            float height=text.textBounds.size.y;
            if(height>available&&height>0){
                text.fontSizeMax=text.fontSize*available/height;
                text.fontSizeMin=Mathf.Min(text.fontSizeMin,text.fontSizeMax);
                text.ForceMeshUpdate();
            }
            var position=text.transform.localPosition;
            position.y=(lower+upper)*.5f-text.textBounds.center.y;
            text.transform.localPosition=position;
        }
        if(compactRoom&&roomNames!=null){foreach(var name in roomNames)Fit(name);}
        else Fit(title);
    }

    void AlignLoyaltyGlyphs()
    {
        if(!compactFace||card==null||!card.planeswalker||card.creature||card.room||string.IsNullOrEmpty(stats.text))return;
        // TMP centers typographic advance, not the bold glyph's visible bounds.
        // Run after auto-sizing has generated the mesh, and center the ink in
        // the shield's face with a small optical lift above its pointed tail.
        var position=statsPlate.transform.localPosition;
        position.x-=stats.textBounds.center.x;
        position.y+=statsPlate.transform.localScale.y*.03f-stats.textBounds.center.y;
        position.z=stats.transform.localPosition.z;
        stats.transform.localPosition=position;
    }

    void LayoutLandCaption()
    {
        float width=1,height=aspect;
        float bottom=-height*.5f;
        float countWidth=Mathf.Clamp(landCaptionCountWidth,0,.36f);
        // Match the other permanent footers: type left, unobtrusive count
        // right, both inside the art rather than a floating badge below it.
        float typeWidth=width-.09f-countWidth-(countWidth>0?.02f:0);
        void Place(Transform node,Vector3 position){node.localRotation=Quaternion.identity;node.localPosition=position;}
        Place(compactKind.transform,new Vector3(-width*.5f+.04f+typeWidth*.5f,bottom+.13f,-.025f));
        compactKind.rectTransform.sizeDelta=new Vector2(typeWidth,.18f);
        Place(compactFoot.transform,new Vector3(0,bottom+.155f,-.012f));
        compactFoot.transform.localScale=new Vector3(width-.05f,.31f,1);
        float countX=width*.5f-.04f-countWidth*.5f;
        Place(countLabel.transform,new Vector3(countX,bottom+.13f,-.025f));
        countLabel.rectTransform.sizeDelta=new Vector2(Mathf.Max(.05f,countWidth),.18f);
        Place(countPlate.transform,new Vector3(countX,bottom+.13f,-.020f));
        countPlate.transform.localScale=new Vector3(countWidth,.18f,1);
    }

    void BindCountBadge(int count,bool inHand)
    {
        if(inHand||count<=1)return;
        // The quantity sits outside the art, above its left edge, so it stays
        // legible on both land and nonland groups without squeezing the type line.
        countLabel.text="x"+count;
        float width=count<10?.37f:count<100?.48f:.60f;
        float y=aspect*.5f-.055f;
        float x=-.44f;
        countBorder.transform.localPosition=new Vector3(x,y,-.018f);
        countBorder.transform.localScale=new Vector3(width+.034f,.272f,1);
        countPlate.transform.localPosition=new Vector3(x,y,-.022f);
        countPlate.transform.localScale=new Vector3(width,.238f,1);
        countLabel.transform.localPosition=new Vector3(x,y+.005f,-.030f);
        countLabel.rectTransform.sizeDelta=new Vector2(width,.235f);
        countLabel.font=table.numberFont!=null?table.numberFont:table.font;
        countLabel.fontSize=2.5f;countLabel.fontStyle=FontStyles.Bold;
        countLabel.enableAutoSizing=true;countLabel.fontSizeMin=1.5f;countLabel.fontSizeMax=2.5f;
        countLabel.alignment=TextAlignmentOptions.Center;
        countLabel.color=Color.white;
        colors[surfaces.IndexOf(countPlate)]=new Color(.015f,.020f,.027f,.97f);
        colors[surfaces.IndexOf(countBorder)]=new Color(.92f,.94f,.94f,.96f);
        foreach(var part in new[]{countBorder,countPlate}){
            part.GetPropertyBlock(properties);
            properties.SetFloat("_Radius",.055f);
            part.SetPropertyBlock(properties);
        }
    }

    void ResetCompactLayout()
    {
        ResetRoomFace();
        compactHeaderHeight=0;
        rim.transform.localPosition=Vector3.zero;colliderShape.center=Vector3.zero;
        for(int i=0;i<groupLayers.Length;i++)groupLayers[i].transform.localPosition=new Vector3(.035f*(i+1),.035f*(i+1),.055f+.03f*i);
        foreach(var coloredPart in new[]{compactBody,compactNameRule}){
            coloredPart.GetPropertyBlock(properties);properties.SetFloat("_FrameTint",0);properties.SetFloat("_FrameWidth",0);properties.SetFloat("_PaperHeader",0);coloredPart.SetPropertyBlock(properties);
        }
        foreach(var caption in new[]{title.transform,compactKind.transform,compactHeader.transform,compactFoot.transform,countLabel.transform,countPlate.transform})caption.localRotation=Quaternion.identity;
        shadow.GetPropertyBlock(properties);properties.SetFloat("_Contact",0);shadow.SetPropertyBlock(properties);
        foreach(var surface in new[]{rim,compactBody,face,compactFoot}){
            surface.GetPropertyBlock(properties);properties.SetFloat("_CutCorner",0);surface.SetPropertyBlock(properties);
        }
        rim.GetPropertyBlock(properties);properties.SetFloat("_Bevel",0);rim.SetPropertyBlock(properties);
        face.transform.localPosition=new Vector3(0,0,-.004f);
        shadow.transform.localPosition=new Vector3(.035f,-.045f,.045f);
        title.font=table.font;
        title.transform.localPosition=Vector3.back*.02f;
        title.rectTransform.sizeDelta=new Vector2(.9f,.7f);
        title.enableAutoSizing=false;title.fontSize=2.4f;title.fontStyle=FontStyles.Normal;title.alignment=TextAlignmentOptions.Center;
        title.overflowMode=TextOverflowModes.Overflow;title.color=new Color(.95f,.95f,.91f);
        stats.font=table.font;stats.fontStyle=FontStyles.Normal;stats.enableAutoSizing=false;
        stats.rectTransform.sizeDelta=new Vector2(.47f,.19f);stats.fontSize=2.35f;
        statsPlate.transform.localScale=new Vector3(.48f,.20f,1);
        statsPlate.GetPropertyBlock(properties);properties.SetFloat("_Radius",.025f);properties.SetFloat("_StatBadge",0);properties.SetFloat("_FrameFinish",0);statsPlate.SetPropertyBlock(properties);
        colors[surfaces.IndexOf(statsPlate)]=new Color(.035f,.05f,.06f);
        countLabel.font=table.font;countLabel.fontStyle=FontStyles.Normal;
        countLabel.enableAutoSizing=false;countLabel.alignment=TextAlignmentOptions.Center;
        countLabel.rectTransform.sizeDelta=new Vector2(.33f,.19f);
        countLabel.transform.localPosition=new Vector3(-.33f,-.62f,-.016f);countLabel.fontSize=2.1f;
        countPlate.transform.localPosition=new Vector3(-.33f,-.62f,-.012f);countPlate.transform.localScale=new Vector3(.33f,.19f,1);
        countPlate.GetPropertyBlock(properties);properties.SetFloat("_Radius",.025f);countPlate.SetPropertyBlock(properties);
        status.transform.localPosition=new Vector3(0,.66f,-.016f);status.rectTransform.sizeDelta=new Vector2(.98f,.19f);
        status.fontSize=1.7f;status.overflowMode=TextOverflowModes.Overflow;
        statePlate.transform.localPosition=new Vector3(0,.66f,-.012f);statePlate.transform.localScale=new Vector3(.98f,.19f,1);
    }

    internal static int FrameColorBits(ForgeCardView value)
    {
        if(value.land&&!value.creature)
            return value.landPresentationKnown||value.landManaColors!=0?value.landManaColors&31:ForgeBattlefieldController.RedesignLandColors(value);
        // A projected zero is genuinely colorless, including devoid and color
        // removal. Only legacy views without a baseline infer printed colors.
        if(value.hasColorBaseline||value.currentColors!=0)return value.currentColors&31;
        int bits=0;
        foreach(char symbol in new[]{'W','U','B','R','G'})if((value.manaCost??"").Contains(symbol))bits|=symbol=='W'?1:symbol=='U'?2:symbol=='B'?4:symbol=='R'?8:16;
        return bits;
    }
    internal static void FramePalette(int bits,out Color left,out Color right,bool readable=false)
    {
        Color Tint(int bit)=>readable?bit switch {
            1=>new Color(.95f,.92f,.72f),2=>new Color(.07f,.42f,1.0f),
            4=>new Color(.14f,.14f,.16f),8=>new Color(1.0f,.12f,.045f),
            16=>new Color(.035f,.60f,.16f),_=>new Color(.46f,.60f,.70f)}:bit switch {
            1=>new Color(.91f,.86f,.65f),2=>new Color(.08f,.44f,.85f),
            4=>new Color(.45f,.29f,.58f),8=>new Color(.83f,.16f,.07f),
            16=>new Color(.10f,.55f,.25f),_=>new Color(.32f,.40f,.48f)};
        int first=0,second=0,count=0;
        for(int bit=1;bit<=16;bit<<=1)if((bits&bit)!=0){if(count==0)first=bit;else if(count==1)second=bit;count++;}
        left=right=count>2?(readable?new Color(.98f,.62f,.10f):new Color(.73f,.43f,.10f)):Tint(first);
        if(count==2)right=Tint(second);
    }
    static Color FrameColor(ForgeCardView value)
    {
        int bits=FrameColorBits(value);
        if(bits!=0&&(bits&(bits-1))!=0)return new Color(.65f,.53f,.28f);
        return bits switch {1=>new Color(.76f,.72f,.54f),2=>new Color(.20f,.47f,.64f),4=>new Color(.37f,.32f,.43f),8=>new Color(.67f,.30f,.20f),16=>new Color(.27f,.47f,.31f),_=>new Color(.46f,.51f,.54f)};
    }
    static string CompactType(ForgeCardView value,string type)
    {
        // Preserve the entire current type line, including unknown/new subtypes.
        // Ellipsis belongs to the label renderer, never to the source text.
        return CardTypeText.Line(string.IsNullOrWhiteSpace(type)?value.originalType??"":type);
    }
    void SyncCompactVisibility(bool visible)
    {
        compactBody.enabled=visible&&(compactFace||missingHandArt);
        compactHeader.enabled=compactFoot.enabled=compactNameRule.enabled=visible&&compactFace;
        compactHeader.enabled=false;
        compactNameRule.enabled=false;
        // The thin printed frame supplies its own rounded silhouette.
        surfaces[1].enabled=visible&&!compactFace;
        SyncRoomVisibility(visible);
    }
}
