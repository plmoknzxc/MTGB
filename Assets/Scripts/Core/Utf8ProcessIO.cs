using System.Diagnostics;
using System.Text;

/// <summary>Text pipe contract: UTF-8 without BOM, independent of Windows code pages.</summary>
public static class Utf8ProcessIO
{
    public static void Configure(ProcessStartInfo info)
    {
        var encoding=new UTF8Encoding(false);
        if(info.RedirectStandardInput)info.StandardInputEncoding=encoding;
        if(info.RedirectStandardOutput)info.StandardOutputEncoding=encoding;
        if(info.RedirectStandardError)info.StandardErrorEncoding=encoding;
    }
}
