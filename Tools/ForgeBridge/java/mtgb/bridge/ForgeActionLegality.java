package mtgb.bridge;

import forge.ai.AvailableActions;
import forge.ai.PlayerControllerAi;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.spellability.SpellAbility;
import forge.game.zone.ZoneType;

/** Read-only proposals: native rules, targets and costs must agree on the same action. */
final class ForgeActionLegality {
    record Prediction(boolean selectable,boolean payable,boolean response) {}

    static boolean permits(SpellAbility ability,Player player) {
        if(ability.isTrigger())return true;
        Card host=ability.getHostCard();
        if(ability.isSpell())host=java.util.Objects.requireNonNullElse(ability.getAlternateHost(host),host);
        // Native spell restrictions inspect both cast origin and the announced face/X.
        // checkRestrictions sets castSA, so always restore prediction scratch state.
        var castFrom=host.getCastFrom();var castSA=host.getCastSA();
        try {
            if(ability.isSpell()&&!host.isInZone(ZoneType.Stack))host.setCastFrom(ability.getHostCard().getZone());
            return ability.checkRestrictions(host,player);
        } finally {host.setCastFrom(castFrom);host.setCastSA(castSA);}
    }

    static Prediction predict(Card card,Player player) {
        boolean[] result=new boolean[3];
        var abilities=card.getAllPossibleAbilities(player,true);
        player.runWithController(()->{
            for(var ability:abilities) {
                if(!permits(ability,player)||!hasLegalTargets(ability))continue;
                boolean payable=AvailableActions.canAffordHuman(ability,player)
                    ||ability.isSpell()&&ForgeCardCostPreview.canPayMinimum(ability,player);
                boolean deferred=ability.isSpell()&&(card.hasKeyword(forge.game.keyword.Keyword.CONVOKE)
                    ||card.hasKeyword(forge.game.keyword.Keyword.IMPROVISE)
                    ||ForgeBridge.hasNativeCastingPaymentChoice(card,player));
                if(!payable&&!deferred)continue;
                result[0]=true;result[1]|=payable;result[2]|=payable&&!ability.isManaAbility();
            }
        },new PlayerControllerAi(ForgeBridge.game,player,player.getOriginalLobbyPlayer()));
        return new Prediction(result[0],result[1],result[2]);
    }

    static boolean hasLegalTargets(SpellAbility ability) {
        for(SpellAbility part=ability;part!=null;part=part.getSubAbility()) {
            if(!part.usesTargeting())continue;
            Player previous=part.getTargetingPlayer();
            // setupTargets assigns this immediately before the real chooser.
            // Predict each permitted chooser without prompting or committing one;
            // TargetingPlayerControls otherwise rejects every card against null.
            Iterable<Player> choosers=previous!=null?java.util.List.of(previous)
                :part.hasParam("TargetingPlayer")
                    ?forge.game.ability.AbilityUtils.getDefinedPlayers(part.getHostCard(),part.getParam("TargetingPlayer"),part)
                    :java.util.Collections.singletonList(part.getActivatingPlayer());
            boolean possible=false;
            try {
                for(Player chooser:choosers) {
                    if(chooser==null||!chooser.isInGame())continue;
                    part.setTargetingPlayer(chooser);
                    if(part.getTargetRestrictions().getNumCandidates(part)>=part.getMinTargets()) {possible=true;break;}
                }
            } finally {part.setTargetingPlayer(previous);}
            if(!possible)return false;
        }
        return true;
    }
}
