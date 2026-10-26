using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public static class DeckCommanderInspector
{
    [Serializable] public sealed class Result { public string[] legal,partners;public string problem,error;public int identity; }
    [Serializable] sealed class Config { public string format;public List<DeckEntry> cards,sideboard,commanders,candidates; }
    static readonly Dictionary<string,Task<Result>> cache=new();
    static readonly System.Threading.SemaphoreSlim worker=new(1,1);
    public static Task<Result> Inspect(DeckDefinition deck,CardDefinition candidate=null)
    {
        var config=new Config{format=deck.format,cards=deck.cards,sideboard=deck.sideboard,commanders=deck.commanders,candidates=new()};
        if(candidate!=null&&!candidate.customContent&&!candidate.forgeAmbiguous)config.candidates.Add(new DeckEntry{setCode=candidate.setCode,collectorNumber=candidate.CollectorNumber,count=1});
        string json=JsonUtility.ToJson(config);
        if(cache.TryGetValue(json,out var existing)&&(!existing.IsCompleted||existing.Status==TaskStatus.RanToCompletion&&existing.Result.error==null))return existing;
        string root=ForgeCardCatalog.Root;var runtime=ForgeCardCatalog.ReadConfigForMatch(root);
        string parent=Path.GetFullPath(Path.Combine(Application.persistentDataPath,"CommanderInspection"));Directory.CreateDirectory(parent);
        var task=Task.Run(async()=>{
            await worker.WaitAsync().ConfigureAwait(false);
            string folder=Path.Combine(parent,Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(folder);string path=Path.Combine(folder,"inspect.json");File.WriteAllText(path,json);
                string Quote(string s)=>"\""+s.Replace("\"","\\\"")+"\"";
                var start=new ProcessStartInfo{FileName=Path.Combine(root,runtime.java),WorkingDirectory=Path.Combine(root,runtime.assets),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
                    Arguments="-Xmx384m -Djava.awt.headless=true -Dfile.encoding=UTF-8 -Duser.home="+Quote(folder)+" -cp "+Quote(Path.Combine(root,"classes")+Path.PathSeparator+Path.Combine(root,"lib/*"))+" mtgb.bridge.CommanderDeckInspector "+Quote(Path.Combine(root,runtime.assets))+" "+Quote(path)};
                Utf8ProcessIO.Configure(start);start.EnvironmentVariables["APPDATA"]=folder;start.EnvironmentVariables["LOCALAPPDATA"]=folder;
                using var process=Process.Start(start);var output=process.StandardOutput.ReadToEndAsync();var diagnostics=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(25000)){process.Kill();return new Result{error="timeout"};}
                string line=(await output.ConfigureAwait(false)).Split('\n').LastOrDefault(l=>l.TrimStart().StartsWith("{\"legal\""));
                await diagnostics.ConfigureAwait(false);
                return process.ExitCode==0&&line!=null?JsonUtility.FromJson<Result>(line):new Result{error="engine"};
            } catch(Exception){return new Result{error="engine"};}
            finally {try{Directory.Delete(folder,true);}catch(IOException){}catch(UnauthorizedAccessException){}worker.Release();}
        });
        if(cache.Count>32)cache.Clear();cache[json]=task;return task;
    }
    public static string Status(Result result)
    {
        string T(string zh,string en,string ja)=>DeckCompanion.Text(zh,en,ja);
        if(result==null)return T("正在检查主将与构筑条件…","Checking commanders and construction…","統率者と構築条件を確認中…");
        if(result.error!=null)return T("暂时无法检查主将，请重试。","Commander check unavailable; retry.","統率者を確認できません。再試行してください。");
        return result.problem switch {
            ""=>T("符合主将构筑条件；禁牌表另行标示。","Meets commander construction rules; bans are marked separately.","統率者の構築条件を満たしています。禁止カードは別途表示します。"),
            "missing"=>T("尚未指定主将。","No commander designated.","統率者が未指定です。"),
            "identity"=>T("牌组包含不符合主将标识色的牌。","The deck contains cards outside its commanders' color identity.","統率者の固有色に合わないカードがあります。"),
            "partners"=>T("这两张牌不能共同担任主将。","These cards cannot be partner commanders.","この２枚を共に統率者にはできません。"),
            "commander"=>T("当前主将不符合资格。","The commander is not eligible.","統率者の資格を満たしていません。"),
            "copies"=>T("同名牌数量超过构筑上限。","A card exceeds its copy limit.","同名カードが枚数制限を超えています。"),
            "sideboard"=>T("备牌数量不符合构筑条件。","Sideboard size does not meet construction rules.","サイドボードの枚数が構築条件を満たしていません。"),
            "size"=>T("牌组必须包含主将在内共 100 张牌。","The deck must contain 100 cards including commanders.","統率者を含め100枚のデッキが必要です。"),
            _=>T("牌组尚不符合主将构筑条件。","The deck does not meet commander construction rules.","統率者の構築条件を満たしていません。")
        };
    }
}
