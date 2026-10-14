package mtgb.bridge;

import com.google.common.eventbus.Subscribe;
import forge.game.card.Card;
import forge.game.event.GameEventRandomLog;
import forge.game.zone.ZoneType;
import forge.item.PaperCard;
import java.util.*;

/** Capture the engine's exact affected objects before they cease to exist. No inventory writes. */
final class BridgeMatchRemovals {
    private final Map<Integer,Map<String,Object>> removed = new LinkedHashMap<>();

    @Subscribe public void logged(GameEventRandomLog event) {
        if (event.removedFromMatch() == null || event.sourceAbility() == null) return;
        synchronized (ForgeBridge.class) {
            var source = event.sourceAbility().getHostCard();
            List<Object> affected = new ArrayList<>();
            for (Card card : event.removedFromMatch()) {
                affected.add(ForgeBridge.card(card));
                if (!card.isCollectible() || card.isToken() || !(card.getPaperCard() instanceof PaperCard paper)) continue;
                int owner = ForgeBridge.game.getRegisteredPlayers().indexOf(card.getOwner()) + 1;
                if (owner < 1 || owner > 2) continue;
                Map<String,Object> original=ForgeBridge.seriesOwnership.removal(card,event.removeFromInventory());
                removed.putIfAbsent(card.getId(),original!=null?original:Map.of("cardId",card.getId(),"ownerSeat",owner,
                    "printingKey",ForgeCatalog.key(paper),"fromSideboard",card.isInZone(ZoneType.Sideboard),
                    "fromSection",BridgeSeriesOwnership.section(card.getZone().getZoneType()),"fromInventory",event.removeFromInventory()));
            }
            long id = ForgeBridge.noticeSequence.incrementAndGet();
            int actor = ForgeBridge.game.getRegisteredPlayers().indexOf(event.sourceAbility().getActivatingPlayer()) + 1;
            for (var gui : ForgeBridge.guis) {
                Map<String,Object> notice = new LinkedHashMap<>();
                notice.put("id",id);notice.put("kind","match-removal");notice.put("actorSeat",actor);
                notice.put("publicNotice",true);notice.put("message",event.message());notice.put("value",event.message());
                notice.put("sourceCard",ForgeBridge.card(source));notice.put("removedCards",affected);
                gui.notices.addLast(notice);while(gui.notices.size()>16)gui.notices.removeFirst();
            }
        }
    }

    List<Object> snapshot() { return new ArrayList<>(removed.values()); }
}
