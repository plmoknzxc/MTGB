// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import com.google.gson.*;
import forge.game.card.*;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.util.*;

// Room-scoped training rule. Ordinary rooms use the installed physical shuffle.
final class BridgeLibraryOrder {
    private final boolean enabled;
    private final Map<Player,List<String>> initialOrders=new IdentityHashMap<>();
    BridgeLibraryOrder(boolean enabled){this.enabled=enabled;}
    boolean enabled(){return enabled;}

    void register(Player player,JsonObject deck){
        if(!enabled)return;
        JsonArray entries=deck.getAsJsonArray("cards");
        List<String> order=new ArrayList<>();
        for(JsonElement value:entries){
            JsonObject entry=value.getAsJsonObject();
            String key=(entry.get("setCode").getAsString()+"/"+entry.get("collectorNumber").getAsString()).toUpperCase(Locale.ROOT);
            for(int i=0;i<entry.get("count").getAsInt();i++)order.add(key);
        }
        JsonArray explicit=deck.has("mainOrder")&&!deck.get("mainOrder").isJsonNull()?deck.getAsJsonArray("mainOrder"):null;
        if(explicit!=null&&explicit.size()>0){
            if(explicit.size()!=order.size()||explicit.size()>500)throw new IllegalArgumentException("Invalid physical-copy order length");
            Map<String,Integer> counts=new TreeMap<>(String.CASE_INSENSITIVE_ORDER);
            for(String key:order)counts.merge(key,1,Integer::sum);
            List<String> requested=new ArrayList<>();
            for(JsonElement value:explicit){
                String key=value.getAsString();
                Integer count=counts.get(key);
                if(count==null||count<=0)throw new IllegalArgumentException("Physical-copy order does not match deck");
                counts.put(key,count-1);requested.add(key.toUpperCase(Locale.ROOT));
            }
            order=requested;
        }
        initialOrders.put(player,order);
    }

    CardCollectionView preserve(Player player,CardCollectionView shuffled){
        if(!enabled)return shuffled;
        CardCollection current=new CardCollection(player.getCardsIn(ZoneType.Library));
        List<String> initial=initialOrders.remove(player);
        if(initial==null)return current;
        // CardPool groups/sorts printings. Restore the submitted editor order at
        // the first shuffle, using the original Card objects and multiplicities.
        Map<String,Deque<Card>> byPrinting=new HashMap<>();
        for(Card card:current){
            var paper=card.getPaperCard();
            if(paper!=null)byPrinting.computeIfAbsent((paper.getEdition()+"/"+paper.getCollectorNumber()).toUpperCase(Locale.ROOT),k->new ArrayDeque<>()).add(card);
        }
        CardCollection ordered=new CardCollection();
        for(String key:initial){
            Deque<Card> copies=byPrinting.get(key);
            if(copies!=null&&!copies.isEmpty())ordered.add(copies.removeFirst());
        }
        // Pre-game effects may remove or add cards. Preserve all remaining
        // physical identities; never recreate or drop a card to enforce a rank.
        for(Card card:current)if(!ordered.contains(card))ordered.add(card);
        return ordered;
    }
}
