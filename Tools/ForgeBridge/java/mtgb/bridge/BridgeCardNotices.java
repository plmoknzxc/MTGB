// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import com.google.common.eventbus.Subscribe;
import forge.game.card.Card;
import forge.game.card.CardView;
import forge.game.event.GameEventCardChangeZone;
import forge.game.event.GameEventShuffle;
import forge.game.spellability.SpellAbility;
import forge.game.zone.ZoneType;
import java.util.*;

/** Informational card faces and completed moves never occupy the input queue. */
public final class BridgeCardNotices {
    private record Group(long revision,int source,String reason,ZoneType from,ZoneType to,int owner) {}
    private final Map<ForgeBridge.Gui,Map<Group,Map<String,Object>>> groups=new IdentityHashMap<>();
    private long epoch=Long.MIN_VALUE;
    private record Reveal(long revision,Map<String,Object> note,Set<Object> cards) {}
    private static final Map<ForgeBridge.Gui,Reveal> recentReveals=new IdentityHashMap<>();
    private static final Set<Integer> publiclyRevealed=new HashSet<>();
    private record Arrival(long revision,ZoneType from,SpellAbility ability) {}
    private static final Map<Integer,Arrival> handArrivals=new HashMap<>();
    static void restarted(){recentReveals.clear();publiclyRevealed.clear();handArrivals.clear();}
    @Subscribe public void shuffled(GameEventShuffle event){synchronized(ForgeBridge.class){for(var player:ForgeBridge.game.getPlayers())if(player.getId()==event.player().getId())for(Card card:player.getCardsIn(ZoneType.Library))publiclyRevealed.remove(card.getId());}}
    static SpellAbility cause() {
        var cost=ForgeBridge.game.costPaymentStack.peek();
        if(cost!=null)return cost.payment().getAbility();
        return ForgeBridge.game.getStack().isResolving()?ForgeBridge.game.getStack().peekAbility():null;
    }
    static Map<String,Object> notice(ForgeBridge.Gui gui,String kind,String message,List<Object> cards,SpellAbility ability) {
        Map<String,Object> result=new LinkedHashMap<>();
        result.put("id",ForgeBridge.noticeSequence.incrementAndGet());result.put("kind",kind);
        result.put("message",message==null?"":message);result.put("value","");result.put("cards",cards);
        result.put("reason",ForgeBridge.game.costPaymentStack.peek()!=null?"cost":ability!=null?"effect":"rules");
        result.put("actorSeat",ability==null?0:ForgeBridge.game.getRegisteredPlayers().indexOf(ability.getActivatingPlayer())+1);
        Card source=ability==null?null:ability.getHostCard();
        if(source!=null&&!source.isFaceDown()&&source.getZone()!=null&&!source.getZone().getZoneType().isHidden()
                &&ForgeBridge.guis.stream().allMatch(g->source.getView().canBeShownTo(g.controller.getPlayer().getView())))
            result.put("sourceCard",ForgeBridge.card(source));
        result.put("publicNotice",false);result.put("title","");
        return result;
    }
    static void append(ForgeBridge.Gui gui,Map<String,Object> notice) {
        gui.notices.addLast(notice);
        while(gui.notices.size()>128)gui.notices.removeFirst();
    }
    static void revealed(ForgeBridge.Gui gui,String title,List<Object> cards) {
        // Temporary controller GUIs publish to their receiving client seat.
        // Their private card list was authorized by the native callback itself.
        if(gui.seat>0&&gui.seat<=ForgeBridge.guis.size())gui=ForgeBridge.guis.get(gui.seat-1);
        var cost=ForgeBridge.game.costPaymentStack.peek();
        cards.removeIf(v->v instanceof Map<?,?> c&&("Stack".equals(c.get("zone"))
            ||cost!=null&&!cost.payment().getAbility().isManaAbility()&&Objects.equals(c.get("id"),cost.payment().getAbility().getHostCard().getId())));
        if(cards.isEmpty())return;
        synchronized(ForgeBridge.class) {
            var note=notice(gui,"card-reveal",title,new ArrayList<>(cards),cause());
            // ChangeZone reveals a fetched card after putting it in the hand.
            // Retain the real pre-reveal movement, not its now-current zone.
            var arrivals=ids(cards).stream().map(handArrivals::get).toList();
            if(!arrivals.isEmpty()&&arrivals.stream().allMatch(a->a!=null&&a.revision()==ForgeBridge.revision
                    &&a.from()==arrivals.get(0).from()&&a.ability()==cause())) {
                note.put("kind","card-move");note.put("fromZone",arrivals.get(0).from().name());note.put("toZone","Hand");
            }
            append(gui,note);
            // Only explicit delivery to every primary seat establishes public
            // revelation. A private look does not become a spectator notice.
            var matching=new ArrayList<Map<String,Object>>();
            var identities=ids(cards);
            recentReveals.put(gui,new Reveal(ForgeBridge.revision,note,identities));
            for(var recipient:ForgeBridge.guis) {
                Reveal previous=recentReveals.get(recipient);
                if(previous!=null&&previous.revision()==ForgeBridge.revision&&previous.cards().equals(identities)
                    &&Objects.equals(previous.note().get("message"),note.get("message")))matching.add(previous.note());
            }
            if(matching.size()==ForgeBridge.guis.size()) {
                for(var item:matching)item.put("publicNotice",true);
                for(Object id:identities)if(id instanceof Integer value)publiclyRevealed.add(value);
            }
        }
    }
    private static Set<Object> ids(List<Object> cards) {
        Set<Object> result=new LinkedHashSet<>();
        for(Object item:cards)if(item instanceof Map<?,?> card)result.add(card.get("id"));
        return result;
    }
    @Subscribe public void moved(GameEventCardChangeZone event) {
        if(event.card()==null||event.from()==null||event.to()==null)return;
        ZoneType from=event.from().zoneType(),to=event.to().zoneType();
        if(from==to||from==ZoneType.None||!(to==ZoneType.Graveyard||to==ZoneType.Exile||to==ZoneType.Hand||to==ZoneType.Library&&!from.isHidden()))return;
        Card card=ForgeBridge.game.getCardsInGame().stream().filter(c->c.getId()==event.card().getId()).findFirst().orElse(null);
        if(card==null)return;
        synchronized(ForgeBridge.class) {
            SpellAbility ability=cause();
            // The stack already shows the cast spell / cycling source. Notify
            // the fetched, milled, exiled or other separately moved objects.
            if(from==ZoneType.Stack&&(ability==null||card.getId()==ability.getHostCard().getId())
                ||ForgeBridge.game.costPaymentStack.peek()!=null&&ability!=null&&!ability.isManaAbility()&&card.getId()==ability.getHostCard().getId())return;
            // A hidden library draw is not a revealed move into the hand.
            boolean publicOrigin=!from.isHidden()&&!event.card().isFaceDown();
            // Entering a hidden library must not reveal a face-down object.
            if(to==ZoneType.Library&&!publicOrigin)return;
            boolean seenByOpponent=ForgeBridge.guis.stream().anyMatch(g->g.controller.getPlayer()!=card.getOwner()
                &&ForgeBridge.libraryKnowledge.hand(card.getOwner(),g.seat,g.controller.getPlayer()).stream()
                    .anyMatch(v->v instanceof Map<?,?> m&&Objects.equals(m.get("id"),card.getId())));
            if(to==ZoneType.Hand) {
                handArrivals.entrySet().removeIf(e->e.getValue().revision()!=ForgeBridge.revision);
                handArrivals.put(card.getId(),new Arrival(ForgeBridge.revision,from,cause()));
            }
            if(to==ZoneType.Hand&&!publicOrigin&&!seenByOpponent)return;
            String reason=ForgeBridge.game.costPaymentStack.peek()!=null?"cost":ability!=null?"effect":"rules";
            if(epoch!=ForgeBridge.revision){groups.clear();epoch=ForgeBridge.revision;}
            Group key=new Group(epoch,ability==null?0:ability.getHostCard().getId(),reason,from,to,card.getOwner().getId());
            boolean publicReveal=publiclyRevealed.remove(card.getId());
            boolean publicFace=!card.isFaceDown()&&(to!=ZoneType.Hand||publicOrigin||publicReveal);
            for(var gui:ForgeBridge.guis) {
                // Remove the duplicate pre-move reveal from this unsent batch;
                // the destination notice retains the actual shown identity.
                for(var it=gui.notices.iterator();it.hasNext();) {
                    Object item=it.next();
                    if(item instanceof Map<?,?> reveal&&"card-reveal".equals(reveal.get("kind"))) {
                        var shown=(List<Object>)reveal.get("cards");
                        shown.removeIf(v->v instanceof Map<?,?> m&&Objects.equals(m.get("id"),card.getId()));
                        if(shown.isEmpty())it.remove();
                    }
                }
                boolean knownHand=to==ZoneType.Hand&&ForgeBridge.libraryKnowledge.hand(card.getOwner(),gui.seat,gui.controller.getPlayer()).stream()
                    .anyMatch(v->v instanceof Map<?,?> m&&Objects.equals(m.get("id"),card.getId()));
                Map<String,Object> face=publicFace||publicOrigin||knownHand||card.getView().canBeShownTo(gui.controller.getPlayer().getView())
                    ?new LinkedHashMap<>((Map<String,Object>)ForgeBridge.card(card))
                    :new LinkedHashMap<>(Map.of("id",card.getId(),"name","Face-down card","faceDown",true));
                face.put("zone",to.name());
                var byGroup=groups.computeIfAbsent(gui,g->new HashMap<>());
                var note=byGroup.get(key);
                if(note==null||!gui.notices.contains(note)) {
                    note=notice(gui,"card-move","",new ArrayList<>(),ability);
                    note.put("fromZone",from.name());note.put("toZone",to.name());note.put("publicNotice",publicFace);
                    byGroup.put(key,note);append(gui,note);
                }
                ((List<Object>)note.get("cards")).add(face);
            }
        }
    }
}
