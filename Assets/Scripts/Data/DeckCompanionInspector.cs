using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// Uses the pinned Player.assignCompanion rule path, not a second implementation
// of the ten deck restrictions. UI callers debounce and discard stale results.
public static class DeckCompanionInspector
{
    [Serializable] public sealed class Result{public string[] legal;public string error;public bool Allows(DeckDefinition deck)=>!DeckCompanion.Has(deck)||legal?.Contains(deck.companion.PrintingKey,StringComparer.OrdinalIgnoreCase)==true;}
    [Serializable] sealed class Config{public string session="companion-inspect";public int seed=1,startingLife=20,firstPlayerSeat=1;public bool inspectCompanions=true,commanderCompanionCheck;public DeckDefinition[] decks;}
    static readonly Dictionary<string,Task<Result>> cache=new();
    static readonly System.Threading.SemaphoreSlim worker=new(1,1);
    public static Task<Result> Inspect(DeckDefinition deck)
    {
        // Commanders are part of the starting deck for companion restrictions.
        // Yorion's extra twenty cards cannot fit a fixed-size Commander/Brawl deck.
        bool commander=DeckCommander.IsFormat(deck?.format);
        var projection=new DeckDefinition{name="Companion check",cards=commander?deck.cards.Concat(deck.commanders).ToList():deck.cards,sideboard=deck.sideboard};
        var config=new Config{decks=new[]{projection,projection},commanderCompanionCheck=commander};string json=JsonUtility.ToJson(config);
        if(cache.TryGetValue(json,out var existing)&&(!existing.IsCompleted||existing.Status==TaskStatus.RanToCompletion&&existing.Result.error==null))return existing;
        string root=ForgeCardCatalog.Root;var runtime=ForgeCardCatalog.ReadConfigForMatch(root);
        string parent=Path.GetFullPath(Path.Combine(Application.persistentDataPath,"CompanionInspection"));Directory.CreateDirectory(parent);
        var task=Task.Run(()=>{
            worker.Wait();
            string folder=Path.GetFullPath(Path.Combine(parent,Guid.NewGuid().ToString("N")));
            try{
            Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,"inspect.json");File.WriteAllText(path,json);
            string Quote(string s)=>"\""+s.Replace("\"","\\\"")+"\"";
            var start=new ProcessStartInfo{FileName=Path.Combine(root,runtime.java),WorkingDirectory=Path.Combine(root,runtime.assets),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
                Arguments="-Xmx384m -Djava.awt.headless=true -Dmtgb.bridge.lazyCatalog=true -Dfile.encoding=UTF-8 -Duser.home="+Quote(folder)+" -cp "+Quote(Path.Combine(root,"classes")+Path.PathSeparator+Path.Combine(root,"lib/*"))+" mtgb.bridge.ForgeBridge "+Quote(Path.Combine(root,runtime.assets))+" "+Quote(path)};
            Utf8ProcessIO.Configure(start);start.EnvironmentVariables["APPDATA"]=folder;start.EnvironmentVariables["LOCALAPPDATA"]=folder;
                using var process=Process.Start(start);var output=process.StandardOutput.ReadToEndAsync();var diagnostics=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(25000)){process.Kill();return new Result{error="timeout"};}
                string line=output.GetAwaiter().GetResult().Split('\n').LastOrDefault(l=>l.TrimStart().StartsWith("{\"legal\""));
                return process.ExitCode==0&&line!=null?JsonUtility.FromJson<Result>(line):new Result{error="engine"};
            }catch(Exception){return new Result{error="engine"};}
            finally{try{if(folder.StartsWith(parent+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))Directory.Delete(folder,true);}catch(IOException){}catch(UnauthorizedAccessException){}worker.Release();}
        });
        if(cache.Count>64)cache.Clear();cache[json]=task;return task;
    }
    public static string Status(Result r,bool selectedLegal)=>r?.error!=null?
        DeckCompanion.Text("暂时无法检查行侣，请重试。","Companion check unavailable; retry.","相棒条件を確認できません。再試行してください。"):
        selectedLegal?DeckCompanion.Text("起始牌组满足行侣构筑条件（含主将）。","The starting deck meets this companion's restriction, including commanders.","統率者を含む開始時のデッキは相棒条件を満たしています。"):
        DeckCompanion.Text("起始牌组不满足此行侣的构筑条件（含主将）。","The starting deck does not meet this companion's restriction, including commanders.","統率者を含む開始時のデッキはこの相棒条件を満たしていません。");
}
