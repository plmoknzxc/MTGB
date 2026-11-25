using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ClientUpdateDialog : MonoBehaviour
{
    readonly CancellationTokenSource lifetime=new CancellationTokenSource();
    Text message;
    Button primary,secondary;
    CanvasGroup menu;
    bool menuWasInteractive,handedOff;
    GameObject returnFocus;

    public static void Show(Transform parent)
    {
        if(parent.GetComponentInChildren<ClientUpdateDialog>(true)!=null)return;
        var overlay=IntegratedUiUtility.Panel(parent,"ClientUpdateDialog",new Color(.025f,.035f,.055f,.72f),true);
        IntegratedUiUtility.Stretch(overlay.rectTransform,0,0,0,0);
        overlay.transform.SetAsLastSibling();
        var dialog=overlay.gameObject.AddComponent<ClientUpdateDialog>();
        dialog.menu=parent.Find("StartMenuContent").GetComponent<CanvasGroup>();
        dialog.menuWasInteractive=dialog.menu.interactable;
        dialog.returnFocus=EventSystem.current?.currentSelectedGameObject;
        dialog.menu.interactable=false;
        var panel=ReplayWidgets.Panel(overlay.transform,"Panel",0,0,650,306,MenuVisualTheme.Panel);
        var heading=ReplayWidgets.Label(panel,"检查更新",0,0,580,68,32);
        MenuVisualTheme.StyleDeckDialogSurface(panel,heading,68);
        dialog.message=ReplayWidgets.Label(panel,"",0,-1,560,120,22,TextAnchor.MiddleCenter);
        dialog.message.horizontalOverflow=HorizontalWrapMode.Wrap;
        dialog.primary=ReplayWidgets.Button(panel,"确定",0,-106,232,()=>{});
        dialog.secondary=ReplayWidgets.Button(panel,"稍后再说",0,-106,232,()=>{});
        dialog.Check();
    }

    void Buttons(string first,Action action,string second=null,Action other=null)
    {
        void Bind(Button button,string label,Action click,float x,bool accent){
            button.gameObject.SetActive(label!=null);
            button.onClick.RemoveAllListeners();
            if(label==null)return;
            button.GetComponentInChildren<Text>().text=label;
            ((RectTransform)button.transform).anchoredPosition=new Vector2(x,-106);
            MenuVisualTheme.StyleDeckTool(button,accent);
            button.onClick.AddListener(()=>click());
        }
        Bind(primary,first,action,second==null?0:125,true);
        Bind(secondary,second,other,-125,false);
        if(first!=null)EventSystem.current?.SetSelectedGameObject(primary.gameObject);
    }

    void Close()=>Destroy(gameObject);

    async void Check()
    {
        var token=lifetime.Token;
        message.text="正在检查更新…";
        Buttons("取消",Close);
        try{
            var release=await ClientReleaseUpdate.CheckAsync(Version.Parse(Application.version),token);
            if(token.IsCancellationRequested)return;
            if(release==null){
                message.text="当前已是最新版本  "+Application.version;
                Buttons("确定",Close);
            }else if(!release.HasPackage){
                message.text="发现版本 "+release.Version+"，Windows 更新包尚未就绪。";
                Buttons("关闭",Close);
            }else{
                message.text="发现新版本  "+Application.version+"  →  "+release.Version+"\n更新包 "+Math.Ceiling(release.Size/1048576d)+" MB，是否更新？";
                if(Application.platform==RuntimePlatform.WindowsPlayer)
                    Buttons("更新并重启",()=>Install(release),"稍后再说",Close);
                else{
                    message.text="发现新版本 "+release.Version+"。请在 Windows 独立客户端中安装更新。";
                    Buttons("确定",Close);
                }
            }
        }catch(Exception ex){
            if(token.IsCancellationRequested)return;
            UnityEngine.Debug.LogWarning("Client update check: "+ex.Message);
            message.text="无法获取版本信息。请检查网络连接，稍后再试。";
            Buttons("重试",Check,"关闭",Close);
        }
    }

    async void Install(ClientReleaseUpdate release)
    {
        var token=lifetime.Token;
        string stage=null;
        try{
            string root=Path.GetDirectoryName(Application.dataPath);
            stage=Path.Combine(root,".mtgb-update-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            message.text="正在连接下载服务器…";
            Buttons("取消下载",Close);
            var progress=new Progress<string>(text=>{if(this && !token.IsCancellationRequested && !handedOff)message.text=text;});
            string[] programEntries=File.ReadAllLines(Path.Combine(Application.streamingAssetsPath,ClientReleaseUpdate.FileList));
            await release.PrepareAsync(stage,programEntries,progress,token);
            token.ThrowIfCancellationRequested();
            string helper=Path.Combine(stage,"InstallClientUpdate.ps1");
            File.Copy(Path.Combine(Application.streamingAssetsPath,"InstallClientUpdate.ps1"),helper);
            var start=new ProcessStartInfo{
                FileName=Path.Combine(Environment.SystemDirectory,@"WindowsPowerShell\v1.0\powershell.exe"),
                Arguments="-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -File \""+helper+"\"",
                WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden
            };
            using var self=Process.GetCurrentProcess();
            start.EnvironmentVariables["MTGB_UPDATE_PID"]=self.Id.ToString();
            using var installer=Process.Start(start);
            var elapsed=Stopwatch.StartNew();
            while(!File.Exists(Path.Combine(stage,"ready"))){
                if(installer.HasExited||elapsed.Elapsed>TotalStartupTime)
                    throw new IOException("无法启动安装程序，请确认游戏目录可写。");
                await Task.Delay(150,token);
            }
            token.ThrowIfCancellationRequested();
            File.WriteAllText(Path.Combine(stage,"commit"),"");
            handedOff=true;
            Buttons(null,null);
            message.text="正在重启并应用更新…";
            Application.Quit();
        }catch(Exception ex){
            if(token.IsCancellationRequested)return;
            UnityEngine.Debug.LogWarning("Client update install: "+ex);
            message.text=ex is InvalidDataException?ex.Message:"更新未完成。请检查网络、磁盘空间和游戏目录的写入权限后重试。";
            Buttons("重试",Check,"关闭",Close);
        }finally{
            if(!handedOff && stage!=null && Directory.Exists(stage)){
                try{await Task.Run(()=>Directory.Delete(stage,true));}catch(IOException ex){UnityEngine.Debug.LogWarning(ex.Message);}
                catch(UnauthorizedAccessException ex){UnityEngine.Debug.LogWarning(ex.Message);}
            }
        }
    }

    static readonly TimeSpan TotalStartupTime=TimeSpan.FromSeconds(25);

    void OnDestroy()
    {
        lifetime.Cancel();
        if(menu!=null)menu.interactable=menuWasInteractive;
        if(returnFocus!=null && returnFocus.activeInHierarchy)EventSystem.current?.SetSelectedGameObject(returnFocus);
    }
}
