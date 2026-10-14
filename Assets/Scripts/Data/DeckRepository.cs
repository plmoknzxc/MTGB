using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class DeckRepository
{
    public const string FolderName = "Decks";
    public const string Extension = ".mdk";
    public const int DefaultCopyLimit = 4;

    private enum DeckSection
    {
        Commander,
        Main,
        Sideboard
    }

    public static string DeckRoot => Path.Combine(PrintingKeyUtility.ApplicationRoot, FolderName);
    private static string EmptyMarker => Path.Combine(DeckRoot, ".mtgb-empty");
    public static bool WasExplicitlyEmptied => File.Exists(EmptyMarker);

    public static string DirectoryRevision()
    {
        if(!Directory.Exists(DeckRoot))return "";
        var files=EnumerateDeckFiles();Array.Sort(files,StringComparer.OrdinalIgnoreCase);
        var revision=new System.Text.StringBuilder();
        foreach(var path in files){var info=new FileInfo(path);if(info.Exists)revision.Append(info.Name).Append(':').Append(info.Length).Append(':').Append(info.LastWriteTimeUtc.Ticks).Append('|');}
        return revision.ToString();
    }

    public static IReadOnlyList<DeckDefinition> LoadAll()
    {
        EnsureFolder();
        var decks = new List<DeckDefinition>();
        string[] files = EnumerateDeckFiles();
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < files.Length; i++)
        {
            DeckDefinition deck = Load(files[i]);
            if (deck != null) decks.Add(deck);
        }

        return decks;
    }

    public static DeckDefinition Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        try
        {
            string content = File.ReadAllText(path);
            DeckDefinition deck = LooksLikeJson(content)
                ? LoadLegacyJson(path, content)
                : LoadLineDeck(path, content);

            if (deck == null) return null;
            if (string.IsNullOrWhiteSpace(deck.name)) deck.name = Path.GetFileNameWithoutExtension(path);
            string fileName=Path.GetFileNameWithoutExtension(path);
            if(!string.Equals(fileName,MakeSafeFileName(deck.name),StringComparison.OrdinalIgnoreCase))deck.name=fileName;
            deck.sourcePath=Path.GetFullPath(path);
            if (string.IsNullOrWhiteSpace(deck.format)) deck.format = "standard";
            if (string.IsNullOrWhiteSpace(deck.banlist)) deck.banlist = "current";
            if (deck.commanders == null) deck.commanders = new List<DeckEntry>();
            if (deck.cards == null) deck.cards = new List<DeckEntry>();
            if (deck.sideboard == null) deck.sideboard = new List<DeckEntry>();
            Normalize(deck);
            return deck;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not load deck '{path}': {exception.Message}");
            return null;
        }
    }

    public static string Save(DeckDefinition deck)
    {
        if (deck == null) throw new ArgumentNullException(nameof(deck));
        EnsureFolder();
        Normalize(deck);

        string fileName = MakeSafeFileName(string.IsNullOrWhiteSpace(deck.name) ? "New Deck" : deck.name) + Extension;
        string path = Path.Combine(DeckRoot, fileName);
        File.WriteAllText(path, SerializeLineDeck(deck));
        return path;
    }

    // Saving an existing deck with a new name replaces its file instead of creating a copy.
    public static string SaveReplacing(string originalName, DeckDefinition deck)
    {
        if (deck == null) throw new ArgumentNullException(nameof(deck));
        EnsureFolder();
        string source = FindUniqueDeckPath(originalName);
        if (source == null) throw new FileNotFoundException("找不到原卡组文件，请先重载。", originalName);
        string basis = MakeSafeFileName(string.IsNullOrWhiteSpace(deck.name) ? "New Deck" : deck.name).TrimEnd('.', ' ');
        if (string.IsNullOrEmpty(basis)) basis = "Deck";
        string destination = null;
        string savedName = null;
        for (int i = 0; i < 10000; i++)
        {
            string candidateName = basis + (i == 0 ? "" : " (" + i + ")");
            string candidatePath = Path.Combine(DeckRoot, candidateName + Extension);
            if (!string.Equals(source, candidatePath, StringComparison.OrdinalIgnoreCase) && File.Exists(candidatePath)) continue;
            destination = candidatePath;
            savedName = candidateName;
            break;
        }
        if (destination == null) throw new IOException("同名卡组过多，请换一个名称。");
        deck.name = savedName;
        Normalize(deck);
        string content = SerializeLineDeck(deck);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(source, content);
            return source;
        }
        string temporary = Path.Combine(DeckRoot, ".mtgb-rename-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, content);
            File.Move(temporary, destination);
            try { File.Delete(source); }
            catch
            {
                if (File.Exists(source)) File.Delete(destination);
                throw;
            }
            return destination;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string FindUniqueDeckPath(string deckName)
    {
        if (string.IsNullOrWhiteSpace(deckName)) return null;
        string match = null;
        foreach (string path in EnumerateDeckFiles())
        {
            DeckDefinition existing = Load(path);
            if (existing == null || !string.Equals(existing.name, deckName, StringComparison.OrdinalIgnoreCase)) continue;
            if (match != null) throw new IOException("存在同名卡组文件，无法确定要修改哪一份。");
            match = path;
        }
        return match;
    }

    public static bool DeleteByName(string deckName)
    {
        EnsureFolder();
        string match = FindUniqueDeckPath(deckName);
        if (match == null) return false;
        File.Delete(match);
        if (EnumerateDeckFiles().Length == 0) File.WriteAllText(EmptyMarker, string.Empty);
        return true;
    }

    public static DeckDefinition CreateDefaultDeck(IReadOnlyList<CardDefinition> cards)
    {
        var deck = new DeckDefinition { name = "Starter" };
        AddKnown(deck, cards, "10E", "376", 36);
        AddKnown(deck, cards, "2ED", "162", 4);
        AddKnown(deck, cards, "10E", "224", 4);
        AddKnown(deck, cards, "10E", "232", 4);
        AddKnown(deck, cards, "10E", "209", 4);
        AddKnown(deck, cards, "7ED", "226", 4);
        AddKnown(deck, cards, "10E", "212", 4);
        Normalize(deck);
        return deck;
    }

    public static List<CardDefinition> BuildRuntimeDeck(DeckDefinition deck, IReadOnlyList<CardDefinition> cards, out string warning)
    {
        warning = string.Empty;
        var runtimeDeck = new List<CardDefinition>();
        if (deck == null)
        {
            warning = "No deck selected.";
            return runtimeDeck;
        }

        var missing = new List<string>();
        for (int i = 0; i < deck.cards.Count; i++)
        {
            DeckEntry entry = deck.cards[i];
            if (entry == null || entry.count <= 0) continue;

            CardDefinition card = ResolveCard(entry, cards);
            if (card == null)
            {
                missing.Add(!string.IsNullOrEmpty(entry.PrintingKey) ? entry.PrintingKey : entry.cardId.ToString());
                continue;
            }

            for (int copy = 0; copy < entry.count; copy++)
            {
                runtimeDeck.Add(card);
            }
        }

        if (missing.Count > 0)
        {
            warning = $"Missing cards: {string.Join(", ", missing)}";
        }
        return runtimeDeck;
    }

    public static int AddCard(DeckDefinition deck, CardDefinition card, int count)
    {
        return AddCard(deck, card, count, false);
    }

    public static int AddCard(DeckDefinition deck, CardDefinition card, int count, bool sideboard)
    {
        if (deck == null || card == null || count <= 0) return 0;

        int allowed = GetAllowedCopiesToAdd(deck, card);
        int actualCount = IsUnlimitedCopies(card) ? count : Mathf.Min(count, allowed);
        if (actualCount <= 0) return 0;

        List<DeckEntry> entries = GetEntryList(deck, sideboard);
        DeckEntry entry = FindEntry(entries, card);
        if (entry == null)
        {
            entry = new DeckEntry { count = 0 };
            FillEntry(entry, card);
            entries.Add(entry);
        }

        entry.count += actualCount;
        Normalize(deck);
        return actualCount;
    }

    public static void RemoveCard(DeckDefinition deck, CardDefinition card, int count)
    {
        RemoveCard(deck, card, count, false);
    }

    public static void RemoveCard(DeckDefinition deck, CardDefinition card, int count, bool sideboard)
    {
        if (deck == null || card == null || count <= 0) return;

        List<DeckEntry> entries = GetEntryList(deck, sideboard);
        DeckEntry entry = FindEntry(entries, card);
        if (entry == null) return;

        entry.count -= count;
        Normalize(deck);
    }

    public static int GetAllowedCopiesToAdd(DeckDefinition deck, CardDefinition card)
    {
        if (deck == null || card == null) return 0;
        if (IsUnlimitedCopies(card)) return int.MaxValue;
        return Mathf.Max(0, card.forgeDeckLimit - CountCopies(deck, card));
    }

    public static bool EnforceCopyLimits(DeckDefinition deck, IReadOnlyList<CardDefinition> cards)
    {
        if (deck == null) return false;
        if (cards == null || cards.Count == 0) return false;

        bool changed = false;
        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        changed |= EnforceCopyLimits(GetEntryList(deck, DeckSection.Commander), cards, used);
        changed |= EnforceCopyLimits(GetEntryList(deck, false), cards, used);
        changed |= EnforceCopyLimits(GetEntryList(deck, true), cards, used);
        Normalize(deck);
        return changed;
    }

    public static int GetTotalCount(DeckDefinition deck)
    {
        return GetTotalCount(deck, false);
    }

    public static int GetTotalCount(DeckDefinition deck, bool sideboard)
    {
        if (deck == null) return 0;
        List<DeckEntry> entries = GetEntryList(deck, sideboard);
        int count = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].count > 0) count += entries[i].count;
        }
        return count;
    }

    public static CardDefinition ResolveCard(DeckEntry entry, IReadOnlyList<CardDefinition> cards)
    {
        if (entry == null || cards == null) return null;

        string setCode = entry.setCode?.Trim();
        string collectorNumber = entry.collectorNumber?.Trim();
        if (!string.IsNullOrWhiteSpace(setCode) && !string.IsNullOrWhiteSpace(collectorNumber))
        {
            if(CardDatabaseRepository.TryResolveSharedPrinting(cards,setCode+"/"+collectorNumber,out var indexed))return indexed;
            for (int i = 0; i < cards.Count; i++)
            {
                CardDefinition card = cards[i];
                if (card == null) continue;
                if (string.Equals(card.setCode, setCode, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(card.CollectorNumber, collectorNumber, StringComparison.OrdinalIgnoreCase))
                {
                    return card.forgeAmbiguous ? null : card;
                }
            }
            return null; // An explicit printing must never fall back to another card's local ID.
        }

        if (!string.IsNullOrWhiteSpace(setCode) || !string.IsNullOrWhiteSpace(collectorNumber)) return null;
        return null; // Legacy local IDs cannot identify a Forge printing.
    }

    private static bool LooksLikeJson(string content)
    {
        return !string.IsNullOrWhiteSpace(content) && content.TrimStart().StartsWith("{", StringComparison.Ordinal);
    }

    private static DeckDefinition LoadLegacyJson(string path, string content)
    {
        DeckDefinition deck = JsonUtility.FromJson<DeckDefinition>(content);
        if (deck != null && string.IsNullOrWhiteSpace(deck.name))
        {
            deck.name = Path.GetFileNameWithoutExtension(path);
        }
        return deck;
    }

    private static DeckDefinition LoadLineDeck(string path, string content)
    {
        var deck = new DeckDefinition { name = Path.GetFileNameWithoutExtension(path) };
        List<DeckEntry> target = deck.cards;
        bool ignoreSection = false;

        string[] lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("#name ", StringComparison.OrdinalIgnoreCase))
            {
                deck.name = line.Substring(6).Trim();
                continue;
            }
            if (line.StartsWith("#format ", StringComparison.OrdinalIgnoreCase))
            {
                deck.format = line.Substring(8).Trim().ToLowerInvariant();
                continue;
            }
            if (line.StartsWith("#banlist ", StringComparison.OrdinalIgnoreCase))
            {
                deck.banlist = line.Substring(9).Trim();
                continue;
            }

            if (line.StartsWith("//", StringComparison.Ordinal)) continue;
            if (line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("!", StringComparison.Ordinal))
            {
                ignoreSection = IsIgnoredSectionHeader(line);
                if (IsCommanderHeader(line))
                {
                    target = deck.commanders;
                    ignoreSection = false;
                }
                else if (IsMainDeckHeader(line))
                {
                    target = deck.cards;
                    ignoreSection = false;
                }
                else if (IsSideboardHeader(line))
                {
                    target = deck.sideboard;
                    ignoreSection = false;
                }
                else if (line.Equals("#attractions", StringComparison.OrdinalIgnoreCase))
                {
                    target = deck.attractions;
                    ignoreSection = false;
                }
                else if (line.Equals("#contraptions", StringComparison.OrdinalIgnoreCase))
                {
                    target = deck.contraptions;
                    ignoreSection = false;
                }
                continue;
            }

            if (ignoreSection) continue;
            AddLineEntry(target, line);
        }

        return deck;
    }

    public static string SerializeLineDeck(DeckDefinition deck)
    {
        var lines = new List<string>
        {
            "#name " + (string.IsNullOrWhiteSpace(deck.name) ? "New Deck" : deck.name.Trim()),
            "#format " + (string.IsNullOrWhiteSpace(deck.format) ? "standard" : deck.format.Trim()),
            "#banlist " + (string.IsNullOrWhiteSpace(deck.banlist) ? "current" : deck.banlist.Trim())
        };

        if (deck.commanders != null && deck.commanders.Count > 0)
        {
            lines.Add("#commander");
            AppendDeckLines(lines, deck.commanders);
        }

        lines.Add("#main");
        AppendDeckLines(lines, deck.cards);
        lines.Add("!side");
        AppendDeckLines(lines, deck.sideboard);
        if (deck.attractions != null && deck.attractions.Count > 0)
        {
            lines.Add("#attractions");
            AppendDeckLines(lines, deck.attractions);
        }
        if (deck.contraptions != null && deck.contraptions.Count > 0)
        {
            lines.Add("#contraptions");
            AppendDeckLines(lines, deck.contraptions);
        }
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    // Import and Save As must never overwrite the original, including sanitized-name collisions.
    public static string SaveNew(DeckDefinition deck)
    {
        if(deck==null)throw new ArgumentNullException(nameof(deck));
        EnsureFolder();
        string basis=MakeSafeFileName(string.IsNullOrWhiteSpace(deck.name)?"Imported Deck":deck.name).TrimEnd('.', ' ');
        if(string.IsNullOrEmpty(basis))basis="Deck";
        for(int i=0;i<10000;i++)
        {
            string name=basis+(i==0?"":" ("+i+")"), path=Path.Combine(DeckRoot,name+Extension);
            if(File.Exists(path))continue;
            using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write))
            using(var writer=new StreamWriter(stream,new System.Text.UTF8Encoding(false)))
            { deck.name=name;writer.Write(SerializeLineDeck(deck)); }
            return path;
        }
        throw new IOException("同名卡组过多，请换一个名称。");
    }

    private static void AppendDeckLines(List<string> lines, List<DeckEntry> entries)
    {
        if (entries == null) return;
        for (int i = 0; i < entries.Count; i++)
        {
            DeckEntry entry = entries[i];
            if (entry == null || entry.count <= 0) continue;

            string token = !string.IsNullOrWhiteSpace(entry.PrintingKey)
                ? entry.PrintingKey
                : entry.cardId.ToString();
            lines.Add($"{entry.count} {token}");
        }
    }

    private static void AddLineEntry(List<DeckEntry> entries, string token)
    {
        if (entries == null || string.IsNullOrWhiteSpace(token)) return;

        string cleaned = token.Trim();
        int count = 1;
        if (TryReadCountPrefix(cleaned, out int parsedCount, out string rest))
        {
            count = parsedCount;
            cleaned = rest;
        }

        var entry = new DeckEntry();
        cleaned = cleaned.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        int slash = cleaned.IndexOf('/');
        if (slash > 0 && slash < cleaned.Length - 1)
        {
            entry.setCode = cleaned.Substring(0, slash).Trim();
            entry.collectorNumber = cleaned.Substring(slash + 1).Trim();
        }
        else if (int.TryParse(cleaned, out int cardId))
        {
            entry.cardId = cardId;
        }
        else
        {
            Debug.LogWarning($"Ignored invalid deck line: {token}");
            return;
        }

        DeckEntry existing = FindEntry(entries, entry);
        if (existing == null)
        {
            entry.count = count;
            entries.Add(entry);
        }
        else
        {
            existing.count += count;
        }
    }

    private static bool TryReadCountPrefix(string line, out int count, out string rest)
    {
        count = 1;
        rest = line;
        if (string.IsNullOrWhiteSpace(line)) return false;

        int index = 0;
        while (index < line.Length && char.IsDigit(line[index])) index++;
        if (index == 0 || index >= line.Length || !char.IsWhiteSpace(line[index])) return false;
        if (!int.TryParse(line.Substring(0, index), out count) || count <= 0) return false;

        rest = line.Substring(index).Trim();
        return rest.Length > 0;
    }

    private static bool IsCommanderHeader(string line)
    {
        return line.Equals("#commander", StringComparison.OrdinalIgnoreCase)
            || line.Equals("!commander", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMainDeckHeader(string line)
    {
        return line.Equals("#main", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSideboardHeader(string line)
    {
        return line.Equals("!side", StringComparison.OrdinalIgnoreCase)
            || line.Equals("#side", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnoredSectionHeader(string line)
    {
        return line.Equals("#extra", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EnforceCopyLimits(List<DeckEntry> entries, IReadOnlyList<CardDefinition> cards, Dictionary<string, int> used)
    {
        bool changed = false;
        if (entries == null) return false;

        for (int i = 0; i < entries.Count; i++)
        {
            DeckEntry entry = entries[i];
            if (entry == null || entry.count <= 0)
            {
                entries.RemoveAt(i);
                i--;
                changed = true;
                continue;
            }

            CardDefinition card = ResolveCard(entry, cards);
            string key = GetDeckKey(entry, card);
            if (string.IsNullOrEmpty(key))
            {
                entries.RemoveAt(i);
                i--;
                changed = true;
                continue;
            }

            int max = card == null ? DefaultCopyLimit : IsUnlimitedCopies(card) ? int.MaxValue : card.forgeDeckLimit;
            used.TryGetValue(key, out int current);
            int allowed = max == int.MaxValue ? entry.count : Mathf.Max(0, max - current);
            if (allowed <= 0)
            {
                entries.RemoveAt(i);
                i--;
                changed = true;
                continue;
            }

            if (entry.count > allowed)
            {
                entry.count = allowed;
                changed = true;
            }

            used[key] = current + entry.count;
        }

        return changed;
    }

    private static bool IsUnlimitedCopies(CardDefinition card)
    {
        return card != null && card.forgeDeckLimit < 0;
    }

    private static int CountCopies(DeckDefinition deck, CardDefinition card)
    {
        if (deck == null || card == null) return 0;
        return CountCopies(GetEntryList(deck, DeckSection.Commander), card)
            + CountCopies(GetEntryList(deck, false), card)
            + CountCopies(GetEntryList(deck, true), card);
    }

    private static int CountCopies(List<DeckEntry> entries, CardDefinition card)
    {
        if (entries == null || card == null) return 0;
        int count = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            DeckEntry entry = entries[i];
            if (EntryMatchesCard(entry, card)) count += Mathf.Max(0, entry.count);
        }
        return count;
    }

    private static void AddKnown(DeckDefinition deck, IReadOnlyList<CardDefinition> cards, string setCode, string collector, int count)
    {
        CardDefinition card = ResolveCard(new DeckEntry { setCode = setCode, collectorNumber = collector }, cards);
        if (card == null) throw new InvalidDataException("Starter printing is unavailable: " + setCode + "/" + collector);

        AddCard(deck, card, count, false);
    }

    private static CardDefinition FindById(IReadOnlyList<CardDefinition> cards, int cardId)
    {
        if (cards == null) return null;
        CardDefinition match = null;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null || cards[i].cardID != cardId) continue;
            if (match != null) return null;
            match = cards[i];
        }
        return match;
    }

    private static DeckEntry FindEntry(DeckDefinition deck, CardDefinition card)
    {
        return FindEntry(deck.cards, card);
    }

    private static DeckEntry FindEntry(List<DeckEntry> entries, CardDefinition card)
    {
        if (entries == null || card == null) return null;
        for (int i = 0; i < entries.Count; i++)
        {
            DeckEntry entry = entries[i];
            if (EntryMatchesCard(entry, card)) return entry;
        }

        return null;
    }

    private static DeckEntry FindEntry(List<DeckEntry> entries, DeckEntry match)
    {
        if (entries == null || match == null) return null;
        for (int i = 0; i < entries.Count; i++)
        {
            DeckEntry entry = entries[i];
            if (EntryMatchesEntry(entry, match)) return entry;
        }

        return null;
    }

    public static bool EntryMatchesCard(DeckEntry entry, CardDefinition card)
    {
        if (entry == null || card == null) return false;
        if (!string.IsNullOrWhiteSpace(entry.PrintingKey))
            return string.Equals(entry.PrintingKey, card.PrintingKey, StringComparison.OrdinalIgnoreCase);
        return false;
    }

    private static bool EntryMatchesEntry(DeckEntry entry, DeckEntry match)
    {
        if (entry == null || match == null) return false;
        if (!string.IsNullOrWhiteSpace(entry.PrintingKey) || !string.IsNullOrWhiteSpace(match.PrintingKey))
            return !string.IsNullOrWhiteSpace(entry.PrintingKey)
                && string.Equals(entry.PrintingKey, match.PrintingKey, StringComparison.OrdinalIgnoreCase);
        return entry.cardId != 0 && entry.cardId == match.cardId;
    }

    private static void FillEntry(DeckEntry entry, CardDefinition card)
    {
        entry.cardId = card.cardID;
        entry.setCode = card.setCode;
        entry.collectorNumber = card.CollectorNumber;
    }

    private static void Normalize(DeckDefinition deck)
    {
        NormalizeEntries(GetEntryList(deck, DeckSection.Commander));
        NormalizeEntries(GetEntryList(deck, false));
        NormalizeEntries(GetEntryList(deck, true));
        if (deck.attractions == null) deck.attractions = new List<DeckEntry>();
        if (deck.contraptions == null) deck.contraptions = new List<DeckEntry>();
        NormalizeEntries(deck.attractions);
        NormalizeEntries(deck.contraptions);
    }

    private static void NormalizeEntries(List<DeckEntry> entries)
    {
        if (entries == null) return;
        var merged = new Dictionary<string, DeckEntry>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < entries.Count; i++)
        {
            DeckEntry entry = entries[i];
            if (entry == null || entry.count <= 0)
            {
                entries.RemoveAt(i);
                i--;
                continue;
            }

            entry.setCode = entry.setCode?.Trim();
            entry.collectorNumber = entry.collectorNumber?.Trim();

            string key = GetDeckKey(entry, null);
            if (string.IsNullOrEmpty(key))
            {
                entries.RemoveAt(i);
                i--;
                continue;
            }

            if (!merged.TryGetValue(key, out DeckEntry existing))
            {
                merged[key] = entry;
                continue;
            }

            existing.count += entry.count;
            entries.RemoveAt(i);
            i--;
        }
    }

    private static string GetDeckKey(DeckEntry entry, CardDefinition card)
    {
        if (card != null && !string.IsNullOrWhiteSpace(card.PrintingKey)) return card.PrintingKey;
        if (entry != null && !string.IsNullOrWhiteSpace(entry.PrintingKey)) return entry.PrintingKey;
        return entry != null && entry.cardId != 0 ? $"id:{entry.cardId}" : string.Empty;
    }

    private static List<DeckEntry> GetEntryList(DeckDefinition deck, bool sideboard)
    {
        if (deck.cards == null) deck.cards = new List<DeckEntry>();
        if (deck.sideboard == null) deck.sideboard = new List<DeckEntry>();
        return sideboard ? deck.sideboard : deck.cards;
    }

    private static List<DeckEntry> GetEntryList(DeckDefinition deck, DeckSection section)
    {
        if (deck.commanders == null) deck.commanders = new List<DeckEntry>();
        if (deck.cards == null) deck.cards = new List<DeckEntry>();
        if (deck.sideboard == null) deck.sideboard = new List<DeckEntry>();

        switch (section)
        {
            case DeckSection.Commander:
                return deck.commanders;
            case DeckSection.Sideboard:
                return deck.sideboard;
            default:
                return deck.cards;
        }
    }

    private static string MakeSafeFileName(string value)
    {
        string fileName = value.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return string.IsNullOrWhiteSpace(fileName) ? "Deck" : fileName;
    }

    private static void EnsureFolder()
    {
        Directory.CreateDirectory(DeckRoot);
    }

    private static string[] EnumerateDeckFiles()
    {
        return Directory.GetFiles(DeckRoot, "*" + Extension, SearchOption.TopDirectoryOnly);
    }
}
