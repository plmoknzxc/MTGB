using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    private string puzzleTransferDirectory;

    private void OpenPuzzleFile(RectTransform root, LocalPuzzle exporting, Action<string,bool> transfer)
    {
        bool save = exporting != null;
        var shade = PuzzleModal(root,"PuzzleFilePicker");
        var panel = MenuVisualTheme.ArchiveSection(shade,"Files",save ? "导出残局" : "导入残局",0,0,1050,780,MenuVisualTheme.SideAccent);
        var location = ReplayWidgets.Label(panel,"",-98,259,775,48,20);
        location.GetComponent<PreferenceText>().Literal = true;
        location.resizeTextForBestFit = true; location.resizeTextMinSize = 14; location.resizeTextMaxSize = 20;
        string directory = null;
        string[] entries = Array.Empty<string>();
        int page = 0;
        const int pageSize = 6;
        var rows = new Button[pageSize];
        var labels = new Text[pageSize];
        var feedback = ReplayWidgets.Label(panel,"",0,-272,975,42,20);
        feedback.color = MenuVisualTheme.Warning;
        var filename = PuzzleInput(panel,"PuzzleFilePath",save ? LocalPuzzleStore.SuggestedFilename(exporting) : "",82,-205,805,1024);
        ReplayWidgets.Label(panel,"文件名或路径",-400,-205,175,48,20);
        var pagination = ReplayWidgets.Label(panel,"",0,-139,240,44,19,TextAnchor.MiddleCenter);
        Button prev=null,next=null,up=null;
        prev = PuzzleButton(panel,"上一页",-384,-139,180,()=>{page--;Draw();});
        next = PuzzleButton(panel,"下一页",384,-139,180,()=>{page++;Draw();});
        up = PuzzleButton(panel,"上一级",394,259,180,()=>{
            if(directory!=null) Browse(Directory.GetParent(directory)?.FullName);
        });
        for(int i=0;i<pageSize;i++)
        {
            int slot=i;
            var row=PuzzleButton(panel,"文件",0,192-i*54,970,()=>{
                string path=entries[page*pageSize+slot];
                if(Directory.Exists(path)){ Browse(path); return; }
                filename.text=Path.GetFileName(path);
                feedback.text="";Draw();
            });
            row.name="FileRow"+i;
            var label=row.GetComponentInChildren<Text>();
            label.GetComponent<PreferenceText>().Literal=true;
            label.rectTransform.sizeDelta=new Vector2(923,46);
            label.alignment=TextAnchor.MiddleLeft;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;
            rows[i]=row;labels[i]=label;
        }
        void Draw()
        {
            int pages=Math.Max(1,(entries.Length+pageSize-1)/pageSize);
            pagination.text=entries.Length==0?"此处没有残局文件":$"{page+1} / {pages}";
            prev.interactable=page>0;next.interactable=page+1<pages;
            up.interactable=directory!=null;
            for(int i=0;i<pageSize;i++)
            {
                int index=page*pageSize+i;
                rows[i].gameObject.SetActive(index<entries.Length);
                if(index>=entries.Length)continue;
                string path=entries[index], name=directory==null?path:Path.GetFileName(path);
                bool folder=Directory.Exists(path);
                labels[i].text=folder?"▸   "+name:name;
                MenuVisualTheme.StyleDeckOption(rows[i],!folder && filename.text==name);
            }
        }
        void Browse(string target)
        {
            try
            {
                string[] items = target == null ? Directory.GetLogicalDrives() :
                    Directory.GetDirectories(target).OrderBy(p=>p,StringComparer.CurrentCultureIgnoreCase)
                    .Concat(Directory.GetFiles(target,"*.json").OrderBy(p=>p,StringComparer.CurrentCultureIgnoreCase)).ToArray();
                directory=target; entries=items; page=0;
                location.text=target??"驱动器";
                feedback.text="";
                if(!save)filename.text="";
                Draw();
            }
            catch(Exception error){feedback.text=error.Message;}
        }
        void Finish(string path,bool overwrite)
        {
            try
            {
                transfer(path,overwrite);
                puzzleTransferDirectory=Path.GetDirectoryName(path);
                ClosePuzzleModal(root,shade);
            }
            catch(Exception error){feedback.text=error.Message;}
        }
        void Choose()
        {
            try
            {
                string name=filename.text.Trim().Trim('"');
                if(name.Length==0){feedback.text="请选择 JSON 文件或填写路径。";return;}
                if(directory==null && !Path.IsPathRooted(name)){feedback.text="请先选择驱动器。";return;}
                string path=Path.GetFullPath(Path.Combine(directory??"",name));
                if(Directory.Exists(path)){Browse(path);return;}
                if(save && !Path.HasExtension(path))path+=".json";
                if(!save || !File.Exists(path)){Finish(path,false);return;}
                var confirm=PuzzleModal(shade,"ConfirmOverwrite");
                var box=MenuVisualTheme.ArchiveSection(confirm,"Overwrite","覆盖已有文件？",0,0,740,274,MenuVisualTheme.Warning);
                ReplayWidgets.Label(box,Path.GetFileName(path),0,0,660,77,23);
                PuzzleButton(box,"取消",-148,-88,257,()=>ClosePuzzleModal(shade,confirm)).Select();
                PuzzleButton(box,"确认覆盖",148,-88,257,()=>{ClosePuzzleModal(shade,confirm);Finish(path,true);});
            }
            catch(Exception error){feedback.text=error.Message;}
        }
        PuzzleButton(panel,"取消",158,-339,200,()=>ClosePuzzleModal(root,shade));
        PuzzleButton(panel,save?"导出":"导入",389,-339,195,Choose,true);
        Browse(Directory.Exists(puzzleTransferDirectory)?puzzleTransferDirectory:
            save && Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : LocalPuzzleStore.DirectoryPath);
    }
}
