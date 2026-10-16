using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

// Authority catalog consumers need identity, construction and companion metadata,
// while Forge owns rule faces. The client read must remain complete.
public static class ServerCatalogMemoryValidation
{
    public static async void Run()
    {
        string output = ".tools/server-memory-1016/catalog-validation.txt";
        File.WriteAllText(output, "running");
        try
        {
            string root = ForgeCardCatalog.Root;
            string hash = JsonUtility.FromJson<ForgeCardCatalog.RuntimeConfig>(File.ReadAllText(Path.Combine(root, "runtime.json"))).catalogHash;
            List<CardDefinition> authorityForGuard = null;
            string report = await Task.Run(() =>
            {
                var expectedWarnings = new List<string>();
                var actualWarnings = new List<string>();
                var full = ForgeCatalogStore.Read(Path.Combine(root, ForgeCatalogStore.FileName), hash, root, expectedWarnings, CancellationToken.None);
                var authority = ForgeCatalogStore.Read(Path.Combine(root, ForgeCatalogStore.FileName), hash, root, actualWarnings, CancellationToken.None, true);
                authorityForGuard = authority;
                if (authority.Count != full.Count || string.Join("\n", actualWarnings) != string.Join("\n", expectedWarnings)) throw new Exception("Catalog identities/warnings changed.");
                int companionPrintings = 0, faceObjects = 0, authorityFaces = 0;
                FieldInfo[] fields = typeof(CardDefinition).GetFields(BindingFlags.Instance | BindingFlags.Public);
                for (int i = 0; i < full.Count; i++)
                {
                    CardDefinition a = full[i], b = authority[i];
                    faceObjects += a.features.Count;
                    authorityFaces += b.features?.Count ?? 0;
                    if (a.PrintingKey != b.PrintingKey) throw new Exception("Printing changed: " + a.PrintingKey);
                    foreach (var field in fields)
                    {
                        if (field.Name == "features" || field.Name == "description" || field.Name == "flavorText") continue;
                        if (!Equals(field.GetValue(a), field.GetValue(b))) throw new Exception("Authority metadata changed: " + a.PrintingKey + "/" + field.Name);
                    }
                    bool companion = DeckCompanion.IsCompanion(a);
                    if (companion) companionPrintings++;
                    if (DeckCompanion.IsCompanion(b) != companion) throw new Exception("Companion classification changed: " + a.PrintingKey);
                    if (companion && a.description != b.description) throw new Exception("Companion text changed.");
                    if (!companion && b.description != null) throw new Exception("Unneeded authority display rules retained.");
                    if (b.features != null) throw new Exception("Authority still creates display face collections: " + a.PrintingKey);
                    if (DeckAuxiliary.IsAttraction(a) != DeckAuxiliary.IsAttraction(b) || DeckAuxiliary.IsContraption(a) != DeckAuxiliary.IsContraption(b)) throw new Exception("Auxiliary classification changed.");
                }
                if (companionPrintings < 10 || faceObjects < 90000 || authorityFaces != 0) throw new Exception("Insufficient positive input.");
                ValidateAuthorityBoundaries(full.Find(c => c.features.Count == 1), hash);
                return "PASS: " + full.Count + " printing identities and all authority root fields matched; " + companionPrintings + " companion printings preserved; " + faceObjects + " display faces omitted only in authority mode.\nClient complete face data and auxiliary classifications preserved.";
            });
            ValidateDeckGuard(authorityForGuard);
            report += "\nPASS: actual authority ValidateDeck accepts a Forest/Lurrus deck; rejects ordinary-card companion, missing sideboard reference and invalid companion count; previous static catalog restored.";
            File.WriteAllText(output, report); Debug.Log(report);
        }
        catch (Exception error) { File.WriteAllText(output, "FAIL: " + error); Debug.LogWarning("[ServerCatalogValidation] " + error.Message); }
    }

    static void ValidateAuthorityBoundaries(CardDefinition sample, string hash)
    {
        string path = ".tools/server-memory-1016/authority-boundary.cache";
        ForgeCatalogStore.Write(path, hash, string.Empty, new[] { sample });
        byte[] valid = File.ReadAllBytes(path);
        void Reject(byte[] bytes)
        {
            File.WriteAllBytes(path, bytes);
            foreach (bool authority in new[] { false, true })
            {
                try { ForgeCatalogStore.Read(path, hash, string.Empty, new List<string>(), CancellationToken.None, authority); }
                catch (Exception error) when (error is IOException || error is InvalidDataException) { continue; }
                throw new Exception("Invalid face/record boundary accepted, authority=" + authority);
            }
        }
        // A v1 face is fifteen Int32 values, followed by the final marker.
        // Its name index is deliberately unused by the authority projection.
        byte[] badIndex = (byte[])valid.Clone();
        BitConverter.GetBytes(int.MaxValue).CopyTo(badIndex, valid.Length - 64 + 12); Reject(badIndex);
        byte[] noFace = (byte[])valid.Clone();
        BitConverter.GetBytes(0).CopyTo(noFace, valid.Length - 68); Reject(noFace);
        var truncated = new byte[valid.Length - 3]; Array.Copy(valid, truncated, truncated.Length); Reject(truncated);
        var trailing = new byte[valid.Length + 1]; Array.Copy(valid, trailing, valid.Length); Reject(trailing);
        File.WriteAllBytes(path, valid);
    }

    static void ValidateDeckGuard(List<CardDefinition> authority)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        Type repository = typeof(CardDatabaseRepository);
        var loadedField = repository.GetField("loaded", flags);
        var indexField = repository.GetField("printingIndex", flags);
        var cards = (List<CardDefinition>)repository.GetField("LoadedCards", flags).GetValue(null);
        var previous = cards.ToArray(); object wasLoaded = loadedField.GetValue(null), index = indexField.GetValue(null);
        try
        {
            cards.Clear(); cards.AddRange(authority); loadedField.SetValue(null, true); indexField.SetValue(null, null);
            CardDefinition forest = authority.Find(c => c.cardName == "Forest" && !c.forgeAmbiguous);
            CardDefinition lurrus = authority.Find(c => c.cardName == "Lurrus of the Dream-Den" && !c.forgeAmbiguous);
            DeckEntry Entry(CardDefinition card, int count) => new DeckEntry { setCode = card.setCode, collectorNumber = card.CollectorNumber, cardId = card.cardID, count = count };
            var deck = new DeckDefinition { name = "Authority companion metadata regression" };
            deck.cards.Add(Entry(forest, 60)); deck.sideboard.Add(Entry(lurrus, 1)); deck.companion = Entry(lurrus, 1);
            var service = (OnlineMatchService)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(OnlineMatchService));
            var validate = typeof(OnlineMatchService).GetMethod("ValidateDeck", BindingFlags.Instance | BindingFlags.NonPublic);
            bool Accepts()
            {
                object[] args = { deck, new OnlineRoomConfig { formatId = "none" }, null };
                return (bool)validate.Invoke(service, args);
            }
            if (!Accepts()) throw new Exception("Authority rejected valid companion metadata.");
            deck.companion = Entry(forest, 1);
            if (Accepts()) throw new Exception("Ordinary card accepted as companion.");
            deck.companion = Entry(lurrus, 1); deck.sideboard.Clear();
            if (Accepts()) throw new Exception("Companion missing from sideboard accepted.");
            deck.sideboard.Add(Entry(lurrus, 1)); deck.companion.count = 2;
            if (Accepts()) throw new Exception("Invalid companion count accepted.");
        }
        finally
        {
            cards.Clear(); cards.AddRange(previous); loadedField.SetValue(null, wasLoaded); indexField.SetValue(null, index);
        }
    }
}
