using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

// A real scene object: mesh surfaces and 3D TextMeshPro, never a card Canvas.
// The invisible anchor is the compatibility boundary for native actions and arrows.
public sealed partial class BattlefieldWorldCard : MonoBehaviour
{
    BattlefieldWorldTable table;
    RectTransform pose;
    ForgeBattlefieldCardPointer pointer;
    ForgeCardView card;
    bool hand;
    int order;
    float aspect = 1.4f, opacity = 1, handBrightness = 1;
    bool deathDissolve;
    public void SetDeathDissolve(bool enabled)
    {
        deathDissolve=enabled;
        appliedOpacity=-1;
    }
    BoxCollider colliderShape;
    readonly List<MeshRenderer> surfaces = new();
    readonly List<TextMeshPro> labels = new();
    readonly List<Color> colors = new();
    readonly Dictionary<string,BattlefieldCardPart> prefabParts = new();
    static void ReleasePreviewObject(UnityEngine.Object value){
        if(value==null)return;
#if UNITY_EDITOR
        if(!Application.isPlaying){DestroyImmediate(value);return;}
#endif
        Destroy(value);
    }
    MaterialPropertyBlock properties;
    MeshRenderer face, rim, shadow;
    MeshRenderer statsPlate,countPlate,countBorder,statePlate,woundMark;
    TextMeshPro stats, countLabel, status, fees, title;
    Vector3[] corners = new Vector3[4];
    readonly List<Renderer> textRenderers = new();
    sealed class ExileLayer
    {
        public MeshRenderer frame,image,printedName;
        public bool Contains(MeshRenderer part)=>part==frame||part==image||part==printedName;
    }
    ExileLayer firstExile,secondExile;
    float appliedOpacity=-1;
    float tappedVisual,appliedTappedVisual=-1;
    MeshRenderer tappedIndicator;
    int appliedHandOrder;
    bool poseInitialized,previousLift;
    Vector2 displayedCenter;
    Transform fieldMotionSpace;
    Vector3 displayedLocalCenter;
    bool hasPriorTap;
    private const float StackLandingDuration=.32f;
    private float stackLandingElapsed=StackLandingDuration,stackLandingWidth;
    private Vector2 stackLandingOrigin;
    private Vector3 landingHitOffset;
    float combatAdvance, desiredCombatAdvance;
    internal const float AttackLungeDuration = .54f;
    internal const float AttackContactDelay = AttackLungeDuration*.47f;
    float attackLungeElapsed = AttackLungeDuration;
    Vector2 attackDirection;
    public void StartAttackLunge(Vector2 direction)
    {
        attackDirection = direction.normalized;
        attackLungeElapsed = 0;
    }
    public void SetCombatAdvance(int direction) => desiredCombatAdvance=direction;
    public void StartStackLanding(Vector2 screenCenter,float screenWidth)
    {
        stackLandingOrigin=screenCenter;
        stackLandingWidth=screenWidth;
        stackLandingElapsed=0;
    }
    public void CancelStackLanding()
    {
        stackLandingElapsed=StackLandingDuration;
        combatAdvance=desiredCombatAdvance=0;
        attackLungeElapsed=AttackLungeDuration;
        landingHitOffset=Vector3.zero;
    }
    public void StartFieldTransition(Vector2 screenCenter,bool previouslyTapped)
    {
        displayedCenter=screenCenter;
        fieldMotionSpace=null;
        tappedVisual=previouslyTapped?1:0;
        hasPriorTap=true;
    }
    Rect clipBounds;
    public Rect? SeatViewport { get; set; }
    bool hadSeatViewport;
    Vector4 appliedClip;
    Vector4 appliedExileOffsets;
    int representedCount;
    MeshRenderer[] groupLayers;
    MeshRenderer[] costCovers;
    Texture2D[] costTextures=new Texture2D[2];
    BattlefieldWorldCosts costData;
    Sprite costArt;
    string patchKey;
    public RectTransform Anchor { get; private set; }
    public bool Lifted { get; private set; }
    public bool UsesHudCamera => hand;
    public bool Interactive { get; private set; }
    public int InputOrder { get; private set; }
    public int NativeId => card?.id ?? 0;
    internal bool HasVisibleFace => colliderShape!=null && colliderShape.enabled && poseInitialized && clipBounds.Contains(ProjectedCenter);
    internal Vector2 ProjectedCenter => table.TableCamera.WorldToScreenPoint(transform.TransformPoint(colliderShape.center));

    internal Vector2 ProjectedEdgeToward(Vector2 screenTarget)
    {
        // Intersect the pointer ray with the actual tilted face, then clip the
        // segment from its centre to its rim in the card's own coordinates.
        Vector3 center=colliderShape.center;
        var ray=table.TableCamera.ScreenPointToRay(screenTarget);
        var plane=new Plane(transform.forward,transform.TransformPoint(center));
        if(!plane.Raycast(ray,out float depth))return ProjectedCenter;
        Vector2 toward=transform.InverseTransformPoint(ray.GetPoint(depth))-center;
        Vector2 half=(Vector2)colliderShape.size*.5f;
        float distance=Mathf.Max(Mathf.Abs(toward.x)/half.x,Mathf.Abs(toward.y)/half.y);
        Vector3 edge=center+(Vector3)(toward/Mathf.Max(1,distance));
        return table.TableCamera.WorldToScreenPoint(transform.TransformPoint(edge));
    }
    public bool Concealed => card?.faceDown == true;
    public float HandCostExtent => hand && fees!=null && !string.IsNullOrEmpty(fees.text)
        ?fees.transform.localPosition.y+fees.textBounds.max.y:0;
    public Texture ArtTexture => costArt != null ? costArt.texture : null;
    public void SetLayoutOrder(int slot)=>order=slot;

    public void Initialize(BattlefieldWorldTable owner, RectTransform anchor, RectTransform visualPose, ForgeBattlefieldCardPointer input)
    {
        if(table != null) throw new InvalidOperationException("World card is already initialized.");
        table = owner; Anchor = anchor; pose = visualPose; pointer = input;
        foreach(var part in GetComponentsInChildren<BattlefieldCardPart>(true))prefabParts.Add(part.Binding,part);
        properties = new MaterialPropertyBlock();
        shadow = Surface("Contact shadow", new Vector3(.035f,-.045f,.045f), new Vector2(1.13f,1.55f), Color.white);
        shadow.sharedMaterial = table.Shadow;
        var edge=Surface("Paper edge",new Vector3(0,0,.012f),new Vector2(1.016f,1.416f),new Color(.2f,.22f,.23f));
        edge.GetComponent<MeshFilter>().sharedMesh=table.EdgeMesh;
        rim = Surface("State rim",Vector3.zero,new Vector2(1.025f,1.425f),new Color(.26f,.31f,.32f));
        face = Surface("Printed face",new Vector3(0,0,-.004f),new Vector2(1,1.4f),Color.white);
        colliderShape = GetComponent<BoxCollider>();
        if(colliderShape==null)colliderShape=gameObject.AddComponent<BoxCollider>();
        colliderShape.size = new Vector3(1.025f,1.425f,.045f);
        stats = Label("Current stats",new Vector3(.25f,-.61f,-.014f),new Vector2(.47f,.19f),2.35f,TextAlignmentOptions.Center);
        countLabel = Label("Group count",new Vector3(-.33f,-.62f,-.016f),new Vector2(.32f,.18f),2.1f,TextAlignmentOptions.Center);
        status = Label("State",new Vector3(0,.66f,-.016f),new Vector2(.98f,.19f),1.7f,TextAlignmentOptions.Center);
        fees = Label("Current costs",new Vector3(0,.79f,-.018f),new Vector2(1,.18f),1.05f,TextAlignmentOptions.Right);
        title = Label("Unavailable art",Vector3.back*.02f,new Vector2(.9f,.7f),2.4f,TextAlignmentOptions.Center);
        statsPlate=Surface("Stats backing",new Vector3(.25f,-.61f,-.01f),new Vector2(.48f,.20f),new Color(.035f,.05f,.06f));
        countPlate=Surface("Count backing",new Vector3(-.33f,-.62f,-.012f),new Vector2(.33f,.19f),new Color(.035f,.05f,.06f));
        countBorder=Surface("Count outline",new Vector3(-.33f,-.62f,-.009f),new Vector2(.37f,.23f),new Color(.88f,.91f,.92f));
        statePlate=Surface("State backing",new Vector3(0,.66f,-.012f),new Vector2(.98f,.19f),new Color(.035f,.05f,.06f,.85f));
        groupLayers=new[]{
            Surface("Grouped card 2",new Vector3(.035f,.035f,.055f),new Vector2(1.025f,1.425f),new Color(.24f,.28f,.30f)),
            Surface("Grouped card 3",new Vector3(.07f,.07f,.085f),new Vector2(1.025f,1.425f),new Color(.16f,.20f,.22f))};
        costCovers=new[]{Surface("Printed cost cover",Vector3.back*.009f,Vector2.one,Color.white),Surface("Secondary cost cover",Vector3.back*.009f,Vector2.one,Color.white)};
        BuildCompactFace();
        BuildActionHint();
        tappedIndicator=Surface("Tapped indicator",new Vector3(0,0,-.05f),Vector2.one*.64f,new Color(.91f,.92f,.94f,.64f));
        tappedIndicator.GetComponent<MeshFilter>().sharedMesh=table.TappedMesh;
        tappedIndicator.enabled=false;
    }
    MeshRenderer Surface(string name, Vector3 position, Vector2 size, Color color)
    {
        prefabParts.TryGetValue(name,out var part);
        var node = part != null ? part.gameObject : new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
        if(part == null){node.transform.SetParent(transform,false);part=node.AddComponent<BattlefieldCardPart>();part.Configure(name,color);prefabParts.Add(name,part);}
        node.transform.localPosition = position;node.transform.localScale = new Vector3(size.x,size.y,1);
        node.GetComponent<MeshFilter>().sharedMesh = table.Quad;
        var renderer = node.GetComponent<MeshRenderer>();renderer.sharedMaterial = table.Paper;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;surfaces.Add(renderer);colors.Add(part.Tint);return renderer;
    }
    TextMeshPro Label(string name,Vector3 position,Vector2 size,float fontSize,TextAlignmentOptions align)
    {
        prefabParts.TryGetValue(name,out var part);
        var node = part != null ? part.gameObject : new GameObject(name);
        if(part == null){node.transform.SetParent(transform,false);part=node.AddComponent<BattlefieldCardPart>();part.Configure(name,Color.white);prefabParts.Add(name,part);}
        node.transform.localPosition=position;
        var text = node.GetComponent<TextMeshPro>();
        if(text==null)text=node.AddComponent<TextMeshPro>();
        text.font=table.font;
        text.fontSize=fontSize;text.alignment=align;text.color=new Color(.95f,.95f,.91f);
        text.rectTransform.sizeDelta=size;text.enableAutoSizing=false;text.textWrappingMode=TextWrappingModes.NoWrap;
        text.overflowMode=TextOverflowModes.Overflow;text.richText=true;
        text.spriteAsset=CardSymbolText.Atlas;
        labels.Add(text);return text;
    }
    public void Bind(ForgeCardView value, Sprite art, bool inHand, int count, int slot, Color accent, string cost, string secondaryCost, string state,BattlefieldWorldCosts costs=null,bool emphasizeFrame=false,BattlefieldWorldActionHint actionHint=BattlefieldWorldActionHint.None,float handBrightness=1,BattlefieldExilePreview exile=default)
    {
        ResetCompactLayout();
        this.handBrightness=handBrightness;
        card=value;hand=inHand;order=slot;representedCount=count;gameObject.name="World card "+value.id;
        if(inHand)tappedVisual=0;
        colors[surfaces.IndexOf(rim)]=accent;
        colors[surfaces.IndexOf(face)]=art!=null?Color.white:new Color(.15f,.19f,.23f);
        aspect=art!=null?Mathf.Clamp(art.rect.height/art.rect.width,.7f,1.5f):1.4f;
        bool landscapeBattle=value.battle&&!hand&&!value.faceDown;
        if(landscapeBattle)aspect=1/aspect;
        face.transform.localRotation=Quaternion.Euler(0,0,landscapeBattle?-90:0);
        face.transform.localScale=landscapeBattle?new Vector3(aspect,1,1):new Vector3(1,aspect,1);
        rim.transform.localScale=new Vector3(1.025f,aspect+.025f,1);
        shadow.transform.localScale=new Vector3(1.14f,aspect+.18f,1);
        surfaces[1].transform.localScale=new Vector3(1.016f,aspect+.016f,.025f);
        colliderShape.size=new Vector3(1.025f,aspect+.025f,.045f);
        if(landscapeBattle){
            stats.transform.localPosition=new Vector3(0,-aspect*.5f+.06f,-.014f);
            statsPlate.transform.localPosition=new Vector3(0,-aspect*.5f+.06f,-.01f);
        }else{
            stats.transform.localPosition=new Vector3(.25f,-.61f,-.014f);
            statsPlate.transform.localPosition=new Vector3(.25f,-.61f,-.01f);
        }
        foreach(var layer in groupLayers)layer.transform.localScale=new Vector3(1.025f,aspect+.025f,1);
        face.GetPropertyBlock(properties);
        properties.SetTexture("_MainTex",art!=null?art.texture:Texture2D.whiteTexture);
        // Favor the next sharper mip on world faces after the local card art
        // is cropped. Leave the shared hand/inspector texture unchanged.
        properties.SetFloat("_MipBias",!inHand&&art!=null?-art.texture.mipMapBias-.25f:0);
        properties.SetFloat("_ImageSharpness",0);
        properties.SetVector("_SourceTexelSize",art!=null
            ?new Vector4(1f/art.texture.width,1f/art.texture.height,art.texture.width,art.texture.height)
            :Vector4.zero);
        if(art!=null){var r=art.textureRect;properties.SetVector("_UVRect",new Vector4(r.x/art.texture.width,r.y/art.texture.height,r.width/art.texture.width,r.height/art.texture.height));}
        else properties.SetVector("_UVRect",new Vector4(0,0,1,1));
        face.SetPropertyBlock(properties);
        title.text=art==null?(value.faceDown?"?":GamePreferences.CardFaceName(value.name??"")):"";
        bool showStats=!hand&&!value.faceDown;
        stats.text=showStats&&value.creature?ForgeBattlefieldController.WorldStatsText(value,true):showStats&&value.planeswalker?value.loyalty.ToString():showStats&&value.battle?value.defense.ToString():"";
        stats.color=new Color(.96f,.96f,.90f);
        countLabel.text=count>1?"×"+count:"";
        status.text=value.faceDown?"":state;
        status.color=value.attacking?new Color(1,.43f,.3f):new Color(.66f,.83f,.93f);
        // Hidden objects never receive a cost/name projection.
        fees.text=hand&&!value.faceDown?Symbols(cost)+(secondaryCost!=null?"  /  "+Symbols(secondaryCost):""):"";
        costData=hand&&!value.faceDown?costs:null;
        if(costArt!=art||patchKey!=costData?.PatchKey){
            costArt=art;patchKey=costData?.PatchKey;
            for(int i=0;i<2;i++){
                ReleasePreviewObject(costTextures[i]);costTextures[i]=null;
                if(costData==null||art==null)continue;
                costTextures[i]=costData.CreatePatch(art,i==1,out var bounds);
                if(costTextures[i]==null)continue;
                var cover=costCovers[i];cover.transform.localPosition=new Vector3(bounds.center.x-.5f,(bounds.center.y-.5f)*aspect,-.009f);
                cover.transform.localScale=new Vector3(bounds.width,bounds.height*aspect,1);
                cover.GetPropertyBlock(properties);properties.SetTexture("_MainTex",costTextures[i]);properties.SetFloat("_Radius",0);cover.SetPropertyBlock(properties);
            }
        }
        BindCompactFace(value,art,inHand,accent,emphasizeFrame);
        BindCountBadge(count,inHand);
        BindSagaChapters(value);
        BindWorldBadges(value);
        BindExilePreview(exile);
        BindActionHint(value,inHand,actionHint);
        tappedIndicator.transform.localScale=Vector3.one*(.64f*Mathf.Min(1,aspect));
        foreach(var text in labels)text.ForceMeshUpdate();
        BindWoundMark();
        AlignCompactNames();
        AlignLoyaltyGlyphs();
        textRenderers.Clear();
        foreach(var text in labels)textRenderers.AddRange(text.GetComponentsInChildren<Renderer>());
        appliedOpacity=-1;
    }
    bool HasMarkedDamage => !hand&&card!=null&&card.creature&&!card.faceDown&&card.damage>0;

    void BindWoundMark()
    {
        if(!HasMarkedDamage)return;
        if(woundMark==null){
            woundMark=Surface("Wound behind toughness",Vector3.zero,Vector2.one,Color.white);
            woundMark.GetPropertyBlock(properties);
            properties.SetFloat("_WoundMark",1);
            woundMark.SetPropertyBlock(properties);
        }
        // Align to the glyphs after TMP fits the badge, including signed or multi-digit values.
        var info=stats.textInfo;
        float x=statsPlate.transform.localPosition.x+statsPlate.transform.localScale.x*.22f;
        for(int i=0;i+1<info.characterCount;i++){
            if(info.characterInfo[i].character!='/')continue;
            float center=(info.characterInfo[i+1].bottomLeft.x+info.characterInfo[info.characterCount-1].topRight.x)*.5f;
            x=stats.transform.localPosition.x+center*stats.transform.localScale.x;
            break;
        }
        var badge=statsPlate.transform;
        float height=badge.localScale.y;
        woundMark.transform.localPosition=new Vector3(x,badge.localPosition.y+height*.08f,badge.localPosition.z+.002f);
        woundMark.transform.localScale=new Vector3(height*1.48f,height*2.05f,1);
    }

    void BindExilePreview(BattlefieldExilePreview preview)
    {
        if(!preview.first.present && firstExile==null)return;
        Vector2 size=colliderShape.size;
        var center=(Vector2)colliderShape.center+new Vector2(size.x*.08f,-size.y*.18f);
        BindExileLayer(ref firstExile,preview.first,"Linked exile",center,size,.013f);
        BindExileLayer(ref secondExile,preview.second,"Linked exile second",center+BattlefieldExilePreview.SecondOffset*size.x,size,.018f);
    }
    void BindExileLayer(ref ExileLayer layer,BattlefieldExilePreview.Face preview,string label,Vector2 center,Vector2 size,float front)
    {
        if(layer==null) {
            if(!preview.present)return;
            layer=new ExileLayer {
                frame=Surface(label+" card",Vector3.zero,Vector2.one,Color.white),
                image=Surface(label+" printing",Vector3.zero,Vector2.one,Color.white)
            };
        }
        bool showPrintedName=preview.faceUp && preview.art!=null;
        if(showPrintedName && layer.printedName==null)
            layer.printedName=Surface(label+" printed name",Vector3.zero,Vector2.one,Color.white);
        layer.frame.gameObject.SetActive(preview.present);
        layer.image.gameObject.SetActive(preview.art!=null);
        if(layer.printedName!=null)layer.printedName.gameObject.SetActive(showPrintedName);
        if(!preview.present)return;
        void Place(Transform part,Vector2 pos,Vector2 dimensions,float depth) {
            part.localPosition=new Vector3(pos.x,pos.y,front+depth);
            part.localScale=new Vector3(dimensions.x,dimensions.y,1);
        }
        Place(layer.frame.transform,center,size,showPrintedName?.001f:.004f);
        FramePalette(preview.colorBits,out var left,out var right,table.darkMetalCardFrames);
        colors[surfaces.IndexOf(layer.frame)]=showPrintedName?Color.white:Color.Lerp(left,right,.5f);
        if(!showPrintedName) {
            layer.frame.GetPropertyBlock(properties);
            properties.SetFloat("_FrameTint",0);
            properties.SetTexture("_MainTex",Texture2D.whiteTexture);
            layer.frame.SetPropertyBlock(properties);
        }
        if(preview.art==null)return;
        var texture=preview.art.texture;
        var scan=preview.art.textureRect;
        void Print(MeshRenderer part,Vector2 offset,Vector2 dimensions,Rect crop,float depth=.003f) {
            Place(part.transform,center+offset,dimensions,depth);
            part.GetPropertyBlock(properties);
            properties.SetTexture("_MainTex",texture);
            properties.SetVector("_UVRect",new Vector4(crop.x/texture.width,crop.y/texture.height,crop.width/texture.width,crop.height/texture.height));
            part.SetPropertyBlock(properties);
        }
        if(!preview.faceUp){Print(layer.image,Vector2.zero,size,scan);return;}
        float titleHeight=size.x*.96f*(scan.height*.052f)/(scan.width*.87f);
        ApplyPrintedFrame(layer.frame,titleHeight,size.y/size.x,left,right);
        var artSize=new Vector2(size.x-.028f,size.y-titleHeight-.028f);
        var layout=table.artLayouts?.Find(preview.art);
        var illustration=BattlefieldCardArtLayouts.Fit(scan,layout?.illustration??new Rect(.07f,.43f,.86f,.45f),artSize.x/artSize.y,layout?.focus??new Vector2(.5f,1));
        Print(layer.image,Vector2.down*(titleHeight*.5f),artSize,illustration);
        Print(layer.printedName,Vector2.up*((size.y-titleHeight)*.5f-.01f),new Vector2(size.x*.96f,titleHeight),
            new Rect(scan.x+scan.width*.065f,scan.y+scan.height*.895f,scan.width*.87f,scan.height*.052f),0);
    }
    static string Symbols(string value)=>string.Equals(value,"no cost",StringComparison.OrdinalIgnoreCase)?"—":Regex.Replace(value??"",@"\{([^{}]+)\}",m=>"<sprite name=\""+m.Groups[1].Value.ToUpperInvariant()+"\">");
    public bool Hit(Ray ray,out RaycastHit hit)
    {
        // The legal hit area is at the settled slot from the first frame. A flying
        // permanent must not capture a click aimed at a card along its route.
        ray.origin-=landingHitOffset;
        if(!colliderShape.Raycast(ray,out hit,1000))return false;
        hit.point+=landingHitOffset;
        return true;
    }
    public bool AcceptPoint(Vector2 point)=>clipBounds.Contains(point)&&(!hand||pointer.IsRaycastLocationValid(point,table.HudCamera));
    public void SyncPose()
    {
        if(Anchor==null){ReleasePreviewObject(gameObject);return;}
        bool visible=Anchor.gameObject.activeInHierarchy;
        opacity=1;Interactive=visible;
        for(Transform t=Anchor;t!=null;t=t.parent)if(t.TryGetComponent<CanvasGroup>(out var group)){opacity*=group.alpha;Interactive&=group.blocksRaycasts;}
        visible&=opacity>.01f;Interactive&=visible;
        if(hand&&GameSettingsPanel.IsOpen)visible=Interactive=false;
        if(card==null||pose==null)visible=Interactive=false;
        float tapTarget=!hand&&card?.tapped==true?1:0;
        float step=Application.isPlaying?Mathf.Min(Time.unscaledDeltaTime,.05f):1;
        tappedVisual=!poseInitialized&&!hasPriorTap?tapTarget:Mathf.MoveTowards(tappedVisual,tapTarget,step/.32f);
        foreach(var r in surfaces)r.enabled=visible;
        SyncCompactVisibility(visible);
        SyncActionHintVisibility(visible);
        tappedIndicator.enabled=visible&&tappedVisual>.001f;
        if(statsPlate!=null)statsPlate.enabled=visible&&!string.IsNullOrEmpty(stats.text);
        if(woundMark!=null)woundMark.enabled=visible&&HasMarkedDamage;
        if(countPlate!=null)countPlate.enabled=visible&&!string.IsNullOrEmpty(countLabel.text);
        if(countBorder!=null)countBorder.enabled=visible&&!string.IsNullOrEmpty(countLabel.text);
        if(statePlate!=null)statePlate.enabled=visible&&!string.IsNullOrEmpty(status.text)&&status.text!=ForgeBattlefieldController.SicknessBadge;
        for(int i=0;i<groupLayers.Length;i++)groupLayers[i].enabled=visible&&representedCount>i+1;
        for(int i=0;i<costCovers.Length;i++)costCovers[i].enabled=visible&&costTextures[i]!=null&&costData!=null
            &&!(pointer.IsHovered&&(i==0?costData.MainChanged:costData.SecondaryChanged));
        foreach(var text in labels)text.enabled=visible;
        SyncSagaVisibility(visible);
        SyncWorldBadgeVisibility(visible);
        colliderShape.enabled=visible;
        if(!visible)return;
        bool returning=pose.localPosition.sqrMagnitude>.25f||(pose.localScale-Vector3.one).sqrMagnitude>.0001f;
        bool activeHand=hand&&(pointer.IsHovered||pointer.IsCastingDrag);
        Lifted=activeHand||hand&&returning;
        int handOrder=activeHand?34:Lifted?33:0;
        InputOrder=handOrder;
        clipBounds=(!hand?SeatViewport:null)??new Rect(0,0,Screen.width,Screen.height);
        for(Transform t=Anchor;t!=null;t=t.parent){
            if(t.TryGetComponent<Canvas>(out var canvas)&&canvas.overrideSorting)InputOrder=Mathf.Max(InputOrder,canvas.sortingOrder);
            if(Lifted||t is not RectTransform rect)continue;
            // Dense rows already page entire cards via CanvasGroup. Their flat layout rectangle
            // is smaller than a card's perspective silhouette near the sides of the table.
            bool clipRect=t.TryGetComponent<UnityEngine.UI.RectMask2D>(out var mask)&&mask.isActiveAndEnabled
                &&(hand||!t.TryGetComponent<DenseFieldViewport>(out var row)||!row.isActiveAndEnabled);
            if(clipRect||t.TryGetComponent<UnityEngine.UI.Mask>(out var stencil)&&stencil.isActiveAndEnabled){
                rect.GetWorldCorners(corners);
                clipBounds=Rect.MinMaxRect(Mathf.Max(clipBounds.xMin,corners[0].x),Mathf.Max(clipBounds.yMin,corners[0].y),Mathf.Min(clipBounds.xMax,corners[2].x),Mathf.Min(clipBounds.yMax,corners[2].y));
            }
        }
        pose.GetWorldCorners(corners);
        Vector2 center=(corners[0]+corners[2])*.5f;
        float pixels=Vector3.Distance(corners[0],corners[3]);
        float displayAspect=aspect+(compactFace?compactHeaderHeight:0);
        // Shortening the body must not enlarge the card to refill its old slot.
        float fittingAspect=compactFace&&!card.room?(card.battle?displayAspect:1.26f):displayAspect;
        float width=hand?pixels:Mathf.Min(pixels,Vector3.Distance(corners[0],corners[1])/fittingAspect);
        if(!hand && table.fitFieldCardsToCell){
            // Reserve the tilted footprint in both states so tapping rotates the
            // card without shrinking it relative to its neighbours.
            const float angle=12*Mathf.Deg2Rad;
            float sine=Mathf.Sin(angle), cosine=Mathf.Cos(angle);
            Vector3 face=colliderShape.size;
            float cellHeight=Vector3.Distance(corners[0],corners[1]);
            width=Mathf.Min(pixels/(cosine*face.x+sine*face.y),cellHeight/(sine*face.x+cosine*face.y));
        }
        else if(!hand&&compactFace&&!card.room)width*=.92f;
        else if(!hand&&card.creature)width*=1.18f;
        Quaternion rotation;
        landingHitOffset=Vector3.zero;
        if(hand){
            // Hand meshes stay square to the screen even at rest. They share
            // pixel-space input anchors, without a Canvas-rendered card face.
            float depth=Lifted?(activeHand?-9:-8)
                :-1+(pointer.LeftHandCardOnTop?order:-order)*.001f;
            transform.position=new Vector3(center.x,center.y,depth);
            rotation=pose.rotation;
        }else{
            var parent=Anchor.parent;
            bool landing=stackLandingElapsed<StackLandingDuration;
            if(!landing&&Anchor!=pose&&Application.isPlaying&&(poseInitialized||hasPriorTap)){
                // Pan, zoom and row scrolling move the layout immediately. Ease only the
                // card's motion within that layout, or its arrival from another zone.
                Vector2 previous=fieldMotionSpace!=null&&fieldMotionSpace==parent
                    ?(Vector2)parent.TransformPoint(displayedLocalCenter):displayedCenter;
                center=Vector2.Lerp(previous,center,1-Mathf.Exp(-13*step));
            }
            displayedCenter=center;
            fieldMotionSpace=parent;
            if(parent!=null)displayedLocalCenter=parent.InverseTransformPoint(center);
            if(landing){
                float t=stackLandingElapsed/StackLandingDuration;
                float travel=Mathf.SmoothStep(0,1,t);
                float lift=Mathf.Min(Screen.height*.035f,Vector2.Distance(stackLandingOrigin,center)*.10f);
                center=Vector2.Lerp(stackLandingOrigin,center,travel)+Vector2.up*(4*t*(1-t)*lift);
                width=Mathf.Lerp(Mathf.Clamp(stackLandingWidth,width*.46f,width*1.35f),width,travel);
                stackLandingElapsed+=step;
            }
            combatAdvance=Mathf.MoveTowards(combatAdvance,desiredCombatAdvance,step/.16f);
            center.y+=Mathf.Sign(combatAdvance)*Mathf.SmoothStep(0,1,Mathf.Abs(combatAdvance))*width*.045f;
            bool lunging = attackLungeElapsed < AttackLungeDuration;
            float attackLift=0;
            if (lunging) {
                float t = attackLungeElapsed / AttackLungeDuration;
                float reach = t < .23f ? Mathf.SmoothStep(0, -.16f, t / .23f)
                    : t < .47f ? Mathf.SmoothStep(-.16f, .59f, (t - .23f) / .24f)
                    : t < .55f ? .59f : Mathf.SmoothStep(.59f, 0, (t - .55f) / .45f);
                center += attackDirection * (width * reach);
                attackLift=Mathf.Sin(Mathf.PI*t);
                attackLungeElapsed += step;
            }
            float tapArc=Mathf.Sin(Mathf.PI*tappedVisual);
            float height=.045f+order*.0007f+.12f*tapArc+.18f*attackLift;
            transform.position=table.TablePoint(center,height);
            if(landing || combatAdvance!=0 || lunging)landingHitOffset=table.TablePoint(displayedCenter,height)-transform.position;
            float angle=-12*Mathf.SmoothStep(0,1,tappedVisual);
            rotation=Quaternion.Euler(90-9*tapArc-7*attackLift,0,0)*Quaternion.AngleAxis(angle,Vector3.forward);
            width=Vector3.Distance(table.TablePoint(center-Vector2.right*width*.5f,height),table.TablePoint(center+Vector2.right*width*.5f,height));
        }
        transform.rotation=!poseInitialized||previousLift!=Lifted?rotation:Quaternion.Slerp(transform.rotation,rotation,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
        if(!hand&&compactFace)transform.position-=transform.rotation*(Vector3.up*(compactHeaderHeight*.5f*width));
        poseInitialized=true;previousLift=Lifted;
        // The HUD uses pixels in X/Y. Card parts must stay inside their own depth
        // band even when a high-resolution or enlarged hand face is hundreds of pixels wide.
        transform.localScale=new Vector3(width,width,hand?1f:width);
        int layer=hand?BattlefieldWorldTable.LiftLayer:BattlefieldWorldTable.CardLayer;
        gameObject.layer=layer;
        var clip=new Vector4(clipBounds.xMin/Screen.width,clipBounds.yMin/Screen.height,clipBounds.xMax/Screen.width,clipBounds.yMax/Screen.height);
        // TMP uses a different material from the paper. Hide a label as it
        // crosses a seat edge so its glyphs cannot float over the adjacent seat.
        bool clipLabels=!hand&&SeatViewport.HasValue;
        if(clipLabels||hadSeatViewport){
            foreach(var renderer in textRenderers){
                if(renderer==null)continue;
                bool outside=false;
                if(clipLabels && renderer.transform is RectTransform textRect){
                    textRect.GetWorldCorners(corners);
                    for(int i=0;i<4&&!outside;i++)
                        outside=!clipBounds.Contains(table.TableCamera.WorldToScreenPoint(corners[i]));
                }
                renderer.forceRenderingOff=outside;
            }
        }
        hadSeatViewport=clipLabels;
        // Mask each tucked layer in the source card's coordinates, keeping its stepped
        // silhouette intact when a lane ends immediately beside a visible card.
        var exileOffsets=Vector4.zero;
        if(firstExile!=null && firstExile.frame.gameObject.activeSelf) {
            var camera=table.TableCamera;
            Vector2 origin=camera.WorldToViewportPoint(transform.TransformPoint(colliderShape.center));
            Vector2 first=(Vector2)camera.WorldToViewportPoint(firstExile.frame.transform.position)-origin;
            Vector2 second=secondExile!=null && secondExile.frame.gameObject.activeSelf
                ?(Vector2)camera.WorldToViewportPoint(secondExile.frame.transform.position)-origin:Vector2.zero;
            exileOffsets=new Vector4(first.x,first.y,second.x,second.y);
        }
        if(Mathf.Approximately(appliedOpacity,opacity)&&appliedHandOrder==handOrder&&appliedClip==clip&&appliedExileOffsets==exileOffsets&&Mathf.Approximately(appliedTappedVisual,tappedVisual))return;
        appliedOpacity=opacity;appliedHandOrder=handOrder;
        appliedTappedVisual=tappedVisual;
        appliedClip=clip;
        appliedExileOffsets=exileOffsets;
        float brightness=hand?handBrightness:Mathf.Lerp(1,.58f,tappedVisual);
        for(int i=0;i<surfaces.Count;i++){
            var r=surfaces[i];r.gameObject.layer=layer;
            // Alpha name vignettes must composite after the opaque illustration.
            r.sortingOrder=Lifted?handOrder:r==actionHintSurface||r==tappedIndicator?2:r==compactHeader||r==compactNameRule||r==compactFoot||r==compactBody?1:0;
            r.sharedMaterial=r==shadow?(Lifted?table.LiftShadow:table.Shadow):(Lifted?table.LiftPaper:table.Paper);
            r.GetPropertyBlock(properties);var color=colors[i];color.a*=deathDissolve?Mathf.Min(1,opacity*6):opacity;
            properties.SetFloat("_DeathDissolve",deathDissolve?1-Mathf.Pow(opacity,1f/3f):0);
            if(r==tappedIndicator)color.a*=tappedVisual;
            properties.SetFloat("_Brightness",r==actionHintSurface||r==tappedIndicator?1:brightness);
            properties.SetColor("_Color",color);
            var surfaceClip=clip;
            if(!SeatViewport.HasValue&&firstExile!=null && firstExile.Contains(r))
                surfaceClip+=new Vector4(exileOffsets.x,exileOffsets.y,exileOffsets.x,exileOffsets.y);
            else if(!SeatViewport.HasValue&&secondExile!=null && secondExile.Contains(r))
                surfaceClip+=new Vector4(exileOffsets.z,exileOffsets.w,exileOffsets.z,exileOffsets.w);
            properties.SetVector("_ClipRect",surfaceClip);r.SetPropertyBlock(properties);
        }
        foreach(var renderer in textRenderers)if(renderer!=null){renderer.gameObject.layer=layer;renderer.sortingOrder=Lifted?handOrder:2;}
        // The black type face must composite after its wider white silhouette.
        if(compactFace&&compactKind!=null&&compactKind.GetComponent<Renderer>() is Renderer kindRenderer)
            kindRenderer.sortingOrder=Lifted?handOrder+1:3;
        foreach(var text in labels)text.alpha=opacity*Mathf.Lerp(1,.78f,tappedVisual);
    }
    void OnDestroy(){foreach(var texture in costTextures)ReleasePreviewObject(texture);}
}
