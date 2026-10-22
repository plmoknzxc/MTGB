// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import forge.game.mana.ManaCostBeingPaid;
import forge.game.player.Player;
import forge.game.spellability.SpellAbility;
import forge.gamemodes.match.input.InputPayMana;
import java.lang.reflect.Field;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.LinkedHashMap;
import java.util.IdentityHashMap;

/** Select native mana by color and snow provenance; Forge decides payability. */
final class ForgePoolMana {
    private static final IdentityHashMap<forge.game.spellability.AbilityManaPart,Integer> RESTRICTION_IDS=new IdentityHashMap<>();
    private static final Field PLAYER=field("player"), COST=field("manaCost"), ABILITY=field("saPaidFor");
    private static Field field(String name) {
        try { Field f=InputPayMana.class.getDeclaredField(name); f.setAccessible(true); return f; }
        catch(ReflectiveOperationException e) { throw new IllegalStateException("Pinned native mana input changed",e); }
    }
    static Player payer(InputPayMana input) {
        try { return (Player)PLAYER.get(input); }
        catch(IllegalAccessException e) { throw new IllegalStateException(e); }
    }
    static int count(Player player,byte color,boolean snow) {
        return count(player.getManaPool().snapshot(),color,snow);
    }
    static int count(List<forge.game.mana.Mana> snapshot,byte color,boolean snow) {
        int count=0;
        for(var mana:snapshot) if(mana.getColor()==color&&mana.isSnow()==snow) count++;
        return count;
    }
    private static String symbol(byte color) {
        byte[] colors=forge.card.mana.ManaAtom.MANATYPES;
        String[] symbols={"W","U","B","R","G","C"};
        for(int i=0;i<colors.length;i++)if(colors[i]==color)return symbols[i];
        throw new IllegalArgumentException("Unknown native mana color "+color);
    }
    private static String key(forge.game.mana.Mana mana) {
        String base="mana:"+(mana.isSnow()?"S:":"")+symbol(mana.getColor());
        if(!mana.isRestricted())return base;
        var part=mana.getManaAbility();
        // Source identity also preserves chosen types and source-relative checks.
        // Separate abilities on one source cannot silently exchange restrictions.
        // An opaque ID cannot reveal a hidden source's rule text. Identity also
        // keeps different native abilities/spending effects in separate buckets.
        int tag;
        synchronized(RESTRICTION_IDS){tag=RESTRICTION_IDS.computeIfAbsent(part,p->RESTRICTION_IDS.size()+1);}
        return base+":restricted:"+mana.getSourceCard().getId()+":"+tag;
    }
    static List<Map<String,Object>> entries(Player player,Player viewer) {
        return entries(player.getManaPool().snapshot(),viewer);
    }
    static List<Map<String,Object>> entries(List<forge.game.mana.Mana> snapshot,Player viewer) {
        Map<String,Map<String,Object>> buckets=new LinkedHashMap<>();
        for(var mana:snapshot) {
            String id=key(mana);
            var bucket=buckets.get(id);
            if(bucket==null) {
                bucket=new LinkedHashMap<>();bucket.put("id",id);bucket.put("symbol",symbol(mana.getColor()));
                bucket.put("count",0);bucket.put("snow",mana.isSnow());bucket.put("restricted",mana.isRestricted());
                var source=mana.getSourceCard();
                bucket.put("sourceName",mana.isRestricted()&&viewer!=null&&source.getView().canBeShownTo(viewer.getView())?source.getName():"");
                buckets.put(id,bucket);
            }
            bucket.put("count",(int)bucket.get("count")+1);
        }
        var result=new ArrayList<>(buckets.values());
        result.sort(java.util.Comparator.comparing((Map<String,Object> e)->(boolean)e.get("restricted"))
            .thenComparing(e->(boolean)e.get("snow")).thenComparing(e->List.of("W","U","B","R","G","C").indexOf(e.get("symbol")))
            .thenComparing(e->(String)e.get("id")));
        return result;
    }
    static boolean pay(InputPayMana input,byte color,boolean snow) {
        return pay(input,"mana:"+(snow?"S:":"")+symbol(color));
    }
    static boolean pay(InputPayMana input,String bucketId) {
        try {
            Player player=payer(input);
            var pool=player.getManaPool();
            var cost=(ManaCostBeingPaid)COST.get(input);
            var ability=(SpellAbility)ABILITY.get(input);
            // Copy before native removal changes the iterable. Preserve the actual
            // Mana instance, source, restrictions, refund and spending effects.
            var candidates=new ArrayList<forge.game.mana.Mana>();
            for(var mana:pool.snapshot()) if(key(mana).equals(bucketId)) candidates.add(mana);
            for(var mana:candidates) {
                if(!mana.meetsManaRestrictions(ability)||!ability.allowsPayingWithShard(mana.getSourceCard(),mana.getColor())) continue;
                if(pool.tryPayCostWithMana(ability,cost,mana,false)) {
                    ability.getPayingMana().add(mana);
                    input.showMessage();
                    return true;
                }
            }
            return false;
        } catch(IllegalAccessException e) { throw new IllegalStateException("Pinned native mana fields changed",e); }
    }
}
