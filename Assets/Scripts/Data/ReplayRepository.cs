using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

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
        data.frames.Add(JsonUtility.FromJson<ReplayFrame>(JsonUtility.ToJson(frame)));
    }
    public MatchReplay Complete(string[] players, int game, int bestOf, int winner, string reason)
    {
        data.players = players; data.gameNumber = game; data.bestOf = bestOf;
        data.winnerSeat = winner; data.reason = reason;
        return data;
    }
}

public static class ReplayRepository
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
    public static string Save(MatchReplay replay, string name = null, string directory = null)
    {
        return SaveBytes(Encode(replay), name ?? DefaultName(replay), directory ?? Root);
    }
    private static string SaveBytes(byte[] bytes, string name, string root)
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
                Changed?.Invoke();
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
        byte[] json = Encoding.UTF8.GetBytes(JsonUtility.ToJson(replay));
        if (json.Length > MaxJsonBytes) throw new InvalidDataException("录像内容过大。");
        using var output = new MemoryStream();
        output.Write(Magic, 0, Magic.Length);
        using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Fastest, true)) gzip.Write(json, 0, json.Length);
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
        using var output = new MemoryStream();
        var buffer = new byte[32768];
        int count;
        while ((count = gzip.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + count > MaxJsonBytes) throw new InvalidDataException("录像解压后过大。");
            output.Write(buffer, 0, count);
        }
        var replay = JsonUtility.FromJson<MatchReplay>(Encoding.UTF8.GetString(output.ToArray()));
        Validate(replay);
        return replay;
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
