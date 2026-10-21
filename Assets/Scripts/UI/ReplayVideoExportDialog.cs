using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Select a folder and exact video name before starting a background job.</summary>
public sealed class ReplayVideoExportDialog : MonoBehaviour
{
    private static ReplayVideoExportDialog current;
    private InputField folder,filename;
    private Text status,pageText;
    private Button confirm,previous,next;
    private RectTransform entries;
    private Func<string,bool,int,int,int,bool> start;
    private int resolution,fps=30;
    private string[] directories=Array.Empty<string>();
    private string overwritePath;
    private int page;
    public static void Show(string name,Func<string,bool,int,int,int,bool> onStart)
    {
        if(current!=null)return;
        var canvas=ReplayWidgets.Canvas("ReplayExportDialog",900);
        current=canvas.gameObject.AddComponent<ReplayVideoExportDialog>();
        current.start=onStart;current.Build(name);
    }
    private void Build(string name)
    {
        var shade=ReplayWidgets.Panel(transform,"Shade",0,0,1920,1080,new Color(0,0,0,.75f));
        shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.offsetMin=shade.offsetMax=Vector2.zero;
        var box=ReplayWidgets.Panel(transform,"ExportVideo",0,0,900,780,MenuVisualTheme.Panel);
        ReplayWidgets.Label(box,"导出视频",0,325,820,50,30);
        ReplayWidgets.Label(box,"输出目录",-320,265,180,40,20);
        string initial=PlayerPrefs.GetString("Replay.VideoDirectory",ReplayRepository.VideoRoot);
        try{Directory.CreateDirectory(ReplayRepository.VideoRoot);}catch(Exception e)when(e is IOException||e is UnauthorizedAccessException){}
        if(!Directory.Exists(initial))initial=Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        folder=ReplayWidgets.Input(box,initial,55,265,660);
        folder.characterLimit=32760;
        ReplayWidgets.Button(box,"上级目录",-320,206,160,()=>{try{Browse(Directory.GetParent(folder.text)?.FullName??folder.text);}catch(Exception e){status.text=e.Message;}});
        ReplayWidgets.Button(box,"此电脑",-145,206,160,()=>Browse(null));
        ReplayWidgets.Button(box,"打开目录",30,206,160,()=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo{FileName=Path.GetFullPath(folder.text),UseShellExecute=true});}catch(Exception e){status.text=e.Message;}});
        entries=ReplayWidgets.Rect(box,"Folders",0,45,820,250);
        previous=ReplayWidgets.Button(box,"上一页",-290,-100,150,()=>{page--;RenderDirectories();});
        next=ReplayWidgets.Button(box,"下一页",290,-100,150,()=>{page++;RenderDirectories();});
        pageText=ReplayWidgets.Label(box,"",0,-100,300,40,18,TextAnchor.MiddleCenter);
        ReplayWidgets.Label(box,"文件名",-320,-160,180,42,20);
        filename=ReplayWidgets.Input(box,name+".mp4",55,-160,660);
        ReplayWidgets.Label(box,"画质",-320,-220,180,42,20);
        Button size=null,rate=null;
        size=ReplayWidgets.Button(box,"1080p",-110,-220,250,()=>{resolution=(resolution+1)%3;size.GetComponentInChildren<Text>().text=new[]{"1080p","1440p","4K"}[resolution];});
        rate=ReplayWidgets.Button(box,"30 帧",175,-220,250,()=>{fps=fps==30?60:30;rate.GetComponentInChildren<Text>().text=fps+GamePreferences.T(" 帧");});
        status=ReplayWidgets.Label(box,"",0,-277,820,50,18);status.horizontalOverflow=HorizontalWrapMode.Wrap;
        ReplayWidgets.Button(box,"取消",-210,-342,300,()=>Destroy(gameObject));
        confirm=ReplayWidgets.Button(box,"开始后台导出",180,-342,360,Confirm,true);
        folder.onEndEdit.AddListener(value=>Browse(value));
        folder.onValueChanged.AddListener(_=>ResetOverwrite());filename.onValueChanged.AddListener(_=>ResetOverwrite());
        Browse(folder.text);
    }
    private void ResetOverwrite(){overwritePath=null;if(confirm!=null)confirm.GetComponentInChildren<Text>().text=GamePreferences.T("开始后台导出");}
    private void Browse(string path)
    {
        try {
            directories=path==null?Directory.GetLogicalDrives():Directory.GetDirectories(Path.GetFullPath(path)).OrderBy(p=>p,StringComparer.CurrentCultureIgnoreCase).ToArray();
            if(path!=null)folder.SetTextWithoutNotify(Path.GetFullPath(path));
            page=0;status.text="";ResetOverwrite();RenderDirectories();
        }catch(Exception e){status.text=GamePreferences.T("无法读取目录：")+e.Message;}
    }
    private void RenderDirectories()
    {
        foreach(Transform child in entries){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        int pages=Math.Max(1,(directories.Length+7)/8);page=Mathf.Clamp(page,0,pages-1);
        for(int i=page*8;i<Math.Min(directories.Length,page*8+8);i++) {
            string directory=directories[i];string label=Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar));
            if(string.IsNullOrEmpty(label))label=directory;
            int slot=i-page*8;
            var button=ReplayWidgets.Button(entries,label,slot%2==0?-210:210,90-slot/2*60,400,()=>Browse(directory));
            button.name="Folder_"+i;
        }
        pageText.text=(page+1)+" / "+pages;previous.interactable=page>0;next.interactable=page+1<pages;
    }
    private void Confirm()
    {
        try {
            string directory=Path.GetFullPath(folder.text.Trim());
            if(!Directory.Exists(directory))throw new IOException(GamePreferences.T("输出目录不存在。"));
            string name=filename.text.Trim();
            if(name.EndsWith(".mp4",StringComparison.OrdinalIgnoreCase))name=name.Substring(0,name.Length-4);
            if(string.IsNullOrWhiteSpace(name)||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.EndsWith(".")||name.EndsWith(" "))
                throw new IOException(GamePreferences.T("视频文件名无效。"));
            string destination=Path.Combine(directory,name+".mp4");
            bool overwrite=File.Exists(destination);
            if(overwrite&&overwritePath!=destination){overwritePath=destination;status.text=GamePreferences.T("文件已存在，再次确认将覆盖该视频。");confirm.GetComponentInChildren<Text>().text=GamePreferences.T("确认覆盖并导出");return;}
            if(!start(destination,overwrite,new[]{1920,2560,3840}[resolution],new[]{1080,1440,2160}[resolution],fps)){status.text=GamePreferences.T("无法启动后台导出，请查看任务提示。");return;}
            PlayerPrefs.SetString("Replay.VideoDirectory",directory);PlayerPrefs.Save();Destroy(gameObject);
        }catch(Exception e){status.text=e.Message;}
    }
    private void OnDestroy(){if(current==this)current=null;}
}
