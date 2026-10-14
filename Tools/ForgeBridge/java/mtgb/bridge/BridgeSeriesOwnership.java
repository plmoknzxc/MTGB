package mtgb.bridge;

import com.google.common.eventbus.Subscribe;
import forge.game.card.Card;
import forge.game.event.GameEventAnteCardsSelected;
import forge.game.zone.ZoneType;
import forge.item.PaperCard;
import java.util.*;

/** Read-only physical registration and final ownership; the host owns the temporary BO3 pool. */
final class BridgeSeriesOwnership {
    private record Original(int owner, String printing, String section, Card card) {}
    private final Map<Integer,Original> originals = new LinkedHashMap<>();
    private final Map<Integer,Integer> displayedOwners = new HashMap<>();
    private boolean captured;

    @Subscribe public void anteSelected(GameEventAnteCardsSelected event) { capture(); }

    static String section(ZoneType zone) {
        return switch(zone) {
            case Sideboard -> "Sideboard";
            case AttractionDeck -> "Attractions";
            case ContraptionDeck -> "Contraptions";
            default -> "Main";
        };
    }

    void capture() {
        if (captured) return;
        captured=true;
        for (var player : ForgeBridge.game.getRegisteredPlayers()) for (ZoneType zone : ZoneType.values())
            for (Card card : player.getCardsIn(zone)) {
                if (card.isToken() || !card.isCollectible() || !(card.getPaperCard() instanceof PaperCard paper)) continue;
                int owner=ForgeBridge.game.getRegisteredPlayers().indexOf(card.getOwner())+1;
                if (owner<1 || owner>2) continue;
                originals.putIfAbsent(card.getId(),new Original(owner,ForgeCatalog.key(paper),section(zone),card));
                displayedOwners.putIfAbsent(card.getId(),owner);
            }
    }

    void publishChanges() {
        if(!captured)return;
        for(var player:ForgeBridge.game.getRegisteredPlayers())for(ZoneType zone:ZoneType.values())for(Card card:player.getCardsIn(zone)) {
            Integer previous=displayedOwners.get(card.getId());if(previous==null)continue;
            int owner=ForgeBridge.game.getRegisteredPlayers().indexOf(card.getOwner())+1;
            if(owner<1||owner>2||owner==previous)continue;
            displayedOwners.put(card.getId(),owner);
            long id=ForgeBridge.noticeSequence.incrementAndGet();
            for(var gui:ForgeBridge.guis) {
                Map<String,Object> notice=new LinkedHashMap<>();notice.put("id",id);notice.put("kind","ownership");
                notice.put("actorSeat",owner);notice.put("publicNotice",true);notice.put("sourceCard",ForgeBridge.card(card));
                notice.put("value",card.getName());gui.notices.addLast(notice);while(gui.notices.size()>16)gui.notices.removeFirst();
            }
        }
    }

    Map<String,Object> removal(Card card, boolean inventory) {
        Original original=originals.get(card.getId());
        if (original==null) return null;
        return Map.of("cardId",card.getId(),"ownerSeat",original.owner(),"printingKey",original.printing(),
            "fromSideboard",original.section().equals("Sideboard"),"fromSection",original.section(),"fromInventory",inventory);
    }

    List<Object> snapshot(int winner) {
        if (!captured) return List.of();
        Map<Integer,Integer> finalOwners=new LinkedHashMap<>();
        for (var player : ForgeBridge.game.getRegisteredPlayers()) {
            for (ZoneType zone : ZoneType.values()) for (Card card : player.getCardsIn(zone))
                finalOwners.put(card.getId(),ForgeBridge.game.getRegisteredPlayers().indexOf(card.getOwner())+1);
        }
        // Native Match awards the losing player's actual Ante zone after play returns.
        // The bridge can end earlier, so project that exact payout without mutating native decks.
        if (winner>=1 && winner<=2) for (var player : ForgeBridge.game.getRegisteredPlayers()) {
            int zoneSeat=ForgeBridge.game.getRegisteredPlayers().indexOf(player)+1;
            if (zoneSeat==winner) continue;
            for (Card card : player.getCardsIn(ZoneType.Ante)) if (card.isCollectible()) finalOwners.put(card.getId(),winner);
        }
        Set<Integer> removed=new HashSet<>();
        for (Object row : ForgeBridge.matchRemovals.snapshot()) removed.add((Integer)((Map<?,?>)row).get("cardId"));
        List<Object> result=new ArrayList<>();
        for (var entry : originals.entrySet()) {
            if (removed.contains(entry.getKey())) continue;
            Original original=entry.getValue();
            int owner=finalOwners.getOrDefault(entry.getKey(),ForgeBridge.game.getRegisteredPlayers().indexOf(original.card().getOwner())+1);
            if (owner<1 || owner>2) throw new IllegalStateException("Unresolved physical owner: "+entry.getKey());
            if (owner==original.owner()) continue;
            result.add(Map.of("cardId",entry.getKey(),"fromOwnerSeat",original.owner(),"toOwnerSeat",owner,
                "printingKey",original.printing(),"fromSideboard",original.section().equals("Sideboard"),"fromSection",original.section()));
        }
        return result;
    }
}
