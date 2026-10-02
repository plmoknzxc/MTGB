using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

// Run in Play Mode, once the menu is ready, to measure full reads rather than warm static caches.
public static class StartupLoadValidation
{
    [MenuItem("MTGB/验证/启动加载")]
    public static async void Run()
    {
        if (!Application.isPlaying) { UnityEngine.Debug.LogError("Run startup validation in Play Mode."); return; }
        string root=ForgeCardCatalog.Root;
        string flavor=Path.Combine(Application.streamingAssetsPath,"CardFlavor.jsonl");
        string scratch=Path.GetFullPath(".tools/startup-validation/"+Guid.NewGuid().ToString("N"));
        SessionState.SetString("Startup.Validation","running");
        try
        {
            string result=await Task.Run(()=>
            {
                var report=new List<string>();
                for(int i=0;i<3;i++)
                {
                    ForgeCardCatalog.InvalidateVerification();
                    var sw=Stopwatch.StartNew();
                    var cards=ForgeCardCatalog.Load(root,new List<string>(),CancellationToken.None,null);
                    CardFlavorCatalog.Apply(cards,flavor,CancellationToken.None);
                    if(cards.Count!=97120) throw new Exception("Unexpected catalog count: "+cards.Count);
                    report.Add("Full load "+(i+1)+": "+sw.ElapsedMilliseconds+" ms, "+cards.Count+" printings");
                }
                foreach(string dir in new[]{"classes","lib","assets/res","jre"}) Directory.CreateDirectory(Path.Combine(scratch,dir));
                string asset=Path.Combine(scratch,"classes/test.txt");File.WriteAllText(asset,"original");
                string catalog=Path.Combine(scratch,"catalog.jsonl");
                using(var reader=File.OpenText(Path.Combine(root,"catalog.jsonl"))) File.WriteAllText(catalog,reader.ReadLine()+"\n");
                string manifest=Path.Combine(scratch,"files.json");
                File.WriteAllText(manifest,"{\"files\":[{\"path\":\"classes/test.txt\",\"sha256\":\""+Hash(asset)+"\"}]}");
                var config=new ForgeCardCatalog.RuntimeConfig {java="jre/bin/java.exe",assets="assets",revision=ForgeCardCatalog.Revision,protocol=ForgeCardCatalog.Protocol,catalogHash=Hash(catalog),runtimeHash=Hash(manifest)};
                File.WriteAllText(Path.Combine(scratch,"runtime.json"),JsonUtility.ToJson(config));
                Action load=()=>{ForgeCardCatalog.InvalidateVerification();ForgeCardCatalog.Load(scratch,new List<string>(),CancellationToken.None,null);};
                load();report.Add("Valid fixture: PASS");
                File.WriteAllText(asset,"modified");Reject(load,"Changed runtime file",report);File.WriteAllText(asset,"original");
                File.Move(asset,asset+".moved");Reject(load,"Missing runtime file",report);File.Move(asset+".moved",asset);
                string extra=Path.Combine(scratch,"lib/extra.txt");File.WriteAllText(extra,"extra");Reject(load,"Unlisted runtime file",report);File.Delete(extra);
                string originalCatalog=File.ReadAllText(catalog);File.WriteAllText(catalog,originalCatalog+" ");Reject(load,"Changed catalog",report);File.WriteAllText(catalog,originalCatalog);
                string originalManifest=File.ReadAllText(manifest);File.WriteAllText(manifest,originalManifest+" ");Reject(load,"Changed manifest",report);File.WriteAllText(manifest,originalManifest);
                using(var cancel=new CancellationTokenSource())
                {
                    cancel.Cancel();
                    try {ForgeCardCatalog.Load(scratch,new List<string>(),cancel.Token,null);throw new Exception("Cancellation ignored");}
                    catch(OperationCanceledException) {report.Add("Cancellation: PASS");}
                }
                load();report.Add("Retry after failures: PASS");ForgeCardCatalog.InvalidateVerification();
                return string.Join("\n",report);
            });
            SessionState.SetString("Startup.Validation",result);UnityEngine.Debug.Log(result);
        }
        catch(Exception error) {SessionState.SetString("Startup.Validation",error.ToString());UnityEngine.Debug.LogException(error);}
    }
    static void Reject(Action load,string name,List<string> report)
    {
        try {load();} catch(Exception error) when(error is IOException || error is InvalidDataException) {report.Add(name+": PASS");return;}
        throw new Exception(name+" was incorrectly accepted");
    }
    static string Hash(string path)
    {
        using(var sha=SHA256.Create()) using(var file=File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
    }
}
