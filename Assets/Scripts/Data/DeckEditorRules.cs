using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// Build-time facets exported by the pinned Forge engine, shared by all printings.
public static class DeckEditorRules
{
    [Serializable] public sealed class Entry
    {
        public string name;
        public int identity,types,companions;
        public bool commander,brawl,land;
        public string[] partners,exemptions;
    }
    [Serializable] sealed class Catalog { public string[] companions; public Entry[] cards; }
    static Dictionary<string,Entry> entries;
    static string[] companionNames;
    static Task loading;
    public static Task LoadAsync()
    {
        if(entries!=null)return Task.CompletedTask;
        if(loading!=null)return loading;
        string path=Path.Combine(Application.streamingAssetsPath,"DeckEditorRules.json");
        return loading=Read(path);
    }
    static async Task Read(string path)
    {
        try {
            var data=await Task.Run(()=>JsonUtility.FromJson<Catalog>(File.ReadAllText(path)));
            companionNames=data.companions;
            entries=data.cards.ToDictionary(e=>e.name,StringComparer.Ordinal);
        } finally {loading=null;}
    }
    public static Entry Get(CardDefinition card)=>card!=null&&!card.customContent&&!card.forgeAmbiguous&&entries!=null&&entries.TryGetValue(card.cardName,out var e)?e:null;
    public static int CompanionIndex(CardDefinition card)=>companionNames==null||card==null?-1:Array.IndexOf(companionNames,card.cardName);
    public static bool CanCommand(CardDefinition card,string format)
    {
        var e=Get(card);return e!=null&&(format=="brawl"?e.brawl:e.commander);
    }
    public static bool CanPartner(CardDefinition commander,CardDefinition candidate)
        =>Get(commander)?.partners?.Contains(candidate?.cardName,StringComparer.Ordinal)==true;
}
