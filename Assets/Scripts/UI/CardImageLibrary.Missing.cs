using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

public static partial class CardImageLibrary
{
    private static readonly Dictionary<string,double> MissingImages=new(StringComparer.Ordinal);
    private static readonly Dictionary<string,bool> ImageDirectories=new(StringComparer.OrdinalIgnoreCase);
    private static int directoryRevision;
    private static int imageRevision,missingRevision;
    private static double nextDirectoryRefresh;
    private static string[] imageRoots;

    // Download/import callers invalidate synchronously before refreshing their
    // views. A short retry also discovers files copied in outside the application.
    public static void InvalidateMissingImages()=>Interlocked.Increment(ref imageRevision);

    private static bool ImageDirectoryExists(string path)
    {
        int revision=Volatile.Read(ref imageRevision);
        double now=Time.realtimeSinceStartupAsDouble;
        if(directoryRevision!=revision||now>=nextDirectoryRefresh){ImageDirectories.Clear();directoryRevision=revision;nextDirectoryRefresh=now+1;}
        if(ImageDirectories.TryGetValue(path,out bool exists))return exists;
        if(ImageDirectories.Count>=4096)ImageDirectories.Clear();
        return ImageDirectories[path]=Directory.Exists(path);
    }

    private static bool IsKnownMissingImage(string key)
    {
        int revision=Volatile.Read(ref imageRevision);
        if(missingRevision!=revision){MissingImages.Clear();missingRevision=revision;return false;}
        return MissingImages.TryGetValue(key,out double retry)&&Time.realtimeSinceStartupAsDouble<retry;
    }
    private static void RememberMissingImage(string key,int revision)
    {
        // A download may complete while a long fallback scan is in progress.
        // Never remember a miss across that image-arrival notification.
        if(Volatile.Read(ref imageRevision)!=revision)return;
        if(MissingImages.Count>=4096)MissingImages.Clear();
        missingRevision=revision;
        MissingImages[key]=Time.realtimeSinceStartupAsDouble+1;
    }
    private static string[] ImageRoots()
    {
        if(imageRoots!=null)return imageRoots;
        string external=ExternalRoot,persistent=PersistentRoot;
        return imageRoots=string.Equals(external,persistent,StringComparison.OrdinalIgnoreCase)
            ?new[]{external,AlternateExternalRoot}:new[]{external,AlternateExternalRoot,persistent};
    }
    private static void ResetMissingImages()
    {
        MissingImages.Clear();ImageDirectories.Clear();nextDirectoryRefresh=0;imageRoots=null;
        missingRevision=Volatile.Read(ref imageRevision);
    }
}
