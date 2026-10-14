package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Initial positions only; observer never changes results or replacement abilities. */
public final class CombatRelationFixture {
    static Map<String,Object> card(Card c) {
        Map<String,Object> m=new LinkedHashMap<>();
        m.put("id",c.getId());m.put("name",c.getName());m.put("zone",c.getZone().getZoneType().name());
        m.put("timestamp",c.getGameTimestamp());m.put("owner",c.getOwner().getId()+1);m.put("controller",c.getController().getId()+1);
        m.put("tapped",c.isTapped());m.put("damage",c.getDamage());m.put("shields",c.getShieldCount());
        m.put("regenerated",c.getRegeneratedThisTurn());
        m.put("detained",c.isDetained());
        m.put("deathtouchDamage",c.hasBeenDealtDeathtouchDamage());
        m.put("assignedDamage",c.getAssignedDamageMap().values().stream().mapToInt(Integer::intValue).sum());
        m.put("attachedTo",c.getEntityAttachedTo()==null?0:c.getEntityAttachedTo().getId());
        m.put("canAttack",c.isCreature()&&forge.game.combat.CombatUtil.canAttack(c));
        m.put("canBlock",c.isCreature()&&forge.game.combat.CombatUtil.canBlock(c));
        m.put("activatedPlayable",c.getSpellAbilities().stream().filter(a->a.isActivatedAbility()).anyMatch(a->a.canPlay()));
        m.put("mustBlock",c.getMustBlockCards().stream().map(Card::getId).toList());
        m.put("gotBlocked",c.getDamageHistory().getCreatureGotBlockedThisCombat());
        var combat=c.getGame().getCombat();
        var defender=combat==null?null:combat.getDefenderByAttacker(c);
        m.put("defender",defender==null?0:defender.getId());
        m.put("defenderKind",defender==null?"none":defender instanceof Player?"player":"card");
        m.put("loyalty",c.getCounters(forge.game.card.CounterType.getType("LOYALTY")));
        m.put("blocking",c.getGame().getCombat()!=null&&c.getGame().getCombat().isBlocking(c));
        m.put("blocked",c.getGame().getCombat()!=null&&c.getGame().getCombat().isBlocked(c));
        m.put("blockers",c.getGame().getCombat()==null?List.of():c.getGame().getCombat().getBlockers(c).stream().map(Card::getId).toList());
        m.put("attacking",c.getGame().getCombat()!=null && c.getGame().getCombat().isAttacking(c));
        m.put("immutable",c.isImmutable());m.put("canRegenerate",c.canBeShielded());
        List<Integer> remembered=new ArrayList<>();for(Object x:c.getRemembered())if(x instanceof Card)remembered.add(((Card)x).getId());
        m.put("remembered",remembered); m.put("token",c.isToken()); m.put("power",c.getNetPower()); m.put("toughness",c.getNetToughness()); m.put("imprinted",c.getImprintedCards().stream().map(Card::getId).toList()); return m;
    }
    static List<Object> stack() {
        List<Object> result=new ArrayList<>();
        for(var si:ForgeBridge.game.getStack()) {
            Map<String,Object> m=new LinkedHashMap<>();m.put("source",si.getSourceCard().getId());m.put("trigger",si.isTrigger());
            for(var key:new forge.game.ability.AbilityKey[]{forge.game.ability.AbilityKey.Attacker,forge.game.ability.AbilityKey.Defender,forge.game.ability.AbilityKey.DefendingPlayer,forge.game.ability.AbilityKey.OriginalDefender}) {
                Object x=si.getTriggeringObject(key);m.put(key.name(),x instanceof forge.game.GameEntity?((forge.game.GameEntity)x).getId():0);
            }result.add(m);
        }return result;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int seat=1;seat<=2;seat++) {
                Player p=ForgeBridge.game.getPlayers().get(seat-1);
                for(String land:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int j=0;j<8;j++)StateBranchFixture.place(land,p,ZoneType.Battlefield);
                StateBranchFixture.setup(p,ForgeBridge.config.getAsJsonObject(seat==1?"own":"other"));
            }
            if(ForgeBridge.config.has("loyalty"))for(var entry:ForgeBridge.config.getAsJsonObject("loyalty").entrySet())
                for(Player p:ForgeBridge.game.getPlayers())for(Card c:p.getCardsIn(ZoneType.Battlefield))if(c.getName().equals(entry.getKey()))c.setCounters(forge.game.card.CounterType.getType("LOYALTY"),entry.getValue().getAsInt());
            if(ForgeBridge.config.has("attachments"))for(var item:ForgeBridge.config.getAsJsonArray("attachments")) {
                var spec=item.getAsJsonArray();Player p=ForgeBridge.game.getPlayers().get(spec.get(0).getAsInt()-1);
                Card equipment=p.getCardsIn(ZoneType.Battlefield).stream().filter(c->c.getName().equals(spec.get(1).getAsString())).findFirst().orElseThrow();
                Card recipient=p.getCardsIn(ZoneType.Battlefield).stream().filter(c->c.getName().equals(spec.get(2).getAsString())).findFirst().orElseThrow();equipment.attachToEntity(recipient,null);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
            Thread observer=new Thread(()->{
                Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var request=ForgeBridge.pending;
                        if(request!=null&&!request.consumed&&seen.add(request.requestId)) {
                            List<Object> players=new ArrayList<>();
                            for(Player p:ForgeBridge.game.getPlayers())players.add(Map.of("life",p.getLife(),"cards",p.getAllCards().stream().map(CombatRelationFixture::card).toList(),"library",p.getCardsIn(ZoneType.Library).stream().map(Card::getId).toList()));
                            Path d=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=d.resolve(request.requestId+".tmp"),dst=d.resolve(request.requestId+".json");
                            String error=!request.kind.equals("InputBlock")||ForgeBridge.game.getCombat()==null?null:forge.game.combat.CombatUtil.validateBlocks(ForgeBridge.game.getCombat(),ForgeBridge.game.getPlayers().get(request.playerSeat-1));
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("players",players,"requestId",request.requestId,"stack",stack(),"blockError",error==null?"":error)));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"combat-relations-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
