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
    private CardArtDownloadService downloader;
    private CancellationTokenSource cancellation;
    private readonly List<Texture2D> thumbnails = new List<Texture2D>();
    private UnityWebRequest thumbnailRequest;
    private readonly List<Tuple<CardDefinition, Outline>> choices = new List<Tuple<CardDefinition, Outline>>();
    private static readonly Color Ink = MenuVisualTheme.Panel;
    private static readonly Color Paper = MenuVisualTheme.Paper;
    private bool embedded;

    public static CardArtPanel CreateEmbedded(Transform parent)
    {
        var go = new GameObject("CardArtEmbedded", typeof(RectTransform), typeof(Image), typeof(CardArtPanel));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = Ink;
        var panel = go.GetComponent<CardArtPanel>();
        panel.embedded = true;
        panel.Build();
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
        go.GetComponent<Image>().color = Ink;
        var panel = go.GetComponent<CardArtPanel>();
        panel.Build();
        go.SetActive(false);
        return panel;
    }

    private void Build()
    {
        title = Label(transform,"Title",24); Top(title.rectTransform,20,18,430,36);
        var close = Button(transform,"关闭",Close); Top((RectTransform)close.transform,-88,18,72,36,true);
        var hint = Label(transform,"Hint",16); hint.text="选择异画 · 偏好仅影响本机显示"; Top(hint.rectTransform,20,60,600,26);
        grid = new GameObject("Versions",typeof(RectTransform)).GetComponent<RectTransform>(); grid.SetParent(transform,false);
        grid.anchorMin=new Vector2(0,.23f); grid.anchorMax=new Vector2(1,.86f); grid.offsetMin=new Vector2(16,0);grid.offsetMax=new Vector2(-16,0);
        previous=Button(transform,"上一页",()=> {page--; RenderPage();}); Bottom((RectTransform)previous.transform,20,144,100,34);
        next=Button(transform,"下一页",()=> {page++; RenderPage();}); Bottom((RectTransform)next.transform,-120,144,100,34,true);
        pageLabel=Label(transform,"Page",17); Bottom(pageLabel.rectTransform,140,144,350,34);
        status=Label(transform,"Status",17); status.horizontalOverflow=HorizontalWrapMode.Wrap;
        var sr=status.rectTransform; sr.anchorMin=new Vector2(0,0);sr.anchorMax=new Vector2(1,0);sr.pivot=new Vector2(0,0);sr.offsetMin=new Vector2(20,64);sr.offsetMax=new Vector2(-20,134);
        clear=Button(transform,"取消偏好 · 恢复默认",()=> {CardArtPreferences.Clear(card);changed?.Invoke();RenderPage();}); Bottom((RectTransform)clear.transform,20,18,226,38);
        apply=Button(transform,"设为偏好",DownloadAndApply); Bottom((RectTransform)apply.transform,-252,18,232,38,true);

        if (embedded)
        {
            close.gameObject.SetActive(false);
            Top(title.rectTransform,12,10,360,46);
            PreferenceText.SetSize(title,19);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 13;
            title.resizeTextMaxSize = 19;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            Top(hint.rectTransform,12,62,360,44);
            PreferenceText.SetSize(hint,14);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            grid.anchorMin = Vector2.zero; grid.anchorMax = Vector2.one;
            grid.offsetMin = new Vector2(8,184); grid.offsetMax = new Vector2(-8,-112);
            var clearRect = (RectTransform)clear.transform;
            clearRect.anchorMin = new Vector2(0,0); clearRect.anchorMax = new Vector2(.5f,0);
            clearRect.offsetMin = new Vector2(12,12); clearRect.offsetMax = new Vector2(-6,56);
            var applyRect = (RectTransform)apply.transform;
            applyRect.anchorMin = new Vector2(.5f,0); applyRect.anchorMax = new Vector2(1,0);
            applyRect.offsetMin = new Vector2(6,12); applyRect.offsetMax = new Vector2(-12,56);
            foreach (var button in new[] { clear, apply })
            {
                var label = button.GetComponentInChildren<Text>();
                PreferenceText.SetSize(label,16);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 12;
                label.resizeTextMaxSize = 16;
            }
            Bottom((RectTransform)previous.transform,10,140,88,34);
            Bottom((RectTransform)next.transform,-98,140,88,34,true);
            pageLabel.rectTransform.anchorMin = new Vector2(0,0);
            pageLabel.rectTransform.anchorMax = new Vector2(1,0);
            pageLabel.rectTransform.offsetMin = new Vector2(104,140);
            pageLabel.rectTransform.offsetMax = new Vector2(-104,178);
            pageLabel.alignment = TextAnchor.MiddleCenter;
            PreferenceText.SetSize(pageLabel,14);
            pageLabel.resizeTextForBestFit = true;
            pageLabel.resizeTextMinSize = 12;
            pageLabel.resizeTextMaxSize = 14;
            status.alignment = TextAnchor.MiddleCenter;
            status.resizeTextForBestFit = true;
            status.resizeTextMinSize = 13;
            status.resizeTextMaxSize = 17;
        }
        ApplyTextPreferences();
    }

    public async void Show(CardDefinition source, Action onChanged)
    {
        Close(); gameObject.SetActive(true); transform.SetAsLastSibling();
        card=source;changed=onChanged;selected=null;page=0;
        cancellation=new CancellationTokenSource();int request=++generation;
        title.text=GamePreferences.CardName(source);status.text="正在读取异画列表…";apply.interactable=clear.interactable=false;
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
        int visibleCount = Math.Min(6, versions.Count - page * 6);
        if (embedded)
        {
            int rows = Math.Max(1, (visibleCount + 1) / 2);
            var hostLayout = transform.parent.GetComponent<LayoutElement>();
            if (hostLayout != null) hostLayout.preferredHeight = 284 + rows * 335;
        }
        previous.interactable=!busy && page>0;next.interactable=!busy && page+1<pages;
        previous.gameObject.SetActive(page>0);next.gameObject.SetActive(page+1<pages);
        pageLabel.text=$"{page+1} / {pages} 页 · {versions.Count} 个版本";
        var jobs=new List<Tuple<CardDefinition,RawImage>>();
        for(int i=0;i<6 && page*6+i<versions.Count;i++)
        {
            var version=versions[page*6+i];
            var button=Button(grid,"",()=> {selected=version;UpdateSelection();});
            var rect=(RectTransform)button.transform;int columns=embedded?(visibleCount==1?1:2):3,rows=embedded?Math.Max(1,(visibleCount+1)/2):2;
            int col=i%columns,row=i/columns;
            rect.anchorMin=new Vector2(col/(float)columns,1-(row+1)/(float)rows);rect.anchorMax=new Vector2((col+1)/(float)columns,1-row/(float)rows);rect.offsetMin=new Vector2(5,5);rect.offsetMax=new Vector2(-5,-5);
            var outline=button.gameObject.AddComponent<Outline>();outline.effectDistance=new Vector2(2,2);choices.Add(Tuple.Create(version,outline));
            var container=new GameObject("ImageArea",typeof(RectTransform)).GetComponent<RectTransform>();container.SetParent(button.transform,false);
            container.anchorMin=new Vector2(.02f,.15f);container.anchorMax=new Vector2(.98f,.99f);container.offsetMin=container.offsetMax=Vector2.zero;
            var image=new GameObject("Thumbnail",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();image.transform.SetParent(container,false);image.raycastTarget=false;
            image.rectTransform.anchorMin=Vector2.zero;image.rectTransform.anchorMax=Vector2.one;image.rectTransform.offsetMin=image.rectTransform.offsetMax=Vector2.zero;
            var fit=image.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=488f/680f;
            var label=button.GetComponentInChildren<Text>();PreferenceText.SetSize(label,embedded?14:15);label.alignment=TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=new Vector2(1,.15f);label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;
            bool installed=CardImageLibrary.TryLoadExact(version,0,out var sprite);
            label.text=version.PrintingKey+"\n"+(CardArtPreferences.Get(card)==version.PrintingKey?"当前偏好":installed?"已下载":downloader.CanDownload(version)?"未下载":"暂无图片来源");
            if(installed) image.texture=sprite.texture;
            else {image.color=new Color(.3f,.34f,.35f);jobs.Add(Tuple.Create(version,image));}
        }
        UpdateSelection();
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
        foreach(var choice in choices) {choice.Item2.effectColor=choice.Item1==selected?MenuVisualTheme.Accent:MenuVisualTheme.Line;choice.Item2.GetComponent<Button>().interactable=!busy;}
        clear.interactable=!busy && !string.IsNullOrEmpty(CardArtPreferences.Get(card));
        bool available=selected!=null&&downloader.CanDownload(selected);
        apply.interactable=!busy && selected!=null&&available;
        bool installed=selected!=null && CardImageLibrary.TryLoadExact(selected,0,out _);
        apply.GetComponentInChildren<Text>().text=installed?"使用此版本 · 设为偏好":selected!=null&&!available?"暂无图片来源":"下载并设为偏好";
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
    private void RefreshLanguage() {if(title!=null && card!=null) title.text=GamePreferences.CardName(card);}
    private void OnEnable() {GamePreferences.Changed+=RefreshLanguage;RefreshLanguage();}
    private void OnDisable() {GamePreferences.Changed-=RefreshLanguage;generation++;cancellation?.Cancel();StopThumbnails();}
    private void OnDestroy() {cancellation?.Cancel();cancellation?.Dispose();}

    private static Text Label(Transform parent,string name,int size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=size;text.color=Paper;text.raycastTarget=false;text.supportRichText=false;PreferenceText.SetSize(text,size);return text;
    }
    private static Button Button(Transform parent,string label,Action click)
    {
        var go=new GameObject("Button_"+label,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.color=MenuVisualTheme.Raised;var button=go.GetComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>click());var text=Label(go.transform,"Label",18);text.text=label;text.alignment=TextAnchor.MiddleCenter;text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(4,2);text.rectTransform.offsetMax=new Vector2(-4,-2);return button;
    }
    private static void Top(RectTransform r,float x,float y,float w,float h,bool right=false) {r.anchorMin=r.anchorMax=new Vector2(right?1:0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    private static void Bottom(RectTransform r,float x,float y,float w,float h,bool right=false) {r.anchorMin=r.anchorMax=new Vector2(right?1:0,0);r.pivot=Vector2.zero;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
}
