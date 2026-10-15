using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public static class CardDatabaseRepository
{
    private static readonly List<CardDefinition> LoadedCards = new List<CardDefinition>();
    private static readonly List<string> loadWarnings = new List<string>();
    public static IReadOnlyList<string> LoadWarnings => loadWarnings;
    private static bool loaded;
    private static Dictionary<string,CardDefinition> printingIndex;
    private sealed class SnapshotIndex { public int Count; public Dictionary<string,CardDefinition> Cards; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IReadOnlyList<CardDefinition>,SnapshotIndex> snapshotIndexes=new();
    // Call after replacing an owned catalog snapshot. Its entries remain immutable
    // until the next registration; the weak key does not retain closed menus.
    internal static void RegisterPrintingSnapshot(IReadOnlyList<CardDefinition> source)
    {
        var index=new Dictionary<string,CardDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in source)if(item!=null){string key=item.setCode+"/"+item.CollectorNumber;if(!index.ContainsKey(key))index.Add(key,item);}
        snapshotIndexes.Remove(source);snapshotIndexes.Add(source,new SnapshotIndex{Count=source.Count,Cards=index});
    }
    internal static bool TryResolveSharedPrinting(IReadOnlyList<CardDefinition> source,string key,out CardDefinition card)
    {
        card=null;
        if(source!=null&&snapshotIndexes.TryGetValue(source,out var snapshot)&&snapshot.Count==source.Count){snapshot.Cards.TryGetValue(key,out card);if(card?.forgeAmbiguous==true)card=null;return true;}
        if(!loaded||!ReferenceEquals(source,LoadedCards))return false;
        if(printingIndex==null){
            printingIndex=new Dictionary<string,CardDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in LoadedCards)if(item!=null){string printing=item.setCode+"/"+item.CollectorNumber;if(!printingIndex.ContainsKey(printing))printingIndex.Add(printing,item);}
        }
        printingIndex.TryGetValue(key,out card);if(card?.forgeAmbiguous==true)card=null;
        return true;
    }
    public static async Task<IReadOnlyList<CardDefinition>> LoadAllAsync(bool forceReload, CancellationToken cancellation, Action<string> progress)
    {
        if (loaded && !forceReload) return LoadedCards;
        if (forceReload) ForgeCardCatalog.InvalidateVerification();
        string root = ForgeCardCatalog.Root;
        string flavorPath = Path.Combine(Application.streamingAssetsPath, "CardFlavor.jsonl");
        var warnings = new List<string>();
        var result = await Task.Run(() =>
        {
            if (!forceReload) return ForgeCatalogStore.Load(root, warnings, cancellation);
            var catalog = ForgeCardCatalog.Load(root, warnings, cancellation, progress, verifyRuntime: true);
            CardFlavorCatalog.Apply(catalog, flavorPath, cancellation);
            return catalog;
        }, cancellation);
        cancellation.ThrowIfCancellationRequested();
        // Resume on Unity's main thread before touching caches or Unity objects.
        Clear();
        LoadedCards.AddRange(result);
        loadWarnings.AddRange(warnings);
        loaded = true;
        return LoadedCards;
    }
    public static IReadOnlyList<CardDefinition> LoadAll(bool forceReload = false)
    {
        if (loaded && !forceReload) return LoadedCards;

        if (forceReload) ForgeCardCatalog.InvalidateVerification();

        Clear();
        LoadedCards.AddRange(forceReload
            ? ForgeCardCatalog.Load(ForgeCardCatalog.Root, loadWarnings, CancellationToken.None, null, verifyRuntime: true)
            : ForgeCatalogStore.Load(ForgeCardCatalog.Root, loadWarnings, CancellationToken.None));
        if (forceReload) CardFlavorCatalog.Apply(LoadedCards, Path.Combine(Application.streamingAssetsPath, "CardFlavor.jsonl"), CancellationToken.None);
        loaded = true;
        Debug.Log($"Loaded {LoadedCards.Count} Forge printings.");
        return LoadedCards;
    }

    public static CardDefinition FindById(int cardId)
    {
        IReadOnlyList<CardDefinition> cards = LoadAll();
        CardDefinition match = null;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].cardID != cardId) continue;
            if (match != null) return null; // A library-local ID cannot identify an ambiguous printing.
            match = cards[i];
        }
        return match;
    }

    public static CardDefinition FindByPrintingKey(string setCode, string collectorNumber)
    {
        if (string.IsNullOrWhiteSpace(setCode) || string.IsNullOrWhiteSpace(collectorNumber))
        {
            return null;
        }

        string expectedSet = setCode.Trim();
        string expectedCollector = collectorNumber.Trim();
        IReadOnlyList<CardDefinition> cards = LoadAll();
        for (int i = 0; i < cards.Count; i++)
        {
            CardDefinition card = cards[i];
            if (card != null
                && string.Equals(card.setCode, expectedSet, StringComparison.OrdinalIgnoreCase)
                && string.Equals(card.CollectorNumber, expectedCollector, StringComparison.OrdinalIgnoreCase))
            {
                return card.forgeAmbiguous ? null : card;
            }
        }

        return null;
    }

    public static void Clear()
    {
        printingIndex=null;
        CardArtVersions.Reset();
        for (int i = 0; i < LoadedCards.Count; i++)
        {
            if (LoadedCards[i] == null) continue;
            if (LoadedCards[i]?.artworkRuntime == true && LoadedCards[i].artwork != null)
            {
                Texture2D texture = LoadedCards[i].artwork.texture;
                UnityEngine.Object.Destroy(LoadedCards[i].artwork);
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
        }
        LoadedCards.Clear();
        loadWarnings.Clear();
        loaded = false;
    }

}
