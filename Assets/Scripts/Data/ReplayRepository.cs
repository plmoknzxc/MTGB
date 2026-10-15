using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;

[Serializable] public sealed class ReplayFrame
{
    public double time;
    public ForgeSeatView[] views;
}

[Serializable] public sealed class MatchReplay
{
    public int version = 1, gameNumber = 1, bestOf = 1, winnerSeat;
    public string matchId, startedUtc, reason;
    public string[] players;
    // Optional header field; older recordings omit it. Full playback still validates the timeline.
    public double previewDuration = -1;
    public List<ReplayFrame> frames = new List<ReplayFrame>();
    public double Duration => frames.Count == 0 ? 0 : frames[frames.Count - 1].time;
}

// Records resolved views, so playback does not execute old card scripts again.
public sealed class MatchReplayRecorder
{
    private readonly MatchReplay data;
    private double firstTime = -1;
    public MatchReplayRecorder(string id)
    {
        data = new MatchReplay { matchId = id, startedUtc = DateTime.UtcNow.ToString("O") };
    }
    public void Record(double seconds, ForgeSeatView first, ForgeSeatView second)
    {
        if (first?.players == null || second?.players == null) return;
        if (firstTime < 0) firstTime = seconds;
        // Views are subsequently cleared when a response is submitted. Own a deep copy.
        var frame = new ReplayFrame { time = Math.Max(0, seconds - firstTime), views = new[] { first, second } };
        data.frames.Add(ServerSnapshotJson.Copy(frame));
    }
    public MatchReplay Complete(string[] players, int game, int bestOf, int winner, string reason)
    {
        data.players = players; data.gameNumber = game; data.bestOf = bestOf;
        data.winnerSeat = winner; data.reason = reason;
        return data;
    }
}

public static partial class ReplayRepository
{
    public const string Extension = ".mtgbr";
    public const int MaxFileBytes = 64 * 1024 * 1024;
    private const int MaxJsonBytes = 256 * 1024 * 1024;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("MTGBR1\n");
    public static string Root => Path.Combine(PrintingKeyUtility.ApplicationRoot, "Replays");
    public static string VideoRoot => Path.Combine(Root, "Videos");
    public static event Action Changed;

    public static string DefaultName(MatchReplay replay)
    {
        DateTime date = DateTime.Parse(replay.startedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return date.ToLocalTime().ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture);
    }
    public static string[] ListFiles()
    {
        Directory.CreateDirectory(Root);
        var files = Directory.GetFiles(Root, "*" + Extension);
        Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
        return files;
    }
    public static bool Delete(string path)
    {
        string full = Path.GetFullPath(path);
        if (!string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(Root), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetExtension(full), Extension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("只能删除录像目录中的对局录像。");
        bool existed = File.Exists(full);
        File.Delete(full);
        Changed?.Invoke();
        return existed;
    }
    public static string Save(MatchReplay replay, string name = null, string directory = null)
    {
        return SaveBytes(Encode(replay), name ?? DefaultName(replay), directory ?? Root);
    }
    // The receiver already decoded and validated these immutable bytes. Save
    // them directly rather than serializing/compressing every frame a second time.
    internal static string SaveReceived(byte[] bytes,MatchReplay replay,string directory)
        => SaveBytes(bytes,DefaultName(replay),directory,false);
    internal static void NotifyChanged()=>Changed?.Invoke();
    internal static string SaveEncodedArchive(byte[] bytes,string name,string directory)
        => SaveBytes(bytes,name,directory,false);
    private static string SaveBytes(byte[] bytes, string name, string root,bool notify=true)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0 || name.Length > 120 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name == "." || name == ".." || name.EndsWith("."))
            throw new InvalidDataException("录像名称无效。");
        Directory.CreateDirectory(root);
        for (int suffix = 0; ; suffix++)
        {
            string path = Path.Combine(root, name + (suffix == 0 ? "" : " (" + suffix + ")") + Extension);
            if (File.Exists(path)) continue;
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temp, bytes);
                try { File.Move(temp, path); }
                catch (IOException) when (File.Exists(path)) { continue; }
                if(notify)Changed?.Invoke();
                return path;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
    public static MatchReplay Load(string path)
    {
        if (new FileInfo(path).Length > MaxFileBytes) throw new InvalidDataException("录像文件过大。");
        return Decode(File.ReadAllBytes(path));
    }
    public static byte[] Encode(MatchReplay replay)
    {
        Validate(replay);
        replay.previewDuration=replay.Duration;
        using var output = new MemoryStream();
        output.Write(Magic, 0, Magic.Length);
        using(var compressedLimit=new ReplayLimitStream(output,MaxFileBytes-Magic.Length))
        using (var gzip = new GZipStream(compressedLimit, System.IO.Compression.CompressionLevel.Fastest, true))
        using(var jsonLimit=new ReplayLimitStream(gzip,MaxJsonBytes))
        using(var writer=new StreamWriter(jsonLimit,new UTF8Encoding(false),32768,true))ServerSnapshotJson.WriteTo(writer,replay);
        if (output.Length > MaxFileBytes) throw new InvalidDataException("录像文件过大。");
        return output.ToArray();
    }
    public static MatchReplay Decode(byte[] bytes)
    {
        if (bytes == null || bytes.Length < Magic.Length || bytes.Length > MaxFileBytes)
            throw new InvalidDataException("无效的 MTGB 录像。");
        for (int i = 0; i < Magic.Length; i++)
            if (bytes[i] != Magic[i]) throw new InvalidDataException("这不是 MTGB 录像文件。");
        using var input = new MemoryStream(bytes, Magic.Length, bytes.Length - Magic.Length);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var jsonLimit=new ReplayLimitStream(gzip,MaxJsonBytes);
        using var reader=new StreamReader(jsonLimit,Encoding.UTF8,true,32768,true);
        var replay = ServerSnapshotJson.ReadFrom<MatchReplay>(reader);
        Validate(replay);
        return replay;
    }
    // Network saving needs the header/timeline, not hundreds of full card views.
    // Preserve the original compressed bytes; playback still uses full Decode.
    internal static MatchReplay ReadReceivedSummary(byte[] bytes)
    {
        if(bytes==null||bytes.Length<Magic.Length||bytes.Length>MaxFileBytes)throw new InvalidDataException("无效的 MTGB 录像。");
        for(int i=0;i<Magic.Length;i++)if(bytes[i]!=Magic[i])throw new InvalidDataException("这不是 MTGB 录像文件。");
        using var input=new MemoryStream(bytes,Magic.Length,bytes.Length-Magic.Length);
        using var gzip=new GZipStream(input,CompressionMode.Decompress);
        using var bounded=new ReplayLimitStream(gzip,MaxJsonBytes);
        using var text=new StreamReader(bounded,Encoding.UTF8,true,32768,true);
        using var reader=new JsonTextReader(text){MaxDepth=64,DateParseHandling=DateParseHandling.None};
        if(!reader.Read()||reader.TokenType!=JsonToken.StartObject)throw new InvalidDataException("录像结构无效。");
        var replay=new MatchReplay();
        ReadSummaryFields(reader,(name)=>{
            switch(name){
                case "version":replay.version=Convert.ToInt32(reader.Value,CultureInfo.InvariantCulture);break;
                case "matchId":replay.matchId=reader.Value as string;break;
                case "startedUtc":replay.startedUtc=reader.Value as string;break;
                case "reason":replay.reason=reader.Value as string;break;
                case "gameNumber":replay.gameNumber=Convert.ToInt32(reader.Value,CultureInfo.InvariantCulture);break;
                case "bestOf":replay.bestOf=Convert.ToInt32(reader.Value,CultureInfo.InvariantCulture);break;
                case "winnerSeat":replay.winnerSeat=Convert.ToInt32(reader.Value,CultureInfo.InvariantCulture);break;
                case "players":replay.players=JsonSerializer.CreateDefault().Deserialize<string[]>(reader);break;
                case "frames":
                    if(reader.TokenType!=JsonToken.StartArray)throw new InvalidDataException("录像时间轴无效。");
                    while(reader.Read()&&reader.TokenType!=JsonToken.EndArray){
                        if(replay.frames.Count>=100000||reader.TokenType!=JsonToken.StartObject)throw new InvalidDataException("录像时间轴无效。");
                        var frame=new ReplayFrame();
                        ReadSummaryFields(reader,field=>{
                            if(field=="time")frame.time=Convert.ToDouble(reader.Value,CultureInfo.InvariantCulture);
                            else if(field=="views"){
                                if(reader.TokenType!=JsonToken.StartArray)throw new InvalidDataException("录像玩家视图无效。");
                                var views=new List<ForgeSeatView>();
                                while(reader.Read()&&reader.TokenType!=JsonToken.EndArray){
                                    if(views.Count>=2||reader.TokenType!=JsonToken.StartObject)throw new InvalidDataException("录像玩家视图无效。");
                                    var view=new ForgeSeatView();
                                    ReadSummaryFields(reader,key=>{
                                        if(key=="seat")view.seat=Convert.ToInt32(reader.Value,CultureInfo.InvariantCulture);
                                        else if(key=="players"&&reader.TokenType==JsonToken.StartArray){view.players=Array.Empty<ForgePlayerView>();reader.Skip();}
                                        else reader.Skip();
                                    });
                                    views.Add(view);
                                }
                                frame.views=views.ToArray();
                            }else reader.Skip();
                        });
                        replay.frames.Add(frame);
                    }
                    break;
                default:reader.Skip();break;
            }
        });
        if(reader.Read())throw new InvalidDataException("录像含额外内容。");
        Validate(replay);return replay;
    }
    private static void ReadSummaryFields(JsonTextReader reader,Action<string> readField)
    {
        while(reader.Read()){
            if(reader.TokenType==JsonToken.EndObject)return;
            if(reader.TokenType!=JsonToken.PropertyName)throw new InvalidDataException("录像结构无效。");
            string name=(string)reader.Value;
            if(!reader.Read())throw new InvalidDataException("录像内容不完整。");
            readField(name);
        }
        throw new InvalidDataException("录像内容不完整。");
    }

    private sealed class ReplayLimitStream:Stream
    {
        private readonly Stream inner;private readonly long limit;private long used;
        public ReplayLimitStream(Stream inner,long limit){this.inner=inner;this.limit=limit;}
        private void Count(int size){used+=size;if(used>limit)throw new InvalidDataException("录像内容过大。");}
        public override int Read(byte[] buffer,int offset,int count){int n=inner.Read(buffer,offset,count);Count(n);return n;}
        public override void Write(byte[] buffer,int offset,int count){Count(count);inner.Write(buffer,offset,count);}
        public override void Flush()=>inner.Flush();
        public override bool CanRead=>inner.CanRead;public override bool CanWrite=>inner.CanWrite;public override bool CanSeek=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>used;set=>throw new NotSupportedException();}
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
        // The caller owns the underlying gzip/file stream and closes it in order.
    }
    public static void Validate(MatchReplay replay)
    {
        if (replay == null || replay.version != 1 || string.IsNullOrWhiteSpace(replay.matchId)
            || !DateTime.TryParse(replay.startedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)
            || replay.players == null || replay.players.Length != 2 || replay.frames == null
            || replay.frames.Count == 0 || replay.frames.Count > 100000 || replay.winnerSeat < 0 || replay.winnerSeat > 2)
            throw new InvalidDataException("录像缺失内容或版本不受支持。");
        double previous = -1;
        foreach (var frame in replay.frames)
        {
            if (frame == null || double.IsNaN(frame.time) || double.IsInfinity(frame.time) || frame.time < previous
                || frame.time < 0 || frame.time > 86400 || frame.views == null || frame.views.Length != 2)
                throw new InvalidDataException("录像时间轴无效。");
            for (int i = 0; i < 2; i++)
                if (frame.views[i]?.seat != i + 1 || frame.views[i].players == null)
                    throw new InvalidDataException("录像玩家视图无效。");
            previous = frame.time;
        }
    }
    public static int FrameAt(MatchReplay replay, double seconds)
    {
        int low = 0, high = replay.frames.Count - 1;
        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            if (replay.frames[mid].time <= seconds) low = mid; else high = mid - 1;
        }
        return low;
    }
}
