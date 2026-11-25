package mtgb.bridge;

import forge.ai.ComputerUtilCombat;
import forge.card.mana.ManaCost;
import forge.card.mana.ManaCostShard;
import forge.deck.Deck;
import forge.game.ability.ApiType;
import forge.game.card.Card;
import forge.game.card.CardCollection;
import forge.game.card.CardCollectionView;
import forge.game.cost.CostTap;
import forge.game.cost.CostPartMana;
import forge.game.keyword.Keyword;
import forge.game.mana.ManaCostBeingPaid;
import forge.game.phase.PhaseType;
import forge.game.player.Player;
import forge.game.spellability.SpellAbility;
import forge.game.trigger.TriggerType;
import forge.game.zone.ZoneType;
import java.util.*;

/** Bounded preferences layered on Forge's legal-action and card-specific AI.
 * Inputs are the registered deck, our hand and public battlefield; never library order
 * or an opponent's hand. Scores rank choices, they do not grant permission to play.
 */
final class BridgeAiStrategy {
    enum Plan { AGGRO, MIDRANGE, CONTROL, BIG_MANA }
    private final Player player;
    private Deck analyzedDeck;
    private Plan plan = Plan.MIDRANGE;
    private double landRatio;

    BridgeAiStrategy(Player player) { this.player = player; }

    Plan plan() {
        // Controlling another player's turn reveals their hand, not their registered
        // decklist. Mind-slave controllers must not classify that hidden deck.
        if (!(player.getOriginalLobbyPlayer() instanceof ForgeBridge.BridgeAiPlayer)) {
            landRatio=0; return Plan.MIDRANGE;
        }
        Deck deck = player.getRegisteredPlayer().getDeck();
        if (deck != analyzedDeck) {
            analyzedDeck = deck;
            int total=0, lands=0, spells=0, cheapCreatures=0, creatures=0, reactive=0, mana=0;
            for (var entry : deck.getMain()) {
                var rules=entry.getKey().getRules(); int n=entry.getValue(); total+=n;
                if (rules.getType().isLand()) { lands+=n; continue; }
                spells+=n; int cost=rules.getManaCost().getCMC(); mana+=cost*n;
                if (rules.getType().isCreature()) {
                    creatures+=n; if (cost<=2) cheapCreatures+=n;
                }
                if (rules.getType().isInstant() || rules.getType().isSorcery()) reactive+=n;
            }
            landRatio=total==0?0:(double)lands/total;
            double average=spells==0?0:(double)mana/spells;
            plan=spells>0 && cheapCreatures>=spells*.45 && average<=2.6 ? Plan.AGGRO
                : spells>0 && creatures<=spells*.30 && reactive>=spells*.55 ? Plan.CONTROL
                : average>=3.8 ? Plan.BIG_MANA : Plan.MIDRANGE;
        }
        return plan;
    }

    boolean nonAggro() { return plan()!=Plan.AGGRO; }

    // London decisions must evaluate the same retained hand that will actually be kept.
    // Very unusual opening sizes and landless/land-heavy decks retain Forge's specialist AI.
    boolean handlesMulligan(CardCollectionView hand) {
        plan();
        return hand.size()<=8 && hand.size()>0 && landRatio>=.20 && landRatio<=.60
            && hand.stream().noneMatch(c -> !c.hasPlayableLandFace() && c.getManaCost().getCMC()==0
                && !c.getManaAbilities().isEmpty());
    }

    boolean keep(CardCollectionView hand, int bottom) {
        int size=hand.size()-bottom;
        if (size<=5) return true;
        CardCollection retained=new CardCollection(hand);
        retained.removeAll(bottom(hand,bottom));
        HandValue value=evaluate(retained);
        // No forced rejection for a fetchland, MDFC, restricted source or alternate cost
        // whose opening potential this deliberately small evaluator cannot establish.
        if (value.lands<2 || value.lands>4) return false;
        return value.uncertain || value.early>0;
    }

    CardCollection bottom(CardCollectionView hand, int count) {
        if (count<=0) return new CardCollection();
        int n=hand.size(), keep=Math.max(0,n-count);
        double best=-Double.MAX_VALUE; int bestMask=0;
        // At most 256 subsets (normal London hand: 128), no search over future draws.
        for (int mask=0; mask<(1<<n); mask++) {
            if (Integer.bitCount(mask)!=keep) continue;
            CardCollection candidate=new CardCollection();
            for(int i=0;i<n;i++) if((mask&(1<<i))!=0) candidate.add(hand.get(i));
            double score=evaluate(candidate).score;
            if(score>best) {best=score;bestMask=mask;}
        }
        CardCollection result=new CardCollection();
        for(int i=0;i<n;i++) if((bestMask&(1<<i))==0) result.add(hand.get(i));
        return result;
    }

    private record HandValue(int lands,int early,boolean uncertain,double score) {}
    private HandValue evaluate(CardCollectionView hand) {
        List<List<String>> sources=new ArrayList<>(); int lands=0, early=0; boolean uncertain=false;
        for(Card c:hand) if(c.hasPlayableLandFace()) {
            lands++;
            List<String> colors=new ArrayList<>();
            for(SpellAbility ma:c.getManaAbilities()) {
                String produced=ma.getParamOrDefault("Produced", "");
                if(ma.hasParam("RestrictValid") || ma.hasParam("ActivationZone")
                    || ma.hasParam("Condition") || ma.getPayCosts()==null
                    || ma.getPayCosts().getCostParts().stream().anyMatch(p -> !(p instanceof CostTap)
                        && !(p instanceof CostPartMana && ma.getPayCosts().getTotalMana().getCMC()==0))) {
                    uncertain=true; continue;
                }
                if(produced.equals("Any")) produced="W U B R G";
                if(!produced.matches("[WUBRGC]( [WUBRGC])*")) {uncertain=true;continue;}
                for(String color:produced.split(" ")) if(!colors.contains(color)) colors.add(color);
            }
            if(colors.isEmpty()) uncertain=true;
            sources.add(colors);
        }
        double quality=0;
        for(Card c:hand) if(!c.hasPlayableLandFace()) {
            ManaCost cost=c.getManaCost();
            for(ManaCostShard shard:cost) if(shard.isSnow()) uncertain=true;
            if(cost.isNoCost() || cost.getPhyrexianCount()>0 || !c.getManaAbilities().isEmpty()
                || c.hasKeyword(Keyword.EVOKE) || c.hasKeyword(Keyword.DELVE) || c.hasKeyword(Keyword.CONVOKE)
                || c.hasKeyword(Keyword.IMPROVISE) || c.hasKeyword(Keyword.AFFINITY)
                || c.getStaticAbilities().stream().anyMatch(s -> s.getParamOrDefault("Mode", "").equals("AlternativeCost"))) uncertain=true;
            boolean playable=cost.getCMC()<=Math.min(3,lands) && !cost.isNoCost()
                && payable(new ManaCostBeingPaid(cost),sources,0);
            if(playable) {early++;quality+=14;}
            if(c.isCreature() && plan()==Plan.AGGRO && playable) quality+=7;
            // Prefer keeping cheap action, but do not turn the entire deck into a curve race.
            quality+=Math.max(0,5-cost.getCMC());
        }
        int desired=plan()==Plan.AGGRO?2:plan()==Plan.BIG_MANA?4:3;
        desired=Math.min(desired,Math.max(1,hand.size()-2));
        double score=quality-12*Math.abs(lands-desired);
        if(lands<2) score-=90;
        if(lands>=hand.size()-1) score-=55;
        if(early==0&&!uncertain) score-=45;
        return new HandValue(lands,early,uncertain,score);
    }

    private boolean payable(ManaCostBeingPaid cost,List<List<String>> sources,int index) {
        if(cost.isPaid()) return true;
        if(index==sources.size()) return false;
        for(String color:sources.get(index)) {
            ManaCostBeingPaid next=new ManaCostBeingPaid(cost);
            if(next.ai_payMana(color,player.getManaPool()) && payable(next,sources,index+1)) return true;
        }
        return payable(cost,sources,index+1);
    }

    int spellPriority(SpellAbility sa) {
        var game=player.getGame(); var phase=game.getPhaseHandler(); Card card=sa.getHostCard();
        if(card==null || !game.getStack().isEmpty() || !phase.isPlayerTurn(player)
            || !(phase.is(PhaseType.MAIN1)||phase.is(PhaseType.MAIN2))
            || sa.isManaAbility() || !sa.isSpell() || sa.hasParam("AIActivateLast")
            || card.hasSVar("AIPriorityModifier") || sa.getPayCosts()==null) return 0;
        int mana=sa.getPayCosts().getTotalMana().getCMC();
        boolean behind=player.getLife()<=8 || player.getOpponents().getCreaturesInPlay().size()>player.getCreaturesInPlay().size()+1;
        // Information first when it leaves mana to act. Never promote expensive draw
        // over stabilising a losing board. Forge still decides whether to cast at all.
        if(!behind && mana<=1 && !sa.costHasManaX() && player.getCardsIn(ZoneType.Battlefield).stream()
            .filter(c -> c.isLand()&&!c.isTapped()&&!c.getManaAbilities().isEmpty()).count()>=3
            && (sa.getApi()==ApiType.Draw || sa.getApi()==ApiType.Scry)) return 8;
        if(plan()==Plan.AGGRO && card.isCreature() && mana<=2 && phase.is(PhaseType.MAIN1)) return 2;
        if(plan()==Plan.BIG_MANA && mana<=3 && card.isPermanent() && !card.getManaAbilities().isEmpty()) return 4;
        if(behind && (sa.getApi()==ApiType.Destroy || sa.getApi()==ApiType.DestroyAll || sa.getApi()==ApiType.DealDamage)) return 4;
        return 0;
    }

    int removalPriority(Card card) {
        if(!card.isInPlay() || !card.getController().isOpponentOf(player) || card.isFaceDown() || card.isLand()) return 0;
        var combat=player.getGame().getCombat();
        if(combat!=null && player.getLife()>0 && !player.cantLoseForZeroOrLessLife()
            && combat.isAttacking(card,player) && !combat.isBlocked(card)
            && ComputerUtilCombat.damageIfUnblocked(card,player,combat,false)>=player.getLife()) return 1000;
        // Use current native abilities, so humiliation/ability loss removes the bonus.
        // ETB/leaves-play triggers have already happened: do not mistake them for engines.
        int engine=0;
        for(var trigger:card.getTriggers()) {
            if(trigger.isSuppressed() || !trigger.getParamOrDefault("TriggerZones", "").equals("Battlefield")) continue;
            var mode=trigger.getMode();
            if(mode!=TriggerType.Phase && mode!=TriggerType.SpellCast && mode!=TriggerType.SpellCastOrCopy
                && mode!=TriggerType.DamageDone) continue;
            SpellAbility ability=trigger.ensureAbility();
            for(int i=0;ability!=null&&i<12;i++,ability=ability.getSubAbility()) {
                if((ability.getApi()==ApiType.Draw || (ability.getApi()==ApiType.Dig
                    && ability.getParamOrDefault("DestinationZone", "").equals("Hand")))
                    && ability.getParamOrDefault("Defined","You").equals("You")) engine=Math.max(engine,140);
                if(ability.getApi()==ApiType.Token && ability.getParamOrDefault("TokenController","You").equals("You")
                    && ability.getParamOrDefault("Defined","You").equals("You")) engine=Math.max(engine,70);
            }
        }
        return engine;
    }
}
