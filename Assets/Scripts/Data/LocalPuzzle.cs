using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class PuzzleCard
{
    public string name;
    public int side, zone, count = 1;
    public bool tapped, summoningSick;
    public int damage, plusCounters;
    public bool overrideCounter;
    public int counterValue;
    public string key, attachedTo;
    public int targetCopy;
    public int tablePlace; // One-based placement within this player's editor mat.
    public int entryBatch; // Zero in older files: each physical card has its own entry.
    public bool HasState => tapped || summoningSick || damage != 0 || plusCounters != 0 || overrideCounter || !string.IsNullOrEmpty(attachedTo);

    internal static (string kind, string label, int initial) PrintedCounter(CardDefinition card)
    {
        if (card.HasType(CardType.Planeswalker)) return ("LOYALTY","忠诚指示物",card.loyalty);
        if (card.HasType(CardType.Battle)) return ("DEFENSE","布防指示物",card.battleDefense);
        if ((card.subtypes ?? "").Split(' ').Contains("Saga")) return ("LORE","学问指示物",1);
        return default;
    }

    internal string BattlefieldEntry(CardDefinition card)
    {
        var counter = PrintedCounter(card);
        int number = overrideCounter ? counterValue : counter.initial;
        var amounts = new List<string>(2);
        if (plusCounters > 0) amounts.Add("P1P1=" + plusCounters);
        if (number > 0 && counter.kind != null) amounts.Add(counter.kind + "=" + number);
        return name + "|NoETBTrigs" + (tapped ? "|Tapped" : "") + (summoningSick ? "|SummonSick" : "")
            + (amounts.Count > 0 ? "|Counters:" + string.Join(",",amounts) : "") + (damage > 0 ? "|Damage:" + damage : "");
    }
}

[Serializable]
public sealed class LocalPuzzle
{
    public const int Version = 3;
    public static readonly string[] Zones = { "battlefield", "hand", "library", "graveyard", "exile" };
    public static readonly string[] Phases = { "MAIN1", "COMBAT_BEGIN", "MAIN2", "END_OF_TURN" };
    public int version = Version;
    public string id, title, description, author;
    public int humanLife = 20, aiLife = 20, firstSide, phase, turns = 1;
    public bool survive;
    public PuzzleGoal goal;
    public int seed = 11431;
    public bool orderedBattlefield;
    public bool hasTableRows;
    public List<PuzzleCard> cards = new();

    [NonSerialized] internal string libraryFile;
    public string LibraryKey => libraryFile == null ? id : "file:" + libraryFile;

    public LocalPuzzle Copy()
    {
        var copy = JsonUtility.FromJson<LocalPuzzle>(JsonUtility.ToJson(this));
        if (goal == null) copy.goal = null;
        copy.libraryFile = libraryFile;
        return copy;
    }

    public const int TableColumns=8;
    private static bool RearPlace(int place) => ((place-1)/TableColumns)%2==1;

    public int FreePlace(int side,bool rear=false)
    {
        var used = new HashSet<int>(cards.Where(r => r.side == side && r.zone == 0).Select(r => r.tablePlace));
        int place = rear?TableColumns+1:1;
        while (used.Contains(place)) {place++;if(RearPlace(place)!=rear)place+=TableColumns;}
        return place;
    }

    // Layout is independent of the order in cards, which Forge uses for timestamps.
    public void PrepareTableRows(Func<string,bool> backRow)
    {
        if(hasTableRows)return;
        for(int side=0;side<2;side++)
        {
            var used=new HashSet<int>();
            var rows=cards.Where(r=>r.side==side && r.zone==0).ToList();
            foreach(var row in rows)
                if(row.tablePlace<1 || RearPlace(row.tablePlace)!=backRow(row.name) || !used.Add(row.tablePlace))
                    row.tablePlace=0;
            foreach(var row in rows)
                if(row.tablePlace==0)row.tablePlace=FreePlace(side,backRow(row.name));
        }
        hasTableRows=true;
    }

    public void PrepareTable()
    {
        if (orderedBattlefield) return;
        // Old puzzles entered one player's battlefield at a time. Preserve that order and resolve grouped attachment indices.
        var groups = cards.Where(r => r.zone == 0).OrderBy(r => r.side).ToList();
        cards.RemoveAll(r => r.zone == 0);
        var copies = new Dictionary<(string,int),string>();
        int[] places = {0,0};
        foreach (var group in groups)
        {
            for (int i=0;i<group.count;i++)
            {
                var card = JsonUtility.FromJson<PuzzleCard>(JsonUtility.ToJson(group));
                card.count = 1;
                card.tablePlace = ++places[card.side];
                if (!string.IsNullOrEmpty(group.key))
                {
                    card.key = i == 0 ? group.key : Guid.NewGuid().ToString("N");
                    copies.Add((group.key,i),card.key);
                }
                cards.Add(card);
            }
        }
        foreach (var row in cards)
            if (row.attachedTo != null && copies.TryGetValue((row.attachedTo,row.targetCopy),out string key))
            { row.attachedTo=key; row.targetCopy=0; }
        orderedBattlefield = true;
    }

    public List<List<PuzzleCard>> EntryBatches()
    {
        var batches = new List<List<PuzzleCard>>();
        var named = new Dictionary<int,List<PuzzleCard>>();
        foreach (var card in cards.Where(c => c.zone == 0))
        {
            if (card.entryBatch > 0 && named.TryGetValue(card.entryBatch, out var existing))
                existing.Add(card);
            else
            {
                var batch = new List<PuzzleCard> { card };
                batches.Add(batch);
                if (card.entryBatch > 0) named.Add(card.entryBatch,batch);
            }
        }
        return batches;
    }

    private void SetEntryBatches(List<List<PuzzleCard>> batches)
    {
        var ordered = new Queue<PuzzleCard>();
        for (int i=0;i<batches.Count;i++)
            foreach (var card in batches[i])
            {
                card.entryBatch = batches[i].Count > 1 ? i+1 : 0;
                ordered.Enqueue(card);
            }
        for (int i=0;i<cards.Count;i++)
            if (cards[i].zone==0) cards[i]=ordered.Dequeue();
    }

    // Join a single card to the preceding batch, or detach it immediately after its batch.
    public bool ToggleSimultaneous(PuzzleCard row)
    {
        var batches=EntryBatches();
        int i=batches.FindIndex(batch=>batch.Contains(row));
        if (i<0) return false;
        if (batches[i].Count>1)
        {
            batches[i].Remove(row);
            batches.Insert(i+1,new List<PuzzleCard>{row});
        }
        else
        {
            if (i==0) return false;
            batches[i-1].Add(row);
            batches.RemoveAt(i);
        }
        SetEntryBatches(batches);
        version=Version;
        return true;
    }

    public bool ShiftOrder(PuzzleCard row,int step)
    {
        if(row.zone==0)
        {
            var batches=EntryBatches();
            int index=batches.FindIndex(batch=>batch.Contains(row)), destination=index+step;
            if(index<0 || destination<0 || destination>=batches.Count) return false;
            (batches[index],batches[destination])=(batches[destination],batches[index]);
            SetEntryBatches(batches);
            return true;
        }
        var sequence=cards.Where(r=>r.zone==row.zone && (row.zone==0 || r.side==row.side)).ToList();
        int from=sequence.IndexOf(row), to=from+step;
        if(from<0 || to<0 || to>=sequence.Count)return false;
        int a=cards.IndexOf(row), b=cards.IndexOf(sequence[to]);
        (cards[a],cards[b])=(cards[b],cards[a]);
        return true;
    }

    // The quantity control removes the last physical copy in this row.
    public bool RemoveLastCopy(PuzzleCard row)
    {
        row.count--;
        if (row.count == 0) cards.Remove(row);
        if (string.IsNullOrEmpty(row.key)) return false;

        bool detached = false;
        foreach (var card in cards)
        {
            if (card.attachedTo != row.key || card.targetCopy < row.count) continue;
            card.attachedTo = null;
            card.targetCopy = 0;
            detached = true;
        }
        return detached;
    }

    public bool IsExample => libraryFile == null && (id == "sealed-sky" || id == "second-funeral");
    public string Objective
    {
        get
        {
            if (goal == null) return survive ? $"撑到第 {turns + 1} 回合的维持步骤" : $"在第 {turns} 回合清除步骤前获胜";
            string conditions = goal.mode == "all" ? "全部条件" : "任一条件";
            if (goal.checkAt == "survive") return $"存活至第 {turns + 1} 回合维持 · {conditions}";
            if (goal.checkAt == "win") return $"第 {turns} 回合清除前获胜，并满足{conditions}";
            return $"第 {turns} 回合清除前满足{conditions}";
        }
    }
    public string Brief => goal == null ? description :
        (goal.checkAt == "survive" ? $"在第 {turns+1} 回合的维持步骤开始或提前获胜时检查。\n" : "") +
        string.Join("\n",goal.conditions.Select(c=>"• "+c.Describe())) +
        (string.IsNullOrWhiteSpace(description) ? "" : "\n\n" + description.Trim());

    public void Validate()
    {
        goal?.Validate();
        if (version < 1 || version > Version) throw new InvalidDataException("无法读取这个版本的残局。");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 60) throw new ArgumentException("请填写 1～60 字的残局名称。");
        if (firstSide < 0 || firstSide > 1 || phase < 0 || phase >= Phases.Length ||
            turns < 1 || turns > 30 || humanLife < 1 || humanLife > 200 || aiLife < 1 || aiLife > 200)
            throw new ArgumentException("生命范围为 1～200，回合数范围为 1～30。");
        if (cards == null || cards.Count == 0) throw new ArgumentException("请至少向局面中加入一张牌。");
        int total = 0;
        foreach (var row in cards)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.name) || row.name.IndexOfAny(new[]{';','|','\r','\n'}) >= 0 ||
                row.side < 0 || row.side > 1 || row.zone < 0 || row.zone >= Zones.Length ||
                row.count < 1 || row.count > 100 || row.damage < 0 || row.damage > 99 ||
                row.plusCounters < 0 || row.plusCounters > 99 || row.counterValue < 0 || row.counterValue > 99 ||
                (row.HasState && row.zone != 0) || row.entryBatch<0 ||
                (row.entryBatch>0 && (row.zone!=0 || !orderedBattlefield || version<2)))
                throw new ArgumentException("请检查区域、卡名、横置状态和数量（每行 1～100 张）。");
            total += row.count;
            if (total > 500) throw new ArgumentException("此局面最多可放 500 张牌。");
        }
    }

    [Serializable] private sealed class Setup
    {
        public string title, goal;
        public int turns;
        public List<string> state = new();
        public List<Attachment> attachments = new();
    }

    [Serializable] private sealed class Attachment
    {
        public string name, targetName;
        public int seat, targetSeat, count;
    }

    public string EngineSetup()
    {
        Validate();
        var setup = new Setup { title = title.Trim(), turns = turns, goal = survive ? "Survive" : "Win" };
        setup.state.Add("turn=1");
        setup.state.Add("activeplayer=" + (firstSide == 0 ? "human" : "ai"));
        string openingPhase = Phases[phase];
        if (openingPhase == "COMBAT_BEGIN") {
            // Enter the step after the opening board exists so Forge initializes combat and fires beginning-of-combat triggers.
            setup.state.Add("activephase=MAIN1");
            setup.state.Add("activephaseadvance=COMBAT_BEGIN");
        } else {
            setup.state.Add("activephase=" + openingPhase);
        }
        setup.state.Add("humanlife=" + humanLife);
        setup.state.Add("ailife=" + aiLife);
        var needed = new HashSet<string>(cards.Select(r => r.name), StringComparer.Ordinal);
        var definitions = new Dictionary<string,CardDefinition>();
        foreach (var card in CardDatabaseRepository.LoadAll())
        {
            if (!card.forgeAmbiguous && needed.Remove(card.cardName)) definitions.Add(card.cardName,card);
            if (needed.Count == 0) break;
        }
        if (needed.Count > 0) throw new ArgumentException("卡库中找不到：" + string.Join("、",needed));
        var targets = new Dictionary<string,(PuzzleCard row,int firstId)>();
        var ranks = new Dictionary<PuzzleCard,int>();
        int rank=1;
        foreach (var batch in EntryBatches())
            foreach (var row in batch.OrderBy(r=>r.side==firstSide ? 0 : 1))
            {
                ranks.Add(row,rank);
                rank+=row.count;
            }
        int id = 1;
        foreach (var row in cards.Where(r => r.zone == 0))
        {
            if (!string.IsNullOrEmpty(row.key)) targets.Add(row.key,(row,id));
            id += row.count;
        }
        for (int side = 0; side < 2; side++)
            for (int zone = 0; zone < Zones.Length; zone++)
            {
                var entries = new List<string>();
                foreach (var row in cards.Where(r => r.side == side && r.zone == zone))
                {
                    if (zone != 0) { entries.AddRange(Enumerable.Repeat(row.name,row.count)); continue; }
                    var card = definitions[row.name];
                    string entry = row.BattlefieldEntry(card);
                    if (row.attachedTo == "human" || row.attachedTo == "ai")
                    {
                        entry += "|EnchantingPlayer:" + row.attachedTo;
                        setup.attachments.Add(new Attachment {name=row.name,seat=side+1,targetName="",
                            targetSeat=row.attachedTo=="human"?1:2,count=row.count});
                    }
                    else if (!string.IsNullOrEmpty(row.attachedTo))
                    {
                        if (!targets.TryGetValue(row.attachedTo,out var target) || target.row == row || row.targetCopy < 0 || row.targetCopy >= target.row.count)
                            throw new ArgumentException("请为「" + GamePreferences.CardName(card) + "」重新选择结附对象。");
                        entry += "|AttachedTo:" + (target.firstId + row.targetCopy);
                        setup.attachments.Add(new Attachment {name=row.name,seat=side+1,targetName=target.row.name,
                            targetSeat=target.row.side+1,count=row.count});
                    }
                    else if ((card.subtypes ?? "").Split(' ').Contains("Aura"))
                        throw new ArgumentException("请在「" + GamePreferences.CardName(card) + "」的状态中指定结附对象。");
                    for (int i=0;i<row.count;i++)
                    {
                        string copy = string.IsNullOrEmpty(row.key) ? entry : entry + "|Id:" + (targets[row.key].firstId + i);
                        if (orderedBattlefield) copy += "|PuzzleEntry:" + (ranks[row]+i);
                        if (orderedBattlefield && row.entryBatch>0) copy += "|PuzzleBatch:" + row.entryBatch;
                        entries.Add(copy);
                    }
                }
                setup.state.Add((side == 0 ? "human" : "ai") + Zones[zone] + "=" + string.Join(";", entries));
            }
        string json=JsonUtility.ToJson(setup);
        if(goal==null)return json;
        var data=Newtonsoft.Json.Linq.JObject.Parse(json);
        data["objective"]=Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(goal));
        return data.ToString(Newtonsoft.Json.Formatting.None);
    }

    public static List<LocalPuzzle> Examples()
    {
        var sky = new LocalPuzzle { id = "sealed-sky", title = "封锁线外", author = "MTGB", humanLife = 3, aiLife = 30,
            description = "你只剩3点生命。对手有30点生命，尚未中毒。辉光统领和梅梨莱都在场，你必须在这个回合突破封锁。" };
        foreach(string name in new[]{"Phyrexian Crusader","Death's Shadow","Tundra","Island"})
            sky.cards.Add(new PuzzleCard { name=name });
        sky.cards.Add(new PuzzleCard { name="Swamp",count=2 });
        foreach(string name in new[]{"Swords to Plowshares","Snap","Snuff Out","Dark Ritual","Hatred"})
            sky.cards.Add(new PuzzleCard { name=name,zone=1 });
        foreach(string name in new[]{"Blazing Archon","Melira, Sylvok Outcast"})
            sky.cards.Add(new PuzzleCard { name=name,side=1 });
        sky.cards.Add(new PuzzleCard { name="Mountain",zone=2,count=4 });
        sky.cards.Add(new PuzzleCard { name="Plains",side=1,zone=2,count=4 });

        var funeral = new LocalPuzzle { id = "second-funeral", title = "第二次葬礼", author = "MTGB", humanLife = 2, aiLife = 28,
            description = "你只剩2点生命。对手有28点生命，受圣洁地脉保护，白金天使躺在其坟墓场里。你必须在本回合取胜。" };
        foreach(string name in new[]{"Goblin Bombardment","Gray Merchant of Asphodel","Dauthi Slayer"})
            funeral.cards.Add(new PuzzleCard { name=name });
        funeral.cards.Add(new PuzzleCard { name="Geralf's Messenger",tapped=true,plusCounters=1 });
        funeral.cards.Add(new PuzzleCard { name="Swamp",count=4 });
        funeral.cards.Add(new PuzzleCard { name="Mountain",count=2 });
        foreach(string name in new[]{"Terminate","Living Death","Bojuka Bog","Faerie Macabre","Dark Ritual","Malakir Rebirth"})
            funeral.cards.Add(new PuzzleCard { name=name,zone=1 });
        foreach(string name in new[]{"Massacre Wurm","Murderous Redcap"})
            funeral.cards.Add(new PuzzleCard { name=name,zone=3 });
        funeral.cards.Add(new PuzzleCard { name="Anafenza, the Foremost",side=1 });
        funeral.cards.Add(new PuzzleCard { name="Platinum Emperion",side=1 });
        funeral.cards.Add(new PuzzleCard { name="Leyline of Sanctity",side=1 });
        funeral.cards.Add(new PuzzleCard { name="Platinum Angel",side=1,zone=3 });
        funeral.cards.Add(new PuzzleCard { name="Swamp",zone=2,count=4 });
        funeral.cards.Add(new PuzzleCard { name="Forest",side=1,zone=2,count=4 });
        return new List<LocalPuzzle> { sky,funeral };
    }
}

public static class LocalPuzzleStore
{
    public static string DirectoryPath => Path.Combine(PrintingKeyUtility.ApplicationRoot, "Puzzles");

    private static LocalPuzzle Read(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("请选择残局 JSON 文件。");
        if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidDataException("残局文件超过 2 MiB。");
        var puzzle = PuzzleFileJson.Read(File.ReadAllText(path));
        if (puzzle.cards.Where(c => !string.IsNullOrEmpty(c.key)).GroupBy(c => c.key).Any(g => g.Count() > 1))
            throw new InvalidDataException("残局中存在重复的牌张编号。");
        return puzzle;
    }

    private static IEnumerable<string> CandidatePaths(string filename)
    {
        string stem = Path.GetFileNameWithoutExtension(filename);
        for (int i = 1; ; i++)
            yield return Path.Combine(DirectoryPath, i == 1 ? filename : $"{stem} ({i}).json");
    }

    private static void PrepareDirectory()
    {
        Directory.CreateDirectory(DirectoryPath);
        string legacy = Path.Combine(Application.persistentDataPath, "Puzzles");
        string marker = Path.Combine(DirectoryPath, ".legacy-imported");
        if (!Directory.Exists(legacy) || File.Exists(marker) || Path.GetFullPath(legacy) == Path.GetFullPath(DirectoryPath)) return;
        string[] legacyFiles = Directory.GetFiles(legacy, "*.json");
        if (legacyFiles.Length == 0) return;
        foreach (string source in legacyFiles)
        {
            byte[] bytes = File.ReadAllBytes(source);
            foreach (string dest in CandidatePaths(Path.GetFileName(source)))
            {
                if (!File.Exists(dest)) { Write(dest, bytes, false); break; }
                if (bytes.SequenceEqual(File.ReadAllBytes(dest))) break;
            }
        }
        File.WriteAllText(marker, "");
    }

    public static List<LocalPuzzle> List(out int unreadable)
    {
        var puzzles = LocalPuzzle.Examples();
        unreadable = 0;
        PrepareDirectory();
        foreach (var path in Directory.GetFiles(DirectoryPath, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            try
            {
                var puzzle = Read(path);
                puzzle.libraryFile = Path.GetFileName(path);
                puzzles.Add(puzzle);
            }
            catch (Exception error) { unreadable++; Debug.LogWarning($"无法载入残局 {Path.GetFileName(path)}：{error.Message}"); }
        }
        return puzzles;
    }

    public static string SuggestedFilename(LocalPuzzle puzzle)
    {
        string name = string.Concat(puzzle.title.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).TrimEnd(' ', '.');
        if (name.Length == 0) name = "残局";
        if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) name = "_" + name;
        return name + ".json";
    }

    public static void Save(LocalPuzzle puzzle)
    {
        puzzle.Validate();
        if (puzzle.IsExample) throw new ArgumentException("请先将内置残局复制为自制残局。");
        PrepareDirectory();
        bool existing = puzzle.libraryFile != null;
        string path = existing ? Path.Combine(DirectoryPath, puzzle.libraryFile) :
            CandidatePaths(SuggestedFilename(puzzle)).First(p => !File.Exists(p));
        if (!Guid.TryParseExact(puzzle.id, "N", out _)) puzzle.id = Guid.NewGuid().ToString("N");
        Write(path, System.Text.Encoding.UTF8.GetBytes(PuzzleFileJson.Write(puzzle)), existing);
        puzzle.libraryFile = Path.GetFileName(path);
    }

    public static LocalPuzzle Import(string path)
    {
        path = Path.GetFullPath(path);
        var puzzle = Read(path);
        if (string.Equals(Path.GetDirectoryName(path), Path.GetFullPath(DirectoryPath), StringComparison.OrdinalIgnoreCase))
            puzzle.libraryFile = Path.GetFileName(path);
        else
        {
            puzzle.id = Guid.NewGuid().ToString("N");
            Save(puzzle);
        }
        return puzzle;
    }

    public static void Export(LocalPuzzle puzzle, string path, bool overwrite)
    {
        puzzle.Validate();
        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("残局文件名应以 .json 结尾。");
        Write(path, System.Text.Encoding.UTF8.GetBytes(PuzzleFileJson.Write(puzzle)), overwrite);
    }

    public static void Delete(LocalPuzzle puzzle)
    {
        if (puzzle.libraryFile == null) throw new InvalidOperationException("只可删除自制残局。");
        File.Delete(Path.Combine(DirectoryPath, puzzle.libraryFile));
    }

    private static void Write(string path, byte[] bytes, bool overwrite)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temp, bytes);
            if (overwrite && File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
