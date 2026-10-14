using System;
using System.Linq;
using UnityEngine;

public sealed partial class OnlineMatchService
{
    private static void ApplySeriesRemovals(OnlineRoomState room)
    {
        var removals=room.match.MatchRemovals;
        var ownership=room.config.useAnte?room.match.MatchOwnership:Array.Empty<ForgeMatchOwnership>();
        if((removals==null||removals.Length==0)&&(ownership==null||ownership.Length==0))return;
        // Build both complete results before mutating either player.
        var changes=OnlineSeriesDeckChanges.Apply(room.members[1].deck,room.members[2].deck,removals,ownership);
        foreach(var member in room.members.Values.Where(m=>!m.IsSpectator)) {
            member.deck=changes[member.seat-1];
            member.registeredDeck=CloneDeck(member.deck);
            member.seriesDeckChanged=true;
        }
    }

    private static OnlineRoomConfig SeriesValidationConfig(OnlineRoomState room,OnlineRoomMember member)
    {
        var config=CloneRoomConfig(room.config);
        if(room.series?.finished!=true&&member.seriesDeckChanged&&member.registeredDeck!=null) {
            config.minimumMainDeckSize=Math.Max(0,Math.Min(config.minimumMainDeckSize,DeckRepository.GetTotalCount(member.registeredDeck,false)));
            config.authorizedSeriesPool=member.registeredDeck;
        }
        return config;
    }

    private static void RestoreSeriesDecks(OnlineRoomState room)
    {
        foreach(var member in room.members.Values) {
            if(member.seriesOriginalDeck!=null)member.deck=CloneDeck(member.seriesOriginalDeck);
            member.seriesOriginalDeck=null;member.registeredDeck=null;member.seriesDeckChanged=false;
        }
    }
}
