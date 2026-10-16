using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

// Bounded, local display evidence. Never stores a seat snapshot or hidden card.
public static class BattleTextDiagnostics
{
    [Serializable] public sealed class Entry
    {
        public string utc, language, channel, printingKey, cardName, source, display;
        public string originalName,currentArtPrintingKey,ruleText,ruleSourceName,ruleSourcePrintingKey;
        public bool copy,merged,truncated;
    }
    const int MaxPending = 128, MaxRemembered = 512, MaxText = 2048;
    const long MaxFileBytes = 2 * 1024 * 1024;
    static readonly Regex Prose = new Regex(@"[A-Za-z][A-Za-z'’-]*(?: +[A-Za-z][A-Za-z'’-]*){2,}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20));
    static readonly Queue<string> pending = new Queue<string>();
    static readonly HashSet<string> remembered = new HashSet<string>(StringComparer.Ordinal);
    static readonly Queue<string> order = new Queue<string>();
    static Task write;
    static string retry = "";
    static float nextFlush;
    static int deferred;
    public readonly struct DisplayScope : IDisposable
    {
        public void Dispose() { deferred--; }
    }
    public static DisplayScope DeferIntermediate() { deferred++; return new DisplayScope(); }
    public static string PathName => Path.Combine(Application.persistentDataPath, "Diagnostics", "battle-text.jsonl");

    public static bool HasEnglishProse(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        // Most translated labels take this allocation-free fast path.
        int latin = 0;
        foreach (char c in text) if (c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z') latin++;
        if (latin < 6) return false;
        string clean = Regex.Replace(text, @"\{[^}]*\}|<[^>]*>", "");
        try { return Prose.IsMatch(clean); } catch (RegexMatchTimeoutException) { return true; }
    }
    public static bool IsUntranslatedDisplay(string text)=>HasEnglishProse(text)&&!GamePreferences.IsCardIdentity(text);

    public static void Record(string channel, string source, string display, ForgeCardView card = null, bool final = false, string recipe = null, ForgeCardView origin = null)
    {
#if UNITY_SERVER
        return;
#else
        if (deferred > 0 && !final || !GamePreferences.Ready || GamePreferences.Language == "en-US" || card?.faceDown == true || !HasEnglishProse(display)) return;
        // A known untranslated proper name is not an untranslated rules sentence.
        if (GamePreferences.IsCardIdentity(display)) return;
        string Clip(string s) => string.IsNullOrEmpty(s) ? "" : s.Length <= MaxText ? s : s.Substring(0, MaxText);
        bool truncated=(source?.Length??0)>MaxText||(display?.Length??0)>MaxText||(recipe?.Length??0)>MaxText;
        source = Clip(source); display = Clip(display);
        recipe=Clip(recipe);
        string key = GamePreferences.Language + "\n" + channel + "\n" + card?.printingKey + "\n" + source + "\n" + display+"\n"+recipe+"\n"+origin?.printingKey;
        if (!remembered.Add(key)) return;
        order.Enqueue(key);
        while (order.Count > MaxRemembered) remembered.Remove(order.Dequeue());
        if (pending.Count == MaxPending) pending.Dequeue();
        pending.Enqueue(JsonUtility.ToJson(new Entry { utc = DateTime.UtcNow.ToString("O"), language = GamePreferences.Language,
            channel = channel, printingKey = card?.printingKey ?? "", cardName = Clip(card?.name), source = source, display = display,
            originalName=Clip(card?.originalName),currentArtPrintingKey=card?.currentArtPrintingKey??"",copy=card?.copy==true,merged=card?.merged==true,
            ruleText=recipe,ruleSourceName=Clip(origin?.name),ruleSourcePrintingKey=origin?.printingKey??"",truncated=truncated }) + "\n");
#endif
    }

    public static void Tick()
    {
        if (Time.realtimeSinceStartup < nextFlush) return;
        nextFlush = Time.realtimeSinceStartup + 5;
        FlushAsync();
    }

    public static Task FlushAsync(string destination = null)
    {
        if (write != null && !write.IsCompleted) return write;
        // Keep a failed batch for retry; a locked/full drive must not break play.
        if (write?.IsFaulted == true) { _ = write.Exception; }
        else retry = "";
        var text = new StringBuilder(retry);
        while (pending.Count > 0) text.Append(pending.Dequeue());
        if (text.Length == 0) { write = null; return Task.CompletedTask; }
        retry = text.ToString();
        // Bound a repeatedly failing disk as well as a normal pending queue.
        if (retry.Length > MaxPending * (MaxText * 2 + 512))
            retry = retry.Substring(retry.IndexOf('\n', retry.Length - MaxPending * (MaxText * 2 + 512)) + 1);
        string path = destination ?? PathName, batch = retry;
        write = Task.Run(() => Append(path, batch));
        return write;
    }

    public static void FlushOnExit()
    {
        try { write?.GetAwaiter().GetResult(); } catch { /* Retried below. */ }
        try { FlushAsync().GetAwaiter().GetResult(); } catch { /* Gameplay never depends on diagnostic storage. */ }
    }

    static void Append(string path, string batch)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (File.Exists(path) && new FileInfo(path).Length + Encoding.UTF8.GetByteCount(batch) > MaxFileBytes)
        {
            File.Copy(path, path + ".previous", true);
            File.WriteAllText(path, "", new UTF8Encoding(false));
        }
        File.AppendAllText(path, batch, new UTF8Encoding(false));
    }
}
