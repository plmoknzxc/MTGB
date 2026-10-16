using System;
using System.IO;
using System.IO.Compression;
using System.Text;

// Freeze each view before the authority clears its request, without retaining
// a second object graph for every action of a long match.
public sealed class MatchReplayRecorder : IDisposable
{
    private readonly MatchReplay metadata;
    private readonly string directory;
    private string framesPath;
    private StreamWriter frames;
    private double firstTime=-1, duration;
    private long jsonBytes;
    private int frameCount;
    private bool finished;
    public string Failure { get; private set; }

    public MatchReplayRecorder(string id,string temporaryDirectory=null)
    {
        metadata=new MatchReplay{matchId=id,startedUtc=DateTime.UtcNow.ToString("O")};
        directory=temporaryDirectory??Path.Combine(Path.GetTempPath(),"MTGB","ReplayFrames");
    }

    public void Record(double seconds,ForgeSeatView first,ForgeSeatView second)
    {
        if(finished||Failure!=null||first?.players==null||second?.players==null)return;
        try
        {
            if(firstTime<0)firstTime=seconds;
            double time=Math.Max(0,seconds-firstTime);
            if(double.IsNaN(time)||double.IsInfinity(time)||time<duration||time>86400
                ||first.seat!=1||second.seat!=2||frameCount>=100000)
                throw new InvalidDataException("录像时间轴无效。");
            string json=ServerSnapshotJson.SerializeReplayFrame(new ReplayFrame{time=time,views=new[]{first,second}});
            jsonBytes+=Encoding.UTF8.GetByteCount(json)+1;
            if(jsonBytes>256L*1024*1024)throw new InvalidDataException("录像内容过大。");
            if(frames==null)
            {
                Directory.CreateDirectory(directory);
                framesPath=Path.Combine(directory,Guid.NewGuid().ToString("N")+".frames.gz");
                var file=new FileStream(framesPath,FileMode.CreateNew,FileAccess.Write,FileShare.None,32768);
                frames=new StreamWriter(new GZipStream(file,CompressionLevel.Fastest),new UTF8Encoding(false),32768);
            }
            frames.WriteLine(json);
            duration=time;frameCount++;
        }
        catch(Exception error)when(error is IOException||error is UnauthorizedAccessException||error is InvalidDataException)
        {
            // A failed recording must not abort an otherwise playable match.
            Failure=error.Message;
            try{Dispose();}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }

    public RecordedMatchReplay Complete(string[] players,int game,int bestOf,int winner,string reason)
    {
        if(Failure!=null)throw new InvalidDataException("录像记录失败："+Failure);
        if(finished)throw new InvalidOperationException("录像已经结束。");
        frames?.Dispose();frames=null;finished=true;
        metadata.players=players;metadata.gameNumber=game;metadata.bestOf=bestOf;
        metadata.winnerSeat=winner;metadata.reason=reason;metadata.previewDuration=duration;
        var result=new RecordedMatchReplay(metadata,framesPath,frameCount,duration);
        framesPath=null; // Ownership transfers to the completed recording.
        return result;
    }

    public void Dispose()
    {
        finished=true;
        try{frames?.Dispose();}
        finally
        {
            frames=null;
            if(framesPath!=null)
            {
                string path=framesPath;framesPath=null;
                File.Delete(path);
            }
        }
    }
}

// Transport and archival consume the compressed recording directly. Only
// explicit playback decodes the complete timeline into MatchReplay objects.
public sealed class RecordedMatchReplay : IDisposable
{
    internal MatchReplay Metadata { get; }
    private string framesPath;
    public int FrameCount { get; }
    public double Duration { get; }
    internal RecordedMatchReplay(MatchReplay metadata,string path,int count,double duration)
    {
        Metadata=metadata;framesPath=path;FrameCount=count;Duration=duration;
    }
    public byte[] Encode()
    {
        if(framesPath==null||FrameCount==0)throw new InvalidDataException("录像尚无可回放画面。");
        return ReplayRepository.EncodeRecording(Metadata,framesPath,FrameCount);
    }
    public void Dispose()
    {
        if(framesPath==null)return;
        string path=framesPath;framesPath=null;File.Delete(path);
    }
}
