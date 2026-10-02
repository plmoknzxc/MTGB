using System;
using System.Globalization;
using System.IO;
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
    private Button play, export, previousPage, nextPage;
    private string[] files = Array.Empty<string>();
    private string selectedPath;
    private MatchReplay selected;
    private int page, selectionRevision;
    private const int PageSize = 8;
    private static readonly Color Surface = new Color(.075f, .085f, .10f);
    private static readonly Color Border = new Color(.25f, .235f, .20f);
    private static readonly Color Gold = new Color(.75f, .61f, .39f);
    private static readonly Color WarmText = new Color(.92f, .90f, .84f);
    public static void Show()
    {
        if (current != null) return;
        var canvas = ReplayWidgets.Canvas("ReplayBrowser", 420);
        current = canvas.gameObject.AddComponent<ReplayBrowser>(); current.Build();
    }
    private void Build()
    {
        var background = ReplayWidgets.Panel(transform, "Background", 0, 0, 1920, 1080, new Color(.035f,.042f,.052f));
        background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one; background.offsetMin = background.offsetMax = Vector2.zero;
        var art = ReplayWidgets.Rect(background, "ArchiveArtwork", 0, 0, 1920, 1080);
        art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one; art.offsetMin = art.offsetMax = Vector2.zero;
        var image = art.gameObject.AddComponent<RawImage>();
        image.texture = Resources.Load<Texture2D>("Menu/ArchiveSanctum");
        image.color = new Color(.45f,.43f,.38f,.14f); image.raycastTarget = false;
        var fit = art.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = image.texture != null ? (float)image.texture.width / image.texture.height : 16f / 9;
        ReplayWidgets.Panel(transform, "HeaderRule", 0, 396, 1660, 1, Border).GetComponent<Image>().raycastTarget = false;
        ReplayWidgets.Label(transform, "观看录像", -600, 445, 500, 72, 42);
        ReplayWidgets.Button(transform, "返回", 760, 450, 140, () => Destroy(gameObject));
        ReplayWidgets.Button(transform, "打开录像目录", -710, 350, 240, OpenFolder);
        ReplayWidgets.Button(transform, "刷新", -495, 350, 150, RefreshFiles);
        ReplayWidgets.Button(transform, "设置", 760, 350, 140, () => GameSettingsPanel.Show(transform));
        rows = ReplayWidgets.Panel(transform, "ReplayList", -305, -30, 1050, 670, Surface);
        var info = ReplayWidgets.Panel(transform, "ReplayDetails", 540, -30, 580, 670, new Color(.12f,.112f,.098f));
        AddBorder(rows.gameObject); AddBorder(info.gameObject);
        ReplayWidgets.Panel(info, "DetailRule", 0, 238, 510, 1, Border).GetComponent<Image>().raycastTarget = false;
        ReplayWidgets.Label(info, "录像详情", 0, 279, 510, 50, 28);
        details = ReplayWidgets.Label(info, "选择一场对局", 0, 42, 510, 385, 22, TextAnchor.UpperLeft);
        play = ReplayWidgets.Button(info, "播放录像", 0, -220, 500, () => OpenSelected(false), true);
        export = ReplayWidgets.Button(info, "导出视频", 0, -288, 500, () => OpenSelected(true));
        pagination = ReplayWidgets.Label(transform, "", -300, -420, 480, 52, 20, TextAnchor.MiddleCenter);
        previousPage = ReplayWidgets.Button(transform, "上一页", -695, -420, 170, () => { if (page > 0) { page--; RenderFiles(); } });
        nextPage = ReplayWidgets.Button(transform, "下一页", 95, -420, 170, () => { if ((page + 1) * PageSize < files.Length) { page++; RenderFiles(); } });
        status = ReplayWidgets.Label(transform, ReturnMessage ?? "", 0, -487, 1660, 46, 18);
        ReturnMessage = null;
        foreach (var label in GetComponentsInChildren<Text>()) label.color = WarmText;
        foreach (var button in GetComponentsInChildren<Button>()) StyleButton(button, button == play);
        pagination.color = status.color = new Color(.64f,.65f,.64f);
        ReplayRepository.Changed += RefreshFiles;
        GamePreferences.Changed += RefreshDetails;
        RefreshFiles();
    }
    private void OnDestroy() { ReplayRepository.Changed -= RefreshFiles; GamePreferences.Changed -= RefreshDetails; if (current == this) current = null; }
    public void RefreshFiles()
    {
        try { files = ReplayRepository.ListFiles(); page = Math.Min(page, Math.Max(0, (files.Length - 1) / PageSize)); RenderFiles(); }
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
        }
        if (files.Length == 0) ReplayWidgets.Label(rows, "还没有保存的录像", 0, 0, 900, 80, 28, TextAnchor.MiddleCenter).color = new Color(.60f,.62f,.64f);
        pagination.text = (page + 1) + " / " + Math.Max(1, (files.Length + PageSize - 1) / PageSize);
        play.interactable = export.interactable = selected != null;
        previousPage.interactable = page > 0;
        nextPage.interactable = (page + 1) * PageSize < files.Length;
    }
    private static void AddBorder(GameObject target)
    {
        var outline = target.AddComponent<Outline>(); outline.effectColor = Border; outline.effectDistance = new Vector2(1,-1);
    }
    private static void StyleButton(Button button, bool primary)
    {
        button.GetComponent<Image>().color = primary ? new Color(.38f,.285f,.17f) : new Color(.125f,.14f,.16f);
        AddBorder(button.gameObject);
        var colors = button.colors; colors.highlightedColor = new Color(1.25f,1.2f,1.1f);
        colors.pressedColor = new Color(.8f,.8f,.8f); colors.disabledColor = new Color(.55f,.55f,.55f,.7f); button.colors = colors;
        button.GetComponentInChildren<Text>().color = WarmText;
    }
    public async void Select(string path)
    {
        int request = ++selectionRevision;
        selected = null; selectedPath = path; play.interactable = export.interactable = false;
        details.text = "正在读取录像…";
        try
        {
            var replay = await Task.Run(() => ReplayRepository.Load(path));
            if (this == null || request != selectionRevision) return;
            selected = replay;
            RefreshDetails();
            status.text = ""; RenderFiles();
        }
        catch (Exception e) { if (this != null && request == selectionRevision) { details.text = "无法打开录像"; status.text = e.Message; } }
    }
    private void RefreshDetails()
    {
        if (selected == null || details == null) return;
        string started = DateTime.Parse(selected.startedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            .ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
        details.text = started + "\n\n" + selected.players[0] + "\n── VS ──\n" + selected.players[1]
            + "\n\n" + string.Format(GamePreferences.T("时长：{0}"), ReplayWidgets.TimeText(selected.Duration));
    }
    private void OpenSelected(bool video)
    {
        if (selected == null) return;
        if (video && !ReplayVideoEncoder.TryFind(out _)) { status.text = "未找到 FFmpeg，请将 ffmpeg.exe 放入 VideoTools 文件夹或加入 PATH。"; return; }
        if (OnlineMatchService.Instance?.IsInMatch == true) { status.text = "请先完成当前对局。"; return; }
        ReplayPlayback.Open(selected, selectedPath, video); Destroy(gameObject);
    }
    private void OpenFolder()
    {
        try { Directory.CreateDirectory(ReplayRepository.Root); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ReplayRepository.Root) { UseShellExecute = true }); }
        catch (Exception e) { status.text = e.Message; }
    }
}
