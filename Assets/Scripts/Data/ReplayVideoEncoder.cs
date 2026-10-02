using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

// Runtime exporter: no UnityEditor/Recorder dependency, bounded to one raw frame in flight.
public sealed class ReplayVideoEncoder : IDisposable
{
    private readonly Process process;
    private readonly string output, temporary;
    private readonly StringBuilder errors = new StringBuilder();
    private bool finished;
    public static bool TryFind(out string executable)
    {
        string local = Path.Combine(PrintingKeyUtility.ApplicationRoot, "VideoTools", "ffmpeg.exe");
        if (File.Exists(local)) { executable = local; return true; }
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            string path = Path.Combine(directory.Trim().Trim('"'), "ffmpeg.exe");
            if (File.Exists(path)) { executable = path; return true; }
        }
        executable = null; return false;
    }
    public ReplayVideoEncoder(string executable, string destination, int width, int height, int fps)
    {
        output = destination; temporary = destination + "." + Guid.NewGuid().ToString("N") + ".partial.mp4";
        process = new Process { StartInfo = new ProcessStartInfo {
            FileName = executable, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardError = true,
            Arguments = "-hide_banner -loglevel error -y -f rawvideo -pixel_format rgb24 -video_size " + width + "x" + height
                + " -framerate " + fps + " -i pipe:0 -an -vf \"vflip,scale=1280:720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2\""
                + " -c:v libx264 -preset fast -crf 20 -pix_fmt yuv420p -movflags +faststart \"" + temporary + "\""
        }};
        Utf8ProcessIO.Configure(process.StartInfo);
        process.ErrorDataReceived += (_, args) => { if (args.Data != null) lock(errors) { if(errors.Length < 8192) errors.AppendLine(args.Data); } };
        try { process.Start(); process.BeginErrorReadLine(); }
        catch { process.Dispose(); throw; }
    }
    public Task Write(byte[] bytes) => process.StandardInput.BaseStream.WriteAsync(bytes, 0, bytes.Length);
    public Task Finish() => Task.Run(() => {
        process.StandardInput.Close();
        if (!process.WaitForExit(30000)) { process.Kill(); throw new IOException("视频编码超时。"); }
        process.WaitForExit();
        if (process.ExitCode != 0) { lock(errors) throw new IOException(errors.ToString()); }
        File.Move(temporary, output); finished = true;
    });
    public void Dispose()
    {
        try { if (!process.HasExited) { process.StandardInput.Close(); if (!process.WaitForExit(1000)) process.Kill(); } }
        catch (InvalidOperationException) { }
        finally { process.Dispose(); if (!finished && File.Exists(temporary)) File.Delete(temporary); }
    }
}
