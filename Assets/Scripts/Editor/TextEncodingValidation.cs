#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public static class TextEncodingValidation
{
    [Serializable] sealed class Payload { public string text; }
    public static string Run()
    {
        string sample="不可选、繁茂丘陵／カードを選択／English café e\u0301／𠮷🌲\"\\\n第二行\t終わり";
        string json=JsonUtility.ToJson(new Payload{text=string.Concat(Enumerable.Repeat(sample,512))});
        var config=ForgeCardCatalog.ReadConfigForMatch();
        foreach(string legacy in new[]{"GBK","windows-31j"})
        {
            var info=new ProcessStartInfo {
                FileName=Path.Combine(ForgeCardCatalog.Root,config.java),
                Arguments="-Dfile.encoding="+legacy+" -cp \""+Path.GetFullPath(".tools/encoding-tests")+"\" mtgb.bridge.Utf8PipeFixture",
                UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true
            };
            Utf8ProcessIO.Configure(info);
            using var process=new Process{StartInfo=info};process.Start();
            try
            {
                var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
                process.StandardInput.WriteLine(json);process.StandardInput.Close();
                if(!process.WaitForExit(10000))throw new Exception("UTF-8 pipe fixture timed out");
                if(process.ExitCode!=0 || stdout.Result!=json+"\n" || stderr.Result!=json+"\n")throw new Exception("UTF-8 pipe round trip failed: "+legacy);
                if(JsonUtility.FromJson<Payload>(stdout.Result).text!=string.Concat(Enumerable.Repeat(sample,512)))throw new Exception("JSON text changed");
            }
            finally {if(!process.HasExited)process.Kill();}
        }
        using(var writer=new FastBufferWriter(json.Length*2+16,Allocator.Temp))
        {
            writer.WriteValueSafe(json);
            using var reader=new FastBufferReader(writer,Allocator.Temp);
            reader.ReadValueSafe(out string decoded);if(decoded!=json)throw new Exception("Network Unicode round trip failed");
        }
        var replay=new MatchReplay{matchId="unicode",startedUtc=DateTime.UtcNow.ToString("O"),players=new[]{sample,"日本語"},reason=sample};
        replay.frames.Add(new ReplayFrame{time=0,views=new[]{new ForgeSeatView{seat=1,players=Array.Empty<ForgePlayerView>()},new ForgeSeatView{seat=2,players=Array.Empty<ForgePlayerView>()}}});
        var restored=ReplayRepository.Decode(ReplayRepository.Encode(replay));
        if(restored.players[0]!=sample || restored.reason!=sample)throw new Exception("Replay UTF-8 round trip failed");
        string folder="Tools/Validation/Reports/Encoding";Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,"中文-日本語.txt");File.WriteAllText(path,json);
        if(File.ReadAllText(path)!=json)throw new Exception("File Unicode round trip failed");
        string result="PASS: Chinese/Japanese/Latin/emoji/quotes/newlines; C#↔Java stdin/stdout/stderr under GBK and Shift-JIS defaults; split UTF-8 writes; Netcode string serialization; replay encode/decode; Unicode file path and contents.";
        File.WriteAllText(Path.Combine(folder,"result.txt"),result);return result;
    }
}
#endif
