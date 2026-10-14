"""Maintain guarded native fixes; no upstream/card script edits."""
import json
from pathlib import Path
path=Path(__file__).with_name('NativePatches.json');data=json.loads(path.read_text(encoding='utf8'))
mana=next(p for p in data['patches'] if p['id']=='phyrexian-minimum-mana-payment')
before='''                            sc.totalCount -= shardAmount;
                            if (sc.xCount > sc.totalCount) {
                                sc.xCount = sc.totalCount;
                            }
                            // nothing more left in otherSubtract
                            break;'''
after='''                            sc.totalCount -= shardAmount;
                            otherSubtract -= shardAmount * 2;
                            // CR 601.2b/f: choosing the generic half allows an odd
                            // reduction too. One remaining 2/color becomes {1}.
                            // The old integer division silently discarded this unit.
                            if (otherSubtract > 0 && sc.totalCount > 0) {
                                sc.totalCount--;
                                increaseGenericMana(1);
                                otherSubtract--;
                            }
                            if (sc.xCount > sc.totalCount) {
                                sc.xCount = sc.totalCount;
                            }
                            if (sc.totalCount == 0) toRemove.add(eShard);
                            break;'''
if not any(c['before']==before for c in mana['changes']):mana['changes'].append(dict(before=before,after=after))
before_floor='''        this.sunburstMap |= colorMask;'''
after_floor='''        // Choosing the colored half of a two-brid reduces its announced
        // mana total by one. Preserve the original minimum independently of
        // earlier mana payments, just as for a Phyrexian life choice.
        if (chosenShard.isOr2Generic() && (chosenShard.getColorMask() & possibleUses) != 0
                && phyrexianMinimumHeadroom != null) {
            if (phyrexianMinimumHeadroom > 0) phyrexianMinimumHeadroom--;
            else increaseGenericMana(1);
        }
        this.sunburstMap |= colorMask;'''
if not any(c['before']==before_floor for c in mana['changes']):mana['changes'].append(dict(before=before_floor,after=after_floor))
floor=next(p for p in data['patches'] if p['id']=='phyrexian-minimum-cost-adjustment')
if 's.isOr2Generic()' not in floor['changes'][0]['after']:
    floor['changes'][0]['after']=floor['changes'][0]['after'].replace('manaCost.containsPhyrexianMana()', '(manaCost.containsPhyrexianMana() || java.util.stream.StreamSupport.stream(manaCost.toManaCost().spliterator(), false).anyMatch(s -> s.isOr2Generic()))')
play=next(p for p in data['patches'] if p['id']=='same-graveyard-cost-cancellation')
payment=next(c for c in play['changes'] if c['before'].startswith('        CostAdjustment.adjust(toPay'))
payment['after']='''        // CR 601.2b: life choices precede the total and nonmana payment.
        // For tap/delve payment, deciding life afterwards can create a new
        // generic debt after all eligible resources have already been selected.
        int earlyPhyLife = 0;
        ability.setSVar("MTGBEarlyPhyLife", "0");
        ability.setSVar("MTGBNonmanaTapped", "");
        if (!effect && ability.isSpell() && toPay.containsPhyrexianMana()
                && (source.hasKeyword(forge.game.keyword.Keyword.CONVOKE)
                    || source.hasKeyword(forge.game.keyword.Keyword.IMPROVISE)
                    || source.hasKeyword(forge.game.keyword.Keyword.DELVE))) {
            int maximum = 0;
            for (var shard : toPay.toManaCost()) if (shard.isPhyrexian()) maximum++;
            while (maximum > 0 && !activator.canPayLife(maximum * 2, false, ability)) maximum--;
            int count = maximum == 0 ? 0 : controller.chooseNumber(ability,
                    "Choose Phyrexian symbols to pay with life before nonmana payment", 0, maximum);
            for (int i=0; i<count; i++) { toPay.payPhyrexian(); earlyPhyLife += 2; }
            if (earlyPhyLife > 0) ability.setSpendPhyrexianMana(true);
            ability.setSVar("MTGBEarlyPhyLife", Integer.toString(earlyPhyLife));
        }
        CostAdjustment.adjust(toPay, ability, activator, cardsToDelve, false, effect);
        if (!effect && ability.isSpell()) {
            ability.setSVar("MTGBCastingManaCost", toPay.isPaid() ? "{0}" : toPay.toManaCost().toString());
        }
        // Defer life until cancellation checks and native payment are complete.'''
commit_before='''        return handleOfferingConvokeAndDelve(ability, cardsToDelve, false);'''
commit_after='''        boolean paid = handleOfferingConvokeAndDelve(ability, cardsToDelve, false);
        // No InputPayMana.done callback exists when tap/delve paid everything.
        // Commit the reservation only after all native cancellation boundaries.
        if (paid && toPay.isPaid() && earlyPhyLife > 0
                && !ability.getSVar("MTGBEarlyPhyLife").equals("committed"))
            activator.payLife(earlyPhyLife, ability, false);
        return paid;'''
if not any(c['before']==commit_before for c in play['changes']):play['changes'].append(dict(before=commit_before,after=commit_after))
tap_before='''                if (c.tap(true, sa, payer)) tapped.add(c);'''
tap_after='''                if (c.tap(true, sa, payer)) {
                    tapped.add(c);
                    sa.setSVar("MTGBNonmanaTapped", sa.getSVar("MTGBNonmanaTapped") + c.getId() + ",");
                }'''
if not any(c['before']==tap_before for c in floor['changes']):floor['changes'].append(dict(before=tap_before,after=tap_after))
undo_before='''        final Card hostCard = ability.getHostCard();'''
undo_after='''        final Card hostCard = ability.getHostCard();
        if (manaInputCancelled) {
            String ids = "," + ability.getSVar("MTGBNonmanaTapped");
            for (Card c : hostCard.getGame().getCardsIn(forge.game.zone.ZoneType.Battlefield))
                if (ids.contains("," + c.getId() + ",")) c.setTapped(false);
            ability.clearTappedForConvoke();
        }
        ability.setSVar("MTGBNonmanaTapped", "");'''
if not any(c['before']==undo_before for c in play['changes']):play['changes'].append(dict(before=undo_before,after=undo_after))
source=Path('.tools/forge-proof/upstream/forge-gui/src/main/java/forge/gamemodes/match/input/InputPayManaOfCostPayment.java')
import hashlib
if not any(p['id']=='early-phyrexian-input-reservation' for p in data['patches']):
    data['patches'].append(dict(id='early-phyrexian-input-reservation',source=source.as_posix(),sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),javaPath='forge/gamemodes/match/input/InputPayManaOfCostPayment.java',reason='Carry native early Phyrexian reservations into input life affordability, cancel and commit; actual costs remain native.',changes=[dict(before='''        manaCost = cost;''',after='''        manaCost = cost;
        String reserved = spellAbility.getSVar("MTGBEarlyPhyLife");
        phyLifeToLose = reserved.isEmpty() ? 0 : Integer.parseInt(reserved);''')]))
avail=next(p for p in data['patches'] if p['id']=='human-phyrexian-affordability')
c=next(c for c in avail['changes'] if c['before'].startswith('        return ComputerUtilMana'))
c['after']='''        // AI-only spending preferences must not veto human decisions.
        SpellAbility intent = sa.copy(player);
        intent.removeParam("AIPhyrexianPayment");
        intent.putParam("MTGBHumanAffordability", "True");
        if (ComputerUtilMana.canPayManaCost(intent, player, 0, false)) return true;
        // Optional colored reductions (e.g. Defilers) are part of the announced
        // casting intent. Test their native added costs and native adjusted mana.
        var reductions = forge.game.GameActionUtil.getOptionalCostValues(intent).stream()
                .filter(v -> v.getType().name().startsWith("Reduce")).collect(java.util.stream.Collectors.toList());
        for (var reduction : reductions) {
            SpellAbility reduced = forge.game.GameActionUtil.addOptionalCosts(intent, List.of(reduction));
            if (reduced.getPayCosts().canPay(reduced, player, false)
                    && ComputerUtilMana.canPayManaCost(reduced, player, 0, false)) return true;
        }
        return false;'''
inp=next(p for p in data['patches'] if p['id']=='early-phyrexian-input-reservation')
done_before='''            player.payLife(this.phyLifeToLose, saPaidFor, this.effect);'''
if not any(c['before']==done_before for c in inp['changes']):
    inp['changes'].append(dict(before=done_before,after=done_before+'\n            saPaidFor.setSVar("MTGBEarlyPhyLife", "committed");'))
computer=Path('.tools/forge-proof/upstream/forge-ai/src/main/java/forge/ai/ComputerUtilMana.java')
if not any(p['id']=='human-life-payment-estimate' for p in data['patches']):
    data['patches'].append(dict(id='human-life-payment-estimate',source=computer.as_posix(),sourceSha256=hashlib.sha256(computer.read_bytes()).hexdigest(),javaPath='forge/ai/ComputerUtilMana.java',reason='Human native affordability may pay the last life, and may pay an all-black cost with Krrik without mana sources. AI spending strategy stays unchanged.',changes=[
        dict(before='''        boolean purePhyrexian = cost.containsOnlyPhyrexianMana();''',after='''        boolean purePhyrexian = cost.containsOnlyPhyrexianMana()
                || (sa.hasParam("MTGBHumanAffordability") && ai.hasKeyword("PayLifeInsteadOf:B")
                    && java.util.stream.StreamSupport.stream(cost.toManaCost().spliterator(), false)
                        .allMatch(s -> s == ManaCostShard.BLACK));'''),
        dict(before='''                        || (ai.getLife() <= phyLifeToPay && !ai.cantLoseForZeroOrLessLife())) {''',after='''                        || (!sa.hasParam("MTGBHumanAffordability")
                            && ai.getLife() <= phyLifeToPay && !ai.cantLoseForZeroOrLessLife())) {'''),
        dict(before='''        shardsToPay.sort(Comparator.comparingInt(shard -> sourcesForShards.get(shard).size()));''',after='''        if (sourcesForShards != null)
            shardsToPay.sort(Comparator.comparingInt(shard -> sourcesForShards.get(shard).size()));''')]))
estimate=next(p for p in data['patches'] if p['id']=='human-life-payment-estimate')
estimate['changes'][0]['after']=estimate['changes'][0]['after'].replace('.allMatch(s -> s.isBlack())', '.allMatch(s -> s == ManaCostShard.BLACK)')
null_before='''        shardsToPay.sort(Comparator.comparingInt(shard -> sourcesForShards.get(shard).size()));'''
if not any(c['before']==null_before for c in estimate['changes']):estimate['changes'].append(dict(before=null_before,after='''        if (sourcesForShards != null)
            shardsToPay.sort(Comparator.comparingInt(shard -> sourcesForShards.get(shard).size()));'''))
preview=Path('.tools/forge-proof/upstream/forge-gui/src/main/java/forge/gamemodes/match/input/InputPayMana.java')
if not any(p['id']=='human-auto-preview-preserves-intent' for p in data['patches']):
    data['patches'].append(dict(id='human-auto-preview-preserves-intent',source=preview.as_posix(),sourceSha256=hashlib.sha256(preview.read_bytes()).hexdigest(),javaPath='forge/gamemodes/match/input/InputPayMana.java',reason='The native Auto preview resets Offering/Emerge sacrifice reservations. Preview a copied intent and restore shared usedToPay markers so actual Auto and cancellation retain the human choice.',changes=[dict(before='''                return ComputerUtilMana.getManaSourcesToPayCost(costCopy, saPaidFor, player, effect);''',after='''                Card offering = saPaidFor.getSacrificedAsOffering();
                Card emerge = saPaidFor.getSacrificedAsEmerge();
                boolean offeringUsed = offering != null && offering.isUsedToPay();
                boolean emergeUsed = emerge != null && emerge.isUsedToPay();
                SpellAbility preview = saPaidFor.copy(player);
                try {
                    return ComputerUtilMana.getManaSourcesToPayCost(costCopy, preview, player, effect);
                } finally {
                    if (offering != null) offering.setUsedToPay(offeringUsed);
                    if (emerge != null) emerge.setUsedToPay(emergeUsed);
                }''')]))
path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
