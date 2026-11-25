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
    private ForgeBattlefieldController board;
    private Canvas controls;
    private RectTransform controlBar;
    private Button controlsToggle;
    private Slider slider;
    private Text timeText, stateText, playText, speedText;
    private int lastFrame = -1;
    private int lastPresentationRevision = -1;
    private string source;
    internal static string BattlefieldScene(int playerCount) => playerCount == 4
        ? ForgeBattlefieldController.FourPlayerSceneName : ForgeBattlefieldController.SceneName;
    public static void Open(MatchReplay replay, string path, bool export)
    {
        if (Current != null) return;
        var go = new GameObject("ReplayPlayback"); DontDestroyOnLoad(go);
        Current = go.AddComponent<ReplayPlayback>(); Current.Replay = replay; Current.source = path;
        Current.StartCoroutine(Current.Load(export));
    }
    private IEnumerator Load(bool export)
    {
        yield return SceneManager.LoadSceneAsync(BattlefieldScene(Replay.players.Length));
        board = FindFirstObjectByType<ForgeBattlefieldController>();
        if (board == null) board = new GameObject("ReplayBattlefield").AddComponent<ForgeBattlefieldController>();
        BuildControls(); Render(0,true);
        if (export) ChooseExport();
    }
    private void BuildControls()
    {
        controls = ReplayWidgets.Canvas("ReplayControls", 700);
        var bar = controlBar = ReplayWidgets.Panel(controls.transform, "Controls", 0, 295, 1280, 170, new Color(.045f,.065f,.09f,.96f));
        var header=ReplayWidgets.Panel(bar,"DragHeader",0,65,1260,30,new Color(.08f,.12f,.17f));
        ReplayWidgets.Label(header,"录像控制 · 拖动此处移动",0,0,1190,30,18,TextAnchor.MiddleCenter);
        header.gameObject.AddComponent<ForgeCardNoticeDrag>().Window=bar;
        controlsToggle=ReplayWidgets.Button(controls.transform,"录像控制",0,0,160,()=>SetControlsVisible(!controlBar.gameObject.activeSelf));
        var toggleRect=(RectTransform)controlsToggle.transform;
        toggleRect.anchorMin=toggleRect.anchorMax=new Vector2(1,1);toggleRect.pivot=new Vector2(1,1);toggleRect.anchoredPosition=new Vector2(-20,-18);
        ReplayWidgets.Button(bar, "返回录像", -540, 24, 160, Exit);
        playText = ReplayWidgets.Button(bar, "暂停", -395, 24, 110, () => {
            Playing = !Playing;
            if (Playing && lastFrame == Replay.frames.Count - 1) { Position = 0; Render(0,true); }
        }).GetComponentInChildren<Text>();
        ReplayWidgets.Button(bar, "上一步", -275, 24, 110, () => Step(-1));
        ReplayWidgets.Button(bar, "下一步", -155, 24, 110, () => Step(1));
        speedText = ReplayWidgets.Button(bar, "1×", -55, 24, 70, () => { Speed = Speed >= 4 ? .5f : Speed * 2; }).GetComponentInChildren<Text>();
        ReplayWidgets.Button(bar, "切换视角", 70, 24, 160, () => { Seat = Seat % Replay.players.Length + 1; Render(lastFrame,true); });
        ReplayWidgets.Button(bar, "导出视频", 240, 24, 160, ChooseExport);
        timeText = ReplayWidgets.Label(bar, "", 475, 24, 270, 48, 20, TextAnchor.MiddleRight);
        var track = ReplayWidgets.Panel(bar, "Timeline", 0, -32, 1230, 12, MenuVisualTheme.Raised);
        slider = track.gameObject.AddComponent<Slider>(); slider.minValue = 0; slider.maxValue = (float)Math.Max(.01, Replay.Duration);
        var fillArea = ReplayWidgets.Rect(track, "FillArea", 0, 0, 1210, 12);
        var fill = ReplayWidgets.Panel(fillArea, "Fill", 0, 0, 0, 0, MenuVisualTheme.Accent);
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
        slider.fillRect = fill;
        var handle = ReplayWidgets.Panel(track, "Handle", 0, 0, 22, 30, MenuVisualTheme.Paper);
        slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
        slider.onValueChanged.AddListener(value => { Playing = false; Seek(value); });
        stateText = ReplayWidgets.Label(bar, "", 0, -65, 1230, 32, 18, TextAnchor.MiddleCenter);
        SetControlsVisible(false);
    }
    private void SetControlsVisible(bool visible)
    {
        controlBar.gameObject.SetActive(visible);
        controlsToggle.GetComponent<Image>().color=visible?MenuVisualTheme.Primary:MenuVisualTheme.Raised;
    }
    private void ChooseExport()
    {
        if(ReplayVideoExportJob.Busy){SetControlsVisible(true);stateText.text=GamePreferences.T("已有视频正在后台导出");return;}
        string name=Path.GetFileNameWithoutExtension(source);
        ReplayVideoExportDialog.Show(string.IsNullOrWhiteSpace(name)?ReplayRepository.DefaultName(Replay):name,
            (destination,overwrite,width,height,fps)=>ReplayVideoExportJob.Start(source,Seat,destination,overwrite,width,height,fps));
    }
    private void Update()
    {
        if (board == null) return;
        // A paused frame still depends on loaded translations, fonts and text size.
        if (Playing)
        {
            Position = Math.Min(Replay.Duration, Position + Time.unscaledDeltaTime * Speed);
            Render(ReplayRepository.FrameAt(Replay,Position),false);
            if (Position >= Replay.Duration) Playing = false;
        }
        else if(GamePreferences.Ready && lastPresentationRevision!=GamePreferences.Revision)Render(lastFrame,false);
        slider.SetValueWithoutNotify((float)Position);
        timeText.text = ReplayWidgets.TimeText(Position) + " / " + ReplayWidgets.TimeText(Replay.Duration);
        playText.text = GamePreferences.T(Playing ? "暂停" : "播放"); speedText.text = Speed + "×";
    }
    public void Seek(double seconds)
    {
        Position = Math.Max(0, Math.Min(Replay.Duration, seconds));
        Render(ReplayRepository.FrameAt(Replay,Position),true);
    }
    private void Step(int direction)
    {
        Playing = false;
        int frame = Mathf.Clamp(lastFrame + direction, 0, Replay.frames.Count - 1);
        Position = Replay.frames[frame].time;
        Render(frame,true);
    }
    private void Render(int frame,bool seek)
    {
        if (board == null || frame < 0) return;
        if (!seek && frame == lastFrame && lastPresentationRevision==GamePreferences.Revision) return;
        lastFrame = frame;
        lastPresentationRevision=GamePreferences.Revision;
        board.PresentReplayFrame(Replay,frame,Seat,seek);
        if(frame==Replay.frames.Count-1){
            string winner=Replay.winnerSeat>0?Replay.players[Replay.winnerSeat-1]:null;
            winner=OnlineRoomConfig.WinnerLabel(Replay.frames[frame].views[0],Replay.winnerSeat,winner);
            stateText.text=winner==null?GamePreferences.T(Replay.reason??"对局结束"):GamePreferences.Language switch {
                "zh-CN"=>winner+" 获胜", "ja-JP"=>winner+"の勝利", _=>winner+" wins"
            };
            SetControlsVisible(true);
        }else stateText.text = Replay.players[Seat - 1] + " · " + GamePreferences.T("观看录像");
    }
    private void Exit()
    {
        ReplayBrowser.ReopenOnReturn = true;
        if (controls != null) Destroy(controls.gameObject);
        Destroy(gameObject); SceneManager.LoadScene("Main");
    }
    private void OnDestroy() { if (Current == this) Current = null; }
}
