using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class ReplayBrowser : MonoBehaviour
{
    private static ReplayBrowser current;
    public static bool ReopenOnReturn;
    public static string ReturnMessage;
    private RectTransform rows;
    private Text details, status, pagination;
    private Button play, export, delete, previousPage, nextPage;
    private RectTransform deleteDialog;
    private string[] files = Array.Empty<string>();
    private string selectedPath;
    private ReplayPreview selected;
    private bool opening;
    private CancellationTokenSource previewRead;
    private int page, selectionRevision;
    private const int PageSize = 8;
    private static readonly Color Surface = new Color(.075f, .105f, .15f);
    private static readonly Color Border = new Color(.22f, .30f, .41f);
    private static readonly Color Gold = new Color(.87f, .70f, .40f);
    private static readonly Color WarmText = new Color(.91f, .95f, 1f);
    public static void Show()
    {
        if (current != null) return;
        var canvas = ReplayWidgets.Canvas("ReplayBrowser", 420);
        current = canvas.gameObject.AddComponent<ReplayBrowser>(); current.Build();
    }
    private void Build()
    {
        var background = ReplayWidgets.Panel(transform, "Background", 0, 0, 1920, 1080, new Color(.035f,.055f,.085f));
        background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one; background.offsetMin = background.offsetMax = Vector2.zero;
        var art = ReplayWidgets.Rect(background, "ArchiveArtwork", 0, 0, 1920, 1080);
        art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one; art.offsetMin = art.offsetMax = Vector2.zero;
        var image = art.gameObject.AddComponent<RawImage>();
        image.texture = Resources.Load<Texture2D>("Menu/ArchiveSanctum");
        image.color = new Color(.40f,.57f,.80f,.19f); image.raycastTarget = false;
        var fit = art.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = image.texture != null ? (float)image.texture.width / image.texture.height : 16f / 9;
        ReplayWidgets.Panel(transform, "HeaderRule", 0, 396, 1660, 1, Border).GetComponent<Image>().raycastTarget = false;
        ReplayWidgets.Label(transform, "观看录像", -600, 445, 500, 72, 42);
        ReplayWidgets.Button(transform, "返回", 760, 450, 140, () => Destroy(gameObject));
        ReplayWidgets.Button(transform, "打开录像目录", -710, 350, 240, OpenFolder);
        ReplayWidgets.Button(transform, "刷新", -495, 350, 150, RefreshFiles);
        ReplayWidgets.Button(transform, "设置", 760, 350, 140, () => GameSettingsPanel.Show(transform));
        rows = ReplayWidgets.Panel(transform, "ReplayList", -305, -30, 1050, 670, Surface);
        var info = ReplayWidgets.Panel(transform, "ReplayDetails", 540, -30, 580, 670, new Color(.095f,.135f,.19f));
        AddBorder(rows.gameObject); AddBorder(info.gameObject);
        ReplayWidgets.Panel(info, "DetailRule", 0, 238, 510, 1, Border).GetComponent<Image>().raycastTarget = false;
        ReplayWidgets.Label(info, "录像详情", 0, 279, 510, 50, 28);
        details = ReplayWidgets.Label(info, "选择一场对局", 0, 42, 510, 385, 22, TextAnchor.UpperLeft);
        play = ReplayWidgets.Button(info, "播放录像", 0, -208, 500, () => OpenSelected(false), true);
        export = ReplayWidgets.Button(info, "导出视频", -130, -276, 240, () => OpenSelected(true));
        delete = ReplayWidgets.Button(info, "删除录像", 130, -276, 240, ShowDeleteSelected);
        pagination = ReplayWidgets.Label(transform, "", -300, -420, 480, 52, 20, TextAnchor.MiddleCenter);
        previousPage = ReplayWidgets.Button(transform, "上一页", -695, -420, 170, () => { if (page > 0) { page--; RenderFiles(); } });
        nextPage = ReplayWidgets.Button(transform, "下一页", 95, -420, 170, () => { if ((page + 1) * PageSize < files.Length) { page++; RenderFiles(); } });
        status = ReplayWidgets.Label(transform, ReturnMessage ?? "", 0, -487, 1660, 46, 18);
        ReturnMessage = null;
        foreach (var label in GetComponentsInChildren<Text>()) label.color = WarmText;
        foreach (var button in GetComponentsInChildren<Button>()) StyleButton(button, button == play);
        pagination.color = status.color = new Color(.64f,.73f,.84f);
        ReplayRepository.Changed += RefreshFiles;
        GamePreferences.Changed += RefreshDetails;
        RefreshFiles();
    }
    private void OnDestroy() { previewRead?.Cancel();previewRead?.Dispose();ReplayRepository.Changed -= RefreshFiles; GamePreferences.Changed -= RefreshDetails; if (current == this) current = null; }
    public void RefreshFiles()
    {
        try {
            files = ReplayRepository.ListFiles();
            if (selectedPath != null && Array.IndexOf(files, selectedPath) < 0) {
                ++selectionRevision; selected = null; selectedPath = null;
                previewRead?.Cancel();
                details.text = "选择一场对局";
            }
            page = Math.Min(page, Math.Max(0, (files.Length - 1) / PageSize)); RenderFiles();
        }
        catch (Exception e) { status.text = e.Message; }
    }
    private void RenderFiles()
    {
        foreach (Transform child in rows) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        for (int i = page * PageSize; i < Math.Min(files.Length, (page + 1) * PageSize); i++)
        {
            string path = files[i];
            var button = ReplayWidgets.Button(rows, Path.GetFileNameWithoutExtension(path), 0, 285 - (i % PageSize) * 77,
                1000, () => Select(path), selectedPath == path);
            ((RectTransform)button.transform).sizeDelta = new Vector2(1000, 65);
            StyleButton(button, selectedPath == path);
            if (selectedPath == path) ReplayWidgets.Panel(button.transform, "SelectedMark", -496, 0, 3, 47, Gold).GetComponent<Image>().raycastTarget = false;
            var label = button.GetComponentInChildren<Text>(); label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.sizeDelta = new Vector2(958, 60);
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.resizeTextForBestFit=true;
            label.resizeTextMinSize=16;label.resizeTextMaxSize=Mathf.RoundToInt(20*GamePreferences.TextScale);
        }
        if (files.Length == 0) ReplayWidgets.Label(rows, "还没有保存的录像", 0, 0, 900, 80, 28, TextAnchor.MiddleCenter).color = new Color(.60f,.62f,.64f);
        pagination.text = (page + 1) + " / " + Math.Max(1, (files.Length + PageSize - 1) / PageSize);
        play.interactable = export.interactable = selected != null && !opening;
        delete.interactable = selectedPath != null && File.Exists(selectedPath);
        previousPage.interactable = page > 0;
        nextPage.interactable = (page + 1) * PageSize < files.Length;
    }
    private static void AddBorder(GameObject target)
    {
        var outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>(); outline.effectColor = Border; outline.effectDistance = new Vector2(1,-1);
    }
    private static void StyleButton(Button button, bool primary)
    {
        button.GetComponent<Image>().color = primary ? new Color(.21f,.39f,.61f) : new Color(.12f,.18f,.27f);
        AddBorder(button.gameObject);
        var colors = button.colors; colors.highlightedColor = new Color(1.22f,1.25f,1.3f);
        colors.pressedColor = new Color(.8f,.8f,.8f); colors.disabledColor = new Color(.70f,.75f,.82f,.85f); button.colors = colors;
        button.GetComponentInChildren<Text>().color = WarmText;
    }
    public async void Select(string path)
    {
        int request = ++selectionRevision;
        previewRead?.Cancel();previewRead?.Dispose();previewRead=new CancellationTokenSource();var cancellation=previewRead.Token;
        opening=false; selected = null; selectedPath = path; play.interactable = export.interactable = false;
        delete.interactable = File.Exists(path);
        details.text = "正在读取录像…";
        try
        {
            var replay = await Task.Run(() => ReplayRepository.ReadPreview(path,false,cancellation),cancellation);
            if (this == null || request != selectionRevision) return;
            selected = replay;
            RefreshDetails();
            status.text = ""; RenderFiles();
            if(replay.duration<0){
                var complete=await Task.Run(()=>ReplayRepository.ReadPreview(path,true,cancellation),cancellation);
                if(this==null||request!=selectionRevision)return;
                selected=complete;RefreshDetails();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { if (this != null && request == selectionRevision) { details.text = "无法打开录像"; status.text = e.Message; } }
    }
    private void RefreshDetails()
    {
        if (selected == null || details == null) return;
        string started = DateTime.Parse(selected.startedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            .ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
        details.text = started + "\n\n" + selected.players[0] + "\n── VS ──\n" + selected.players[1]
            + "\n\n" + string.Format(GamePreferences.T("时长：{0}"), selected.duration<0 ? "…" : ReplayWidgets.TimeText(selected.duration));
    }
    private async void OpenSelected(bool video)
    {
        if (selected == null) return;
        if (video && !ReplayVideoEncoder.TryFind(out _)) { status.text = "未找到 FFmpeg，请将 ffmpeg.exe 放入 VideoTools 文件夹或加入 PATH。"; return; }
        if (!video&&OnlineMatchService.Instance?.IsInMatch == true) { status.text = "请先完成当前对局。"; return; }
        if(video) {
            if(ReplayVideoExportJob.Busy){status.text=GamePreferences.T("已有视频正在后台导出");return;}
            string videoSource=selectedPath;
            ReplayVideoExportDialog.Show(Path.GetFileNameWithoutExtension(videoSource),
                (destination,overwrite,width,height,fps)=>ReplayVideoExportJob.Start(videoSource,1,destination,overwrite,width,height,fps));
            return;
        }
        if(opening)return;
        int request=selectionRevision;string path=selectedPath;opening=true;
        play.interactable=export.interactable=false;status.text="正在加载录像…";
        try{
            var replay=await Task.Run(()=>ReplayRepository.Load(path));
            if(this==null||request!=selectionRevision)return;
            if(OnlineMatchService.Instance?.IsInMatch==true){status.text="请先完成当前对局。";return;}
            ReplayPlayback.Open(replay,path,video);Destroy(gameObject);
        }catch(Exception e){if(this!=null&&request==selectionRevision)status.text=e.Message;}
        finally{if(this!=null&&request==selectionRevision){opening=false;RenderFiles();}}
    }
    private void ShowDeleteSelected()
    {
        if (selectedPath == null || deleteDialog != null) return;
        string path = selectedPath;
        deleteDialog = ReplayWidgets.Panel(transform, "DeleteReplayModal", 0, 0, 1920, 1080, new Color(0,0,0,.7f));
        deleteDialog.anchorMin = Vector2.zero; deleteDialog.anchorMax = Vector2.one;
        deleteDialog.offsetMin = deleteDialog.offsetMax = Vector2.zero;
        var box = ReplayWidgets.Panel(deleteDialog, "DeleteReplayDialog", 0, 0, 720, 300, Surface);
        ReplayWidgets.Label(box, "删除录像", 0, 96, 640, 52, 30);
        var question = ReplayWidgets.Label(box, "确定删除“" + Path.GetFileNameWithoutExtension(path) + "”？\n删除后无法恢复。", 0, 8, 640, 100, 21);
        question.horizontalOverflow = HorizontalWrapMode.Wrap;
        var cancel = ReplayWidgets.Button(box, "取消", -145, -100, 250, CloseDeleteDialog);
        var confirm = ReplayWidgets.Button(box, "确认删除", 145, -100, 250, () => {
            try {
                bool removed = ReplayRepository.Delete(path);
                CloseDeleteDialog();
                status.text = removed ? "录像已删除。" : "录像已不存在，列表已刷新。";
            } catch (Exception e) { CloseDeleteDialog(); status.text = "删除失败：" + e.Message; }
        });
        StyleButton(cancel, false); StyleButton(confirm, false);
    }
    private void CloseDeleteDialog()
    {
        if (deleteDialog == null) return;
        deleteDialog.gameObject.SetActive(false); Destroy(deleteDialog.gameObject); deleteDialog = null;
    }
    private void OpenFolder()
    {
        try { Directory.CreateDirectory(ReplayRepository.Root); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ReplayRepository.Root) { UseShellExecute = true }); }
        catch (Exception e) { status.text = e.Message; }
    }
}
