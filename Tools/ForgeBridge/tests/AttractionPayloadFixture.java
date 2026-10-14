package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Initial auxiliary order only. All opening, rolls, visits and prizes are native operations. */
public final class AttractionPayloadFixture {
    static Map<String,Object> card(Card c) {
        var out=NamePredicateFixture.card(c);
        out.put("attraction",c.isAttraction()); out.put("lights",c.getAttractionLights());
        out.put("visited",c.wasVisitedThisTurn()); out.put("phasedOut",c.isPhasedOut());
        out.put("remembered",com.google.common.collect.Lists.newArrayList(c.getRemembered()).stream().map(NamePredicateFixture::remembered).toList());
        out.put("counters",c.getCounters().entrySet().stream().map(e->Map.of("type",e.getElement().toString(),"amount",e.getCount())).toList());
        out.put("variant",c.getPaperCard()==null?"":c.getPaperCard().getFunctionalVariant());
        return out;
    }
    static List<Object> stack() {
        List<Object> out=new ArrayList<>();
        for(var si:ForgeBridge.game.getStack()) {
            var a=si.getSpellAbility(); var host=a.getHostCard();
            Map<String,Object> item=new LinkedHashMap<>();
            item.put("id",si.getId()); item.put("source",host.getId()); item.put("name",host.getName());
            item.put("api",a.getApi()==null?"":a.getApi().name()); item.put("actor",a.getActivatingPlayer().getId()+1);
            item.put("trigger",a.isTrigger()); item.put("spell",a.isSpell());
            item.put("payingMana",a.getPayingMana().stream().map(NamePredicateFixture::mana).toList());
            item.put("description",a.getDescription()); item.put("targetCards",a.getTargets().getTargetCards().stream().map(Card::getId).toList());
            Map<String,Object> objects=new LinkedHashMap<>();
            for(var key:List.of(forge.game.ability.AbilityKey.Card,forge.game.ability.AbilityKey.Player,forge.game.ability.AbilityKey.Result)) {
                Object value=a.getTriggeringObject(key);
                if(value!=null)objects.put(key.name(),NamePredicateFixture.remembered(value));
            }
            item.put("triggering",objects);out.add(item);
        }
        return out;
    }
    static Map<String,Object> pendingAbility() throws Exception {
        Map<String,Object> out=new LinkedHashMap<>();
        for(var gui:ForgeBridge.guis) {
            var input=gui.controller.getInputQueue().getInput();
            if(input instanceof forge.gamemodes.match.input.InputSelectTargets) {
                var field=forge.gamemodes.match.input.InputSelectTargets.class.getDeclaredField("sa");field.setAccessible(true);
                var a=(forge.game.spellability.SpellAbility)field.get(input);
                out.put("source",a.getHostCard().getId());out.put("sourceName",a.getHostCard().getName());out.put("actor",a.getActivatingPlayer().getId()+1);
                for(var key:List.of(forge.game.ability.AbilityKey.Card,forge.game.ability.AbilityKey.Player)) {
                    var value=a.getTriggeringObject(key);if(value!=null)out.put(key.name(),NamePredicateFixture.remembered(value));
                }
            }
        }
        return out;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()-> {
            for(int seat=1;seat<=2;seat++)RoyalStateFixture.setup(ForgeBridge.game.getPlayers().get(seat-1),ForgeBridge.config.getAsJsonArray("positions").get(seat-1).getAsJsonObject());
            // A frozen starting order, not a rule result or later mutation.
            var owner=ForgeBridge.game.getPlayers().get(0);var deck=owner.getZone(ZoneType.AttractionDeck);
            List<Card> ordered=new ArrayList<>();
            for(var x:ForgeBridge.config.getAsJsonArray("Attractions")) {
                var spec=x.getAsJsonObject();String name=spec.get("name").getAsString(),variant=spec.get("variant").getAsString();
                ordered.add(owner.getCardsIn(ZoneType.AttractionDeck).stream().filter(c->c.getName().equals(name)&&c.getPaperCard().getFunctionalVariant().equals(variant)).findFirst().orElseThrow());
            }
            for(Card c:ordered)deck.remove(c); for(Card c:ordered)deck.add(c);
            if(ForgeBridge.config.has("initialAttractionLuck")) {
                var c=ordered.get(0);deck.remove(c);owner.getZone(ZoneType.Battlefield).add(c);
                c.setCounters(forge.game.card.CounterType.getType("LUCK"),ForgeBridge.config.get("initialAttractionLuck").getAsInt());
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
            var events=new NamePredicateFixture.Events();ForgeBridge.game.subscribeToEvents(events);
            Thread observer=new Thread(()-> {
                Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&!seen.contains(r.requestId)) {
                            List<Object> players=new ArrayList<>();
                            for(Player p:ForgeBridge.game.getPlayers()) {
                                Map<String,Object> zones=new LinkedHashMap<>();
                                for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack,ZoneType.Command,ZoneType.AttractionDeck,ZoneType.Junkyard})
                                    zones.put(z.name(),p.getCardsIn(z).stream().map(AttractionPayloadFixture::card).toList());
                                players.add(Map.of("seat",p.getId()+1,"life",p.getLife(),"zones",zones,"mana",NamePredicateFixture.pool(p),"visitedCount",p.getAttractionsVisitedThisTurn()));
                            }
                            var path=Path.of(ForgeBridge.config.get("auditDir").getAsString());
                            var tmp=path.resolve(r.requestId+".tmp");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players,"stack",stack(),"pendingAbility",pendingAbility(),"events",events.snapshot(),"phase",ForgeBridge.game.getPhaseHandler().getPhase().name(),"turn",ForgeBridge.game.getPhaseHandler().getTurn())));
                            Files.move(tmp,path.resolve(r.requestId+".json"),StandardCopyOption.REPLACE_EXISTING);seen.add(r.requestId);
                        }
                    }Thread.sleep(5);
                }catch(Exception e){throw new IllegalStateException(e);}
            },"attraction-payload-read-only");observer.setDaemon(true);observer.start();
        },registered->{
            var original=registered.get(0);var deck=(forge.deck.Deck)original.getDeck().copyTo("Native attraction payload initial deck");
            for(var x:ForgeBridge.config.getAsJsonArray("Attractions")) {
                var spec=x.getAsJsonObject();String name=spec.get("name").getAsString(),variant=spec.get("variant").getAsString();
                var available=new ArrayList<>(forge.model.FModel.getMagicDb().getCommonCards().getAllCards(name));
                available.addAll(forge.model.FModel.getMagicDb().getVariantCards().getAllCards(name));
                var paper=available.stream().filter(p->p.getFunctionalVariant().equals(variant)).findFirst().orElseThrow(()->new IllegalArgumentException(name+" variant "+variant+" available="+available.stream().map(p->p.getName()+":"+p.getFunctionalVariant()).distinct().toList()));
                deck.getOrCreate(forge.deck.DeckSection.Attractions).add(paper,1);
            }
            var replacement=new forge.game.player.RegisteredPlayer(deck).setPlayer(original.getPlayer());
            replacement.setStartingLife(original.getStartingLife());registered.set(0,replacement);
        });
    }
}
