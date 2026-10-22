using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>UI-independent downloader. Construct and call on Unity's main thread.</summary>
public sealed class CardArtDownloadService
{
    [Serializable]
    public sealed class Settings
    {
        public string baseUrl = "";
        public bool allowHttp;
        public int timeoutSeconds = 30;
        public bool useCatalogUrls;
    }

    public sealed class Progress
    {
        public string PrintingKey;
        public int FaceIndex;
        public float Fraction;
    }

    private const ulong MaxBytes = 8 * 1024 * 1024;
    private readonly Uri root;
    private readonly string cacheRoot;
    private readonly int timeout;
    private readonly int mainThread;
    private readonly bool useCatalogUrls;
    private readonly SemaphoreSlim slots = new SemaphoreSlim(2);
    private readonly Dictionary<string, Task> pending = new Dictionary<string, Task>(StringComparer.OrdinalIgnoreCase);

    public event Action<string> Downloaded;
    public bool IsDownloading(CardDefinition card) => card != null && pending.ContainsKey(card.PrintingKey);

    public static CardArtDownloadService FromConfig()
    {
        string path = Path.Combine(PrintingKeyUtility.ApplicationRoot, "card-images.json");
        if (!File.Exists(path)) return new CardArtDownloadService(new Settings { baseUrl = "https://cards.scryfall.io/", useCatalogUrls = true });
        return new CardArtDownloadService(JsonUtility.FromJson<Settings>(File.ReadAllText(path)));
    }

    public CardArtDownloadService(Settings settings, string downloadRoot = null)
    {
        if (settings == null || !Uri.TryCreate(settings.baseUrl?.TrimEnd('/') + "/", UriKind.Absolute, out root)
            || (root.Scheme != "https" && !(root.Scheme == "http" && (settings.allowHttp || root.IsLoopback)))
            || !string.IsNullOrEmpty(root.Query) || !string.IsNullOrEmpty(root.Fragment) || !string.IsNullOrEmpty(root.UserInfo))
            throw new ArgumentException("卡图地址必须是 HTTPS 目录；HTTP 需显式启用 allowHttp。");
        cacheRoot = Path.GetFullPath(downloadRoot ?? CardImageLibrary.PersistentRoot);
        timeout = Math.Max(1, Math.Min(300, settings.timeoutSeconds));
        mainThread = Thread.CurrentThread.ManagedThreadId;
        useCatalogUrls = settings.useCatalogUrls;
    }

    public Uri GetImageUrl(CardDefinition card, int faceIndex = 0)
    {
        Validate(card);
        if (!Faces(card).Contains(faceIndex)) throw new ArgumentOutOfRangeException(nameof(faceIndex));
        if (useCatalogUrls) return CardArtSources.Url(card, faceIndex, PlayerPrefs.GetString("CardArt.Quality", "normal"));
        string suffix = faceIndex == 0 ? "" : "_" + (faceIndex + 1);
        return new Uri(root, Uri.EscapeDataString(card.setCode.Trim().ToUpperInvariant()) + "/" +
            Uri.EscapeDataString(card.CollectorNumber + suffix + ".jpg"));
    }

    public bool IsDownloaded(CardDefinition card)
    {
        Validate(card);
        foreach (int face in Faces(card))
            if (!TryExisting(card, face, out _)) return false;
        return true;
    }

    public bool CanDownload(CardDefinition card)
    {
        try {Validate(card);foreach(int face in Faces(card)){if(!TryExisting(card,face,out _))GetImageUrl(card,face);}return true;}
        catch(Exception error) when(error is ArgumentException || error is InvalidOperationException || error is InvalidDataException){return false;}
    }

    // Duplicate clicks share the first request; its token/progress owns that request.
    public Task DownloadAsync(CardDefinition card, IProgress<Progress> progress = null, CancellationToken cancellation = default)
    {
        if (Thread.CurrentThread.ManagedThreadId != mainThread)
            throw new InvalidOperationException("请从 Unity 主线程调用卡图下载。");
        Validate(card);
        if (pending.TryGetValue(card.PrintingKey, out var active)) return active;
        var task = Run(card, progress, cancellation);
        pending[card.PrintingKey] = task;
        return task;
    }

    private async Task Run(CardDefinition card, IProgress<Progress> progress, CancellationToken cancellation)
    {
        await Task.Yield(); // Register pending before any synchronous completion.
        bool entered = false;
        try
        {
            await slots.WaitAsync(cancellation);
            entered = true;
            foreach (int face in Faces(card))
            {
                cancellation.ThrowIfCancellationRequested();
                if (TryExisting(card, face, out _)) continue;
                await DownloadFace(card, face, progress, cancellation);
            }
        }
        finally
        {
            if (entered) slots.Release();
            pending.Remove(card.PrintingKey);
        }
        CardImageLibrary.InvalidateMissingImages();
        Downloaded?.Invoke(card.PrintingKey);
    }

    private async Task DownloadFace(CardDefinition card, int face, IProgress<Progress> progress, CancellationToken cancellation)
    {
        using (var request = UnityWebRequest.Get(GetImageUrl(card, face)))
        {
            request.SetRequestHeader("User-Agent", "MTGB-CardArt/1.0");
            request.SetRequestHeader("Accept", "image/jpeg");
            request.timeout = timeout;
            request.redirectLimit = 0;
            var operation = request.SendWebRequest();
            try
            {
                while (!operation.isDone)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (request.downloadedBytes > MaxBytes) throw new InvalidDataException("卡图超过 8 MiB 限制。");
                    progress?.Report(new Progress { PrintingKey = card.PrintingKey, FaceIndex = face, Fraction = request.downloadProgress });
                    await Task.Yield();
                }
                cancellation.ThrowIfCancellationRequested();
                if (request.result != UnityWebRequest.Result.Success)
                    throw new IOException($"卡图下载失败（HTTP {request.responseCode}）：{request.error}");
                byte[] bytes = request.downloadHandler.data;
                if (bytes == null || bytes.Length < 4 || (ulong)bytes.Length > MaxBytes || bytes[0] != 0xff || bytes[1] != 0xd8)
                    throw new InvalidDataException("服务器未返回有效的 JPEG 卡图。");
                var texture = new Texture2D(2, 2);
                try
                {
                    if (!texture.LoadImage(bytes, true) || texture.width < 100 || texture.height < 100
                        || texture.width > 4096 || texture.height > 4096)
                        throw new InvalidDataException("卡图无法解码或尺寸不合法。");
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                string target = LocalPath(card, face);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                string temporary = target + "." + Guid.NewGuid().ToString("N") + ".download";
                try
                {
                    File.WriteAllBytes(temporary, bytes);
                    cancellation.ThrowIfCancellationRequested();
                    // Another client may share this cache; never overwrite its completed image.
                    if (!File.Exists(target)) File.Move(temporary, target);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                progress?.Report(new Progress { PrintingKey = card.PrintingKey, FaceIndex = face, Fraction = 1f });
            }
            finally { if (!operation.isDone) request.Abort(); }
        }
    }

    private bool TryExisting(CardDefinition card, int face, out string path)
    {
        path = LocalPath(card, face);
        if (File.Exists(path)) return true;
        return CardImageLibrary.TryFindImagePath(card, face, out path, false);
    }

    private string LocalPath(CardDefinition card, int face) =>
        PrintingKeyUtility.BuildImagePath(cacheRoot, card.setCode.Trim().ToUpperInvariant(), card.CollectorNumber, face, ".jpg");

    private static SortedSet<int> Faces(CardDefinition card)
    {
        var faces = new SortedSet<int> { 0 };
        if (card.features != null)
            foreach (var feature in card.features)
                if (feature != null && feature.faceIndex >= 0) faces.Add(feature.faceIndex);
        return faces;
    }

    private static void Validate(CardDefinition card)
    {
        if (card == null || card.forgeAmbiguous || !PrintingKeyUtility.TryGetPrinting(card, out var set, out var number)
            || set.IndexOfAny(new[] { '/', '\\', ':', '?', '#' }) >= 0
            || number.IndexOfAny(new[] { '/', '\\', ':', '?', '#' }) >= 0)
            throw new ArgumentException("无法下载无效或歧义印刷版本。");
    }
}
