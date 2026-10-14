package mtgb.bridge;

import forge.game.card.Card;
import forge.game.cost.CostAdjustment;
import forge.game.mana.ManaCostBeingPaid;
import forge.game.zone.ZoneType;

/** Bounded component-face pilot. No optional costs, targets, X or payment choices. */
final class ForgeCardCostPreview {
    static String forCard(Card card) {
        if(card.isFaceDown() || !pilot(card)
            || !(card.isInZone(ZoneType.Hand)||card.isInZone(ZoneType.Graveyard)
                ||card.isInZone(ZoneType.Exile)||card.isInZone(ZoneType.Library)))return null;
        var original=card.getSpellAbilities().stream().filter(a->a.isSpell()&&!a.isAdventure()).findFirst().orElse(null);
        if(original==null)return null;
        return forAbility(original);
    }
    static boolean pilot(Card card) {
        return java.util.Set.of("Grizzly Bears","Elvish Visionary","Bonecrusher Giant").contains(card.getState(forge.card.CardStateName.Original).getName());
    }
    static String adventure(Card card) {
        if(!pilot(card)||card.isFaceDown()||card.isInZone(ZoneType.Stack))return null;
        var spell=card.getAllSpellAbilities().stream().filter(a->a.isAdventure()).findFirst().orElse(null);
        return spell==null?null:forAbility(spell);
    }
    static String forAbility(forge.game.spellability.SpellAbility original) {
        Card card=original.getHostCard();if(!pilot(card)||card.isFaceDown())return null;
        var intent=original.copy();var payer=card.getOwner();intent.setActivatingPlayer(payer);
        String[] preview={null};
        // Native test adjustment on a copied ability, with a noninteractive
        // controller for static reduction ordering. No live input is consumed.
        payer.runWithController(()->{
            var raised=CostAdjustment.adjust(intent.getPayCosts().copy(),intent,false);
            var cost=new ManaCostBeingPaid(raised.getCostMana().getManaCostFor(intent));
            CostAdjustment.adjust(cost,intent,payer,new forge.game.card.CardCollection(),true,false);
            preview[0]=cost.isPaid()?"{0}":cost.toManaCost().toString();
        },new forge.ai.PlayerControllerAi(ForgeBridge.game,payer,payer.getOriginalLobbyPlayer()));
        return preview[0];
    }
}
