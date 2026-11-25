using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public sealed class CardArtPanel : MonoBehaviour
{
    private CardDefinition card, selected;
    private IReadOnlyList<CardDefinition> versions;
    private Action changed;
    private RectTransform grid;
    private Text title, status, pageLabel;
    private Button apply, clear, previous, next;
    private int page, generation;
    private bool busy;
    private bool selectedNeedsDownload;
    private CardArtDownloadService downloader;
    private CancellationTokenSource cancellation;
    private readonly List<Texture2D> thumbnails = new List<Texture2D>();
    private UnityWebRequest thumbnailRequest;
    private readonly List<Tuple<CardDefinition, Outline>> choices = new List<Tuple<CardDefinition, Outline>>();
    private bool embedded;
    private GridLayoutGroup versionLayout;
    private ScrollRect galleryScroll;
    private Text galleryHint;
    private const float VersionAspect=488f/680f;

    public static CardArtPanel CreateEmbedded(Transform parent,Scrollbar scrollbar=null)
    {
        var go = new GameObject("CardArtEmbedded", typeof(RectTransform), typeof(Image), typeof(CardArtPanel));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var panel = go.GetComponent<CardArtPanel>();
        panel.embedded = true;
        rect.anchorMin=Vector2.up;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);
        rect.anchoredPosition=Vector2.zero;
        panel.Build(scrollbar);
        go.SetActive(false);
        return panel;
    }

    public static CardArtPanel Create(Transform parent)
    {
        var go = new GameObject("CardArtDrawer", typeof(RectTransform), typeof(Image), typeof(CardArtPanel));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(.60f,.005f); rect.anchorMax = new Vector2(.997f,.92f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var panel = go.GetComponent<CardArtPanel>();
        panel.Build();
        go.SetActive(false);
        return panel;
    }

    private void Build(Scrollbar scrollbar=null)
    {
        MenuTypography.Scope(transform);
        GetComponent<Image>().color = MenuVisualTheme.ArchivePanel;
        title = Label(transform,"Title",24); Top(title.rectTransform,20,18,430,36);
        MenuTypography.Use(title,MenuTypography.Face.Heading);
        var close = Button(transform,"关闭",Close); Top((RectTransform)close.transform,-88,18,72,36,true);
        var hint = Label(transform,"Hint",16); hint.text="选择异画 · 偏好仅影响本机显示"; Top(hint.rectTransform,20,60,600,26);
        grid = new GameObject("Versions",typeof(RectTransform)).GetComponent<RectTransform>(); grid.SetParent(transform,false);
        grid.anchorMin=new Vector2(0,.23f); grid.anchorMax=new Vector2(1,.86f); grid.offsetMin=new Vector2(16,0);grid.offsetMax=new Vector2(-16,0);
        previous=Button(transform,"上一页",()=> {page--; RenderPage();}); Bottom((RectTransform)previous.transform,20,144,100,34);
        next=Button(transform,"下一页",()=> {page++; RenderPage();}); Bottom((RectTransform)next.transform,-120,144,100,34,true);
        pageLabel=Label(transform,"Page",17); Bottom(pageLabel.rectTransform,140,144,350,34);
        status=Label(transform,"Status",17); status.horizontalOverflow=HorizontalWrapMode.Wrap;
        var sr=status.rectTransform; sr.anchorMin=new Vector2(0,0);sr.anchorMax=new Vector2(1,0);sr.pivot=new Vector2(0,0);sr.offsetMin=new Vector2(20,64);sr.offsetMax=new Vector2(-20,134);
        clear=Button(transform,"恢复默认",()=> {CardArtPreferences.Clear(card);changed?.Invoke();RenderPage();}); Bottom((RectTransform)clear.transform,20,18,226,38);
        apply=Button(transform,"设为偏好",DownloadAndApply); Bottom((RectTransform)apply.transform,-252,18,232,38,true);
        foreach (var action in new[] { close, previous, next, clear, apply })
        {
            MenuVisualTheme.StyleDeckTool(action,action==apply);
            DisabledButtonVisual.Bind(action);
        }
        hint.color=pageLabel.color=MenuVisualTheme.Stone;
        status.color=MenuVisualTheme.Silver;
        clear.GetComponentInChildren<Text>().GetComponent<PreferenceText>().Literal=true;
        apply.GetComponentInChildren<Text>().GetComponent<PreferenceText>().Literal=true;

        if (embedded)
        {
            close.gameObject.SetActive(false);
            HeaderRow(title.rectTransform,8,36);
            PreferenceText.SetSize(title,20);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 13;
            title.resizeTextMaxSize = title.fontSize;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            galleryHint=hint;
            HeaderRow(hint.rectTransform,45,29);
            PreferenceText.SetSize(hint,14);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            BuildGalleryList(scrollbar);
            var clearRect = (RectTransform)clear.transform;
            clearRect.anchorMin = new Vector2(0,0); clearRect.anchorMax = new Vector2(.5f,0);
            clearRect.offsetMin = new Vector2(12,12); clearRect.offsetMax = new Vector2(-6,56);
            var applyRect = (RectTransform)apply.transform;
            applyRect.anchorMin = new Vector2(.5f,0); applyRect.anchorMax = new Vector2(1,0);
            applyRect.offsetMin = new Vector2(6,12); applyRect.offsetMax = new Vector2(-12,56);
            foreach (var button in new[] { clear, apply, previous, next })
            {
                var label = button.GetComponentInChildren<Text>();
                PreferenceText.SetSize(label,16);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 12;
                label.resizeTextMaxSize = label.fontSize;
            }
            Bottom((RectTransform)previous.transform,12,104,76,34);
            Bottom((RectTransform)next.transform,-88,104,76,34,true);
            pageLabel.rectTransform.anchorMin = new Vector2(0,0);
            pageLabel.rectTransform.anchorMax = new Vector2(1,0);
            pageLabel.rectTransform.offsetMin = new Vector2(92,104);
            pageLabel.rectTransform.offsetMax = new Vector2(-92,138);
            pageLabel.alignment = TextAnchor.MiddleCenter;
            PreferenceText.SetSize(pageLabel,14);
            pageLabel.resizeTextForBestFit = true;
            pageLabel.resizeTextMinSize = 12;
            pageLabel.resizeTextMaxSize = 14;
            status.alignment = TextAnchor.MiddleCenter;
            status.resizeTextForBestFit = true;
            status.resizeTextMinSize = 13;
            status.resizeTextMaxSize = 17;
            sr.offsetMin=new Vector2(12,58);sr.offsetMax=new Vector2(-12,101);
        }
        ApplyTextPreferences();
    }

    private void BuildGalleryList(Scrollbar scrollbar)
    {
        var list=new GameObject("GalleryList",typeof(RectTransform),typeof(ScrollRect));
        list.transform.SetParent(transform,false);
        var bounds=(RectTransform)list.transform;
        bounds.anchorMin=Vector2.zero;bounds.anchorMax=Vector2.one;
        bounds.offsetMin=new Vector2(8,105);bounds.offsetMax=new Vector2(-4,-81);
        var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D));
        viewport.transform.SetParent(bounds,false);
        viewport.GetComponent<Image>().color=Color.clear;
        var view=(RectTransform)viewport.transform;
        view.anchorMin=Vector2.zero;view.anchorMax=Vector2.one;
        view.offsetMin=Vector2.zero;view.offsetMax=new Vector2(scrollbar!=null?-14:-3,0);
        grid.SetParent(view,false);
        grid.anchorMin=Vector2.up;grid.anchorMax=Vector2.one;grid.pivot=Vector2.up;
        grid.offsetMin=grid.offsetMax=Vector2.zero;
        versionLayout=grid.gameObject.AddComponent<GridLayoutGroup>();
        versionLayout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;
        versionLayout.constraintCount=2;
        versionLayout.padding=new RectOffset(2,2,2,2);
        versionLayout.spacing=new Vector2(8,10);
        galleryScroll=list.GetComponent<ScrollRect>();
        galleryScroll.horizontal=false;galleryScroll.vertical=true;
        galleryScroll.viewport=view;galleryScroll.content=grid;
        galleryScroll.movementType=ScrollRect.MovementType.Clamped;galleryScroll.scrollSensitivity=60;
        if(scrollbar!=null)
        {
            var bar=Instantiate(scrollbar,bounds,false);
            bar.gameObject.SetActive(true);
            var track=(RectTransform)bar.transform;
            track.anchorMin=Vector2.right;track.anchorMax=Vector2.one;
            track.offsetMin=new Vector2(-12,0);track.offsetMax=Vector2.zero;
            bar.onValueChanged=new Scrollbar.ScrollEvent();
            galleryScroll.verticalScrollbar=bar;
            galleryScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        }
    }

    private float GalleryCaptionHeight => Mathf.Ceil(36*GamePreferences.TextScale)+4;

    private void ResizeGallery()
    {
        if(galleryScroll==null)return;
        float width=Mathf.Max(1,(galleryScroll.viewport.rect.width-versionLayout.padding.horizontal-versionLayout.spacing.x)/2);
        float height=Mathf.Ceil(Mathf.Max(1,width-8)/VersionAspect)+8+GalleryCaptionHeight;
        versionLayout.cellSize=new Vector2(width,height);
        int rows=(choices.Count+1)/2;
        float total=versionLayout.padding.vertical+rows*height+Mathf.Max(0,rows-1)*versionLayout.spacing.y;
        if(Mathf.Abs(grid.rect.height-total)>.1f)grid.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,total);
        var panel=(RectTransform)transform;
        var list=(RectTransform)galleryScroll.transform;
        float available=((RectTransform)panel.parent).rect.height;
        float preferred=Mathf.Min(available,total+list.offsetMin.y-list.offsetMax.y+4);
        if(Mathf.Abs(panel.rect.height-preferred)>.1f)panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,preferred);
    }

    private void OnRectTransformDimensionsChange()=>ResizeGallery();

    public async void Show(CardDefinition source, Action onChanged)
    {
        if(card==source && isActiveAndEnabled && cancellation!=null && !cancellation.IsCancellationRequested)return;
        Close(); gameObject.SetActive(true); transform.SetAsLastSibling();
        card=source;changed=onChanged;selected=null;page=0;
        selectedNeedsDownload=false;
        RefreshActionCaptions();
        cancellation=new CancellationTokenSource();int request=++generation;
        title.text=GamePreferences.CardName(source);status.text="正在读取异画列表…";apply.interactable=clear.interactable=false;
        if(embedded)
        {
            versions=null;choices.Clear();grid.gameObject.SetActive(false);
            previous.gameObject.SetActive(false);next.gameObject.SetActive(false);pageLabel.gameObject.SetActive(false);
            galleryHint.text=string.Empty;
            ResizeGallery();
        }
        try
        {
            await CardArtSources.LoadAsync();
            if(this==null || request!=generation || !gameObject.activeSelf) return;
            downloader=CardArtDownloadService.FromConfig();
            versions=CardArtVersions.GetVersions(card);
            RenderPage();
        }
        catch(Exception error) { if(this!=null && request==generation) status.text="加载失败："+error.Message+"\n关闭后可重新打开重试。"; }
    }

    private void RenderPage()
    {
        StopThumbnails();
        foreach(Transform child in grid) {child.gameObject.SetActive(false);Destroy(child.gameObject);}
        choices.Clear();
        int pages=Math.Max(1,(versions.Count+5)/6);page=Mathf.Clamp(page,0,pages-1);
        previous.interactable=!busy && page>0;next.interactable=!busy && page+1<pages;
        previous.gameObject.SetActive(page>0);next.gameObject.SetActive(page+1<pages);
        if(embedded)
        {
            grid.gameObject.SetActive(true);
            var bounds=(RectTransform)galleryScroll.transform;
            bounds.offsetMin=new Vector2(8,pages>1?147:105);
            pageLabel.gameObject.SetActive(pages>1);
            galleryHint.text=string.Format(DeckCompanion.Text("{0} 个版本 · 仅本机生效","{0} printings · Local preference","{0} 件の版 · この端末のみ"),versions.Count);
        }
        pageLabel.text=embedded?$"{page+1} / {pages}":$"{page+1} / {pages} 页 · {versions.Count} 个版本";
        var jobs=new List<Tuple<CardDefinition,RawImage>>();
        for(int i=0;i<6 && page*6+i<versions.Count;i++)
        {
            var version=versions[page*6+i];
            var button=Button(grid,"",()=> {selected=version;UpdateSelection();});
            if(!embedded)
            {
                var rect=(RectTransform)button.transform;int col=i%3,row=i/3;
                rect.anchorMin=new Vector2(col/3f,1-(row+1)/2f);rect.anchorMax=new Vector2((col+1)/3f,1-row/2f);rect.offsetMin=new Vector2(5,5);rect.offsetMax=new Vector2(-5,-5);
            }
            var outline=button.gameObject.AddComponent<Outline>();outline.effectDistance=new Vector2(2,2);choices.Add(Tuple.Create(version,outline));
            var container=new GameObject("ImageArea",typeof(RectTransform)).GetComponent<RectTransform>();container.SetParent(button.transform,false);
            container.anchorMin=new Vector2(.02f,.15f);container.anchorMax=new Vector2(.98f,.99f);container.offsetMin=container.offsetMax=Vector2.zero;
            var image=new GameObject("Thumbnail",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();image.transform.SetParent(container,false);image.raycastTarget=false;
            image.rectTransform.anchorMin=Vector2.zero;image.rectTransform.anchorMax=Vector2.one;image.rectTransform.offsetMin=image.rectTransform.offsetMax=Vector2.zero;
            var fit=image.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=VersionAspect;
            var label=button.GetComponentInChildren<Text>();PreferenceText.SetSize(label,embedded?14:15);label.alignment=TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=new Vector2(1,.15f);label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;
            bool installed=CardImageLibrary.TryLoadExact(version,0,out var sprite);
            string availability=CardArtPreferences.Get(card)==version.PrintingKey?"当前偏好":installed?"已下载":downloader.CanDownload(version)?"未下载":"暂无图片来源";
            if(embedded)
            {
                button.name="Version_"+version.PrintingKey.Replace('/','_');
                container.anchorMin=Vector2.zero;container.anchorMax=Vector2.one;
                container.offsetMin=new Vector2(4,GalleryCaptionHeight+4);container.offsetMax=new Vector2(-4,-4);
                label.rectTransform.anchorMax=Vector2.right;
                float middle=Mathf.Ceil(GalleryCaptionHeight*.47f);
                label.rectTransform.offsetMin=new Vector2(7,middle);label.rectTransform.offsetMax=new Vector2(-5,GalleryCaptionHeight+2);
                label.text=version.PrintingKey;
                label.alignment=TextAnchor.MiddleLeft;
                MenuTypography.Use(label,MenuTypography.Face.Control);
                label.resizeTextForBestFit=true;label.resizeTextMinSize=12;label.resizeTextMaxSize=label.fontSize;
                var state=Label(button.transform,"Availability",13);
                state.text=availability;state.color=MenuVisualTheme.Stone;state.alignment=TextAnchor.MiddleLeft;
                state.rectTransform.anchorMin=Vector2.zero;state.rectTransform.anchorMax=Vector2.right;
                state.rectTransform.offsetMin=new Vector2(7,2);state.rectTransform.offsetMax=new Vector2(-5,middle);
                state.resizeTextForBestFit=true;state.resizeTextMinSize=10;state.resizeTextMaxSize=state.fontSize;
            }
            else label.text=version.PrintingKey+"\n"+availability;
            if(installed) image.texture=sprite.texture;
            else {image.color=new Color(.3f,.34f,.35f);jobs.Add(Tuple.Create(version,image));}
        }
        UpdateSelection();
        if(embedded)
        {
            ResizeGallery();
            galleryScroll.StopMovement();
            galleryScroll.verticalNormalizedPosition=1;
        }
        // Bind newly created labels before their first frame, after assigning final base sizes.
        // Waiting for the global preference scan produces a visible 14 -> 18 point jump.
        ApplyTextPreferences();
        StartCoroutine(LoadThumbnails(jobs));
    }

    private void ApplyTextPreferences()
    {
        foreach(var text in GetComponentsInChildren<Text>(true))
        {
            var preference=text.GetComponent<PreferenceText>();
            if(preference==null)
            {
                float baseSize=text.fontSize;
                preference=text.gameObject.AddComponent<PreferenceText>();
                preference.SetBaseSize(baseSize);
            }
            else preference.Apply();
        }
    }

    private IEnumerator LoadThumbnails(List<Tuple<CardDefinition,RawImage>> jobs)
    {
        foreach(var job in jobs)
        {
            Uri url;
            try {url=CardArtSources.Url(job.Item1,0,"normal");} catch {continue;}
            string cached=PrintingKeyUtility.BuildImagePath(Path.Combine(Application.persistentDataPath,"CardArtPreviews"),job.Item1.setCode,job.Item1.CollectorNumber,0,".jpg");
            bool fromCache=File.Exists(cached);
            using(var request=UnityWebRequestTexture.GetTexture(fromCache?new Uri(cached).AbsoluteUri:url.AbsoluteUri,true))
            {
                if(!fromCache) {request.SetRequestHeader("User-Agent","MTGB-CardArt/1.0");request.SetRequestHeader("Accept","image/jpeg");}
                thumbnailRequest=request;request.timeout=15;request.redirectLimit=0;
                yield return request.SendWebRequest();
                thumbnailRequest=null;
                if(request.result!=UnityWebRequest.Result.Success || job.Item2==null)
                {
                    if(fromCache) {try {File.Delete(cached);} catch(IOException) {}}
                    continue;
                }
                var texture=DownloadHandlerTexture.GetContent(request);thumbnails.Add(texture);job.Item2.texture=texture;job.Item2.color=Color.white;
                if(!fromCache)
                {
                    try {Directory.CreateDirectory(Path.GetDirectoryName(cached));File.WriteAllBytes(cached,request.downloadHandler.data);}
                    catch(Exception error) when(error is IOException || error is UnauthorizedAccessException) {Debug.LogWarning("Thumbnail cache: "+error.Message);}
                }
            }
        }
    }

    private void UpdateSelection()
    {
        foreach(var choice in choices)
        {
            bool highlighted=choice.Item1==selected;
            choice.Item2.effectColor=highlighted?MenuVisualTheme.SideAccent:MenuVisualTheme.Etching;
            choice.Item2.effectDistance=highlighted?new Vector2(3,-3):new Vector2(1,-1);
            choice.Item2.GetComponent<Button>().interactable=!busy;
        }
        clear.interactable=!busy && !string.IsNullOrEmpty(CardArtPreferences.Get(card));
        bool available=selected!=null&&downloader.CanDownload(selected);
        apply.interactable=!busy && selected!=null&&available;
        bool installed=selected!=null && CardImageLibrary.TryLoadExact(selected,0,out _);
        selectedNeedsDownload=available && !installed;
        RefreshActionCaptions();
        if(!busy) status.text=selected==null?"当前偏好："+(string.IsNullOrEmpty(CardArtPreferences.Get(card))?GamePreferences.T("默认卡图"):CardArtPreferences.Get(card)):
            "已选："+selected.PrintingKey+(installed?" · 已下载":available?" · 点击下方按钮下载并使用":" · 暂无图片来源，请选择其他版本");
    }

    private async void DownloadAndApply()
    {
        if(busy || selected==null || !downloader.CanDownload(selected)) return;
        var version=selected;int request=generation;var token=cancellation.Token;
        busy=true;UpdateSelection();previous.interactable=next.interactable=false;
        try
        {
            status.text="正在下载 "+version.PrintingKey+"…";
            if(!downloader.IsDownloaded(version))
                await downloader.DownloadAsync(version,new Progress<CardArtDownloadService.Progress>(p=> {if(this!=null && request==generation) status.text=$"正在下载 {p.PrintingKey} · {p.Fraction:P0}";}),token);
            token.ThrowIfCancellationRequested();
            if(this==null || request!=generation) return;
            CardArtPreferences.Set(card,version);changed?.Invoke();busy=false;RenderPage();
            status.text="正在使用："+version.PrintingKey+" · 可取消偏好恢复默认";
        }
        catch(OperationCanceledException) { }
        catch(Exception error) {if(this!=null && request==generation) {busy=false;UpdateSelection();status.text="下载失败，可重试："+error.Message;}}
        finally {if(this!=null && request==generation) {busy=false;previous.interactable=page>0;next.interactable=versions!=null && (page+1)*6<versions.Count;}}
    }

    private void StopThumbnails() {if(thumbnailRequest!=null) {thumbnailRequest.Abort();thumbnailRequest.Dispose();thumbnailRequest=null;}StopAllCoroutines();foreach(var texture in thumbnails) if(texture!=null) Destroy(texture);thumbnails.Clear();}
    public void Close() {generation++;cancellation?.Cancel();cancellation?.Dispose();cancellation=null;busy=false;StopThumbnails();gameObject.SetActive(false);}
    private void RefreshActionCaptions()
    {
        clear.GetComponentInChildren<Text>(true).text=DeckCompanion.Text("恢复默认","Reset","既定に戻す");
        apply.GetComponentInChildren<Text>(true).text=selectedNeedsDownload
            ? DeckCompanion.Text("下载并使用","Download & use","取得して使う")
            : DeckCompanion.Text("设为偏好","Use artwork","優先に設定");
    }
    private void RefreshLanguage()
    {
        if(title!=null && card!=null) title.text=GamePreferences.CardName(card);
        if(apply!=null) RefreshActionCaptions();
        if(embedded && versions!=null && !busy && cancellation?.IsCancellationRequested==false)RenderPage();
    }
    private void OnEnable() {GamePreferences.Changed+=RefreshLanguage;RefreshLanguage();}
    private void OnDisable() {GamePreferences.Changed-=RefreshLanguage;generation++;cancellation?.Cancel();StopThumbnails();}
    private void OnDestroy() {cancellation?.Cancel();cancellation?.Dispose();}

    private static Text Label(Transform parent,string name,int size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=size;text.color=MenuVisualTheme.ArchivePaper;text.raycastTarget=false;text.supportRichText=false;PreferenceText.SetSize(text,size);return text;
    }
    private static Button Button(Transform parent,string label,Action click)
    {
        var go=new GameObject("Button_"+label,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.color=MenuVisualTheme.Recess;var button=go.GetComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>click());var text=Label(go.transform,"Label",18);text.text=label;text.alignment=TextAnchor.MiddleCenter;text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(4,2);text.rectTransform.offsetMax=new Vector2(-4,-2);return button;
    }
    private static void HeaderRow(RectTransform rect,float top,float height)
    {
        rect.anchorMin=Vector2.up;rect.anchorMax=Vector2.one;
        rect.offsetMin=new Vector2(12,-top-height);rect.offsetMax=new Vector2(-12,-top);
    }
    private static void Top(RectTransform r,float x,float y,float w,float h,bool right=false) {r.anchorMin=r.anchorMax=new Vector2(right?1:0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    private static void Bottom(RectTransform r,float x,float y,float w,float h,bool right=false) {r.anchorMin=r.anchorMax=new Vector2(right?1:0,0);r.pivot=Vector2.zero;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
}
