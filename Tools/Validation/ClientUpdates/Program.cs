using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

var installed=new Version("2.0.0");
var wanted=new Version("2.10.0");
var token=CancellationToken.None;
var temp=Directory.CreateTempSubdirectory("MTGB-UpdateTest-");
string[] roots=File.ReadAllLines("Assets/StreamingAssets/ClientProgramFiles.txt");
try{
    byte[] Zip(string extra=null,string version="2.10.0"){
        using var stream=new MemoryStream();
        using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true)){
            var entries=new[]{"MTGB.exe","UnityPlayer.dll","MTGB_Data/globalgamemanagers","MTGB_Data/Managed/Assembly-CSharp.dll",
                "MTGB_Data/StreamingAssets/InstallClientUpdate.ps1","MTGB_Data/StreamingAssets/ClientProgramFiles.txt","MTGB_Data/mtgb-version.txt",
                "MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll","ForgeRuntime/runtime.json","ForgeRuntime/jre/bin/java.exe"};
            foreach(string name in extra==null?entries:entries.Append(extra)){
                using var writer=new StreamWriter(zip.CreateEntry(name).Open());writer.Write(version);
            }
        }
        return stream.ToArray();
    }
    var bytes=Zip();
    string digest="sha256:"+Convert.ToHexString(SHA256.HashData(bytes));
    JObject Entry(string tag,bool pre=false,bool draft=false)=>new(){
        ["tag_name"]=tag,["prerelease"]=pre,["draft"]=draft,["assets"]=new JArray(new JObject{
            ["name"]=ClientReleaseUpdate.AssetName,["state"]="uploaded",["size"]=bytes.Length,["digest"]=digest})};
    var first=new JArray(Enumerable.Range(0,100).Select(i=>Entry("forge-source-"+i)));
    var second=new JArray(Entry("v2.0.0"),Entry("v9.0.0",true),Entry("v8.0.0",draft:true),
        Entry("v2.9.9"),Entry("v2.10.0"),Entry("v3.0.0-beta.1"));
    void Require(bool ok,string check){if(!ok)throw new Exception(check);Console.WriteLine("PASS "+check);}
    using var http=new HttpClient(new Reply(req=>req.RequestUri.Host=="api.github.com"
        ?new StringContent((req.RequestUri.Query.EndsWith("page=2")?second:first).ToString())
        :new ByteArrayContent(bytes)));
    var newer=await ClientReleaseUpdate.CheckAsync(installed,token,http);
    Require(newer.Version==wanted && newer.HasPackage,"stable client version across release pages");
    Require(ClientReleaseUpdate.Newest(second,new Version("3.0.0"),null)==null,"never downgrade");
    var missing=Entry("v2.11.0");missing["assets"]=new JArray();
    Require(!ClientReleaseUpdate.Newest(new JArray(missing),installed,newer).HasPackage,"missing Windows asset is not up to date");
    var stage=temp.CreateSubdirectory("success").FullName;
    await newer.PrepareAsync(stage,roots,new Progress<string>(),token,http);
    Require(File.Exists(Path.Combine(stage,"payload","MTGB.exe")) && !File.Exists(Path.Combine(stage,"client.zip")),"verified download and extraction");
    async Task Reject<T>(Func<Task> act,string check) where T:Exception {
        try{await act();}catch(T){Console.WriteLine("PASS "+check);return;}throw new Exception(check);
    }
    var bad=Entry("v2.10.0");bad["assets"][0]["digest"]="sha256:"+new string('0',64);
    var wrong=ClientReleaseUpdate.Newest(new JArray(bad),installed,null);
    string rejected=temp.CreateSubdirectory("rejected").FullName;
    await Reject<InvalidDataException>(()=>wrong.PrepareAsync(rejected,roots,new Progress<string>(),token,http),"hash mismatch before extraction");
    Require(!Directory.Exists(Path.Combine(rejected,"payload")),"rejected hash leaves install untouched");
    foreach(var pair in new[]{("Decks/user.mdk","2.10.0"),("MTGB_Data/../../escape.txt","2.10.0"),("MTGB_Data/duplicate","2.0.0")}){
        string path=Path.Combine(temp.FullName,"fixture.zip");File.WriteAllBytes(path,Zip(pair.Item1,pair.Item2));
        await Reject<InvalidDataException>(()=>Task.Run(()=>ClientReleaseUpdate.Extract(path,temp.CreateSubdirectory(Guid.NewGuid().ToString()).FullName,wanted,roots,token)),"reject "+pair);
    }
    using var offline=new HttpClient(new Reply(req=>throw new HttpRequestException("Offline")));
    await Reject<HttpRequestException>(()=>ClientReleaseUpdate.CheckAsync(installed,token,offline),"offline is not up to date");
    await Reject<OperationCanceledException>(()=>ClientReleaseUpdate.CheckAsync(installed,new CancellationToken(true),http),"canceled check");
    if(args.Length==2){
        string output=temp.CreateSubdirectory("full-client").FullName;
        ClientReleaseUpdate.Extract(args[0],output,Version.Parse(args[1]),roots,token);
        Require(Directory.EnumerateFiles(output,"*",SearchOption.AllDirectories).Any(),"full release ZIP accepted by client installer");
    }
}finally{temp.Delete(true);}

sealed class Reply(Func<HttpRequestMessage,HttpContent> reply):HttpMessageHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct) {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=reply(req)});
    }
}
