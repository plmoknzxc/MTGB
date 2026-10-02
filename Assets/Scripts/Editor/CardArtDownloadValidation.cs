#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public static class CardArtDownloadValidation
{
    public static string Result { get; private set; } = "Not run";
    public static async void Run(string url, string cacheRoot)
    {
        Result = "Running";
        try
        {
            var service = new CardArtDownloadService(new CardArtDownloadService.Settings { baseUrl = url }, cacheRoot);
            string set = "MTGB_DOWNLOAD_TEST_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
            var card = new CardDefinition { setCode = set, collectorNumberText = "1" };
            card.features.Add(new CardFeatureDefinition { faceIndex = 0 });
            card.features.Add(new CardFeatureDefinition { faceIndex = 1 });
            Require(!service.IsDownloaded(card), "Fresh cache already installed");
            int completed = 0;
            service.Downloaded += _ => completed++;
            Task first = service.DownloadAsync(card);
            Require(ReferenceEquals(first, service.DownloadAsync(card)), "Duplicate request not coalesced");
            await first;
            Require(completed == 1 && service.IsDownloaded(card) && !service.IsDownloading(card), "Completion state");
            string path = Path.Combine(cacheRoot, set, "1.jpg");
            var timestamp = File.GetLastWriteTimeUtc(path);
            await service.DownloadAsync(card);
            Require(File.GetLastWriteTimeUtc(path) == timestamp, "Existing image overwritten");
            foreach (string number in new[] { "404", "bad" })
            {
                var invalid = new CardDefinition { setCode = set, collectorNumberText = number };
                bool failed = false;
                try { await service.DownloadAsync(invalid); }
                catch (Exception error) when (error is IOException || error is InvalidDataException) { failed = true; }
                Require(failed && !service.IsDownloaded(invalid) && !service.IsDownloading(invalid), "Invalid response installed");
            }
            var slow = new CardDefinition { setCode = set, collectorNumberText = "slow" };
            using (var cancel = new CancellationTokenSource())
            {
                cancel.CancelAfter(100);
                bool cancelled = false;
                try { await service.DownloadAsync(slow, cancellation: cancel.Token); }
                catch (OperationCanceledException) { cancelled = true; }
                Require(cancelled && !service.IsDownloaded(slow) && !service.IsDownloading(slow), "Cancellation failed");
            }
            var retry = new CardDefinition { setCode = set, collectorNumberText = "retry" };
            bool firstFailed = false;
            try { await service.DownloadAsync(retry); } catch (IOException) { firstFailed = true; }
            Require(firstFailed, "Retry fixture did not fail first");
            await service.DownloadAsync(retry);
            Require(service.IsDownloaded(retry), "Retry failed");
            bool blocked = false;
            try { service.GetImageUrl(new CardDefinition { setCode = "../escape", collectorNumberText = "1" }); }
            catch (ArgumentException) { blocked = true; }
            Require(blocked, "Traversal accepted");
            Require(Directory.GetFiles(cacheRoot, "*.download", SearchOption.AllDirectories).Length == 0, "Temporary files remain");
            Result = "PASS: two faces, duplicate clicks, cache reuse, 404, invalid JPEG, cancellation, retry, traversal, temporary cleanup";
            Debug.Log(Result);
        }
        catch (Exception error) { Result = "FAIL: " + error; Debug.LogException(error); }
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
#endif
