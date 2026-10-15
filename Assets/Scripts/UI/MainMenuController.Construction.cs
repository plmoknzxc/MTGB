using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class MainMenuController
{
    int constructionIdentity,constructionCompanion=-1,constructionTypes=-1;
    bool constructionHasCommander;
    readonly HashSet<string> constructionExemptions=new(StringComparer.Ordinal);
    readonly HashSet<string> constructionPartners=new(StringComparer.Ordinal);
    readonly Dictionary<string,int> constructionNames=new(StringComparer.Ordinal);
    string constructionFilterKey;
    Coroutine constructionFilterRefresh;
    DeckDefinition constructionSearchDeck;
    string commanderSearchSource,companionSearchSource;

    void RefreshConstructionFilters()
    {
        DeckCompanion.ClearMissing(currentDeck);
        constructionIdentity=0;constructionHasCommander=IsCommandZoneEditor&&DeckCommander.Has(currentDeck);
        constructionExemptions.Clear();constructionPartners.Clear();constructionNames.Clear();constructionTypes=-1;
        var commanders=currentDeck?.commanders?.Where(e=>e.count>0).Select(e=>DeckRepository.ResolveCard(e,cards)).ToArray()??Array.Empty<CardDefinition>();
        if(constructionHasCommander)foreach(var commander in commanders){
            var rule=DeckEditorRules.Get(commander);
            constructionIdentity|=rule?.identity??CardSearchMetadata.Get(commander)?.identity??0;
            if(rule?.exemptions!=null)constructionExemptions.UnionWith(rule.exemptions);
            if(commanders.Length==1&&rule?.partners!=null)constructionPartners.UnionWith(rule.partners);
        }
        constructionCompanion=DeckCompanion.Has(currentDeck)?DeckEditorRules.CompanionIndex(DeckRepository.ResolveCard(currentDeck.companion,cards)):-1;
        // A new designation applies a normal search criterion. Ordinary refreshes
        // must preserve an explicit opt-out or a search reset.
        string commanderSource=constructionHasCommander?string.Join("|",commanders.Select(c=>c?.cardName).OrderBy(n=>n,StringComparer.Ordinal)):null;
        string companionSource=constructionCompanion>=0?constructionCompanion.ToString():null;
        bool newDeck=!ReferenceEquals(constructionSearchDeck,currentDeck);
        bool searchChanged=newDeck||commanderSource!=commanderSearchSource||companionSource!=companionSearchSource;
        if(newDeck||commanderSource!=commanderSearchSource)advancedSearch.CommanderFit=commanderSource!=null;
        if(newDeck||companionSource!=companionSearchSource)advancedSearch.CompanionFit=companionSource!=null;
        constructionSearchDeck=currentDeck;commanderSearchSource=commanderSource;companionSearchSource=companionSource;
        foreach(var entry in (currentDeck?.cards??new List<DeckEntry>()).Concat(constructionHasCommander?currentDeck.commanders:Enumerable.Empty<DeckEntry>())){
            if(entry==null||entry.count<=0)continue;
            var card=DeckRepository.ResolveCard(entry,cards);if(card==null)continue;
            constructionNames.TryGetValue(card.cardName,out int count);constructionNames[card.cardName]=count+entry.count;
            var rule=DeckEditorRules.Get(card);if(rule!=null&&!rule.land)constructionTypes&=rule.types;
        }
        string key=constructionHasCommander+":"+constructionIdentity+":"+string.Join("|",commanders.Select(c=>c?.cardName))+":"+constructionCompanion+":"+(constructionCompanion==7?constructionTypes:-1)+(constructionCompanion==5?":"+string.Join("|",constructionNames.Keys.OrderBy(n=>n,StringComparer.Ordinal)):"");
        if(key==constructionFilterKey&&!searchChanged)return;
        constructionFilterKey=key;cachedCollectionQuery=null;collectionPage=0;
        if(constructionFilterRefresh!=null)StopCoroutine(constructionFilterRefresh);
        if(isActiveAndEnabled)constructionFilterRefresh=StartCoroutine(RefreshConstructionCollection());
    }
    IEnumerator RefreshConstructionCollection()
    {
        yield return null;
        while(!GamePreferences.Ready)yield return null;
        constructionFilterRefresh=null;
        if(collectionContent!=null&&deckBuilderGroup!=null&&deckBuilderGroup.gameObject.activeInHierarchy)RefreshCollectionGrid();
    }
    bool MatchesCommanderIdentity(CardDefinition card,bool includePartners)
    {
        if(!constructionHasCommander)return true;
        var rule=DeckEditorRules.Get(card);
        int? identity=rule?.identity??CardSearchMetadata.Get(card)?.identity;
        // Unknown DIY data stays visible, without claiming native validation.
        return !identity.HasValue||(identity.Value&~constructionIdentity)==0||constructionExemptions.Contains(card.cardName)||includePartners&&constructionPartners.Contains(card.cardName);
    }
    bool MatchesCompanionCard(CardDefinition card)
    {
        if(constructionCompanion<0)return true;
        var rule=DeckEditorRules.Get(card);if(rule==null)return true;
        if((rule.companions&(1<<constructionCompanion))==0)return false;
        return constructionCompanion!=7||rule.land||(rule.types&constructionTypes)!=0;
    }
    bool MatchesConstructionFilter(CardDefinition card)=>(!advancedSearch.CommanderFit||MatchesCommanderIdentity(card,true))
        &&(!advancedSearch.CompanionFit||MatchesCompanionCard(card)
        &&(constructionCompanion!=5||DeckEditorRules.Get(card)?.land!=false||!constructionNames.ContainsKey(card.cardName)));
    string ConstructionCardProblem(MainMenuCardTile tile)
    {
        if(!MatchesCommanderIdentity(tile.Card,false))return DeckCompanion.Text("标识色","Identity","固有色");
        if(tile.Zone==MainMenuCardTileZone.Sideboard||tile.Zone==MainMenuCardTileZone.Collection)return null;
        if(!MatchesCompanionCard(tile.Card))return DeckCompanion.Text("行侣","Companion","相棒");
        if(constructionCompanion==5&&DeckEditorRules.Get(tile.Card)?.land==false&&constructionNames.TryGetValue(tile.Card.cardName,out int count)&&count>1)return DeckCompanion.Text("行侣","Companion","相棒");
        return null;
    }
}
