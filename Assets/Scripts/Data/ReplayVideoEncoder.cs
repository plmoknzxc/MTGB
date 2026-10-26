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
    private readonly bool overwrite;
    public static bool TryFind(out string executable)
    {
        string local = Path.Combine(PrintingKeyUtility.ApplicationRoot, "VideoTools", "ffmpeg.exe");
        if (File.Exists(local)) { executable = local; return true; }
        // A running desktop app can retain PATH from before FFmpeg was installed.
        // Read the persisted Windows paths as well; workers use the same lookup.
        foreach (var scope in new[]{EnvironmentVariableTarget.Process,EnvironmentVariableTarget.User,EnvironmentVariableTarget.Machine})
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH",scope) ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            string path = Path.Combine(Environment.ExpandEnvironmentVariables(directory.Trim().Trim('"')), "ffmpeg.exe");
            if (File.Exists(path)) { executable = path; return true; }
        }
        executable = null; return false;
    }
    public ReplayVideoEncoder(string executable, string destination, int width, int height, int fps,bool replaceExisting=false)
    {
        overwrite=replaceExisting;
        output = destination; temporary = destination + "." + Guid.NewGuid().ToString("N") + ".partial.mp4";
        process = new Process { StartInfo = new ProcessStartInfo {
            FileName = executable, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardError = true,
            Arguments = "-hide_banner -loglevel error -y -f rawvideo -pixel_format rgb24 -video_size " + width + "x" + height
                + " -framerate " + fps + " -i pipe:0 -an -vf \"vflip,scale=out_color_matrix=bt709:out_range=tv\""
                + " -c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -color_range tv -colorspace bt709 -color_primaries bt709 -color_trc bt709 -movflags +faststart \"" + temporary + "\""
        }};
        Utf8ProcessIO.Configure(process.StartInfo);
        process.ErrorDataReceived += (_, args) => { if (args.Data != null) lock(errors) { if(errors.Length < 8192) errors.AppendLine(args.Data); } };
        try { process.Start(); process.BeginErrorReadLine(); }
        catch { process.Dispose(); throw; }
    }
    public async Task Write(byte[] bytes)
    {
        // A 4K RGB frame exceeds the per-write quota of Windows anonymous pipes.
        // Keep one frame in flight, with bounded writes and no additional copies.
        const int chunk=1024*1024;
        var input=process.StandardInput.BaseStream;
        for(int offset=0;offset<bytes.Length;offset+=chunk)
            await input.WriteAsync(bytes,offset,Math.Min(chunk,bytes.Length-offset)).ConfigureAwait(false);
    }
    public Task Finish() => Task.Run(() => {
        process.StandardInput.Close();
        if (!process.WaitForExit(30000)) { process.Kill(); throw new IOException("视频编码超时。"); }
        process.WaitForExit();
        if (process.ExitCode != 0) { lock(errors) throw new IOException(errors.ToString()); }
        if(overwrite&&File.Exists(output))File.Replace(temporary,output,null);else File.Move(temporary, output);
        finished = true;
    });
    public void Dispose()
    {
        try { if (!process.HasExited) { process.StandardInput.Close(); if (!process.WaitForExit(1000)) process.Kill(); } }
        catch (InvalidOperationException) { }
        finally { process.Dispose(); if (!finished && File.Exists(temporary)) File.Delete(temporary); }
    }
}
