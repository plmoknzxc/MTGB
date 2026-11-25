using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Separate graphics-enabled batch player for deterministic video frames.</summary>
public sealed class ReplayExportWorker : MonoBehaviour
{
    public static bool IsWorker { get; }=!string.IsNullOrEmpty(OnlineClientProfileStore.ReadArgument("-mtgb-replay-export"));
    public static float TextScale { get; private set; }=1;
    internal static string[] ImageRoots { get; private set; }
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
        if(request.seat<1||request.seat>4||!Path.IsPathRooted(request.output)||!string.Equals(Path.GetExtension(request.output),".mp4",StringComparison.OrdinalIgnoreCase))
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
        if(request.seat>replay.players.Length)throw new IOException("Invalid replay perspective.");
        // Do not persist rendering preferences from an isolated export process.
        if(GamePreferences.Language!=request.language)GamePreferences.SetLanguage(request.language);
        while(!GamePreferences.Ready)yield return null;
        if(!string.IsNullOrEmpty(request.themeDirectory) && Path.IsPathFullyQualified(request.themeDirectory) &&
           Directory.Exists(request.themeDirectory) && !string.Equals(request.themeDirectory,MenuThemePackage.Root,StringComparison.OrdinalIgnoreCase))
            MenuThemeCatalog.ReadDirectory(request.themeDirectory);
        var theme=MenuThemeCatalog.Find(request.theme) ?? MenuThemeCatalog.Default;
        if(theme!=null && theme!=MenuThemeCatalog.Current)MenuThemeCatalog.Activate(theme);
        ImageRoots=request.imageRoots?.Where(p=>!string.IsNullOrWhiteSpace(p)&&Path.IsPathFullyQualified(p)).ToArray();
        CardImageLibrary.ClearCache();
        yield return SceneManager.LoadSceneAsync(ReplayPlayback.BattlefieldScene(replay.players.Length));
        var board=FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("ReplayExportBoard").AddComponent<ForgeBattlefieldController>();
        int last=-1,rate=Time.captureFramerate;
        var camera=Camera.main??new GameObject("ReplayExportCamera").AddComponent<Camera>();
        var table=FindFirstObjectByType<BattlefieldWorldTable>();
        var uiCamera=table!=null && table.HudCamera!=null ? table.HudCamera : camera;
        var target=new RenderTexture(request.width,request.height,24,RenderTextureFormat.ARGB32);target.Create();
        camera.targetTexture=target;camera.aspect=(float)request.width/request.height;
        RenderTexture worldTarget=null;
        Canvas backdrop=null;
        var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        var buffer=new byte[pixels.width*pixels.height*3];
        ReplayVideoEncoder encoder=null;
        try {
            // Offscreen URP stack submissions omit camera-space uGUI on the overlay
            // camera. Composite the table behind a separate HUD camera submission.
            if(uiCamera!=camera && GraphicsSettings.currentRenderPipeline!=null) {
                worldTarget=new RenderTexture(request.width,request.height,24,RenderTextureFormat.ARGB32);
                worldTarget.Create();
                camera.targetTexture=worldTarget;
                camera.GetUniversalAdditionalCameraData().cameraStack.Remove(uiCamera);
                uiCamera.GetUniversalAdditionalCameraData().renderType=CameraRenderType.Base;
                uiCamera.targetTexture=target;
                backdrop=new GameObject("Export table backdrop",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
                backdrop.renderMode=RenderMode.ScreenSpaceCamera;
                backdrop.worldCamera=uiCamera;
                backdrop.planeDistance=uiCamera.farClipPlane-1;
                backdrop.sortingOrder=short.MinValue+1;
                var rect=ReplayWidgets.Rect(backdrop.transform,"Table",0,0,0,0);
                rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
                var surface=rect.gameObject.AddComponent<RawImage>();
                surface.texture=worldTarget;surface.raycastTarget=false;
            }
            encoder=new ReplayVideoEncoder(ffmpeg,request.output,pixels.width,pixels.height,request.fps,request.overwrite);
            Time.captureFramerate=request.fps;
            int count=(int)Math.Ceiling((replay.Duration+2)*request.fps);
            var renderRequest=new RenderPipeline.StandardRequest{destination=target};
            var worldRequest=new RenderPipeline.StandardRequest{destination=worldTarget};
            // A camera-space canvas picks up its target size on the next player
            // frame. Placing raised HUD panels before then locks in a stale Z scale.
            yield return null;
            Canvas.ForceUpdateCanvases();
            for(int i=0;i<count;i++) {
                if(File.Exists(request.cancel)){Publish("cancelled",GamePreferences.T("已取消导出"));yield break;}
                int frame=ReplayRepository.FrameAt(replay,Math.Min(replay.Duration,(double)i/request.fps));
                if(frame!=last) {
                    board.PresentReplayFrame(replay,frame,request.seat,last<0);last=frame;
                    foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(canvas.isRootCanvas&&canvas.renderMode==RenderMode.ScreenSpaceOverlay) {
                        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=uiCamera;canvas.planeDistance=1;
                    }
                    Canvas.ForceUpdateCanvases();
                }
                yield return null;
                Canvas.ForceUpdateCanvases();
                if(GraphicsSettings.currentRenderPipeline!=null) {
                    if(worldTarget!=null)RenderPipeline.SubmitRenderRequest(camera,worldRequest);
                    RenderPipeline.SubmitRenderRequest(uiCamera,renderRequest);
                }
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
            if(worldTarget!=null){
                uiCamera.targetTexture=null;
                uiCamera.GetUniversalAdditionalCameraData().renderType=CameraRenderType.Overlay;
                camera.GetUniversalAdditionalCameraData().cameraStack.Add(uiCamera);
                if(backdrop!=null)Destroy(backdrop.gameObject);
                camera.targetTexture=null;worldTarget.Release();Destroy(worldTarget);
            }
            camera.targetTexture=null;target.Release();Destroy(target);
        }
    }
}
