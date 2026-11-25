using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

internal sealed class ClientReleaseUpdate
{
    internal const string AssetName="MTGB-Windows-x64.zip";
    const string Repository="plmoknzxc/MTGB";
    const string ReleasesApi="https://api.github.com/repos/"+Repository+"/releases";
    static readonly HttpClient Client=new HttpClient {Timeout=TimeSpan.FromSeconds(30)};
    static readonly Regex StableTag=new Regex(@"^v?(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})$");
    internal const string FileList="ClientProgramFiles.txt";
    internal readonly Version Version;
    internal readonly string Url,Digest;
    internal readonly long Size;

    ClientReleaseUpdate(Version version,string tag,JObject asset)
    {
        Version=version;
        Url="https://github.com/"+Repository+"/releases/download/"+tag+"/"+AssetName;
        Size=(long?)asset?["size"]??0;
        Digest=(string)asset?["digest"];
    }

    internal bool HasPackage => Size>0 && Regex.IsMatch(Digest??"", @"^sha256:[a-fA-F0-9]{64}$");

    // This repository also publishes Forge source archives. Only stable client
    // version tags participate, regardless of publication order or GitHub's "latest" flag.
    internal static ClientReleaseUpdate Newest(JArray page,Version installed,ClientReleaseUpdate newest)
    {
        foreach(var release in page.OfType<JObject>()){
            string tag=(string)release["tag_name"]??"";
            if((bool?)release["draft"]!=false || (bool?)release["prerelease"]!=false || !StableTag.IsMatch(tag))continue;
            var version=System.Version.Parse(tag.TrimStart('v'));
            if(version<=installed || newest!=null && version<=newest.Version)continue;
            var assets=release["assets"] as JArray;
            var asset=assets?.OfType<JObject>().FirstOrDefault(a=>(string)a["name"]==AssetName && (string)a["state"]=="uploaded");
            newest=new ClientReleaseUpdate(version,tag,asset);
        }
        return newest;
    }

    internal static async Task<ClientReleaseUpdate> CheckAsync(Version installed,CancellationToken token,HttpClient transport=null)
    {
        ClientReleaseUpdate newest=null;
        for(int page=1;;page++){
            using var req=new HttpRequestMessage(HttpMethod.Get,ReleasesApi+"?per_page=100&page="+page.ToString(CultureInfo.InvariantCulture));
            req.Headers.UserAgent.ParseAdd("MTGB/"+installed);
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response=await (transport??Client).SendAsync(req,token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var releases=JArray.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            newest=Newest(releases,installed,newest);
            if(releases.Count<100)return newest;
        }
    }

    internal async Task PrepareAsync(string stage,string[] programEntries,IProgress<string> progress,CancellationToken token,HttpClient transport=null)
    {
        string zip=Path.Combine(stage,"client.zip");
        using(var request=new HttpRequestMessage(HttpMethod.Get,Url)){
            request.Headers.UserAgent.ParseAdd("MTGB-Updater/2");
            using var response=await (transport??Client).SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var file=new FileStream(zip,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true);
            using var sha=SHA256.Create();
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);
            byte[] buffer=new byte[131072];
            long received=0;int previous=-1;
            while(true){
                timeout.CancelAfter(TimeSpan.FromSeconds(60));
                int n=await input.ReadAsync(buffer,0,buffer.Length,timeout.Token).ConfigureAwait(false);
                if(n==0)break;
                received+=n;
                if(received>Size)throw new InvalidDataException("更新包超过发布大小。");
                sha.TransformBlock(buffer,0,n,buffer,0);
                await file.WriteAsync(buffer,0,n,token).ConfigureAwait(false);
                int percent=(int)(received*100/Size);
                if(previous!=percent){previous=percent;progress.Report("正在下载更新包  "+percent+"%");}
            }
            sha.TransformFinalBlock(Array.Empty<byte>(),0,0);
            string actual="sha256:"+BitConverter.ToString(sha.Hash).Replace("-","");
            if(received!=Size || !string.Equals(actual,Digest,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("更新包校验失败，请重新下载。");
        }
        progress.Report("正在准备安装文件…");
        await Task.Run(()=>Extract(zip,Path.Combine(stage,"payload"),Version,programEntries,token),token).ConfigureAwait(false);
        File.Delete(zip);
    }

    internal static void Extract(string zip,string payload,Version expected,string[] programEntries,CancellationToken token)
    {
        Directory.CreateDirectory(payload);
        using var archive=ZipFile.OpenRead(zip);
        long total=0;
        foreach(var entry in archive.Entries){
            token.ThrowIfCancellationRequested();
            string name=entry.FullName;
            string[] parts=name.Split('/');
            if(parts.Any(p=>string.IsNullOrEmpty(p)||p=="."||p==".."||p.EndsWith(".")||p.EndsWith(" ")||
                p.IndexOfAny(Path.GetInvalidFileNameChars())>=0||p.IndexOfAny(new[]{'\\',':'})>=0) ||
                !programEntries.Contains(parts[0],StringComparer.Ordinal))
                throw new InvalidDataException("更新包包含无法安装的路径。");
            total=checked(total+entry.Length);
            if(total>12L*1024*1024*1024)throw new InvalidDataException("更新包解压大小异常。");
            string path=Path.Combine(payload,Path.Combine(parts));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write);
            using var input=entry.Open();
            input.CopyTo(output);
        }
        foreach(string required in new[]{"MTGB.exe","UnityPlayer.dll","MTGB_Data/globalgamemanagers","MTGB_Data/Managed/Assembly-CSharp.dll",
                "MTGB_Data/StreamingAssets/InstallClientUpdate.ps1","MTGB_Data/StreamingAssets/"+FileList,
                "MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll","ForgeRuntime/runtime.json","ForgeRuntime/jre/bin/java.exe"}){
            if(!File.Exists(Path.Combine(payload,required)))throw new InvalidDataException("更新包缺少程序文件："+required);
        }
        if(File.ReadAllText(Path.Combine(payload,"MTGB_Data","mtgb-version.txt")).Trim()!=expected.ToString())
            throw new InvalidDataException("更新包中的版本与发布版本不一致。");
    }
}
