package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Initial positions only; physical links and zones are read from real native actions. */
public final class MeldPayloadFixture {
    static Map<String,Object> card(Card c) {
        var out=NamePredicateFixture.card(c);
        out.put("state",c.getCurrentStateName().name());
        out.put("meldedWith",c.getMeldedWith()==null?0:c.getMeldedWith().getId());
        out.put("cloneOrigin",c.getCloneOrigin()==null?0:c.getCloneOrigin().getId());
        var combat=ForgeBridge.game.getCombat();
        out.put("attacking",combat!=null&&combat.isAttacking(c));
        var defender=combat==null?null:combat.getDefenderByAttacker(c);
        out.put("defenderId",defender==null?0:defender.getId());
        out.put("defenderKind",defender instanceof Player?"player":defender instanceof Card?"card":"");
        if(c.getMeldedWith()!=null)out.put("component",NamePredicateFixture.card(c.getMeldedWith()));
        return out;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int seat=1;seat<=2;seat++)RoyalStateFixture.setup(ForgeBridge.game.getPlayers().get(seat-1),ForgeBridge.config.getAsJsonArray("positions").get(seat-1).getAsJsonObject());
            if(ForgeBridge.config.has("orderedGraveyard"))forge.model.FModel.getPreferences().setPref(forge.localinstance.properties.ForgePreferences.FPref.UI_ALLOW_ORDER_GRAVEYARD_WHEN_NEEDED,forge.localinstance.properties.ForgeConstants.GRAVEYARD_ORDERING_OWN_CARDS);
            if(ForgeBridge.config.has("initialPartner")) {
                String mode=ForgeBridge.config.get("initialPartner").getAsString();
                var own=ForgeBridge.game.getPlayers().get(0);
                String partnerName=ForgeBridge.config.has("initialPartnerName")?ForgeBridge.config.get("initialPartnerName").getAsString():"Hanweir Garrison";
                Card partner=own.getCardsIn(ZoneType.Battlefield).stream().filter(c->c.getName().equals(partnerName)).findFirst().orElse(null);
                if(mode.equals("token"))partner.setGamePieceType(forge.card.GamePieceType.TOKEN);
                else if(mode.equals("copy"))throw new IllegalArgumentException("Artificial clone-origin is not a valid copied permanent; use actual Clone flow");
                else if(mode.equals("wrong-owner"))partner.setOwner(ForgeBridge.game.getPlayers().get(1));
                else if(!mode.equals("missing"))throw new IllegalArgumentException(mode);
            }
            // A new suffix history, with an initial position matching the frozen
            // real Meld endpoint. This is never counted as another paid Meld.
            if(ForgeBridge.config.has("initialMeld")) {
                var owner=ForgeBridge.game.getPlayers().get(0);
                String primName=ForgeBridge.config.has("initialMeldPrimary")?ForgeBridge.config.get("initialMeldPrimary").getAsString():"Hanweir Battlements";
                String secName=ForgeBridge.config.has("initialMeldSecondary")?ForgeBridge.config.get("initialMeldSecondary").getAsString():"Hanweir Garrison";
                Card primary=owner.getCardsIn(ZoneType.Battlefield).stream().filter(c->c.getName().equals(primName)).findFirst().orElseThrow();
                Card secondary=owner.getCardsIn(ZoneType.Battlefield).stream().filter(c->c.getName().equals(secName)).findFirst().orElseThrow();
                owner.getZone(ZoneType.Battlefield).remove(secondary);
                ((forge.game.zone.PlayerZoneBattlefield)owner.getZone(ZoneType.Battlefield)).addToMelded(secondary);
                primary.changeToState(forge.card.CardStateName.Meld);primary.setBackSide(true);primary.setMeldedWith(secondary);
                if(ForgeBridge.config.has("initialMeldLoyalty"))primary.setCounters(forge.game.card.CounterType.getType("LOYALTY"),ForgeBridge.config.get("initialMeldLoyalty").getAsInt());
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
            var events=new NamePredicateFixture.Events();ForgeBridge.game.subscribeToEvents(events);
            Thread observer=new Thread(()->{
                Set<Long> seen=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class){var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&!seen.contains(r.requestId)){
                            List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers()){
                                Map<String,Object> zones=new LinkedHashMap<>();for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack,ZoneType.Command})zones.put(z.name(),p.getCardsIn(z).stream().map(MeldPayloadFixture::card).toList());
                                players.add(Map.of("seat",p.getId()+1,"life",p.getLife(),"zones",zones,"mana",NamePredicateFixture.pool(p)));
                            }
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players,"stack",NamePredicateFixture.stack(),"events",events.snapshot(),"turn",ForgeBridge.game.getPhaseHandler().getTurn(),"phase",ForgeBridge.game.getPhaseHandler().getPhase().name())));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);seen.add(r.requestId);
                        }
                    }Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"meld-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
