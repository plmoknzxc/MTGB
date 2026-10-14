package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.spellability.SpellAbility;
import forge.game.mana.ManaCostBeingPaid;
import forge.game.zone.ZoneType;
import forge.gamemodes.match.input.InputPayMana;
import java.nio.file.*;
import java.util.*;

/** Initial card positions only. This observer never changes native rule objects. */
public final class RestrictedManaTailFixture {
    static final List<SpellAbility> observedPayments=new ArrayList<>();
    static Object field(Object input,String name)throws Exception {
        var f=InputPayMana.class.getDeclaredField(name);f.setAccessible(true);return f.get(input);
    }
    static Map<String,Object> ability(SpellAbility a) {
        Map<String,Object> out=new LinkedHashMap<>();out.put("identity",System.identityHashCode(a));out.put("id",a.getId());
        out.put("source",a.getHostCard().getId());out.put("name",a.getHostCard().getName());out.put("class",a.getClass().getSimpleName());out.put("rootClass",a.getRootAbility().getClass().getSimpleName());
        out.put("rootStatic",a.getRootAbility() instanceof forge.game.spellability.AbilityStatic);out.put("classFullName",a.getClass().getName());out.put("spell",a.isSpell());out.put("activated",a.isActivatedAbility());out.put("api",a.getApi()==null?"":a.getApi().name());out.put("params",a.getMapParams());
        out.put("foretelling",a.isForetelling());out.put("unlock",a.isUnlock());out.put("morphUp",a.isMorphUp());out.put("manifestUp",a.isManifestUp());out.put("turnFaceUp",a.isTurnFaceUp());out.put("castFaceDown",a.isCastFaceDown());
        out.put("payingMana",a.getPayingMana().stream().map(DrainManaDeltaFixture::mana).toList());out.put("cost",a.getPayCosts()==null?"":a.getPayCosts().toString());return out;
    }
    static Map<String,Object> card(Card c) {
        Map<String,Object> out=DrainManaDeltaFixture.card(c);out.put("printedName",c.getPaperCard()==null?c.getName():c.getPaperCard().getName());out.put("faceDown",c.isFaceDown());out.put("manifested",c.isManifested());out.put("foretold",c.isForetold());
        out.put("unlockedDoors",c.getUnlockedRoomNames());out.put("chosenColors",c.getChosenColors());out.put("abilities",c.isInZone(ZoneType.Library)?List.of():c.getSpellAbilities().stream().map(RestrictedManaTailFixture::ability).toList());return out;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()-> {
            for(int seat=1;seat<=2;seat++)RoyalStateFixture.setup(ForgeBridge.game.getPlayers().get(seat-1),ForgeBridge.config.getAsJsonArray("positions").get(seat-1).getAsJsonObject());
            ForgeBridge.game.getAction().checkStaticAbilities();var events=new DrainManaDeltaFixture.Events();ForgeBridge.game.subscribeToEvents(events);
            Thread observer=new Thread(()-> {
                Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&!seen.contains(r.requestId)) {
                        Map<String,Object> current=new LinkedHashMap<>();var input=r.gui.controller.getInputQueue().getInput();
                        if(input instanceof InputPayMana) {
                            SpellAbility a=(SpellAbility)field(input,"saPaidFor");if(observedPayments.stream().noneMatch(x->x==a))observedPayments.add(a);
                            current.put("ability",ability(a));current.put("remaining",field(input,"manaCost").toString());current.put("payer",((Player)field(input,"player")).getId()+1);
                            var cost=(ManaCostBeingPaid)field(input,"manaCost");List<Object> eligible=new ArrayList<>();
                            for(var m:((Player)field(input,"player")).getManaPool()) {
                                Map<String,Object> unit=new LinkedHashMap<>(DrainManaDeltaFixture.mana(m));unit.put("meetsRestriction",m.meetsManaRestrictions(a));unit.put("allowsShard",a.allowsPayingWithShard(m.getSourceCard(),m.getColor()));unit.put("addsNoCounter",m.addsNoCounterMagic(a));
                                unit.put("shards",cost.getUnpaidShards().stream().map(s->Map.of("shard",s.toString(),"generic",s.isGeneric(),"or2",s.isOr2Generic(),"correctColor",s.isColor(m.getColor()),"allowed",m.getManaAbility()==null||m.getManaAbility().meetsManaShardRestrictions(s,m.getColor()))).toList());eligible.add(unit);
                            }current.put("units",eligible);
                        }
                        List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers()) {
                            Map<String,Object> zones=new LinkedHashMap<>();for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack,ZoneType.Command})zones.put(z.name(),p.getCardsIn(z).stream().map(RestrictedManaTailFixture::card).toList());
                            players.add(Map.of("seat",p.getId()+1,"life",p.getLife(),"zones",zones,"mana",DrainManaDeltaFixture.pool(p),"piracy",p.hasKeyword("Piracy")));
                        }
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                        Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players,"stack",DrainManaDeltaFixture.stack(),"payment",current,"paymentHistory",observedPayments.stream().map(RestrictedManaTailFixture::ability).toList(),"events",events.snapshot(),"turn",ForgeBridge.game.getPhaseHandler().getTurn(),"phase",ForgeBridge.game.getPhaseHandler().getPhase().name())));
                        Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);seen.add(r.requestId);
                    }}Thread.sleep(5);
                }catch(Exception e){e.printStackTrace();throw new RuntimeException(e);}
            },"restriction-tail-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
