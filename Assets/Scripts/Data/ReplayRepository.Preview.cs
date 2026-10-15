using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

public sealed class ReplayPreview
{
    public string startedUtc;
    public string[] players;
    public double duration = -1;
    public int gameNumber, bestOf;
}

public static partial class ReplayRepository
{
    private sealed class PreviewCacheEntry
    {
        public long length, ticks;
        public ReplayPreview preview;
    }
    private static readonly Dictionary<string,PreviewCacheEntry> previews=new(StringComparer.OrdinalIgnoreCase);

    // Read only the small header. In particular, never deserialize card views for a preview.
    public static ReplayPreview ReadPreview(string path,bool includeLegacyDuration=false,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();
        var file=new FileInfo(path);long length=file.Length,ticks=file.LastWriteTimeUtc.Ticks;
        if(length>MaxFileBytes)throw new InvalidDataException("录像文件过大。");
        string key=file.FullName;
        lock(previews)if(previews.TryGetValue(key,out var cached)&&cached.length==length&&cached.ticks==ticks
            &&(!includeLegacyDuration||cached.preview.duration>=0))return cached.preview;
        using var input=File.OpenRead(key);
        foreach(byte expected in Magic)if(input.ReadByte()!=expected)throw new InvalidDataException("这不是 MTGB 录像文件。");
        using var gzip=new GZipStream(input,CompressionMode.Decompress);
        using var bounded=new ReplayLimitStream(gzip,MaxJsonBytes);
        using var text=new StreamReader(bounded,Encoding.UTF8,true,4096,true);
        using var reader=new JsonTextReader(text){MaxDepth=64,DateParseHandling=DateParseHandling.None};
        if(!reader.Read()||reader.TokenType!=JsonToken.StartObject)throw new InvalidDataException("录像结构无效。");
        var result=new ReplayPreview();int version=0;bool foundFrames=false;
        while(reader.Read()&&reader.TokenType!=JsonToken.EndObject)
        {
            cancellation.ThrowIfCancellationRequested();
            if(reader.TokenType!=JsonToken.PropertyName)throw new InvalidDataException("录像结构无效。");
            string name=(string)reader.Value;if(!reader.Read())throw new InvalidDataException("录像内容不完整。");
            switch(name)
            {
                case "version":version=Convert.ToInt32(reader.Value,CultureInfo.InvariantCulture);break;
                case "startedUtc":result.startedUtc=reader.Value as string;break;
                case "players":result.players=JsonSerializer.CreateDefault().Deserialize<string[]>(reader);break;
                case "gameNumber":result.gameNumber=Convert.ToInt32(reader.Value,CultureInfo.InvariantCulture);break;
                case "bestOf":result.bestOf=Convert.ToInt32(reader.Value,CultureInfo.InvariantCulture);break;
                case "previewDuration":result.duration=Convert.ToDouble(reader.Value,CultureInfo.InvariantCulture);break;
                case "frames":
                    foundFrames=true;
                    if(reader.TokenType!=JsonToken.StartArray)throw new InvalidDataException("录像时间轴无效。");
                    if(!includeLegacyDuration||result.duration>=0)goto Complete;
                    double duration=-1;int count=0;
                    while(reader.Read()&&reader.TokenType!=JsonToken.EndArray)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if(++count>100000||reader.TokenType!=JsonToken.StartObject)throw new InvalidDataException("录像时间轴无效。");
                        ReadSummaryFields(reader,field=>{if(field=="time")duration=Convert.ToDouble(reader.Value,CultureInfo.InvariantCulture);else reader.Skip();});
                    }
                    result.duration=duration;break;
                default:reader.Skip();break;
            }
        }
        Complete:
        if(version!=1||!foundFrames||result.players==null||result.players.Length!=2
            ||!DateTime.TryParse(result.startedUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out _)
            ||double.IsNaN(result.duration)||double.IsInfinity(result.duration)||result.duration < -1||result.duration>86400)
            throw new InvalidDataException("录像缺失内容或版本不受支持。");
        // File replacement during a background read must not populate the cache under the new identity.
        file.Refresh();
        if(file.Exists&&file.Length==length&&file.LastWriteTimeUtc.Ticks==ticks)
            lock(previews){if(previews.Count>=256)previews.Clear();previews[key]=new PreviewCacheEntry{length=length,ticks=ticks,preview=result};}
        return result;
    }
}
