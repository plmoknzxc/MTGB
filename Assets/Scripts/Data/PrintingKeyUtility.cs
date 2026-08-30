using System;
using System.IO;
using UnityEngine;

public static class PrintingKeyUtility
{
    public static string ApplicationRoot
    {
        get => ResolveApplicationRoot(Application.dataPath);
    }

    public static string ResolveApplicationRoot(string dataPath)
    {
        DirectoryInfo dataDirectory = new DirectoryInfo(Path.GetFullPath(dataPath));
        DirectoryInfo candidate = dataDirectory.Parent;
        if (candidate == null) return dataDirectory.FullName;

        DirectoryInfo current = candidate;
        for (int depth = 0; depth < 4 && current?.Parent != null; depth++, current = current.Parent)
        {
            DirectoryInfo virtualPlayers = current.Parent;
            DirectoryInfo library = virtualPlayers?.Parent;
            if (string.Equals(virtualPlayers?.Name, "VP", StringComparison.OrdinalIgnoreCase)
                && string.Equals(library?.Name, "Library", StringComparison.OrdinalIgnoreCase)
                && library.Parent != null)
            {
                return library.Parent.FullName;
            }
        }

        return candidate.FullName;
    }

    public static bool TryGetPrinting(CardDefinition card, out string setCode, out string collectorNumber)
    {
        setCode = card != null ? card.setCode?.Trim() : string.Empty;
        collectorNumber = card != null ? card.CollectorNumber : string.Empty;
        return IsSafePathSegment(setCode) && IsSafePathSegment(collectorNumber);
    }

    public static bool IsSafePathSegment(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value != "."
            && value != ".."
            && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    public static string BuildImagePath(string root, string setCode, string collectorNumber, string extension)
    {
        return BuildImagePath(root, setCode, collectorNumber, 0, extension);
    }

    public static string BuildImagePath(
        string root,
        string setCode,
        string collectorNumber,
        int faceIndex,
        string extension)
    {
        string faceSuffix = faceIndex > 0 ? "_" + (faceIndex + 1) : string.Empty;
        return Path.Combine(root, setCode, collectorNumber + faceSuffix + extension);
    }

    public static string BuildScriptPath(string root, string setCode, string collectorNumber)
    {
        return Path.Combine(root, setCode, "m" + collectorNumber + ".lua");
    }

    public static bool IsPathInsideRoot(string root, string path)
    {
        string fullRoot = Path.GetFullPath(root);
        string fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
