using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Scene-owned world presentation. The Canvas contains HUD and invisible input
// anchors only; card surfaces, rims, shadows and labels are scene meshes.
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public sealed class BattlefieldWorldTable : BaseRaycaster
{
    public Camera tableCamera;
    public Shader cardShader, tableShader;
    public TMP_FontAsset font;
    public TMP_FontAsset numberFont;
    public Transform tableSurface;
    [Tooltip("Preview compact illustration-first battlefield cards; hands retain full card faces.")]
    public bool compactBattlefieldCards=true;
    [Tooltip("Fit the visible card including its frame and tapped rotation within the assigned table cell.")]
    public bool fitFieldCardsToCell;
    [Tooltip("Use a dark engraved metal frame on compact battlefield cards. Full card scans and hand cards are unchanged.")]
    public bool darkMetalCardFrames;
    public BattlefieldWorldCard cardPrefab;
    public BattlefieldCardArtLayouts artLayouts;
    public Texture2D cardFrameTexture;
    TMP_FontAsset cardNameFont;
    bool nameFontLoaded;
    public TMP_FontAsset CardNameFont {
        get {
            if(GamePreferences.Language.StartsWith("zh")){
                var styled=TemplateCardFace.DisplayNameFont;
                if(styled!=null)return styled;
            }
            if(!nameFontLoaded){
                nameFontLoaded=true;
                cardNameFont=Resources.Load<TMP_FontAsset>("UI/Battlefield/BattlefieldCardNamesSDF");
            }
            return cardNameFont!=null?cardNameFont:font;
        }
    }
    Sprite cardNameplate;
    bool nameplateLoaded;
    public Sprite CardNameplate {
        get {
            if(!nameplateLoaded){
                nameplateLoaded=true;
                var frames=Resources.LoadAll<Sprite>("UI/Battlefield/PrintedNameplateV1");
                if(frames.Length>0)cardNameplate=frames[0];
            }
            return cardNameplate;
        }
    }
    public const int CardLayer = 27, LiftLayer = 28;
    readonly List<BattlefieldWorldCard> cards = new();
    Camera hudCamera;
    Canvas hud;
    Vector2Int viewport;
    UnityEngine.Rendering.RenderPipelineAsset previousQualityPipeline;
    UniversalRenderPipelineAsset scenePipeline;
    public override Camera eventCamera => hudCamera;
    public Camera TableCamera => tableCamera;
    public Camera HudCamera => hudCamera;
    public Transform CardsRoot { get; private set; }
    public Material Paper { get; private set; }
    public Material LiftPaper { get; private set; }
    public Material Shadow { get; private set; }
    public Material LiftShadow { get; private set; }
    public Mesh Quad { get; private set; }
    public Mesh EdgeMesh { get; private set; }
    public Mesh TappedMesh { get; private set; }
    Mesh roomLockMesh;
    Renderer tableRenderer;
    MaterialPropertyBlock tableProperties;
#if UNITY_EDITOR
    bool editorPreview;
    int previewCameraMask, previewTableLayer;
    bool previewCameraMsaa, previewBackdropActive;
    RenderMode previewCanvasMode;
    Camera previewCanvasCamera;
    float previewCanvasDistance;
#endif
    public Texture BackdropTexture { get; private set; }
    Texture sceneBackdrop;
    Vector2 viewPan;
    float viewZoom = 1;
    MenuThemeStyle appliedTheme;
    public Mesh RoomLockMesh => roomLockMesh!=null?roomLockMesh:roomLockMesh=BattlefieldWorldCard.CreateRoomLockMesh();

    protected override void Awake()
    {
        base.Awake();
        if (!Application.isPlaying) return;
        InitializeWorld();
    }

#if UNITY_EDITOR
    public void InitializeEditorPreview()
    {
        if(Application.isPlaying || CardsRoot!=null)return;
        editorPreview=true;
        var sourceCamera=tableCamera!=null?tableCamera:Camera.main;
        if(sourceCamera==null)return;
        previewCameraMask=sourceCamera.cullingMask;
        previewCameraMsaa=sourceCamera.allowMSAA;
        var sourceCanvas=GetComponent<Canvas>();
        previewCanvasMode=sourceCanvas.renderMode;
        previewCanvasCamera=sourceCanvas.worldCamera;
        previewCanvasDistance=sourceCanvas.planeDistance;
        if(tableSurface!=null)previewTableLayer=tableSurface.gameObject.layer;
        var backdrop=transform.Find("EnvironmentLayer");
        if(backdrop!=null)previewBackdropActive=backdrop.gameObject.activeSelf;
        InitializeWorld();
        if(CardsRoot!=null)CardsRoot.gameObject.hideFlags=HideFlags.HideInHierarchy|HideFlags.DontSaveInEditor;
        if(hudCamera!=null)hudCamera.gameObject.hideFlags=HideFlags.HideInHierarchy|HideFlags.DontSaveInEditor;
    }

    public void RefreshEditorPreview()
    {
        if(!editorPreview)return;
        Resize();
        foreach(var card in cards)if(card!=null)card.SyncPose();
    }
#endif

    void InitializeWorld()
    {
        hud = GetComponent<Canvas>();
        if (tableCamera == null) tableCamera = Camera.main;
        if (tableCamera == null || cardShader == null) { enabled = false; return; }
        // Keep this scene's mesh-edge AA local; do not edit the project's shared
        // pipeline asset or soften the entire HUD with a post-process blur.
        if(Application.isPlaying && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline){
            previousQualityPipeline=QualitySettings.renderPipeline;
            scenePipeline=Instantiate(pipeline);
            scenePipeline.name=pipeline.name+" (world battlefield)";
            scenePipeline.hideFlags=HideFlags.DontSave;
            scenePipeline.msaaSampleCount=Mathf.Max(4,pipeline.msaaSampleCount);
            QualitySettings.renderPipeline=scenePipeline;
        }
        tableCamera.allowMSAA=true;
        CardsRoot = new GameObject("World cards").transform;
        CardsRoot.SetParent(tableCamera.transform.parent, false);
        hudCamera = new GameObject("HUD and lifted cards", typeof(Camera), typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();
        hudCamera.orthographic = true;
        hudCamera.nearClipPlane = .1f; hudCamera.farClipPlane = 200;
        hudCamera.cullingMask = ~((1 << 26) | (1 << CardLayer));
        hudCamera.clearFlags = CameraClearFlags.Depth;
        var data = hudCamera.GetUniversalAdditionalCameraData();
        data.renderType = CameraRenderType.Overlay;
        tableCamera.GetUniversalAdditionalCameraData().cameraStack.Add(hudCamera);
        tableCamera.cullingMask = (1 << 26) | (1 << CardLayer);
        if(tableSurface!=null)tableSurface.gameObject.layer=26;
        hud.renderMode = RenderMode.ScreenSpaceCamera; hud.worldCamera = hudCamera; hud.planeDistance = 100;
        Paper = new Material(cardShader) { name = "World card paper", renderQueue = 3000 };
        LiftPaper = new Material(Paper) { name = "Lifted card paper", renderQueue = 3000 };
        Shadow = new Material(Paper) { name = "Card contact shadow", renderQueue = 2990 };
        Shadow.SetFloat("_Shadow", 1);
        LiftShadow = new Material(Shadow) { name = "Lifted card shadow", renderQueue = 3000 };
        Quad = new Mesh { name = "World card quad" };
        Quad.vertices = new[] { new Vector3(-.5f,-.5f), new Vector3(-.5f,.5f), new Vector3(.5f,.5f), new Vector3(.5f,-.5f) };
        Quad.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        Quad.triangles = new[] {0,1,2,0,2,3}; Quad.RecalculateNormals(); Quad.RecalculateBounds();
        EdgeMesh=new Mesh{name="Card paper thickness"};
        EdgeMesh.vertices=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(-.5f,.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(.5f,-.5f,.5f)};
        EdgeMesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right,Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
        EdgeMesh.triangles=new[]{0,1,2,0,2,3,4,6,5,4,7,6,0,4,5,0,5,1,3,2,6,3,6,7,1,5,6,1,6,2,0,3,7,0,7,4};
        EdgeMesh.RecalculateNormals();EdgeMesh.RecalculateBounds();
        TappedMesh=ForgeTappedIndicator.CreateWorldMesh();
        Resize();
        // The painted surface is rendered beneath the cards by the table camera.
        var backdrop = transform.Find("EnvironmentLayer");
        if (backdrop != null) sceneBackdrop = backdrop.Find("SanctumBackdrop")?.GetComponent<RawImage>()?.texture;
        tableRenderer = tableSurface != null ? tableSurface.GetComponent<Renderer>() : null;
        RefreshTheme();
        GamePreferences.Changed += RefreshTheme;
        if (backdrop != null) backdrop.gameObject.SetActive(false);
    }

    void RefreshTheme()
    {
        var theme = MenuThemeCatalog.Current;
        if (appliedTheme == theme) return;
        appliedTheme = theme;
        BackdropTexture = theme.Load<Texture2D>(theme.BattlefieldBackdrop);
        if (BackdropTexture == null) BackdropTexture = sceneBackdrop;
        SetView(viewPan, viewZoom);
    }

    public void SetView(Vector2 pan, float zoom)
    {
        viewPan = pan;
        viewZoom = zoom;
        if (tableRenderer == null) return;
        tableProperties ??= new MaterialPropertyBlock();
        tableRenderer.GetPropertyBlock(tableProperties);
        tableProperties.SetTexture("_BackdropTex", BackdropTexture != null ? BackdropTexture : Texture2D.blackTexture);
        tableProperties.SetVector("_ViewParams", new Vector4(zoom, pan.x, pan.y, BackdropTexture != null ? 1 : 0));
        tableRenderer.SetPropertyBlock(tableProperties);
    }

    void Resize()
    {
        viewport = new Vector2Int(Screen.width, Screen.height);
        // Preserve the existing HUD's pixel-space hit/arrow contracts while the
        // independent perspective camera renders the table underneath it.
        hudCamera.transform.SetPositionAndRotation(new Vector3(Screen.width * .5f, Screen.height * .5f, -100), Quaternion.identity);
        hudCamera.orthographicSize = Screen.height * .5f;
        Canvas.ForceUpdateCanvases();
    }
    void LateUpdate()
    {
        if (hudCamera == null) return;
        if (viewport != new Vector2Int(Screen.width, Screen.height)) Resize();
        for (int i = cards.Count - 1; i >= 0; i--) {
            if (cards[i] == null) cards.RemoveAt(i); else cards[i].SyncPose();
        }
    }
    public BattlefieldWorldCard Create(RectTransform anchor, RectTransform pose, ForgeBattlefieldCardPointer pointer)
    {
        var card = cardPrefab != null ? Instantiate(cardPrefab) : new GameObject("World_" + anchor.name).AddComponent<BattlefieldWorldCard>();
        card.transform.SetParent(CardsRoot, false); card.Initialize(this, anchor, pose, pointer);
        cards.Add(card); return card;
    }
    public Vector3 TablePoint(Vector2 screen,float height=0)
    {
        var ray = tableCamera.ScreenPointToRay(screen);
        return new Plane(Vector3.up, Vector3.up*height).Raycast(ray, out float distance) ? ray.GetPoint(distance) : Vector3.zero;
    }
    public override void Raycast(PointerEventData eventData, List<RaycastResult> results)
    {
        if (tableCamera == null || hudCamera == null) return;
        foreach (var card in cards) {
            if (card == null || card.Anchor==null || !card.Anchor.gameObject.activeInHierarchy || !card.Interactive) continue;
            if (!card.AcceptPoint(eventData.position)) continue;
            var camera = card.UsesHudCamera ? hudCamera : tableCamera;
            var ray=camera.ScreenPointToRay(eventData.position);
            bool faceHit=card.Hit(ray, out var hit);
            // Keep the resting hand slot interactive while its mesh lifts away.
            // AcceptPoint already resolves fan ownership and viewport clipping.
            bool anchorHit=card.UsesHudCamera&&RectTransformUtility.RectangleContainsScreenPoint(card.Anchor,eventData.position,null);
            if(!faceHit&&!anchorHit)continue;
            results.Add(new RaycastResult {
                gameObject = card.Anchor.gameObject, module = this, distance = faceHit?hit.distance:Mathf.Abs(card.transform.position.z-ray.origin.z),
                worldPosition = faceHit?hit.point:ray.GetPoint(Mathf.Abs(card.transform.position.z-ray.origin.z)),
                worldNormal = faceHit?hit.normal:-ray.direction, screenPosition = eventData.position,
                index = results.Count, depth = card.Lifted ? 10000 : 1000,
                sortingOrder = card.InputOrder
            });
        }
    }
    static void ReleaseWorldObject(UnityEngine.Object value)
    {
        if(value==null)return;
#if UNITY_EDITOR
        if(!Application.isPlaying){DestroyImmediate(value);return;}
#endif
        Destroy(value);
    }

#if UNITY_EDITOR
    public void DisposeEditorPreview()
    {
        if(!editorPreview)return;
        ReleaseWorld();
        if(tableCamera!=null){tableCamera.cullingMask=previewCameraMask;tableCamera.allowMSAA=previewCameraMsaa;}
        if(hud!=null){hud.renderMode=previewCanvasMode;hud.worldCamera=previewCanvasCamera;hud.planeDistance=previewCanvasDistance;}
        if(tableSurface!=null)tableSurface.gameObject.layer=previewTableLayer;
        var backdrop=transform.Find("EnvironmentLayer");
        if(backdrop!=null)backdrop.gameObject.SetActive(previewBackdropActive);
        if(tableRenderer!=null)tableRenderer.SetPropertyBlock(null);
        editorPreview=false;
    }
#endif

    void ReleaseWorld()
    {
        GamePreferences.Changed -= RefreshTheme;
        appliedTheme = null;
        if (tableCamera != null && hudCamera != null) tableCamera.GetUniversalAdditionalCameraData().cameraStack.Remove(hudCamera);
        if (CardsRoot != null) ReleaseWorldObject(CardsRoot.gameObject);
        if (hudCamera != null) ReleaseWorldObject(hudCamera.gameObject);
        foreach (var material in new[] {Paper, LiftPaper, Shadow, LiftShadow}) ReleaseWorldObject(material);
        ReleaseWorldObject(Quad);
        ReleaseWorldObject(EdgeMesh);
        ReleaseWorldObject(TappedMesh);
        ReleaseWorldObject(roomLockMesh);
        if(scenePipeline!=null){
            if(QualitySettings.renderPipeline==scenePipeline)QualitySettings.renderPipeline=previousQualityPipeline;
            ReleaseWorldObject(scenePipeline);
        }
        CardsRoot=null;hudCamera=null;Paper=LiftPaper=Shadow=LiftShadow=null;
        Quad=EdgeMesh=TappedMesh=roomLockMesh=null;scenePipeline=null;
        cards.Clear();
    }

    protected override void OnDestroy()
    {
#if UNITY_EDITOR
        if(editorPreview)DisposeEditorPreview();else ReleaseWorld();
#else
        ReleaseWorld();
#endif
        base.OnDestroy();
    }
}
