using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Separate graphics-enabled batch player for deterministic video frames.</summary>
public sealed class ReplayExportWorker : MonoBehaviour
{
    public static bool IsWorker { get; }=!string.IsNullOrEmpty(OnlineClientProfileStore.ReadArgument("-mtgb-replay-export"));
    public static float TextScale { get; private set; }=1;
    private ReplayVideoExportRequest request;
    private ReplayVideoExportStatus progress=new ReplayVideoExportStatus();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if(!IsWorker)return;
        Application.runInBackground=true;Application.targetFrameRate=30;QualitySettings.vSyncCount=0;
        var go=new GameObject("ReplayExportWorker");DontDestroyOnLoad(go);go.AddComponent<ReplayExportWorker>();
    }
    private IEnumerator Start()
    {
        var task=Run();Exception failure=null;
        for(;;) {
            bool more=false;object next=null;
            try{more=task.MoveNext();if(more)next=task.Current;}catch(Exception e){failure=e;}
            if(failure!=null||!more)break;
            yield return next;
        }
        (task as IDisposable)?.Dispose();
        if(failure!=null){UnityEngine.Debug.LogException(failure);try{Publish("failed",GamePreferences.T("导出失败：")+failure.Message);}catch(IOException e){UnityEngine.Debug.LogException(e);}}
        try{if(request!=null&&File.Exists(request.replay))File.Delete(request.replay);}catch(IOException e){UnityEngine.Debug.LogWarning(e.Message);}
        Application.Quit(failure==null?0:1);
    }
    private void Publish(string state,string message="",int percent=0)
    {
        if(request==null)return;
        progress.state=state;progress.message=message;progress.percent=percent;
        bool terminal=state=="completed"||state=="cancelled"||state=="failed";
        if(!ReplayVideoExportStatus.Write(request.status,JsonUtility.ToJson(progress),terminal)&&terminal)
            UnityEngine.Debug.LogWarning("Replay export finished without publishing its terminal status; the parent can use the exit code.");
    }
    private IEnumerator Run()
    {
        request=JsonUtility.FromJson<ReplayVideoExportRequest>(File.ReadAllText(OnlineClientProfileStore.ReadArgument("-mtgb-replay-export")));
        TextScale=Mathf.Clamp(request.textScale,.85f,1.3f);
        if(request.seat<1||request.seat>2||!Path.IsPathRooted(request.output)||!string.Equals(Path.GetExtension(request.output),".mp4",StringComparison.OrdinalIgnoreCase))
            throw new IOException("Invalid replay export request.");
        if(!((request.width==1920&&request.height==1080)||(request.width==2560&&request.height==1440)||(request.width==3840&&request.height==2160))||(request.fps!=30&&request.fps!=60))
            throw new IOException("Invalid video quality settings.");
        Publish("starting");
        if(!ReplayVideoEncoder.TryFind(out string ffmpeg))throw new FileNotFoundException("FFmpeg is unavailable.");
        var load=Task.Run(()=>ReplayRepository.Load(request.replay));
        float deadline=Time.realtimeSinceStartup+90;
        while(!GamePreferences.Ready||!load.IsCompleted) {
            if(File.Exists(request.cancel)) {
                // Let the owned file read close before removing the job copy.
                while(!load.IsCompleted)yield return null;
                Publish("cancelled",GamePreferences.T("已取消导出"));yield break;
            }
            if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Replay export initialization timed out.");
            yield return null;
        }
        var replay=load.GetAwaiter().GetResult();
        // Do not persist rendering preferences from an isolated export process.
        if(GamePreferences.Language!=request.language)GamePreferences.SetLanguage(request.language);
        while(!GamePreferences.Ready)yield return null;
        yield return SceneManager.LoadSceneAsync(ForgeBattlefieldController.SceneName);
        foreach(var notice in FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))notice.enabled=false;
        var board=FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ReplayExportBoard").AddComponent<ForgeBattlefieldController>();
        board.enabled=false;
        int last=-1,rate=Time.captureFramerate;
        var camera=Camera.main??new GameObject("ReplayExportCamera").AddComponent<Camera>();
        var target=new RenderTexture(request.width,request.height,24,RenderTextureFormat.ARGB32);target.Create();
        camera.targetTexture=target;camera.aspect=(float)request.width/request.height;
        var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        var buffer=new byte[pixels.width*pixels.height*3];
        ReplayVideoEncoder encoder=null;
        try {
            encoder=new ReplayVideoEncoder(ffmpeg,request.output,pixels.width,pixels.height,request.fps,request.overwrite);
            Time.captureFramerate=request.fps;
            int count=(int)Math.Ceiling((replay.Duration+2)*request.fps);
            var renderRequest=new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target};
            for(int i=0;i<count;i++) {
                if(File.Exists(request.cancel)){Publish("cancelled",GamePreferences.T("已取消导出"));yield break;}
                int frame=ReplayRepository.FrameAt(replay,Math.Min(replay.Duration,(double)i/request.fps));
                if(frame!=last) {
                    board.PresentReplay(replay.frames[frame].views[request.seat-1],request.seat,replay.players,last<0,replay.frames[frame].views[2-request.seat]);last=frame;
                    foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(canvas.isRootCanvas&&canvas.renderMode==RenderMode.ScreenSpaceOverlay) {
                        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                    }
                    Canvas.ForceUpdateCanvases();
                }
                // Batch players do not draw the presentation framebuffer.
                // Explicitly submit the complete camera stack to the offscreen target.
                yield return null;
                Canvas.ForceUpdateCanvases();
                if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=null)
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,renderRequest);
                else camera.Render();
                var previous=RenderTexture.active;
                try{RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,pixels.width,pixels.height),0,0,false);}
                finally{RenderTexture.active=previous;}
                pixels.GetRawTextureData<byte>().CopyTo(buffer);
                if(i==0) {
                    bool visible=false;for(int pixel=0;pixel<buffer.Length;pixel+=47)if(buffer[pixel]>8){visible=true;break;}
                    if(!visible)throw new IOException(GamePreferences.T("后台渲染未生成有效画面，导出已停止。"));
                }
                var write=encoder.Write(buffer);
                while(!write.IsCompleted)yield return null;
                write.GetAwaiter().GetResult();
                if(i%request.fps==0)Publish("rendering",percent:i*100/count);
            }
            var finish=encoder.Finish();while(!finish.IsCompleted)yield return null;finish.GetAwaiter().GetResult();
            Publish("completed",percent:100);
        }finally {
            encoder?.Dispose();Destroy(pixels);Time.captureFramerate=rate;
            camera.targetTexture=null;target.Release();Destroy(target);
        }
    }
}
