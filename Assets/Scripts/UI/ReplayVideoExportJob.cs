using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class ReplayVideoExportRequest
{
    public string replay,output,status,cancel,language;
    public int seat=1;
    public int width=1920,height=1080,fps=30;
    public float textScale=1;
    public bool overwrite;
}
[Serializable]
public sealed class ReplayVideoExportStatus
{
    public string state="starting",message="";
    public int percent;
    public static ReplayVideoExportStatus Read(string path)
    {
        using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        using var reader=new StreamReader(input);
        return JsonUtility.FromJson<ReplayVideoExportStatus>(reader.ReadToEnd());
    }
    public static bool Write(string path,string json,bool terminal)
    {
        // Mono/Windows can reject ReplaceFile while another process polls it,
        // even with delete sharing. Progress must not abort a valid video.
        for(int attempt=0;attempt<(terminal?50:3);attempt++) {
            try {
                string temporary=path+".tmp";File.WriteAllText(temporary,json);
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
                return true;
            }catch(IOException){System.Threading.Thread.Sleep(terminal?10:2);}
        }
        return false;
    }
}

/// <summary>Owns one independent renderer, never the foreground replay or match.</summary>
public sealed class ReplayVideoExportJob : MonoBehaviour
{
    public static ReplayVideoExportJob Current { get; private set; }
    public static bool Busy=>Current!=null&&!Current.finished;
#if UNITY_EDITOR
    // Keep this project-scoped path across Play sessions and domain reloads.
    // A temporary static override made the validation pass but normal Editor use fail.
    private static string EditorWorkerKey=>"MTGB.ReplayExport.Player."+Application.dataPath;
    public static string EditorWorkerExecutable
    {
        get=>UnityEditor.EditorPrefs.GetString(EditorWorkerKey,"");
        set {if(string.IsNullOrEmpty(value))UnityEditor.EditorPrefs.DeleteKey(EditorWorkerKey);
            else UnityEditor.EditorPrefs.SetString(EditorWorkerKey,Path.GetFullPath(value));}
    }
#endif
    private Process worker;
    private ReplayVideoExportRequest request;
    private Text label;
    private Button cancel,close,fold;
    private RectTransform panel;
    private string directory;
    private string executable;
    private bool finished;
    private float nextPoll;
    private Task preparation;
    public static bool Start(string replay,int seat,string output,bool overwrite,int width=1920,int height=1080,int fps=30)
    {
        if(Busy)return false;
        if(Current!=null)Destroy(Current.gameObject);
        var canvas=ReplayWidgets.Canvas("ReplayVideoExportJob",950);
        DontDestroyOnLoad(canvas.gameObject);
        var job=Current=canvas.gameObject.AddComponent<ReplayVideoExportJob>();
        job.Build();
        try {
            if(!File.Exists(replay))throw new FileNotFoundException(GamePreferences.T("录像文件不存在。"));
            if(!ReplayVideoEncoder.TryFind(out _))throw new IOException(GamePreferences.T("未找到 FFmpeg，请将 ffmpeg.exe 放入 VideoTools 文件夹或加入 PATH。"));
#if UNITY_EDITOR
            job.executable=EditorWorkerExecutable;
            if(string.IsNullOrEmpty(job.executable)||!File.Exists(job.executable))
                throw new IOException(GamePreferences.T("编辑器尚无可用的后台客户端，请先退出播放模式，使用 MTGB／构建／Windows 客户端（开发版）完成构建。"));
#else
            using(var self=Process.GetCurrentProcess())job.executable=self.MainModule.FileName;
            if(string.IsNullOrEmpty(job.executable)||!File.Exists(job.executable))throw new IOException(GamePreferences.T("找不到后台导出程序。"));
#endif
            job.directory=Path.Combine(Application.temporaryCachePath,"ReplayExports",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(job.directory);
            job.request=new ReplayVideoExportRequest{replay=Path.Combine(job.directory,"source.mtgbr"),output=Path.GetFullPath(output),
                status=Path.Combine(job.directory,"status.json"),cancel=Path.Combine(job.directory,"cancel"),
                language=GamePreferences.Language,textScale=GamePreferences.TextScale,seat=seat,overwrite=overwrite,width=width,height=height,fps=fps};
            string json=JsonUtility.ToJson(job.request);
            job.preparation=Task.Run(()=>{File.Copy(replay,job.request.replay);File.WriteAllText(Path.Combine(job.directory,"job.json"),json);});
            return true;
        }catch(Exception e){job.Complete(GamePreferences.T("导出失败：")+e.Message);return false;}
    }
    private void Build()
    {
        var box=panel=ReplayWidgets.Panel(transform,"ExportProgress",0,0,340,112,MenuVisualTheme.Panel);
        box.anchorMin=box.anchorMax=box.pivot=Vector2.one;box.anchoredPosition=new Vector2(-20,-138);
        fold=ReplayWidgets.Button(transform,"导出任务",0,0,160,()=>panel.gameObject.SetActive(!panel.gameObject.activeSelf));
        var chip=(RectTransform)fold.transform;chip.anchorMin=chip.anchorMax=chip.pivot=Vector2.one;chip.anchoredPosition=new Vector2(-20,-80);
        label=ReplayWidgets.Label(box,"正在准备后台导出…",0,28,310,44,18,TextAnchor.MiddleCenter);
        cancel=ReplayWidgets.Button(box,"取消导出",-80,-28,140,Cancel);
        close=ReplayWidgets.Button(box,"关闭",80,-28,130,()=>Destroy(gameObject));close.gameObject.SetActive(false);
        panel.gameObject.SetActive(false);
    }
    private void Update()
    {
        if(finished||Time.unscaledTime<nextPoll)return;
        nextPoll=Time.unscaledTime+.5f;
        try {
            if(worker==null) {
                if(preparation==null||!preparation.IsCompleted)return;
                preparation.GetAwaiter().GetResult();
                if(File.Exists(request.cancel)){Complete(GamePreferences.T("已取消导出"));return;}
                worker=Process.Start(new ProcessStartInfo{FileName=executable,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,
                    WorkingDirectory=Path.GetDirectoryName(executable),
                    Arguments="-batchmode -force-d3d11 -screen-fullscreen 0 -screen-width "+request.width+" -screen-height "+request.height+" -mtgb-replay-export \""+Path.Combine(directory,"job.json")+"\" -logFile \""+Path.Combine(directory,"worker.log")+"\""});
                try{worker.PriorityClass=ProcessPriorityClass.BelowNormal;}catch(System.ComponentModel.Win32Exception){}
            }
            if(File.Exists(request.status)) {
                var status=ReplayVideoExportStatus.Read(request.status);
                if(status.state=="completed"){Complete(GamePreferences.T("视频已导出：")+request.output,true);return;}
                if(status.state=="cancelled"||status.state=="failed"){Complete(status.message);return;}
                label.text=File.Exists(request.cancel)?GamePreferences.T("正在取消导出…"):GamePreferences.T("正在后台导出视频")+" · "+status.percent+"%";
            }
            if(worker.HasExited) {
                if(File.Exists(request.cancel))Complete(GamePreferences.T("已取消导出"));
                else if(worker.ExitCode==0&&File.Exists(request.output))Complete(GamePreferences.T("视频已导出：")+request.output,true);
                else Complete(GamePreferences.T("后台导出已停止，请查看日志：")+Path.Combine(directory,"worker.log"));
            }
        }catch(IOException)when(worker!=null&&!worker.HasExited){/* A status replacement can race a poll; retry without cancelling a live renderer. */}
        catch(Exception e){Complete(GamePreferences.T("导出失败：")+e.Message);}
    }
    private void Cancel()
    {
        if(finished||request==null)return;
        try{File.WriteAllText(request.cancel,"");cancel.interactable=false;label.text=GamePreferences.T("正在取消导出…");}
        catch(Exception e){label.text=e.Message;}
    }
    private void Complete(string message,bool success=false)
    {
        finished=true;label.text=message;label.horizontalOverflow=HorizontalWrapMode.Wrap;
        cancel.gameObject.SetActive(false);close.gameObject.SetActive(true);
        fold.GetComponentInChildren<Text>().text=GamePreferences.T(success?"导出完成":"导出已停止");
        if(success) {
            cancel.gameObject.SetActive(true);cancel.interactable=true;
            cancel.GetComponentInChildren<Text>().text=GamePreferences.T("打开目录");cancel.onClick.RemoveAllListeners();
            cancel.onClick.AddListener(()=>{try{Process.Start(new ProcessStartInfo{FileName=Path.GetDirectoryName(request.output),UseShellExecute=true});}catch(Exception e){label.text=e.Message;}});
        }
        // The worker owns its copy only until it exits. Never remove a live input.
        if(worker==null||worker.HasExited) {try{if(request!=null&&File.Exists(request.replay))File.Delete(request.replay);}catch(IOException){}}
    }
    private void OnDestroy(){worker?.Dispose();if(Current==this)Current=null;}
}
