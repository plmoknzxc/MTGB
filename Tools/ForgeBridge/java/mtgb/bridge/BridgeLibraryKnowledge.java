// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import com.google.common.eventbus.Subscribe;
import forge.game.card.Card;
import forge.game.card.CardView;
import forge.game.event.GameEventShuffle;
import forge.game.event.GameEventZone;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.util.*;

/** Seat-local knowledge of revealed cards; never a projection of a hidden zone. */
public final class BridgeLibraryKnowledge {
    private final Map<Integer,Map<Integer,Integer>> known = new HashMap<>();
    private final Map<Integer,Map<Integer,Set<Integer>>> hands = new HashMap<>();
    private final Map<Integer,Map<Integer,Set<Integer>>> revealedHands = new HashMap<>();
    // Observed faces can follow a native move, but never reveal hidden order.
    private final Map<Integer,Map<Integer,Set<Integer>>> libraryFaces = new HashMap<>();
    private final Map<Integer,Map<Integer,Set<Integer>>> departures = new HashMap<>();
    private final Map<Integer,Map<Integer,Set<Integer>>> revealedSideboards = new HashMap<>();
    private final Map<Integer,Map<Integer,Integer>> orderedTops = new HashMap<>();
    // Knowledge from an actual private look follows only this face-down object.
    private final Map<Integer,Map<Integer,String>> battlefieldFaces = new HashMap<>();
    synchronized void restarted() {
        known.clear();hands.clear();revealedHands.clear();libraryFaces.clear();departures.clear();
        revealedSideboards.clear();orderedTops.clear();battlefieldFaces.clear();
    }
    private static String faceIdentity(Card card) {
        return card.getGameTimestamp()+":"+card.getLayerTimestamp();
    }
    synchronized boolean knowsBattlefieldFace(Card card,int viewerSeat) {
        var memory=battlefieldFaces.get(viewerSeat);
        if(memory==null)return false;
        if(!card.isFaceDown() || card.getZone().getZoneType()!=ZoneType.Battlefield
                || !Objects.equals(memory.get(card.getId()),faceIdentity(card))) {
            memory.remove(card.getId());return false;
        }
        return true;
    }
    synchronized void remember(int seat,List<?> shown) {
        for(Object item:shown)if(item instanceof CardView cv)
            for(Player owner:ForgeBridge.game.getPlayers()) {
                for(Card card:owner.getZone(ZoneType.Battlefield).getCards(false))
                    if(card.getId()==cv.getId() && card.isFaceDown())
                        battlefieldFaces.computeIfAbsent(seat,k->new HashMap<>()).put(card.getId(),faceIdentity(card));
                var library=owner.getCardsIn(ZoneType.Library);
                if(library.stream().anyMatch(c->c.getId()==cv.getId()))
                    libraryFaces.computeIfAbsent(owner.getId(),k->new HashMap<>()).computeIfAbsent(cv.getId(),k->new HashSet<>()).add(seat);
                if(!library.isEmpty()&&library.get(0).getId()==cv.getId())
                    known.computeIfAbsent(seat,k->new HashMap<>()).put(owner.getId(),cv.getId());
                if(owner.getCardsIn(ZoneType.Hand).stream().anyMatch(c->c.getId()==cv.getId()))
                    revealedHands.computeIfAbsent(seat,k->new HashMap<>()).computeIfAbsent(owner.getId(),k->new HashSet<>()).add(cv.getId());
                if(owner.getCardsIn(ZoneType.Sideboard).stream().anyMatch(c->c.getId()==cv.getId()))
                    revealedSideboards.computeIfAbsent(owner.getId(),k->new HashMap<>()).computeIfAbsent(cv.getId(),k->new HashSet<>()).add(seat);
            }
    }
    synchronized void ordered(int seat,List<?> values) {
        if(values.isEmpty()||!(values.get(0) instanceof CardView first))return;
        for(Player owner:ForgeBridge.game.getPlayers())
            if(values.stream().allMatch(v->v instanceof CardView cv&&owner.getCardsIn(ZoneType.Library).stream().anyMatch(c->c.getId()==cv.getId())))
                orderedTops.computeIfAbsent(seat,k->new HashMap<>()).put(owner.getId(),first.getId());
    }
    synchronized void arranged(int seat,List<Card> shown,List<Card> top) {
        ordered(seat,new ArrayList<>(CardView.getCollection(top)));
        // Keeping the sole inspected card on top has no hidden permutation.
        if(shown.size()==1&&top.size()==1&&shown.get(0).getId()==top.get(0).getId())
            for(var entry:known.entrySet())if(Objects.equals(entry.getValue().get(top.get(0).getOwner().getId()),top.get(0).getId()))
                orderedTops.computeIfAbsent(entry.getKey(),k->new HashMap<>()).put(top.get(0).getOwner().getId(),top.get(0).getId());
    }
    synchronized List<Object> hand(Player owner,int viewerSeat,Player viewer) {
        var memory=revealedHands.computeIfAbsent(viewerSeat,k->new HashMap<>()).computeIfAbsent(owner.getId(),k->new HashSet<>());
        Set<Integer> current=new HashSet<>(),visible=new HashSet<>();List<Object> result=new ArrayList<>();
        for(Card c:owner.getCardsIn(ZoneType.Hand)) {
            current.add(c.getId());
            // Native viewing permissions remain authoritative. The memory only
            // retains identities this recipient already saw in this hand.
            if(c.getView().canBeShownTo(viewer.getView())||memory.contains(c.getId())) {
                result.add(ForgeBridge.card(c));visible.add(c.getId());
                if(c.getView().canBeShownTo(viewer.getView()))memory.add(c.getId());
            }
        }
        memory.retainAll(current);
        hands.computeIfAbsent(viewerSeat,k->new HashMap<>()).put(owner.getId(),visible);
        return result;
    }
    private void forget(int playerId) {for(var memory:known.values())memory.remove(playerId);}
    @Subscribe public synchronized void shuffle(GameEventShuffle event) {
        int owner=event.player().getId();forget(owner);
        libraryFaces.remove(owner);departures.remove(owner);
        for(var memory:orderedTops.values())memory.remove(owner);
        for(var memory:hands.values())memory.remove(owner);
    }
    @Subscribe public synchronized void changed(GameEventZone event) {
        if(event.player()==null)return;
        if(event.zoneType()==ZoneType.Battlefield && event.card()!=null
                && event.mode()==forge.game.event.EventValueChangeType.Removed)
            for(var memory:battlefieldFaces.values())memory.remove(event.card().getId());
        int ownerId=event.player().getId();
        if(event.zoneType()==ZoneType.Sideboard&&event.card()!=null&&event.mode()==forge.game.event.EventValueChangeType.Removed) {
            var ownerFaces=revealedSideboards.get(ownerId);
            var shown=ownerFaces==null?null:ownerFaces.remove(event.card().getId());
            if(shown!=null)departures.computeIfAbsent(ownerId,k->new HashMap<>()).put(event.card().getId(),shown);
        }
        if(event.card()!=null&&event.mode()==forge.game.event.EventValueChangeType.Added) {
            var moved=departures.getOrDefault(ownerId,Map.of()).get(event.card().getId());
            if(moved!=null) {
                if(event.zoneType()==ZoneType.Hand)for(int recipient:moved)
                    revealedHands.computeIfAbsent(recipient,k->new HashMap<>()).computeIfAbsent(ownerId,k->new HashSet<>()).add(event.card().getId());
                departures.get(ownerId).remove(event.card().getId());
            }
        }
        if(event.zoneType()!=ZoneType.Library)return;
        if(event.card()!=null&&event.mode()==forge.game.event.EventValueChangeType.Removed) {
            var ownerFaces=libraryFaces.get(ownerId);Set<Integer> recipients=new HashSet<>();
            if(ownerFaces!=null){var shown=ownerFaces.remove(event.card().getId());if(shown!=null)recipients.addAll(shown);}
            for(var entry:known.entrySet())if(Objects.equals(entry.getValue().get(ownerId),event.card().getId()))recipients.add(entry.getKey());
            if(!recipients.isEmpty())departures.computeIfAbsent(ownerId,k->new HashMap<>()).put(event.card().getId(),recipients);
        }
        if(event.mode()==forge.game.event.EventValueChangeType.ComplexUpdate){libraryFaces.remove(ownerId);departures.remove(ownerId);}
        forget(ownerId);
        if(event.card()==null||event.mode()!=forge.game.event.EventValueChangeType.Added)return;
        for(Player owner:ForgeBridge.game.getPlayers())if(owner.getId()==ownerId) {
            boolean returnedHand=hands.values().stream().anyMatch(m->m.getOrDefault(ownerId,Set.of()).contains(event.card().getId()));
            if(returnedHand)for(var entry:revealedHands.entrySet()) {
                Player recipient=ForgeBridge.guis.get(entry.getKey()-1).controller.getPlayer();
                if(recipient!=owner&&owner.getControllingPlayer()!=recipient)entry.getValue().remove(ownerId);
            }
            var library=owner.getCardsIn(ZoneType.Library);
            if(library.isEmpty()||library.get(0).getId()!=event.card().getId())return;
            for(var entry:orderedTops.entrySet())if(Objects.equals(entry.getValue().get(ownerId),event.card().getId()))
                known.computeIfAbsent(entry.getKey(),k->new HashMap<>()).put(ownerId,event.card().getId());
            // A visible hand card placed on top remains known to that viewer.
            // Previously visible hands never grant visibility to the rest of a library.
            for(var entry:hands.entrySet()) {
                Player recipient=ForgeBridge.guis.get(entry.getKey()-1).controller.getPlayer();
                // The opponent cannot track which formerly revealed card was
                // returned by a hidden hand choice such as Brainstorm.
                if((recipient==owner||owner.getControllingPlayer()==recipient)&&entry.getValue().getOrDefault(ownerId,Set.of()).contains(event.card().getId()))
                    known.computeIfAbsent(entry.getKey(),k->new HashMap<>()).put(ownerId,event.card().getId());
            }
        }
    }
    synchronized Map<String,Object> top(Player owner,int viewerSeat,Player viewer) {
        hand(owner,viewerSeat,viewer);
        var library=owner.getCardsIn(ZoneType.Library);
        if(library.isEmpty()){forget(owner.getId());return Map.of();}
        Card top=library.get(0);
        var ownOrder=orderedTops.get(viewerSeat);Integer ordered=ownOrder==null?null:ownOrder.remove(owner.getId());
        if(Objects.equals(ordered,top.getId()))known.computeIfAbsent(viewerSeat,k->new HashMap<>()).put(owner.getId(),top.getId());
        for(var memory:known.values())if(!Objects.equals(memory.get(owner.getId()),top.getId()))memory.remove(owner.getId());
        if(top.getView().canBeShownTo(viewer.getView()))
            known.computeIfAbsent(viewerSeat,k->new HashMap<>()).put(owner.getId(),top.getId());
        if(!Objects.equals(known.getOrDefault(viewerSeat,Map.of()).get(owner.getId()),top.getId()))return Map.of();
        boolean publicTop=true;
        for(int seat=1;seat<=ForgeBridge.guis.size();seat++)
            // Independent private looks are not authorization for a spectator.
            publicTop &= top.getView().canBeShownTo(ForgeBridge.guis.get(seat-1).controller.getPlayer().getView());
        return Map.of("libraryTop",ForgeBridge.card(top),"libraryTopPublic",publicTop);
    }
}
