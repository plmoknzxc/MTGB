using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

// Prepared display database. All rules remain owned by Forge; no executable effects are stored.
// Strings are shared across printings, so repeated names/rules are decoded just once.
public static class ForgeCatalogStore
{
    public const string FileName = "catalog.mtgbcache";
    const int Magic = 0x4247544d;
    const int Version = 1;
    const int EndMarker = 0x454e4421;
    const int MaxCount = 2000000;

    public static List<CardDefinition> Load(string root, List<string> warnings, CancellationToken cancellation)
    {
        var config = ForgeCardCatalog.ReadConfigMetadata(root);
        return Read(Path.Combine(root, FileName), config.catalogHash, root, warnings, cancellation);
    }

    public static List<CardDefinition> Read(string path, string catalogHash, string sourceRoot,
        List<string> warnings, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!File.Exists(path)) throw new FileNotFoundException("缺少预生成卡牌数据库，请重新生成或安装完整游戏资源。", path);
        var file = new FileInfo(path);
        if (file.Length > 512L * 1024 * 1024) throw new InvalidDataException("卡牌数据库过大。");
        using (var stream = new MemoryStream(File.ReadAllBytes(path), false))
        using (var reader = new BinaryReader(stream, Encoding.UTF8))
        {
            ReadHeader(reader, catalogHash);
            ReadString(reader); // Flavor source digest, used by the build pipeline only.
            int stringCount = Count(reader);
            var strings = new string[stringCount];
            if (stringCount < 2) throw new InvalidDataException("卡牌字符串表无效。");
            strings[0] = null; strings[1] = string.Empty;
            for (int i = 2; i < stringCount; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                strings[i] = ReadString(reader);
            }
            string S()
            {
                int id = reader.ReadInt32();
                if ((uint)id >= (uint)strings.Length) throw new InvalidDataException("卡牌字符串索引无效。");
                return strings[id];
            }
            int count = Count(reader);
            if (count == 0) throw new InvalidDataException("卡牌数据库为空。");
            var cards = new List<CardDefinition>(count);
            string sourcePath = Path.Combine(sourceRoot, "catalog.jsonl");
            var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var c = new CardDefinition
                {
                    cardID = reader.ReadInt32(), cardName = S(), forgeSet = S(),
                    forgeArtIndex = reader.ReadInt32(), forgeAmbiguous = reader.ReadBoolean(),
                    forgeDeckLimit = reader.ReadInt32(), forgeSplitType = S(), oracleID = S(),
                    setCode = S(), collectorNumberText = S(), cardTypes = (CardType)reader.ReadInt32(),
                    supertypes = (CardSupertype)reader.ReadInt32(), subtypes = S(), description = S(), manaCost = S(),
                    attack = reader.ReadInt32(), defence = reader.ReadInt32(), loyalty = reader.ReadInt32(),
                    battleDefense = reader.ReadInt32(), flavorText = S(), sourceDatabasePath = sourcePath
                };
                int faces = Count(reader, 64);
                if (faces == 0) throw new InvalidDataException("卡牌缺少牌面。");
                c.features.Capacity = faces;
                for (int f = 0; f < faces; f++)
                {
                    var face = new CardFeatureDefinition
                    {
                        faceIndex = reader.ReadInt32(), featureIndex = reader.ReadInt32(), role = (CardFeatureRole)reader.ReadInt32(),
                        cardName = S(), cardTypes = (CardType)reader.ReadInt32(), supertypes = (CardSupertype)reader.ReadInt32(),
                        subtypes = S(), description = S(), manaCost = S(), attack = reader.ReadInt32(), defence = reader.ReadInt32(),
                        loyalty = reader.ReadInt32(), battleDefense = reader.ReadInt32(), powerExpression = S(), toughnessExpression = S()
                    };
                    if (!string.IsNullOrEmpty(face.description))
                        face.ruleEntries.Add(new CardRuleEntryDefinition { text = face.description });
                    c.features.Add(face);
                }
                if (c.forgeAmbiguous) ambiguous.Add(c.PrintingKey);
                cards.Add(c);
            }
            if (reader.ReadInt32() != EndMarker || stream.Position != stream.Length)
                throw new InvalidDataException("卡牌数据库内容不完整。");
            if (ambiguous.Count > 0) warnings.Add($"Forge 目录含 {ambiguous.Count} 个歧义印刷键；这些条目不能提交对局。");
            return cards;
        }
    }

    static void ReadHeader(BinaryReader reader, string catalogHash)
    {
        if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version)
            throw new InvalidDataException("卡牌数据库版本不匹配，请重新生成或更新游戏资源。");
        if (ReadString(reader) != catalogHash)
            throw new InvalidDataException("卡牌数据库与 Forge 版本不匹配，请重新生成或更新游戏资源。");
    }

    static int Count(BinaryReader reader, int maximum = MaxCount)
    {
        int value = reader.ReadInt32();
        if (value < 0 || value > maximum) throw new InvalidDataException("卡牌数据库记录数无效。");
        return value;
    }

    static string ReadString(BinaryReader reader)
    {
        int length = Count(reader, 1024 * 1024);
        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length) throw new EndOfStreamException("卡牌数据库被截断。");
        return Encoding.UTF8.GetString(bytes);
    }

#if UNITY_EDITOR
    public static bool MatchesSources(string path, string catalogHash, string flavorHash)
    {
        try
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                ReadHeader(reader, catalogHash);
                return ReadString(reader) == flavorHash;
            }
        }
        catch (IOException) { return false; }
        catch (InvalidDataException) { return false; }
    }

    public static void Write(string path, string catalogHash, string flavorHash, IReadOnlyList<CardDefinition> cards)
    {
        var strings = new List<string> { null, string.Empty };
        var ids = new Dictionary<string, int>(StringComparer.Ordinal) { [string.Empty] = 1 };
        int Id(string value)
        {
            if (value == null) return 0;
            if (!ids.TryGetValue(value, out int id)) { id = strings.Count; ids.Add(value, id); strings.Add(value); }
            return id;
        }
        // Collect using the same record writer; the second pass emits stable string indices.
        void Records(BinaryWriter w)
        {
            void S(string s) { w.Write(Id(s)); }
            w.Write(cards.Count);
            foreach (var c in cards)
            {
                w.Write(c.cardID); S(c.cardName); S(c.forgeSet); w.Write(c.forgeArtIndex); w.Write(c.forgeAmbiguous);
                w.Write(c.forgeDeckLimit); S(c.forgeSplitType); S(c.oracleID); S(c.setCode); S(c.collectorNumberText);
                w.Write((int)c.cardTypes); w.Write((int)c.supertypes); S(c.subtypes); S(c.description); S(c.manaCost);
                w.Write(c.attack); w.Write(c.defence); w.Write(c.loyalty); w.Write(c.battleDefense); S(c.flavorText);
                w.Write(c.features.Count);
                foreach (var f in c.features)
                {
                    w.Write(f.faceIndex); w.Write(f.featureIndex); w.Write((int)f.role); S(f.cardName);
                    w.Write((int)f.cardTypes); w.Write((int)f.supertypes); S(f.subtypes); S(f.description); S(f.manaCost);
                    w.Write(f.attack); w.Write(f.defence); w.Write(f.loyalty); w.Write(f.battleDefense);
                    S(f.powerExpression); S(f.toughnessExpression);
                }
            }
        }
        using (var sink = new BinaryWriter(Stream.Null)) Records(sink);
        string temp = path + ".tmp";
        try
        {
            using (var file = File.Create(temp))
            using (var writer = new BinaryWriter(file, Encoding.UTF8))
            {
                void S(string value) { var bytes = Encoding.UTF8.GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes); }
                writer.Write(Magic); writer.Write(Version); S(catalogHash); S(flavorHash);
                writer.Write(strings.Count);
                for (int i = 2; i < strings.Count; i++) S(strings[i]);
                Records(writer); writer.Write(EndMarker);
            }
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
#endif
}
