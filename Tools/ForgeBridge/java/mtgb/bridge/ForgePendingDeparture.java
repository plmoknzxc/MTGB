package mtgb.bridge;

import forge.game.card.Card;
import forge.game.trigger.Trigger;
import forge.game.zone.ZoneType;
import java.util.*;

/** Read-only presentation links. These are delayed instructions, not card abilities. */
final class ForgePendingDeparture {
    static Map<String,Object> instruction(Trigger trigger) {
        var ability=trigger.getOverridingAbility();
        boolean remembered=ability!=null && List.of("DelayTriggerRememberedLKI","DelayTriggerRemembered").contains(ability.getParamOrDefault("Defined",""));
        boolean self=ability!=null && ("Self".equals(ability.getParam("Defined")) || "Self".equals(ability.getParam("SacValid")));
        if(trigger.isSuppressed() || ability==null || !"Phase".equals(trigger.getMode().name()) || !remembered&&!self)return null;
        String action=switch(ability.getApi()) {
            case Sacrifice, SacrificeAll -> "sacrifice";
            case Destroy -> "destroy";
            case ChangeZone -> "Exile".equals(ability.getParam("Destination")) && "Battlefield".equals(ability.getParam("Origin")) ? "exile" : "";
            default -> "";
        };
        if(action.isEmpty())return null;
        String condition=ability.getParamOrDefault("ConditionCheckSVar","");
        if(trigger.getMapParams().keySet().stream().anyMatch(k->k.startsWith("Check") || k.startsWith("Condition"))
            || trigger.hasParam("ValidPlayer") && !"You".equals(trigger.getParam("ValidPlayer")))condition="native-condition";
        Map<String,Object> result=new LinkedHashMap<>();
        result.put("triggerId",trigger.getId());result.put("action",action);
        result.put("phase",trigger.getParamOrDefault("Phase","").trim());
        result.put("yourTurn","You".equals(trigger.getParam("ValidPlayer")));
        result.put("condition",condition);result.put("description",ForgeBridge.publicMarkerSource(trigger.getHostCard())?trigger.toString():"");
        return result;
    }
    static List<Object> forCard(Card card,List<Trigger> triggers) {
        List<Object> result=new ArrayList<>();
        if(card.isFaceDown() || !card.isInZone(ZoneType.Battlefield))return result;
        for(Trigger trigger:triggers) {
            var instruction=instruction(trigger);if(instruction==null)continue;
            // Match the remembered incarnation, never just its name or reusable object ID.
            if(trigger.getTriggerRemembered().stream().anyMatch(o->o instanceof Card remembered
                && remembered.getId()==card.getId() && remembered.getGameTimestamp()==card.getGameTimestamp()))result.add(instruction);
        }
        // Heat Shimmer gains a self-trigger; Reflection uses an external one-shot
        // trigger. Self-triggers remain on the card if that occurrence is countered.
        if(card.isToken() || card.getCopiedPermanent()!=null)for(Trigger trigger:card.getTriggers()) {
            var ability=trigger.getOverridingAbility();
            if(ability==null || !("Self".equals(ability.getParam("Defined")) || "Self".equals(ability.getParam("SacValid"))))continue;
            var instruction=instruction(trigger);if(instruction!=null)result.add(instruction);
        }
        return result;
    }
    static List<Trigger> withStack(List<Trigger> delayed) {
        Map<Integer,Trigger> result=new LinkedHashMap<>();for(Trigger t:delayed)result.put(t.getId(),t);
        for(var entry:ForgeBridge.game.getStack())addAbility(entry.getSpellAbility(),result);
        // Ordering removes triggers from delayed storage before they reach the
        // visible stack. Read the pinned queues to preserve the copy's border.
        try {
            for(String name:List.of("activePlayerSAs","simultaneousStackEntryList","frozenStack")) {
                var field=ForgeBridge.game.getStack().getClass().getDeclaredField(name);field.setAccessible(true);
                for(Object item:(Iterable<?>)field.get(ForgeBridge.game.getStack())) {
                    if(item instanceof forge.game.spellability.SpellAbility sa)addAbility(sa,result);
                    else if(item instanceof forge.game.spellability.SpellAbilityStackInstance entry)addAbility(entry.getSpellAbility(),result);
                }
            }
        } catch(ReflectiveOperationException ex){throw new IllegalStateException("Pending departure ordering snapshot unavailable",ex);}
        return new ArrayList<>(result.values());
    }
    private static void addAbility(forge.game.spellability.SpellAbility ability,Map<Integer,Trigger> result) {
        if(ability.getTrigger()!=null)result.put(ability.getTrigger().getId(),ability.getTrigger());
    }
    static String groupingKey(Trigger trigger) {
        if(instruction(trigger)==null)return "";
        var spawning=trigger.getSpawningAbility();
        // AtEOT descriptions contain affected card IDs. Compare the actual source recipe
        // and scheduling/conditions instead; retain every original trigger separately.
        var params=new TreeMap<>(trigger.getMapParams());params.remove("TriggerDescription");
        return params+"|"+new TreeMap<>(trigger.getOverridingAbility().getMapParams())
            +"|"+(spawning==null?trigger.toString():new TreeMap<>(spawning.getMapParams()).toString());
    }
}
