using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ReplayPlayback : MonoBehaviour
{
    public static ReplayPlayback Current { get; private set; }
    public MatchReplay Replay { get; private set; }
    public double Position { get; private set; }
    public bool Playing { get; private set; } = true;
    public int Seat { get; private set; } = 1;
    public float Speed { get; private set; } = 1;
    public bool Exporting { get; private set; }
    private bool cancelExport;
    private ForgeBattlefieldController board;
    private Canvas controls;
    private Slider slider;
    private Text timeText, stateText, playText, speedText;
    private int lastFrame = -1;
    private string source;
    public static void Open(MatchReplay replay, string path, bool export)
    {
        if (Current != null) return;
        var go = new GameObject("ReplayPlayback"); DontDestroyOnLoad(go);
        Current = go.AddComponent<ReplayPlayback>(); Current.Replay = replay; Current.source = path;
        Current.StartCoroutine(Current.Load(export));
    }
    private IEnumerator Load(bool export)
    {
        yield return SceneManager.LoadSceneAsync(ForgeBattlefieldController.SceneName);
        board = FindFirstObjectByType<ForgeBattlefieldController>();
        if (board == null) board = new GameObject("ReplayBattlefield").AddComponent<ForgeBattlefieldController>();
        BuildControls(); Seek(0);
        if (export) StartCoroutine(Export());
    }
    private void BuildControls()
    {
        controls = ReplayWidgets.Canvas("ReplayControls", 700);
        var bar = ReplayWidgets.Panel(controls.transform, "Controls", 0, 462, 1280, 126, new Color(.045f,.065f,.09f,.96f));
        ReplayWidgets.Button(bar, "返回录像", -540, 24, 160, Exit);
        playText = ReplayWidgets.Button(bar, "暂停", -395, 24, 110, () => { if(Exporting)return; Playing = !Playing; if (Position >= Replay.Duration) Seek(0); }).GetComponentInChildren<Text>();
        ReplayWidgets.Button(bar, "上一步", -275, 24, 110, () => Step(-1));
        ReplayWidgets.Button(bar, "下一步", -155, 24, 110, () => Step(1));
        speedText = ReplayWidgets.Button(bar, "1×", -55, 24, 70, () => { if(!Exporting) Speed = Speed >= 4 ? .5f : Speed * 2; }).GetComponentInChildren<Text>();
        ReplayWidgets.Button(bar, "切换视角", 70, 24, 160, () => { if(Exporting)return; Seat = 3 - Seat; Seek(Position); });
        ReplayWidgets.Button(bar, "导出视频", 240, 24, 160, () => { if (!Exporting) StartCoroutine(Export()); });
        timeText = ReplayWidgets.Label(bar, "", 475, 24, 270, 48, 20, TextAnchor.MiddleRight);
        var track = ReplayWidgets.Panel(bar, "Timeline", 0, -32, 1230, 12, MenuVisualTheme.Raised);
        slider = track.gameObject.AddComponent<Slider>(); slider.minValue = 0; slider.maxValue = (float)Math.Max(.01, Replay.Duration);
        var fillArea = ReplayWidgets.Rect(track, "FillArea", 0, 0, 1210, 12);
        var fill = ReplayWidgets.Panel(fillArea, "Fill", 0, 0, 0, 0, MenuVisualTheme.Accent);
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
        slider.fillRect = fill;
        var handle = ReplayWidgets.Panel(track, "Handle", 0, 0, 22, 30, MenuVisualTheme.Paper);
        slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
        slider.onValueChanged.AddListener(value => { if (!Exporting) { Playing = false; Seek(value); } });
        stateText = ReplayWidgets.Label(controls.transform, "", 0, 373, 1350, 40, 20, TextAnchor.MiddleCenter);
    }
    private void Update()
    {
        if (board == null) return;
        if (!Exporting && Playing)
        {
            Position = Math.Min(Replay.Duration, Position + Time.unscaledDeltaTime * Speed);
            Render(false);
            if (Position >= Replay.Duration) Playing = false;
        }
        slider.SetValueWithoutNotify((float)Position);
        timeText.text = ReplayWidgets.TimeText(Position) + " / " + ReplayWidgets.TimeText(Replay.Duration);
        playText.text = GamePreferences.T(Playing ? "暂停" : "播放"); speedText.text = Speed + "×";
    }
    public void Seek(double seconds)
    {
        Position = Math.Max(0, Math.Min(Replay.Duration, seconds)); Render(true);
    }
    private void Step(int direction)
    {
        if (Exporting) return;
        Playing = false;
        int frame = Mathf.Clamp(ReplayRepository.FrameAt(Replay, Position) + direction, 0, Replay.frames.Count - 1);
        Seek(Replay.frames[frame].time);
    }
    private void Render(bool seek)
    {
        if (board == null) return;
        int frame = ReplayRepository.FrameAt(Replay, Position);
        if (!seek && frame == lastFrame) return;
        lastFrame = frame;
        board.PresentReplay(Replay.frames[frame].views[Seat - 1], Seat, Replay.players, seek);
        if (!Exporting) stateText.text = Replay.players[Seat - 1] + " · " + GamePreferences.T("观看录像");
    }
    private void Exit()
    {
        if (Exporting) { cancelExport = true; return; }
        ReplayBrowser.ReopenOnReturn = true;
        if (controls != null) Destroy(controls.gameObject);
        Destroy(gameObject); SceneManager.LoadScene("Main");
    }
    private void OnDestroy() { if (Current == this) Current = null; }
    private IEnumerator Export()
    {
        if (!ReplayVideoEncoder.TryFind(out string ffmpeg)) { stateText.text = "未找到 FFmpeg，请将 ffmpeg.exe 放入 VideoTools 文件夹或加入 PATH。"; yield break; }
        Exporting = true; Playing = false; cancelExport = false;
        int previousRate = Time.captureFramerate;
        ReplayVideoEncoder encoder = null;
        string output = null, failure = null;
        Texture2D pixels = null;
        var previousPosition = Position;
        try
        {
            Directory.CreateDirectory(ReplayRepository.VideoRoot);
            string name = Path.GetFileNameWithoutExtension(source) ?? ReplayRepository.DefaultName(Replay);
            output = Path.Combine(ReplayRepository.VideoRoot, name + ".mp4");
            for (int n = 1; File.Exists(output); n++) output = Path.Combine(ReplayRepository.VideoRoot, name + " (" + n + ").mp4");
            int width = Screen.width, height = Screen.height;
            try
            {
                encoder = new ReplayVideoEncoder(ffmpeg, output, width, height, 30);
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            }
            catch (Exception e) { failure = e.Message; }
            if (failure == null)
            {
                Time.captureFramerate = 30;
                int count = (int)Math.Ceiling((Replay.Duration + 2) * 30);
                var endOfFrame = new WaitForEndOfFrame();
                Seek(0);
                for (int i = 0; i < count && !cancelExport; i++)
                {
                    Position = Math.Min(Replay.Duration, i / 30d); Render(false);
                    controls.enabled = false;
                    yield return endOfFrame;
                    System.Threading.Tasks.Task write = null;
                    try
                    {
                        if (Screen.width != width || Screen.height != height) throw new IOException("窗口尺寸改变，视频导出已停止，请重试。");
                        pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                        write = encoder.Write(pixels.GetRawTextureData());
                    }
                    catch (Exception e) { failure = e.Message; }
                    controls.enabled = true;
                    stateText.text = GamePreferences.T("正在导出视频") + " · " + (i * 100 / count) + "%  ·  " + GamePreferences.T("点击返回取消");
                    if (failure != null) break;
                    while (!write.IsCompleted) yield return null;
                    if (write.IsFaulted) { failure = write.Exception.GetBaseException().Message; break; }
                }
                if (!cancelExport && failure == null)
                {
                    var finish = encoder.Finish();
                    while (!finish.IsCompleted) yield return null;
                    if (finish.IsFaulted) failure = finish.Exception.GetBaseException().Message;
                }
            }
        }
        finally
        {
            encoder?.Dispose(); if (pixels != null) Destroy(pixels);
            Time.captureFramerate = previousRate;
            Exporting = false; if (controls != null) controls.enabled = true;
        }
        Seek(previousPosition);
        stateText.text = failure != null ? "导出失败：" + failure : cancelExport ? "已取消导出" : "视频已导出：" + output;
    }
}
