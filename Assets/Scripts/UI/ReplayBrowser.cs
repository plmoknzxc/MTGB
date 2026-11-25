using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class ReplayBrowser : MonoBehaviour
{
    private static ReplayBrowser current;
    private GameObject coveredMenu;
    public static bool ReopenOnReturn;
    public static string ReturnMessage;
    private RectTransform rows;
    private Text details, status, pagination, versus, timing, filename, listHeading;
    private readonly Text[] playerLabels = new Text[4];
    private Button play, export, delete, previousPage, nextPage;
    private RectTransform deleteDialog;
    private string[] files = Array.Empty<string>();
    private string selectedPath;
    private ReplayPreview selected;
    private bool opening;
    private CancellationTokenSource previewRead;
    private int page, selectionRevision;
    private const int PageSize = 8;
    // Recorder names contain a timestamp, sometimes followed by a game number and match id.
    // Custom filenames are displayed verbatim.
    private static readonly Regex RecorderName = new Regex(
        @"^(?<stamp>\d{4}-\d{2}-\d{2} \d{2}-\d{2}-\d{2})(?: G(?<game>\d+) [a-fA-F0-9]{32})?(?<copy> \(\d+\))?$");
    public static void Show(GameObject menuToCover = null)
    {
        if (current != null) return;
        var canvas = ReplayWidgets.Canvas("ReplayBrowser", 420);
        current = canvas.gameObject.AddComponent<ReplayBrowser>(); current.Build();
        if (menuToCover != null && menuToCover.activeSelf)
        {
            current.coveredMenu = menuToCover;
            menuToCover.SetActive(false);
        }
    }
    private void Build()
    {
        var root = MenuVisualTheme.ArchivePage(GetComponent<Canvas>(), "ReplayPage", "观看录像", "返回", () => Destroy(gameObject));
        Button Tool(Transform parent, string label, float x, float y, float width, Action action, bool primary = false)
        {
            var button = ReplayWidgets.Button(parent, label, x, y, width, action, primary);
            MenuVisualTheme.StyleDeckTool(button, primary);
            return button;
        }
        Tool(root, "设置", 500, 401, 156, () => GameSettingsPanel.Show(transform, archiveStyle:true));
        var list = MenuVisualTheme.ArchiveSection(root, "ReplayList", DeckCompanion.Text("本地录像","Local recordings","ローカル録画"),
            -330, -25, 980, 690, MenuVisualTheme.LibraryAccent);
        listHeading = list.GetComponentInChildren<Text>();
        Tool(list, "打开录像目录", 212, 299, 206, OpenFolder);
        Tool(list, "刷新", 394, 299, 130, RefreshFiles);
        rows = ReplayWidgets.Rect(list, "Files", 0, -8, 916, 488);
        pagination = ReplayWidgets.Label(list, "", 0, -301, 350, 50, 20, TextAnchor.MiddleCenter);
        pagination.color = MenuVisualTheme.Stone;
        previousPage = Tool(list, "上一页", -364, -301, 188, () => { if (page > 0) { page--; RenderFiles(); } });
        nextPage = Tool(list, "下一页", 364, -301, 188, () => { if ((page + 1) * PageSize < files.Length) { page++; RenderFiles(); } });

        var info = MenuVisualTheme.ArchiveSection(root, "ReplayDetails", "录像详情", 502, -25, 636, 690, MenuVisualTheme.SideAccent);
        var plate = ReplayWidgets.Panel(info, "Matchup", 0, 107, 572, 250, MenuVisualTheme.Recess);
        var scenery = ReplayWidgets.Rect(plate, "MoonlitBackdrop", 0, 0, 572, 250).gameObject.AddComponent<RawImage>();
        MenuThemeArtwork.Bind(scenery, MenuThemeArtwork.Role.Replay);
        scenery.uvRect = new Rect(.04f, .07f, .92f, .683f);
        scenery.color = new Color(.70f, .82f, .94f, .96f);
        scenery.raycastTarget = false;
        details = ReplayWidgets.Label(plate, "选择一场对局", 0, 74, 518, 52, 21);
        details.color = MenuVisualTheme.Silver;
        Text PlayerLabel(float y)
        {
            var player = ReplayWidgets.Label(plate, "", 0, y, 518, 55, 30);
            player.GetComponent<PreferenceText>().Literal = true;
            player.resizeTextForBestFit = true; player.resizeTextMinSize = 14; player.resizeTextMaxSize = 36;
            MenuTypography.Use(player, MenuTypography.Face.Heading);
            return player;
        }
        for (int i = 0; i < playerLabels.Length; i++) playerLabels[i] = PlayerLabel(0);
        versus = ReplayWidgets.Label(plate, "", 0, -28, 518, 32, 16);
        MenuTypography.Use(versus, MenuTypography.Face.Control);
        versus.color = MenuVisualTheme.SideAccent;
        timing = ReplayWidgets.Label(info, "", 0, -61, 572, 46, 22);
        filename = ReplayWidgets.Label(info, "", 0, -126, 572, 78, 17);
        filename.color = MenuVisualTheme.Stone;
        filename.GetComponent<PreferenceText>().Literal = true;
        filename.resizeTextForBestFit = true; filename.resizeTextMinSize = 14; filename.resizeTextMaxSize = 20;
        play = Tool(info, "播放录像", 0, -213, 572, () => OpenSelected(false), true);
        ((RectTransform)play.transform).sizeDelta = new Vector2(572, 66);
        export = Tool(info, "导出视频", -148, -301, 276, () => OpenSelected(true));
        delete = Tool(info, "删除录像", 148, -301, 276, ShowDeleteSelected);
        delete.GetComponentInChildren<Text>().color = MenuVisualTheme.Warning;
        status = ReplayWidgets.Label(root, ReturnMessage ?? "", 0, -413, 1640, 65, 20);
        status.color = MenuVisualTheme.Silver;
        ReturnMessage = null;
        ReplayRepository.Changed += RefreshFiles;
        GamePreferences.Changed += RefreshPresentation;
        RefreshFiles();
    }
    private void OnDestroy()
    {
        previewRead?.Cancel();
        previewRead?.Dispose();
        ReplayRepository.Changed -= RefreshFiles;
        GamePreferences.Changed -= RefreshPresentation;
        if (current == this) current = null;
        if (coveredMenu != null && coveredMenu.scene.isLoaded) coveredMenu.SetActive(true);
    }
    private void RefreshPresentation()
    {
        listHeading.text = DeckCompanion.Text("本地录像","Local recordings","ローカル録画");
        RefreshDetails();
        RenderFiles();
    }
    public void RefreshFiles()
    {
        try {
            files = ReplayRepository.ListFiles();
            if (selectedPath != null && Array.IndexOf(files, selectedPath) < 0) {
                ++selectionRevision; selected = null; selectedPath = null;
                previewRead?.Cancel();
                ClearPreview("选择一场对局");
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
            string name = Path.GetFileNameWithoutExtension(path);
            bool active = selectedPath == path;
            float y = 214 - (i % PageSize) * 61;
            var button = ReplayWidgets.Button(rows, name, 0, y, 916, () => Select(path));
            ((RectTransform)button.transform).sizeDelta = new Vector2(916, 56);
            MenuVisualTheme.StyleDeckOption(button, active);
            if (!active)
            {
                var colors = button.colors;
                colors.normalColor = (i % 2 == 0) ? MenuVisualTheme.ArchivePanel : MenuVisualTheme.Recess;
                button.colors = colors;
                button.targetGraphic.CrossFadeColor(colors.normalColor, 0, true, true);
            }
            else
                ReplayWidgets.Panel(button.transform, "SelectedMark", -453, 0, 4, 42, MenuVisualTheme.SideAccent).GetComponent<Image>().raycastTarget = false;
            var label = button.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.GetComponent<PreferenceText>().Literal = true;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.rectTransform.sizeDelta = new Vector2(858, 54);
            label.resizeTextMinSize = 15; label.resizeTextMaxSize = 24;
            var match = RecorderName.Match(name);
            if (match.Success && DateTime.TryParseExact(match.Groups["stamp"].Value, "yyyy-MM-dd HH-mm-ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp))
            {
                label.text = stamp.ToString("yyyy / MM / dd", CultureInfo.InvariantCulture);
                label.rectTransform.sizeDelta = new Vector2(426, 52);
                label.rectTransform.anchoredPosition = new Vector2(-216, 0);
                var clock = ReplayWidgets.Label(button.transform, stamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + match.Groups["copy"].Value,
                    32, 0, 228, 50, 20);
                MenuTypography.Use(clock, MenuTypography.Face.Control);
                clock.color = MenuVisualTheme.Silver;
                if (match.Groups["game"].Success)
                {
                    string game = match.Groups["game"].Value;
                    var round = ReplayWidgets.Label(button.transform, DeckCompanion.Text("第 " + game + " 局","Game " + game,"第 " + game + " ゲーム"),
                        319, 0, 214, 50, 20, TextAnchor.MiddleRight);
                    round.color = MenuVisualTheme.SideAccent;
                }
            }
        }
        if (files.Length == 0)
            ReplayWidgets.Label(rows, "还没有保存的录像", 0, 0, 830, 80, 26, TextAnchor.MiddleCenter).color = MenuVisualTheme.Stone;
        pagination.text = (page + 1) + " / " + Math.Max(1, (files.Length + PageSize - 1) / PageSize);
        play.interactable = export.interactable = selected != null && !opening;
        delete.interactable = selectedPath != null && File.Exists(selectedPath);
        previousPage.interactable = page > 0;
        nextPage.interactable = (page + 1) * PageSize < files.Length;
    }
    private void ClearPreview(string message)
    {
        details.text = message;
        foreach (var label in playerLabels) label.text = "";
        versus.text = timing.text = filename.text = "";
    }
    public async void Select(string path)
    {
        int request = ++selectionRevision;
        previewRead?.Cancel();previewRead?.Dispose();previewRead=new CancellationTokenSource();var cancellation=previewRead.Token;
        opening=false; selected = null; selectedPath = path; play.interactable = export.interactable = false;
        delete.interactable = File.Exists(path);
        ClearPreview("正在读取录像…");
        filename.text = Path.GetFileName(path);
        RenderFiles();
        try
        {
            var replay = await Task.Run(() => ReplayRepository.ReadPreview(path,false,cancellation),cancellation);
            if (this == null || request != selectionRevision) return;
            selected = replay;
            RefreshDetails();
            status.text = ""; play.interactable = export.interactable = !opening;
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
        details.text = started;
        bool fourSeats = selected.players.Length == 4;
        versus.text = fourSeats ? "" : "VS";
        for (int i = 0; i < playerLabels.Length; i++)
        {
            var label = playerLabels[i];
            label.gameObject.SetActive(i < selected.players.Length);
            if (i >= selected.players.Length) continue;
            label.text = selected.players[i];
            label.supportRichText = false;
            label.rectTransform.sizeDelta = new Vector2(fourSeats ? 252 : 518, 55);
            label.rectTransform.anchoredPosition = fourSeats
                ? new Vector2(i % 2 == 0 ? -136 : 136, i < 2 ? 12 : -64)
                : new Vector2(0, i == 0 ? 20 : -76);
        }
        timing.text = string.Format(GamePreferences.T("时长：{0}"), selected.duration < 0 ? "…" : ReplayWidgets.TimeText(selected.duration));
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
                (destination,overwrite,width,height,fps)=>ReplayVideoExportJob.Start(videoSource,1,destination,overwrite,width,height,fps), archiveStyle:true);
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
        deleteDialog = ReplayWidgets.Panel(transform, "DeleteReplayModal", 0, 0, 1920, 1080, MenuVisualTheme.DialogShade);
        deleteDialog.anchorMin = Vector2.zero; deleteDialog.anchorMax = Vector2.one;
        deleteDialog.offsetMin = deleteDialog.offsetMax = Vector2.zero;
        var box = ReplayWidgets.Panel(deleteDialog, "DeleteReplayDialog", 0, 0, 780, 336, MenuVisualTheme.ArchivePanel);
        var heading = ReplayWidgets.Label(box, "删除录像", 0, 96, 640, 52, 30);
        MenuVisualTheme.StyleDeckDialogSurface(box, heading);
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
        MenuVisualTheme.StyleDeckTool(cancel); MenuVisualTheme.StyleDeckTool(confirm);
        confirm.GetComponentInChildren<Text>().color = MenuVisualTheme.Warning;
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
