package mtgb.bridge;

import forge.game.card.Card;
import forge.game.ability.ApiType;
import forge.game.spellability.SpellAbility;
import forge.game.zone.ZoneType;
import forge.item.PaperCard;
import java.util.HashMap;
import java.util.LinkedHashSet;
import java.util.Map;
import java.util.WeakHashMap;

/** Public state only; IDs avoid recursive card graphs and preserve zone visibility. */
final class ForgePublicCardDetails {
    private record DraftPick(int seat, String name, String printing) {}
    private static final Map<SpellAbility,Map<Integer,DraftPick>> draftPicks=new WeakHashMap<>();

    static synchronized Card rememberDraft(Card chosen, SpellAbility ability, int seat) {
        if(chosen!=null && ability!=null && ability.getApi()==ApiType.Draft
                && chosen.getPaperCard() instanceof PaperCard paper) {
            draftPicks.computeIfAbsent(ability,k->new HashMap<>())
                .put(chosen.getId(),new DraftPick(seat,chosen.getName(),ForgeCatalog.key(paper)));
        }
        return chosen;
    }
    // Display the chooser's own decision, without granting permission to inspect a face-down card.
    static synchronized void addDraftMemory(Card card, int seat, Map<String,Object> out) {
        if(!card.isFaceDown() || !card.isInZone(ZoneType.Exile))return;
        for(SpellAbility cause=card.getExiledSA();cause!=null;cause=cause.getParent()) {
            if(cause.getApi()!=ApiType.Draft)continue;
            var choices=draftPicks.get(cause);
            DraftPick pick=choices==null?null:choices.get(card.getId());
            if(pick!=null && seat>0 && pick.seat()==seat) {
                out.put("draftedFaceName",pick.name());
                out.put("draftedFacePrintingKey",pick.printing());
            }
        }
    }
    static void add(Card card,Map<String,Object> out) {
        if(card.isFaceDown())return;
        if(card.isInZone(ZoneType.Battlefield)) {
            var controller=card.getController();
            if(hasCondition(card,"Threshold")) {
                out.put("qualificationKind","threshold");out.put("qualificationCount",controller.getCardsIn(ZoneType.Graveyard).size());
                out.put("qualificationRequired",7);out.put("qualificationEarned",controller.hasThreshold());
            } else if(hasCondition(card,"Delirium")) {
                out.put("qualificationKind","delirium");out.put("qualificationCount",forge.game.ability.AbilityUtils.countCardTypesFromList(controller.getCardsIn(ZoneType.Graveyard),false));
                out.put("qualificationRequired",4);out.put("qualificationEarned",controller.hasDelirium());
            } else if(hasCondition(card,"Metalcraft")) {
                out.put("qualificationKind","metalcraft");out.put("qualificationCount",controller.getCardsIn(ZoneType.Battlefield).stream().filter(Card::isArtifact).count());
                out.put("qualificationRequired",3);out.put("qualificationEarned",controller.hasMetalcraft());
            }
            var linked=new LinkedHashSet<Integer>();
            addCurrentExiles(linked,card.getUntilLeavesBattlefield());
            addCurrentExiles(linked,card.getImprintedCards());
            // Forge records the source of ordinary one-shot exile as well. Only
            // expose that history as a pile if a rule still refers to these cards.
            if(!card.getExiledCards().isEmpty() && readsExiledWith(card))
                addCurrentExiles(linked,card.getExiledCards());
            if(!linked.isEmpty())out.put("temporaryExileIds",linked);
            boolean sacrifices=card.getSVars().values().stream()
                .anyMatch(s->s.equals("PlayerCountPropertyYou$SacrificedThisTurn Permanent"));
            boolean threshold=card.getSVars().values().stream().anyMatch(s->s.contains("ConditionSVarCompare$ GE3"));
            if(sacrifices&&threshold) {
                int count=(int)card.getController().getSacrificedThisTurn().stream().filter(Card::isPermanent).count();
                out.put("qualificationKind","sacrificedThisTurn");out.put("qualificationCount",count);
                out.put("qualificationRequired",3);out.put("qualificationEarned",count>=3);
            }
        }
        var cast=card.getCastSA();
        if(card.wasCast()&&!card.isCopiedSpell()&&cast!=null&&!cast.getPayingMana().isEmpty()) {
            StringBuilder symbols=new StringBuilder();
            for(var mana:cast.getPayingMana())symbols.append('{').append(forge.card.MagicColor.toShortString(mana.getColor())).append('}');
            out.put("spentMana",symbols.toString());
        }
    }
    private static void addCurrentExiles(LinkedHashSet<Integer> linked,Iterable<Card> cards) {
        for(Card entry:cards) {
            var current=entry.getOwner().getCardsIn(ZoneType.Exile).get(entry);
            if(current!=null && current.equalsWithGameTimestamp(entry))linked.add(current.getId());
        }
    }
    private static boolean readsExiledWith(Card source) {
        if(source.getSVars().values().stream().anyMatch(ForgePublicCardDetails::referencesSourceExile))return true;
        return java.util.stream.Stream.of(source.getSpellAbilities().stream(),source.getTriggers().stream(),
                source.getStaticAbilities().stream(),source.getReplacementEffects().stream())
            .flatMap(stream->stream).anyMatch(trait->trait.getMapParams().values().stream()
                .anyMatch(ForgePublicCardDetails::referencesSourceExile));
    }
    private static boolean referencesSourceExile(String value) {
        // ExiledWithEffectSource belongs to a separate continuous effect.
        return value.replace("ExiledWithEffectSource","").contains("ExiledWith");
    }
    private static boolean hasCondition(Card card,String condition) {
        return card.getStaticAbilities().stream().anyMatch(a->condition.equals(a.getParam("Condition")))
            ||card.getTriggers().stream().anyMatch(a->condition.equals(a.getParam("Condition")))
            ||card.getSpellAbilities().stream().anyMatch(a->condition.equals(a.getParam("Condition"))||"True".equals(a.getParam("Activation"+condition)));
    }
}
