using System;
using System.Collections.Generic;
using UnityEngine;

public static class StateBasedActionProcessor
{
    public static bool ProcessBattlefieldStateBasedActions(
        List<RuntimeCard> player1Battlefield,
        List<RuntimeCard> player2Battlefield,
        List<RuntimeCard> player1Graveyard,
        List<RuntimeCard> player2Graveyard,
        Func<RuntimeCard, bool> hasPendingBattleDefeatAbility,
        Action<RuntimeCard, GameZone, GameZone> onMoved = null,
        Func<IReadOnlyList<RuntimeCard>, bool> moveBatchToGraveyard = null)
    {
        var battlefield = new List<RuntimeCard>();
        if (player1Battlefield != null) battlefield.AddRange(player1Battlefield);
        if (player2Battlefield != null) battlefield.AddRange(player2Battlefield);

        bool performed = false;
        var cardsToMove = new HashSet<RuntimeCard>();
        var aurasToMove = new HashSet<RuntimeCard>();
        for (int i = 0; i < battlefield.Count; i++)
        {
            RuntimeCard card = battlefield[i];
            if (card == null) continue;

            performed |= card.CancelOpposingPowerToughnessCounters();
            if (card.IsDead
                || (card.HasType(CardType.Planeswalker) && card.CurrentLoyalty <= 0)
                || (card.HasType(CardType.Battle)
                    && card.CurrentBattleDefense <= 0
                    && (hasPendingBattleDefeatAbility == null
                        || !hasPendingBattleDefeatAbility(card))))
            {
                card.Die();
                cardsToMove.Add(card);
                continue;
            }

            if (card.HasType(CardType.Creature))
            {
                if (card.CurrentDefence <= 0)
                {
                    card.Die();
                    cardsToMove.Add(card);
                    continue;
                }

                if (card.DamageTaken >= card.CurrentDefence
                    || card.HasDeathtouchDamageSinceStateCheck)
                {
                    int shieldsBefore = card.RegenerationShields;
                    int damageBefore = card.DamageTaken;
                    bool tappedBefore = card.IsTapped;
                    if (card.Destroy(CanRegenerate(card)))
                    {
                        cardsToMove.Add(card);
                        continue;
                    }

                    performed |= card.RegenerationShields < shieldsBefore
                        || card.DamageTaken != damageBefore
                        || card.IsTapped != tappedBefore;
                }
            }

            RuntimeCard target = card.AttachedTo;
            bool isAura = card.HasSubtype("Aura");
            if (isAura && target == null)
            {
                aurasToMove.Add(card);
                continue;
            }

            if (target == null) continue;

            bool legalAttachment = battlefield.Contains(target)
                && TargetingRules.CanAttachToCard(card, target);
            if (legalAttachment) continue;

            if (isAura)
            {
                aurasToMove.Add(card);
            }
            else
            {
                performed |= card.Detach();
            }
        }

        foreach (RuntimeCard aura in aurasToMove)
        {
            cardsToMove.Add(aura);
        }

        if (moveBatchToGraveyard != null && cardsToMove.Count > 0)
        {
            var batch = new List<RuntimeCard>(cardsToMove);
            batch.Sort((left, right) =>
                (left?.ObjectId ?? 0).CompareTo(right?.ObjectId ?? 0));
            return moveBatchToGraveyard(batch) || performed;
        }

        // CR 704.3: all applicable state-based actions in one check happen
        // simultaneously. Determine the complete batch before changing a zone.
        foreach (RuntimeCard card in cardsToMove)
        {
            if (card == null) continue;
            List<RuntimeCard> controllerBattlefield = card.ControllerPlayerID == 1
                ? player1Battlefield
                : player2Battlefield;
            List<RuntimeCard> ownerGraveyard = card.OwnerPlayerID == 1
                ? player1Graveyard
                : player2Graveyard;
            if (controllerBattlefield == null
                || ownerGraveyard == null
                || !controllerBattlefield.Remove(card))
            {
                continue;
            }

            Debug.Log(card.HasType(CardType.Creature)
                ? $"{card.Data.cardName} dies!"
                : $"{card.Data.cardName} is put into its owner's graveyard.");
            card.MarkZoneChange();
            card.SetCurrentZone(GameZone.Graveyard);
            ownerGraveyard.Add(card);
            onMoved?.Invoke(card, GameZone.Battlefield, GameZone.Graveyard);
            performed = true;
        }

        return performed;
    }

    public static bool ProcessCreatureStateBasedActions(
        List<RuntimeCard> battlefield,
        List<RuntimeCard> graveyard,
        Action<RuntimeCard, GameZone, GameZone> onMoved = null)
    {
        bool performed = false;
        var cardsToMove = new List<RuntimeCard>();
        for (int i = battlefield.Count - 1; i >= 0; i--)
        {
            RuntimeCard card = battlefield[i];
            if (card == null) continue;

            if (card.CancelOpposingPowerToughnessCounters())
            {
                performed = true;
            }

            if (card.IsDead)
            {
                cardsToMove.Add(card);
                performed = true;
                continue;
            }

            if (card.HasType(CardType.Planeswalker) && card.CurrentLoyalty <= 0)
            {
                card.Die();
                cardsToMove.Add(card);
                performed = true;
                continue;
            }

            if (!card.HasType(CardType.Creature)) continue;

            if (card.CurrentDefence <= 0)
            {
                card.Die();
                cardsToMove.Add(card);
                performed = true;
                continue;
            }

            if (card.DamageTaken >= card.CurrentDefence || card.HasDeathtouchDamageSinceStateCheck)
            {
                int shieldsBefore = card.RegenerationShields;
                int damageBefore = card.DamageTaken;
                bool tappedBefore = card.IsTapped;
                bool destroyed = card.Destroy(CanRegenerate(card));
                if (destroyed)
                {
                    cardsToMove.Add(card);
                    performed = true;
                    continue;
                }

                bool regenerated = card.RegenerationShields < shieldsBefore
                    || card.DamageTaken != damageBefore
                    || card.IsTapped != tappedBefore;
                if (regenerated) performed = true;
            }
        }

        // State-based actions happen simultaneously. Mark every dying creature first,
        // then move the whole batch so death triggers can observe the other dying cards.
        for (int i = 0; i < cardsToMove.Count; i++)
        {
            RuntimeCard card = cardsToMove[i];
            int battlefieldIndex = battlefield.IndexOf(card);
            if (battlefieldIndex >= 0)
            {
                MoveToGraveyard(card, battlefield, graveyard, battlefieldIndex);
                onMoved?.Invoke(card, GameZone.Battlefield, GameZone.Graveyard);
            }
        }

        return performed;
    }

    public static bool ProcessAttachmentStateBasedActions(
        List<RuntimeCard> player1Battlefield,
        List<RuntimeCard> player2Battlefield,
        List<RuntimeCard> player1Graveyard,
        List<RuntimeCard> player2Graveyard,
        Action<RuntimeCard, GameZone, GameZone> onMoved = null)
    {
        var battlefield = new List<RuntimeCard>();
        if (player1Battlefield != null) battlefield.AddRange(player1Battlefield);
        if (player2Battlefield != null) battlefield.AddRange(player2Battlefield);

        bool performed = false;
        var aurasToMove = new List<RuntimeCard>();
        for (int i = 0; i < battlefield.Count; i++)
        {
            RuntimeCard attachment = battlefield[i];
            if (attachment == null || attachment.IsDead) continue;

            bool isAura = attachment.HasSubtype("Aura");
            bool isEquipment = attachment.HasSubtype("Equipment");
            bool isFortification = attachment.HasSubtype("Fortification");
            if (!isAura && !isEquipment && !isFortification) continue;

            RuntimeCard target = attachment.AttachedTo;
            bool targetOnBattlefield = target != null && battlefield.Contains(target);
            bool legalAttachment = targetOnBattlefield
                && TargetingRules.CanAttachToCard(attachment, target);
            if (legalAttachment) continue;

            if (isAura)
            {
                aurasToMove.Add(attachment);
            }
            else if (attachment.Detach())
            {
                performed = true;
            }
        }

        for (int i = 0; i < aurasToMove.Count; i++)
        {
            RuntimeCard aura = aurasToMove[i];
            List<RuntimeCard> controllerBattlefield = aura.ControllerPlayerID == 1
                ? player1Battlefield
                : player2Battlefield;
            List<RuntimeCard> ownerGraveyard = aura.OwnerPlayerID == 1
                ? player1Graveyard
                : player2Graveyard;
            if (controllerBattlefield == null
                || ownerGraveyard == null
                || !controllerBattlefield.Remove(aura))
            {
                continue;
            }

            aura.MarkZoneChange();
            ownerGraveyard.Add(aura);
            onMoved?.Invoke(aura, GameZone.Battlefield, GameZone.Graveyard);
            performed = true;
            Debug.Log($"{aura.Data.cardName} is put into its owner's graveyard because it is not legally attached.");
        }

        return performed;
    }

    private static void MoveToGraveyard(RuntimeCard card, List<RuntimeCard> battlefield, List<RuntimeCard> graveyard, int battlefieldIndex)
    {
        Debug.Log(card.HasType(CardType.Creature)
            ? $"{card.Data.cardName} dies!"
            : $"{card.Data.cardName} is put into its owner's graveyard.");
        card.MarkZoneChange();
        graveyard.Add(card);
        battlefield.RemoveAt(battlefieldIndex);
    }

    private static bool CanRegenerate(RuntimeCard card)
    {
        return card != null
            && !card.HasKeyword("CantBeRegenerated")
            && !card.HasKeyword("CannotRegenerate")
            && !card.HasKeyword("Can't Be Regenerated")
            && !card.HasKeyword("CanNotBeRegenerated");
    }
}
